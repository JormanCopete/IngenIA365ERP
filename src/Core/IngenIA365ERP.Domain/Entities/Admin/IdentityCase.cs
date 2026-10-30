using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Un conflicto de identidad que resuelve el maestro (feature 017, FR-031, FR-034). Maps to
/// [dbo].[ADM_IdentityCases].
/// </summary>
public class IdentityCase : AuditableEntity
{
    public IdentityCaseKind Kind { get; private set; }
    public Guid CentralUserId { get; private set; }
    public Guid? OtherCentralUserId { get; private set; }
    public IdentityCaseStatus Status { get; private set; } = IdentityCaseStatus.Open;

    /// <summary>JSON con el detalle (cooperativas pendientes de una fusión, documento intentado…).</summary>
    public string? Detail { get; private set; }

    public Guid? OpenedByCentralUserId { get; private set; }
    public Guid? ResolvedByCentralUserId { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    [MaxLength(500)]
    public string? Resolution { get; private set; }

    private IdentityCase() { }

    public static IdentityCase Abrir(
        IdentityCaseKind kind, Guid centralUserId, Guid? otherCentralUserId, string? detail, Guid? openedBy) => new()
        {
            Kind = kind,
            CentralUserId = centralUserId,
            OtherCentralUserId = otherCentralUserId,
            Detail = detail,
            OpenedByCentralUserId = openedBy,
        };

    public void EnCurso(string? detail)
    {
        Status = IdentityCaseStatus.InProgress;
        Detail = detail;
    }

    public void Cerrar(IdentityCaseStatus estadoFinal, Guid resolvedBy, string? resolution, DateTime now)
    {
        if (estadoFinal is not (IdentityCaseStatus.Done or IdentityCaseStatus.Dismissed))
            throw new ArgumentOutOfRangeException(nameof(estadoFinal));
        Status = estadoFinal;
        ResolvedByCentralUserId = resolvedBy;
        Resolution = resolution;
        ResolvedAt = now;
    }
}

public enum IdentityCaseKind
{
    /// <summary>Alguien quiso registrar un documento que ya es de otra identidad.</summary>
    DocumentTaken = 0,
    /// <summary>Fusión de dos identidades de la misma persona.</summary>
    Merge = 1,
    /// <summary>Las cooperativas de una cuenta tienen documentos distintos para ella.</summary>
    DocumentMismatch = 2,
}

public enum IdentityCaseStatus
{
    Open = 0,
    InProgress = 1,
    Done = 2,
    Dismissed = 3,
}
