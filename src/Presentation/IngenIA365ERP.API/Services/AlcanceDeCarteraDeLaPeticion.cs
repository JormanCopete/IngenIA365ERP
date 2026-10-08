using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IAlcanceDeCartera"/> de la API (feature 013, T62, R131, T0285; FR-004, SC-015): las oficinas y las líneas de crédito de
/// quien hace la petición. Molde: <see cref="AlcanceDeInventarioDeLaPeticion"/>.
///
/// <list type="bullet">
/// <item><b>En segundo plano</b> (<see cref="ContextoAmbiental"/>, lo que fija <c>IEjecutorEnCooperativa</c>) el alcance es total.</item>
/// <item><b>Sin petición ni contexto</b>, vacío: falla cerrado, y no pregunta por un actor que no existe.</item>
/// <item><b>En una petición</b>, lo resuelve <see cref="AlcanceDeCarteraDelActor"/> —memorizado: es Scoped—, que lee las oficinas por
/// <c>OficinasDelUsuario</c> (sin filas, ninguna, salvo <c>Lending.Scope.AllOffices</c>) y las líneas de <c>LND_UserCreditLineScopes</c>
/// (sin filas, ninguna, salvo <c>Lending.Scope.AllLines</c>).</item>
/// </list>
/// </summary>
internal sealed class AlcanceDeCarteraDeLaPeticion(IHttpContextAccessor accessor, AlcanceDeCarteraDelActor delActor) : IAlcanceDeCartera
{
    public Task<AlcanceDeCartera> ObtenerAsync(CancellationToken ct = default)
    {
        if (ContextoAmbiental.Activo) return Task.FromResult(AlcanceDeCartera.Total);
        if (accessor.HttpContext is null) return Task.FromResult(AlcanceDeCartera.Vacio);
        return delActor.ObtenerAsync(ct);
    }
}
