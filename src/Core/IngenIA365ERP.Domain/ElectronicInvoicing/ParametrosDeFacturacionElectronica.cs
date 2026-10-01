using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using static IngenIA365ERP.Domain.Common.Parametros.CatalogoDeParametros;
using static IngenIA365ERP.Domain.Common.Parametros.EntregaDelComercio;

namespace IngenIA365ERP.Domain.ElectronicInvoicing;

/// <summary>
/// Catálogo cerrado de las claves <c>EINV</c> (feature 012, T21, T068; data-model §4.2; decisiones-transversales
/// §2.8): obligación de facturar, esperas, plazos y avisos de la facturación electrónica. Todas exigen
/// <see cref="Permiso"/> además de <c>Inventory.Parameters.Manage</c>. Los defectos son configurables con vigencia,
/// no valores legales fijos —el plazo de contingencia exige además su fuente legal—: por eso el archivo está
/// exceptuado de <c>ElComercioNoTieneValoresLegalesFijos</c>.
/// </summary>
public static class ParametrosDeFacturacionElectronica
{
    public const string Modulo = "EINV";

    public const string Permiso = "ElectronicInvoicing.Settings.Manage";

    public const string ObligadaAFacturar = "Dian.ObligadaAFacturar";
    public const string EsperaMaximaPosSegundos = "Dian.EsperaMaximaPosSegundos";
    public const string PlazoContingenciaHoras = "Dian.PlazoContingenciaHoras";
    public const string AlertaHorasAntesDelPlazo = "Dian.AlertaHorasAntesDelPlazo";
    public const string MinutosAlertaSinValidar = "Dian.MinutosAlertaSinValidar";
    public const string UmbralFallasCircuito = "Dian.UmbralFallasCircuito";
    public const string AvisoResolucionPorcentaje = "Dian.AvisoResolucionPorcentaje";
    public const string AvisoResolucionDias = "Dian.AvisoResolucionDias";
    public const string EntregaCorreo = "Dian.EntregaCorreo";
    public const string DocumentoSoporteGeneracion = "DocumentoSoporte.Generacion";

    public static IReadOnlyList<DefinicionDeParametro> Definiciones { get; } =
    [
        SiNo(Modulo, ObligadaAFacturar, "La cooperativa está obligada a facturar electrónicamente.", true, I3) with { PermisoAdicional = Permiso },
        Entero(Modulo, EsperaMaximaPosSegundos, "Segundos que el POS espera la validación antes de pasar a contingencia.",
            "15", I4, ParameterScopeKind.None, ParameterScopeKind.PointOfSale) with { PermisoAdicional = Permiso },
        Entero(Modulo, PlazoContingenciaHoras, "Horas para transmitir lo emitido en contingencia, con su norma.",
            "48", I4) with { PermisoAdicional = Permiso, ExigeFuenteLegal = true },
        Entero(Modulo, AlertaHorasAntesDelPlazo, "Horas antes del plazo de contingencia en que se alerta.", "6", I4) with { PermisoAdicional = Permiso },
        Entero(Modulo, MinutosAlertaSinValidar, "Minutos sin validación de la DIAN antes de alertar.", "10", I4) with { PermisoAdicional = Permiso },
        Entero(Modulo, UmbralFallasCircuito, "Fallas seguidas del canal que abren el circuito (contingencia).", "3", I4) with { PermisoAdicional = Permiso },
        NumeroDecimal(Modulo, AvisoResolucionPorcentaje, "Fracción consumida de la resolución de numeración desde la que se avisa.",
            "0.90", I4) with { PermisoAdicional = Permiso, Maximo = 1 },
        Entero(Modulo, AvisoResolucionDias, "Días antes del vencimiento de la resolución en que se avisa.", "30", I4) with { PermisoAdicional = Permiso },
        Eleccion(Modulo, EntregaCorreo, "Quién entrega el documento electrónico al adquirente.", ["Erp", "Canal"], "Erp", I4) with { PermisoAdicional = Permiso },
        Eleccion(Modulo, DocumentoSoporteGeneracion, "Cuándo se genera el documento soporte.",
            ["PorOperacion", "Semanal"], "PorOperacion", I4) with { PermisoAdicional = Permiso },
    ];
}
