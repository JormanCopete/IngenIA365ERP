using IngenIA365ERP.Shared.Services;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Web.Client.Services
{
    /// <summary>
    /// Feature 003 (US4, FR-113..FR-115) — Storage de tokens respaldado por
    /// <c>sessionStorage</c> del navegador: la sesión sobrevive a F5 pero muere
    /// al cerrar la pestaña (clarificación del spec: alcance solo-pestaña).
    /// sessionStorage no se comparte entre pestañas ni persiste en disco tras
    /// cerrar, lo que acota la exposición frente a localStorage.
    ///
    /// <para>En WASM el runtime JS es in-process, así que el interop es
    /// síncrono (<see cref="IJSInProcessRuntime"/>) — necesario porque
    /// <c>Remove/RemoveAll</c> de <see cref="ISecureStorage"/> son síncronos.
    /// Si el runtime no es in-process (nunca en WASM real), degrada a
    /// diccionario en memoria.</para>
    /// </summary>
    public class BrowserSessionSecureStorage : ISecureStorage
    {
        private const string Prefix = "ingenia365:";

        private readonly IJSInProcessRuntime? _js;
        private readonly Dictionary<string, string> _fallback = new();

        public BrowserSessionSecureStorage(IJSRuntime js)
        {
            _js = js as IJSInProcessRuntime;
        }

        public Task SetAsync(string key, string value)
        {
            if (_js is not null)
                _js.InvokeVoid("sessionStorage.setItem", Prefix + key, value);
            else
                _fallback[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key)
        {
            if (_js is not null)
                return Task.FromResult(_js.Invoke<string?>("sessionStorage.getItem", Prefix + key));

            _fallback.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public bool Remove(string key)
        {
            if (_js is not null)
            {
                var existed = _js.Invoke<string?>("sessionStorage.getItem", Prefix + key) is not null;
                _js.InvokeVoid("sessionStorage.removeItem", Prefix + key);
                return existed;
            }
            return _fallback.Remove(key);
        }

        public void RemoveAll()
        {
            if (_js is not null)
            {
                // Solo nuestras claves — no arrasar sessionStorage de terceros.
                var count = _js.Invoke<int>("eval",
                    $"Object.keys(sessionStorage).filter(k => k.startsWith('{Prefix}')).map(k => sessionStorage.removeItem(k)).length");
                _ = count;
            }
            _fallback.Clear();
        }
    }
}
