using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Settlements;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;

/// <summary>
/// Quién quedó fuera de una prima ya calculada y por qué (<c>SalarioIntegral</c>, <c>AprendizLectiva</c>,
/// <c>Pasante</c>, <c>YaPagadaEnDefinitiva</c>, <c>SinDiasEnElSemestre</c>). La corrida no guarda a los
/// excluidos —sólo tiene filas de quien sí liquidó—, así que la pestaña «Excluidos» los vuelve a derivar
/// con el mismo cargador y el mismo motor puro, sin persistir nada: es una lectura (Principio XI). Si la
/// ficha cambió después del cálculo (un aprendiz que pasó a práctica), la lista lo refleja y el
/// recálculo lo incorpora.
/// </summary>
public sealed record GetServiceBonusExclusionsQuery(Guid RunPublicId) : IRequest<Result<IReadOnlyList<ExcludedEmployeeDto>>>;

public sealed class GetServiceBonusExclusionsQueryValidator : AbstractValidator<GetServiceBonusExclusionsQuery>
{
    public GetServiceBonusExclusionsQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty().WithMessage("La corrida es obligatoria.");
}

public sealed class GetServiceBonusExclusionsQueryHandler(IApplicationDbContext db, SettlementInputLoader loader, ILogger<GetServiceBonusExclusionsQueryHandler> logger)
    : IRequestHandler<GetServiceBonusExclusionsQuery, Result<IReadOnlyList<ExcludedEmployeeDto>>>
{
    public async Task<Result<IReadOnlyList<ExcludedEmployeeDto>>> Handle(GetServiceBonusExclusionsQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<IReadOnlyList<ExcludedEmployeeDto>>(SettlementErrors.RunNotFound);
        if (run.Kind != PayrollRunKind.ServiceBonus) return Result.Failure<IReadOnlyList<ExcludedEmployeeDto>>(SettlementErrors.KindMismatch(run.Kind, PayrollRunKind.ServiceBonus));
        if (run.Year is not { } year || run.Semester is not { } semester) return Result.Success<IReadOnlyList<ExcludedEmployeeDto>>([]);

        var batch = await loader.LoadAsync(SettlementLoadRequest.Prima(year, semester), ct);
        if (batch.MissingRequiredParameters.Count > 0) return Result.Success<IReadOnlyList<ExcludedEmployeeDto>>(batch.Excluded);

        var engine = new SettlementCalculationEngine();
        var excluidos = new List<ExcludedEmployeeDto>(batch.Excluded);
        foreach (var e in batch.Employees)
        {
            try
            {
                var resultado = engine.Calculate(e.Input);
                if (resultado.Excluded)
                    excluidos.Add(new ExcludedEmployeeDto(e.Employee.PublicId, e.FullName, resultado.ExclusionReasonCode!, resultado.ExclusionReason ?? resultado.ExclusionReasonCode!));
            }
            catch (CalculationRefusedException ex)
            {
                // Una negativa del motor no es una exclusión: la corrida la muestra como bloqueo. Queda en el log, no en silencio.
                logger.LogWarning(ex, "Al derivar los excluidos de la prima {Run}, el motor se negó a liquidar a {Employee}", run.PublicId, e.FullName);
            }
        }
        return Result.Success<IReadOnlyList<ExcludedEmployeeDto>>(excluidos.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
    }
}
