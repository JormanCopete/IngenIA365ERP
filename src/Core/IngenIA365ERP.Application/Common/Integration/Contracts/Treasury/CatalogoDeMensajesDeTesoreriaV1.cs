using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

/// <summary>
/// Los hechos que Tesorería emite a Contabilidad (feature 015, US10, T0145; US11, T0159; contracts/contabilidad.md §4; mensajes.md §2.5), cada uno con su código
/// corto. Todos son de <c>TES</c>, v1, a un solo destino y los origina una persona (ninguno admite proceso, mensajes.md §1). Cada historia suma los suyos: US10 el
/// GMF del pago y US11 el pago girado. <c>ResultadoDelGiro</c> (de Tesorería hacia Cartera) sigue en <c>CatalogoDeMensajesHaciaCarteraV1</c>. (nuevo)
/// </summary>
public static class CatalogoDeMensajesDeTesoreriaV1
{
    private const string A = IntegrationDestinations.Accounting;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const FormaDeClaveDeEvento C = FormaDeClaveDeEvento.Confirmacion;

    /// <summary>Los hechos <c>TES → Accounting</c> construidos hasta aquí.</summary>
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        // US10 (fase 7): el GMF del pago que dispone de una cuenta contable a favor de un tercero.
        Hecho(typeof(GmfDePagoCausadoV1), GmfDePagoCausadoV1.Type, "GMP"),
        // US11 (fase 8): el pago girado de una orden.
        Hecho(typeof(PagoDeOrdenGiradoV1), PagoDeOrdenGiradoV1.Type, "POG"),
        // US19 (fase 14): los cheques girados y la remesa confirmada.
        Hecho(typeof(ChequeGiradoEmitidoV1), ChequeGiradoEmitidoV1.Type, "CHE"),
        Hecho(typeof(ChequeGiradoAnuladoV1), ChequeGiradoAnuladoV1.Type, "CHA"),
        Hecho(typeof(RemesaConfirmadaV1), RemesaConfirmadaV1.Type, "RMC"),
    ];

    private static TipoDeMensaje Hecho(Type record, string type, string codigoCorto) =>
        new(record, type, 1, ModulosDeOrigen.Tesoreria, TipoDeMensaje.Un(A), N, C, AdmiteProceso: false, codigoCorto);
}
