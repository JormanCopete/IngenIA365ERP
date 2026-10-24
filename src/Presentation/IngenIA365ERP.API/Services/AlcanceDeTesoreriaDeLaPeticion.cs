using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Security;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAlcanceDeTesoreria"/> de la API (feature 015, T0058; US12; R-30; FR-011, FR-106): las cuentas bancarias propias de quien hace la petición.
/// En segundo plano, todo; sin petición, nada; en una petición, <see cref="AlcanceDeTesoreriaDelActor"/>. (nuevo)
/// </summary>
internal sealed class AlcanceDeTesoreriaDeLaPeticion(IHttpContextAccessor accessor, AlcanceDeTesoreriaDelActor delActor) : IAlcanceDeTesoreria
{
    public Task<AlcanceDeTesoreria> ObtenerAsync(CancellationToken ct = default)
    {
        if (ContextoAmbiental.Activo) return Task.FromResult(AlcanceDeTesoreria.Todo);
        if (accessor.HttpContext is null) return Task.FromResult(AlcanceDeTesoreria.Vacio);
        return delActor.ObtenerAsync(ct);
    }
}
