using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Agreements;

// Feature 013, K2 (US10, bloque B; T0833; api.md §13.1; FR-114): lo descontado pendiente de giro de un convenio, sólo de créditos del alcance de
// oficina y línea de quien pregunta. (nuevo)

/// <summary>Lo descontado pendiente de giro de un archivo del convenio, con la mora de la pagaduría y si superó el umbral sellado. (nuevo)</summary>
public sealed record PendingPayrollTransferDto(
    int Year,
    int Month,
    int SubPeriod,
    Guid FilePublicId,
    string FileNumber,
    int Lines,
    decimal Amount,
    DateOnly? FirstDeductionDate,
    DateOnly? TransferDueDate,
    int PaymasterArrearsDays,
    int? ThresholdDays,
    bool ThresholdExceeded);

/// <summary>
/// Lo descontado pendiente de giro de un convenio por período (api.md §13.1, <c>GET /{id}/pending-transfers?asOf=</c>,
/// <c>PayrollDeductionFiles.Read</c>; FR-114): sólo de créditos del alcance de oficina y línea de quien pregunta. (nuevo)
/// </summary>
public sealed record ListPendingPayrollTransfersQuery(Guid AgreementPublicId, DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<PendingPayrollTransferDto>>>;

public sealed class ListPendingPayrollTransfersQueryValidator : AbstractValidator<ListPendingPayrollTransfersQuery>
{
    public ListPendingPayrollTransfersQueryValidator() => RuleFor(x => x.AgreementPublicId).NotEmpty();
}

public sealed class ListPendingPayrollTransfersQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<ListPendingPayrollTransfersQuery, Result<IReadOnlyList<PendingPayrollTransferDto>>>
{
    public async Task<Result<IReadOnlyList<PendingPayrollTransferDto>>> Handle(ListPendingPayrollTransfersQuery request, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.AgreementPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<IReadOnlyList<PendingPayrollTransferDto>>(ErroresDeLibranza.ConvenioInexistente());
        var fecha = request.AsOf ?? reloj.HoyLocal;
        AlcanceDeCartera alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var archivos = db.PayrollDeductionFiles.AsNoTracking().Where(f => f.PayrollAgreementId == convenio.Id).Select(f => f.Id);
        var pendientes = await PendientesDeGiro.Consulta(db).AsNoTracking()
            .Where(r => archivos.Contains(r.PayrollDeductionFileId) && creditos.Contains(r.LoanId))
            .Select(r => new { r.PayrollDeductionFileId, r.DeductedAmount, r.DeductionDate, r.TransferDueDate, r.PaymasterArrearsThresholdDays })
            .ToListAsync(ct);
        var ids = pendientes.Select(p => p.PayrollDeductionFileId).Distinct().ToList();
        var deArchivo = await db.PayrollDeductionFiles.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        IReadOnlyList<PendingPayrollTransferDto> lista = pendientes.GroupBy(p => p.PayrollDeductionFileId).Select(g =>
            {
                var f = deArchivo[g.Key];
                var vence = g.Min(p => p.TransferDueDate);
                var umbral = g.Select(p => p.PaymasterArrearsThresholdDays).FirstOrDefault(u => u is not null);
                return new PendingPayrollTransferDto(f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber, f.PublicId, f.Number, g.Count(), g.Sum(p => p.DeductedAmount),
                    g.Min(p => p.DeductionDate), vence, PendientesDeGiro.DiasDeMora(vence, fecha), umbral, PendientesDeGiro.SuperaElUmbral(vence, umbral, fecha));
            })
            .OrderBy(d => d.Year).ThenBy(d => d.Month).ThenBy(d => d.SubPeriod).ToList();
        return Result.Success(lista);
    }
}
