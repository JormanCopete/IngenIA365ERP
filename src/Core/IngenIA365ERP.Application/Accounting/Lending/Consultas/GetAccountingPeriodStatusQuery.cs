using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>
/// El estado contable de una fecha para Cartera (feature 013, T0649; contracts/contabilidad.md §8 y §16.1), detrás de
/// <see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>: si la contabilidad está iniciada, el estado del mes de la fecha (abierto,
/// cerrado o inexistente) y el primer día del primer mes abierto desde ella. Un mes que no existe no se salta: la fecha contable queda en él y
/// el consumidor reintenta. Sin contabilidad iniciada, «no iniciada» y sin primer mes. Lectura en proceso sobre la misma base: si falla, falla
/// la operación de Cartera (Principio IX). Es la misma lectura que hacía <c>ContabilidadParaCarteraProvisional</c>. (nuevo)
/// </summary>
public sealed record GetAccountingPeriodStatusQuery(DateOnly Date) : IRequest<Result<EstadoDelPeriodoDto>>;

public sealed class GetAccountingPeriodStatusQueryValidator : AbstractValidator<GetAccountingPeriodStatusQuery>
{
    public GetAccountingPeriodStatusQueryValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
    }
}

public sealed class GetAccountingPeriodStatusQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountingPeriodStatusQuery, Result<EstadoDelPeriodoDto>>
{
    /// <summary>El tope de meses que recorre la búsqueda del primer mes abierto (diez años; nunca se llega en la práctica).</summary>
    private const int MesesMaximos = 120;

    public async Task<Result<EstadoDelPeriodoDto>> Handle(GetAccountingPeriodStatusQuery request, CancellationToken ct)
    {
        var fecha = request.Date;
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Success(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));

        var inicioDelMes = new DateOnly(fecha.Year, fecha.Month, 1);
        var periodos = await db.AccountingPeriods.AsNoTracking()
            .Where(p => !p.IsDeleted && p.EndDate >= inicioDelMes)
            .OrderBy(p => p.StartDate)
            .Select(p => new { p.StartDate, p.EndDate, p.Status })
            .ToListAsync(ct);
        var delMes = periodos.FirstOrDefault(p => p.StartDate <= fecha && p.EndDate >= fecha);
        var estado = delMes is null ? EstadoDePeriodoContable.NotFound
            : delMes.Status == PeriodStatus.Open ? EstadoDePeriodoContable.Open
            : EstadoDePeriodoContable.Closed;

        DateOnly? primero = null;
        var mes = inicioDelMes;
        for (var i = 0; i < MesesMaximos && primero is null; i++, mes = mes.AddMonths(1))
        {
            var periodo = periodos.FirstOrDefault(p => p.StartDate <= mes && p.EndDate >= mes);
            if (periodo is null || periodo.Status == PeriodStatus.Open) primero = mes;
        }
        return Result.Success(new EstadoDelPeriodoDto(true, estado, primero));
    }
}
