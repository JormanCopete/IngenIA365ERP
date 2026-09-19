using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.CreateWithholdingParameter;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.UpdateWithholdingParameter;

public record UpdateWithholdingParameterCommand : IRequest<Result>, ITramoDeRetencion
{
    public Guid PublicId { get; init; }
    public Guid PayrollPlanPublicId { get; init; }
    public int UvtRangeStart { get; init; }
    public int UvtRangeEnd { get; init; }
    public decimal Rate { get; init; }
    public int AdditionalUvt { get; init; }
}

public class UpdateWithholdingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWithholdingParameterCommand, Result>
{
    public async Task<Result> Handle(UpdateWithholdingParameterCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingParameters
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);
        if (entity is null)
            return Result.Failure(Error.NotFound);

        var plan = await context.PayrollPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PayrollPlanPublicId && !p.IsDeleted, cancellationToken);
        if (plan is null)
            return Result.Failure(TramoDeRetencion.PlanNoEncontrado);

        var solapado = await TramoDeRetencion.SolapadoAsync(context, plan.Id, request.UvtRangeStart, request.UvtRangeEnd, entity.PublicId, cancellationToken);
        if (solapado is not null) return Result.Failure(solapado);

        entity.PayrollPlanId = plan.Id;
        entity.UvtRangeStart = request.UvtRangeStart;
        entity.UvtRangeEnd = request.UvtRangeEnd;
        entity.Rate = request.Rate;
        entity.AdditionalUvt = request.AdditionalUvt;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public class UpdateWithholdingParameterCommandValidator : AbstractValidator<UpdateWithholdingParameterCommand>
{
    public UpdateWithholdingParameterCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.PayrollPlanPublicId).NotEmpty().WithMessage("Elija el plan de nómina.");
        TramoDeRetencion.Reglas(this);
    }
}
