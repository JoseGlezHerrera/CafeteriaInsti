# CaféIES — Sistema de pedidos de cafetería para institutos

> App móvil + panel de administración para gestionar pedidos de cafetería en centros educativos.
> **Multi-instituto** con **pago real (Stripe)**, **notificaciones push (FCM)** e **infraestructura Azure lista para producción** (App Service + SQL + Blob Storage + Static Web Apps + GitHub Actions CI/CD).

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-Android%20%7C%20iOS%20%7C%20Windows-blue?logo=dotnet)](https://learn.microsoft.com/dotnet/maui/)
[![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-purple?logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![Stripe](https://img.shields.io/badge/Stripe-Pagos-635bff?logo=stripe)](https://stripe.com/)
[![Firebase](https://img.shields.io/badge/Firebase-Push%20FCM-FFCA28?logo=firebase&logoColor=black)](https://firebase.google.com/)

---

## Tabla de contenidos

- [Arquitectura](#arquitectura)
- [Stack tecnológico](#stack-tecnológico)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Puesta en marcha](#puesta-en-marcha)
- [Despliegue en Azure](#despliegue-en-azure)
- [Flujo de registro](#flujo-de-registro)
- [Lógica de horarios](#lógica-de-horarios)
- [Notificaciones en tiempo real](#notificaciones-en-tiempo-real-signalr)
- [Notificaciones push](#notificaciones-push-fcm)
- [Seguridad](#seguridad)
- [Estado actual del proyecto](#estado-actual-del-proyecto)
- [Roadmap — Próximos hitos](#roadmap--próximos-hitos)
- [Changelog](#changelog)

---

## Arquitectura

```
┌─────────────────┐     HTTPS/JSON      ┌──────────────────────┐
│   CafeIES.MAUI  │◄───────────────────►│    CafeIES.API       │
│  (Android/iOS/   │     SignalR WS      │  (ASP.NET Core 9)    │
│   Windows)       │◄──────────────────►│                      │
└────────┬────────┘                      │  SQL Server + EF Core│
         │  FCM Push (recibe)            │  JWT + BCrypt        │
         │◄──────────────────────────    │  SignalR Hub         │
         │                               │  Stripe SDK          │
         │  Stripe REST API              │  FcmService (env)    │
         │◄──────────────────►           └──────────┬───────────┘
         │                                          │ Webhook (Stripe)
┌─────────────────┐     HTTPS/JSON                  │ Push (FCM HTTP v1)
│  CafeIES.Admin  │◄───────────────────►            │
│  (Blazor WASM)   │     SignalR WS      ┌───────────▼──────────┐
└─────────────────┘                      │  Stripe / Firebase   │
                                         │  (servicios externos)│
        Ambos comparten ────────────────►└──────────────────────┘
┌─────────────────┐
│  CafeIES.Shared │ ← DTOs, Entidades, Enums, Validaciones (compartido por todos)
└─────────────────┘
```

---

## Stack tecnológico

| Componente | Tecnología | Versión |
|-----------|-----------|---------|
| Backend API | ASP.NET Core | .NET 9 |
| Base de datos | SQL Server + Entity Framework Core | EF Core 9 |
| App móvil | .NET MAUI | .NET 9 (Android, iOS, Windows) |
| Panel admin | Blazor WebAssembly | .NET 9 |
| Autenticación | JWT Bearer + BCrypt (workFactor: 12) | — |
| Pagos | Stripe (PaymentIntent + Webhook) | Stripe.net 50.x |
| Tiempo real | SignalR | — |
| Notificaciones push | Firebase Cloud Messaging (FCM HTTP v1) | Plugin.Firebase.CloudMessaging 3.0.2 |
| Almacenamiento imágenes | Azure Blob Storage (prod) / wwwroot (dev) | Azure.Storage.Blobs 12.22.2 |
| Hosting API | Azure App Service (.NET 9) | — |
| Hosting Admin | Azure Static Web Apps (free tier) | — |
| CI/CD | GitHub Actions | — |
| QR invitaciones | QRCoder | — |
| MVVM (MAUI) | CommunityToolkit.Mvvm 8.3.2 | — |
| UI helpers (MAUI) | CommunityToolkit.Maui 9.0.3 | — |

---

## Estructura del proyecto

```
CafeIES/
│
├── CafeIES.sln
│
├── CafeIES.Shared/                    ← Modelos compartidos (DTOs, Entidades, Enums, Validaciones)
│   ├── Models/
│   │   ├── Enums.cs                   Turno, RolUsuario, EstadoPedido, MetodoPago
│   │   ├── Entities.cs                Usuario, Producto, Pedido, FranjaHoraria, Invitacion
│   │   └── DTOs.cs                    Todos los DTOs de request/response
│   └── Validation/
│       └── PasswordComplexityAttribute.cs  Validación: mayúscula + número + símbolo
│
├── CafeIES.API/                       ← Backend ASP.NET Core 9 (puerto 50658)
│   ├── Controllers/
│   │   ├── AuthController.cs          Login, registro alumno/invitado, refresh JWT (rate-limited)
│   │   ├── ProductosController.cs     CRUD + toggle activo + actualizar stock + subida imagen
│   │   ├── CategoriasController.cs    CRUD categorías
│   │   ├── PedidosController.cs       Crear pedido (validación horaria + stock + Stripe + push)
│   │   ├── PagosController.cs         PaymentIntent Stripe + webhook
│   │   ├── InstitutosController.cs    Listado público de institutos (para registro)
│   │   ├── InvitacionesController.cs  Generar/revocar QR+enlace para profe/personal
│   │   ├── NotificacionesController.cs  POST/DELETE /api/notificaciones/token (tokens FCM)
│   │   ├── ReportesController.cs      GET /api/reportes/excel y /pdf
│   │   └── AdminController.cs         Dashboard, validar alumnos, gestión + audit trail
│   ├── Data/
│   │   ├── AppDbContext.cs            EF Core + seed de institutos, categorías y franjas
│   │   └── DbSeeder.cs               Crea el admin inicial al arrancar
│   ├── Extensions/
│   │   ├── ClaimsPrincipalExtensions.cs  GetUserId() null-safe para claims JWT
│   │   └── DtoMapperExtensions.cs        ToDto() centralizado (Usuario, Pedido, FranjaHoraria)
│   ├── Services/
│   │   ├── AuthService.cs             JWT (access 1h + refresh 30d), BCrypt, token rotation
│   │   ├── HorarioService.cs          Lógica de restricción horaria por turno
│   │   ├── StripeService.cs           PaymentIntent, verificación de pago, webhook
│   │   ├── FcmService.cs              Push via FCM HTTP v1 + Google OAuth2 service account
│   │   ├── IBlobStorageService.cs     Abstracción para almacenamiento de imágenes
│   │   ├── LocalBlobStorageService.cs Implementación local (wwwroot/uploads — desarrollo)
│   │   ├── AzureBlobStorageService.cs Implementación Azure Blob Storage (producción)
│   │   ├── ReporteExcelService.cs     Genera .xlsx (3 hojas) con ClosedXML
│   │   └── ReportePdfService.cs       Genera .pdf con QuestPDF
│   ├── Hubs/CafeteriaHub.cs           SignalR: grupos cafeteria + user-{id}
│   ├── Program.cs                     Setup: EF, JWT, CORS, SignalR, Swagger, RateLimiter, health check
│   ├── appsettings.json               BBDD, JWT Key, Stripe, AzureStorage (placeholders)
│   ├── appsettings.Production.json    Overrides de logging y CORS para Azure App Service
│   └── appsettings.Development.json   Claves reales (gitignored)
│
├── CafeIES.MAUI/                      ← App móvil (Android + iOS + Windows)
│   ├── AppShell.xaml(.cs)             Shell con TabBar + rutas + visibilidad por rol
│   ├── MauiProgram.cs                 DI: servicios, ViewModels, páginas
│   ├── Services/
│   │   ├── ApiService.cs              HTTP client con auto-refresh, logging y SesionExpiradaMessage
│   │   ├── TokenService.cs            JWT en SecureStorage (Keychain/EncryptedPrefs)
│   │   └── PushNotificationService.cs  Obtiene token FCM y lo registra/elimina en la API
│   ├── Converters/Converters.cs       Conversores XAML (stock, estado, visibilidad)
│   ├── Platforms/
│   │   ├── Android/google-services.json  Placeholder Firebase — sustituir con archivo real
│   │   └── iOS/GoogleService-Info.plist  Placeholder Firebase — sustituir con archivo real
│   ├── ViewModels/
│   │   ├── LoginViewModel.cs          Login con validación + registro de token push
│   │   ├── RegistroViewModel.cs       Autoregistro alumno (selección de turno)
│   │   ├── RegistroInvitacionViewModel.cs  Registro por QR + registro de token push
│   │   ├── HomeViewModel.cs           Catálogo + horario + filtros + cache local 5min
│   │   ├── CarritoViewModel.cs        Carrito + checkout + validación stock
│   │   ├── PedidosViewModel.cs        Historial paginado
│   │   ├── DetallePedidoViewModel.cs  Estado en tiempo real via SignalR
│   │   ├── AdminPedidosViewModel.cs   Gestión pedidos (admin móvil)
│   │   ├── AdminProductosViewModel.cs CRUD productos + subida de imagen
│   │   └── AdminUsuariosViewModel.cs  Gestión usuarios (admin móvil)
│   └── Views/                         Todas las páginas XAML con tema dark & warm
│
└── CafeIES.Admin/                     ← Panel web Blazor WASM (puerto 50660)
    ├── Layout/
    │   ├── MainLayout.razor           Sidebar + contenido + info usuario + IDisposable
    │   └── EmptyLayout.razor          Layout vacío para login
    ├── Pages/
    │   ├── Login.razor                Login solo admins
    │   ├── Dashboard.razor            Stats en vivo, pedidos en curso (SignalR), alertas stock
    │   ├── Productos.razor            CRUD con modal, stock visual, toggle activo
    │   ├── Categorias.razor           Gestión de categorías con emoji
    │   ├── Usuarios.razor             Validar/rechazar alumnos, cambiar estado
    │   ├── Pedidos.razor              Historial completo con filtros
    │   ├── Invitaciones.razor         QR+enlace para profe/personal (con confirmación)
    │   ├── Horarios.razor             Franjas horarias por turno (con validación)
    │   └── Reportes.razor             Estadísticas y métricas
    ├── Services/
    │   ├── AdminApiService.cs         HTTP client con auto-refresh (timeout 20s)
    │   └── AuthAdminService.cs        Sesión: accessToken en sessionStorage, refreshToken en memoria
    └── wwwroot/
        ├── appsettings.json           URL de la API (configurable por entorno / GitHub Actions)
        ├── staticwebapp.config.json   SPA fallback + MIME types para Azure Static Web Apps
        └── css/app.css                Tema dark & warm completo

.github/
└── workflows/
    ├── deploy-api.yml                 CI/CD: build + publish → Azure App Service
    └── deploy-admin.yml               CI/CD: build + inject URL + publish → Azure Static Web Apps
```

---

## Puesta en marcha

### Requisitos
- **.NET 9 SDK** — [descargar](https://dotnet.microsoft.com/download)
- **SQL Server** (Express o LocalDB)
- **Visual Studio 2022 17.12+** con workloads: **.NET MAUI** y **ASP.NET and web development**

### 1. Configurar la API

Edita `CafeIES.API/appsettings.json` con tus datos de conexión.
Las claves de Stripe y Firebase van en `appsettings.Development.json` (no se sube a Git):

```json
// appsettings.Development.json (crear este archivo, está en .gitignore)
{
  "Stripe": {
    "SecretKey": "sk_test_TU_CLAVE",
    "PublishableKey": "pk_test_TU_CLAVE",
    "WebhookSecret": "whsec_TU_SECRET"
  },
  "Fcm": {
    "ProjectId": "TU_FIREBASE_PROJECT_ID",
    "ServiceAccountJson": "{ ...contenido del service-account.json descargado de Firebase Console... }"
  }
}
```

> **FCM opcional**: si `Fcm:ProjectId` está vacío, el servidor simplemente no envía push. El resto de la app funciona con normalidad.

### 2. Configurar la URL de la API en el panel Admin

Edita `CafeIES.Admin/wwwroot/appsettings.json` para apuntar a tu servidor:

```json
{
  "ApiBaseUrl": "https://localhost:50658/"
}
```

### 3. Primera migración
```bash
cd CafeIES.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Arrancar los 3 proyectos

Se pueden lanzar simultáneamente desde Visual Studio con el perfil `.slnlaunch`:

| Proyecto | Puerto | Descripción |
|----------|--------|-------------|
| **CafeIES.API** | `http://localhost:50658` | Backend REST + SignalR + Swagger |
| **CafeIES.Admin** | `http://localhost:50660` | Panel de administración web |
| **CafeIES.MAUI** | — | App móvil (emulador o dispositivo) |

Al arrancar la API por primera vez:
```
✅ Admin creado: admin@cafeies.local
⚠️  Cambia la contraseña tras el primer login.
```

> **MAUI en Android emulador**: la API se alcanza en `10.0.2.2:50658` (ya configurado en `MauiProgram.cs`).

---

## Despliegue en Azure

### Recursos necesarios

| Recurso | Tier recomendado | Descripción |
|---------|-----------------|-------------|
| **Azure App Service** | B1 (Basic) | Hosting de la API .NET 9 |
| **Azure SQL Database** | Basic (5 DTU) | Base de datos de producción |
| **Azure Blob Storage** | LRS Standard | Imágenes de productos (contenedor `productos`, acceso público blob) |
| **Azure Static Web Apps** | Free | Hosting del panel Blazor WASM |

### CI/CD con GitHub Actions

El repositorio incluye dos pipelines que se disparan automáticamente al hacer push a `main`:

| Workflow | Archivo | Disparo |
|----------|---------|---------|
| Deploy API | `.github/workflows/deploy-api.yml` | Cambios en `CafeIES.API/**` o `CafeIES.Shared/**` |
| Deploy Admin | `.github/workflows/deploy-admin.yml` | Cambios en `CafeIES.Admin/**` o `CafeIES.Shared/**` |

### Secrets de GitHub necesarios

Configurar en **Settings → Secrets and variables → Actions** del repositorio:

| Secret | Descripción | Cómo obtenerlo |
|--------|-------------|----------------|
| `AZURE_WEBAPP_NAME` | Nombre del App Service (ej: `cafeies-api`) | Azure Portal → App Service → nombre |
| `AZURE_CREDENTIALS` | JSON del service principal de Azure | `az ad sp create-for-rbac --name cafeies-github --role contributor --scopes /subscriptions/.../resourceGroups/cafeies-rg --json-auth` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Token de la Static Web App | Azure Portal → Static Web App → Overview → "Manage deployment token" |
| `API_BASE_URL` | URL pública de la API (ej: `https://cafeies-api.azurewebsites.net/`) | Azure Portal → App Service → URL |

### Configuración de Azure App Service

En el portal de Azure, añadir estas **Application Settings** (equivalen a `appsettings.json`):

```
ConnectionStrings__DefaultConnection  → cadena de conexión de Azure SQL
Jwt__Key                              → clave secreta JWT (mín. 32 caracteres)
Stripe__SecretKey                     → sk_live_...
Stripe__PublishableKey                → pk_live_...
Stripe__WebhookSecret                 → whsec_...
Fcm__ProjectId                        → project-id de Firebase
Fcm__ServiceAccountJson               → JSON del service account (una línea)
AzureStorage__ConnectionString        → DefaultEndpointsProtocol=https;AccountName=...
```

> Los secretos se inyectan como variables de entorno en el proceso — nunca tocan el disco del servidor CI.

### Pasos de despliegue (primera vez)

1. **Crear los recursos Azure** (App Service + SQL + Storage + Static Web App)
2. **Ejecutar las migraciones** contra Azure SQL:
   ```bash
   # Con la cadena de conexión de producción en ASPNETCORE_ConnectionStrings__DefaultConnection
   dotnet ef database update --project CafeIES.API
   ```
3. **Configurar Application Settings** en el App Service (tabla anterior)
4. **Configurar los secrets** en GitHub (tabla anterior)
5. **Actualizar URLs** en el código:
   - `CafeIES.API/appsettings.Production.json` → reemplazar `https://REPLACE_ME.azurestaticapps.net` con la URL real de la Static Web App (para CORS)
   - `CafeIES.MAUI/MauiProgram.cs` → verificar que `https://cafeies-api.azurewebsites.net/` coincide con el nombre de tu App Service
6. **Hacer push a `main`** — los pipelines se disparan automáticamente

### Almacenamiento de imágenes

El servicio `IBlobStorageService` selecciona automáticamente la implementación según el entorno:

- **Desarrollo** (`AzureStorage:ConnectionString` vacío): guarda en `wwwroot/uploads/productos/`, devuelve URL relativa
- **Producción** (connection string presente): sube a Azure Blob Storage, devuelve URL pública permanente

### Health check

La API expone `GET /health` que responde `200 Healthy`. Azure App Service lo usa para verificar que la aplicación arrancó correctamente antes de enrutar tráfico.

---

## Flujo de registro

```
Admin ──────────── Creado automáticamente al arrancar la API (DbSeeder)
                    → Accede al panel Blazor y a las funciones admin de la app

Profesor/Personal ─ Admin genera QR/enlace en /invitaciones
                    → El invitado escanea con el móvil
                    → Registro inmediato, cuenta activa sin validación

Alumno ──────────── Se registra solo en la app (elige turno, NO elige rol)
                    → Queda en estado "Pendiente"
                    → Admin valida desde /usuarios o desde la app
                    → Cuenta activa
```

---

## Lógica de horarios

Franjas editables desde `/horarios` en el panel admin — **sin tocar código**.
Incluye validación de que `HoraInicio < HoraFin` y confirmación antes de eliminar.

| Turno   | Franja 1 (ejemplo) | Franja 2 (ejemplo) |
|---------|--------------------|--------------------|
| Mañana  | 07:30 – 08:00      | 11:00 – 11:30      |
| Tarde   | 13:45 – 14:00      | 17:00 – 17:30      |
| Noche   | 20:45 – 21:00      | 23:00 – 23:20      |

- **Alumnos**: solo pueden pedir durante las franjas de su turno.
- **Admin, Profesor, Personal**: sin restricción horaria.

---

## Notificaciones en tiempo real (SignalR)

- **Dashboard admin**: recibe pedidos nuevos al instante sin recargar (auto-refresh cada 30s como backup).
- **App móvil**: el alumno ve el estado de su pedido actualizado en vivo en `DetallePedidoPage`.
- **Grupos SignalR**: `cafeteria` (para admins) y `user-{id}` (para cada usuario).
- **Sesión expirada**: si el refresh token caduca, `ApiService` desconecta SignalR automáticamente y emite `SesionExpiradaMessage`.

---

## Notificaciones push (FCM)

Cuando el personal de cafetería marca un pedido como **Listo**, el usuario recibe una notificación push aunque la app esté cerrada.

### Flujo

```
Personal marca pedido → Listo
        │
        ▼
PedidosController.CambiarEstado
        │  consulta DispositivoTokens del usuario
        ▼
FcmService.EnviarAsync()
        │  OAuth2 con Service Account → FCM HTTP v1 API
        ▼
Firebase Cloud Messaging
        │
   ┌────┴────┐
   ▼         ▼
Android     iOS
 (FCM)     (APNs via FCM)
```

### Configuración necesaria

| Paso | Descripción |
|------|-------------|
| 1 | Crear proyecto en [Firebase Console](https://console.firebase.google.com) |
| 2 | Android: descargar `google-services.json` → `CafeIES.MAUI/Platforms/Android/` |
| 3 | iOS: descargar `GoogleService-Info.plist` → `CafeIES.MAUI/Platforms/iOS/` |
| 4 | Firebase Console → Configuración → Cuentas de servicio → Generar nueva clave privada |
| 5 | Copiar el JSON en `appsettings.Development.json` → `Fcm:ServiceAccountJson` |
| 6 | Poner el project-id en `Fcm:ProjectId` |
| 7 | iOS además: subir clave de autenticación APNs en Firebase → Cloud Messaging |

### Comportamiento si FCM no está configurado

- `FcmService` detecta que `Fcm:ProjectId` está vacío y sale sin hacer nada.
- `PushNotificationService` en MAUI captura cualquier excepción de Firebase y la registra como warning.
- El resto de la aplicación (SignalR, pedidos, pagos) funciona con total normalidad.

---

## Seguridad

| Mecanismo | Detalle |
|-----------|---------|
| Contraseñas | BCrypt con workFactor 12 |
| Complejidad contraseña | Mínimo 8 caracteres + mayúscula + número + símbolo |
| JWT Access Token | Duración 1 hora, firmado con HMAC-SHA256 |
| JWT Refresh Token | Duración 30 días, rotación en cada uso |
| Auto-refresh | MAUI (`ApiService`) y Blazor (`AuthAdminService`) renuevan tokens transparentemente |
| Almacenamiento tokens | MAUI: `SecureStorage` (Keychain/EncryptedSharedPreferences). Blazor: accessToken en `sessionStorage`, refreshToken **solo en memoria** (no persiste entre recargas) |
| Rate limiting | 10 req/min por IP en endpoints de auth — responde HTTP 429 |
| Audit trail | Todas las acciones admin (validar, suspender, eliminar, cambiar turno/horarios) se registran con `[AUDIT]` en los logs del servidor |
| Pagos | Stripe PaymentIntent — total calculado en servidor, verificado antes de crear pedido |
| Secretos | Claves reales en `appsettings.Development.json` (gitignored), placeholders en repo |
| Stock | Transacciones SQL para evitar sobreventa concurrente |
| Pedidos | Máquina de estados: solo transiciones válidas permitidas |
| Ownership | Los usuarios solo ven/cancelan sus propios pedidos |
| SSL en desarrollo | `ServerCertificateCustomValidationCallback` solo activo bajo `#if DEBUG` |
| Claims JWT | Extracción null-safe con `ClaimsPrincipalExtensions.GetUserId()` |

---

## Estado actual del proyecto

### Funcionalidades completadas

- [x] Registro de alumnos con selección de turno e instituto
- [x] Registro de profesores/personal por invitación QR
- [x] Login/logout con JWT + refresh automático
- [x] Catálogo de productos con categorías, filtros y búsqueda
- [x] Carrito de compras con control de cantidad (tope de stock)
- [x] Creación de pedidos con validación horaria y de stock
- [x] **Pago real con Stripe** — formulario de tarjeta en la app, PaymentIntent + webhook
- [x] Historial de pedidos con paginación
- [x] Detalle de pedido en tiempo real (SignalR)
- [x] Panel admin web completo (Dashboard, Productos, Categorías, Usuarios, Pedidos, Horarios, Invitaciones, Reportes)
- [x] Funciones admin desde la app móvil (pedidos, productos, usuarios)
- [x] **Soporte multi-instituto** — entidad Instituto, selector en registro, filtros en admin
- [x] Dashboard admin con filtro por instituto
- [x] **Exportación de reportes** — Excel (3 hojas) y PDF desde el panel admin
- [x] **Tests unitarios** — 95 tests (HorarioService, AuthService, dominio, validaciones)
- [x] **Subida de imágenes de productos** — servidor local + Blazor picker + MAUI MediaPicker
- [x] **Notificaciones push (FCM)** — aviso al usuario cuando su pedido está listo para recoger (Android + iOS via Firebase, configurable)
- [x] **Infraestructura Azure** — `IBlobStorageService` (local/Azure Blob), health check `/health`, CORS desde config, `appsettings.Production.json`, GitHub Actions CI/CD (API + Admin), `staticwebapp.config.json` para SPA routing
- [x] **Despliegue en producción** — App Service + SQL Database + Blob Storage + Static Web Apps creados y operativos en Azure (`northeurope`)
- [x] **Migraciones en Azure SQL** — esquema y seed aplicados contra `cafeies-sql2.database.windows.net/cafeiesdb`
- [x] **Stripe webhook configurado en producción** — endpoint `https://cafeies-api.azurewebsites.net/api/pagos/webhook` registrado en Stripe, `whsec_` inyectado en App Settings
- [x] **Test end-to-end de pagos verificado** — login → producto → PaymentIntent → confirm (`pm_card_visa`) → pedido creado → `succeeded` (1.50 EUR)
- [x] Tema dark & warm consistente en app y panel web
- [x] Cache local de catálogo (5 min) para rendimiento
- [x] Modales de confirmación en acciones destructivas
- [x] Validación de franjas horarias
- [x] **Rate limiting** en endpoints de autenticación (10 req/min/IP)
- [x] **Audit trail** de acciones admin en logs estructurados
- [x] **Validación de complejidad de contraseña** (mayúscula + número + símbolo)
- [x] Timeout en HttpClient (15s MAUI, 20s Admin)
- [x] Desconexión automática de SignalR al expirar la sesión

### Bugs corregidos (auditorías S26-S28)

- [x] Máquina de estados de pedidos (solo transiciones válidas)
- [x] Confirmación al cancelar pedidos
- [x] Restauración de sesión admin tras refresh de página
- [x] Validación `MinLength` corregida en DTOs
- [x] Tope de cantidad en carrito según stock disponible
- [x] Verificación de ownership en pedidos
- [x] Fechas locales (`DateTime.Now`) en vez de UTC donde corresponde
- [x] Endpoint `GetProductoById` añadido
- [x] Auto-refresh del dashboard cada 30 segundos
- [x] Paginación completa en `GetAllPedidosAsync` (MAUI)
- [x] `PropertyNameCaseInsensitive` en `TokenService`
- [x] Textos de UI corregidos (desactivar vs eliminar)
- [x] Toast protegido con try-catch (COMException en Windows unpackaged)

---

## Roadmap — Próximos hitos

### Fase 1 — ~~Pasarela de pago real~~ ✅ COMPLETADA
- [x] Integrar Stripe como pasarela de pago
- [x] Flujo de pago: formulario de tarjeta → PaymentIntent → confirmación → pedido
- [x] Webhook de Stripe para confirmación y detección de pagos huérfanos
- [x] Verificación server-side antes de crear pedido

### Fase 5a — ~~Despliegue Azure~~ ✅ COMPLETADA
- [x] Infraestructura Azure: App Service (B1), Azure SQL (Basic), Blob Storage (LRS), Static Web Apps (Free)
- [x] `IBlobStorageService` con implementación local (dev) y Azure Blob Storage (prod)
- [x] Health check en `/health` para App Service
- [x] CORS configurable desde `appsettings.Production.json`
- [x] Pipelines CI/CD para API (`az webapp deploy --type zip`) y Admin Blazor (`static-web-apps-deploy@v1`)
- [x] Recursos Azure creados y secrets configurados en GitHub
- [x] Migración inicial aplicada en Azure SQL
- [x] Stripe webhook configurado en producción
- [x] Test end-to-end de pagos verificado en producción

### Fase 5b — Distribución Android ✅ COMPLETADA (GitHub Releases)
- [x] `AndroidManifest.xml` con permisos correctos (INTERNET, CAMERA, POST_NOTIFICATIONS, READ_MEDIA_IMAGES)
- [x] `network_security_config.xml` — cleartext HTTP bloqueado en producción, solo CAs del sistema
- [x] `proguard.cfg` — reglas R8 para Mono runtime, Firebase, OkHttp y SignalR
- [x] `.csproj` — `ApplicationTitle`, firma via env vars, `AndroidPackageFormat=aab`, `MauiLinkMode=SdkAndUserAssemblies`
- [x] `infra/generar-keystore.ps1` — genera el keystore de firma con `keytool`
- [x] `infra/build-android-release.ps1` — build y firma del AAB con `dotnet publish` (AAB generado: ~30 MB)
- [x] `.github/workflows/deploy-android.yml` — pipeline CI/CD operativo: build APK debug → GitHub Release automático en cada push
- [x] `infra/configurar-play-store-secrets.ps1` — preparado para activar Play Store cuando se disponga de cuenta
- [x] Política de privacidad publicada en GitHub Pages
- [x] **Primera distribución**: `cafeies-2026.03.23.apk` (14.4 MB) disponible en [Releases](https://github.com/JoseGlezHerrera/CafeteriaInsti/releases)
- [ ] Sustituir `google-services.json` con el archivo real de Firebase Console
- [ ] Diseñar icono definitivo (appicon.svg / appiconfg.svg) y capturas de pantalla

### Fase 5c — Google Play Store (pendiente cuenta developer)
- [ ] Registrar cuenta Google Play Developer (pago único 25 USD)
- [ ] Cambiar pipeline a AAB firmado con keystore release
- [ ] Subir primera versión a prueba interna en Play Console
- [ ] Política de privacidad ya disponible en GitHub Pages

### Fase 3 — ~~Soporte multi-instituto~~ ✅ COMPLETADA
- [x] Entidad `Instituto` con seed de 3 centros iniciales
- [x] Selector de instituto en registro (alumno e invitación)
- [x] Filtrar pedidos, usuarios y dashboard por instituto en admin
- [x] Badge de instituto en pedidos y usuarios
- [x] Claim `institutoId` en JWT

### Fase 4 — ~~Mejoras adicionales~~ ✅ COMPLETADA
- [x] ~~Seguridad~~: rate limiting, audit trail, complejidad de contraseña, null-safe claims, timeout HTTP
- [x] ~~Subida de imágenes de productos~~ — servidor local, Admin Blazor picker, MAUI MediaPicker
- [x] ~~Notificaciones push~~ — FCM HTTP v1 (Android + iOS via APNs), configurable con Firebase
- [x] ~~Exportación de reportes a Excel/PDF~~ — ClosedXML (3 hojas) + QuestPDF
- [x] ~~Tests unitarios~~ — 95 tests: HorarioService, AuthService, dominio, validaciones

---

## Changelog

### v0.10.0 — Pipeline CI/CD Android operativo + distribución via GitHub Releases (actual)
- **`AndroidManifest.xml`** (fuente): permisos explícitos (`INTERNET`, `CAMERA`, `POST_NOTIFICATIONS`, `READ_MEDIA_IMAGES`, `READ_EXTERNAL_STORAGE` con `maxSdkVersion="32"`), `allowBackup="false"`, referencia a `network_security_config`; iconos omitidos (MAUI resizetizer los inyecta automáticamente)
- **`network_security_config.xml`**: cleartext HTTP bloqueado globalmente; solo CAs del sistema
- **`proguard.cfg`**: reglas R8 para Mono/.NET MAUI runtime (`crc64**`), Firebase Cloud Messaging, OkHttp/OkIO (SignalR), enumeraciones y Parcelable
- **`MauiProgram.cs`**: eliminada llamada `UseFirebase()` — Plugin.Firebase.CloudMessaging 3.1.0 auto-inicializa desde `google-services.json`
- **`infra/generar-keystore.ps1`**: genera keystore RSA-2048 10000 días; guarda credenciales en `keystore-credentials.local.txt` (gitignored)
- **`infra/build-android-release.ps1`**: build local del AAB firmado (~30 MB)
- **`.github/workflows/deploy-android.yml`**: pipeline **operativo** — `global.json` fija SDK 9.x antes de instalar workload (runner tiene .NET 10 preinstalado), restore separado Shared/MAUI con `--no-restore` en publish, build APK debug, crea GitHub Release con el APK adjunto; `versionCode = github.run_number + 1000`; trigger automático en push a `CafeIES.MAUI/**`
- **`docs/politica-privacidad.html`**: página RGPD completa alojada en GitHub Pages — `https://JoseGlezHerrera.github.io/CafeteriaInsti/politica-privacidad.html`
- **Primera distribución**: `cafeies-2026.03.23.apk` (14.4 MB) publicado como GitHub Release pre-release

### v0.9.0 — Despliegue Azure completo y pagos verificados en producción
- **Recursos Azure** creados: App Service `cafeies-api` (B1, Linux, .NET 9), Azure SQL `cafeies-sql2/cafeiesdb` (Basic 5 DTU, northeurope), Blob Storage `cafeiesimgs` (contenedor `productos`, LRS), Static Web App (free tier)
- **EF Core migrations** aplicadas contra Azure SQL con `dotnet ef database update --connection`; seed inicial ejecutado (admin, institutos, categorías, franjas)
- **Stripe webhook** registrado en producción (`POST /api/pagos/webhook`); `whsec_` inyectado como App Setting en Azure App Service
- **CI/CD corregido**: `deploy-api.yml` migrado de `azure/webapps-deploy@v3` + publish profile a `azure/login@v2` (service principal `AZURE_CREDENTIALS`) + `az webapp deploy --type zip`; `workflow_dispatch` añadido a ambos pipelines
- **Test end-to-end** en producción verificado: login → creación de producto → `POST /api/pagos/crear-intent` → confirmación Stripe (`pm_card_visa`, succeeded 1.50 EUR) → `POST /api/pedidos` con `stripePaymentIntentId` → Pedido #1 creado
- **Scripts infra**: `create-sp.ps1`, `fix-credentials.ps1`, `create-stripe-webhook.ps1`, `test-pagos.ps1` (claves via `$env:STRIPE_SECRET_KEY`)

### v0.8.0 — Infraestructura Azure y CI/CD
- **`IBlobStorageService`**: abstracción con dos implementaciones — `LocalBlobStorageService` (desarrollo, guarda en `wwwroot/uploads/`) y `AzureBlobStorageService` (producción, Azure Blob Storage contenedor `productos` con acceso público). Selección automática según la presencia de `AzureStorage:ConnectionString`
- **Health check**: `GET /health` → HTTP 200; usado por Azure App Service para liveness probes
- **CORS desde config**: `Cors:AllowedOrigins` en `appsettings.json`; `appsettings.Production.json` añade la URL de la Static Web App
- **`appsettings.Production.json`**: overrides de log level (Warning en producción) y CORS para Azure
- **GitHub Actions CI/CD**:
  - `deploy-api.yml`: `dotnet publish -c Release` + `az webapp deploy --type zip`
  - `deploy-admin.yml`: inyecta `API_BASE_URL` en `appsettings.json` + `Azure/static-web-apps-deploy@v1`
- **`staticwebapp.config.json`**: `navigationFallback` para SPA routing de Blazor WASM en Azure Static Web Apps + MIME types correctos para `.wasm`/`.dll`
- **MAUI producción**: URL `https://cafeies-api.azurewebsites.net/` en bloque `#else` (release builds)
- **Tests**: guardas de medianoche en 3 tests de `HorarioService` con franjas relativas al tiempo actual

### v0.7.0 — Notificaciones push FCM
- **Push notifications**: aviso al usuario cuando su pedido pasa a "Listo"
- `DispositivoToken` — entidad para almacenar tokens FCM por usuario/dispositivo (un usuario puede tener varios)
- `FcmService` — FCM HTTP v1 API con autenticación OAuth2 mediante Service Account de Firebase; completamente opcional (se deshabilita si no hay configuración)
- `NotificacionesController` — `POST /api/notificaciones/token` (upsert con reasignación en reinstalaciones) y `DELETE` para limpiar al cerrar sesión
- `PushNotificationService` (MAUI) — obtiene el token FCM y lo registra/elimina automáticamente
- Plugin.Firebase.CloudMessaging 3.0.2 — soporta Android (FCM) e iOS (APNs via Firebase) con `#if ANDROID || IOS`
- Placeholders de configuración Firebase para `google-services.json` y `GoogleService-Info.plist`
- Android: `MainActivity` con `OnNewIntent` para deep links desde notificación; iOS: `AppDelegate` con callbacks APNs

### v0.6.1 — Tests, imágenes y exportación
- **95 tests unitarios** con xUnit + EF InMemory: HorarioService (12), AuthService (16), dominio (FranjaHoraria, Invitacion, Producto, EstadoPedido), validación de contraseña
- **Exportación Excel** (3 hojas: Resumen KPIs, Pedidos, Ranking productos) con ClosedXML 0.102.3
- **Exportación PDF** (KPIs, métodos de pago, top-10 productos) con QuestPDF Community
- **Subida de imágenes de productos**: endpoint `POST /api/productos/{id}/imagen` con protección path-traversal, validación de tipo/tamaño (5 MB), soft-delete del archivo anterior
- Panel Admin Blazor: selector de imagen con previsualización en modal de producto
- MAUI: `MediaPicker.Default.PickPhotoAsync()` + upload multipart desde `AdminEditProductoPage`
- 10 bugs corregidos (auditoría): transacción serializable, null-safe mappers, category validation en PUT, request size limits, date validation, timing leak en GetById, catch logging

### v0.6.0 — Seguridad y calidad
- **Rate limiting**: 10 req/min por IP en endpoints de auth, responde HTTP 429
- **Audit trail**: todas las acciones admin registradas con `[AUDIT]` en logs del servidor
- **Validación de contraseñas**: `PasswordComplexityAttribute` — mayúscula + número + símbolo obligatorios
- **Null-safe claims**: `ClaimsPrincipalExtensions.GetUserId()` evita `NullReferenceException`
- **Timeout HTTP**: 15s en MAUI, 20s en Admin Blazor
- **SSL**: `ServerCertificateCustomValidationCallback` solo activo bajo `#if DEBUG`
- **RefreshToken**: eliminado de `sessionStorage` en Admin — solo en memoria
- **SignalR**: desconexión automática y `SesionExpiradaMessage` al expirar sesión
- **DtoMapperExtensions**: `ToDto()` centralizado elimina duplicación en controllers
- **Logging**: `ILogger<ApiService>` en todos los catch de la app móvil

### v0.5.0 — Stripe + Multi-instituto
- **Pagos reales con Stripe**: PaymentIntent + formulario de tarjeta en MAUI + verificación server-side + webhook
- **Soporte multi-instituto**: entidad Instituto, selector en registro, filtros por instituto en Dashboard/Usuarios/Pedidos
- Claim `institutoId` en JWT para identificación por instituto
- Endpoint público `/api/institutos` para pantallas de registro
- Endpoint público `/api/pagos/config` para publishable key de Stripe
- Webhook de Stripe con detección de pagos huérfanos y log de fallos
- Secretos protegidos: claves reales en `appsettings.Development.json` (gitignored)

### v0.3.0 — Auditoría y estabilización (S26-S28)
- Corregidos 11 bugs encontrados durante las auditorías de código
- Máquina de estados robusta para transiciones de pedidos
- Modales de confirmación en todas las acciones destructivas (cancelar pedido, revocar invitación, eliminar franja)
- Validación de franjas horarias (HoraInicio < HoraFin)
- Paginación completa en consultas admin desde MAUI
- Protección contra crash de Toast en Windows (COMException)
- Consistencia en el uso de `DateTime.Now` vs `DateTime.UtcNow`

### v0.2.0 — Panel admin y funciones avanzadas
- Panel Blazor WASM completo con 8 páginas de administración
- Funciones admin accesibles desde la app móvil
- SignalR para actualizaciones en tiempo real
- Sistema de invitaciones QR para profesores/personal
- Dashboard con estadísticas y alertas de stock
- Auto-refresh de tokens en MAUI y Blazor
- Tema dark & warm unificado

### v0.1.0 — MVP inicial
- API REST con autenticación JWT + BCrypt
- Registro de alumnos con selección de turno
- Catálogo de productos con categorías
- Carrito de compras y creación de pedidos
- Restricción horaria por turno
- Historial de pedidos

---

## Licencia

Proyecto privado — uso interno para centros educativos.
