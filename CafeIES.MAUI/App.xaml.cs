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

        // Actualizar cuando cambie el tema del sistema en caliente
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
        var window = new Window(new AppShell());
        // Aplicar tema inicial aquí: RequestedTheme ya está resuelto por la plataforma
        ApplyTheme(RequestedTheme);
        return window;
    }

    private void ApplyTheme(AppTheme theme)
    {
        // Unspecified → el OS no ha declarado preferencia: usar Light como predeterminado
        var palette = theme == AppTheme.Dark ? DarkPalette : LightPalette;
        foreach (var (key, hex) in palette)
            Resources[key] = Color.FromArgb(hex);
    }
}
