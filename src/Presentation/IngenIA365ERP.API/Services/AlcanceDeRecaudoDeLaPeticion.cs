using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAlcanceDeRecaudo"/> de la API (feature 015, T0058; US12; R-30; FR-011, SC-018): las oficinas, puntos, cajas y pagadurías de quien hace la
/// petición. Molde: <see cref="AlcanceDeCarteraDeLaPeticion"/>.
/// <list type="bullet">
/// <item><b>En segundo plano</b> (<see cref="ContextoAmbiental"/>) el alcance es total.</item>
/// <item><b>Sin petición ni contexto</b>, vacío: falla cerrado.</item>
/// <item><b>En una petición</b>, lo resuelve <see cref="AlcanceDeRecaudoDelActor"/> (memorizado: es Scoped).</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class AlcanceDeRecaudoDeLaPeticion(IHttpContextAccessor accessor, AlcanceDeRecaudoDelActor delActor) : IAlcanceDeRecaudo
{
    public Task<AlcanceDeRecaudo> ObtenerAsync(CancellationToken ct = default)
    {
        if (ContextoAmbiental.Activo) return Task.FromResult(AlcanceDeRecaudo.Todo);
        if (accessor.HttpContext is null) return Task.FromResult(AlcanceDeRecaudo.Vacio);
        return delActor.ObtenerAsync(ct);
    }
}
