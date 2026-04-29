using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Cities.Commands.CreateCity;

public record CreateCityCommand : IRequest<Result<Guid>>
{
    public string Name { get; init; } = string.Empty;
    public Guid DepartmentPublicId { get; init; }
}

public class CreateCityCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCityCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCityCommand request,
        CancellationToken cancellationToken)
    {
        var department = await context.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.PublicId == request.DepartmentPublicId && !d.IsDeleted,
                cancellationToken);

        if (department is null)
            return Result.Failure<Guid>(new Error("City.DepartmentNotFound", "Department not found."));

        var entity = new City
        {
            Name = request.Name,
            DepartmentId = department.Id,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Cities.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.DepartmentPublicId)
            .NotEmpty().WithMessage("Department is required.");
    }
}
