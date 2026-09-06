using System.Runtime.InteropServices;

namespace GrokLauncher;

internal static class UiStyle
{
    public const float MinimumPointSize = 18F;

    public static readonly Font Text = new("Segoe UI", MinimumPointSize, FontStyle.Bold, GraphicsUnit.Point);
    static readonly IntPtr NativeFontHandle = Text.ToHfont();

    public static readonly Color FormBack = Color.FromArgb(214, 220, 228);
    public static readonly Color RecentsPanel = Color.FromArgb(188, 198, 210);
    public static readonly Color LowerPanel = Color.FromArgb(228, 232, 238);
    public static readonly Color SplitterBar = Color.FromArgb(36, 46, 60);
    public static readonly Color Surface = Color.White;
    public static readonly Color Ink = Color.FromArgb(12, 16, 22);
    public static readonly Color ButtonFace = Color.FromArgb(248, 250, 252);
    public static readonly Color ButtonHot = Color.FromArgb(220, 228, 238);
    public static readonly Color Border = Color.FromArgb(28, 36, 48);

    const int LvmGetHeader = 0x101F;
    const int WmSetFont = 0x0030;

    public static void ApplyTo(Control root)
    {
        if (root is Form form) form.Text = RevisionHistory.TitleBar; /* caption from revisionHistory.h */
        ApplyTree(root, Text);
        ApplyColors(root);
        ApplyListViewHeaderFonts(root);
    }

    static void ApplyListViewHeaderFonts(Control root)
    {
        if (root is ListView listView) ApplyListViewHeaderFont(listView);
        foreach (Control child in root.Controls)
            ApplyListViewHeaderFonts(child);
    }

    public static void ApplyListViewHeaderFont(ListView listView)
    {
        void SetHeaderFont()
        {
            IntPtr headerHandle = SendMessage(listView.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
            if (headerHandle == IntPtr.Zero) return;
            SendMessage(headerHandle, WmSetFont, NativeFontHandle, (IntPtr)1);
        }

        if (listView.IsHandleCreated)
            SetHeaderFont();
        else
            listView.HandleCreated += (_, _) => SetHeaderFont();
    }

    static void ApplyTree(Control control, Font font)
    {
        control.Font = font;
        foreach (Control child in control.Controls)
            ApplyTree(child, font);
    }

    static void ApplyColors(Control control)
    {
        switch (control)
        {
            case Form form:
                form.BackColor = FormBack;
                form.ForeColor = Ink;
                break;
            case SplitContainer split:
                split.BackColor = SplitterBar; /* the bar between panels */
                split.Panel1.BackColor = RecentsPanel;
                split.Panel2.BackColor = LowerPanel;
                break;
            case ListView list:
                list.BackColor = Surface;
                list.ForeColor = Ink;
                list.BorderStyle = BorderStyle.FixedSingle;
                list.GridLines = true;
                break;
            case TextBox textBox:
                textBox.BackColor = Surface;
                textBox.ForeColor = Ink;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case RichTextBox notes:
                notes.BackColor = Surface;
                notes.ForeColor = Ink;
                notes.BorderStyle = BorderStyle.FixedSingle;
                break;
            case Button button:
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = ButtonFace;
                button.ForeColor = Ink;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 2;
                button.FlatAppearance.MouseOverBackColor = ButtonHot;
                break;
            case Label label:
                if (label.Name == "lblDatePopup") break;
                label.BackColor = Color.Transparent;
                label.ForeColor = Ink;
                break;
            case Panel panel:
                if (panel.Parent is SplitContainer) break; /* recents/lower colors stay */
                panel.BackColor = Color.Transparent;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyColors(child);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);
}
