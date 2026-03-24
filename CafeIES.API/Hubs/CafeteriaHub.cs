using CafeIES.Shared.Models;
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

        // FIX-09: Usar nameof(RolUsuario.*) en lugar de magic strings
        if (user.IsInRole(nameof(RolUsuario.Admin)) || user.IsInRole(nameof(RolUsuario.Personal)) || user.IsInRole(nameof(RolUsuario.Empleado)))
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
