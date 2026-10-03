namespace GrokLauncher;

public partial class MainForm : Form
{
    readonly AppSettings _settings;
    readonly List<GrokWindowHost> _openSessions = new();
    string? _newProjectRoot;
    string? _notesFolder;
    bool _launchNewProject;
    bool _loadingNotes;
    bool _notesDirty;
    bool _sizingRecentColumns;
    bool _uiReady;
    readonly System.Windows.Forms.Timer _dateHoverTimer = new();
    readonly System.Windows.Forms.Timer _heartbeatTimer = new();
    readonly Label _datePopupLabel = new();
    ListViewItem? _datePopupRow;
    ListViewItem? _dateHoverRow;

    const int LastChangedColumnIndex = 1;
    const int DateHoverDelayMs = 400;
    const int LastChangedMaxDepth = 12;
    const int LastChangedMaxFiles = 8000;
    static readonly HashSet<string> LastChangedSkipFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        ".vs",
        "__pycache__",
        ".idea"
    };

    public MainForm()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        try
        {
            Icon? associatedIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (associatedIcon is not null) Icon = associatedIcon;
        }
        catch (Exception)
        {
            /* keep the default icon if the exe icon cannot be read */
        }
        UiStyle.ApplyTo(this);
        LoadConsoleFontChoice();
        InitDateHoverPopup();
        StartHeartbeat();
        ShowTemplateNotes();
        PopulateRecentList();
        RestoreLastRoot();
        if (lstRecent.Items.Count > 0) lstRecent.Items[0].Selected = true;
        UpdateTargetPreview();
    }

    void LoadConsoleFontChoice()
    {
        cmbConsoleFont.Items.Clear();
        cmbConsoleFont.Items.Add("16 pt");
        cmbConsoleFont.Items.Add("18 pt");
        cmbConsoleFont.Items.Add("20 pt");
        cmbConsoleFont.Items.Add("28 pt");
        _settings.CapturedConsoleFontPoints = 28;
        cmbConsoleFont.SelectedIndex = 3;
        WindowsTerminalFont.TryEnsureProfile(28);
    }

    int SelectedFontPoints()
    {
        if (cmbConsoleFont.SelectedIndex == 0) return 16;
        if (cmbConsoleFont.SelectedIndex == 1) return 18;
        if (cmbConsoleFont.SelectedIndex == 2) return 20;
        return 28;
    }

    void cmbConsoleFont_SelectedIndexChanged(object? sender, EventArgs eventArgs)
    {
        if (!_uiReady) return;
        _settings.CapturedConsoleFontPoints = SelectedFontPoints();
        _settings.Save();
        WindowsTerminalFont.TryEnsureProfile(_settings.CapturedConsoleFontPoints);
    }

    void InitDateHoverPopup()
    {
        components ??= new System.ComponentModel.Container();
        components.Add(_dateHoverTimer);

        _datePopupLabel.AutoSize = true;
        _datePopupLabel.BackColor = SystemColors.Info;
        _datePopupLabel.BorderStyle = BorderStyle.FixedSingle;
        _datePopupLabel.Font = UiStyle.Text;
        _datePopupLabel.ForeColor = SystemColors.InfoText;
        _datePopupLabel.Name = "lblDatePopup";
        _datePopupLabel.Padding = new Padding(12, 8, 12, 8);
        _datePopupLabel.Visible = false;
        _datePopupLabel.MouseLeave += lstRecent_MouseLeave;
        Controls.Add(_datePopupLabel);
        _datePopupLabel.BringToFront();

        _dateHoverTimer.Interval = DateHoverDelayMs;
        _dateHoverTimer.Tick += dateHoverTimer_Tick;
    }

    void StartHeartbeat()
    {
        components ??= new System.ComponentModel.Container();
        components.Add(_heartbeatTimer);
        _heartbeatTimer.Interval = 1000;
        _heartbeatTimer.Tick += (_, _) =>
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(Path.GetTempPath(), "GrokLauncher.heartbeat"),
                    DateTime.UtcNow.ToString("o"));
            }
            catch (Exception)
            {
                /* heartbeat is optional */
            }
        };
        _heartbeatTimer.Start();
    }

    void RestoreLastRoot()
    {
        string? lastRoot = _settings.LastNewProjectRoot;
        if (string.IsNullOrWhiteSpace(lastRoot) || !Directory.Exists(lastRoot)) return;
        _newProjectRoot = lastRoot;
        lblRoot.Text = lastRoot;
    }

    void PopulateRecentList(string? selectFolder = null)
    {
        CancelDateHover();
        lstRecent.BeginUpdate();
        lstRecent.Items.Clear();

        foreach (string folder in _settings.RecentFolders)
        {
            string projectName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(projectName)) projectName = folder;

            DateTime? lastChanged = TryGetLastChanged(folder);
            string dateText = lastChanged is DateTime changedAt ? changedAt.ToString("yyyy-MM-dd") : string.Empty;
            var row = new ListViewItem(projectName) { Tag = folder };
            ListViewItem.ListViewSubItem lastChangedCell = row.SubItems.Add(dateText);
            lastChangedCell.Tag = lastChanged;
            row.SubItems.Add(folder);
            if (!Directory.Exists(folder)) row.ForeColor = SystemColors.GrayText; /* missing on disk */
            lstRecent.Items.Add(row);

            if (selectFolder is not null && string.Equals(folder, selectFolder, StringComparison.OrdinalIgnoreCase)) row.Selected = true;
        }

        lstRecent.EndUpdate();
        if (_uiReady) SizeRecentColumns();
        if (lstRecent.SelectedItems.Count > 0) lstRecent.EnsureVisible(lstRecent.SelectedItems[0].Index);
    }

    void lstRecent_SizeChanged(object? sender, EventArgs eventArgs)
    {
        if (!_uiReady) return; /* native list is not ready during WM_CREATE */
        CancelDateHover();
        SizeRecentColumns();
    }

    void SizeRecentColumns()
    {
        if (!_uiReady) return;
        if (lstRecent.Font is null) return;
        if (lstRecent.Columns.Count < 3) return;
        if (_sizingRecentColumns) return;
        _sizingRecentColumns = true;
        try
        {
            int projectWidth = MeasureRecentColumn(0, "Project");
            int changedWidth = MeasureRecentColumn(1, "Last changed");
            int folderWidth = MeasureRecentColumn(2, "Folder");

            lstRecent.Columns[0].Width = projectWidth;
            lstRecent.Columns[1].Width = changedWidth;

            int remainingWidth = lstRecent.ClientSize.Width - projectWidth - changedWidth - 8; /* avoid phantom h-scroll */
            if (remainingWidth > folderWidth) folderWidth = remainingWidth; /* fill leftover space */
            if (folderWidth < 40) folderWidth = 40;
            lstRecent.Columns[2].Width = folderWidth;
        }
        finally
        {
            _sizingRecentColumns = false;
        }
    }

    int MeasureRecentColumn(int columnIndex, string headerText)
    {
        const int cellPadding = 28; /* 18pt bold clips without extra cell inset */
        Font font = lstRecent.Font ?? UiStyle.Text;
        int width = TextRenderer.MeasureText(headerText, font).Width + cellPadding;
        foreach (ListViewItem row in lstRecent.Items)
        {
            if (columnIndex >= row.SubItems.Count) continue;
            string cellText = row.SubItems[columnIndex].Text ?? string.Empty;
            int cellWidth = TextRenderer.MeasureText(cellText, font).Width + cellPadding;
            if (cellWidth > width) width = cellWidth;
        }

        return width;
    }

    static DateTime? TryGetLastChanged(string folder)
    {
        try
        {
            var root = new DirectoryInfo(folder);
            if (!root.Exists) return null;

            DateTime newestWrite = root.LastWriteTime; /* LastWriteTime, never CreationTime */
            int filesSeen = 0;
            WalkForNewestWrite(root, ref newestWrite, ref filesSeen, depth: 0);
            return newestWrite;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static void WalkForNewestWrite(DirectoryInfo directory, ref DateTime newestWrite, ref int filesSeen, int depth)
    {
        if (depth > LastChangedMaxDepth) return;
        if (filesSeen >= LastChangedMaxFiles) return;

        IEnumerable<FileInfo> files;
        try
        {
            files = directory.EnumerateFiles();
        }
        catch (Exception)
        {
            return;
        }

        foreach (FileInfo file in files)
        {
            filesSeen++;
            if (filesSeen > LastChangedMaxFiles) return;
            try
            {
                DateTime writeTime = file.LastWriteTime;
                if (writeTime > newestWrite) newestWrite = writeTime;
            }
            catch (Exception)
            {
                /* skip unreadable file */
            }
        }

        IEnumerable<DirectoryInfo> children;
        try
        {
            children = directory.EnumerateDirectories();
        }
        catch (Exception)
        {
            return;
        }

        foreach (DirectoryInfo child in children)
        {
            if (LastChangedSkipFolders.Contains(child.Name)) continue;
            try
            {
                if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue; /* no symlink cycles */
                DateTime folderWrite = child.LastWriteTime;
                if (folderWrite > newestWrite) newestWrite = folderWrite;
            }
            catch (Exception)
            {
                continue;
            }

            WalkForNewestWrite(child, ref newestWrite, ref filesSeen, depth + 1);
            if (filesSeen >= LastChangedMaxFiles) return;
        }
    }

    void lstRecent_MouseMove(object? sender, MouseEventArgs eventArgs)
    {
        ListViewHitTestInfo? hit = lstRecent.HitTest(eventArgs.Location);
        if (hit is null || hit.Item is null || ColumnIndexAtX(eventArgs.X) != LastChangedColumnIndex)
        {
            CancelDateHover();
            return;
        }

        if (_datePopupRow == hit.Item) return; /* already showing for this row */
        if (_dateHoverRow == hit.Item) return; /* wait for hover delay */

        _dateHoverTimer.Stop();
        HideDatePopup();
        _dateHoverRow = hit.Item;
        _dateHoverTimer.Start();
    }

    void lstRecent_MouseLeave(object? sender, EventArgs eventArgs)
    {
        if (_datePopupLabel.Visible)
        {
            Point onPopup = _datePopupLabel.PointToClient(Cursor.Position);
            if (_datePopupLabel.ClientRectangle.Contains(onPopup)) return;
        }

        Point onList = lstRecent.PointToClient(Cursor.Position);
        if (lstRecent.ClientRectangle.Contains(onList)) return; /* still over the list */

        CancelDateHover();
    }

    void dateHoverTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _dateHoverTimer.Stop();
        if (_dateHoverRow is null) return;
        ShowDatePopup(_dateHoverRow);
    }

    void ShowDatePopup(ListViewItem row)
    {
        if (row.SubItems.Count <= LastChangedColumnIndex)
        {
            HideDatePopup();
            return;
        }

        if (row.SubItems[LastChangedColumnIndex].Tag is not DateTime lastChanged)
        {
            HideDatePopup();
            return;
        }

        Rectangle cellBounds = DateCellBounds(row);
        _datePopupLabel.Text = lastChanged.ToString("F");
        _datePopupLabel.Visible = true;
        _datePopupLabel.BringToFront();

        Point belowCell = lstRecent.PointToScreen(new Point(cellBounds.Left, cellBounds.Bottom + 4));
        Point onForm = PointToClient(belowCell);
        if (onForm.Y + _datePopupLabel.Height > ClientSize.Height) onForm = PointToClient(lstRecent.PointToScreen(new Point(cellBounds.Left, cellBounds.Top - _datePopupLabel.Height - 4))); /* keep on form */
        if (onForm.X + _datePopupLabel.Width > ClientSize.Width) onForm.X = ClientSize.Width - _datePopupLabel.Width;
        if (onForm.X < 0) onForm.X = 0;
        if (onForm.Y < 0) onForm.Y = 0;
        _datePopupLabel.Location = onForm;
        _datePopupRow = row;
    }

    Rectangle DateCellBounds(ListViewItem row)
    {
        Rectangle bounds = row.SubItems[LastChangedColumnIndex].Bounds;
        if (bounds.Width > 0 && bounds.Height > 0) return bounds;

        int left = 0;
        for (int columnIndex = 0; columnIndex < LastChangedColumnIndex; columnIndex++)
            left += lstRecent.Columns[columnIndex].Width;
        return new Rectangle(left, row.Bounds.Top, lstRecent.Columns[LastChangedColumnIndex].Width, row.Bounds.Height);
    }

    int ColumnIndexAtX(int clientX)
    {
        int left = 0;
        for (int columnIndex = 0; columnIndex < lstRecent.Columns.Count; columnIndex++)
        {
            int width = lstRecent.Columns[columnIndex].Width;
            if (clientX >= left && clientX < left + width) return columnIndex;
            left += width;
        }

        return -1;
    }

    void HideDatePopup()
    {
        _datePopupRow = null;
        _datePopupLabel.Visible = false;
    }

    void CancelDateHover()
    {
        _dateHoverTimer.Stop();
        _dateHoverRow = null;
        HideDatePopup();
    }

    void btnNewProject_Click(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the root folder for the new project",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(_newProjectRoot) && Directory.Exists(_newProjectRoot))
            dialog.SelectedPath = _newProjectRoot;
        else if (!string.IsNullOrWhiteSpace(_settings.LastNewProjectRoot) &&
                 Directory.Exists(_settings.LastNewProjectRoot))
            dialog.SelectedPath = _settings.LastNewProjectRoot;

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        _newProjectRoot = dialog.SelectedPath;
        _settings.LastNewProjectRoot = _newProjectRoot;
        _settings.Save();
        lblRoot.Text = _newProjectRoot;
        _launchNewProject = true;
        SaveCurrentNotes();
        foreach (ListViewItem row in lstRecent.SelectedItems)
            row.Selected = false;
        ShowTemplateNotes();
        txtProjectName.Focus();
        UpdateTargetPreview();
    }

    void btnExistingProject_Click(object? sender, EventArgs eventArgs)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select an existing project folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (lstRecent.SelectedItems.Count > 0)
        {
            string selectedFolder = (string)lstRecent.SelectedItems[0].Tag!;
            if (Directory.Exists(selectedFolder)) dialog.SelectedPath = selectedFolder;
        }
        else if (!string.IsNullOrWhiteSpace(_settings.LastNewProjectRoot) &&
                 Directory.Exists(_settings.LastNewProjectRoot))
            dialog.SelectedPath = _settings.LastNewProjectRoot;

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        string folder = Path.GetFullPath(dialog.SelectedPath);
        SaveCurrentNotes();
        _launchNewProject = false;
        txtProjectName.Clear();
        _settings.RememberFolder(folder);
        _settings.Save();
        PopulateRecentList(folder);
        UpdateTargetPreview();
    }

    void lstRecent_SelectedIndexChanged(object? sender, EventArgs eventArgs)
    {
        CancelDateHover(); /* popup goes away when row focus changes */
        if (lstRecent.SelectedItems.Count == 0) return;
        SaveCurrentNotes();
        _launchNewProject = false;
        string folder = (string)lstRecent.SelectedItems[0].Tag!;
        LoadNotesFor(folder);
        UpdateTargetPreview();
    }

    void lstRecent_DoubleClick(object? sender, EventArgs eventArgs)
    {
        if (lstRecent.SelectedItems.Count == 0) return;
        _launchNewProject = false;
        LaunchSelectedTarget(startWithSpec: false);
    }

    void lstRecent_KeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyCode != Keys.Delete) return;
        if (lstRecent.SelectedItems.Count == 0) return;
        RemoveSelectedRecent();
        eventArgs.Handled = true;
    }

    void btnRemoveRecent_Click(object? sender, EventArgs eventArgs)
    {
        RemoveSelectedRecent();
    }

    void RemoveSelectedRecent()
    {
        if (lstRecent.SelectedItems.Count == 0)
        {
            BigDialog.Show(this, "Select a recent project to remove from the list.", RevisionHistory.TitleBar);
            return;
        }

        string folder = (string)lstRecent.SelectedItems[0].Tag!;
        SaveCurrentNotes();
        _settings.RemoveFolder(folder);
        _settings.Save();
        PopulateRecentList();
        ShowTemplateNotes();
        UpdateTargetPreview();
    }

    void txtProjectName_TextChanged(object? sender, EventArgs eventArgs)
    {
        if (!string.IsNullOrWhiteSpace(txtProjectName.Text) && !string.IsNullOrWhiteSpace(_newProjectRoot)) _launchNewProject = true; /* name + root means create */
        UpdateTargetPreview();
    }

    void btnLaunch_Click(object? sender, EventArgs eventArgs)
    {
        LaunchSelectedTarget(startWithSpec: false);
    }

    void btnLaunchWithSpec_Click(object? sender, EventArgs eventArgs)
    {
        LaunchSelectedTarget(startWithSpec: true);
    }

    void btnInstallGrok_Click(object? sender, EventArgs eventArgs)
    {
        try
        {
            if (GrokCli.IsInstalled())
            {
                BigDialog.Show(
                    this,
                    "Grok Build is already installed.\nA PowerShell window will run grok update.",
                    RevisionHistory.TitleBar);
            }

            GrokCli.InstallOrUpdate();
        }
        catch (Exception exception)
        {
            BigDialog.Show(this, exception.Message, RevisionHistory.TitleBar);
        }
    }

    void LaunchSelectedTarget(bool startWithSpec)
    {
        string? targetFolder = ResolveTargetFolder();
        if (targetFolder is null) return;

        try
        {
            Directory.CreateDirectory(targetFolder);
        }
        catch (Exception exception)
        {
            BigDialog.Show(
                this,
                $"Could not create the project folder:\n{exception.Message}",
                "Grok Launcher");
            return;
        }

        try
        {
            SessionNotes.SaveFrom(rtfNotes, targetFolder);
            _notesFolder = targetFolder;
            _notesDirty = false;
        }
        catch (Exception exception)
        {
            BigDialog.Show(
                this,
                $"Could not save session notes:\n{exception.Message}",
                "Grok Launcher");
        }

        GrokWindowHost? existing = FindOpenSession(targetFolder);
        if (existing is not null)
        {
            tabsMain.SelectedTab = existing.TabPage;
            existing.SetGuestVisible(true);
        }
        else
        {
            try
            {
                string? specPrompt = startWithSpec ? SessionNotes.BuildSpecPrompt(rtfNotes.Text) : null;
                GrokLaunch launch = GrokCli.LaunchInFolder(targetFolder, specPrompt, SelectedFontPoints());
                OpenSessionTab(launch);
            }
            catch (Exception exception)
            {
                BigDialog.Show(this, exception.Message, "Grok Launcher");
                return;
            }
        }

        _settings.RememberFolder(targetFolder);
        _settings.Save();
        PopulateRecentList(targetFolder);
        _launchNewProject = false;
        txtProjectName.Clear();
        UpdateTargetPreview();
    }

    GrokWindowHost? FindOpenSession(string folderPath)
    {
        foreach (GrokWindowHost session in _openSessions)
        {
            if (!session.IsAlive) continue;
            if (string.Equals(session.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
                return session;
        }

        return null;
    }

    void OpenSessionTab(GrokLaunch launch)
    {
        var page = new TabPage(TabLabelFor(launch.FolderPath))
        {
            Name = "tabSession",
            Padding = new Padding(0),
            UseVisualStyleBackColor = false,
            BackColor = Color.Black
        };
        var host = new GrokWindowHost(launch, page);
        host.SessionClosed += Host_SessionClosed;
        page.Controls.Add(host);
        tabsMain.TabPages.Add(page);
        tabsMain.SelectedTab = page;
        _openSessions.Add(host);
        UiStyle.ApplyTo(page);
        host.BeginCapture();
    }

    static string TabLabelFor(string folderPath)
    {
        string projectName = Path.GetFileName(
            folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(projectName)) projectName = folderPath;
        if (projectName.Length <= 20) return projectName;
        return projectName.Substring(0, 20);
    }

    void Host_SessionClosed(GrokWindowHost host)
    {
        if (IsDisposed) return;
        host.SessionClosed -= Host_SessionClosed;
        _openSessions.Remove(host);
        TabPage page = host.TabPage;
        if (tabsMain.TabPages.Contains(page)) tabsMain.TabPages.Remove(page);
        if (!host.IsDisposed) host.Dispose();
        page.Dispose();
    }

    void tabsMain_SelectedIndexChanged(object? sender, EventArgs eventArgs)
    {
        foreach (GrokWindowHost session in _openSessions)
        {
            bool selected = session.TabPage == tabsMain.SelectedTab;
            session.SetGuestVisible(selected);
        }
    }

    void MainForm_Activated(object? sender, EventArgs eventArgs)
    {
        BeginInvoke(FocusSelectedSessionGuest); /* after WinForms restores ActiveControl */
    }

    void FocusSelectedSessionGuest()
    {
        if (WindowState == FormWindowState.Minimized) return;
        foreach (GrokWindowHost session in _openSessions)
        {
            if (session.TabPage != tabsMain.SelectedTab) continue;
            ActiveControl = session;
            session.FocusGuest();
            return;
        }
    }

    void tabsMain_MouseDown(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Middle) return;

        for (int tabIndex = 0; tabIndex < tabsMain.TabCount; tabIndex++)
        {
            if (!tabsMain.GetTabRect(tabIndex).Contains(eventArgs.Location)) continue;
            if (tabIndex == 0) return; /* launcher tab stays */
            CloseSessionTab(tabsMain.TabPages[tabIndex]);
            return;
        }
    }

    void CloseSessionTab(TabPage page)
    {
        GrokWindowHost? host = null;
        foreach (GrokWindowHost session in _openSessions)
        {
            if (session.TabPage != page) continue;
            host = session;
            break;
        }

        if (host is null) return;

        host.SessionClosed -= Host_SessionClosed;
        host.ReleaseToDesktop();
        _openSessions.Remove(host);
        if (tabsMain.TabPages.Contains(page)) tabsMain.TabPages.Remove(page);
        if (!host.IsDisposed) host.Dispose();
        page.Dispose();
    }

    string? ResolveTargetFolder()
    {
        if (_launchNewProject || ShouldCreateNewProject())
        {
            if (string.IsNullOrWhiteSpace(_newProjectRoot))
            {
                BigDialog.Show(this, "Click New project and choose the root folder first.", "Grok Launcher");
                return null;
            }

            string projectName = txtProjectName.Text.Trim();
            if (string.IsNullOrEmpty(projectName))
            {
                BigDialog.Show(this, "Enter a name for the new project folder.", "Grok Launcher");
                return null;
            }

            if (projectName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                BigDialog.Show(
                    this,
                    "The project folder name contains characters that Windows does not allow.",
                    "Grok Launcher");
                return null;
            }

            return Path.GetFullPath(Path.Combine(_newProjectRoot, projectName));
        }

        if (lstRecent.SelectedItems.Count > 0) return (string)lstRecent.SelectedItems[0].Tag!;

        BigDialog.Show(
            this,
            "Select a recent project, click Existing project, or click New project, enter a folder name, and Launch.",
            "Grok Launcher");
        return null;
    }

    bool ShouldCreateNewProject()
    {
        return !string.IsNullOrWhiteSpace(_newProjectRoot) &&
               !string.IsNullOrWhiteSpace(txtProjectName.Text) &&
               lstRecent.SelectedItems.Count == 0;
    }

    void UpdateTargetPreview()
    {
        if (_launchNewProject || ShouldCreateNewProject())
        {
            string projectName = txtProjectName.Text.Trim();
            if (string.IsNullOrWhiteSpace(_newProjectRoot))
            {
                lblTarget.Text = "Click New project to choose the root folder.";
                return;
            }

            if (string.IsNullOrEmpty(projectName))
            {
                lblTarget.Text = $"New project will be created under {_newProjectRoot}";
                return;
            }

            lblTarget.Text = $"Will launch in: {Path.Combine(_newProjectRoot, projectName)}";
            return;
        }

        if (lstRecent.SelectedItems.Count > 0)
        {
            string folder = (string)lstRecent.SelectedItems[0].Tag!;
            lblTarget.Text = $"Will launch in: {folder}";
            return;
        }

        if (lstRecent.Items.Count == 0) lblTarget.Text = "No recent projects yet. Click New project or Existing project to get started.";
        else lblTarget.Text = "Select a recent project, open an existing one, or create a new one.";
    }

    void rtfNotes_TextChanged(object? sender, EventArgs eventArgs)
    {
        if (_loadingNotes) return;
        _notesDirty = true;
    }

    void MainForm_Shown(object? sender, EventArgs eventArgs)
    {
        _uiReady = true;
        RestoreWindowBounds();
        RestoreRecentSplitter();
        SizeRecentColumns();
    }

    void splitMain_SplitterMoved(object? sender, SplitterEventArgs eventArgs)
    {
        CancelDateHover();
        SizeRecentColumns();
    }

    void RestoreRecentSplitter()
    {
        int distance = _settings.RecentSplitterDistance;
        if (distance < splitMain.Panel1MinSize) return;
        if (splitMain.Height <= 0) return;

        int maxDistance = splitMain.Height - splitMain.SplitterWidth - splitMain.Panel2MinSize;
        if (maxDistance < splitMain.Panel1MinSize) return;
        if (distance > maxDistance) distance = maxDistance;
        try
        {
            splitMain.SplitterDistance = distance;
        }
        catch (Exception)
        {
            /* keep the designer splitter if the saved distance does not fit */
        }
    }

    void MainForm_FormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        CancelDateHover();
        foreach (GrokWindowHost session in _openSessions.ToArray())
            session.CloseCapturedSession();
        SaveCurrentNotes();
        RememberWindowBounds();
        _settings.RecentSplitterDistance = splitMain.SplitterDistance;
        _settings.Save();
    }

    void RememberWindowBounds()
    {
        Rectangle bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _settings.WindowLeft = bounds.Left;
        _settings.WindowTop = bounds.Top;
        _settings.WindowWidth = bounds.Width;
        _settings.WindowHeight = bounds.Height;
        if (WindowState == FormWindowState.Minimized) _settings.WindowState = (int)FormWindowState.Normal; /* reopen restored */
        else _settings.WindowState = (int)WindowState;
    }

    void RestoreWindowBounds()
    {
        if (_settings.WindowWidth < MinimumSize.Width) return;
        if (_settings.WindowHeight < MinimumSize.Height) return;

        var bounds = new Rectangle(
            _settings.WindowLeft,
            _settings.WindowTop,
            _settings.WindowWidth,
            _settings.WindowHeight);
        if (!WindowBoundsAreOnAScreen(bounds)) return;

        try
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (_settings.WindowState == (int)FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
        }
        catch (Exception)
        {
            /* stay with the default centered window */
        }
    }

    static bool WindowBoundsAreOnAScreen(Rectangle bounds)
    {
        foreach (Screen screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.IntersectsWith(bounds)) return true;
        }

        return false;
    }

    void ShowTemplateNotes()
    {
        _loadingNotes = true;
        SessionNotes.LoadTemplate(rtfNotes);
        _loadingNotes = false;
        _notesFolder = null;
        _notesDirty = false;
    }

    void LoadNotesFor(string projectFolder)
    {
        _loadingNotes = true;
        SessionNotes.LoadInto(rtfNotes, projectFolder);
        _loadingNotes = false;
        _notesFolder = projectFolder;
        _notesDirty = false;
    }

    void SaveCurrentNotes()
    {
        if (!_notesDirty) return;
        if (string.IsNullOrWhiteSpace(_notesFolder) || !Directory.Exists(_notesFolder)) return;

        try
        {
            SessionNotes.SaveFrom(rtfNotes, _notesFolder);
            _notesDirty = false;
        }
        catch (Exception)
        {
            /* keep dirty so Launch or a later save can retry */
        }
    }
}
