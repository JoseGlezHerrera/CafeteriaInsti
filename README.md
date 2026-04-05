# CaféIES — Sistema de pedidos de cafetería para institutos

> Aplicación móvil + panel de administración web para gestionar pedidos de cafetería en centros educativos.
> Multi-instituto · Desayuno gratuito · Pago real con Stripe · Tiempo real con SignalR · Infraestructura Azure lista para producción.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-Android%20%7C%20iOS-blue?logo=dotnet)](https://learn.microsoft.com/dotnet/maui/)
[![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-purple?logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![Stripe](https://img.shields.io/badge/Stripe-Pagos-635bff?logo=stripe)](https://stripe.com/)
[![Azure](https://img.shields.io/badge/Azure-Producción-0089D6?logo=microsoftazure)](https://azure.microsoft.com/)
[![CI/CD](https://img.shields.io/badge/CI%2FCD-GitHub%20Actions-2088FF?logo=githubactions)](https://github.com/features/actions)

---

## Tabla de contenidos

- [Arquitectura](#arquitectura)
- [Stack tecnológico](#stack-tecnológico)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Puesta en marcha local](#puesta-en-marcha-local)
- [Despliegue en Azure](#despliegue-en-azure)
- [Flujo de registro de usuarios](#flujo-de-registro-de-usuarios)
- [Lógica de horarios](#lógica-de-horarios)
- [Sistema de desayuno gratuito](#sistema-de-desayuno-gratuito)
- [Pagos con Stripe](#pagos-con-stripe)
- [Tiempo real con SignalR](#tiempo-real-con-signalr)
- [Seguridad](#seguridad)
- [Distribución Android](#distribución-android)
- [Estado actual del proyecto](#estado-actual-del-proyecto)
- [Roadmap](#roadmap)
- [Changelog](#changelog)

---

## Arquitectura

```
┌──────────────────┐    HTTPS / JSON     ┌───────────────────────┐
│  CafeIES.MAUI    │◄───────────────────►│   CafeIES.API         │
│  (Android / iOS) │    SignalR WS        │   ASP.NET Core 9      │
│                  │◄───────────────────►│                       │
└──────────────────┘                     │   SQL Server + EF 9   │
                                         │   JWT + BCrypt 12     │
┌──────────────────┐    HTTPS / JSON     │   SignalR Hub         │
│  CafeIES.Admin   │◄───────────────────►│   Stripe SDK          │
│  (Blazor WASM)   │    SignalR WS        │   Azure Blob Storage  │
└──────────────────┘◄───────────────────►└──────────┬────────────┘
                                                     │
                                          ┌──────────▼────────────┐
              ┌───────────────────────────┤   Servicios externos   │
              │                           │   Stripe (Pagos)       │
              ▼                           │   Azure (Hosting)      │
   ┌──────────────────┐                   └───────────────────────┘
   │  CafeIES.Shared  │
   │  DTOs · Entidades│
   │  Enums · Validac.│
   └──────────────────┘
```

---

## Stack tecnológico

| Componente | Tecnología | Versión |
|---|---|---|
| Backend API | ASP.NET Core | .NET 9 |
| Base de datos | SQL Server + Entity Framework Core | EF Core 9 |
| App móvil | .NET MAUI | .NET 9 (Android, iOS) |
| Panel admin | Blazor WebAssembly | .NET 9 |
| Autenticación | JWT Bearer + BCrypt | workFactor 12 |
| Pagos | Stripe PaymentIntent + Webhook | Stripe.net 50.x |
| Tiempo real | ASP.NET Core SignalR | — |
| Almacenamiento imágenes | Azure Blob Storage (prod) / local (dev) | Azure.Storage.Blobs 12.x |
| Hosting API | Azure App Service (B1, Linux, .NET 9) | — |
| Hosting Admin | Azure Static Web Apps (free tier) | — |
| CI/CD | GitHub Actions | — |
| Reportes | ClosedXML (Excel) + QuestPDF (PDF) | — |
| QR invitaciones | QRCoder | — |
| MVVM (MAUI) | CommunityToolkit.Mvvm | 8.3.x |
| UI helpers (MAUI) | CommunityToolkit.Maui | 9.x |

---

## Estructura del proyecto

```
CafeIES/
├── CafeIES.sln
│
├── CafeIES.Shared/                     ← Modelos compartidos por todos los proyectos
│   ├── Models/
│   │   ├── Entities.cs                 Instituto, Usuario, Producto, Pedido, LineaPedido,
│   │   │                               FranjaHoraria, Invitacion, ConsumoDesayuno,
│   │   │                               DispositivoToken, RefreshToken
│   │   ├── DTOs.cs                     DTOs request/response con Data Annotations
│   │   └── Enums.cs                    Turno, RolUsuario, EstadoPedido, MetodoPago,
│   │                                   ComponenteDesayuno (Ninguno/Zumo/Bocata)
│   └── Validation/
│       └── PasswordComplexityAttribute.cs  Mayúscula + número + símbolo obligatorios
│
├── CafeIES.API/                        ← Backend REST (puerto local 50658)
│   ├── Controllers/
│   │   ├── AuthController.cs           Login, registro alumno/invitado, refresh JWT, logout
│   │   ├── ProductosController.cs      CRUD productos + imagen; filtro por categoría/búsqueda
│   │   ├── CategoriasController.cs     CRUD categorías
│   │   ├── PedidosController.cs        Crear/listar/detalle; máquina de estados; desayuno-status
│   │   ├── PagosController.cs          Crear PaymentIntent (con split gratuito), cancelar, webhook
│   │   ├── AdminController.cs          Usuarios, institutos, invitaciones, horarios, reportes,
│   │   │                               desayunos (consumos + gestión beneficiarios)
│   │   ├── NotificacionesController.cs Registro/eliminación tokens FCM (infraestructura)
│   │   └── EmpleadoController.cs       Pedidos en curso para empleados/personal
│   ├── Data/
│   │   ├── AppDbContext.cs             EF Core context; índices en Pedidos.Estado,
│   │   │                               DispositivoTokens.UsuarioId, ConsumoDesayuno(UsuarioId,Fecha)
│   │   ├── DbSeeder.cs                 Admin, institutos de ejemplo, categorías, franjas horarias
│   │   └── Migrations/                 Historial completo de migraciones EF Core
│   ├── Services/
│   │   ├── AuthService.cs              JWT access+refresh, BCrypt hash, rotación de tokens
│   │   ├── HorarioService.cs           Validación de franja horaria antes de crear pedido
│   │   ├── StripeService.cs            PaymentIntent, cancelación, firma de webhooks
│   │   ├── FcmService.cs               FCM HTTP v1 con GoogleCredential cacheado (infraestructura)
│   │   ├── LocalBlobStorageService.cs  Almacenamiento local (dev) con validación path-traversal
│   │   ├── AzureBlobStorageService.cs  Azure Blob Storage (prod)
│   │   ├── ReporteExcelService.cs      Excel con ClosedXML (pedidos, productos, usuarios)
│   │   └── ReportePdfService.cs        PDF con QuestPDF — limitado a 1.000 registros
│   ├── Extensions/
│   │   ├── ClaimsPrincipalExtensions.cs  GetUserId() null-safe
│   │   └── DtoMapperExtensions.cs        ToDto() centralizado para Usuario, Pedido, FranjaHoraria
│   ├── Hubs/
│   │   └── PedidosHub.cs               SignalR hub — grupos cafeteria y user-{id}
│   └── Program.cs                      DI, middleware, rate limiting (4 políticas), CORS, Swagger
│
├── CafeIES.MAUI/                       ← App móvil Android/iOS
│   ├── Views/                          18 páginas XAML
│   │   ├── LoginPage                   Auto-login transparente; fade-in solo si no hay sesión
│   │   ├── RegistroPage                Registro alumno con instituto y turno
│   │   ├── RegistroInvitacionPage      Registro por enlace/QR de invitación
│   │   ├── HomePage                    Catálogo con categorías, búsqueda y filtros; guard IsLoading
│   │   ├── ProductoDetallePage         Detalle de producto con imagen real (fallback emoji); bloqueado si sin stock
│   │   ├── CarritoPage                 Resumen, spinner desayuno, banner 🍊, descuento, TotalEfectivo
│   │   ├── PagamentoWebPage            WebView con Stripe.js
│   │   ├── ConfirmacionPedidoPage      Polling cada 2s; token "gratuito-{num}" sin polling
│   │   ├── PedidosPage                 Historial con chips Hoy/Todo y paginación
│   │   ├── DetallePedidoPage           Detalle en tiempo real vía SignalR
│   │   ├── PerfilPage                  Datos personales, cambio de contraseña
│   │   ├── AdminPedidosPage            Todos los pedidos: filtro por instituto, fecha y estado; Cargar más paginado
│   │   ├── AdminProductosPage          Gestión de productos con imagen
│   │   ├── AdminEditProductoPage       Crear/editar producto con selector ComponenteDesayuno
│   │   ├── AdminUsuariosPage           Panel contextual animado con acciones contextuales
│   │   ├── AdminInvitacionesPage       Crear/listar invitaciones con QR descargable
│   │   ├── AdminHorariosPage           Gestión de franjas horarias por instituto
│   │   └── EmpleadoPedidosPage         Historial del día: activos (Pendiente/EnPrep) + cerrados (Listo/Entregado/Cancelado)
│   ├── ViewModels/                     MVVM con CommunityToolkit.Mvvm
│   ├── Services/
│   │   ├── ApiService.cs               HTTP client (timeout 45s) + SignalR; warmup a /health
│   │   └── TokenService.cs             SecureStorage para access/refresh token
│   ├── Converters/
│   │   └── Converters.cs               ~30 converters: estado pedido, stock, rol, desayuno, chips…
│   └── Resources/Styles/
│       └── AppStyles.xaml              Paleta dark & warm (ámbar/naranja), tipografía Syne+DMSans
│
├── CafeIES.Admin/                      ← Panel administración Blazor WASM
│   ├── Pages/
│   │   ├── Dashboard.razor             Métricas del día, pedidos recientes, SignalR live
│   │   ├── Pedidos.razor               Lista paginada + cambio de estado
│   │   ├── Productos.razor             CRUD con subida de imagen, badges ComponenteDesayuno 🥤🥪
│   │   ├── Categorias.razor            CRUD categorías
│   │   ├── Usuarios.razor              Lista usuarios + toggle desayuno gratuito 🍊
│   │   ├── Desayunos.razor             Beneficiarios (buscar/filtrar/toggle) + consumos del día
│   │   ├── Institutos.razor            CRUD multi-instituto con dirección
│   │   ├── Horarios.razor              Franjas horarias por instituto y turno
│   │   ├── Invitaciones.razor          Crear invitaciones + QR descargable
│   │   └── Reportes.razor              Exportar Excel/PDF (límite 1.000 registros)
│   ├── Services/
│   │   └── AdminApiService.cs          HTTP client (timeout 20s); imagen a bytes antes de retry
│   └── wwwroot/
│       ├── appsettings.json            URL base de la API (configurable sin recompilar)
│       └── css/app.css                 Estilos custom (ahora correctamente en git)
│
├── CafeIES.Tests/                      ← Tests unitarios (xUnit + EF InMemory)
│   └── ...                             96 tests: HorarioService, AuthService, dominio, validaciones
│
└── .github/workflows/
    ├── deploy-api.yml                  Push a main + API/Shared → Azure App Service (~4 min)
    ├── deploy-admin.yml                Push a main + Admin/Shared → Static Web Apps (~2 min)
    └── deploy-android.yml              Push a main + MAUI/Shared → GitHub Releases APK (~3 min)
```

---

## Puesta en marcha local

### Requisitos

- .NET 9 SDK
- SQL Server (Express, Developer o Docker)
- Visual Studio 2022 / Rider / VS Code con extensión C#
- Android SDK (solo para ejecutar la app móvil)

### 1. Configurar la API

```bash
cd CafeIES.API
```

Crear `appsettings.Development.json` (no commitear):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CafeIES;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "tu-clave-secreta-de-al-menos-32-caracteres",
    "Issuer": "CafeIES",
    "Audience": "CafeIES"
  },
  "Admin": {
    "Email": "admin@cafeies.local",
    "Password": "Admin1234!"
  },
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSecret": "whsec_..."
  },
  "BlobStorage": {
    "UseAzure": false
  }
}
```

```bash
dotnet ef database update
dotnet run
```

La API queda en `https://localhost:50658`. Swagger en `/swagger`.

### 2. Configurar el Admin Blazor

```bash
cd CafeIES.Admin
```

Editar `wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://localhost:50658"
}
```

```bash
dotnet run
```

Panel en `https://localhost:50660`.

### 3. Ejecutar la app MAUI

En `CafeIES.MAUI/Services/ApiService.cs`, la constante `ApiBaseUrl` cambia según la plataforma:

```csharp
#if ANDROID
    private const string ApiBaseUrl = "https://10.0.2.2:50658"; // Emulador Android
#else
    private const string ApiBaseUrl = "https://localhost:50658"; // iOS simulator / Windows
#endif
```

Para dispositivo físico Android, reemplazar `10.0.2.2` por la IP local de tu máquina.

---

## Despliegue en Azure

### Recursos creados

| Recurso | Tipo | Región |
|---|---|---|
| `cafeies-api` | App Service (B1, Linux, .NET 9) | North Europe |
| `cafeies-sql` | Azure SQL Database | North Europe |
| `cafeies-storage` | Storage Account (Blob) | North Europe |
| `cafeies-admin` | Static Web App (Free) | Global |

### Variables de entorno (Azure App Settings)

```
ConnectionStrings__DefaultConnection = <cadena de conexión SQL>
Jwt__Key                             = <clave secreta producción>
Jwt__Issuer                          = CafeIES
Jwt__Audience                        = CafeIES
Admin__Email                         = <email admin>
Admin__Password                      = <contraseña admin>
Stripe__SecretKey                    = sk_live_...
Stripe__PublishableKey               = pk_live_...
Stripe__WebhookSecret                = whsec_...
BlobStorage__UseAzure                = true
BlobStorage__ConnectionString        = <cadena Azure Storage>
BlobStorage__ContainerName           = productos
```

### CI/CD — GitHub Actions

Los tres workflows se disparan automáticamente al hacer push a `main`:

| Workflow | Trigger (paths) | Destino |
|---|---|---|
| `deploy-api.yml` | `CafeIES.API/**`, `CafeIES.Shared/**` | Azure App Service |
| `deploy-admin.yml` | `CafeIES.Admin/**`, `CafeIES.Shared/**` | Azure Static Web Apps |
| `deploy-android.yml` | `CafeIES.MAUI/**`, `CafeIES.Shared/**` | GitHub Releases (APK) |

El APK se versiona automáticamente como `YYYY.MM.<run_number>` y se publica como pre-release en [Releases](https://github.com/JoseGlezHerrera/CafeteriaInsti/releases).

---

## Flujo de registro de usuarios

```
Alumno  ──────────────────────────► /api/auth/registro/alumno
                                     Selecciona instituto y turno
                                     Estado inicial: Pendiente
                                     Admin aprueba desde MAUI o Blazor

Profesor/Personal ── QR o enlace ──► /api/auth/registro/invitado
                                     Invitación válida + no caducada
                                     Estado inicial: Pendiente
                                     Admin aprueba

Admin ────────────────────────────► Seeding inicial (DbSeeder.cs)
                                     Email/password configurados en appsettings
```

### Auto-login al arrancar

Al abrir la app, `LoginViewModel.TryAutoLoginAsync` intenta renovar el token guardado en `SecureStorage`. Si tiene éxito, navega directamente a la pantalla principal sin mostrar el formulario de login. El formulario arranca con `Opacity=0` y solo hace `FadeTo(1)` si no hay sesión activa — eliminando el flash de login.

---

## Lógica de horarios

La API valida que el pedido se realice dentro de la franja horaria asignada al turno del alumno antes de crearlo o de generar el PaymentIntent. La franja horaria es configurable por instituto y turno desde el panel admin.

```
Alumno turno Mañana → puede pedir entre 08:00 y 10:30
Alumno turno Tarde  → puede pedir entre 14:00 y 16:00
Alumno turno Noche  → puede pedir entre 18:00 y 20:00
```

- `HorarioService.PuedePedirAhoraAsync` consulta la BD y usa `TimeOnly.TryParse` seguro.
- Si no hay franja configurada para el turno, el pedido se permite (permisivo por defecto).
- Si la franja no está activa, devuelve 400 con mensaje claro.
- Personal e Invitados no tienen restricción horaria.

---

## Sistema de desayuno gratuito

Programa de desayuno escolar gratuito para alumnos de familias desfavorecidas: **1 zumo + 1 bocadillo al día**, sin pasar por Stripe.

### Configuración de productos

Cada producto puede marcarse con `ComponenteDesayuno`:

| Valor | Significado |
|---|---|
| `Ninguno` | Producto normal (no entra en el programa) |
| `Zumo` | Puede ser el zumo gratuito del día |
| `Bocata` | Puede ser el bocadillo gratuito del día |

Se configura desde:
- **MAUI Admin** → Productos → Editar → selector con tres opciones ❌ / 🥤 / 🥪
- **Blazor Admin** → Productos (visible en tabla con badges de color)

> **Importante**: al añadir un producto nuevo o editar uno existente desde MAUI, hay que configurar `ComponenteDesayuno` para que el descuento se aplique. Si se deja en `Ninguno`, el producto no entra en el programa aunque el alumno sea beneficiario.

### Activación por alumno

El admin activa el flag `DesayunoGratuito` en el perfil del alumno desde:
- **Blazor Admin** → página Usuarios (botón 🍊) o página Desayunos → sección Beneficiarios
- **MAUI Admin** → Gestión de usuarios → panel contextual animado → botón 🍊 Desayuno

### Flujo en la app

1. Al abrir el carrito, se bloquea el botón de pago (`IsLoadingDesayuno = true`) y se consulta `GET /api/pedidos/desayuno-status`.
2. Una vez cargado el estado, el botón se desbloquea. Si hay desayuno disponible, aparece el banner 🍊 con los componentes restantes del día.
3. El `TotalEfectivo` se calcula client-side descontando la primera unidad elegible de cada componente.
4. Si el total efectivo es **0 €** → flujo gratuito: `POST /api/pedidos` directo, sin Stripe.
5. Si hay parte de pago → `POST /api/pagos/crear-intent` con el descuento ya aplicado en el PaymentIntent. El metadata incluye líneas con precios divididos (0 € para la unidad gratuita, precio normal para el resto).

### Restricciones de seguridad

- Solo **1 unidad** por componente es gratis al día — las adicionales se cobran a precio normal.
- El precio 0 se valida en servidor en una transacción `Serializable`.
- La tabla `ConsumoDesayuno` tiene índice único `(UsuarioId, Fecha)` — previene dobles consumos concurrentes.
- El webhook de Stripe detecta líneas a 0 € y marca `ConsumoDesayuno` correctamente aunque la app se haya cerrado antes de crear el pedido.

### Reporte diario

`GET /api/admin/desayunos/consumos` devuelve el reporte del día. Visible en Blazor Admin → Desayunos.

---

## Pagos con Stripe

### Flujo completo

```
1. Cliente: POST /api/pagos/crear-intent  →  API crea PaymentIntent con total calculado en servidor
                                              (descuento desayuno aplicado; metadata con precios split)
2. Cliente: abre WebView con Stripe.js    →  Usuario introduce tarjeta
3. Stripe confirma el pago
4. Cliente: navega INMEDIATAMENTE a ConfirmacionPedidoPage (muestra TotalEfectivo)
5. Background: POST /api/pedidos         →  crea el pedido en BD
6. ConfirmacionPedidoPage: sondea GET /api/pedidos/by-intent/{id} cada 2s → muestra número
7. Webhook Stripe:                       →  respaldo: crea el pedido y marca ConsumoDesayuno
                                              si el cliente falló en paso 5
```

Si el total es 0 € (desayuno completamente gratuito), se salta Stripe y se va directamente al paso 5.

### Seguridad

- El total **siempre** lo calcula el servidor — el cliente nunca envía el importe.
- Redondeo correcto a céntimos: `Math.Round(total * 100, MidpointRounding.AwayFromZero)`.
- Rate limiting específico en `POST /api/pagos/crear-intent` (20 req/min/IP).
- La clave pública de Stripe se inyecta desde configuración del servidor — el cliente nunca la maneja ni la expone en la URL.
- El webhook rechaza con 503 si `WebhookSecret` no está configurado.
- `confirmation_method: automatic` (compatible con Stripe.js en WebView).

---

## Tiempo real con SignalR

- **Dashboard admin**: recibe pedidos nuevos al instante (auto-refresh cada 30s como respaldo).
- **App móvil**: el alumno ve el estado de su pedido actualizado en vivo.
- **Grupos**: `cafeteria` (admins/empleados) y `user-{id}` (usuario específico).
- **Reconexión automática**: si el token se renueva (refresh), SignalR se reconecta si estaba desconectado.
- **Sesión expirada**: `ApiService` desconecta SignalR y navega al login con fallback directo.
- **Keepalive**: `KeepAliveInterval = 15s`, `ClientTimeoutInterval = 30s`.
- **Fire-and-forget**: `SendAsync` en creación de pedido no bloquea la respuesta HTTP.

---

## Seguridad

| Mecanismo | Detalle |
|---|---|
| Contraseñas | BCrypt workFactor 12 |
| Complejidad | Mínimo 8 caracteres + mayúscula + número + símbolo |
| JWT access token | Duración 1 hora, HMAC-SHA256 |
| JWT refresh token | Duración 30 días, rotación en cada uso, guardado atómicamente |
| Auto-refresh | Transparente en MAUI (`ApiService`) y Blazor (`AuthAdminService`) |
| Almacenamiento tokens | MAUI: `SecureStorage`. Blazor: accessToken en `sessionStorage`, refreshToken solo en memoria |
| Rate limiting auth | Política "auth" (10 req/min/IP) en endpoints de autenticación |
| Rate limiting general | Política "general" (60 req/min/IP) en el resto de endpoints |
| Rate limiting invitaciones | Política "invitaciones" (5 req/min/IP) en `/api/invitaciones/validar` |
| Rate limiting pagos | Política "pagos" (20 req/min/IP) en `POST /api/pagos/crear-intent` |
| Audit trail | Acciones admin registradas con prefijo `[AUDIT]` en logs del servidor |
| Pagos | Total calculado en servidor — cliente solo recibe el clientSecret |
| Desayuno gratuito | Precio 0 validado en servidor; solo 1 unidad/componente/día; índice único en ConsumoDesayuno |
| Stock | Transacciones `ReadCommitted` + `[ConcurrencyCheck]` para evitar sobreventa |
| Pedidos | Máquina de estados: solo transiciones válidas permitidas |
| Ownership | Usuarios solo acceden a sus propios pedidos |
| Instituto | Admin solo puede mutar usuarios de su propio instituto (cross-institute bloqueado) |
| Personal | Endpoint `/en-curso` filtrado por instituto igual que Empleado |
| XSS | Notas de pedido sanitizadas antes de persistir |
| Path traversal | `LocalBlobStorageService` usa `Path.GetRelativePath` para validar rutas |
| SSL en desarrollo | `ServerCertificateCustomValidationCallback` solo bajo `#if DEBUG` |
| Secretos | Claves reales en `appsettings.Development.json` (gitignored) o Azure App Settings |
| Invitaciones | `DiasValidez` limitado a 1–365 días |
| MetodoPago | Validado con `Enum.IsDefined` en servidor |
| Líneas de pedido | `MaxLength(30)` en `CrearPedidoRequest.Lineas` — previene pedidos abusivos |
| Stock negativo | `NuevoStock < -1` rechazado explícitamente |
| Webhook | Rechaza con 503 si `WebhookSecret` no configurado |

---

## Distribución Android

El APK se genera automáticamente en GitHub Actions al hacer push a `main` con cambios en `CafeIES.MAUI/**` o `CafeIES.Shared/**`.

### Descarga e instalación

1. Ve a [Releases](https://github.com/JoseGlezHerrera/CafeteriaInsti/releases)
2. Descarga el último `cafeies-X.X.X.apk`
3. En el móvil: **Ajustes → Seguridad → Instalar apps de fuentes desconocidas** → activar para el navegador
4. Abre el APK e instala

> El APK está firmado con la debug key de Android. Es apto para pruebas internas — para distribución en Play Store se necesita firma con keystore release (ver sección Roadmap).

---

## Estado actual del proyecto

### Funcionalidades implementadas y operativas ✅

**Usuarios y acceso**
- Registro de alumnos con selección de turno e instituto
- Registro de profesores/personal mediante invitación QR o enlace
- Login/logout con JWT + refresh automático y transparente
- Auto-login al arrancar sin flash de login (fade-in solo si no hay sesión activa)
- Panel contextual animado en gestión de usuarios — bottom sheet con ScaleTo + FadeTo overlay + TranslateTo panel

**Catálogo y carrito**
- Catálogo de productos con categorías, filtros y búsqueda
- Skeleton loading animado durante la carga del catálogo (tarjetas placeholder pulsantes)
- Tema claro/oscuro adaptativo — sigue la preferencia del sistema automáticamente
- Carrito persistente entre sesiones (via `Preferences`) — se recupera al volver a la app
- Control de cantidad y stock; productos agotados bloqueados visualmente
- Validación horaria por turno antes de crear el pedido
- **Ingredientes personalizables** — switch para extras (on/off) y stepper para cantidades múltiples en base y extras (ej. ×3 jamón); precio recalculado en tiempo real; edición desde el carrito; acceso a la gestión de ingredientes desde la pantalla de Productos (admin y empleados); empleados pueden crear y gestionar ingredientes

**Desayuno gratuito**
- Spinner de carga del estado de desayuno — bloquea el botón de pago hasta tener el estado real
- Banner 🍊 en el carrito cuando hay desayuno disponible hoy
- Descuento automático: 1 zumo + 1 bocadillo al día para beneficiarios
- Flujo completamente gratuito si el pedido no tiene coste (sin Stripe)
- Consumo único diario validado en servidor con transacción Serializable
- Webhook de Stripe actualiza `ConsumoDesayuno` si la app falla tras el pago
- Configuración de `ComponenteDesayuno` directamente desde el formulario MAUI (❌ / 🥤 / 🥪)
- Gestión de beneficiarios desde MAUI (panel contextual) y Blazor Admin

**Pagos**
- Pago real con Stripe — confirmación inmediata tras pago, pedido en background
- Pantalla de confirmación muestra el `TotalEfectivo` (con descuento) — no el precio bruto
- Pedidos de coste 0 sin pasar por Stripe
- Webhook como respaldo si el cliente falla tras el pago; metadata con precios split correctos

**Pedidos**
- Historial con chips Hoy/Todo en vista usuario y empleado (filtrado client-side)
- Chips Hoy/Semana/Todo en vista admin (filtrado server-side con parámetro `desde`)
- Estado activo visual en todos los chips de fecha y estado (resalte ámbar)
- Botones de acción (Preparar/Listo/Entregar/Cancelar) en forma de píldora con borde semántico y animación de press
- Toast de confirmación tras cada cambio de estado (EmpleadoPedidosPage)
- Detalle de pedido en tiempo real (SignalR)
- Historial completo del día para empleados: chips Listo, Entregado y Cancelado además de los activos
- "Cargar más" paginado en AdminPedidosPage cuando hay más de 20 pedidos (modo Todo)
- Gestión de estado con máquina de estados (transiciones válidas)

**Panel admin web (Blazor WASM) — 11 páginas**
- Dashboard con métricas en tiempo real
- Gestión de Productos con imagen, badges ComponenteDesayuno (🥤 Zumo / 🥪 Bocata / —) y asignación de ingredientes personalizables por producto
- Gestión de Ingredientes — catálogo completo con emoji, precio extra, stock, toggle activo/inactivo
- Gestión de Categorías
- Gestión de Usuarios con toggle desayuno gratuito 🍊
- Desayunos: beneficiarios (buscar/filtrar/toggle) + consumos del día
- Gestión de Pedidos con cambio de estado
- Gestión de Institutos con dirección
- Gestión de Horarios por turno
- Sistema de Invitaciones con QR descargable
- Reportes: Excel (3 hojas) y PDF (límite 1.000 registros)

**Infraestructura**
- Multi-instituto — selector en registro, filtros por instituto en admin, claim en JWT
- Subida de imágenes — Admin Blazor y MAUI, local (dev) o Azure Blob (prod); fix: boundary multipart y URLs absolutas corregidas
- Infraestructura Azure operativa — App Service + SQL + Blob Storage + Static Web Apps
- CI/CD completo — GitHub Actions para API, Admin y APK Android (~3 min)
- 108 tests unitarios — HorarioService, AuthService, dominio, validaciones, DesayunoService
- Health check en `/health` para Azure App Service
- Warmup automático al arrancar (ping a `/health` en frío para reducir lag)
- Hard delete de productos con historial conservado (FK nullable `SET NULL`)

---

## Roadmap

| Fase | Estado | Descripción |
|---|---|---|
| MVP — Pedidos y catálogo | ✅ Completada | API REST, JWT, MAUI, catálogo, carrito, horarios |
| Panel admin y SignalR | ✅ Completada | Blazor WASM, 10 páginas, tiempo real, invitaciones QR |
| Seguridad y calidad | ✅ Completada | Rate limiting, audit trail, complejidad de contraseña, timeouts |
| Multi-instituto | ✅ Completada | Entidad Instituto, filtros, claim en JWT |
| Stripe + pagos reales | ✅ Completada | PaymentIntent, WebView con Stripe.js, webhook, flujo instantáneo |
| Reportes e imágenes | ✅ Completada | Excel, PDF, subida de imágenes, tests unitarios |
| Azure + CI/CD | ✅ Completada | App Service, SQL, Blob, Static Web Apps, GitHub Actions |
| Distribución Android | ✅ Completada | APK via GitHub Releases, pipeline automatizado |
| Desayuno gratuito | ✅ Completada | Programa escolar: zumo + bocata/día; flujo gratuito sin Stripe |
| Auto-login + UX pulida | ✅ Completada | Sin flash de login, panel contextual animado, filtros por fecha |
| Desayuno — robustez completa | ✅ Completada | Race condition, webhook, metadata split, ComponenteDesayuno en MAUI |
| Robustez y calidad | ✅ Completada | 96 tests, guards IsLoading, audit logging extendido, validación contraseña client-side, carga paralela |
| Historial staff + imagen detalle | ✅ Completada | Endpoint historial empleados/admin; chips estado completos; imagen real en detalle producto; Cargar más paginado |
| Seguridad pagos + deudas técnicas | ✅ Completada | Stripe pk server-side; transacción RepeatableRead desayuno; CerrarSesionAsync centralizado; tests robustos |
| UX sprint — tema claro, skeleton y accesibilidad | ✅ Completada | Tema claro/oscuro reactivo; skeleton loading; SemanticProperties; animaciones de press; toasts; 108 tests |
| Deuda técnica + Bugs + UX 2ª ronda | ✅ Completada | ApiService partial classes; PedidoCardView; errores servidor en toasts; skeleton en PedidosPage; entrada animada; timer horario |
| Ingredientes personalizables | ✅ Completada | Catálogo de ingredientes; asignación por producto (Base/Quitable/Orden); personalización en MAUI con precio reactivo; snapshot en pedido con SetNull histórico |
| Stepper ingredientes + imágenes | ✅ Completada | Stepper para cantidades múltiples (base y extras); empleados gestionan ingredientes; fix subida de imágenes (multipart boundary); BuildImageUrl soporta Azure Blob |
| UX carrito | ✅ Completada | Fix duplicación visual items; "Editar ingredientes" visible en todos los productos configurables; botones ±  circulares 44×44dp |
| Bugs navegación + UX stepper | ✅ Completada | Fix duplicación PedidosPage (List vs ObservableCollection); ConfirmacionPedidoPage se saca del stack; stepper ingredientes 36×36dp |
| Push Notifications | ⏳ Pendiente | FCM Android + APNs iOS — infraestructura lista, falta activar |
| Google Play Store | ⏳ Pendiente | Requiere cuenta developer (25 USD) + keystore release |
| Paginación completa en API | ⏳ Pendiente | Listados con page/pageSize en todos los endpoints admin |
| Versionado de API | ⏳ Pendiente | Prefijo `/api/v1/...` para migraciones graduales |
| Tests de integración | ⏳ Pendiente | Endpoints contra BD real (actualmente solo unitarios) |

---

## Changelog

### v0.26.0 — Fix duplicación definitivo en PedidosPage (2026-04-05)

#### MAUI
- **`PedidosViewModel`**: reemplaza `ObservableCollection<PedidoDto>` por `List<PedidoDto>` como propiedad observable (`[ObservableProperty]`). `AplicarFiltro` reasigna la referencia completa en lugar de `Clear()` + `Add()`. Al recibir un nuevo objeto como `ItemsSource`, `CollectionView` descarta todo lo renderizado y reconstruye desde cero, eliminando la duplicación visual que ocurría al navegar entre tabs sin refrescar manualmente.

---

### v0.25.0 — Fix persistencia confirmación + stepper ingredientes (2026-04-05)

#### MAUI
- **`ConfirmacionPedidoPage.xaml.cs`**: los botones "Ver mis pedidos" y "Seguir pidiendo" ahora hacen `GoToAsync("..")` antes de cambiar de tab. Esto saca la página del stack de navegación del tab Carrito, de forma que al volver al carrito el usuario ve el carrito vacío en lugar de la pantalla de confirmación otra vez (que permitía pulsar los botones de nuevo generando la sensación de pedido duplicado).
- **`ProductoDetallePage.xaml`**: stepper de personalización de ingredientes rediseñado con botones circulares de 36×36dp — mismo estilo que el carrito, mucho más fáciles de pulsar en móvil.

---

### v0.24.0 — Stepper carrito rediseñado (2026-04-05)

#### MAUI
- **`CarritoPage.xaml`**: controles de cantidad rediseñados como botones circulares de 44×44dp (mínimo recomendado por Apple HIG y Material Design) — botón **−** con borde en AccentColor, botón **+** relleno en AccentColor con icono blanco; cantidad centrada en columna fija de 40dp entre ambos. Elimina el problema de tap impreciso en móvil.

---

### v0.23.0 — Fix duplicación carrito + Editar ingredientes siempre visible (2026-04-05)

#### MAUI
- **`CarritoPage.xaml.cs`**: workaround para bug de MAUI donde `BindableLayout` duplicaba visualmente los items al navegar a la pestaña del carrito. `OnAppearing` reasigna `ItemsSource = null → colección` para forzar un repintado limpio sin afectar los datos del `ObservableCollection`.
- **`ItemCarrito`**: nueva propiedad `TieneConfiguracionIngredientes` — indica si el producto tiene ingredientes configurables, independientemente de si el usuario aplicó alguna modificación. Se propaga desde `producto.Ingredientes?.Count > 0` en `AnadirProducto`, se persiste en `Preferences` y se restaura correctamente.
- **`CarritoPage.xaml`**: el botón "✏️ Editar ingredientes" ahora usa `TieneConfiguracionIngredientes` en lugar de `TieneIngredientes`. Antes solo aparecía si había modificaciones aplicadas; ahora aparece en todos los productos con ingredientes configurables, aunque ninguno se haya modificado.

---

### v0.22.0 — Stepper ingredientes, UX productos y fix imágenes (2026-04-05)

#### Backend
- **`IngredientesController`**: POST, PUT, PATCH/toggle y DELETE ahora permiten rol `Empleado` además de `Admin` — los empleados pueden crear y gestionar ingredientes del catálogo
- **`DtoMapperExtensions`**: `ProductoIngredientes` ordenados alfabéticamente por nombre en lugar del campo `Orden` (que resultaba confuso para los usuarios)
- **Migración `20260404153947_AddCantidadIngredientes`** regenerada con `.Designer.cs` correcto — la versión anterior carecía del atributo `[Migration(...)]` y EF Core 9 nunca la aplicaba; la columna `CantidadMaxima` no existía en producción provocando HTTP 500 en todos los endpoints de productos

#### MAUI
- **`ProductoDetalleViewModel` — stepper base+extras**: `UsaStepper` ahora se activa para ingredientes base Y extras (`CantidadMaxima > 1`); antes solo funcionaba para extras
  - Ingredientes base con stepper arrancan en `Cantidad=1` (1ª unidad gratis); extras con stepper arrancan en `Cantidad=0`
  - `PrecioExtraActivo` correcto: la 1ª unidad base es gratuita, las adicionales tienen precio extra
  - `EsModificado` y construcción del request `IngredienteRequest` corregidos para los 4 casos (base+switch, base+stepper, extra+switch, extra+stepper)
  - Modo edición desde el carrito: `Quitar` en base+stepper → `Cantidad=0`; `Añadir` → `Cantidad = 1 + ir.Cantidad`
- **`ApiService.EnviarConRefreshAsync`**: preserva el `ContentType` completo al convertir a `ByteArrayContent`, incluyendo el parámetro `boundary` de `multipart/form-data`. Sin él, el servidor devolvía 400 y la subida de imagen siempre fallaba
- **`ApiService.BuildImageUrl`**: soporta URLs absolutas (Azure Blob Storage devuelve `https://...` completa) sin prefijárseles dos veces la URL base
- **`AdminEditProductoPage.xaml`**: campo "Orden" eliminado del configurador de ingredientes — solo queda "Máx. unidades"; leyenda actualizada
- **`AdminPerfilPage.xaml`**: tarjeta Ingredientes eliminada del perfil (grid 3→2 columnas); los ingredientes pertenecen a la gestión de productos, no al perfil
- **`AdminProductosPage.xaml` + `AdminProductosViewModel`**: botón "Ingredientes" en cabecera → navega a `AdminIngredientes`
- **`EmpleadoProductosPage.xaml` + `EmpleadoProductosViewModel`**: botón "Ingredientes" en cabecera → misma ruta

#### Blazor Admin
- **`AdminApiService.BuildImageUrl`**: nuevo helper que soporta URLs relativas y absolutas
- **`Productos.razor`**: miniaturas de producto y vista previa del formulario usaban `prod.ImagenUrl` directamente como `src` — la URL relativa `/uploads/...` se resolvía contra el origen del Blazor (no de la API), roto; corregido con `Api.BuildImageUrl()`

---

### v0.21.0 — Ingredientes personalizables end-to-end (2026-04-04)

#### Backend
- **Nuevas entidades**: `Ingrediente` (catálogo), `ProductoIngrediente` (configuración por producto con `EsBase`/`EsQuitable`/`Orden`), `LineaPedidoIngrediente` (snapshot inmutable en el pedido)
- **FK nullable**: `LineaPedidoIngrediente.IngredienteId` es `int?` — EF configura `ON DELETE SET NULL` para preservar el historial aunque se elimine un ingrediente del catálogo
- **Restricción Conflict**: `ON DELETE NO ACTION` en `ProductoIngrediente → Ingrediente`; intentar eliminar un ingrediente asignado devuelve HTTP 409 con mensaje descriptivo
- **`IngredientesController`** (nuevo): CRUD completo — GET (lista + detalle), POST, PUT, PATCH/stock, PATCH/toggle, DELETE con manejo de conflicto; todas las escrituras emiten `[AUDIT]`
- **`ProductosController`**: PUT resincroniza `ProductoIngredientes` al actualizar
- **`PedidosController`**: valida ingredientes contra la config del producto, calcula `extraPorUnidad = Σ PrecioExtra (solo Añadir)`, crea `LineaPedidoIngrediente` por línea; restaura stock de ingredientes al cancelar; desayuno gratuito aplica `PrecioUnitario = 0` incluyendo extras
- **Migración** `20260403183926_AddIngredientesPersonalizables` aplicada

#### MAUI
- **`ProductoDetalleViewModel`**: nuevo `IngredienteSeleccionVm`; switches reactivos; `PrecioConPersonalizacion` se recalcula en tiempo real; `AnadirAlCarritoAsync` pasa las selecciones al carrito
- **`CarritoViewModel`**: `ItemCarrito` con `PrecioExtra`, `Ingredientes`, `IngredientesDescripcion`; `PrecioUnitario = Precio + PrecioExtra`; items con personalización siempre crean nueva entrada; serialización a `Preferences` preserva ingredientes
- **`ProductoDetallePage.xaml`**: sección de ingredientes con Switch por ingrediente, etiqueta de precio extra y recuento reactivo
- **`CarritoPage.xaml`**: muestra `PrecioUnitario` y descripción de modificaciones en cursiva
- **`DetallePedidoPage.xaml`**: `CollectionView` anidado muestra cada modificación (`sin 🥬 Lechuga` / `+ 🧀 Queso extra`) con `AccionIngredienteConverter`
- **Nuevos converters**: `AccionIngredienteConverter` (enum → "sin" / "+") y `ListNotEmptyConverter` registrados en `App.xaml`

#### Blazor Admin
- **Nueva página `/ingredientes`**: tabla con emoji/nombre/precio extra/stock/estado; modal crear/editar; toggle activo/inactivo; eliminar con mensaje de conflicto
- **`Productos.razor`**: sección de ingredientes personalizables en el modal — checkbox de asignación + controles Base/Quitable/Orden por ingrediente; carga en paralelo junto a categorías y alérgenos
- **Sidebar**: enlace 🥬 Ingredientes añadido a la navegación

---

### v0.20.0 — Auditoría completa + correcciones de bugs y tema claro (2026-04-03)

#### Correcciones de bugs
- `PedidosPage` — skeleton de carga ahora se suscribe a `CargarCommand.PropertyChanged` en lugar de `ViewModel.PropertyChanged`: `AsyncRelayCommand.IsRunning` notifica en el propio comando, no en el VM, por lo que la animación pulsante ahora termina correctamente
- `ConfirmacionPedidoPage` — botón "atrás" permanentemente bloqueado tras un pago con Stripe: se añade flag `_pagoCompletado` que se activa cuando el polling encuentra el pedido o agota el timeout (60 s); el usuario ya puede volver al historial sin quedar atrapado

#### Mejoras de tema claro
- `AppShell` — colores de la barra de tabs eran hardcoded oscuros en XAML; ahora se aplican desde código en `ApplyTabBarTheme()` con `RequestedThemeChanged`, adaptándose al tema del sistema
- `DetallePedidoViewModel` — color "dim" de los pasos del pedido cambiado de `#2E2B26` (invisible en fondo claro) a `#7A7468` (gris neutro legible en ambos temas)
- 20 archivos XAML: `StaticResource` en estilos de colores cambiados a `DynamicResource` para que el cambio de tema sea instantáneo sin reiniciar la app

---

### v0.19.0 — Deuda técnica, bugs/robustez y UX 2ª ronda (2026-04-02)

#### Deuda técnica (T-01..T-06)
- `ApiService.cs` (~1100 líneas) refactorizado en 6 clases parciales por dominio: Auth, Pagos, Catalog, Pedidos, Admin
- `PedidoCardView` — ContentView reutilizable con events tipados `EventHandler<PedidoDto>`; usado en AdminPedidosPage y EmpleadoPedidosPage
- Tema claro/oscuro: reemplazados `AppThemeColor` (incompatibles con XamlC) por `<Color>` + código en `App.xaml.cs`
- `MauiEnableXamlCBindingWithSourceCompilation=true` — elimina 12 avisos XC0025 de compiled bindings
- `OperatingSystem.IsAndroidVersionAtLeast(35)` guard en `MainActivity.cs` — resuelve CA1422
- `#pragma warning disable CS0649` en `PushNotificationService` — campo `_currentToken` intencionalmente sin asignar

#### Bugs / Robustez (B-01..B-06)
- `CambiarEstadoPedidoAsync` ahora devuelve `(bool Ok, string? Error)` — se muestra el mensaje real del servidor en los diálogos
- `PedidosViewModel.CargarMasAsync` envuelto en try/finally — `IsCargandoMas` siempre se resetea aunque falle la red
- `PedidosPage.OnAppearing` — null-check de `Shell.Current` en la recuperación de PaymentIntent pendiente
- Toast en `EmpleadoPedidosViewModel` envueltos en try-catch (COMException en Windows/unpackaged)
- `AdminEditProductoViewModel` — `IsNullOrEmpty` en lugar de `is not null` para `ImagenUrl`; evita pasar `""` a `BuildImageUrl`
- Skeleton de `HomePage` solo arranca si `IsLoading=true`; se para/reanuda via `PropertyChanged`

#### UX / Calidad visual (U-01..U-06)
- `LoginPage` — `SemanticProperties` en heading, campos de texto y los 3 botones
- `PedidosPage` — `EmptyView` oculto mientras `CargarCommand.IsRunning` (ya no aparece "Sin pedidos" durante la carga inicial)
- `ProductoDetallePage` — animación de entrada fade+slide (280ms, `CubicOut`) al abrir la página
- `CarritoPage` — press animation en botón Pagar (`ScaleTo 94%` + rebote); `InputTransparent` en el ScrollView mientras `IsLoading`
- `PedidosPage` — skeleton loading con 3 tarjetas placeholder y animación pulsante (igual que `HomePage`)
- `HomeViewModel` — `PeriodicTimer` cada 60 s que refresca solo el banner de horario sin recargar el catálogo

---

### v0.18.0 — UX sprint: tema claro, skeleton loading, accesibilidad y animaciones

#### Tema claro/oscuro adaptativo
- La app detecta el tema del sistema (claro u oscuro) al arrancar y cuando el usuario lo cambia
- 7 colores de UI se actualizan dinámicamente vía `DynamicResource` + `RequestedThemeChanged` en `App.xaml.cs`
- Paleta clara: fondos crema (`#FAF8F5`/`#FFFFFF`), bordes suaves (`#E5DFD7`), texto carbón (`#1A1614`)
- Paleta oscura: fondos casi-negro (`#0F0E0C`/`#1A1916`), texto cálido (`#F2EDE6`) — misma que v0.17
- Los 20 archivos XAML ya usaban `DynamicResource`, por lo que el cambio es instantáneo y sin parpadeo

#### Skeleton loading en HomePage
- El `ActivityIndicator` de carga del catálogo fue reemplazado por un grid 2×2 de tarjetas placeholder con `BoxView`
- Las tarjetas skeleton replican exactamente la estructura del producto real (zona imagen 110px + 3 líneas de texto)
- Animación pulsante (opacidad 35%→100%, 900ms, `SinInOut`, infinita) activada en `OnAppearing`

#### Accesibilidad — SemanticProperties
- `SemanticProperties.Description` en buscador de HomePage, botón carrito y botón "+" añadir al carrito
- `SemanticProperties.HeadingLevel` en los dos títulos principales de HomePage
- `SemanticProperties.Hint` en los 4 botones de acción de EmpleadoPedidosPage (Preparar / Listo / Entregar / Cancelar)

#### Animaciones de interacción en EmpleadoPedidosPage
- Press animation (`ScaleTo 88%` + rebote) en todos los botones de acción
- Toast de confirmación (`CommunityToolkit.Maui.Alerts.Toast`) tras cada cambio de estado exitoso
- Arreglados 4 avisos XC0022 (Picker sin `x:DataType`) en RegistroPage, RegistroInvitacionPage, AdminPedidosPage y AdminEditProductoPage

#### Robustez de pagos (BUG-F: recuperación de pago incompleto)
- `CarritoViewModel` persiste el `PaymentIntentId` en `Preferences` al iniciar un pago
- Si la app se cierra durante el pago y reabre, `PedidosPage.OnAppearing` detecta el PI pendiente y redirige a `ConfirmacionPedidoPage`
- Añadido catch de `SqlException 1205` (deadlock SQL Server) en `PedidosController` — responde 503 con mensaje amigable

#### Tests
- 12 nuevos tests para `DesayunoService` (108 en total, 0 errores)

---

### v0.17.0 — Seguridad pagos, deudas técnicas y robustez de tests

#### Seguridad Stripe (BUG-E)
- La clave pública de Stripe (`pk`) ya no viaja en la URL de la página de pago — el servidor la lee de `Stripe:PublishableKey` (configuración) y la valida con regex antes de inyectarla en el HTML
- Ambas claves (`pk` y `cs`) se sanitizan con regex antes de insertarse en el `<script>` — previene XSS si llegaran valores maliciosos
- `PagamentoWebPage` simplificada: solo pasa `?cs=` en la URL

#### Race condition desayuno gratuito (BUG-D)
- `POST /api/pagos/crear-intent`: la lectura del estado de desayuno usa `IsolationLevel.RepeatableRead` — reduce la ventana temporal en la que dos requests simultáneos podrían leer "no consumido" y aplicar el beneficio dos veces
- La validación definitiva continúa siendo la transacción `Serializable` en `POST /api/pedidos` (FIX-02 existente)

#### Cierre de sesión explícito (D-4)
- Al cambiar la contraseña con éxito, se muestra un `DisplayAlert` informativo antes de cerrar la sesión — el usuario no se queda desconectado sin aviso
- Nuevo método `ApiService.CerrarSesionAsync()`: desconecta SignalR, limpia tokens, envía `SesionExpiradaMessage` y navega a login — reutilizable desde cualquier ViewModel

#### Robustez ViewModels y tests (D-3, D-5)
- `HomeViewModel.CargarAsync()`: eliminado guard manual `if (IsLoading) return;` — `AsyncRelayCommand(AllowConcurrentExecutions=false)` ya previene re-entradas de forma segura
- `FranjaHorariaTests`: añadidos guards `if (DateTime.Now.Hour is 0 or 23) return;` en 2 tests — evitan falsos negativos a las 23:xx cuando `AddMinutes(+30)` cruza la medianoche

---

### v0.16.0 — Historial staff, imagen detalle y paginación admin

#### Historial completo para empleados (F-3)
- Nuevo endpoint `GET /api/pedidos/historial` (Empleado/Personal/Admin): devuelve hasta 200 pedidos del día en todos los estados, filtrado por instituto para no-admin
- `EmpleadoPedidosPage` reemplaza "pedidos en curso" por historial completo del día: chips activos (Pendiente/En preparación) + chips cerrados (Listo, Entregado, Cancelado)
- `ApiService.GetHistorialStaffAsync()` añadido

#### Imagen real en detalle de producto (BUG-H)
- `ProductoDetallePage` muestra la imagen real del producto cuando está disponible; fallback al emoji si no hay imagen
- `ProductoDetalleViewModel`: propiedades calculadas `ImagenUrlCompleta` (URL absoluta) y `TieneImagen` para controlar visibilidad

#### Paginación "Cargar más" en admin (D-1)
- `AdminPedidosPage`: `CollectionView.Footer` con botón "Cargar más" y `ActivityIndicator`, visibles solo cuando `HayMas = true` (modo Todo, hay más páginas en servidor)

#### Corrección guards IsLoading en ViewModels (BUG-F)
- `AdminPedidosViewModel` y `EmpleadoPedidosViewModel`: eliminado `[ObservableProperty] _isLoading` y sus guards manuales
- `IsRefreshing` del `RefreshView` bindeado a `CargarCommand.IsRunning` — el framework gestiona el ciclo de vida del spinner sin conflictos

---

### v0.15.0 — Robustez: 96 tests, guards IsLoading, audit logging y UX polish

#### Fiabilidad de ViewModels (MAUI)
- Todos los `CargarAsync` usan `try/finally { IsLoading = false; }` — el spinner se desactiva siempre, incluso si la red falla
- Guard `if (IsLoading) return;` en todos los ViewModels de carga: evita race conditions entre SignalR y llamadas directas
- ViewModels afectados: `EmpleadoPedidosViewModel`, `AdminPedidosViewModel`, `AdminHorariosViewModel`, `AdminProductosViewModel`, `AdminEditProductoViewModel`, `EmpleadoProductosViewModel`, `AdminUsuariosViewModel`, `HomeViewModel`, `PedidosViewModel`, `ProductoDetalleViewModel`, `DetallePedidoViewModel`, `AdminInvitacionesViewModel`

#### Audit logging extendido
- `AuthController`: log `[AUDIT]` en login exitoso, login fallido y cuenta bloqueada — con email e IP
- `InvitacionesController`: log `[AUDIT]` al crear y revocar invitaciones

#### Guards anti-doble-clic (Blazor Admin)
- `Institutos.razor`: `_guardando` en botón Guardar y `_toggling` en ToggleActivo — el botón se deshabilita durante la petición

#### Rendimiento (Blazor Admin)
- `Dashboard.razor`, `Pedidos.razor`, `Usuarios.razor`: carga paralela con `Task.WhenAll` — institutos + datos principales en una sola espera
- `PerfilViewModel` (MAUI): horario y estadísticas en paralelo

#### Validación de contraseña client-side (MAUI)
- `RegistroViewModel` y `RegistroInvitacionViewModel`: validación local antes de llamar a la API — mínimo 8 caracteres + mayúscula + número + símbolo, alineado con `PasswordComplexityAttribute` del servidor
- Eliminado el ejemplo de contraseña `(ej: Admin1234!)` del placeholder de la pantalla de registro

#### Tests
- Test `PuedePedirAhora_FranjaBloqueoActiva_RetornaDenegado` añadido y funcionando — cobertura del modelo "permisivo por defecto + ventanas de bloqueo" de `HorarioService`
- Total: **96 tests**, todos en verde

---

### v0.14.0 — Desayuno gratuito: robustez completa + ComponenteDesayuno en MAUI

#### ComponenteDesayuno en el formulario de producto MAUI
- **Nueva UI** en `AdminEditProductoPage`: selector de tres opciones (❌ Ninguno / 🥤 Zumo / 🥪 Bocata) con resalte por color al seleccionar
- El ViewModel carga `prod.ComponenteDesayuno` al editar y lo envía en `CrearProductoRequest` al guardar
- Hasta ahora, guardar un producto desde MAUI reseteaba siempre `ComponenteDesayuno` a `Ninguno` — bug crítico que impedía configurar el zumo como gratuito desde el móvil

#### Corrección del flujo de desayuno gratuito

**Race condition en el carrito**:
- `OnAppearing` lanzaba `CargarDesayunoStatusAsync` de forma fire-and-forget — el usuario podía pulsar "Pagar" antes de saber si tenía desayuno disponible
- Solución: propiedad `IsLoadingDesayuno` que bloquea inmediatamente el botón de pago hasta tener el estado real. `PuedePulsarPagar = !IsLoading && !IsLoadingDesayuno`

**Metadata Stripe con precios incorrectos**:
- `PagosController.CrearIntent` siempre almacenaba `producto.Precio` (precio completo) en la metadata, incluso para la unidad gratuita
- Si la app se cerraba tras pagar, el webhook reconstruía el pedido con precios completos (sin descuento) y no marcaba `ConsumoDesayuno`
- Solución: la metadata ahora almacena líneas divididas — `(ProductoId, 1, 0€)` para la unidad gratuita y `(ProductoId, resto, precio)` para las demás

**Webhook no actualizaba ConsumoDesayuno**:
- `ReconstruirPedidoAsync` no leía ni actualizaba `ConsumoDesayuno` — un usuario podía volver a usar el beneficio gratuito si la app fallaba tras el pago
- Solución: el webhook ahora carga/crea `ConsumoDesayuno` y marca `ZumoConsumido`/`BocataConsumido` cuando detecta una línea con `PrecioUnitario == 0`

**Total incorrecto en pantalla de confirmación (Stripe)**:
- `FinalizarPagoAsync` capturaba `Total` (precio bruto sin descuento) en lugar de `TotalEfectivo` (precio real cobrado)
- La pantalla de confirmación mostraba el precio sin descuento aunque Stripe solo hubiera cobrado el precio con descuento
- Solución: cambiado a `TotalEfectivo` para mostrar el importe real

**`HayDesayunoDisponible` visible con descuento 0**:
- Si los productos del carrito tenían `ComponenteDesayuno.Ninguno`, la fila de descuento mostraba "-0.00 €"
- Solución: `HayDesayunoDisponible` ahora requiere `Descuento > 0`

#### Admin panel Blazor
- Tabla de Productos: columna "Desayuno 🍊" con badges de color: `🥤 Zumo` (azul) / `🥪 Bocata` (verde) / `—`
- Filtro "Solo desayuno" para ver solo productos del programa
- **Fix `.gitignore`**: patrón `wwwroot/` global excluía los CSS del admin — el panel desplegaba sin estilos en producción

#### Carrito persistente
- El carrito se serializa a `Preferences` al modificarse y se restaura al volver a la app
- Incluye `ComponenteDesayuno` de cada item para que el cálculo del descuento sea correcto tras restaurar

---

### v0.13.0 — Desayuno gratuito, UX pulida y auditoría de seguridad

#### Sistema de desayuno gratuito (base)
- Enum `ComponenteDesayuno` (`Ninguno`/`Zumo`/`Bocata`) en entidad `Producto`
- Flag `DesayunoGratuito` en `Usuario`; activable por admin
- Tabla `ConsumoDesayuno` con índice único `(UsuarioId, Fecha)`
- `MetodoPago.Gratuito` para pedidos de coste 0 (sin Stripe)
- `GET /api/pedidos/desayuno-status` — componentes disponibles del día
- `PATCH /api/admin/usuarios/{id}/desayuno-gratuito` — activar/desactivar beneficiario
- Banner 🍊, línea de descuento y `TotalEfectivo` en el carrito
- Nueva página `/desayunos` en Blazor Admin: beneficiarios + consumos del día

#### Panel contextual animado (MAUI)
- Bottom sheet con animación `ScaleTo(1.04)` + `FadeTo` overlay + `TranslateTo` panel
- Botones contextuales según estado: Aprobar/Rechazar/🍊 Desayuno/Suspender/Reactivar/Eliminar
- Guard `_panelAnimando` con `try-finally` evita doble apertura

#### Filtro por fecha en vistas de pedidos
- Chips Hoy/Todo en `PedidosPage` y `EmpleadoPedidosPage` (filtrado client-side)
- Chips Hoy/Semana/Todo en `AdminPedidosPage` (filtrado server-side con `desde`)
- Chips de estado con resalte ámbar para el seleccionado

#### Seguridad corregida
- **CRÍTICO**: precio 0 se aplicaba a todas las unidades — ahora solo a la primera unidad/componente/día
- **ALTO**: Personal filtrado por instituto en `/api/pedidos/en-curso`
- **ALTO**: Rate limiting "pagos" en `POST /api/pagos/crear-intent`
- **ALTO**: Guard de instituto en mutaciones de usuario (cross-institute bloqueado)
- **ALTO**: Webhook rechaza con 503 si `WebhookSecret` no configurado
- **MEDIO**: `MaxLength(30)` en `CrearPedidoRequest.Lineas`

---

### v0.12.0 — Flujo de pago instantáneo + revisión quirúrgica

- Confirmación inmediata tras pago — sin bloquear la UI; pedido creado en background
- Webhook Stripe como respaldo si el cliente no puede crear el pedido
- `GET /api/pedidos/by-intent/{paymentIntentId}` para polling en confirmación
- Hard delete de productos con FK nullable `SET NULL` en historial
- Bugs corregidos: redondeo Stripe, double-submit SQL, `NullReferenceException` con producto eliminado, 9 más

---

### v0.11.0 — Auditoría de seguridad y calidad completa

- 40 problemas corregidos: deadlock `Task.Result`, rate limiting extendido, `ReadCommitted` + `ConcurrencyCheck`, `TryParse` horarios, notas XSS sanitizadas, path-traversal, SignalR keepalive, cache catálogo 60s

---

### v0.10.0 — Pipeline CI/CD Android

- `AndroidManifest.xml`, `network_security_config.xml`, `proguard.cfg`
- `deploy-android.yml`: APK debug versionado `YYYY.MM.<run>` en GitHub Releases
- `infra/generar-keystore.ps1` para keystore release futuro
- `docs/politica-privacidad.html` en GitHub Pages (RGPD)

---

### v0.9.0 — Despliegue Azure + pagos verificados en producción

- Recursos Azure: App Service B1, Azure SQL, Blob Storage, Static Web App
- Stripe webhook registrado en producción; test E2E 1.50 EUR verificado
- CI/CD corregido con `azure/login@v2` + `az webapp deploy --type zip`

---

### v0.4.0 — v0.8.0

- **v0.8.0**: `IBlobStorageService` local/Azure; health check; GitHub Actions API + Admin
- **v0.7.0**: Infraestructura FCM (tokens, FcmService, NotificacionesController)
- **v0.6.0**: 96 tests unitarios; reportes Excel/PDF; subida de imágenes
- **v0.5.0**: Rate limiting auth; audit trail; complejidad contraseña; null-safe claims
- **v0.4.0**: Stripe PaymentIntent + webhook; multi-instituto

---

### v0.1.0 — v0.3.0

- **v0.3.0**: 11 bugs corregidos: máquina de estados, modales, validaciones, paginación
- **v0.2.0**: Panel Blazor WASM (8 páginas); SignalR; invitaciones QR; dashboard
- **v0.1.0**: MVP — API REST, JWT + BCrypt, catálogo, carrito, pedidos, horarios

---

## Licencia

Proyecto privado — uso interno para centros educativos.
