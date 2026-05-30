using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Commands.RegisterAssociate;

/// <summary>
/// Asigna el rol Asociado a una Person existente. Crea fila en COR_Associates
/// y marca Person.IsAssociate = true.
/// </summary>
public record RegisterAssociateCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }

    // Afiliacion
    public DateOnly? JoinDate { get; init; }
    public decimal ContributionRate { get; init; }
    public Guid? EmployerCompanyPublicId { get; init; }
    public Guid? BranchPublicId { get; init; }
    public Guid? SectionPublicId { get; init; }
    public Guid? CommitteePublicId { get; init; }
    public string? CategoryRating { get; init; }
    public string? AssociateClass { get; init; }
    public string? PaymentType { get; init; }
    public decimal ContributionPledged { get; init; }

    // Empleo externo
    public string? ExternalEmployerName { get; init; }
    public DateOnly? ExternalEmploymentStartDate { get; init; }
    public decimal ExternalSalary { get; init; }
    public string? ExternalSalaryType { get; init; }
    public decimal ExternalSeverance { get; init; }
    public string? ExternalSeveranceFund { get; init; }

    // Banca deposito
    public Guid? DepositBankPublicId { get; init; }
    public string? DepositBankAccountNumber { get; init; }
    public string? DepositBankAccountType { get; init; }

    // Conyuge laboral
    public string? SpouseEmployer { get; init; }
    public decimal SpouseSalary { get; init; }
    public string? SpousePosition { get; init; }
    public string? SpouseProfession { get; init; }
}

public class RegisterAssociateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RegisterAssociateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterAssociateCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure<Guid>(new Error("Associate.PersonNotFound",
                "Persona no encontrada."));

        var existing = await context.Associates.AsNoTracking()
            .AnyAsync(a => a.PersonId == person.Id && !a.IsDeleted, ct);
        if (existing)
            return Result.Failure<Guid>(new Error("Associate.AlreadyExists",
                "Esta persona ya esta registrada como asociado."));

        // Lookups
        int? employerCompanyId = null;
        if (request.EmployerCompanyPublicId.HasValue)
        {
            var ec = await context.EmployerCompanies.AsNoTracking()
                .FirstOrDefaultAsync(e => e.PublicId == request.EmployerCompanyPublicId.Value && !e.IsDeleted, ct);
            employerCompanyId = ec?.Id;
        }
        int? branchId = await ResolveBranchId(request.BranchPublicId, ct);
        int? sectionId = await ResolveSectionId(request.SectionPublicId, ct);
        int? committeeId = await ResolveCommitteeId(request.CommitteePublicId, ct);

        int? depositBankId = null;
        if (request.DepositBankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.DepositBankPublicId.Value && !b.IsDeleted, ct);
            depositBankId = bank?.Id;
        }

        var entity = new Associate
        {
            PersonId = person.Id,
            JoinDate = request.JoinDate,
            ContributionRate = request.ContributionRate,
            EmployerCompanyId = employerCompanyId,
            BranchId = branchId,
            SectionId = sectionId,
            CommitteeId = committeeId,
            CategoryRating = request.CategoryRating,
            AssociateClass = request.AssociateClass,
            PaymentType = request.PaymentType,
            ContributionPledged = request.ContributionPledged,
            Status = "A",
            ExternalEmployerName = request.ExternalEmployerName,
            ExternalEmploymentStartDate = request.ExternalEmploymentStartDate,
            ExternalSalary = request.ExternalSalary,
            ExternalSalaryType = request.ExternalSalaryType,
            ExternalSeverance = request.ExternalSeverance,
            ExternalSeveranceFund = request.ExternalSeveranceFund,
            DepositBankId = depositBankId,
            DepositBankAccountNumber = request.DepositBankAccountNumber,
            DepositBankAccountType = request.DepositBankAccountType,
            SpouseEmployer = request.SpouseEmployer,
            SpouseSalary = request.SpouseSalary,
            SpousePosition = request.SpousePosition,
            SpouseProfession = request.SpouseProfession,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.Associates.Add(entity);

        person.IsAssociate = true;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success(entity.PublicId);
    }

    private async Task<int?> ResolveBranchId(Guid? publicId, CancellationToken ct)
    {
        if (!publicId.HasValue) return null;
        var b = await context.Branches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId.Value && !x.IsDeleted, ct);
        return b?.Id;
    }

    private async Task<int?> ResolveSectionId(Guid? publicId, CancellationToken ct)
    {
        if (!publicId.HasValue) return null;
        var s = await context.Sections.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId.Value && !x.IsDeleted, ct);
        return s?.Id;
    }

    private async Task<int?> ResolveCommitteeId(Guid? publicId, CancellationToken ct)
    {
        if (!publicId.HasValue) return null;
        var c = await context.Committees.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PublicId == publicId.Value && !x.IsDeleted, ct);
        return c?.Id;
    }
}

public class RegisterAssociateCommandValidator : AbstractValidator<RegisterAssociateCommand>
{
    public RegisterAssociateCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Persona requerida.");
        RuleFor(x => x.ContributionRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExternalSalary).GreaterThanOrEqualTo(0);
    }
}
