using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Commands.CreateSalesperson;

public record CreateSalespersonCommand : IRequest<Result<Guid>>
{
    public string IdNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public int? CityId { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

public class CreateSalespersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSalespersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSalespersonCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new Salesperson
        {
            IdNumber = request.IdNumber,
            Name = request.Name,
            LastName = request.LastName,
            Address = request.Address,
            Phone = request.Phone,
            Mobile = request.Mobile,
            CityId = request.CityId,
            SalespersonType = request.SalespersonType,
            AppliesCommission = request.AppliesCommission,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Salespeople.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSalespersonCommandValidator : AbstractValidator<CreateSalespersonCommand>
{
    public CreateSalespersonCommandValidator()
    {
        RuleFor(x => x.IdNumber)
            .NotEmpty().WithMessage("ID number is required.")
            .MaximumLength(20).WithMessage("ID number must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(100).WithMessage("Address must not exceed 100 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");

        RuleFor(x => x.Mobile)
            .MaximumLength(30).WithMessage("Mobile must not exceed 30 characters.");
    }
}
