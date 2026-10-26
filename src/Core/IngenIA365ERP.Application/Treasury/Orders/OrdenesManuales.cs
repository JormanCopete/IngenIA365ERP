using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Orders;

// Feature 015, T1 (fase 34, T0417; FR-211, FR-012; US26-3, US26-4; contracts/api.md §13): las órdenes de pago manuales de Tesorería. Sólo por conceptos de un catálogo
// cerrado, con soporte obligatorio, el tope del concepto, dos aprobadores distintos de quien prepara y beneficiario distinto de quien prepara o aprueba; si el concepto
// exige retención y la orden no la trae, se deriva a una causación previa en Contabilidad. Aprobada, la orden entra a la bandeja única con origen Manual y sigue el camino
// de todas (lote, archivo, resultado).

/// <summary>Un concepto del catálogo cerrado de órdenes manuales. (nuevo)</summary>
public sealed record ManualOrderConceptDto(Guid PublicId, string Code, string Name, decimal MaxAmount, bool RequiresWithholding, string? WithholdingNote,
    TreasuryCatalogStatus Status);

/// <summary>Una orden manual con su aprobación. (nuevo)</summary>
public sealed record ManualPaymentOrderDto(
    Guid PublicId,
    string Number,
    Guid ConceptPublicId,
    string ConceptCode,
    Guid BeneficiaryPersonPublicId,
    string BeneficiaryName,
    PaymentForm Form,
    decimal GrossAmount,
    decimal WithholdingAmount,
    decimal NetAmount,
    string Concept,
    DateOnly PayDate,
    Guid SupportAttachmentPublicId,
    ManualOrderApprovalState ApprovalState,
    PaymentOrderState Status,
    string PreparedBy,
    string? FirstApprovedBy,
    string? SecondApprovedBy,
    string? RejectionReason);

/// <summary>Lo común de las órdenes manuales: el DTO y la lectura. (nuevo)</summary>
public static class OrdenesManuales
{
    public const string PermisoDeAprobar = "Treasury.ManualOrders.Approve";

    public static ManualOrderConceptDto Dto(ManualOrderConcept c) => new(c.PublicId, c.Code, c.Name, c.MaxAmount, c.RequiresWithholding, c.WithholdingNote, c.Status);

    public static async Task<ManualPaymentOrderDto> DtoAsync(IApplicationDbContext db, PaymentOrder o, CancellationToken ct)
    {
        var concepto = await db.ManualOrderConcepts.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == o.ManualOrderConceptId).Select(c => new { c.PublicId, c.Code })
            .FirstAsync(ct);
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == o.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var ids = new[] { o.PreparedByUserId, o.FirstApprovedByUserId, o.SecondApprovedByUserId }.OfType<int>().Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
        string? Nombre(int? id) => id is { } i ? usuarios.GetValueOrDefault(i) : null;
        return new ManualPaymentOrderDto(o.PublicId, o.Number, concepto.PublicId, concepto.Code, persona, o.BeneficiaryName, o.Form, o.GrossAmount, o.WithholdingAmount ?? 0m,
            o.NetAmount, o.Concept, o.PayDate, o.SupportAttachmentPublicId ?? Guid.Empty, o.ManualApprovalState ?? ManualOrderApprovalState.PendingFirstApproval, o.Status,
            Nombre(o.PreparedByUserId) ?? string.Empty, Nombre(o.FirstApprovedByUserId), Nombre(o.SecondApprovedByUserId), o.ManualRejectionReason);
    }
}

// ------------------------------------------------------------------------------------------------------------ el catálogo --

/// <summary><c>GET /api/treasury/manual-order-concepts?includeInactive=</c> (<c>Treasury.ManualOrders.Create</c> o <c>.Approve</c>). (nuevo)</summary>
public sealed record ListManualOrderConceptsQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<ManualOrderConceptDto>>>;

public sealed class ListManualOrderConceptsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListManualOrderConceptsQuery, Result<IReadOnlyList<ManualOrderConceptDto>>>
{
    public async Task<Result<IReadOnlyList<ManualOrderConceptDto>>> Handle(ListManualOrderConceptsQuery request, CancellationToken ct)
    {
        var consulta = db.ManualOrderConcepts.AsNoTracking();
        if (!request.IncludeInactive) consulta = consulta.Where(c => c.Status == TreasuryCatalogStatus.Active);
        var filas = await consulta.OrderBy(c => c.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<ManualOrderConceptDto>>(filas.Select(OrdenesManuales.Dto).ToList());
    }
}

/// <summary>
/// <c>POST /api/treasury/manual-order-concepts</c> (crea) y <c>PUT /{id}</c> (edita o inactiva) (<c>Treasury.ManualOrders.ManageConcepts</c>, nuevo; con clave): el concepto con
/// su código de la cooperativa (<c>CodigoDeCatalogo</c>), su tope y si exige retención. El código no se repite y no cambia. (nuevo)
/// </summary>
public sealed record SaveManualOrderConceptCommand(Guid? PublicId, string Code, string Name, decimal MaxAmount, bool RequiresWithholding, string? WithholdingNote = null,
    TreasuryCatalogStatus Status = TreasuryCatalogStatus.Active) : IRequest<Result<ManualOrderConceptDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveManualOrderConceptCommandValidator : AbstractValidator<SaveManualOrderConceptCommand>
{
    public SaveManualOrderConceptCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código.").MaximumLength(CodigoDeCatalogo.LargoCorto)
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Escriba el nombre del concepto.").MaximumLength(120);
        RuleFor(x => x.MaxAmount).GreaterThan(0m).WithMessage("El tope del concepto tiene que ser positivo.");
        RuleFor(x => x.WithholdingNote).MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class SaveManualOrderConceptCommandHandler(IApplicationDbContext db, AuditoriaDeTesoreria auditoria)
    : IRequestHandler<SaveManualOrderConceptCommand, Result<ManualOrderConceptDto>>
{
    public async Task<Result<ManualOrderConceptDto>> Handle(SaveManualOrderConceptCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        ManualOrderConcept concepto;
        if (request.PublicId is { } id)
        {
            var existente = await db.ManualOrderConcepts.FirstOrDefaultAsync(c => c.PublicId == id, ct);
            if (existente is null) return Result.Failure<ManualOrderConceptDto>(ErroresDeTesoreria.NoEncontrado("El concepto"));
            concepto = existente;
        }
        else
        {
            if (await db.ManualOrderConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo, ct) is { } repetido)
                return Result.Failure<ManualOrderConceptDto>(CodigoDeCatalogo.Duplicado("un concepto de orden manual", codigo, repetido.Name, repetido.PublicId));
            concepto = new ManualOrderConcept { Code = codigo };
            db.ManualOrderConcepts.Add(concepto);
        }
        var antes = request.PublicId is null ? null : OrdenesManuales.Dto(concepto);
        concepto.Name = request.Name.Trim();
        concepto.MaxAmount = request.MaxAmount;
        concepto.RequiresWithholding = request.RequiresWithholding;
        concepto.WithholdingNote = string.IsNullOrWhiteSpace(request.WithholdingNote) ? null : request.WithholdingNote.Trim();
        concepto.Status = request.Status;
        await auditoria.AgregarAsync("Treasury.ManualOrderConcept.Saved", "ManualOrderConcept", concepto.PublicId, antes, OrdenesManuales.Dto(concepto), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(OrdenesManuales.Dto(concepto));
    }
}

// --------------------------------------------------------------------------------------------------------------- preparar --

/// <summary>
/// <c>POST /api/treasury/manual-orders</c> (<c>Treasury.ManualOrders.Create</c>, con clave): prepara una orden manual por un concepto activo del catálogo, con su soporte, su
/// beneficiario (distinto de quien prepara), su forma, el bruto dentro del tope y la retención si el concepto la exige (si no la trae, se deriva a una causación previa en
/// Contabilidad). Queda esperando dos aprobaciones (<c>PendingFirstApproval</c>) y no entra a lotes hasta tenerlas. (nuevo)
/// </summary>
public sealed record CreateManualPaymentOrderCommand(
    Guid ConceptPublicId,
    Guid BeneficiaryPersonPublicId,
    PaymentForm Form,
    decimal GrossAmount,
    decimal WithholdingAmount,
    string Concept,
    Guid SupportAttachmentPublicId,
    Guid? PersonBankAccountPublicId = null,
    DateOnly? PayDate = null) : IRequest<Result<ManualPaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateManualPaymentOrderCommandValidator : AbstractValidator<CreateManualPaymentOrderCommand>
{
    public CreateManualPaymentOrderCommandValidator()
    {
        RuleFor(x => x.ConceptPublicId).NotEmpty().WithMessage("Elija el concepto del catálogo de órdenes manuales.");
        RuleFor(x => x.BeneficiaryPersonPublicId).NotEmpty().WithMessage("Indique el beneficiario.");
        RuleFor(x => x.Form).IsInEnum();
        RuleFor(x => x.GrossAmount).GreaterThan(0m).WithMessage("El valor de la orden tiene que ser positivo.");
        RuleFor(x => x.WithholdingAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Concept).NotEmpty().WithMessage("Escriba el concepto del pago.").MaximumLength(120);
        RuleFor(x => x.SupportAttachmentPublicId).NotEmpty().WithMessage("Adjunte el soporte de la orden manual (factura, cuenta de cobro o acta).");
        RuleFor(x => x.PersonBankAccountPublicId).NotEmpty().When(x => x.Form != PaymentForm.Check)
            .WithMessage("Elija la cuenta registrada del beneficiario a la que se gira.");
    }
}

public sealed class CreateManualPaymentOrderCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    PersonasDeLosUsuarios personas,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ParametrosDeTesoreria parametros,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<CreateManualPaymentOrderCommand, Result<ManualPaymentOrderDto>>
{
    public async Task<Result<ManualPaymentOrderDto>> Handle(CreateManualPaymentOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var concepto = await db.ManualOrderConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.ConceptPublicId, ct);
        if (concepto is null) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("El concepto"));
        if (concepto.Status != TreasuryCatalogStatus.Active) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualConceptoInactivo());
        if (request.GrossAmount > concepto.MaxAmount) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualSobreElTope(concepto.MaxAmount));
        if (request.WithholdingAmount >= request.GrossAmount) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualRetencionInvalida());
        if (concepto.RequiresWithholding && request.WithholdingAmount <= 0m)
            return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualDerivarACausacion(concepto.WithholdingNote));

        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == request.BeneficiaryPersonPublicId)
            .Select(p => new { p.Id, p.FirstName, p.LastName, p.TaxId, p.IdType }).FirstOrDefaultAsync(ct);
        if (persona is null) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("El beneficiario"));
        if (await personas.PersonaDeAsync(usuario, ct) == persona.Id) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualBeneficiarioPrepara());
        var cuenta = request.PersonBankAccountPublicId is { } c
            ? await (from a in db.PersonBankAccounts.AsNoTracking()
                     join b in db.Banks.AsNoTracking() on a.BankId equals b.Id
                     where a.PublicId == c && a.PersonId == persona.Id
                     select new { a.Id, a.AccountNumber, a.AccountType, b.TransferCode, BankId = b.Id }).FirstOrDefaultAsync(ct)
            : null;
        if (request.Form != PaymentForm.Check && cuenta is null) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La cuenta del beneficiario"));

        await cerrojo.TomarSetupAsync(ct: ct);
        var hoy = reloj.HoyLocal;
        var neto = request.GrossAmount - request.WithholdingAmount;
        var orden = new PaymentOrder
        {
            Origin = PaymentOrderOrigin.Manual, OrderKind = concepto.Code, Form = request.Form, BeneficiaryPersonId = persona.Id,
            BeneficiaryName = Recortar($"{persona.FirstName} {persona.LastName}", 200), BeneficiaryTaxIdType = persona.IdType, BeneficiaryTaxId = Recortar(persona.TaxId ?? string.Empty, 20),
            DestinationBankCode = cuenta?.TransferCode, DestinationBankId = cuenta?.BankId, DestinationAccountNumber = cuenta?.AccountNumber,
            DestinationAccountType = cuenta?.AccountType, PersonBankAccountId = cuenta?.Id, GrossAmount = request.GrossAmount, NetAmount = neto,
            PayDate = request.PayDate ?? hoy, Concept = Recortar(request.Concept, 120), ReceivedAt = reloj.UtcNow, Status = PaymentOrderState.Received,
            ManualOrderConceptId = concepto.Id, SupportAttachmentPublicId = request.SupportAttachmentPublicId, WithholdingAmount = request.WithholdingAmount,
            PreparedByUserId = usuario, ManualApprovalState = ManualOrderApprovalState.PendingFirstApproval,
            PendingReasonsJson = $"[\"{ErroresDeTesoreria.CodigoOrdenManualSinAprobar}\"]",
        };
        orden.OriginOrderPublicId = orden.PublicId;
        var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, false, false, false),
            await parametros.GmfAsync(orden.PayDate, ct));
        orden.GmfExempt = gmf.Exento;
        orden.GmfExemptionReason = gmf.Evaluado ? gmf.Motivo : null;
        orden.GmfEstimate = gmf.Gravado ? gmf.Gmf : 0m;
        await numerador.NumerarAsync(orden, Integration.ConsumePaymentOrderCommandHandler.CodigoDeOrden, Integration.ConsumePaymentOrderCommandHandler.CodigoDeOrden, ct);
        orden.OriginOrderNumber = orden.Number;
        db.PaymentOrders.Add(orden);
        await auditoria.AgregarAsync("Treasury.ManualOrder.Prepared", "PaymentOrder", orden.PublicId, null,
            new { number = orden.Number, concept = concepto.Code, gross = orden.GrossAmount, withholding = orden.WithholdingAmount, net = orden.NetAmount, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesManuales.DtoAsync(db, orden, ct));
    }

    private static string Recortar(string texto, int largo)
    {
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}

// -------------------------------------------------------------------------------------------------------- aprobar y rechazar --

/// <summary>
/// <c>POST /api/treasury/manual-orders/{id}/approve</c> (<c>Treasury.ManualOrders.Approve</c>, con clave): da la primera o la segunda aprobación. Ninguna la da quien preparó
/// ni el beneficiario, y la segunda la da otra persona que la primera. Con la segunda, la orden queda aprobada y se valida como cualquiera de la bandeja (cuenta registrada,
/// listas, marcas): lista para un lote o pendiente con sus motivos. (nuevo)
/// </summary>
public sealed record ApproveManualPaymentOrderCommand(Guid PaymentOrderPublicId) : IRequest<Result<ManualPaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveManualPaymentOrderCommandValidator : AbstractValidator<ApproveManualPaymentOrderCommand>
{
    public ApproveManualPaymentOrderCommandValidator() => RuleFor(x => x.PaymentOrderPublicId).NotEmpty();
}

public sealed class ApproveManualPaymentOrderCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    PersonasDeLosUsuarios personas,
    ICerrojoDeTesoreria cerrojo,
    ValidadorDeOrdenes validador,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ApproveManualPaymentOrderCommand, Result<ManualPaymentOrderDto>>
{
    public async Task<Result<ManualPaymentOrderDto>> Handle(ApproveManualPaymentOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leida = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.PaymentOrderPublicId && o.Origin == PaymentOrderOrigin.Manual, ct);
        if (leida is null) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La orden manual"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [leida.Id], ct: ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == leida.Id, ct);
        if (orden.ManualApprovalState is not (ManualOrderApprovalState.PendingFirstApproval or ManualOrderApprovalState.PendingSecondApproval))
            return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualNoPendiente(orden.Number));
        if (orden.PreparedByUserId is { } preparo && await personas.MismaPersonaAsync(preparo, usuario, ct))
            return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualAutoaprobada());
        if (await personas.PersonaDeAsync(usuario, ct) == orden.BeneficiaryPersonId)
            return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualBeneficiarioAprueba());

        if (orden.ManualApprovalState == ManualOrderApprovalState.PendingFirstApproval)
        {
            orden.FirstApprovedByUserId = usuario;
            orden.FirstApprovedAt = reloj.UtcNow;
            orden.ManualApprovalState = ManualOrderApprovalState.PendingSecondApproval;
        }
        else
        {
            if (orden.FirstApprovedByUserId is { } primero && await personas.MismaPersonaAsync(primero, usuario, ct))
                return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualMismoAprobador());
            orden.SecondApprovedByUserId = usuario;
            orden.SecondApprovedAt = reloj.UtcNow;
            orden.ManualApprovalState = ManualOrderApprovalState.Approved;
            await validador.ValidarAsync(orden, ct);
        }
        await auditoria.AgregarAsync("Treasury.ManualOrder.Approved", "PaymentOrder", orden.PublicId, null,
            new { number = orden.Number, level = orden.ManualApprovalState == ManualOrderApprovalState.Approved ? 2 : 1, status = orden.Status.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesManuales.DtoAsync(db, orden, ct));
    }
}

/// <summary><c>POST /api/treasury/manual-orders/{id}/reject</c> (<c>Treasury.ManualOrders.Approve</c>, con clave): rechaza con motivo una orden manual que espera aprobación; queda anulada. (nuevo)</summary>
public sealed record RejectManualPaymentOrderCommand(Guid PaymentOrderPublicId, string Reason) : IRequest<Result<ManualPaymentOrderDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectManualPaymentOrderCommandValidator : AbstractValidator<RejectManualPaymentOrderCommand>
{
    public RejectManualPaymentOrderCommandValidator()
    {
        RuleFor(x => x.PaymentOrderPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del rechazo.").MaximumLength(500);
    }
}

public sealed class RejectManualPaymentOrderCommandHandler(IApplicationDbContext db, IActorActual actorActual, ICerrojoDeTesoreria cerrojo, AuditoriaDeTesoreria auditoria)
    : IRequestHandler<RejectManualPaymentOrderCommand, Result<ManualPaymentOrderDto>>
{
    public async Task<Result<ManualPaymentOrderDto>> Handle(RejectManualPaymentOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var leida = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.PaymentOrderPublicId && o.Origin == PaymentOrderOrigin.Manual, ct);
        if (leida is null) return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.NoEncontrado("La orden manual"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [leida.Id], ct: ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == leida.Id, ct);
        if (orden.ManualApprovalState is not (ManualOrderApprovalState.PendingFirstApproval or ManualOrderApprovalState.PendingSecondApproval))
            return Result.Failure<ManualPaymentOrderDto>(ErroresDeTesoreria.OrdenManualNoPendiente(orden.Number));
        orden.ManualApprovalState = ManualOrderApprovalState.Rejected;
        orden.ManualRejectionReason = request.Reason.Trim();
        orden.Status = PaymentOrderState.Voided;
        orden.VoidReason = orden.ManualRejectionReason;
        await auditoria.AgregarAsync("Treasury.ManualOrder.Rejected", "PaymentOrder", orden.PublicId, null,
            new { number = orden.Number, reason = orden.ManualRejectionReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await OrdenesManuales.DtoAsync(db, orden, ct));
    }
}

/// <summary><c>GET /api/treasury/manual-orders?state=</c> (<c>Treasury.ManualOrders.Create</c> o <c>.Approve</c>): las órdenes manuales con su aprobación. (nuevo)</summary>
public sealed record ListManualPaymentOrdersQuery(ManualOrderApprovalState? State = null) : IRequest<Result<IReadOnlyList<ManualPaymentOrderDto>>>;

public sealed class ListManualPaymentOrdersQueryHandler(IApplicationDbContext db) : IRequestHandler<ListManualPaymentOrdersQuery, Result<IReadOnlyList<ManualPaymentOrderDto>>>
{
    public async Task<Result<IReadOnlyList<ManualPaymentOrderDto>>> Handle(ListManualPaymentOrdersQuery request, CancellationToken ct)
    {
        var consulta = db.PaymentOrders.AsNoTracking().Where(o => o.Origin == PaymentOrderOrigin.Manual);
        if (request.State is { } s) consulta = consulta.Where(o => o.ManualApprovalState == s);
        var filas = await consulta.OrderByDescending(o => o.Id).Take(300).ToListAsync(ct);
        var lista = new List<ManualPaymentOrderDto>(filas.Count);
        foreach (var o in filas) lista.Add(await OrdenesManuales.DtoAsync(db, o, ct));
        return Result.Success<IReadOnlyList<ManualPaymentOrderDto>>(lista);
    }
}
