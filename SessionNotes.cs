namespace GrokLauncher;

internal static class SessionNotes
{
    public const string Template =
        "Goals:\n\n\nDont Break:\n\n\nDone When:\n\n";

    public static string RtfFilePath(string projectFolder)
    {
        return Path.Combine(projectFolder, "docs", "SOP.rtf");
    }

    public static string TextFilePath(string projectFolder)
    {
        return Path.Combine(projectFolder, "docs", "SOP.txt");
    }

    public static void LoadTemplate(RichTextBox notesBox)
    {
        notesBox.Text = Template;
        ApplyReadableFont(notesBox);
    }

    public static void LoadInto(RichTextBox notesBox, string? projectFolder)
    {
        if (!string.IsNullOrWhiteSpace(projectFolder))
        {
            string rtfPath = RtfFilePath(projectFolder);
            if (File.Exists(rtfPath))
            {
                notesBox.LoadFile(rtfPath);
                ApplyReadableFont(notesBox);
                return;
            }

            string textPath = TextFilePath(projectFolder);
            if (File.Exists(textPath))
            {
                notesBox.Text = File.ReadAllText(textPath);
                ApplyReadableFont(notesBox);
                return;
            }
        }

        LoadTemplate(notesBox);
    }

    public static void SaveFrom(RichTextBox notesBox, string projectFolder)
    {
        string docsFolder = Path.Combine(projectFolder, "docs");
        Directory.CreateDirectory(docsFolder);
        notesBox.SaveFile(RtfFilePath(projectFolder));
        File.WriteAllText(TextFilePath(projectFolder), notesBox.Text.Replace("\n", Environment.NewLine));
    }

    public static string BuildSpecPrompt(string notesText)
    {
        string flattened = notesText
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\n", " | ")
            .Trim();
        if (flattened.Length > 4000) flattened = flattened.Substring(0, 4000) + " ...";

        return "Help me write a spec for this sitting. Use the /design skill. " +
               "Read docs/SOP.txt for the full session notes if that file exists. " +
               "Write a functional spec with name, inputs, outputs, pass, fail, and out of scope. " +
               "Ask questions if anything is missing, then write the spec. " +
               "Session notes: " + flattened;
    }

    public static void ApplyReadableFont(RichTextBox notesBox)
    {
        int caret = notesBox.SelectionStart;
        int selectionLength = notesBox.SelectionLength;
        notesBox.SelectAll();
        notesBox.SelectionFont = UiStyle.Text;
        notesBox.Select(caret, selectionLength);
        notesBox.Font = UiStyle.Text;
    }
}
