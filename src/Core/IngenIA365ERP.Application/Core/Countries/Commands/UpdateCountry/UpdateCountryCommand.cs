using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Countries.Commands.UpdateCountry;

public record UpdateCountryCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
}

public class UpdateCountryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCountryCommand, Result>
{
    public async Task<Result> Handle(UpdateCountryCommand request, CancellationToken ct)
    {
        var entity = await context.Countries
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Country.NotFound", "Pais no encontrado."));

        if (entity.Name != request.Name)
        {
            var duplicate = await context.Countries.AsNoTracking()
                .AnyAsync(c => c.Name == request.Name && c.Id != entity.Id && !c.IsDeleted, ct);
            if (duplicate)
                return Result.Failure(new Error("Country.NameDuplicate",
                    $"Ya existe otro pais con nombre '{request.Name}'."));
        }

        entity.Name = request.Name;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateCountryCommandValidator : AbstractValidator<UpdateCountryCommand>
{
    public UpdateCountryCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nombre obligatorio.")
            .MaximumLength(100);
    }
}
