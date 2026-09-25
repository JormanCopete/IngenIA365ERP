using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// Los filtros comunes de toda vista de <c>/api/reports/inventory/{vista}</c> (feature 012, T44, T182; contracts/api.md
/// §27), tal como llegan por la query string: <c>from</c>, <c>to</c>, <c>asOf</c>, <c>branch</c>, <c>warehouse</c>,
/// <c>pointOfSale</c>, <c>cashRegister</c>, <c>session</c>, <c>product</c>, <c>category</c>, <c>accountingGroup</c>,
/// <c>person</c> y <c>documentType</c>, todos por PublicId (Principio VI), más <c>format</c>. Cada vista usa los que le
/// sirven y lee los propios (<c>groupBy</c>, <c>count</c>, <c>days</c>…) aparte. Es una clase con <c>set</c> porque
/// <c>[AsParameters]</c> la puebla propiedad a propiedad, como <c>AccountingReportsEndpoints.FiltrosQuery</c>. (nuevo)
///
/// <para>
/// El rango es de hasta <see cref="RangoMaximoEnAnios"/> años, como en la 009 (<c>MovimientosContables</c>): un
/// ejercicio completo cabe justo, el tope se compara con el mismo día cinco años después. Sin fechas, el rango es el mes
/// en curso y el corte (<c>asOf</c>) es hoy.
/// </para>
/// </summary>
public sealed class FiltrosDeInformeDeInventario
{
    public const int RangoMaximoEnAnios = 5;
    public const string RangoInvalidoCodigo = "Inventory.Report.RangeInvalid";
    public const string RangoDemasiadoLargoCodigo = "Inventory.Report.RangeTooLong";

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? AsOf { get; set; }
    public Guid? Branch { get; set; }
    public Guid? Warehouse { get; set; }
    public Guid? PointOfSale { get; set; }
    public Guid? CashRegister { get; set; }
    public Guid? Session { get; set; }
    public Guid? Product { get; set; }
    public Guid? Category { get; set; }
    public Guid? AccountingGroup { get; set; }
    public Guid? Person { get; set; }
    public Guid? DocumentType { get; set; }
    /// <summary><c>json</c> (por defecto), <c>xlsx</c>, <c>pdf</c> o <c>docx</c> (<c>FormatosDeInforme</c>).</summary>
    public string? Format { get; set; }

    /// <summary>El inicio del rango: <see cref="From"/> o el primer día del mes de <paramref name="hoy"/>.</summary>
    public DateOnly Desde(DateOnly hoy) => From ?? new DateOnly(hoy.Year, hoy.Month, 1);

    /// <summary>El fin del rango: <see cref="To"/> o <paramref name="hoy"/>.</summary>
    public DateOnly Hasta(DateOnly hoy) => To ?? hoy;

    /// <summary>El corte de las vistas «a una fecha» (valorizado, existencias, conciliación).</summary>
    public DateOnly ALaFecha(DateOnly hoy) => AsOf ?? hoy;

    public static Error RangoInvalido { get; } = new(RangoInvalidoCodigo, "La fecha inicial es posterior a la final.");

    public static Error RangoDemasiadoLargo { get; } = new(RangoDemasiadoLargoCodigo, $"El rango no puede superar {RangoMaximoEnAnios} años.");

    /// <summary>El rango efectivo no va al revés ni abarca más de <see cref="RangoMaximoEnAnios"/> años.</summary>
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
        Fecha("from", From);
        Fecha("to", To);
        Fecha("asOf", AsOf);
        Id("branch", Branch);
        Id("warehouse", Warehouse);
        Id("pointOfSale", PointOfSale);
        Id("cashRegister", CashRegister);
        Id("session", Session);
        Id("product", Product);
        Id("category", Category);
        Id("accountingGroup", AccountingGroup);
        Id("person", Person);
        Id("documentType", DocumentType);
        return d;
    }
}
