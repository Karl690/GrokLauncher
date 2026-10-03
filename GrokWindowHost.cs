using System.Diagnostics;

namespace GrokLauncher;

internal sealed class GrokWindowHost : Panel
{
    readonly GrokLaunch _launch;
    readonly Label _statusLabel;
    readonly System.Windows.Forms.Timer _captureTimer = new();
    readonly System.Windows.Forms.Timer _guestWatchTimer = new();
    int _captureAttempts;
    GuestPlacement _original;
    bool _released;
    bool _closedRaised;

    const int CaptureIntervalMs = 50;
    const int MaxCaptureAttempts = 200; /* 10 seconds */

    public event Action<GrokWindowHost>? SessionClosed;

    public GrokWindowHost(GrokLaunch launch, TabPage tabPage)
    {
        _launch = launch;
        TabPage = tabPage;
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Name = "grokWindowHost";
        TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);

        _statusLabel = new Label
        {
            Name = "lblCaptureStatus",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            BackColor = Color.Black,
            Font = UiStyle.Text,
            Text = "Starting Grok Build..."
        };
        Controls.Add(_statusLabel);

        _captureTimer.Interval = CaptureIntervalMs;
        _captureTimer.Tick += captureTimer_Tick;

        _guestWatchTimer.Interval = 500;
        _guestWatchTimer.Tick += guestWatchTimer_Tick;

        try
        {
            launch.HostProcess.EnableRaisingEvents = true;
            launch.HostProcess.Exited += hostProcess_Exited;
        }
        catch (Exception)
        {
            /* ShellExecute host may not expose wait handles; guest watch still closes the tab */
        }
    }

    public TabPage TabPage { get; }

    public string FolderPath => _launch.FolderPath;

    public IntPtr GuestHandle { get; private set; }

    public bool IsAlive
    {
        get
        {
            try
            {
                return !_launch.HostProcess.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void BeginCapture()
    {
        if (IsHandleCreated) _captureTimer.Start();
        else HandleCreated += (_, _) => _captureTimer.Start();
    }

    public void FitToHost()
    {
        if (GuestHandle == IntPtr.Zero) return;
        if (!IsHandleCreated) return;
        if (ClientSize.Width < 16 || ClientSize.Height < 16) return;
        GuestWindow.MoveTo(GuestHandle, 0, 0, ClientSize.Width, ClientSize.Height);
    }

    public void SetGuestVisible(bool visible)
    {
        if (GuestHandle == IntPtr.Zero) return;
        GuestWindow.SetVisible(GuestHandle, visible);
        if (visible)
        {
            FitToHost();
            FocusGuest();
        }
    }

    public void FocusGuest()
    {
        if (GuestHandle == IntPtr.Zero) return;
        if (!GuestWindow.IsAlive(GuestHandle)) return;
        GuestWindow.Focus(GuestHandle);
    }

    public void ReleaseToDesktop()
    {
        _captureTimer.Stop();
        _guestWatchTimer.Stop();
        if (_released) return;
        _released = true;
        if (GuestHandle != IntPtr.Zero) GuestWindow.ReleaseToDesktop(_original);
        GuestHandle = IntPtr.Zero;
    }

    public void CloseCapturedSession()
    {
        _captureTimer.Stop();
        _guestWatchTimer.Stop();
        if (_released) return;
        _released = true;
        IntPtr guest = GuestHandle;
        GuestHandle = IntPtr.Zero;
        GuestWindow.CloseAndKill(guest, _launch.HostProcess);
        GuestWindow.DeleteConsoleTitleProperties(_launch.TitleToken);
    }

    protected override void OnResize(EventArgs eventArgs)
    {
        base.OnResize(eventArgs);
        FitToHost();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _captureTimer.Stop();
            _captureTimer.Dispose();
            _guestWatchTimer.Stop();
            _guestWatchTimer.Dispose();
            if (!_released) CloseCapturedSession();
        }

        base.Dispose(disposing);
    }

    void captureTimer_Tick(object? sender, EventArgs eventArgs)
    {
        IntPtr found = GuestWindow.FindConsole(_launch.HostProcess, _launch.TitleToken);
        if (found != IntPtr.Zero)
        {
            _captureTimer.Stop();
            Attach(found);
            return;
        }

        _captureAttempts++;
        if (_captureAttempts < MaxCaptureAttempts) return;

        _captureTimer.Stop();
        _statusLabel.Text = "Could not capture the Grok Build window. It may be open on the desktop.";
    }

    void Attach(IntPtr guestHandle)
    {
        if (!IsHandleCreated) return;
        GuestWindow.SetConsolePointFont(guestHandle, _launch.FontPoints);
        if (!GuestWindow.TryEmbed(guestHandle, Handle, out _original))
        {
            _statusLabel.Text = "Could not attach the Grok Build window to this tab.";
            return;
        }

        GuestHandle = guestHandle;
        _statusLabel.Visible = false;
        ApplyCapturedFont(); /* once at attach; do not AttachConsole again later */
        _guestWatchTimer.Start();
        bool selected = TabPage.Parent is TabControl tabs && tabs.SelectedTab == TabPage;
        if (selected)
        {
            FitToHost();
            FocusGuest();
        }
        else
        {
            GuestWindow.SetVisible(guestHandle, false);
        }
    }

    void guestWatchTimer_Tick(object? sender, EventArgs eventArgs)
    {
        if (GuestHandle == IntPtr.Zero) return;
        if (!GuestWindow.IsAlive(GuestHandle))
        {
            BeginClose();
            return;
        }
    }

    protected override void OnGotFocus(EventArgs eventArgs)
    {
        base.OnGotFocus(eventArgs);
        FocusGuest();
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);
        FocusGuest();
    }

    void ApplyCapturedFont()
    {
        if (GuestHandle == IntPtr.Zero) return;
        GuestWindow.SetConsolePointFont(GuestHandle, _launch.FontPoints);
    }

    void hostProcess_Exited(object? sender, EventArgs eventArgs)
    {
        if (GuestWindow.IsAlive(GuestHandle)) return; /* console still hosted after the stub process */
        if (_captureTimer.Enabled) return; /* still looking for the window */
        BeginClose();
    }

    void BeginClose()
    {
        if (_closedRaised) return;
        if (IsDisposed)
        {
            RaiseClosed();
            return;
        }

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(BeginClose);
                return;
            }
        }
        catch (Exception)
        {
            RaiseClosed();
            return;
        }

        _captureTimer.Stop();
        GuestHandle = IntPtr.Zero;
        RaiseClosed();
    }

    void RaiseClosed()
    {
        if (_closedRaised) return;
        _closedRaised = true;
        SessionClosed?.Invoke(this);
    }
}
