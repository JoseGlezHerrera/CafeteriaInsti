namespace CafeIES.MAUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Captura de excepciones no controladas — escribir a archivo para diagnóstico
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                var msg = e.ExceptionObject?.ToString() ?? "Unknown";
                var path = Path.Combine(FileSystem.AppDataDirectory, "cafeies_crash.txt");
                File.AppendAllText(path, $"[{DateTime.Now}] UNHANDLED:\n{msg}\n\n");
            }
            catch { /* no propagar */ }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            try
            {
                var path = Path.Combine(FileSystem.AppDataDirectory, "cafeies_crash.txt");
                File.AppendAllText(path, $"[{DateTime.Now}] TASK:\n{e.Exception}\n\n");
            }
            catch { /* no propagar */ }
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
