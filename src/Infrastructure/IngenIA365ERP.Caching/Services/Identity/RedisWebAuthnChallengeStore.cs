using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Implementación Redis de <see cref="IWebAuthnChallengeStore"/>.
///
/// <para>
/// La clave lleva dentro la persona y el propósito
/// —<c>webauthn-reto:{proposito}:{centralUserId:N}:{id}</c>— y no sólo el
/// identificador del reto. Así, pedir un reto de alta y presentarlo como reto de
/// ingreso no encuentra nada, en vez de encontrar algo y tener que acordarse de
/// comprobar a quién pertenecía.
/// </para>
///
/// <para>
/// El consumo usa <c>GETDEL</c>, que lee y borra en una sola operación atómica.
/// Con un GET seguido de un DELETE, dos peticiones simultáneas podrían leer el
/// mismo reto antes de que ninguna lo borrara, y el reto dejaría de ser de un
/// solo uso justo en el caso que importa.
/// </para>
/// </summary>
internal sealed class RedisWebAuthnChallengeStore(IConnectionMultiplexer redis) : IWebAuthnChallengeStore
{
    private const string PrefijoDeClave = "webauthn-reto:";

    public async Task<string> GuardarAsync(
        Guid centralUserId,
        PropositoDeReto proposito,
        string opcionesJson,
        TimeSpan vigencia,
        CancellationToken ct)
    {
        // Identificador opaco y aleatorio. No es un secreto —el reto de dentro sí
        // lo es— pero que sea impredecible evita que alguien pueda tantear los
        // retos vivos de otras personas.
        var idDelReto = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        await redis.GetDatabase().StringSetAsync(
            Clave(centralUserId, proposito, idDelReto), opcionesJson, vigencia);

        return idDelReto;
    }

    public async Task<string?> ConsumirAsync(
        Guid centralUserId,
        PropositoDeReto proposito,
        string idDelReto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idDelReto)) return null;

        var valor = await redis.GetDatabase()
            .StringGetDeleteAsync(Clave(centralUserId, proposito, idDelReto));

        return valor.HasValue ? (string?)valor : null;
    }

    private static string Clave(Guid centralUserId, PropositoDeReto proposito, string idDelReto) =>
        $"{PrefijoDeClave}{proposito}:{centralUserId:N}:{idDelReto}";
}
