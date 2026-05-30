using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Countries.Commands.CreateCountry;

public record CreateCountryCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
}

public class CreateCountryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCountryCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCountryCommand request, CancellationToken ct)
    {
        // Nombre unico (case-insensitive ya que SQL Server por defecto es CI)
        var duplicate = await context.Countries.AsNoTracking()
            .AnyAsync(c => c.Name == request.Name && !c.IsDeleted, ct);
        if (duplicate)
            return Result.Failure<Guid>(new Error("Country.NameDuplicate",
                $"Ya existe un pais con nombre '{request.Name}'."));

        var entity = new Country
        {
            Name = request.Name,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Countries.Add(entity);
        await context.SaveChangesAsync(ct);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCountryCommandValidator : AbstractValidator<CreateCountryCommand>
{
    public CreateCountryCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nombre obligatorio.")
            .MaximumLength(100).WithMessage("Nombre no puede superar 100 caracteres.");
    }
}
