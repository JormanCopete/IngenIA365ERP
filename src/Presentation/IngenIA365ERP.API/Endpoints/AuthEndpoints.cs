using Carter;
using IngenIA365ERP.Identity.Services;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

public class AuthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login")
            .WithDescription("Autenticación de usuario");

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithDescription("Cerrar sesión y revocar refresh token");

        group.MapPost("/refresh", RefreshTokenAsync)
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithDescription("Renovar tokens usando refresh token");

        group.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .WithName("ChangePassword")
            .WithDescription("Cambiar contraseña del usuario actual");

        group.MapGet("/me", (Delegate)GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithDescription("Obtener datos del usuario autenticado");
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        IIdentityAuthenticationService authService,
        HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var command = new LoginCommand(request.Email, request.Password, request.TenantId);
        var result = await authService.LoginAsync(command, ipAddress, userAgent);

        if (!result.Succeeded)
            return Results.Unauthorized();

        return Results.Ok(result.Data);
    }

    private static async Task<IResult> LogoutAsync(
        IIdentityAuthenticationService authService,
        HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
            return Results.Unauthorized();

        var result = await authService.LogoutAsync(userId);
        return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Error);
    }

    private static async Task<IResult> RefreshTokenAsync(
        [FromBody] RefreshRequest request,
        IIdentityAuthenticationService authService)
    {
        var command = new RefreshTokenCommand(request.AccessToken, request.RefreshToken);
        var result = await authService.RefreshTokenAsync(command);

        if (!result.Succeeded)
            return Results.Unauthorized();

        return Results.Ok(result.Data);
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        IIdentityAuthenticationService authService,
        HttpContext context)
    {
        var userIdClaim = context.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
            return Results.Unauthorized();

        var command = new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword);
        var result = await authService.ChangePasswordAsync(command);

        return result.Succeeded ? Results.Ok() : Results.BadRequest(result.Error);
    }

    private static Task<IResult> GetCurrentUserAsync(HttpContext context)
    {
        var claims = context.User;
        var response = new
        {
            UserId = claims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value,
            PublicId = claims.FindFirst("publicId")?.Value,
            Email = claims.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value,
            FullName = claims.FindFirst("fullName")?.Value,
            TenantId = claims.FindFirst("tenantId")?.Value,
            Roles = claims.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value),
            Permissions = claims.FindAll("permission").Select(c => c.Value)
        };

        return Task.FromResult(Results.Ok(response));
    }
}

// === Request DTOs ===
public record LoginRequest(string Email, string Password, string TenantId);
public record RefreshRequest(string AccessToken, string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
