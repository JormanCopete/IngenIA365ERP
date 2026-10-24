using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

// Feature 015, US6 (T0195; R-17; FR-066 a FR-074, FR-171, FR-197 a FR-199; contracts/api.md §5): la identificación de los abonos del extracto de Tesorería —por persona y
// producto con suma exacta, como partida por identificar, clasificada a un concepto con aprobación o descartada como duplicado— y las corridas que la registran. El abono es
// de Tesorería (R-06): Recaudo lo lee y marca lo identificado por IMovimientosBancariosParaRecaudo.

/// <summary>Una parte de la identificación de un abono: un crédito, una cuenta o un concepto vario de un titular, con su valor. (nuevo)</summary>
public sealed record ParteDeAbono(ApplicationTarget Target, Guid BeneficiaryPersonPublicId, decimal Amount, Guid? ProductPublicId = null, Guid? MiscConceptPublicId = null,
    string? CrossDocument = null, IReadOnlyList<Guid>? InstallmentPublicIds = null);

/// <summary>Lo identificado de un abono. (nuevo)</summary>
public sealed record BankItemIdentificationDto(Guid PublicId, Guid BankStatementLinePublicId, int Sequence, BankItemOutcome Outcome, decimal Amount, string? ReceiptNumber,
    string? UnidentifiedItemNumber, string? MiscConceptCode, Guid? PaymentOrderPublicId, DateTime IdentifiedAt);

/// <summary>El resultado de identificar un abono: la corrida (con su línea) y lo que se registró. (nuevo)</summary>
public sealed record IdentificacionDeAbonoDto(ProcessingRunDto Run, IReadOnlyList<BankItemIdentificationDto> Identifications, decimal Identified, decimal Pending,
    bool LateValueDate, string? CreditBalanceNumber);

/// <summary>Lo que se pide de un abono en la corrida (para reprocesarlo igual). (nuevo)</summary>
public sealed record PedidoDeAbono(Guid LinePublicId, int Sequence, Guid PayerPersonPublicId, IReadOnlyList<ParteDeAbono> Parts, bool RemainderAsUnidentified,
    PaymentMeansClass MeansClass, string? Note);

/// <summary>
/// <c>GET /api/receipts/bank-items?account=&amp;from=&amp;to=&amp;status=</c> (<c>Receipts.BankItems.View</c>): los abonos por identificar de los extractos de Tesorería, de las
/// oficinas al alcance. (nuevo)
/// </summary>
public sealed record ListBankItemsQuery(Guid? AccountPublicId = null, DateOnly? From = null, DateOnly? To = null, string? Reference = null, bool OnlyPending = true,
    int Page = 1, int PageSize = 50) : IRequest<Result<PaginaDto<LineaDeExtractoDto>>>;

public sealed class ListBankItemsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IMovimientosBancariosParaRecaudo movimientos)
    : IRequestHandler<ListBankItemsQuery, Result<PaginaDto<LineaDeExtractoDto>>>
{
    public async Task<Result<PaginaDto<LineaDeExtractoDto>>> Handle(ListBankItemsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        IReadOnlyCollection<Guid>? oficinas = null;
        if (!mio.Total)
        {
            var ids = mio.Oficinas.ToList();
            oficinas = await db.Branches.AsNoTracking().Where(b => ids.Contains(b.Id)).Select(b => b.PublicId).ToListAsync(ct);
        }
        return Result.Success(await movimientos.AbonosPorIdentificarAsync(new FiltroDeAbonos(request.AccountPublicId, request.From, request.To, request.Reference,
            request.Page, request.PageSize, request.OnlyPending, oficinas), ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/bank-items/{lineId}/identify</c> (<c>Receipts.BankItems.Identify</c>): identifica un abono —entre varias personas y productos, con suma exacta
/// (<c>Receipts.BankItem.SplitMismatch</c>), o con el resto como partida si se pide— y lo aplica en forma no interactiva con un recibo de canal banco, la fecha valor del
/// movimiento y la modalidad del canal (FR-068, FR-198), como una línea de la corrida <see cref="RunPublicId"/> (o de una nueva). Un rechazo del contrato no es un error de
/// la operación: la línea queda rechazada en la corrida con su motivo y se reprocesa con su clave (FR-171). (nuevo)
/// </summary>
public sealed record IdentifyBankItemCommand(Guid LinePublicId, IReadOnlyList<ParteDeAbono> Parts, Guid? PayerPersonPublicId = null, bool RemainderAsUnidentified = false,
    string? Note = null, Guid? RunPublicId = null, PaymentMeansClass MeansClass = PaymentMeansClass.Transfer)
    : IRequest<Result<IdentificacionDeAbonoDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class IdentifyBankItemCommandValidator : AbstractValidator<IdentifyBankItemCommand>
{
    public IdentifyBankItemCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Parts).NotEmpty().WithMessage("Indique a qué personas y productos va el abono.");
        RuleForEach(x => x.Parts).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0m);
            p.RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty();
            p.RuleFor(x => x.ProductPublicId).NotNull().When(x => x.Target is ApplicationTarget.Loan or ApplicationTarget.Account)
                .WithMessage("Un crédito o una cuenta se identifica por su producto.");
            p.RuleFor(x => x.MiscConceptPublicId).NotNull().When(x => x.Target == ApplicationTarget.MiscConcept);
            p.RuleFor(x => x.Target).Must(t => t is ApplicationTarget.Loan or ApplicationTarget.Account or ApplicationTarget.MiscConcept)
                .WithMessage("Un abono se aplica a un crédito, una cuenta o un concepto vario.");
        });
        RuleFor(x => x.MeansClass).Must(m => m is PaymentMeansClass.Transfer or PaymentMeansClass.BankDeposit)
            .WithMessage("El medio de un abono es transferencia o consignación.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class IdentifyBankItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    AplicacionNoInteractiva aplicacion,
    PartidasPorIdentificar partidas,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<IdentifyBankItemCommand, Result<IdentificacionDeAbonoDto>>
{
    public async Task<Result<IdentificacionDeAbonoDto>> Handle(IdentifyBankItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IdentificacionDeAbonoDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<IdentificacionDeAbonoDto>(visto.Error);
        var (linea, oficina) = visto.Value;
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente <= 0m) return Result.Failure<IdentificacionDeAbonoDto>(ErroresDeRecaudo.AbonoYaIdentificado());
        var dividido = request.Parts.Sum(p => p.Amount);
        if (dividido > pendiente) return Result.Failure<IdentificacionDeAbonoDto>(ErroresDeRecaudo.AbonoSobreIdentificado(pendiente));
        if (dividido < pendiente && !request.RemainderAsUnidentified) return Result.Failure<IdentificacionDeAbonoDto>(ErroresDeRecaudo.DivisionNoCuadra(pendiente, dividido));

        var ahora = reloj.UtcNow;
        var corrida = await CorridasNoInteractivas.AbrirAsync(db, ProcessingRunKind.BankItem, linea.PublicId, $"{linea.StatementNumber}·{linea.MovementDate:yyyyMMdd}",
            request.RunPublicId, usuario, ahora, ct);
        await db.SaveChangesAsync(ct);
        var secuencia = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct);
        var pedido = new PedidoDeAbono(linea.PublicId, secuencia, request.PayerPersonPublicId ?? request.Parts[0].BeneficiaryPersonPublicId, request.Parts,
            request.RemainderAsUnidentified, request.MeansClass, request.Note);
        return await AbonosDeRecaudo.ProcesarAsync(db, aplicacion, partidas, movimientos, auditoria, corrida, linea, oficina, pedido, usuario, reloj, ct);
    }
}

/// <summary>
/// <c>POST /api/receipts/bank-items/{lineId}/unidentified</c> (<c>Receipts.BankItems.Identify</c>): deja lo que queda del abono como partida por identificar (FR-070): un
/// pasivo con banco, cuenta, fecha, valor y referencia, sin persona, con su hecho <c>PartidaPorIdentificarReconocida</c>. (nuevo)
/// </summary>
public sealed record MarkBankItemUnidentifiedCommand(Guid LinePublicId, string? Note = null)
    : IRequest<Result<UnidentifiedItemDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkBankItemUnidentifiedCommandValidator : AbstractValidator<MarkBankItemUnidentifiedCommand>
{
    public MarkBankItemUnidentifiedCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class MarkBankItemUnidentifiedCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    ICerrojoDeRecaudo cerrojo,
    PartidasPorIdentificar partidas,
    IDateTimeService reloj) : IRequestHandler<MarkBankItemUnidentifiedCommand, Result<UnidentifiedItemDto>>
{
    public async Task<Result<UnidentifiedItemDto>> Handle(MarkBankItemUnidentifiedCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<UnidentifiedItemDto>(visto.Error);
        var (linea, oficina) = visto.Value;
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente <= 0m) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.AbonoYaIdentificado());
        await cerrojo.TomarSetupAsync(ct: ct);
        var partida = await partidas.ReconocerAsync(linea, oficina, pendiente, usuario, request.Note, ct);
        await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Amount, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PartidasPorIdentificar.DtoAsync(db, partida, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/bank-items/{lineId}/classify</c> (<c>Receipts.BankItems.Classify</c>): clasifica (lo que queda de) un abono que no es recaudo a un concepto de
/// ingreso —rendimiento, reintegro, traslado propio o giro devuelto— con la aprobación de otra persona presente (sujeto <c>BankItemClassification</c>; FR-072). El giro
/// devuelto (concepto <c>GIRODEV</c>) se enlaza a la orden girada y alerta al origen y al tesorero (<c>Tesoreria.GiroDevuelto</c>); Tesorería no cambia el resultado (FR-199).
/// (nuevo)
/// </summary>
public sealed record ClassifyBankItemCommand(Guid LinePublicId, Guid MiscConceptPublicId, string Reason, decimal? Amount = null, Guid? PaymentOrderPublicId = null)
    : IRequest<Result<BankItemIdentificationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    [JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class ClassifyBankItemCommandValidator : AbstractValidator<ClassifyBankItemCommand>
{
    public ClassifyBankItemCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.MiscConceptPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
    }
}

public sealed class ClassifyBankItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    IOrdenesDePagoDeTesoreria ordenes,
    ICerrojoDeRecaudo cerrojo,
    AprobacionPresenteEnCaja aprobacion,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ClassifyBankItemCommand, Result<BankItemIdentificationDto>>
{
    public const string ConceptoGiroDevuelto = "GIRODEV";

    public async Task<Result<BankItemIdentificationDto>> Handle(ClassifyBankItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<BankItemIdentificationDto>(visto.Error);
        var (linea, oficina) = visto.Value;
        var pendiente = linea.Amount - linea.Identified;
        if (pendiente <= 0m) return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.AbonoYaIdentificado());
        var valor = request.Amount ?? pendiente;
        if (valor > pendiente) return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.AbonoSobreIdentificado(pendiente));
        var concepto = await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.MiscConceptPublicId, ct);
        if (concepto is null || concepto.Status != ReceiptsCatalogStatus.Active || concepto.Direction != MiscConceptDirection.Income)
            return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.ConceptoDeClasificacionInvalido(concepto?.Code ?? request.MiscConceptPublicId.ToString()));
        var giroDevuelto = concepto.Code == ConceptoGiroDevuelto;
        EstadoDeOrdenDto? orden = null;
        if (giroDevuelto)
        {
            if (request.PaymentOrderPublicId is not { } o) return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.GiroDevueltoSinOrden());
            try { orden = await ordenes.EstadoAsync(o, ct); }
            catch (ArgumentException) { return Result.Failure<BankItemIdentificationDto>(ErroresDeRecaudo.NoEncontrado("La orden de pago")); }
        }

        var huella = HuellaDeRecibo.Calcular(new { linea.PublicId, concepto = concepto.Code, valor, request.PaymentOrderPublicId });
        var aprobada = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(ApprovalSubjects.BankItemClassification, linea.PublicId,
            $"Clasificar el abono {linea.StatementNumber} a {concepto.Code}", valor, reloj.HoyLocal, huella, null, [usuario], request.ApprovalRequestPublicId), ct);
        if (aprobada.IsFailure) return Result.Failure<BankItemIdentificationDto>(aprobada.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        var secuencia = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct);
        var identificacion = new BankItemIdentification
        {
            BankStatementLinePublicId = linea.PublicId, Sequence = secuencia, OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate,
            Amount = valor, Outcome = giroDevuelto ? BankItemOutcome.ReturnedDisbursement : BankItemOutcome.Classified, MiscConceptId = concepto.Id,
            PaymentOrderPublicId = request.PaymentOrderPublicId, IdentifiedByUserId = usuario, IdentifiedAt = reloj.UtcNow,
            ApprovalRequestPublicId = aprobada.Value.ApprovalRequestPublicId, Note = request.Reason.Trim(),
        };
        db.BankItemIdentifications.Add(identificacion);
        await movimientos.MarcarIdentificacionAsync(linea.PublicId, linea.Identified + valor, ct);
        await db.SaveChangesAsync(ct);
        var numero = string.Create(CultureInfo.InvariantCulture, $"{linea.StatementNumber}-{secuencia}");
        await emision.PartidaClasificadaAsync(identificacion.PublicId, numero, false, giroDevuelto ? "ReturnedDisbursement" : "ClassifiedToConcept", concepto.Code, valor,
            linea.MovementDate, oficina, linea.OwnBankAccountPublicId, request.PaymentOrderPublicId, aprobada.Value.ApprovalRequestPublicId, ct);
        await auditoria.AgregarAsync("Receipts.BankItem.Classified", "BankItemIdentification", identificacion.PublicId, null,
            new { line = linea.PublicId, concept = concepto.Code, amount = valor, order = orden?.Number, approval = aprobada.Value.ApprovalRequestPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        if (giroDevuelto && orden is not null)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaGiroDevuelto, "Giro devuelto por el banco",
                string.Create(CultureInfo.InvariantCulture,
                    $"El banco devolvió {valor:N0} de la orden {orden.Number}, ya informada «girada»: el origen gestiona el reintegro o una orden nueva."),
                "PaymentOrder", orden.PaymentOrderPublicId, DedupKey: $"Tesoreria.GiroDevuelto:{identificacion.PublicId:N}", ModuloDeOrigen: "Receipts"), ct);
        return Result.Success(await AbonosDeRecaudo.DtoAsync(db, identificacion, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/bank-items/{lineId}/duplicate-decision</c> (<c>Receipts.BankItems.Identify</c>): una persona decide un posible duplicado (FR-197): si es un
/// duplicado, sale de lo que Recaudo identifica (queda la identificación <c>PossibleDuplicateDismissed</c>); si no, sigue como un movimiento distinto. (nuevo)
/// </summary>
public sealed record DecideBankItemDuplicateCommand(Guid LinePublicId, bool IsDuplicate, string? Note = null)
    : IRequest<Result<LineaDeExtractoDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideBankItemDuplicateCommandValidator : AbstractValidator<DecideBankItemDuplicateCommand>
{
    public DecideBankItemDuplicateCommandValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class DecideBankItemDuplicateCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    ICerrojoDeRecaudo cerrojo,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<DecideBankItemDuplicateCommand, Result<LineaDeExtractoDto>>
{
    public async Task<Result<LineaDeExtractoDto>> Handle(DecideBankItemDuplicateCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<LineaDeExtractoDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, request.LinePublicId, ct, admitirDuplicado: true);
        if (visto.IsFailure) return Result.Failure<LineaDeExtractoDto>(visto.Error);
        var (linea, _) = visto.Value;
        if (!linea.PossibleDuplicate) return Result.Failure<LineaDeExtractoDto>(ErroresDeRecaudo.AbonoNoEsDuplicado());
        await cerrojo.TomarSetupAsync(ct: ct);
        await movimientos.DecidirDuplicadoAsync(linea.PublicId, request.IsDuplicate, ct);
        if (request.IsDuplicate)
            db.BankItemIdentifications.Add(new BankItemIdentification
            {
                BankStatementLinePublicId = linea.PublicId, Sequence = await AbonosDeRecaudo.SiguienteSecuenciaAsync(db, linea.PublicId, ct),
                OwnBankAccountPublicId = linea.OwnBankAccountPublicId, MovementDate = linea.MovementDate, Amount = linea.Amount - linea.Identified,
                Outcome = BankItemOutcome.PossibleDuplicateDismissed, IdentifiedByUserId = usuario, IdentifiedAt = reloj.UtcNow, Note = request.Note?.Trim(),
            });
        await auditoria.AgregarAsync("Receipts.BankItem.DuplicateDecided", "BankStatementLine", linea.PublicId, null,
            new { duplicate = request.IsDuplicate, note = request.Note, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success((await movimientos.LineaAsync(linea.PublicId, ct))!);
    }
}

/// <summary>
/// <c>POST /api/receipts/processing-runs/{id}/lines/{lineId}/retry</c> (<c>Receipts.BankItems.Identify</c>): reprocesa una línea rechazada o pendiente de una corrida con
/// <b>la misma clave</b> (el contrato no duplica, FR-171). (nuevo)
/// </summary>
public sealed record RetryProcessingRunLineCommand(Guid RunPublicId, Guid LinePublicId) : IRequest<Result<ProcessingRunDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RetryProcessingRunLineCommandValidator : AbstractValidator<RetryProcessingRunLineCommand>
{
    public RetryProcessingRunLineCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
    }
}

public sealed class RetryProcessingRunLineCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IMovimientosBancariosParaRecaudo movimientos,
    AplicacionNoInteractiva aplicacion,
    PartidasPorIdentificar partidas,
    AuditoriaDeRecaudo auditoria,
    IEnumerable<IReprocesoDeCorrida> otras,
    IDateTimeService reloj) : IRequestHandler<RetryProcessingRunLineCommand, Result<ProcessingRunDto>>
{
    public async Task<Result<ProcessingRunDto>> Handle(RetryProcessingRunLineCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var corrida = await db.ProcessingRuns.FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        var linea = corrida is null ? null : await db.ProcessingRunLines.FirstOrDefaultAsync(l => l.PublicId == request.LinePublicId && l.ProcessingRunId == corrida.Id, ct);
        if (corrida is null || linea is null) return Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.NoEncontrado("La línea de la corrida"));
        if (linea.Outcome is not (ProcessingLineOutcome.Rejected or ProcessingLineOutcome.Pending))
            return Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.CorridaLineaNoReprocesable(linea.Outcome.ToString()));
        if (corrida.Kind != ProcessingRunKind.BankItem)
        {
            var otra = otras.FirstOrDefault(o => o.Clase == corrida.Kind);
            if (otra is null) return Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.CorridaLineaNoReprocesable(corrida.Kind.ToString()));
            var r = await otra.ReprocesarAsync(corrida, linea, usuario, ct);
            if (r.IsFailure) return Result.Failure<ProcessingRunDto>(r.Error);
            return Result.Success(await CorridasNoInteractivas.DtoAsync(db, corrida, ct));
        }
        var pedido = CorridasNoInteractivas.Pedido<PedidoDeAbono>(linea);
        if (pedido is null) return Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.CorridaLineaNoReprocesable("sin pedido"));
        var visto = await AbonosDeRecaudo.VisibleAsync(db, alcance, movimientos, pedido.LinePublicId, ct);
        if (visto.IsFailure) return Result.Failure<ProcessingRunDto>(visto.Error);
        var (abono, oficina) = visto.Value;
        var r2 = await AbonosDeRecaudo.ProcesarAsync(db, aplicacion, partidas, movimientos, auditoria, corrida, abono, oficina, pedido, usuario, reloj, ct);
        return r2.IsFailure ? Result.Failure<ProcessingRunDto>(r2.Error) : Result.Success(r2.Value.Run);
    }
}

/// <summary>El reproceso de una línea de una corrida de otra clase (el giro de una pagaduría, fase 12): lo registra quien la procesa. (nuevo)</summary>
public interface IReprocesoDeCorrida
{
    ProcessingRunKind Clase { get; }

    Task<Result> ReprocesarAsync(ProcessingRun corrida, ProcessingRunLine linea, int usuario, CancellationToken ct);
}

/// <summary><c>GET /api/receipts/processing-runs/{id}</c> (<c>Receipts.BankItems.Identify</c>): la corrida con sus líneas. (nuevo)</summary>
public sealed record GetProcessingRunQuery(Guid RunPublicId) : IRequest<Result<ProcessingRunDto>>;

public sealed class GetProcessingRunQueryHandler(IApplicationDbContext db) : IRequestHandler<GetProcessingRunQuery, Result<ProcessingRunDto>>
{
    public async Task<Result<ProcessingRunDto>> Handle(GetProcessingRunQuery request, CancellationToken ct)
    {
        var corrida = await db.ProcessingRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        return corrida is null
            ? Result.Failure<ProcessingRunDto>(ErroresDeRecaudo.NoEncontrado("La corrida"))
            : Result.Success(await CorridasNoInteractivas.DtoAsync(db, corrida, ct));
    }
}

/// <summary><c>GET /api/receipts/bank-items/{lineId}/identifications</c> (<c>Receipts.BankItems.View</c>): lo identificado de un abono. (nuevo)</summary>
public sealed record ListBankItemIdentificationsQuery(Guid LinePublicId) : IRequest<Result<IReadOnlyList<BankItemIdentificationDto>>>;

public sealed class ListBankItemIdentificationsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListBankItemIdentificationsQuery, Result<IReadOnlyList<BankItemIdentificationDto>>>
{
    public async Task<Result<IReadOnlyList<BankItemIdentificationDto>>> Handle(ListBankItemIdentificationsQuery request, CancellationToken ct)
    {
        var filas = await db.BankItemIdentifications.AsNoTracking().Where(i => i.BankStatementLinePublicId == request.LinePublicId).OrderBy(i => i.Sequence).ToListAsync(ct);
        var lista = new List<BankItemIdentificationDto>(filas.Count);
        foreach (var f in filas) lista.Add(await AbonosDeRecaudo.DtoAsync(db, f, ct));
        return Result.Success<IReadOnlyList<BankItemIdentificationDto>>(lista);
    }
}

/// <summary>Lo común de los abonos que Recaudo identifica: el abono visible, la secuencia, el proceso de una línea y su DTO. (nuevo)</summary>
public static class AbonosDeRecaudo
{
    /// <summary>El abono por su <c>PublicId</c> con la oficina de su cuenta, si está al alcance; fuera de alcance, el 404 genérico.</summary>
    public static async Task<Result<(LineaDeExtractoDto Linea, int Oficina)>> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance,
        IMovimientosBancariosParaRecaudo movimientos, Guid publicId, CancellationToken ct, bool admitirDuplicado = false)
    {
        var linea = await movimientos.LineaAsync(publicId, ct);
        var oficina = linea?.BranchPublicId is { } b
            ? await db.Branches.AsNoTracking().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        if (linea is null || oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Value))
            return Result.Failure<(LineaDeExtractoDto, int)>(ErroresDeRecaudo.NoEncontrado("El abono"));
        if (!admitirDuplicado && linea.PossibleDuplicate) return Result.Failure<(LineaDeExtractoDto, int)>(ErroresDeRecaudo.AbonoDuplicadoPendiente());
        if (linea.RecaudoStatus == "NotApplicable" && !admitirDuplicado) return Result.Failure<(LineaDeExtractoDto, int)>(ErroresDeRecaudo.AbonoYaIdentificado());
        return Result.Success((linea, oficina.Value));
    }

    public static async Task<int> SiguienteSecuenciaAsync(IApplicationDbContext db, Guid linea, CancellationToken ct) =>
        (db.BankItemIdentifications.Local.Where(i => i.BankStatementLinePublicId == linea).Select(i => (int?)i.Sequence).Max()
         ?? await db.BankItemIdentifications.AsNoTracking().Where(i => i.BankStatementLinePublicId == linea).MaxAsync(i => (int?)i.Sequence, ct) ?? 0) + 1;

    /// <summary>La clave de una línea de abono en la corrida: <c>REC:banco:{línea:N}:{secuencia}</c>.</summary>
    public static string Clave(Guid linea, int secuencia) => string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:banco:{linea:N}:{secuencia}");

    /// <summary>
    /// Procesa la identificación de un abono como una línea de la corrida: el recibo no interactivo, la identificación, el resto como partida si se pidió, lo marcado en
    /// el extracto y el cierre de la tanda (evento resumen). Un rechazo del contrato queda en la línea, no como error.
    /// </summary>
    public static async Task<Result<IdentificacionDeAbonoDto>> ProcesarAsync(IApplicationDbContext db, AplicacionNoInteractiva aplicacion, PartidasPorIdentificar partidas,
        IMovimientosBancariosParaRecaudo movimientos, AuditoriaDeRecaudo auditoria, ProcessingRun corrida, LineaDeExtractoDto linea, int oficina, PedidoDeAbono pedido,
        int usuario, IDateTimeService reloj, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var clave = Clave(linea.PublicId, pedido.Sequence);
        var lineaDeCorrida = await CorridasNoInteractivas.LineaAsync(db, corrida, $"{linea.StatementNumber}·{pedido.Sequence}", clave, pedido.Parts.Sum(p => p.Amount), pedido, ct);
        await db.SaveChangesAsync(ct);
        var resultado = await aplicacion.AplicarAsync(new LineaNoInteractiva(clave, ReceiptChannel.Bank, pedido.PayerPersonPublicId, linea.MovementDate,
            pedido.Parts.Sum(p => p.Amount), oficina, pedido.Parts.Select(p => new ParteNoInteractiva(p.Target, p.ProductPublicId, p.BeneficiaryPersonPublicId, p.Amount,
                p.MiscConceptPublicId, p.CrossDocument, InstallmentPublicIds: p.InstallmentPublicIds)).ToList(),
            pedido.MeansClass == PaymentMeansClass.BankDeposit ? ClaseDeMedioDeRecaudo.BankDeposit : ClaseDeMedioDeRecaudo.Transfer, pedido.MeansClass,
            linea.OwnBankAccountPublicId, linea.Reference, linea.PublicId), ct);
        CorridasNoInteractivas.Anotar(corrida, lineaDeCorrida, resultado, ahora);
        var identificaciones = new List<BankItemIdentification>();
        UnidentifiedItemDto? partida = null;
        if (resultado.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && resultado.Receipt is { } recibo)
        {
            if (!await db.BankItemIdentifications.AnyAsync(i => i.BankStatementLinePublicId == linea.PublicId && i.ReceiptId == recibo.Id, ct))
            {
                var i = new BankItemIdentification
                {
                    BankStatementLinePublicId = linea.PublicId, Sequence = pedido.Sequence, OwnBankAccountPublicId = linea.OwnBankAccountPublicId,
                    MovementDate = linea.MovementDate, Amount = recibo.TotalMeans, Outcome = BankItemOutcome.Receipt, ReceiptId = recibo.Id, IdentifiedByUserId = usuario,
                    IdentifiedAt = ahora, Note = pedido.Note,
                };
                db.BankItemIdentifications.Add(i);
                identificaciones.Add(i);
            }
            var identificado = linea.Identified + recibo.TotalMeans;
            var resto = linea.Amount - identificado;
            if (resto > 0m && pedido.RemainderAsUnidentified)
            {
                var nueva = await partidas.ReconocerAsync(linea, oficina, resto, usuario, pedido.Note, ct, pedido.Sequence + 1);
                partida = await PartidasPorIdentificar.DtoAsync(db, nueva, reloj.HoyLocal, ct);
                identificado = linea.Amount;
            }
            await movimientos.MarcarIdentificacionAsync(linea.PublicId, identificado, ct);
        }
        await db.SaveChangesAsync(ct);
        await CorridasNoInteractivas.CerrarTandaAsync(db, auditoria, corrida, resultado.Receipt is { } r && resultado.Outcome == ProcessingLineOutcome.Applied ? [r] : [],
            resultado.Outcome == ProcessingLineOutcome.Rejected ? [lineaDeCorrida] : [], ahora, ct);
        await db.SaveChangesAsync(ct);
        var actual = await movimientos.LineaAsync(linea.PublicId, ct);
        var dtos = new List<BankItemIdentificationDto>();
        foreach (var i in identificaciones) dtos.Add(await DtoAsync(db, i, ct));
        return Result.Success(new IdentificacionDeAbonoDto(await CorridasNoInteractivas.DtoAsync(db, corrida, ct), dtos, actual?.Identified ?? linea.Identified,
            (actual?.Amount ?? linea.Amount) - (actual?.Identified ?? linea.Identified), resultado.LateValueDate, resultado.CreditBalance?.OriginDocumentNumber ?? partida?.Number));
    }

    public static async Task<BankItemIdentificationDto> DtoAsync(IApplicationDbContext db, BankItemIdentification i, CancellationToken ct)
    {
        var recibo = i.ReceiptId is { } r ? await db.IssuedReceipts.AsNoTracking().Where(x => x.Id == r).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;
        var partida = i.UnidentifiedItemId is { } p ? await db.UnidentifiedItems.AsNoTracking().Where(x => x.Id == p).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;
        var concepto = i.MiscConceptId is { } c ? await db.MiscConcepts.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        return new BankItemIdentificationDto(i.PublicId, i.BankStatementLinePublicId, i.Sequence, i.Outcome, i.Amount, recibo, partida, concepto, i.PaymentOrderPublicId,
            i.IdentifiedAt);
    }
}
