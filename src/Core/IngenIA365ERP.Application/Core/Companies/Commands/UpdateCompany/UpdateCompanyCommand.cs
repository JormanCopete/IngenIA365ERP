using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Companies.Commands.UpdateCompany;

public record UpdateCompanyCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? City { get; init; }
    public string? Department { get; init; }
}

public class UpdateCompanyCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCompanyCommand, Result>
{
    public async Task<Result> Handle(UpdateCompanyCommand request, CancellationToken ct)
    {
        var entity = await context.Companies
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);
        if (entity is null)
            return Result.Failure(new Error("Company.NotFound", "Empresa no encontrada."));

        if (entity.TaxId != request.TaxId)
        {
            var duplicate = await context.Companies.AsNoTracking()
                .AnyAsync(c => c.TaxId == request.TaxId && c.Id != entity.Id && !c.IsDeleted, ct);
            if (duplicate)
                return Result.Failure(new Error("Company.TaxIdDuplicate",
                    $"Ya existe otra empresa con NIT '{request.TaxId}'."));
        }

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.TaxId = request.TaxId;
        entity.TaxIdCheckDigit = request.TaxIdCheckDigit;
        entity.Address = request.Address;
        entity.Phone = request.Phone;
        entity.City = request.City;
        entity.Department = request.Department;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(20);
        RuleFor(x => x.TaxIdCheckDigit).MaximumLength(2);
        RuleFor(x => x.Address).MaximumLength(80);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.City).MaximumLength(40);
        RuleFor(x => x.Department).MaximumLength(40);
    }
}
