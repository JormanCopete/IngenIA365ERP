using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

// Feature 013, US5 (T0556; api.md §20; mensajes.md §18; T20, C35): lo que se opera del recaudador de prueba. Cada operación llama al contrato de obligaciones
// como lo haría Recaudo —recaudador TST, clase Test—, y las escrituras, dentro de una TransaccionExplicita (la del recaudador: el contrato nunca abre la
// suya). Sólo sirven si la guarda DoblesDePruebaDeCarteraEnDi registró RecaudadorDePrueba (fuera de producción y con Lending:TestDoubles:Enabled); si
// no, el 404 genérico. Son dobles fuera de producción, no consultas de Cartera: no aplican el alcance de oficina y línea. (nuevo)

/// <summary>La guarda de las operaciones del recaudador de prueba (T20). (nuevo)</summary>
public static class RecaudadorDePruebaRegistrado
{
    public static bool Esta(IEnumerable<IDestinoDeMensajes> destinos) => destinos.Any(d => d is RecaudadorDePrueba);

    public static Error NoExiste() => new("Generic.NotFound", "El recurso no existe.");
}

/// <summary><c>GET /api/lending/test-doubles/collector/obligations</c>: lo que devuelve <c>ListarAsync</c> del contrato (C28). (nuevo)</summary>
public sealed record ListTestCollectorObligationsQuery(Guid PersonPublicId, bool IncludeCoDebtor = false)
    : IRequest<Result<RespuestaDelContratoDto<ListadoDeObligacionesDto>>>;

public sealed class ListTestCollectorObligationsQueryHandler(IContratoDeObligaciones contrato, IEnumerable<IDestinoDeMensajes> destinos, IDateTimeService reloj)
    : IRequestHandler<ListTestCollectorObligationsQuery, Result<RespuestaDelContratoDto<ListadoDeObligacionesDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ListadoDeObligacionesDto>>> Handle(ListTestCollectorObligationsQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ListadoDeObligacionesDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return Result.Success(await contrato.ListarAsync(
            new SolicitudDeListado(RecaudadorDePrueba.Recaudador, request.PersonPublicId, reloj.HoyLocal, request.IncludeCoDebtor), ct));
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/simulations</c>: <c>SimularAsync</c> del contrato; con la clave de un pago, la redistribución de su reverso
/// antes de confirmarlo (C38). (nuevo)
/// </summary>
public sealed record SimulateTestCollectorPaymentQuery(
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    EleccionDeAbono? PrepaymentChoice = null,
    int? InstallmentNumber = null,
    string? PaymentIdempotencyKeyToReverse = null) : IRequest<Result<RespuestaDelContratoDto<SimulacionDePagoDto>>>;

public sealed class SimulateTestCollectorPaymentQueryHandler(IContratoDeObligaciones contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<SimulateTestCollectorPaymentQuery, Result<RespuestaDelContratoDto<SimulacionDePagoDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<SimulacionDePagoDto>>> Handle(SimulateTestCollectorPaymentQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<SimulacionDePagoDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return Result.Success(await contrato.SimularAsync(new SolicitudDeSimulacion(RecaudadorDePrueba.Recaudador, request.ObligationPublicId, request.Modality,
            request.Amount, request.ValueDate, request.PrepaymentChoice, request.InstallmentNumber, request.PaymentIdempotencyKeyToReverse), ct));
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/payments</c> (con clave de pantalla): un lote de pagos por <c>AplicarAsync</c>, en la forma interactiva
/// (cada ítem con el <c>SimulationId</c> que vio quien prueba) o no interactiva; las claves de negocio <c>TST:{guid:N}</c> las propone la pantalla y
/// deja repetirlas para probar la idempotencia (C35). (nuevo)
/// </summary>
public sealed record ApplyTestCollectorPaymentCommand(IReadOnlyList<PagoAAplicar> Items, bool Interactive)
    : IRequest<Result<RespuestaDeAplicacionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyTestCollectorPaymentCommandValidator : AbstractValidator<ApplyTestCollectorPaymentCommand>
{
    public ApplyTestCollectorPaymentCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Agregue al menos un pago.");
    }
}

public sealed class ApplyTestCollectorPaymentCommandHandler(IApplicationDbContext db, IContratoDeObligaciones contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ApplyTestCollectorPaymentCommand, Result<RespuestaDeAplicacionDto>>
{
    public async Task<Result<RespuestaDeAplicacionDto>> Handle(ApplyTestCollectorPaymentCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos)) return Result.Failure<RespuestaDeAplicacionDto>(RecaudadorDePruebaRegistrado.NoExiste());
        var forma = request.Interactive ? FormaDeAplicacion.Interactive : FormaDeAplicacion.NonInteractive;
        return await TransaccionExplicita.EjecutarAsync(db, async () => Result.Success(await contrato.AplicarAsync(
            new SolicitudDeAplicacion(RecaudadorDePrueba.Recaudador, forma, request.Items, []), ct)), ct);
    }
}

/// <summary><c>POST /api/lending/test-doubles/collector/payments/{key}/reverse</c> (con clave de pantalla): <c>ReversarAsync</c> por la clave del pago. (nuevo)</summary>
public sealed record ReverseTestCollectorPaymentCommand(string PaymentIdempotencyKey, string Reason)
    : IRequest<Result<RespuestaDelContratoDto<ComprobanteDeReversoDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseTestCollectorPaymentCommandValidator : AbstractValidator<ReverseTestCollectorPaymentCommand>
{
    public ReverseTestCollectorPaymentCommandValidator()
    {
        RuleFor(x => x.PaymentIdempotencyKey).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se reversa el pago.").MaximumLength(500);
    }
}

public sealed class ReverseTestCollectorPaymentCommandHandler(IApplicationDbContext db, IContratoDeObligaciones contrato, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ReverseTestCollectorPaymentCommand, Result<RespuestaDelContratoDto<ComprobanteDeReversoDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ComprobanteDeReversoDto>>> Handle(ReverseTestCollectorPaymentCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ComprobanteDeReversoDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        return await TransaccionExplicita.EjecutarAsync(db, async () => Result.Success(await contrato.ReversarAsync(
            new SolicitudDeReverso(RecaudadorDePrueba.Recaudador, request.PaymentIdempotencyKey, request.Reason.Trim()), ct)), ct);
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/credit-balances/resolve</c> (con clave de pantalla): <c>ResolverSaldoAFavorAsync</c> con los valores de
/// <see cref="FormaDeResolucion"/> (contradicción 6). Sin <see cref="Amount"/>, todo lo disponible del concepto según el listado del contrato. (nuevo)
/// </summary>
public sealed record ResolveTestCollectorCreditBalanceCommand(
    Guid ObligationPublicId,
    FormaDeResolucion Kind,
    string IdempotencyKey,
    decimal? Amount = null,
    ConceptoDeObligacion Concept = ConceptoDeObligacion.CreditBalance,
    Guid? TargetObligationPublicId = null,
    ModalidadDePago? TargetModality = null,
    EleccionDeAbono? PrepaymentChoice = null) : IRequest<Result<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ResolveTestCollectorCreditBalanceCommandValidator : AbstractValidator<ResolveTestCollectorCreditBalanceCommand>
{
    public ResolveTestCollectorCreditBalanceCommandValidator()
    {
        RuleFor(x => x.ObligationPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.Amount is not null);
    }
}

public sealed class ResolveTestCollectorCreditBalanceCommandHandler(
    IApplicationDbContext db,
    IContratoDeObligaciones contrato,
    IEnumerable<IDestinoDeMensajes> destinos,
    IDateTimeService reloj) : IRequestHandler<ResolveTestCollectorCreditBalanceCommand, Result<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>>
{
    public async Task<Result<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>> Handle(ResolveTestCollectorCreditBalanceCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<RespuestaDelContratoDto<ComprobanteDeResolucionDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        var monto = request.Amount ?? await DisponibleAsync(request, ct);
        var hoy = reloj.HoyLocal;
        var recibo = new ReferenciaDeRecibo($"TST-{request.IdempotencyKey[^Math.Min(8, request.IdempotencyKey.Length)..]}", hoy, null, null,
            request.Kind == FormaDeResolucion.RefundThroughCollector ? "Egreso" : "Recibo");
        return await TransaccionExplicita.EjecutarAsync(db, async () => Result.Success(await contrato.ResolverSaldoAFavorAsync(new SolicitudDeResolucion(
            RecaudadorDePrueba.Recaudador, request.IdempotencyKey, request.ObligationPublicId, request.Concept, monto, request.Kind, recibo,
            request.TargetObligationPublicId, request.TargetModality, request.PrepaymentChoice), ct)), ct);
    }

    private async Task<decimal> DisponibleAsync(ResolveTestCollectorCreditBalanceCommand request, CancellationToken ct)
    {
        var persona = await (from l in db.Loans.AsNoTracking()
                             join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.DebtorPersonId equals p.Id
                             where l.PublicId == request.ObligationPublicId
                             select (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
        if (persona is null) return 0m;
        var listado = await contrato.ListarAsync(new SolicitudDeListado(RecaudadorDePrueba.Recaudador, persona.Value, reloj.HoyLocal), ct);
        var obligacion = listado.Value?.Obligations.FirstOrDefault(o => o.ObligationPublicId == request.ObligationPublicId);
        return obligacion is null ? 0m : request.Concept == ConceptoDeObligacion.Advance ? obligacion.Advance : obligacion.CreditBalance;
    }
}

/// <summary>Un <c>PagoReversadoPorCartera</c> que recibió el recaudador de prueba. (nuevo)</summary>
public sealed record TestCollectorReversalDto(
    Guid MessagePublicId,
    string LoanNumber,
    string ReversalNumber,
    string PaymentNumber,
    string IdempotencyKey,
    decimal ReversedAmount,
    string Reason,
    bool Reopened,
    DateTime EmittedAt,
    string? DeliveryStatus);

/// <summary><c>GET /api/lending/test-doubles/collector/reversals</c>: los <c>PagoReversadoPorCartera</c> de la bandeja con destino <c>Receipts</c>. (nuevo)</summary>
public sealed record ListTestCollectorReversalsQuery : IRequest<Result<IReadOnlyList<TestCollectorReversalDto>>>;

public sealed class ListTestCollectorReversalsQueryHandler(IApplicationDbContext db, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ListTestCollectorReversalsQuery, Result<IReadOnlyList<TestCollectorReversalDto>>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<IReadOnlyList<TestCollectorReversalDto>>> Handle(ListTestCollectorReversalsQuery request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos))
            return Result.Failure<IReadOnlyList<TestCollectorReversalDto>>(RecaudadorDePruebaRegistrado.NoExiste());
        var mensajes = await db.IntegrationMessages.AsNoTracking().Where(m => m.Type == PagoReversadoPorCarteraV1.Type)
            .OrderByDescending(m => m.Id).Take(200)
            .Select(m => new
            {
                m.PublicId, m.PayloadJson, m.EmittedAt,
                Estado = db.IntegrationMessageDeliveries.Where(d => d.MessageId == m.Id && d.Destination == IntegrationDestinations.Receipts)
                    .Select(d => d.Status.ToString()).FirstOrDefault(),
            }).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TestCollectorReversalDto>>(mensajes.Select(m =>
        {
            var c = JsonSerializer.Deserialize<PagoReversadoPorCarteraV1>(m.PayloadJson, Json) ?? new PagoReversadoPorCarteraV1();
            return new TestCollectorReversalDto(m.PublicId, c.Loan.LoanNumber, c.ReversalNumber, c.Payment.Number, c.Collector.IdempotencyKey, c.ReversedAmount,
                c.Reason, c.Reopened, m.EmittedAt, m.Estado);
        }).ToList());
    }
}
