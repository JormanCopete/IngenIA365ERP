using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Reports;

/// <summary>
/// Lo que declara una vista del centro de informes de Recaudo (<c>/api/reports/receipts/{vista}</c>) o de Tesorería (<c>/api/reports/treasury/{vista}</c>)
/// (feature 015, US21, T0314; R-36; api.md §17): su clave, su nombre, su descripción, el nombre del archivo exportado, los filtros comunes que usa y los propios.
/// Va como metadata de la ruta para el registro de vistas (<c>GET /api/reports/receipts</c>), igual que <c>VistaDeInformeDeCartera</c>. Vive en Common porque
/// los dos módulos no se conocen entre sí (<c>RecaudoYTesoreriaSeHablanPorSusPuertos</c>). (nuevo)
/// </summary>
public sealed record VistaDeInformeDeRecaudoYTesoreria(
    string Key,
    string Name,
    string Description,
    string FileName,
    IReadOnlyList<string> Filters,
    IReadOnlyList<string>? OwnFilters = null);

/// <summary>
/// Los filtros comunes de las vistas de Recaudo y Tesorería tal como llegan por la query (feature 015, US21, T0314): <c>from</c>, <c>to</c>, <c>asOf</c>,
/// <c>branch</c> (la oficina, <c>COR_Branches</c>), <c>point</c> y <c>cashBox</c> (Recaudo) y <c>account</c> (la cuenta propia, Tesorería), todos por PublicId
/// (Principio VI), y <c>groupBy</c> (el propio de <c>daily-collection</c>), más <c>format</c>. Clase con <c>set</c> porque <c>[AsParameters]</c> la puebla. Sin
/// fechas, el rango es el mes en curso y el corte es hoy, en la fecha local; el rango no va al revés ni pasa de un año. (nuevo)
/// </summary>
public sealed class FiltrosDeInformeDeRecaudoYTesoreria
{
    public const int RangoMaximoEnDias = 366;
    public const string RangoInvalidoCodigo = "Reports.ReceiptsTreasury.RangeInvalid";
    public const string FiltroInvalidoCodigo = "Reports.ReceiptsTreasury.FilterInvalid";

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? AsOf { get; set; }
    public Guid? Branch { get; set; }
    public Guid? Point { get; set; }
    public Guid? CashBox { get; set; }
    public Guid? Account { get; set; }
    public string? GroupBy { get; set; }
    public string? Format { get; set; }

    public DateOnly Desde(DateOnly hoy) => From ?? new DateOnly(hoy.Year, hoy.Month, 1);

    public DateOnly Hasta(DateOnly hoy) => To ?? hoy;

    public DateOnly ALaFecha(DateOnly hoy) => AsOf ?? hoy;

    public Result ValidarRango(DateOnly hoy)
    {
        var desde = Desde(hoy);
        var hasta = Hasta(hoy);
        if (hasta < desde || hasta.DayNumber - desde.DayNumber > RangoMaximoEnDias)
            return Result.Failure(new Error(RangoInvalidoCodigo, $"El rango va de «desde» a «hasta» y no pasa de {RangoMaximoEnDias} días."));
        return Result.Success();
    }

    public static Error FiltroInvalido(string filtro, string? valor, IEnumerable<string> admitidos)
    {
        var lista = admitidos.ToList();
        return new ErrorConDatos(FiltroInvalidoCodigo, $"«{valor}» no es un valor de {filtro}. Admite: {string.Join(", ", lista)}.",
            new { filter = filtro, value = valor, allowed = lista });
    }

    /// <summary>Los filtros que vinieron (sin el formato), para la auditoría de la exportación.</summary>
    public IReadOnlyDictionary<string, string> ParaAuditoria()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void F(string k, DateOnly? v) { if (v is { } x) d[k] = x.ToString("yyyy-MM-dd"); }
        void G(string k, Guid? v) { if (v is { } x) d[k] = x.ToString(); }
        F("from", From);
        F("to", To);
        F("asOf", AsOf);
        G("branch", Branch);
        G("point", Point);
        G("cashBox", CashBox);
        G("account", Account);
        if (!string.IsNullOrWhiteSpace(GroupBy)) d["groupBy"] = GroupBy.Trim();
        return d;
    }
}
