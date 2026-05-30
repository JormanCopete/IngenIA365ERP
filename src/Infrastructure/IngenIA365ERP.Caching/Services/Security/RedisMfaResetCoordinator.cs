using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisMfaResetCoordinator(IConnectionMultiplexer redis) : IMfaResetCoordinator
{
    private const string Prefix = "mfa:reset:";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);
    private static string Key(Guid id) => Prefix + id.ToString("N");

    public async Task<Guid> CreateRequestAsync(int requesterUserId, int targetUserId, string tenantId, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var state = new MfaResetRequestState(
            id, requesterUserId, targetUserId, tenantId,
            now, now.Add(Ttl),
            Approvers: [],
            IsExecuted: false);
        var db = redis.GetDatabase();
        await db.StringSetAsync(Key(id), JsonSerializer.Serialize(state), Ttl);
        return id;
    }

    public async Task<int> ApproveAsync(Guid requestPublicId, int approverUserId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(requestPublicId));
        if (raw.IsNullOrEmpty) return 0;

        var state = JsonSerializer.Deserialize<MfaResetRequestState>((string)raw!)!;

        // No auto-aprobación; no doble aprobación del mismo admin.
        if (approverUserId == state.RequesterUserId) return state.Approvers.Count;
        if (state.Approvers.Contains(approverUserId)) return state.Approvers.Count;

        var updated = state with { Approvers = [.. state.Approvers, approverUserId] };
        var remaining = await db.KeyTimeToLiveAsync(Key(requestPublicId)) ?? Ttl;
        await db.StringSetAsync(Key(requestPublicId), JsonSerializer.Serialize(updated), remaining);
        return updated.Approvers.Count;
    }

    public async Task<MfaResetRequestState?> GetAsync(Guid requestPublicId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(requestPublicId));
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<MfaResetRequestState>((string)raw!);
    }
}
