namespace GrokLauncher;

public partial class MainForm : Form
{
    readonly AppSettings _settings;
    string? _newProjectRoot;
    string? _notesFolder;
    bool _launchNewProject;
    bool _loadingNotes;
    bool _notesDirty;

    public MainForm()
    {
        _settings = AppSettings.Load();
        InitializeComponent();
        System.Drawing.Icon? associatedIcon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (associatedIcon is not null) Icon = associatedIcon;
        UiStyle.ApplyTo(this);
        ShowTemplateNotes();
        PopulateRecentList();
        RestoreLastRoot();
        if (lstRecent.Items.Count > 0) lstRecent.Items[0].Selected = true;
        UpdateTargetPreview();
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
        lstRecent.BeginUpdate();
        lstRecent.Items.Clear();

        foreach (string folder in _settings.RecentFolders)
        {
            string projectName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(projectName)) projectName = folder;

            var row = new ListViewItem(projectName) { Tag = folder };
            row.SubItems.Add(folder);
            if (!Directory.Exists(folder)) row.ForeColor = SystemColors.GrayText; /* missing on disk */
            lstRecent.Items.Add(row);

            if (selectFolder is not null && string.Equals(folder, selectFolder, StringComparison.OrdinalIgnoreCase)) row.Selected = true;
        }

        lstRecent.EndUpdate();
        if (lstRecent.SelectedItems.Count > 0) lstRecent.EnsureVisible(lstRecent.SelectedItems[0].Index);
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
        SaveCurrentNotes();
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
