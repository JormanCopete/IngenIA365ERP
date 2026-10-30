using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Solicitud de cambio de correo (feature 017, D-09, FR-037/FR-038). Maps to
/// [dbo].[ADM_EmailChangeRequests].
/// </summary>
public class EmailChangeRequest : AuditableEntity
{
    public const int MinutosDeVigencia = 30;
    public const int IntentosMaximos = 5;

    public Guid CentralUserId { get; private set; }

    [MaxLength(256)] public string NewEmail { get; private set; } = string.Empty;
    [MaxLength(256)] public string NewNormalizedEmail { get; private set; } = string.Empty;

    /// <summary>SHA-256 del código enviado al correo nuevo. El código plano nunca se guarda.</summary>
    public byte[] CodeHash { get; private set; } = [];

    public DateTime ExpiresAt { get; private set; }
    public int Attempts { get; private set; }
    public EmailChangeStatus Status { get; private set; } = EmailChangeStatus.Pending;

    [MaxLength(45)] public string? RequestedFromIp { get; private set; }

    /// <summary>La hizo el maestro como rescate (FR-038).</summary>
    public bool ByMaster { get; private set; }

    [MaxLength(500)] public string? Reason { get; private set; }

    private EmailChangeRequest() { }

    public static EmailChangeRequest Solicitar(
        Guid centralUserId, string newEmail, string newNormalizedEmail, byte[] codeHash, DateTime now, string? ip) => new()
        {
            CentralUserId = centralUserId,
            NewEmail = newEmail,
            NewNormalizedEmail = newNormalizedEmail,
            CodeHash = codeHash,
            ExpiresAt = now.AddMinutes(MinutosDeVigencia),
            RequestedFromIp = ip,
        };

    public static EmailChangeRequest PorMaestro(
        Guid centralUserId, string newEmail, string newNormalizedEmail, string reason, DateTime now) => new()
        {
            CentralUserId = centralUserId,
            NewEmail = newEmail,
            NewNormalizedEmail = newNormalizedEmail,
            CodeHash = [],
            ExpiresAt = now,
            Status = EmailChangeStatus.Confirmed,
            ByMaster = true,
            Reason = reason,
        };

    public bool Vigente(DateTime now) => Status == EmailChangeStatus.Pending && ExpiresAt > now && Attempts < IntentosMaximos;

    public void IntentoFallido() => Attempts++;

    public void Confirmar() => Status = EmailChangeStatus.Confirmed;

    public void Cancelar() => Status = EmailChangeStatus.Cancelled;
}

public enum EmailChangeStatus
{
    Pending = 0,
    Confirmed = 1,
    Expired = 2,
    Cancelled = 3,
}
