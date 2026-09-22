using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Periods;

// Cierre y reapertura del ejercicio (feature 009 E2, US6; FR-023, FR-024; contracts/api.md §5).
//
// El cierre cancela ingresos, costos y gastos —las clases 4 a 7, las cuentas cuyo rubro vive en el
// estado de resultados— contra la cuenta de resultado del ejercicio que la empresa fijó en su
// configuración, con un comprobante `CI` (`Kind = Closing`) fechado el 31 de diciembre y
// contabilizado por el contrato, como todo. No hay «período 13»: el 31/12 está cerrado y el
// contrato admite la fecha sólo por ser de esa clase. El saldo que cancela es el acumulado de la
// cuenta hasta el 31/12: como el ejercicio anterior tiene que estar cerrado (su cierre las dejó en
// cero), ese acumulado es lo del año más la apertura si la cooperativa arrancó a mitad de él
// (spec, casos borde). Las consultas excluyen el cierre del ejercicio consultado salvo
// `includeClosing` y cuentan siempre los de ejercicios anteriores: así el balance del 1 de enero
// siguiente ya no trae resultados y el estado de resultados del año cerrado sigue mostrándolos.
//
// Reabrir reversa el cierre en su misma fecha (el reverso es también de clase Cierre, ver
// AccountingPoster.PrepareReversalAsync), deja el ejercicio abierto con sus meses todavía
// cerrados —cada mes se reabre aparte, con su motivo— y queda auditado con el motivo.

/// <summary>Respuesta del cierre: el comprobante <c>CI</c> (nulo si no había nada que cancelar), su número, las líneas y el resultado (positivo = excedente).</summary>
public sealed record CierreDeEjercicioDto(int Year, Guid? ClosingDocumentPublicId, long? Number, int Lines, decimal Result);

// ---------------------------------------------------------------------------- cerrar ejercicio --

public sealed record CloseFiscalYearCommand(int Year) : IRequest<Result<CierreDeEjercicioDto>>, IReintentableAnteConcurrencia;

public sealed class CloseFiscalYearCommandValidator : AbstractValidator<CloseFiscalYearCommand>
{
    public CloseFiscalYearCommandValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class CloseFiscalYearCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingPoster poster,
    AccountingAuditEmitter audit)
    : IRequestHandler<CloseFiscalYearCommand, Result<CierreDeEjercicioDto>>
{
    public const string TipoDeComprobante = "CI";

    public async Task<Result<CierreDeEjercicioDto>> Handle(CloseFiscalYearCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.Include(s => s.ResultAccount).FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Fallo(AccountingErrors.NotInitialized);

        var ejercicio = await db.FiscalYears.Include(f => f.Periods).FirstOrDefaultAsync(f => f.Year == request.Year && !f.IsDeleted, ct);
        if (ejercicio is null) return Fallo(AccountingErrors.FiscalYearNotFound);
        if (ejercicio.Status == PeriodStatus.Closed) return Fallo(AccountingErrors.FiscalYearAlreadyClosed);

        var abiertos = ejercicio.Periods.Where(p => !p.IsDeleted && p.Status != PeriodStatus.Closed).OrderBy(p => p.Month).Select(p => (int)p.Month).ToList();
        if (abiertos.Count > 0)
            return Fallo(new ErrorConDatos(AccountingErrors.FiscalYearPeriodsOpen.Code,
                $"{AccountingErrors.FiscalYearPeriodsOpen.Message} Siguen abiertos: {string.Join(", ", abiertos)}.", new { openMonths = abiertos }));

        var anterior = await db.FiscalYears.AsNoTracking().Where(f => !f.IsDeleted && f.Year < request.Year).OrderByDescending(f => f.Year).FirstOrDefaultAsync(ct);
        if (anterior is { Status: not PeriodStatus.Closed }) return Fallo(AccountingErrors.FiscalYearPreviousOpen);

        if (setup.ResultAccount is null || setup.ResultAccount.IsDeleted) return Fallo(AccountingErrors.FiscalYearResultAccountMissing);
        if (!setup.ResultAccount.IsMovement || !setup.ResultAccount.IsActive) return Fallo(AccountingErrors.SetupResultAccountNotMovement);

        // Lo acumulado en cada cuenta de resultado hasta el 31/12, por sucursal y centro de costo
        // (la línea del cierre conserva las dos dimensiones para que el balance por sucursal siga
        // cuadrando y el centro de costo vea su gasto cancelado). Todo lo contabilizado cuenta: las
        // reversiones se netean solas y los cierres anteriores ya dejaron en cero lo de otros años.
        var hasta = ejercicio.EndDate;
        var saldos = await db.JournalEntries.AsNoTracking()
            .Where(e => !e.IsDeleted && e.IsPosted && e.Date <= hasta
                        && (e.Account!.Code.StartsWith("4") || e.Account!.Code.StartsWith("5") || e.Account!.Code.StartsWith("6") || e.Account!.Code.StartsWith("7"))
                        && e.AccountId != setup.ResultAccountId)
            .GroupBy(e => new { e.AccountId, e.Account!.Code, e.BranchId, e.CostCenterId })
            .Select(g => new { g.Key.AccountId, g.Key.Code, g.Key.BranchId, g.Key.CostCenterId, Debitos = g.Sum(e => e.Debit), Creditos = g.Sum(e => e.Credit) })
            .ToListAsync(ct);

        var lineas = new List<PostingLine>();
        var resultadoPorSucursal = new Dictionary<int, decimal>();
        foreach (var s in saldos.OrderBy(s => s.Code).ThenBy(s => s.BranchId).ThenBy(s => s.CostCenterId))
        {
            var neto = s.Debitos - s.Creditos;
            if (neto == 0m) continue;
            var detalle = $"Cierre {request.Year}: cancela {s.Code}";
            lineas.Add(new PostingLine
            {
                AccountId = s.AccountId, AccountCode = s.Code, BranchId = s.BranchId, CostCenterId = s.CostCenterId,
                Debit = neto < 0m ? -neto : 0m, Credit = neto > 0m ? neto : 0m, Detail = detalle,
            });
            // Un neto crédito (ingreso) suma al resultado; un neto débito (gasto, costo) lo resta.
            resultadoPorSucursal[s.BranchId] = resultadoPorSucursal.GetValueOrDefault(s.BranchId) - neto;
        }

        var resultado = resultadoPorSucursal.Values.Sum();
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";

        AccountingDocument? cierre = null;
        if (lineas.Count > 0)
        {
            foreach (var (sucursal, neto) in resultadoPorSucursal.Where(kv => kv.Value != 0m).OrderBy(kv => kv.Key))
            {
                lineas.Add(new PostingLine
                {
                    AccountId = setup.ResultAccountId, AccountCode = setup.ResultAccount.Code, BranchId = sucursal,
                    Debit = neto < 0m ? -neto : 0m, Credit = neto > 0m ? neto : 0m,
                    Detail = neto >= 0m ? $"Excedente del ejercicio {request.Year}" : $"Pérdida del ejercicio {request.Year}",
                });
            }
            var preparado = await poster.PrepareAsync(new PostingRequest(
                TipoDeComprobante, hasta,
                $"Cierre del ejercicio {request.Year}: {(resultado >= 0m ? "excedente" : "pérdida")} de {Math.Abs(resultado):N2}",
                new AccountingOrigin(ModuloContable.Contabilidad, nameof(FiscalYear), ejercicio.PublicId),
                lineas, DocumentKind.Closing), ct);
            if (preparado.IsFailure) return Fallo(preparado.Error);
            cierre = preparado.Value;
            ejercicio.ClosingDocument = cierre;
        }

        ejercicio.Status = PeriodStatus.Closed;
        ejercicio.ClosedAt = ahora;
        ejercicio.ClosedBy = quien;
        ejercicio.UpdatedAt = ahora;
        ejercicio.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.FiscalYear.Closed", nameof(FiscalYear), ejercicio.PublicId, new { status = "Open" },
            new { status = "Closed", request.Year, closingDocumentPublicId = cierre?.PublicId, number = cierre?.Number, lines = lineas.Count, result = resultado }, ct);
        return Result.Success(new CierreDeEjercicioDto(request.Year, cierre?.PublicId, cierre?.Number, lineas.Count, resultado));
    }

    private static Result<CierreDeEjercicioDto> Fallo(Error error) => Result.Failure<CierreDeEjercicioDto>(error);
}

// --------------------------------------------------------------------------- reabrir ejercicio --

/// <summary>Reversa el cierre (si lo hubo) en su misma fecha y deja el ejercicio abierto; los meses siguen cerrados hasta que alguien los reabra con motivo.</summary>
public sealed record ReopenFiscalYearCommand(int Year, string Reason) : IRequest<Result<CierreDeEjercicioDto>>, IReintentableAnteConcurrencia;

public sealed class ReopenFiscalYearCommandValidator : AbstractValidator<ReopenFiscalYearCommand>
{
    public ReopenFiscalYearCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indique el motivo de la reapertura del ejercicio.").MaximumLength(200);
    }
}

public sealed class ReopenFiscalYearCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingPoster poster,
    AccountingAuditEmitter audit)
    : IRequestHandler<ReopenFiscalYearCommand, Result<CierreDeEjercicioDto>>
{
    public async Task<Result<CierreDeEjercicioDto>> Handle(ReopenFiscalYearCommand request, CancellationToken ct)
    {
        var ejercicio = await db.FiscalYears.FirstOrDefaultAsync(f => f.Year == request.Year && !f.IsDeleted, ct);
        if (ejercicio is null) return Fallo(AccountingErrors.FiscalYearNotFound);
        if (ejercicio.Status != PeriodStatus.Closed) return Fallo(AccountingErrors.FiscalYearNotClosed);
        // El cierre del año siguiente partió de que éste estaba cerrado: se deshace de atrás hacia adelante.
        if (await db.FiscalYears.AnyAsync(f => !f.IsDeleted && f.Year > request.Year && f.Status == PeriodStatus.Closed, ct))
            return Fallo(AccountingErrors.FiscalYearNotLast);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var motivo = request.Reason.Trim();

        AccountingDocument? reverso = null;
        if (ejercicio.ClosingDocumentId is { } cierreId)
        {
            var cierre = await db.AccountingDocuments.Include(d => d.VoucherType).Include(d => d.Lines).FirstOrDefaultAsync(d => d.Id == cierreId && !d.IsDeleted, ct);
            if (cierre is null) return Fallo(AccountingErrors.DocumentNotFound);
            if (cierre.Status == DocumentStatus.Posted)
            {
                var preparado = await poster.PrepareReversalAsync(cierre, cierre.Date, $"Reapertura del ejercicio {request.Year}: {motivo}",
                    new AccountingOrigin(ModuloContable.Contabilidad, nameof(FiscalYear), ejercicio.PublicId), ct);
                if (preparado.IsFailure) return Fallo(preparado.Error);
                reverso = preparado.Value;
            }
        }

        ejercicio.Status = PeriodStatus.Open;
        ejercicio.ReopenedAt = ahora;
        ejercicio.ReopenedBy = quien;
        ejercicio.ReopenReason = motivo;
        ejercicio.ClosingDocumentId = null;
        ejercicio.ClosingDocument = null;
        ejercicio.UpdatedAt = ahora;
        ejercicio.UpdatedBy = quien;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.FiscalYear.Reopened", nameof(FiscalYear), ejercicio.PublicId, new { status = "Closed" },
            new { status = "Open", request.Year, reason = motivo, reversalPublicId = reverso?.PublicId, number = reverso?.Number }, ct);
        return Result.Success(new CierreDeEjercicioDto(request.Year, reverso?.PublicId, reverso?.Number, reverso?.Lines.Count ?? 0, 0m));
    }

    private static Result<CierreDeEjercicioDto> Fallo(Error error) => Result.Failure<CierreDeEjercicioDto>(error);
}
