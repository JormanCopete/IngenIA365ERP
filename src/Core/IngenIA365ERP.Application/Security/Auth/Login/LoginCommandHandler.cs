using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Security.Auth.Login;

/// <summary>
/// Verifica credenciales, gestiona lockout y emite el challenge MFA. Nunca
/// revela qué condición falló — todos los errores de credenciales devuelven
/// <c>Auth.InvalidCredentials</c> (FR-005 — defensa contra enumeración).
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginChallengeResult>>
{
    private const int MfaChallengeMinutes = 5;

    private readonly IApplicationDbContext _db;
    private readonly IPasswordPolicyEnforcer _passwords;
    private readonly IMfaChallengeStore _challenges;
    private readonly IDateTimeService _clock;
    private readonly ISender _mediator;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly ITenantDirectory? _tenantDirectory;

    public LoginCommandHandler(
        IApplicationDbContext db,
        IPasswordPolicyEnforcer passwords,
        IMfaChallengeStore challenges,
        IDateTimeService clock,
        ISender mediator,
        ILogger<LoginCommandHandler> logger,
        ITenantDirectory? tenantDirectory = null)
    {
        _db = db;
        _passwords = passwords;
        _challenges = challenges;
        _clock = clock;
        _mediator = mediator;
        _logger = logger;
        _tenantDirectory = tenantDirectory;
    }

    public async Task<Result<LoginChallengeResult>> Handle(LoginCommand request, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        // FR-005: el identificador de login puede ser Username o Email.
        // Aceptamos el mismo valor en ambas columnas — buena UX, mantiene
        // el contrato genérico de "loginId".
        var loginId = request.Username;
        var user = await _db.Users
            .Include(u => u.Roles)
            .IgnoreQueryFilters() // necesitamos inspeccionar soft-deleted para no exponer estado
            .FirstOrDefaultAsync(u => u.Username == loginId || u.Email == loginId, ct);

        await RecordAttempt(user?.Id, request.Username, request.IpAddress, request.UserAgent, ct);

        if (user is null || user.IsDeleted)
        {
            return Result.Failure<LoginChallengeResult>(
                "Auth.InvalidCredentials",
                "Usuario o contraseña inválidos.");
        }

        if (!user.IsActive)
        {
            return Result.Failure<LoginChallengeResult>(
                "Auth.AccountDisabled",
                "La cuenta está deshabilitada. Contacta al administrador.");
        }

        if (user.LockoutEndAt is not null && user.LockoutEndAt > now)
        {
            return Result.Failure<LoginChallengeResult>(
                "Auth.AccountLocked",
                $"Cuenta bloqueada hasta las {user.LockoutEndAt:HH:mm} UTC.");
        }

        var passwordOk = _passwords.Verify(request.Password, user.PasswordHash);
        if (!passwordOk)
        {
            await ApplyLockoutOnFailureAsync(user, now, ct);
            return Result.Failure<LoginChallengeResult>(
                "Auth.InvalidCredentials",
                "Usuario o contraseña inválidos.");
        }

        // Credenciales OK → resetea contador, emite challenge MFA.
        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;

        var tenant = await ResolveTenantAsync(request.TenantSubdomainOrNit, ct);
        var (tenantId, tenantPublicId, tenantIdentifier) = tenant;

        var challengeCtx = new MfaChallengeContext(
            user.Id, user.PublicId, user.Username,
            tenantId, tenantPublicId, tenantIdentifier, now);

        var token = await _challenges.IssueAsync(
            challengeCtx, TimeSpan.FromMinutes(MfaChallengeMinutes), ct);

        await _db.SaveChangesAsync(ct);

        return Result.Success(new LoginChallengeResult(token, user.MustChangePassword));
    }

    private async Task ApplyLockoutOnFailureAsync(User user, DateTime now, CancellationToken ct)
    {
        user.FailedLoginAttempts++;
        var policy = await _db.PasswordPolicies.AsNoTracking()
            .Where(p => p.TenantId == null)
            .FirstOrDefaultAsync(ct);
        var threshold = policy?.LockoutThreshold ?? 5;
        var lockoutMin = policy?.LockoutMinutes ?? 15;

        if (user.FailedLoginAttempts >= threshold)
        {
            user.LockoutEndAt = now.AddMinutes(lockoutMin);
            await _mediator.Send(new SendNotificationCommand(new NotificationPayload(
                RecipientUserPublicId: user.PublicId,
                Type: NotificationType.AccountLocked,
                Subject: "Cuenta bloqueada temporalmente",
                Body: $"Tu cuenta fue bloqueada por superar {threshold} intentos fallidos. " +
                      $"El bloqueo termina a las {user.LockoutEndAt:HH:mm} UTC.",
                Channels: NotificationChannels.InApp | NotificationChannels.Email)), ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RecordAttempt(int? _, string usernameTried, string? ip, string? ua, CancellationToken ct)
    {
        // El registro forense del intento NO debe bloquear el flujo de login.
        // Lo persistimos en su propio SaveChanges atómico y, si la tabla está
        // desfasada (Invalid column name 207, 208), logueamos y seguimos —
        // el lockout posterior usa solo la entidad User, que sí sabemos consistente.
        var attempt = new LoginAttempt
        {
            Email = usernameTried,
            IpAddress = ip,
            UserAgent = ua,
            AttemptedAt = _clock.UtcNow,
            WasSuccessful = false
        };
        _db.LoginAttempts.Add(attempt);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsSchemaMismatch(ex))
        {
            _logger.LogError(ex,
                "SEC_LoginAttempts está desfasada. Ejecuta " +
                "database/migration/20_SEC_LoginAttempts_BackfillColumns.sql. " +
                "El intento de login no se registró, pero el flujo continúa.");
            // Detach para que el siguiente SaveChanges no reintente esta entidad
            // y vuelva a chocar contra el mismo SqlException.
            (_db as Microsoft.EntityFrameworkCore.DbContext)?
                .Entry(attempt).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Detecta los SqlException 207 (Invalid column name) y 208 (Invalid object name)
    /// sin acoplar Application a Microsoft.Data.SqlClient — se inspecciona el
    /// nombre del tipo y el mensaje. Permite degradación segura cuando la BD
    /// está desfasada respecto a la entidad.
    /// </summary>
    private static bool IsSchemaMismatch(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is null) return false;
        var typeName = inner.GetType().FullName ?? string.Empty;
        if (!typeName.Equals("Microsoft.Data.SqlClient.SqlException", StringComparison.Ordinal))
        {
            return false;
        }
        var msg = inner.Message ?? string.Empty;
        return msg.Contains("Invalid column name", StringComparison.Ordinal)
            || msg.Contains("Invalid object name", StringComparison.Ordinal);
    }

    private async Task<(int? TenantId, Guid TenantPublicId, string Identifier)> ResolveTenantAsync(
        string? subdomainOrNit, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subdomainOrNit))
        {
            return (null, Guid.Empty, string.Empty);
        }

        // ADM_Tenants vive en la BD IngenIA365ERP_Admin (cadena TenantConnection),
        // NO en la operacional. Usamos ITenantDirectory para consultarla.
        // Si el directorio no está registrado (tests, escenarios mínimos) o
        // falla, el login sigue su flujo con tenant vacío — la resolución del
        // tenant_id en el JWT se aplaza a US2.
        if (_tenantDirectory is not null)
        {
            try
            {
                var entry = await _tenantDirectory.FindBySubdomainOrNitAsync(subdomainOrNit, ct);
                if (entry is not null)
                {
                    return (entry.Id, entry.PublicId, entry.Identifier);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Tenant directory lookup falló para '{Subdomain}'. " +
                    "Login continúa sin tenant_id resuelto.", subdomainOrNit);
            }
        }

        return (null, Guid.Empty, subdomainOrNit);
    }
}
