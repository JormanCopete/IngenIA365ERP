using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El lector del registro de participantes de la cooperativa (feature 013, T0078; T16; data-model parte 3 §4.6; interfaces.md §11.3;
/// contracts/mensajes.md §13). Un participante <b>cuenta</b> a una fecha si está habilitado y vigente, su implementación está en este
/// despliegue con la versión que adoptó y no es un doble de prueba en un despliegue de producción. Lo usan el despachador (un destino
/// se recorre sólo si su fila cuenta), la consulta del crédito de una venta (destino «disponible»), el contrato de obligaciones
/// (valida al recaudador) y la activación de Cartera (exige Recaudo y Tesorería reales). Sólo lee: lo escribe
/// <see cref="EscritorDelRegistroDeIntegracion"/>. (nuevo)
/// </summary>
public interface IRegistroDeIntegracion
{
    /// <summary>La fila que cuenta para el destino <paramref name="destino"/> a <paramref name="fecha"/>, o nula.</summary>
    Task<ParticipanteVigente?> DestinoAsync(string destino, DateOnly fecha, CancellationToken ct);

    /// <summary>¿El destino tiene una fila que cuenta a <paramref name="fecha"/>? (consumidor registrado incluido).</summary>
    Task<bool> DestinoHabilitadoAsync(string destino, DateOnly fecha, CancellationToken ct);

    /// <summary>El recaudador del contrato de obligaciones con ese código que cuenta a <paramref name="fecha"/>, o nulo.</summary>
    Task<ParticipanteVigente?> RecaudadorAsync(string codigo, DateOnly fecha, CancellationToken ct);

    /// <summary>Todos los participantes que cuentan a <paramref name="fecha"/>.</summary>
    Task<IReadOnlyList<ParticipanteVigente>> QueCuentanAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>
    /// ¿La implementación del participante está en este despliegue con las versiones que adoptó a <paramref name="fecha"/>? Un destino
    /// necesita su consumidor (<see cref="IDestinoDeMensajes"/>) y que éste acepte lo adoptado; un recaudador, que el contrato que
    /// adoptó sea de una versión que Cartera implementa.
    /// </summary>
    bool ImplementacionDisponible(IntegrationParticipant participante, DateOnly fecha);

    /// <summary>¿Cuenta a <paramref name="fecha"/>? (habilitado, vigente, implementado y no doble en producción).</summary>
    bool Cuenta(IntegrationParticipant participante, DateOnly fecha);
}

/// <summary>Un participante que cuenta, con las versiones que adoptó vigentes a la fecha. (nuevo)</summary>
public sealed record ParticipanteVigente(
    Guid PublicId,
    string Code,
    IntegrationParticipantClass Class,
    IntegrationParticipantRole Role,
    string Destination,
    bool IsTestDouble,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<VersionAdoptada> Adopciones);

/// <summary>Un contrato o tipo de mensaje adoptado y su versión. (nuevo)</summary>
public sealed record VersionAdoptada(string Contrato, int Version);
