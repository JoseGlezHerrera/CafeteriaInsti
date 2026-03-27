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
- [Pendiente de implementar](#pendiente-de-implementar)
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
│   │   ├── PagosController.cs          Crear PaymentIntent, cancelar, webhook Stripe
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
│   │   ├── ProductoDetallePage         Detalle de producto; bloqueado si sin stock
│   │   ├── CarritoPage                 Resumen, banner desayuno gratuito, descuento, TotalEfectivo
│   │   ├── PagamentoWebPage            WebView con Stripe.js
│   │   ├── ConfirmacionPedidoPage      Polling cada 2s; token "gratuito-{num}" sin polling
│   │   ├── PedidosPage                 Historial con chips Hoy/Todo y paginación
│   │   ├── DetallePedidoPage           Detalle en tiempo real vía SignalR
│   │   ├── PerfilPage                  Datos personales, cambio de contraseña
│   │   ├── AdminPedidosPage            Todos los pedidos: filtro por instituto, fecha y estado
│   │   ├── AdminProductosPage          Gestión de productos con imagen
│   │   ├── AdminEditProductoPage       Crear/editar producto (nombre, precio, stock, imagen…)
│   │   ├── AdminUsuariosPage           Panel contextual animado con acciones contextuales
│   │   ├── AdminInvitacionesPage       Crear/listar invitaciones con QR descargable
│   │   ├── AdminHorariosPage           Gestión de franjas horarias por instituto
│   │   └── EmpleadoPedidosPage         Pedidos en curso: filtro por fecha y estado
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
│   │   ├── Productos.razor             CRUD con subida de imagen y campo ComponenteDesayuno
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
│       └── appsettings.json            URL base de la API (configurable sin recompilar)
│
├── CafeIES.Tests/                      ← Tests unitarios (xUnit + EF InMemory)
│   └── ...                             95 tests: HorarioService, AuthService, dominio, validaciones
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
    "Email": "admin@cafeies.com",
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

- `HorarioService.EsHorarioValidoAsync` consulta la BD y usa `TimeOnly.TryParse` seguro.
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

Se configura desde el panel admin (Productos → campo "Desayuno gratuito") o desde MAUI.

### Activación por alumno

El admin activa el flag `DesayunoGratuito` en el perfil del alumno desde:
- **Blazor Admin** → página Usuarios (botón 🍊) o página Desayunos → sección Beneficiarios
- **MAUI Admin** → Gestión de usuarios → panel contextual animado → botón 🍊 Desayuno

### Flujo en la app

1. Al abrir el carrito, se consulta `GET /api/pedidos/desayuno-status`.
2. Si hay desayuno disponible, aparece el banner 🍊 con los componentes restantes del día.
3. El `TotalEfectivo` se calcula client-side descontando la primera unidad elegible de cada componente.
4. Si el total efectivo es **0 €** → flujo gratuito: `POST /api/pedidos` directo, sin Stripe.
5. Si hay parte de pago → `POST /api/pagos/crear-intent` con el descuento ya aplicado en el PaymentIntent.

### Restricciones de seguridad

- Solo **1 unidad** por componente es gratis al día — las adicionales se cobran a precio normal.
- El precio 0 se valida en servidor en una transacción `Serializable`.
- La tabla `ConsumoDesayuno` tiene índice único `(UsuarioId, Fecha)` — previene dobles consumos concurrentes.
- El webhook de Stripe incluye `PrecioUnitario` en metadata para calcular correctamente el total en pedidos mixtos.

### Reporte diario

`GET /api/admin/desayunos/consumos` devuelve el reporte del día. Visible en Blazor Admin → Desayunos.

---

## Pagos con Stripe

### Flujo completo

```
1. Cliente: POST /api/pagos/crear-intent  →  API crea PaymentIntent con total calculado en servidor
                                              (descuento desayuno aplicado si aplica)
2. Cliente: abre WebView con Stripe.js    →  Usuario introduce tarjeta
3. Stripe confirma el pago
4. Cliente: navega INMEDIATAMENTE a ConfirmacionPedidoPage (sin esperar al servidor)
5. Background: POST /api/pedidos         →  crea el pedido en BD
6. ConfirmacionPedidoPage: sondea GET /api/pedidos/by-intent/{id} cada 2s → muestra número
7. Webhook Stripe:                       →  respaldo: crea el pedido si el cliente falló en paso 5
```

Si el total es 0 € (desayuno completamente gratuito), se salta Stripe y se va directamente al paso 5.

### Seguridad

- El total **siempre** lo calcula el servidor — el cliente nunca envía el importe.
- Redondeo correcto a céntimos: `Math.Round(total * 100, MidpointRounding.AwayFromZero)`.
- Rate limiting específico en `POST /api/pagos/crear-intent` (20 req/min/IP).
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
- Panel contextual animado en gestión de usuarios — bottom sheet con ScaleTo + FadeTo overlay

**Catálogo y carrito**
- Catálogo de productos con categorías, filtros y búsqueda
- Carrito de compras con control de cantidad y stock
- Productos agotados bloqueados visualmente (sin tap, opacidad reducida)
- Validación horaria por turno antes de crear el pedido

**Desayuno gratuito**
- Banner 🍊 en el carrito cuando hay desayuno disponible hoy
- Descuento automático: 1 zumo + 1 bocadillo al día para beneficiarios
- Flujo completamente gratuito si el pedido no tiene coste (sin Stripe)
- Consumo único diario validado en servidor con transacción Serializable
- Gestión de beneficiarios desde MAUI (panel contextual) y Blazor Admin

**Pagos**
- **Pago real con Stripe** — flujo instantáneo: confirmación inmediata tras pago, pedido en background
- Pedidos de coste 0 sin pasar por Stripe
- Webhook como respaldo si el cliente falla tras el pago

**Pedidos**
- Historial de pedidos del usuario con chips Hoy/Todo y paginación
- Chips de filtro por fecha y estado en vista de empleado (Hoy/Todo + En curso/Pendiente/En prep.)
- Chips de filtro por instituto, fecha (Hoy/Semana/Todo) y estado en vista admin
- Estado activo visual en todos los chips de fecha y estado (resalte ámbar)
- Botones de acción (Preparar/Listo/Entregar/Cancelar) en forma de píldora con borde semántico
- Detalle de pedido en tiempo real (SignalR)
- Gestión de estado con máquina de estados (transiciones válidas)

**Panel admin web (Blazor WASM) — 10 páginas**
- Dashboard con métricas en tiempo real
- Gestión de Productos (imagen, ComponenteDesayuno)
- Gestión de Categorías
- Gestión de Usuarios con toggle desayuno gratuito 🍊
- **Desayunos**: beneficiarios (buscar/filtrar/toggle) + consumos del día
- Gestión de Pedidos con cambio de estado
- Gestión de Institutos con dirección
- Gestión de Horarios por turno
- Sistema de Invitaciones con QR descargable
- Reportes: Excel (3 hojas) y PDF (límite 1.000 registros)

**Infraestructura**
- **Multi-instituto** — selector en registro, filtros por instituto en admin, claim en JWT
- **Subida de imágenes** — Admin Blazor y MAUI, local (dev) o Azure Blob (prod)
- **Infraestructura Azure operativa** — App Service + SQL + Blob Storage + Static Web Apps
- **CI/CD completo** — GitHub Actions para API, Admin y APK Android (~3 min)
- **95 tests unitarios** — HorarioService, AuthService, dominio, validaciones
- Health check en `/health` para Azure App Service
- Warmup automático al arrancar (ping a `/health` en frío para reducir lag)
- Hard delete de productos con historial conservado (FK nullable `SET NULL`)

---

## Pendiente de implementar

### 🔴 Alta prioridad

#### Push Notifications (FCM)
La infraestructura está preparada pero **no está activa**:
- `FcmService.cs` en la API con FCM HTTP v1 y `GoogleCredential` cacheado en constructor
- `PushNotificationService.cs` en MAUI es un stub vacío (pendiente de activar)
- `DispositivoToken` y `NotificacionesController` implementados

Para activarlo:
1. Crear proyecto en [Firebase Console](https://console.firebase.google.com)
2. Descargar `google-services.json` → `CafeIES.MAUI/Platforms/Android/`
3. Descargar `GoogleService-Info.plist` → `CafeIES.MAUI/Platforms/iOS/`
4. Generar Service Account JSON → Firebase Console → Configuración → Cuentas de servicio
5. Añadir en Azure App Settings: `Fcm__ProjectId` y `Fcm__ServiceAccountJson`
6. Implementar el cuerpo de `PushNotificationService.cs` para registrar el token

Sin push notifications, los usuarios deben abrir la app para saber si su pedido está listo.

### 🟡 Media prioridad

#### Google Play Store
Actualmente la distribución es por GitHub Releases (sideloading). Para Play Store:
- Registrar cuenta Google Play Developer (pago único 25 USD)
- Activar el pipeline de AAB firmado con keystore release (scripts en `infra/`)
- Diseñar icono definitivo y capturas de pantalla
- Publicar en canal de prueba interna

#### Paginación completa en admin
Los endpoints de usuarios admin devuelven todos los registros. Los pedidos admin ya tienen paginación (`page` + `pageSize`); falta extenderla a usuarios.

#### Versionado de API
No hay prefijo de versión (`/api/v1/...`). Cualquier cambio breaking rompe todos los clientes sin posibilidad de migración gradual.

### 🟢 Baja prioridad

#### XAML Compiled Bindings
Warnings de MAUI sobre bindings no compilados en algunas vistas. Añadir `x:DataType` y activar `MauiEnableXamlCBindingWithSourceCompilation` mejoraría el rendimiento de la UI.

#### Tests de integración
Los 95 tests actuales son unitarios. No hay tests de integración que validen los endpoints contra BD real.

#### Icono definitivo de la app
El icono actual es el placeholder por defecto de MAUI.

---

## Roadmap

| Fase | Estado | Descripción |
|---|---|---|
| MVP — Pedidos y catálogo | ✅ Completada | API REST, JWT, MAUI, catálogo, carrito, horarios |
| Panel admin y SignalR | ✅ Completada | Blazor WASM, 8 páginas, tiempo real, invitaciones QR |
| Seguridad y calidad | ✅ Completada | Rate limiting, audit trail, complejidad de contraseña, timeouts |
| Multi-instituto | ✅ Completada | Entidad Instituto, filtros, claim en JWT |
| Stripe + pagos reales | ✅ Completada | PaymentIntent, WebView con Stripe.js, webhook, flujo instantáneo |
| Reportes e imágenes | ✅ Completada | Excel, PDF, subida de imágenes, tests unitarios |
| Azure + CI/CD | ✅ Completada | App Service, SQL, Blob, Static Web Apps, GitHub Actions |
| Distribución Android | ✅ Completada | APK via GitHub Releases, pipeline automatizado |
| Revisión quirúrgica v0.12 | ✅ Completada | 37 tests E2E en prod, 12 bugs corregidos, flujo pago rediseñado |
| Desayuno gratuito | ✅ Completada | Programa escolar: zumo + bocata/día; flujo gratuito sin Stripe |
| Auto-login + UX pulida | ✅ Completada | Sin flash de login, panel contextual animado, filtros por fecha |
| Push Notifications | ⏳ Pendiente | FCM Android + APNs iOS — infraestructura lista, falta activar |
| Google Play Store | ⏳ Pendiente | Requiere cuenta developer (25 USD) |
| Paginación completa en API | ⏳ Pendiente | Listados con page/pageSize en todos los endpoints admin |

---

## Changelog

### v0.13.0 — Desayuno gratuito, UX pulida y auditoría de seguridad (actual)

#### Sistema de desayuno gratuito
- **Nuevo enum `ComponenteDesayuno`** (`Ninguno`/`Zumo`/`Bocata`) en entidad `Producto`
- **Flag `DesayunoGratuito`** en entidad `Usuario`; activable por admin
- **Tabla `ConsumoDesayuno`** con índice único `(UsuarioId, Fecha)` — previene doble consumo concurrente
- **`MetodoPago.Gratuito`** para pedidos de coste 0 (sin Stripe)
- **`GET /api/pedidos/desayuno-status`** — devuelve los componentes disponibles del día
- **`PATCH /api/admin/usuarios/{id}/desayuno-gratuito`** — activa/desactiva beneficiario
- **`GET /api/admin/desayunos/consumos`** — reporte diario de consumos
- Precio 0 validado en servidor en transacción `Serializable`; solo 1 unidad/componente/día
- Metadata en PaymentIntent incluye `PrecioUnitario` para webhook correcto en pedidos mixtos
- **Banner 🍊 en el carrito** cuando hay desayuno disponible
- **Línea de descuento** en el resumen del carrito con `TotalEfectivo` client-side
- `ConfirmacionPedidoPage` maneja token `"gratuito-{numero}"` sin polling a Stripe
- **Admin Blazor — nueva página `/desayunos`**: beneficiarios (buscar/filtrar/toggle) + consumos del día
- **Campo `ComponenteDesayuno`** en modal de producto (Admin Blazor y MAUI)
- **Botón 🍊 en usuarios** en Blazor (página Usuarios) y en MAUI (panel contextual)
- Migración `DesayunoGratuito` aplicada en producción

#### Panel contextual animado en gestión de usuarios (MAUI)
- Tap en tarjeta abre un **bottom sheet** con animación `ScaleTo(1.04)` + `FadeTo` overlay + `TranslateTo` panel
- El panel muestra avatar, nombre, email, rol y estado en badges
- Botones de acción contextuales según el estado del usuario:
  - `Aprobar` / `Rechazar` (usuarios pendientes)
  - `🍊 Desayuno` (alumnos activos — toggle)
  - `Suspender` (usuarios activos)
  - `Reactivar` (usuarios suspendidos)
  - `Eliminar` (cualquier no-admin)
- Cerrar tocando el overlay o el botón Cancelar; tarjeta vuelve a escala 1.0
- Guard `_panelAnimando` con `try-finally` evita doble apertura y garantiza liberación aunque falle una animación

#### Filtro por fecha en vistas de pedidos
- **Chips Hoy / Todo** en `PedidosPage` (usuario) y `EmpleadoPedidosPage` (empleado) — filtrado client-side
- **Chips Hoy / Semana / Todo** en `AdminPedidosPage` — filtrado server-side con parámetro `desde`
- Por defecto todas las vistas muestran solo pedidos del día
- Los **chips de estado** también muestran ahora cuál está seleccionado (resalte ámbar)

#### Auto-login y eliminación del flash de login
- `LoginViewModel.TryAutoLoginAsync` devuelve `bool` — `true` si navegó, `false` si no hay sesión
- El formulario arranca con `Opacity=0` y solo hace `FadeTo(1)` si no hay sesión activa
- Sin sesión: fondo oscuro → formulario aparece con fade suave
- Con sesión: fondo oscuro → pantalla principal directamente, sin ver el formulario

#### Mejoras visuales en pedidos
- Botones de acción (Preparar / Listo / Entregar / Cancelar) en **forma de píldora** (`CornerRadius=50`) con borde semántico — coherentes con el sistema de chips de la app

#### Vulnerabilidades de seguridad corregidas
- **CRÍTICO**: precio 0 se aplicaba a *todas* las unidades; ahora solo la primera unidad/componente/día
- **ALTO**: Personal filtrado por instituto en `/api/pedidos/en-curso` (antes veía todos los institutos)
- **ALTO**: Rate limiting `"pagos"` (20 req/min/IP) en `POST /api/pagos/crear-intent`
- **ALTO**: Guard de instituto en mutaciones de usuario (cross-institute privilege escalation bloqueado)
- **ALTO**: Webhook rechaza con 503 si `WebhookSecret` no configurado
- **MEDIO**: `MaxLength(30)` en `CrearPedidoRequest.Lineas` — previene pedidos abusivos

#### Bugs corregidos
- Race condition en desplegable de institutos: `_institutosCargados = true` fijado antes del `await`
- Guard `if (IsLoading) return` en `CargarAsync` de `PedidosViewModel`, `EmpleadoPedidosViewModel` y `HomeViewModel` — evita duplicados
- Precio gratuito solo aplicado a 1 unidad: `TotalEfectivo` corregido en `CarritoViewModel`
- Actualización optimista de `ZumoDisponible`/`BocataDisponible` + refresh real tras confirmar en background
- `NotifyPropertyChangedFor` en estado de desayuno — precios, descuentos y banner se actualizan reactivamente
- `TryAutoLoginAsync` limpia `HayError`/`ErrorMessage` al inicio — no muestra errores de sesiones anteriores
- N+1 fix en `PedidosController.GetById` — una sola query con includes
- `AdminApiService` lee imagen a bytes antes del retry (stream no consumido)
- Chip `⏳ Pendientes` eliminado del filtro de estado de usuarios (devolvía lista vacía)

---

### v0.12.0 — Flujo de pago instantáneo + revisión quirúrgica completa

#### Flujo de pago rediseñado
- **Confirmación inmediata**: tras el pago, la app navega al instante — sin bloquear al usuario
- El carrito se limpia en el acto; la creación del pedido ocurre en `Task.Run` background
- `ConfirmacionPedidoPage` sondea `GET /api/pedidos/by-intent/{id}` cada 2s hasta mostrar el número
- Webhook de Stripe actúa como respaldo si el cliente no puede completar la llamada background
- Eliminado el estado "Creando pedido…" que bloqueaba la UI 10-45 segundos

#### Hard delete de productos
- `LineaPedido.ProductoId` es ahora `int?` con `DeleteBehavior.SetNull`
- Borrar un producto siempre hace hard delete; el historial conserva nombre y precio del momento
- Migración `NullableProductoIdEnLineas` aplicada en producción

#### Bugs críticos/altos corregidos (revisión línea a línea)
- **A1**: `NullReferenceException` al cancelar pedido con producto eliminado
- **A2**: Redondeo de céntimos Stripe: `(long)(total*100)` → `Math.Round(..., AwayFromZero)`
- **C1**: Log crítico al arrancar si `Stripe:WebhookSecret` no configurado
- **M1**: Double-submit: predicado no traducible a SQL → movido a memoria tras `ToListAsync()`
- **M2**: `PUT /api/productos` restringido a solo `Admin`
- **M6**: `AbrirEditar` en panel Institutos pre-rellena el campo Dirección
- **M8**: `[Range(-1, int.MaxValue)]` en `CrearProductoRequest.Stock`
- **B4**: `GoogleCredential` cacheado en constructor de `FcmService`
- **B9**: Índices añadidos en `Pedidos.Estado` y `DispositivoTokens.UsuarioId`

#### Fixes de datos y rendimiento
- Categoría "Café" con nombre y emoji corruptos corregida via migración SQL directa
- `SignalR.SendAsync` en creación de pedido es fire-and-forget
- Nuevo endpoint `GET /api/pedidos/by-intent/{paymentIntentId}`
- Warmup automático al arrancar — ping a `/health` para reducir lag de cold start
- Timeout `HttpClient` MAUI: 15s → 45s

---

### v0.11.0 — Auditoría de seguridad y calidad completa

Revisión exhaustiva línea por línea. 40 problemas identificados y corregidos:

- **Crítico**: `Task.Result` en `HomeViewModel` → `await Task.WhenAll()` — elimina deadlock potencial
- **Seguridad**: claves JWT/Stripe en placeholders; rate limiting extendido; `DiasValidez` 1–365; `Enum.IsDefined` en MetodoPago; notas XSS sanitizadas; path-traversal con `Path.GetRelativePath`
- **Robustez**: `ReadCommitted` + `[ConcurrencyCheck]`; `TryParse` en horarios; transacción atómica en refresh token; código muerto `ConfirmarPagoAsync` eliminado
- **Calidad**: `DateTime.UtcNow`; compresión HTTP; SignalR keepalive; auto-reconexión tras refresh; cache catálogo 60s; logging en todos los controllers

---

### v0.10.0 — Pipeline CI/CD Android + distribución via GitHub Releases

- `AndroidManifest.xml`: permisos explícitos, `allowBackup="false"`, `network_security_config`
- `network_security_config.xml`: cleartext HTTP bloqueado; solo CAs del sistema
- `proguard.cfg`: reglas R8 para Mono runtime, OkHttp y SignalR
- `infra/generar-keystore.ps1`: genera keystore RSA-2048 de 10.000 días
- `.github/workflows/deploy-android.yml`: pipeline operativo con `global.json` para fijar .NET 9
- `docs/politica-privacidad.html`: página RGPD en GitHub Pages
- Primera distribución: `cafeies-2026.03.23.apk` (14.4 MB)

---

### v0.9.0 — Despliegue Azure completo y pagos verificados en producción

- Recursos Azure: App Service B1 (Linux, northeurope), Azure SQL, Blob Storage, Static Web App
- EF Core migrations aplicadas; seed inicial ejecutado
- Stripe webhook registrado en producción
- CI/CD corregido: `azure/login@v2` + `az webapp deploy --type zip`
- Test end-to-end verificado: PaymentIntent → Stripe `pm_card_visa` → Pedido creado (1.50 EUR)
- `confirmation_method` cambiado a `automatic` (fix crítico para WebView + Stripe.js)

---

### v0.8.0 — Infraestructura Azure y CI/CD

- `IBlobStorageService`: local (dev) y Azure Blob Storage (prod) con selección automática
- Health check `GET /health`; CORS desde `appsettings.Production.json`
- GitHub Actions: `deploy-api.yml` y `deploy-admin.yml`
- `staticwebapp.config.json` para SPA routing de Blazor WASM

---

### v0.7.0 — Notificaciones push FCM (infraestructura)

- `DispositivoToken` para almacenar tokens FCM
- `FcmService` con FCM HTTP v1 y autenticación OAuth2 via Service Account
- `NotificacionesController` para registro/eliminación de tokens

---

### v0.6.0 — Reportes, imágenes y tests

- 95 tests unitarios con xUnit + EF InMemory
- Exportación Excel (ClosedXML) y PDF (QuestPDF)
- Subida de imágenes de productos con protección path-traversal
- Funciones admin desde MAUI: pedidos, productos, usuarios

---

### v0.5.0 — Seguridad y calidad

- Rate limiting en auth, audit trail, complejidad de contraseña, timeouts
- Null-safe claims, timeout HTTP, SSL solo en `#if DEBUG`
- RefreshToken solo en memoria en Blazor; DtoMapperExtensions; ILogger en ApiService

---

### v0.4.0 — Stripe + Multi-instituto

- Pagos reales con Stripe: PaymentIntent + webhook + verificación server-side
- Multi-instituto: entidad Instituto, claim en JWT, filtros en admin

---

### v0.3.0 — Auditoría y estabilización

- 11 bugs corregidos: máquina de estados, modales de confirmación, validaciones, paginación

---

### v0.2.0 — Panel admin y funciones avanzadas

- Panel Blazor WASM completo (8 páginas); SignalR tiempo real; invitaciones QR; dashboard

---

### v0.1.0 — MVP

- API REST con JWT + BCrypt, catálogo, carrito, pedidos, restricción horaria

---

## Licencia

Proyecto privado — uso interno para centros educativos.
