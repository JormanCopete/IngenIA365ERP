using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

// Feature 013, US3 (T0456; api.md §20; mensajes.md §18; T20): lo que se opera de la tesorería de prueba. Sólo sirve si la guarda
// DoblesDePruebaDeCarteraEnDi registró TesoreriaDePrueba (fuera de producción y con Lending:TestDoubles:Enabled); si no, el 404 genérico. (nuevo)

/// <summary>Una orden que recibió la tesorería de prueba, con la anulación pedida y los resultados ya informados.</summary>
public sealed record TestTreasuryOrderDto(
    Guid OrderPublicId,
    string OrderNumber,
    string LoanNumber,
    string Form,
    string Beneficiary,
    decimal NetAmount,
    string Status,
    bool PaidByFile,
    bool VoidRequested,
    string? VoidReason,
    Guid? OrderMessagePublicId,
    IReadOnlyList<string> Results);

/// <summary>
/// Las órdenes y anulaciones recibidas por la tesorería de prueba (<c>GET /api/lending/test-doubles/treasury/orders?status=</c>,
/// <c>Lending.TestDoubles.Operate</c>): las órdenes de giro de Cartera con su <c>OrdenDeGiroEmitida</c>. Es un doble fuera de producción, no una
/// consulta de Cartera: no aplica el alcance de oficina y línea.
/// </summary>
public sealed record ListTestTreasuryOrdersQuery(DisbursementOrderStatus? Status = null) : IRequest<Result<IReadOnlyList<TestTreasuryOrderDto>>>;

public sealed class ListTestTreasuryOrdersQueryHandler(IApplicationDbContext db, IEnumerable<IDestinoDeMensajes> destinos)
    : IRequestHandler<ListTestTreasuryOrdersQuery, Result<IReadOnlyList<TestTreasuryOrderDto>>>
{
    public async Task<Result<IReadOnlyList<TestTreasuryOrderDto>>> Handle(ListTestTreasuryOrdersQuery request, CancellationToken ct)
    {
        if (!TesoreriaDePruebaRegistrada.Esta(destinos)) return Result.Failure<IReadOnlyList<TestTreasuryOrderDto>>(TesoreriaDePruebaRegistrada.NoExiste());
        var consulta = db.DisbursementOrders.AsNoTracking().Where(o => !o.IsDeleted);
        if (request.Status is { } estado) consulta = consulta.Where(o => o.Status == estado);
        var ordenes = await consulta.OrderByDescending(o => o.IssuedAt).Take(200).ToListAsync(ct);
        var publicos = ordenes.Select(o => o.PublicId).ToList();
        var mensajes = await db.IntegrationMessages.AsNoTracking()
            .Where(m => publicos.Contains(m.OriginPublicId) && m.Type == OrdenDeGiroEmitidaV1.Type)
            .ToDictionaryAsync(m => m.OriginPublicId, m => m.PublicId, ct);
        var creditos = ordenes.Select(o => o.LoanId).Distinct().ToList();
        var numeros = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Number, ct);
        var ids = ordenes.Select(o => o.Id).ToList();
        var resultados = (await db.DisbursementOrderResults.AsNoTracking().Where(r => ids.Contains(r.OrderId)).OrderBy(r => r.RegisteredAt).ToListAsync(ct))
            .ToLookup(r => r.OrderId);
        return Result.Success<IReadOnlyList<TestTreasuryOrderDto>>(ordenes.Select(o => new TestTreasuryOrderDto(o.PublicId, o.Number, numeros[o.LoanId],
            o.Form.ToString(), o.BeneficiaryName, o.NetAmount, o.Status.ToString(), o.PaidByFile, o.AnulacionPedida, o.VoidReason,
            mensajes.TryGetValue(o.PublicId, out var m) ? m : null, resultados[o.Id].Select(r => r.Result.ToString()).ToList())).ToList());
    }
}

/// <summary>Lo que respondió la emisión del resultado de prueba.</summary>
public sealed record TestDisbursementResultDto(Guid MessagePublicId, Guid OrderPublicId, string Outcome);

/// <summary>
/// Emite un <c>ResultadoDelGiro</c> v1 como <c>TES</c> (<c>POST /api/lending/test-doubles/treasury/orders/{orderPublicId}/results</c>,
/// <c>Lending.TestDoubles.Operate</c>, con clave): origen un documento de Tesorería nuevo, clave <c>Confirmation:{orden:N}</c>, <c>related</c> = la
/// orden, <c>OrderMessagePublicId</c> = su <c>OrdenDeGiroEmitida</c>, valor = el neto de la orden (o el informado) y la persona que prueba como
/// usuario de origen (T8). Lo consume <c>DestinoCartera</c> como el de la Tesorería real.
/// </summary>
public sealed record EmitTestDisbursementResultCommand(Guid OrderPublicId, string Outcome, string Reference, DateOnly Date, decimal? Amount = null,
    string? Reason = null, string? CheckNumber = null) : IRequest<Result<TestDisbursementResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EmitTestDisbursementResultCommandValidator : AbstractValidator<EmitTestDisbursementResultCommand>
{
    public EmitTestDisbursementResultCommandValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.Outcome).Must(o => o is nameof(DisbursementOrderStatus.Disbursed) or nameof(DisbursementOrderStatus.Rejected)
                or nameof(DisbursementOrderStatus.CheckNotCashed) or nameof(DisbursementOrderStatus.Voided))
            .WithMessage("El resultado es Disbursed, Rejected, CheckNotCashed o Voided.");
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class EmitTestDisbursementResultCommandHandler(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    EmisionDeCartera emision,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<EmitTestDisbursementResultCommand, Result<TestDisbursementResultDto>>
{
    public async Task<Result<TestDisbursementResultDto>> Handle(EmitTestDisbursementResultCommand request, CancellationToken ct)
    {
        if (!TesoreriaDePruebaRegistrada.Esta(destinos)) return Result.Failure<TestDisbursementResultDto>(TesoreriaDePruebaRegistrada.NoExiste());
        var orden = await db.DisbursementOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.OrderPublicId && !o.IsDeleted, ct);
        if (orden is null) return Result.Failure<TestDisbursementResultDto>(ErroresDeDesembolso.OrdenInexistente());
        var mensajeDeLaOrden = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == orden.PublicId && m.Type == OrdenDeGiroEmitidaV1.Type).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
        var credito = await db.Loans.AsNoTracking().Where(l => l.Id == orden.LoanId).Select(l => new { l.BranchId }).FirstAsync(ct);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<TestDisbursementResultDto>(sucursal.Error);
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);

        var contenido = new ResultadoDelGiroV1
        {
            OrderPublicId = orden.PublicId,
            OrderMessagePublicId = mensajeDeLaOrden ?? Guid.Empty,
            Outcome = request.Outcome,
            OccurredOn = request.Date,
            Amount = request.Amount ?? orden.NetAmount,
            TreasuryReference = request.Reference.Trim(),
            CheckNumber = request.CheckNumber,
            Reason = request.Reason,
        };
        var documento = Guid.NewGuid();
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, documento, "TreasuryResult", "TST", request.Reference.Trim(), request.Date, sucursal.Value,
                PersonPublicId: beneficiario),
            ClavesDeEvento.ConfirmacionPor(orden.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(orden.PublicId, EmisionDeCartera.ClaseOrdenDeGiro, orden.Number)), ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new TestDisbursementResultDto(mensajes[0].PublicId, orden.PublicId, request.Outcome));
    }
}

/// <summary>La guarda de las operaciones de la tesorería de prueba: sólo existen si el doble está registrado (T20). (nuevo)</summary>
public static class TesoreriaDePruebaRegistrada
{
    public static bool Esta(IEnumerable<IDestinoDeMensajes> destinos) => destinos.Any(d => d is TesoreriaDePrueba);

    public static Error NoExiste() => new("Generic.NotFound", "El recurso no existe.");
}
