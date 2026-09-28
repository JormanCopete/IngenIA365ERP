using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Sales.Cash;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

// ------------------------------------------------------------------------------------------------ esperado --

/// <summary>
/// <c>GET /api/inventory/cash-sessions/{id}/expected</c> (§21.2): el <c>CashSessionExpectedDto</c> por <see cref="CalculadoraDeEsperado"/>;
/// con arqueo ciego y sin <c>Inventory.CashSessions.ViewAll</c>, <c>blind</c> y las cifras nulas. (nuevo)
/// </summary>
public sealed record GetCashSessionExpectedQuery(Guid CashSessionPublicId) : IRequest<Result<CashSessionExpectedDto>>;

public sealed class GetCashSessionExpectedQueryHandler(SesionesDeCaja sesiones) : IRequestHandler<GetCashSessionExpectedQuery, Result<CashSessionExpectedDto>>
{
    public async Task<Result<CashSessionExpectedDto>> Handle(GetCashSessionExpectedQuery request, CancellationToken ct)
    {
        var sesion = await sesiones.VisibleAsync(request.CashSessionPublicId, seguir: false, ct);
        return sesion is null
            ? Result.Failure<CashSessionExpectedDto>(ErroresDeCaja.SessionNotFound())
            : Result.Success(await sesiones.EsperadoDtoAsync(sesion, ct));
    }
}

// ------------------------------------------------------------------------------------------------ cierre --

/// <summary>
/// Cierra la sesión con su arqueo por medio de pago (<c>POST /api/inventory/cash-sessions/{id}/close</c>, <c>Inventory.CashSessions.Close</c>;
/// feature 012, I3, T618; contracts/api.md §21.2; FR-099, T50, T12). (nuevo)
/// </summary>
public sealed record CloseCashSessionCommand(Guid CashSessionPublicId, IReadOnlyList<CashCountInput> Counts, ClosingWithdrawalInput? ClosingWithdrawal = null)
    : IRequest<Result<CloseCashSessionResultDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseCashSessionCommandValidator : AbstractValidator<CloseCashSessionCommand>
{
    public CloseCashSessionCommandValidator()
    {
        RuleFor(x => x.CashSessionPublicId).NotEmpty();
        RuleFor(x => x.Counts).NotNull();
        RuleForEach(x => x.Counts).SetValidator(new CashCountInputValidator());
        RuleFor(x => x.ClosingWithdrawal!.Destination).Must(d => d is CashMovementDestination.Safe or CashMovementDestination.Deposit)
            .When(x => x.ClosingWithdrawal is not null).WithMessage("El retiro de cierre va a la caja fuerte (Safe) o a consignar (Deposit).");
    }
}

/// <summary>La forma de lo contado de un medio (§21.2). (nuevo)</summary>
public sealed class CashCountInputValidator : AbstractValidator<CashCountInput>
{
    public CashCountInputValidator()
    {
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.CountedTotal).GreaterThanOrEqualTo(0).When(x => x.CountedTotal is not null);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleForEach(x => x.Denominations).ChildRules(d => d.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0));
        RuleForEach(x => x.TerminalBatches).ChildRules(b =>
        {
            b.RuleFor(x => x.BatchNumber).NotEmpty().MaximumLength(20);
            b.RuleFor(x => x.Total).GreaterThanOrEqualTo(0);
            b.RuleFor(x => x.Count).GreaterThanOrEqualTo(0);
        });
    }
}

/// <summary>
/// En una transacción:
/// <list type="number">
/// <item>la sesión visible (propia o con <c>ViewAll</c>; si no, 404) y abierta; se <b>toca</b> <c>LastActivityAt</c> antes de todo
/// (<see cref="IToqueDeSesionDeCaja"/>): un cobro en curso termina antes o llega tarde y responde <c>NotOpen</c>;</item>
/// <item>sin ventas suspendidas ni borradores (<c>HasOpenDrafts</c>) ni movimientos en borrador o en aprobación
/// (<c>HasPendingMovements</c>);</item>
/// <item>lo contado según el <c>CashCountMethod</c> de cada medio (<c>CountMethodMismatch</c>), el arqueo por
/// <see cref="EvaluadorDeArqueo"/> (motivo en toda diferencia: <c>ReasonRequired</c>) y su escritura en <c>INV_CashCounts</c> con líneas,
/// denominaciones, lotes de datáfono y cotejo de referencias, con la tolerancia <b>copiada</b> del medio;</item>
/// <item>con diferencia, el documento <c>CashCountDifference</c> con una línea por medio que difiere (<c>INV_CashDocumentLines</c>) por el
/// flujo canónico: dentro de la tolerancia se confirma con su motivo; por encima, la política del tipo con el cajero excluido;</item>
/// <item>el retiro de cierre (<c>closingWithdrawal</c>) por lo contado en efectivo menos el fondo fijo;</item>
/// <item>la sesión pasa a <c>Closed</c> (la caja puede abrir otra) y, si algo de la sesión espera el disparador <c>CierreDeTurno</c>, la orden
/// del lote <c>CashSessionClose</c> en la misma transacción (T12).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class CloseCashSessionCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    SesionesDeCaja sesiones,
    ArqueoDeLaSesion arqueos,
    IToqueDeSesionDeCaja toque,
    ConfirmacionDeDocumento confirmacion,
    IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<CloseCashSessionCommand, Result<CloseCashSessionResultDto>>
{
    public Task<Result<CloseCashSessionResultDto>> Handle(CloseCashSessionCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await CerrarAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<CloseCashSessionResultDto>> CerrarAsync(CloseCashSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        // (1) La sesión.
        var sesion = await sesiones.VisibleAsync(request.CashSessionPublicId, seguir: true, ct);
        if (sesion is null) return Falla(ErroresDeCaja.SessionNotFound());
        if (!sesion.EstaAbierta || !await toque.TocarAsync(sesion.Id, reloj.UtcNow, ct)) return Falla(ErroresDelPos.CashSessionNotOpen());

        // (2) Ventas y movimientos en curso.
        var borradores = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.CashSessionId == sesion.Id && d.Status == DocumentStatus.Draft && d.Class != DocumentClass.CashMovement
                        && d.Class != DocumentClass.CashCountDifference)
            .OrderBy(d => d.Id)
            .Select(d => new BorradorAbiertoDto(d.PublicId, d.SuspendedLabel, d.AmountDue)).ToListAsync(ct);
        if (borradores.Count > 0) return Falla(ErroresDeCaja.HasOpenDrafts(borradores));
        var pendientes = await (from m in db.CashMovementDetails.AsNoTracking()
                                join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                                where !m.IsDeleted && m.CashSessionId == sesion.Id
                                      && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval)
                                orderby d.Id
                                select new MovimientoPendienteDto(d.PublicId, m.Kind, m.Amount, d.Status)).ToListAsync(ct);
        if (pendientes.Count > 0) return Falla(ErroresDeCaja.HasPendingMovements(pendientes));

        // (3) El arqueo.
        var evaluado = await arqueos.EvaluarAsync(sesion, request.Counts, ct);
        if (evaluado.IsFailure) return Falla(evaluado.Error);
        var (resultado, preparados) = evaluado.Value;
        var arqueo = new CashCount { CashSessionId = sesion.Id, CountedAt = reloj.UtcNow, CountedByUserId = usuario, IsBlind = sesion.IsBlindCount };
        db.CashCounts.Add(arqueo);
        await arqueos.EscribirAsync(arqueo, resultado, preparados, ct);

        // (4) La diferencia.
        var punto = await db.PointsOfSale.AsNoTracking().FirstAsync(p => p.Id == sesion.PointOfSaleId, ct);
        CashDocumentRefDto? diferencia = null;
        if (resultado.HasDifference)
        {
            var creada = await arqueos.DiferenciaAsync(sesion, arqueo, resultado, punto.BranchId, usuario, ct);
            if (creada.IsFailure) return Falla(creada.Error);
            diferencia = creada.Value;
        }
        else
        {
            arqueo.Fijar();
        }

        // (5) El retiro de cierre: lo contado en efectivo menos el fondo fijo.
        CashDocumentRefDto? retiro = null;
        if (request.ClosingWithdrawal is { } pedido)
        {
            var hecho = await RetiroDeCierreAsync(sesion, resultado, pedido.Destination, punto.BranchId, usuario, ct);
            if (hecho.IsFailure) return Falla(hecho.Error);
            retiro = hecho.Value;
        }

        // (6) Cerrar y ordenar el lote del turno.
        sesion.Cerrar(usuario, reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        var lote = await LoteDelTurnoAsync(sesion, actor, ct);
        await auditoria.AnotarAsync(AuditEventTypes.InventoryCashSessionClosed, request, sesion.PublicId,
            new
            {
                resultado.TotalExpected, resultado.TotalCounted, resultado.TotalDifference,
                lines = resultado.Lines.Select(l => new { l.Code, l.Expected, l.Counted, l.Difference, l.Reason }).ToList(),
                differenceDocument = diferencia?.DocumentPublicId, closingWithdrawal = retiro?.DocumentPublicId,
            }, ct, entidad: AuditoriaDelPuntoDeVenta.EntidadSesionDeCaja);
        await db.SaveChangesAsync(ct);

        return Result.Success(new CloseCashSessionResultDto(sesion.PublicId, sesion.Status, ArqueoDeLaSesion.Lineas(resultado), diferencia, retiro,
            lote is null ? null : new CashSessionBatchDto(lote.PublicId, lote.Number, lote.Trigger.ToString())));
    }

    private async Task<Result<CashDocumentRefDto?>> RetiroDeCierreAsync(CashSession sesion, ResultadoDeArqueo resultado, CashMovementDestination destino,
        int sucursal, int usuario, CancellationToken ct)
    {
        var efectivo = await sesiones.EfectivoAsync(ct);
        if (efectivo is null) return Result.Failure<CashDocumentRefDto?>(ErroresDeCaja.CashMeansMissing());
        var contado = resultado.Lines.Where(l => l.PaymentMeansId == efectivo.Id).Sum(l => l.Counted);
        var fondo = sesion.BaseMode == CashSession.BaseFondoFijo ? sesion.OpeningBase : 0m;
        var valor = contado - fondo;
        if (valor <= 0m) return Result.Success<CashDocumentRefDto?>(null);

        var tipo = await TiposDeCaja.PorDefectoAsync(db, DocumentClass.CashMovement, ct);
        if (tipo is null) return Result.Failure<CashDocumentRefDto?>(InventoryErrors.DocumentClassNotAvailable(DocumentClass.CashMovement));
        var clase = destino == CashMovementDestination.Deposit ? CashMovementKind.WithdrawalForDeposit : CashMovementKind.WithdrawalToSafe;
        var documento = await MovimientosDelSistema.CrearAsync(db, tipo, sesion, sucursal, usuario, reloj.HoyLocal, clase, efectivo.Id, destino, valor,
            null, "Retiro de cierre de la sesión", ct);
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(documento.PublicId, DocumentClassGroup.Cash), ct);
        return confirmada.IsFailure
            ? Result.Failure<CashDocumentRefDto?>(confirmada.Error)
            : Result.Success<CashDocumentRefDto?>(SesionesDeCaja.Referencia(documento, confirmada.Value.Approval?.RequestPublicId));
    }

    /// <summary>
    /// El lote <c>CashSessionClose</c> del turno (T12; contracts/contabilidad.md §5.4): las entregas a Contabilidad que esperan en lote
    /// (<c>InBatch</c>, sin lote) con <c>BatchScopeKey = CashSession:{publicId}</c> y el disparador <c>CierreDeTurno</c> sellado. Sin
    /// ninguna, no se crea. Lo pide quien cierra y lo corre el despachador. (nuevo)
    /// </summary>
    private async Task<Domain.Entities.Integration.IntegrationBatch?> LoteDelTurnoAsync(CashSession sesion, IngenIA365ERP.Application.Common.Execution.Actor actor, CancellationToken ct)
    {
        var alcance = ClavesDeLote.SesionDeCaja(sesion.PublicId);
        var candidatas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
            .Where(d => d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.InBatch && d.BatchId == null && d.BatchScopeKey == alcance)
            .OrderBy(d => d.MessageId)
            .ToListAsync(ct);
        var entregas = candidatas.Where(d => ClavesDeLote.Leer(d.ScheduleKey)?.Disparador == ClavesDeLote.CierreDeTurno).ToList();
        if (entregas.Count == 0) return null;

        var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting,
            BatchTrigger.CashSessionClose, actor, motivo: null, reloj.UtcNow);
        lote.CashSessionPublicId = sesion.PublicId;
        var granularidades = entregas.Select(e => ClavesDeLote.Leer(e.ScheduleKey)!.GranularidadDelLote).Distinct().ToList();
        lote.Granularity = granularidades.Count == 1 ? granularidades[0] : null;
        lote.CutoffMessageId = entregas[^1].MessageId;
        lote.MessageCount = entregas.Count;
        lote.DocumentCount = entregas.Select(e => e.Message!.OriginPublicId).Distinct().Count();
        lote.DateFrom = entregas.Min(e => e.Message!.OperationDate);
        lote.DateTo = entregas.Max(e => e.Message!.OperationDate);
        db.IntegrationBatches.Add(lote);
        await db.SaveChangesAsync(ct);
        LotesDeIntegracion.Asignar(lote, entregas);
        return lote;
    }

    private static Result<CloseCashSessionResultDto> Falla(Error error) => Result.Failure<CloseCashSessionResultDto>(error);
}

// ------------------------------------------------------------------------------------------------ reconteo --

/// <summary>
/// Recuenta una sesión cuya diferencia fue rechazada (<c>POST /api/inventory/cash-sessions/{id}/recount</c>; §21.2): el mismo cuerpo
/// <c>counts</c>. (nuevo)
/// </summary>
public sealed record RecountCashSessionCommand(Guid CashSessionPublicId, IReadOnlyList<CashCountInput> Counts)
    : IRequest<Result<CloseCashSessionResultDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }
}

public sealed class RecountCashSessionCommandValidator : AbstractValidator<RecountCashSessionCommand>
{
    public RecountCashSessionCommandValidator()
    {
        RuleFor(x => x.CashSessionPublicId).NotEmpty();
        RuleFor(x => x.Counts).NotNull();
        RuleForEach(x => x.Counts).SetValidator(new CashCountInputValidator());
    }
}

/// <summary>
/// Sólo con el documento de diferencia de la sesión devuelto a borrador (rechazado; si no, <c>NothingToRecount</c>): vuelve a medir con
/// el mismo esperado, <b>reemplaza</b> las líneas del arqueo y del documento, y lo vuelve a confirmar por el flujo canónico (dentro de la
/// tolerancia se confirma; por encima, otra vez la política, sin el cajero). Si el reconteo cuadra, el documento se descarta y el arqueo se
/// fija. Queda auditado con antes y después (<c>Inventory.CashSession.Recounted</c>). (nuevo)
/// </summary>
public sealed class RecountCashSessionCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    SesionesDeCaja sesiones,
    ArqueoDeLaSesion arqueos,
    ConfirmacionDeDocumento confirmacion,
    IAuditoriaDelPuntoDeVenta auditoria)
    : IRequestHandler<RecountCashSessionCommand, Result<CloseCashSessionResultDto>>
{
    public Task<Result<CloseCashSessionResultDto>> Handle(RecountCashSessionCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await RecontarAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<CloseCashSessionResultDto>> RecontarAsync(RecountCashSessionCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());
        var sesion = await sesiones.VisibleAsync(request.CashSessionPublicId, seguir: true, ct);
        if (sesion is null) return Falla(ErroresDeCaja.SessionNotFound());

        var arqueo = await db.CashCounts.Include(c => c.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(c => c.CashSessionId == sesion.Id && !c.IsDeleted, ct);
        var documento = arqueo?.DifferenceDocumentId is int d ? await db.InventoryDocuments.FirstOrDefaultAsync(x => x.Id == d, ct) : null;
        if (arqueo is null || documento is null || documento.Status != DocumentStatus.Draft) return Falla(ErroresDeCaja.NothingToRecount());

        var antes = arqueo.Lines.Select(l => new { l.PaymentMeansId, l.ExpectedAmount, l.CountedAmount, l.DifferenceAmount, l.Reason }).ToList();
        var evaluado = await arqueos.EvaluarAsync(sesion, request.Counts, ct);
        if (evaluado.IsFailure) return Falla(evaluado.Error);
        var (resultado, preparados) = evaluado.Value;
        arqueo.CountedAt = reloj.UtcNow;
        arqueo.CountedByUserId = usuario;
        await arqueos.EscribirAsync(arqueo, resultado, preparados, ct);

        CashDocumentRefDto? diferencia;
        if (resultado.HasDifference)
        {
            var reemplazada = await arqueos.ReemplazarDiferenciaAsync(sesion, arqueo, documento, resultado, ct);
            if (reemplazada.IsFailure) return Falla(reemplazada.Error);
            diferencia = reemplazada.Value;
        }
        else
        {
            foreach (var vieja in await db.CashDocumentLines.Where(l => l.DocumentId == documento.Id && !l.IsDeleted).ToListAsync(ct))
            {
                vieja.IsDeleted = true;
                vieja.DeletedAt = reloj.UtcNow;
            }
            documento.Descartar(usuario, reloj.UtcNow, "El reconteo cuadra: no queda diferencia.");
            arqueo.DifferenceDocumentId = null;
            arqueo.Fijar();
            diferencia = SesionesDeCaja.Referencia(documento);
        }

        await auditoria.AnotarAsync(AuditEventTypes.InventoryCashSessionRecounted, request, sesion.PublicId,
            new
            {
                before = antes,
                after = resultado.Lines.Select(l => new { l.PaymentMeansId, l.Expected, l.Counted, l.Difference, l.Reason }).ToList(),
                differenceDocument = documento.PublicId,
            }, ct, entidad: AuditoriaDelPuntoDeVenta.EntidadSesionDeCaja);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CloseCashSessionResultDto(sesion.PublicId, sesion.Status, ArqueoDeLaSesion.Lineas(resultado), diferencia, null, null));
    }

    private static Result<CloseCashSessionResultDto> Falla(Error error) => Result.Failure<CloseCashSessionResultDto>(error);
}

// ------------------------------------------------------------------------------------------------ arqueo --

/// <summary>Lo contado de un medio ya validado contra su método, con lo que se escribe como detalle. (nuevo)</summary>
public sealed record ConteoPreparado(
    int PaymentMeansId,
    decimal? Counted,
    string? Reason,
    IReadOnlyList<(int DenominationId, decimal Value, int Quantity)> Denominaciones,
    IReadOnlyList<(int TerminalId, string BatchNumber, decimal Total, int Count)> Lotes,
    IReadOnlyList<(int PaymentId, bool Checked)> Referencias);

/// <summary>
/// El arqueo de una sesión (feature 012, I3, T618; §21.2; FR-099, FR-101): valida lo contado contra el <c>CashCountMethod</c> de cada
/// medio, lo mide con <see cref="EvaluadorDeArqueo"/> sobre el esperado de <see cref="SesionesDeCaja"/>, escribe
/// <c>INV_CashCounts</c> (reemplazando el detalle en un reconteo) y arma el documento <c>CashCountDifference</c> con sus
/// <c>INV_CashDocumentLines</c>. Lo usan el cierre y el reconteo. (nuevo)
/// </summary>
public sealed class ArqueoDeLaSesion(IApplicationDbContext db, IDateTimeService reloj, SesionesDeCaja sesiones, ConfirmacionDeDocumento confirmacion)
{
    /// <summary>
    /// Lo contado (<c>CountMethodMismatch</c> si un dato no corresponde al método) medido contra el esperado; con diferencia y sin motivo,
    /// <c>ReasonRequired</c>.
    /// </summary>
    public async Task<Result<(ResultadoDeArqueo Resultado, IReadOnlyList<ConteoPreparado> Preparados)>> EvaluarAsync(CashSession sesion,
        IReadOnlyList<CashCountInput> conteos, CancellationToken ct)
    {
        var pedidos = conteos.Select(c => c.PaymentMeansPublicId).Distinct().ToList();
        if (pedidos.Count != conteos.Count) return Falla(Invalido("counts", "Cada medio se cuenta una sola vez."));
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => pedidos.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, ct);
        if (medios.Count != pedidos.Count) return Falla(Invalido("paymentMeansPublicId", "Algún medio de pago contado no existe."));

        var esperado = await sesiones.EsperadoAsync(sesion, medios.Values.Select(m => m.Id).ToList(), ct);
        var preparados = new List<ConteoPreparado>();
        foreach (var c in conteos)
        {
            var medio = medios[c.PaymentMeansPublicId];
            var linea = esperado.Lines.First(l => l.Medio.PaymentMeansId == medio.Id);
            var conDenominaciones = c.Denominations is { Count: > 0 };
            var conLotes = c.TerminalBatches is { Count: > 0 };
            var conReferencias = c.ReferenceChecks is { Count: > 0 };
            var admitido = medio.CountMethod switch
            {
                CashCountMethod.PhysicalCount => !conLotes && !conReferencias,
                CashCountMethod.VoucherTotal => !conDenominaciones,
                CashCountMethod.ByReference => !conDenominaciones && !conLotes,
                _ => !conDenominaciones && !conLotes && !conReferencias && c.CountedTotal is null,
            };
            if (!admitido) return Falla(ErroresDeCaja.CountMethodMismatch(medio.Code, medio.CountMethod));

            var denominaciones = new List<(int, decimal, int)>();
            if (conDenominaciones)
            {
                var ids = c.Denominations!.Select(d => d.CashDenominationPublicId).Distinct().ToList();
                var catalogo = await db.CashDenominations.AsNoTracking().Where(d => ids.Contains(d.PublicId)).ToDictionaryAsync(d => d.PublicId, d => (d.Id, d.Value), ct);
                if (catalogo.Count != ids.Count || ids.Count != c.Denominations!.Count)
                    return Falla(Invalido("denominations", $"Alguna denominación de {medio.Code} no existe o está repetida."));
                denominaciones.AddRange(c.Denominations!.Select(d => (catalogo[d.CashDenominationPublicId].Id, catalogo[d.CashDenominationPublicId].Value, d.Quantity)));
            }
            var lotes = new List<(int, string, decimal, int)>();
            if (conLotes)
            {
                var ids = c.TerminalBatches!.Select(b => b.CardTerminalPublicId).Distinct().ToList();
                var terminales = await db.CardTerminals.AsNoTracking().Where(t => ids.Contains(t.PublicId)).ToDictionaryAsync(t => t.PublicId, t => t.Id, ct);
                if (terminales.Count != ids.Count) return Falla(Invalido("terminalBatches", $"Algún datáfono de {medio.Code} no existe."));
                lotes.AddRange(c.TerminalBatches!.Select(b => (terminales[b.CardTerminalPublicId], b.BatchNumber.Trim(), b.Total, b.Count)));
            }
            var referencias = new List<(int, bool)>();
            if (conReferencias)
            {
                var ids = c.ReferenceChecks!.Select(r => r.DocumentPaymentPublicId).Distinct().ToList();
                var pagos = await db.DocumentPayments.AsNoTracking().Where(p => ids.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
                var cotejables = linea.References.Select(r => r.DocumentPaymentId).ToHashSet();
                if (pagos.Count != ids.Count || pagos.Values.Any(p => !cotejables.Contains(p)))
                    return Falla(Invalido("referenceChecks", $"Alguna referencia de {medio.Code} no es un pago de esta sesión."));
                referencias.AddRange(c.ReferenceChecks!.Select(r => (pagos[r.DocumentPaymentPublicId], r.Checked)));
            }

            decimal? contado = medio.CountMethod switch
            {
                CashCountMethod.PhysicalCount => conDenominaciones ? denominaciones.Sum(d => d.Item2 * d.Item3) : c.CountedTotal,
                CashCountMethod.VoucherTotal => conLotes ? lotes.Sum(l => l.Item3) : c.CountedTotal,
                CashCountMethod.ByReference => c.CountedTotal
                    ?? (conReferencias ? linea.References.Where(r => referencias.Any(x => x.Item1 == r.DocumentPaymentId && x.Item2)).Sum(r => r.Amount) : null),
                _ => null,
            };
            preparados.Add(new ConteoPreparado(medio.Id, contado, c.Reason, denominaciones, lotes, referencias));
        }

        var tratamiento = EvaluadorDeArqueo.TratamientoDelFaltanteDesde(string.IsNullOrWhiteSpace(sesion.ShortageTreatment)
            ? EvaluadorDeArqueo.FaltanteAlGasto : sesion.ShortageTreatment);
        var resultado = EvaluadorDeArqueo.Evaluar(esperado, preparados.Select(p => new ConteoDeMedio(p.PaymentMeansId, p.Counted, p.Reason)).ToList(), tratamiento);
        if (resultado.MissingReasons.Count > 0) return Falla(ErroresDeCaja.ReasonRequired(resultado.MissingReasons));
        return Result.Success<(ResultadoDeArqueo, IReadOnlyList<ConteoPreparado>)>((resultado, preparados));

        static Result<(ResultadoDeArqueo, IReadOnlyList<ConteoPreparado>)> Falla(Error e) => Result.Failure<(ResultadoDeArqueo, IReadOnlyList<ConteoPreparado>)>(e);
    }

    /// <summary>
    /// Escribe (o reescribe, en un reconteo) el arqueo: totales, una línea por medio medido con la tolerancia copiada y su detalle —las
    /// denominaciones, los lotes de datáfono con lo esperado de ese datáfono, las marcas de referencia—. El detalle anterior queda de baja.
    /// </summary>
    public async Task EscribirAsync(CashCount arqueo, ResultadoDeArqueo resultado, IReadOnlyList<ConteoPreparado> preparados, CancellationToken ct)
    {
        arqueo.TotalExpected = resultado.TotalExpected;
        arqueo.TotalCounted = resultado.TotalCounted;
        arqueo.TotalDifference = resultado.TotalDifference;

        var existentes = arqueo.Id == 0 ? [] : arqueo.Lines.Where(l => !l.IsDeleted).ToList();
        if (existentes.Count > 0)
        {
            var ids = existentes.Select(l => l.Id).ToList();
            foreach (var d in await db.CashCountDenominations.Where(x => ids.Contains(x.CashCountLineId) && !x.IsDeleted).ToListAsync(ct)) Baja(d);
            foreach (var b in await db.CashCountTerminalBatches.Where(x => ids.Contains(x.CashCountLineId) && !x.IsDeleted).ToListAsync(ct)) Baja(b);
            foreach (var r in await db.CashCountReferenceChecks.Where(x => ids.Contains(x.CashCountLineId) && !x.IsDeleted).ToListAsync(ct)) Baja(r);
            foreach (var sobrante in existentes.Where(l => resultado.Lines.All(r => r.PaymentMeansId != l.PaymentMeansId))) Baja(sobrante);
            await db.SaveChangesAsync(ct);
        }

        var esperadoPorDatafono = (await DatafonosAsync(arqueo.CashSessionId, ct));
        foreach (var r in resultado.Lines)
        {
            var linea = existentes.FirstOrDefault(l => l.PaymentMeansId == r.PaymentMeansId);
            if (linea is null)
            {
                linea = new CashCountLine { CashCount = arqueo, PaymentMeansId = r.PaymentMeansId };
                arqueo.Lines.Add(linea);
            }
            linea.CountMethod = r.CountMethod;
            linea.ExpectedAmount = r.Expected;
            linea.CountedAmount = r.Counted;
            linea.DifferenceAmount = r.Difference;
            linea.PaymentCount = r.PaymentsCount;
            linea.ToleranceAmount = r.Tolerance;
            linea.WithinTolerance = r.WithinTolerance;
            linea.Reason = r.Reason;
            linea.Treatment = r.Treatment;

            var p = preparados.FirstOrDefault(x => x.PaymentMeansId == r.PaymentMeansId);
            if (p is null) continue;
            foreach (var d in p.Denominaciones)
                linea.Denominations.Add(new CashCountDenomination { CashDenominationId = d.DenominationId, DenominationValue = d.Value, Quantity = d.Quantity, Amount = d.Value * d.Quantity });
            foreach (var b in p.Lotes)
                linea.TerminalBatches.Add(new CashCountTerminalBatch
                {
                    CardTerminalId = b.TerminalId, BatchNumber = b.BatchNumber, BatchTotal = b.Total, VoucherCount = b.Count,
                    ExpectedTotal = esperadoPorDatafono.GetValueOrDefault((r.PaymentMeansId, b.TerminalId)),
                });
            foreach (var x in p.Referencias)
                linea.ReferenceChecks.Add(new CashCountReferenceCheck { DocumentPaymentId = x.PaymentId, IsVerified = x.Checked });
        }
        await db.SaveChangesAsync(ct);

        void Baja(Domain.Common.AuditableEntity e)
        {
            switch (e)
            {
                case CashCountDenomination d: d.IsDeleted = true; d.DeletedAt = reloj.UtcNow; break;
                case CashCountTerminalBatch b: b.IsDeleted = true; b.DeletedAt = reloj.UtcNow; break;
                case CashCountReferenceCheck r: r.IsDeleted = true; r.DeletedAt = reloj.UtcNow; break;
                case CashCountLine l: l.IsDeleted = true; l.DeletedAt = reloj.UtcNow; break;
            }
        }
    }

    /// <summary>Lo esperado por (medio, datáfono) en la sesión: Σ recibido − reintegrado de los pagos confirmados.</summary>
    private async Task<Dictionary<(int, int), decimal>> DatafonosAsync(int sesionId, CancellationToken ct)
    {
        var filas = await (from p in db.DocumentPayments.AsNoTracking()
                           join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                           where p.CashSessionId == sesionId && !p.IsDeleted && d.Status == DocumentStatus.Confirmed && p.CardTerminalId != null
                           select new { p.PaymentMeansId, Terminal = p.CardTerminalId!.Value, p.Direction, p.Amount }).ToListAsync(ct);
        return filas.GroupBy(f => (f.PaymentMeansId, f.Terminal))
            .ToDictionary(g => g.Key, g => g.Sum(f => f.Direction == PaymentDirection.Received ? f.Amount : -f.Amount));
    }

    /// <summary>
    /// Crea el documento <c>CashCountDifference</c> de la sesión con sus líneas y lo confirma por el flujo canónico: dentro de la tolerancia
    /// queda confirmado; por encima, en aprobación (sin el cajero).
    /// </summary>
    public async Task<Result<CashDocumentRefDto>> DiferenciaAsync(CashSession sesion, CashCount arqueo, ResultadoDeArqueo resultado, int sucursal, int usuario,
        CancellationToken ct)
    {
        var tipo = await TiposDeCaja.PorDefectoAsync(db, DocumentClass.CashCountDifference, ct);
        if (tipo is null) return Result.Failure<CashDocumentRefDto>(InventoryErrors.DocumentClassNotAvailable(DocumentClass.CashCountDifference));
        var documento = new InventoryDocument
        {
            Class = DocumentClass.CashCountDifference,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = reloj.HoyLocal,
            CreatedByUserId = usuario,
            BranchId = sucursal,
            PointOfSaleId = sesion.PointOfSaleId,
            CashRegisterId = sesion.CashRegisterId,
            CashSessionId = sesion.Id,
            Currency = InventoryDocument.MonedaPorDefecto,
            ExchangeRate = 1m,
            Reason = Motivo(resultado),
        };
        db.InventoryDocuments.Add(documento);
        await db.SaveChangesAsync(ct);
        arqueo.DifferenceDocumentId = documento.Id;
        return await LineasYConfirmarAsync(sesion, arqueo, documento, resultado, ct);
    }

    /// <summary>En un reconteo: reemplaza las líneas del documento devuelto a borrador y lo vuelve a confirmar.</summary>
    public async Task<Result<CashDocumentRefDto>> ReemplazarDiferenciaAsync(CashSession sesion, CashCount arqueo, InventoryDocument documento, ResultadoDeArqueo resultado,
        CancellationToken ct)
    {
        foreach (var vieja in await db.CashDocumentLines.Where(l => l.DocumentId == documento.Id && !l.IsDeleted).ToListAsync(ct))
        {
            vieja.IsDeleted = true;
            vieja.DeletedAt = reloj.UtcNow;
        }
        documento.Reason = Motivo(resultado);
        await db.SaveChangesAsync(ct);
        return await LineasYConfirmarAsync(sesion, arqueo, documento, resultado, ct);
    }

    private async Task<Result<CashDocumentRefDto>> LineasYConfirmarAsync(CashSession sesion, CashCount arqueo, InventoryDocument documento, ResultadoDeArqueo resultado,
        CancellationToken ct)
    {
        foreach (var d in resultado.DifferenceLines)
        {
            db.CashDocumentLines.Add(new CashDocumentLine
            {
                DocumentId = documento.Id,
                LineNumber = d.LineNumber,
                CashCountLineId = arqueo.Lines.First(l => !l.IsDeleted && l.PaymentMeansId == d.PaymentMeansId).Id,
                PaymentMeansId = d.PaymentMeansId,
                Sign = d.Sign,
                Amount = d.Amount,
                Treatment = d.Treatment,
                WithinTolerance = d.WithinTolerance,
                CashierUserId = sesion.CashierUserId,
                CashierPersonId = sesion.CashierPersonId,
                Reason = d.Reason ?? string.Empty,
            });
        }
        documento.Subtotal = resultado.DifferenceLines.Sum(l => l.Amount);
        documento.Total = documento.Subtotal;
        documento.AmountDue = documento.Subtotal;
        await db.SaveChangesAsync(ct);

        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(documento.PublicId, DocumentClassGroup.Cash), ct);
        return confirmada.IsFailure
            ? Result.Failure<CashDocumentRefDto>(confirmada.Error)
            : Result.Success(SesionesDeCaja.Referencia(documento, confirmada.Value.Approval?.RequestPublicId));
    }

    /// <summary>Las líneas de la respuesta del cierre y del reconteo.</summary>
    public static IReadOnlyList<CashCountLineDto> Lineas(ResultadoDeArqueo resultado) =>
        resultado.Lines.Select(l => new CashCountLineDto(l.Code, l.Expected, l.Counted, l.Difference, l.Tolerance, l.WithinTolerance, l.Treatment)).ToList();

    private static string Motivo(ResultadoDeArqueo resultado)
    {
        var texto = string.Join("; ", resultado.DifferenceLines.Select(l => $"{l.Code}: {l.Reason}"));
        return texto.Length <= 500 ? texto : texto[..500];
    }

    private static Error Invalido(string campo, string mensaje) => new ErrorConDatos(Error.Validation.Code, mensaje, new { field = campo });
}
