using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.LoanApplications.Commands.RejectLoanApplication;

public record RejectLoanApplicationCommand(Guid PublicId, string RejectionReason) : IRequest<Result>;

public class RejectLoanApplicationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RejectLoanApplicationCommand, Result>
{
    public async Task<Result> Handle(RejectLoanApplicationCommand request, CancellationToken ct)
    {
        var application = await context.LoanApplications
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId && !a.IsDeleted, ct);

        if (application is null)
            return Result.Failure(new Error("LoanApplication.NotFound",
                "Solicitud de credito no encontrada."));

        if (application.Status != "R")
            return Result.Failure(new Error("LoanApplication.InvalidStatus",
                $"Solo se pueden rechazar solicitudes en estado Radicada. Estado actual: {application.Status}"));

        application.Status = "X"; // Rechazada
        application.Remarks = string.IsNullOrEmpty(application.Remarks)
            ? $"[RECHAZO] {request.RejectionReason}"
            : $"{application.Remarks}\n[RECHAZO] {request.RejectionReason}";
        application.AuthorizingUserId = currentUser.UserName ?? "system";
        application.UpdatedAt = dateTime.UtcNow;
        application.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class RejectLoanApplicationCommandValidator : AbstractValidator<RejectLoanApplicationCommand>
{
    public RejectLoanApplicationCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id de solicitud requerido.");

        RuleFor(x => x.RejectionReason)
            .NotEmpty().WithMessage("Motivo de rechazo requerido.")
            .MaximumLength(500).WithMessage("Motivo de rechazo no puede exceder 500 caracteres.");
    }
}
