using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

/// <summary>
/// Los hechos que Recaudo emite a Contabilidad (feature 015, T0091; contracts/contabilidad.md §3; mensajes.md §2.5), cada uno con su código corto. Todos son
/// de <c>REC</c>, v1, a un solo destino y los origina una persona (ninguno admite proceso, mensajes.md §1). Cada historia suma los suyos: US2 los cinco de la
/// caja y US3 el recibo. Los tres de Recaudo hacia Cartera siguen en <c>CatalogoDeMensajesHaciaCarteraV1</c>. (nuevo)
/// </summary>
public static class CatalogoDeMensajesDeRecaudoV1
{
    private const string A = IntegrationDestinations.Accounting;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const FormaDeClaveDeEvento C = FormaDeClaveDeEvento.Confirmacion;

    /// <summary>Los hechos <c>REC → Accounting</c> construidos hasta aquí.</summary>
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        // US2 (fase 5): la caja.
        Hecho(typeof(MovimientoDeCajaRegistradoV1), MovimientoDeCajaRegistradoV1.Type, "MCR"),
        Hecho(typeof(MedioReclasificadoV1), MedioReclasificadoV1.Type, "MRC"),
        Hecho(typeof(DiferenciaDeArqueoAprobadaV1), DiferenciaDeArqueoAprobadaV1.Type, "DAA"),
        Hecho(typeof(AjusteDeSesionRegistradoV1), AjusteDeSesionRegistradoV1.Type, "ASR"),
        Hecho(typeof(SobranteLlevadoAIngresoV1), SobranteLlevadoAIngresoV1.Type, "SLI"),
        // US3 (fase 6): el recibo de caja, por lotes resumidos (D25).
        Hecho(typeof(ReciboEmitidoV1), ReciboEmitidoV1.Type, "RCE"),
        // US5 (fase 10): la anulación, el reverso del módulo y los saldos a favor, en línea.
        Hecho(typeof(ReciboAnuladoV1), ReciboAnuladoV1.Type, "RAN"),
        Hecho(typeof(AplicacionReversadaPorModuloV1), AplicacionReversadaPorModuloV1.Type, "ARM"),
        Hecho(typeof(SaldoAFavorDePagadorResueltoV1), SaldoAFavorDePagadorResueltoV1.Type, "SFP"),
        Hecho(typeof(SaldoAFavorDeCarteraDevueltoV1), SaldoAFavorDeCarteraDevueltoV1.Type, "SFC"),
    ];

    private static TipoDeMensaje Hecho(Type record, string type, string codigoCorto) =>
        new(record, type, 1, ModulosDeOrigen.Recaudo, TipoDeMensaje.Un(A), N, C, AdmiteProceso: false, codigoCorto);
}
