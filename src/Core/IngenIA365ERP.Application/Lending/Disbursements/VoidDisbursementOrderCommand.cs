using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Pide a Tesorería anular una orden de giro (feature 013, US3, T0446; api.md §9.2, <c>POST /api/lending/disbursement-orders/{id}/void</c>,
/// <c>Lending.DisbursementOrders.Void</c>, con clave): sólo <c>Issued</c> sin anulación pedida (<c>Lending.DisbursementOrder.NotVoidable</c> con
/// <c>data.status</c>) y fuera de un archivo vivo (<c>.InFile</c> con <c>data.filePublicId</c>: se anula primero el archivo). Emite
/// <c>AnulacionDeOrdenDeGiro</c> (clave <c>Confirmation:{orden:N}</c>, <c>related</c> = la orden, C12) y registra la intervención
/// <c>AnularOrdenDeGiro</c>; la orden sigue <c>Issued</c> hasta que Tesorería responda. (nuevo)
/// </summary>
public sealed record VoidDisbursementOrderCommand(Guid OrderPublicId, string Reason) : IRequest<Result<DisbursementOrderDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidDisbursementOrderCommandValidator : AbstractValidator<VoidDisbursementOrderCommand>
{
    public VoidDisbursementOrderCommandValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula la orden.").MaximumLength(500);
    }
}

public sealed class VoidDisbursementOrderCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    RegistroDeIntervenciones intervenciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidDisbursementOrderCommand, Result<DisbursementOrderDto>>
{
    public async Task<Result<DisbursementOrderDto>> Handle(VoidDisbursementOrderCommand request, CancellationToken ct)
    {
        var leida = await OrdenesEnAlcance.LeerAsync(db, alcance, auditoria, request.OrderPublicId, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (orden, credito) = leida.Value;
        var archivo = orden.CurrentDisbursementFileId is { } f
            ? await db.DisbursementFiles.AsNoTracking().Where(x => x.Id == f).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        if (orden.PuedePedirAnulacion(archivo) is { } problema) return Falla(ErroresDeGobierno.De(problema));

        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(await OrdenesEnAlcance.ExpedienteAsync(db, credito, ct), AccionesDeSegregacion.AnularOrdenDeGiro, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Falla(sucursal.Error);
        var fecha = await fechaContable.CalcularAsync(hoy, ct);
        if (fecha.IsFailure) return Falla(fecha.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var o = await db.DisbursementOrders.FirstAsync(x => x.Id == orden.Id, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            if (o.PuedePedirAnulacion(archivo) is { } tarde) return Falla(ErroresDeGobierno.De(tarde));

            var actor = await actorActual.ObtenerAsync(ct);
            o.PedirAnulacion(actor.UserId ?? verificada.Value.UsuarioId, reloj.UtcNow, request.Reason);
            var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == o.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);
            await emision.AnulacionDeOrdenAsync(o, beneficiario, sucursal.Value, fecha.Value, request.Reason.Trim(), ct);
            intervenciones.Registrar(verificada.Value, EmisionDeCartera.ClaseOrdenDeGiro, o.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeOrdenesDeGiro.DtosAsync(db, [o], cuentaCompleta: true, ct))[0]);
        }, ct);
    }

    private static Result<DisbursementOrderDto> Falla(Error e) => Result.Failure<DisbursementOrderDto>(e);
}

/// <summary>La orden de giro de un comando: existe y su crédito está en el alcance (si no, el mismo 404). (nuevo)</summary>
public static class OrdenesEnAlcance
{
    public static async Task<Result<(DisbursementOrder Orden, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var orden = await db.DisbursementOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == publicId && !o.IsDeleted, ct);
        if (orden is null) return Result.Failure<(DisbursementOrder, Loan)>(ErroresDeDesembolso.OrdenInexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == orden.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, ErroresDeDesembolso.OrdenInexistente(), auditoria,
            nameof(DisbursementOrder), orden.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(DisbursementOrder, Loan)>(dentro.Error) : Result.Success((orden, credito));
    }

    /// <summary>El expediente de segregación del crédito: su solicitud, o el crédito si nació sin ella.</summary>
    public static async Task<ExpedienteDeCartera> ExpedienteAsync(IApplicationDbContext db, Loan credito, CancellationToken ct)
    {
        var solicitud = credito.ApplicationId is { } id
            ? await db.CreditApplications.AsNoTracking().Where(a => a.Id == id).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return solicitud is { } s
            ? new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s)
            : new ExpedienteDeCartera(nameof(Loan), credito.PublicId);
    }
}
