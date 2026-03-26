using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class InstitutosController : ControllerBase
{
    private readonly AppDbContext _db;
    public InstitutosController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lista los institutos activos — endpoint público para las pantallas de registro.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<InstitutoDto>>> GetInstitutos()
    {
        var institutos = await _db.Institutos
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitutoDto(i.Id, i.Nombre, i.CodigoCorto, i.Activo, i.Direccion))
            .ToListAsync();
        return Ok(institutos);
    }
}
