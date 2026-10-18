using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>Quién lo trae a Contabilidad (§2.1): la entrega de Cartera en que se emite. Los de K3 y K4 se declaran desde K1. (nuevo)</summary>
public enum EntregaDelHecho
{
    K1 = 1,
    K3 = 3,
    K4 = 4,

    /// <summary>K6b (T1056): la corrección monetaria de los créditos en UVR.</summary>
    K6b = 7,

    // Feature 014 (T048): las entregas de captaciones y aportes, con el número de EntregaDeCartera (research E06). El contrato
    // (contabilidad.md §3 de la 014) decía K7 = 7…, pero el 7 ya es K6b (research §7, E25).

    /// <summary>Aportes, traslados y retiro del asociado.</summary>
    K7 = 8,

    /// <summary>Ahorro a la vista: consignación, retiro, causación, liquidación, retención, GMF, cargos y embargos.</summary>
    K8 = 9,

    /// <summary>CDAT y ahorro contractual.</summary>
    K9 = 10,

    /// <summary>Asamblea y no reclamados.</summary>
    K10 = 11,

    /// <summary>Fondo de liquidez (sin hechos contables propios).</summary>
    K11 = 12,
}

/// <summary>
/// Un hecho de Cartera que va a Contabilidad (feature 013, T0640; contracts/contabilidad.md §2.1; contracts/mensajes.md §2.1 y §7.2):
/// su código (= el <c>Type</c> del mensaje, que es el código de operación de la matriz), su código corto (el de la <c>ScheduleKey</c>), si
/// es informativo (recibo sin comprobante), los roles del débito y del crédito del importe positivo (un negativo los invierte), el modo y
/// el tipo de comprobante por defecto, la entrega, los acompañantes de su unidad si es principal, si llega por partes y si hereda el modo
/// de su original. (nuevo)
/// </summary>
public sealed record OperacionDeCartera(
    string Codigo,
    string Corto,
    bool EsInformativo,
    IReadOnlyList<string> RolesDebito,
    IReadOnlyList<string> RolesCredito,
    string ModoPorDefecto,
    string? DisparadorPorDefecto,
    string? Comprobante,
    EntregaDelHecho Entrega,
    IReadOnlyList<string> Acompanantes,
    bool PorPartes = false,
    bool HeredaElModo = false)
{
    /// <summary>Todos los roles del hecho, sin repetir, en el orden débito → crédito.</summary>
    public IReadOnlyList<string> Roles { get; } = RolesDebito.Concat(RolesCredito).Distinct(StringComparer.Ordinal).ToList();

    public bool TieneRol(string rol) => Roles.Contains(rol, StringComparer.Ordinal);
}

/// <summary>
/// Los 70 hechos de Cartera que van a Contabilidad: los 37 de crédito y, al final, los 33 de captaciones y aportes de la feature 014 (T048;
/// contabilidad.md §3 de la 014: 30 de negocio y 3 informativos). Los de crédito (feature 013, T0640, C110; contracts/contabilidad.md §2.1): 31 de negocio (con
/// comprobante; K6b suma la corrección monetaria UVR y el reparto de un pago corregido por la UVR) y 6 informativos (con recibo y sin comprobante). Catálogo fijo en código, del lado de Contabilidad: Contabilidad no depende
/// de <c>Application.Lending</c> (lo vigila <c>CarteraNoConoceContabilidad</c> en las dos direcciones), así que el modo y el comprobante por
/// defecto se repiten aquí y en la semilla de <c>LND_FactTypes</c>; una prueba los coteja. (nuevo)
///
/// <para>
/// Los roles son los que el hecho puede mover en cada lado; los de saldo, orden, pasivo, puente, diferido y contingentes se resuelven
/// siempre con la regla de hecho <c>*</c> (C77), así que el hecho sólo restringe qué roles de resultado admiten una regla con hecho fijo.
/// Los traslados (<c>DimensionContableCambiada</c>, la reclasificación por calificación, las novedades) mueven cada rol de saldo de una
/// combinación a otra: están en los dos lados.
/// </para>
/// </summary>
public static class OperacionesDeCartera
{
    public const string EnLinea = "EnLinea";
    public const string PorLotes = "PorLotes";
    public const string NoPasa = "NoPasa";

    /// <summary>El de su original (C08): los reversos nunca tienen modo propio.</summary>
    public const string Heredado = "Heredado";

    /// <summary>Los informativos: siempre, sin comprobante.</summary>
    public const string Siempre = "Always";

    public const string FinDeCausacion = "FinDeCausacion";
    public const string CierreDeCartera = "CierreDeCartera";

    /// <summary>Los roles de saldo que mueve un traslado de una combinación a otra (§2.2, «naturaleza crédito»).</summary>
    public static IReadOnlyList<string> RolesDeTraslado { get; } =
    [
        R.Capital, R.InteresPorCobrar, R.PagoPorCuentaDelAsociado, R.CargoPorCobrar, R.DeterioroIndividual, R.SuspensoEnOrden,
        R.CastigadaEnOrden, R.Anticipo, R.SaldoAFavor, R.IngresoDiferido,
    ];

    private static readonly string[] ConceptosDeLaCuota = [R.Capital, R.InteresPorCobrar, R.PagoPorCuentaDelAsociado, R.CargoPorCobrar];
    private static readonly string[] Causados = [R.InteresPorCobrar, R.SuspensoEnOrden, R.CastigadaEnOrden];
    private static readonly string[] ContrasDeLaCausacion = [R.Ingreso, R.ContraOrdenContingente, R.ContraOrdenControl];
    private static readonly string[] DelDeterioro = [R.GastoDeterioro, R.DeterioroIndividual, R.DeterioroGeneral, R.RecuperacionDeterioro];
    private static readonly string[] Ninguno = [];

    // ------------------------------------------------------------------------------------- captaciones (feature 014) --

    /// <summary>Los roles de saldo de una cuenta de captación o de aportes: los que mueven un traslado y una reclasificación.</summary>
    private static readonly string[] SaldosDeCuenta = [R.CapitalDeCaptacion, R.CapitalDeCaptacionInactiva, R.AportesSociales];

    /// <summary>Los roles de saldo que una reclasificación mueve de una combinación a otra (contabilidad.md §3, fila 17).</summary>
    private static readonly string[] ReclasificablesDeCaptacion = [R.CapitalDeCaptacion, R.CapitalDeCaptacionInactiva, R.InteresPorPagar];

    /// <summary>A dónde va el dinero de un CDAT que se paga, redime o cancela: otra cuenta, el puente de devoluciones o el recaudador.</summary>
    private static readonly string[] DestinosDelCdat = [R.CapitalDeCaptacion, R.DevolucionPorPagar, R.RecaudoPorAplicar];

    /// <summary>
    /// Todos los roles que un hecho de captaciones puede mover (con el puente del recaudo): el reverso de una operación de cuenta es el espejo
    /// de cualquiera de ellos, en los dos lados.
    /// </summary>
    private static readonly string[] RolesDeCuenta =
    [
        R.CapitalDeCaptacion, R.CapitalDeCaptacionInactiva, R.InteresPorPagar, R.GastoPorIntereses, R.RetencionPorPagar, R.GmfPorPagar,
        R.GastoPorBonificacion, R.IngresoPorCargos, R.CargoDeCuentaPorCobrar, R.AportesSociales, R.FondoDeRevalorizacion, R.FondoDeExcedentes,
        R.DevolucionPorPagar, R.NoReclamadosPorPagar, R.EmbargosPorConsignar, R.IngresoPorPenalidad, R.PerdidaAbsorbida, R.RecaudoPorAplicar,
    ];

    private static readonly string[] AcompananDeposito = [CargoPendienteCobradoV1.Type, AjusteDeCausacionDeCaptacionV1.Type];
    private static readonly string[] AcompananDisposicion = [GmfRetenidoV1.Type, AjusteDeCausacionDeCaptacionV1.Type];
    private static readonly string[] AcompananPagoDelCdat = [RetencionPracticadaV1.Type, GmfRetenidoV1.Type];
    private static readonly string[] AcompananProrroga = [RetencionPracticadaV1.Type, GmfRetenidoV1.Type, CaptacionReclasificadaV1.Type];
    private static readonly string[] AcompananRedencion =
        [RetencionPracticadaV1.Type, PenalidadAplicadaV1.Type, GmfRetenidoV1.Type, AjusteDeCausacionDeCaptacionV1.Type];

    public static IReadOnlyList<OperacionDeCartera> Todas { get; } =
    [
        new(CreditoDesembolsadoV1.Type, "DES", false,
            [R.Capital], [R.DesembolsoPorGirar, R.PuenteComercial, R.CuentaPorCobrarComercial], EnLinea, null, "DS", EntregaDelHecho.K1, Ninguno),
        new(DesembolsoReversadoV1.Type, "DRV", false,
            [R.DesembolsoPorGirar, R.PuenteComercial, R.CuentaPorCobrarComercial, R.Ingreso, R.SeguroPorPagar, R.ContraOrdenContingente],
            [R.Capital, R.InteresPorCobrar, R.PagoPorCuentaDelAsociado, R.SuspensoEnOrden], Heredado, null, "DS", EntregaDelHecho.K1,
            [AjusteDeCausacionPorFechaValorV1.Type], HeredaElModo: true),
        // T1098 (decisión del dueño del 2026-10-18; contabilidad.md §32): sus reallocations se asientan con el molde de PRN, como las de PagoAplicado,
        // así que el ajuste también puede mover los conceptos de la cuota, el ingreso de la recuperación, la orden, el anticipo y el saldo a favor.
        new(CreditoAjustadoDesdeComercialV1.Type, "ACO", false,
            [R.Capital, R.PuenteComercial, R.CuentaPorCobrarComercial, R.Ingreso, R.InteresPorCobrar, R.PagoPorCuentaDelAsociado, R.CargoPorCobrar,
                R.IngresoPorRecuperacion, R.SuspensoEnOrden, R.CastigadaEnOrden],
            [R.PuenteComercial, R.CuentaPorCobrarComercial, .. ConceptosDeLaCuota, R.Anticipo, R.SaldoAFavor, R.ContraOrdenContingente,
                R.ContraOrdenControl], EnLinea, null, "DS", EntregaDelHecho.K1,
            [SaldoAFavorReconocidoV1.Type]),
        new(InteresCausadoV1.Type, "ICA", false, Causados, ContrasDeLaCausacion, PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K1, Ninguno, PorPartes: true),
        new(MoraCausadaV1.Type, "MCA", false, Causados, ContrasDeLaCausacion, PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K1, Ninguno, PorPartes: true),
        new(SeguroCausadoV1.Type, "SCA", false, [R.PagoPorCuentaDelAsociado], [R.SeguroPorPagar], PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K1, Ninguno,
            PorPartes: true),
        new(AjusteDeCausacionPorFechaValorV1.Type, "AFV", false,
            [.. Causados, R.PagoPorCuentaDelAsociado], [.. ContrasDeLaCausacion, R.SeguroPorPagar], PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K1, Ninguno,
            PorPartes: true),
        new(CausacionSuspendidaV1.Type, "CSU", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        new(CausacionReanudadaV1.Type, "CRE", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        // Decisión del dueño del 2026-10-18 (contabilidad.md §31): sus reallocations (pagos posteriores redistribuidos) se asientan con el molde de PRN,
        // así que el pago también puede mover el ingreso del suspenso o de la recuperación, la orden, el anticipo y el saldo a favor.
        new(PagoAplicadoV1.Type, "PAP", false,
            [R.RecaudoPorAplicar, .. ConceptosDeLaCuota, R.Ingreso, R.IngresoPorRecuperacion, R.SuspensoEnOrden, R.CastigadaEnOrden],
            [.. ConceptosDeLaCuota, R.Anticipo, R.SaldoAFavor, R.ContraOrdenContingente, R.ContraOrdenControl], EnLinea, null, "RC", EntregaDelHecho.K1,
            [AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type, SaldoAFavorReconocidoV1.Type, AjusteDeCausacionPorFechaValorV1.Type]),
        new(PagoReversadoV1.Type, "PRV", false,
            [.. ConceptosDeLaCuota, R.Anticipo, R.SaldoAFavor, R.Ingreso, R.IngresoPorRecuperacion, R.SuspensoEnOrden, R.CastigadaEnOrden],
            [R.RecaudoPorAplicar, R.ContraOrdenContingente, R.ContraOrdenControl], Heredado, null, "RC", EntregaDelHecho.K1,
            [AjusteDeCausacionPorFechaValorV1.Type], HeredaElModo: true),
        new(AnticipoRecibidoV1.Type, "ANR", false, [R.RecaudoPorAplicar], [R.Anticipo], EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(AnticipoAplicadoV1.Type, "ANA", false, [R.Anticipo], ConceptosDeLaCuota, EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(CobroDeInteresEnSuspensoV1.Type, "CIS", false, [R.RecaudoPorAplicar, R.ContraOrdenContingente], [R.Ingreso, R.SuspensoEnOrden],
            EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(SaldoAFavorReconocidoV1.Type, "SFR", false, [R.RecaudoPorAplicar, R.PuenteComercial, R.CuentaPorCobrarComercial, R.Ingreso], [R.SaldoAFavor],
            EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(SaldoAFavorResueltoV1.Type, "SFS", false, [R.SaldoAFavor, R.Anticipo], [R.RecaudoPorAplicar, .. ConceptosDeLaCuota],
            EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(RecuperacionDeCastigadaV1.Type, "RCA", false, [R.RecaudoPorAplicar, R.ContraOrdenControl], [R.IngresoPorRecuperacion, R.CastigadaEnOrden],
            EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(DimensionContableCambiadaV1.Type, "DCC", false, RolesDeTraslado, RolesDeTraslado, EnLinea, FinDeCausacion, "CN", EntregaDelHecho.K1, Ninguno),
        new(CobroProvisionalIncorporadoV1.Type, "CPI", false, [R.CuentaPorCobrarComercial], ConceptosDeLaCuota, EnLinea, null, "RC", EntregaDelHecho.K1, Ninguno),
        new(DeterioroCalculadoV1.Type, "DET", false, [R.GastoDeterioro, R.DeterioroIndividual, R.DeterioroGeneral], DelDeterioro,
            PorLotes, CierreDeCartera, "PV", EntregaDelHecho.K1, Ninguno, PorPartes: true),
        new(CarteraReclasificadaPorCalificacionV1.Type, "RCL", false, RolesDeTraslado, RolesDeTraslado, PorLotes, CierreDeCartera, "PV", EntregaDelHecho.K1,
            Ninguno, PorPartes: true),
        new(ReversoDeCierreV1.Type, "RCI", false, [.. RolesDeTraslado, .. DelDeterioro], [.. RolesDeTraslado, .. DelDeterioro], Heredado, CierreDeCartera, "PV",
            EntregaDelHecho.K1, Ninguno, PorPartes: true, HeredaElModo: true),
        new(GastoDeCobranzaCausadoV1.Type, "GCO", false, [R.CargoPorCobrar, R.PagoPorCuentaDelAsociado], [R.Ingreso, R.CuentaPorPagarATercero],
            EnLinea, null, "CA", EntregaDelHecho.K4, Ninguno),
        // Las novedades trasladan saldos y capitalizan: lo causado contra su rol de saldo, lo suspendido contra el ingreso diferido y su orden
        // contingente se cancela (T0646; contabilidad.md §2.1).
        new(CreditoReestructuradoV1.Type, "REE", false, [.. RolesDeTraslado, R.ContraOrdenContingente], RolesDeTraslado, EnLinea, null, "CN", EntregaDelHecho.K3, Ninguno),
        new(CreditoModificadoV1.Type, "MOD", false, [.. RolesDeTraslado, R.ContraOrdenContingente], RolesDeTraslado, EnLinea, null, "CN", EntregaDelHecho.K3, Ninguno),
        new(CreditoNovadoV1.Type, "NOV", false, [.. RolesDeTraslado, R.ContraOrdenContingente], RolesDeTraslado, EnLinea, null, "CN", EntregaDelHecho.K3, Ninguno),
        new(CondonacionAprobadaV1.Type, "CON", false, [R.DeterioroIndividual, R.GastoDeCastigoOCondonacion, R.ContraOrdenContingente],
            [.. ConceptosDeLaCuota, R.SuspensoEnOrden], EnLinea, null, "CN", EntregaDelHecho.K3, Ninguno),
        new(CreditoCastigadoV1.Type, "CAS", false,
            [R.DeterioroIndividual, R.GastoDeCastigoOCondonacion, R.CastigadaEnOrden, R.ContraOrdenContingente],
            [.. ConceptosDeLaCuota, R.ContraOrdenControl, R.SuspensoEnOrden], EnLinea, null, "PV", EntregaDelHecho.K3, Ninguno),
        new(CarteraActivadaV1.Type, "ACT", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        new(CargaDeCarteraAplicadaV1.Type, "CAP", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        new(CierreDeCarteraRealizadoV1.Type, "CIR", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        new(CierreDeCarteraReabiertoV1.Type, "CRA", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K1, Ninguno),
        // A3 (C15, C91): las cuentas contingentes, sin pasar hasta que la contadora decida; salen si el dueño rechaza A3.
        new(CreditoAprobadoSinDesembolsarV1.Type, "CAD", false, [R.ContraContingenteAcreedora], [R.ContingenteAcreedora], NoPasa, null, "CN", EntregaDelHecho.K1, Ninguno),
        new(CupoRotativoOtorgadoV1.Type, "CRO", false, [R.ContraContingenteAcreedora], [R.ContingenteAcreedora], NoPasa, null, "CN", EntregaDelHecho.K1, Ninguno),
        new(GarantiaConstituidaV1.Type, "GCT", false, [R.ContraContingenteAcreedora], [R.ContingenteAcreedora], NoPasa, null, "CN", EntregaDelHecho.K1, Ninguno),
        // K6b (T1056; pregunta E12, propuesta por defecto): la corrección monetaria de los créditos en UVR. Lo que se debe en UVR (capital, intereses,
        // cargos) contra el Ingreso con este hecho fijado en la matriz; el suspenso y la castigada contra su orden; el anticipo y el saldo a favor (pasivos
        // con el deudor), al revés: el Ingreso al débito (contabilidad.md §2.1).
        new(CorreccionMonetariaUvrCausadaV1.Type, "CMU", false,
            [R.Capital, R.InteresPorCobrar, R.PagoPorCuentaDelAsociado, R.CargoPorCobrar, R.SuspensoEnOrden, R.CastigadaEnOrden, R.Ingreso],
            [R.Ingreso, R.ContraOrdenContingente, R.ContraOrdenControl, R.Anticipo, R.SaldoAFavor], PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K6b,
            Ninguno, PorPartes: true),
        // K6b (decisión del dueño del 2026-10-17; contabilidad.md §30): el reparto en pesos de un pago aplicado en un día cuya UVR se corrigió. Sin
        // rol nuevo: lo que el pago cubre de menos vuelve al débito de su rol de saldo y lo que cubre de más sale al crédito; el anticipo y el saldo a
        // favor al revés; el suspenso y la castigada, por su ingreso (el del cobro del suspenso, el de la recuperación) y su orden. Sin el puente del
        // recaudo: lo recibido no cambia. Un importe negativo invierte los lados.
        new(PagoRedistribuidoPorValorNacionalV1.Type, "PRN", false,
            [.. ConceptosDeLaCuota, R.Ingreso, R.IngresoPorRecuperacion, R.SuspensoEnOrden, R.CastigadaEnOrden],
            [R.Anticipo, R.SaldoAFavor, R.ContraOrdenContingente, R.ContraOrdenControl], PorLotes, FinDeCausacion, "CA", EntregaDelHecho.K6b,
            Ninguno, PorPartes: true),

        // Feature 014 (T048; contracts/contabilidad.md §3 de la 014, con los roles en español de research C10): los 33 hechos de captaciones y
        // aportes, al final (37 → 70). El lado es el del importe positivo; un negativo lo invierte. Los de saldo, puente y pasivo se resuelven con
        // la regla de hecho «*»: el hecho sólo restringe qué roles de resultado admiten una regla con hecho fijo. Los acompañantes llevan el
        // modo y el comprobante de su principal más frecuente; rige el del principal (C01).
        new(ConsignacionEnCuentaAplicadaV1.Type, "CCN", false, [R.RecaudoPorAplicar], [R.CapitalDeCaptacion], EnLinea, null, "MA", EntregaDelHecho.K8,
            AcompananDeposito),
        new(AporteRecibidoV1.Type, "APR", false, [R.RecaudoPorAplicar], [R.AportesSociales], EnLinea, null, "AO", EntregaDelHecho.K7, AcompananDeposito),
        new(RetiroDeCuentaAplicadoV1.Type, "RCT", false, [R.CapitalDeCaptacion, R.CapitalDeCaptacionInactiva], [R.RecaudoPorAplicar, R.DevolucionPorPagar],
            EnLinea, null, "MA", EntregaDelHecho.K8, AcompananDisposicion),
        new(TrasladoEntreCuentasAplicadoV1.Type, "TRC", false, SaldosDeCuenta, SaldosDeCuenta, EnLinea, null, "MA", EntregaDelHecho.K7, AcompananDisposicion),
        new(GmfRetenidoV1.Type, "GMF", false, [R.CapitalDeCaptacion], [R.GmfPorPagar], EnLinea, null, "MA", EntregaDelHecho.K8, Ninguno),
        new(InteresDeCaptacionCausadoV1.Type, "ICC", false, [R.GastoPorIntereses], [R.InteresPorPagar], PorLotes, FinDeCausacion, "CC", EntregaDelHecho.K8,
            Ninguno, PorPartes: true),
        new(AjusteDeCausacionDeCaptacionV1.Type, "AJC", false, [R.GastoPorIntereses], [R.InteresPorPagar], PorLotes, FinDeCausacion, "CC",
            EntregaDelHecho.K8, Ninguno, PorPartes: true),
        new(InteresDeCaptacionLiquidadoV1.Type, "ILQ", false, [R.InteresPorPagar], [R.CapitalDeCaptacion], PorLotes, FinDeCausacion, "CC",
            EntregaDelHecho.K8, [RetencionPracticadaV1.Type], PorPartes: true),
        new(RetencionPracticadaV1.Type, "RTF", false, [R.CapitalDeCaptacion, R.InteresPorPagar], [R.RetencionPorPagar], PorLotes, FinDeCausacion, "CC",
            EntregaDelHecho.K8, Ninguno, PorPartes: true),
        new(CdatConstituidoV1.Type, "CDC", false, [R.RecaudoPorAplicar, R.CapitalDeCaptacion], [R.CapitalDeCaptacion], EnLinea, null, "TD",
            EntregaDelHecho.K9, [GmfRetenidoV1.Type]),
        new(InteresesDeCdatPagadosV1.Type, "CDP", false, [R.InteresPorPagar], [R.CapitalDeCaptacion, R.DevolucionPorPagar], EnLinea, null, "TD",
            EntregaDelHecho.K9, AcompananPagoDelCdat),
        new(CdatProrrogadoV1.Type, "CDR", false, [R.InteresPorPagar], [R.CapitalDeCaptacion], EnLinea, null, "TD", EntregaDelHecho.K9, AcompananProrroga),
        new(CdatRenovadoV1.Type, "CDN", false, [R.InteresPorPagar, R.RecaudoPorAplicar, R.CapitalDeCaptacion], DestinosDelCdat, EnLinea, null, "TD",
            EntregaDelHecho.K9, AcompananProrroga),
        new(CdatRedimidoV1.Type, "CDD", false, [R.CapitalDeCaptacion, R.InteresPorPagar], DestinosDelCdat, EnLinea, null, "TD", EntregaDelHecho.K9,
            AcompananRedencion),
        new(CdatCanceladoV1.Type, "CDX", false, [R.CapitalDeCaptacion, R.InteresPorPagar], DestinosDelCdat, EnLinea, null, "TD", EntregaDelHecho.K9,
            AcompananRedencion),
        new(PenalidadAplicadaV1.Type, "PEN", false, [R.InteresPorPagar, R.CapitalDeCaptacion], [R.IngresoPorPenalidad], EnLinea, null, "TD",
            EntregaDelHecho.K9, Ninguno),
        new(CaptacionReclasificadaV1.Type, "RCC", false, ReclasificablesDeCaptacion, ReclasificablesDeCaptacion, EnLinea, FinDeCausacion, "CN",
            EntregaDelHecho.K8, Ninguno),
        new(BonificacionReconocidaV1.Type, "BON", false, [R.GastoPorBonificacion], [R.InteresPorPagar], EnLinea, null, "CC", EntregaDelHecho.K9,
            [PenalidadAplicadaV1.Type]),
        new(CargoDeCuentaCausadoV1.Type, "CGA", false, [R.CapitalDeCaptacion, R.CargoDeCuentaPorCobrar], [R.IngresoPorCargos], EnLinea, null, "MA",
            EntregaDelHecho.K8, Ninguno),
        new(CargoPendienteCobradoV1.Type, "CGP", false, [R.CapitalDeCaptacion], [R.CargoDeCuentaPorCobrar], EnLinea, null, "MA", EntregaDelHecho.K8, Ninguno),
        new(CargoDeCuentaCondonadoV1.Type, "CGN", false, [R.IngresoPorCargos], [R.CargoDeCuentaPorCobrar, R.CapitalDeCaptacion], EnLinea, null, "MA",
            EntregaDelHecho.K8, Ninguno),
        new(RevalorizacionDeAportesAplicadaV1.Type, "RVA", false, [R.FondoDeRevalorizacion], [R.AportesSociales], EnLinea, null, "AO", EntregaDelHecho.K10,
            Ninguno),
        new(ExcedentesDistribuidosV1.Type, "EXD", false, [R.FondoDeExcedentes], [R.AportesSociales, R.CapitalDeCaptacion, R.DevolucionPorPagar], EnLinea,
            null, "AO", EntregaDelHecho.K10, Ninguno),
        new(AportesAmortizadosV1.Type, "AMZ", false, [R.AportesSociales], [R.DevolucionPorPagar], EnLinea, null, "AO", EntregaDelHecho.K10, Ninguno),
        // El cruce acredita el puente con OwnLoanPayoff y el PagoAplicado de cada crédito lo debita en la misma transacción (§3).
        new(CruceAlRetiroAplicadoV1.Type, "CRR", false, SaldosDeCuenta, [R.RecaudoPorAplicar], EnLinea, null, "RA", EntregaDelHecho.K7,
            [PerdidaAplicadaAlRetiroV1.Type]),
        new(PerdidaAplicadaAlRetiroV1.Type, "PDR", false, [R.AportesSociales], [R.PerdidaAbsorbida], EnLinea, null, "RA", EntregaDelHecho.K7, Ninguno),
        new(DevolucionOrdenadaV1.Type, "DVO", false, [.. SaldosDeCuenta, R.NoReclamadosPorPagar], [R.DevolucionPorPagar, R.CapitalDeCaptacion], EnLinea,
            null, "RA", EntregaDelHecho.K7, Ninguno),
        new(DevolucionGiradaV1.Type, "DVG", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K7, Ninguno),
        new(SaldoTrasladadoANoReclamadosV1.Type, "SNR", false, [R.DevolucionPorPagar, R.CapitalDeCaptacion, R.CapitalDeCaptacionInactiva],
            [R.NoReclamadosPorPagar], EnLinea, null, "MA", EntregaDelHecho.K10, Ninguno),
        new(EmbargoConsignadoV1.Type, "EMC", false, [R.CapitalDeCaptacion], [R.EmbargosPorConsignar], EnLinea, null, "MA", EntregaDelHecho.K8, Ninguno),
        // El espejo de lo que movió la unidad original, con las dimensiones vigentes (la 013 §9); hereda el modo y el comprobante del original.
        new(OperacionDeCuentaReversadaV1.Type, "RVC", false, RolesDeCuenta, RolesDeCuenta, Heredado, null, "MA", EntregaDelHecho.K7,
            [AjusteDeCausacionDeCaptacionV1.Type], HeredaElModo: true),
        new(CaptacionesActivadasV1.Type, "CAV", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K7, Ninguno),
        new(CargaDeCaptacionesAplicadaV1.Type, "CAC", true, Ninguno, Ninguno, Siempre, null, null, EntregaDelHecho.K7, Ninguno),
    ];

    /// <summary>Los 33 hechos de captaciones y aportes (feature 014): los que se agregaron al final de <see cref="Todas"/>.</summary>
    public static IReadOnlyList<OperacionDeCartera> DeCaptaciones { get; } = Todas.Skip(37).ToList();

    private static readonly Dictionary<string, OperacionDeCartera> PorCodigo = Todas.ToDictionary(o => o.Codigo, StringComparer.Ordinal);

    /// <summary>El hecho por su código exacto (el <c>Type</c> del mensaje).</summary>
    public static OperacionDeCartera? Buscar(string? codigo) =>
        codigo is null ? null : PorCodigo.GetValueOrDefault(codigo.Trim());

    /// <summary>Los de negocio (con comprobante): los que pueden tener regla con el hecho fijado y mapeo de tipo de comprobante.</summary>
    public static IReadOnlyList<OperacionDeCartera> DeNegocio { get; } = Todas.Where(o => !o.EsInformativo).ToList();

    /// <summary>¿Es un acompañante de algún principal (mensajes.md §7.2)? Su mapeo no se usa: el tipo lo da el principal (C01).</summary>
    public static bool EsAcompanante(string tipo) => Todas.Any(o => o.Acompanantes.Contains(tipo, StringComparer.Ordinal));

    /// <summary>
    /// El hecho principal de una unidad (C01): el que tiene a los demás entre sus acompañantes; si ninguno los tiene (una unidad de un
    /// solo hecho), el primero. Nulo con la unidad vacía.
    /// </summary>
    public static string? Principal(IReadOnlyList<string> tiposDeLaUnidad)
    {
        if (tiposDeLaUnidad.Count == 0) return null;
        var otros = tiposDeLaUnidad.Distinct(StringComparer.Ordinal).ToList();
        return otros.FirstOrDefault(t => Buscar(t) is { } o && otros.Where(x => x != t).All(x => o.Acompanantes.Contains(x, StringComparer.Ordinal)))
            ?? tiposDeLaUnidad[0];
    }
}
