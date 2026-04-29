using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Documents.Commands.CreateDocument;

public record JournalEntryLineDto(
    Guid AccountPublicId,
    Guid? PersonPublicId,
    Guid? CostCenterPublicId,
    Guid? BranchPublicId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? Reference);

public record CreateDocumentCommand : IRequest<Result<Guid>>
{
    public Guid VoucherTypePublicId { get; init; }
    public DateOnly DocumentDate { get; init; }
    public string? Description { get; init; }
    public List<JournalEntryLineDto> Lines { get; init; } = [];
}

public class CreateDocumentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateDocumentCommand request, CancellationToken ct)
    {
        // 1. Resolve VoucherType
        var voucherType = await context.VoucherTypes.FirstOrDefaultAsync(
            v => v.PublicId == request.VoucherTypePublicId && !v.IsDeleted, ct);
        if (voucherType is null)
            return Result.Failure<Guid>(new Error("Document.VoucherTypeNotFound",
                "Tipo de comprobante no encontrado."));

        // 2. Validate accounting period is open
        var period = await context.AccountingPeriods.FirstOrDefaultAsync(
            p => p.Year == request.DocumentDate.Year
              && p.PeriodNumber == (byte)request.DocumentDate.Month
              && p.ModuleCode == "CNT" && !p.IsDeleted, ct);
        if (period is null || period.Status == "C")
            return Result.Failure<Guid>(new Error("Document.PeriodClosed",
                "El periodo contable esta cerrado o no existe."));

        // 3. Validate double-entry (debits == credits)
        var totalDebit = request.Lines.Sum(l => l.DebitAmount);
        var totalCredit = request.Lines.Sum(l => l.CreditAmount);
        if (totalDebit != totalCredit)
            return Result.Failure<Guid>(new Error("Document.Unbalanced",
                $"Partida doble no cuadra: Debitos={totalDebit}, Creditos={totalCredit}"));

        // 4. Generate sequential number
        var nextNumber = voucherType.NextSequenceNumber + 1;
        voucherType.NextSequenceNumber = nextNumber;

        // 5. Compute period code as YYYYMM integer
        var periodCode = request.DocumentDate.Year * 100 + request.DocumentDate.Month;

        // 6. Create document header
        var document = new AccountingDocument
        {
            VoucherTypeCode = voucherType.Code,
            DocumentNumber = nextNumber,
            Detail = request.Description ?? "",
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            DocumentDate = request.DocumentDate,
            IsClosed = false,
            IsVoided = false,
            PeriodCode = periodCode,
            ModuleCode = "CNT",
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.AccountingDocuments.Add(document);

        // 7. Resolve default branch and cost center (fallback to 0 if not provided)
        var defaultBranch = await context.Branches.AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync(ct);
        var defaultCostCenter = await context.CostCenters.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync(ct);

        // 8. Create journal entry lines
        foreach (var line in request.Lines)
        {
            var account = await context.ChartOfAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.PublicId == line.AccountPublicId && !a.IsDeleted, ct);
            if (account is null)
                return Result.Failure<Guid>(new Error("Document.AccountNotFound",
                    $"Cuenta no encontrada: {line.AccountPublicId}"));

            // Resolve optional FKs
            int branchId = defaultBranch?.Id ?? 0;
            int costCenterId = defaultCostCenter?.Id ?? 0;
            int? personId = null;

            if (line.BranchPublicId.HasValue)
            {
                var branch = await context.Branches.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.PublicId == line.BranchPublicId.Value && !b.IsDeleted, ct);
                if (branch is not null) branchId = branch.Id;
            }
            if (line.CostCenterPublicId.HasValue)
            {
                var cc = await context.CostCenters.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.PublicId == line.CostCenterPublicId.Value && !c.IsDeleted, ct);
                if (cc is not null) costCenterId = cc.Id;
            }
            if (line.PersonPublicId.HasValue)
            {
                var person = await context.People.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PublicId == line.PersonPublicId.Value && !p.IsDeleted, ct);
                if (person is not null) personId = person.Id;
            }

            var entry = new JournalEntry
            {
                VoucherTypeCode = document.VoucherTypeCode,
                DocumentNumber = document.DocumentNumber,
                AccountId = account.Id,
                PersonId = personId,
                BranchId = branchId,
                CostCenterId = costCenterId,
                PeriodCode = periodCode.ToString(),
                TransactionDate = request.DocumentDate,
                Description = line.Description ?? document.Detail,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Status = 0,
                UserName = currentUser.UserName,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.JournalEntries.Add(entry);

            // 9. Update account balances (upsert)
            var balance = await context.AccountBalances.FirstOrDefaultAsync(
                b => b.AccountId == account.Id
                  && b.PeriodYear == request.DocumentDate.Year
                  && b.PeriodMonth == (byte)request.DocumentDate.Month
                  && b.BranchId == branchId
                  && b.CostCenterId == costCenterId, ct);

            if (balance is null)
            {
                balance = new AccountBalance
                {
                    AccountId = account.Id,
                    PeriodYear = request.DocumentDate.Year,
                    PeriodMonth = (byte)request.DocumentDate.Month,
                    BranchId = branchId,
                    CostCenterId = costCenterId,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                };
                context.AccountBalances.Add(balance);
            }
            else
            {
                balance.DebitAmount += line.DebitAmount;
                balance.CreditAmount += line.CreditAmount;
                balance.UpdatedAt = dateTime.UtcNow;
                balance.UpdatedBy = currentUser.UserName;
            }
        }

        await context.SaveChangesAsync(ct);
        return Result.Success(document.PublicId);
    }
}

public class CreateDocumentCommandValidator : AbstractValidator<CreateDocumentCommand>
{
    public CreateDocumentCommandValidator()
    {
        RuleFor(x => x.VoucherTypePublicId)
            .NotEmpty().WithMessage("Tipo de comprobante requerido.");

        RuleFor(x => x.DocumentDate)
            .NotEmpty().WithMessage("Fecha requerida.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Minimo 2 lineas requeridas.")
            .Must(l => l.Count >= 2).WithMessage("Minimo 2 lineas.");

        RuleFor(x => x.Lines)
            .Must(l => l.Sum(e => e.DebitAmount) == l.Sum(e => e.CreditAmount))
            .WithMessage("La partida doble no cuadra (Debitos != Creditos).");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountPublicId)
                .NotEmpty().WithMessage("Cuenta requerida en cada linea.");
            line.RuleFor(l => l.DebitAmount)
                .GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.CreditAmount)
                .GreaterThanOrEqualTo(0);
            line.RuleFor(l => l)
                .Must(l => l.DebitAmount > 0 || l.CreditAmount > 0)
                .WithMessage("Cada linea debe tener debito o credito > 0.");
        });
    }
}
