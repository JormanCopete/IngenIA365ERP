using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.Certificates.Commands.RenewCertificate;

public record RenewCertificateCommand : IRequest<Result<Guid>>
{
    public Guid CertificatePublicId { get; init; }
    public bool RenewWithInterest { get; init; }
}

public class RenewCertificateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RenewCertificateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RenewCertificateCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find certificate
        var cert = await context.Certificates
            .FirstOrDefaultAsync(c => c.PublicId == request.CertificatePublicId && !c.IsDeleted, cancellationToken);

        if (cert is null)
            return Result.Failure<Guid>(new Error("Certificate.NotFound",
                "Certificado no encontrado."));

        if (cert.Status != "V")
            return Result.Failure<Guid>(new Error("Certificate.NotActive",
                "Solo se pueden renovar certificados vigentes."));

        // 2. Calculate accrued interest
        var daysElapsed = (DateOnly.FromDateTime(dateTime.UtcNow).DayNumber - cert.IssueDate.DayNumber);
        var accruedInterest = Math.Round(cert.Amount * cert.InterestRate / 100m * daysElapsed / 365m, 0);

        // 3. Close old certificate
        cert.Status = "R"; // Renovado
        cert.UpdatedAt = dateTime.UtcNow;
        cert.UpdatedBy = currentUser.UserName;

        // Create closure entry
        var closureEntry = new CertificateEntry
        {
            CertificateId = cert.Id,
            PersonId = cert.PersonId,
            CreditLineId = cert.CreditLineId,
            EntryDate = DateOnly.FromDateTime(dateTime.UtcNow),
            EntryType = "RN", // Renovacion
            PreviousRate = cert.InterestRate,
            CurrentRate = cert.InterestRate,
            Amount = cert.Amount,
            Description = "Cierre por renovacion",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.CertificateEntries.Add(closureEntry);

        // 4. Create new certificate
        var newAmount = request.RenewWithInterest ? cert.Amount + accruedInterest : cert.Amount;
        var issueDate = DateOnly.FromDateTime(dateTime.UtcNow);

        // Get latest rate
        var newRate = await context.CdtRatesByTerm.AsNoTracking()
            .Where(r => r.CreditLineId == cert.CreditLineId
                        && r.TermStart <= cert.Term
                        && r.TermEnd >= cert.Term
                        && r.AmountRangeStart <= newAmount
                        && r.AmountRangeEnd >= newAmount
                        && !r.IsDeleted)
            .Select(r => r.InterestRate)
            .FirstOrDefaultAsync(cancellationToken) ?? cert.InterestRate;

        // Generate new number
        var lastCertNumber = await context.Certificates
            .Where(c => !c.IsDeleted || c.Id == cert.Id)
            .OrderByDescending(c => c.Id)
            .Select(c => c.CertificateNumber)
            .FirstOrDefaultAsync(cancellationToken);
        int.TryParse(lastCertNumber, out var lastNum);

        var newCert = new Certificate
        {
            CertificateNumber = (lastNum + 1).ToString(),
            PersonId = cert.PersonId,
            CreditLineId = cert.CreditLineId,
            IssueDate = issueDate,
            MaturityDate = issueDate.AddDays(cert.Term),
            Amount = newAmount,
            InterestRate = newRate,
            Term = cert.Term,
            Status = "V",
            RenewalType = cert.RenewalType,
            PreviousCertificateId = cert.Id,
            IsCapitalized = request.RenewWithInterest,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Certificates.Add(newCert);
        await context.SaveChangesAsync(cancellationToken);

        // Create opening entry for new cert
        var openEntry = new CertificateEntry
        {
            CertificateId = newCert.Id,
            PersonId = newCert.PersonId,
            CreditLineId = newCert.CreditLineId,
            EntryDate = issueDate,
            OpeningDate = issueDate,
            EntryType = "AP",
            CurrentRate = newRate,
            Amount = newAmount,
            Description = $"Renovacion desde CDT #{cert.CertificateNumber}" +
                          (request.RenewWithInterest ? $" (capitalizado +{accruedInterest:N0})" : ""),
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.CertificateEntries.Add(openEntry);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(newCert.PublicId);
    }
}

public class RenewCertificateCommandValidator : AbstractValidator<RenewCertificateCommand>
{
    public RenewCertificateCommandValidator()
    {
        RuleFor(x => x.CertificatePublicId)
            .NotEmpty().WithMessage("El certificado es requerido.");
    }
}
