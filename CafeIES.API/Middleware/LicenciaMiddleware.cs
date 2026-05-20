using Microsoft.EntityFrameworkCore;
using CafeIES.API.Data;
using System.Security.Claims;

namespace CafeIES.API.Middleware;

public class LicenciaMiddleware
{
    private readonly RequestDelegate _next;

    public LicenciaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var path = context.Request.Path.Value ?? "";

        if (path.StartsWith("/api/auth") ||
            path.StartsWith("/api/pagos/webhook") ||
            path.StartsWith("/swagger") ||
            path.StartsWith("/hubs") ||
            path == "/health")
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var institutoIdClaim = context.User.FindFirstValue("institutoId");
            if (int.TryParse(institutoIdClaim, out var institutoId))
            {
                var instituto = await db.Institutos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == institutoId);

                if (instituto?.FechaExpiracion != null &&
                    instituto.FechaExpiracion < DateTime.UtcNow)
                {
                    context.Response.StatusCode = 402;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(
                        "{\"mensaje\":\"Licencia expirada. Contacta con el administrador.\"}");
                    return;
                }
            }
        }

        await _next(context);
    }
}
