using System.Globalization;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Receipts.Parameters;
using IngenIA365ERP.Domain.Treasury.Gmf;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// Los parámetros <c>TES</c> leídos con su tipo (feature 015, US10, T0143; FR-009, FR-100, FR-173; data-model §23), siempre por el único lector
/// (<see cref="ILectorDeParametros"/>, <c>LosParametrosSeLeenEnUnSoloSitio</c>) a la fecha de la operación. Molde: <c>ParametrosDeCaja</c> de Recaudo. Una clave que
/// no se puede leer cae al defecto seguro de su definición; una «sin valor» queda nula (no rige). Ningún valor legal se escribe aquí. (nuevo)
/// </summary>
public sealed class ParametrosDeTesoreria(ILectorDeParametros lector)
{
    private const string Modulo = ParametrosDeRecaudoYTesoreria.ModuloTesoreria;

    public async Task<bool> SiNoAsync(string clave, DateOnly fecha, CancellationToken ct) =>
        (await ValorAsync(clave, fecha, ct)) is bool b ? b : string.Equals(Defecto(clave), "true", StringComparison.OrdinalIgnoreCase);

    public async Task<int?> EnteroAsync(string clave, DateOnly fecha, CancellationToken ct) =>
        (await ValorAsync(clave, fecha, ct)) switch
        {
            int i => i,
            _ when int.TryParse(Defecto(clave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };

    public async Task<decimal?> DecimalAsync(string clave, DateOnly fecha, CancellationToken ct) =>
        (await ValorAsync(clave, fecha, ct)) switch
        {
            decimal m => m,
            int i => i,
            _ when decimal.TryParse(Defecto(clave), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };

    public async Task<string> TextoAsync(string clave, DateOnly fecha, CancellationToken ct) =>
        (await ValorAsync(clave, fecha, ct)) is string s && s.Length > 0 ? s : Defecto(clave);

    /// <summary>Los parámetros del GMF de egresos vigentes a <paramref name="fecha"/> (tarifa, hechos generadores, exención de desembolsos, redondeo).</summary>
    public async Task<ParametrosDeGmf> GmfAsync(DateOnly fecha, CancellationToken ct) => new(
        await DecimalAsync(ParametrosDeRecaudoYTesoreria.GmfTarifa, fecha, ct) ?? 0m,
        ParametrosDeGmf.Hechos(await TextoAsync(ParametrosDeRecaudoYTesoreria.GmfHechosGeneradoresDeEgresos, fecha, ct)),
        await SiNoAsync(ParametrosDeRecaudoYTesoreria.GmfExencionDesembolsos, fecha, ct),
        CalculoDeGmf.Redondeo(await TextoAsync(ParametrosDeRecaudoYTesoreria.GmfRedondeo, fecha, ct)));

    private async Task<object?> ValorAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Valor : null;
    }

    private static string Defecto(string clave) =>
        CatalogoDeParametros.Buscar(Modulo, clave)?.DefectoSeguro
        ?? ParametrosDeRecaudoYTesoreria.Definiciones.FirstOrDefault(d => d.Modulo == Modulo && d.Clave == clave)?.DefectoSeguro
        ?? string.Empty;
}
