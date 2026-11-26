using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

// 2026-11-25 (pedido del dueño; feature 026): la anulación y las consultas de los cargos masivos. (nuevos)

// ------------------------------------------------------------------------------------------------------- anular la corrida --

/// <summary>Anula lo vivo de una corrida terminada (<c>POST /{id}/void</c>, <c>Lending.BulkCharges.Void</c>, 202): en segundo plano, un comprobante.</summary>
public sealed record VoidBulkChargeRunCommand(Guid RunPublicId, string Reason) : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidBulkChargeRunCommandValidator : AbstractValidator<VoidBulkChargeRunCommand>
{
    public VoidBulkChargeRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo de la anulación.").MaximumLength(500);
    }
}

public sealed class VoidBulkChargeRunCommandHandler(
    IApplicationDbContext db,
    EntradaDeCargoMasivo entrada,
    CorridasDeCargoMasivo corridas,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidBulkChargeRunCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(VoidBulkChargeRunCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BulkChargeRunDto>(ErroresDeGobierno.SinUsuario());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var run = await entrada.LeerAsync(request.RunPublicId, ct);
            if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
            if (run.Status != BulkChargeRunStatus.Completed)
                return Result.Failure<BulkChargeRunDto>(new Error(ErroresDeCargoMasivo.CodigoEstadoInvalido,
                    "Sólo se anula una corrida terminada (con el borrador, descártelo)."));
            var ahora = reloj.UtcNow;
            var corrida = new LendingProcessRun
            {
                Kind = LendingProcessKind.BulkChargeVoid,
                ProcessDate = reloj.HoyLocal,
                Status = LendingProcessRunStatus.Requested,
                RequestedByKind = ActorKind.Person,
                RequestedByUserId = usuario,
                RequestedByCentralUserId = actor.CentralUserId,
                RequestedByName = actor.Name.Length > 150 ? actor.Name[..150] : actor.Name,
                ExecutionOrigin = actor.UserPublicId is { } u ? $"Orden:{u:D}" : $"AnulacionDeCargoMasivo:{run.Number}",
                Reason = request.Reason.Trim(),
                TotalLoans = run.CreatedCount,
                RequestedAt = ahora,
            };
            db.LendingProcessRuns.Add(corrida);
            await db.SaveChangesAsync(ct);
            run.IniciarAnulacion(request.Reason, usuario, corrida.Id, ahora);
            await db.SaveChangesAsync(ct);
            return Result.Success(await corridas.DtoAsync(run, null, ct));
        }, ct);
    }
}

// ------------------------------------------------------------------------------------------------------- anular un cargo --

/// <summary>Anula el cargo de un asociado (<c>POST /{id}/lines/{lineId}/void</c>, <c>Lending.BulkCharges.Void</c>): en línea, su propio comprobante.</summary>
public sealed record VoidBulkChargeLineCommand(Guid RunPublicId, Guid LinePublicId, string Reason)
    : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidBulkChargeLineCommandValidator : AbstractValidator<VoidBulkChargeLineCommand>
{
    public VoidBulkChargeLineCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo de la anulación.").MaximumLength(500);
    }
}

public sealed class VoidBulkChargeLineCommandHandler(
    IApplicationDbContext db,
    EntradaDeCargoMasivo entrada,
    CorridasDeCargoMasivo corridas,
    AnulacionDeCargoMasivo anulacion,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidBulkChargeLineCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(VoidBulkChargeLineCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BulkChargeRunDto>(ErroresDeGobierno.SinUsuario());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var run = await entrada.LeerAsync(request.RunPublicId, ct);
            if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
            if (run.Status != BulkChargeRunStatus.Completed)
                return Result.Failure<BulkChargeRunDto>(new Error(ErroresDeCargoMasivo.CodigoEstadoInvalido,
                    "Sólo se anula el cargo de una corrida terminada que no se está anulando."));
            var linea = await db.BulkChargeLines.FirstOrDefaultAsync(l => l.PublicId == request.LinePublicId && l.RunId == run.Id && !l.IsDeleted, ct);
            if (linea is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.LineaDeCorridaInexistente());
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([linea.PersonId], ModoDeEspera.Esperar, ct);
            var contable = await fechaContable.CalcularAsync(reloj.HoyLocal, ct);
            if (contable.IsFailure) return Result.Failure<BulkChargeRunDto>(contable.Error);
            var alcance = EmisionDeCartera.AlcanceDeCargoMasivo(run.PublicId, $"linea:{linea.PublicId:N}");
            var modo = await emision.ModoDeCargoMasivoAsync(CargoMasivoAnuladoV1.Type, "CMA", contable.Value, alcance, ct);
            if (modo.IsFailure) return Result.Failure<BulkChargeRunDto>(modo.Error);
            var anulado = await anulacion.AnularAsync(run, linea, request.Reason.Trim(), usuario, actor.CentralUserId, modo.Value, ct);
            if (anulado.IsFailure) return Result.Failure<BulkChargeRunDto>(anulado.Error);

            // Su comprobante: el lote con la entrega de este alcance.
            var entregas = await db.IntegrationMessageDeliveries
                .Where(x => x.Destination == IntegrationDestinations.Accounting && x.Status == DeliveryStatus.InBatch && x.BatchId == null
                            && x.BatchScopeKey == alcance).ToListAsync(ct);
            if (entregas.Count > 0)
            {
                var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct),
                    IntegrationDestinations.Accounting, BatchTrigger.Manual, actor, $"Anulación de un cargo masivo de {run.Number}",
                    reloj.UtcNow);
                lote.MessageCount = entregas.Count;
                lote.CutoffMessageId = entregas.Max(e => e.MessageId);
                lote.DateFrom = contable.Value;
                lote.DateTo = contable.Value;
                db.IntegrationBatches.Add(lote);
                await db.SaveChangesAsync(ct);
                LotesDeIntegracion.Asignar(lote, entregas);
            }
            var vivos = await db.BulkChargeLines.AsNoTracking().Where(l => l.RunId == run.Id && !l.IsDeleted)
                .GroupBy(l => l.Status).Select(g => new { g.Key, Cuantas = g.Count(), Total = g.Sum(x => x.Amount) }).ToListAsync(ct);
            run.Recontar(vivos.Where(v => v.Key == BulkChargeLineStatus.Pending).Sum(v => v.Cuantas),
                vivos.Where(v => v.Key == BulkChargeLineStatus.Created).Sum(v => v.Cuantas),
                vivos.Where(v => v.Key == BulkChargeLineStatus.Skipped).Sum(v => v.Cuantas),
                vivos.Where(v => v.Key == BulkChargeLineStatus.Failed).Sum(v => v.Cuantas),
                vivos.Where(v => v.Key == BulkChargeLineStatus.Voided).Sum(v => v.Cuantas),
                vivos.Where(v => v.Key == BulkChargeLineStatus.Created).Sum(v => v.Total));
            await db.SaveChangesAsync(ct);
            await auditoria.EmitirCargoMasivoAsync(AuditEventTypes.LendingBulkChargeLineVoided, run.PublicId,
                new { run.Number, line = linea.PublicId, reason = request.Reason.Trim() }, ct);
            return Result.Success(await corridas.DtoAsync(run, null, ct));
        }, ct);
    }
}

// --------------------------------------------------------------------------------------------------------------- consultas --

/// <summary>Las líneas que admiten cargos masivos hoy, con sus valores por defecto, y si la cooperativa exige otra persona para confirmar.</summary>
public sealed record GetBulkChargeOptionsQuery : IRequest<Result<BulkChargeOptionsDto>>;

public sealed class GetBulkChargeOptionsQueryHandler(IApplicationDbContext db, VersionVigenteDeLinea versiones, ILectorDeParametros parametros,
    IDateTimeService reloj) : IRequestHandler<GetBulkChargeOptionsQuery, Result<BulkChargeOptionsDto>>
{
    public async Task<Result<BulkChargeOptionsDto>> Handle(GetBulkChargeOptionsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var lineas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Nature == LineNature.Placement).OrderBy(l => l.Code).ToListAsync(ct);
        var leidas = await versiones.LeerVariasAsync(lineas.Select(l => l.Id).ToList(), hoy, ct: ct);
        var opciones = new List<BulkChargeLineOptionDto>();
        foreach (var l in lineas)
        {
            if (!leidas.TryGetValue(l.Id, out var leida) || !leida.Version.AllowsBulkCharge) continue;
            var v = leida.Version;
            opciones.Add(new BulkChargeLineOptionDto(l.PublicId, l.Code, l.Name, v.MinAmount, v.MaxAmount, v.BulkChargeDefaultAmount,
                v.BulkChargeDefaultInstallments ?? 1, v.MaxInstallments, v.DefaultPeriodicity, v.LeerPeriodicidades(), v.DefaultRepaymentChannel,
                v.LeerFormasDePago(), v.BulkChargeReportsToBureaus, v.BulkChargeEntersCollection));
        }
        var segregar = await parametros.LeerComoAsync<bool>(ParametrosDeCartera.Modulo, ParametrosDeCartera.SegregacionCargoMasivoPorOtraPersona, hoy, ct: ct);
        return Result.Success(new BulkChargeOptionsDto(opciones, segregar is { IsSuccess: true, Value: true }));
    }
}

/// <summary>La lista de corridas, de la más reciente a la más vieja, con filtros por línea, período y estado.</summary>
public sealed record ListBulkChargeRunsQuery(Guid? CreditLinePublicId = null, int? Period = null, BulkChargeRunStatus? Status = null, int Page = 1,
    int PageSize = 50) : IRequest<Result<IReadOnlyList<BulkChargeRunSummaryDto>>>;

public sealed class ListBulkChargeRunsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListBulkChargeRunsQuery, Result<IReadOnlyList<BulkChargeRunSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<BulkChargeRunSummaryDto>>> Handle(ListBulkChargeRunsQuery request, CancellationToken ct)
    {
        var consulta = from r in db.BulkChargeRuns.AsNoTracking()
                       join l in db.CreditLines.AsNoTracking() on r.CreditLineId equals l.Id
                       where !r.IsDeleted
                       select new { r, l };
        if (request.CreditLinePublicId is { } linea) consulta = consulta.Where(x => x.l.PublicId == linea);
        if (request.Period is { } periodo) consulta = consulta.Where(x => x.r.Period == periodo);
        if (request.Status is { } estado) consulta = consulta.Where(x => x.r.Status == estado);
        var tam = Math.Clamp(request.PageSize, 1, 200);
        var pagina = Math.Max(1, request.Page);
        var filas = await consulta.OrderByDescending(x => x.r.Id).Skip((pagina - 1) * tam).Take(tam)
            .Select(x => new BulkChargeRunSummaryDto(x.r.PublicId, x.r.Number, x.r.Status, x.l.Code, x.l.Name, x.r.Period, x.r.ChargeDate, x.r.CreatedCount,
                x.r.SkippedCount, x.r.FailedCount, x.r.VoidedCount, x.r.PendingCount, x.r.TotalAmount, x.r.CreatedAt))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<BulkChargeRunSummaryDto>>(filas);
    }
}

/// <summary>El detalle de una corrida; en borrador, con la evaluación contable al día.</summary>
public sealed record GetBulkChargeRunQuery(Guid RunPublicId) : IRequest<Result<BulkChargeRunDto>>;

public sealed class GetBulkChargeRunQueryHandler(EntradaDeCargoMasivo entrada, CorridasDeCargoMasivo corridas)
    : IRequestHandler<GetBulkChargeRunQuery, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(GetBulkChargeRunQuery request, CancellationToken ct)
    {
        var run = await entrada.LeerAsync(request.RunPublicId, ct);
        if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
        DecisionDeValidacionPrevia? contable = null;
        if (run.EsBorrador && run.PendingCount > 0)
        {
            var calculo = await corridas.CalcularAsync(run, ct);
            if (calculo.IsSuccess) contable = calculo.Value.Contable;
        }
        return Result.Success(await corridas.DtoAsync(run, contable, ct));
    }
}

/// <summary>Las líneas de una corrida, por página, con filtro por estado.</summary>
public sealed record ListBulkChargeLinesQuery(Guid RunPublicId, BulkChargeLineStatus? Status = null, int Page = 1, int PageSize = 100)
    : IRequest<Result<BulkChargeLinesPageDto>>;

public sealed class ListBulkChargeLinesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListBulkChargeLinesQuery, Result<BulkChargeLinesPageDto>>
{
    public async Task<Result<BulkChargeLinesPageDto>> Handle(ListBulkChargeLinesQuery request, CancellationToken ct)
    {
        var runId = await db.BulkChargeRuns.AsNoTracking().Where(r => r.PublicId == request.RunPublicId && !r.IsDeleted).Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);
        if (runId is not { } id) return Result.Failure<BulkChargeLinesPageDto>(ErroresDeCargoMasivo.Inexistente());
        var consulta = from l in db.BulkChargeLines.AsNoTracking()
                       join p in db.People.AsNoTracking() on l.PersonId equals p.Id
                       join b in db.Branches.AsNoTracking() on l.BranchId equals b.Id into bs
                       from b in bs.DefaultIfEmpty()
                       join c in db.Loans.AsNoTracking() on l.LoanId equals c.Id into cs
                       from c in cs.DefaultIfEmpty()
                       where l.RunId == id && !l.IsDeleted
                       select new { l, p, Agencia = b == null ? null : b.Name, Credito = c };
        if (request.Status is { } estado) consulta = consulta.Where(x => x.l.Status == estado);
        var total = await consulta.CountAsync(ct);
        var tam = Math.Clamp(request.PageSize, 1, 500);
        var pagina = Math.Max(1, request.Page);
        var filas = await consulta.OrderBy(x => x.p.LastName).ThenBy(x => x.p.FirstName).ThenBy(x => x.l.Id).Skip((pagina - 1) * tam).Take(tam)
            .Select(x => new BulkChargeLineDto(x.l.PublicId, x.p.PublicId, x.p.TaxId,
                x.p.BusinessName ?? (x.p.FirstName + " " + x.p.LastName), x.Agencia, x.l.Amount, x.l.Installments, x.l.FirstDueDate, x.l.RepaymentChannel,
                x.l.Status, x.l.Reason, x.l.ReasonDetail, x.l.Warning, x.Credito == null ? null : x.Credito.PublicId,
                x.Credito == null ? null : x.Credito.Number, x.l.SourceRow, x.l.ProcessedAt))
            .ToListAsync(ct);
        return Result.Success(new BulkChargeLinesPageDto(filas, total, pagina, tam));
    }
}
