using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// 2026-11-26 (pedido del dueño): <b>un recibo de caja, un comprobante</b>. Con <see cref="Clave"/> encendido (parámetro del sistema, de fábrica
/// «false»), lo que Recaudo contabiliza del recibo de caja (el RR: caja contra el puente) y lo que Cartera contabiliza de cada obligación que el recibo
/// abona (créditos, aportes, ahorro: puente contra la obligación) salen en <b>un solo comprobante</b>, al instante, para que la contadora revise un
/// recibo en un solo lugar. El puente queda a la vista: entra y sale en el mismo comprobante.
///
/// <para>
/// Vive en <c>Common</c> porque lo leen dos módulos que no se conocen: Recaudo, que con él encendido pasa el recibo de caja en línea aunque su
/// <c>Contabilidad.ModoDePaso</c> diga «PorLotes» (si no, no habría con qué juntarlo), y Contabilidad, que junta las unidades al consumir. Ni Recaudo ni
/// Cartera cambian lo que emiten.
/// </para>
/// </summary>
public static class ReciboEnUnSoloComprobante
{
    /// <summary>La clave en <c>COR_SystemSettings</c> (módulo <c>ACC</c>; la siembra <c>SystemParametersSeeder</c> en «false»).</summary>
    public const string Clave = "Accounting.CashReceiptSingleVoucher";

    /// <summary>El prefijo de la clave de grupo del recibo con su caja (Recaudo y Cartera juntos).</summary>
    public const string PrefijoDeGrupo = "ReciboCaja|";

    public static bool EsClaveDeGrupo(string? clave) => clave is not null && clave.StartsWith(PrefijoDeGrupo, StringComparison.Ordinal);

    /// <summary>Lo guardado como sí; cualquier otra cosa es «no».</summary>
    public static bool Encendido(string? valor)
    {
        var v = valor?.Trim();
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "sí", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "si", StringComparison.OrdinalIgnoreCase) || v == "1";
    }

    /// <summary>Si la cooperativa lo tiene encendido. Sólo lee.</summary>
    public static async Task<bool> EncendidoAsync(IApplicationDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return Encendido(valor);
    }
}
