using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Regulatory;

/// <summary>
/// Una advertencia de una generación regulatoria (feature 013, K5, US16; adaptadores-y-archivos.md §7.3–§7.4; api.md §1.9): no detiene nada, pero
/// generar con ella exige <c>acknowledgeWarnings</c> y se guarda con la generación. <b>Nunca</b> va dentro del archivo. (nuevo)
/// </summary>
public sealed record AdvertenciaRegulatoria(string Code, string Message);

/// <summary>Los códigos de las advertencias regulatorias de Cartera (K5, US16). (nuevo)</summary>
public static class AdvertenciasRegulatorias
{
    /// <summary>El layout vigente tiene campos sin cotejar con el instructivo (§7.3; precedente D-43 de la PILA).</summary>
    public const string LayoutSinCotejar = "Lending.Regulatory.LayoutSinCotejar";

    /// <summary>El cierre del SICSES se hizo sin el modelo obligatorio de pérdida esperada (D28, FR-105).</summary>
    public const string ModeloObligatorioNoDisponible = "Lending.Regulatory.ModeloObligatorioNoDisponible";

    /// <summary>El cierre del SICSES usó valores o tablas «pendiente de validar» (T41, C76).</summary>
    public const string NormativaPendienteDeValidar = "Lending.Regulatory.NormativaPendienteDeValidar";

    /// <summary>El nivel de supervisión del perfil no reporta ese mes al SICSES (documento <c>TiposDeOrganizacion</c>).</summary>
    public const string MesFueraDeLaPeriodicidad = "Lending.Regulatory.MesFueraDeLaPeriodicidad";

    public static AdvertenciaRegulatoria MesFueraDePeriodicidad(int anio, int mes, int nivel) => new(MesFueraDeLaPeriodicidad,
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Con el nivel de supervisión {nivel}, el mes {anio:0000}-{mes:00} no se reporta al SICSES: el archivo se genera, pero no tiene plazo."));

    public static AdvertenciaRegulatoria SinCotejar(string layout, int sinCotejar, int total) => new(LayoutSinCotejar,
        $"El formato {layout} tiene {sinCotejar} de {total} campos sin cotejar con el instructivo: verifique el archivo en el validador antes de cargarlo.");

    public static AdvertenciaRegulatoria SinModeloObligatorio() => new(ModeloObligatorioNoDisponible,
        "El cierre se hizo con el modelo obligatorio de pérdida esperada no disponible: el archivo lleva el deterioro del modelo efectivamente aplicado.");

    public static AdvertenciaRegulatoria NormativaPendiente(string? detalle) => new(NormativaPendienteDeValidar,
        "El cierre usó valores o tablas pendientes de validar" + (string.IsNullOrWhiteSpace(detalle) ? "." : $": {detalle}"));
}

/// <summary>El estado de una obligación tal como lo informa Cartera a una central (antes de traducirlo al código de la central). (nuevo)</summary>
public enum EstadoEnCentral
{
    AlDia = 1,
    EnMora = 2,
    Cancelada = 3,
    Castigada = 4,

    /// <summary>Retiro de lo reportado: anulación desde el origen (FR-054) o reporte negativo hecho sin aviso (FR-123).</summary>
    Retirado = 5,
}

/// <summary>
/// Las traducciones de los códigos del ERP a los del SICSES y de cada central (feature 013, K5, US16; T1002, T1003). Son <b>por cotejar</b>, igual
/// que los layouts (pregunta I7): el instructivo vigente y los layouts de cada central los aporta el dueño. Las tablas viven aquí —no en el JSON del
/// layout, que es estricto— y un código que no está en la tabla pasa tal cual, para que el validador lo señale contra el campo. (nuevo)
/// </summary>
public static class TraduccionesRegulatorias
{
    /// <summary>El tipo de documento estándar (<c>CC</c>, <c>CE</c>, <c>NI</c>, <c>PA</c>, <c>TI</c>) desde el <c>IdType</c> de la persona.</summary>
    public static string TipoDeDocumento(string? idType) => idType?.Trim().ToUpperInvariant() switch
    {
        null or "" or "C" or "CC" => "CC",
        "E" or "CE" => "CE",
        "N" or "NI" or "NIT" => "NI",
        "P" or "PA" or "PP" => "PA",
        "T" or "TI" => "TI",
        var otro => otro,
    };

    /// <summary>La modalidad del SICSES (hasta diez caracteres) desde el código de modalidad del catálogo nacional.</summary>
    public static string ModalidadDelSicses(string modalidad) => modalidad.Trim().ToUpperInvariant() switch
    {
        "CONSUMO" => "CONSUMO",
        "CONSUMO_LIBRANZA" => "CONSLIB",
        "CONSUMO_SIN_LIBRANZA" => "CONSSINLIB",
        "COMERCIAL" or "COMERCIAL_PN" => "COMERPN",
        "COMERCIAL_PJ" => "COMERPJ",
        "VIVIENDA" => "VIVIENDA",
        "MICROCREDITO" => "MICRO",
        var otra => otra,
    };

    /// <summary>El tipo de garantía del SICSES (dos caracteres) de la garantía más fuerte del crédito; sin garantía, el pagaré.</summary>
    public static string GarantiaDelSicses(CollateralKind? garantia) => garantia switch
    {
        CollateralKind.Mortgage => "HI",
        CollateralKind.Pledge => "PR",
        CollateralKind.GuaranteeFund => "FG",
        CollateralKind.Contributions => "AP",
        CollateralKind.CoDebtor => "CD",
        _ => "PA",
    };

    /// <summary>De varias garantías, la que manda en el reporte (hipoteca, prenda, fondo, aportes, codeudor, pagaré).</summary>
    public static CollateralKind? GarantiaMasFuerte(IEnumerable<CollateralKind> garantias) =>
        garantias.Select(g => (CollateralKind?)g).OrderBy(g => g switch
        {
            CollateralKind.Mortgage => 1,
            CollateralKind.Pledge => 2,
            CollateralKind.GuaranteeFund => 3,
            CollateralKind.Contributions => 4,
            CollateralKind.CoDebtor => 5,
            _ => 6,
        }).FirstOrDefault();

    public static string SiNo(bool valor) => valor ? "S" : "N";

    /// <summary>La subcategoría de E como la lleva el SICSES; nula si no tiene.</summary>
    public static string? Subcategoria(RatingSubcategory s) => s == RatingSubcategory.None ? null : s.ToString();

    /// <summary>La traducción de una central (por su código); una central sin tabla propia usa la genérica.</summary>
    public static TraduccionDeCentral DeLaCentral(string central) =>
        string.Equals(central, "DATACREDITO", StringComparison.OrdinalIgnoreCase) ? Datacredito : Generica;

    /// <summary>DataCrédito Experian (por cotejar): códigos numéricos.</summary>
    public static readonly TraduccionDeCentral Datacredito = new(
        TipoDeCuenta: "01",
        TiposDeIdentificacion: new Dictionary<string, string> { ["CC"] = "1", ["NI"] = "2", ["TI"] = "3", ["CE"] = "4", ["PA"] = "5" },
        Deudor: "00",
        Codeudor: "01",
        Estados: new Dictionary<EstadoEnCentral, string>
        {
            [EstadoEnCentral.AlDia] = "01", [EstadoEnCentral.EnMora] = "06", [EstadoEnCentral.Cancelada] = "05",
            [EstadoEnCentral.Castigada] = "13", [EstadoEnCentral.Retirado] = "99",
        },
        EstadoDeOrigenNormal: "0",
        EstadoDeOrigenReestructurado: "1",
        Periodicidades: new Dictionary<RepaymentPeriodicity, string>
        {
            [RepaymentPeriodicity.Monthly] = "1", [RepaymentPeriodicity.Bimonthly] = "2", [RepaymentPeriodicity.Quarterly] = "3",
            [RepaymentPeriodicity.SemiAnnual] = "4", [RepaymentPeriodicity.Annual] = "5", [RepaymentPeriodicity.SinglePayment] = "6",
            [RepaymentPeriodicity.Biweekly] = "7", [RepaymentPeriodicity.Weekly] = "8", [RepaymentPeriodicity.TenDay] = "9",
        });

    /// <summary>La genérica (TransUnion y cualquier otra sin tabla propia, por cotejar): códigos alfabéticos.</summary>
    public static readonly TraduccionDeCentral Generica = new(
        TipoDeCuenta: "CR",
        TiposDeIdentificacion: new Dictionary<string, string>(),
        Deudor: "P",
        Codeudor: "C",
        Estados: new Dictionary<EstadoEnCentral, string>
        {
            [EstadoEnCentral.AlDia] = "AD", [EstadoEnCentral.EnMora] = "MO", [EstadoEnCentral.Cancelada] = "CA",
            [EstadoEnCentral.Castigada] = "CS", [EstadoEnCentral.Retirado] = "RT",
        },
        EstadoDeOrigenNormal: "N",
        EstadoDeOrigenReestructurado: "R",
        Periodicidades: new Dictionary<RepaymentPeriodicity, string>());
}

/// <summary>Los códigos de una central (por cotejar): tipo de cuenta, identificación, calidad del titular, estado, estado de origen y periodicidad.</summary>
public sealed record TraduccionDeCentral(
    string TipoDeCuenta,
    IReadOnlyDictionary<string, string> TiposDeIdentificacion,
    string Deudor,
    string Codeudor,
    IReadOnlyDictionary<EstadoEnCentral, string> Estados,
    string EstadoDeOrigenNormal,
    string EstadoDeOrigenReestructurado,
    IReadOnlyDictionary<RepaymentPeriodicity, string> Periodicidades)
{
    public string TipoDeIdentificacion(string estandar) => TiposDeIdentificacion.TryGetValue(estandar, out var c) ? c : estandar;

    public string Estado(EstadoEnCentral estado) => Estados[estado];

    public string Periodicidad(RepaymentPeriodicity p) =>
        Periodicidades.TryGetValue(p, out var c) ? c : ((int)p).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
