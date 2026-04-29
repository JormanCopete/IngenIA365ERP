using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.CDT;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.Certificates.Commands.CreateCertificate;

public record CreateCertificateCommand : IRequest<Result<Guid>>
{
    public Guid PersonPublicId { get; init; }
    public Guid CdtParameterPublicId { get; init; }
    public decimal Amount { get; init; }
    public int TermDays { get; init; }
    public bool AutoRenewal { get; init; }
}

public class CreateCertificateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateCertificateCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateCertificateCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve Person
        var person = await context.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, cancellationToken);

        if (person is null)
            return Result.Failure<Guid>(new Error("Person.NotFound", "Persona no encontrada."));

        // 2. Resolve CdtParameter
        var cdtParam = await context.CdtParameters.AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.PublicId == request.CdtParameterPublicId && !cp.IsDeleted, cancellationToken);

        if (cdtParam is null)
            return Result.Failure<Guid>(new Error("CdtParameter.NotFound",
                "Parametro de CDT no encontrado."));

        // 3. Validate minimum amount
        if (cdtParam.MinAmount.HasValue && request.Amount < cdtParam.MinAmount.Value)
            return Result.Failure<Guid>(new Error("Certificate.BelowMinimum",
                $"El monto minimo es {cdtParam.MinAmount.Value:N0}."));

        if (cdtParam.MaxAmount.HasValue && request.Amount > cdtParam.MaxAmount.Value)
            return Result.Failure<Guid>(new Error("Certificate.AboveMaximum",
                $"El monto maximo es {cdtParam.MaxAmount.Value:N0}."));

        // 4. Look up rate from CdtRatesByTerm
        var rate = await context.CdtRatesByTerm.AsNoTracking()
            .Where(r => r.CreditLineId == cdtParam.CreditLineId
                        && r.TermStart <= request.TermDays
                        && r.TermEnd >= request.TermDays
                        && r.AmountRangeStart <= request.Amount
                        && r.AmountRangeEnd >= request.Amount
                        && !r.IsDeleted)
            .Select(r => r.InterestRate)
            .FirstOrDefaultAsync(cancellationToken);

        var interestRate = rate ?? cdtParam.AnnualRate ?? cdtParam.InterestRate ?? 0m;

        // 5. Generate certificate number
        var lastCertNumber = await context.Certificates
            .Where(c => !c.IsDeleted)
            .OrderByDescending(c => c.Id)
            .Select(c => c.CertificateNumber)
            .FirstOrDefaultAsync(cancellationToken);

        // Increment: parse existing number or start from 1
        int.TryParse(lastCertNumber, out var lastNum);
        var newCertNumber = (lastNum + 1).ToString();

        // 6. Calculate maturity
        var issueDate = DateOnly.FromDateTime(dateTime.UtcNow);
        var maturityDate = issueDate.AddDays(request.TermDays);

        // 7. Create Certificate
        var certificate = new Certificate
        {
            CertificateNumber = newCertNumber,
            PersonId = person.Id,
            CreditLineId = cdtParam.CreditLineId,
            IssueDate = issueDate,
            MaturityDate = maturityDate,
            Amount = request.Amount,
            InterestRate = interestRate,
            Term = request.TermDays,
            Status = "V", // Vigente
            RenewalType = request.AutoRenewal ? "A" : "N",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.Certificates.Add(certificate);

        // 8. Create initial CertificateEntry
        var entry = new CertificateEntry
        {
            CertificateId = 0, // will be set by EF after save
            PersonId = person.Id,
            CreditLineId = cdtParam.CreditLineId,
            EntryDate = issueDate,
            OpeningDate = issueDate,
            EntryType = "AP", // Apertura
            Amount = request.Amount,
            CurrentRate = interestRate,
            Description = "Apertura de CDT",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        // Save certificate first to get Id
        await context.SaveChangesAsync(cancellationToken);

        entry.CertificateId = certificate.Id;
        context.CertificateEntries.Add(entry);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(certificate.PublicId);
    }
}

public class CreateCertificateCommandValidator : AbstractValidator<CreateCertificateCommand>
{
    public CreateCertificateCommandValidator()
    {
        RuleFor(x => x.PersonPublicId)
            .NotEmpty().WithMessage("El titular es requerido.");

        RuleFor(x => x.CdtParameterPublicId)
            .NotEmpty().WithMessage("El tipo de CDT es requerido.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.TermDays)
            .GreaterThan(0).WithMessage("El plazo debe ser mayor a cero dias.");
    }
}
