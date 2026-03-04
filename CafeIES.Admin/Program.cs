using CafeIES.Admin.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<CafeIES.Admin.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:50658/")
});

builder.Services.AddScoped<AdminApiService>();
builder.Services.AddScoped<AuthAdminService>();

await builder.Build().RunAsync();