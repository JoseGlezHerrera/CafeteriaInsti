namespace CafeIES.MAUI.Services;

/// <summary>Implementación vacía para plataformas que no admiten impresión (iOS, Windows).</summary>
public class NoOpPrintService : IPrintService
{
    public Task ImprimirAsync(string htmlContent, string jobName) => Task.CompletedTask;
}
