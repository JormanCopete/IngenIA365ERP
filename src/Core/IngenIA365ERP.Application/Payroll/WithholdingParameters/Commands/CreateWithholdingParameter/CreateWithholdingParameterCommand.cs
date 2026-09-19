using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.CreateWithholdingParameter;

/// <summary>
/// Un tramo de la tabla de retención propia de un plan de nómina (2026-09-19: antes el tramo
/// colgaba de la «empresa nómina» del legado y la liquidación no lo miraba). Tramos en UVT,
/// tarifa marginal en porcentaje, UVT fijas; «hasta» en 0 = en adelante.
/// </summary>
public record CreateWithholdingParameterCommand : IRequest<Result<Guid>>, ITramoDeRetencion
{
    public Guid PayrollPlanPublicId { get; init; }
    public int UvtRangeStart { get; init; }
    public int UvtRangeEnd { get; init; }
    public decimal Rate { get; init; }
    public int AdditionalUvt { get; init; }
}

public class CreateWithholdingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateWithholdingParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateWithholdingParameterCommand request, CancellationToken cancellationToken)
    {
        var plan = await context.PayrollPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayrollPlanPublicId && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return Result.Failure<Guid>(TramoDeRetencion.PlanNoEncontrado);

        var solapado = await TramoDeRetencion.SolapadoAsync(context, plan.Id, request.UvtRangeStart, request.UvtRangeEnd, null, cancellationToken);
        if (solapado is not null) return Result.Failure<Guid>(solapado);

        var entity = new WithholdingParameter
        {
            PayrollCompanyId = 1,
            PayrollPlanId = plan.Id,
            UvtRangeStart = request.UvtRangeStart,
            UvtRangeEnd = request.UvtRangeEnd,
            Rate = request.Rate,
            AdditionalUvt = request.AdditionalUvt,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.WithholdingParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateWithholdingParameterCommandValidator : AbstractValidator<CreateWithholdingParameterCommand>
{
    public CreateWithholdingParameterCommandValidator()
    {
        RuleFor(x => x.PayrollPlanPublicId).NotEmpty().WithMessage("Elija el plan de nómina.");
        TramoDeRetencion.Reglas(this);
    }
}

/// <summary>Los cuatro datos de un tramo, para validar igual al crear y al editar.</summary>
public interface ITramoDeRetencion
{
    int UvtRangeStart { get; }
    int UvtRangeEnd { get; }
    decimal Rate { get; }
    int AdditionalUvt { get; }
}

/// <summary>Lo que comparten crear y editar un tramo: reglas del rango y el reparo por solapamiento.</summary>
public static class TramoDeRetencion
{
    public static readonly Error PlanNoEncontrado = new("Payroll.PlanNotFound", "El plan de nómina no existe.");

    public static void Reglas<T>(AbstractValidator<T> v) where T : ITramoDeRetencion
    {
        v.RuleFor(x => x.UvtRangeStart).GreaterThanOrEqualTo(0).WithMessage("El inicio del tramo en UVT no puede ser negativo.");
        v.RuleFor(x => x.UvtRangeEnd)
            .Must((x, fin) => fin == 0 || fin >= x.UvtRangeStart)
            .WithMessage("«UVT hasta» es mayor o igual que «UVT desde», o 0 para «en adelante».");
        v.RuleFor(x => x.Rate).InclusiveBetween(0m, 100m).WithMessage("La tarifa es un porcentaje entre 0 y 100.");
        v.RuleFor(x => x.AdditionalUvt).GreaterThanOrEqualTo(0).WithMessage("Las UVT adicionales no pueden ser negativas.");
    }

    /// <summary>
    /// Otro tramo del mismo plan que se cruza con [desde, hasta) (hasta 0 = abierto), si lo hay. Los tramos
    /// son semiabiertos como los de la tabla legal (0–95, 95–150…): el fin de uno es el inicio del siguiente.
    /// </summary>
    public static async Task<Error?> SolapadoAsync(IApplicationDbContext db, int planId, int desde, int hasta, Guid? excepto, CancellationToken ct)
    {
        var tramos = await db.WithholdingParameters.AsNoTracking()
            .Where(t => t.PayrollPlanId == planId && !t.IsDeleted && (excepto == null || t.PublicId != excepto))
            .Select(t => new { t.UvtRangeStart, t.UvtRangeEnd })
            .ToListAsync(ct);
        var finNuevo = hasta <= 0 ? int.MaxValue : hasta;
        var cruce = tramos.FirstOrDefault(t => desde < (t.UvtRangeEnd <= 0 ? int.MaxValue : t.UvtRangeEnd) && t.UvtRangeStart < finNuevo);
        return cruce is null
            ? null
            : new Error("Payroll.WithholdingRange.Overlap",
                $"El tramo se cruza con el que va de {cruce.UvtRangeStart} a {(cruce.UvtRangeEnd <= 0 ? "en adelante" : cruce.UvtRangeEnd.ToString())} UVT del mismo plan.");
    }
}
