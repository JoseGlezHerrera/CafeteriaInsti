namespace CafeIES.MAUI.Services;

public interface IPrintService
{
    Task ImprimirAsync(string htmlContent, string jobName);
}
