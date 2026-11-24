using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): los comandos del proceso de causación (/api/lending/accrual-processes). Toda escritura lleva
// Idempotency-Key; los que piden motivo lo dejan en la auditoría del comando (IConMotivo). (nuevos)

/// <summary>
/// Lo que se pide al armar o recalcular un proceso: periodicidad, quincena o década, rango de días causados, conceptos y los filtros opcionales (línea,
/// oficina y pagaduría por su <c>PublicId</c>). (nuevo)
/// </summary>
public sealed record AccrualProcessInput(
    AccrualProcessPeriodicity Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<AccrualProcessConcepts> Concepts,
    Guid? CreditLinePublicId = null,
    Guid? BranchPublicId = null,
    Guid? PaymasterPersonPublicId = null,
    AccrualPaymentForm? PaymentForm = null,
    AccrualInstallmentScope? InstallmentScope = null,
    AccrualDiscountCycle? DiscountCycle = null)
{
    /// <summary>2026-11-24 (feature 025, fase 5, FR-018): ¿trae la forma de pago, las cuotas o el ciclo de descuento? (nuevo)</summary>
    public bool ConFiltrosDeCausacion => PaymentForm is not null || InstallmentScope is not null || DiscountCycle is not null;
}

/// <summary>Las reglas de forma de una entrada (el validador de los dos comandos que la reciben). (nuevo)</summary>
public sealed class AccrualProcessInputValidator : AbstractValidator<AccrualProcessInput>
{
    public AccrualProcessInputValidator()
    {
        RuleFor(x => x.Periodicity).IsInEnum().WithMessage("Elija la periodicidad: mensual, quincenal, decadal, semanal o diaria.");
        RuleFor(x => x.SubPeriod).InclusiveBetween((byte)1, (byte)3).When(x => x.SubPeriod is not null)
            .WithMessage("La quincena es 1 o 2 y la década, de 1 a 3.");
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.Concepts).NotEmpty().WithMessage("Elija al menos un concepto.")
            .Must(c => c is null || c.Distinct().Count() == c.Count).WithMessage("Cada concepto va una sola vez.");
        RuleForEach(x => x.Concepts).Must(c => ConceptosDeCausacion.Todos.Any(t => t.Concepto == c))
            .WithMessage("Concepto desconocido: interés corriente, de mora, seguro, administración u otros rubros.");
        RuleFor(x => x.CreditLinePublicId).NotEqual(Guid.Empty).When(x => x.CreditLinePublicId is not null);
        RuleFor(x => x.BranchPublicId).NotEqual(Guid.Empty).When(x => x.BranchPublicId is not null);
        RuleFor(x => x.PaymasterPersonPublicId).NotEqual(Guid.Empty).When(x => x.PaymasterPersonPublicId is not null);
        // 2026-11-24 (feature 025, fase 5, FR-018): los filtros de la causación, si vienen, con un valor conocido.
        RuleFor(x => x.PaymentForm).IsInEnum().When(x => x.PaymentForm is not null).WithMessage("La forma de pago es nómina o caja.");
        RuleFor(x => x.InstallmentScope).IsInEnum().When(x => x.InstallmentScope is not null)
            .WithMessage("Las cuotas son todas, sin extras o sólo extras.");
        RuleFor(x => x.DiscountCycle).IsInEnum().When(x => x.DiscountCycle is not null)
            .WithMessage("El ciclo de descuento es una quincena, una década o una semana del mes.");
    }
}

/// <summary>
/// Lo que comparten armar y recalcular: los filtros por su Id interno (404 si no existen), en el alcance de quien opera, el rango de la periodicidad
/// (<see cref="PeriodoDeCausacion.Validar"/>) y que cada concepto esté hoy en modo manual o tenga algo esperando en el rango. (nuevo)
/// </summary>
public sealed class EntradaDeProcesoDeCausacion(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    CausacionPorContabilizar porContabilizar,
    ILectorDeParametros parametros,
    IDateTimeService reloj)
{
    public async Task<Result<ParametrosDelProceso>> ResolverAsync(AccrualProcessKind clase, AccrualProcessInput entrada, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        int? linea = null, oficina = null, pagaduria = null;
        if (entrada.CreditLinePublicId is { } l)
        {
            linea = await db.CreditLines.AsNoTracking().Where(x => x.PublicId == l && !x.IsDeleted).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (linea is null) return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.FiltroInexistente("la línea de crédito"));
        }
        if (entrada.BranchPublicId is { } o)
        {
            oficina = await db.Branches.AsNoTracking().Where(x => x.PublicId == o).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (oficina is null) return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.FiltroInexistente("la oficina"));
        }
        if (entrada.PaymasterPersonPublicId is { } pp)
        {
            pagaduria = await db.People.AsNoTracking().Where(x => x.PublicId == pp).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (pagaduria is null) return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.FiltroInexistente("la pagaduría"));
        }
        var enAlcance = await procesos.FiltrosEnAlcanceAsync(oficina, linea, ct);
        if (enAlcance.IsFailure) return Result.Failure<ParametrosDelProceso>(enAlcance.Error);
        // 2026-11-24 (feature 025, fase 5, FR-018): la forma de pago, las cuotas y el ciclo de descuento son exactos sólo en el reconocimiento (cada línea es de
        // un crédito y una cuota); un hecho de lo causado junta créditos y no se parte, así que las otras clases no los admiten.
        if (entrada.ConFiltrosDeCausacion && clase != AccrualProcessKind.PeriodRecognition)
            return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.FiltrosSoloDelReconocimiento());

        var hoy = reloj.HoyLocal;
        // 2026-11-24 (feature 025, fase 3): el recálculo de la carga desde el corte no tiene rango ni modo: toma los créditos cargados a la fecha de la firma
        // y sólo el interés corriente (lo que lleva su hecho InteresDelCorte); la periodicidad y el rango quedan en el día de hoy.
        if (clase == AccrualProcessKind.CutoverRecalculation)
            return Result.Success(new ParametrosDelProceso(clase, AccrualProcessPeriodicity.Daily, null, hoy, hoy, AccrualProcessConcepts.CurrentInterest, linea,
                oficina, pagaduria));
        // 2026-11-16 (feature 024, fase 4): el reconocimiento de la cuota del período elige el ciclo (puede seguir después de hoy, pero tiene que haber
        // empezado) y sólo los rubros que se reconocen (interés corriente, seguro, administración), en «Manual»: los de «Automatica» los reconoce el ciclo.
        var reconocimiento = clase == AccrualProcessKind.PeriodRecognition;
        if (PeriodoDeCausacion.Validar(entrada.Periodicity, entrada.SubPeriod, entrada.DateFrom, entrada.DateTo, hoy, reconocimiento) is { } rango)
            return Result.Failure<ParametrosDelProceso>(ErroresDeGobierno.De(rango));

        var conceptos = ConceptosDeCausacion.Juntar(entrada.Concepts);
        if (conceptos == AccrualProcessConcepts.None) return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.SinConceptos());
        if (reconocimiento && ConceptosDeCausacion.De(conceptos).FirstOrDefault(c => ConceptosDeCausacion.RubroReconocible(c.Concepto) is null) is { } noReconocible)
            return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.ConceptoNoReconocible(noReconocible.Nombre));
        foreach (var concepto in ConceptosDeCausacion.De(conceptos))
        {
            var modo = await parametros.LeerAsync(ParametrosDeCartera.Modulo, concepto.ParametroDeModo, hoy, ct: ct);
            if (modo.IsSuccess && modo.Value.Texto == ParametrosDeCartera.CausacionManual) continue;
            if (!reconocimiento && await porContabilizar.Entregas(entrada.DateFrom, entrada.DateTo, [concepto.TipoDeHecho]).AnyAsync(ct)) continue;
            return Result.Failure<ParametrosDelProceso>(ErroresDeProcesosDeCausacion.ConceptoNoManual(concepto.Nombre));
        }
        return Result.Success(new ParametrosDelProceso(clase, entrada.Periodicity, entrada.SubPeriod, entrada.DateFrom, entrada.DateTo, conceptos, linea,
            oficina, pagaduria)
        {
            FormaDePago = entrada.PaymentForm, Cuotas = entrada.InstallmentScope, CicloDeDescuento = entrada.DiscountCycle,
        });
    }

    /// <summary>¿Cartera está activada? Sin activar no hay causación que contabilizar.</summary>
    public Task<bool> CarteraActivadaAsync(CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == Domain.Entities.Lending.Configuration.LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    public static Error NoActivada() => new(IngenIA365ERP.Application.Common.Integration.Lending.ContratoDeObligacionesSinActivar.CodigoNoActivada,
        "Cartera todavía no está activada en esta cooperativa. El proceso de causación estará disponible cuando se registre la activación.");
}

// ---------------------------------------------------------------------------------------------------------------- armar --

/// <summary>
/// Arma un proceso de causación y calcula su borrador (<c>POST /api/lending/accrual-processes</c>, <c>Lending.AccrualProcesses.Calculate</c>, con clave,
/// responde 201): Cartera activada, filtros en el alcance, rango de la periodicidad, conceptos en modo manual (o con algo esperando), sin otro proceso
/// abierto de esos conceptos ni uno en firme que se cruce. El borrador no contabiliza nada. (nuevo)
/// </summary>
public sealed record CreateAccrualProcessCommand(
    AccrualProcessPeriodicity Periodicity,
    byte? SubPeriod,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<AccrualProcessConcepts> Concepts,
    Guid? CreditLinePublicId = null,
    Guid? BranchPublicId = null,
    Guid? PaymasterPersonPublicId = null,
    AccrualProcessKind Kind = AccrualProcessKind.Posting,
    AccrualPaymentForm? PaymentForm = null,
    AccrualInstallmentScope? InstallmentScope = null,
    AccrualDiscountCycle? DiscountCycle = null) : IRequest<Result<AccrualProcessDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    public AccrualProcessInput Entrada => new(Periodicity, SubPeriod, DateFrom, DateTo, Concepts ?? [], CreditLinePublicId, BranchPublicId, PaymasterPersonPublicId,
        PaymentForm, InstallmentScope, DiscountCycle);
}

public sealed class CreateAccrualProcessCommandValidator : AbstractValidator<CreateAccrualProcessCommand>
{
    public CreateAccrualProcessCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Entrada).SetValidator(new AccrualProcessInputValidator());
    }
}

public sealed class CreateAccrualProcessCommandHandler(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    EntradaDeProcesoDeCausacion entradas,
    NumeradorDeCartera numerador,
    IActorActual actorActual) : IRequestHandler<CreateAccrualProcessCommand, Result<AccrualProcessDto>>
{
    public async Task<Result<AccrualProcessDto>> Handle(CreateAccrualProcessCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.SinUsuario());
        if (!await entradas.CarteraActivadaAsync(ct)) return Result.Failure<AccrualProcessDto>(EntradaDeProcesoDeCausacion.NoActivada());
        var resueltos = await entradas.ResolverAsync(request.Kind, request.Entrada, ct);
        if (resueltos.IsFailure) return Result.Failure<AccrualProcessDto>(resueltos.Error);
        var p = resueltos.Value;
        if (await procesos.AbiertoQueComparteAsync(p.Conceptos, null, ct, p.Clase) is { } abierto)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.OtroAbierto(abierto.Number, abierto.PublicId));
        if (await procesos.EnFirmeQueSeCruzaAsync(p, null, ct) is { } cruzado)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.SeCruza(cruzado.Number, cruzado.PublicId));
        var calculo = await procesos.CalcularAsync(p, null, ct);
        if (calculo.IsFailure) return Result.Failure<AccrualProcessDto>(calculo.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cruces = await procesos.TomarYVerificarCrucesAsync(p, null, null, ct);
            if (cruces.IsFailure) return Result.Failure<AccrualProcessDto>(cruces.Error);
            var proceso = new AccrualProcess
            {
                Number = await numerador.NumeroDeProcesoDeCausacionAsync(ProcesosDeCausacion.Prefijo, ct),
                Kind = p.Clase, Periodicity = p.Periodicidad, SubPeriod = p.Subperiodo, DateFrom = p.Desde, DateTo = p.Hasta, Concepts = p.Conceptos,
                CreditLineId = p.LineaId, BranchId = p.OficinaId, PaymasterPersonId = p.PagaduriaId, CreatedByUserId = usuario,
                PaymentForm = p.FormaDePago, InstallmentScope = p.Cuotas, DiscountCycle = p.CicloDeDescuento,
            };
            db.AccrualProcesses.Add(proceso);
            await db.SaveChangesAsync(ct);
            await procesos.AplicarAsync(proceso, calculo.Value, usuario, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await procesos.DtoAsync(proceso, ct));
        }, ct);
    }
}

// ------------------------------------------------------------------------------------------------------------ recalcular --

/// <summary>
/// Recalcula el borrador (<c>POST /{id}/recalculate</c>, <c>Lending.AccrualProcesses.Calculate</c>, con clave), cuantas veces se quiera, con los mismos
/// parámetros o con otros (<see cref="Changes"/>; un reproceso no los cambia). Uno que esperaba la aprobación vuelve a borrador y su solicitud se invalida:
/// lo que se aprobaba cambió. Uno en firme no se recalcula: se reprocesa. (nuevo)
/// </summary>
public sealed record RecalculateAccrualProcessCommand(Guid AccrualProcessPublicId, AccrualProcessInput? Changes = null)
    : IRequest<Result<AccrualProcessDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecalculateAccrualProcessCommandValidator : AbstractValidator<RecalculateAccrualProcessCommand>
{
    public RecalculateAccrualProcessCommandValidator()
    {
        RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
        RuleFor(x => x.Changes!).SetValidator(new AccrualProcessInputValidator()).When(x => x.Changes is not null);
    }
}

public sealed class RecalculateAccrualProcessCommandHandler(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    EntradaDeProcesoDeCausacion entradas,
    IMotorDeAprobaciones motor,
    IActorActual actorActual) : IRequestHandler<RecalculateAccrualProcessCommand, Result<AccrualProcessDto>>
{
    public async Task<Result<AccrualProcessDto>> Handle(RecalculateAccrualProcessCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.SinUsuario());
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<AccrualProcessDto>(leido.Error);
        var proceso = leido.Value;
        if (proceso.ExigirAbierto() is { } cerrado) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.De(cerrado));
        if (request.Changes is not null && proceso.ReprocessOfId is not null)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.ReprocesoFijo());

        var p = ParametrosDelProceso.De(proceso);
        if (request.Changes is { } cambios)
        {
            var resueltos = await entradas.ResolverAsync(proceso.Kind, cambios, ct);
            if (resueltos.IsFailure) return Result.Failure<AccrualProcessDto>(resueltos.Error);
            p = resueltos.Value;
        }
        var reprocesado = proceso.ReprocessOfId is { } id ? await db.AccrualProcesses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        if (await procesos.EnFirmeQueSeCruzaAsync(p, reprocesado?.Id, ct) is { } cruzado)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.SeCruza(cruzado.Number, cruzado.PublicId));
        var calculo = await procesos.CalcularAsync(p, reprocesado, ct);
        if (calculo.IsFailure) return Result.Failure<AccrualProcessDto>(calculo.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cruces = await procesos.TomarYVerificarCrucesAsync(p, proceso.Id, reprocesado?.Id, ct);
            if (cruces.IsFailure) return Result.Failure<AccrualProcessDto>(cruces.Error);
            if (proceso.Status == AccrualProcessStatus.PendingApproval)
                await motor.InvalidarAsync(ApprovalSourceTypes.LendingAccrualProcess, proceso.PublicId, ApprovalSubjects.LendingAccrualProcess, ct);
            proceso.Periodicity = p.Periodicidad;
            proceso.SubPeriod = p.Subperiodo;
            proceso.DateFrom = p.Desde;
            proceso.DateTo = p.Hasta;
            proceso.Concepts = p.Conceptos;
            proceso.CreditLineId = p.LineaId;
            proceso.BranchId = p.OficinaId;
            proceso.PaymasterPersonId = p.PagaduriaId;
            proceso.PaymentForm = p.FormaDePago;
            proceso.InstallmentScope = p.Cuotas;
            proceso.DiscountCycle = p.CicloDeDescuento;
            await procesos.AplicarAsync(proceso, calculo.Value, usuario, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await procesos.DtoAsync(proceso, ct));
        }, ct);
    }
}

// --------------------------------------------------------------------------------------------------------------- anular --

/// <summary>
/// Anula un proceso abierto (<c>POST /{id}/void</c>, <c>Lending.AccrualProcesses.Calculate</c>, con clave y motivo): en borrador o esperando la aprobación
/// (cuya solicitud se invalida) pasa a <c>Voided</c> y sus conceptos quedan libres para otro proceso. Uno en firme no se anula ni se borra. (nuevo)
/// </summary>
public sealed record VoidAccrualProcessCommand(Guid AccrualProcessPublicId, string Reason)
    : IRequest<Result<AccrualProcessDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidAccrualProcessCommandValidator : ValidadorConMotivo<VoidAccrualProcessCommand>
{
    public VoidAccrualProcessCommandValidator() => RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
}

public sealed class VoidAccrualProcessCommandHandler(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    IMotorDeAprobaciones motor,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidAccrualProcessCommand, Result<AccrualProcessDto>>
{
    public async Task<Result<AccrualProcessDto>> Handle(VoidAccrualProcessCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.SinUsuario());
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<AccrualProcessDto>(leido.Error);
        var proceso = leido.Value;
        if (proceso.PuedeIrA(AccrualProcessStatus.Voided) is { } problema) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.De(problema));
        if (proceso.Status == AccrualProcessStatus.PendingApproval)
            await motor.InvalidarAsync(ApprovalSourceTypes.LendingAccrualProcess, proceso.PublicId, ApprovalSubjects.LendingAccrualProcess, ct);
        proceso.Anular(request.Reason, usuario, reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success(await procesos.DtoAsync(proceso, ct));
    }
}

// -------------------------------------------------------------------------------------------------------- poner en firme --

/// <summary>
/// Pide poner en firme el borrador (<c>POST /{id}/confirm</c>, <c>Lending.AccrualProcesses.Confirm</c>; un reproceso exige además
/// <c>Lending.AccrualProcesses.Reprocess</c>; con clave y motivo). El borrador que se pide es el que la persona revisó (<see cref="ExpectedContentSha256"/>)
/// y sigue igual al recalcularlo; sin bloqueos, con algo para contabilizar y sin cruces. Queda <c>PendingApproval</c> con una solicitud de la plataforma
/// (sujeto <c>LendingAccrualProcess</c>, regla fija de un nivel con <c>Lending.AccrualProcesses.Confirm</c>) que decide otra persona presente; su
/// aprobación lo pone en firme y ordena el lote (<see cref="FuenteDeAprobacionDeCausacion"/>). Pedirlo otra vez con la misma huella devuelve la misma
/// solicitud. (nuevo)
/// </summary>
public sealed record ConfirmAccrualProcessCommand(Guid AccrualProcessPublicId, string Reason, string ExpectedContentSha256)
    : IRequest<Result<AccrualProcessDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmAccrualProcessCommandValidator : ValidadorConMotivo<ConfirmAccrualProcessCommand>
{
    public ConfirmAccrualProcessCommandValidator()
    {
        RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
        RuleFor(x => x.ExpectedContentSha256).NotEmpty().WithMessage("Indique la huella del borrador que revisó (expectedContentSha256).");
    }
}

public sealed class ConfirmAccrualProcessCommandHandler(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    IMotorDeAprobaciones motor,
    ICurrentUserPermissions permisos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ConfirmAccrualProcessCommand, Result<AccrualProcessDto>>
{
    public const string PermisoDeReproceso = "Lending.AccrualProcesses.Reprocess";

    public async Task<Result<AccrualProcessDto>> Handle(ConfirmAccrualProcessCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.SinUsuario());
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: true, ct);
        if (leido.IsFailure) return Result.Failure<AccrualProcessDto>(leido.Error);
        var proceso = leido.Value;

        // La misma petición otra vez: la solicitud ya está pedida para este borrador.
        if (proceso is { Status: AccrualProcessStatus.PendingApproval, ApprovalRequestPublicId: not null }
            && string.Equals(proceso.ContentSha256, request.ExpectedContentSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Success(await procesos.DtoAsync(proceso, ct));
        if (proceso.PuedeIrA(AccrualProcessStatus.PendingApproval) is { } problema) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.De(problema));
        if (proceso.ReprocessOfId is not null && !permisos.EsMaestroGlobal
            && !(await permisos.ListAsync(ct)).Contains(PermisoDeReproceso, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.PermisoRequerido(PermisoDeReproceso));
        if (!string.Equals(proceso.ContentSha256, request.ExpectedContentSha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.ContenidoCambiado(proceso.ContentSha256));

        // Lo que se pide aprobar es lo que hay: se recalcula y tiene que dar la misma huella.
        var reprocesado = proceso.ReprocessOfId is { } id ? await db.AccrualProcesses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        var p = ParametrosDelProceso.De(proceso);
        var calculo = await procesos.CalcularAsync(p, reprocesado, ct);
        if (calculo.IsFailure) return Result.Failure<AccrualProcessDto>(calculo.Error);
        if (!string.Equals(calculo.Value.Huella(p), proceso.ContentSha256, StringComparison.Ordinal))
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.Desactualizado());
        if (calculo.Value.Bloquea) return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.Bloqueado(ProcesosDeCausacion.Motivos(calculo.Value)));
        if (!calculo.Value.Incluidos.Any()) return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.NadaQueContabilizar());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cruces = await procesos.TomarYVerificarCrucesAsync(p, proceso.Id, reprocesado?.Id, ct);
            if (cruces.IsFailure) return Result.Failure<AccrualProcessDto>(cruces.Error);
            var rotulo = $"Causación {proceso.Number} · {proceso.DateFrom:yyyy-MM-dd} a {proceso.DateTo:yyyy-MM-dd}"
                         + (reprocesado is null ? string.Empty : $" · reproceso de {reprocesado.Number}");
            IReadOnlyCollection<int> participantes = proceso.CalculatedByUserId is { } calculador && calculador != usuario ? [calculador] : [];
            var solicitada = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                ApprovalSubjects.LendingAccrualProcess,
                ApprovalSourceTypes.LendingAccrualProcess,
                proceso.PublicId,
                rotulo.Length > 200 ? rotulo[..200] : rotulo,
                null,
                null,
                null,
                proceso.IncludedAmount,
                reloj.HoyLocal,
                proceso.CreatedByUserId,
                participantes,
                proceso.ContentSha256!,
                null), ct);
            if (solicitada.IsFailure) return Result.Failure<AccrualProcessDto>(solicitada.Error);
            if (solicitada.Value is not { } solicitud) return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.SinPolitica());
            proceso.PedirAprobacion(request.Reason, solicitud.PublicId, usuario, reloj.UtcNow);
            await db.SaveChangesAsync(ct);
            return Result.Success(await procesos.DtoAsync(proceso, ct));
        }, ct);
    }
}

// -------------------------------------------------------------------------------------------------------------- reprocesar --

/// <summary>
/// Reprocesa un proceso en firme (<c>POST /{id}/reprocess</c>, <c>Lending.AccrualProcesses.Reprocess</c>, con clave y motivo, responde 201): arma un
/// proceso nuevo con lo mismo —clase, periodicidad, rango, conceptos y filtros— y calcula su borrador. Lo que se recalcula y lo que no:
/// <list type="bullet">
/// <item>entra lo que <b>todavía espera</b> en ese alcance: lo que el proceso reprocesado dejó fuera con un motivo que ya se corrigió (un mes reabierto,
/// una regla que faltaba) y lo causado que llegó después al rango (la puesta al día de un crédito). Es la diferencia entre lo que el libro tiene causado en
/// el rango y lo que se contabilizó: lo ya contabilizado no se vuelve a contabilizar ni se reversa, y los valores no se recalculan (el libro manda);</item>
/// <item>los créditos con <b>abonos</b> registrados después de la puesta en firme del reprocesado quedan <b>intactos</b> y se listan
/// (<c>LaterPayment</c>), y un hecho que los lleva queda fuera entero;</item>
/// <item>cada línea dice lo que el reprocesado contabilizó de ese crédito y concepto (<c>previousAmount</c>).</item>
/// </list>
/// Se pone en firme como cualquiera (motivo y aprobación de otra persona, con <c>Lending.AccrualProcesses.Reprocess</c> además); entonces el reprocesado
/// queda <c>Reprocessed</c>. (nuevo)
/// </summary>
public sealed record ReprocessAccrualProcessCommand(Guid AccrualProcessPublicId, string Reason)
    : IRequest<Result<AccrualProcessDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReprocessAccrualProcessCommandValidator : ValidadorConMotivo<ReprocessAccrualProcessCommand>
{
    public ReprocessAccrualProcessCommandValidator() => RuleFor(x => x.AccrualProcessPublicId).NotEmpty();
}

public sealed class ReprocessAccrualProcessCommandHandler(
    IApplicationDbContext db,
    ProcesosDeCausacion procesos,
    NumeradorDeCartera numerador,
    IActorActual actorActual) : IRequestHandler<ReprocessAccrualProcessCommand, Result<AccrualProcessDto>>
{
    public async Task<Result<AccrualProcessDto>> Handle(ReprocessAccrualProcessCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AccrualProcessDto>(ErroresDeGobierno.SinUsuario());
        var leido = await procesos.LeerAsync(request.AccrualProcessPublicId, seguir: false, ct);
        if (leido.IsFailure) return Result.Failure<AccrualProcessDto>(leido.Error);
        var anterior = leido.Value;
        if (anterior.Status != AccrualProcessStatus.Confirmed)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.NoEnFirme(anterior.Status.ToString()));
        // 2026-11-24 (feature 025, fase 3; FR-006): el recálculo de la carga desde el corte no se reprocesa.
        if (anterior.Kind == AccrualProcessKind.CutoverRecalculation) return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.SinReproceso());
        var p = ParametrosDelProceso.De(anterior);
        if (await procesos.AbiertoQueComparteAsync(p.Conceptos, null, ct, p.Clase) is { } abierto)
            return Result.Failure<AccrualProcessDto>(ErroresDeProcesosDeCausacion.OtroAbierto(abierto.Number, abierto.PublicId));
        var calculo = await procesos.CalcularAsync(p, anterior, ct);
        if (calculo.IsFailure) return Result.Failure<AccrualProcessDto>(calculo.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cruces = await procesos.TomarYVerificarCrucesAsync(p, null, anterior.Id, ct);
            if (cruces.IsFailure) return Result.Failure<AccrualProcessDto>(cruces.Error);
            var proceso = new AccrualProcess
            {
                Number = await numerador.NumeroDeProcesoDeCausacionAsync(ProcesosDeCausacion.Prefijo, ct),
                Kind = anterior.Kind, Periodicity = p.Periodicidad, SubPeriod = p.Subperiodo, DateFrom = p.Desde, DateTo = p.Hasta, Concepts = p.Conceptos,
                CreditLineId = p.LineaId, BranchId = p.OficinaId, PaymasterPersonId = p.PagaduriaId, ReprocessOfId = anterior.Id, CreatedByUserId = usuario,
                PaymentForm = p.FormaDePago, InstallmentScope = p.Cuotas, DiscountCycle = p.CicloDeDescuento,
            };
            db.AccrualProcesses.Add(proceso);
            await db.SaveChangesAsync(ct);
            await procesos.AplicarAsync(proceso, calculo.Value, usuario, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(await procesos.DtoAsync(proceso, ct));
        }, ct);
    }
}
