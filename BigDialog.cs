namespace GrokLauncher;

internal static class BigDialog
{
    public static void Show(IWin32Window? owner, string text, string title)
    {
        using var form = new Form
        {
            Text = title,
            Font = UiStyle.Text,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = owner is null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(24)
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var messageLabel = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Font = UiStyle.Text,
            Padding = new Padding(0, 0, 0, 20)
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            Font = UiStyle.Text,
            Padding = new Padding(24, 8, 24, 8),
            Anchor = AnchorStyles.None
        };

        form.AcceptButton = okButton;
        layout.Controls.Add(messageLabel, 0, 0);
        layout.Controls.Add(okButton, 0, 1);
        form.Controls.Add(layout);
        UiStyle.ApplyTo(form);

        if (owner is null)
            form.ShowDialog();
        else
            form.ShowDialog(owner);
    }
}
