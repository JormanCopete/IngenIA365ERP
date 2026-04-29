using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents.Commands.VoidDocument;

public record VoidDocumentCommand(Guid PublicId, string? Reason) : IRequest<Result>;

public class VoidDocumentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<VoidDocumentCommand, Result>
{
    public async Task<Result> Handle(VoidDocumentCommand request, CancellationToken ct)
    {
        // 1. Find the document
        var document = await context.AccountingDocuments.FirstOrDefaultAsync(
            d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (document is null)
            return Result.Failure(new Error("Document.NotFound",
                "Comprobante no encontrado."));

        // 2. Validate not already voided
        if (document.IsVoided)
            return Result.Failure(new Error("Document.AlreadyVoided",
                "El comprobante ya fue anulado."));

        // 3. Validate accounting period is still open
        var periodYear = document.PeriodCode.HasValue ? document.PeriodCode.Value / 100 : 0;
        var periodMonth = document.PeriodCode.HasValue ? (byte)(document.PeriodCode.Value % 100) : (byte)0;
        var period = await context.AccountingPeriods.FirstOrDefaultAsync(
            p => p.Year == periodYear
              && p.PeriodNumber == periodMonth
              && p.ModuleCode == "CNT" && !p.IsDeleted, ct);
        if (period is not null && period.Status == "C")
            return Result.Failure(new Error("Document.PeriodClosed",
                "No se puede anular un comprobante en periodo cerrado."));

        // 4. Set voided
        document.IsVoided = true;
        document.Detail = $"[ANULADO] {request.Reason ?? ""} - {document.Detail}";
        document.UpdatedAt = dateTime.UtcNow;
        document.UpdatedBy = currentUser.UserName;

        // 5. Reverse all balance entries — get journal entries for this document
        var entries = await context.JournalEntries
            .Where(j => j.VoucherTypeCode == document.VoucherTypeCode
                     && j.DocumentNumber == document.DocumentNumber
                     && !j.IsDeleted)
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            // Mark entry as voided (status = -1)
            entry.Status = -1;
            entry.UpdatedAt = dateTime.UtcNow;
            entry.UpdatedBy = currentUser.UserName;

            // Reverse the balance
            var balance = await context.AccountBalances.FirstOrDefaultAsync(
                b => b.AccountId == entry.AccountId
                  && b.PeriodYear == periodYear
                  && b.PeriodMonth == periodMonth
                  && b.BranchId == entry.BranchId
                  && b.CostCenterId == entry.CostCenterId, ct);

            if (balance is not null)
            {
                balance.DebitAmount -= entry.DebitAmount;
                balance.CreditAmount -= entry.CreditAmount;
                balance.UpdatedAt = dateTime.UtcNow;
                balance.UpdatedBy = currentUser.UserName;
            }
        }

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class VoidDocumentCommandValidator : AbstractValidator<VoidDocumentCommand>
{
    public VoidDocumentCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id del comprobante requerido.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("La razon de anulacion no debe exceder 500 caracteres.");
    }
}
