using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Identity.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string permissionCode);
    Task<IList<string>> GetPermissionsAsync(int userId);
    Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes);
    Task InvalidateCacheAsync(int userId);
}

public class PermissionService : IPermissionService
{
    private readonly ErpIdentityDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<PermissionService> _logger;

    private const string CacheKeyPrefix = "permissions";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public PermissionService(
        ErpIdentityDbContext dbContext,
        ICacheService cacheService,
        ICurrentTenantService tenantService,
        ILogger<PermissionService> logger)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
    {
        var permissions = await GetPermissionsAsync(userId);
        return permissions.Contains(permissionCode);
    }

    public async Task<IList<string>> GetPermissionsAsync(int userId)
    {
        var tenantId = _tenantService.TenantId ?? "default";
        var cacheKey = $"{CacheKeyPrefix}:{tenantId}:{userId}";

        var cached = await _cacheService.GetAsync<List<string>>(cacheKey);
        if (cached is not null)
            return cached;

        var permissions = await LoadPermissionsFromDbAsync(userId);

        await _cacheService.SetAsync(cacheKey, permissions, CacheExpiration);

        return permissions;
    }

    public async Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes)
    {
        var permissions = await GetPermissionsAsync(userId);
        return permissionCodes.Any(code => permissions.Contains(code));
    }

    public async Task InvalidateCacheAsync(int userId)
    {
        var tenantId = _tenantService.TenantId ?? "default";
        var cacheKey = $"{CacheKeyPrefix}:{tenantId}:{userId}";
        await _cacheService.RemoveAsync(cacheKey);
        _logger.LogInformation("Permission cache invalidated for user {UserId} in tenant {TenantId}", userId, tenantId);
    }

    private async Task<List<string>> LoadPermissionsFromDbAsync(int userId)
    {
        // Get all role IDs for the user via Identity's UserRoles table
        var roleIds = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (roleIds.Count == 0)
            return [];

        // Get all permission codes for those roles
        var permissions = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct()
            .ToListAsync();

        return permissions;
    }
}
