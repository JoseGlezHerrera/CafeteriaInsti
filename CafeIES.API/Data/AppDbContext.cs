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
    public DbSet<LineaPedido>       LineasPedido      => Set<LineaPedido>();
    public DbSet<DispositivoToken>  DispositivoTokens => Set<DispositivoToken>();
    public DbSet<Alergeno>          Alergenos         => Set<Alergeno>();
    public DbSet<ConsumoDesayuno>   ConsumoDesayunos  => Set<ConsumoDesayuno>();

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

        // ── Alergeno ─────────────────────────────────────────────────────────
        mb.Entity<Alergeno>(e =>
        {
            e.HasMany(a => a.Productos)
             .WithMany(p => p.Alergenos)
             .UsingEntity(j => j.ToTable("ProductoAlergeno"));
        });

        // ── Producto ─────────────────────────────────────────────────────────
        mb.Entity<Producto>(e =>
        {
            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.ComponenteDesayuno).HasConversion<int>();
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

            // Índice para filtrar por estado (panel admin / cola de preparación)
            e.HasIndex(p => p.Estado);

            // Unicidad de ReferenciasPago (PaymentIntentId de Stripe) — evita doble pedido
            // si el webhook llega dos veces antes de que el primero haga commit.
            e.HasIndex(p => p.ReferenciasPago)
             .IsUnique()
             .HasFilter("[ReferenciasPago] IS NOT NULL");
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
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // ── DispositivoToken ─────────────────────────────────────────────────
        mb.Entity<DispositivoToken>(e =>
        {
            e.HasOne(t => t.Usuario)
             .WithMany()
             .HasForeignKey(t => t.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            // Un token FCM es único por dispositivo (no por usuario)
            e.HasIndex(t => t.Token).IsUnique();

            // Índice para buscar todos los tokens de un usuario (notificaciones push)
            e.HasIndex(t => t.UsuarioId);
        });

        // ── ConsumoDesayuno ───────────────────────────────────────────────────
        mb.Entity<ConsumoDesayuno>(e =>
        {
            e.HasOne(c => c.Usuario)
             .WithMany()
             .HasForeignKey(c => c.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            // Un solo registro por usuario y día (unicidad que evita doble consumo)
            e.HasIndex(c => new { c.UsuarioId, c.Fecha }).IsUnique();
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

        // ── Seed: Franjas horarias bloqueadas (horarios de clase) ───────────
        mb.Entity<FranjaHoraria>().HasData(
            new FranjaHoraria { Id = 1, Turno = Turno.Manana, Descripcion = "Horario de clase", HoraInicio = "08:00", HoraFin = "14:00", EsBloqueada = true },
            new FranjaHoraria { Id = 2, Turno = Turno.Tarde,  Descripcion = "Horario de clase", HoraInicio = "14:30", HoraFin = "20:30", EsBloqueada = true },
            new FranjaHoraria { Id = 3, Turno = Turno.Noche,  Descripcion = "Horario de clase", HoraInicio = "21:00", HoraFin = "03:00", EsBloqueada = true }
        );

        // ── Seed: 14 alérgenos UE (Reglamento 1169/2011) ─────────────────────
        mb.Entity<Alergeno>().HasData(
            new Alergeno { Id = 1,  Nombre = "Gluten",       Emoji = "🌾" },
            new Alergeno { Id = 2,  Nombre = "Crustáceos",   Emoji = "🦐" },
            new Alergeno { Id = 3,  Nombre = "Huevo",        Emoji = "🥚" },
            new Alergeno { Id = 4,  Nombre = "Pescado",      Emoji = "🐟" },
            new Alergeno { Id = 5,  Nombre = "Cacahuetes",   Emoji = "🥜" },
            new Alergeno { Id = 6,  Nombre = "Soja",         Emoji = "🫘" },
            new Alergeno { Id = 7,  Nombre = "Lácteos",      Emoji = "🥛" },
            new Alergeno { Id = 8,  Nombre = "Frutos secos", Emoji = "🌰" },
            new Alergeno { Id = 9,  Nombre = "Apio",         Emoji = "🌿" },
            new Alergeno { Id = 10, Nombre = "Mostaza",      Emoji = "🌻" },
            new Alergeno { Id = 11, Nombre = "Sésamo",       Emoji = "🌱" },
            new Alergeno { Id = 12, Nombre = "Sulfitos",     Emoji = "🍷" },
            new Alergeno { Id = 13, Nombre = "Altramuces",   Emoji = "🌼" },
            new Alergeno { Id = 14, Nombre = "Moluscos",     Emoji = "🦑" }
        );
    }
}
