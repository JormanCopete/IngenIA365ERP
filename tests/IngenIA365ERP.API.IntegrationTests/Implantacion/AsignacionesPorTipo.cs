using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace IngenIA365ERP.API.IntegrationTests.Implantacion;

/// <summary>
/// Cuánto se asigna por tipo mientras dura una medición, con el evento <c>AllocationTick</c> del runtime (una muestra cada
/// 100 KB aproximadamente): dice qué está costando sin instalar un perfilador. (nuevo)
/// </summary>
internal sealed class AsignacionesPorTipo : EventListener
{
    private readonly ConcurrentDictionary<string, long> _porTipo = new();

    protected override void OnEventSourceCreated(EventSource fuente)
    {
        if (fuente.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(fuente, EventLevel.Verbose, (EventKeywords)0x1);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventName is null || !e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) || e.Payload is null) return;
        var nombres = e.PayloadNames!;
        var tipo = e.Payload[nombres.IndexOf("TypeName")] as string ?? "?";
        var cantidad = Convert.ToInt64(e.Payload[nombres.IndexOf("AllocationAmount64")], System.Globalization.CultureInfo.InvariantCulture);
        _porTipo.AddOrUpdate(tipo, cantidad, (_, v) => v + cantidad);
    }

    public string Resumen(int cuantos = 18)
    {
        var total = _porTipo.Values.Sum();
        return string.Join("\n", _porTipo.OrderByDescending(p => p.Value).Take(cuantos)
            .Select(p => $"  {p.Value / (1024 * 1024),6} MB  {p.Value * 100 / Math.Max(total, 1),3} %  {p.Key}"));
    }
}
