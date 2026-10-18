using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// Los tipos que Tesorería y Recaudo emiten hacia Cartera (feature 013, T0071; contracts/mensajes.md §2.3, §12; D15, D27). Viven
/// aquí porque los define esta especificación; la feature que los adopte los emite sin moverlos. Ninguno admite proceso: llegan
/// con la persona que actuó en su módulo (T11). Las ventas y ajustes de Comercial a Cartera están en <c>CatalogoDeMensajesV1</c>.
/// </summary>
public static class CatalogoDeMensajesHaciaCarteraV1
{
    private const string L = IntegrationDestinations.Lending;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const FormaDeClaveDeEvento P = FormaDeClaveDeEvento.ConfirmacionPorPublicId;

    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(ResultadoDelGiroV1), ResultadoDelGiroV1.Type, 1, ModulosDeOrigen.Tesoreria, TipoDeMensaje.Un(L), N, P, AdmiteProceso: false),
        // K2, declarados en K1.
        new(typeof(EnvioDeDescuentosConfirmadoV1), EnvioDeDescuentosConfirmadoV1.Type, 1, ModulosDeOrigen.Recaudo, TipoDeMensaje.Un(L), N, P, AdmiteProceso: false),
        new(typeof(ResultadoDeDescuentosV1), ResultadoDeDescuentosV1.Type, 1, ModulosDeOrigen.Recaudo, TipoDeMensaje.Un(L), N, P, AdmiteProceso: false),
        // Feature 014, US4 (T112; mensajes.md §1.3 #40 y §5.6 de la 014): el estado de la factura de un cargo de captación; la emite Comercial y la Cartera
        // sólo lo guarda (informativa).
        new(typeof(Deposits.FacturaDeCargoRegistradaV1), Deposits.FacturaDeCargoRegistradaV1.Type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(L),
            IntegrationMessageKind.Informational, P, AdmiteProceso: false),
    ];
}
