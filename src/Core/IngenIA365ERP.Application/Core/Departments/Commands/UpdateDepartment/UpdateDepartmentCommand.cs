using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Departments.Commands.UpdateDepartment;

public record UpdateDepartmentCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public Guid CountryPublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public class UpdateDepartmentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDepartmentCommand, Result>
{
    public async Task<Result> Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var entity = await context.Departments
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (entity is null)
            return Result.Failure(new Error("Department.NotFound", "Departamento no encontrado."));

        // Resolver Pais
        var country = await context.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.CountryPublicId && !c.IsDeleted, ct);
        if (country is null)
            return Result.Failure(new Error("Department.CountryNotFound", "Pais no encontrado."));

        // Si cambio Country o Code, validar unicidad
        if (entity.CountryId != country.Id || entity.Code != request.Code)
        {
            var duplicate = await context.Departments.AsNoTracking()
                .AnyAsync(d => d.CountryId == country.Id
                            && d.Code == request.Code
                            && d.Id != entity.Id
                            && !d.IsDeleted, ct);
            if (duplicate)
                return Result.Failure(new Error("Department.CodeDuplicate",
                    $"Ya existe otro departamento con codigo '{request.Code}' en este pais."));
        }

        entity.CountryId = country.Id;
        entity.Code = request.Code;
        entity.Name = request.Name;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.CountryPublicId).NotEmpty().WithMessage("Pais obligatorio.");
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
