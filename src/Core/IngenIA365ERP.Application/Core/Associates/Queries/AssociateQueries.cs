using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Associates.Queries;

/// <summary>
/// DTO de Asociado para listado. Datos personales (nombre, documento, contacto)
/// vienen de COR_People via JOIN. Datos del rol (afiliacion, empleador externo,
/// scoring) vienen de COR_Associates.
/// </summary>
public record AssociateRowDto(
    Guid PublicId,
    Guid PersonPublicId,
    string IdentificationNumber,
    string FullName,
    string? Email,
    string? Phone,
    string? Mobile,
    string? CityName,
    DateOnly? JoinDate,
    string? Status,
    decimal ContributionRate,
    decimal ExternalSalary,
    string? ExternalEmployerName);

public record ListAssociatesQuery : IRequest<Result<PagedList<AssociateRowDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SearchTerm { get; init; }
    public string? Status { get; init; }
}

public class ListAssociatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAssociatesQuery, Result<PagedList<AssociateRowDto>>>
{
    public async Task<Result<PagedList<AssociateRowDto>>> Handle(
        ListAssociatesQuery request, CancellationToken ct)
    {
        var query = from a in context.Associates.AsNoTracking().Where(a => !a.IsDeleted)
                    join p in context.People.AsNoTracking() on a.PersonId equals p.Id
                    where !p.IsDeleted
                    select new { a, p };

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(x => x.a.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(x =>
                x.p.TaxId.Contains(term) ||
                x.p.FirstName.Contains(term) ||
                x.p.LastName.Contains(term) ||
                (x.p.BusinessName != null && x.p.BusinessName.Contains(term)));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.p.LastName).ThenBy(x => x.p.FirstName)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new AssociateRowDto(
                x.a.PublicId,
                x.p.PublicId,
                x.p.TaxId,
                x.p.BusinessName != null && x.p.BusinessName.Length > 0
                    ? x.p.BusinessName
                    : x.p.FirstName + " " + x.p.LastName,
                x.p.Email,
                x.p.Phone1,
                x.p.Mobile,
                x.p.City != null ? x.p.City.Name : null,
                x.a.JoinDate,
                x.a.Status,
                x.a.ContributionRate,
                x.a.ExternalSalary,
                x.a.ExternalEmployerName))
            .ToListAsync(ct);

        return Result.Success(new PagedList<AssociateRowDto>(
            items, totalCount, request.PageNumber, request.PageSize));
    }
}

/// <summary>DTO completo del asociado para edicion.</summary>
public record AssociateEditDto(
    Guid PublicId,
    Guid PersonPublicId,
    string PersonName,
    string IdentificationNumber,
    DateOnly? JoinDate,
    decimal ContributionRate,
    Guid? EmployerCompanyPublicId,
    Guid? BranchPublicId,
    Guid? CostCenterPublicId,
    Guid? SectionPublicId,
    short CutoffDay,
    string? Status,
    string? CategoryRating,
    Guid? AdvisorPublicId,
    Guid? CommitteePublicId,
    string? AssociateClass,
    string? PaymentType,
    decimal ContributionPledged,
    // Empleo externo
    string? ExternalEmployerName,
    DateOnly? ExternalEmploymentStartDate,
    decimal ExternalSalary,
    string? ExternalSalaryType,
    decimal ExternalSeverance,
    string? ExternalSeveranceFund,
    string? ExternalOtherIncomeDescription,
    // Banca deposito
    Guid? DepositBankPublicId,
    string? DepositBankAccountNumber,
    string? DepositBankAccountType,
    // Scoring
    bool AuthCentralRisk,
    decimal CreditLimit,
    decimal PosCardLimit,
    decimal InsuranceRiskRate,
    bool IsSiplaExempt,
    bool SinglePromissoryNote,
    bool PledgesContributions,
    bool InManagement,
    // Estados rol
    bool IsFromGovernment,
    bool IsPublicResourceAdmin,
    bool IsPensioner,
    bool IsInsubordinate,
    bool IsOnVacation,
    bool IsOnUnpaidLeave,
    // Conyuge laboral
    string? SpouseEmployer,
    decimal SpouseSalary,
    string? SpousePosition,
    string? SpouseProfession);

/// <summary>
/// Recupera el rol asociado para una persona dada (si existe).
/// Si la persona no tiene rol asociado, devuelve NotFound.
/// </summary>
public record GetAssociateByPersonIdQuery(Guid PersonPublicId) : IRequest<Result<AssociateEditDto>>;

public class GetAssociateByPersonIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAssociateByPersonIdQuery, Result<AssociateEditDto>>
{
    public async Task<Result<AssociateEditDto>> Handle(GetAssociateByPersonIdQuery request, CancellationToken ct)
    {
        var entity = await context.Associates
            .AsNoTracking()
            .Include(a => a.Person)
            .Include(a => a.EmployerCompany)
            .Include(a => a.Branch)
            .Include(a => a.CostCenter)
            .Include(a => a.Section)
            .Include(a => a.Advisor)
            .Include(a => a.Committee)
            .FirstOrDefaultAsync(a => a.Person.PublicId == request.PersonPublicId && !a.IsDeleted, ct);

        if (entity is null)
            return Result.Failure<AssociateEditDto>(new Error("Associate.NotFound",
                "Esta persona aun no tiene el rol asociado."));

        Guid? depositBankPublicId = null;
        if (entity.DepositBankId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == entity.DepositBankId.Value, ct);
            depositBankPublicId = bank?.PublicId;
        }

        var p = entity.Person;
        return Result.Success(new AssociateEditDto(
            entity.PublicId, p.PublicId,
            p.BusinessName != null && p.BusinessName.Length > 0 ? p.BusinessName : $"{p.FirstName} {p.LastName}",
            p.TaxId,
            entity.JoinDate, entity.ContributionRate,
            entity.EmployerCompany?.PublicId, entity.Branch?.PublicId, entity.CostCenter?.PublicId,
            entity.Section?.PublicId, entity.CutoffDay, entity.Status, entity.CategoryRating,
            entity.Advisor?.PublicId, entity.Committee?.PublicId,
            entity.AssociateClass, entity.PaymentType, entity.ContributionPledged,
            entity.ExternalEmployerName, entity.ExternalEmploymentStartDate,
            entity.ExternalSalary, entity.ExternalSalaryType, entity.ExternalSeverance,
            entity.ExternalSeveranceFund, entity.ExternalOtherIncomeDescription,
            depositBankPublicId, entity.DepositBankAccountNumber, entity.DepositBankAccountType,
            entity.AuthCentralRisk, entity.CreditLimit, entity.PosCardLimit, entity.InsuranceRiskRate,
            entity.IsSiplaExempt, entity.SinglePromissoryNote, entity.PledgesContributions, entity.InManagement,
            entity.IsFromGovernment, entity.IsPublicResourceAdmin, entity.IsPensioner, entity.IsInsubordinate,
            entity.IsOnVacation, entity.IsOnUnpaidLeave,
            entity.SpouseEmployer, entity.SpouseSalary, entity.SpousePosition, entity.SpouseProfession));
    }
}

public record GetAssociateByIdQuery(Guid PublicId) : IRequest<Result<AssociateEditDto>>;

public class GetAssociateByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAssociateByIdQuery, Result<AssociateEditDto>>
{
    public async Task<Result<AssociateEditDto>> Handle(GetAssociateByIdQuery request, CancellationToken ct)
    {
        var entity = await context.Associates
            .AsNoTracking()
            .Include(a => a.Person)
            .Include(a => a.EmployerCompany)
            .Include(a => a.Branch)
            .Include(a => a.CostCenter)
            .Include(a => a.Section)
            .Include(a => a.Advisor)
            .Include(a => a.Committee)
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);

        if (entity is null)
            return Result.Failure<AssociateEditDto>(new Error("Associate.NotFound", "Asociado no encontrado."));

        // Resolve DepositBank PublicId (BankId is int? in Associate)
        Guid? depositBankPublicId = null;
        if (entity.DepositBankId.HasValue)
        {
            var bank = await context.Banks.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == entity.DepositBankId.Value, ct);
            depositBankPublicId = bank?.PublicId;
        }

        var p = entity.Person;
        return Result.Success(new AssociateEditDto(
            entity.PublicId,
            p.PublicId,
            p.BusinessName != null && p.BusinessName.Length > 0 ? p.BusinessName : $"{p.FirstName} {p.LastName}",
            p.TaxId,
            entity.JoinDate,
            entity.ContributionRate,
            entity.EmployerCompany?.PublicId,
            entity.Branch?.PublicId,
            entity.CostCenter?.PublicId,
            entity.Section?.PublicId,
            entity.CutoffDay,
            entity.Status,
            entity.CategoryRating,
            entity.Advisor?.PublicId,
            entity.Committee?.PublicId,
            entity.AssociateClass,
            entity.PaymentType,
            entity.ContributionPledged,
            entity.ExternalEmployerName,
            entity.ExternalEmploymentStartDate,
            entity.ExternalSalary,
            entity.ExternalSalaryType,
            entity.ExternalSeverance,
            entity.ExternalSeveranceFund,
            entity.ExternalOtherIncomeDescription,
            depositBankPublicId,
            entity.DepositBankAccountNumber,
            entity.DepositBankAccountType,
            entity.AuthCentralRisk,
            entity.CreditLimit,
            entity.PosCardLimit,
            entity.InsuranceRiskRate,
            entity.IsSiplaExempt,
            entity.SinglePromissoryNote,
            entity.PledgesContributions,
            entity.InManagement,
            entity.IsFromGovernment,
            entity.IsPublicResourceAdmin,
            entity.IsPensioner,
            entity.IsInsubordinate,
            entity.IsOnVacation,
            entity.IsOnUnpaidLeave,
            entity.SpouseEmployer,
            entity.SpouseSalary,
            entity.SpousePosition,
            entity.SpouseProfession));
    }
}
