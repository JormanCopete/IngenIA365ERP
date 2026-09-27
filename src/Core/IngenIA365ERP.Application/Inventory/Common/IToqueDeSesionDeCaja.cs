namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// El toque de la sesión de caja antes del cerrojo (feature 012, I3, T606; data-model §15 «Concurrencia»):
/// <c>UPDATE INV_CashSessions SET LastActivityAt = @ahora WHERE Id = @id AND Status = 1</c>. Si afecta una fila la sesión seguía
/// abierta y queda bloqueada para la fila de esta transacción: un cierre concurrente espera o ve la actividad; si no afecta ninguna,
/// la sesión se cerró mientras tanto y el cobro responde <c>Inventory.CashSession.NotOpen</c>. Lo usan el cobro del POS y el cierre de
/// la sesión (T618). La implementación vive en <c>Persistence/Inventory/ToqueDeSesionDeCaja</c> (<c>ExecuteUpdateAsync</c>, portable).
/// (nuevo)
/// </summary>
public interface IToqueDeSesionDeCaja
{
    /// <summary>Toca la sesión si sigue abierta; <c>true</c> si afectó exactamente una fila.</summary>
    Task<bool> TocarAsync(int cashSessionId, DateTime ahoraUtc, CancellationToken ct = default);
}
