namespace GrokLauncher;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    private Label lblRecent;
    private ListView lstRecent;
    private Button btnRemoveRecent;
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
        lblRecent = new Label();
        lstRecent = new ListView();
        btnRemoveRecent = new Button();
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
        SuspendLayout();

        Font = UiStyle.Text;

        lblRecent.AutoSize = true;
        lblRecent.Font = UiStyle.Text;
        lblRecent.Location = new Point(20, 16);
        lblRecent.Name = "lblRecent";
        lblRecent.Text = "Recent projects (last 10)";

        lstRecent.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lstRecent.Font = UiStyle.Text;
        lstRecent.FullRowSelect = true;
        lstRecent.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        lstRecent.HideSelection = false;
        lstRecent.Location = new Point(20, 60);
        lstRecent.MultiSelect = false;
        lstRecent.Name = "lstRecent";
        lstRecent.Size = new Size(920, 200);
        lstRecent.TabIndex = 0;
        lstRecent.UseCompatibleStateImageBehavior = false;
        lstRecent.View = View.Details;
        lstRecent.Columns.AddRange(new ColumnHeader[]
        {
            new ColumnHeader { Text = "Project", Width = 280 },
            new ColumnHeader { Text = "Folder", Width = 610 }
        });
        lstRecent.SelectedIndexChanged += lstRecent_SelectedIndexChanged;
        lstRecent.DoubleClick += lstRecent_DoubleClick;
        lstRecent.KeyDown += lstRecent_KeyDown;

        btnRemoveRecent.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRemoveRecent.Font = UiStyle.Text;
        btnRemoveRecent.Location = new Point(700, 8);
        btnRemoveRecent.Name = "btnRemoveRecent";
        btnRemoveRecent.Size = new Size(240, 48);
        btnRemoveRecent.TabIndex = 1;
        btnRemoveRecent.Text = "Remove from list";
        btnRemoveRecent.UseVisualStyleBackColor = true;
        btnRemoveRecent.Click += btnRemoveRecent_Click;

        btnNewProject.Font = UiStyle.Text;
        btnNewProject.Location = new Point(20, 276);
        btnNewProject.Name = "btnNewProject";
        btnNewProject.Size = new Size(240, 56);
        btnNewProject.TabIndex = 2;
        btnNewProject.Text = "New project";
        btnNewProject.UseVisualStyleBackColor = true;
        btnNewProject.Click += btnNewProject_Click;

        btnExistingProject.Font = UiStyle.Text;
        btnExistingProject.Location = new Point(276, 276);
        btnExistingProject.Name = "btnExistingProject";
        btnExistingProject.Size = new Size(240, 56);
        btnExistingProject.TabIndex = 3;
        btnExistingProject.Text = "Existing project";
        btnExistingProject.UseVisualStyleBackColor = true;
        btnExistingProject.Click += btnExistingProject_Click;

        lblRoot.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblRoot.AutoEllipsis = true;
        lblRoot.Font = UiStyle.Text;
        lblRoot.Location = new Point(532, 288);
        lblRoot.Name = "lblRoot";
        lblRoot.Size = new Size(408, 36);
        lblRoot.Text = "No root folder selected";

        lblProjectName.AutoSize = true;
        lblProjectName.Font = UiStyle.Text;
        lblProjectName.Location = new Point(20, 348);
        lblProjectName.Name = "lblProjectName";
        lblProjectName.Text = "New project folder name";

        txtProjectName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtProjectName.Font = UiStyle.Text;
        txtProjectName.Location = new Point(20, 392);
        txtProjectName.Name = "txtProjectName";
        txtProjectName.PlaceholderText = "for example MyGame";
        txtProjectName.Size = new Size(920, 40);
        txtProjectName.TabIndex = 4;
        txtProjectName.TextChanged += txtProjectName_TextChanged;

        lblNotes.AutoSize = true;
        lblNotes.Font = UiStyle.Text;
        lblNotes.Location = new Point(20, 448);
        lblNotes.Name = "lblNotes";
        lblNotes.Text = "Session notes";

        rtfNotes.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        rtfNotes.DetectUrls = true;
        rtfNotes.Font = UiStyle.Text;
        rtfNotes.HideSelection = false;
        rtfNotes.Location = new Point(20, 492);
        rtfNotes.Name = "rtfNotes";
        rtfNotes.Size = new Size(920, 300);
        rtfNotes.TabIndex = 5;
        rtfNotes.TextChanged += rtfNotes_TextChanged;

        lblTarget.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lblTarget.AutoEllipsis = true;
        lblTarget.Font = UiStyle.Text;
        lblTarget.Location = new Point(20, 804);
        lblTarget.Name = "lblTarget";
        lblTarget.Size = new Size(920, 40);
        lblTarget.Text = "Select a recent project, open an existing one, or create a new one.";

        btnLaunch.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        btnLaunch.Font = UiStyle.Text;
        btnLaunch.Location = new Point(20, 852);
        btnLaunch.Name = "btnLaunch";
        btnLaunch.Size = new Size(920, 64);
        btnLaunch.TabIndex = 6;
        btnLaunch.Text = "Launch";
        btnLaunch.UseVisualStyleBackColor = true;
        btnLaunch.Click += btnLaunch_Click;

        btnLaunchWithSpec.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        btnLaunchWithSpec.Font = UiStyle.Text;
        btnLaunchWithSpec.Location = new Point(20, 924);
        btnLaunchWithSpec.Name = "btnLaunchWithSpec";
        btnLaunchWithSpec.Size = new Size(920, 64);
        btnLaunchWithSpec.TabIndex = 7;
        btnLaunchWithSpec.Text = "Launch and start With Spec";
        btnLaunchWithSpec.UseVisualStyleBackColor = true;
        btnLaunchWithSpec.Click += btnLaunchWithSpec_Click;

        AutoScaleDimensions = new SizeF(15F, 36F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(960, 1008);
        Controls.Add(btnLaunchWithSpec);
        Controls.Add(btnLaunch);
        Controls.Add(lblTarget);
        Controls.Add(rtfNotes);
        Controls.Add(lblNotes);
        Controls.Add(txtProjectName);
        Controls.Add(lblProjectName);
        Controls.Add(lblRoot);
        Controls.Add(btnExistingProject);
        Controls.Add(btnNewProject);
        Controls.Add(btnRemoveRecent);
        Controls.Add(lstRecent);
        Controls.Add(lblRecent);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        MinimumSize = new Size(900, 900);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Grok Launcher V1.001";
        FormClosing += MainForm_FormClosing;
        ResumeLayout(false);
        PerformLayout();
    }
}
