using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Payroll;
using MediatR;

namespace IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.CreateAutoContributionParam;

public record CreateAutoContributionParamCommand : IRequest<Result<Guid>>
{
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

public class CreateAutoContributionParamCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateAutoContributionParamCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateAutoContributionParamCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new AutoContributionParam
        {
            Code = request.Code,
            IdType = request.IdType,
            IdNumber = request.IdNumber,
            CheckDigit = request.CheckDigit,
            Name = request.Name,
            Address = request.Address ?? string.Empty,
            Phone = request.Phone ?? string.Empty,
            CityId = request.CityId,
            CityName = request.CityName ?? string.Empty,
            DepartmentId = request.DepartmentId,
            DepartmentName = request.DepartmentName ?? string.Empty,
            HealthRate = request.HealthRate,
            PensionRate = request.PensionRate,
            WorkRiskRate = request.WorkRiskRate,
            CcfRate = request.CcfRate,
            SenaRate = request.SenaRate,
            IcbfRate = request.IcbfRate,
            SolidarityFundRate = request.SolidarityFundRate,
            MinimumWage = request.MinimumWage,
            Email = request.Email ?? string.Empty,
            PresentationMethod = request.PresentationMethod,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.AutoContributionParams.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateAutoContributionParamCommandValidator : AbstractValidator<CreateAutoContributionParamCommand>
{
    public CreateAutoContributionParamCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(100).WithMessage("Address must not exceed 100 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(30).WithMessage("Phone must not exceed 30 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");
    }
}
