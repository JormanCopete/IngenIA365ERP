using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

// Feature 015, US2 (T0094; FR-036, FR-183): los faltantes a cargo del cajero (la autorización escrita para descontarlos por Nómina), los sobrantes (su paso
// a ingreso tras Caja.Sobrantes.DiasAntesDeIngreso y con aprobación) y la consulta y explicación del cuadre diario. (nuevo)

/// <summary>Lo común: la sesión y la oficina de una línea de arqueo, si está al alcance. (nuevo)</summary>
internal static class LineasDeArqueo
{
    public static async Task<(CashCountLine Linea, CashSession Sesion, int Oficina)?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, int lineaId,
        CancellationToken ct)
    {
        var fila = await (from l in db.ReceiptsCashCountLines.AsNoTracking()
                          join c in db.ReceiptsCashCounts.AsNoTracking() on l.CashCountId equals c.Id
                          join s in db.ReceiptsCashSessions.AsNoTracking() on c.CashSessionId equals s.Id
                          join b in db.CashBoxes.AsNoTracking() on s.CashBoxId equals b.Id
                          join p in db.CollectionPoints.AsNoTracking() on b.CollectionPointId equals p.Id
                          where l.Id == lineaId
                          select new { l, s, CajaId = b.Id, PuntoId = p.Id, p.BranchId }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;
        var mio = await alcance.ObtenerAsync(ct);
        return mio.IncluyeCaja(fila.CajaId, fila.PuntoId) || mio.IncluyeOficina(fila.BranchId) ? (fila.l, fila.s, fila.BranchId) : null;
    }
}

/// <summary><c>GET /api/receipts/cashier-receivables?state=</c> (<c>Receipts.Session.View</c>): los faltantes a cargo de cajeros al alcance. (nuevo)</summary>
public sealed record ListCashierReceivablesQuery(CashierReceivableState? State = null) : IRequest<Result<IReadOnlyList<CashierReceivableDto>>>;

public sealed class ListCashierReceivablesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCashierReceivablesQuery, Result<IReadOnlyList<CashierReceivableDto>>>
{
    public async Task<Result<IReadOnlyList<CashierReceivableDto>>> Handle(ListCashierReceivablesQuery request, CancellationToken ct)
    {
        var filas = await (from r in db.CashierReceivables.AsNoTracking()
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on r.CashierPersonId equals p.Id
                           where request.State == null || r.Status == request.State
                           orderby r.Id descending
                           select new { r, p.PublicId }).Take(500).ToListAsync(ct);
        var lista = new List<CashierReceivableDto>();
        foreach (var f in filas)
            if (await LineasDeArqueo.VisibleAsync(db, alcance, f.r.CashCountLineId, ct) is not null)
                lista.Add(new CashierReceivableDto(f.r.PublicId, f.PublicId, f.r.Amount, f.r.Balance, f.r.PayrollDeductionAuthorizationAttachmentPublicId is not null, f.r.Status));
        return Result.Success<IReadOnlyList<CashierReceivableDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/receipts/cashier-receivables/{id}/payroll-authorization</c> (<c>Receipts.Count.ApproveDifference</c>): adjunta la autorización escrita del
/// cajero para descontar <b>este</b> faltante por Nómina (FR-183). Sin ella, el faltante no se descuenta: el descuento lo programa Nómina leyendo el faltante
/// con su autorización. La autorización se fija una vez. (nuevo)
/// </summary>
public sealed record AttachPayrollDeductionAuthorizationCommand(Guid ReceivablePublicId, Guid AttachmentPublicId)
    : IRequest<Result<CashierReceivableDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class AttachPayrollDeductionAuthorizationCommandValidator : AbstractValidator<AttachPayrollDeductionAuthorizationCommand>
{
    public AttachPayrollDeductionAuthorizationCommandValidator()
    {
        RuleFor(x => x.ReceivablePublicId).NotEmpty();
        RuleFor(x => x.AttachmentPublicId).NotEmpty().WithMessage("Adjunte la autorización escrita del cajero.");
    }
}

public sealed class AttachPayrollDeductionAuthorizationCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<AttachPayrollDeductionAuthorizationCommand, Result<CashierReceivableDto>>
{
    public async Task<Result<CashierReceivableDto>> Handle(AttachPayrollDeductionAuthorizationCommand request, CancellationToken ct)
    {
        var faltante = await db.CashierReceivables.FirstOrDefaultAsync(r => r.PublicId == request.ReceivablePublicId, ct);
        var visible = faltante is null ? null : await LineasDeArqueo.VisibleAsync(db, alcance, faltante.CashCountLineId, ct);
        if (faltante is null || visible is not { } v) return Result.Failure<CashierReceivableDto>(ErroresDeRecaudo.NoEncontrado("El faltante"));
        var sesion = v.Sesion;
        if (faltante.Status != CashierReceivableState.Open) return Result.Failure<CashierReceivableDto>(ErroresDeRecaudo.MovimientoInvalido("El faltante ya no está abierto."));
        var adjunto = await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == request.AttachmentPublicId
            && a.OwnerEntityType == Attachments.Common.AdjuntosDeModulo.SoporteDeCaja && a.OwnerEntityPublicId == sesion.PublicId, ct);
        if (!adjunto) return Result.Failure<CashierReceivableDto>(ErroresDeRecaudo.AutorizacionDeDescuento());
        faltante.PayrollDeductionAuthorizationAttachmentPublicId ??= request.AttachmentPublicId;
        await db.SaveChangesAsync(ct);
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == faltante.CashierPersonId).Select(p => p.PublicId).FirstAsync(ct);
        return Result.Success(new CashierReceivableDto(faltante.PublicId, persona, faltante.Amount, faltante.Balance, true, faltante.Status));
    }
}

/// <summary><c>GET /api/receipts/cash-surpluses?state=</c> (<c>Receipts.Session.View</c>): los sobrantes al alcance. (nuevo)</summary>
public sealed record ListCashSurplusesQuery(CashSurplusState? State = null) : IRequest<Result<IReadOnlyList<CashSurplusDto>>>;

public sealed class ListCashSurplusesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCashSurplusesQuery, Result<IReadOnlyList<CashSurplusDto>>>
{
    public async Task<Result<IReadOnlyList<CashSurplusDto>>> Handle(ListCashSurplusesQuery request, CancellationToken ct)
    {
        var filas = await db.CashSurpluses.AsNoTracking().Where(s => request.State == null || s.Status == request.State).OrderByDescending(s => s.Id)
            .Take(500).ToListAsync(ct);
        var lista = new List<CashSurplusDto>();
        foreach (var s in filas)
            if (await LineasDeArqueo.VisibleAsync(db, alcance, s.CashCountLineId, ct) is not null)
                lista.Add(new CashSurplusDto(s.PublicId, s.Amount, s.ToIncomeFrom, s.Status));
        return Result.Success<IReadOnlyList<CashSurplusDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/receipts/cash-surpluses/{id}/to-income</c> (<c>Receipts.Count.ApproveDifference</c>): un sobrante pasa a ingreso sólo desde
/// <see cref="CashSurplus.ToIncomeFrom"/> (conteo + <c>Caja.Sobrantes.DiasAntesDeIngreso</c>; sin valor del parámetro, nunca) y lo aprueba otra persona que el
/// cajero de la sesión (FR-183). Emite <c>SobranteLlevadoAIngreso</c>. (nuevo)
/// </summary>
public sealed record MoveSurplusToIncomeCommand(Guid SurplusPublicId) : IRequest<Result<CashSurplusDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class MoveSurplusToIncomeCommandValidator : AbstractValidator<MoveSurplusToIncomeCommand>
{
    public MoveSurplusToIncomeCommandValidator() => RuleFor(x => x.SurplusPublicId).NotEmpty();
}

public sealed class MoveSurplusToIncomeCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    SegregacionDeRecaudo segregacion,
    EmisionDeRecaudo emision) : IRequestHandler<MoveSurplusToIncomeCommand, Result<CashSurplusDto>>
{
    public async Task<Result<CashSurplusDto>> Handle(MoveSurplusToIncomeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } aprobador) return Result.Failure<CashSurplusDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var sobrante = await db.CashSurpluses.FirstOrDefaultAsync(s => s.PublicId == request.SurplusPublicId, ct);
        var visible = sobrante is null ? null : await LineasDeArqueo.VisibleAsync(db, alcance, sobrante.CashCountLineId, ct);
        if (sobrante is null || visible is not { } v) return Result.Failure<CashSurplusDto>(ErroresDeRecaudo.NoEncontrado("El sobrante"));
        var (linea, sesion, oficina) = v;
        if (sobrante.Status != CashSurplusState.Liability) return Result.Failure<CashSurplusDto>(ErroresDeRecaudo.SobranteNoEsPasivo());
        if (sobrante.ToIncomeFrom is not { } desde || desde > hoy) return Result.Failure<CashSurplusDto>(ErroresDeRecaudo.SobranteNoVencido(sobrante.ToIncomeFrom));
        if (await segregacion.MismaPersonaAsync(aprobador, sesion.CashierUserId, ct)) return Result.Failure<CashSurplusDto>(ErroresDeRecaudo.AutoaprobacionDeArqueo());

        await cerrojo.TomarSetupAsync(ct: ct);
        sobrante.Status = CashSurplusState.MovedToIncome;
        sobrante.MovedToIncomeAt = reloj.UtcNow;
        await emision.SobranteAIngresoAsync(sobrante, linea, oficina, aprobador, hoy, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CashSurplusDto(sobrante.PublicId, sobrante.Amount, sobrante.ToIncomeFrom, sobrante.Status));
    }
}

/// <summary><c>GET /api/receipts/daily-squares?branch=&amp;date=</c> (<c>Receipts.Reports.View</c>): el cuadre diario de las oficinas al alcance. (nuevo)</summary>
public sealed record ListDailySquaresQuery(Guid? BranchPublicId = null, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<DailySquareDto>>>;

public sealed class ListDailySquaresQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListDailySquaresQuery, Result<IReadOnlyList<DailySquareDto>>>
{
    public async Task<Result<IReadOnlyList<DailySquareDto>>> Handle(ListDailySquaresQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var filas = await (from q in db.DailySquares.AsNoTracking()
                           join b in db.Branches.AsNoTracking() on q.BranchId equals b.Id
                           where (request.BranchPublicId == null || b.PublicId == request.BranchPublicId) && (request.Date == null || q.BusinessDate == request.Date)
                           orderby q.BusinessDate descending, b.Name
                           select new { q, b.PublicId, b.Name }).Take(500).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DailySquareDto>>(filas.Where(f => mio.IncluyeOficina(f.q.BranchId))
            .Select(f => CuadresDiarios.Dto(f.q, f.PublicId, f.Name)).ToList());
    }
}

/// <summary>
/// <c>POST /api/receipts/daily-squares/{id}/explain</c> (<c>Receipts.Day.Close</c>): una persona explica la diferencia del cuadre; queda <c>Explained</c> y la
/// tarea ya no la recalcula. (nuevo)
/// </summary>
public sealed record ExplainDailySquareCommand(Guid PublicId, string Explanation) : IRequest<Result<DailySquareDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ExplainDailySquareCommandValidator : AbstractValidator<ExplainDailySquareCommand>
{
    public ExplainDailySquareCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Explanation).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ExplainDailySquareCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IActorActual actorActual)
    : IRequestHandler<ExplainDailySquareCommand, Result<DailySquareDto>>
{
    public async Task<Result<DailySquareDto>> Handle(ExplainDailySquareCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var cuadre = await db.DailySquares.FirstOrDefaultAsync(q => q.PublicId == request.PublicId, ct);
        if (cuadre is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(cuadre.BranchId))
            return Result.Failure<DailySquareDto>(ErroresDeRecaudo.NoEncontrado("El cuadre"));
        cuadre.Explanation = request.Explanation.Trim();
        cuadre.ExplainedByUserId = actor.UserId;
        cuadre.Status = DailySquareStatus.Explained;
        await db.SaveChangesAsync(ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == cuadre.BranchId).Select(b => new { b.PublicId, b.Name }).FirstAsync(ct);
        return Result.Success(CuadresDiarios.Dto(cuadre, oficina.PublicId, oficina.Name));
    }
}

/// <summary>El DTO del cuadre diario. (nuevo)</summary>
public static class CuadresDiarios
{
    public static DailySquareDto Dto(DailySquare q, Guid oficina, string nombre) => new(q.PublicId, oficina, nombre, q.BusinessDate, q.CashInBoxes, q.CashInVault,
        q.CashInTransit, q.LedgerBalance, q.EmittedPendingBatch, q.Difference, q.Status, q.VaultBalanceMatches, q.Explanation);
}
