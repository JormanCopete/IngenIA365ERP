using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Reservar y liberar el número de la obligación de una venta a crédito (feature 013, T0151; FR-057, T22; contracts/interfaces.md
/// §5.2). Corre dentro de la confirmación de Comercial, exige transacción en curso y nunca confirma por su cuenta. Separada de
/// <see cref="IConsultasDeCartera"/> porque una consulta no cambia estado (T90). Antes de activar Cartera responde
/// <see cref="EstadosDeReserva.NotEnabled"/> (<see cref="ReservasDeCarteraNoHabilitada"/>). (nuevo)
/// </summary>
public interface IReservasDeCartera
{
    Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva solicitud, CancellationToken ct);
    Task<ResultadoDeReservaDto> LiberarAsync(SolicitudDeLiberacion solicitud, CancellationToken ct);
}

/// <summary>Pide reservar el número del pago de crédito de una venta (clave <c>INV:{paymentPublicId:N}:{monto}</c>). (nuevo)</summary>
public sealed record SolicitudDeReserva(
    string IdempotencyKey,
    Guid PaymentPublicId,
    Guid DocumentPublicId,
    Guid PersonPublicId,
    PaymentMeansClass MeansClass,
    decimal Amount,
    DateOnly OperationDate,
    string CreditLineCode,
    string? SalesChannelCode)
{
    /// <summary>
    /// Feature 013 (T0584; C31): el <c>EvidenceId</c> de la consulta de cupo y estado que precedió a la reserva (la consulta verifica listas sin
    /// escribir; la evidencia queda en la auditoría y en la reserva). Opcional: aditivo al contrato de la 012.
    /// </summary>
    public string? ConsultationEvidence { get; init; }
}

/// <summary>Pide liberar una reserva por su clave. (nuevo)</summary>
public sealed record SolicitudDeLiberacion(string IdempotencyKey, Guid PaymentPublicId, string Reason);

/// <summary>Lo que respondió la reserva o la liberación (<see cref="EstadosDeReserva"/>). (nuevo)</summary>
public sealed record ResultadoDeReservaDto(
    string Status,
    string? LoanNumber,
    Guid? ReservationPublicId,
    DateOnly? ExpiresOn,
    string? Code,
    string? Message);

/// <summary>Valores de <see cref="ResultadoDeReservaDto.Status"/>. (nuevo)</summary>
public static class EstadosDeReserva
{
    public const string Reserved = "Reserved";
    public const string Used = "Used";
    public const string Released = "Released";
    public const string Expired = "Expired";
    public const string NotEnabled = "NotEnabled";
    public const string Rejected = "Rejected";
    public const string NoResponse = "NoResponse";
}
