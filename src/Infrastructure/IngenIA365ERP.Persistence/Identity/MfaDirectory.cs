using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Identity;

/// <summary>
/// Implementación de <see cref="IMfaDirectory"/> sobre <c>ADM_MfaCredentials</c>.
///
/// <para>
/// Escribe la auditoría a mano porque <c>AdminDbContext</c> no enchufa los
/// <c>ISaveChangesInterceptor</c> —sólo lo hace <c>ApplicationDbContext</c>—, así
/// que aquí no hay nada que rellene <c>CreatedAt</c>/<c>CreatedBy</c> ni convierta
/// un <c>Remove()</c> en baja lógica. Por eso también se revoca llamando a
/// <c>Revocar</c> de la entidad y nunca con <c>Remove</c>: un <c>Remove</c> aquí
/// borraría de verdad el segundo factor de alguien.
/// </para>
///
/// <para>
/// Cada operación de escritura es UN solo <c>SaveChangesAsync</c>. Los dos
/// proveedores llevan <c>EnableRetryOnFailure</c>, y con reintentos activos una
/// transacción explícita exige la estrategia de ejecución; manteniendo todo en un
/// guardado no hace falta.
/// </para>
/// </summary>
public sealed class MfaDirectory : IMfaDirectory
{
    private readonly AdminDbContext _db;
    private readonly ICurrentUserService? _usuarioActual;

    public MfaDirectory(AdminDbContext db, ICurrentUserService? usuarioActual = null)
    {
        _db = db;
        _usuarioActual = usuarioActual;
    }

    /// <summary>
    /// Quién queda firmando la fila. Cae a un centinela explícito y no a null: una
    /// credencial de segundo factor sin autor en la auditoría es justo lo que no
    /// se puede permitir cuando haya que reconstruir qué pasó.
    /// </summary>
    private string Actor => _usuarioActual?.UserName ?? "sistema";

    public async Task<string?> ObtenerCifradoTotpActivoAsync(Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .OfType<TotpCredential>()
            .Where(c => c.CentralUserId == centralUserId)
            .Select(c => c.SecretProtected)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TieneTotpActivoAsync(Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .OfType<TotpCredential>()
            .AnyAsync(c => c.CentralUserId == centralUserId, ct);

    public async Task<Guid> ReemplazarTotpAsync(
        Guid centralUserId,
        string secretoProtegido,
        string? label,
        DateTime utcNow,
        CancellationToken ct)
    {
        var existente = await _db.MfaCredentials
            .OfType<TotpCredential>()
            .FirstOrDefaultAsync(c => c.CentralUserId == centralUserId, ct);

        if (existente is not null)
        {
            // En sitio: un UPDATE, sin pasar por un estado intermedio donde la
            // persona se quedaría sin segundo factor si algo fallara en medio.
            existente.RotarSecreto(secretoProtegido, label, utcNow, Actor);
            await _db.SaveChangesAsync(ct);
            return existente.PublicId;
        }

        var nueva = TotpCredential.Inscribir(centralUserId, secretoProtegido, label, utcNow, Actor);
        _db.MfaCredentials.Add(nueva);
        await _db.SaveChangesAsync(ct);
        return nueva.PublicId;
    }

    public async Task<int> RevocarTodasAsync(Guid centralUserId, DateTime utcNow, CancellationToken ct)
    {
        var activas = await _db.MfaCredentials
            .Where(c => c.CentralUserId == centralUserId)
            .ToListAsync(ct);

        if (activas.Count == 0) return 0;

        foreach (var credencial in activas)
        {
            credencial.Revocar(utcNow, Actor);
        }

        await _db.SaveChangesAsync(ct);
        return activas.Count;
    }
}
