using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Companies.Commands.CreateCompany;

public record CreateCompanyCommand : IRequest<Result<Guid>>
{
    /// <summary>Código alfanumérico de la cooperativa (hasta 10); se guarda en LegacyCode.</summary>
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? City { get; init; }
    public string? Department { get; init; }
}

public class CreateCompanyCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCompanyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateCompanyCommand request, CancellationToken ct)
    {
        // TaxId unico
        var duplicate = await context.Companies.AsNoTracking()
            .AnyAsync(c => c.TaxId == request.TaxId && !c.IsDeleted, ct);
        if (duplicate)
            return Result.Failure<Guid>(new Error("Company.TaxIdDuplicate",
                $"Ya existe una empresa con NIT '{request.TaxId}'."));

        var codigo = CodigoDeCatalogo.Normalizar(request.Code);
        if (codigo is not null)
        {
            var repetido = await context.Companies.AsNoTracking()
                .FirstOrDefaultAsync(e => e.LegacyCode == codigo && !e.IsDeleted, ct);
            if (repetido is not null)
                return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("una empresa", codigo, repetido.Name));
        }

        var entity = new Company
        {
            LegacyCode = codigo,
            Name = request.Name,
            ShortName = request.ShortName,
            TaxId = request.TaxId,
            TaxIdCheckDigit = request.TaxIdCheckDigit,
            Address = request.Address,
            Phone = request.Phone,
            City = request.City,
            Department = request.Department,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Companies.Add(entity);
        await context.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }
}

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage($"El código no puede superar {CodigoDeCatalogo.LargoCorto} caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron)
            .When(x => !string.IsNullOrWhiteSpace(x.Code));

        RuleFor(x => x.Name).NotEmpty().WithMessage("Nombre obligatorio.").MaximumLength(120);
        RuleFor(x => x.ShortName).MaximumLength(60);
        RuleFor(x => x.TaxId).NotEmpty().WithMessage("NIT/Documento obligatorio.").MaximumLength(20);
        RuleFor(x => x.TaxIdCheckDigit).MaximumLength(2);
        RuleFor(x => x.Address).MaximumLength(80);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.City).MaximumLength(40);
        RuleFor(x => x.Department).MaximumLength(40);
    }
}
