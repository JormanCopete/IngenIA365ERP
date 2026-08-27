using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.Credenciales;

/// <summary>
/// Las credenciales de segundo factor de quien llama. Sin parámetros: siempre son
/// las suyas, salidas del token. Nadie lista las de otro por esta vía.
/// </summary>
public sealed record ListMfaCredentialsQuery : IRequest<Result<ListMfaCredentialsResult>>;

/// <param name="Credenciales">Activas, en orden estable.</param>
/// <param name="CodigosDeRecuperacionRestantes">
/// Cuántos códigos de un solo uso le quedan. Va aquí y no en otra pantalla porque
/// es la misma decisión: cuando alguien mira qué autenticadores tiene, lo que en
/// realidad está evaluando es si podría volver a entrar si perdiera el teléfono.
/// </param>
public sealed record ListMfaCredentialsResult(
    IReadOnlyList<CredencialMfaResumen> Credenciales,
    int CodigosDeRecuperacionRestantes);

public sealed class ListMfaCredentialsQueryHandler(
    ICurrentCentralUserContext currentUser,
    IMfaDirectory credenciales,
    ICentralIdentityProvider centralIdentity)
    : IRequestHandler<ListMfaCredentialsQuery, Result<ListMfaCredentialsResult>>
{
    public async Task<Result<ListMfaCredentialsResult>> Handle(
        ListMfaCredentialsQuery request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
        {
            return Result.Failure<ListMfaCredentialsResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        }

        var centralUserId = currentUser.CentralUserId.Value;

        var lista = await credenciales.ListarActivasAsync(centralUserId, ct);
        var restantes = await centralIdentity.CountRecoveryCodesAsync(centralUserId, ct);

        return Result.Success(new ListMfaCredentialsResult(lista, restantes));
    }
}
