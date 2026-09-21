using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.ElectronicPayroll;
using IngenIA365ERP.Domain.Payroll.Pila;
using IngenIA365ERP.Domain.Payroll.Settlements;
using IngenIA365ERP.Domain.Payroll.Withholding;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.LegalParameters;

/// <summary>Los procesos que tienen su propia lista de parámetros requeridos (research R4).</summary>
public enum LegalParameterProcess
{
    /// <summary>La nómina ordinaria (<see cref="LegalParameterCodes.Required"/>).</summary>
    Ordinary = 0,

    /// <summary>Prima, cesantías e intereses, vacaciones y definitiva (<see cref="SettlementParameterCodes.Required"/>).</summary>
    Settlements = 1,

    /// <summary>Planilla de aportes (<see cref="PilaParameterCodes.Required"/>).</summary>
    Pila = 2,

    /// <summary>Porcentaje fijo del procedimiento 2 (<see cref="WithholdingRateParameterCodes.Required"/>).</summary>
    WithholdingRates = 3,

    /// <summary>Nómina electrónica (<see cref="ElectronicPayrollParameterCodes.Required"/>).</summary>
    ElectronicPayroll = 4,
}

/// <summary>Un código requerido sin vigencia a la fecha: qué es y de qué norma sale, para que la contadora sepa qué registrar.</summary>
public sealed record MissingLegalParameterDto(string Code, string Description, string? Source);

public sealed record MissingLegalParametersDto(string Process, DateTime AsOf, IReadOnlyList<MissingLegalParameterDto> Missing);

/// <summary>
/// Feature 010 (T006, FR-003, R4): qué parámetros legales le faltan a un proceso a una fecha. Es
/// la misma comprobación que hace cada cálculo (<c>ParameterSet.Missing</c> sobre la lista
/// <c>Required</c> del proceso) pero por adelantado, para la pantalla de parámetros: la nómina
/// ordinaria de una cooperativa en producción no se niega porque falte un código de prestaciones,
/// pero la prima de diciembre sí, y conviene saberlo en noviembre. La descripción y la norma
/// salen de la versión más reciente del código si alguna vez existió; si nunca se sembró, del
/// catálogo de la semilla no se puede leer desde aquí y se dice «sin registrar».
/// </summary>
public sealed record GetMissingLegalParametersQuery(LegalParameterProcess Process, DateTime? AsOf = null) : IRequest<Result<MissingLegalParametersDto>>;

public sealed class GetMissingLegalParametersQueryValidator : AbstractValidator<GetMissingLegalParametersQuery>
{
    public GetMissingLegalParametersQueryValidator()
    {
        RuleFor(x => x.Process).IsInEnum().WithMessage("El proceso es Ordinary, Settlements, Pila, WithholdingRates o ElectronicPayroll.");
    }
}

public sealed class GetMissingLegalParametersQueryHandler(IApplicationDbContext db, IDateTimeService clock)
    : IRequestHandler<GetMissingLegalParametersQuery, Result<MissingLegalParametersDto>>
{
    public static IReadOnlyList<string> RequeridosDe(LegalParameterProcess process) => process switch
    {
        LegalParameterProcess.Ordinary => LegalParameterCodes.Required,
        LegalParameterProcess.Settlements => SettlementParameterCodes.Required,
        LegalParameterProcess.Pila => PilaParameterCodes.Required,
        LegalParameterProcess.WithholdingRates => WithholdingRateParameterCodes.Required,
        LegalParameterProcess.ElectronicPayroll => ElectronicPayrollParameterCodes.Required,
        _ => [],
    };

    public async Task<Result<MissingLegalParametersDto>> Handle(GetMissingLegalParametersQuery request, CancellationToken ct)
    {
        var asOf = (request.AsOf ?? clock.UtcNow).Date;
        var requeridos = RequeridosDe(request.Process);
        var codigos = requeridos.ToList();
        var versiones = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => codigos.Contains(p.Code))
            .ToListAsync(ct);

        var faltantes = new ParameterSet(versiones, asOf).Missing(requeridos);

        var lista = faltantes.Select(code =>
        {
            var ultima = versiones.Where(v => v.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            return new MissingLegalParameterDto(code,
                ultima is null ? "Sin registrar: nunca se sembró ni se creó en esta cooperativa. Reaplique la semilla de nómina o créelo." : $"{ultima.Name} (última vigencia desde {ultima.ValidFrom:dd/MM/yyyy}{(ultima.ValidTo is { } h ? $" hasta {h:dd/MM/yyyy}" : string.Empty)})",
                ultima?.Source);
        }).ToList();

        return Result.Success(new MissingLegalParametersDto(request.Process.ToString(), asOf, lista));
    }
}
