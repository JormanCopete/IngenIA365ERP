using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>
/// 2026-11-24 (feature 025, fase 4, HU4; FR-015): lo que la opción «el período en curso al corte se causa completo» suma a un crédito de la carga: la primera
/// cuota posterior al corte, el inicio de su período, los días que el corte le dejó por fuera, el interés del período completo, lo que el motor causa desde
/// el corte, lo que trajo en la carga y el interés que suma. (nuevo)
/// </summary>
public sealed record FullPeriodInterestItemDto(string LoanNumber, int Installment, DateOnly PeriodStart, DateOnly DueDate, int MissingDays,
    decimal FullPeriodInterest, decimal FromCutoff, decimal Loaded, decimal Added);

/// <summary>2026-11-24 (feature 025, fase 4, HU4; FR-015): la vista previa de la opción: si está encendida, el total, los créditos que suman y el detalle. (nuevo)</summary>
public sealed record MigrationFullPeriodInterestDto(bool CauseFullInProgressPeriod, DateOnly CutoffDate, decimal Total, int Loans,
    IReadOnlyList<FullPeriodInterestItemDto> Items);

/// <summary>
/// La vista previa del interés que suma la opción del período completo (2026-11-24, feature 025, fase 4, HU4; FR-015;
/// <c>GET /api/lending/migration/loads/{id}/full-period-interest</c>): por crédito vivo de la carga, sobre su plan migrado con el inicio del período
/// (<see cref="NacimientoDeCreditosMigrados.CuotasDelPlan"/>) y con la regla de <see cref="CuadresDeMigracion.InteresDelPeriodoCompleto"/>. Se informa con la
/// opción encendida o apagada (apagada dice cuánto dejaría de sumar). <b>No</b> se cuadra contra el balance al corte: es ingreso del día de la activación.
/// Es una estimación antes de que nazcan los créditos: la activación lo calcula sobre la reconstrucción y deja fuera lo que llega con intereses en suspenso
/// (aquí también: el crédito con suspenso cargado no suma), en UVR o de pago único. (nuevo)
/// </summary>
public sealed record GetMigrationFullPeriodInterestQuery(Guid MigrationLoadPublicId) : IRequest<Result<MigrationFullPeriodInterestDto>>;

public sealed class GetMigrationFullPeriodInterestQueryValidator : AbstractValidator<GetMigrationFullPeriodInterestQuery>
{
    public GetMigrationFullPeriodInterestQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetMigrationFullPeriodInterestQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMigrationFullPeriodInterestQuery, Result<MigrationFullPeriodInterestDto>>
{
    public async Task<Result<MigrationFullPeriodInterestDto>> Handle(GetMigrationFullPeriodInterestQuery request, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted && l.Nature == LineNature.Placement, ct);
        if (carga is null) return Result.Failure<MigrationFullPeriodInterestDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        var corte = carga.CutoffDate;
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        var filas = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.SourceLoanNumber).ToListAsync(ct);
        var cuotas = (await db.MigrationInstallmentRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var saldos = (await db.MigrationBalanceRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(filas.Select(f => f.CreditLineId ?? 0).Where(i => i > 0).ToList(), corte, ct: ct);

        var items = new List<FullPeriodInterestItemDto>();
        foreach (var fila in filas)
        {
            if (versiones.GetValueOrDefault(fila.CreditLineId ?? 0) is not { } version) continue;
            var susCuotas = cuotas[fila.SourceLoanNumber].ToList();
            if (susCuotas.Count == 0) continue;
            var susSaldos = saldos[fila.SourceLoanNumber].ToList();
            // Lo que llega con intereses en suspenso tiene la causación suspendida: lo anterior al corte no es ingreso (la activación tampoco lo suma).
            if (susSaldos.Any(s => s.Amount > 0m && s.Concept is LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense)) continue;
            var condiciones = NacimientoDeCreditosMigrados.Condiciones(new Loan(), fila.TermsJson, version, null, corte, susCuotas);
            if (condiciones.DenominationUnit == DenominationUnit.Uvr || condiciones.Periodicity == RepaymentPeriodicity.SinglePayment) continue;
            var capital = susSaldos.Where(s => s.Concept == LoanConcept.Principal).Sum(s => s.Amount);
            if (CuadresDeMigracion.InteresDelPeriodoCompleto(condiciones, corte, capital, susCuotas, fila.DisbursementDate) is not { Ajuste: > 0m } p) continue;
            items.Add(new FullPeriodInterestItemDto(fila.SourceLoanNumber, p.CuotaDelCorte, p.InicioDelPeriodo, p.Vencimiento, p.DiasFaltantes, p.InteresDelPeriodo,
                p.YaCausado, p.Cargado, p.Ajuste));
        }
        return Result.Success(new MigrationFullPeriodInterestDto(carga.CauseFullInProgressPeriod, corte, items.Sum(i => i.Added), items.Count, items));
    }
}
