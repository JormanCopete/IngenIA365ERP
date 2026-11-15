using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// 2026-11-12 (decisión del dueño): cómo se busca un producto en las pantallas que lo registran, el parámetro
/// <c>Comercial.BusquedaDeProductos</c> de la cooperativa. Igual en el POS, la factura, el ciclo comercial, compras, traslados, ajustes y
/// conteos.
/// </summary>
public enum ModoDeBusquedaDeProductos
{
    /// <summary>El campo es para el lector o el código (y «3*código»); F2, o un código que no existe, abre la búsqueda por nombre en una ventana.</summary>
    VentanaF2,

    /// <summary>Los resultados salen bajo el campo mientras se escribe (↑ ↓ Enter Esc); el lector y el código + Enter siguen leyendo de una.</summary>
    EnElCampo,
}

/// <summary>
/// Lo que el campo de producto dice de sí mismo según el modo: el texto de ayuda, la leyenda bajo el campo, el texto del campo vacío
/// y lo que hace F2. Salen del modo para que la leyenda diga siempre la verdad; antes cada pantalla la escribía a mano («lector o
/// código + Enter · ↑ ↓ recorren…») y no cambiaba con el modo. (nuevo)
/// </summary>
public static class TextosDeBusquedaDeProductos
{
    /// <summary>Los valores del parámetro (los de <c>ParametrosDeInventario.BusquedaEn*</c> del servidor).</summary>
    public const string ValorVentanaF2 = "VentanaF2";
    public const string ValorEnElCampo = "EnElCampo";

    /// <summary>El modo de un valor del parámetro; lo que no se reconoce es el defecto seguro, la ventana con F2.</summary>
    public static ModoDeBusquedaDeProductos Interpretar(string? valor) =>
        string.Equals(valor?.Trim(), ValorEnElCampo, StringComparison.OrdinalIgnoreCase)
            ? ModoDeBusquedaDeProductos.EnElCampo
            : ModoDeBusquedaDeProductos.VentanaF2;

    /// <summary>El texto del campo vacío. <paramref name="conCantidad"/>: la pantalla registra productos y admite «3*código».</summary>
    public static string Placeholder(ModoDeBusquedaDeProductos modo, bool conCantidad) => modo switch
    {
        ModoDeBusquedaDeProductos.VentanaF2 => conCantidad ? "Lector, código o 3*código (F2 busca por nombre)" : "Lector o código (F2 busca por nombre)",
        _ => "Código, código de barras, nombre o referencia",
    };

    /// <summary>La leyenda bajo el campo.</summary>
    public static string Leyenda(ModoDeBusquedaDeProductos modo, bool conCantidad) => modo switch
    {
        ModoDeBusquedaDeProductos.VentanaF2 => conCantidad
            ? "Lector o código + Enter · 3*código lleva tres · F2 busca por nombre"
            : "Lector o código + Enter · F2 busca por nombre",
        _ => conCantidad
            ? "Lector o código + Enter · 3*código lleva tres · escriba el nombre: ↑ ↓ recorren · Enter elige · Esc cierra"
            : "Lector o código + Enter · escriba el nombre: ↑ ↓ recorren · Enter elige · Esc cierra",
    };

    /// <summary>La ayuda de F1.</summary>
    public static string Ayuda(ModoDeBusquedaDeProductos modo, bool conCantidad)
    {
        var cantidad = conCantidad ? " «3*código» registra tres de una vez." : "";
        return modo switch
        {
            ModoDeBusquedaDeProductos.VentanaF2 =>
                "Lea el código de barras o escriba el código y Enter para elegirlo de una." + cantidad
                + " Para buscar por nombre o referencia pulse F2: se abre la búsqueda con lo que haya escrito. Un código que no existe también la abre.",
            _ =>
                "Lea el código de barras o escriba el código y Enter para elegirlo de una." + cantidad
                + " También puede escribir parte del nombre o la referencia: la lista sale bajo el campo; ↑ ↓ la recorren, Enter elige y Esc la cierra.",
        };
    }

    /// <summary>Lo que hace F2 en la barra de atajos o en la leyenda de una pantalla.</summary>
    public static string AccionDeF2(ModoDeBusquedaDeProductos modo) =>
        modo == ModoDeBusquedaDeProductos.VentanaF2 ? "buscar producto por nombre" : "ir al campo de producto";
}

/// <summary>
/// El modo de búsqueda de la cooperativa activa, pedido una vez (<c>GET /api/inventory/products/search-mode</c>) y recordado mientras no
/// cambie la cooperativa de la sesión y por <see cref="Vigencia"/> (así una vigencia nueva llega sin volver a entrar). Varios campos en la
/// misma pantalla comparten una sola petición. Si no se puede leer (sin sesión, sin permiso de buscar, sin red) vale el defecto seguro,
/// la ventana con F2, y se vuelve a preguntar en un minuto. (nuevo)
/// </summary>
public sealed class BusquedaDeProductosDeLaCooperativa(InventarioClient inventario, RenovadorDeSesion sesion, TimeProvider? reloj = null)
{
    /// <summary>Cuánto se recuerda un modo leído.</summary>
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(10);

    /// <summary>Cuánto se recuerda el defecto cuando no se pudo leer.</summary>
    public static readonly TimeSpan VigenciaDelDefecto = TimeSpan.FromMinutes(1);

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;
    private Task<(ModoDeBusquedaDeProductos Modo, bool Leido)>? _enVuelo;
    private string? _de;
    private DateTimeOffset _vence;

    public async Task<ModoDeBusquedaDeProductos> ModoAsync(CancellationToken ct = default)
    {
        var de = CooperativaDe(sesion.AccessToken);
        var ahora = _reloj.GetUtcNow();
        if (_enVuelo is null || !string.Equals(_de, de, StringComparison.Ordinal) || ahora >= _vence)
        {
            _de = de;
            _vence = ahora + Vigencia;
            _enVuelo = CargarAsync(ct);
        }
        var tarea = _enVuelo;
        var (modo, leido) = await tarea;
        if (!leido && ReferenceEquals(tarea, _enVuelo)) _vence = _reloj.GetUtcNow() + VigenciaDelDefecto;
        return modo;
    }

    /// <summary>Olvida lo leído: la próxima pregunta vuelve a la API.</summary>
    public void Invalidar() => _enVuelo = null;

    private async Task<(ModoDeBusquedaDeProductos, bool)> CargarAsync(CancellationToken ct)
    {
        try
        {
            var r = await inventario.ModoDeBusquedaAsync(ct);
            return r.IsSuccess && r.Value is { } dto
                ? (TextosDeBusquedaDeProductos.Interpretar(dto.Mode), true)
                : (ModoDeBusquedaDeProductos.VentanaF2, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Sin respuesta vale el defecto seguro; se vuelve a preguntar en un minuto.
            return (ModoDeBusquedaDeProductos.VentanaF2, false);
        }
    }

    /// <summary>La cooperativa activa del access (claim <c>active_tenant_id</c>), para no usar el modo de otra tras cambiar de cooperativa.</summary>
    public static string? CooperativaDe(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        try
        {
            var partes = jwt.Split('.');
            if (partes.Length < 2) return null;
            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            return doc.RootElement.TryGetProperty("active_tenant_id", out var t) ? t.ToString() : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }
}
