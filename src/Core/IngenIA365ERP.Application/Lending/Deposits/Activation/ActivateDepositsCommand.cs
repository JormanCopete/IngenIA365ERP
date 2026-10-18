using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>Lo que responde la activación de aportes o de captaciones (api.md §16 de la 014). (nuevo)</summary>
public sealed record DepositsActivationDto(
    Guid ActivationPublicId,
    string Nature,
    DateOnly ActivationDate,
    int Accounts,
    int OpeningTransactions,
    IReadOnlyList<MensajeEmitidoDto> Messages,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto> Warnings);

/// <summary>
/// <c>POST /api/lending/deposits/activation</c> con <c>nature = Deposit</c> (feature 014, T055; FR-116, FR-117, R21; api.md §16): activa las
/// captaciones. En la fase fundacional sólo en su modo <b>sin carga</b> (cooperativa sin historia): <see cref="MigrationLoadPublicId"/> nulo. La
/// activación con carga se completa con la migración (US9). <see cref="SessionItemPublicId"/> es el acta de las diferencias de una carga: sin carga no
/// hay diferencias y no se pide. (nuevo)
/// </summary>
public sealed record ActivateDepositsCommand(
    DateOnly ActivationDate,
    string Reason,
    Guid? MigrationLoadPublicId = null,
    Guid? SessionItemPublicId = null,
    bool AcknowledgeWarnings = false) : IRequest<Result<DepositsActivationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>Ídem con <c>nature = Contribution</c>: activa los aportes (feature 014, T055). (nuevo)</summary>
public sealed record ActivateContributionsCommand(
    DateOnly ActivationDate,
    string Reason,
    Guid? MigrationLoadPublicId = null,
    Guid? SessionItemPublicId = null,
    bool AcknowledgeWarnings = false) : IRequest<Result<DepositsActivationDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateDepositsCommandValidator : ValidadorConMotivo<ActivateDepositsCommand>
{
    public ActivateDepositsCommandValidator() =>
        RuleFor(x => x.ActivationDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de activación.");
}

public sealed class ActivateContributionsCommandValidator : ValidadorConMotivo<ActivateContributionsCommand>
{
    public ActivateContributionsCommandValidator() =>
        RuleFor(x => x.ActivationDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de activación.");
}

public sealed class ActivateDepositsCommandHandler(ActivacionDeCaptacionesSinCarga activacion)
    : IRequestHandler<ActivateDepositsCommand, Result<DepositsActivationDto>>
{
    public Task<Result<DepositsActivationDto>> Handle(ActivateDepositsCommand request, CancellationToken ct) =>
        activacion.ActivarAsync(LineNature.Deposit, request.MigrationLoadPublicId, request.ActivationDate, request.AcknowledgeWarnings, request.Reason, ct);
}

public sealed class ActivateContributionsCommandHandler(ActivacionDeCaptacionesSinCarga activacion)
    : IRequestHandler<ActivateContributionsCommand, Result<DepositsActivationDto>>
{
    public Task<Result<DepositsActivationDto>> Handle(ActivateContributionsCommand request, CancellationToken ct) =>
        activacion.ActivarAsync(LineNature.Contribution, request.MigrationLoadPublicId, request.ActivationDate, request.AcknowledgeWarnings,
            request.Reason, ct);
}

/// <summary>Los errores de la activación de aportes y captaciones que no tiene la de créditos (feature 014, T055). (nuevo)</summary>
public static class ErroresDeActivacionDeCaptaciones
{
    public const string CodigoCreditosPrimero = "Lending.Activation.LoansFirst";
    public const string CodigoConCargaNoDisponible = "Lending.Activation.LoadNotAvailable";

    public static Error YaActivada(LineNature naturaleza, DateOnly fecha) =>
        new ErrorConDatos(ErroresDeActivacion.CodigoYaActivada,
            string.Create(CultureInfo.InvariantCulture, $"{Nombre(naturaleza)} ya están activados desde el {fecha:yyyy-MM-dd}."),
            new { nature = naturaleza.ToString(), activationDate = fecha });

    public static Error CreditosPrimero(DateOnly? creditos) =>
        new ErrorConDatos(CodigoCreditosPrimero,
            creditos is { } desde
                ? string.Create(CultureInfo.InvariantCulture,
                    $"Los créditos se activaron el {desde:yyyy-MM-dd}: aportes y captaciones se activan ese día o después.")
                : "Active primero los créditos de la Cartera: aportes y captaciones se activan después (los cruces y la consulta de aportes los necesitan).",
            new { loansActivationDate = creditos });

    public static Error FechaFutura(DateOnly fecha, DateOnly hoy) =>
        new ErrorConDatos(ErroresDeActivacion.CodigoCorteInvalido,
            string.Create(CultureInfo.InvariantCulture, $"La activación no puede regir desde una fecha futura ({fecha:yyyy-MM-dd}; hoy es {hoy:yyyy-MM-dd})."),
            new { expected = hoy });

    public static Error ConCargaNoDisponible(LineNature naturaleza) =>
        new ErrorConDatos(CodigoConCargaNoDisponible,
            $"La activación de {Nombre(naturaleza).ToLowerInvariant()} con una carga de migración llega con la migración de captaciones. Hoy sólo se activa sin carga (cooperativa sin historia).",
            new { nature = naturaleza.ToString() });

    private static string Nombre(LineNature naturaleza) => naturaleza == LineNature.Contribution ? "Los aportes" : "Las captaciones";
}

/// <summary>
/// La activación <b>sin carga</b> de aportes o de captaciones (feature 014, T055; FR-116, FR-117, R21): la cooperativa sin historia activa cada
/// naturaleza una vez, después de los créditos, con la misma regla que la activación de créditos —«corte + 1» es la fecha elegida (el corte es la
/// víspera), requisitos y mes contable—. Fuera de la transacción revisa, en este orden: la entrega (<c>Lending.Savings.DeliveryNotAvailable</c>), ya
/// activada (<c>Lending.Activation.AlreadyActivated</c>), créditos primero (<c>Lending.Activation.LoansFirst</c>), sin carga
/// (<c>Lending.Activation.LoadNotAvailable</c> hasta US9), fecha no futura (<c>.CutoffInvalid</c>) y los requisitos de
/// <see cref="RequisitosDeActivacionDeCaptaciones"/> (<c>.RequirementsMissing</c>). En una <see cref="TransaccionExplicita"/>, con <c>LND_Setup</c>
/// exclusivo, lo repite y escribe la fila de <c>LND_Activations</c> con su <c>Nature</c> (sin carga y sin acta), el informativo
/// <c>CaptacionesActivadas</c> y la fecha de la naturaleza en <c>LND_Setup</c>, que es lo que cambia el contrato de cuentas y la consulta de aportes a
/// «habilitados» (las implementaciones reales, T083 y T084, leen esa fecha). Sin carga no nace ninguna cuenta ni transacción de saldo inicial, y la
/// activación no emite hechos contables (FR-114). Audita <c>Lending.Activation.Registered</c>. (nuevo)
/// </summary>
public sealed class ActivacionDeCaptacionesSinCarga(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    RequisitosDeActivacionDeCaptaciones requisitos,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    LendingAuditEmitter? auditoria = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<DepositsActivationDto>> ActivarAsync(
        LineNature naturaleza, Guid? cargaPublicId, DateOnly fecha, bool reconoceAvisos, string motivo, CancellationToken ct)
    {
        if (naturaleza is not (LineNature.Deposit or LineNature.Contribution))
            throw new ArgumentOutOfRangeException(nameof(naturaleza), naturaleza, "Sólo se activan aportes o captaciones.");
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DepositsActivationDto>(ErroresDeActivacion.SinUsuario());

        var previa = await ValidarAsync(naturaleza, cargaPublicId, fecha, ct);
        if (previa.IsFailure) return Result.Failure<DepositsActivationDto>(previa.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: true, ct);
            var setup = await db.LendingSetups.FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct)
                ?? throw new InvalidOperationException("Falta la fila única de LND_Setup: la migración CarteraNucleo no se aplicó.");
            var validado = await ValidarAsync(naturaleza, cargaPublicId, fecha, ct);
            if (validado.IsFailure) return Result.Failure<DepositsActivationDto>(validado.Error);
            var evaluacion = validado.Value;

            var ahora = reloj.UtcNow;
            var activacionPublicId = Guid.NewGuid();
            var mensaje = await emision.ActivacionDeCaptacionesAsync(activacionPublicId, naturaleza, fecha, null, cuentas: 0, transaccionesIniciales: 0, ct);
            if (mensaje.IsFailure) return Result.Failure<DepositsActivationDto>(mensaje.Error);

            var activacion = new LendingActivation
            {
                PublicId = activacionPublicId,
                Nature = naturaleza,
                MigrationLoadId = null,
                CutoffDate = fecha.AddDays(-1),
                ActivationDate = fecha,
                RegisteredByUserId = usuario,
                RegisteredAt = ahora,
                SessionItemId = null,
                CheckVersion = 0,
                BalanceCheckSha256 = ApplyMigrationLoadCommandHandler.HuellaVacia,
                SalesReconciliationSha256 = ApplyMigrationLoadCommandHandler.HuellaVacia,
                IsProductionEnvironment = requisitos.EsProduccion,
                RequirementsJson = JsonSerializer.Serialize(evaluacion.Requisitos.Select(r => new { r.Code, r.Status, r.Met, r.Detail }), Json),
                CountsJson = JsonSerializer.Serialize(new { accounts = 0, openingTransactions = 0, withoutLoad = true }, Json),
                CommercialAdjustmentsJson = JsonSerializer.Serialize(new
                {
                    warnings = evaluacion.Avisos.Select(a => a.Code), acknowledged = reconoceAvisos, reason = motivo.Trim(),
                }, Json),
                ActivatedMessagePublicId = mensaje.Value.PublicId,
            };
            db.LendingActivations.Add(activacion);
            await db.SaveChangesAsync(ct);

            if (naturaleza == LineNature.Deposit)
            {
                setup.DepositsActivationDate = fecha;
                setup.DepositsActivationId = activacion.Id;
                setup.DepositsActivatedAt = ahora;
            }
            else
            {
                setup.ContributionsActivationDate = fecha;
                setup.ContributionsActivationId = activacion.Id;
                setup.ContributionsActivatedAt = ahora;
            }
            await db.SaveChangesAsync(ct);

            if (auditoria is not null)
                await auditoria.EmitirActivacionRegistradaAsync(activacion.PublicId, new
                {
                    nature = naturaleza.ToString(), activationDate = fecha, withoutLoad = true, isProduction = activacion.IsProductionEnvironment,
                    requirements = evaluacion.Requisitos.Select(r => new { r.Code, r.Status }), reason = motivo.Trim(),
                }, ct);

            return Result.Success(new DepositsActivationDto(activacion.PublicId, naturaleza.ToString(), fecha, 0, 0,
                [new MensajeEmitidoDto(mensaje.Value.Type, mensaje.Value.PublicId)], evaluacion.Requisitos, evaluacion.Avisos));
        }, ct);
    }

    /// <summary>Entrega, activada, créditos primero, sin carga, fecha y requisitos, en ese orden.</summary>
    private async Task<Result<EvaluacionDeActivacion>> ValidarAsync(LineNature naturaleza, Guid? cargaPublicId, DateOnly fecha, CancellationToken ct)
    {
        if (requisitos.EntregaNoDisponible(naturaleza) is { } entrega) return Result.Failure<EvaluacionDeActivacion>(entrega);

        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        if (fechas.De(naturaleza) is { } activada) return Result.Failure<EvaluacionDeActivacion>(ErroresDeActivacionDeCaptaciones.YaActivada(naturaleza, activada));
        if (fechas.Creditos is not { } creditos || creditos > fecha)
            return Result.Failure<EvaluacionDeActivacion>(ErroresDeActivacionDeCaptaciones.CreditosPrimero(fechas.Creditos));
        if (cargaPublicId is not null) return Result.Failure<EvaluacionDeActivacion>(ErroresDeActivacionDeCaptaciones.ConCargaNoDisponible(naturaleza));
        var hoy = reloj.HoyLocal;
        if (fecha > hoy) return Result.Failure<EvaluacionDeActivacion>(ErroresDeActivacionDeCaptaciones.FechaFutura(fecha, hoy));

        var evaluacion = await requisitos.EvaluarAsync(naturaleza, fecha, hoy, ct);
        var faltantes = evaluacion.Faltantes;
        return faltantes.Count > 0
            ? Result.Failure<EvaluacionDeActivacion>(ErroresDeActivacion.FaltanRequisitos(faltantes))
            : Result.Success(evaluacion);
    }
}
