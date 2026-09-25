using System.Diagnostics;

namespace GrokLauncher;

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

    public static void LaunchInFolder(string folderPath, string? initialPrompt = null)
    {
        string? grokExe = FindExecutable();
        if (grokExe is null)
        {
            throw new FileNotFoundException(
                "grok.exe was not found. Install Grok Build, then try again.");
        }

        string fullFolder = Path.GetFullPath(folderPath);
        if (TryLaunchWithWindowsTerminal(fullFolder, grokExe, initialPrompt)) return;
        LaunchWithCommandPrompt(fullFolder, grokExe, initialPrompt);
    }

    static bool TryLaunchWithWindowsTerminal(string folderPath, string grokExe, string? initialPrompt)
    {
        try
        {
            string arguments = $"-d {Quote(folderPath)} -- {Quote(grokExe)} --cwd {Quote(folderPath)}";
            if (!string.IsNullOrWhiteSpace(initialPrompt))
                arguments += " " + Quote(initialPrompt);

            var startInfo = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = arguments,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void LaunchWithCommandPrompt(string folderPath, string grokExe, string? initialPrompt)
    {
        string command = $"title Grok && {Quote(grokExe)} --cwd {Quote(folderPath)}";
        if (!string.IsNullOrWhiteSpace(initialPrompt))
            command += " " + QuoteForCmd(initialPrompt);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/K " + command,
            WorkingDirectory = folderPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    static string Quote(string value)
    {
        string trimmed = value.Trim().Trim('"');
        string escaped = trimmed.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }

    static string QuoteForCmd(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
