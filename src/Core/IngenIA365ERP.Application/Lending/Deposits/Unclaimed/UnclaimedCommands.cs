using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Unclaimed;

// Feature 014, US14 (T209, T212; contracts/api.md §14; FR-081, FR-084; research §23): la lista, el traslado a mano y el pago de los saldos no reclamados, y
// la vista «unclaimed» del centro de informes. (nuevo)

/// <summary>Un saldo no reclamado (api.md §14). (nuevo)</summary>
public sealed record UnclaimedBalanceDto(
    Guid PublicId,
    MemberAccountPersonDto Person,
    UnclaimedSource Source,
    Guid AccountPublicId,
    string AccountNumber,
    string? SourceReference,
    decimal Amount,
    DateOnly TransferredOn,
    string TransferOperationNumber,
    UnclaimedStatus Status,
    DateOnly? ClaimedOn,
    string? ClaimOrderNumber,
    string Reason);

/// <summary>Lo común de los saldos no reclamados: la lectura en el alcance y el DTO. (nuevo)</summary>
public static class VistaDeNoReclamados
{
    /// <summary>El saldo por su PublicId con su cuenta de origen en el alcance; fuera, el 404 del saldo.</summary>
    public static async Task<Result<UnclaimedBalance>> EnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.UnclaimedBalances : db.UnclaimedBalances.AsNoTracking();
        var saldo = await consulta.FirstOrDefaultAsync(u => u.PublicId == publicId && !u.IsDeleted, ct);
        if (saldo is null) return Result.Failure<UnclaimedBalance>(ErroresDeNoReclamados.Inexistente());
        var cuenta = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == saldo.SourceMemberAccountId).Select(a => new { a.BranchId, a.CreditLineId })
            .FirstAsync(ct);
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeNoReclamados.Inexistente(), auditoria,
            nameof(UnclaimedBalance), saldo.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<UnclaimedBalance>(dentro.Error) : Result.Success(saldo);
    }

    public static async Task<IReadOnlyList<UnclaimedBalanceDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<UnclaimedBalance> saldos, CancellationToken ct)
    {
        if (saldos.Count == 0) return [];
        var personas = await LectorDeCuentas.PersonasAsync(db, saldos.Select(s => s.PersonId).Distinct(), ct);
        var cuentasIds = saldos.Select(s => s.SourceMemberAccountId).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => cuentasIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => new { a.PublicId, a.Number }, ct);
        var ordenesIds = saldos.SelectMany(s => new[] { s.SourcePayoutOrderId, s.ClaimPayoutOrderId }).Where(x => x is not null).Select(x => x!.Value).Distinct().ToList();
        var ordenes = await db.AccountPayoutOrders.AsNoTracking().Where(o => ordenesIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        var opsIds = saldos.Select(s => s.TransferOperationId).ToList();
        var ops = await db.MemberAccountOperations.AsNoTracking().Where(o => opsIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        return saldos.Select(s => new UnclaimedBalanceDto(s.PublicId, personas[s.PersonId], s.Source, cuentas[s.SourceMemberAccountId].PublicId,
                cuentas[s.SourceMemberAccountId].Number, s.SourcePayoutOrderId is { } o ? ordenes.GetValueOrDefault(o) : null, s.Amount, s.TransferredOn,
                ops.GetValueOrDefault(s.TransferOperationId) ?? string.Empty, s.Status, s.ClaimedOn,
                s.ClaimPayoutOrderId is { } c ? ordenes.GetValueOrDefault(c) : null, s.Reason))
            .ToList();
    }
}

// ----------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// Los saldos no reclamados (api.md §14, <c>GET /api/lending/unclaimed</c>, <c>Lending.Accounts.Read</c>): de una persona o todos, por estado, sólo los de
/// cuentas en el alcance. Lectura sensible auditada (los saldos de cada persona de la página). (nuevo)
/// </summary>
public sealed record ListUnclaimedBalancesQuery(Guid? PersonPublicId = null, UnclaimedStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<UnclaimedBalanceDto>>>;

public sealed class ListUnclaimedBalancesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LendingAuditEmitter auditoria)
    : IRequestHandler<ListUnclaimedBalancesQuery, Result<PagedResult<UnclaimedBalanceDto>>>
{
    public async Task<Result<PagedResult<UnclaimedBalanceDto>>> Handle(ListUnclaimedBalancesQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = from u in db.UnclaimedBalances.AsNoTracking()
                       join a in db.MemberAccounts.AsNoTracking() on u.SourceMemberAccountId equals a.Id
                       where !u.IsDeleted
                       select new { u, a.BranchId, a.CreditLineId };
        if (request.PersonPublicId is { } p) consulta = consulta.Where(x => db.People.Any(pe => pe.Id == x.u.PersonId && pe.PublicId == p));
        if (request.Status is { } s) consulta = consulta.Where(x => x.u.Status == s);
        var todos = (await consulta.OrderByDescending(x => x.u.TransferredOn).ThenByDescending(x => x.u.Id).Take(5_000).ToListAsync(ct))
            .Where(x => alcance.IncluyeOficina(x.BranchId) && alcance.IncluyeLinea(x.CreditLineId)).Select(x => x.u).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var deLaPagina = todos.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var items = await VistaDeNoReclamados.DtosAsync(db, deLaPagina, ct);
        foreach (var persona in items.Select(i => i.Person.PersonPublicId).Distinct())
            await auditoria.EmitirSaldosDePersonaVistosAsync(persona, nameof(ListUnclaimedBalancesQuery),
                new { status = request.Status?.ToString(), rows = items.Count(i => i.Person.PersonPublicId == persona) }, ct);
        return Result.Success(new PagedResult<UnclaimedBalanceDto>(items, pagina.SafePage, pagina.SafePageSize, todos.Count));
    }
}

// ------------------------------------------------------------------------------------------------------------- trasladar --

/// <summary>
/// <c>POST /api/lending/unclaimed/transfer</c> (<c>TransferToUnclaimedCommand</c>, <c>Lending.Unclaimed.Manage</c>): traslada a no reclamados las cuentas
/// inactivas pedidas (<paramref name="AccountPublicIds"/>; sólo con <c>CuentasAbandonadas.TrasladoHabilitado</c>, si no <c>RuleNotEnabled</c>) y lo
/// devuelto y no cobrado (<paramref name="RefundPublicIds"/>: las órdenes de giro que volvieron). Todo o nada: si alguno no puede, <c>NotEligible</c> con
/// la lista y no se traslada ninguno. A mano el plazo sólo rige si la cooperativa lo fijó. (nuevo)
/// </summary>
public sealed record TransferToUnclaimedCommand(IReadOnlyList<Guid>? AccountPublicIds, IReadOnlyList<Guid>? RefundPublicIds, string Reason)
    : IRequest<Result<IReadOnlyList<UnclaimedBalanceDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class TransferToUnclaimedCommandValidator : ValidadorConMotivo<TransferToUnclaimedCommand>
{
    public TransferToUnclaimedCommandValidator() =>
        RuleFor(x => x).Must(x => (x.AccountPublicIds?.Count ?? 0) + (x.RefundPublicIds?.Count ?? 0) > 0).WithName("accountPublicIds")
            .WithMessage("Indique las cuentas inactivas o las devoluciones no cobradas que pasan a no reclamados.");
}

public sealed class TransferToUnclaimedCommandHandler(
    IApplicationDbContext db,
    NoReclamados noReclamados,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ICerrojoDeCartera cerrojo,
    Lines.IContextoDeLineasDeCaptacion contexto,
    IDateTimeService reloj,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : IRequestHandler<TransferToUnclaimedCommand, Result<IReadOnlyList<UnclaimedBalanceDto>>>
{
    public async Task<Result<IReadOnlyList<UnclaimedBalanceDto>>> Handle(TransferToUnclaimedCommand request, CancellationToken ct)
    {
        if (contexto.Entrega < EntregaDeCartera.K10)
            return Falla(Contributions.ErroresDeAportes.EntregaNoDisponible("El traslado a no reclamados", nameof(EntregaDeCartera.K10)));
        var hoy = reloj.HoyLocal;
        var cuentasPedidas = new List<(Guid Cuenta, Guid? Orden)>();
        foreach (var id in (request.AccountPublicIds ?? []).Distinct()) cuentasPedidas.Add((id, null));
        foreach (var ordenId in (request.RefundPublicIds ?? []).Distinct())
        {
            var orden = await db.AccountPayoutOrders.AsNoTracking().Where(o => o.PublicId == ordenId && !o.IsDeleted)
                .Join(db.MemberAccounts.AsNoTracking(), o => o.MemberAccountId, a => a.Id, (o, a) => (Guid?)a.PublicId).FirstOrDefaultAsync(ct);
            if (orden is null) return Falla(ErroresDeNoReclamados.NoElegibles([new(null, null, ordenId, "RefundNotFound", "La devolución no existe.")]));
            cuentasPedidas.Add((orden.Value, ordenId));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cuentas = new List<(MemberAccount Cuenta, Guid? Orden)>();
            foreach (var (publicId, orden) in cuentasPedidas.DistinctBy(c => c.Cuenta))
            {
                var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, publicId, seguir: true, ct);
                if (leida.IsFailure) return Falla(leida.Error);
                cuentas.Add((leida.Value, orden));
            }
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync(cuentas.Select(c => c.Cuenta.Id).Order().ToList(), ModoDeEspera.Esperar, ct);
            var candidatas = new List<(MemberAccount Cuenta, CandidataANoReclamados Candidata)>();
            var noElegibles = new List<NoElegible>();
            foreach (var (cuenta, orden) in cuentas)
            {
                var (candidata, motivo, reglaNoHabilitada) = await noReclamados.EvaluarAsync(cuenta, hoy, automatico: false, ct);
                if (reglaNoHabilitada) return Falla(ErroresDeNoReclamados.ReglaNoHabilitada());
                if (candidata is null)
                {
                    noElegibles.Add(motivo! with { RefundPublicId = orden });
                    continue;
                }
                if (orden is not null && !await db.AccountPayoutOrders.AsNoTracking().AnyAsync(o => o.PublicId == orden && o.Id == candidata.OrdenId, ct))
                {
                    noElegibles.Add(new NoElegible(cuenta.PublicId, cuenta.Number, orden, "RefundNotLast",
                        $"Esa devolución no es la orden que volvió en la cuenta {cuenta.Number}: no pasa a no reclamados."));
                    continue;
                }
                candidatas.Add((cuenta, candidata));
            }
            if (noElegibles.Count > 0) return Falla(ErroresDeNoReclamados.NoElegibles(noElegibles));
            // D23 (pendiente de la fase 17, research E206): no se traslada nada si el traslado de alguna no se contabilizaría (todo o nada).
            if (validacionPrevia is not null && await ValidacionPreviaDelCicloDeVida.TrasladoANoReclamadosAsync(validacionPrevia, db,
                    candidatas.Select(c => (c.Cuenta.Id, c.Candidata.Fuente.ToString())).ToList(), hoy, ct) is { } bloqueo)
                return Falla(bloqueo);
            var hechos = new List<UnclaimedBalance>();
            foreach (var (cuenta, candidata) in candidatas)
            {
                var hecho = await noReclamados.TrasladarAsync(cuenta, candidata, hoy, request.Reason, ct);
                if (hecho.IsFailure) return Falla(hecho.Error);
                hechos.Add(hecho.Value);
            }
            return Result.Success(await VistaDeNoReclamados.DtosAsync(db, hechos, ct));
        }, ct);
    }

    private static Result<IReadOnlyList<UnclaimedBalanceDto>> Falla(Error e) => Result.Failure<IReadOnlyList<UnclaimedBalanceDto>>(e);
}

// ----------------------------------------------------------------------------------------------------------------- pagar --

/// <summary>
/// <c>POST /api/lending/unclaimed/{id}/pay</c> (<c>PayUnclaimedBalanceCommand</c>, <c>Lending.Unclaimed.Manage</c>): paga el saldo al titular que aparece,
/// por orden de giro (al banco pedido o a la cuenta de depósito registrada del asociado) o por traslado a una cuenta de ahorro a la vista o permanente viva
/// del titular en el alcance. Ya pagado: <c>AlreadyPaid</c>. (nuevo)
/// </summary>
public sealed record PayUnclaimedBalanceCommand(
    Guid UnclaimedPublicId,
    FormaDePagoDeNoReclamado Form,
    Guid? TargetAccountPublicId,
    Guid? BankPublicId,
    byte? AccountType,
    string? AccountNumber,
    string Reason) : IRequest<Result<UnclaimedBalanceDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class PayUnclaimedBalanceCommandValidator : ValidadorConMotivo<PayUnclaimedBalanceCommand>
{
    public PayUnclaimedBalanceCommandValidator()
    {
        RuleFor(x => x.UnclaimedPublicId).NotEmpty();
        RuleFor(x => x.Form).IsInEnum();
        RuleFor(x => x.TargetAccountPublicId).NotNull().When(x => x.Form == FormaDePagoDeNoReclamado.TransferToAccount)
            .WithMessage("El traslado nombra la cuenta de ahorro del titular que recibe.");
        RuleFor(x => x.TargetAccountPublicId).Null().When(x => x.Form == FormaDePagoDeNoReclamado.DisbursementOrder)
            .WithMessage("La orden de giro no lleva cuenta de destino.");
    }
}

public sealed class PayUnclaimedBalanceCommandHandler(
    IApplicationDbContext db,
    NoReclamados noReclamados,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ICerrojoDeCartera cerrojo,
    IDateTimeService? reloj = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : IRequestHandler<PayUnclaimedBalanceCommand, Result<UnclaimedBalanceDto>>
{
    public async Task<Result<UnclaimedBalanceDto>> Handle(PayUnclaimedBalanceCommand request, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var leido = await VistaDeNoReclamados.EnAlcanceAsync(db, alcance, auditoria, request.UnclaimedPublicId, seguir: true, ct);
            if (leido.IsFailure) return Falla(leido.Error);
            var saldo = leido.Value;
            if (saldo.Status != UnclaimedStatus.Held) return Falla(ErroresDeNoReclamados.YaPagado(saldo.ClaimedOn));
            MemberAccount? destino = null;
            if (request.TargetAccountPublicId is { } d)
            {
                var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, d, seguir: true, ct);
                if (leida.IsFailure) return Falla(leida.Error);
                destino = leida.Value;
                if (destino.HolderPersonId != saldo.PersonId || destino.Nature != LineNature.Deposit
                    || destino.DepositClass is not (DepositClass.Demand or DepositClass.Permanent)
                    || destino.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
                    return Falla(ErroresDeNoReclamados.Invalido("targetAccountPublicId",
                        $"La cuenta {destino.Number} no es una cuenta de ahorro a la vista o permanente viva del titular."));
            }
            // D23 (pendiente de la fase 17, research E206): la pantalla no paga si el pago no se contabilizaría.
            if (validacionPrevia is not null && reloj is not null)
            {
                var numero = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == saldo.SourceMemberAccountId).Select(a => a.Number).FirstAsync(ct);
                if (await ValidacionPreviaDelCicloDeVida.PagoDeNoReclamadoAsync(validacionPrevia, saldo.SourceMemberAccountId, destino, reloj.HoyLocal, numero, ct)
                    is { } bloqueo)
                    return Falla(bloqueo);
            }
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync(new[] { saldo.SourceMemberAccountId, destino?.Id ?? saldo.SourceMemberAccountId }.Distinct().Order().ToList(),
                ModoDeEspera.Esperar, ct);
            var pagado = await noReclamados.PagarAsync(saldo, request.Form, destino, request.BankPublicId, request.AccountType, request.AccountNumber,
                request.Reason, ct);
            if (pagado.IsFailure) return Falla(pagado.Error);
            return Result.Success((await VistaDeNoReclamados.DtosAsync(db, [saldo], ct))[0]);
        }, ct);

    private static Result<UnclaimedBalanceDto> Falla(Error e) => Result.Failure<UnclaimedBalanceDto>(e);
}

// --------------------------------------------------------------------------------------------------------------- informe --

/// <summary>
/// La vista <c>unclaimed</c> (feature 014, US14, T209; api.md §18.1; research R24): los saldos no reclamados a una fecha —trasladados hasta el corte, con su
/// estado a esa fecha—, con datos del titular y dentro del alcance. (nuevo)
/// </summary>
public sealed record UnclaimedReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class UnclaimedReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<UnclaimedReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "unclaimed", "Saldos no reclamados",
        "Los saldos trasladados a no reclamados a una fecha, por titular, con su origen, valor y estado.", "no-reclamados",
        ["asOf"], [], EntregaDeCartera.K10);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Titular"),
        new("Documento"),
        new("Origen"),
        new("Cuenta"),
        new("Valor", TipoDeColumna.Moneda),
        new("Desde", TipoDeColumna.Fecha),
        new("Estado"),
        new("Pagado el", TipoDeColumna.Fecha),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(UnclaimedReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var filasDb = await (from u in db.UnclaimedBalances.AsNoTracking()
                             join a in db.MemberAccounts.AsNoTracking() on u.SourceMemberAccountId equals a.Id
                             where !u.IsDeleted && u.TransferredOn <= corte
                             orderby u.TransferredOn, u.Id
                             select new { u, a.Number, a.BranchId, a.CreditLineId }).ToListAsync(ct);
        var visibles = filasDb.Where(x => alcance.IncluyeOficina(x.BranchId) && alcance.IncluyeLinea(x.CreditLineId)).ToList();
        var personas = await LectorDeCuentas.PersonasAsync(db, visibles.Select(x => x.u.PersonId).Distinct(), ct);
        var filas = visibles.Select(x =>
        {
            var pagado = x.u.ClaimedOn is { } c && c <= corte;
            var p = personas[x.u.PersonId];
            return new FilaExportable([p.FullName, p.DocumentNumber, Origen(x.u.Source), x.Number, x.u.Amount, x.u.TransferredOn,
                pagado ? "Pagado" : "Retenido", pagado ? x.u.ClaimedOn : null, p.PersonPublicId.ToString()]);
        }).ToList();
        var retenido = visibles.Where(x => x.u.ClaimedOn is not { } c || c > corte).Sum(x => x.u.Amount);
        var totales = new FilaExportable(["Total retenido", null, null, null, retenido, null, null, null, null]);
        return Result.Success(new TablaExportable("Saldos no reclamados", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales,
            ["Lo devuelto y no cobrado pasa con Captaciones.DiasParaNoReclamados; el saldo de una cuenta inactiva, sólo si el reglamento lo ordena "
             + "(CuentasAbandonadas.TrasladoHabilitado). El dinero sigue siendo del titular."]));
    }

    public static string Origen(UnclaimedSource fuente) => fuente switch
    {
        UnclaimedSource.UncashedRefund => "Devolución no cobrada",
        UnclaimedSource.UncashedPayout => "Giro no cobrado",
        UnclaimedSource.InactiveBalance => "Cuenta inactiva",
        _ => fuente.ToString(),
    };
}
