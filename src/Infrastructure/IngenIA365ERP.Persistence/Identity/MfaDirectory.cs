using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Identity;

/// <summary>
/// Implementación de <see cref="IMfaDirectory"/> sobre <c>ADM_MfaCredentials</c>.
///
/// <para>
/// Escribe la auditoría a mano porque <c>AdminDbContext</c> no enchufa los
/// <c>ISaveChangesInterceptor</c> —sólo lo hace <c>ApplicationDbContext</c>—, así
/// que aquí no hay nada que rellene <c>CreatedAt</c>/<c>CreatedBy</c> ni convierta
/// un <c>Remove()</c> en baja lógica. Por eso se revoca llamando a <c>Revocar</c>
/// de la entidad y nunca con <c>Remove</c>: un <c>Remove</c> aquí borraría de
/// verdad el segundo factor de alguien.
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
    private readonly ILogger<MfaDirectory> _log;

    public MfaDirectory(
        AdminDbContext db,
        ILogger<MfaDirectory> log,
        ICurrentUserService? usuarioActual = null)
    {
        _db = db;
        _log = log;
        _usuarioActual = usuarioActual;
    }

    /// <summary>
    /// Quién queda firmando la fila. Cae a un centinela explícito y no a null: una
    /// credencial de segundo factor sin autor en la auditoría es justo lo que no se
    /// puede permitir el día que haya que reconstruir qué pasó.
    /// </summary>
    private string Actor => _usuarioActual?.UserName ?? "sistema";

    public async Task<IReadOnlyList<CredencialTotpCifrada>> ListarCifradosTotpActivosAsync(
        Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .OfType<TotpCredential>()
            .Where(c => c.CentralUserId == centralUserId)
            // Orden determinista: sin él, dos motores distintos podrían devolver
            // las filas en orden distinto. No cambia el resultado del barrido
            // —se prueban todas— pero sí hace reproducible cuál se marca como
            // usada cuando dos credenciales comparten secreto.
            .OrderBy(c => c.Id)
            .Select(c => new CredencialTotpCifrada(c.PublicId, c.SecretProtected))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CredencialMfaResumen>> ListarActivasAsync(
        Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .Where(c => c.CentralUserId == centralUserId)
            .OrderBy(c => c.Id)
            .Select(c => new CredencialMfaResumen(
                c.PublicId, c.Label, c.CreatedAt, c.ConfirmedAt, c.LastUsedAt))
            .ToListAsync(ct);

    /// <summary>
    /// SIN filtro por tipo, y es deliberado: tiene que emparejar con
    /// <see cref="RevocarTodasAsync"/>, que tampoco lo tiene. Cuando contaba sólo
    /// las TOTP, retirar un passkey teniendo además un TOTP daba cuenta 1, se
    /// disparaba el apagado total, y la revocación se llevaba por delante el TOTP
    /// que la persona no había tocado.
    /// </summary>
    public async Task<int> ContarActivasAsync(Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .CountAsync(c => c.CentralUserId == centralUserId, ct);

    public async Task<bool> HuboAlgunaVezTotpAsync(Guid centralUserId, CancellationToken ct) =>
        await _db.MfaCredentials
            .AsNoTracking()
            .IgnoreQueryFilters()
            .OfType<TotpCredential>()
            .AnyAsync(c => c.CentralUserId == centralUserId, ct);

    public async Task<Guid> InscribirTotpAsync(
        Guid centralUserId,
        string secretoProtegido,
        string? label,
        DateTime utcNow,
        CancellationToken ct)
    {
        var nueva = TotpCredential.Inscribir(centralUserId, secretoProtegido, label, utcNow, Actor);
        _db.MfaCredentials.Add(nueva);
        await _db.SaveChangesAsync(ct);
        return nueva.PublicId;
    }

    public async Task<bool> RenombrarAsync(
        Guid centralUserId, Guid credencialPublicId, string? label, DateTime utcNow, CancellationToken ct)
    {
        var credencial = await BuscarSuyaAsync(centralUserId, credencialPublicId, ct);
        if (credencial is null) return false;

        credencial.Renombrar(label, utcNow, Actor);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RevocarUnaAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime utcNow, CancellationToken ct)
    {
        var credencial = await BuscarSuyaAsync(centralUserId, credencialPublicId, ct);
        if (credencial is null) return false;

        credencial.Revocar(utcNow, Actor);
        await _db.SaveChangesAsync(ct);
        return true;
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

    public async Task MarcarUsoAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime utcNow, CancellationToken ct)
    {
        var credencial = await BuscarSuyaAsync(centralUserId, credencialPublicId, ct);

        // Si desapareció entre la verificación y el sello —la revocó otra sesión
        // mientras ésta iniciaba— no pasa nada: el ingreso ya es correcto y no se
        // puede caer por no haber podido escribir telemetría.
        if (credencial is null)
        {
            _log.LogDebug(
                "No se pudo sellar el uso de {CredencialPublicId} de {CentralUserId}: ya no está activa.",
                credencialPublicId, centralUserId);
            return;
        }

        credencial.MarcarUso(utcNow);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Busca una credencial ACTIVA comprobando que sea de esa persona. El filtro
    /// por dueño no es decorativo: sin él, un PublicId ajeno adivinado dejaría
    /// renombrar o revocar la credencial de otro.
    /// </summary>
    private Task<MfaCredential?> BuscarSuyaAsync(
        Guid centralUserId, Guid credencialPublicId, CancellationToken ct) =>
        _db.MfaCredentials
            .FirstOrDefaultAsync(
                c => c.PublicId == credencialPublicId && c.CentralUserId == centralUserId, ct);
}
