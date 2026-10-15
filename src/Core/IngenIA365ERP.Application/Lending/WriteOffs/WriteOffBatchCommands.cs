using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// Arma un lote de castigo (feature 013, K3, US14, T0906; api.md §11.3, <c>POST /api/lending/write-off-batches</c>, <c>Lending.WriteOffs.Prepare</c>, con
/// clave; FR-095, US14-1, C98): sobre créditos vigentes del alcance que no estén en otro lote abierto, verifica a hoy los requisitos de la norma y de la
/// política de cada uno (<see cref="CastigosDeCartera.VerificarAsync"/>); los que cumplen quedan <c>Included</c> con sus montos al corte y el deterioro del
/// último cierre (<c>ImpairmentAtCutoff</c>) y los que no, <c>Rejected</c> con lo que falta. Numera el lote (<c>LCA-</c>), registra la intervención
/// <c>ArmarLoteDeCastigo</c> y responde 201 con el lote en borrador. (nuevo)
/// </summary>
public sealed record CreateWriteOffBatchCommand(IReadOnlyList<Guid> LoanPublicIds, string Reason, IReadOnlyList<WriteOffSupportInput>? Reports = null)
    : IRequest<Result<WriteOffBatchDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateWriteOffBatchCommandValidator : AbstractValidator<CreateWriteOffBatchCommand>
{
    public CreateWriteOffBatchCommandValidator()
    {
        RuleFor(x => x.LoanPublicIds).NotEmpty().WithMessage("Elija al menos un crédito para el lote.")
            .Must(l => l.Distinct().Count() == l.Count).WithMessage("Un crédito va una sola vez en el lote.");
        RuleForEach(x => x.LoanPublicIds).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se propone el castigo.").MaximumLength(1000);
        RuleFor(x => x.Reports).Must(r => r is null || r.Select(x => x.LoanPublicId).Distinct().Count() == r.Count)
            .WithMessage("Los soportes de un crédito van una sola vez.");
    }
}

public sealed class CreateWriteOffBatchCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateWriteOffBatchCommand, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(CreateWriteOffBatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leidos = await PreparacionDeLotes.CreditosAsync(db, alcance, auditoria, request.LoanPublicIds, null, ct);
        if (leidos.IsFailure) return Falla(leidos.Error);
        var creditos = leidos.Value;
        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CastigosDeCartera.Expediente, Guid.Empty),
            AccionesDeSegregacion.ArmarLoteDeCastigo, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);
        var soportes = (request.Reports ?? []).ToDictionary(r => r.LoanPublicId);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync(creditos.Select(c => c.Id).ToList(), ModoDeEspera.Esperar, ct);
            var verificados = await castigos.VerificarAsync(creditos, soportes, hoy, ct);
            if (verificados.IsFailure) return Falla(verificados.Error);

            var numero = await numerador.AsignarAsync(LendingNumberKind.WriteOffBatch, string.Empty, CastigosDeCartera.Prefijo, ct);
            var lote = new WriteOffBatch
            {
                Number = numero, CutoffDate = hoy, Reason = request.Reason.Trim(), PreparedByUserId = usuario, PreparedAt = reloj.UtcNow,
            };
            db.WriteOffBatches.Add(lote);
            await db.SaveChangesAsync(ct);
            var items = verificados.Value.Select(v => WriteOffBatchItem.Desde(lote.Id, v.Verificacion, v.Capital, v.Intereses, v.Otros, v.Deterioro,
                v.Informe, v.Concepto)).ToList();
            db.WriteOffBatchItems.AddRange(items);
            lote.Recalcular(items);
            intervenciones.Registrar(verificada.Value with { Expediente = new ExpedienteDeCartera(CastigosDeCartera.Expediente, lote.PublicId) },
                LendingDecisionSubjects.WriteOffBatch, lote.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await castigos.DtoAsync(lote, items, ct));
        }, ct);
    }

    private static Result<WriteOffBatchDto> Falla(Error e) => Result.Failure<WriteOffBatchDto>(e);
}

/// <summary>
/// Agrega un crédito a un lote en borrador (feature 013, K3, US14, T0906; api.md §11.3, <c>POST /{id}/items</c>, <c>Lending.WriteOffs.Prepare</c>, con
/// clave): fuera de <c>Draft</c> → <c>Lending.WriteOffBatch.NotDraft</c>; uno que no cumple no entra: <c>Lending.WriteOff.RequirementMissing</c> con
/// <c>data.missing[]</c> (C98). (nuevo)
/// </summary>
public sealed record AddWriteOffBatchItemCommand(
    Guid WriteOffBatchPublicId,
    Guid LoanPublicId,
    Guid? IrrecoverabilityAttachmentPublicId = null,
    Guid? ReportAttachmentPublicId = null,
    Guid? ContributionsOffsetAttachmentPublicId = null) : IRequest<Result<WriteOffBatchDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AddWriteOffBatchItemCommandValidator : AbstractValidator<AddWriteOffBatchItemCommand>
{
    public AddWriteOffBatchItemCommandValidator()
    {
        RuleFor(x => x.WriteOffBatchPublicId).NotEmpty();
        RuleFor(x => x.LoanPublicId).NotEmpty();
    }
}

public sealed class AddWriteOffBatchItemCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<AddWriteOffBatchItemCommand, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(AddWriteOffBatchItemCommand request, CancellationToken ct)
    {
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: true, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var (lote, items) = leido.Value;
        if (lote.ExigirBorrador() is { } noBorrador) return Falla(ErroresDeGobierno.De(noBorrador));
        var leidos = await PreparacionDeLotes.CreditosAsync(db, alcance, auditoria, [request.LoanPublicId], lote.Id, ct);
        if (leidos.IsFailure) return Falla(leidos.Error);
        var credito = leidos.Value[0];
        if (items.Any(i => i.LoanId == credito.Id)) return Falla(ErroresDeCastigo.YaIncluido(credito.Number));
        var hoy = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CastigosDeCartera.Expediente, lote.PublicId),
            AccionesDeSegregacion.ArmarLoteDeCastigo, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            var soporte = new WriteOffSupportInput(credito.PublicId, request.IrrecoverabilityAttachmentPublicId, request.ReportAttachmentPublicId,
                request.ContributionsOffsetAttachmentPublicId);
            var verificados = await castigos.VerificarAsync([credito], new Dictionary<Guid, WriteOffSupportInput> { [credito.PublicId] = soporte }, hoy, ct);
            if (verificados.IsFailure) return Falla(verificados.Error);
            var v = verificados.Value[0];
            if (!v.Verificacion.Cumple) return Falla(ErroresDeCastigo.RequisitoFaltante(credito.Number, v.Verificacion));

            var item = WriteOffBatchItem.Desde(lote.Id, v.Verificacion, v.Capital, v.Intereses, v.Otros, v.Deterioro, v.Informe, v.Concepto);
            db.WriteOffBatchItems.Add(item);
            lote.Recalcular([.. items, item]);
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.WriteOffBatch, lote.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await castigos.DtoAsync(lote, [.. items, item], ct));
        }, ct);
    }

    private static Result<WriteOffBatchDto> Falla(Error e) => Result.Failure<WriteOffBatchDto>(e);
}

/// <summary>
/// Quita un crédito (incluido o rechazado) de un lote en borrador (feature 013, K3, US14, T0906; api.md §11.3, <c>DELETE /{id}/items/{itemId}</c>,
/// <c>Lending.WriteOffs.Prepare</c>, con clave): fuera de <c>Draft</c> → <c>Lending.WriteOffBatch.NotDraft</c>. El ítem de un borrador no es un hecho: se
/// borra y los totales se recalculan. (nuevo)
/// </summary>
public sealed record RemoveWriteOffBatchItemCommand(Guid WriteOffBatchPublicId, Guid ItemPublicId) : IRequest<Result<WriteOffBatchDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveWriteOffBatchItemCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<RemoveWriteOffBatchItemCommand, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(RemoveWriteOffBatchItemCommand request, CancellationToken ct)
    {
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<WriteOffBatchDto>(leido.Error);
        var (lote, items) = leido.Value;
        if (lote.ExigirBorrador() is { } noBorrador) return Result.Failure<WriteOffBatchDto>(ErroresDeGobierno.De(noBorrador));
        var item = items.FirstOrDefault(i => i.PublicId == request.ItemPublicId);
        if (item is null) return Result.Failure<WriteOffBatchDto>(ErroresDeCastigo.ItemInexistente());
        db.WriteOffBatchItems.Remove(item);
        var quedan = items.Where(i => i != item).ToList();
        lote.Recalcular(quedan);
        await db.SaveChangesAsync(ct);
        return Result.Success(await castigos.DtoAsync(lote, quedan, ct));
    }
}

/// <summary>
/// Envía el lote al consejo (feature 013, K3, US14, T0906; api.md §11.3, <c>POST /{id}/submit</c>, <c>Lending.WriteOffs.Prepare</c>, con clave): lo
/// congela en <c>Submitted</c> para llevarlo a una sesión del consejo de administración (§5), con al menos un crédito incluido. (nuevo)
/// </summary>
public sealed record SubmitWriteOffBatchCommand(Guid WriteOffBatchPublicId) : IRequest<Result<WriteOffBatchDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SubmitWriteOffBatchCommandHandler(
    IApplicationDbContext db,
    CastigosDeCartera castigos,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria) : IRequestHandler<SubmitWriteOffBatchCommand, Result<WriteOffBatchDto>>
{
    public async Task<Result<WriteOffBatchDto>> Handle(SubmitWriteOffBatchCommand request, CancellationToken ct)
    {
        var leido = await CastigosDeCartera.LeerAsync(db, alcance, auditoria, request.WriteOffBatchPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<WriteOffBatchDto>(leido.Error);
        var (lote, items) = leido.Value;
        if (await CastigosDeCartera.ConsejoAsync(db, ct) is null) return Result.Failure<WriteOffBatchDto>(ErroresDeCastigo.SinConsejo());
        try
        {
            lote.Enviar();
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<WriteOffBatchDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await castigos.DtoAsync(lote, items, ct));
    }
}

/// <summary>Lo que comparten armar el lote y agregarle un crédito: los créditos del pedido, en el alcance, vigentes y fuera de otro lote abierto. (nuevo)</summary>
internal static class PreparacionDeLotes
{
    public static async Task<Result<IReadOnlyList<Loan>>> CreditosAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        IReadOnlyList<Guid> publicIds, int? loteActual, CancellationToken ct)
    {
        var ids = publicIds.Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.PublicId) && !l.IsDeleted).ToListAsync(ct);
        if (creditos.Count != ids.Count) return Result.Failure<IReadOnlyList<Loan>>(Settlement.LecturaDelCredito.Inexistente());
        foreach (var c in creditos)
        {
            var dentro = await alcance.AsegurarAsync(c.BranchId, c.CreditLineId, Settlement.LecturaDelCredito.Inexistente(), auditoria, "Loan", c.PublicId, ct);
            if (dentro.IsFailure) return Result.Failure<IReadOnlyList<Loan>>(dentro.Error);
            if (c.Status != LoanStatus.Active) return Result.Failure<IReadOnlyList<Loan>>(ErroresDeCastigo.CreditoNoActivo(c.Number, c.Status.ToString()));
        }
        var abiertos = await CastigosDeCartera.EnLotesAbiertosAsync(db, creditos.Select(c => c.Id).ToList(), loteActual, ct);
        if (creditos.FirstOrDefault(c => abiertos.ContainsKey(c.Id)) is { } enOtro)
            return Result.Failure<IReadOnlyList<Loan>>(ErroresDeCastigo.EnOtroLote(enOtro.Number, abiertos[enOtro.Id]));
        // En el orden en que se pidieron.
        return Result.Success<IReadOnlyList<Loan>>(ids.Select(id => creditos.Single(c => c.PublicId == id)).ToList());
    }
}
