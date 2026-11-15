namespace IngenIA365Canal.App.Puente;

/// <summary>
/// Feature 021, US11 (T136; contracts/pasarela-y-app.md, «Puente de la app»): lo que la PWA pide al teléfono con
/// <c>window.HybridWebView.InvokeDotNet(método, [parámetros])</c>. Los nombres de los métodos son el contrato (<c>METODOS_DEL_PUENTE</c> de
/// <c>canal/web/src/plataforma.ts</c>); las respuestas se serializan con <see cref="ContextoDelPuente"/>. La credencial del dispositivo vive en
/// <see cref="SecureStorage"/> y sólo sale después de la biometría (R13).
/// </summary>
public sealed class PuenteDelCanal(IBiometria biometria)
{
    /// <summary>La clave de la credencial del dispositivo de confianza en el almacén seguro.</summary>
    public const string ClaveDeCredencial = "canal.credencial-del-dispositivo";

    public InfoDeLaPlataforma Plataforma() =>
        new(DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android", DeviceInfo.VersionString, AppInfo.VersionString);

    public async Task GuardarCredencial(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Length > 512) throw new ArgumentException("La credencial no tiene la forma esperada.", nameof(valor));
        await SecureStorage.Default.SetAsync(ClaveDeCredencial, valor);
    }

    /// <summary>Pide la biometría y, si pasa, devuelve la credencial guardada; nulo si se cancela, falla o no hay.</summary>
    public async Task<string?> LeerCredencialConBiometria(string motivo)
    {
        var guardada = await SecureStorage.Default.GetAsync(ClaveDeCredencial);
        if (guardada is null) return null;
        return await biometria.AutenticarAsync(string.IsNullOrWhiteSpace(motivo) ? "Confirma que eres tú" : motivo) ? guardada : null;
    }

    public Task BorrarCredencial()
    {
        SecureStorage.Default.Remove(ClaveDeCredencial);
        return Task.CompletedTask;
    }

    public Task<BiometriaDelDispositivo> BiometriaDisponible() => biometria.DisponibleAsync();

    /// <summary>
    /// Lleva a la persona a la pasarela en el navegador del sistema (Custom Tabs / <c>ASWebAuthenticationSession</c>) y espera el retorno por
    /// App Link / Universal Link a <paramref name="retorno"/>. La pasarela sólo se abre por https y el retorno sólo es del host del canal.
    /// </summary>
    public async Task<ResultadoDeLaPasarela> AbrirPasarela(string url, string retorno)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var destino) || destino.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("La dirección de pago no es https.", nameof(url));
        if (!Uri.TryCreate(retorno, UriKind.Absolute, out var vuelta) || vuelta.Scheme != Uri.UriSchemeHttps
            || !string.Equals(vuelta.Host, ConfiguracionDelCanal.Host, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("El retorno no es del canal.", nameof(retorno));
        try
        {
            var r = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions { Url = destino, CallbackUrl = vuelta });
            return new ResultadoDeLaPasarela("returned", r.CallbackUri?.ToString() ?? vuelta.ToString());
        }
        catch (TaskCanceledException)
        {
            return new ResultadoDeLaPasarela("cancelled", null);
        }
    }

    /// <summary>El token de FCM del teléfono para <c>POST /devices/push-token</c>; nulo sin Firebase o sin permiso.</summary>
    public Task<string?> TokenPush() => Notificaciones.TokenAsync();

    /// <summary>Guarda o comparte un documento (el comprobante o un extracto en PDF) con la hoja del sistema.</summary>
    public async Task AbrirDocumento(string bytesBase64, string nombre, string tipo)
    {
        var bytes = Convert.FromBase64String(bytesBase64);
        var archivo = Path.Combine(FileSystem.CacheDirectory, Path.GetFileName(string.IsNullOrWhiteSpace(nombre) ? "documento.pdf" : nombre));
        await File.WriteAllBytesAsync(archivo, bytes);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = Path.GetFileName(archivo), File = new ShareFile(archivo, tipo) });
    }
}

/// <summary>Plataforma(): el sistema, su versión y la de la app.</summary>
public sealed record InfoDeLaPlataforma(string Os, string Version, string? AppVersion);

/// <summary>BiometriaDisponible(): si hay y cuál (huella, rostro).</summary>
public sealed record BiometriaDelDispositivo(bool Disponible, string? Tipo);

/// <summary>AbrirPasarela(): «returned» con la URL del retorno, o «cancelled».</summary>
public sealed record ResultadoDeLaPasarela(string Resultado, string? Url);
