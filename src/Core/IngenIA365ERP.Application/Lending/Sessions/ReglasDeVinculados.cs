using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Governance;

namespace IngenIA365ERP.Application.Lending.Sessions;

/// <summary>
/// Lo que la ley fija sobre los vinculados en los órganos (feature 013, T0294, T0296, T0298; FR-040, T42; Ley 454 de 1998, art. 61): si
/// la cooperativa tiene actividad financiera según su perfil regulatorio vigente (<see cref="IPerfilRegulatorio"/>; sin perfil vigente se
/// asume que sí, que es lo más estricto), la fracción calificada de vinculados del consejo frente al límite nacional
/// <c>VINCULADOS_FRACCION_CALIFICADA</c> y el grado de parentesco de un pariente frente a <c>VINCULADOS_GRADO_PARENTESCO</c>
/// (<see cref="IValoresNacionales"/>). Ninguno de los dos valores está en el código (FR-014). (nuevo)
/// </summary>
public sealed class ReglasDeVinculados(IValoresNacionales valoresNacionales, IPerfilRegulatorio perfilRegulatorio)
{
    /// <summary>Si la cooperativa ejerce actividad financiera en <paramref name="fecha"/> (sin perfil vigente: sí).</summary>
    public async Task<bool> ConActividadFinancieraAsync(DateOnly fecha, CancellationToken ct = default) =>
        (await perfilRegulatorio.VigenteAsync(fecha, null, ct))?.FinancialActivityAuthorized ?? true;

    /// <summary>
    /// La regla de quórum y mayorías que se quiere registrar, frente a la ley: con actividad financiera, el consejo lleva la mayoría
    /// calificada de vinculados (<c>RulesInvalid</c>, <c>RelatedPartyFractionRequired</c>) y ésta no puede ser menos estricta que el límite
    /// nacional vigente en su <c>ValidFrom</c> (<c>Lending.Parameter.BelowLegalFloor</c> con <c>limitCode</c>, <c>limitValue</c> y
    /// <c>source</c>). Sin actividad financiera rige lo de los estatutos.
    /// </summary>
    public async Task<Result> ValidarAsync(GoverningBodyKind claseDelOrgano, BorradorDeReglasDeOrgano reglas, CancellationToken ct = default)
    {
        if (!await ConActividadFinancieraAsync(reglas.ValidFrom, ct)) return Result.Success();
        if (claseDelOrgano == GoverningBodyKind.BoardOfDirectors && reglas.RelatedPartyNumerator is null)
            return Result.Failure(ErroresDeGobierno.ReglasInvalidas("RelatedPartyFractionRequired",
                "Con actividad financiera, la regla del consejo lleva la mayoría calificada de vinculados (Ley 454, art. 61)."));
        if (reglas.RelatedPartyNumerator is not { } n || reglas.RelatedPartyDenominator is not { } d) return Result.Success();

        var limite = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.VinculadosFraccionCalificada,
            reglas.ValidFrom, null, ct);
        if (limite.IsFailure) return Result.Failure(limite.Error);
        if (limite.Value.Valor is not { } valor) return Result.Success();

        var propuesta = new GoverningBodyRuleVersion { RelatedPartyNumerator = n, RelatedPartyDenominator = d };
        return propuesta.EsMenosEstrictaQue(valor)
            ? Result.Failure(ErroresDeParametrosDeCartera.MenosEstrictoQueLaLey("relatedPartyFraction",
                string.Create(CultureInfo.InvariantCulture, $"{n}/{d}"), CatalogoDeLimitesLegales.VinculadosFraccionCalificada, valor, limite.Value.Fuente))
            : Result.Success();
    }

    /// <summary>
    /// El grado de parentesco de un vinculado pariente frente al límite nacional vigente en <paramref name="fecha"/>: por encima,
    /// <c>Lending.RelatedParty.KinshipDegreeExceeded</c>. Sin grado no hay nada que comparar.
    /// </summary>
    public async Task<Result> ValidarGradoAsync(byte? grado, DateOnly fecha, CancellationToken ct = default)
    {
        if (grado is not { } g) return Result.Success();
        var limite = await valoresNacionales.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.VinculadosGradoParentesco, fecha, null, ct);
        if (limite.IsFailure) return Result.Failure(limite.Error);
        return limite.Value.Valor is { } maximo && g > maximo
            ? Result.Failure(ErroresDeGobierno.GradoFueraDelLimite(g, maximo, CatalogoDeLimitesLegales.VinculadosGradoParentesco, limite.Value.Fuente))
            : Result.Success();
    }
}
