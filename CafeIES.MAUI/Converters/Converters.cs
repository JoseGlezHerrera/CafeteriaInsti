using System.Globalization;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Converters;

// ── Bool helpers ──────────────────────────────────────────────────────────────

public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class IntToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StringNotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Bool → Accent (para turno selector en RegistroPage) ──────────────────────

public class BoolToAccentBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#f5a623")
            : Color.FromArgb("#2e2b26");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToAccentBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#1af5a623")
            : Color.FromArgb("#232119");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToAccentTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#f5a623")
            : Color.FromArgb("#7a7468");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Stock level converters

public class StockLevelBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "agotado" => Color.FromArgb("#e05252"),
            "bajo"    => Color.FromArgb("#e8834a"),
            _         => Color.FromArgb("#4caf82")
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StockLevelTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "agotado" => Color.FromArgb("#e05252"),
            "bajo"    => Color.FromArgb("#e8834a"),
            _         => Color.FromArgb("#4caf82")
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StockLevelEmojiConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "agotado" => "⛔",
            "bajo"    => "⚠️",
            _         => "✓"
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StockDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int stock
            ? stock == -1 ? "∞" : stock.ToString()
            : "—";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Estado pedido converters (PedidosPage) ───────────────────────────────────

public class EstadoPedidoBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoPedido e ? e switch
        {
            EstadoPedido.Pendiente     => Color.FromArgb("#1af5a623"),
            EstadoPedido.EnPreparacion => Color.FromArgb("#1ae8834a"),
            EstadoPedido.Listo         => Color.FromArgb("#1a4caf82"),
            EstadoPedido.Entregado     => Color.FromArgb("#1a4caf82"),
            EstadoPedido.Cancelado     => Color.FromArgb("#1ae05252"),
            _ => Colors.Transparent
        } : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EstadoPedidoBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoPedido e ? e switch
        {
            EstadoPedido.Pendiente     => Color.FromArgb("#40f5a623"),
            EstadoPedido.EnPreparacion => Color.FromArgb("#40e8834a"),
            EstadoPedido.Listo         => Color.FromArgb("#404caf82"),
            EstadoPedido.Entregado     => Color.FromArgb("#404caf82"),
            EstadoPedido.Cancelado     => Color.FromArgb("#40e05252"),
            _ => Colors.Transparent
        } : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EstadoPedidoLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoPedido e ? e switch
        {
            EstadoPedido.Pendiente     => "🧾 Pendiente",
            EstadoPedido.EnPreparacion => "👨‍🍳 Preparando",
            EstadoPedido.Listo         => "🔔 Listo",
            EstadoPedido.Entregado     => "✅ Entregado",
            EstadoPedido.Cancelado     => "❌ Cancelado",
            _ => ""
        } : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EstadoPedidoTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoPedido e ? e switch
        {
            EstadoPedido.Pendiente     => Color.FromArgb("#f5a623"),
            EstadoPedido.EnPreparacion => Color.FromArgb("#e8834a"),
            EstadoPedido.Listo         => Color.FromArgb("#4caf82"),
            EstadoPedido.Entregado     => Color.FromArgb("#4caf82"),
            EstadoPedido.Cancelado     => Color.FromArgb("#e05252"),
            _ => Color.FromArgb("#7a7468")
        } : Color.FromArgb("#7a7468");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Líneas resumen (lista → string como "2× Bocadillo, 1× Café") ────────────

public class LineasResumenConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<LineaPedidoDto> lineas) return "";
        return string.Join(", ", lineas.Select(l => $"{l.Cantidad}× {l.ProductoNombre}"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: estado pedido es igual a parámetro ─────────────────────────────────

public class EstadoEsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: pedido cancelable (Pendiente o EnPreparacion) ─────────────────────

public class EstadoCancelableConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoPedido e && (e == EstadoPedido.Pendiente || e == EstadoPedido.EnPreparacion);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: estado cuenta ──────────────────────────────────────────────────────

public class EstadoCuentaTextoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoCuenta e ? e switch
        {
            EstadoCuenta.Activa              => "Activa",
            EstadoCuenta.PendienteValidacion => "Pendiente",
            EstadoCuenta.Suspendida          => "Suspendida",
            EstadoCuenta.Rechazada           => "Rechazada",
            _                               => e.ToString()
        } : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EstadoCuentaBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoCuenta e ? e switch
        {
            EstadoCuenta.Activa              => Color.FromArgb("#1a4caf82"),
            EstadoCuenta.PendienteValidacion => Color.FromArgb("#1af5a623"),
            EstadoCuenta.Suspendida          => Color.FromArgb("#1ae05252"),
            EstadoCuenta.Rechazada           => Color.FromArgb("#1ae05252"),
            _                               => Colors.Transparent
        } : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class EstadoCuentaTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoCuenta e ? e switch
        {
            EstadoCuenta.Activa              => Color.FromArgb("#4caf82"),
            EstadoCuenta.PendienteValidacion => Color.FromArgb("#f5a623"),
            EstadoCuenta.Suspendida          => Color.FromArgb("#e05252"),
            EstadoCuenta.Rechazada           => Color.FromArgb("#e05252"),
            _                               => Color.FromArgb("#7a7468")
        } : Color.FromArgb("#7a7468");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: estado cuenta - borde ────────────────────────────────────────────

public class EstadoCuentaBorderConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EstadoCuenta e && e == EstadoCuenta.Suspendida
            ? Color.FromArgb("#50e05252")
            : Color.FromArgb("#2e2b26");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: rol usuario ────────────────────────────────────────────────────────

public class RolTextoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RolUsuario r ? r switch
        {
            RolUsuario.Alumno   => "🎓 Alumno",
            RolUsuario.Profesor => "👨‍🏫 Profesor",
            RolUsuario.Personal => "🏢 Personal",
            RolUsuario.Empleado => "☕ Empleado",
            RolUsuario.Admin    => "👑 Admin",
            _                  => r.ToString()
        } : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: producto activo ────────────────────────────────────────────────────

public class ActivoTextoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? (b ? "Activo" : "Oculto") : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ActivoBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b
            ? (b ? Color.FromArgb("#1a4caf82") : Color.FromArgb("#1a7a7468"))
            : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ActivoTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b
            ? (b ? Color.FromArgb("#4caf82") : Color.FromArgb("#7a7468"))
            : Color.FromArgb("#7a7468");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: rol no es admin ────────────────────────────────────────────────────

public class RolNoEsAdminConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RolUsuario r && r != RolUsuario.Admin;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Admin: rol es alumno ──────────────────────────────────────────────────────

public class RolEsAlumnoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is RolUsuario r && r == RolUsuario.Alumno;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Stock: nuevos converters para ocultar cantidad al usuario ─────────────────

/// <summary>Devuelve true si Stock == 0 (mostrar badge "Agotado").</summary>
public class StockAcabadoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int stock && stock == 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Devuelve true si Stock != 0 (el botón + está habilitado).</summary>
public class StockDisponibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int stock && stock != 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Devuelve 0.4 si Stock == 0, 1.0 si no.</summary>
public class StockToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int stock && stock == 0 ? 0.4 : 1.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Invitación: estado (válida/expirada) ──────────────────────────────────────

public class InvitacionEstadoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? (b ? "Activa" : "Inválida") : "—";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Ingredientes personalizables ─────────────────────────────────────────────

/// <summary>AccionIngrediente → prefijo de texto: "sin" para Quitar, "+" para Añadir.</summary>
public class AccionIngredienteConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is AccionIngrediente a
            ? a == AccionIngrediente.Quitar ? "sin" : "+"
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Para ingredientes de pedido: devuelve el sufijo " ×N (+X,XX€)" según cantidad y precio aplicado.
/// Muestra ×N solo cuando Cantidad &gt; 1; precio solo cuando PrecioAplicado &gt; 0.
/// Ejemplo: " ×2 (+1,00€)" o " (+0,50€)" o " ×3" o "".
/// </summary>
public class PrecioIngredienteConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LineaPedidoIngredienteDto ing) return string.Empty;
        var sb = new System.Text.StringBuilder();
        if (ing.Cantidad > 1)
            sb.Append($" ×{ing.Cantidad}");
        var extra = ing.PrecioAplicado * ing.Cantidad;
        if (extra > 0)
            sb.Append($" (+{extra:F2}€)");
        return sb.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Devuelve true si la colección no es null y tiene al menos 1 elemento.</summary>
public class ListNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Collections.ICollection col) return col.Count > 0;
        if (value is IEnumerable<object> seq) return seq.Any();
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// ── Alérgenos: IReadOnlyList<AlergenoDto> → string de emojis ─────────────────

public class AlergenosToEmojiConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<AlergenoDto> alergenos) return string.Empty;
        var lista = alergenos.ToList();
        return lista.Count == 0 ? string.Empty : string.Join(" ", lista.Select(a => a.Emoji));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class AlergenosVisibleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<AlergenoDto> alergenos) return false;
        return alergenos.Any();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Convierte el enum Turno a texto con emoji: 🌅 Mañana, 🌤️ Tarde, 🌙 Noche</summary>
public class TurnoDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            Turno.Manana => "🌅 Mañana",
            Turno.Tarde  => "🌤️ Tarde",
            Turno.Noche  => "🌙 Noche",
            _            => value?.ToString() ?? string.Empty
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte un DateTime UTC a hora local del dispositivo y lo formatea.
/// Usar ConverterParameter para pasar el formato (defecto: "dd/MM/yyyy HH:mm").
/// </summary>
public class UtcToLocalDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return string.Empty;
        var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        var fmt   = parameter as string ?? "dd/MM/yyyy HH:mm";
        return local.ToString(fmt, CultureInfo.CurrentCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>decimal > 0 → true (para mostrar precio extra de ingrediente)</summary>
public class DecimalPositivoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal d && d > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true → ChipButtonActive style, false → ChipButton style</summary>
public class BoolToChipStyleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var active = value is bool b && b;
        var key = active ? "ChipButtonActive" : "ChipButton";
        if (Application.Current?.Resources.TryGetValue(key, out var style) == true)
            return style;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
