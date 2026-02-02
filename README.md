# ☕ CafESapp

Aplicación móvil multiplataforma desarrollada con **.NET MAUI** que simula el flujo completo de compra de una cafetería de instituto: listado de productos, favoritos, carrito y confirmación de pedido.

Proyecto enfocado a aplicar **MVVM, navegación con Shell y servicios compartidos**.

---

## 📱 Funcionalidades

- Listado de productos
- Filtrado por categoría
- Productos favoritos
- Detalle de producto
- Carrito de compra
- Modificación de cantidades
- Cálculo automático de total
- Confirmación de pedido

---

## 🧱 Arquitectura

- **.NET MAUI**
- **MVVM**
- **Shell Navigation**
- **Dependency Injection**
- Estado global mediante servicios

Estructura:

Models/ -> Entidades de dominio
ViewModels/ -> Lógica de presentación
Views/ -> Interfaces XAML
Services/ -> Estado compartido (carrito, productos, favoritos)
Converters/ -> Conversores XAML

---

## 🛠 Tecnologías

- C#
- .NET MAUI
- XAML
- MVVM
- Android / iOS / Windows / MacCatalyst

---

## 🚀 Ejecución

1. Clonar repositorio
2. Abrir `CafeteriaInsti.slnx` en Visual Studio
3. Restaurar paquetes
4. Seleccionar plataforma (Android recomendado)
5. Ejecutar

---

## ⚠️ Limitaciones actuales

- Sin persistencia de datos
- Sin backend
- Sin autenticación
- Datos cargados en memoria
- Sin tests automatizados

---

## 🔮 Mejoras futuras

- Persistencia con SQLite
- Historial de pedidos
- Usuarios / login
- Backend REST
- Tests unitarios
- CI con GitHub Actions

---

## 🧾 Notas

Proyecto orientado a aprender arquitectura MAUI moderna.
