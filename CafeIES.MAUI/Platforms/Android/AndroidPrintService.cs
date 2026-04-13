using Android.Content;
using Android.Print;
using Android.Webkit;
using CafeIES.MAUI.Services;

namespace CafeIES.MAUI.Platforms.Android;

/// <summary>
/// Implementación Android de IPrintService.
/// Carga el HTML en un WebView oculto y delega al PrintManager nativo de Android,
/// que soporta impresoras WiFi, Bluetooth y exportación a PDF.
/// </summary>
public class AndroidPrintService : IPrintService
{
    public Task ImprimirAsync(string htmlContent, string jobName)
    {
        return MainThread.InvokeOnMainThreadAsync(() =>
        {
            var activity = Platform.CurrentActivity;
            if (activity is null) return;

            var webView = new global::Android.Webkit.WebView(activity);
            webView.Settings.JavaScriptEnabled = false;

            // Mantener referencia hasta que termine de imprimir
            webView.SetWebViewClient(new TicketPrintWebViewClient(activity, jobName, webView));
            webView.LoadDataWithBaseURL(null, htmlContent, "text/html", "UTF-8", null);
        });
    }

    // ── WebViewClient interno ─────────────────────────────────────────────────

    private sealed class TicketPrintWebViewClient : WebViewClient
    {
        private readonly global::Android.App.Activity _activity;
        private readonly string _jobName;
        // Mantener WebView vivo hasta OnPageFinished para evitar GC prematuro
        private readonly global::Android.Webkit.WebView _webView;

        public TicketPrintWebViewClient(
            global::Android.App.Activity activity,
            string jobName,
            global::Android.Webkit.WebView webView)
        {
            _activity = activity;
            _jobName  = jobName;
            _webView  = webView;
        }

        public override void OnPageFinished(global::Android.Webkit.WebView? view, string? url)
        {
            if (view is null) return;

            var pm = (PrintManager?)_activity.GetSystemService(Context.PrintService);
            if (pm is null) return;

            var adapter = view.CreatePrintDocumentAdapter(_jobName);
            // Usar A5 (148×210 mm) como tamaño de referencia. En la previsualización de Android
            // todo el contenido aparece en una sola página sin truncar, independientemente del
            // número de líneas o ingredientes del pedido. La impresora térmica real puede
            // ajustar su propio tamaño; este valor sólo afecta al diálogo de impresión.
            var attrs   = new PrintAttributes.Builder()
                .SetMediaSize(PrintAttributes.MediaSize.IsoA5)
                .SetMinMargins(PrintAttributes.Margins.NoMargins)
                .Build();

            pm.Print(_jobName, adapter, attrs);
        }
    }
}
