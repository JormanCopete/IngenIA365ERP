using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Un módulo habilitable de una cooperativa (feature 020, R1; FR-001 a FR-004). Una fila por cooperativa y módulo; la habilita o
/// deshabilita sólo el maestro, con motivo, y cada cambio queda en la auditoría. Sin fila, el módulo está apagado.
/// Maps to [dbo].[ADM_TenantModules].
/// </summary>
public class TenantModule : AuditableEntity
{
    /// <summary>El <c>PublicId</c> de la cooperativa (como <see cref="TenantIdentityPolicy.TenantId"/>).</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Un código de <c>ModulosHabilitables</c> (<c>TRN</c>).</summary>
    public string ModuleCode { get; private set; } = string.Empty;

    public bool IsEnabled { get; private set; }

    public DateTime ChangedAt { get; private set; }

    public string ChangedBy { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    private TenantModule() { }

    public static TenantModule Nuevo(Guid tenantId, string moduleCode) =>
        new() { TenantId = tenantId, ModuleCode = moduleCode.Trim().ToUpperInvariant() };

    /// <summary>Habilita o deshabilita, con quién, cuándo y por qué.</summary>
    public void Cambiar(bool habilitado, DateTime cuando, string quien, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("El cambio de un módulo lleva motivo.", nameof(motivo));
        IsEnabled = habilitado;
        ChangedAt = cuando;
        ChangedBy = quien.Length <= 150 ? quien : quien[..150];
        Reason = motivo.Trim();
    }
}
