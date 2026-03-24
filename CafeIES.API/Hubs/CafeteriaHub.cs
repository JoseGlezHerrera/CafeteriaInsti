using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CafeIES.API.Hubs;

/// <summary>
/// Hub de SignalR para notificaciones en tiempo real.
/// 
/// Grupos:
///   "cafeteria"     → recibe todos los pedidos nuevos (panel de cafetería/admin)
///   "user-{userId}" → recibe actualizaciones del estado de sus pedidos (app MAUI)
/// </summary>
[Authorize]
public class CafeteriaHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User!;

        // El panel de cafetería/admin/empleado se une a su grupo de instituto
        // Admins globales (sin institutoId) van al grupo "cafeteria-global" (ven todos los pedidos)
        // Staff con instituto van al grupo "cafeteria-{institutoId}" (solo su instituto)
        if (user.IsInRole("Admin") || user.IsInRole("Personal") || user.IsInRole("Empleado"))
        {
            var institutoIdStr = user.FindFirst("institutoId")?.Value;
            var grupo = int.TryParse(institutoIdStr, out var iid) && iid > 0
                ? $"cafeteria-{iid}"
                : "cafeteria-global";
            await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
        }

        // Todo usuario se une a su grupo personal para recibir updates de sus pedidos
        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }
}
