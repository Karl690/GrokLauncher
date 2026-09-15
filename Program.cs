namespace GrokLauncher;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) => LogFatal(eventArgs.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            LogFatal(eventArgs.ExceptionObject as Exception);

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception exception)
        {
            LogFatal(exception);
        }
    }

    static void LogFatal(Exception? exception)
    {
        if (exception is null) return;

        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GrokLauncher");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "startup-error.txt"),
                DateTime.Now.ToString("o") + Environment.NewLine + exception);
        }
        catch (Exception)
        {
            /* still show the dialog */
        }

        MessageBox.Show(exception.ToString(), "Grok Launcher");
    }
}
