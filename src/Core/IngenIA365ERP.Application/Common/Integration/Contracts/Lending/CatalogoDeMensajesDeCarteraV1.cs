using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// Los 42 tipos que emite Cartera (feature 013, T0070, T0071; contracts/mensajes.md §2.1 y §2.2; DT §2.11, C01–C03, C15, C91, C94,
/// C117): los 36 hechos a Contabilidad (K6b, T1056, suma la corrección monetaria UVR), cada uno con su código corto (<c>LND_FactTypes.ShortCode</c>, que la semilla de tipos de
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

    /// <summary>Los 36 hechos <c>CAR → Accounting</c> de §2.1, en su orden.</summary>
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
        Hecho(typeof(PagoAplicadoV1), PagoAplicadoV1.Type, "PAP", N, C, proceso: false),
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
    ];

    /// <summary>Los 6 mensajes de Cartera a otros destinos de §2.2, en su orden.</summary>
    public static IReadOnlyList<TipoDeMensaje> AOtrosDestinos { get; } =
    [
        Otro(typeof(ObligacionCreadaV1), ObligacionCreadaV1.Type, IntegrationDestinations.Inventory, C, proceso: true),
        Otro(typeof(ObligacionRechazadaV1), ObligacionRechazadaV1.Type, IntegrationDestinations.Inventory, P, proceso: true),
        Otro(typeof(OrdenDeGiroEmitidaV1), OrdenDeGiroEmitidaV1.Type, IntegrationDestinations.Treasury, C, proceso: false),
        Otro(typeof(AnulacionDeOrdenDeGiroV1), AnulacionDeOrdenDeGiroV1.Type, IntegrationDestinations.Treasury, P, proceso: false),
        // Pensado para varios destinos (el del recaudador del pago); en V1 el conjunto admitido es sólo Recaudo (§11.5).
        Otro(typeof(PagoReversadoPorCarteraV1), PagoReversadoPorCarteraV1.Type, IntegrationDestinations.Receipts, C, proceso: false),
        // K2, declarado en K1.
        Otro(typeof(DescuentosPorPagaduriaV1), DescuentosPorPagaduriaV1.Type, IntegrationDestinations.Receipts, C, proceso: false),
    ];

    /// <summary>Los 42 tipos de Cartera.</summary>
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } = [.. HechosAContabilidad, .. AOtrosDestinos];

    private static TipoDeMensaje Hecho(Type record, string type, string codigoCorto, IntegrationMessageKind kind, FormaDeClaveDeEvento forma, bool proceso) =>
        new(record, type, 1, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(A), kind, forma, proceso, codigoCorto);

    private static TipoDeMensaje Otro(Type record, string type, string destino, FormaDeClaveDeEvento forma, bool proceso) =>
        new(record, type, 1, ModulosDeOrigen.Cartera, TipoDeMensaje.Un(destino), N, forma, proceso);
}
