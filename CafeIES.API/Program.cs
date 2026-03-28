using System.Text;
using System.Threading.RateLimiting;
using CafeIES.API.Data;
using CafeIES.API.Hubs;
using CafeIES.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// QuestPDF Community licence — gratuita para proyectos no comerciales / open-source
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ── Base de datos ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Servicios de negocio ──────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HorarioService>();
builder.Services.AddSingleton<StripeService>();
builder.Services.AddScoped<FcmService>();
builder.Services.AddScoped<DesayunoService>();

// ── Almacenamiento de imágenes ────────────────────────────────────────────────
// Si AzureStorage:ConnectionString está configurado → Azure Blob Storage (producción)
// Si no                                             → disco local wwwroot/uploads/ (desarrollo)
if (!string.IsNullOrEmpty(builder.Configuration["AzureStorage:ConnectionString"]))
    builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
else
    builder.Services.AddSingleton<IBlobStorageService, LocalBlobStorageService>();

// ── HTTP clients (para llamadas salientes: FCM, etc.) ─────────────────────────
builder.Services.AddHttpClient("fcm", c =>
    c.Timeout = TimeSpan.FromSeconds(10));

// ── Caché en memoria ──────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Health check ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Compresión de respuestas ──────────────────────────────────────────────────
builder.Services.AddResponseCompression(opts => { opts.EnableForHttps = true; });

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // SignalR necesita el token por query string
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// Protección contra fuerza bruta en endpoints de autenticación.
// Máximo 5 intentos por IP por minuto. Responde con 429 si se supera.
builder.Services.AddRateLimiter(options =>
{
    // Política auth: 5 req/min/IP — para login, registro, refresh
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit          = 5;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Política general: 60 req/min/IP — para el resto de endpoints
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.PermitLimit          = 60;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Política invitaciones: 5 req/min/IP — para validar tokens de invitación
    options.AddFixedWindowLimiter("invitaciones", opt =>
    {
        opt.PermitLimit          = 5;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    // Política pagos: 20 req/min/IP — para crear PaymentIntents (evita abuso de Stripe API)
    options.AddFixedWindowLimiter("pagos", opt =>
    {
        opt.PermitLimit          = 20;
        opt.Window               = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit           = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsync(
            "{\"mensaje\":\"Demasiadas solicitudes. Espera un momento antes de volver a intentarlo.\"}");
    };
});

// ── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval   = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// ── CORS ──────────────────────────────────────────────────────────────────────
// En desarrollo: acepta cualquier origen localhost.
// En producción: añadir la URL del Admin (Azure Static Web Apps) en Cors:AllowedOrigins.
var corsAllowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowAdmin", p =>
        p.SetIsOriginAllowed(origin =>
                origin.StartsWith("https://localhost") ||
                origin.StartsWith("http://localhost") ||
                corsAllowedOrigins.Any(o =>
                    origin.TrimEnd('/').Equals(o.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()
    )
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CaféIES API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http,
        Scheme = "Bearer", BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddControllers();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Migraciones y seed automáticos al arrancar ────────────────────────────────
// Se ejecuta al correr la app (F5). Si la BD no está lista, avisa en el log sin romper.
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var webEnv = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

    // C1: Validar que el WebhookSecret de Stripe esté configurado en producción
    if (!webEnv.IsDevelopment())
    {
        var webhookSecret = config["Stripe:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
            logger.LogCritical("⛔ Stripe:WebhookSecret no está configurado. " +
                "El endpoint /api/pagos/webhook aceptará cualquier petición sin verificar firma. " +
                "Configúralo en Azure > Configuration antes de recibir pagos reales.");
    }

    try
    {
        await db.Database.MigrateAsync();                    // Aplica migraciones pendientes
        await DbSeeder.SeedAdminAsync(db, config, webEnv);   // Crea admin si no existe (FIX-22)
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "⚠️  No se pudo conectar a la BD al arrancar. " +
            "Comprueba la cadena de conexión en appsettings.json.");
        // No lanzamos la excepción: la app arranca igual, Swagger sigue disponible
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();   // Sirve wwwroot/uploads/productos/
app.UseCors("AllowAdmin");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<CafeteriaHub>("/hubs/cafeteria");
app.MapHealthChecks("/health");

app.Run();
