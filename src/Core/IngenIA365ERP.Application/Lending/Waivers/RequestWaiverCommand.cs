using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Waivers;

/// <summary>
/// Pide una condonación y la radica (feature 013, K3, T0884; api.md §11.2, <c>POST /api/lending/waivers</c>, <c>Lending.Condonations.Request</c>, con
/// clave; FR-094, US11-5): sobre un crédito vigente del alcance, por concepto —mora, corriente, suspenso, gasto de cobranza, seguro y, por excepción,
/// capital— con el saldo de cada uno a hoy (la liquidación base, <c>AsOfDate</c>): lo pedido no lo supera (<c>Lending.Waiver.ExceedsBalance</c> con
/// <c>concept</c> y <c>balance</c>); los días de mora y si el deudor o una parte es vinculado quedan sellados (son las condiciones de la atribución).
/// Siempre con motivo; el soporte, si viene, queda en la auditoría del comando. Responde 201 con su <c>routing</c>: quien la pidió no la decide ni la
/// aplica. (nuevo)
/// </summary>
public sealed record RequestWaiverCommand(Guid LoanPublicId, IReadOnlyList<WaiverConceptInput> Concepts, string Reason, Guid? SupportAttachmentPublicId = null)
    : IRequest<Result<WaiverDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestWaiverCommandValidator : AbstractValidator<RequestWaiverCommand>
{
    public RequestWaiverCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se condona.").MaximumLength(1000);
        RuleFor(x => x.Concepts).NotEmpty().WithMessage("Diga qué conceptos se condonan y por cuánto.");
        RuleForEach(x => x.Concepts).ChildRules(c =>
        {
            c.RuleFor(x => x.Concept).Must(WaiverLine.Condonables.Contains)
                .WithMessage("Se condona interés de mora o corriente (también en suspenso), gasto de cobranza, seguro o, por excepción, capital.");
            c.RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).WithMessage("El valor que se condona es mayor que cero, con hasta dos decimales.");
            c.RuleFor(x => x.InstallmentNumber).GreaterThan(0).When(x => x.InstallmentNumber is not null);
        });
    }
}

public sealed class RequestWaiverCommandHandler(
    IApplicationDbContext db,
    CondonacionesDeCartera condonaciones,
    CargadorDeReconstruccion cargador,
    VinculadosVigentes vinculados,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<RequestWaiverCommand, Result<WaiverDto>>
{
    public async Task<Result<WaiverDto>> Handle(RequestWaiverCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Falla(Settlement.LecturaDelCredito.Inexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, Settlement.LecturaDelCredito.Inexistente(), auditoria, "Loan",
            credito.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        if (credito.Status != LoanStatus.Active) return Falla(ErroresDeNovedades.CreditoNoActivo(credito.Number, credito.Status.ToString()));
        var hoy = reloj.HoyLocal;

        var cargado = await cargador.CargarAsync(credito.Id, null, hoy, ct);
        if (cargado is not { Listo: true }) return Falla(cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente());
        EstadoDelCredito estado;
        try
        {
            estado = MotorDeCartera.Reconstruir(cargado.Entrada!, hoy);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        var partes = await db.LoanParties.AsNoTracking().Where(p => p.LoanId == credito.Id && !p.IsDeleted && p.ReleasedOn == null && p.ExtinguishedOn == null)
            .Select(p => p.PersonId).ToListAsync(ct);
        var vinculado = await vinculados.HayAlgunoAsync([credito.DebtorPersonId, .. partes], hoy, ct);
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, Guid.Empty), AccionesDeSegregacion.Radicar,
            hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);

            var numero = await numerador.AsignarAsync(LendingNumberKind.Waiver, string.Empty, CondonacionesDeCartera.Prefijo, ct);
            var w = Waiver.Nueva(numero, credito.Id, hoy, request.Reason, estado.AlturaDeMora.Dias, vinculado, usuario, reloj.UtcNow);
            try
            {
                foreach (var linea in request.Concepts)
                    w.AgregarLinea(linea.Concept, linea.InstallmentNumber, linea.Amount, Disponible(estado, linea.Concept, linea.InstallmentNumber));
                w.Radicar();
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            var enrutada = await condonaciones.EnrutarAsync(w, credito, hoy, ct);
            if (enrutada.IsFailure) return Falla(enrutada.Error);
            db.Waivers.Add(w);
            await db.SaveChangesAsync(ct);
            intervenciones.Registrar(verificada.Value with { Expediente = new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, w.PublicId) },
                LendingDecisionSubjects.Condonation, w.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await condonaciones.DtoAsync(w, credito, ct));
        }, ct);
    }

    /// <summary>El saldo de un concepto a la fecha base: en la cuota pedida o en todas (en el capital, el pendiente de cada cuota).</summary>
    public static decimal Disponible(EstadoDelCredito estado, LoanConcept concepto, int? cuota) =>
        estado.Saldos.Where(s => s.Concepto == concepto && (cuota is null || s.Cuota == cuota)).Sum(s => s.Valor);

    private static Result<WaiverDto> Falla(Error e) => Result.Failure<WaiverDto>(e);
}
