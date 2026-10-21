using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Housing;

namespace IngenIA365ERP.Application.Lending.Housing;

/// <summary>
/// Lo que las reglas de vivienda leen de los valores nacionales (feature 013, K6b, T1051, T1055; FR-024, D18): los cinco límites de la Ley 546 de
/// 1999 (valor nacional <c>LIMITE</c>) y el tope de tasa de vivienda (<c>TOPE_TASA_VIVIENDA</c>), con su fuente y su <c>PublicId</c>. Sólo por
/// <see cref="IValoresNacionales"/>, el único lector (<c>LosValoresNacionalesSeLeenEnUnSoloSitio</c>): sin un valor vigente responde
/// <c>NationalValues.Missing</c> (que el lector alerta). Ningún valor de la ley vive aquí. (nuevo)
/// </summary>
public sealed class LectorDeVivienda(IValoresNacionales valores)
{
    /// <summary>
    /// ¿La vivienda está disponible con <paramref name="entrega"/> (por defecto, la vigente)? Antes de K6b una línea, una solicitud o una fila de
    /// migración de vivienda se rechazan como en K1 (D18).
    /// </summary>
    public static bool Disponible(EntregaDeCartera? entrega = null) => (entrega ?? CatalogoDeCartera.EntregaVigente) >= EntregaDeCartera.K6b;

    /// <summary>Los cinco límites de vivienda vigentes a la fecha; <c>NationalValues.Missing</c> si falta alguno.</summary>
    public async Task<Result<LimitesDeVivienda>> LimitesAsync(DateOnly fecha, CancellationToken ct)
    {
        var codigos = new[]
        {
            CatalogoDeLimitesLegales.ViviendaPlazoMinimoMeses, CatalogoDeLimitesLegales.ViviendaPlazoMaximoMeses, CatalogoDeLimitesLegales.ViviendaFinanciable,
            CatalogoDeLimitesLegales.ViviendaFinanciableVis, CatalogoDeLimitesLegales.ViviendaCuotaIngreso,
        };
        var leidos = new List<LimiteVigente>(codigos.Length);
        foreach (var codigo in codigos)
        {
            var leido = await valores.LeerAsync(CodigosDeValorNacional.Limite, codigo, fecha, null, ct);
            if (leido.IsFailure) return Result.Failure<LimitesDeVivienda>(leido.Error);
            leidos.Add(new LimiteVigente(codigo, leido.Value.Valor ?? 0m, leido.Value.Fuente, leido.Value.PublicId));
        }
        return Result.Success(new LimitesDeVivienda(leidos[0], leidos[1], leidos[2], leidos[3], leidos[4]));
    }

    /// <summary>El tope de tasa de vivienda vigente a la fecha; <c>NationalValues.Missing</c> si no hay.</summary>
    public async Task<Result<TopeDeTasaDeVivienda>> TopeAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await valores.LeerAsync(CodigosDeValorNacional.TopeTasaVivienda, string.Empty, fecha, null, ct);
        return leido.IsFailure
            ? Result.Failure<TopeDeTasaDeVivienda>(leido.Error)
            : Result.Success(new TopeDeTasaDeVivienda(leido.Value.Valor ?? 0m, leido.Value.PublicId, leido.Value.ValidFrom, leido.Value.Fuente));
    }

    /// <summary>El primer problema de vivienda como error del contrato (422 con sus datos: límite, valor, fuente).</summary>
    public static Error Error(ProblemaDeCartera problema) => ErroresDeGobierno.De(problema);
}
