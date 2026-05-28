using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Departments.Commands.CreateDepartment;

public record CreateDepartmentCommand : IRequest<Result<Guid>>
{
    public Guid CountryPublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public class CreateDepartmentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDepartmentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateDepartmentCommand request, CancellationToken ct)
    {
        // 1. Resolver Pais
        var country = await context.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.PublicId == request.CountryPublicId && !c.IsDeleted, ct);
        if (country is null)
            return Result.Failure<Guid>(new Error("Department.CountryNotFound", "Pais no encontrado."));

        // 2. Validar Code unico dentro del pais
        var duplicate = await context.Departments.AsNoTracking()
            .AnyAsync(d => d.CountryId == country.Id && d.Code == request.Code && !d.IsDeleted, ct);
        if (duplicate)
            return Result.Failure<Guid>(new Error("Department.CodeDuplicate",
                $"Ya existe un departamento con codigo '{request.Code}' en este pais."));

        var entity = new Department
        {
            CountryId = country.Id,
            Code = request.Code,
            Name = request.Name,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Departments.Add(entity);
        await context.SaveChangesAsync(ct);

        return Result.Success(entity.PublicId);
    }
}

public class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.CountryPublicId).NotEmpty().WithMessage("Pais obligatorio.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Codigo obligatorio.")
            .MaximumLength(10).WithMessage("Codigo no puede superar 10 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nombre obligatorio.")
            .MaximumLength(100).WithMessage("Nombre no puede superar 100 caracteres.");
    }
}
