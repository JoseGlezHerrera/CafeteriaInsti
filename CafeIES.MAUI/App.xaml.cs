namespace CafeIES.MAUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Captura de excepciones no controladas — escribir a archivo para diagnóstico
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var msg = e.ExceptionObject?.ToString() ?? "Unknown";
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "cafeies_crash.txt");
            File.AppendAllText(path, $"[{DateTime.Now}] UNHANDLED:\n{msg}\n\n");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "cafeies_crash.txt");
            File.AppendAllText(path, $"[{DateTime.Now}] TASK:\n{e.Exception}\n\n");
            e.SetObserved();
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
