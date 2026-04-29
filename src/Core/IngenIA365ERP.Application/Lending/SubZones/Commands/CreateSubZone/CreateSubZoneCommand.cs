using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.SubZones.Commands.CreateSubZone;

public record CreateSubZoneCommand : IRequest<Result<Guid>>
{
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
}

public class CreateSubZoneCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSubZoneCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSubZoneCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SubZone
        {
            Code = request.Code,
            Name = request.Name,
            ShortName = request.ShortName ?? string.Empty,
            Address = request.Address ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SubZones.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSubZoneCommandValidator : AbstractValidator<CreateSubZoneCommand>
{
    public CreateSubZoneCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(120).WithMessage("Name must not exceed 120 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(50).WithMessage("Short name must not exceed 50 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(120).WithMessage("Address must not exceed 120 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(120).WithMessage("Phone must not exceed 120 characters.");
    }
}
