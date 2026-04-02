namespace CafeIES.MAUI;

public partial class App : Application
{
    // ── Paletas de color ─────────────────────────────────────────────────────
    private static readonly Dictionary<string, string> DarkPalette = new()
    {
        ["BgColor"]      = "#0F0E0C",
        ["SurfaceColor"] = "#1A1916",
        ["Surface2Color"]= "#232119",
        ["CardColor"]    = "#1E1C18",
        ["BorderColor"]  = "#2E2B26",
        ["TextColor"]    = "#F2EDE6",
        ["MutedColor"]   = "#7A7468",
    };

    private static readonly Dictionary<string, string> LightPalette = new()
    {
        ["BgColor"]      = "#FAF8F5",
        ["SurfaceColor"] = "#FFFFFF",
        ["Surface2Color"]= "#F2EEE8",
        ["CardColor"]    = "#FFFFFF",
        ["BorderColor"]  = "#E5DFD7",
        ["TextColor"]    = "#1A1614",
        ["MutedColor"]   = "#9E978F",
    };

    public App()
    {
        InitializeComponent();

        // Aplicar tema inicial según preferencia del sistema
        ApplyTheme(RequestedTheme);

        // Actualizar cuando cambie el tema del sistema
        RequestedThemeChanged += (_, e) => ApplyTheme(e.RequestedTheme);

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

    private void ApplyTheme(AppTheme theme)
    {
        var palette = theme == AppTheme.Light ? LightPalette : DarkPalette;
        foreach (var (key, hex) in palette)
            Resources[key] = Color.FromArgb(hex);
    }
}
