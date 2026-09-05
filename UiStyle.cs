using System.Runtime.InteropServices;

namespace GrokLauncher;

internal static class UiStyle
{
    public const float MinimumPointSize = 18F;

    public static readonly Font Text = new("Segoe UI", MinimumPointSize, FontStyle.Bold, GraphicsUnit.Point);
    static readonly IntPtr NativeFontHandle = Text.ToHfont();

    const int LvmGetHeader = 0x101F;
    const int WmSetFont = 0x0030;

    public static void ApplyTo(Control root)
    {
        if (root is Form form) form.Text = RevisionHistory.TitleBar; /* caption from revisionHistory.h */
        ApplyTree(root, Text);
        if (root is ListView listView)
            ApplyListViewHeaderFont(listView);
        foreach (Control child in root.Controls)
        {
            if (child is ListView childList)
                ApplyListViewHeaderFont(childList);
        }
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

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, IntPtr lParam);
}
