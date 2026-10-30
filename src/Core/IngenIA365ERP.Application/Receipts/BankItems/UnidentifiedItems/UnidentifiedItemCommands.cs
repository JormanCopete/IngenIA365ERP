using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

// Feature 015, US6 (T0195; FR-070 a FR-073; contracts/api.md §5): las partidas por identificar —identificadas después, entera o en partes; clasificadas a un concepto con
// aprobación; devueltas al consignante por orden de pago a Tesorería; o llevadas a ingreso por una decisión registrada con aprobación, nunca automática—.

/// <summary><c>GET /api/receipts/unidentified-items?aging=&amp;state=&amp;depositor=</c> (<c>Receipts.UnidentifiedItems.View</c>): las partidas de las oficinas al alcance, por antigüedad. (nuevo)</summary>
public sealed record ListUnidentifiedItemsQuery(int? MinAgeDays = null, UnidentifiedItemState? State = null, string? Depositor = null, bool OnlyOpen = true)
    : IRequest<Result<IReadOnlyList<UnidentifiedItemDto>>>;

public sealed class ListUnidentifiedItemsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListUnidentifiedItemsQuery, Result<IReadOnlyList<UnidentifiedItemDto>>>
{
    public async Task<Result<IReadOnlyList<UnidentifiedItemDto>>> Handle(ListUnidentifiedItemsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;
        var consulta = db.UnidentifiedItems.AsNoTracking();
        if (request.State is { } e) consulta = consulta.Where(p => p.Status == e);
        else if (request.OnlyOpen) consulta = consulta.Where(p => p.Status == UnidentifiedItemState.Open || p.Status == UnidentifiedItemState.PartiallyIdentified);
        if (request.MinAgeDays is { } dias) consulta = consulta.Where(p => p.OpenedOn <= hoy.AddDays(-dias));
        if (!string.IsNullOrWhiteSpace(request.Depositor))
        {
            var d = request.Depositor.Trim();
            consulta = consulta.Where(p => (p.DepositorTaxId != null && p.DepositorTaxId == d) || (p.DepositorName != null && p.DepositorName.Contains(d)));
        }
        var filas = (await consulta.OrderBy(p => p.OpenedOn).Take(500).ToListAsync(ct)).Where(p => mio.IncluyeOficina(p.BranchId)).ToList();
        var lista = new List<UnidentifiedItemDto>(filas.Count);
        foreach (var p in filas) lista.Add(await PartidasPorIdentificar.DtoAsync(db, p, hoy, ct));
        return Result.Success<IReadOnlyList<UnidentifiedItemDto>>(lista);
    }
}

/// <summary>Lo común de los comandos sobre una partida: la partida visible, en su alcance y con saldo. (nuevo)</summary>
internal static class PartidaVisible
{
    public static async Task<Result<UnidentifiedItem>> LeerAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, CancellationToken ct)
    {
        var p = await db.UnidentifiedItems.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (p is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(p.BranchId)) return Result.Failure<UnidentifiedItem>(ErroresDeRecaudo.NoEncontrado("La partida"));
        if (p.Balance <= 0m) return Result.Failure<UnidentifiedItem>(ErroresDeRecaudo.PartidaResuelta(p.Status.ToString()));
        return Result.Success(p);
    }

    /// <summary>La partida seguida, después de su cerrojo (nivel 6).</summary>
    public static async Task<UnidentifiedItem> BloquearAsync(IApplicationDbContext db, ICerrojoDeRecaudo cerrojo, UnidentifiedItem leida, CancellationToken ct)
    {
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Partidas, [leida.Id], ct: ct);
        return await db.UnidentifiedItems.FirstAsync(p => p.Id == leida.Id, ct);
    }
}

/// <summary>
/// <c>POST /api/receipts/unidentified-items/{id}/identify</c> (<c>Receipts.UnidentifiedItems.Resolve</c>): identifica la partida, entera o en partes (FR-071), con un
/// recibo de canal banco aplicado en forma no interactiva desde la partida (débito <c>PartidasPorIdentificar</c>). En R0 se aplica con la fecha del movimiento o, si el
/// contrato no la admite, con la más antigua que admite; el recibo lo dice y se alerta al módulo (<c>Recaudo.FechaValorTardia</c>). Aquí el rechazo del contrato sí es un
/// error de la operación (se ve en pantalla y no deja nada). (nuevo)
/// </summary>
public sealed record IdentifyUnidentifiedItemCommand(Guid ItemPublicId, IReadOnlyList<ParteDeAbono> Parts, Guid? PayerPersonPublicId = null, string? Note = null,
    PaymentMeansClass MeansClass = PaymentMeansClass.Transfer) : IRequest<Result<UnidentifiedItemDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class IdentifyUnidentifiedItemCommandValidator : AbstractValidator<IdentifyUnidentifiedItemCommand>
{
    public IdentifyUnidentifiedItemCommandValidator()
    {
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.Parts).NotEmpty();
        RuleForEach(x => x.Parts).ChildRules(p =>
        {
            p.RuleFor(x => x.Amount).GreaterThan(0m);
            p.RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty();
            p.RuleFor(x => x.ProductPublicId).NotNull().When(x => x.Target is ApplicationTarget.Loan or ApplicationTarget.Account);
            p.RuleFor(x => x.MiscConceptPublicId).NotNull().When(x => x.Target == ApplicationTarget.MiscConcept);
        });
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class IdentifyUnidentifiedItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    AplicacionNoInteractiva aplicacion,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<IdentifyUnidentifiedItemCommand, Result<UnidentifiedItemDto>>
{
    public async Task<Result<UnidentifiedItemDto>> Handle(IdentifyUnidentifiedItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await PartidaVisible.LeerAsync(db, alcance, request.ItemPublicId, ct);
        if (leida.IsFailure) return Result.Failure<UnidentifiedItemDto>(leida.Error);
        var valor = request.Parts.Sum(p => p.Amount);
        if (valor > leida.Value.Balance) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.PartidaSinSaldo(leida.Value.Balance));
        var n = await db.UnidentifiedItemResolutions.CountAsync(r => r.UnidentifiedItemId == leida.Value.Id, ct) + 1;
        var clave = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:partida:{leida.Value.PublicId:N}:{n}");
        var resultado = await aplicacion.AplicarAsync(new LineaNoInteractiva(clave, ReceiptChannel.Bank,
            request.PayerPersonPublicId ?? request.Parts[0].BeneficiaryPersonPublicId, leida.Value.MovementDate, valor, leida.Value.BranchId,
            request.Parts.Select(p => new ParteNoInteractiva(p.Target, p.ProductPublicId, p.BeneficiaryPersonPublicId, p.Amount, p.MiscConceptPublicId, p.CrossDocument,
                InstallmentPublicIds: p.InstallmentPublicIds)).ToList(),
            request.MeansClass == PaymentMeansClass.BankDeposit ? ClaseDeMedioDeRecaudo.BankDeposit : ClaseDeMedioDeRecaudo.Transfer, request.MeansClass,
            leida.Value.OwnBankAccountPublicId, leida.Value.Reference, leida.Value.BankStatementLinePublicId, UnidentifiedItemPublicId: leida.Value.PublicId,
            Origenes: [new OrigenABloquear(TablasDeRecaudo.Partidas, leida.Value.Id)]), ct);
        if (resultado.Outcome == ProcessingLineOutcome.Rejected || resultado.Receipt is null)
            return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.ItemRechazado([new { code = resultado.Code, message = resultado.Message }]));
        var partida = await db.UnidentifiedItems.FirstAsync(p => p.Id == leida.Value.Id, ct);
        if (resultado.Outcome == ProcessingLineOutcome.Applied)
        {
            PartidasPorIdentificar.Resolver(db, partida, UnidentifiedItemResolutionKind.IdentifiedToReceipt, resultado.Receipt.TotalMeans, resultado.Receipt.PublicId,
                resultado.Receipt.Number, null, null, usuario, reloj.UtcNow, request.Note);
            await auditoria.AgregarAsync("Receipts.UnidentifiedItem.Identified", "UnidentifiedItem", partida.PublicId, null,
                new { receipt = resultado.Receipt.Number, amount = valor, late = resultado.LateValueDate, by = actor.Name }, ct);
            await db.SaveChangesAsync(ct);
        }
        return Result.Success(await PartidasPorIdentificar.DtoAsync(db, partida, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/unidentified-items/{id}/classify</c> (<c>Receipts.UnidentifiedItems.Resolve</c>): clasifica (parte de) la partida a un concepto de ingreso
/// (rendimiento, reintegro, traslado propio, giro devuelto) con la aprobación de otra persona presente (sujeto <c>BankItemClassification</c>; FR-072). (nuevo)
/// </summary>
public sealed record ClassifyUnidentifiedItemCommand(Guid ItemPublicId, Guid MiscConceptPublicId, string Reason, decimal? Amount = null)
    : IRequest<Result<UnidentifiedItemDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    [JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class ClassifyUnidentifiedItemCommandValidator : AbstractValidator<ClassifyUnidentifiedItemCommand>
{
    public ClassifyUnidentifiedItemCommandValidator()
    {
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.MiscConceptPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
    }
}

public sealed class ClassifyUnidentifiedItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    AprobacionPresenteEnCaja aprobacion,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ClassifyUnidentifiedItemCommand, Result<UnidentifiedItemDto>>
{
    public async Task<Result<UnidentifiedItemDto>> Handle(ClassifyUnidentifiedItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await PartidaVisible.LeerAsync(db, alcance, request.ItemPublicId, ct);
        if (leida.IsFailure) return Result.Failure<UnidentifiedItemDto>(leida.Error);
        var valor = request.Amount ?? leida.Value.Balance;
        if (valor > leida.Value.Balance) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.PartidaSinSaldo(leida.Value.Balance));
        var concepto = await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.MiscConceptPublicId, ct);
        if (concepto is null || concepto.Status != ReceiptsCatalogStatus.Active || concepto.Direction != MiscConceptDirection.Income)
            return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.ConceptoDeClasificacionInvalido(concepto?.Code ?? request.MiscConceptPublicId.ToString()));
        var huella = HuellaDeRecibo.Calcular(new { leida.Value.PublicId, concepto = concepto.Code, valor });
        var aprobada = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(ApprovalSubjects.BankItemClassification, leida.Value.PublicId,
            $"Clasificar la partida {leida.Value.Number} a {concepto.Code}", valor, reloj.HoyLocal, huella, null, [usuario], request.ApprovalRequestPublicId), ct);
        if (aprobada.IsFailure) return Result.Failure<UnidentifiedItemDto>(aprobada.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        var partida = await PartidaVisible.BloquearAsync(db, cerrojo, leida.Value, ct);
        if (valor > partida.Balance) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.PartidaSinSaldo(partida.Balance));
        var r = PartidasPorIdentificar.Resolver(db, partida, UnidentifiedItemResolutionKind.ClassifiedToConcept, valor, Guid.NewGuid(), partida.Number, concepto.Id,
            aprobada.Value.ApprovalRequestPublicId, usuario, reloj.UtcNow, request.Reason);
        r.DocumentPublicId = r.PublicId;
        await db.SaveChangesAsync(ct);
        await emision.PartidaClasificadaAsync(r.PublicId, partida.Number, true, "ClassifiedToConcept", concepto.Code, valor, reloj.HoyLocal, partida.BranchId,
            partida.OwnBankAccountPublicId, null, aprobada.Value.ApprovalRequestPublicId, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.Classified", "UnidentifiedItem", partida.PublicId, null,
            new { concept = concepto.Code, amount = valor, approval = aprobada.Value.ApprovalRequestPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PartidasPorIdentificar.DtoAsync(db, partida, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/unidentified-items/{id}/refund-by-order</c> (<c>Receipts.UnidentifiedItems.Resolve</c>): devuelve (parte de) la partida al consignante con una
/// orden de pago a Tesorería (<see cref="IOrdenesDePagoDeTesoreria"/>, clase <c>UnidentifiedItemRefund</c>) en la misma transacción, y emite
/// <c>PartidaDevueltaAlConsignante</c>. Si Tesorería informa la orden rechazada o anulada, el saldo vuelve a la partida. (nuevo)
/// </summary>
public sealed record RefundUnidentifiedItemByOrderCommand(Guid ItemPublicId, Guid BeneficiaryPersonPublicId, decimal? Amount = null, Guid? PersonBankAccountPublicId = null,
    string Form = "Transfer", string? Note = null) : IRequest<Result<UnidentifiedItemDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RefundUnidentifiedItemByOrderCommandValidator : AbstractValidator<RefundUnidentifiedItemByOrderCommand>
{
    public RefundUnidentifiedItemByOrderCommandValidator()
    {
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.Form).Must(f => f is "Transfer" or "Check").WithMessage("La devolución se gira por transferencia (Transfer) o cheque (Check).");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RefundUnidentifiedItemByOrderCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IOrdenesDePagoDeTesoreria ordenes,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RefundUnidentifiedItemByOrderCommand, Result<UnidentifiedItemDto>>
{
    public async Task<Result<UnidentifiedItemDto>> Handle(RefundUnidentifiedItemByOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await PartidaVisible.LeerAsync(db, alcance, request.ItemPublicId, ct);
        if (leida.IsFailure) return Result.Failure<UnidentifiedItemDto>(leida.Error);
        if (!await db.People.AsNoTracking().AnyAsync(p => p.PublicId == request.BeneficiaryPersonPublicId, ct))
            return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El consignante"));
        await cerrojo.TomarSetupAsync(ct: ct);
        var partida = await PartidaVisible.BloquearAsync(db, cerrojo, leida.Value, ct);
        var valor = request.Amount ?? partida.Balance;
        if (valor > partida.Balance) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.PartidaSinSaldo(partida.Balance));
        var hoy = reloj.HoyLocal;
        var orden = await ordenes.EmitirAsync(new SolicitudDeOrdenDePago(request.OperationKey == Guid.Empty ? Guid.NewGuid() : request.OperationKey,
            ClasesDeOrdenDeRecaudo.DevolucionDePartida, request.BeneficiaryPersonPublicId, request.PersonBankAccountPublicId, request.Form, valor, hoy,
            string.Create(CultureInfo.InvariantCulture, $"Devolución de la partida {partida.Number}"), partida.PublicId), ct);
        var r = PartidasPorIdentificar.Resolver(db, partida, UnidentifiedItemResolutionKind.RefundedByOrder, valor, orden.PaymentOrderPublicId, orden.Number, null, null,
            usuario, reloj.UtcNow, request.Note);
        await db.SaveChangesAsync(ct);
        await emision.PartidaDevueltaAlConsignanteAsync(partida, r, request.BeneficiaryPersonPublicId, hoy, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.RefundedByOrder", "UnidentifiedItem", partida.PublicId, null,
            new { order = orden.Number, amount = valor, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PartidasPorIdentificar.DtoAsync(db, partida, hoy, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/unidentified-items/{id}/move-to-income</c> (<c>Receipts.UnidentifiedItems.MoveToIncome</c>): lleva a ingreso una partida vencida —pasado el
/// último escalón de <c>Partidas.EscalonesDias</c>— cuando la política vigente (<c>Partidas.DestinoVencido</c>) lo admite: una decisión registrada con motivo y la
/// aprobación de otra persona (sujeto <c>UnidentifiedItemToIncome</c>), que conserva el derecho del dueño a reclamar; nunca es automática (FR-073). (nuevo)
/// </summary>
public sealed record MoveUnidentifiedItemToIncomeCommand(Guid ItemPublicId, Guid MiscConceptPublicId, string Reason)
    : IRequest<Result<UnidentifiedItemDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    [JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class MoveUnidentifiedItemToIncomeCommandValidator : AbstractValidator<MoveUnidentifiedItemToIncomeCommand>
{
    public MoveUnidentifiedItemToIncomeCommandValidator()
    {
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.MiscConceptPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class MoveUnidentifiedItemToIncomeCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    ParametrosDeCaja parametros,
    AprobacionPresenteEnCaja aprobacion,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<MoveUnidentifiedItemToIncomeCommand, Result<UnidentifiedItemDto>>
{
    /// <summary>El valor de <c>Partidas.DestinoVencido</c> que deja las partidas como pasivo (el defecto, D10): no admite llevarlas a ingreso.</summary>
    public const string SiguenComoPasivo = "SiguenComoPasivo";

    public async Task<Result<UnidentifiedItemDto>> Handle(MoveUnidentifiedItemToIncomeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await PartidaVisible.LeerAsync(db, alcance, request.ItemPublicId, ct);
        if (leida.IsFailure) return Result.Failure<UnidentifiedItemDto>(leida.Error);
        var hoy = reloj.HoyLocal;
        var destino = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.PartidasDestinoVencido, hoy, ct);
        if (string.IsNullOrWhiteSpace(destino) || destino == SiguenComoPasivo)
            return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.DestinoVencidoNoAdmiteIngreso(string.IsNullOrWhiteSpace(destino) ? SiguenComoPasivo : destino));
        var plazo = (await Tareas.AntiguedadDeRecaudo.EscalonesAsync(parametros, hoy, ct)).Max();
        var dias = hoy.DayNumber - leida.Value.OpenedOn.DayNumber;
        if (dias < plazo) return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.PartidaNoVencida(dias, plazo));
        var concepto = await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.MiscConceptPublicId, ct);
        if (concepto is null || concepto.Status != ReceiptsCatalogStatus.Active || concepto.Direction != MiscConceptDirection.Income)
            return Result.Failure<UnidentifiedItemDto>(ErroresDeRecaudo.ConceptoDeClasificacionInvalido(concepto?.Code ?? request.MiscConceptPublicId.ToString()));
        var huella = HuellaDeRecibo.Calcular(new { leida.Value.PublicId, concepto = concepto.Code, leida.Value.Balance, accion = "ingreso" });
        var aprobada = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(ApprovalSubjects.UnidentifiedItemToIncome, leida.Value.PublicId,
            $"Llevar a ingreso la partida {leida.Value.Number}", leida.Value.Balance, hoy, huella, null, [usuario], request.ApprovalRequestPublicId), ct);
        if (aprobada.IsFailure) return Result.Failure<UnidentifiedItemDto>(aprobada.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        var partida = await PartidaVisible.BloquearAsync(db, cerrojo, leida.Value, ct);
        var valor = partida.Balance;
        var r = PartidasPorIdentificar.Resolver(db, partida, UnidentifiedItemResolutionKind.MovedToIncome, valor, Guid.NewGuid(), partida.Number, concepto.Id,
            aprobada.Value.ApprovalRequestPublicId, usuario, reloj.UtcNow, request.Reason);
        r.DocumentPublicId = r.PublicId;
        await db.SaveChangesAsync(ct);
        await emision.PartidaClasificadaAsync(r.PublicId, partida.Number, true, "MovedToIncome", concepto.Code, valor, hoy, partida.BranchId, partida.OwnBankAccountPublicId,
            null, aprobada.Value.ApprovalRequestPublicId, ct);
        await auditoria.AgregarAsync("Receipts.UnidentifiedItem.MovedToIncome", "UnidentifiedItem", partida.PublicId, null,
            new { concept = concepto.Code, amount = valor, policy = destino, ageDays = dias, reason = request.Reason, approval = aprobada.Value.ApprovalRequestPublicId,
                by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PartidasPorIdentificar.DtoAsync(db, partida, hoy, ct));
    }
}
