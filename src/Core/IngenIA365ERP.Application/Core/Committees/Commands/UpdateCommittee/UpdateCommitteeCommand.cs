using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Committees.Commands.UpdateCommittee;

public record UpdateCommitteeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CommitteeType { get; init; }
}

public class UpdateCommitteeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCommitteeCommand, Result>
{
    public async Task<Result> Handle(UpdateCommitteeCommand request, CancellationToken ct)
    {
        var entity = await context.Committees
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);
        if (entity is null)
            return Result.Failure(new Error("Committee.NotFound", "Comite no encontrado."));

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.CommitteeType = request.CommitteeType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateCommitteeCommandValidator : AbstractValidator<UpdateCommitteeCommand>
{
    public UpdateCommitteeCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.CommitteeType).MaximumLength(2);
    }
}
