# CaféIES — Sistema de pedidos de cafetería para institutos

> App móvil + panel de administración para gestionar pedidos de cafetería en centros educativos.  
> Actualmente en fase de pruebas internas — preparando despliegue multi-instituto y Play Store.

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-Android%20%7C%20iOS%20%7C%20Windows-blue?logo=dotnet)](https://learn.microsoft.com/dotnet/maui/)
[![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-purple?logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)

---

## Tabla de contenidos

- [Arquitectura](#arquitectura)
- [Stack tecnológico](#stack-tecnológico)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Puesta en marcha](#puesta-en-marcha)
- [Flujo de registro](#flujo-de-registro)
- [Lógica de horarios](#lógica-de-horarios)
- [Notificaciones en tiempo real](#notificaciones-en-tiempo-real-signalr)
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
└─────────────────┘                      │  SQL Server + EF Core│
                                         │  JWT + BCrypt        │
┌─────────────────┐     HTTPS/JSON      │  SignalR Hub         │
│  CafeIES.Admin  │◄───────────────────►│                      │
│  (Blazor WASM)   │     SignalR WS      └──────────────────────┘
└─────────────────┘
                                         ┌──────────────────────┐
        Ambos comparten ────────────────►│   CafeIES.Shared     │
                                         │  (DTOs, Entidades,   │
                                         │   Enums)             │
                                         └──────────────────────┘
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
| Tiempo real | SignalR | — |
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
├── CafeIES.Shared/                    ← Modelos compartidos (DTOs, Entidades, Enums)
│   └── Models/
│       ├── Enums.cs                   Turno, RolUsuario, EstadoPedido, MetodoPago
│       ├── Entities.cs                Usuario, Producto, Pedido, FranjaHoraria, Invitacion
│       └── DTOs.cs                    Todos los DTOs de request/response
│
├── CafeIES.API/                       ← Backend ASP.NET Core 9 (puerto 50658)
│   ├── Controllers/
│   │   ├── AuthController.cs          Login, registro alumno/invitado, refresh JWT
│   │   ├── ProductosController.cs     CRUD + toggle activo + actualizar stock
│   │   ├── CategoriasController.cs    CRUD categorías
│   │   ├── PedidosController.cs       Crear pedido (validación horaria + stock transaccional)
│   │   ├── InvitacionesController.cs  Generar/revocar QR+enlace para profe/personal
│   │   └── AdminController.cs         Dashboard, validar alumnos, gestión usuarios, franjas, reportes
│   ├── Data/
│   │   ├── AppDbContext.cs            EF Core + seed de categorías y franjas horarias
│   │   └── DbSeeder.cs               Crea el admin inicial al arrancar
│   ├── Services/
│   │   ├── AuthService.cs             JWT (access 1h + refresh 30d), BCrypt, token rotation
│   │   └── HorarioService.cs          Lógica de restricción horaria por turno
│   ├── Hubs/CafeteriaHub.cs           SignalR: grupos cafeteria + user-{id}
│   ├── Program.cs                     Setup: EF, JWT, CORS, SignalR, Swagger
│   └── appsettings.json               BBDD, JWT Key, credenciales admin
│
├── CafeIES.MAUI/                      ← App móvil (Android + iOS + Windows)
│   ├── AppShell.xaml(.cs)             Shell con TabBar + rutas + visibilidad por rol
│   ├── MauiProgram.cs                 DI: servicios, ViewModels, páginas
│   ├── Services/
│   │   ├── ApiService.cs              HTTP client con auto-refresh de tokens
│   │   └── TokenService.cs            JWT en SecureStorage (Keychain/EncryptedPrefs)
│   ├── Converters/Converters.cs       Conversores XAML (stock, estado, visibilidad)
│   ├── ViewModels/
│   │   ├── LoginViewModel.cs          Login con validación
│   │   ├── RegistroViewModel.cs       Autoregistro alumno (selección de turno)
│   │   ├── RegistroInvitacionViewModel.cs  Registro por QR (profe/personal)
│   │   ├── HomeViewModel.cs           Catálogo + horario + filtros + cache local 5min
│   │   ├── CarritoViewModel.cs        Carrito + checkout + validación stock
│   │   ├── PedidosViewModel.cs        Historial paginado
│   │   ├── DetallePedidoViewModel.cs  Estado en tiempo real via SignalR
│   │   ├── AdminPedidosViewModel.cs   Gestión pedidos (admin móvil)
│   │   ├── AdminProductosViewModel.cs CRUD productos (admin móvil)
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
    │   ├── AdminApiService.cs         HTTP client con auto-refresh
    │   └── AuthAdminService.cs        Sesión en sessionStorage + refresh token
    └── wwwroot/
        └── css/app.css                Tema dark & warm completo
```

---

## Puesta en marcha

### Requisitos
- **.NET 9 SDK** — [descargar](https://dotnet.microsoft.com/download)
- **SQL Server** (Express o LocalDB)
- **Visual Studio 2022 17.12+** con workloads: **.NET MAUI** y **ASP.NET and web development**

### 1. Configurar la API

Edita `CafeIES.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=CafeIES;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Jwt": {
    "Key": "pon-aqui-32-caracteres-minimo-cambialo!"
  },
  "Admin": {
    "Email":    "admin@cafeies.local",
    "Password": "CambiaEsto2024!"
  }
}
```

### 2. Primera migración
```bash
cd CafeIES.API
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 3. Arrancar los 3 proyectos

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

> **MAUI en Android emulador**: la API se alcanza en `10.0.2.2:50658` (ya configurado en `ApiService.cs`).

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

---

## Seguridad

| Mecanismo | Detalle |
|-----------|---------|
| Contraseñas | BCrypt con workFactor 12 |
| JWT Access Token | Duración 1 hora, firmado con HMAC-SHA256 |
| JWT Refresh Token | Duración 30 días, rotación en cada uso |
| Auto-refresh | MAUI (`ApiService`) y Blazor (`AuthAdminService`) renuevan tokens transparentemente |
| Almacenamiento | MAUI: `SecureStorage` (Keychain/EncryptedSharedPreferences). Blazor: `sessionStorage` |
| Stock | Transacciones SQL para evitar sobreventa concurrente |
| Pedidos | Máquina de estados: solo transiciones válidas permitidas |
| Ownership | Los usuarios solo ven/cancelan sus propios pedidos |

---

## Estado actual del proyecto

### Funcionalidades completadas

- [x] Registro de alumnos con selección de turno
- [x] Registro de profesores/personal por invitación QR
- [x] Login/logout con JWT + refresh automático
- [x] Catálogo de productos con categorías, filtros y búsqueda
- [x] Carrito de compras con control de cantidad (tope de stock)
- [x] Creación de pedidos con validación horaria y de stock
- [x] Historial de pedidos con paginación
- [x] Detalle de pedido en tiempo real (SignalR)
- [x] Panel admin web completo (Dashboard, Productos, Categorías, Usuarios, Pedidos, Horarios, Invitaciones, Reportes)
- [x] Funciones admin desde la app móvil (pedidos, productos, usuarios)
- [x] Tema dark & warm consistente en app y panel web
- [x] Cache local de catálogo (5 min) para rendimiento
- [x] Modales de confirmación en acciones destructivas
- [x] Validación de franjas horarias

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

### Fase 1 — Pasarela de pago real
- [ ] Integrar pasarela de pago (Redsys para España / Stripe como alternativa)
- [ ] Flujo de pago en la app: selección de método, confirmación, recibo
- [ ] Webhook de confirmación de pago en la API
- [ ] Registro de transacciones y conciliación

### Fase 2 — Despliegue y Play Store
- [ ] Desplegar API en servidor remoto (Azure App Service / VPS)
- [ ] Desplegar panel admin Blazor en hosting estático o junto a la API
- [ ] Configurar dominio y certificado HTTPS
- [ ] Preparar la app MAUI para publicación en Google Play Store (firma, manifest, ficha)
- [ ] Distribución interna / beta cerrada para pruebas con usuarios reales

### Fase 3 — Soporte multi-instituto
- [ ] Añadir entidad `Instituto` a la base de datos (id, nombre, dirección, logo)
- [ ] Asociar cada usuario a un instituto en el registro
- [ ] Selector de instituto en las pantallas de registro (alumno e invitación)
- [ ] Filtrar pedidos, usuarios y reportes por instituto en el panel admin
- [ ] Dashboard con vista agregada y desglose por instituto
- [ ] Cada instituto puede tener sus propias franjas horarias
- [ ] Soporte para 3 institutos iniciales (escalable a más)

### Fase 4 — Mejoras adicionales
- [ ] Subida de imágenes de productos (Azure Blob Storage / servidor)
- [ ] Notificaciones push cuando el pedido esté listo (FCM para Android, APNS para iOS)
- [ ] Exportación de reportes a Excel/PDF
- [ ] Tests unitarios para servicios críticos (HorarioService, AuthService, stock)

---

## Changelog

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
