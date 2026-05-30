using Microsoft.JSInterop;

namespace IngenIA365ERP.Web.Client.Services;

/// <summary>
/// T057 — Vigila la actividad del usuario (mousemove/keydown). Si transcurren
/// 30 minutos sin actividad, dispara <see cref="InactivityExpired"/> para que
/// el host cierre la sesión. Ligado a JS interop: la captura de eventos vive
/// en el DOM via <c>InactivityWatchdog.registerActivity</c>.
/// </summary>
public sealed class InactivityWatchdog : IAsyncDisposable
{
    private const int IdleMinutes = 30;
    private readonly IJSRuntime _js;
    private DotNetObjectReference<InactivityWatchdog>? _self;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private Timer? _timer;

    public InactivityWatchdog(IJSRuntime js)
    {
        _js = js;
    }

    public event EventHandler? InactivityExpired;

    public async Task StartAsync()
    {
        _self = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("InactivityWatchdog.start", _self);

        _timer = new Timer(_ => Tick(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    [JSInvokable]
    public void OnActivity()
    {
        _lastActivityUtc = DateTime.UtcNow;
    }

    private void Tick()
    {
        var idle = DateTime.UtcNow - _lastActivityUtc;
        if (idle.TotalMinutes >= IdleMinutes)
        {
            InactivityExpired?.Invoke(this, EventArgs.Empty);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer is not null) await _timer.DisposeAsync();
        try
        {
            await _js.InvokeVoidAsync("InactivityWatchdog.stop");
        }
        catch (JSDisconnectedException)
        {
            // Circuito Blazor caído / pestaña cerrada: el JS interop ya no
            // existe. El watchdog se desmonta junto con el ciclo de vida del
            // cliente — la limpieza JS ocurre vía el unload del navegador.
        }
        _self?.Dispose();
    }
}
