using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Commands.UpdateAssociate;

/// <summary>
/// Actualiza datos del rol asociado. La fila debe existir previamente.
/// Datos personales se editan via UpdatePersonCommand desde la misma UI.
/// </summary>
public record UpdateAssociateCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }

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
    public string? Status { get; init; }

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

public class UpdateAssociateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAssociateCommand, Result>
{
    public async Task<Result> Handle(UpdateAssociateCommand request, CancellationToken ct)
    {
        var entity = await context.Associates
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);
        if (entity is null)
            return Result.Failure(new Error("Associate.NotFound", "Asociado no encontrado."));

        // Lookups
        if (request.EmployerCompanyPublicId.HasValue)
        {
            var ec = await context.EmployerCompanies.AsNoTracking()
                .FirstOrDefaultAsync(e => e.PublicId == request.EmployerCompanyPublicId.Value && !e.IsDeleted, ct);
            entity.EmployerCompanyId = ec?.Id;
        }
        else { entity.EmployerCompanyId = null; }

        if (request.BranchPublicId.HasValue)
        {
            var b = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PublicId == request.BranchPublicId.Value && !x.IsDeleted, ct);
            entity.BranchId = b?.Id;
        }
        else { entity.BranchId = null; }

        if (request.SectionPublicId.HasValue)
        {
            var s = await context.Sections.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PublicId == request.SectionPublicId.Value && !x.IsDeleted, ct);
            entity.SectionId = s?.Id;
        }
        else { entity.SectionId = null; }

        if (request.CommitteePublicId.HasValue)
        {
            var c = await context.Committees.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PublicId == request.CommitteePublicId.Value && !x.IsDeleted, ct);
            entity.CommitteeId = c?.Id;
        }
        else { entity.CommitteeId = null; }

        if (request.DepositBankPublicId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.PublicId == request.DepositBankPublicId.Value && !b.IsDeleted, ct);
            entity.DepositBankId = bank?.Id;
        }
        else { entity.DepositBankId = null; }

        // Aplicar cambios
        entity.JoinDate = request.JoinDate;
        entity.ContributionRate = request.ContributionRate;
        entity.CategoryRating = request.CategoryRating;
        entity.AssociateClass = request.AssociateClass;
        entity.PaymentType = request.PaymentType;
        entity.ContributionPledged = request.ContributionPledged;
        if (!string.IsNullOrWhiteSpace(request.Status)) entity.Status = request.Status;
        entity.ExternalEmployerName = request.ExternalEmployerName;
        entity.ExternalEmploymentStartDate = request.ExternalEmploymentStartDate;
        entity.ExternalSalary = request.ExternalSalary;
        entity.ExternalSalaryType = request.ExternalSalaryType;
        entity.ExternalSeverance = request.ExternalSeverance;
        entity.ExternalSeveranceFund = request.ExternalSeveranceFund;
        entity.DepositBankAccountNumber = request.DepositBankAccountNumber;
        entity.DepositBankAccountType = request.DepositBankAccountType;
        entity.SpouseEmployer = request.SpouseEmployer;
        entity.SpouseSalary = request.SpouseSalary;
        entity.SpousePosition = request.SpousePosition;
        entity.SpouseProfession = request.SpouseProfession;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdateAssociateCommandValidator : AbstractValidator<UpdateAssociateCommand>
{
    public UpdateAssociateCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.ContributionRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExternalSalary).GreaterThanOrEqualTo(0);
    }
}
