using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Cliente del API público <b>HaveIBeenPwned Pwned Passwords v3</b> con k-anonymity
/// (T036, research D-04): el cliente envía solo los primeros 5 caracteres del hash
/// SHA-1 de la contraseña; el servidor responde con todos los sufijos que matchean
/// y sus conteos; el cliente busca su sufijo localmente. La contraseña nunca
/// abandona el servidor del producto en plano ni en hash completo.
///
/// <para><b>Fail-open</b>: si el servicio externo falla o timeout (2s default), retorna
/// <see cref="PwnedPasswordResult.Unavailable"/>. El caller decide si dejar pasar
/// (caso registro/cambio — la longitud mínima 12 ofrece base de seguridad) o
/// bloquear. La política recomendada y configurable es <c>FailOpenOnError=true</c>
/// en <c>appsettings.json:PwnedPassword</c>.</para>
/// </summary>
internal sealed class PwnedPasswordService : IPwnedPasswordService
{
    public const string HttpClientName = "pwned-passwords";

    private readonly HttpClient _http;
    private readonly PwnedPasswordOptions _opts;
    private readonly ILogger<PwnedPasswordService> _log;

    public PwnedPasswordService(
        HttpClient http,
        IOptions<PwnedPasswordOptions> opts,
        ILogger<PwnedPasswordService> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
    }

    public async Task<PwnedPasswordResult> IsPwnedAsync(string password, CancellationToken ct)
    {
        if (!_opts.Enabled)
            return PwnedPasswordResult.Unavailable();

        if (string.IsNullOrEmpty(password))
            return PwnedPasswordResult.Available(isPwned: false);

        var sha1 = ComputeSha1Hex(password);   // 40 hex chars uppercase
        var prefix = sha1[..5];                // primeros 5
        var suffix = sha1[5..];                // resto 35

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opts.TimeoutSeconds));

            using var response = await _http.GetAsync($"range/{prefix}", cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Pwned.UnavailableFallback HTTP {StatusCode}", response.StatusCode);
                return PwnedPasswordResult.Unavailable();
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            return ParseResponse(body, suffix);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El caller canceló — propagar, no es fail-open.
            throw;
        }
        catch (OperationCanceledException)
        {
            _log.LogWarning("Pwned.UnavailableFallback timeout");
            return PwnedPasswordResult.Unavailable();
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning(ex, "Pwned.UnavailableFallback HttpRequestException");
            return PwnedPasswordResult.Unavailable();
        }
    }

    private static PwnedPasswordResult ParseResponse(string body, string suffix)
    {
        // Formato: una línea por sufijo "SUFFIX:COUNT\n".
        foreach (var line in body.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;

            var candidateSuffix = line.AsSpan(0, colon).Trim();
            if (candidateSuffix.Equals(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var countSpan = line.AsSpan(colon + 1).Trim();
                var count = int.TryParse(countSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c)
                    ? c : 1;
                return PwnedPasswordResult.Available(isPwned: true, occurrenceCount: count);
            }
        }
        return PwnedPasswordResult.Available(isPwned: false);
    }

    private static string ComputeSha1Hex(string input)
    {
        Span<byte> hash = stackalloc byte[20];
        SHA1.HashData(Encoding.UTF8.GetBytes(input), hash);

        var sb = new StringBuilder(40);
        foreach (var b in hash) sb.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", b);
        return sb.ToString();
    }
}

/// <summary>Bound to <c>appsettings.json:PwnedPassword</c>.</summary>
public sealed class PwnedPasswordOptions
{
    public const string SectionName = "PwnedPassword";

    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.pwnedpasswords.com";
    public int TimeoutSeconds { get; set; } = 2;
    public bool FailOpenOnError { get; set; } = true;
}
