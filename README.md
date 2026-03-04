# CaféIES — Documentación completa del proyecto

## Inventario de la solución (68 ficheros)

```
CafeIES/
│
├── CafeIES.sln
│
├── CafeIES.Shared/                    ← Modelos compartidos
│   └── Models/
│       ├── Enums.cs                   Turno, RolUsuario, EstadoPedido, MetodoPago...
│       └── Entities.cs                Usuario, Producto, Pedido, FranjaHoraria, Invitacion...
│
├── CafeIES.API/                       ← Backend ASP.NET Core 8
│   ├── Controllers/
│   │   ├── AuthController.cs          Login, registro alumno/invitado, refresh JWT
│   │   ├── ProductosController.cs     CRUD + toggle activo + actualizar stock
│   │   ├── CategoriasController.cs    CRUD categorías
│   │   ├── PedidosController.cs       Crear pedido (con validación horaria), mis pedidos, estados
│   │   ├── InvitacionesController.cs  Generar/revocar QR+enlace para profe/personal
│   │   └── AdminController.cs         Dashboard, validar alumnos, gestión usuarios, franjas
│   ├── Data/
│   │   ├── AppDbContext.cs            EF Core + seed de categorías y franjas horarias
│   │   └── DbSeeder.cs               Crea el admin inicial al arrancar
│   ├── Services/
│   │   ├── AuthService.cs             JWT, bcrypt, refresh tokens
│   │   └── HorarioService.cs          ⭐ Lógica de restricción horaria por turno
│   ├── DTOs/DTOs.cs                   Todos los DTOs de request/response
│   ├── Hubs/CafeteriaHub.cs           SignalR: pedidos en tiempo real
│   ├── Program.cs                     Setup completo: EF, JWT, CORS, SignalR, Swagger
│   └── appsettings.json               Configura: BBDD, JWT Key, credenciales admin
│
├── CafeIES.MAUI/                      ← App móvil iOS + Android
│   ├── AppShell.xaml(.cs)             Shell con TabBar y rutas registradas
│   ├── MauiProgram.cs                 DI: servicios, ViewModels, páginas
│   ├── Resources/Styles/
│   │   └── AppStyles.xaml             Paleta dark&warm, estilos globales XAML
│   ├── Services/
│   │   ├── ApiService.cs              Todas las llamadas HTTP a la API
│   │   └── TokenService.cs            JWT en SecureStorage (Keychain/EncryptedPrefs)
│   ├── ViewModels/
│   │   ├── LoginViewModel.cs
│   │   ├── RegistroViewModel.cs       Autoregistro alumno con selección de turno
│   │   ├── RegistroInvitacionViewModel.cs  Registro por QR (profe/personal)
│   │   ├── HomeViewModel.cs           Catálogo + estado horario + filtros por categoría
│   │   ├── CarritoViewModel.cs        Gestión carrito + checkout
│   │   ├── PedidosViewModel.cs        Historial de pedidos
│   │   └── DetallePedidoViewModel.cs  Estado en tiempo real via SignalR
│   └── Views/
│       ├── LoginPage.xaml             Login limpio, enlace a registro
│       ├── RegistroPage.xaml          Autoregistro alumno con chips de turno
│       ├── RegistroInvitacionPage.xaml Registro por QR con badge de rol
│       ├── HomePage.xaml              Carta con banner horario, categorías, grid de productos
│       ├── CarritoPage.xaml           Carrito con qty control y pago
│       ├── ConfirmacionPedidoPage.xaml Confirmación con número de pedido
│       ├── PedidosPage.xaml           Historial con estados visuales
│       ├── DetallePedidoPage.xaml     Detalle con barra de progreso de estado
│       └── PerfilPage.xaml            Perfil + turno + cerrar sesión
│
└── CafeIES.Admin/                     ← Panel Blazor WASM (escritorio)
    ├── App.razor
    ├── _Imports.razor
    ├── Layout/
    │   ├── MainLayout.razor           Sidebar + contenido + info usuario
    │   └── EmptyLayout.razor          Layout vacío para la página de login
    ├── Pages/
    │   ├── Login.razor                Login solo para admins
    │   ├── Dashboard.razor            Stats, pedidos en curso (SignalR), alertas stock
    │   ├── Productos.razor            CRUD completo con modal, stock visual, toggle activo
    │   ├── Usuarios.razor             Lista usuarios, validar/rechazar alumnos, cambiar estado
    │   ├── Invitaciones.razor         Generar QR+enlace para profe/personal, gestionar vigencia
    │   └── Horarios.razor             Configurar franjas horarias por turno (sin tocar código)
    ├── Services/
    │   ├── AdminApiService.cs         Todas las llamadas HTTP desde Blazor
    │   └── AuthAdminService.cs        Sesión admin con sessionStorage + refresh token
    └── wwwroot/
        ├── index.html
        └── app.css                    Tema dark&warm completo para Blazor
```

---

## Puesta en marcha rápida

### Requisitos
- **.NET 8 SDK** — [descargar](https://dotnet.microsoft.com/download)
- **SQL Server** (Express o LocalDB vale)
- **Visual Studio 2022 17.8+** con workload MAUI y ASP.NET

### 1. Configurar la API
Edita `CafeIES.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=CafeIES;Trusted_Connection=True;"
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
- **API**: `dotnet run` en `CafeIES.API` → https://localhost:7001
- **Admin**: `dotnet run` en `CafeIES.Admin` → https://localhost:7100
- **MAUI**: F5 desde Visual Studio (Android/iOS emulador)

Al arrancar la API por primera vez verás en consola:
```
✅ Admin creado: admin@cafeies.local
⚠️  Cambia la contraseña tras el primer login.
```

---

## Flujo de registro (blindado)

```
Admin ──────────── Creado automáticamente al arrancar la API
Profesor/Personal ─ Admin genera QR en /invitaciones
                    → se escanea con el móvil
                    → registro inmediato, sin validación
Alumno ────────────  Se registra solo en la app (elige turno, no elige rol)
                    → queda en "Pendiente"
                    → Admin valida en /usuarios
                    → cuenta activa
```

---

## Lógica de horarios

Franjas editables desde `/horarios` en el panel admin.  
Nadie toca código para cambiar los horarios de recreo.

| Turno   | Franja 1           | Franja 2           |
|---------|--------------------|--------------------|
| Mañana  | 07:30 – 08:00      | 11:00 – 11:30      |
| Tarde   | 13:45 – 14:00      | 17:00 – 17:30      |
| Noche   | 20:45 – 21:00      | 23:00 – 23:20      |

Admin, Profesor, Personal → **sin restricción horaria**.

---

## Notificaciones en tiempo real (SignalR)

- La cafetería (panel admin, Dashboard) recibe **pedidos nuevos al instante** sin recargar.
- El alumno ve el **estado de su pedido actualizado en vivo** en DetallePedidoPage.

---

## Próximos pasos sugeridos
1. Integrar pasarela de pago real (Redsys para España / Stripe)
2. Subida de imágenes de productos (Azure Blob / local)
3. Notificaciones push cuando el pedido esté listo (FCM/APNS)
4. Exportación de reportes a Excel/PDF
5. Tests unitarios para HorarioService
