using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.UpdateAutoContributionParam;

public record UpdateAutoContributionParamCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public int IdType { get; init; }
    public int IdNumber { get; init; }
    public int CheckDigit { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public int CityId { get; init; }
    public string? CityName { get; init; }
    public int DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public decimal HealthRate { get; init; }
    public decimal PensionRate { get; init; }
    public decimal WorkRiskRate { get; init; }
    public decimal CcfRate { get; init; }
    public decimal SenaRate { get; init; }
    public decimal IcbfRate { get; init; }
    public decimal SolidarityFundRate { get; init; }
    public decimal MinimumWage { get; init; }
    public string? Email { get; init; }
    public int PresentationMethod { get; init; }
}

public class UpdateAutoContributionParamCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAutoContributionParamCommand, Result>
{
    public async Task<Result> Handle(
        UpdateAutoContributionParamCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AutoContributionParams
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.Code = request.Code;
        entity.IdType = request.IdType;
        entity.IdNumber = request.IdNumber;
        entity.CheckDigit = request.CheckDigit;
        entity.Name = request.Name;
        entity.Address = request.Address ?? string.Empty;
        entity.Phone = request.Phone ?? string.Empty;
        entity.CityId = request.CityId;
        entity.CityName = request.CityName ?? string.Empty;
        entity.DepartmentId = request.DepartmentId;
        entity.DepartmentName = request.DepartmentName ?? string.Empty;
        entity.HealthRate = request.HealthRate;
        entity.PensionRate = request.PensionRate;
        entity.WorkRiskRate = request.WorkRiskRate;
        entity.CcfRate = request.CcfRate;
        entity.SenaRate = request.SenaRate;
        entity.IcbfRate = request.IcbfRate;
        entity.SolidarityFundRate = request.SolidarityFundRate;
        entity.MinimumWage = request.MinimumWage;
        entity.Email = request.Email ?? string.Empty;
        entity.PresentationMethod = request.PresentationMethod;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
