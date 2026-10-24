using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Checks;

/// <summary>Un cheque recibido, para la pantalla. (nuevo)</summary>
public sealed record ReceivedCheckDto(Guid PublicId, Guid DraweeBankPublicId, string DraweeBankName, string CheckNumber, string AccountNumber, Guid? DrawerPersonPublicId,
    string? DrawerName, Guid PayerPersonPublicId, string PayerName, DateOnly IssueDate, decimal Amount, bool IsThirdParty, bool SamePlace, DateOnly PresentationDeadline,
    bool PastDeadline, ReceivedCheckState Status, string? ReceiptNumber, string? ReturnCauseCode, string? PayerReceivableNumber, DateTime CreatedAt);

/// <summary>Un girador con cheques devueltos (FR-086: advierte al cajero). (nuevo)</summary>
public sealed record DrawerWithReturnsDto(Guid? DrawerPersonPublicId, string? DrawerName, string AccountNumber, int Returned, DateOnly LastReturnedOn);

/// <summary>Una causal de devolución del catálogo. (nuevo)</summary>
public sealed record CheckReturnCauseDto(string Code, string Description, bool CountsForDrawerList);

/// <summary>Lo común de los cheques recibidos (feature 015, US15; FR-083 a FR-087): la validación del cheque en caja, el plazo de presentación, la transición con su evento y el DTO. (nuevo)</summary>
public static class ChequesRecibidos
{
    /// <summary>Lo que la validación de un cheque en caja dice: el plazo y si ya pasó (sólo avisa, salvo la bandera).</summary>
    public sealed record Validacion(int DraweeBankId, int? DrawerPersonId, bool IsThirdParty, DateOnly PresentationDeadline, bool PastDeadline);

    /// <summary>
    /// Valida un cheque recibido como medio (FR-083, FR-084, FR-086; D21, D37; FR-010): la caja lo admite (<c>Caja.AdmiteCheque</c>), banco, número, cuenta y fecha; uno de
    /// tercero sólo con <c>Caja.AdmiteChequeDeTercero</c>; un posfechado no paga un recibo (va en custodia, si se admite); el plazo de presentación por plaza sólo avisa
    /// salvo <c>Cheques.BloquearFueraDePlazo</c>; el mismo cheque no entra dos veces.
    /// </summary>
    public static async Task<Result<Validacion>> ValidarAsync(IApplicationDbContext db, ParametrosDeCaja parametros, ChequeDeCajaDto? cheque, decimal valor, int pagador,
        DateOnly hoy, bool enCustodia, CancellationToken ct)
    {
        if (!await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmiteCheque, hoy, ct)) return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeApagado());
        if (cheque is null || string.IsNullOrWhiteSpace(cheque.CheckNumber) || string.IsNullOrWhiteSpace(cheque.AccountNumber))
            return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeSinDatos());
        var banco = await db.Banks.AsNoTracking().Where(b => b.PublicId == cheque.DraweeBankPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (banco is null) return Result.Failure<Validacion>(ErroresDeRecaudo.NoEncontrado("El banco librado"));
        int? girador = null;
        if (cheque.DrawerPersonPublicId is { } g)
        {
            girador = await db.People.AsNoTracking().Where(p => p.PublicId == g).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (girador is null) return Result.Failure<Validacion>(ErroresDeRecaudo.NoEncontrado("El girador"));
        }
        var deTercero = girador is { } gi ? gi != pagador : !string.IsNullOrWhiteSpace(cheque.DrawerName);
        if (deTercero && !await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmiteChequeDeTercero, hoy, ct))
            return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeDeTercero());
        if (cheque.IssueDate > hoy)
        {
            if (!enCustodia)
                return Result.Failure<Validacion>(ErroresDeRecaudo.ChequePosfechado(await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmitePosfechadoEnCustodia, hoy, ct)));
        }
        else if (enCustodia)
        {
            return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeNoPosfechado());
        }
        if (valor <= 0m) return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeSinDatos());
        var plazo = await PlazoAsync(parametros, cheque.IssueDate, cheque.SamePlace, hoy, ct);
        var vencido = hoy > plazo;
        if (vencido && await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.ChequesBloquearFueraDePlazo, hoy, ct))
            return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeFueraDePlazo(plazo));
        var numero = cheque.CheckNumber.Trim();
        var cuenta = cheque.AccountNumber.Trim();
        if (await db.ReceivedChecks.AsNoTracking().AnyAsync(c => c.DraweeBankId == banco.Value && c.AccountNumber == cuenta && c.CheckNumber == numero, ct))
            return Result.Failure<Validacion>(ErroresDeRecaudo.ChequeRepetido(numero));
        return Result.Success(new Validacion(banco.Value, girador, deTercero, plazo, vencido));
    }

    /// <summary>El último día del plazo de presentación (C. Co. art. 718): días si es de la misma plaza, meses si es de otra.</summary>
    public static async Task<DateOnly> PlazoAsync(ParametrosDeCaja parametros, DateOnly emision, bool mismaPlaza, DateOnly hoy, CancellationToken ct) =>
        mismaPlaza
            ? emision.AddDays(await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ChequesPlazoPresentacionMismaPlaza, hoy, ct) ?? 15)
            : emision.AddMonths(await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ChequesPlazoPresentacionOtraPlaza, hoy, ct) ?? 1);

    /// <summary>La fila del cheque recibido con lo validado. Sin guardar.</summary>
    public static ReceivedCheck Nuevo(ChequeDeCajaDto c, Validacion v, decimal valor, int pagador, int oficina, ReceivedCheckState estado) => new()
    {
        DraweeBankId = v.DraweeBankId, CheckNumber = c.CheckNumber.Trim(), AccountNumber = c.AccountNumber.Trim(), DrawerPersonId = v.DrawerPersonId,
        DrawerName = string.IsNullOrWhiteSpace(c.DrawerName) ? null : c.DrawerName.Trim(), PayerPersonId = pagador, IssueDate = c.IssueDate, Amount = valor,
        IsThirdParty = v.IsThirdParty, SamePlace = c.SamePlace, PresentationDeadline = v.PresentationDeadline, BranchId = oficina, Status = estado,
    };

    /// <summary>Cambia el estado del cheque y anota el evento. Sin guardar.</summary>
    public static void Transicion(IApplicationDbContext db, ReceivedCheck c, ReceivedCheckState hacia, int usuario, DateTime ahora, string? causa = null, Guid? soporte = null,
        string? nota = null)
    {
        db.ReceivedCheckEvents.Add(new ReceivedCheckEvent
        {
            ReceivedCheckId = c.Id, FromStatus = c.Status, ToStatus = hacia, ByUserId = usuario, At = ahora, CauseCode = causa, SupportAttachmentPublicId = soporte,
            Note = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(),
        });
        c.Status = hacia;
    }

    /// <summary>El cheque por su <c>PublicId</c> si su oficina está al alcance; fuera, el 404 genérico.</summary>
    public static async Task<ReceivedCheck?> VisibleAsync(IApplicationDbContext db, IAlcanceDeRecaudo alcance, Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.ReceivedChecks : db.ReceivedChecks.AsNoTracking();
        var c = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        return c is not null && (await alcance.ObtenerAsync(ct)).IncluyeOficina(c.BranchId) ? c : null;
    }

    public static async Task<ReceivedCheckDto> DtoAsync(IApplicationDbContext db, ReceivedCheck c, DateOnly hoy, CancellationToken ct)
    {
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == c.DraweeBankId).Select(b => new { b.PublicId, b.Name }).FirstAsync(ct);
        var ids = new[] { c.PayerPersonId, c.DrawerPersonId ?? 0 };
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.PublicId, Nombre = (p.BusinessName ?? (p.FirstName + " " + p.LastName)).Trim() }, ct);
        var recibo = c.ReceiptId is { } r ? await db.IssuedReceipts.AsNoTracking().Where(x => x.Id == r).Select(x => x.Number).FirstOrDefaultAsync(ct) : null;
        var cxc = c.PayerReceivableId is { } x2 ? await db.PayerReceivables.AsNoTracking().Where(x => x.Id == x2).Select(x => x.OriginDocumentNumber).FirstOrDefaultAsync(ct) : null;
        var girador = c.DrawerPersonId is { } d && personas.TryGetValue(d, out var pd) ? pd : null;
        return new ReceivedCheckDto(c.PublicId, banco.PublicId, banco.Name, c.CheckNumber, c.AccountNumber, girador?.PublicId, girador?.Nombre ?? c.DrawerName,
            personas[c.PayerPersonId].PublicId, personas[c.PayerPersonId].Nombre, c.IssueDate, c.Amount, c.IsThirdParty, c.SamePlace, c.PresentationDeadline,
            hoy > c.PresentationDeadline && c.Status is ReceivedCheckState.Received or ReceivedCheckState.InCustody, c.Status, recibo, c.ReturnCauseCode, cxc, c.CreatedAt);
    }
}

/// <summary>
/// <c>POST /api/receipts/received-checks/custody</c> (<c>Receipts.Checks.Manage</c>, con clave; FR-084): recibe en custodia un cheque posfechado sin recibo, si la cooperativa
/// lo admite (<c>Caja.AdmitePosfechadoEnCustodia</c>). No paga nada: cuando llegue su fecha se registra como medio de un recibo. (nuevo)
/// </summary>
public sealed record RegisterCheckInCustodyCommand(Guid PayerPersonPublicId, Guid BranchPublicId, decimal Amount, ChequeDeCajaDto Check, string? Note = null)
    : IRequest<Result<ReceivedCheckDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterCheckInCustodyCommandValidator : AbstractValidator<RegisterCheckInCustodyCommand>
{
    public RegisterCheckInCustodyCommandValidator()
    {
        RuleFor(x => x.PayerPersonPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Check).NotNull();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RegisterCheckInCustodyCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ParametrosDeCaja parametros,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterCheckInCustodyCommand, Result<ReceivedCheckDto>>
{
    public async Task<Result<ReceivedCheckDto>> Handle(RegisterCheckInCustodyCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == request.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(oficina.Value)) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("La oficina"));
        var pagador = await db.People.AsNoTracking().Where(p => p.PublicId == request.PayerPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (pagador is null) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El pagador"));
        if (!await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAdmitePosfechadoEnCustodia, hoy, ct))
            return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.ChequePosfechado(false));
        var v = await ChequesRecibidos.ValidarAsync(db, parametros, request.Check, request.Amount, pagador.Value, hoy, enCustodia: true, ct);
        if (v.IsFailure) return Result.Failure<ReceivedCheckDto>(v.Error);
        var cheque = ChequesRecibidos.Nuevo(request.Check, v.Value, request.Amount, pagador.Value, oficina.Value, ReceivedCheckState.InCustody);
        db.ReceivedChecks.Add(cheque);
        await db.SaveChangesAsync(ct);
        db.ReceivedCheckEvents.Add(new ReceivedCheckEvent
        {
            ReceivedCheckId = cheque.Id, FromStatus = ReceivedCheckState.InCustody, ToStatus = ReceivedCheckState.InCustody, ByUserId = usuario, At = reloj.UtcNow, Note = request.Note,
        });
        await auditoria.AgregarAsync("Receipts.Check.InCustody", "ReceivedCheck", cheque.PublicId, null,
            new { check = cheque.CheckNumber, amount = cheque.Amount, issueDate = cheque.IssueDate, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequesRecibidos.DtoAsync(db, cheque, hoy, ct));
    }
}

/// <summary>
/// Las transiciones manuales del cheque (FR-084): <c>…/deposit</c> (recibido → consignado), <c>…/clearing</c> (consignado → en canje) y <c>…/paid</c> (consignado o en canje →
/// pagado), con <c>Receipts.Checks.Manage</c> y clave; cada una deja su evento. La firmeza la informan los módulos. (nuevo)
/// </summary>
public sealed record MarkReceivedCheckDepositedCommand(Guid CheckPublicId, Guid? RemittancePublicId = null, string? Note = null)
    : IRequest<Result<ReceivedCheckDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed record MarkReceivedCheckInClearingCommand(Guid CheckPublicId, string? Note = null) : IRequest<Result<ReceivedCheckDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed record MarkReceivedCheckPaidCommand(Guid CheckPublicId, string? Note = null) : IRequest<Result<ReceivedCheckDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class TransicionesDeChequeHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IActorActual actorActual, ICerrojoDeRecaudo cerrojo,
    AuditoriaDeRecaudo auditoria, IDateTimeService reloj) :
    IRequestHandler<MarkReceivedCheckDepositedCommand, Result<ReceivedCheckDto>>,
    IRequestHandler<MarkReceivedCheckInClearingCommand, Result<ReceivedCheckDto>>,
    IRequestHandler<MarkReceivedCheckPaidCommand, Result<ReceivedCheckDto>>
{
    public Task<Result<ReceivedCheckDto>> Handle(MarkReceivedCheckDepositedCommand request, CancellationToken ct) =>
        MoverAsync(request.CheckPublicId, [ReceivedCheckState.Received], ReceivedCheckState.Deposited, request.Note, c => c.RemittancePublicId ??= request.RemittancePublicId, ct);

    public Task<Result<ReceivedCheckDto>> Handle(MarkReceivedCheckInClearingCommand request, CancellationToken ct) =>
        MoverAsync(request.CheckPublicId, [ReceivedCheckState.Received, ReceivedCheckState.Deposited], ReceivedCheckState.InClearing, request.Note, null, ct);

    public Task<Result<ReceivedCheckDto>> Handle(MarkReceivedCheckPaidCommand request, CancellationToken ct) =>
        MoverAsync(request.CheckPublicId, [ReceivedCheckState.Deposited, ReceivedCheckState.InClearing], ReceivedCheckState.Paid, request.Note, null, ct);

    private async Task<Result<ReceivedCheckDto>> MoverAsync(Guid publicId, IReadOnlyList<ReceivedCheckState> desde, ReceivedCheckState hacia, string? nota,
        Action<ReceivedCheck>? ademas, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await ChequesRecibidos.VisibleAsync(db, alcance, publicId, seguir: false, ct);
        if (leido is null) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El cheque"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Cheques, [leido.Id], ct: ct);
        var c = await db.ReceivedChecks.FirstAsync(x => x.Id == leido.Id, ct);
        if (c.Status == hacia) return Result.Success(await ChequesRecibidos.DtoAsync(db, c, reloj.HoyLocal, ct));
        if (!desde.Contains(c.Status)) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.ChequeTransicionInvalida(c.Status.ToString(), hacia.ToString()));
        var antes = c.Status;
        ChequesRecibidos.Transicion(db, c, hacia, usuario, reloj.UtcNow, nota: nota);
        ademas?.Invoke(c);
        await auditoria.AgregarAsync("Receipts.Check.StatusChanged", "ReceivedCheck", c.PublicId, new { status = antes.ToString() }, new { status = hacia.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequesRecibidos.DtoAsync(db, c, reloj.HoyLocal, ct));
    }
}

/// <summary>
/// <c>POST /api/receipts/received-checks/{id}/return</c> (<c>Receipts.Checks.Return</c>, con clave; FR-060, FR-085, FR-188): el banco devolvió el cheque. Con la causal del
/// catálogo, en una sola operación: reversa por los contratos sólo las porciones pagadas con ese cheque (obligaciones antes que cuentas, con la clave <c>:reverso</c>; la
/// cuenta puede quedar en sobregiro técnico con bloqueo, lo decide Captaciones), da de baja el medio (<c>WrittenOff</c>, sin saldo a favor), deja una cuenta por cobrar al
/// pagador por lo que el cheque respaldaba y ya se entregó (vueltas o un saldo devuelto), emite <c>ChequeRecibidoDevuelto</c> y alerta <c>Recaudo.ChequeDevuelto</c>. El
/// cargo por devolución (<c>Cheques.CargoPorDevolucion</c>) sigue apagado en R0. (nuevo)
/// </summary>
public sealed record ReturnReceivedCheckCommand(Guid CheckPublicId, string CauseCode, Guid? SupportAttachmentPublicId = null, string? Note = null)
    : IRequest<Result<ReceivedCheckDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReturnReceivedCheckCommandValidator : AbstractValidator<ReturnReceivedCheckCommand>
{
    public ReturnReceivedCheckCommandValidator()
    {
        RuleFor(x => x.CheckPublicId).NotEmpty();
        RuleFor(x => x.CauseCode).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class ReturnReceivedCheckCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ReturnReceivedCheckCommand, Result<ReceivedCheckDto>>
{
    public const string ClaseDeDevolucion = "CD";

    public async Task<Result<ReceivedCheckDto>> Handle(ReturnReceivedCheckCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await ChequesRecibidos.VisibleAsync(db, alcance, request.CheckPublicId, seguir: false, ct);
        if (leido is null) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.NoEncontrado("El cheque"));
        var causa = await db.CheckReturnCauses.AsNoTracking().FirstOrDefaultAsync(x => x.Code == request.CauseCode.Trim() && x.IsActive, ct);
        if (causa is null) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.CausalDeDevolucionDesconocida(request.CauseCode));
        if (leido.Status == ReceivedCheckState.Returned) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.ChequeYaDevuelto());
        if (leido.Status is ReceivedCheckState.InCustody or ReceivedCheckState.Paid)
            return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.ChequeTransicionInvalida(leido.Status.ToString(), nameof(ReceivedCheckState.Returned)));

        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([leido.PayerPersonId], ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Cheques, [leido.Id], ct: ct);
        var cheque = await db.ReceivedChecks.FirstAsync(x => x.Id == leido.Id, ct);
        if (cheque.Status == ReceivedCheckState.Returned) return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.ChequeYaDevuelto());

        var medio = await db.ReceiptMeans.FirstOrDefaultAsync(m => m.ReceivedCheckId == cheque.Id, ct);
        Receipt? recibo = medio is null ? null : await db.IssuedReceipts.FirstAsync(r => r.Id == medio.ReceiptId, ct);
        var reversadas = new List<(ReceiptApplicationPortion Porcion, ReceiptApplication Aplicacion)>();
        var numero = await numerador.AsignarAsync(ClaseDeDevolucion, ClaseDeDevolucion, ct);
        if (medio is not null && recibo is not null)
        {
            var porciones = await db.ReceiptApplicationPortions.Where(p => p.ReceiptMeansId == medio.Id && p.Status == PortionState.Applied).OrderBy(p => p.Id).ToListAsync(ct);
            var ids = porciones.Select(p => p.ReceiptApplicationId).Distinct().ToList();
            var aplicaciones = await db.ReceiptApplications.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == recibo.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
            var referencia = new ReferenciaDeRecibo(numero, hoy, oficina, recibo.Number, "CheckReturn");
            var razon = $"Cheque {cheque.CheckNumber} devuelto ({causa.Code}: {causa.Description})";
            var rechazos = new List<object>();
            foreach (var p in porciones.Where(p => aplicaciones[p.ReceiptApplicationId].Target == ApplicationTarget.Loan))
            {
                var r = await obligaciones.ReversarAsync(new SolicitudDeReverso(ClavesDeRecaudo.Recaudador, p.IdempotencyKey, razon, referencia), ct);
                if (r.Result == ResultadoDelContrato.Rejected) rechazos.Add(new { idempotencyKey = p.IdempotencyKey, code = r.Code, message = r.Message });
            }
            if (rechazos.Count == 0)
                foreach (var p in porciones.Where(p => aplicaciones[p.ReceiptApplicationId].Target == ApplicationTarget.Account))
                {
                    var r = await cuentas.ReversarAsync(new SolicitudDeReversoDeCuenta(ClavesDeRecaudo.Recaudador, p.IdempotencyKey, razon, referencia), ct);
                    if (r.Result == ResultadoDelContrato.Rejected) rechazos.Add(new { idempotencyKey = p.IdempotencyKey, code = r.Code, message = r.Message });
                }
            if (rechazos.Count > 0)
            {
                numerador.Devolver(ClaseDeDevolucion, ClaseDeDevolucion, numero);
                return Result.Failure<ReceivedCheckDto>(ErroresDeRecaudo.DevolucionRechazadaPorContrato(rechazos));
            }
            foreach (var p in porciones)
            {
                p.Status = PortionState.Reversed;
                p.ReversalKey = ClavesDeRecaudo.Reverso(p.IdempotencyKey);
                var a = aplicaciones[p.ReceiptApplicationId];
                reversadas.Add((p, a));
            }
            foreach (var a in aplicaciones.Values.Where(a => a.Status == ApplicationState.Applied
                         && reversadas.Where(x => x.Aplicacion.Id == a.Id).Sum(x => x.Porcion.Amount) > 0m))
                a.Status = ApplicationState.ReversedByCheckReturn;
            medio.Status = ReceiptMeansState.WrittenOff;
        }

        // Lo que el cheque respaldaba y ya no está en ninguna porción (vueltas entregadas, un saldo ya devuelto): cuenta por cobrar al pagador.
        var porCobrar = cheque.Amount - reversadas.Sum(x => x.Porcion.Amount);
        PayerReceivable? cxc = null;
        if (porCobrar > 0m && medio is not null)
        {
            cxc = new PayerReceivable
            {
                PersonId = cheque.PayerPersonId, OriginDocumentPublicId = cheque.PublicId, OriginDocumentNumber = numero, Amount = porCobrar, Balance = porCobrar,
                OpenedOn = hoy,
            };
            db.PayerReceivables.Add(cxc);
            await db.SaveChangesAsync(ct);
            cheque.PayerReceivableId = cxc.Id;
        }
        cheque.ReturnCauseCode = causa.Code;
        ChequesRecibidos.Transicion(db, cheque, ReceivedCheckState.Returned, usuario, ahora, causa.Code, request.SupportAttachmentPublicId, request.Note);
        if (recibo is not null && medio is not null) await emision.ChequeRecibidoDevueltoAsync(cheque, numero, recibo, medio, reversadas, porCobrar, hoy, ct);
        await auditoria.AgregarAsync("Receipts.Check.Returned", "ReceivedCheck", cheque.PublicId, null,
            new { number = numero, check = cheque.CheckNumber, cause = causa.Code, reversed = reversadas.Count, receivable = porCobrar, by = actor.Name }, ct);
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoChequeDevuelto, "Cheque devuelto",
            string.Create(CultureInfo.InvariantCulture,
                $"El banco devolvió el cheque {cheque.CheckNumber} por {cheque.Amount:N0} ({causa.Description}): se reversaron {reversadas.Count} aplicación(es){(porCobrar > 0m ? $" y quedó una cuenta por cobrar de {porCobrar:N0}" : string.Empty)}. Avise al pagador."),
            "ReceivedCheck", cheque.PublicId, DedupKey: $"Recaudo.ChequeDevuelto:{cheque.PublicId:N}", ModuloDeOrigen: "Receipts",
            ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == cheque.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ChequesRecibidos.DtoAsync(db, cheque, hoy, ct));
    }
}

/// <summary><c>GET /api/receipts/received-checks?state=&amp;from=&amp;to=</c> (<c>Receipts.Checks.View</c>): los cheques de las oficinas al alcance. (nuevo)</summary>
public sealed record ListReceivedChecksQuery(ReceivedCheckState? State = null, DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<ReceivedCheckDto>>>;

public sealed class ListReceivedChecksQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, IDateTimeService reloj)
    : IRequestHandler<ListReceivedChecksQuery, Result<IReadOnlyList<ReceivedCheckDto>>>
{
    public async Task<Result<IReadOnlyList<ReceivedCheckDto>>> Handle(ListReceivedChecksQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.ReceivedChecks.AsNoTracking();
        if (request.State is { } s) consulta = consulta.Where(c => c.Status == s);
        if (request.From is { } d) consulta = consulta.Where(c => c.IssueDate >= d);
        if (request.To is { } h) consulta = consulta.Where(c => c.IssueDate <= h);
        var filas = (await consulta.OrderByDescending(c => c.Id).Take(300).ToListAsync(ct)).Where(c => mio.IncluyeOficina(c.BranchId)).ToList();
        var lista = new List<ReceivedCheckDto>(filas.Count);
        foreach (var c in filas) lista.Add(await ChequesRecibidos.DtoAsync(db, c, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<ReceivedCheckDto>>(lista);
    }
}

/// <summary>
/// <c>GET /api/receipts/received-checks/drawers-with-returns?person=&amp;account=</c> (<c>Receipts.Checks.View</c> o <c>Receipts.Receipt.Register</c>; FR-086): los giradores
/// con cheques devueltos por una causal que cuenta, para advertir al cajero antes de recibir otro. (nuevo)
/// </summary>
public sealed record ListDrawersWithReturnsQuery(Guid? DrawerPersonPublicId = null, string? AccountNumber = null) : IRequest<Result<IReadOnlyList<DrawerWithReturnsDto>>>;

public sealed class ListDrawersWithReturnsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDrawersWithReturnsQuery, Result<IReadOnlyList<DrawerWithReturnsDto>>>
{
    public async Task<Result<IReadOnlyList<DrawerWithReturnsDto>>> Handle(ListDrawersWithReturnsQuery request, CancellationToken ct)
    {
        var causas = await db.CheckReturnCauses.AsNoTracking().Where(c => c.CountsForDrawerList).Select(c => c.Code).ToListAsync(ct);
        var consulta = db.ReceivedChecks.AsNoTracking().Where(c => c.Status == ReceivedCheckState.Returned && c.ReturnCauseCode != null && causas.Contains(c.ReturnCauseCode));
        if (request.DrawerPersonPublicId is { } p)
        {
            var id = await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(c => c.DrawerPersonId == id || (c.DrawerPersonId == null && c.PayerPersonId == id));
        }
        if (!string.IsNullOrWhiteSpace(request.AccountNumber)) consulta = consulta.Where(c => c.AccountNumber == request.AccountNumber.Trim());
        var filas = await consulta.Select(c => new { c.DrawerPersonId, c.PayerPersonId, c.DrawerName, c.AccountNumber, c.UpdatedAt, c.CreatedAt }).ToListAsync(ct);
        var ids = filas.Select(f => f.DrawerPersonId ?? f.PayerPersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        var lista = filas.GroupBy(f => (Persona: f.DrawerPersonId ?? f.PayerPersonId, f.AccountNumber))
            .Select(g => new DrawerWithReturnsDto(publicos.GetValueOrDefault(g.Key.Persona), g.First().DrawerName, g.Key.AccountNumber, g.Count(),
                DateOnly.FromDateTime(g.Max(x => x.UpdatedAt ?? x.CreatedAt))))
            .OrderByDescending(x => x.Returned).ToList();
        return Result.Success<IReadOnlyList<DrawerWithReturnsDto>>(lista);
    }
}

/// <summary><c>GET /api/receipts/received-checks/return-causes</c> (<c>Receipts.Checks.View</c>): el catálogo de causales. (nuevo)</summary>
public sealed record ListCheckReturnCausesQuery : IRequest<Result<IReadOnlyList<CheckReturnCauseDto>>>;

public sealed class ListCheckReturnCausesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListCheckReturnCausesQuery, Result<IReadOnlyList<CheckReturnCauseDto>>>
{
    public async Task<Result<IReadOnlyList<CheckReturnCauseDto>>> Handle(ListCheckReturnCausesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<CheckReturnCauseDto>>(await db.CheckReturnCauses.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code)
            .Select(c => new CheckReturnCauseDto(c.Code, c.Description, c.CountsForDrawerList)).ToListAsync(ct));
}
