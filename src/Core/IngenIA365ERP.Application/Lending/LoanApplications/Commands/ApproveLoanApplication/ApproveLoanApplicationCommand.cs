using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanApplications.Commands.ApproveLoanApplication;

public record ApproveLoanApplicationCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public decimal ApprovedAmount { get; init; }
    public int ApprovedTerm { get; init; }
    public decimal ApprovedRate { get; init; }
    public string? ApproverNotes { get; init; }
    public string? MinutesNumber { get; init; }
}

public class ApproveLoanApplicationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveLoanApplicationCommand, Result>
{
    public async Task<Result> Handle(ApproveLoanApplicationCommand request, CancellationToken ct)
    {
        var application = await context.LoanApplications
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);

        if (application is null)
            return Result.Failure(new Error("LoanApplication.NotFound",
                "Solicitud de credito no encontrada."));

        if (application.Status != "R")
            return Result.Failure(new Error("LoanApplication.InvalidStatus",
                $"Solo se pueden aprobar solicitudes en estado Radicada. Estado actual: {application.Status}"));

        // Validate approved amount <= requested
        if (request.ApprovedAmount > application.RequestedAmount)
            return Result.Failure(new Error("LoanApplication.ApprovedExceedsRequested",
                "El monto aprobado no puede exceder el monto solicitado."));

        // Calculate new installment with approved values
        var monthlyRate = request.ApprovedRate / 100m / 12m;
        decimal installment;
        if (monthlyRate > 0)
        {
            var factor = (double)monthlyRate * Math.Pow(1 + (double)monthlyRate, request.ApprovedTerm)
                         / (Math.Pow(1 + (double)monthlyRate, request.ApprovedTerm) - 1);
            installment = request.ApprovedAmount * (decimal)factor;
        }
        else
        {
            installment = request.ApprovedAmount / request.ApprovedTerm;
        }

        application.Status = "A"; // Aprobada
        application.ApprovedAmount = request.ApprovedAmount;
        application.Term = request.ApprovedTerm;
        application.InterestRate = request.ApprovedRate;
        application.InstallmentAmount = Math.Round(installment, 0);
        application.ApprovalDate = dateTime.TodayUtc;
        application.MinutesNumber = request.MinutesNumber ?? "";
        application.MinutesDate = dateTime.TodayUtc;
        application.Remarks = string.IsNullOrEmpty(request.ApproverNotes)
            ? application.Remarks
            : $"{application.Remarks}\n[APROBACION] {request.ApproverNotes}";
        application.AuthorizingUserId = currentUser.UserName ?? "system";
        application.UpdatedAt = dateTime.UtcNow;
        application.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class ApproveLoanApplicationCommandValidator : AbstractValidator<ApproveLoanApplicationCommand>
{
    public ApproveLoanApplicationCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id de solicitud requerido.");

        RuleFor(x => x.ApprovedAmount)
            .GreaterThan(0).WithMessage("Monto aprobado debe ser mayor a cero.");

        RuleFor(x => x.ApprovedTerm)
            .GreaterThan(0).WithMessage("Plazo aprobado debe ser mayor a cero.");

        RuleFor(x => x.ApprovedRate)
            .GreaterThanOrEqualTo(0).WithMessage("Tasa de interes no puede ser negativa.");
    }
}
