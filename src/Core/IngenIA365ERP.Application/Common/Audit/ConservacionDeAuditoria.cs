using System.Globalization;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// Cuántos años se conserva la auditoría de Recaudo, Tesorería y la lista de go/no-go (feature 015, R1; decisión de R1 sobre <c>Conservacion.Anios</c>; FR-014,
/// FR-178; Principio X): el parámetro <c>Conservacion.Anios</c> con vigencia de cada cooperativa —<c>RCD</c> para Recaudo y go/no-go, <c>TES</c> para Tesorería—
/// vigente a la fecha del evento, <b>nunca menos de cinco años</b> (el piso de SARLAFT, <see cref="ParametrosDeRecaudoYTesoreria.PisoDeConservacion"/>). Hasta
/// R0/T0 esos módulos se conservaban diez años fijos (el defecto del parámetro); los demás módulos siguen con su retención fija (<c>AuditRetention</c>).
/// <para>
/// El reenviador sella los años en la carga del evento antes de calcular su huella (<c>AuditEventDocument.RetentionYears</c>): así el vencimiento queda dentro
/// de lo sellado y no cambia si después se registra otra vigencia del parámetro. (nuevo)
/// </para>
/// </summary>
public sealed class ConservacionDeAuditoria(ILectorDeParametros lector)
{
    /// <summary>¿El módulo de auditoría lee su conservación del parámetro? Recaudo, Tesorería y go/no-go; nulo si no.</summary>
    public static string? ModuloDeParametros(string? moduloDeAuditoria) => moduloDeAuditoria switch
    {
        ModuloDeAuditoria.Receipts or ModuloDeAuditoria.GoLive => ParametrosDeRecaudoYTesoreria.ModuloRecaudo,
        ModuloDeAuditoria.Treasury => ParametrosDeRecaudoYTesoreria.ModuloTesoreria,
        _ => null,
    };

    /// <summary>Los años del módulo a la fecha (piso de cinco); nulo si el módulo no lee el parámetro.</summary>
    public async Task<int?> AniosAsync(string? moduloDeAuditoria, DateOnly fecha, CancellationToken ct)
    {
        if (ModuloDeParametros(moduloDeAuditoria) is not { } modulo) return null;
        var leido = await lector.LeerAsync(modulo, ParametrosDeRecaudoYTesoreria.ConservacionAnios, fecha, ct: ct);
        var anios = leido.IsSuccess
            ? leido.Value.Valor switch
            {
                int i => i,
                long l => (int)l,
                decimal d => (int)d,
                _ => int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : Defecto(),
            }
            : Defecto();
        return Math.Max(anios, ParametrosDeRecaudoYTesoreria.PisoDeConservacion);
    }

    /// <summary>El defecto del catálogo (diez años, «pendiente de validar» por el tributarista, P19).</summary>
    public static int Defecto() =>
        int.TryParse(ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Clave == ParametrosDeRecaudoYTesoreria.ConservacionAnios).DefectoSeguro,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? d : ParametrosDeRecaudoYTesoreria.PisoDeConservacion;

    /// <summary>Cuándo vence un evento ocurrido en <paramref name="ocurrido"/> que se conserva <paramref name="anios"/> años (con el piso).</summary>
    public static DateTime VenceEl(DateTime ocurrido, int anios) => ocurrido.AddYears(Math.Max(anios, ParametrosDeRecaudoYTesoreria.PisoDeConservacion));
}
