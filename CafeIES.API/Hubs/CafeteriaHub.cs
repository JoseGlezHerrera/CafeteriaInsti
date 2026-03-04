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

        // El panel de cafetería/admin se une al grupo "cafeteria"
        if (user.IsInRole("Admin") || user.IsInRole("Personal"))
            await Groups.AddToGroupAsync(Context.ConnectionId, "cafeteria");

        // Todo usuario se une a su grupo personal para recibir updates de sus pedidos
        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }
}
