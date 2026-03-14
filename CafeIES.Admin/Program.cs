using CafeIES.Admin.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<CafeIES.Admin.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// La URL base de la API se lee de wwwroot/appsettings.json para facilitar el cambio
// entre entornos (desarrollo / producción) sin recompilar.
var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? "https://localhost:50658/";

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBase),
    Timeout     = TimeSpan.FromSeconds(20)
});

builder.Services.AddScoped<AdminApiService>();
builder.Services.AddScoped<AuthAdminService>();

await builder.Build().RunAsync();
