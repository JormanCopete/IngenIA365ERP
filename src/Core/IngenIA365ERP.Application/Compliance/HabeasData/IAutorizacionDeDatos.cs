using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Application.Compliance.HabeasData;

/// <summary>
/// La autorización de tratamiento de datos vigente de una persona (feature 012, T46, FR-011; decisiones-transversales §2.16).
/// La consultan promociones y contacto comercial antes de usar los datos. <b>Sólo por PublicId</b> (Principio VI).
/// </summary>
public interface IAutorizacionDeDatos
{
    /// <summary>
    /// La última decisión registrada del titular en <c>CMP_HabeasDataConsents</c>, o nulo si nunca decidió (o no existe).
    /// <see cref="AutorizacionVigente.Autoriza"/> es verdadero sólo con <c>Accepted</c>: <c>Declined</c> y
    /// <c>Revoked</c> se leen como sin autorización. Feature 013 (T0086): es la finalidad <c>GeneralTreatment</c>, que es lo que
    /// guardaban todas las filas anteriores, así que responde igual que antes.
    /// </summary>
    Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, CancellationToken ct);

    /// <summary>
    /// Feature 013 (T69 a, T0086; contracts/interfaces.md §10.1; data-model parte 3 §4.10): la última decisión del titular para
    /// <paramref name="finalidad"/> y, en <see cref="DataAuthorizationPurpose.ContactChannel"/>, para <paramref name="canal"/> (nulo =
    /// cualquier canal). Nulo si nunca decidió esa finalidad.
    /// </summary>
    Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, DataAuthorizationPurpose finalidad, string? canal, CancellationToken ct);

    /// <summary>
    /// Feature 013 (T0086): agrega la autorización a la unidad de trabajo <b>sin guardar</b>; la guarda quien llama, en su
    /// transacción (la radicación, la activación, el registro de la evidencia). Asíncrono porque resuelve la persona y la versión
    /// de política por su <c>PublicId</c> (Principio VI). Sin versión indicada, la vigente de la finalidad; sin ninguna,
    /// <c>Compliance.HabeasData.NoCurrentPolicy</c>.
    /// </summary>
    Task<Result<Guid>> AgregarAsync(AutorizacionAAgregar autorizacion, CancellationToken ct);
}

/// <summary>La última decisión del titular (nuevo).</summary>
public sealed record AutorizacionVigente(
    string Decision,
    bool Autoriza,
    Guid PolicyVersionPublicId,
    int PolicyVersionNumber,
    DateTime DecididaEl,
    string? Channel)
{
    /// <summary>Feature 013: la finalidad de la decisión.</summary>
    public DataAuthorizationPurpose Purpose { get; init; } = DataAuthorizationPurpose.GeneralTreatment;

    /// <summary>
    /// Feature 013 (FR-006): verdadero si tiene evidencia —adjunto <c>DataAuthorization</c>— o no es migrada (el alta y la radicación
    /// la capturan en su propio acto). Una migrada sin evidencia autoriza el tratamiento y el cobro, no la consulta ni el reporte a
    /// centrales ni los mensajes de cobranza; eso lo decide quien consume.
    /// </summary>
    public bool HasEvidence { get; init; }

    /// <summary>Feature 013: el canal de contacto de la decisión (sólo en <c>ContactChannel</c>).</summary>
    public DataAuthorizationChannel? ContactChannel { get; init; }

    /// <summary>Feature 013: de dónde salió.</summary>
    public DataAuthorizationOrigin Origin { get; init; } = DataAuthorizationOrigin.PersonRegistration;

    /// <summary>
    /// Feature 013 (T0306): el <c>PublicId</c> de la decisión (<c>CMP_HabeasDataConsents</c>), que el mensaje al deudor sella como la
    /// autorización de canal usada (<c>AuthorizationConsentPublicId</c>). Nulo si quien la construye no lo sabe.
    /// </summary>
    public Guid? ConsentPublicId { get; init; }
}

/// <summary>
/// Feature 013 (T0086; contracts/interfaces.md §10.1): una autorización por finalidad que se agrega sin guardar. El origen va como
/// enum (el contrato lo escribe como texto) y <see cref="Notes"/> se suma para dejar la referencia del soporte. (nuevo)
/// </summary>
public sealed record AutorizacionAAgregar(
    Guid PersonPublicId,
    DataAuthorizationPurpose Purpose,
    DecisionDeAutorizacion Decision,
    Guid? PolicyVersionPublicId,
    DataAuthorizationChannel? ContactChannel,
    Guid? EvidenceAttachmentPublicId,
    DataAuthorizationOrigin Origin,
    DateTime DecidedAt,
    string? Notes = null);

/// <summary>Lo que decidió el titular al darse de alta (nuevo). Viaja como <c>decision: Accepted | Declined</c>.</summary>
public enum DecisionDeAutorizacion
{
    Accepted = 1,
    Declined = 2,
}

/// <summary>
/// La autorización capturada al crear una persona (feature 012, T46; <c>contracts/api.md</c> §29): opcional en
/// <c>CreatePersonCommand</c> y en los compuestos <c>with-person</c>, y se escribe junto con la persona —las dos o
/// ninguna—. <see cref="PolicyVersionPublicId"/> es la versión que se le mostró al titular; nula sólo cuando la
/// cooperativa no tiene política publicada (el alta procede con la constancia «sin política vigente»).
/// </summary>
public sealed record AutorizacionAlCrear(DecisionDeAutorizacion Decision, Guid? PolicyVersionPublicId, string? Channel);

/// <summary>Forma de <see cref="AutorizacionAlCrear"/>; lo incluyen el alta y los compuestos. (nuevo)</summary>
public sealed class AutorizacionAlCrearValidator : AbstractValidator<AutorizacionAlCrear>
{
    public AutorizacionAlCrearValidator()
    {
        RuleFor(x => x.Decision).IsInEnum().WithMessage("La decisión del titular es «Accepted» o «Declined».");
        RuleFor(x => x.Channel).MaximumLength(50);
    }
}
