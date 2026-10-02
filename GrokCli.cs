using System.Diagnostics;

namespace GrokLauncher;

internal sealed class GrokLaunch
{
    public required Process HostProcess { get; init; }
    public required string FolderPath { get; init; }
    public required string TitleToken { get; init; }
    public required int FontPoints { get; init; }
}

internal static class GrokCli
{
    public static string? FindExecutable()
    {
        string bundled = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".grok",
            "bin",
            "grok.exe");
        if (File.Exists(bundled)) return bundled;

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable)) return null;

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string candidate = Path.Combine(directory.Trim(), "grok.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    public static bool IsInstalled()
    {
        return FindExecutable() is not null;
    }

    public static void InstallOrUpdate()
    {
        string bundled = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".grok",
            "bin",
            "grok.exe");

        string command;
        if (FindExecutable() is null)
        {
            command =
                "Write-Host 'Installing Grok Build from x.ai ...'; " +
                "irm https://x.ai/cli/install.ps1 | iex; " +
                "Write-Host 'Running grok update ...'; " +
                "& ($env:USERPROFILE + '\\.grok\\bin\\grok.exe') update; " +
                "Write-Host 'Done. You can close this window.';";
        }
        else
        {
            string grokExe = (FindExecutable() ?? bundled).Replace("'", "''");
            command =
                "Write-Host 'Grok Build is already installed. Running grok update ...'; " +
                "& '" + grokExe + "' update; " +
                "Write-Host 'Done. You can close this window.';";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -Command \"" + command + "\"",
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    public static GrokLaunch LaunchInFolder(string folderPath, string? initialPrompt = null, int fontPoints = 28)
    {
        string? grokExe = FindExecutable();
        if (grokExe is null)
        {
            throw new FileNotFoundException(
                "grok.exe was not found. Install Grok Build, then try again.");
        }

        string fullFolder = Path.GetFullPath(folderPath);
        string titleToken = "GL" + Guid.NewGuid().ToString("N")[..8];
        fontPoints = AppSettings.NormalizeFontPoints(fontPoints);
        WindowsTerminalFont.TryEnsureProfile(fontPoints);
        GuestWindow.WriteConsoleFontProperties(titleToken, grokExe, fontPoints);

        Process? process = StartConhost(titleToken, fullFolder, grokExe, initialPrompt);
        if (process is null)
            throw new InvalidOperationException("Could not start Grok Build.");

        return new GrokLaunch
        {
            HostProcess = process,
            FolderPath = fullFolder,
            TitleToken = titleToken,
            FontPoints = fontPoints
        };
    }

    static Process? StartConhost(
        string titleToken,
        string folderPath,
        string grokExe,
        string? initialPrompt)
    {
        string command = $"title {titleToken} && {CmdQuote(grokExe)} --cwd {CmdQuote(folderPath)}";
        if (!string.IsNullOrWhiteSpace(initialPrompt))
            command += " " + QuoteForCmd(initialPrompt);

        string conhost = Path.Combine(Environment.SystemDirectory, "conhost.exe");
        ProcessStartInfo startInfo;
        if (File.Exists(conhost))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = conhost,
                Arguments = "cmd.exe /K " + command,
                WorkingDirectory = folderPath,
                UseShellExecute = true
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/K " + command,
                WorkingDirectory = folderPath,
                UseShellExecute = true
            };
        }

        return Process.Start(startInfo);
    }

    static string CmdQuote(string value)
    {
        string trimmed = value.Trim().Trim('"');
        return "\"" + trimmed + "\"";
    }

    static string QuoteForCmd(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
