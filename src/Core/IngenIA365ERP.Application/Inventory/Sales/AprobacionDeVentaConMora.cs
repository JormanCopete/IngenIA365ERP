using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Pedir la aprobación de una venta a crédito con mora (<c>POST /api/inventory/sales/credit-arrears-approvals</c>, permiso
/// <c>Inventory.Sales.SellOnCredit</c>, con <c>Idempotency-Key</c>; decisión del dueño del 2026-11-12). Lo pide la pantalla de cobro (POS, factura,
/// documentos del ciclo) cuando la evaluación dice <c>ArrearsAction = Approve</c>; después un supervisor presente decide con su passkey o su TOTP por
/// <c>/api/inventory/approvals/{id}/presence-challenge</c> y <c>/decide</c> —el mismo camino que el descuento sobre el tope—, y el pago lleva el
/// <c>PublicId</c> de la solicitud aprobada. (nuevo)
/// </summary>
public sealed record RequestCreditInArrearsApprovalCommand(
    Guid PersonPublicId,
    Guid PaymentMeansPublicId,
    decimal Amount,
    DateOnly? OperationDate = null,
    string? CreditLineCode = null,
    Guid? PointOfSalePublicId = null,
    Guid? DocumentTypePublicId = null) : IRequest<Result<CreditInArrearsApprovalDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestCreditInArrearsApprovalCommandValidator : AbstractValidator<RequestCreditInArrearsApprovalCommand>
{
    public RequestCreditInArrearsApprovalCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.CreditLineCode).MaximumLength(20);
    }
}

/// <summary>
/// La solicitud de aprobación de la venta con mora: su <c>PublicId</c> (el que viaja en el pago), su estado (<c>Pending</c> o <c>Approved</c>), la huella
/// que confirma la decisión en persona, los días de mora y la línea con que se evaluó, y el permiso que necesita el supervisor. (nuevo)
/// </summary>
public sealed record CreditInArrearsApprovalDto(
    Guid ApprovalRequestPublicId,
    string Status,
    string ContentSha256,
    int DaysPastDue,
    string? CreditLineCode,
    string PermissionCode);

/// <summary>
/// El manejador (decisión del dueño del 2026-11-12): vuelve a preguntar a Cartera con la línea de la venta —la pantalla no decide—, y sólo si la mora
/// exige aprobación (<c>Approve</c>) pide al motor de aprobaciones la solicitud del sujeto <c>CreditInArrears</c> con su huella
/// (<see cref="ReglaDeMoraParaVender.Huella"/>: persona, clase del medio, valor, línea, fecha y días) y el punto de venta como alcance. Una solicitud
/// pendiente o aprobada (y sin usar) de la misma huella, pedida por la misma persona, se devuelve en vez de pedir otra. Sin política, rige la regla
/// fija de un nivel con <c>Inventory.Sales.AuthorizeCreditInArrears</c>; el motor excluye a quien pide y la fuente compara personas. (nuevo)
/// </summary>
public sealed class RequestCreditInArrearsApprovalCommandHandler(
    IApplicationDbContext db,
    IConsultasDeCartera cartera,
    IMotorDeAprobaciones motor,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILectorDeParametros parametros)
    : IRequestHandler<RequestCreditInArrearsApprovalCommand, Result<CreditInArrearsApprovalDto>>
{
    public const string Permiso = "Inventory.Sales.AuthorizeCreditInArrears";
    public const string CodigoSinPolitica = "Inventory.Credit.ArrearsApprovalPolicyNotRequired";

    public async Task<Result<CreditInArrearsApprovalDto>> Handle(RequestCreditInArrearsApprovalCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } solicitante) return Result.Failure<CreditInArrearsApprovalDto>(Error.NotFound);

        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<CreditInArrearsApprovalDto>(PaymentMeansErrors.NotFound());
        if (!ClasesDeMedio.EsCredito(medio.Class)) return Result.Failure<CreditInArrearsApprovalDto>(ErroresDeCredito.MeansNotCredit(medio.Code));
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<CreditInArrearsApprovalDto>(Error.NotFound);

        var fecha = request.OperationDate ?? reloj.HoyLocal;
        var elegibilidad = await CreditoEnLaVenta.ElegibilidadAsync(db, persona, fecha, medio.Class, ct);
        if (!elegibilidad.Elegible) return Result.Failure<CreditInArrearsApprovalDto>(elegibilidad.Motivos[0]);
        var canal = request.PointOfSalePublicId is { } punto
            ? await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == punto).Select(p => p.SalesChannel!.Code).FirstOrDefaultAsync(ct)
            : null;
        var linea = string.IsNullOrWhiteSpace(request.CreditLineCode) ? medio.SuggestedCreditLineCode : request.CreditLineCode.Trim();
        var estado = medio.CreditWithoutLending
            ? null
            : await CreditoEnLaVenta.ConsultarConPlazoAsync(cartera,
                new ConsultaCrediticia(persona.PublicId, medio.Class, request.Amount, fecha, canal) { CreditLineCode = linea },
                await CreditoEnLaVenta.SegundosDeConsultaAsync(parametros, fecha, ct), ct);
        if (estado is not { LendingEnabled: true } || estado.ArrearsAction != AccionesPorMora.Approve)
            return Result.Failure<CreditInArrearsApprovalDto>(ErroresDeCredito.ArrearsApprovalNotRequired(estado?.ArrearsAction ?? AccionesPorMora.None));
        if (estado.AvailableQuota is { } cupo && request.Amount > cupo)
            return Result.Failure<CreditInArrearsApprovalDto>(ErroresDeCredito.QuotaExceeded(request.Amount, cupo));

        var dias = estado.DaysPastDue ?? 0;
        var huella = ReglaDeMoraParaVender.Huella(persona.PublicId, medio.Class, request.Amount, linea, fecha, dias);

        // La misma venta pedida otra vez (otra evaluación, otro clic): la solicitud viva sirve si nadie la usó todavía.
        var previas = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.SaleCreditInArrears && r.Subject == ApprovalSubjects.CreditInArrears && r.ContentSha256 == huella
                        && r.RequestedByUserId == solicitante && (r.Status == ApprovalRequestStatus.Pending || r.Status == ApprovalRequestStatus.Approved))
            .OrderByDescending(r => r.Id)
            .ToListAsync(ct);
        foreach (var previa in previas)
        {
            var usada = await db.DocumentPayments.AsNoTracking()
                .Where(p => p.ArrearsApprovalRequestPublicId == previa.PublicId && !p.IsDeleted)
                .Join(db.InventoryDocuments.AsNoTracking(), p => p.DocumentId, d => d.Id, (p, d) => d.Status)
                .AnyAsync(s => s == DocumentStatus.Confirmed || s == DocumentStatus.PendingApproval, ct);
            if (!usada) return Result.Success(Dto(previa, dias, linea));
        }

        var rotulo = $"Crédito con mora · {elegibilidad.Nombre} · {dias} días";
        var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(
            ApprovalSubjects.CreditInArrears,
            ApprovalSourceTypes.SaleCreditInArrears,
            Guid.NewGuid(),
            rotulo.Length > 80 ? rotulo[..80] : rotulo,
            request.DocumentTypePublicId,
            null,
            request.PointOfSalePublicId,
            request.Amount,
            fecha,
            solicitante,
            [],
            huella,
            null), ct);
        if (solicitada.IsFailure) return Result.Failure<CreditInArrearsApprovalDto>(solicitada.Error);
        if (solicitada.Value is not { } nueva)
            // Una política de la cooperativa para este sujeto que no pide aprobación para este valor dejaría la venta sin salida: se dice.
            return Result.Failure<CreditInArrearsApprovalDto>(new Error(CodigoSinPolitica,
                "La política de aprobaciones de la cooperativa para la venta a crédito con mora no pide aprobación para este valor. Corrija la política "
                + "(el primer nivel desde cero) o cambie la política de mora para vender a «Advertir»."));

        await db.SaveChangesAsync(ct);
        return Result.Success(Dto(nueva, dias, linea));
    }

    private static CreditInArrearsApprovalDto Dto(ApprovalRequest r, int dias, string? linea) => new(
        r.PublicId, r.Status.ToString(), r.ContentSha256, dias, linea,
        VistaDeSolicitudes.NivelActual(r)?.PermissionCode ?? Permiso);
}

/// <summary>
/// La fuente de aprobación de la venta a crédito con mora (decisión del dueño del 2026-11-12; <c>SourceType = SaleCreditInArrears</c>). Aprobar
/// <b>no vende nada</b>: la venta se cobra después con la solicitud aprobada en su pago, y la confirmación y la reserva de Cartera comprueban su huella.
/// En cada nivel, quien aprueba no es la misma <b>persona</b> que pidió (el motor compara usuarios y esta fuente compara personas). El alcance es el del
/// punto de venta de la solicitud, si lo tiene. (nuevo)
/// </summary>
public sealed class FuenteDeAprobacionDeVentaConMora(IApplicationDbContext db, PersonasDeLosUsuarios personas) : IFuenteDeAprobacion, IFuenteConAprobador
{
    public const string CodigoAutoaprobacion = "Inventory.Credit.ArrearsSelfApproval";
    public const string CodigoPresenciaRequerida = "Inventory.Credit.ArrearsApproverNotPresent";

    public string SourceType => ApprovalSourceTypes.SaleCreditInArrears;

    public async Task<Result> AlAprobarElNivelAsync(ApprovalRequest solicitud, int aprobadorUserId, ApprovalMethod metodo, bool esElUltimo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        // El dueño pidió un supervisor presente en la caja con su segundo factor: aprobar desde la bandeja con la sesión propia no vale.
        if (metodo is not (ApprovalMethod.InPersonPasskey or ApprovalMethod.InPersonTotp))
            return Result.Failure(new Error(CodigoPresenciaRequerida,
                "La venta a crédito con mora la aprueba el supervisor presente en la caja, con su llave o su código, no desde la bandeja."));
        return await personas.AlgunaEsLaMismaAsync(aprobadorUserId, [solicitud.RequestedByUserId, solicitud.CreatedByUserId, .. solicitud.Excluidos()], ct)
            ? Result.Failure(new Error(CodigoAutoaprobacion,
                "Quien aprueba es la misma persona que está cobrando: la venta a crédito con mora la aprueba otra persona con el permiso del nivel."))
            : Result.Success();
    }

    public Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, ApprovalRequestStatus.Approved.ToString(), solicitud.SourceLabel)));

    public Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct) =>
        Task.FromResult(Result.Success(new EstadoDeFuenteDto(solicitud.SourcePublicId, SourceType, "NotApplied", solicitud.SourceLabel)));

    public async Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(alcance);
        if (solicitud.ScopePointOfSalePublicId is not { } punto || alcance.TodosLosPuntos) return true;
        var id = await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == punto).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is { } p && alcance.Puntos.Contains(p);
    }

    public async Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct)
    {
        var filas = await db.ApprovalRequests.AsNoTracking()
            .Where(r => r.SourceType == ApprovalSourceTypes.SaleCreditInArrears && sourcePublicIds.Contains(r.SourcePublicId))
            .Select(r => new { r.SourcePublicId, r.SourceLabel, r.Amount, r.OperationDate, r.ScopePointOfSalePublicId })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.SourcePublicId).ToDictionary(g => g.Key, g =>
        {
            var f = g.First();
            return new OrigenDeAprobacionDto(f.SourcePublicId, "SaleCreditInArrears", null, null, f.OperationDate, null, f.ScopePointOfSalePublicId,
                $"{f.SourceLabel}: venta a crédito por {f.Amount:N2}. El supervisor la aprueba en persona, en el equipo de la caja.", null);
        });
    }
}
