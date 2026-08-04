using IngenIA365ERP.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Services;

public interface IIdentityAuthenticationService
{
    Task<IdentityOpResult<AuthResponse>> LoginAsync(LoginCommand command, string ipAddress, string userAgent);
    Task<IdentityOpResult> LogoutAsync(int userId);
    Task<IdentityOpResult<AuthResponse>> RefreshTokenAsync(RefreshTokenCommand command);
    Task<IdentityOpResult> ChangePasswordAsync(ChangePasswordCommand command);
    Task<IdentityOpResult> ResetPasswordAsync(ResetPasswordCommand command);
}

public record LoginCommand(string Email, string Password, string TenantId);
public record RefreshTokenCommand(string AccessToken, string RefreshToken);
public record ChangePasswordCommand(int UserId, string CurrentPassword, string NewPassword);
public record ResetPasswordCommand(string Email, string TenantId, string NewPassword);

public record IdentityOpResult(bool Succeeded, string? Error = null)
{
    public static IdentityOpResult Success() => new(true);
    public static IdentityOpResult Failure(string error) => new(false, error);
}

public record IdentityOpResult<T>(bool Succeeded, T? Data = default, string? Error = null)
{
    public static IdentityOpResult<T> Success(T data) => new(true, data);
    public static IdentityOpResult<T> Failure(string error) => new(false, default, error);
}

public class IdentityAuthenticationService : IIdentityAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ErpIdentityDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<IdentityAuthenticationService> _logger;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 30;

    public IdentityAuthenticationService(
        UserManager<ApplicationUser> userManager,
        ErpIdentityDbContext dbContext,
        IJwtService jwtService,
        IPermissionService permissionService,
        ILogger<IdentityAuthenticationService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtService = jwtService;
        _permissionService = permissionService;
        _logger = logger;
    }

    public async Task<IdentityOpResult<AuthResponse>> LoginAsync(LoginCommand command, string ipAddress, string userAgent)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.TenantId == command.TenantId);

        if (user is null)
        {
            await RecordLoginAttempt(command.Email, command.TenantId, ipAddress, userAgent, false, "User not found");
            return IdentityOpResult<AuthResponse>.Failure("Credenciales inválidas.");
        }

        // Check lockout
        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            await RecordLoginAttempt(command.Email, command.TenantId, ipAddress, userAgent, false, "Account locked");
            var remainingMinutes = (int)(user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
            return IdentityOpResult<AuthResponse>.Failure($"Cuenta bloqueada. Intente de nuevo en {remainingMinutes} minutos.");
        }

        // Verify password
        var passwordValid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!passwordValid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                _logger.LogWarning("Account locked for user {Email} in tenant {TenantId} after {Attempts} failed attempts",
                    command.Email, command.TenantId, user.FailedLoginAttempts);
            }
            await _userManager.UpdateAsync(user);
            await RecordLoginAttempt(command.Email, command.TenantId, ipAddress, userAgent, false, "Invalid password");
            return IdentityOpResult<AuthResponse>.Failure("Credenciales inválidas.");
        }

        if (!user.IsActive)
        {
            await RecordLoginAttempt(command.Email, command.TenantId, ipAddress, userAgent, false, "Account inactive");
            return IdentityOpResult<AuthResponse>.Failure("Cuenta desactivada. Contacte al administrador.");
        }

        // Successful login
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.LastLoginIp = ipAddress;
        user.LastLoginUserAgent = userAgent;

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsAsync(user.Id);

        var tokens = await _jwtService.GenerateTokensAsync(user, roles, permissions);

        // Store refresh token
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiry = tokens.RefreshTokenExpiry;

        await _userManager.UpdateAsync(user);
        await RecordLoginAttempt(command.Email, command.TenantId, ipAddress, userAgent, true, null);

        _logger.LogInformation("Successful login for user {Email} in tenant {TenantId}", command.Email, command.TenantId);

        return IdentityOpResult<AuthResponse>.Success(new AuthResponse(
            user.PublicId,
            user.FullName,
            user.Email ?? string.Empty,
            user.TenantId,
            user.TenantId, // TenantName — resolved later if needed
            roles,
            permissions,
            tokens));
    }

    public async Task<IdentityOpResult> LogoutAsync(int userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return IdentityOpResult.Failure("Usuario no encontrado.");

        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {UserId} logged out", userId);
        return IdentityOpResult.Success();
    }

    public async Task<IdentityOpResult<AuthResponse>> RefreshTokenAsync(RefreshTokenCommand command)
    {
        var principal = _jwtService.ValidateExpiredToken(command.AccessToken);
        if (principal is null)
            return IdentityOpResult<AuthResponse>.Failure("Token de acceso inválido.");

        var userIdStr = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (userIdStr is null || !int.TryParse(userIdStr, out var userId))
            return IdentityOpResult<AuthResponse>.Failure("Token de acceso inválido.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
            return IdentityOpResult<AuthResponse>.Failure("Usuario no encontrado o inactivo.");

        // Validate stored refresh token
        if (user.RefreshToken != command.RefreshToken || user.RefreshTokenExpiry < DateTime.UtcNow)
            return IdentityOpResult<AuthResponse>.Failure("Refresh token inválido o expirado.");

        var roles = await _userManager.GetRolesAsync(user);
        var permissions = await _permissionService.GetPermissionsAsync(user.Id);

        var tokens = await _jwtService.GenerateTokensAsync(user, roles, permissions);

        // Rotate refresh token
        user.RefreshToken = tokens.RefreshToken;
        user.RefreshTokenExpiry = tokens.RefreshTokenExpiry;
        await _userManager.UpdateAsync(user);

        return IdentityOpResult<AuthResponse>.Success(new AuthResponse(
            user.PublicId,
            user.FullName,
            user.Email ?? string.Empty,
            user.TenantId,
            user.TenantId,
            roles,
            permissions,
            tokens));
    }

    public async Task<IdentityOpResult> ChangePasswordAsync(ChangePasswordCommand command)
    {
        var user = await _userManager.FindByIdAsync(command.UserId.ToString());
        if (user is null)
            return IdentityOpResult.Failure("Usuario no encontrado.");

        var result = await _userManager.ChangePasswordAsync(user, command.CurrentPassword, command.NewPassword);
        if (!result.Succeeded)
            return IdentityOpResult.Failure(string.Join(", ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password changed for user {UserId}", command.UserId);
        return IdentityOpResult.Success();
    }

    public async Task<IdentityOpResult> ResetPasswordAsync(ResetPasswordCommand command)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Email == command.Email && u.TenantId == command.TenantId);

        if (user is null)
            return IdentityOpResult.Failure("Usuario no encontrado.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, command.NewPassword);
        if (!result.Succeeded)
            return IdentityOpResult.Failure(string.Join(", ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password reset for user {Email} in tenant {TenantId}", command.Email, command.TenantId);
        return IdentityOpResult.Success();
    }

    private Task RecordLoginAttempt(string email, string tenantId, string ipAddress, string userAgent, bool success, string? failureReason)
    {
        // Feature 004 (T013): el INSERT crudo anterior referenciaba columnas que
        // ya no existen en SEC_LoginAttempts (TenantId, Success) y no era portable
        // entre motores. Este servicio es el flujo LEGACY pre-identidad-central
        // (/legacy-login); la telemetria de intentos vive ahora en
        // ADM_CentralUserLoginAttempts via el LoginCommandHandler central.
        _logger.LogInformation(
            "Legacy login attempt: {Email} tenant {TenantId} desde {IpAddress} — success={Success} reason={FailureReason} ua={UserAgent}",
            email, tenantId, ipAddress, success, failureReason, userAgent);
        return Task.CompletedTask;
    }
}
