using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>
/// Los indicadores de la entidad a una fecha (feature 013, T0649; contracts/contabilidad.md §15; T59, C33, C78), detrás de
/// <see cref="IContabilidadParaCartera.IndicadoresDeLaEntidadAsync"/>:
/// <list type="bullet">
///   <item>resultado del ejercicio: el saldo de las clases 4 a 7 desde el 1 de enero de la fecha hasta ella, sin el comprobante de cierre
///         <c>CI</c> de ese ejercicio, por <see cref="MovimientosContables"/> (créditos menos débitos: positivo es excedente);</item>
///   <item>pérdidas acumuladas: el saldo de las cuentas de la <b>clase 3 de naturaleza débito</b> hasta la fecha (C78), elegidas por clase y
///         naturaleza y nunca por código (<c>LaContabilidadNoTieneCuentasEnCodigo</c>);</item>
///   <item>patrimonio técnico y relación de solvencia: el último <c>RegulatoryIndicator</c> no reemplazado con corte ≤ fecha, con su corte,
///         su fuente y su unidad, <b>sin juzgar si sigue vigente</b>: lo decide Cartera con <c>Contabilidad.IndicadoresVigenciaMeses</c> (C33).</item>
/// </list>
/// Lee con <see cref="AlcanceDeSucursales.SinRestriccion"/>: son cifras de la entidad. Sin contabilidad iniciada, <c>Accounting.NotInitialized</c>.
/// (nuevo)
/// </summary>
public sealed record GetEntityIndicatorsQuery(DateOnly Date) : IRequest<Result<IndicadoresDeLaEntidadDto>>;

public sealed class GetEntityIndicatorsQueryValidator : AbstractValidator<GetEntityIndicatorsQuery>
{
    public GetEntityIndicatorsQueryValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
    }
}

public sealed class GetEntityIndicatorsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetEntityIndicatorsQuery, Result<IndicadoresDeLaEntidadDto>>
{
    /// <summary>Las clases del estado de resultados (§15): ingresos, gastos, costos de ventas y de producción.</summary>
    private static readonly string[] ClasesDeResultado = ["4", "5", "6", "7"];

    /// <summary>La clase del patrimonio, de cuyas cuentas de naturaleza débito salen las pérdidas (C78).</summary>
    private const string ClaseDelPatrimonio = "3";

    public const string UnidadPesos = "Pesos";
    public const string UnidadFraccion = "Fraccion";

    public async Task<Result<IndicadoresDeLaEntidadDto>> Handle(GetEntityIndicatorsQuery request, CancellationToken ct)
    {
        var fecha = request.Date;
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Failure<IndicadoresDeLaEntidadDto>(AccountingErrors.NotInitialized);

        var inicio = new DateOnly(fecha.Year, 1, 1);
        var contexto = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, reloj,
            new FiltrosDeInforme { From = inicio, To = fecha }, ct);
        if (contexto.IsFailure) return Result.Failure<IndicadoresDeLaEntidadDto>(contexto.Error);
        var asientos = MovimientosContables.HastaInclusive(MovimientosContables.Base(db, contexto.Value), fecha);

        var resultado = await asientos
            .Where(e => e.Date >= inicio && e.Document!.Kind != DocumentKind.Opening && ClasesDeResultado.Contains(e.Account!.Code.Substring(0, 1)))
            .SumAsync(e => e.Credit - e.Debit, ct);
        var perdidas = await asientos
            .Where(e => e.Account!.Code.StartsWith(ClaseDelPatrimonio) && e.Account.Nature == AccountNature.Debit)
            .SumAsync(e => e.Debit - e.Credit, ct);

        return Result.Success(new IndicadoresDeLaEntidadDto(fecha, resultado, perdidas,
            await UltimoAsync(db, RegulatoryIndicatorKind.TechnicalCapital, fecha, ct),
            await UltimoAsync(db, RegulatoryIndicatorKind.SolvencyRatio, fecha, ct)));
    }

    /// <summary>El último indicador del tipo con corte ≤ fecha que nadie reemplazó (§4.4), o nulo.</summary>
    public static async Task<IndicadorRegistradoDto?> UltimoAsync(IApplicationDbContext db, RegulatoryIndicatorKind tipo, DateOnly fecha, CancellationToken ct)
    {
        var reemplazados = db.RegulatoryIndicators.AsNoTracking().Where(r => r.SupersedesPublicId != null).Select(r => r.SupersedesPublicId!.Value);
        var ultimo = await db.RegulatoryIndicators.AsNoTracking()
            .Where(r => !r.IsDeleted && r.Kind == tipo && r.CutoffDate <= fecha && !reemplazados.Contains(r.PublicId))
            .OrderByDescending(r => r.CutoffDate).ThenByDescending(r => r.Id)
            .Select(r => new { r.PublicId, r.Value, r.CutoffDate, r.Source })
            .FirstOrDefaultAsync(ct);
        return ultimo is null ? null : new IndicadorRegistradoDto(ultimo.PublicId, ultimo.Value, UnidadDe(tipo), ultimo.CutoffDate, ultimo.Source);
    }

    /// <summary>La unidad de cada indicador (§4.4): pesos el patrimonio técnico; fracción la solvencia.</summary>
    public static string UnidadDe(RegulatoryIndicatorKind tipo) => tipo == RegulatoryIndicatorKind.SolvencyRatio ? UnidadFraccion : UnidadPesos;
}
