using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.DisableMfa;

/// <summary>
/// T079c — Desactiva MFA del usuario. Requiere re-validar el password (defensa
/// contra session hijacking). Rechaza si ALGUNA empresa del usuario tiene
/// política <c>IsRequired=true</c>: el usuario no puede salirse del MFA si una
/// de sus cooperativas lo exige (FR-003c reverso).
/// </summary>
public sealed record DisableMfaCommand(string CurrentPassword)
    : IRequest<Result>;
