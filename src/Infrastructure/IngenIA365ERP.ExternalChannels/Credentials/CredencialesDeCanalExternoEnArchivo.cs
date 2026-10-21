using System.Collections.Concurrent;
using System.Text.Json;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ExternalChannels.Credentials;

/// <summary>
/// Lo declara el adaptador que pide credenciales (§3.4): su código y los campos obligatorios de su JSON.
/// <see cref="CredencialesDeCanalExternoEnArchivo"/> los valida al leer. Los simulados no lo implementan: no piden archivo. (nuevo)
/// </summary>
public interface IDeclaraEsquemaDeCredencialExterna
{
    string ChannelCode { get; }

    IReadOnlyList<string> CamposDeCredencial { get; }
}

/// <summary>
/// La caché de las credenciales leídas (singleton del proceso; §3.4): 5 minutos por archivo, invalidada en el acto si cambia la <b>huella</b>
/// del archivo (ruta real tras los enlaces del Secret montado, fecha de escritura y largo). Una rotación del Secret se ve en la siguiente
/// lectura sin reiniciar la API. No se comparte con la de la facturación electrónica. (nuevo)
/// </summary>
public sealed class CacheDeCredencialesExternas
{
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Entrada> _entradas = new(StringComparer.Ordinal);

    /// <summary>Cuántas veces se leyó un archivo (para las pruebas de la caché).</summary>
    public int Lecturas => _lecturas;

    private int _lecturas;

    internal bool Intentar(string ruta, string huella, DateTime ahora, out CredencialDeCanalExterno credencial)
    {
        if (_entradas.TryGetValue(ruta, out var e) && e.Huella == huella && ahora - e.LeidaEn < Duracion)
        {
            credencial = e.Credencial;
            return true;
        }
        credencial = null!;
        return false;
    }

    internal void Guardar(string ruta, string huella, DateTime ahora, CredencialDeCanalExterno credencial)
    {
        Interlocked.Increment(ref _lecturas);
        _entradas[ruta] = new Entrada(huella, ahora, credencial);
    }

    internal void Olvidar(string ruta) => _entradas.TryRemove(ruta, out _);

    private sealed record Entrada(string Huella, DateTime LeidaEn, CredencialDeCanalExterno Credencial);
}

/// <summary>
/// Las credenciales de los canales externos en archivo (feature 013, T0081; T72; contracts/adaptadores-y-archivos.md §3.4): el Secret
/// <c>erp-canales-externos</c> montado como directorio en <c>ExternalChannels:CredentialsPath</c>, una clave por cooperativa y canal,
/// <c>{tenantPublicId}.{channelCode}.json</c>.
/// <list type="bullet">
/// <item>La ruta se arma <b>sólo</b> con el <c>PublicId</c> de la cooperativa resuelta (<see cref="CooperativaDelCanal"/>) y un código que
/// cumple <c>^[A-Z0-9-]{2,30}$</c>: nunca con un texto leído de la base.</item>
/// <item>La base guarda sólo <c>CredentialKey</c>; si la de la configuración vigente del canal no es la derivada,
/// <c>Core.ExternalChannel.CredentialMismatch</c>.</item>
/// <item><c>SIMULADO</c> y <c>CARGA-PROPIA</c> no leen archivo.</item>
/// <item>Valida el esquema que declara el adaptador (<see cref="IDeclaraEsquemaDeCredencialExterna"/>).</item>
/// <item>Un archivo ausente, ilegible o incompleto es un fallo (<c>Core.ExternalChannel.CredentialMissing</c>), nunca una excepción, y
/// ningún mensaje lleva un valor del archivo.</item>
/// <item>Caché de 5 minutos por huella (<see cref="CacheDeCredencialesExternas"/>).</item>
/// </list>
/// No reutiliza <c>CredencialesEnArchivo</c> de la DIAN (acoplada a su configuración y con caché interna, T72). (nuevo)
/// </summary>
public sealed class CredencialesDeCanalExternoEnArchivo(
    IOptions<ExternalChannelsOptions> opciones,
    CacheDeCredencialesExternas cache,
    ICurrentTenantService tenant,
    IApplicationDbContext db,
    IDateTimeService reloj,
    IEnumerable<IDeclaraEsquemaDeCredencialExterna>? esquemas = null) : ICredencialesDeCanalExterno
{
    public async Task<Result<CredencialDeCanalExterno>> ResolverAsync(string channelCode, CancellationToken ct)
    {
        var canal = CodigosDeCanalExterno.Normalizar(channelCode);
        if (canal is null)
            return Fallo(channelCode, "El canal no es un código válido (mayúsculas, dígitos y guiones, de 2 a 30): no se leen credenciales.");

        if (CooperativaDelCanal.Resuelta(tenant) is not { } cooperativa)
            return Fallo(canal, "No hay una cooperativa resuelta: no se leen credenciales.");

        var clave = CredencialDeCanalExterno.ClaveDe(cooperativa, canal);

        // La base sólo guarda el nombre de la clave: si la configuración vigente del canal apunta a otra, alguien la alteró.
        var guardada = await db.ExternalChannelSettings.AsNoTracking()
            .Where(s => s.ChannelCode == canal && s.ValidTo == null)
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => s.CredentialKey)
            .FirstOrDefaultAsync(ct);
        if (guardada is not null && !string.Equals(guardada, clave, StringComparison.Ordinal))
            return Result.Failure<CredencialDeCanalExterno>(ErroresDeCanalesExternos.ClaveNoCoincide(canal, clave));

        if (CodigosDeCanalExterno.NoPideCredencial(canal)) return Result.Success(CredencialDeCanalExterno.Vacia(cooperativa, canal));

        var directorio = Path.GetFullPath(string.IsNullOrWhiteSpace(opciones.Value.CredentialsPath)
            ? ExternalChannelsOptions.RutaDeCredencialesPorDefecto
            : opciones.Value.CredentialsPath);
        var ruta = Path.GetFullPath(Path.Combine(directorio, clave));
        if (!ruta.StartsWith(directorio, StringComparison.Ordinal))
            return Fallo(canal, "La ruta de la credencial sale del directorio del Secret.");

        var huella = Huella(ruta);
        if (huella is null)
        {
            cache.Olvidar(ruta);
            return Fallo(canal, $"No está la credencial «{clave}» del canal {canal}: cárguela en el Secret erp-canales-externos.");
        }

        var ahora = reloj.UtcNow;
        if (cache.Intentar(ruta, huella, ahora, out var enCache)) return Result.Success(enCache);

        var leida = await LeerAsync(ruta, clave, canal, ct);
        if (leida.IsSuccess) cache.Guardar(ruta, huella, ahora, leida.Value);
        else cache.Olvidar(ruta);
        return leida;
    }

    private static Result<CredencialDeCanalExterno> Fallo(string? canal, string detalle) =>
        Result.Failure<CredencialDeCanalExterno>(ErroresDeCanalesExternos.SinCredencial(canal ?? string.Empty, detalle));

    private async Task<Result<CredencialDeCanalExterno>> LeerAsync(string ruta, string clave, string canal, CancellationToken ct)
    {
        Dictionary<string, string> valores;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            using var json = await JsonDocument.ParseAsync(archivo, cancellationToken: ct);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return Fallo(canal, $"La credencial «{clave}» no es un objeto JSON.");
            valores = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in json.RootElement.EnumerateObject())
            {
                valores[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null => string.Empty,
                    _ => p.Value.GetRawText(),
                };
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // El mensaje de JsonException puede citar el contenido: no se devuelve.
            return Fallo(canal, $"La credencial «{clave}» no se pudo leer ({ex.GetType().Name}).");
        }

        var faltan = Esquema(canal).Where(c => !valores.TryGetValue(c, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        if (faltan.Count > 0)
            return Fallo(canal, $"A la credencial «{clave}» le faltan campos que pide el canal {canal}: {string.Join(", ", faltan)}.");

        return Result.Success(new CredencialDeCanalExterno(clave, valores));
    }

    private IReadOnlyList<string> Esquema(string canal) =>
        esquemas?.FirstOrDefault(e => string.Equals(e.ChannelCode, canal, StringComparison.OrdinalIgnoreCase))?.CamposDeCredencial ?? [];

    /// <summary>La huella del archivo tras resolver los enlaces del Secret montado; nula si no existe.</summary>
    private static string? Huella(string ruta)
    {
        try
        {
            FileSystemInfo info = new FileInfo(ruta);
            if (!info.Exists) return null;
            if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { Exists: true } destino) info = destino;
            var real = (FileInfo)info;
            return $"{real.FullName}|{real.LastWriteTimeUtc.Ticks}|{real.Length}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
