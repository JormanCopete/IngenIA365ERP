using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Evaluar una venta a crédito (<c>POST /api/inventory/sales/credit-evaluations</c>; feature 012, I3, T653; contracts/api.md §23.1; T32)
/// (nuevo). Es una <b>consulta</b>, sin clave de operación: la pantalla la llama al elegir un medio de crédito y el servidor repite lo mismo
/// al confirmar (<see cref="CreditoEnLaVenta"/>). Permiso <c>Inventory.Sales.SellOnCredit</c>.
/// </summary>
public sealed record EvaluateSaleCreditQuery(
    Guid PersonPublicId,
    Guid PaymentMeansPublicId,
    decimal Amount,
    DateOnly? OperationDate = null,
    Guid? PointOfSalePublicId = null,
    Guid? DocumentTypePublicId = null) : IRequest<Result<SaleCreditEvaluationDto>>;

public sealed class EvaluateSaleCreditQueryValidator : AbstractValidator<EvaluateSaleCreditQuery>
{
    public EvaluateSaleCreditQueryValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

/// <summary>Un motivo de no elegibilidad, con el mismo código que daría el cobro (§23.1). (nuevo)</summary>
public sealed record CreditReasonDto(string Code, string Message);

/// <summary><c>person</c> de la evaluación (§23.1). (nuevo)</summary>
public sealed record CreditPersonDto(Guid PublicId, string Name, bool IsAssociate, bool? AssociateActive, bool IsCustomer, bool Eligible,
    IReadOnlyList<CreditReasonDto> Reasons);

/// <summary>Un nivel de la aprobación que exigirá el crédito. (nuevo)</summary>
public sealed record CreditApprovalLevelDto(int Order, string PermissionCode, decimal Threshold);

/// <summary><c>approval</c>: los niveles y el mayor monto que alguien de la cooperativa puede aprobar (nulo = sin límite). (nuevo)</summary>
public sealed record CreditApprovalDto(IReadOnlyList<CreditApprovalLevelDto> Levels, decimal? ApproverMaxAmount);

/// <summary>
/// <c>creditDefaults</c> del medio (§23.1): cuotas máximas, plazo propuesto, periodicidad y línea sugerida; <c>MaxTermDays</c> y
/// <c>DefaultInstallments</c> se agregan para que la pantalla edite dentro de los máximos. (nuevo)
/// </summary>
public sealed record CreditDefaultsDto(short? MaxInstallments, short? TermDays, short? PeriodicityDays, string? SuggestedLineCode, short? MaxTermDays,
    short? DefaultInstallments);

/// <summary><c>lending</c> (IC): lo que responde Cartera, nulo en el crédito provisional. (nuevo)</summary>
public sealed record LendingStatusDto(string Status, decimal? AvailableQuota, IReadOnlyList<LineaDeCreditoDto> Lines, string? EvidenceId);

/// <summary>La evaluación de §23.1. (nuevo)</summary>
public sealed record SaleCreditEvaluationDto(
    bool LendingEnabled,
    CreditOrigin Origin,
    CreditPersonDto Person,
    bool RequiresApproval,
    CreditApprovalDto? Approval,
    CreditDefaultsDto CreditDefaults,
    LendingStatusDto? Lending);

/// <summary>
/// La evaluación del crédito provisional (T653): con <see cref="CarteraNoHabilitada"/>, <c>lendingEnabled = false</c>, <c>origin =
/// ProvisionalCredit</c>, <c>requiresApproval = true</c> y <c>lending</c> nulo; la elegibilidad con los códigos <c>Inventory.Credit.*</c>;
/// los niveles de la política <c>ProvisionalCredit</c> del tipo o el nivel fijo con <c>Inventory.Sales.SellOnCredit</c>, y el mayor monto
/// máximo de ese permiso entre quienes podrían aprobar (<c>SEC_PermissionAmountLimits</c>, sin contar a quien consulta). (nuevo)
/// </summary>
public sealed class EvaluateSaleCreditQueryHandler(
    IApplicationDbContext db,
    IConsultasDeCartera cartera,
    IMotorDeAprobaciones motor,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<EvaluateSaleCreditQuery, Result<SaleCreditEvaluationDto>>
{
    public async Task<Result<SaleCreditEvaluationDto>> Handle(EvaluateSaleCreditQuery request, CancellationToken ct)
    {
        var medio = await db.PaymentMeans.AsNoTracking().FirstOrDefaultAsync(m => m.PublicId == request.PaymentMeansPublicId && !m.IsDeleted, ct);
        if (medio is null) return Result.Failure<SaleCreditEvaluationDto>(PaymentMeansErrors.NotFound());
        if (!ClasesDeMedio.EsCredito(medio.Class)) return Result.Failure<SaleCreditEvaluationDto>(ErroresDeCredito.MeansNotCredit(medio.Code));
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<SaleCreditEvaluationDto>(Error.NotFound);

        var fecha = request.OperationDate ?? reloj.HoyLocal;
        var elegibilidad = await CreditoEnLaVenta.ElegibilidadAsync(db, persona, fecha, medio.Class, ct);
        var canal = request.PointOfSalePublicId is { } punto
            ? await db.PointsOfSale.AsNoTracking().Where(p => p.PublicId == punto).Select(p => p.SalesChannel!.Code).FirstOrDefaultAsync(ct)
            : null;
        var estado = await cartera.EstadoCrediticioAsync(new ConsultaCrediticia(persona.PublicId, medio.Class, request.Amount, fecha, canal), ct);

        var evaluacion = await motor.EvaluarAsync(ApprovalSubjects.ProvisionalCredit, request.DocumentTypePublicId, fecha, request.Amount, null, ct);
        IReadOnlyList<NivelDeAprobacion> niveles = evaluacion.IsSuccess ? evaluacion.Value.Evaluacion.Niveles : [];
        if (niveles.Count == 0 && EvaluadorDePolitica.ReglaFija(ApprovalSubjects.ProvisionalCredit) is { } fija && !evaluacion.IsSuccess) niveles = [fija];
        var actor = await actorActual.ObtenerAsync(ct);
        var maximo = await LimitesDePermisoPorUsuario.MayorDeLaCooperativaAsync(db, AprobacionDeCredito.Permiso, fecha,
            actor.UserId is int yo ? [yo] : [], ct);

        return Result.Success(new SaleCreditEvaluationDto(
            estado.LendingEnabled,
            CreditOrigin.ProvisionalCredit,
            new CreditPersonDto(persona.PublicId, elegibilidad.Nombre, elegibilidad.IsAssociate, elegibilidad.AssociateActive, elegibilidad.IsCustomer,
                elegibilidad.Elegible, elegibilidad.Motivos.Select(m => new CreditReasonDto(m.Code, m.Message)).ToList()),
            niveles.Count > 0,
            new CreditApprovalDto(niveles.Select(n => new CreditApprovalLevelDto(n.Order, n.PermissionCode, n.Threshold)).ToList(), maximo),
            new CreditDefaultsDto(medio.MaxInstallments, medio.DefaultTermDays, medio.InstallmentPeriodDays, medio.SuggestedCreditLineCode,
                CreditoEnLaVenta.MaximoDePlazo(medio), medio.DefaultInstallments),
            estado.LendingEnabled ? new LendingStatusDto(estado.Status, estado.AvailableQuota, estado.Lines, estado.EvidenceId) : null));
    }
}
