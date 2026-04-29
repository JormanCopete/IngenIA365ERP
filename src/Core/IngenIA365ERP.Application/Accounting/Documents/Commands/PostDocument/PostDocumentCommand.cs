using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents.Commands.PostDocument;

public record PostDocumentCommand(Guid PublicId) : IRequest<Result>;

public class PostDocumentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<PostDocumentCommand, Result>
{
    public async Task<Result> Handle(PostDocumentCommand request, CancellationToken ct)
    {
        // 1. Find the document
        var document = await context.AccountingDocuments.FirstOrDefaultAsync(
            d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (document is null)
            return Result.Failure(new Error("Document.NotFound",
                "Comprobante no encontrado."));

        // 2. Validate document is in draft status
        if (document.IsClosed)
            return Result.Failure(new Error("Document.AlreadyClosed",
                "El comprobante ya fue contabilizado."));
        if (document.IsVoided)
            return Result.Failure(new Error("Document.IsVoided",
                "No se puede contabilizar un comprobante anulado."));

        // 3. Validate accounting period is open
        var periodYear = document.PeriodCode.HasValue ? document.PeriodCode.Value / 100 : 0;
        var periodMonth = document.PeriodCode.HasValue ? (byte)(document.PeriodCode.Value % 100) : (byte)0;
        var period = await context.AccountingPeriods.FirstOrDefaultAsync(
            p => p.Year == periodYear
              && p.PeriodNumber == periodMonth
              && p.ModuleCode == "CNT" && !p.IsDeleted, ct);
        if (period is not null && period.Status == "C")
            return Result.Failure(new Error("Document.PeriodClosed",
                "No se puede contabilizar en periodo cerrado."));

        // 4. Validate the document is balanced (safety check)
        if (document.TotalDebit != document.TotalCredit)
            return Result.Failure(new Error("Document.Unbalanced",
                "El comprobante no esta cuadrado."));

        // 5. Post the document
        document.IsClosed = true;
        document.UpdatedAt = dateTime.UtcNow;
        document.UpdatedBy = currentUser.UserName;

        // 6. Update all journal entries to posted status
        var entries = await context.JournalEntries
            .Where(j => j.VoucherTypeCode == document.VoucherTypeCode
                     && j.DocumentNumber == document.DocumentNumber
                     && !j.IsDeleted)
            .ToListAsync(ct);

        foreach (var entry in entries)
        {
            entry.Status = 1; // Posted
            entry.UpdatedAt = dateTime.UtcNow;
            entry.UpdatedBy = currentUser.UserName;
        }

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class PostDocumentCommandValidator : AbstractValidator<PostDocumentCommand>
{
    public PostDocumentCommandValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Id del comprobante requerido.");
    }
}
