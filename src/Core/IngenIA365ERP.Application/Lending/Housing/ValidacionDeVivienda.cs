using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Housing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Housing;

/// <summary>
/// Las reglas de la Ley 546 de 1999 sobre una solicitud de vivienda y su desembolso (feature 013, K6b, T1044, T1055; FR-024, D18), con los datos de la
/// solicitud: el valor del inmueble es el de sus hipotecas registradas; los ingresos familiares, los ingresos mensuales del solicitante y sus
/// codeudores; si es de interés social lo dice la solicitud (<c>IsSocialHousing</c>); la primera cuota, la de la simulación llevada a un mes. Una
/// solicitud que no es de vivienda pasa sin mirar nada. (nuevo)
/// </summary>
public static class ValidacionDeVivienda
{
    /// <summary>¿La simulación es de un crédito de vivienda (modalidad o combinación con tope de vivienda)?</summary>
    public static bool EsVivienda(SimulacionCalculada simulacion)
    {
        ArgumentNullException.ThrowIfNull(simulacion);
        return ReglasDeLineaDeCredito.EsVivienda(simulacion.Version.Version.ModalityCode.Trim().ToUpperInvariant(), simulacion.Combinacion);
    }

    /// <summary>
    /// Plazo, porcentaje financiable (general o VIS) y relación cuota-ingreso de una solicitud de vivienda por <paramref name="monto"/> a la fecha;
    /// el primer problema como error del contrato (422 con el límite y su fuente). Éxito si no es de vivienda.
    /// </summary>
    public static async Task<Result> SolicitudAsync(IApplicationDbContext db, LectorDeVivienda lector, CreditApplication s, SimulacionCalculada simulacion,
        decimal monto, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!EsVivienda(simulacion)) return Result.Success();
        var limites = await lector.LimitesAsync(fecha, ct);
        if (limites.IsFailure) return Result.Failure(limites.Error);
        var inmueble = await db.Collaterals.AsNoTracking()
            .Where(c => c.ApplicationId == s.Id && !c.IsDeleted && c.Kind == CollateralKind.Mortgage)
            .SumAsync(c => (decimal?)c.Value, ct) ?? 0m;
        var ingresos = await db.CreditApplicationParties.AsNoTracking()
            .Where(p => p.ApplicationId == s.Id && !p.IsDeleted && (p.Role == CreditPartyRole.Applicant || p.Role == CreditPartyRole.CoDebtor))
            .SumAsync(p => p.MonthlyIncome, ct) ?? 0m;
        var problemas = ReglasDeVivienda.ValidarSolicitud(new SolicitudDeVivienda(monto, simulacion.PlazoMeses, inmueble > 0m ? inmueble : null,
            s.IsSocialHousing ?? false, ingresos > 0m ? ingresos : null, simulacion.CuotaMensual), limites.Value);
        return problemas.Count == 0 ? Result.Success() : Result.Failure(LectorDeVivienda.Error(problemas[0]));
    }

    /// <summary>
    /// La hipoteca de primer grado que exige el desembolso de un crédito de vivienda, entre las garantías constituidas o vigentes de la solicitud; el
    /// problema como error del contrato, o nulo si la tiene o no es de vivienda.
    /// </summary>
    public static async Task<Error?> GarantiasAsync(IApplicationDbContext db, CreditApplication s, SimulacionCalculada simulacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!EsVivienda(simulacion)) return null;
        var garantias = await db.Collaterals.AsNoTracking()
            .Where(c => c.ApplicationId == s.Id && !c.IsDeleted && (c.Status == CollateralStatus.Constituted || c.Status == CollateralStatus.Valid))
            .Select(c => new { c.Kind, c.MortgageRank })
            .ToListAsync(ct);
        return ReglasDeVivienda.ValidarGarantias(garantias.Select(g => new GarantiaDeVivienda(g.Kind, g.MortgageRank))) is { } problema
            ? LectorDeVivienda.Error(problema)
            : null;
    }
}
