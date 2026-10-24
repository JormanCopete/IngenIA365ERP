using System.Globalization;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// Los parámetros <c>RCD</c> de la caja leídos con su tipo (feature 015, T0072, T0092; FR-009, FR-173; data-model §23), siempre por el único lector
/// (<see cref="ILectorDeParametros"/>, <c>LosParametrosSeLeenEnUnSoloSitio</c>) a la fecha de la operación. Una clave que no se puede leer cae al defecto
/// seguro de su definición; una «sin valor» queda nula (no rige). Ningún valor legal se escribe aquí: los defectos viven en
/// <see cref="ParametrosDeRecaudoYTesoreria"/>. (nuevo)
/// </summary>
public sealed class ParametrosDeCaja(ILectorDeParametros lector)
{
    private const string Modulo = ParametrosDeRecaudoYTesoreria.ModuloRecaudo;

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

    public async Task<TimeOnly> HoraAsync(string clave, DateOnly fecha, CancellationToken ct) =>
        (await ValorAsync(clave, fecha, ct)) is TimeOnly h
            ? h
            : TimeOnly.ParseExact(Defecto(clave), "HH:mm", CultureInfo.InvariantCulture);

    private async Task<object?> ValorAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(Modulo, clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Valor : null;
    }

    private static string Defecto(string clave) =>
        CatalogoDeParametros.Buscar(Modulo, clave)?.DefectoSeguro
        ?? ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Modulo == Modulo && d.Clave == clave).DefectoSeguro;
}
