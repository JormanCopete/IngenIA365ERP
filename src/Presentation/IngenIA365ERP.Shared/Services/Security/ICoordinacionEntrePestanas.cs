namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Lo que dos pestañas del mismo navegador necesitan para no matarse la sesión entre sí (2026-11-14).
///
/// <para>
/// <b>El caso.</b> La sesión vive en el <c>sessionStorage</c> de la pestaña. «Duplicar pestaña» lo copia: las dos quedan con el
/// MISMO refresh token. El refresh rota, así que la primera que renueva gasta el token de las dos, y cuando la otra canjea el
/// suyo —ya gastado— el servidor ve un reuso e invalida la familia: las dos pestañas salen al login con «Tu sesión terminó».
/// Pasó en producción el 2026-11-14 («Refresh token reusado detectado»).
/// </para>
///
/// <para>
/// <b>El arreglo.</b> Un <b>turno</b> que abarca todas las pestañas del navegador (Web Locks) para canjear, y un <b>buzón</b>
/// (<c>localStorage</c>) donde la pestaña que canjeó deja, bajo el refresh que gastó, los tokens que recibió. Antes de canjear,
/// con el turno tomado, cada pestaña mira si su refresh ya tiene sucesor y, si lo tiene, lo adopta en vez de canjear. Una
/// pestaña abierta aparte (no duplicada) no comparte nada: tiene su propio refresh y nunca encuentra sucesor.
/// </para>
///
/// <para>MAUI y el host de servidor no tienen pestañas: usan <see cref="SinCoordinacionEntrePestanas"/>.</para>
/// </summary>
public interface ICoordinacionEntrePestanas
{
    /// <summary>Espera el turno de canje de todo el navegador. Se suelta al desechar lo devuelto (o al cerrarse la pestaña).</summary>
    Task<IAsyncDisposable> TomarTurnoAsync(CancellationToken ct = default);

    /// <summary>Los tokens que otra pestaña recibió al canjear ESTE refresh, si los dejó en el buzón.</summary>
    Task<SucesorDeSesion?> SucesorDeAsync(string refreshGastado);

    /// <summary>Deja en el buzón lo que se recibió al canjear <paramref name="refreshGastado"/>.</summary>
    Task PublicarSucesorAsync(string refreshGastado, SucesorDeSesion sucesor);
}

/// <summary>Los tokens que trajo un canje, tal como se adoptan.</summary>
public sealed record SucesorDeSesion(
    string AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    DateTime RotadoEn);

/// <summary>Un host sin pestañas: el turno es inmediato y el buzón está siempre vacío.</summary>
public sealed class SinCoordinacionEntrePestanas : ICoordinacionEntrePestanas
{
    public static readonly SinCoordinacionEntrePestanas Instancia = new();

    public Task<IAsyncDisposable> TomarTurnoAsync(CancellationToken ct = default) => Task.FromResult<IAsyncDisposable>(TurnoVacio.Instancia);
    public Task<SucesorDeSesion?> SucesorDeAsync(string refreshGastado) => Task.FromResult<SucesorDeSesion?>(null);
    public Task PublicarSucesorAsync(string refreshGastado, SucesorDeSesion sucesor) => Task.CompletedTask;

    private sealed class TurnoVacio : IAsyncDisposable
    {
        public static readonly TurnoVacio Instancia = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
