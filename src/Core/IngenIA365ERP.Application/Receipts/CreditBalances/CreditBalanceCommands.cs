using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CreditBalances;

// Feature 015, US5 (T0177; R-05; FR-053, FR-054; interfaces.md §2; contracts/api.md §4): los saldos a favor de pagadores —devueltos por orden de pago a Tesorería en
// la misma transacción que su baja (R0) o aplicados con un recibo nuevo— y el saldo a favor de Cartera devuelto por el recaudador. El resultado de la orden vuelve por
// IResultadoDeOrdenesDeRecaudo: «rechazada» o «anulada» reabre el saldo y «girada» lo cierra.

/// <summary>Las clases de orden de pago que pide Recaudo (interfaces.md §2). (nuevo)</summary>
public static class ClasesDeOrdenDeRecaudo
{
    public const string DevolucionDeSaldoDePagador = "PayerCreditBalanceRefund";
    public const string DevolucionDeSaldoDeCartera = "LendingCreditBalanceRefund";
    public const string DevolucionDePartida = "UnidentifiedItemRefund";
}

/// <summary><c>GET /api/receipts/payer-credit-balances?state=&amp;person=</c> (<c>Receipts.CreditBalances.View</c>): los saldos a favor de las oficinas al alcance. (nuevo)</summary>
public sealed record ListPayerCreditBalancesQuery(PayerCreditBalanceState? State = null, Guid? PersonPublicId = null) : IRequest<Result<IReadOnlyList<PayerCreditBalanceDto>>>;

public sealed class ListPayerCreditBalancesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListPayerCreditBalancesQuery, Result<IReadOnlyList<PayerCreditBalanceDto>>>
{
    public async Task<Result<IReadOnlyList<PayerCreditBalanceDto>>> Handle(ListPayerCreditBalancesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.PayerCreditBalances.AsNoTracking();
        if (request.State is { } e) consulta = consulta.Where(s => s.Status == e);
        if (request.PersonPublicId is { } p)
        {
            var persona = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => s.PersonId == persona);
        }
        var filas = (await consulta.OrderBy(s => s.OpenedOn).Take(500).ToListAsync(ct)).Where(s => mio.IncluyeOficina(s.BranchId)).ToList();
        var lista = new List<PayerCreditBalanceDto>(filas.Count);
        foreach (var s in filas) lista.Add(await SaldosAFavor.DtoAsync(db, s, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<PayerCreditBalanceDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/receipts/payer-credit-balances/{id}/refund-by-order</c> (<c>Receipts.CreditBalances.Resolve</c>): devuelve el saldo (o parte) con una orden de pago a
/// Tesorería (<see cref="IOrdenesDePagoDeTesoreria"/>, clase <c>PayerCreditBalanceRefund</c>) en la misma transacción que la baja del saldo, y emite
/// <c>SaldoAFavorDePagadorResuelto</c>. El saldo se cierra cuando Tesorería informa «girada»; «rechazada» lo reabre. (nuevo)
/// </summary>
public sealed record RefundPayerCreditBalanceByOrderCommand(Guid BalancePublicId, decimal Amount, Guid? PersonBankAccountPublicId, string Form = "Transfer")
    : IRequest<Result<PayerCreditBalanceDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RefundPayerCreditBalanceByOrderCommandValidator : AbstractValidator<RefundPayerCreditBalanceByOrderCommand>
{
    public RefundPayerCreditBalanceByOrderCommandValidator()
    {
        RuleFor(x => x.BalancePublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Form).Must(f => f is "Transfer" or "Check").WithMessage("La devolución se gira por transferencia (Transfer) o cheque (Check).");
    }
}

public sealed class RefundPayerCreditBalanceByOrderCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IOrdenesDePagoDeTesoreria ordenes,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RefundPayerCreditBalanceByOrderCommand, Result<PayerCreditBalanceDto>>
{
    public async Task<Result<PayerCreditBalanceDto>> Handle(RefundPayerCreditBalanceByOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.PayerCreditBalances.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.BalancePublicId, ct);
        if (leido is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(leido.BranchId))
            return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.NoEncontrado("El saldo a favor"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([leido.PersonId], ct);
        var saldo = await db.PayerCreditBalances.FirstAsync(s => s.Id == leido.Id, ct);
        if (saldo.Status == PayerCreditBalanceState.Resolved) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SaldoResuelto());
        if (request.Amount > saldo.Balance) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SaldoInsuficiente(saldo.Balance));

        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var persona = await db.People.AsNoTracking().Where(p => p.Id == saldo.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        // La clave de la orden es la de la operación: repetir con la misma clave devuelve la misma orden (Tesorería es idempotente por ella).
        var orden = await ordenes.EmitirAsync(new SolicitudDeOrdenDePago(request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey,
            ClasesDeOrdenDeRecaudo.DevolucionDeSaldoDePagador, persona, request.PersonBankAccountPublicId, request.Form, request.Amount, hoy,
            string.Create(CultureInfo.InvariantCulture, $"Devolución del saldo a favor {saldo.OriginDocumentNumber}"), saldo.PublicId), ct);
        var movimiento = SaldosAFavor.Bajar(db, saldo, PayerCreditBalanceMovementKind.RefundedByOrder, request.Amount, orden.PaymentOrderPublicId, orden.Number, usuario, ahora,
            null);
        await db.SaveChangesAsync(ct);
        await emision.SaldoAFavorDePagadorResueltoAsync(saldo, movimiento, hoy, ct);
        await auditoria.AgregarAsync("Receipts.CreditBalance.RefundedByOrder", "PayerCreditBalance", saldo.PublicId, null,
            new { amount = request.Amount, order = orden.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await SaldosAFavor.DtoAsync(db, saldo, hoy, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/payer-credit-balances/{id}/apply</c> (<c>Receipts.CreditBalances.Resolve</c>): aplica el saldo (o parte) con un recibo nuevo del mismo pagador
/// —el recibo cita el saldo; el medio «saldo a favor» de la caja llega con R1— y emite <c>SaldoAFavorDePagadorResuelto</c> contra el puente del módulo. (nuevo)
/// </summary>
public sealed record ApplyPayerCreditBalanceCommand(Guid BalancePublicId, decimal Amount, Guid ReceiptPublicId) : IRequest<Result<PayerCreditBalanceDto>>, IOperacionIdempotente,
    IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyPayerCreditBalanceCommandValidator : AbstractValidator<ApplyPayerCreditBalanceCommand>
{
    public ApplyPayerCreditBalanceCommandValidator()
    {
        RuleFor(x => x.BalancePublicId).NotEmpty();
        RuleFor(x => x.ReceiptPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

public sealed class ApplyPayerCreditBalanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ApplyPayerCreditBalanceCommand, Result<PayerCreditBalanceDto>>
{
    public async Task<Result<PayerCreditBalanceDto>> Handle(ApplyPayerCreditBalanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.PayerCreditBalances.AsNoTracking().FirstOrDefaultAsync(s => s.PublicId == request.BalancePublicId, ct);
        if (leido is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(leido.BranchId))
            return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.NoEncontrado("El saldo a favor"));
        var recibo = await db.IssuedReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.ReceiptPublicId, ct);
        if (recibo is null || recibo.PayerPersonId != leido.PersonId || recibo.Status != ReceiptState.Valid)
            return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.ReciboAjeno());
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([leido.PersonId], ct);
        var saldo = await db.PayerCreditBalances.FirstAsync(s => s.Id == leido.Id, ct);
        if (saldo.Status == PayerCreditBalanceState.Resolved) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SaldoResuelto());
        if (request.Amount > saldo.Balance) return Result.Failure<PayerCreditBalanceDto>(ErroresDeRecaudo.SaldoInsuficiente(saldo.Balance));
        var hoy = reloj.HoyLocal;
        var movimiento = SaldosAFavor.Bajar(db, saldo, PayerCreditBalanceMovementKind.AppliedByReceipt, request.Amount, recibo.PublicId, recibo.Number, usuario, reloj.UtcNow,
            null);
        await db.SaveChangesAsync(ct);
        await emision.SaldoAFavorDePagadorResueltoAsync(saldo, movimiento, hoy, ct);
        await auditoria.AgregarAsync("Receipts.CreditBalance.AppliedByReceipt", "PayerCreditBalance", saldo.PublicId, null,
            new { amount = request.Amount, receipt = recibo.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await SaldosAFavor.DtoAsync(db, saldo, hoy, ct));
    }
}

/// <summary>El saldo a favor de Cartera devuelto por el recaudador. (nuevo)</summary>
public sealed record LendingCreditBalanceRefundDto(Guid PaymentOrderPublicId, string PaymentOrderNumber, string LendingResolutionNumber, decimal Amount);

/// <summary>
/// <c>POST /api/receipts/lending-credit-balances/refund-by-order</c> (<c>Receipts.CreditBalances.Resolve</c>; FR-053): Cartera resuelve su saldo a favor por el
/// recaudador (<c>ResolverSaldoAFavorAsync</c> con <c>RefundThroughCollector</c>) y Recaudo lo devuelve con una orden de pago a Tesorería (clase
/// <c>LendingCreditBalanceRefund</c>) en la misma transacción; emite <c>SaldoAFavorDeCarteraDevuelto</c>. Si Tesorería informa la orden rechazada, nace un saldo a
/// favor del beneficiario en Recaudo (Cartera ya lo resolvió). (nuevo)
/// </summary>
public sealed record RefundLendingCreditBalanceByOrderCommand(Guid ObligationPublicId, decimal Amount, Guid BeneficiaryPersonPublicId, Guid BranchPublicId,
    Guid? PersonBankAccountPublicId, string Form = "Transfer") : IRequest<Result<LendingCreditBalanceRefundDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RefundLendingCreditBalanceByOrderCommandValidator : AbstractValidator<RefundLendingCreditBalanceByOrderCommand>
{
    public RefundLendingCreditBalanceByOrderCommandValidator()
    {
        RuleFor(x => x.ObligationPublicId).NotEmpty();
        RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Form).Must(f => f is "Transfer" or "Check").WithMessage("La devolución se gira por transferencia (Transfer) o cheque (Check).");
    }
}

public sealed class RefundLendingCreditBalanceByOrderCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IContratoDeObligaciones obligaciones,
    IOrdenesDePagoDeTesoreria ordenes,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RefundLendingCreditBalanceByOrderCommand, Result<LendingCreditBalanceRefundDto>>
{
    public async Task<Result<LendingCreditBalanceRefundDto>> Handle(RefundLendingCreditBalanceByOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null) return Result.Failure<LendingCreditBalanceRefundDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == request.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Value))
            return Result.Failure<LendingCreditBalanceRefundDto>(ErroresDeRecaudo.NoEncontrado("La oficina"));
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.BeneficiaryPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<LendingCreditBalanceRefundDto>(ErroresDeRecaudo.NoEncontrado("El beneficiario"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([persona.Value], ct);
        var hoy = reloj.HoyLocal;
        var clave = request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey;
        var referencia = new ReferenciaDeRecibo(ClavesDeOrden(clave), hoy, request.BranchPublicId, null, ClasesDeOrdenDeRecaudo.DevolucionDeSaldoDeCartera);
        var r = await obligaciones.ResolverSaldoAFavorAsync(new SolicitudDeResolucion(ClavesDeRecaudo.Recaudador, ClavesDeRecaudo.Destino(ClavesDeOrden(clave)),
            request.ObligationPublicId, ConceptoDeObligacion.CreditBalance, request.Amount, FormaDeResolucion.RefundThroughCollector, referencia), ct);
        if (r.Result == ResultadoDelContrato.Rejected || r.Value is null)
            return Result.Failure<LendingCreditBalanceRefundDto>(ErroresDeRecaudo.DevolucionDeCarteraRechazada(r.Code, r.Message));
        var orden = await ordenes.EmitirAsync(new SolicitudDeOrdenDePago(clave, ClasesDeOrdenDeRecaudo.DevolucionDeSaldoDeCartera, request.BeneficiaryPersonPublicId,
            request.PersonBankAccountPublicId, request.Form, request.Amount, hoy, $"Devolución del saldo a favor de Cartera {r.Value.Number}", r.Value.OperationPublicId), ct);
        // El rastro en Recaudo: un saldo que nace devuelto por la orden; si Tesorería la informa rechazada o anulada, se reabre a favor del beneficiario.
        var ahora = reloj.UtcNow;
        var rastro = SaldosAFavor.Reconocer(db, persona.Value, PayerCreditBalanceOrigin.LendingRefund, r.Value.OperationPublicId, r.Value.Number, oficina.Value,
            request.Amount, hoy, actor.UserId, ahora);
        await db.SaveChangesAsync(ct);
        SaldosAFavor.Reconocido(db, rastro, actor.UserId, ahora);
        SaldosAFavor.Bajar(db, rastro, PayerCreditBalanceMovementKind.RefundedByOrder, request.Amount, orden.PaymentOrderPublicId, orden.Number, actor.UserId, ahora, null);
        await db.SaveChangesAsync(ct);
        await emision.SaldoAFavorDeCarteraDevueltoAsync(new SaldoAFavorDeCarteraDevueltoV1
        {
            ObligationPublicId = request.ObligationPublicId, ObligationNumber = r.Value.Number, LendingResolutionPublicId = r.Value.OperationPublicId,
            LendingResolutionNumber = r.Value.Number, PaymentOrderPublicId = orden.PaymentOrderPublicId, PaymentOrderNumber = orden.Number,
            BeneficiaryPersonPublicId = request.BeneficiaryPersonPublicId, Amount = request.Amount, RefundedOn = hoy,
        }, oficina.Value, ct);
        await auditoria.AgregarAsync("Receipts.LendingCreditBalance.RefundedByOrder", "PaymentOrder", orden.PaymentOrderPublicId, null,
            new { obligation = request.ObligationPublicId, amount = request.Amount, order = orden.Number, lending = r.Value.Number, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new LendingCreditBalanceRefundDto(orden.PaymentOrderPublicId, orden.Number, r.Value.Number, request.Amount));
    }

    /// <summary>La clave del pedido a Cartera: <c>REC:{orden:N}</c> (interfaces.md §2).</summary>
    public static string ClavesDeOrden(Guid orden) => string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:{orden:N}");
}

/// <summary>
/// Lo que Recaudo hace con el resultado de una orden de pago que pidió (feature 015, US5, T0177; R-05; interfaces.md §2; implementa
/// <see cref="IResultadoDeOrdenesDeRecaudo"/>, que Tesorería llama desde <c>RegistroDeResultadosDelGiro</c>): «girada» cierra el saldo a favor cuya devolución fue esa
/// orden; «rechazada», «cheque no cobrado» o «anulada» lo reabre (movimiento <c>RefundRejected</c>); la devolución del saldo de Cartera rechazada deja un saldo a favor
/// del beneficiario en Recaudo. Toma sus cerrojos antes que los de Tesorería. Sin guardar. (nuevo)
/// </summary>
public sealed class ResultadoDeOrdenesDeRecaudo(IApplicationDbContext db, ICerrojoDeRecaudo cerrojo, AuditoriaDeRecaudo auditoria, IDateTimeService reloj)
    : IResultadoDeOrdenesDeRecaudo
{
    public async Task TomarCerrojosAsync(IReadOnlyList<Guid> paymentOrderPublicIds, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(ct: ct);
        var personas = await (from m in db.PayerCreditBalanceMovements.AsNoTracking()
                              join s in db.PayerCreditBalances.AsNoTracking() on m.PayerCreditBalanceId equals s.Id
                              where m.DocumentPublicId != null && paymentOrderPublicIds.Contains(m.DocumentPublicId.Value)
                              select s.PersonId).Distinct().ToListAsync(ct);
        if (personas.Count > 0) await cerrojo.BloquearPagadoresAsync(personas.Order().ToList(), ct);
        // US6 (fase 11): la partida devuelta al consignante por la orden (nivel 6).
        var partidas = await db.UnidentifiedItemResolutions.AsNoTracking()
            .Where(r => r.Kind == Domain.Enums.Receipts.UnidentifiedItemResolutionKind.RefundedByOrder && paymentOrderPublicIds.Contains(r.DocumentPublicId))
            .Select(r => (long)r.UnidentifiedItemId).Distinct().ToListAsync(ct);
        if (partidas.Count > 0) await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Partidas, partidas.Order().ToList(), ct: ct);
    }

    /// <summary>Los resultados que Tesorería informa (FR-215).</summary>
    public static readonly IReadOnlySet<string> Resultados = new HashSet<string>(StringComparer.Ordinal) { "Disbursed", "Rejected", "CheckNotCashed", "Voided" };

    /// <summary>El validador del puerto (desviación III).</summary>
    public static void ValidarResultado(Guid orden, string outcome)
    {
        if (orden == Guid.Empty) throw new ArgumentException("La orden de pago es obligatoria.", nameof(orden));
        if (!Resultados.Contains(outcome)) throw new ArgumentException($"«{outcome}» no es un resultado del giro.", nameof(outcome));
    }

    public async Task InformarAsync(Guid paymentOrderPublicId, string outcome, DateOnly occurredOn, string? reason, CancellationToken ct)
    {
        ValidarResultado(paymentOrderPublicId, outcome);
        if (await InformarPartidaAsync(paymentOrderPublicId, outcome, reason, ct)) return;
        var movimiento = await db.PayerCreditBalanceMovements.AsNoTracking()
            .Where(m => m.DocumentPublicId == paymentOrderPublicId && m.Kind == PayerCreditBalanceMovementKind.RefundedByOrder).OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync(ct);
        if (movimiento is null) return;
        var saldo = await db.PayerCreditBalances.FirstAsync(s => s.Id == movimiento.PayerCreditBalanceId, ct);
        await AuditarAsync(saldo, paymentOrderPublicId, outcome, ct);
        if (outcome == "Disbursed")
        {
            if (saldo.Balance == 0m) saldo.Status = PayerCreditBalanceState.Resolved;
            return;
        }
        if (await db.PayerCreditBalanceMovements.AnyAsync(m => m.DocumentPublicId == paymentOrderPublicId && m.Kind == PayerCreditBalanceMovementKind.RefundRejected, ct))
            return;
        saldo.Balance += movimiento.Amount;
        saldo.Status = PayerCreditBalanceState.Open;
        db.PayerCreditBalanceMovements.Add(new PayerCreditBalanceMovement
        {
            PayerCreditBalanceId = saldo.Id, Kind = PayerCreditBalanceMovementKind.RefundRejected, Amount = movimiento.Amount, DocumentPublicId = paymentOrderPublicId,
            DocumentNumber = movimiento.DocumentNumber, RecordedAt = reloj.UtcNow,
            Note = string.IsNullOrWhiteSpace(reason) ? $"Orden {outcome}" : $"Orden {outcome}: {reason}",
        });
        _ = occurredOn;
    }

    /// <summary>
    /// La devolución de una partida al consignante (US6, fase 11): «girada» la deja como está; «rechazada», «cheque no cobrado» o «anulada» devuelve el saldo a la
    /// partida con la resolución <c>RefundRejected</c>. Devuelve si la orden era de una partida.
    /// </summary>
    private async Task<bool> InformarPartidaAsync(Guid orden, string outcome, string? reason, CancellationToken ct)
    {
        var devolucion = await db.UnidentifiedItemResolutions.AsNoTracking()
            .Where(r => r.DocumentPublicId == orden && r.Kind == Domain.Enums.Receipts.UnidentifiedItemResolutionKind.RefundedByOrder).OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (devolucion is null) return false;
        var partida = await db.UnidentifiedItems.FirstAsync(p => p.Id == devolucion.UnidentifiedItemId, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.OrderResult", "UnidentifiedItem", partida.PublicId, null, new { order = orden, outcome }, ct);
        if (outcome == "Disbursed") return true;
        if (await db.UnidentifiedItemResolutions.AnyAsync(r => r.DocumentPublicId == orden && r.Kind == Domain.Enums.Receipts.UnidentifiedItemResolutionKind.RefundRejected, ct))
            return true;
        partida.Balance += devolucion.Amount;
        partida.Status = partida.Balance >= partida.Amount ? Domain.Enums.Receipts.UnidentifiedItemState.Open : Domain.Enums.Receipts.UnidentifiedItemState.PartiallyIdentified;
        db.UnidentifiedItemResolutions.Add(new Domain.Entities.Receipts.Banks.UnidentifiedItemResolution
        {
            UnidentifiedItemId = partida.Id, Kind = Domain.Enums.Receipts.UnidentifiedItemResolutionKind.RefundRejected, Amount = devolucion.Amount, DocumentPublicId = orden,
            DocumentNumber = devolucion.DocumentNumber, ByUserId = devolucion.ByUserId, At = reloj.UtcNow,
            Note = string.IsNullOrWhiteSpace(reason) ? $"Orden {outcome}" : $"Orden {outcome}: {reason}",
        });
        return true;
    }

    private Task AuditarAsync(PayerCreditBalance saldo, Guid orden, string outcome, CancellationToken ct) =>
        auditoria.AgregarAsync("Receipts.CreditBalance.OrderResult", "PayerCreditBalance", saldo.PublicId, null, new { order = orden, outcome }, ct);
}
