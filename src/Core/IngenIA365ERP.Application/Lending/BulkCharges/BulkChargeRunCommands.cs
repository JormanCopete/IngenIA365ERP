using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

// 2026-11-25 (pedido del dueño; feature 026): los comandos de la corrida de cargos masivos (/api/lending/bulk-charges). Toda escritura lleva
// Idempotency-Key. (nuevos)

/// <summary>Lo común de los comandos: leer la corrida, poner los datos del cargo en el borrador y saber si Cartera está activa.</summary>
public sealed class EntradaDeCargoMasivo(IApplicationDbContext db, VersionVigenteDeLinea versiones)
{
    public async Task<BulkChargeRun?> LeerAsync(Guid publicId, CancellationToken ct) =>
        await db.BulkChargeRuns.FirstOrDefaultAsync(r => r.PublicId == publicId && !r.IsDeleted, ct);

    public Task<bool> CarteraActivadaAsync(CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    /// <summary>
    /// Pone los datos en el borrador con los valores por defecto de la versión de la línea vigente a la fecha del cargo: valor, cuotas, periodicidad y
    /// forma de pago. Cambiar de origen (filtros ↔ plano) descarta las filas del plano.
    /// </summary>
    public async Task<Result> AplicarAsync(BulkChargeRun run, BulkChargeRunInput e, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.PublicId == e.CreditLinePublicId && !l.IsDeleted)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
        if (linea is not { } lineaId) return Result.Failure(ErroresDeCargoMasivo.LineaInexistente());
        var version = await versiones.LeerAsync(lineaId, e.ChargeDate, ct: ct);
        if (version is null || !version.Version.AllowsBulkCharge)
        {
            var codigo = await db.CreditLines.AsNoTracking().Where(l => l.Id == lineaId).Select(l => l.Code).FirstAsync(ct);
            return Result.Failure(ErroresDeCargoMasivo.LineaNoHabilitada(codigo, e.ChargeDate));
        }
        var v = version.Version;
        var valor = e.Amount ?? v.BulkChargeDefaultAmount;
        if (valor is not { } monto) return Result.Failure(ErroresDeCargoMasivo.FueraDeRango("Diga el valor del cargo: la línea no tiene un valor por defecto."));
        int? contraparte = null;
        if (e.CounterpartMode == BulkChargeCounterpart.Person)
        {
            contraparte = e.CounterpartPersonPublicId is { } p
                ? await db.People.AsNoTracking().Where(x => x.PublicId == p && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
                : null;
            if (contraparte is null) return Result.Failure(ErroresDeCargoMasivo.PersonaInexistente());
        }

        var origenAnterior = OrigenDeCargoMasivo.Leer(run.FiltersJson);
        run.CreditLineId = lineaId;
        run.CreditLineVersionId = v.Id;
        run.Period = e.Period;
        run.ChargeDate = e.ChargeDate;
        run.FirstDueDate = e.FirstDueDate;
        run.Amount = monto;
        run.Installments = e.Installments ?? v.BulkChargeDefaultInstallments ?? 1;
        run.Periodicity = e.Periodicity ?? v.DefaultPeriodicity;
        run.ChannelMode = e.ChannelMode;
        run.RepaymentChannel = e.ChannelMode == BulkChargeChannelMode.Fixed ? e.RepaymentChannel ?? v.DefaultRepaymentChannel : null;
        run.PaymentCycle = e.PaymentCycle;
        run.CounterpartMode = e.CounterpartMode;
        run.CounterpartPersonId = contraparte;
        run.FiltersJson = (e.Source == BulkChargeSource.File
            ? new OrigenDeCargoMasivo(null, run.Source == BulkChargeSource.File ? origenAnterior.Rows ?? [] : [])
            : new OrigenDeCargoMasivo(e.Filters ?? new BulkChargeFiltersInput(), null)).Escribir();
        run.Source = e.Source;
        return Result.Success();
    }
}

public sealed class BulkChargeRunInputValidator : AbstractValidator<BulkChargeRunInput>
{
    public BulkChargeRunInputValidator()
    {
        RuleFor(x => x.CreditLinePublicId).NotEmpty().WithMessage("Elija la línea de crédito.");
        RuleFor(x => x.Period).Must(Domain.Lending.BulkCharges.ReglasDeCargoMasivo.PeriodoValido)
            .WithMessage("El período es el año y el mes del cargo (aaaamm).");
        RuleFor(x => x.FirstDueDate).GreaterThan(x => x.ChargeDate).WithMessage("La fecha del primer pago debe ser posterior a la fecha del cargo.");
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null).WithMessage("El valor del cargo debe ser mayor que cero.");
        RuleFor(x => x.Installments).GreaterThanOrEqualTo(1).When(x => x.Installments is not null).WithMessage("El plazo es de al menos una cuota.");
        RuleFor(x => x.ChannelMode).IsInEnum();
        RuleFor(x => x.CounterpartMode).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.CounterpartPersonPublicId).NotNull().When(x => x.CounterpartMode == BulkChargeCounterpart.Person)
            .WithMessage("Elija la persona (proveedor) de la contrapartida.");
    }
}

// ------------------------------------------------------------------------------------------------------------------ crear --

/// <summary>Arma el borrador de una corrida y calcula su vista previa (<c>POST /api/lending/bulk-charges</c>, <c>Lending.BulkCharges.Run</c>, 201).</summary>
public sealed record CreateBulkChargeRunCommand(BulkChargeRunInput Input) : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateBulkChargeRunCommandValidator : AbstractValidator<CreateBulkChargeRunCommand>
{
    public CreateBulkChargeRunCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new BulkChargeRunInputValidator());
}

public sealed class CreateBulkChargeRunCommandHandler(
    IApplicationDbContext db,
    EntradaDeCargoMasivo entrada,
    CorridasDeCargoMasivo corridas,
    NumeradorDeCartera numerador,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateBulkChargeRunCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(CreateBulkChargeRunCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BulkChargeRunDto>(ErroresDeGobierno.SinUsuario());
        if (!await entrada.CarteraActivadaAsync(ct)) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.CarteraSinActivar());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await numerador.SerializarCorridasDeCargosAsync(ct);
            var run = new BulkChargeRun { Number = await numerador.NumeroDeCorridaDeCargosAsync(ct), PreparedByUserId = usuario };
            var aplicado = await entrada.AplicarAsync(run, request.Input, ct);
            if (aplicado.IsFailure) return Result.Failure<BulkChargeRunDto>(aplicado.Error);
            var calculo = await corridas.CalcularAsync(run, ct);
            if (calculo.IsFailure) return Result.Failure<BulkChargeRunDto>(calculo.Error);
            db.BulkChargeRuns.Add(run);
            await db.SaveChangesAsync(ct);
            await corridas.AplicarAsync(run, calculo.Value, reloj.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await corridas.DtoAsync(run, calculo.Value.Contable, ct));
        }, ct);
    }
}

// ------------------------------------------------------------------------------------------------------- cambiar y recalcular --

/// <summary>Cambia los datos del borrador y recalcula (<c>PUT /{id}</c>) o, sin datos, sólo recalcula (<c>POST /{id}/preview</c>).</summary>
public sealed record UpdateBulkChargeRunCommand(Guid RunPublicId, BulkChargeRunInput? Input) : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateBulkChargeRunCommandValidator : AbstractValidator<UpdateBulkChargeRunCommand>
{
    public UpdateBulkChargeRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.Input!).SetValidator(new BulkChargeRunInputValidator()).When(x => x.Input is not null);
    }
}

public sealed class UpdateBulkChargeRunCommandHandler(
    IApplicationDbContext db,
    EntradaDeCargoMasivo entrada,
    CorridasDeCargoMasivo corridas,
    NumeradorDeCartera numerador,
    IDateTimeService reloj) : IRequestHandler<UpdateBulkChargeRunCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(UpdateBulkChargeRunCommand request, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await numerador.SerializarCorridasDeCargosAsync(ct);
            var run = await entrada.LeerAsync(request.RunPublicId, ct);
            if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
            if (run.ExigirBorrador() is { } noBorrador) return Result.Failure<BulkChargeRunDto>(new Error(noBorrador.Codigo, noBorrador.Mensaje));
            if (request.Input is not null)
            {
                var aplicado = await entrada.AplicarAsync(run, request.Input, ct);
                if (aplicado.IsFailure) return Result.Failure<BulkChargeRunDto>(aplicado.Error);
            }
            var calculo = await corridas.CalcularAsync(run, ct);
            if (calculo.IsFailure) return Result.Failure<BulkChargeRunDto>(calculo.Error);
            await corridas.AplicarAsync(run, calculo.Value, reloj.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await corridas.DtoAsync(run, calculo.Value.Contable, ct));
        }, ct);
}

// ---------------------------------------------------------------------------------------------------------------- descartar --

/// <summary>Descarta el borrador (<c>POST /{id}/discard</c>): sus líneas quedan de baja y no reservan nada.</summary>
public sealed record DiscardBulkChargeRunCommand(Guid RunPublicId) : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DiscardBulkChargeRunCommandHandler(IApplicationDbContext db, EntradaDeCargoMasivo entrada, CorridasDeCargoMasivo corridas,
    IDateTimeService reloj) : IRequestHandler<DiscardBulkChargeRunCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(DiscardBulkChargeRunCommand request, CancellationToken ct)
    {
        var run = await entrada.LeerAsync(request.RunPublicId, ct);
        if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
        if (run.ExigirBorrador() is { } noBorrador) return Result.Failure<BulkChargeRunDto>(new Error(noBorrador.Codigo, noBorrador.Mensaje));
        run.Descartar();
        foreach (var l in await db.BulkChargeLines.Where(l => l.RunId == run.Id && !l.IsDeleted).ToListAsync(ct))
        {
            l.IsDeleted = true;
            l.DeletedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await corridas.DtoAsync(run, null, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------------- confirmar --

/// <summary>
/// Confirma la corrida (<c>POST /{id}/confirm</c>, <c>Lending.BulkCharges.Run</c>, 202) con la huella de la vista previa que vio la persona: vuelve a
/// calcular a la fecha del cargo y, si algo cambió, responde <c>PreviewChanged</c>; con la segregación encendida, quien la preparó no la confirma; con la
/// regla contable faltante y <c>Contabilidad.FaltaDeRegla</c> en «Bloquear», no se confirma. Confirmada, cada línea pendiente reserva el período del
/// asociado y la corrida queda pedida en segundo plano (<c>LND_ProcessRuns</c>, clase <c>BulkCharge</c>).
/// </summary>
public sealed record ConfirmBulkChargeRunCommand(Guid RunPublicId, string ExpectedPreviewSha256) : IRequest<Result<BulkChargeRunDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmBulkChargeRunCommandValidator : AbstractValidator<ConfirmBulkChargeRunCommand>
{
    public ConfirmBulkChargeRunCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.ExpectedPreviewSha256).NotEmpty().WithMessage("Confirme la vista previa que revisó.");
    }
}

public sealed class ConfirmBulkChargeRunCommandHandler(
    IApplicationDbContext db,
    EntradaDeCargoMasivo entrada,
    CorridasDeCargoMasivo corridas,
    NumeradorDeCartera numerador,
    ILectorDeParametros parametros,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ConfirmBulkChargeRunCommand, Result<BulkChargeRunDto>>
{
    public async Task<Result<BulkChargeRunDto>> Handle(ConfirmBulkChargeRunCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BulkChargeRunDto>(ErroresDeGobierno.SinUsuario());
        if (!await entrada.CarteraActivadaAsync(ct)) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.CarteraSinActivar());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await numerador.SerializarCorridasDeCargosAsync(ct);
            var run = await entrada.LeerAsync(request.RunPublicId, ct);
            if (run is null) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.Inexistente());
            if (run.ExigirBorrador() is { } noBorrador) return Result.Failure<BulkChargeRunDto>(new Error(noBorrador.Codigo, noBorrador.Mensaje));
            var segregar = await parametros.LeerComoAsync<bool>(ParametrosDeCartera.Modulo, ParametrosDeCartera.SegregacionCargoMasivoPorOtraPersona,
                reloj.HoyLocal, ct: ct);
            if (segregar is { IsSuccess: true, Value: true } && run.PreparedByUserId == usuario)
                return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.MismaPersona());

            var calculo = await corridas.CalcularAsync(run, ct);
            if (calculo.IsFailure) return Result.Failure<BulkChargeRunDto>(calculo.Error);
            if (!string.Equals(calculo.Value.Huella, request.ExpectedPreviewSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(calculo.Value.Huella, run.PreviewSha256, StringComparison.OrdinalIgnoreCase))
                return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.VistaCambiada());
            var pendientes = await db.BulkChargeLines.Where(l => l.RunId == run.Id && !l.IsDeleted && l.Status == BulkChargeLineStatus.Pending)
                .ToListAsync(ct);
            if (pendientes.Count == 0) return Result.Failure<BulkChargeRunDto>(ErroresDeCargoMasivo.NadaQueCargar());
            if (calculo.Value.Contable?.Bloqueo is { } bloqueo) return Result.Failure<BulkChargeRunDto>(bloqueo);

            foreach (var l in pendientes) l.Reservar();
            var ahora = reloj.UtcNow;
            var corrida = new LendingProcessRun
            {
                Kind = LendingProcessKind.BulkCharge,
                ProcessDate = reloj.HoyLocal,
                Status = LendingProcessRunStatus.Requested,
                RequestedByKind = ActorKind.Person,
                RequestedByUserId = usuario,
                RequestedByCentralUserId = actor.CentralUserId,
                RequestedByName = Recortar(actor.Name, 150),
                RequestedByIp = Recortar(actor.Ip, 45),
                ExecutionOrigin = actor.UserPublicId is { } u ? $"Orden:{u:D}" : $"CargoMasivo:{run.Number}",
                Reason = Recortar($"Cargos masivos {run.Number}", 500),
                TotalLoans = pendientes.Count,
                RequestedAt = ahora,
            };
            db.LendingProcessRuns.Add(corrida);
            await db.SaveChangesAsync(ct);
            run.Confirmar(usuario, corrida.Id, ahora);
            await db.SaveChangesAsync(ct);
            await auditoria.EmitirCargoMasivoAsync(AuditEventTypes.LendingBulkChargeConfirmed, run.PublicId, new
            {
                run.Number, run.Period, run.ChargeDate, run.Amount, run.Installments, lines = pendientes.Count, total = pendientes.Sum(l => l.Amount),
            }, ct);
            return Result.Success(await corridas.DtoAsync(run, calculo.Value.Contable, ct));
        }, ct);
    }

    private static string? Recortar(string? texto, int largo) => texto is null ? null : texto.Length > largo ? texto[..largo] : texto;
}
