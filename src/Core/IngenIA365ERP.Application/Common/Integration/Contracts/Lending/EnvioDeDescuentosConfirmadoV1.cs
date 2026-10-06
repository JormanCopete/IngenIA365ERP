using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

/// <summary>
/// <c>EnvioDeDescuentosConfirmado</c> v1 (§12.5; <c>REC → Lending</c>; K2): Recaudo confirma el envío de una versión del archivo de
/// descuentos a la pagaduría. Clave <c>Confirmation:{archivo:N}</c>; no admite proceso.
/// </summary>
public sealed record EnvioDeDescuentosConfirmadoV1
{
    public const string Type = "EnvioDeDescuentosConfirmado";

    /// <summary>El <c>DescuentosPorPagaduria</c>.</summary>
    public Guid FileMessageId { get; init; }

    public Guid FilePublicId { get; init; }

    public int FileVersion { get; init; }

    public string FileSha256 { get; init; } = string.Empty;

    public DateTimeOffset SentAt { get; init; }

    /// <summary><c>Email</c>, <c>Portal</c>, <c>Physical</c> u <c>Other</c>.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Radicado o constancia.</summary>
    public string? Reference { get; init; }

    public UserRefV1 SentBy { get; init; } = new();
}
