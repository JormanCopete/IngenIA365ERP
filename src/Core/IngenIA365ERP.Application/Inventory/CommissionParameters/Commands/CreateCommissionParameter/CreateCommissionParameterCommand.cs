using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.CreateCommissionParameter;

public record CreateCommissionParameterCommand : IRequest<Result<Guid>>
{
    public int InvoiceTypeId { get; init; }
    public int CommissionGroupId { get; init; }
    public int GroupId { get; init; }
    public decimal SalesRangeStart { get; init; }
    public decimal SalesRangeEnd { get; init; }
    public decimal CommissionRate { get; init; }
}

public class CreateCommissionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCommissionParameterCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCommissionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new CommissionParameter
        {
            InvoiceTypeId = request.InvoiceTypeId,
            CommissionGroupId = request.CommissionGroupId,
            GroupId = request.GroupId,
            SalesRangeStart = request.SalesRangeStart,
            SalesRangeEnd = request.SalesRangeEnd,
            CommissionRate = request.CommissionRate,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CommissionParameters.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCommissionParameterCommandValidator : AbstractValidator<CreateCommissionParameterCommand>
{
    public CreateCommissionParameterCommandValidator()
    {
        RuleFor(x => x.SalesRangeEnd)
            .GreaterThanOrEqualTo(x => x.SalesRangeStart)
            .WithMessage("Sales range end must be greater than or equal to sales range start.");

        RuleFor(x => x.CommissionRate)
            .GreaterThanOrEqualTo(0).WithMessage("Commission rate must be non-negative.");
    }
}
