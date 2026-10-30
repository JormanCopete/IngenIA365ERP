using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Identidad;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Política de identidad de una cooperativa (feature 017, D-04, D-06, FR-008, FR-030). Una por
/// cooperativa, como <see cref="TenantMfaPolicy"/>; una cooperativa sin fila usa los defectos.
/// Maps to [dbo].[ADM_TenantIdentityPolicies].
/// </summary>
public class TenantIdentityPolicy : AuditableEntity
{
    public const int IntentosPorDefecto = 3;
    public const int IntentosMinimos = 1;
    public const int IntentosMaximos = 10;

    /// <summary>Por defecto: todos los tipos de acceso del catálogo.</summary>
    public static readonly string TiposPorDefecto = string.Join(',', TiposDeDocumento.DeAcceso.Select(t => t.Codigo));

    public Guid TenantId { get; private set; }

    /// <summary>Códigos de acceso separados por coma (CC,CE,PPT…).</summary>
    [MaxLength(40)]
    public string AcceptedDocumentTypes { get; private set; } = TiposPorDefecto;

    /// <summary>
    /// Desde cuándo esta cooperativa ya no deja posponer el registro del documento. Nula = nunca
    /// bloquea; antes de la fecha es sólo informativa.
    /// </summary>
    public DateOnly? DocumentRequiredFrom { get; private set; }

    /// <summary>Intentos para digitar el documento al activar una invitación.</summary>
    public int MaxActivationAttempts { get; private set; } = IntentosPorDefecto;

    private TenantIdentityPolicy() { }

    public static TenantIdentityPolicy CreateForTenant(Guid tenantId) => new() { TenantId = tenantId };

    /// <summary>Los tipos aceptados, en el orden del catálogo.</summary>
    public IReadOnlyList<TipoDeDocumento> TiposAceptados() => TiposAceptadosDe(AcceptedDocumentTypes);

    public static IReadOnlyList<TipoDeDocumento> TiposAceptadosDe(string? codigos)
    {
        var lista = (codigos ?? TiposPorDefecto)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .ToHashSet();
        return TiposDeDocumento.DeAcceso.Where(t => lista.Contains(t.Codigo)).ToList();
    }

    /// <summary>Cambia la política. Al menos un tipo; intentos entre los límites.</summary>
    public void Cambiar(IEnumerable<string> tipos, DateOnly? documentRequiredFrom, int maxActivationAttempts)
    {
        var aceptados = TiposAceptadosDe(string.Join(',', tipos));
        if (aceptados.Count == 0)
            throw new ArgumentException("La cooperativa debe aceptar al menos un tipo de documento.", nameof(tipos));
        if (maxActivationAttempts is < IntentosMinimos or > IntentosMaximos)
            throw new ArgumentOutOfRangeException(nameof(maxActivationAttempts));
        AcceptedDocumentTypes = string.Join(',', aceptados.Select(t => t.Codigo));
        DocumentRequiredFrom = documentRequiredFrom;
        MaxActivationAttempts = maxActivationAttempts;
    }
}
