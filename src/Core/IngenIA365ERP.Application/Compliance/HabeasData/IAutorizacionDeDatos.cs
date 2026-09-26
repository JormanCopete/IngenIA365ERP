using FluentValidation;

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
    /// <c>Revoked</c> se leen como sin autorización.
    /// </summary>
    Task<AutorizacionVigente?> VigenteAsync(Guid personPublicId, CancellationToken ct);
}

/// <summary>La última decisión del titular (nuevo).</summary>
public sealed record AutorizacionVigente(
    string Decision,
    bool Autoriza,
    Guid PolicyVersionPublicId,
    int PolicyVersionNumber,
    DateTime DecididaEl,
    string? Channel);

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
