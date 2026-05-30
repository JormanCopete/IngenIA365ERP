using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;

namespace IngenIA365ERP.Application.Core.Committees.Commands.CreateCommittee;

public record CreateCommitteeCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? CommitteeType { get; init; }
}

public class CreateCommitteeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCommitteeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCommitteeCommand request, CancellationToken ct)
    {
        var entity = new Committee
        {
            Name = request.Name,
            ShortName = request.ShortName,
            CommitteeType = request.CommitteeType,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Committees.Add(entity);
        await context.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }
}

public class CreateCommitteeCommandValidator : AbstractValidator<CreateCommitteeCommand>
{
    public CreateCommitteeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Nombre obligatorio.").MaximumLength(80);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.CommitteeType).MaximumLength(2);
    }
}
