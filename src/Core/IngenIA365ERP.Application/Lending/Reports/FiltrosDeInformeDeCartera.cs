using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>
/// Los filtros comunes de toda vista de <c>/api/reports/lending/{vista}</c> (feature 013, US15, T0795, T99; contracts/api.md §17,
/// decisiones-transversales §2.15), tal como llegan por la query string: <c>from</c>, <c>to</c>, <c>asOf</c> (fecha de corte), <c>branch</c>
/// (la sucursal contable, <c>COR_Branches</c>), <c>line</c> (la línea de crédito) y <c>advisor</c> (el usuario que radicó) por PublicId
/// (Principio VI); <c>modality</c> y <c>subsegment</c> por código; <c>payer</c> (la pagaduría, K2), <c>collector</c> (el código del
/// recaudador) y <c>stage</c> (la etapa de cobranza, K4), más <c>format</c>. Cada vista usa los que le sirven y lee los propios
/// (<c>groupBy</c>, <c>dateKind</c>, <c>ranges</c>, <c>points</c>…) aparte. Es una clase con <c>set</c> porque <c>[AsParameters]</c> la
/// puebla propiedad a propiedad. Molde: <c>FiltrosDeInformeDeInventario</c>. (nuevo)
///
/// <para>
/// El rango es de hasta <see cref="RangoMaximoEnAnios"/> años (el mismo día cinco años después ya no cabe) y no va al revés: 422
/// <see cref="RangoInvalidoCodigo"/> / <see cref="RangoDemasiadoLargoCodigo"/>. Sin fechas, el rango es el mes en curso y el corte es
/// hoy, siempre en la <b>fecha local</b> (<c>IDateTimeService.HoyLocal</c>, <c>LaCarteraUsaLaFechaLocal</c>). Un filtro propio con un
/// valor que la vista no admite es 422 <see cref="FiltroInvalidoCodigo"/> con los admitidos (<see cref="FiltroInvalido"/>).
/// </para>
/// </summary>
public sealed class FiltrosDeInformeDeCartera
{
    public const int RangoMaximoEnAnios = 5;
    public const string RangoInvalidoCodigo = "Lending.Report.RangeInvalid";
    public const string RangoDemasiadoLargoCodigo = "Lending.Report.RangeTooLong";
    public const string FiltroInvalidoCodigo = "Lending.Report.FilterInvalid";

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? AsOf { get; set; }
    public Guid? Branch { get; set; }
    public Guid? Line { get; set; }
    public string? Modality { get; set; }
    public string? Subsegment { get; set; }
    public Guid? Advisor { get; set; }
    public Guid? Payer { get; set; }
    public string? Collector { get; set; }
    public string? Stage { get; set; }
    /// <summary><c>json</c> (por defecto), <c>xlsx</c>, <c>pdf</c> o <c>docx</c> (<c>FormatosDeInforme</c>).</summary>
    public string? Format { get; set; }

    /// <summary>El inicio del rango: <see cref="From"/> o el primer día del mes de <paramref name="hoy"/>.</summary>
    public DateOnly Desde(DateOnly hoy) => From ?? new DateOnly(hoy.Year, hoy.Month, 1);

    /// <summary>El fin del rango: <see cref="To"/> o <paramref name="hoy"/>.</summary>
    public DateOnly Hasta(DateOnly hoy) => To ?? hoy;

    /// <summary>El corte de las vistas «a una fecha» (edades, usura): <see cref="AsOf"/> u hoy en la fecha local.</summary>
    public DateOnly ALaFecha(DateOnly hoy) => AsOf ?? hoy;

    public static Error RangoInvalido { get; } = new(RangoInvalidoCodigo, "La fecha inicial es posterior a la final.");

    public static Error RangoDemasiadoLargo { get; } = new(RangoDemasiadoLargoCodigo, $"El rango no puede superar {RangoMaximoEnAnios} años.");

    /// <summary>Un filtro propio con un valor que la vista no admite, con los valores que sí admite (<c>data.filter</c>, <c>data.allowed</c>).</summary>
    public static Error FiltroInvalido(string filtro, string? valor, IEnumerable<string> admitidos)
    {
        var lista = admitidos.ToList();
        return new ErrorConDatos(FiltroInvalidoCodigo, $"«{valor}» no es un valor de {filtro}. Admite: {string.Join(", ", lista)}.",
            new { filter = filtro, value = valor, allowed = lista });
    }

    /// <summary>Un filtro propio obligatorio que no vino (<c>load</c> de la conciliación de la migración).</summary>
    public static Error FiltroObligatorio(string filtro, string descripcion) =>
        new ErrorConDatos(FiltroInvalidoCodigo, $"Falta {filtro}: {descripcion}", new { filter = filtro, value = (string?)null, allowed = Array.Empty<string>() });

    /// <summary>
    /// El valor de un filtro propio de lista cerrada: <paramref name="defecto"/> si no vino, el admitido (con la grafía de la lista, sin
    /// distinguir mayúsculas) si vino uno de la lista, o <see cref="FiltroInvalido"/>.
    /// </summary>
    public static Result<string> Elegir(string filtro, string? valor, IReadOnlyList<string> admitidos, string defecto)
    {
        if (string.IsNullOrWhiteSpace(valor)) return Result.Success(defecto);
        var elegido = admitidos.FirstOrDefault(a => string.Equals(a, valor.Trim(), StringComparison.OrdinalIgnoreCase));
        return elegido is null ? Result.Failure<string>(FiltroInvalido(filtro, valor, admitidos)) : Result.Success(elegido);
    }

    /// <summary>El rango efectivo no va al revés ni abarca <see cref="RangoMaximoEnAnios"/> años o más.</summary>
    public Result ValidarRango(DateOnly hoy)
    {
        var desde = Desde(hoy);
        var hasta = Hasta(hoy);
        if (hasta < desde) return Result.Failure(RangoInvalido);
        if (hasta >= desde.AddYears(RangoMaximoEnAnios)) return Result.Failure(RangoDemasiadoLargo);
        return Result.Success();
    }

    /// <summary>Los filtros que vinieron (sin <see cref="Format"/>, que el evento lleva aparte), para la auditoría de la exportación.</summary>
    public IReadOnlyDictionary<string, string> ParaAuditoria()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void Fecha(string clave, DateOnly? valor) { if (valor is { } v) d[clave] = v.ToString("yyyy-MM-dd"); }
        void Id(string clave, Guid? valor) { if (valor is { } v) d[clave] = v.ToString(); }
        void Texto(string clave, string? valor) { if (!string.IsNullOrWhiteSpace(valor)) d[clave] = valor.Trim(); }
        Fecha("from", From);
        Fecha("to", To);
        Fecha("asOf", AsOf);
        Id("branch", Branch);
        Id("line", Line);
        Texto("modality", Modality);
        Texto("subsegment", Subsegment);
        Id("advisor", Advisor);
        Id("payer", Payer);
        Texto("collector", Collector);
        Texto("stage", Stage);
        return d;
    }
}
