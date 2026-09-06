namespace GrokLauncher;

public partial class MainForm : Form
{
    readonly AppSettings _settings;
    string? _newProjectRoot;
    string? _notesFolder;
    bool _launchNewProject;
    bool _loadingNotes;
    bool _notesDirty;
    bool _sizingRecentColumns;
    readonly ToolTip _datePopup = new();
    readonly System.Windows.Forms.Timer _dateHoverTimer = new();
    ListViewItem? _datePopupRow;
    ListViewItem? _dateHoverRow;
    string _datePopupText = string.Empty;

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
        InitDateHoverPopup();
        System.Drawing.Icon? associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (associatedIcon is not null) Icon = associatedIcon;
        UiStyle.ApplyTo(this);
        RestoreWindowBounds();
        ShowTemplateNotes();
        PopulateRecentList();
        RestoreLastRoot();
        if (lstRecent.Items.Count > 0) lstRecent.Items[0].Selected = true;
        UpdateTargetPreview();
    }

    void InitDateHoverPopup()
    {
        components ??= new System.ComponentModel.Container();
        components.Add(_datePopup);
        components.Add(_dateHoverTimer);

        _datePopup.ShowAlways = true;
        _datePopup.OwnerDraw = true;
        _datePopup.Popup += datePopup_Popup;
        _datePopup.Draw += datePopup_Draw;

        _dateHoverTimer.Interval = DateHoverDelayMs;
        _dateHoverTimer.Tick += dateHoverTimer_Tick;
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
        SizeRecentColumns();
        if (lstRecent.SelectedItems.Count > 0) lstRecent.EnsureVisible(lstRecent.SelectedItems[0].Index);
    }

    void lstRecent_SizeChanged(object? sender, EventArgs eventArgs)
    {
        CancelDateHover();
        SizeRecentColumns();
    }

    void SizeRecentColumns()
    {
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
        int width = TextRenderer.MeasureText(headerText, lstRecent.Font).Width + cellPadding;
        foreach (ListViewItem row in lstRecent.Items)
        {
            if (columnIndex >= row.SubItems.Count) continue;
            int cellWidth = TextRenderer.MeasureText(row.SubItems[columnIndex].Text, lstRecent.Font).Width + cellPadding;
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
        ListViewHitTestInfo hit = lstRecent.HitTest(eventArgs.Location);
        if (hit.Item is null || hit.SubItem is null)
        {
            CancelDateHover();
            return;
        }

        int columnIndex = hit.Item.SubItems.IndexOf(hit.SubItem);
        if (columnIndex != LastChangedColumnIndex)
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

        _datePopupText = lastChanged.ToString("F");
        Rectangle cellBounds = row.SubItems[LastChangedColumnIndex].Bounds;
        Point popupAt = new Point(cellBounds.Left, cellBounds.Bottom + 4);
        _datePopupRow = row;
        _datePopup.Show(_datePopupText, lstRecent, popupAt);
    }

    void HideDatePopup()
    {
        _datePopupRow = null;
        _datePopup.Hide(lstRecent);
    }

    void CancelDateHover()
    {
        _dateHoverTimer.Stop();
        _dateHoverRow = null;
        HideDatePopup();
    }

    void datePopup_Popup(object? sender, PopupEventArgs eventArgs)
    {
        Size textSize = TextRenderer.MeasureText(_datePopupText, UiStyle.Text);
        eventArgs.ToolTipSize = new Size(textSize.Width + 24, textSize.Height + 16);
    }

    void datePopup_Draw(object? sender, DrawToolTipEventArgs eventArgs)
    {
        eventArgs.DrawBackground();
        eventArgs.DrawBorder();
        TextRenderer.DrawText(
            eventArgs.Graphics,
            eventArgs.ToolTipText,
            UiStyle.Text,
            eventArgs.Bounds,
            SystemColors.InfoText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
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

        try
        {
            string? specPrompt = startWithSpec ? SessionNotes.BuildSpecPrompt(rtfNotes.Text) : null;
            GrokCli.LaunchInFolder(targetFolder, specPrompt);
        }
        catch (Exception exception)
        {
            BigDialog.Show(this, exception.Message, "Grok Launcher");
            return;
        }

        _settings.RememberFolder(targetFolder);
        _settings.Save();
        PopulateRecentList(targetFolder);
        _launchNewProject = false;
        txtProjectName.Clear();
        UpdateTargetPreview();
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

    void MainForm_FormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        CancelDateHover();
        SaveCurrentNotes();
        RememberWindowBounds();
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

        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        if (_settings.WindowState == (int)FormWindowState.Maximized) WindowState = FormWindowState.Maximized;
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
