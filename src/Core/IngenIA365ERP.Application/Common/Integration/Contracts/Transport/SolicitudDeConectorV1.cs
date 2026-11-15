using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Transport;

/// <summary>
/// <c>SolicitudDeConector</c> v1 (feature 020, R14, R18; contracts/mensajes.md §2 de la 020): Transporte pide avisar a un sistema externo
/// que un servicio de un vehículo quedó pagado. Las variables viajan <b>resueltas</b> al momento del pago y la versión del conector queda
/// sellada: un cambio posterior del vehículo o del conector no altera el envío. <see cref="Sensitive"/> dice qué variables no se escriben en
/// el cuerpo saneado ni en la auditoría. No lleva credenciales: el consumidor las toma de la cooperativa. Clave de evento
/// <c>Confirmation:{cargo:N}</c>: un cargo pide un solo aviso.
/// </summary>
public sealed record SolicitudDeConectorV1
{
    public const string Type = "SolicitudDeConector";

    public Guid ConnectorPublicId { get; init; }

    public int ConnectorVersion { get; init; }

    /// <summary>El identificador que recibe el sistema externo: el mismo en todos los reintentos (el <c>ExternalId</c> del cargo).</summary>
    public Guid ExternalId { get; init; }

    /// <summary>Quién pidió el aviso, como texto: <c>TRN:{cargo:N}</c>. El conector no conoce Transporte.</summary>
    public string OriginRef { get; init; } = string.Empty;

    /// <summary>Las variables del catálogo (<c>VariablesDeConector</c>) con su valor como texto invariante.</summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> Sensitive { get; init; } = [];
}

/// <summary>Los tipos que emite Transporte (feature 020): origen <c>TRN</c>, destino <c>CON</c>, admite proceso.</summary>
public static class CatalogoDeMensajesDeTransporteV1
{
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(SolicitudDeConectorV1), SolicitudDeConectorV1.Type, 1, ModulosDeOrigen.Transporte,
            TipoDeMensaje.Un(IntegrationDestinations.Connectors), IntegrationMessageKind.Business, FormaDeClaveDeEvento.ConfirmacionPorPublicId,
            AdmiteProceso: true),
    ];
}
