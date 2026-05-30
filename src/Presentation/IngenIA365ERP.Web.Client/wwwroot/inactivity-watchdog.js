// T057 — JS interop para el InactivityWatchdog. Reporta actividad al lado .NET
// vía DotNetObjectReference.OnActivity. Eventos escuchados: mousemove, keydown,
// click, touchstart, visibilitychange.
window.InactivityWatchdog = (function () {
    let dotnet = null;
    let throttleUntil = 0;

    function notify() {
        const now = Date.now();
        if (now < throttleUntil || !dotnet) return;
        // Limita reporte a 1 vez cada 5 s.
        throttleUntil = now + 5_000;
        dotnet.invokeMethodAsync("OnActivity").catch(() => { /* swallow */ });
    }

    function start(reference) {
        dotnet = reference;
        ["mousemove", "keydown", "click", "touchstart"].forEach(evt => {
            document.addEventListener(evt, notify, { passive: true });
        });
        document.addEventListener("visibilitychange", () => {
            if (!document.hidden) notify();
        });
    }

    function stop() {
        ["mousemove", "keydown", "click", "touchstart"].forEach(evt => {
            document.removeEventListener(evt, notify);
        });
        dotnet = null;
    }

    return { start, stop };
})();
