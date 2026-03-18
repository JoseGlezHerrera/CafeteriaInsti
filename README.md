# CaféIES — Sistema de pedidos de cafetería para institutos

> Aplicación móvil + panel de administración web para gestionar pedidos de cafetería en centros educativos.
> Multi-instituto · Pago real con Stripe · Tiempo real con SignalR · Infraestructura Azure lista para producción.

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
| Hosting API | Azure App Service (.NET 9, Linux) | — |
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
│   │   ├── Entities.cs                 Usuario, Producto, Pedido, FranjaHoraria, Invitacion, Instituto
│   │   ├── DTOs.cs                     Todos los DTOs de request/response con validaciones
│   │   └── Enums.cs                    Turno, RolUsuario, EstadoPedido, MetodoPago
│   └── Validation/
│       └── PasswordComplexityAttribute.cs  Mayúscula + número + símbolo obligatorios
│
├── CafeIES.API/                        ← Backend REST (puerto 50658)
│   ├── Controllers/
│   │   ├── AuthController.cs           Login, registro, refresh JWT (rate-limited)
│   │   ├── ProductosController.cs      CRUD + stock + imagen
│   │   ├── CategoriasController.cs     CRUD con audit trail
│   │   ├── PedidosController.cs        Crear/gestionar pedidos (horario + stock + Stripe)
│   │   ├── PagosController.cs          PaymentIntent + formulario Stripe + webhook
│   │   ├── InstitutosController.cs     Listado público para registro
│   │   ├── InvitacionesController.cs   QR + enlace para profesores/personal
│   │   ├── NotificacionesController.cs Registro de tokens de dispositivo (FCM — pendiente)
│   │   ├── ReportesController.cs       Excel y PDF (máx. 1.000 registros por informe)
│   │   └── AdminController.cs          Dashboard, usuarios, gestión + audit trail
│   ├── Data/
│   │   ├── AppDbContext.cs             EF Core context con índices y relaciones
│   │   └── DbSeeder.cs                 Admin inicial, institutos, categorías y franjas
│   ├── Extensions/
│   │   ├── ClaimsPrincipalExtensions.cs  GetUserId() null-safe
│   │   └── DtoMapperExtensions.cs        ToDto() centralizado (Usuario, Pedido, FranjaHoraria)
│   ├── Services/
│   │   ├── AuthService.cs              JWT (access 1h + refresh 30d) + BCrypt + rotación
│   │   ├── HorarioService.cs           Restricción horaria por turno
│   │   ├── StripeService.cs            PaymentIntent (automatic) + verificación + webhook
│   │   ├── FcmService.cs               Push FCM HTTP v1 (configuración pendiente)
│   │   ├── IBlobStorageService.cs      Abstracción de almacenamiento de imágenes
│   │   ├── LocalBlobStorageService.cs  Implementación local con protección path-traversal
│   │   ├── AzureBlobStorageService.cs  Implementación Azure Blob Storage
│   │   ├── ReporteExcelService.cs      Genera .xlsx (3 hojas) con ClosedXML
│   │   └── ReportePdfService.cs        Genera .pdf con QuestPDF (límite 1.000 registros)
│   ├── Hubs/CafeteriaHub.cs            SignalR: grupos cafeteria + user-{id}
│   ├── Program.cs                      DI, EF, JWT, CORS, SignalR, Swagger, RateLimiter, HealthCheck
│   ├── appsettings.json                Configuración con placeholders seguros
│   ├── appsettings.Development.json    Claves reales de dev (gitignored)
│   └── appsettings.Production.json     Overrides de logging y CORS para Azure
│
├── CafeIES.MAUI/                       ← App móvil Android + iOS
│   ├── Services/
│   │   ├── ApiService.cs               HTTP client con auto-refresh, SignalR y fallback de sesión
│   │   ├── TokenService.cs             JWT en SecureStorage (Keychain / EncryptedPreferences)
│   │   └── PushNotificationService.cs  Stub — pendiente integración Firebase
│   ├── ViewModels/                     LoginVM, HomeVM, CarritoVM, PedidosVM, AdminVM...
│   ├── Views/                          Todas las páginas XAML (tema dark & warm)
│   └── Platforms/
│       ├── Android/                    google-services.json (placeholder — ver sección FCM)
│       └── iOS/                        GoogleService-Info.plist (placeholder)
│
├── CafeIES.Admin/                      ← Panel web Blazor WASM (puerto 50660)
│   ├── Pages/                          Login, Dashboard, Productos, Categorías, Usuarios,
│   │                                   Pedidos, Invitaciones, Horarios, Reportes
│   ├── Services/
│   │   ├── AdminApiService.cs          HTTP client con auto-refresh (timeout 20s)
│   │   └── AuthAdminService.cs         accessToken en sessionStorage, refreshToken en memoria
│   └── wwwroot/
│       ├── appsettings.json            URL de la API (inyectada por GitHub Actions en prod)
│       └── staticwebapp.config.json    SPA fallback + MIME types para Azure Static Web Apps
│
├── .github/workflows/
│   ├── deploy-api.yml                  CI/CD: build + zip → Azure App Service
│   ├── deploy-admin.yml                CI/CD: build + inject URL → Azure Static Web Apps
│   └── deploy-android.yml             CI/CD: APK debug → GitHub Releases
│
└── infra/
    ├── generar-keystore.ps1            Genera keystore RSA-2048 para firma Android
    ├── build-android-release.ps1       Build local del AAB firmado
    └── configurar-play-store-secrets.ps1  Preparado para Play Store
```

---

## Puesta en marcha local

### Requisitos

- **.NET 9 SDK** — [descargar](https://dotnet.microsoft.com/download)
- **SQL Server** (Express o LocalDB)
- **Visual Studio 2022 17.12+** con workloads: **.NET MAUI** y **ASP.NET and web development**

### 1. Clonar y configurar la API

Crea `CafeIES.API/appsettings.Development.json` (no se sube al repositorio):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CafeIES;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "TU_CLAVE_SECRETA_DE_AL_MENOS_32_CARACTERES"
  },
  "Admin": {
    "Password": "TuPasswordAdmin1!"
  },
  "Stripe": {
    "SecretKey": "sk_test_...",
    "PublishableKey": "pk_test_...",
    "WebhookSecret": "whsec_..."
  }
}
```

> **Stripe opcional en desarrollo**: si no tienes claves, el flujo de pago no funcionará, pero el resto de la app sí.

### 2. Configurar la URL de la API en el panel Admin

Edita `CafeIES.Admin/wwwroot/appsettings.json`:

```json
{
  "ApiBaseUrl": "https://localhost:50658/"
}
```

### 3. Aplicar migraciones

```bash
cd CafeIES.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Arrancar los proyectos

Lanza simultáneamente desde Visual Studio con el perfil `.slnlaunch`, o por separado:

| Proyecto | Puerto | Descripción |
|---|---|---|
| **CafeIES.API** | `https://localhost:50658` | Backend REST + SignalR + Swagger UI |
| **CafeIES.Admin** | `https://localhost:50660` | Panel de administración web |
| **CafeIES.MAUI** | — | App móvil (emulador Android o dispositivo) |

Al arrancar la API por primera vez se crea el administrador inicial:
```
✅ Admin creado: admin@cafeies.local / (contraseña configurada en appsettings)
```

> **MAUI en emulador Android**: la API es accesible en `10.0.2.2:50658` (ya configurado en `MauiProgram.cs` bajo `#if ANDROID`).

---

## Despliegue en Azure

### Recursos necesarios

| Recurso | Tier | Uso |
|---|---|---|
| **Azure App Service** | B1 (Basic) | Hosting API .NET 9 |
| **Azure SQL Database** | Basic (5 DTU) | Base de datos de producción |
| **Azure Blob Storage** | LRS Standard | Imágenes de productos |
| **Azure Static Web Apps** | Free | Hosting Blazor WASM |

### Secrets de GitHub Actions

Configurar en **Settings → Secrets and variables → Actions**:

| Secret | Descripción |
|---|---|
| `AZURE_WEBAPP_NAME` | Nombre del App Service (ej: `cafeies-api`) |
| `AZURE_CREDENTIALS` | JSON del service principal (`az ad sp create-for-rbac`) |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Token de la Static Web App |
| `API_BASE_URL` | URL pública de la API (ej: `https://cafeies-api.azurewebsites.net/`) |

### Application Settings en Azure App Service

```
ConnectionStrings__DefaultConnection  → cadena de conexión Azure SQL
Jwt__Key                              → clave JWT (mín. 32 caracteres)
Admin__Password                       → contraseña admin inicial
Stripe__SecretKey                     → sk_live_...
Stripe__PublishableKey                → pk_live_...
Stripe__WebhookSecret                 → whsec_...
AzureStorage__ConnectionString        → DefaultEndpointsProtocol=https;AccountName=...
```

### Health check

La API expone `GET /health` → HTTP 200. Azure App Service lo usa para verificar el arranque antes de enrutar tráfico.

---

## Flujo de registro de usuarios

```
Administrador ─── Creado automáticamente al arrancar (DbSeeder)
                   Accede al panel Blazor y a funciones admin de la app

Profesor/Personal ─ Admin genera QR o enlace en /invitaciones
                    El invitado escanea con el móvil o abre el enlace
                    Registro inmediato, cuenta activa sin validación manual
                    Los tokens de invitación expiran en 1–365 días (configurable)

Alumno ─────────── Se registra en la app (selecciona turno e instituto)
                    Estado inicial: "Pendiente de validación"
                    Admin valida desde el panel web o desde la app
                    Cuenta activa tras validación
```

---

## Lógica de horarios

Las franjas horarias se gestionan desde `/horarios` en el panel admin, sin tocar código.

| Turno | Ejemplo franja 1 | Ejemplo franja 2 |
|---|---|---|
| Mañana | 07:30 – 08:00 | 11:00 – 11:30 |
| Tarde | 13:45 – 14:15 | 17:00 – 17:30 |
| Noche | 20:45 – 21:00 | 23:00 – 23:20 |

- **Alumnos**: solo pueden crear pedidos durante las franjas de su turno asignado.
- **Profesores, Personal y Administradores**: sin restricción horaria.
- El servidor valida la franja en el momento de crear el pedido — el cliente no puede saltársela.

---

## Pagos con Stripe

### Flujo completo

```
1. App solicita PaymentIntent → POST /api/pagos/crear-intent
   (total calculado en servidor, nunca en cliente)

2. La API devuelve clientSecret + paymentIntentId

3. App abre WebView con formulario HTML/Stripe.js embebido
   (datos de tarjeta nunca pasan por el código de la app)

4. Stripe.js confirma el pago con stripe.confirmCardPayment()

5. Stripe redirige a cafeies://success/{paymentIntentId}

6. App crea el pedido → POST /api/pedidos con el paymentIntentId

7. La API verifica con Stripe que el pago está succeeded
   antes de crear el pedido en base de datos

8. Stripe notifica via Webhook → POST /api/pagos/webhook
```

### Configuración del webhook en Stripe Dashboard

- **Endpoint**: `https://cafeies-api.azurewebsites.net/api/pagos/webhook`
- **Eventos**: `payment_intent.succeeded`, `payment_intent.payment_failed`

---

## Tiempo real con SignalR

- **Dashboard admin**: recibe pedidos nuevos al instante (auto-refresh cada 30s como respaldo).
- **App móvil**: el alumno ve el estado de su pedido actualizado en vivo.
- **Grupos**: `cafeteria` (todos los admins) y `user-{id}` (usuario específico).
- **Reconexión automática**: si el token se renueva (refresh), SignalR se reconecta automáticamente si estaba desconectado.
- **Sesión expirada**: `ApiService` desconecta SignalR y navega al login con fallback directo si el mensaje no se procesa.
- **Keepalive**: `KeepAliveInterval = 15s`, `ClientTimeoutInterval = 30s`.

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
| Rate limiting | Política "auth" (10 req/min/IP) en endpoints de autenticación. Política "general" (60 req/min/IP) en el resto de endpoints |
| Rate limiting invitaciones | Política específica (5 req/min/IP) en `/api/invitaciones/validar` |
| Audit trail | Acciones admin registradas con prefijo `[AUDIT]` en logs del servidor |
| Pagos | Total calculado en servidor — cliente solo recibe el clientSecret |
| Stock | Transacciones `ReadCommitted` + `[ConcurrencyCheck]` para evitar sobreventa |
| Pedidos | Máquina de estados: solo transiciones válidas permitidas |
| Ownership | Usuarios solo acceden a sus propios pedidos |
| XSS | Notas de pedido sanitizadas antes de persistir |
| Path traversal | `LocalBlobStorageService` usa `Path.GetRelativePath` para validar rutas |
| SSL en desarrollo | `ServerCertificateCustomValidationCallback` solo bajo `#if DEBUG` |
| Secretos | Claves reales en `appsettings.Development.json` (gitignored) o Azure App Settings |
| Invitaciones | `DiasValidez` limitado a 1–365 días |
| MetodoPago | Validado con `Enum.IsDefined` en servidor |

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

- Registro de alumnos con selección de turno e instituto
- Registro de profesores/personal mediante invitación QR o enlace
- Login/logout con JWT + refresh automático y transparente
- Catálogo de productos con categorías, filtros y búsqueda
- Carrito de compras con control de cantidad y stock
- Validación horaria por turno antes de crear el pedido
- **Pago real con Stripe** — WebView con Stripe.js, PaymentIntent + webhook + verificación server-side
- Historial de pedidos con paginación
- Detalle de pedido en tiempo real (SignalR)
- Gestión de estado de pedidos con máquina de estados
- **Panel admin web completo** — Dashboard, Productos, Categorías, Usuarios, Pedidos, Horarios, Invitaciones, Reportes
- Funciones admin desde la app móvil (pedidos, productos, usuarios)
- **Multi-instituto** — selector en registro, filtros por instituto en admin
- **Exportación de reportes** — Excel (3 hojas) y PDF, limitados a 1.000 registros
- **Subida de imágenes de productos** — Admin Blazor y MAUI, local (dev) o Azure Blob (prod)
- **Infraestructura Azure operativa** — App Service + SQL + Blob Storage + Static Web Apps
- **CI/CD completo** — GitHub Actions para API, Admin y APK Android
- **Test end-to-end de pagos verificado** en producción (Stripe `pm_card_visa`, 1.50 EUR)
- **95 tests unitarios** — HorarioService, AuthService, dominio, validaciones
- Sistema de invitaciones con QR descargable y expiración configurable
- Tema dark & warm consistente en app y panel web
- Health check en `/health` para Azure App Service

---

## Pendiente de implementar

### 🔴 Alta prioridad

#### Push Notifications (FCM)
La infraestructura está preparada pero **no está activa**:
- `FcmService.cs` existe en la API con lógica completa de FCM HTTP v1
- `PushNotificationService.cs` en MAUI es un stub vacío (pendiente de activar)
- `DispositivoToken` y `NotificacionesController` están implementados

Para activarlo:
1. Crear proyecto en [Firebase Console](https://console.firebase.google.com)
2. Descargar `google-services.json` → `CafeIES.MAUI/Platforms/Android/`
3. Descargar `GoogleService-Info.plist` → `CafeIES.MAUI/Platforms/iOS/`
4. Generar Service Account JSON desde Firebase Console → Configuración → Cuentas de servicio
5. Añadir en Azure App Settings: `Fcm__ProjectId` y `Fcm__ServiceAccountJson`
6. Implementar el cuerpo de `PushNotificationService.cs` en MAUI para registrar el token

Sin push notifications, los usuarios deben abrir la app para saber si su pedido está listo.

#### Pedido recuperable tras pago huérfano
Si el usuario paga con Stripe pero cierra la app antes de que se cree el pedido, el pago queda registrado en Stripe pero sin pedido asociado en la base de datos. El webhook lo detecta y registra un warning, pero **no crea el pedido automáticamente**. Habría que implementar esta lógica en el handler del webhook `payment_intent.succeeded`.

### 🟡 Media prioridad

#### Google Play Store
Actualmente la distribución es por GitHub Releases (sideloading). Para Play Store:
- Registrar cuenta Google Play Developer (pago único 25 USD)
- Activar el pipeline de AAB firmado con keystore release (scripts ya preparados en `infra/`)
- Diseñar icono definitivo y capturas de pantalla
- Publicar en canal de prueba interna

#### Paginación en listados de la API
Los endpoints de pedidos y usuarios devuelven todos los registros sin paginar. Con muchos datos, las respuestas serán lentas. Implementar paginación con `?page=1&pageSize=20`.

#### Versionado de API
No hay prefijo de versión (`/api/v1/...`). Cualquier cambio breaking rompe todos los clientes sin posibilidad de migración gradual.

### 🟢 Baja prioridad

#### XAML Compiled Bindings
16 warnings de MAUI sobre bindings no compilados en las vistas. No es un bug, pero activar `MauiEnableXamlCBindingWithSourceCompilation` y añadir `x:DataType` mejoraría el rendimiento de la UI.

#### Tests de integración
Los 95 tests actuales son unitarios. No hay tests de integración que validen los endpoints de la API contra una base de datos real.

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
| Stripe + pagos reales | ✅ Completada | PaymentIntent, WebView con Stripe.js, webhook |
| Reportes e imágenes | ✅ Completada | Excel, PDF, subida de imágenes, tests unitarios |
| Azure + CI/CD | ✅ Completada | App Service, SQL, Blob, Static Web Apps, GitHub Actions |
| Distribución Android | ✅ Completada | APK via GitHub Releases, pipeline automatizado |
| Push Notifications | ⏳ Pendiente | FCM Android + APNs iOS — infraestructura lista, falta activar |
| Pedido huérfano | ⏳ Pendiente | Recuperación automática de pago sin pedido via webhook |
| Google Play Store | ⏳ Pendiente | Requiere cuenta developer (25 USD) |
| Paginación en API | ⏳ Pendiente | Listados con page/pageSize |

---

## Changelog

### v0.11.0 — Auditoría de seguridad y calidad completa (actual)

Revisión exhaustiva línea por línea. 40 problemas identificados y corregidos:

**Crítico:**
- `Task.Result` en `HomeViewModel` → `await Task.WhenAll()` para eliminar deadlock potencial en hilo principal de MAUI

**Seguridad:**
- Claves JWT, Admin y Stripe reemplazadas por placeholders en `appsettings.json`; valores reales solo en `appsettings.Development.json` (gitignored) o Azure App Settings
- Rate limiting extendido: política "general" (60 req/min) en todos los endpoints; política "invitaciones" (5 req/min) en `/validar`
- `DiasValidez` de invitaciones limitado a 1–365 días
- `MetodoPago` validado con `Enum.IsDefined` antes de crear pedido
- Stock negativo bloqueado (`NuevoStock < -1` rechazado)
- Notas de pedido sanitizadas contra XSS antes de persistir
- `LocalBlobStorageService`: validación path-traversal con `Path.GetRelativePath` en lugar de `StartsWith` frágil

**Robustez:**
- Transacción `SERIALIZABLE` → `ReadCommitted` + `[ConcurrencyCheck]` en `Producto.Stock`
- `TimeOnly.Parse` sin try-catch → `TryParse` seguro en `FranjaHoraria.EstaActiva`
- Transacción atómica al guardar RefreshToken en login
- `ConfirmarPagoAsync` (código muerto con datos de tarjeta en bruto) eliminado de `StripeService`
- `AppDelegate.cs` iOS limpiado de referencias Firebase no usadas (4 errores de compilación eliminados)

**Calidad:**
- `DateTime.Now` → `DateTime.UtcNow` en todas las creaciones/inicializaciones
- Compresión HTTP habilitada (`AddResponseCompression`, `EnableForHttps = true`)
- SignalR configurado con `KeepAliveInterval = 15s` y `ClientTimeoutInterval = 30s`
- SignalR se reconecta automáticamente tras refresh exitoso de token
- Fallback directo a `Shell.GoToAsync("//LoginPage")` si `SesionExpiradaMessage` no tiene suscriptores
- Cache de catálogo reducida de 5 minutos a 60 segundos
- `ReportePdfService` limitado a 1.000 registros con nota en el PDF
- `ReportesController` con `LogWarning` si se supera el límite
- `[MinLength(3)]` añadido a `Producto.Nombre` en DTOs
- Validación manual de email redundante eliminada de `AuthController`
- Logging con `ILogger` añadido en `CategoriasController` con prefijo `[AUDIT]`
- Warning CS8826 corregido en `HomeViewModel` (parámetro de partial method inconsistente)

---

### v0.10.0 — Pipeline CI/CD Android + distribución via GitHub Releases

- `AndroidManifest.xml`: permisos explícitos, `allowBackup="false"`, `network_security_config`
- `network_security_config.xml`: cleartext HTTP bloqueado; solo CAs del sistema
- `proguard.cfg`: reglas R8 para Mono runtime, OkHttp y SignalR
- `infra/generar-keystore.ps1`: genera keystore RSA-2048 de 10.000 días
- `.github/workflows/deploy-android.yml`: pipeline operativo con `global.json` para fijar .NET 9 en runner
- `docs/politica-privacidad.html`: página RGPD en GitHub Pages
- Primera distribución: `cafeies-2026.03.23.apk` (14.4 MB)

### v0.9.0 — Despliegue Azure completo y pagos verificados en producción

- Recursos Azure creados: App Service `cafeies-api` (B1, Linux, northeurope), Azure SQL, Blob Storage, Static Web App
- EF Core migrations aplicadas contra Azure SQL; seed inicial ejecutado
- Stripe webhook registrado en producción
- CI/CD corregido: migrado a `azure/login@v2` + `az webapp deploy --type zip`
- Test end-to-end verificado: PaymentIntent → Stripe `pm_card_visa` → Pedido creado (1.50 EUR)
- `confirmation_method` cambiado de `manual` a `automatic` (fix crítico para WebView + Stripe.js)

### v0.8.0 — Infraestructura Azure y CI/CD

- `IBlobStorageService`: local (dev) y Azure Blob Storage (prod) con selección automática
- Health check `GET /health`
- CORS desde `appsettings.Production.json`
- GitHub Actions: `deploy-api.yml` y `deploy-admin.yml`
- `staticwebapp.config.json` para SPA routing de Blazor WASM

### v0.7.0 — Notificaciones push FCM (infraestructura)

- `DispositivoToken` para almacenar tokens FCM
- `FcmService` con FCM HTTP v1 y autenticación OAuth2 via Service Account
- `NotificacionesController` para registro/eliminación de tokens
- Plugin.Firebase.CloudMessaging preparado (requiere configuración real)

### v0.6.0 — Reportes, imágenes y tests

- 95 tests unitarios con xUnit + EF InMemory
- Exportación Excel (ClosedXML) y PDF (QuestPDF)
- Subida de imágenes de productos con protección path-traversal
- Funciones admin desde MAUI: pedidos, productos, usuarios

### v0.5.0 — Seguridad y calidad

- Rate limiting en auth, audit trail, complejidad de contraseña
- Null-safe claims, timeout HTTP, SSL solo en `#if DEBUG`
- RefreshToken solo en memoria en Blazor
- DtoMapperExtensions, ILogger en ApiService

### v0.4.0 — Stripe + Multi-instituto

- Pagos reales con Stripe: PaymentIntent + webhook + verificación server-side
- Multi-instituto: entidad Instituto, claim en JWT, filtros en admin

### v0.3.0 — Auditoría y estabilización

- 11 bugs corregidos: máquina de estados, modales de confirmación, validaciones, paginación

### v0.2.0 — Panel admin y funciones avanzadas

- Panel Blazor WASM completo (8 páginas)
- SignalR tiempo real, invitaciones QR, dashboard

### v0.1.0 — MVP

- API REST con JWT + BCrypt, catálogo, carrito, pedidos, restricción horaria

---

## Licencia

Proyecto privado — uso interno para centros educativos.
