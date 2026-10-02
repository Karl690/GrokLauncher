using System.Diagnostics;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace GrokLauncher;

internal struct GuestPlacement
{
    public IntPtr Handle;
    public IntPtr Style;
    public IntPtr ExStyle;
    public int Left;
    public int Top;
    public int Width;
    public int Height;
}

internal static class GuestWindow
{
    const int GwlStyle = -16;
    const int GwlExStyle = -20;
    const int WsCaption = 0x00C00000;
    const int WsThickFrame = 0x00040000;
    const int WsMinimizeBox = 0x00020000;
    const int WsMaximizeBox = 0x00010000;
    const int WsSysMenu = 0x00080000;
    const int WsPopup = unchecked((int)0x80000000);
    const int WsChild = 0x40000000;
    const int WsVisible = 0x10000000;
    const int SwHide = 0;
    const int SwShow = 5;
    const int WmClose = 0x0010;
    const int SwpNoZOrder = 0x0004;
    const int SwpNoMove = 0x0002;
    const int SwpNoSize = 0x0001;
    const int SwpFrameChanged = 0x0020;
    const uint Th32CsSnapProcess = 0x00000002;
    const int StdOutputHandle = -11;
    const uint GenericRead = 0x80000000;
    const uint GenericWrite = 0x40000000;
    const uint FileShareRead = 0x00000001;
    const uint FileShareWrite = 0x00000002;
    const uint OpenExisting = 3;
    const int FontFamilyTrueTypeModern = 54;
    const uint InputKeyboard = 1;
    const uint KeyeventfKeyup = 0x0002;
    const ushort VkControl = 0x11;
    const ushort VkOemPlus = 0xBB;

    public static bool IsAlive(IntPtr windowHandle)
    {
        return windowHandle != IntPtr.Zero && IsWindow(windowHandle);
    }

    public static IntPtr FindConsole(Process hostProcess, string titleToken)
    {
        if (hostProcess is null) return IntPtr.Zero;
        try
        {
            if (hostProcess.HasExited) return IntPtr.Zero;
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }

        IntPtr byTitle = FindByTitleToken(titleToken);
        if (byTitle != IntPtr.Zero) return byTitle;

        HashSet<int> processTree = GetProcessTree(hostProcess.Id);
        IntPtr console = FindConsoleInTree(processTree);
        if (console != IntPtr.Zero) return console;
        return FindAnyVisibleInTree(processTree);
    }

    public static bool TryEmbed(IntPtr guestHandle, IntPtr hostHandle, out GuestPlacement original)
    {
        original = default;
        if (guestHandle == IntPtr.Zero || hostHandle == IntPtr.Zero) return false;
        if (!IsWindow(guestHandle) || !IsWindow(hostHandle)) return false;

        GetWindowRect(guestHandle, out RECT rect);
        original = new GuestPlacement
        {
            Handle = guestHandle,
            Style = GetWindowLongPtr(guestHandle, GwlStyle),
            ExStyle = GetWindowLongPtr(guestHandle, GwlExStyle),
            Left = rect.Left,
            Top = rect.Top,
            Width = Math.Max(rect.Right - rect.Left, 640),
            Height = Math.Max(rect.Bottom - rect.Top, 400)
        };

        ShowWindow(guestHandle, SwHide);

        int style = unchecked((int)(long)original.Style);
        style &= ~WsCaption;
        style &= ~WsThickFrame;
        style &= ~WsSysMenu;
        style &= ~WsMinimizeBox;
        style &= ~WsMaximizeBox;
        style &= ~WsPopup;
        style |= WsChild;
        style |= WsVisible;
        SetWindowLongPtr(guestHandle, GwlStyle, new IntPtr(style));

        SetParent(guestHandle, hostHandle);
        SetWindowPos(
            guestHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoZOrder | SwpNoMove | SwpNoSize | SwpFrameChanged);
        ShowWindow(guestHandle, SwShow);
        return true;
    }

    public static void CloseAndKill(IntPtr guestHandle, Process? hostProcess)
    {
        var processIds = new HashSet<int>();
        if (hostProcess is not null)
        {
            try
            {
                processIds.UnionWith(GetProcessTree(hostProcess.Id));
            }
            catch (Exception)
            {
            }
        }

        if (IsAlive(guestHandle))
        {
            GetWindowThreadProcessId(guestHandle, out uint windowProcessId);
            processIds.UnionWith(GetProcessTree((int)windowProcessId));
            PostMessage(guestHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        foreach (int processId in processIds)
            TryKill(processId);
    }

    static void TryKill(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            /* already gone */
        }
    }

    public static void ReleaseToDesktop(GuestPlacement original)
    {
        if (original.Handle == IntPtr.Zero) return;
        if (!IsWindow(original.Handle)) return;

        SetParent(original.Handle, IntPtr.Zero);
        SetWindowLongPtr(original.Handle, GwlStyle, original.Style);
        SetWindowLongPtr(original.Handle, GwlExStyle, original.ExStyle);

        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        int left = original.Left;
        int top = original.Top;
        int width = original.Width;
        int height = original.Height;
        var bounds = new Rectangle(left, top, width, height);
        if (!bounds.IntersectsWith(area))
        {
            left = area.Left + 80;
            top = area.Top + 80;
        }

        SetWindowPos(
            original.Handle,
            IntPtr.Zero,
            left,
            top,
            width,
            height,
            SwpNoZOrder | SwpFrameChanged);
        ShowWindow(original.Handle, SwShow);
    }

    public static void WriteConsoleFontProperties(string titleToken, string grokExePath, int pointSize)
    {
        pointSize = AppSettings.NormalizeFontPoints(pointSize);
        int pixelHeight = PointSizeToPixels(IntPtr.Zero, pointSize);
        int fontSizeValue = pixelHeight << 16;
        string face = PickConsoleFace();

        WriteConsoleKey(@"Console\" + titleToken, face, fontSizeValue);
        if (!string.IsNullOrWhiteSpace(grokExePath))
            WriteConsoleKey(@"Console\" + Path.GetFullPath(grokExePath).Replace('\\', '_'), face, fontSizeValue);

        string conhost = Path.Combine(Environment.SystemDirectory, "conhost.exe");
        WriteConsoleKey(@"Console\" + Path.GetFullPath(conhost).Replace('\\', '_'), face, fontSizeValue);

        /* Capture launches conhost → cmd.exe; Windows reads the cmd.exe console key. */
        string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        WriteConsoleKey(@"Console\" + Path.GetFullPath(cmd).Replace('\\', '_'), face, fontSizeValue);
        string windowsCmd = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "cmd.exe");
        WriteConsoleKey(@"Console\" + windowsCmd.Replace('\\', '_'), face, fontSizeValue);
        WriteConsoleKey(@"Console\%SystemRoot%_System32_cmd.exe", face, fontSizeValue);
    }

    public static void DeleteConsoleTitleProperties(string titleToken)
    {
        if (string.IsNullOrWhiteSpace(titleToken)) return;
        try
        {
            Registry.CurrentUser.DeleteSubKey(@"Console\" + titleToken, throwOnMissingSubKey: false);
        }
        catch (Exception)
        {
        }
    }

    static void WriteConsoleKey(string subkey, string faceName, int fontSizeValue)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(subkey);
            if (key is null) return;
            key.SetValue("FaceName", faceName, RegistryValueKind.String);
            key.SetValue("FontFamily", FontFamilyTrueTypeModern, RegistryValueKind.DWord);
            key.SetValue("FontPitchAndFamily", FontFamilyTrueTypeModern, RegistryValueKind.DWord);
            key.SetValue("FontWeight", 400, RegistryValueKind.DWord);
            key.SetValue("FontSize", fontSizeValue, RegistryValueKind.DWord);
        }
        catch (Exception)
        {
        }
    }

    public static void SetConsolePointFont(IntPtr consoleWindow, int pointSize)
    {
        if (!IsAlive(consoleWindow)) return;
        pointSize = AppSettings.NormalizeFontPoints(pointSize);

        var processIds = new HashSet<int>();
        GetWindowThreadProcessId(consoleWindow, out uint windowProcessId);
        if (windowProcessId != 0)
        {
            processIds.Add((int)windowProcessId);
            processIds.UnionWith(GetProcessTree((int)windowProcessId));
        }

        foreach (int processId in processIds)
        {
            if (TrySetFontOnProcess((uint)processId, consoleWindow, pointSize)) return;
        }
    }

    static bool TrySetFontOnProcess(uint processId, IntPtr consoleWindow, int pointSize)
    {
        FreeConsole();
        if (!AttachConsole(processId)) return false;

        IntPtr outputHandle = IntPtr.Zero;
        bool closeOutput = false;
        try
        {
            outputHandle = CreateFile(
                "CONOUT$",
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
            if (outputHandle == IntPtr.Zero || outputHandle == new IntPtr(-1))
            {
                outputHandle = GetStdHandle(StdOutputHandle);
                closeOutput = false;
            }
            else
            {
                closeOutput = true;
            }

            if (outputHandle == IntPtr.Zero || outputHandle == new IntPtr(-1)) return false;
            return TryApplyConsoleFonts(outputHandle, consoleWindow, pointSize);
        }
        finally
        {
            if (closeOutput) CloseHandle(outputHandle);
            FreeConsole();
        }
    }

    static bool TryApplyConsoleFonts(IntPtr outputHandle, IntPtr windowHandle, int pointSize)
    {
        short preferred = PointSizeToPixels(windowHandle, pointSize);
        string[] faces = { "Consolas", "Lucida Console", "Cascadia Mono", "Courier New" };
        short[] heights =
        {
            preferred,
            (short)(preferred + 1),
            (short)(preferred - 1),
            (short)(preferred + 2),
            (short)(preferred - 2),
            (short)(preferred + 4)
        };

        foreach (string face in faces)
        {
            foreach (short height in heights)
            {
                if (height < 16) continue;
                var info = new CONSOLE_FONT_INFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<CONSOLE_FONT_INFOEX>(),
                    nFont = 0,
                    FontFamily = FontFamilyTrueTypeModern,
                    FontWeight = 400,
                    FaceName = face,
                    dwFontSize = new COORD { X = 0, Y = height }
                };
                if (!SetCurrentConsoleFontEx(outputHandle, false, ref info)) continue;

                var readBack = new CONSOLE_FONT_INFOEX
                {
                    cbSize = info.cbSize,
                    FaceName = string.Empty
                };
                if (!GetCurrentConsoleFontEx(outputHandle, false, ref readBack)) return true;
                if (readBack.dwFontSize.Y >= preferred - 4) return true;
            }
        }

        return false;
    }

    static short PointSizeToPixels(IntPtr windowHandle, int pointSize)
    {
        uint dpi = 0;
        if (windowHandle != IntPtr.Zero) dpi = GetDpiForWindow(windowHandle);
        if (dpi == 0) dpi = GetDpiForSystem();
        if (dpi == 0) dpi = 96;
        int pixels = (int)Math.Round(pointSize * dpi / 72.0);
        if (pixels < 16) pixels = 16;
        return (short)pixels;
    }

    static string PickConsoleFace()
    {
        string[] preferred = { "Consolas", "Lucida Console", "Cascadia Mono", "Courier New" };
        try
        {
            using var installed = new InstalledFontCollection();
            foreach (string face in preferred)
            {
                foreach (FontFamily family in installed.Families)
                {
                    if (family.Name.Equals(face, StringComparison.OrdinalIgnoreCase))
                        return family.Name;
                }
            }
        }
        catch (Exception)
        {
        }

        return "Consolas";
    }

    public static void MoveTo(IntPtr windowHandle, int left, int top, int width, int height)
    {
        if (!IsAlive(windowHandle)) return;
        if (width < 16 || height < 16) return;
        MoveWindow(windowHandle, left, top, width, height, true);
    }

    public static void SetVisible(IntPtr windowHandle, bool visible)
    {
        if (!IsAlive(windowHandle)) return;
        ShowWindow(windowHandle, visible ? SwShow : SwHide);
    }

    public static void Focus(IntPtr windowHandle)
    {
        if (!IsAlive(windowHandle)) return;
        SetForegroundWindow(windowHandle);
    }

    public static void SendCtrlPlus(IntPtr windowHandle, int times)
    {
        if (!IsAlive(windowHandle)) return;
        if (times < 1) return;

        uint thisThread = GetCurrentThreadId();
        uint windowThread = GetWindowThreadProcessId(windowHandle, out _);
        bool attached = false;
        if (windowThread != 0 && windowThread != thisThread)
            attached = AttachThreadInput(thisThread, windowThread, true);

        SetForegroundWindow(windowHandle);
        SetFocus(windowHandle);
        try
        {
            for (int repeat = 0; repeat < times; repeat++)
            {
                SendChord(VkControl, VkOemPlus);
                Thread.Sleep(80);
            }
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, windowThread, false);
        }
    }

    static void SendChord(ushort modifierVirtualKey, ushort keyVirtualKey)
    {
        INPUT[] inputs =
        {
            KeyInput(modifierVirtualKey, keyUp: false),
            KeyInput(keyVirtualKey, keyUp: false),
            KeyInput(keyVirtualKey, keyUp: true),
            KeyInput(modifierVirtualKey, keyUp: true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    static INPUT KeyInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = keyUp ? KeyeventfKeyup : 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    static IntPtr FindByTitleToken(string titleToken)
    {
        if (string.IsNullOrWhiteSpace(titleToken)) return IntPtr.Zero;

        var search = new WindowSearch { TitleToken = titleToken };
        EnumWindowsProc callback = search.ByTitle;
        EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return search.Result;
    }

    static IntPtr FindConsoleInTree(HashSet<int> processTree)
    {
        if (processTree.Count == 0) return IntPtr.Zero;

        var search = new WindowSearch { ProcessTree = processTree };
        EnumWindowsProc callback = search.ByProcessTree;
        EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return search.Result;
    }

    static IntPtr FindAnyVisibleInTree(HashSet<int> processTree)
    {
        if (processTree.Count == 0) return IntPtr.Zero;

        var search = new WindowSearch { ProcessTree = processTree };
        EnumWindowsProc callback = search.AnyVisibleInTree;
        EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return search.Result;
    }

    static HashSet<int> GetProcessTree(int rootProcessId)
    {
        var tree = new HashSet<int> { rootProcessId };
        var rows = new List<(int ProcessId, int ParentProcessId)>();

        IntPtr snapshot = CreateToolhelp32Snapshot(Th32CsSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1)) return tree;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry)) return tree;

            do
            {
                rows.Add(((int)entry.th32ProcessID, (int)entry.th32ParentProcessID));
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        bool added = true;
        while (added)
        {
            added = false;
            foreach ((int processId, int parentProcessId) in rows)
            {
                if (!tree.Contains(parentProcessId)) continue;
                if (tree.Add(processId)) added = true;
            }
        }

        return tree;
    }

    sealed class WindowSearch
    {
        public string? TitleToken;
        public HashSet<int>? ProcessTree;
        public IntPtr Result;

        public bool ByTitle(IntPtr windowHandle, IntPtr lParam)
        {
            if (!IsWindowVisible(windowHandle)) return true;
            if (IsImeWindow(windowHandle)) return true;

            var className = new StringBuilder(256);
            GetClassName(windowHandle, className, className.Capacity);
            if (!IsConsoleClass(className.ToString())) return true;

            var title = new StringBuilder(512);
            GetWindowText(windowHandle, title, title.Capacity);
            if (title.ToString().IndexOf(TitleToken!, StringComparison.OrdinalIgnoreCase) < 0) return true;

            Result = windowHandle;
            return false;
        }

        public bool ByProcessTree(IntPtr windowHandle, IntPtr lParam)
        {
            if (!IsWindowVisible(windowHandle)) return true;
            if (IsImeWindow(windowHandle)) return true;

            GetWindowThreadProcessId(windowHandle, out uint processId);
            if (ProcessTree is null || !ProcessTree.Contains((int)processId)) return true;

            var className = new StringBuilder(256);
            GetClassName(windowHandle, className, className.Capacity);
            if (!IsConsoleClass(className.ToString())) return true;

            Result = windowHandle;
            return false;
        }

        public bool AnyVisibleInTree(IntPtr windowHandle, IntPtr lParam)
        {
            if (!IsWindowVisible(windowHandle)) return true;
            if (IsImeWindow(windowHandle)) return true;

            GetWindowThreadProcessId(windowHandle, out uint processId);
            if (ProcessTree is null || !ProcessTree.Contains((int)processId)) return true;

            Result = windowHandle;
            return false;
        }
    }

    static bool IsImeWindow(IntPtr windowHandle)
    {
        var className = new StringBuilder(256);
        GetClassName(windowHandle, className, className.Capacity);
        string name = className.ToString();
        return name.Equals("IME", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("MSCTFIME UI", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsConsoleClass(string className)
    {
        return className.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase);
    }

    static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
    {
        if (IntPtr.Size == 8) return GetWindowLongPtr64(windowHandle, index);
        return new IntPtr(GetWindowLong32(windowHandle, index));
    }

    static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue)
    {
        if (IntPtr.Size == 8) return SetWindowLongPtr64(windowHandle, index, newValue);
        return new IntPtr(SetWindowLong32(windowHandle, index, newValue.ToInt32()));
    }

    delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr windowHandle, StringBuilder title, int maxCount);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern IntPtr SetParent(IntPtr childHandle, IntPtr parentHandle);

    [DllImport("user32.dll")]
    static extern bool MoveWindow(IntPtr windowHandle, int left, int top, int width, int height, bool repaint);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern IntPtr SetFocus(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern bool AttachThreadInput(uint attachFrom, uint attachTo, bool attach);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr windowHandle, out RECT rect);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int left,
        int top,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    static extern int GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    static extern int SetWindowLong32(IntPtr windowHandle, int index, int newValue);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    static extern IntPtr SetWindowLongPtr64(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr GetStdHandle(int stdHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool GetCurrentConsoleFontEx(IntPtr consoleOutput, bool maximumWindow, ref CONSOLE_FONT_INFOEX font);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetCurrentConsoleFontEx(IntPtr consoleOutput, bool maximumWindow, ref CONSOLE_FONT_INFOEX font);

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    static extern uint GetDpiForSystem();

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CONSOLE_FONT_INFOEX
    {
        public uint cbSize;
        public uint nFont;
        public COORD dwFontSize;
        public int FontFamily;
        public int FontWeight;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }
}
