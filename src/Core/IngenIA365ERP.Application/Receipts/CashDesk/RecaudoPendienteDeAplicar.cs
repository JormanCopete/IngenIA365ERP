using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// Feature 015, R1, US3 (T0363; FR-046; D14; data-model §8.1): el recaudo pendiente de aplicar. Sólo existe para una persona de una carga de migración aplicada sin
// activar, cuando el contrato de su módulo responde «no habilitado»; fuera de ese caso el ítem se rechaza con el aviso del módulo. Ninguna tarea lo aplica: lo aplica
// una persona con permiso que elige la modalidad, con la fecha valor del recibo (FR-174).

/// <summary>Un recaudo pendiente de aplicar. (nuevo)</summary>
public sealed record PendingApplicationDto(
    Guid PublicId,
    Guid ReceiptPublicId,
    string ReceiptNumber,
    Guid PersonPublicId,
    string PersonName,
    ApplicationTarget TargetModule,
    string? ProductReference,
    decimal Amount,
    DateOnly ValueDate,
    PendingApplicationState Status,
    Guid? AppliedToProductPublicId,
    string? ModuleVoucherNumber);

/// <summary>Los errores del recaudo pendiente de aplicar. (nuevo)</summary>
public static class ErroresDelPendiente
{
    public const string CodigoNoElegible = "Receipts.PendingApplication.NotEligible";
    public const string CodigoModuloNoActivado = "Receipts.PendingApplication.ModuleNotActivated";
    public const string CodigoNoPendiente = "Receipts.PendingApplication.NotPending";
    public const string CodigoAplicado = "Receipts.Voiding.PendingApplicationApplied";

    public static Error NoElegible(string motivo) => new ErrorConDatos(CodigoNoElegible,
        $"El valor no puede quedar como recaudo pendiente de aplicar: {motivo} Sólo se admite para una persona de una carga aplicada sin activar.",
        new { reason = motivo });

    public static Error ModuloNoActivado() => new(CodigoModuloNoActivado,
        "El módulo del pendiente todavía no está activado: se aplica el día de su activación.");

    public static Error NoPendiente(PendingApplicationState estado) => new ErrorConDatos(CodigoNoPendiente,
        $"El recaudo pendiente ya no está por aplicar (está {estado}).", new { status = estado.ToString() });

    public static Error YaAplicado() => new(CodigoAplicado,
        "El recibo tiene un recaudo pendiente que ya se aplicó en su módulo: reverse primero esa aplicación en el módulo antes de anular el recibo.");
}

/// <summary>
/// Las reglas del recaudo pendiente de aplicar (feature 015, R1, T0363; FR-046): cuándo una persona es elegible, por los puertos de los dos módulos —Cartera
/// responde «no habilitado» en el listado y la persona está en su carga aplicada sin activar (<see cref="IConsultaDeCargaSinActivar"/>); Captaciones lo informa en
/// sus exigibles (<c>MigratedNotActivated</c>) con la naturaleza sin activar—. (nuevo)
/// </summary>
public sealed class ReglasDelPendiente(IContratoDeObligaciones obligaciones, IContratoDeCuentas cuentas, IConsultaDeCargaSinActivar? carga = null)
{
    /// <summary>Nulo si la persona puede dejar un pendiente para <paramref name="modulo"/> a la fecha; si no, el error con el motivo.</summary>
    public async Task<Error?> ElegibleAsync(Guid persona, ApplicationTarget modulo, DateOnly fecha, CancellationToken ct)
    {
        switch (modulo)
        {
            case ApplicationTarget.Loan:
            {
                var listado = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, persona, fecha), ct);
                if (listado.Value?.LendingStatus is not EstadoDeCartera.NotActivated)
                    return ErroresDelPendiente.NoElegible("Cartera ya está activada; pague el crédito por su contrato.");
                if (carga is null) return ErroresDelPendiente.NoElegible("no hay consulta de la carga de Cartera en este despliegue.");
                var enCarga = await carga.PersonaAsync(persona, ct);
                return enCarga.Value?.InAppliedLoad == true ? null : ErroresDelPendiente.NoElegible("la persona no está en la carga de Cartera aplicada sin activar.");
            }
            case ApplicationTarget.Account:
            {
                var exigibles = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [persona], fecha), ct);
                var v = exigibles.Value;
                if (v is null || (v.DepositsStatus != EstadoDeCartera.NotActivated && v.ContributionsStatus != EstadoDeCartera.NotActivated))
                    return ErroresDelPendiente.NoElegible("Captaciones y aportes ya están activados; consigne por su contrato.");
                return v.MigratedNotActivated.Contains(persona) ? null : ErroresDelPendiente.NoElegible("la persona no está en la carga de captaciones aplicada sin activar.");
            }
            default:
                return ErroresDelPendiente.NoElegible("sólo créditos y cuentas quedan como pendientes.");
        }
    }
}

/// <summary><c>GET /api/receipts/pending-applications?state=&amp;person=</c> (<c>Receipts.PendingApplications.View</c>) (nuevo).</summary>
public sealed record ListPendingApplicationsQuery(PendingApplicationState? State = PendingApplicationState.Pending, Guid? PersonPublicId = null)
    : IRequest<Result<IReadOnlyList<PendingApplicationDto>>>;

public sealed class ListPendingApplicationsQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListPendingApplicationsQuery, Result<IReadOnlyList<PendingApplicationDto>>>
{
    public async Task<Result<IReadOnlyList<PendingApplicationDto>>> Handle(ListPendingApplicationsQuery request, CancellationToken ct)
    {
        var consulta = db.PendingApplications.AsNoTracking();
        if (request.State is { } estado) consulta = consulta.Where(p => p.Status == estado);
        if (request.PersonPublicId is { } persona)
        {
            var id = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(p => p.PersonId == id);
        }
        var a = await alcance.ObtenerAsync(ct);
        if (!a.Total)
        {
            var oficinas = a.Oficinas.ToList();
            consulta = consulta.Where(p => oficinas.Contains(p.BranchId));
        }
        var filas = await consulta.OrderBy(p => p.Id).Take(500).ToListAsync(ct);
        return Result.Success(await PendientesDeRecaudo.DtosAsync(db, filas, ct));
    }
}

/// <summary>Lo común de los pendientes. (nuevo)</summary>
public static class PendientesDeRecaudo
{
    public static async Task<IReadOnlyList<PendingApplicationDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<PendingApplication> filas, CancellationToken ct)
    {
        var recibos = filas.Select(f => f.ReceiptId).Distinct().ToList();
        var porRecibo = await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => (r.PublicId, r.Number), ct);
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var ids = filas.Select(f => f.Id).ToList();
        var resueltas = await db.PendingApplicationResolutions.AsNoTracking().Where(r => ids.Contains(r.PendingApplicationId))
            .ToDictionaryAsync(r => r.PendingApplicationId, ct);
        return filas.Select(f =>
        {
            var (reciboPublicId, numero) = porRecibo.GetValueOrDefault(f.ReceiptId);
            var p = porPersona[f.PersonId];
            var r = resueltas.GetValueOrDefault(f.Id);
            return new PendingApplicationDto(f.PublicId, reciboPublicId, numero, p.PublicId, OperacionesDeCaja.Nombre(p), f.TargetModule, f.ProductReference, f.Amount,
                f.ValueDate, f.Status, r?.ProductPublicId, r?.ModuleOperationNumber);
        }).ToList();
    }
}

/// <summary>
/// <c>POST /api/receipts/pending-applications/{id}/apply</c> (<c>Receipts.PendingApplications.Apply</c>) (nuevo): aplica el pendiente por el contrato de su módulo, ya
/// activado, con la modalidad que elige la persona y la fecha valor del recibo, en forma no interactiva y con la clave de la porción del recibo (repetir no aplica dos
/// veces). Deja la resolución, el pendiente <c>Applied</c> y el hecho <c>RecaudoPendienteAplicado</c> en la misma transacción. (nuevo)
/// </summary>
public sealed record ApplyPendingApplicationCommand(
    Guid PendingApplicationPublicId,
    Guid ProductPublicId,
    ModalidadDePago? LoanModality = null,
    ModalidadDeConsignacion? AccountModality = null,
    EleccionDeAbono? Choice = null) : IRequest<Result<PendingApplicationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyPendingApplicationCommandValidator : AbstractValidator<ApplyPendingApplicationCommand>
{
    public ApplyPendingApplicationCommandValidator()
    {
        RuleFor(x => x.PendingApplicationPublicId).NotEmpty();
        RuleFor(x => x.ProductPublicId).NotEmpty().WithMessage("Indique el crédito o la cuenta donde se aplica.");
        RuleFor(x => x).Must(x => x.LoanModality is not null || x.AccountModality is not null).WithName("modality")
            .WithMessage("Elija la modalidad con que se aplica.");
    }
}

public sealed class ApplyPendingApplicationCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<ApplyPendingApplicationCommand, Result<PendingApplicationDto>>
{
    public async Task<Result<PendingApplicationDto>> Handle(ApplyPendingApplicationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PendingApplicationDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.PendingApplications.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PendingApplicationPublicId, ct);
        if (leido is null) return Result.Failure<PendingApplicationDto>(ErroresDeRecaudo.NoEncontrado("El recaudo pendiente"));

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([leido.PersonId], ct);
        var pendiente = await db.PendingApplications.FirstAsync(p => p.Id == leido.Id, ct);
        if (pendiente.Status == PendingApplicationState.Applied
            && await db.PendingApplicationResolutions.AsNoTracking().AnyAsync(r => r.PendingApplicationId == pendiente.Id && r.ProductPublicId == request.ProductPublicId, ct))
            return Result.Success((await PendientesDeRecaudo.DtosAsync(db, [pendiente], ct))[0]);
        if (pendiente.Status != PendingApplicationState.Pending) return Result.Failure<PendingApplicationDto>(ErroresDelPendiente.NoPendiente(pendiente.Status));

        var recibo = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == pendiente.ReceiptId, ct);
        var aplicacion = await db.ReceiptApplications.AsNoTracking().FirstAsync(a => a.Id == pendiente.ReceiptApplicationId, ct);
        var porcion = await db.ReceiptApplicationPortions.AsNoTracking().Where(p => p.ReceiptApplicationId == aplicacion.Id).OrderBy(p => p.Id).FirstAsync(ct);
        var persona = await db.People.AsNoTracking().Where(p => p.Id == pendiente.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == recibo.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var referencia = new ReferenciaDeRecibo(recibo.Number, reloj.HoyLocal, oficina, null, nameof(ReceiptDocumentKind.CashReceipt));
        var clave = porcion.IdempotencyKey;
        var medio = ClavesDeRecaudo.ClaseDelContrato(porcion.MeansClass);

        Guid? operacion = null;
        string? numeroDelModulo = null, numeroDelProducto = null;
        int modalidad;
        if (pendiente.TargetModule == ApplicationTarget.Loan)
        {
            if (request.LoanModality is not { } m) return Result.Failure<PendingApplicationDto>(ErroresDelPendiente.NoElegible("el pendiente es de Cartera: elija la modalidad del crédito."));
            modalidad = (int)m;
            var r = await obligaciones.AplicarAsync(new SolicitudDeAplicacion(ClavesDeRecaudo.Recaudador, FormaDeAplicacion.NonInteractive,
                [new PagoAAplicar(clave, request.ProductPublicId, m, pendiente.Amount, pendiente.ValueDate, medio, persona, referencia, request.Choice)], []), ct);
            var p = r.Payments.FirstOrDefault();
            if (p is null || p.Result == ResultadoDelContrato.Rejected || p.Value is null)
                return Result.Failure<PendingApplicationDto>(p?.Code == ErroresDePagosDelContrato.NoActivada
                    ? ErroresDelPendiente.ModuloNoActivado()
                    : ErroresDeRecaudo.ItemRechazado([new { itemId = pendiente.PublicId, code = p?.Code ?? "Receipts.Receipt.ContractUnavailable", message = p?.Message ?? "El módulo no respondió." }]));
            (operacion, numeroDelModulo, numeroDelProducto) = (p.Value.OperationPublicId, p.Value.Number, p.Value.ObligationNumber);
        }
        else
        {
            if (request.AccountModality is not { } m) return Result.Failure<PendingApplicationDto>(ErroresDelPendiente.NoElegible("el pendiente es de Captaciones: elija la modalidad de la cuenta."));
            modalidad = (int)m;
            var r = await cuentas.ConsignarAsync(new SolicitudDeConsignacion(ClavesDeRecaudo.Recaudador, FormaDeAplicacion.NonInteractive,
                [new ConsignacionAAplicar(clave, request.ProductPublicId, m, pendiente.Amount, pendiente.ValueDate, medio, persona, referencia)], []), ct);
            var c = r.Deposits.FirstOrDefault();
            if (c is null || c.Result == ResultadoDelContrato.Rejected || c.Value is null)
                return Result.Failure<PendingApplicationDto>(ErroresDeRecaudo.ItemRechazado(
                    [new { itemId = pendiente.PublicId, code = c?.Code ?? "Receipts.Receipt.ContractUnavailable", message = c?.Message ?? "El módulo no respondió." }]));
            (operacion, numeroDelModulo, numeroDelProducto) = (c.Value.OperationPublicId, c.Value.Number, c.Value.AccountNumber);
        }

        var resolucion = new PendingApplicationResolution
        {
            PendingApplicationId = pendiente.Id, ProductPublicId = request.ProductPublicId, ProductNumber = numeroDelProducto, Modality = modalidad,
            PrepaymentChoice = (int?)request.Choice, ValueDate = pendiente.ValueDate, Amount = pendiente.Amount, IdempotencyKey = clave,
            ModuleOperationPublicId = operacion, ModuleOperationNumber = numeroDelModulo, ResolvedByUserId = usuario, ResolvedAt = reloj.UtcNow,
        };
        db.PendingApplicationResolutions.Add(resolucion);
        pendiente.Status = PendingApplicationState.Applied;
        await db.SaveChangesAsync(ct);
        await emision.RecaudoPendienteAplicadoAsync(pendiente, resolucion, recibo, persona, ct);
        await auditoria.AgregarAsync("Receipts.PendingApplication.Applied", "PendingApplication", pendiente.PublicId, null,
            new { receipt = recibo.Number, product = numeroDelProducto, voucher = numeroDelModulo, amount = pendiente.Amount, valueDate = pendiente.ValueDate }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success((await PendientesDeRecaudo.DtosAsync(db, [pendiente], ct))[0]);
    }
}

/// <summary>Los códigos de los contratos que el pendiente distingue. (nuevo)</summary>
internal static class ErroresDePagosDelContrato
{
    public const string NoActivada = ContratoDeObligacionesSinActivar.CodigoNoActivada;
}
