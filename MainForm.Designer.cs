namespace GrokLauncher;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private SplitContainer splitMain;
    private Panel panelRecentHeader;
    private Label lblRecent;
    private ListView lstRecent;
    private Button btnRemoveRecent;
    private TableLayoutPanel tableLower;
    private Panel panelProjectButtons;
    private Button btnNewProject;
    private Button btnExistingProject;
    private Label lblRoot;
    private Label lblProjectName;
    private TextBox txtProjectName;
    private Label lblNotes;
    private RichTextBox rtfNotes;
    private Label lblTarget;
    private Button btnLaunch;
    private Button btnLaunchWithSpec;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null)) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        splitMain = new SplitContainer();
        panelRecentHeader = new Panel();
        lblRecent = new Label();
        lstRecent = new ListView();
        btnRemoveRecent = new Button();
        tableLower = new TableLayoutPanel();
        panelProjectButtons = new Panel();
        btnNewProject = new Button();
        btnExistingProject = new Button();
        lblRoot = new Label();
        lblProjectName = new Label();
        txtProjectName = new TextBox();
        lblNotes = new Label();
        rtfNotes = new RichTextBox();
        lblTarget = new Label();
        btnLaunch = new Button();
        btnLaunchWithSpec = new Button();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        panelRecentHeader.SuspendLayout();
        tableLower.SuspendLayout();
        panelProjectButtons.SuspendLayout();
        SuspendLayout();

        Font = UiStyle.Text;

        btnRemoveRecent.Dock = DockStyle.Right;
        btnRemoveRecent.Font = UiStyle.Text;
        btnRemoveRecent.Name = "btnRemoveRecent";
        btnRemoveRecent.Size = new Size(260, 48);
        btnRemoveRecent.TabIndex = 1;
        btnRemoveRecent.Text = "Remove from list";
        btnRemoveRecent.UseVisualStyleBackColor = true;
        btnRemoveRecent.Click += btnRemoveRecent_Click;

        lblRecent.Dock = DockStyle.Fill;
        lblRecent.Font = UiStyle.Text;
        lblRecent.Name = "lblRecent";
        lblRecent.Padding = new Padding(0, 8, 12, 0);
        lblRecent.Text = "Recent projects (last 20, most recent first)";
        lblRecent.TextAlign = ContentAlignment.MiddleLeft;

        panelRecentHeader.Dock = DockStyle.Top;
        panelRecentHeader.Name = "panelRecentHeader";
        panelRecentHeader.Padding = new Padding(0, 4, 0, 4);
        panelRecentHeader.Size = new Size(960, 56);
        panelRecentHeader.Controls.Add(lblRecent);
        panelRecentHeader.Controls.Add(btnRemoveRecent);

        lstRecent.Dock = DockStyle.Fill;
        lstRecent.Font = UiStyle.Text;
        lstRecent.FullRowSelect = true;
        lstRecent.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lstRecent.HideSelection = false;
        lstRecent.MultiSelect = false;
        lstRecent.Name = "lstRecent";
        lstRecent.Scrollable = true;
        lstRecent.ShowItemToolTips = false;
        lstRecent.TabIndex = 0;
        lstRecent.UseCompatibleStateImageBehavior = false;
        lstRecent.View = View.Details;
        lstRecent.Columns.AddRange(new ColumnHeader[]
        {
            new ColumnHeader { Text = "Project", Width = 200 },
            new ColumnHeader { Text = "Last changed", Width = 180 },
            new ColumnHeader { Text = "Folder", Width = 540 }
        });
        lstRecent.SelectedIndexChanged += lstRecent_SelectedIndexChanged;
        lstRecent.DoubleClick += lstRecent_DoubleClick;
        lstRecent.KeyDown += lstRecent_KeyDown;
        lstRecent.SizeChanged += lstRecent_SizeChanged;
        lstRecent.MouseMove += lstRecent_MouseMove;
        lstRecent.MouseLeave += lstRecent_MouseLeave;
        lstRecent.Leave += lstRecent_MouseLeave;

        splitMain.Panel1.Padding = new Padding(20, 0, 20, 12);
        splitMain.Panel1.Controls.Add(lstRecent);
        splitMain.Panel1.Controls.Add(panelRecentHeader);

        btnNewProject.Font = UiStyle.Text;
        btnNewProject.Location = new Point(0, 4);
        btnNewProject.Name = "btnNewProject";
        btnNewProject.Size = new Size(240, 56);
        btnNewProject.TabIndex = 2;
        btnNewProject.Text = "New project";
        btnNewProject.UseVisualStyleBackColor = true;
        btnNewProject.Click += btnNewProject_Click;

        btnExistingProject.Font = UiStyle.Text;
        btnExistingProject.Location = new Point(256, 4);
        btnExistingProject.Name = "btnExistingProject";
        btnExistingProject.Size = new Size(240, 56);
        btnExistingProject.TabIndex = 3;
        btnExistingProject.Text = "Existing project";
        btnExistingProject.UseVisualStyleBackColor = true;
        btnExistingProject.Click += btnExistingProject_Click;

        lblRoot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblRoot.AutoEllipsis = true;
        lblRoot.Font = UiStyle.Text;
        lblRoot.Location = new Point(512, 16);
        lblRoot.Name = "lblRoot";
        lblRoot.Size = new Size(400, 36);
        lblRoot.Text = "No root folder selected";

        panelProjectButtons.Dock = DockStyle.Fill;
        panelProjectButtons.Name = "panelProjectButtons";
        panelProjectButtons.Size = new Size(920, 64);
        panelProjectButtons.Controls.Add(lblRoot);
        panelProjectButtons.Controls.Add(btnExistingProject);
        panelProjectButtons.Controls.Add(btnNewProject);

        lblProjectName.AutoSize = true;
        lblProjectName.Dock = DockStyle.Fill;
        lblProjectName.Font = UiStyle.Text;
        lblProjectName.Name = "lblProjectName";
        lblProjectName.Text = "New project folder name";
        lblProjectName.TextAlign = ContentAlignment.MiddleLeft;

        txtProjectName.Dock = DockStyle.Fill;
        txtProjectName.Font = UiStyle.Text;
        txtProjectName.Name = "txtProjectName";
        txtProjectName.PlaceholderText = "for example MyGame";
        txtProjectName.TabIndex = 4;
        txtProjectName.TextChanged += txtProjectName_TextChanged;

        lblNotes.AutoSize = true;
        lblNotes.Dock = DockStyle.Fill;
        lblNotes.Font = UiStyle.Text;
        lblNotes.Name = "lblNotes";
        lblNotes.Text = "Session notes";
        lblNotes.TextAlign = ContentAlignment.MiddleLeft;

        rtfNotes.Dock = DockStyle.Fill;
        rtfNotes.DetectUrls = true;
        rtfNotes.Font = UiStyle.Text;
        rtfNotes.HideSelection = false;
        rtfNotes.Name = "rtfNotes";
        rtfNotes.TabIndex = 5;
        rtfNotes.TextChanged += rtfNotes_TextChanged;

        lblTarget.AutoEllipsis = true;
        lblTarget.Dock = DockStyle.Fill;
        lblTarget.Font = UiStyle.Text;
        lblTarget.Name = "lblTarget";
        lblTarget.Text = "Select a recent project, open an existing one, or create a new one.";
        lblTarget.TextAlign = ContentAlignment.MiddleLeft;

        btnLaunch.Dock = DockStyle.Fill;
        btnLaunch.Font = UiStyle.Text;
        btnLaunch.Name = "btnLaunch";
        btnLaunch.TabIndex = 6;
        btnLaunch.Text = "Launch";
        btnLaunch.UseVisualStyleBackColor = true;
        btnLaunch.Click += btnLaunch_Click;

        btnLaunchWithSpec.Dock = DockStyle.Fill;
        btnLaunchWithSpec.Font = UiStyle.Text;
        btnLaunchWithSpec.Name = "btnLaunchWithSpec";
        btnLaunchWithSpec.TabIndex = 7;
        btnLaunchWithSpec.Text = "Launch and start With Spec";
        btnLaunchWithSpec.UseVisualStyleBackColor = true;
        btnLaunchWithSpec.Click += btnLaunchWithSpec_Click;

        tableLower.ColumnCount = 1;
        tableLower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableLower.Dock = DockStyle.Fill;
        tableLower.Name = "tableLower";
        tableLower.Padding = new Padding(20, 8, 20, 16);
        tableLower.RowCount = 8;
        tableLower.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        tableLower.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tableLower.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        tableLower.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tableLower.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableLower.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        tableLower.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        tableLower.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
        tableLower.Controls.Add(panelProjectButtons, 0, 0);
        tableLower.Controls.Add(lblProjectName, 0, 1);
        tableLower.Controls.Add(txtProjectName, 0, 2);
        tableLower.Controls.Add(lblNotes, 0, 3);
        tableLower.Controls.Add(rtfNotes, 0, 4);
        tableLower.Controls.Add(lblTarget, 0, 5);
        tableLower.Controls.Add(btnLaunch, 0, 6);
        tableLower.Controls.Add(btnLaunchWithSpec, 0, 7);

        splitMain.Panel2.Controls.Add(tableLower);

        splitMain.Dock = DockStyle.Fill;
        splitMain.FixedPanel = FixedPanel.None;
        splitMain.Name = "splitMain";
        splitMain.Orientation = Orientation.Horizontal;
        splitMain.Panel1MinSize = 180;
        splitMain.Panel2MinSize = 520;
        splitMain.Size = new Size(960, 1008);
        splitMain.SplitterDistance = 400;
        splitMain.SplitterWidth = 14;
        splitMain.TabStop = false;
        splitMain.SplitterMoved += splitMain_SplitterMoved;

        AutoScaleDimensions = new SizeF(15F, 36F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(960, 1008);
        Controls.Add(splitMain);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(900, 900);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Grok Launcher V1.008";
        FormClosing += MainForm_FormClosing;
        Shown += MainForm_Shown;
        panelProjectButtons.ResumeLayout(false);
        tableLower.ResumeLayout(false);
        tableLower.PerformLayout();
        panelRecentHeader.ResumeLayout(false);
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        ResumeLayout(false);
    }
}
