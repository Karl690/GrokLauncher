using System.Reflection;
using System.Text.RegularExpressions;

namespace GrokLauncher;

internal static class RevisionHistory
{
    public const string DefaultTitle = "Grok Launcher";
    public const string DefaultRevision = "V1.000";

    static readonly Regex DefinePattern = new(
        @"^\s*#define\s+(APP_TITLE|APP_REVISION)\s+""([^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Title { get; }
    public static string Revision { get; }
    public static string TitleBar { get; }

    static RevisionHistory()
    {
        string title = DefaultTitle;
        string revision = DefaultRevision;
        TryReadDefines(ref title, ref revision);
        Title = title;
        Revision = revision;
        TitleBar = title + " " + revision;
    }

    static void TryReadDefines(ref string title, ref string revision)
    {
        Assembly assembly = typeof(RevisionHistory).Assembly;
        string? resourceName = null;
        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith("revisionHistory.h", StringComparison.OrdinalIgnoreCase))
                resourceName = name;
        }

        if (resourceName is null) return;

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            Match match = DefinePattern.Match(line);
            if (!match.Success) continue;
            if (match.Groups[1].Value == "APP_TITLE") title = match.Groups[2].Value;
            if (match.Groups[1].Value == "APP_REVISION") revision = match.Groups[2].Value;
        }
    }
}
