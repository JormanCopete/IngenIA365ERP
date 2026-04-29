using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.Certificates.Commands.CancelCertificate;

public record CancelCertificateCommand : IRequest<Result>
{
    public Guid CertificatePublicId { get; init; }
    public string? Reason { get; init; }
}

public class CancelCertificateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CancelCertificateCommand, Result>
{
    public async Task<Result> Handle(
        CancelCertificateCommand request,
        CancellationToken cancellationToken)
    {
        var cert = await context.Certificates
            .FirstOrDefaultAsync(c => c.PublicId == request.CertificatePublicId && !c.IsDeleted, cancellationToken);

        if (cert is null)
            return Result.Failure(new Error("Certificate.NotFound",
                "Certificado no encontrado."));

        if (cert.Status == "C")
            return Result.Failure(new Error("Certificate.AlreadyCancelled",
                "El certificado ya fue cancelado."));

        // Calculate penalty for early cancellation (e.g., forfeit interest)
        var daysElapsed = (DateOnly.FromDateTime(dateTime.UtcNow).DayNumber - cert.IssueDate.DayNumber);
        var daysRemaining = cert.Term - daysElapsed;
        decimal penalty = 0;

        if (daysRemaining > 0)
        {
            // Early cancellation: forfeit 50% of accrued interest as penalty
            var accruedInterest = Math.Round(cert.Amount * cert.InterestRate / 100m * daysElapsed / 365m, 0);
            penalty = Math.Round(accruedInterest * 0.5m, 0);
        }

        // Create cancellation entry
        var entry = new CertificateEntry
        {
            CertificateId = cert.Id,
            PersonId = cert.PersonId,
            CreditLineId = cert.CreditLineId,
            EntryDate = DateOnly.FromDateTime(dateTime.UtcNow),
            EntryType = "CN", // Cancelacion
            PreviousRate = cert.InterestRate,
            CurrentRate = 0,
            Amount = cert.Amount,
            Description = (request.Reason ?? "Cancelacion anticipada") +
                          (penalty > 0 ? $" - Penalizacion: {penalty:N0}" : ""),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.CertificateEntries.Add(entry);

        // Update certificate status
        cert.Status = "C"; // Cancelado
        cert.CancelledByUserId = currentUser.UserName;
        cert.CancellationDate = dateTime.UtcNow;
        cert.UpdatedAt = dateTime.UtcNow;
        cert.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public class CancelCertificateCommandValidator : AbstractValidator<CancelCertificateCommand>
{
    public CancelCertificateCommandValidator()
    {
        RuleFor(x => x.CertificatePublicId)
            .NotEmpty().WithMessage("El certificado es requerido.");
    }
}
