using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.CulturalActivities.Commands.CreateCulturalActivity;

public record CreateCulturalActivityCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public Guid? CommitteePublicId { get; init; }
}

public class CreateCulturalActivityCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCulturalActivityCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCulturalActivityCommand request,
        CancellationToken cancellationToken)
    {
        int? committeeId = null;
        if (request.CommitteePublicId.HasValue)
        {
            var committee = await context.Committees
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CommitteePublicId.Value && !c.IsDeleted, cancellationToken);

            if (committee is null)
                return Result.Failure<Guid>(new Error("CulturalActivity.CommitteeNotFound", "Committee not found."));

            committeeId = committee.Id;
        }

        var entity = new CulturalActivity
        {
            Name = request.Name,
            ShortName = request.ShortName,
            CommitteeId = committeeId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CulturalActivities.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCulturalActivityCommandValidator : AbstractValidator<CreateCulturalActivityCommand>
{
    public CreateCulturalActivityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(80).WithMessage("Name must not exceed 80 characters.");

        RuleFor(x => x.ShortName)
            .MaximumLength(40).WithMessage("Short name must not exceed 40 characters.");
    }
}
