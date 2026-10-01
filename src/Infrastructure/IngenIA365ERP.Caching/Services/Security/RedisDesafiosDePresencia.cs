using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

/// <summary>
/// <see cref="IDesafiosDePresencia"/> sobre Redis, en la ranura de la cooperativa activa (feature 012, T33, T085):
/// el desafío presencial dura dos minutos y se consume en el primer intento (<c>GETDEL</c>); un código TOTP usado se
/// marca con <c>SET NX</c> para que no apruebe dos veces. La clave lleva además la cooperativa como prefijo, como
/// <see cref="RedisPermissionClaimsCache"/>: el peor caso de una ranura sin resolver es compartir base con lo global,
/// no leer lo de otra cooperativa. Del código TOTP se guarda sólo su SHA-256.
/// </summary>
internal sealed class RedisDesafiosDePresencia(
    IConnectionMultiplexer redis,
    ITenantCacheSlots ranuras,
    ICurrentTenantService cooperativaActual) : IDesafiosDePresencia
{
    private const string Prefijo = "aprobaciones:";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task GuardarAsync(DesafioDePresencia desafio, TimeSpan vigencia, CancellationToken ct)
    {
        var db = await BaseAsync(ct);
        await db.StringSetAsync(ClaveDeDesafio(desafio.PublicId), JsonSerializer.Serialize(desafio, Json), vigencia);
    }

    public async Task<DesafioDePresencia?> ConsumirAsync(Guid publicId, CancellationToken ct)
    {
        var db = await BaseAsync(ct);
        var valor = await db.StringGetDeleteAsync(ClaveDeDesafio(publicId));
        return valor.HasValue ? JsonSerializer.Deserialize<DesafioDePresencia>((string)valor!, Json) : null;
    }

    public async Task<bool> MarcarTotpUsadoAsync(Guid centralUserId, string codigo, TimeSpan vigencia, CancellationToken ct)
    {
        var db = await BaseAsync(ct);
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo)));
        return await db.StringSetAsync($"{Prefijo}{Cooperativa}:totp:{centralUserId:N}:{huella}", "1", vigencia, When.NotExists);
    }

    private string Cooperativa => cooperativaActual.TenantId ?? "-";

    private string ClaveDeDesafio(Guid publicId) => $"{Prefijo}{Cooperativa}:presencia:{publicId:N}";

    private async Task<IDatabase> BaseAsync(CancellationToken ct) =>
        redis.GetDatabase(await ranuras.RanuraDeAsync(cooperativaActual.TenantId, ct));
}
