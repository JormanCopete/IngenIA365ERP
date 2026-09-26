using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.HabeasData;

/// <summary>
/// La autorización de datos al crear una persona y su lectura (feature 012, T46, FR-011; T175). Escribe en el registro de
/// consentimientos que ya existe (<c>CMP_HabeasDataConsents</c>): <c>Accepted</c> o <c>Declined</c> sobre la versión de
/// política que se mostró al titular. Al cajero no se le exige <c>Compliance.HabeasData.RecordConsent</c>: lo autoriza el
/// permiso de crear la persona. Lo usa <c>AltaConAutorizacion</c>, el único que da de alta con autorización. (nuevo)
/// </summary>
public sealed class AutorizacionDeDatos(
    IApplicationDbContext db,
    ICurrentUserService usuario,
    IDateTimeService reloj,
    IAuditService auditoria) : IAutorizacionDeDatos
{
    /// <summary>La constancia del alta sin política publicada.</summary>
    public const string SinPoliticaVigente = "sin política vigente";

    public const string PoliticaDesconocida = "Person.DataAuthorization.PolicyUnknown";
    public const string PoliticaRequerida = "Person.DataAuthorization.PolicyRequired";

    /// <summary>Canal cuando la pantalla no lo dice.</summary>
    public const string CanalPorDefecto = "InPerson";

    /// <summary>
    /// Resuelve la autorización antes de escribir nada: la versión de política (de esta cooperativa) o la constancia
    /// «sin política vigente». Nulo si no vino autorización. Falla —y entonces no se crea la persona— si la versión no
    /// existe en la cooperativa o si, habiendo política vigente, no se dice cuál se mostró.
    /// </summary>
    public async Task<Result<AutorizacionResuelta?>> ResolverAsync(AutorizacionAlCrear? autorizacion, CancellationToken ct)
    {
        if (autorizacion is null) return Result.Success<AutorizacionResuelta?>(null);
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<AutorizacionResuelta?>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");

        var canal = string.IsNullOrWhiteSpace(autorizacion.Channel) ? CanalPorDefecto : autorizacion.Channel.Trim();

        if (autorizacion.PolicyVersionPublicId is not { } versionMostrada || versionMostrada == Guid.Empty)
        {
            var vigente = await db.HabeasDataPolicyVersions.AsNoTracking()
                .Where(p => p.TenantId == cooperativa && p.EffectiveTo == null)
                .Select(p => (int?)p.VersionNumber)
                .FirstOrDefaultAsync(ct);
            if (vigente is { } numero)
                return Result.Failure<AutorizacionResuelta?>(PoliticaRequerida,
                    $"La cooperativa tiene publicada la política de tratamiento de datos (versión {numero}): muéstrela al titular y registre su decisión sobre esa versión.");
            return Result.Success<AutorizacionResuelta?>(new AutorizacionResuelta(autorizacion.Decision, null, cooperativa, canal));
        }

        var version = await db.HabeasDataPolicyVersions.AsNoTracking()
            .Where(p => p.TenantId == cooperativa && p.PublicId == versionMostrada)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        return version is { } id
            ? Result.Success<AutorizacionResuelta?>(new AutorizacionResuelta(autorizacion.Decision, id, cooperativa, canal))
            : Result.Failure<AutorizacionResuelta?>(PoliticaDesconocida, "La versión de la política de tratamiento de datos no existe en esta cooperativa.");
    }

    /// <summary>
    /// Agrega (sin guardar) el consentimiento de una persona ya guardada. El que llama lo guarda dentro de la misma
    /// transacción que la persona. Sólo cuando hay versión de política.
    /// </summary>
    public HabeasDataConsent AgregarConsentimiento(Person persona, AutorizacionResuelta autorizacion)
    {
        if (autorizacion.PolicyVersionId is not { } version)
            throw new InvalidOperationException("Sin versión de política no hay consentimiento que escribir: se deja la constancia.");
        var actor = usuario.UserName ?? "SYSTEM";
        var consentimiento = new HabeasDataConsent
        {
            TenantId = autorizacion.TenantId,
            PersonId = persona.Id,
            PolicyVersionId = version,
            Action = autorizacion.Decision == DecisionDeAutorizacion.Declined ? AccionesDeConsentimiento.Declined : AccionesDeConsentimiento.Accepted,
            ActionAt = reloj.UtcNow,
            ActionBy = actor,
            Channel = autorizacion.Channel,
            Notes = "Capturada al crear la persona.",
            CreatedBy = actor,
            UpdatedBy = actor,
        };
        db.HabeasDataConsents.Add(consentimiento);
        return consentimiento;
    }

    /// <summary>La constancia «sin política vigente» de un alta ya guardada (evento explícito de auditoría).</summary>
    public Task DejarConstanciaSinPoliticaAsync(Person persona, AutorizacionResuelta autorizacion, CancellationToken ct) =>
        auditoria.LogAsync(AuditEventTypes.PersonDataAuthorizationNoCurrentPolicy, "Person", persona.PublicId.ToString(), null,
            new { constancia = SinPoliticaVigente, decision = autorizacion.Decision.ToString(), channel = autorizacion.Channel }, ct);

    public async Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa)) return null;
        var personaId = await db.People.AsNoTracking()
            .Where(p => p.PublicId == personPublicId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (personaId is null) return null;

        var ultima = await db.HabeasDataConsents.AsNoTracking()
            .Where(c => c.TenantId == cooperativa && c.PersonId == personaId)
            .OrderByDescending(c => c.ActionAt).ThenByDescending(c => c.Id)
            .Select(c => new { c.Action, c.ActionAt, c.Channel, Version = c.PolicyVersion!.PublicId, Numero = c.PolicyVersion.VersionNumber })
            .FirstOrDefaultAsync(ct);
        return ultima is null
            ? null
            : new AutorizacionVigente(ultima.Action, ultima.Action == AccionesDeConsentimiento.Accepted, ultima.Version, ultima.Numero, ultima.ActionAt, ultima.Channel);
    }
}

/// <summary>La autorización validada, lista para escribir: con versión (consentimiento) o sin ella (constancia). (nuevo)</summary>
public sealed record AutorizacionResuelta(DecisionDeAutorizacion Decision, int? PolicyVersionId, int TenantId, string Channel)
{
    public bool SinPolitica => PolicyVersionId is null;
}
