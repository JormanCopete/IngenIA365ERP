using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// Los 43 tipos que emite Cartera (feature 013, T0070, T0071; contracts/mensajes.md §2.1 y §2.2; DT §2.11, C01–C03, C15, C91, C94,
/// C117): los 37 hechos a Contabilidad (K6b, T1056, suma la corrección monetaria UVR y, por la decisión del dueño del 2026-10-17, el reparto de
/// un pago corregido por la UVR), cada uno con su código corto (<c>LND_FactTypes.ShortCode</c>, que la semilla de tipos de
/// hecho copia de aquí), y los 6 mensajes a Comercial, Tesorería y Recaudo. Todos son de <c>CAR</c>, v1, y a un solo destino;
/// <see cref="TipoDeMensaje.AdmiteProceso"/> es la lista cerrada de §6 (<c>EmisionesDeProcesoPermitidas</c>). Los de K2, K3 y K4 se
/// declaran desde K1 aunque se emitan desde su entrega. Ninguno lleva cuentas contables (FR-126).
///
/// <para>
/// <b>Cuentas contingentes (A3, C15).</b> <c>CreditoAprobadoSinDesembolsar</c>, <c>CupoRotativoOtorgado</c> y
/// <c>GarantiaConstituida</c> nacen con la propuesta por defecto de A3; <b>se quitan si el dueño rechaza A3</b>, sin tocar nada más.
/// </para>
/// </summary>
public static class CatalogoDeMensajesDeCarteraV1
{
    private const string A = IntegrationDestinations.Accounting;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const IntegrationMessageKind I = IntegrationMessageKind.Informational;
    private const FormaDeClaveDeEvento C = FormaDeClaveDeEvento.Confirmacion;
    private const FormaDeClaveDeEvento P = FormaDeClaveDeEvento.ConfirmacionPorPublicId;
    private const FormaDeClaveDeEvento CoP = FormaDeClaveDeEvento.ConfirmacionOPorPublicId;
    private const FormaDeClaveDeEvento Cierre = FormaDeClaveDeEvento.Cierre;
    private const FormaDeClaveDeEvento Reapertura = FormaDeClaveDeEvento.Reapertura;

    /// <summary>Los 37 hechos <c>CAR → Accounting</c> de §2.1, en su orden, y los 33 de captaciones y aportes de la feature 014 (70).</summary>
    public static IReadOnlyList<TipoDeMensaje> HechosAContabilidad { get; } =
    [
        Hecho(typeof(CreditoDesembolsadoV1), CreditoDesembolsadoV1.Type, "DES", N, C, proceso: false),
        Hecho(typeof(DesembolsoReversadoV1), DesembolsoReversadoV1.Type, "DRV", N, C, proceso: false),
        Hecho(typeof(CreditoAjustadoDesdeComercialV1), CreditoAjustadoDesdeComercialV1.Type, "ACO", N, C, proceso: false),
        Hecho(typeof(InteresCausadoV1), InteresCausadoV1.Type, "ICA", N, P, proceso: true),
        Hecho(typeof(MoraCausadaV1), MoraCausadaV1.Type, "MCA", N, P, proceso: true),
        Hecho(typeof(SeguroCausadoV1), SeguroCausadoV1.Type, "SCA", N, P, proceso: true),
        Hecho(typeof(AjusteDeCausacionPorFechaValorV1), AjusteDeCausacionPorFechaValorV1.Type, "AFV", N, CoP, proceso: true),
        Hecho(typeof(CausacionSuspendidaV1), CausacionSuspendidaV1.Type, "CSU", I, P, proceso: true),
        Hecho(typeof(CausacionReanudadaV1), CausacionReanudadaV1.Type, "CRE", I, P, proceso: true),
        // Feature 014, US4 (T119; mensajes.md §1.4 de la 014): el proceso sólo lo origina en el débito automático (recaudador OwnLoanPayoff con la clave
        // CAR:debito-automatico:…); para Recaudo sigue la regla C14 de la 013.
        Hecho(typeof(PagoAplicadoV1), PagoAplicadoV1.Type, "PAP", N, C, proceso: false) with { ProcesoSi = EsDebitoAutomatico },
        Hecho(typeof(PagoReversadoV1), PagoReversadoV1.Type, "PRV", N, C, proceso: false),
        Hecho(typeof(AnticipoRecibidoV1), AnticipoRecibidoV1.Type, "ANR", N, C, proceso: false),
        Hecho(typeof(AnticipoAplicadoV1), AnticipoAplicadoV1.Type, "ANA", N, P, proceso: true),
        Hecho(typeof(CobroDeInteresEnSuspensoV1), CobroDeInteresEnSuspensoV1.Type, "CIS", N, C, proceso: false),
        Hecho(typeof(SaldoAFavorReconocidoV1), SaldoAFavorReconocidoV1.Type, "SFR", N, CoP, proceso: true),
        Hecho(typeof(SaldoAFavorResueltoV1), SaldoAFavorResueltoV1.Type, "SFS", N, C, proceso: false),
        Hecho(typeof(RecuperacionDeCastigadaV1), RecuperacionDeCastigadaV1.Type, "RCA", N, C, proceso: false),
        Hecho(typeof(DimensionContableCambiadaV1), DimensionContableCambiadaV1.Type, "DCC", N, P, proceso: true),
        Hecho(typeof(CobroProvisionalIncorporadoV1), CobroProvisionalIncorporadoV1.Type, "CPI", N, C, proceso: false),
        Hecho(typeof(DeterioroCalculadoV1), DeterioroCalculadoV1.Type, "DET", N, Cierre, proceso: true),
        Hecho(typeof(CarteraReclasificadaPorCalificacionV1), CarteraReclasificadaPorCalificacionV1.Type, "RCL", N, Cierre, proceso: true),
        Hecho(typeof(ReversoDeCierreV1), ReversoDeCierreV1.Type, "RCI", N, Reapertura, proceso: true),
        // K4 y K3, declarados en K1.
        Hecho(typeof(GastoDeCobranzaCausadoV1), GastoDeCobranzaCausadoV1.Type, "GCO", N, C, proceso: false),
        Hecho(typeof(CreditoReestructuradoV1), CreditoReestructuradoV1.Type, "REE", N, C, proceso: false),
        Hecho(typeof(CreditoModificadoV1), CreditoModificadoV1.Type, "MOD", N, C, proceso: false),
        Hecho(typeof(CreditoNovadoV1), CreditoNovadoV1.Type, "NOV", N, C, proceso: false),
        Hecho(typeof(CondonacionAprobadaV1), CondonacionAprobadaV1.Type, "CON", N, C, proceso: false),
        Hecho(typeof(CreditoCastigadoV1), CreditoCastigadoV1.Type, "CAS", N, C, proceso: false),
        // Informativos de la activación, la carga y el cierre: sin comprobante, con recibo.
        Hecho(typeof(CarteraActivadaV1), CarteraActivadaV1.Type, "ACT", I, C, proceso: false),
        Hecho(typeof(CargaDeCarteraAplicadaV1), CargaDeCarteraAplicadaV1.Type, "CAP", I, C, proceso: false),
        Hecho(typeof(CierreDeCarteraRealizadoV1), CierreDeCarteraRealizadoV1.Type, "CIR", I, Cierre, proceso: true),
        Hecho(typeof(CierreDeCarteraReabiertoV1), CierreDeCarteraReabiertoV1.Type, "CRA", I, Reapertura, proceso: true),
        // A3 (C15): las cuentas contingentes. Se quitan si el dueño rechaza A3.
        Hecho(typeof(CreditoAprobadoSinDesembolsarV1), CreditoAprobadoSinDesembolsarV1.Type, "CAD", N, CoP, proceso: true),
        Hecho(typeof(CupoRotativoOtorgadoV1), CupoRotativoOtorgadoV1.Type, "CRO", N, CoP, proceso: true),
        Hecho(typeof(GarantiaConstituidaV1), GarantiaConstituidaV1.Type, "GCT", N, CoP, proceso: true),
        // K6b (T1056; pregunta E12, propuesta por defecto): la corrección monetaria de los créditos en UVR, masiva como la causación.
        Hecho(typeof(CorreccionMonetariaUvrCausadaV1), CorreccionMonetariaUvrCausadaV1.Type, "CMU", N, P, proceso: true),
        // K6b (decisión del dueño del 2026-10-17): el reparto en pesos de un pago aplicado en un día cuya UVR se corrigió, masivo como el ajuste.
        Hecho(typeof(PagoRedistribuidoPorValorNacionalV1), PagoRedistribuidoPorValorNacionalV1.Type, "PRN", N, P, proceso: true),
        // Feature 014 (T048; contracts/mensajes.md §1.1 de la 014, research C23): los 33 hechos de captaciones y aportes, declarados en la fase
        // fundacional; cada uno lo emite la historia de su entrega (K7 a K10). Viven en Contracts/Lending/Deposits.
        Hecho(typeof(ConsignacionEnCuentaAplicadaV1), ConsignacionEnCuentaAplicadaV1.Type, "CCN", N, C, proceso: false),
        Hecho(typeof(AporteRecibidoV1), AporteRecibidoV1.Type, "APR", N, C, proceso: false),
        Hecho(typeof(RetiroDeCuentaAplicadoV1), RetiroDeCuentaAplicadoV1.Type, "RCT", N, C, proceso: true),
        Hecho(typeof(TrasladoEntreCuentasAplicadoV1), TrasladoEntreCuentasAplicadoV1.Type, "TRC", N, C, proceso: false),
        Hecho(typeof(GmfRetenidoV1), GmfRetenidoV1.Type, "GMF", N, C, proceso: true),
        Hecho(typeof(InteresDeCaptacionCausadoV1), InteresDeCaptacionCausadoV1.Type, "ICC", N, P, proceso: true),
        Hecho(typeof(AjusteDeCausacionDeCaptacionV1), AjusteDeCausacionDeCaptacionV1.Type, "AJC", N, CoP, proceso: true),
        Hecho(typeof(InteresDeCaptacionLiquidadoV1), InteresDeCaptacionLiquidadoV1.Type, "ILQ", N, CoP, proceso: true),
        Hecho(typeof(RetencionPracticadaV1), RetencionPracticadaV1.Type, "RTF", N, CoP, proceso: true),
        Hecho(typeof(CdatConstituidoV1), CdatConstituidoV1.Type, "CDC", N, C, proceso: false),
        Hecho(typeof(InteresesDeCdatPagadosV1), InteresesDeCdatPagadosV1.Type, "CDP", N, C, proceso: true),
        Hecho(typeof(CdatProrrogadoV1), CdatProrrogadoV1.Type, "CDR", N, C, proceso: true),
        Hecho(typeof(CdatRenovadoV1), CdatRenovadoV1.Type, "CDN", N, C, proceso: false),
        Hecho(typeof(CdatRedimidoV1), CdatRedimidoV1.Type, "CDD", N, C, proceso: false),
        Hecho(typeof(CdatCanceladoV1), CdatCanceladoV1.Type, "CDX", N, C, proceso: true),
        Hecho(typeof(PenalidadAplicadaV1), PenalidadAplicadaV1.Type, "PEN", N, C, proceso: true),
        Hecho(typeof(CaptacionReclasificadaV1), CaptacionReclasificadaV1.Type, "RCC", N, CoP, proceso: true),
        Hecho(typeof(BonificacionReconocidaV1), BonificacionReconocidaV1.Type, "BON", N, C, proceso: true),
        Hecho(typeof(CargoDeCuentaCausadoV1), CargoDeCuentaCausadoV1.Type, "CGA", N, C, proceso: true),
        Hecho(typeof(CargoPendienteCobradoV1), CargoPendienteCobradoV1.Type, "CGP", N, C, proceso: false),
        Hecho(typeof(CargoDeCuentaCondonadoV1), CargoDeCuentaCondonadoV1.Type, "CGN", N, C, proceso: false),
        Hecho(typeof(RevalorizacionDeAportesAplicadaV1), RevalorizacionDeAportesAplicadaV1.Type, "RVA", N, C, proceso: false),
        Hecho(typeof(ExcedentesDistribuidosV1), ExcedentesDistribuidosV1.Type, "EXD", N, C, proceso: false),
        Hecho(typeof(AportesAmortizadosV1), AportesAmortizadosV1.Type, "AMZ", N, C, proceso: false),
        Hecho(typeof(CruceAlRetiroAplicadoV1), CruceAlRetiroAplicadoV1.Type, "CRR", N, C, proceso: false),
        Hecho(typeof(PerdidaAplicadaAlRetiroV1), PerdidaAplicadaAlRetiroV1.Type, "PDR", N, C, proceso: false),
        Hecho(typeof(DevolucionOrdenadaV1), DevolucionOrdenadaV1.Type, "DVO", N, C, proceso: true),
        Hecho(typeof(DevolucionGiradaV1), DevolucionGiradaV1.Type, "DVG", I, P, proceso: false),
        Hecho(typeof(SaldoTrasladadoANoReclamadosV1), SaldoTrasladadoANoReclamadosV1.Type, "SNR", N, C, proceso: true),
        Hecho(typeof(EmbargoConsignadoV1), EmbargoConsignadoV1.Type, "EMC", N, C, proceso: false),
        Hecho(typeof(OperacionDeCuentaReversadaV1), OperacionDeCuentaReversadaV1.Type, "RVC", N, C, proceso: false),
        Hecho(typeof(CaptacionesActivadasV1), CaptacionesActivadasV1.Type, "CAV", I, C, proceso: false),
        Hecho(typeof(CargaDeCaptacionesAplicadaV1), CargaDeCaptacionesAplicadaV1.Type, "CAC", I, C, proceso: false),
    ];

    /// <summary>Los 6 mensajes de Cartera a otros destinos de §2.2, en su orden, y la v2 de la orden de giro (feature 014).</summary>
    public static IReadOnlyList<TipoDeMensaje> AOtrosDestinos { get; } =
    [
        Otro(typeof(ObligacionCreadaV1), ObligacionCreadaV1.Type, IntegrationDestinations.Inventory, C, proceso: true),
        Otro(typeof(ObligacionRechazadaV1), ObligacionRechazadaV1.Type, IntegrationDestinations.Inventory, P, proceso: true),
        Otro(typeof(OrdenDeGiroEmitidaV1), OrdenDeGiroEmitidaV1.Type, IntegrationDestinations.Treasury, C, proceso: false),
        Otro(typeof(AnulacionDeOrdenDeGiroV1), AnulacionDeOrdenDeGiroV1.Type, IntegrationDestinations.Treasury, P, proceso: false),
        // Pensado para varios destinos (el del recaudador del pago); en V1 el conjunto admitido es sólo Recaudo (§11.5).
        // Feature 014 (decisión del dueño del 2026-10-18, D19): también a Tesorería, que paga créditos si adopta ProveedorDeObligaciones.
        new(typeof(PagoReversadoPorCarteraV1), PagoReversadoPorCarteraV1.Type, 1, ModulosDeOrigen.Cartera,
            new HashSet<string>([IntegrationDestinations.Receipts, IntegrationDestinations.Treasury], StringComparer.Ordinal), N, C, false),
        // K2, declarado en K1.
        Otro(typeof(DescuentosPorPagaduriaV1), DescuentosPorPagaduriaV1.Type, IntegrationDestinations.Receipts, C, proceso: false),
        // Feature 014 (T055; mensajes.md §1.2 y §5.3 de la 014): la orden de giro v2 de captaciones, declarada para que Tesorería pueda adoptarla; la emite
        // US10 (también el proceso: el pago de intereses del CDAT a un tercero).
        new(typeof(OrdenDeGiroEmitidaV2), OrdenDeGiroEmitidaV2.Type, 2, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(IntegrationDestinations.Treasury), N, C, true),
        // Feature 014, US10 (T098; mensajes.md §1.2 #34 y §5.1): el retiro del asociado aprobado, a Asociados; lo origina la persona que aprueba.
        Otro(typeof(RetiroDeAsociadoAprobadoV1), RetiroDeAsociadoAprobadoV1.Type, IntegrationDestinations.Associates, C, proceso: false),
        // Feature 014, US4 (T112; mensajes.md §1.2 #35, #38, #39): la consignación reversada desde la Cartera al destino de su recaudador (Recaudo o
        // Tesorería; la solicitud dice cuál) y los cargos gravados a Comercial, que los factura (el causado lo emite también el ciclo diario).
        new(typeof(ConsignacionReversadaPorCarteraV1), ConsignacionReversadaPorCarteraV1.Type, 1, ModulosDeOrigen.Cartera,
            new HashSet<string>([IntegrationDestinations.Receipts, IntegrationDestinations.Treasury], StringComparer.Ordinal), N, C, false),
        Otro(typeof(CargoDeCaptacionCausadoV1), CargoDeCaptacionCausadoV1.Type, IntegrationDestinations.Inventory, C, proceso: true),
        Otro(typeof(CargoDeCaptacionReversadoV1), CargoDeCaptacionReversadoV1.Type, IntegrationDestinations.Inventory, P, proceso: false),
        // Feature 014 (T091; decisión del dueño del 2026-10-18, D36; mensajes.md §5.4 de la 014): el archivo de un grupo del convenio, con créditos y cuotas
        // de cuentas; lo origina la persona que genera.
        new(typeof(DescuentosPorPagaduriaV2), DescuentosPorPagaduriaV2.Type, 2, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(IntegrationDestinations.Receipts), N, C,
            false),
    ];

    /// <summary>Los tipos de Cartera (81 desde la feature 014: 70 hechos a Contabilidad y 11 a otros destinos).</summary>
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } = [.. HechosAContabilidad, .. AOtrosDestinos];

    /// <summary>
    /// US4 (T119): el <c>PagoAplicado</c> que origina el débito automático del ciclo diario —recaudador <c>OwnLoanPayoff</c> y clave
    /// <c>CAR:debito-automatico:…</c>— admite proceso; cualquier otro, no.
    /// </summary>
    public static bool EsDebitoAutomatico(object contenido) =>
        contenido is PagoAplicadoV1 { Collector: { } recaudador }
        && string.Equals(recaudador.CollectorClass, nameof(Integration.Lending.ClaseDeRecaudador.OwnLoanPayoff), StringComparison.Ordinal)
        && recaudador.IdempotencyKey.StartsWith(PrefijoDelDebitoAutomatico, StringComparison.Ordinal);

    /// <summary>El prefijo de la clave del débito automático (contrato-de-cuentas §3.12, R16).</summary>
    public const string PrefijoDelDebitoAutomatico = "CAR:debito-automatico:";

    private static TipoDeMensaje Hecho(Type record, string type, string codigoCorto, IntegrationMessageKind kind, FormaDeClaveDeEvento forma, bool proceso) =>
        new(record, type, 1, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(A), kind, forma, proceso, codigoCorto);

    private static TipoDeMensaje Otro(Type record, string type, string destino, FormaDeClaveDeEvento forma, bool proceso) =>
        new(record, type, 1, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(destino), N, forma, proceso);
}
