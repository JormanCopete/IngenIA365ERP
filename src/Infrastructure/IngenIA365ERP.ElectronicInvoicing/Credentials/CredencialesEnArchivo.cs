using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ElectronicInvoicing.Credentials;

/// <summary>
/// Lo declara el adaptador que pide credenciales (contracts/dian.md §11): los campos obligatorios de su JSON. <see cref="CredencialesEnArchivo"/>
/// los valida al leer. Un adaptador que no lo implementa (como <c>CanalSimulado</c>) no exige ninguno. (nuevo)
/// </summary>
public interface IDeclaraEsquemaDeCredenciales
{
    IReadOnlyList<string> CamposDeCredencial { get; }
}

/// <summary>
/// La caché de las credenciales leídas (singleton del proceso; contracts/dian.md §11): 5 minutos por archivo, invalidada en el acto si
/// cambia la <b>huella</b> del archivo (ruta real tras los enlaces del Secret montado, fecha de escritura y largo). Así una rotación del
/// Secret —el kubelet cambia el enlace <c>..data</c>— se ve en la siguiente lectura sin reiniciar la API. (nuevo)
/// </summary>
public sealed class CacheDeCredenciales
{
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, EntradaDeCredencial> _entradas = new(StringComparer.Ordinal);

    internal bool Intentar(string ruta, string huella, DateTime ahora, out CredencialesDeCanal credenciales)
    {
        if (_entradas.TryGetValue(ruta, out var e) && e.Huella == huella && ahora - e.LeidaEn < Duracion)
        {
            credenciales = e.Credenciales;
            return true;
        }
        credenciales = null!;
        return false;
    }

    internal void Guardar(string ruta, string huella, DateTime ahora, CredencialesDeCanal credenciales) =>
        _entradas[ruta] = new EntradaDeCredencial(huella, ahora, credenciales);

    internal void Olvidar(string ruta) => _entradas.TryRemove(ruta, out _);

    private sealed record EntradaDeCredencial(string Huella, DateTime LeidaEn, CredencialesDeCanal Credenciales);
}

/// <summary>
/// Las credenciales de facturación electrónica en archivo (feature 012, I4, T729; contracts/dian.md §11): el Secret
/// <c>erp-fe-credenciales</c> montado como directorio en <c>ElectronicInvoicing:CredentialsPath</c>, una clave por cooperativa y canal,
/// <c>{tenantPublicId}.{channelCode}.json</c>.
/// <list type="bullet">
/// <item>La ruta se arma <b>sólo</b> con el <c>PublicId</c> de la cooperativa resuelta (<see cref="ICurrentTenantService"/>, o la entrada del
/// directorio que fijó <see cref="IEjecutorEnCooperativa"/>) y el código del canal sellado, que tiene que ser un código (letras, dígitos y
/// guiones): nunca un texto leído de la base. Una cooperativa no puede usar las credenciales de otra aunque alguien altere su base.</item>
/// <item>La base guarda sólo <c>CredentialKey</c>; si la de la configuración del canal no es la derivada, <c>ElectronicInvoicing.CredentialMismatch</c>.</item>
/// <item>Valida el esquema que declara el adaptador (<see cref="IDeclaraEsquemaDeCredenciales"/>).</item>
/// <item>Un archivo ausente, ilegible o incompleto es un <b>fallo</b> (<see cref="CodigoNoDisponible"/>), nunca una excepción: la emisión lo
/// traduce a <c>ChannelUnavailable</c> y la verificación a <c>CredentialMismatch</c>. Ningún mensaje lleva un valor del archivo.</item>
/// <item>Caché de 5 minutos por huella del archivo (<see cref="CacheDeCredenciales"/>).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CredencialesEnArchivo(
    IOptions<ElectronicInvoicingOptions> opciones,
    CacheDeCredenciales cache,
    ICurrentTenantService tenant,
    IApplicationDbContext db,
    IDateTimeService reloj,
    ICanalesDeEmision? canales = null) : ICredencialesDeCanal
{
    /// <summary>Archivo ausente, ilegible o sin los campos del esquema. (nuevo)</summary>
    public const string CodigoNoDisponible = "ElectronicInvoicing.Credential.Unavailable";

    /// <summary>La <c>CredentialKey</c> guardada no es la derivada de la cooperativa resuelta.</summary>
    public const string CodigoNoCoincide = "ElectronicInvoicing.CredentialMismatch";

    /// <summary>Un código de canal: sin barras, puntos ni espacios, así no puede salir del directorio.</summary>
    private static readonly Regex CodigoDeCanal = new("^[A-Za-z0-9][A-Za-z0-9-]{0,39}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<Result<CredencialesDeCanal>> ResolverAsync(string channelCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(channelCode) || !CodigoDeCanal.IsMatch(channelCode))
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible, "El canal no es un código válido: no se leen credenciales.");
        var canal = channelCode.ToUpperInvariant();

        var cooperativa = Cooperativa();
        if (cooperativa is null)
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible, "No hay una cooperativa resuelta: no se leen credenciales.");

        var clave = CredencialesDeCanal.ClaveDe(cooperativa.Value, canal);

        // La base sólo guarda el nombre de la clave: si la configuración del canal apunta a otra, alguien la alteró.
        var guardada = await db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ChannelCode == canal)
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => s.CredentialKey)
            .FirstOrDefaultAsync(ct);
        if (guardada is not null && !string.Equals(guardada, clave, StringComparison.Ordinal))
            return Result.Failure<CredencialesDeCanal>(CodigoNoCoincide,
                $"La credencial configurada para el canal {canal} no corresponde a esta cooperativa (se esperaba «{clave}»).");

        var directorio = string.IsNullOrWhiteSpace(opciones.Value.CredentialsPath)
            ? ElectronicInvoicingOptions.RutaDeCredencialesPorDefecto
            : opciones.Value.CredentialsPath;
        var ruta = Path.GetFullPath(Path.Combine(directorio, clave));
        if (!ruta.StartsWith(Path.GetFullPath(directorio), StringComparison.Ordinal))
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible, "La ruta de la credencial sale del directorio del Secret.");

        var huella = Huella(ruta);
        if (huella is null)
        {
            cache.Olvidar(ruta);
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible,
                $"No está la credencial «{clave}» del canal {canal}: cárguela en el Secret de facturación electrónica.");
        }

        var ahora = reloj.UtcNow;
        if (cache.Intentar(ruta, huella, ahora, out var enCache)) return Result.Success(enCache);

        var leida = await LeerAsync(ruta, clave, canal, ct);
        if (leida.IsSuccess) cache.Guardar(ruta, huella, ahora, leida.Value);
        else cache.Olvidar(ruta);
        return leida;
    }

    private Guid? Cooperativa()
    {
        if (Guid.TryParse(tenant.TenantId, out var publica) && publica != Guid.Empty) return publica;
        return ContextoAmbiental.Cooperativa?.PublicId is { } delTrabajo && delTrabajo != Guid.Empty ? delTrabajo : null;
    }

    private async Task<Result<CredencialesDeCanal>> LeerAsync(string ruta, string clave, string canal, CancellationToken ct)
    {
        Dictionary<string, string> valores;
        try
        {
            await using var archivo = File.OpenRead(ruta);
            using var json = await JsonDocument.ParseAsync(archivo, cancellationToken: ct);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible, $"La credencial «{clave}» no es un objeto JSON.");
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
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible, $"La credencial «{clave}» no se pudo leer ({ex.GetType().Name}).");
        }

        var faltan = Esquema(canal).Where(c => !valores.TryGetValue(c, out var v) || string.IsNullOrWhiteSpace(v)).ToList();
        if (faltan.Count > 0)
            return Result.Failure<CredencialesDeCanal>(CodigoNoDisponible,
                $"A la credencial «{clave}» le faltan campos que pide el canal {canal}: {string.Join(", ", faltan)}.");

        return Result.Success(new CredencialesDeCanal(clave, valores));
    }

    private IReadOnlyList<string> Esquema(string canal)
    {
        if (canales is null || !canales.Codigos.Contains(canal, StringComparer.OrdinalIgnoreCase)) return [];
        return canales.Resolver(canal) is IDeclaraEsquemaDeCredenciales declarado ? declarado.CamposDeCredencial : [];
    }

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
