using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// La segregación de Recaudo por <b>persona</b> (feature 015, T0060; US12; R-31; FR-012): quien registra no aprueba lo suyo —diferencias de arqueo,
/// reclasificaciones, fechas valor anteriores, ajustes, anulaciones, clasificación de partidas—. Compara por la persona de cada usuario
/// (<see cref="PersonasDeLosUsuarios"/>), con el <c>SEC_Users.Id</c> que resuelve <c>IActorActual</c> o el aprobador presente identificado por su segundo
/// factor, nunca el entero del token (<c>LaSegregacionNoUsaUserIdDelToken</c>). (nuevo)
/// </summary>
public sealed class SegregacionDeRecaudo(PersonasDeLosUsuarios personas)
{
    /// <summary>¿Son <paramref name="usuarioA"/> y <paramref name="usuarioB"/> la misma persona?</summary>
    public Task<bool> MismaPersonaAsync(int usuarioA, int usuarioB, CancellationToken ct = default) => personas.MismaPersonaAsync(usuarioA, usuarioB, ct);

    /// <summary>¿Es <paramref name="usuario"/> la misma persona que alguno de <paramref name="otros"/>?</summary>
    public Task<bool> AlgunaEsLaMismaAsync(int usuario, IEnumerable<int> otros, CancellationToken ct = default) => personas.AlgunaEsLaMismaAsync(usuario, otros, ct);

    /// <summary>
    /// Que <paramref name="aprueba"/> no sea la misma persona que <paramref name="registro"/>; si lo es, <paramref name="siEsLaMisma"/> (el código de la
    /// historia: <c>Receipts.Count.SelfApproval</c>, <c>Receipts.Voiding.SelfApproval</c>…).
    /// </summary>
    public async Task<Result> OtraPersonaAsync(int registro, int aprueba, Error siEsLaMisma, CancellationToken ct = default) =>
        await personas.MismaPersonaAsync(registro, aprueba, ct) ? Result.Failure(siEsLaMisma) : Result.Success();

    /// <summary>La persona (<c>COR_People.Id</c>) de un usuario, o nula si no la tiene.</summary>
    public Task<int?> PersonaDeAsync(int usuario, CancellationToken ct = default) => personas.PersonaDeAsync(usuario, ct);
}
