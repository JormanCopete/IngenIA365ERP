using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Voidings;

// Feature 015, US5 (T0175; FR-057, FR-058, FR-064, FR-194; D4, D5; contracts/api.md §4): la anulación del recibo entero —un documento numerado con motivo que
// aprueba otra persona y reversa cada aplicación vigente por su contrato (obligaciones antes que cuentas), todo o nada—. El dinero sigue a la sesión: abierta, baja lo
// esperado del efectivo y el cheque; cerrada (o un medio electrónico ya abonado), nace un saldo a favor del pagador que se devuelve por orden de pago. (nuevo)

/// <summary>Una anulación para la pantalla. (nuevo)</summary>
public sealed record ReceiptVoidingDto(
    Guid PublicId,
    string? Number,
    Guid ReceiptPublicId,
    string ReceiptNumber,
    decimal Amount,
    string Reason,
    ReceiptVoidingState Status,
    bool InSameSession,
    string RequestedByName,
    DateTime RequestedAt,
    string? ApprovedByName,
    DateTime? DecidedAt,
    string? DecisionReason,
    Guid? ApprovalRequestPublicId,
    IReadOnlyList<string> OmittedApplications,
    Guid? PayerCreditBalancePublicId,
    string VerificationCode);

/// <summary>Lo común de las anulaciones. (nuevo)</summary>
public static class AnulacionesDeRecibos
{
    public const string Huella = "Recaudo.Anulacion";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string HuellaDe(Receipt recibo, string motivo) => HuellaDeOperacion.Calcular(Huella, new { recibo.PublicId, recibo.Number, recibo.TotalApplied, motivo });

    public static async Task<ReceiptVoidingDto> DtoAsync(IApplicationDbContext db, ReceiptVoiding a, CancellationToken ct)
    {
        var recibo = await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == a.ReceiptId).Select(r => new { r.PublicId, r.Number, r.TotalApplied }).FirstAsync(ct);
        var pidio = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == a.RequestedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        var aprobo = a.ApprovedByUserId is { } ap
            ? await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == ap).Select(u => u.Username).FirstOrDefaultAsync(ct)
            : null;
        var saldo = a.PayerCreditBalanceId is { } s
            ? await db.PayerCreditBalances.AsNoTracking().Where(x => x.Id == s).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var omitidas = string.IsNullOrWhiteSpace(a.OmittedApplicationsJson) ? [] : JsonSerializer.Deserialize<List<string>>(a.OmittedApplicationsJson, Json) ?? [];
        return new ReceiptVoidingDto(a.PublicId, a.Number, recibo.PublicId, recibo.Number, recibo.TotalApplied, a.Reason, a.Status, a.InSameSession, pidio ?? string.Empty,
            a.RequestedAt, aprobo, a.DecidedAt, a.DecisionReason, a.ApprovalRequestPublicId, omitidas, saldo, a.VerificationCode);
    }

    public static string Omitidas(IEnumerable<string> productos) => JsonSerializer.Serialize(productos, Json);
}

/// <summary>
/// <c>POST /api/receipts/receipts/{id}/voiding</c> (<c>Receipts.Voiding.Request</c>): pide la anulación del recibo entero con motivo. El recibo queda
/// <c>VoidPendingApproval</c> y se pide la aprobación (regla fija de un nivel, <c>Receipts.Voiding.Approve</c> con monto): en la sesión abierta y el mismo día la
/// aprueba otra persona presente; después, otra persona con el permiso. El cajero del recibo no aprueba (participante). (nuevo)
/// </summary>
public sealed record RequestReceiptVoidingCommand(Guid ReceiptPublicId, string Reason) : IRequest<Result<ReceiptVoidingDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestReceiptVoidingCommandValidator : AbstractValidator<RequestReceiptVoidingCommand>
{
    public RequestReceiptVoidingCommandValidator()
    {
        RuleFor(x => x.ReceiptPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo de la anulación.").MaximumLength(500);
    }
}

public sealed class RequestReceiptVoidingCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IMotorDeAprobaciones motor,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RequestReceiptVoidingCommand, Result<ReceiptVoidingDto>>
{
    public async Task<Result<ReceiptVoidingDto>> Handle(RequestReceiptVoidingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await RecibosDeCaja.VisibleAsync(db, alcance, request.ReceiptPublicId, ct) is not { } visto)
            return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("El recibo"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([visto.PayerPersonId], ct);
        var recibo = await db.IssuedReceipts.FirstAsync(r => r.Id == visto.Id, ct);
        if (recibo.Status != ReceiptState.Valid || await db.ReceiptVoidings.AnyAsync(v => v.ReceiptId == recibo.Id, ct))
            return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.AnulacionYaHecha(recibo.Number));
        var hoy = reloj.HoyLocal;
        var sesion = recibo.CashSessionId is { } s ? await db.ReceiptsCashSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s, ct) : null;
        var mismaSesion = sesion?.Status == CashSessionState.Open && DateOnly.FromDateTime(recibo.RegisteredAt.ToLocalTime()) == hoy;
        var motivo = request.Reason.Trim();
        var huella = AnulacionesDeRecibos.HuellaDe(recibo, motivo);
        var anulacion = new ReceiptVoiding
        {
            ReceiptId = recibo.Id, Reason = motivo, RequestedByUserId = usuario, RequestedAt = reloj.UtcNow, InSameSession = mismaSesion,
            VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        var participantes = recibo.CashierUserId is { } cajero && cajero != usuario ? new[] { cajero } : Array.Empty<int>();
        var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(ApprovalSubjects.ReceiptVoiding, ApprovalSourceTypes.ReceiptsOperation, anulacion.PublicId,
            $"Anulación del recibo {recibo.Number}", null, null, null, recibo.TotalApplied, hoy, usuario, participantes, huella, AprobarAnulacion.Permiso), ct);
        if (solicitada.IsFailure) return Result.Failure<ReceiptVoidingDto>(solicitada.Error);
        anulacion.ApprovalRequestPublicId = solicitada.Value?.PublicId;
        db.ReceiptVoidings.Add(anulacion);
        recibo.Status = ReceiptState.VoidPendingApproval;
        await auditoria.AgregarAsync("Receipts.Voiding.Requested", "ReceiptVoiding", anulacion.PublicId, null,
            new { receipt = recibo.Number, reason = motivo, inSameSession = mismaSesion, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await AnulacionesDeRecibos.DtoAsync(db, anulacion, ct));
    }
}

/// <summary>Lo común de aprobar y aplicar una anulación. (nuevo)</summary>
public static class AprobarAnulacion
{
    public const string Permiso = "Receipts.Voiding.Approve";
}

/// <summary>
/// <c>POST /api/receipts/voidings/{id}/approve</c> (<c>Receipts.Voiding.Approve</c> con monto): otra persona aprueba la anulación (<c>Receipts.Voiding.SelfApproval</c>
/// si es quien la pidió) y se aplica en la misma transacción: numeración, reverso de cada porción vigente por su contrato con su clave <c>:reverso</c> —obligaciones
/// antes que cuentas, una vez por porción; un rechazo no deja nada (<c>Receipts.Voiding.ContractRejected</c>)—, las aplicaciones que el módulo ya había reversado se
/// omiten, el dinero sigue a la sesión y nace el hecho <c>ReciboAnulado</c> espejo. (nuevo)
/// </summary>
public sealed record ApproveReceiptVoidingCommand(Guid VoidingPublicId, string? Reason) : IRequest<Result<ReceiptVoidingDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveReceiptVoidingCommandValidator : AbstractValidator<ApproveReceiptVoidingCommand>
{
    public ApproveReceiptVoidingCommandValidator()
    {
        RuleFor(x => x.VoidingPublicId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class ApproveReceiptVoidingCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    IMotorDeAprobaciones motor,
    PersonasDeLosUsuarios personas,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj,
    Compliance.RegistroDeEfectivo? registroDeEfectivo = null) : IRequestHandler<ApproveReceiptVoidingCommand, Result<ReceiptVoidingDto>>
{
    private readonly AplicacionDeAnulacion _aplicacion = new(db, numerador, obligaciones, cuentas, emision, reloj, registroDeEfectivo);

    public async Task<Result<ReceiptVoidingDto>> Handle(ApproveReceiptVoidingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await db.ReceiptVoidings.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoidingPublicId, ct);
        if (leida is null) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        var reciboLeido = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == leida.ReceiptId, ct);
        if (await RecibosDeCaja.VisibleAsync(db, alcance, reciboLeido.PublicId, ct) is null) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        if (leida.Status != ReceiptVoidingState.PendingApproval) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.AnulacionNoPendiente(leida.Status.ToString()));
        if (await personas.MismaPersonaAsync(leida.RequestedByUserId, usuario, ct)) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.AnulacionAutoaprobada());

        // Cerrojos en su orden: configuración, día del punto y sesión del recibo, pagador.
        var sesion = reciboLeido.CashSessionId is { } sid ? await db.ReceiptsCashSessions.FirstOrDefaultAsync(x => x.Id == sid, ct) : null;
        await cerrojo.TomarSetupAsync(ct: ct);
        if (sesion is not null)
        {
            await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
            await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        }
        await cerrojo.BloquearPagadoresAsync([reciboLeido.PayerPersonId], ct);
        var anulacion = await db.ReceiptVoidings.FirstAsync(v => v.Id == leida.Id, ct);
        var recibo = await db.IssuedReceipts.FirstAsync(r => r.Id == leida.ReceiptId, ct);
        if (anulacion.Status != ReceiptVoidingState.PendingApproval) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.AnulacionNoPendiente(anulacion.Status.ToString()));

        if (anulacion.ApprovalRequestPublicId is { } solicitud)
        {
            var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, ApprovalDecisionKind.Approve, request.Reason,
                AnulacionesDeRecibos.HuellaDe(recibo, anulacion.Reason)), ct);
            if (decidida.IsFailure) return Result.Failure<ReceiptVoidingDto>(decidida.Error);
            if (decidida.Value.Status != nameof(ApprovalRequestStatus.Approved))
            {
                await db.SaveChangesAsync(ct);
                return Result.Success(await AnulacionesDeRecibos.DtoAsync(db, anulacion, ct));
            }
        }

        var r = await _aplicacion.AplicarAsync(anulacion, recibo, sesion, usuario, request.Reason, DineroDeLaAnulacion.SigueLaSesion, ct);
        if (r.IsFailure) return Result.Failure<ReceiptVoidingDto>(r.Error);
        await auditoria.AgregarAsync("Receipts.Voiding.Applied", "ReceiptVoiding", anulacion.PublicId, null,
            new { receipt = recibo.Number, number = anulacion.Number, omitted = anulacion.OmittedApplicationsJson, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await AnulacionesDeRecibos.DtoAsync(db, anulacion, ct));
    }
}

/// <summary><c>POST /api/receipts/voidings/{id}/reject</c> (<c>Receipts.Voiding.Approve</c>): la anulación se rechaza con motivo y el recibo vuelve a <c>Valid</c>. (nuevo)</summary>
public sealed record RejectReceiptVoidingCommand(Guid VoidingPublicId, string Reason) : IRequest<Result<ReceiptVoidingDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RejectReceiptVoidingCommandValidator : AbstractValidator<RejectReceiptVoidingCommand>
{
    public RejectReceiptVoidingCommandValidator()
    {
        RuleFor(x => x.VoidingPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del rechazo.").MaximumLength(500);
    }
}

public sealed class RejectReceiptVoidingCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IMotorDeAprobaciones motor,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RejectReceiptVoidingCommand, Result<ReceiptVoidingDto>>
{
    public async Task<Result<ReceiptVoidingDto>> Handle(RejectReceiptVoidingCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leida = await db.ReceiptVoidings.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoidingPublicId, ct);
        if (leida is null) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        var reciboLeido = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == leida.ReceiptId, ct);
        if (await RecibosDeCaja.VisibleAsync(db, alcance, reciboLeido.PublicId, ct) is null) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([reciboLeido.PayerPersonId], ct);
        var anulacion = await db.ReceiptVoidings.FirstAsync(v => v.Id == leida.Id, ct);
        var recibo = await db.IssuedReceipts.FirstAsync(r => r.Id == leida.ReceiptId, ct);
        if (anulacion.Status != ReceiptVoidingState.PendingApproval) return Result.Failure<ReceiptVoidingDto>(ErroresDeRecaudo.AnulacionNoPendiente(anulacion.Status.ToString()));
        if (anulacion.ApprovalRequestPublicId is { } solicitud)
        {
            var decidida = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud, ApprovalDecisionKind.Reject, request.Reason.Trim(),
                AnulacionesDeRecibos.HuellaDe(recibo, anulacion.Reason)), ct);
            if (decidida.IsFailure) return Result.Failure<ReceiptVoidingDto>(decidida.Error);
        }
        anulacion.Status = ReceiptVoidingState.Rejected;
        anulacion.ApprovedByUserId = usuario;
        anulacion.DecidedAt = reloj.UtcNow;
        anulacion.DecisionReason = request.Reason.Trim();
        recibo.Status = ReceiptState.Valid;
        await auditoria.AgregarAsync("Receipts.Voiding.Rejected", "ReceiptVoiding", anulacion.PublicId, null,
            new { receipt = recibo.Number, reason = anulacion.DecisionReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await AnulacionesDeRecibos.DtoAsync(db, anulacion, ct));
    }
}

/// <summary><c>GET /api/receipts/voidings?state=</c> (<c>Receipts.Voiding.Request</c> o <c>.Approve</c>, nuevo): las anulaciones de los recibos al alcance.</summary>
public sealed record ListReceiptVoidingsQuery(ReceiptVoidingState? State = null) : IRequest<Result<IReadOnlyList<ReceiptVoidingDto>>>;

public sealed class ListReceiptVoidingsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<ListReceiptVoidingsQuery, Result<IReadOnlyList<ReceiptVoidingDto>>>
{
    public async Task<Result<IReadOnlyList<ReceiptVoidingDto>>> Handle(ListReceiptVoidingsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = from v in db.ReceiptVoidings.AsNoTracking()
                       join r in db.IssuedReceipts.AsNoTracking() on v.ReceiptId equals r.Id
                       select new { Anulacion = v, r.BranchId, r.CashBoxId, r.CollectionPointId };
        if (request.State is { } e) consulta = consulta.Where(x => x.Anulacion.Status == e);
        var filas = (await consulta.OrderByDescending(x => x.Anulacion.Id).Take(200).ToListAsync(ct))
            .Where(x => mio.IncluyeOficina(x.BranchId) || (x.CashBoxId is { } c && x.CollectionPointId is { } p && mio.IncluyeCaja(c, p))).ToList();
        var lista = new List<ReceiptVoidingDto>(filas.Count);
        foreach (var f in filas) lista.Add(await AnulacionesDeRecibos.DtoAsync(db, f.Anulacion, ct));
        return Result.Success<IReadOnlyList<ReceiptVoidingDto>>(lista);
    }
}

/// <summary>Una línea del documento de anulación: la aplicación y su reverso (o la omisión). (nuevo)</summary>
public sealed record LineaDeAnulacionDto(int Ordinal, string Description, string? Product, decimal Amount, string Outcome);

/// <summary>El documento <c>Anulacion</c> (FR-156): número, código de verificación, recibo original, motivo, aprobador, reversos y omitidas. (nuevo)</summary>
public sealed record AnulacionModel(EmpresaDelDocumentoDto Company, string Number, string VerificationCode, string ReceiptNumber, DateOnly ReceiptValueDate,
    string Payer, decimal Amount, string Reason, string RequestedBy, string? ApprovedBy, DateTime? DecidedAt, bool SessionClosed,
    IReadOnlyList<LineaDeAnulacionDto> Lines);

/// <summary><c>GET /api/receipts/voidings/{id}/document</c> (<c>Receipts.Voiding.Request</c> o <c>.Approve</c>): el documento de una anulación aplicada. (nuevo)</summary>
public sealed record GetVoidingDocumentQuery(Guid VoidingPublicId) : IRequest<Result<AnulacionModel>>;

public sealed class GetVoidingDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IngenIA365ERP.Application.Common.Impresos.UsuarioEnImpresos? usuarios = null) : IRequestHandler<GetVoidingDocumentQuery, Result<AnulacionModel>>
{
    public async Task<Result<AnulacionModel>> Handle(GetVoidingDocumentQuery request, CancellationToken ct)
    {
        var a = await db.ReceiptVoidings.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoidingPublicId, ct);
        if (a is null || a.Status != ReceiptVoidingState.Applied) return Result.Failure<AnulacionModel>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        var r = await db.IssuedReceipts.AsNoTracking().FirstAsync(x => x.Id == a.ReceiptId, ct);
        if (await RecibosDeCaja.VisibleAsync(db, alcance, r.PublicId, ct) is null) return Result.Failure<AnulacionModel>(ErroresDeRecaudo.NoEncontrado("La anulación"));
        var dto = await AnulacionesDeRecibos.DtoAsync(db, a, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == r.PayerPersonId).Select(p => p.FirstName + " " + p.LastName).FirstAsync(ct);
        var aplicaciones = await db.ReceiptApplications.AsNoTracking().Where(x => x.ReceiptId == r.Id).OrderBy(x => x.Ordinal).ToListAsync(ct);
        var lineas = aplicaciones.Select(x => new LineaDeAnulacionDto(x.Ordinal, x.Target.ToString(), x.ProductNumber, x.Amount,
            x.Status == ApplicationState.ReversedByModule ? "Omitida: ya la había reversado el módulo" : "Reversada")).ToList();
        // 2026-11-15: quién pidió y quién aprobó, como lo pide la cooperativa en los impresos.
        var pidio = await SesionesDeCaja.NombreImpresoAsync(db, usuarios, a.RequestedByUserId, ct);
        var aprobo = a.ApprovedByUserId is { } ap ? await SesionesDeCaja.NombreImpresoAsync(db, usuarios, ap, ct) : null;
        return Result.Success(new AnulacionModel(await DocumentosDeCajaDeRecaudo.EmpresaAsync(db, ct), a.Number ?? string.Empty, a.VerificationCode, r.Number, r.ValueDate,
            pagador.Trim(), r.TotalApplied, a.Reason, pidio, aprobo, a.DecidedAt, a.PayerCreditBalanceId is not null, lineas));
    }
}
