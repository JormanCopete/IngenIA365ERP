using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Liquidity;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Treasury.Liquidity;

// Feature 015, T2 (fase 43, T0461; FR-137 a FR-139, FR-155 parte T2; US20; contracts/api.md §13): la posición diaria, su foto con el saldo del fondo de liquidez a
// Captaciones, el flujo de caja proyectado por semanas y meses con categorías configurables y real contra proyectado (reemplaza al flujo heredado), y el tablero de
// Tesorería sin datos personales. (nuevo)

/// <summary>Una foto de la posición. (nuevo)</summary>
public sealed record LiquiditySnapshotDto(Guid PublicId, string Number, DateOnly Date, DateTime TakenAt, decimal Banks, decimal Vaults, decimal CashSessions,
    decimal Commitments, decimal? ExpectedLending, decimal? ExpectedDeposits, decimal Available, decimal Projected, decimal LiquidityFund, int MissingCount,
    Guid? FundMessagePublicId);

/// <summary>Una categoría del flujo de caja. (nuevo)</summary>
public sealed record CashFlowCategoryDto(Guid PublicId, string Code, string Name, CashFlowDirection Direction, CashFlowCategorySource Source, int SortOrder, bool IsActive);

/// <summary>Lo proyectado de una categoría en un período: digitado o automático. (nuevo)</summary>
public sealed record LineaDeFlujoDto(Guid CategoryPublicId, string Code, string Name, CashFlowDirection Direction, CashFlowCategorySource Source, decimal Projected,
    bool Automatic);

/// <summary>Un período del flujo: lo proyectado por categoría y, si ya empezó, lo real de los libros de las cuentas bancarias. (nuevo)</summary>
public sealed record PeriodoDeFlujoDto(DateOnly Start, DateOnly End, IReadOnlyList<LineaDeFlujoDto> Lines, decimal ProjectedInflows, decimal ProjectedOutflows,
    decimal? RealInflows, decimal? RealOutflows);

/// <summary>El flujo de caja. (nuevo)</summary>
public sealed record CashFlowDto(CashFlowPeriodKind PeriodKind, IReadOnlyList<PeriodoDeFlujoDto> Periods, IReadOnlyList<ComponenteFaltanteDto> Missing);

/// <summary>El tablero de Tesorería, sin datos personales (FR-155). (nuevo)</summary>
public sealed record TreasuryDashboardDto(DateOnly Date, decimal Available, decimal Projected, decimal Banks, decimal LiquidityFund, int OpenOrders, decimal OpenOrdersAmount,
    int BatchesPendingApproval, int ChecksInTransit, decimal ChecksInTransitAmount, int OpenReconciliations, int BankNotesPendingApproval, DateOnly? LastSnapshotDate,
    int MissingComponents);

// ------------------------------------------------------------------------------------------------------------- posición --

/// <summary><c>GET /api/treasury/liquidity?date=&amp;days=</c> (<c>Treasury.Liquidity.View</c>; FR-137): la posición del día con lo esperado en los días pedidos. (nuevo)</summary>
public sealed record GetLiquidityPositionQuery(DateOnly? Date = null, int? Days = null) : IRequest<Result<LiquidityPositionDto>>;

public sealed class GetLiquidityPositionQueryHandler(PosicionDeLiquidez posicion, IDateTimeService reloj) : IRequestHandler<GetLiquidityPositionQuery, Result<LiquidityPositionDto>>
{
    public async Task<Result<LiquidityPositionDto>> Handle(GetLiquidityPositionQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var dias = Math.Max(1, request.Days ?? 1);
        return Result.Success(await posicion.CalcularAsync(fecha, fecha.AddDays(dias - 1), ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/liquidity/snapshots</c> (<c>Treasury.Liquidity.Manage</c>, con clave; FR-137, FR-139; E-02 e): guarda la foto de la posición del día y, si hay
/// cuentas del fondo de liquidez, emite en el mismo guardado <c>SaldoDelFondoDeLiquidez</c> a Captaciones, originado por quien la toma (el tesorero o quien concilia). (nuevo)
/// </summary>
public sealed record TakeLiquiditySnapshotCommand(DateOnly? Date = null) : IRequest<Result<LiquiditySnapshotDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class TakeLiquiditySnapshotCommandHandler(
    IApplicationDbContext db,
    PosicionDeLiquidez posicion,
    IActorActual actorActual,
    NumeradorDeTesoreria numerador,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<TakeLiquiditySnapshotCommand, Result<LiquiditySnapshotDto>>
{
    public const string CodigoDeFoto = "LQ";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<LiquiditySnapshotDto>> Handle(TakeLiquiditySnapshotCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LiquiditySnapshotDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var fecha = request.Date ?? hoy;
        if (fecha > hoy) return Result.Failure<LiquiditySnapshotDto>(ErroresDeTesoreriaT2Liquidez.FotoFutura());
        var p = await posicion.CalcularAsync(fecha, fecha, ct);
        var foto = new LiquiditySnapshot
        {
            Number = await numerador.AsignarAsync(CodigoDeFoto, CodigoDeFoto, ct), Date = fecha, TakenAt = reloj.UtcNow, TakenByUserId = usuario, BanksTotal = p.Banks,
            VaultsTotal = p.Vaults, CashSessionsTotal = p.CashSessions, CommitmentsTotal = p.Commitments, ExpectedLending = p.ExpectedLending,
            ExpectedDeposits = p.ExpectedDeposits, Available = p.Available, Projected = p.Projected, LiquidityFundTotal = p.LiquidityFund,
            ComponentsJson = JsonSerializer.Serialize(p.Components, Json), MissingComponentsJson = JsonSerializer.Serialize(p.Missing, Json),
        };
        db.TreasuryLiquiditySnapshots.Add(foto);
        if (p.FundAccounts.Count > 0)
        {
            var primera = p.FundAccounts[0].OwnBankAccountPublicId;
            var oficina = await db.OwnBankAccounts.AsNoTracking().Where(c => c.PublicId == primera).Select(c => c.BranchId).FirstAsync(ct);
            var mensaje = await emision.SaldoDelFondoDeLiquidezAsync(foto, p.FundAccounts, oficina, ct);
            foto.FundMessagePublicId = mensaje.PublicId;
        }
        await auditoria.AgregarAsync("Treasury.Liquidity.SnapshotTaken", "LiquiditySnapshot", foto.PublicId, null,
            new { number = foto.Number, date = fecha, available = p.Available, projected = p.Projected, fund = p.LiquidityFund, missing = p.Missing.Select(m => m.Code) }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(LiquidezDeTesoreria.Dto(foto));
    }
}

/// <summary><c>GET /api/treasury/liquidity/snapshots?from=&amp;to=</c> (<c>Treasury.Liquidity.View</c>). (nuevo)</summary>
public sealed record ListLiquiditySnapshotsQuery(DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<LiquiditySnapshotDto>>>;

public sealed class ListLiquiditySnapshotsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListLiquiditySnapshotsQuery, Result<IReadOnlyList<LiquiditySnapshotDto>>>
{
    public async Task<Result<IReadOnlyList<LiquiditySnapshotDto>>> Handle(ListLiquiditySnapshotsQuery request, CancellationToken ct)
    {
        var hasta = request.To ?? reloj.HoyLocal;
        var desde = request.From ?? hasta.AddDays(-30);
        var fotos = await db.TreasuryLiquiditySnapshots.AsNoTracking().Where(f => f.Date >= desde && f.Date <= hasta).OrderByDescending(f => f.TakenAt).ToListAsync(ct);
        return Result.Success<IReadOnlyList<LiquiditySnapshotDto>>(fotos.Select(LiquidezDeTesoreria.Dto).ToList());
    }
}

// ------------------------------------------------------------------------------------------------------- flujo de caja --

/// <summary><c>POST /api/treasury/cash-flow-projections/categories</c> (<c>Treasury.Liquidity.Manage</c>, con clave; FR-138): crea o corrige una categoría. (nuevo)</summary>
public sealed record SaveCashFlowCategoryCommand(Guid? PublicId, string Code, string Name, CashFlowDirection Direction, CashFlowCategorySource Source, int SortOrder,
    bool IsActive = true) : IRequest<Result<CashFlowCategoryDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveCashFlowCategoryCommandValidator : AbstractValidator<SaveCashFlowCategoryCommand>
{
    public SaveCashFlowCategoryCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x).Must(x => LiquidezDeTesoreria.SentidoDeLaFuente(x.Source) is not { } s || s == x.Direction)
            .WithMessage("El sentido de la categoría no corresponde a su fuente: los recaudos esperados son entradas; las órdenes y la nómina, salidas.");
    }
}

public sealed class SaveCashFlowCategoryCommandHandler(IApplicationDbContext db, AuditoriaDeTesoreria auditoria)
    : IRequestHandler<SaveCashFlowCategoryCommand, Result<CashFlowCategoryDto>>
{
    public async Task<Result<CashFlowCategoryDto>> Handle(SaveCashFlowCategoryCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.TreasuryCashFlowCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo && c.PublicId != request.PublicId, ct) is { } otra)
            return Result.Failure<CashFlowCategoryDto>(CodigoDeCatalogo.Duplicado("una categoría del flujo de caja", codigo, otra.Name, otra.PublicId));
        CashFlowCategory categoria;
        if (request.PublicId is { } id)
        {
            var existente = await db.TreasuryCashFlowCategories.FirstOrDefaultAsync(c => c.PublicId == id, ct);
            if (existente is null) return Result.Failure<CashFlowCategoryDto>(ErroresDeTesoreria.NoEncontrado("La categoría del flujo de caja"));
            categoria = existente;
        }
        else
        {
            categoria = new CashFlowCategory();
            db.TreasuryCashFlowCategories.Add(categoria);
        }
        (categoria.Code, categoria.Name, categoria.Direction, categoria.Source, categoria.SortOrder, categoria.IsActive) =
            (codigo, request.Name.Trim(), request.Direction, request.Source, request.SortOrder, request.IsActive);
        await auditoria.AgregarAsync("Treasury.CashFlowCategory.Saved", "CashFlowCategory", categoria.PublicId, null,
            new { code = codigo, direction = request.Direction.ToString(), source = request.Source.ToString(), request.SortOrder, request.IsActive }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(LiquidezDeTesoreria.Dto(categoria));
    }
}

/// <summary><c>GET /api/treasury/cash-flow-projections/categories</c> (<c>Treasury.Liquidity.View</c>). (nuevo)</summary>
public sealed record ListCashFlowCategoriesQuery : IRequest<Result<IReadOnlyList<CashFlowCategoryDto>>>;

public sealed class ListCashFlowCategoriesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCashFlowCategoriesQuery, Result<IReadOnlyList<CashFlowCategoryDto>>>
{
    public async Task<Result<IReadOnlyList<CashFlowCategoryDto>>> Handle(ListCashFlowCategoriesQuery request, CancellationToken ct)
    {
        var filas = await db.TreasuryCashFlowCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<CashFlowCategoryDto>>(filas.Select(LiquidezDeTesoreria.Dto).ToList());
    }
}

/// <summary>
/// <c>POST /api/treasury/cash-flow-projections/entries</c> (<c>Treasury.Liquidity.Manage</c>, con clave; FR-138): lo proyectado a mano de una categoría para un período (la
/// semana desde su lunes o el mes desde su día primero). Corregirla reemplaza el valor; en una categoría automática, lo digitado manda sobre lo calculado. (nuevo)
/// </summary>
public sealed record SaveCashFlowProjectionCommand(Guid CategoryPublicId, CashFlowPeriodKind PeriodKind, DateOnly PeriodStart, decimal Amount, string? Notes = null)
    : IRequest<Result<LineaDeFlujoDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveCashFlowProjectionCommandValidator : AbstractValidator<SaveCashFlowProjectionCommand>
{
    public SaveCashFlowProjectionCommandValidator()
    {
        RuleFor(x => x.CategoryPublicId).NotEmpty();
        RuleFor(x => x.PeriodKind).IsInEnum();
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class SaveCashFlowProjectionCommandHandler(IApplicationDbContext db, IActorActual actorActual, AuditoriaDeTesoreria auditoria)
    : IRequestHandler<SaveCashFlowProjectionCommand, Result<LineaDeFlujoDto>>
{
    public async Task<Result<LineaDeFlujoDto>> Handle(SaveCashFlowProjectionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LineaDeFlujoDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var categoria = await db.TreasuryCashFlowCategories.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.CategoryPublicId, ct);
        if (categoria is null) return Result.Failure<LineaDeFlujoDto>(ErroresDeTesoreria.NoEncontrado("La categoría del flujo de caja"));
        var inicio = LiquidezDeTesoreria.InicioDelPeriodo(request.PeriodKind, request.PeriodStart);
        var fila = await db.TreasuryCashFlowProjections.FirstOrDefaultAsync(p => p.CategoryId == categoria.Id && p.PeriodKind == request.PeriodKind && p.PeriodStart == inicio, ct);
        var antes = fila?.Amount;
        if (fila is null)
        {
            fila = new CashFlowProjection { CategoryId = categoria.Id, PeriodKind = request.PeriodKind, PeriodStart = inicio };
            db.TreasuryCashFlowProjections.Add(fila);
        }
        (fila.Amount, fila.Notes, fila.RegisteredByUserId) = (request.Amount, string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(), usuario);
        await auditoria.AgregarAsync("Treasury.CashFlowProjection.Saved", "CashFlowProjection", fila.PublicId, antes is null ? null : new { amount = antes },
            new { category = categoria.Code, kind = request.PeriodKind.ToString(), start = inicio, amount = request.Amount }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new LineaDeFlujoDto(categoria.PublicId, categoria.Code, categoria.Name, categoria.Direction, categoria.Source, fila.Amount, false));
    }
}

/// <summary>
/// <c>GET /api/treasury/cash-flow-projections?kind=&amp;from=&amp;periods=</c> (<c>Treasury.Liquidity.View</c>; FR-138): el flujo por semanas o meses. Por categoría activa, lo
/// digitado del período o, en una categoría automática sin digitar, lo programado de su fuente (cuotas de Cartera y Captaciones, órdenes por su fecha de pago, dispersiones
/// de Nómina). Lo real de un período que ya empezó son los débitos (entradas) y créditos (salidas) contabilizados de las cuentas bancarias propias, hasta hoy: así el real de
/// un mes cerrado coincide con Contabilidad (US20 «Independent Test»). Sin <c>periods</c>, <c>Liquidez.SemanasDeProyeccion</c> (en meses, su equivalente). (nuevo)
/// </summary>
public sealed record GetCashFlowQuery(CashFlowPeriodKind Kind = CashFlowPeriodKind.Week, DateOnly? From = null, int? Periods = null) : IRequest<Result<CashFlowDto>>;

public sealed class GetCashFlowQueryHandler(
    IApplicationDbContext db,
    IContabilidadParaTesoreria contabilidad,
    ParametrosDeTesoreria parametros,
    IDateTimeService reloj,
    ILogger<GetCashFlowQueryHandler> logger,
    IRecaudosEsperadosDeCartera? cartera = null,
    IRecaudosEsperadosDeCaptaciones? captaciones = null) : IRequestHandler<GetCashFlowQuery, Result<CashFlowDto>>
{
    public async Task<Result<CashFlowDto>> Handle(GetCashFlowQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var semanas = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.LiquidezSemanasDeProyeccion, hoy, ct) ?? 1;
        var cantidad = Math.Max(1, request.Periods ?? (request.Kind == CashFlowPeriodKind.Week ? semanas : Math.Max(1, (semanas * 7 + 29) / 30)));
        var inicio = LiquidezDeTesoreria.InicioDelPeriodo(request.Kind, request.From ?? hoy);
        var periodos = Enumerable.Range(0, cantidad).Select(i =>
        {
            var s = request.Kind == CashFlowPeriodKind.Week ? inicio.AddDays(7 * i) : inicio.AddMonths(i);
            var e = request.Kind == CashFlowPeriodKind.Week ? s.AddDays(6) : s.AddMonths(1).AddDays(-1);
            return (Start: s, End: e);
        }).ToList();
        var desde = periodos[0].Start;
        var hasta = periodos[^1].End;
        var faltan = new List<ComponenteFaltanteDto>();

        var categorias = await db.TreasuryCashFlowCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Code).ToListAsync(ct);
        var ids = categorias.Select(c => c.Id).ToList();
        var digitado = await db.TreasuryCashFlowProjections.AsNoTracking()
            .Where(p => ids.Contains(p.CategoryId) && p.PeriodKind == request.Kind && p.PeriodStart >= desde && p.PeriodStart <= hasta)
            .ToDictionaryAsync(p => (p.CategoryId, p.PeriodStart), p => p.Amount, ct);

        var fuentes = categorias.Select(c => c.Source).ToHashSet();
        var porDia = new Dictionary<CashFlowCategorySource, List<(DateOnly Fecha, decimal Valor)>>();
        if (fuentes.Contains(CashFlowCategorySource.ExpectedLending))
            porDia[CashFlowCategorySource.ExpectedLending] = await EsperadoAsync(cartera is null ? null : (d, h) => cartera.PorFechaAsync(d, h, ct),
                PosicionDeLiquidez.CodigoCartera, "Cartera", desde, hasta, faltan);
        if (fuentes.Contains(CashFlowCategorySource.ExpectedDeposits))
            porDia[CashFlowCategorySource.ExpectedDeposits] = await EsperadoAsync(captaciones is null ? null : (d, h) => captaciones.PorFechaAsync(d, h, ct),
                PosicionDeLiquidez.CodigoCaptaciones, "Captaciones", desde, hasta, faltan);
        if (fuentes.Contains(CashFlowCategorySource.PaymentOrders))
            porDia[CashFlowCategorySource.PaymentOrders] = (await db.PaymentOrders.AsNoTracking()
                    .Where(o => o.PayDate >= desde && o.PayDate <= hasta && o.Status != PaymentOrderState.Voided && o.Status != PaymentOrderState.RejectedOnArrival)
                    .Select(o => new { o.PayDate, o.NetAmount }).ToListAsync(ct))
                .Select(o => (o.PayDate, o.NetAmount)).ToList();
        if (fuentes.Contains(CashFlowCategorySource.PayrollDispersions))
            porDia[CashFlowCategorySource.PayrollDispersions] = (await db.TreasuryPayrollDispersions.AsNoTracking().Where(d => d.PayDate >= desde && d.PayDate <= hasta)
                    .Select(d => new { d.PayDate, d.Total }).ToListAsync(ct))
                .Select(d => (d.PayDate, d.Total)).ToList();

        // Lo real: los movimientos contabilizados de las cuentas bancarias propias hasta hoy.
        List<(DateOnly Fecha, decimal Entra, decimal Sale)>? real = null;
        if (desde <= hoy)
        {
            real = [];
            var finReal = hasta < hoy ? hasta : hoy;
            var cuentas = await db.OwnBankAccounts.AsNoTracking().Select(c => new { c.PublicId, c.Code }).ToListAsync(ct);
            foreach (var c in cuentas)
            {
                try
                {
                    real.AddRange((await contabilidad.MovimientosDeCuentaBancariaAsync(c.PublicId, desde, finReal, ct)).Select(m => (m.Date, m.Debit, m.Credit)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "[Tesoreria.FlujoDeCaja] Contabilidad no dio los movimientos de la cuenta {Cuenta}.", c.Code);
                    faltan.Add(new ComponenteFaltanteDto($"REAL-{c.Code}", $"Contabilidad no respondió los movimientos de la cuenta {c.Code}: lo real no la incluye."));
                }
            }
        }

        var resultado = periodos.Select(p =>
        {
            var lineas = categorias.Select(c =>
            {
                if (digitado.TryGetValue((c.Id, p.Start), out var valor))
                    return new LineaDeFlujoDto(c.PublicId, c.Code, c.Name, c.Direction, c.Source, valor, false);
                var auto = c.Source != CashFlowCategorySource.Manual && porDia.TryGetValue(c.Source, out var dias)
                    ? dias.Where(d => d.Fecha >= p.Start && d.Fecha <= p.End).Sum(d => d.Valor)
                    : 0m;
                return new LineaDeFlujoDto(c.PublicId, c.Code, c.Name, c.Direction, c.Source, auto, c.Source != CashFlowCategorySource.Manual);
            }).ToList();
            var empezo = p.Start <= hoy && real is not null;
            return new PeriodoDeFlujoDto(p.Start, p.End, lineas,
                lineas.Where(l => l.Direction == CashFlowDirection.Inflow).Sum(l => l.Projected),
                lineas.Where(l => l.Direction == CashFlowDirection.Outflow).Sum(l => l.Projected),
                empezo ? real!.Where(r => r.Fecha >= p.Start && r.Fecha <= p.End).Sum(r => r.Entra) : null,
                empezo ? real!.Where(r => r.Fecha >= p.Start && r.Fecha <= p.End).Sum(r => r.Sale) : null);
        }).ToList();
        return Result.Success(new CashFlowDto(request.Kind, resultado, faltan));
    }

    private async Task<List<(DateOnly Fecha, decimal Valor)>> EsperadoAsync(Func<DateOnly, DateOnly, Task<IReadOnlyList<RecaudoEsperadoDelDiaDto>>>? consulta,
        string codigo, string modulo, DateOnly desde, DateOnly hasta, List<ComponenteFaltanteDto> faltan)
    {
        if (consulta is null)
        {
            faltan.Add(new ComponenteFaltanteDto(codigo, $"{modulo} no ofrece sus recaudos esperados en esta instalación."));
            return [];
        }
        try
        {
            return (await consulta(desde, hasta)).Select(d => (d.Date, d.Amount)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Tesoreria.FlujoDeCaja] La consulta de recaudos esperados de {Modulo} no respondió.", modulo);
            faltan.Add(new ComponenteFaltanteDto(codigo, $"La consulta de {modulo} no respondió: su categoría sale en cero."));
            return [];
        }
    }
}

// ------------------------------------------------------------------------------------------------------------- tablero --

/// <summary><c>GET /api/treasury/dashboard</c> (<c>Treasury.Liquidity.View</c>; FR-155 parte T2): las cifras de Tesorería del día, sin datos personales. (nuevo)</summary>
public sealed record GetTreasuryDashboardQuery : IRequest<Result<TreasuryDashboardDto>>;

public sealed class GetTreasuryDashboardQueryHandler(IApplicationDbContext db, PosicionDeLiquidez posicion, IDateTimeService reloj)
    : IRequestHandler<GetTreasuryDashboardQuery, Result<TreasuryDashboardDto>>
{
    public async Task<Result<TreasuryDashboardDto>> Handle(GetTreasuryDashboardQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var p = await posicion.CalcularAsync(hoy, hoy, ct);
        var abiertas = PosicionDeLiquidez.EnBandeja.Concat(PosicionDeLiquidez.EnLotesAprobados).ToList();
        var ordenes = await db.PaymentOrders.AsNoTracking().Where(o => abiertas.Contains(o.Status)).Select(o => o.NetAmount).ToListAsync(ct);
        var lotes = await db.PaymentBatches.AsNoTracking().CountAsync(b => b.Status == PaymentBatchState.PendingApproval, ct);
        var cheques = await db.IssuedChecks.AsNoTracking()
            .Where(c => (c.Status == IssuedCheckState.Issued || c.Status == IssuedCheckState.Delivered) && c.MovedToPayableRunId == null).Select(c => c.Amount).ToListAsync(ct);
        var conciliaciones = await db.TreasuryReconciliations.AsNoTracking().CountAsync(r => r.Status == ReconciliationState.Open, ct);
        var notas = await db.TreasuryBankNotes.AsNoTracking().CountAsync(n => n.Status == BankNoteState.PendingApproval, ct);
        var ultima = await db.TreasuryLiquiditySnapshots.AsNoTracking().OrderByDescending(f => f.Date).Select(f => (DateOnly?)f.Date).FirstOrDefaultAsync(ct);
        return Result.Success(new TreasuryDashboardDto(hoy, p.Available, p.Projected, p.Banks, p.LiquidityFund, ordenes.Count, ordenes.Sum(), lotes, cheques.Count, cheques.Sum(),
            conciliaciones, notas, ultima, p.Missing.Count));
    }
}

// --------------------------------------------------------------------------------------------------------------- común --

/// <summary>Lo común de la liquidez. (nuevo)</summary>
public static class LiquidezDeTesoreria
{
    /// <summary>El lunes de la semana o el día primero del mes de <paramref name="fecha"/>.</summary>
    public static DateOnly InicioDelPeriodo(CashFlowPeriodKind clase, DateOnly fecha) => clase == CashFlowPeriodKind.Week
        ? fecha.AddDays(-(((int)fecha.DayOfWeek + 6) % 7))
        : new DateOnly(fecha.Year, fecha.Month, 1);

    /// <summary>El sentido que exige una fuente automática; nulo, cualquiera (digitada).</summary>
    public static CashFlowDirection? SentidoDeLaFuente(CashFlowCategorySource fuente) => fuente switch
    {
        CashFlowCategorySource.ExpectedLending or CashFlowCategorySource.ExpectedDeposits => CashFlowDirection.Inflow,
        CashFlowCategorySource.PaymentOrders or CashFlowCategorySource.PayrollDispersions => CashFlowDirection.Outflow,
        _ => null,
    };

    public static LiquiditySnapshotDto Dto(LiquiditySnapshot f) => new(f.PublicId, f.Number, f.Date, f.TakenAt, f.BanksTotal, f.VaultsTotal, f.CashSessionsTotal,
        f.CommitmentsTotal, f.ExpectedLending, f.ExpectedDeposits, f.Available, f.Projected, f.LiquidityFundTotal,
        JsonSerializer.Deserialize<List<ComponenteFaltanteDto>>(f.MissingComponentsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Count ?? 0,
        f.FundMessagePublicId);

    public static CashFlowCategoryDto Dto(CashFlowCategory c) => new(c.PublicId, c.Code, c.Name, c.Direction, c.Source, c.SortOrder, c.IsActive);
}

/// <summary>Los errores de la liquidez (feature 015, T2, fase 43; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeTesoreriaT2Liquidez
{
    public static Error FotoFutura() => new("Treasury.Liquidity.FutureSnapshot", "La foto de la posición es de hoy o de un día anterior.");
}
