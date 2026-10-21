using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// A quién le llegan las alertas de la bandeja según el módulo que emitió el mensaje y su destino (feature 013, T19; R14;
/// contracts/mensajes.md §14.4): un rechazo tiene que llegar a quien puede actuar. Lo de Inventario conserva los destinatarios
/// que la cooperativa configuró en el tipo de alerta (nulo = los del tipo); lo de Cartera, Tesorería y Recaudo va a quien
/// reprocesa la bandeja de Cartera y, si el destino es Contabilidad, a quien corre sus lotes. Una validación fallida llega a
/// Cartera y al módulo que originó la venta. (nuevo)
/// </summary>
public static class DestinatariosDeIntegracion
{
    public const string ReprocesarCartera = "Lending.Messages.Reprocess";
    public const string ReprocesarInventario = "Inventory.Messages.Reprocess";
    public const string CorrerLotesDeCartera = "Accounting.LendingBatches.Run";

    /// <summary>
    /// Los permisos destinatarios de <paramref name="tipoDeAlerta"/> para un mensaje de <paramref name="modulo"/> a
    /// <paramref name="destino"/>; nulo para usar los configurados en el tipo de alerta.
    /// </summary>
    public static IReadOnlyList<string>? Permisos(string tipoDeAlerta, string modulo, string destino)
    {
        if (tipoDeAlerta == TiposDeAlerta.ValidacionFallida)
        {
            return modulo == ModulosDeOrigen.Inventario
                ? [ReprocesarCartera, ReprocesarInventario]
                : [ReprocesarCartera];
        }

        if (modulo == ModulosDeOrigen.Inventario) return null;

        return destino == IntegrationDestinations.Accounting
            ? [ReprocesarCartera, CorrerLotesDeCartera]
            : [ReprocesarCartera];
    }
}
