using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData.Common;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
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
    IAuditService auditoria,
    IngenIA365ERP.Application.Common.Alerts.IAlertas? alertas = null) : IAutorizacionDeDatos
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
                .Where(p => p.TenantId == cooperativa && p.Purpose == DataAuthorizationPurpose.GeneralTreatment && p.EffectiveTo == null)
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

    /// <summary>
    /// La constancia «sin política vigente» de un alta ya guardada (evento explícito de auditoría) y, desde I3 (T616, T46), la alerta
    /// <c>Personas.SinPoliticaDeDatos</c> a quien registra consentimientos (<c>Compliance.HabeasData.RecordConsent</c>): una por alta;
    /// mientras siga pendiente, las siguientes suman ocurrencias. Vale para toda alta con autorización —POS, Compras y los compuestos
    /// <c>with-person</c>—, porque todas pasan por aquí.
    /// </summary>
    public async Task DejarConstanciaSinPoliticaAsync(Person persona, AutorizacionResuelta autorizacion, CancellationToken ct)
    {
        await auditoria.LogAsync(AuditEventTypes.PersonDataAuthorizationNoCurrentPolicy, "Person", persona.PublicId.ToString(), null,
            new { constancia = SinPoliticaVigente, decision = autorizacion.Decision.ToString(), channel = autorizacion.Channel }, ct);
        if (alertas is null) return;
        await alertas.LevantarAsync(new IngenIA365ERP.Application.Common.Alerts.AlertaALevantar(
            IngenIA365ERP.Application.Common.Alerts.TiposDeAlerta.SinPoliticaDeDatos,
            "Alta de persona sin política de tratamiento de datos",
            "Se dio de alta una persona sin política de tratamiento de datos publicada: publíquela en Cumplimiento › Habeas Data y registre la autorización del titular.",
            "Person", persona.PublicId,
            DedupKey: IngenIA365ERP.Application.Common.Alerts.TiposDeAlerta.SinPoliticaDeDatos,
            RecipientPermissions: [PermisoDeConsentimiento]), ct);
    }

    /// <summary>Quien recibe la alerta de alta sin política (T46).</summary>
    public const string PermisoDeConsentimiento = "Compliance.HabeasData.RecordConsent";

    public Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, CancellationToken ct) =>
        VigenteAsync(personPublicId, DataAuthorizationPurpose.GeneralTreatment, null, ct);

    public async Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, DataAuthorizationPurpose finalidad, string? canal, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa)) return null;
        var personaId = await db.People.AsNoTracking()
            .Where(p => p.PublicId == personPublicId)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (personaId is null) return null;

        DataAuthorizationChannel? deCanal = null;
        if (finalidad == DataAuthorizationPurpose.ContactChannel && !string.IsNullOrWhiteSpace(canal))
        {
            if (!Enum.TryParse<DataAuthorizationChannel>(canal.Trim(), ignoreCase: true, out var leido) || !Enum.IsDefined(leido)) return null;
            deCanal = leido;
        }

        var consulta = db.HabeasDataConsents.AsNoTracking()
            .Where(c => c.TenantId == cooperativa && c.PersonId == personaId && c.Purpose == finalidad);
        if (deCanal is { } elCanal) consulta = consulta.Where(c => c.ContactChannel == elCanal);

        var ultima = await consulta
            .OrderByDescending(c => c.ActionAt).ThenByDescending(c => c.Id)
            .Select(c => new
            {
                c.Action, c.ActionAt, c.Channel, c.Purpose, c.ContactChannel, c.Origin, c.EvidenceAttachmentPublicId,
                Version = c.PolicyVersion!.PublicId, Numero = c.PolicyVersion.VersionNumber,
            })
            .FirstOrDefaultAsync(ct);
        return ultima is null
            ? null
            : new AutorizacionVigente(ultima.Action, ultima.Action == AccionesDeConsentimiento.Accepted, ultima.Version, ultima.Numero, ultima.ActionAt, ultima.Channel)
            {
                Purpose = ultima.Purpose,
                ContactChannel = ultima.ContactChannel,
                Origin = ultima.Origin,
                HasEvidence = ultima.EvidenceAttachmentPublicId is not null || ultima.Origin != DataAuthorizationOrigin.Migration,
            };
    }

    /// <summary>Feature 013 (T0086): sin finalidad vigente para registrar la decisión.</summary>
    public static Error SinPoliticaDeLaFinalidad(DataAuthorizationPurpose finalidad) => new ErrorConDatos(HabeasDataErrorCodes.NoCurrentPolicy,
        $"La cooperativa no tiene una política de tratamiento de datos vigente para la finalidad {finalidad}: publíquela antes de registrar la autorización.",
        new { purpose = finalidad.ToString() });

    /// <summary>Feature 013 (T0086): el canal es obligatorio con la finalidad <c>ContactChannel</c> y no va con otra.</summary>
    public static readonly Error CanalDeContactoRequerido = new(Error.Validation.Code,
        "La autorización de un canal de contacto dice cuál canal (Sms, WhatsApp, Email, Call o Visit); las demás finalidades no llevan canal.");

    public async Task<Result<Guid>> AgregarAsync(AutorizacionAAgregar autorizacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(autorizacion);
        if (!int.TryParse(usuario.TenantId, out var cooperativa))
            return Result.Failure<Guid>("Auth.TenantRequired", "El usuario actual no está asociado a una cooperativa.");
        if ((autorizacion.Purpose == DataAuthorizationPurpose.ContactChannel) != (autorizacion.ContactChannel is not null))
            return Result.Failure<Guid>(CanalDeContactoRequerido);

        var personaId = db.People.Local.FirstOrDefault(p => p.PublicId == autorizacion.PersonPublicId)?.Id is { } local and > 0
            ? local
            : await db.People.AsNoTracking().Where(p => p.PublicId == autorizacion.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (personaId is not { } persona)
            return Result.Failure<Guid>(new Error("Core.Person.NotFound", "La persona no existe en esta cooperativa."));

        var version = await VersionDePoliticaAsync(cooperativa, autorizacion.Purpose, autorizacion.PolicyVersionPublicId, ct);
        if (version.IsFailure) return Result.Failure<Guid>(version.Error);

        var actor = usuario.UserName ?? "SYSTEM";
        var consentimiento = new HabeasDataConsent
        {
            TenantId = cooperativa,
            PersonId = persona,
            PolicyVersionId = version.Value,
            Action = autorizacion.Decision == DecisionDeAutorizacion.Declined ? AccionesDeConsentimiento.Declined : AccionesDeConsentimiento.Accepted,
            ActionAt = autorizacion.DecidedAt,
            ActionBy = actor,
            Channel = CanalPorDefecto,
            Notes = autorizacion.Notes,
            Purpose = autorizacion.Purpose,
            ContactChannel = autorizacion.ContactChannel,
            EvidenceAttachmentPublicId = autorizacion.EvidenceAttachmentPublicId,
            Origin = autorizacion.Origin,
            CreatedBy = actor,
            UpdatedBy = actor,
        };
        db.HabeasDataConsents.Add(consentimiento);
        return Result.Success(consentimiento.PublicId);
    }

    /// <summary>
    /// Feature 013 (T69 b, T0088): la fila <c>Revoked</c> de una finalidad (y canal) que pidió el titular, sin guardar; la guarda
    /// <c>RespondDataSubjectRequestCommand</c> en el mismo <c>SaveChanges</c> que la respuesta (data-model parte 3 §4.9). Toma la
    /// versión de la última decisión de esa finalidad o, si nunca decidió, la vigente de la finalidad; sin ninguna no hay qué
    /// revocar y devuelve nulo.
    /// </summary>
    public async Task<HabeasDataConsent?> AgregarRevocacionAsync(
        int personaId, DataAuthorizationPurpose finalidad, DataAuthorizationChannel? canal, DateTime revocadaEl, string notas, CancellationToken ct)
    {
        if (!int.TryParse(usuario.TenantId, out var cooperativa)) return null;
        var ultima = await db.HabeasDataConsents.AsNoTracking()
            .Where(c => c.TenantId == cooperativa && c.PersonId == personaId && c.Purpose == finalidad && (canal == null || c.ContactChannel == canal))
            .OrderByDescending(c => c.ActionAt).ThenByDescending(c => c.Id)
            .Select(c => (int?)c.PolicyVersionId)
            .FirstOrDefaultAsync(ct);
        var idDeVersion = ultima;
        if (idDeVersion is null)
        {
            var vigente = await VersionDePoliticaAsync(cooperativa, finalidad, null, ct);
            if (vigente.IsFailure) return null;
            idDeVersion = vigente.Value;
        }

        var actor = usuario.UserName ?? "SYSTEM";
        var revocacion = new HabeasDataConsent
        {
            TenantId = cooperativa,
            PersonId = personaId,
            PolicyVersionId = idDeVersion.Value,
            Action = AccionesDeConsentimiento.Revoked,
            ActionAt = revocadaEl,
            ActionBy = actor,
            Channel = CanalPorDefecto,
            Notes = notas.Length > 2000 ? notas[..2000] : notas,
            Purpose = finalidad,
            ContactChannel = canal,
            Origin = DataAuthorizationOrigin.TitularRequest,
            CreatedBy = actor,
            UpdatedBy = actor,
        };
        db.HabeasDataConsents.Add(revocacion);
        return revocacion;
    }

    /// <summary>La versión de política (Id) indicada, de esta cooperativa y de la finalidad, o la vigente de la finalidad.</summary>
    private async Task<Result<int>> VersionDePoliticaAsync(int cooperativa, DataAuthorizationPurpose finalidad, Guid? versionPublicId, CancellationToken ct)
    {
        if (versionPublicId is { } indicada && indicada != Guid.Empty)
        {
            var id = await db.HabeasDataPolicyVersions.AsNoTracking()
                .Where(p => p.TenantId == cooperativa && p.PublicId == indicada && p.Purpose == finalidad)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);
            return id is { } encontrada
                ? Result.Success(encontrada)
                : Result.Failure<int>(new Error(PoliticaDesconocida, "La versión de la política de tratamiento de datos no existe en esta cooperativa para esa finalidad."));
        }

        var vigente = await db.HabeasDataPolicyVersions.AsNoTracking()
            .Where(p => p.TenantId == cooperativa && p.Purpose == finalidad && p.EffectiveTo == null)
            .OrderByDescending(p => p.VersionNumber)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        return vigente is { } v ? Result.Success(v) : Result.Failure<int>(SinPoliticaDeLaFinalidad(finalidad));
    }
}

/// <summary>La autorización validada, lista para escribir: con versión (consentimiento) o sin ella (constancia). (nuevo)</summary>
public sealed record AutorizacionResuelta(DecisionDeAutorizacion Decision, int? PolicyVersionId, int TenantId, string Channel)
{
    public bool SinPolitica => PolicyVersionId is null;
}
