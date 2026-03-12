using CafeIES.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Tablas ───────────────────────────────────────────────────────────────
    public DbSet<Instituto>    Institutos    => Set<Instituto>();
    public DbSet<Usuario>      Usuarios      => Set<Usuario>();
    public DbSet<FranjaHoraria> FranjasHorarias => Set<FranjaHoraria>();
    public DbSet<Invitacion>   Invitaciones  => Set<Invitacion>();
    public DbSet<Categoria>    Categorias    => Set<Categoria>();
    public DbSet<Producto>     Productos     => Set<Producto>();
    public DbSet<Pedido>       Pedidos       => Set<Pedido>();
    public DbSet<LineaPedido>  LineasPedido  => Set<LineaPedido>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // ── Instituto ─────────────────────────────────────────────────────────
        mb.Entity<Instituto>(e =>
        {
            e.HasIndex(i => i.CodigoCorto).IsUnique();
        });

        // ── Usuario ──────────────────────────────────────────────────────────
        mb.Entity<Usuario>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Rol).HasConversion<int>();
            e.Property(u => u.Estado).HasConversion<int>();
            e.Property(u => u.Turno).HasConversion<int?>();

            e.HasOne(u => u.Instituto)
             .WithMany(i => i.Usuarios)
             .HasForeignKey(u => u.InstitutoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── FranjaHoraria ────────────────────────────────────────────────────
        mb.Entity<FranjaHoraria>(e =>
        {
            e.Property(f => f.Turno).HasConversion<int>();
        });

        // ── Invitacion ───────────────────────────────────────────────────────
        mb.Entity<Invitacion>(e =>
        {
            e.HasIndex(i => i.Token).IsUnique();
            e.Property(i => i.Tipo).HasConversion<int>();
        });

        // ── Producto ─────────────────────────────────────────────────────────
        mb.Entity<Producto>(e =>
        {
            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Pedido ───────────────────────────────────────────────────────────
        mb.Entity<Pedido>(e =>
        {
            e.HasOne(p => p.Usuario)
             .WithMany(u => u.Pedidos)
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.Estado).HasConversion<int>();
            e.Property(p => p.MetodoPago).HasConversion<int>();

            // Índice para buscar pedidos de un usuario rápido
            e.HasIndex(p => new { p.UsuarioId, p.FechaCreacion });
        });

        // ── LineaPedido ──────────────────────────────────────────────────────
        mb.Entity<LineaPedido>(e =>
        {
            e.HasOne(l => l.Pedido)
             .WithMany(p => p.Lineas)
             .HasForeignKey(l => l.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Producto)
             .WithMany(p => p.Lineas)
             .HasForeignKey(l => l.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Seed: Institutos iniciales ────────────────────────────────────────
        mb.Entity<Instituto>().HasData(
            new Instituto { Id = 1, Nombre = "IES Instituto 1", CodigoCorto = "IES-1", Direccion = "" },
            new Instituto { Id = 2, Nombre = "IES Instituto 2", CodigoCorto = "IES-2", Direccion = "" },
            new Instituto { Id = 3, Nombre = "IES Instituto 3", CodigoCorto = "IES-3", Direccion = "" }
        );

        // ── Seed: Categorías iniciales ───────────────────────────────────────
        mb.Entity<Categoria>().HasData(
            new Categoria { Id = 1, Nombre = "Bocadillos",  Emoji = "🥖", Orden = 1 },
            new Categoria { Id = 2, Nombre = "Ensaladas",   Emoji = "🥗", Orden = 2 },
            new Categoria { Id = 3, Nombre = "Bebidas",     Emoji = "🥤", Orden = 3 },
            new Categoria { Id = 4, Nombre = "Postres",     Emoji = "🍰", Orden = 4 },
            new Categoria { Id = 5, Nombre = "Café",        Emoji = "☕", Orden = 5 }
        );

        // ── Seed: Franjas horarias por defecto ───────────────────────────────
        mb.Entity<FranjaHoraria>().HasData(
            // Mañana: antes de entrar + recreo
            new FranjaHoraria { Id = 1, Turno = Turno.Manana, Descripcion = "Antes de entrar", HoraInicio = "07:30", HoraFin = "08:00" },
            new FranjaHoraria { Id = 2, Turno = Turno.Manana, Descripcion = "Recreo",          HoraInicio = "11:00", HoraFin = "11:30" },
            // Tarde
            new FranjaHoraria { Id = 3, Turno = Turno.Tarde,  Descripcion = "Antes de entrar", HoraInicio = "13:45", HoraFin = "14:00" },
            new FranjaHoraria { Id = 4, Turno = Turno.Tarde,  Descripcion = "Recreo",          HoraInicio = "17:00", HoraFin = "17:30" },
            // Noche
            new FranjaHoraria { Id = 5, Turno = Turno.Noche,  Descripcion = "Antes de entrar", HoraInicio = "20:45", HoraFin = "21:00" },
            new FranjaHoraria { Id = 6, Turno = Turno.Noche,  Descripcion = "Recreo",          HoraInicio = "23:00", HoraFin = "23:20" }
        );
    }
}
