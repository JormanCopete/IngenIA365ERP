using System.IdentityModel.Tokens.Jwt;
using IngenIA365ERP.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Policies;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(JwtRegisteredClaimNames.Sub);
        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogWarning("Permission check failed: no valid user ID in claims");
            return;
        }

        // Check in-token permissions first (fast path)
        var tokenPermissions = context.User.FindAll("permission").Select(c => c.Value);
        if (tokenPermissions.Contains(requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        // Fall back to database/cache check
        if (await _permissionService.HasPermissionAsync(userId, requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        _logger.LogWarning("Permission denied: user {UserId} lacks {Permission}", userId, requirement.PermissionCode);
    }
}
