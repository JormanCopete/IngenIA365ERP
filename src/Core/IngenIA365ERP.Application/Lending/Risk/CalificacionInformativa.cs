using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// La calificación <b>informativa</b> de unos créditos a una fecha con lo conocido en R (feature 013, US9, T0701; T46, FR-099): el mismo motor del
/// cierre —con arrastre según <c>Riesgo.CalificacionDiariaConArrastre</c> (pregunta E7)—, comparada con la línea del último cierre vigente hasta esa
/// fecha. No se guarda (salvo la copia de la posición diaria que escribe el ciclo); se rotula «Informativa». (nuevo)
/// </summary>
public sealed class CalificacionInformativa(IApplicationDbContext db, LectorDelContextoDeRiesgo contextos, CargadorDeRiesgo cargador)
{
    /// <summary>El rótulo con que se muestra (api.md §12.2).</summary>
    public const string Rotulo = "Informativa";

    /// <param name="sinAnalisis">Sin la calificación por análisis: la que dan las reglas (para no dejar poner un análisis que no empeora).</param>
    public async Task<Result<RiesgoDeLaTanda>> CalcularAsync(IReadOnlyCollection<int> loanIds, DateOnly fecha, DateTime? informacionHasta, bool sinAnalisis,
        CancellationToken ct)
    {
        var contexto = await contextos.LeerAsync(fecha, informacionHasta, paraElCierre: false, ct);
        if (contexto.IsFailure) return Result.Failure<RiesgoDeLaTanda>(contexto.Error);
        var anterior = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, fecha.AddDays(1), ct);
        var datos = await cargador.CargarAsync(loanIds, fecha, informacionHasta, contexto.Value, anterior, ct);
        return Result.Success(CalculoDeRiesgo.Calcular(datos, contexto.Value, contexto.Value.ArrastreEnLaInformativa, sinAnalisis));
    }
}
