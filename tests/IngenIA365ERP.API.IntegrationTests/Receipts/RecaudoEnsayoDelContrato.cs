using System.Text.Json;
using Carter;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, US9 (T0299, T0300; FR-224): el <b>arnés de las e2e del contrato</b>. Las e2e de Cartera y Captaciones que prueban el contrato de obligaciones y el de
/// cuentas por sí mismos —idempotencia por la clave de negocio, simulaciones, reversos por clave, saldos a favor, consignaciones y retiros con su respuesta
/// <c>RespuestaDelContratoDto</c>— lo llamaban por las rutas del recaudador de prueba (<c>/api/lending/test-doubles/collector</c>, recaudador <c>TST</c>, clase
/// <c>Test</c>). Con los dobles retirados (FR-223) esas rutas no existen. Este arnés vive <b>sólo en el proyecto de pruebas</b>, no en el producto, y llama a los
/// mismos contratos <b>como Recaudo real</b> —recaudador <c>REC</c>, clase <c>Receipts</c>, el que registra la activación de Recaudo—, en una transacción explícita (el
/// contrato nunca abre la suya), con el usuario de la petición: lo mismo que hace la caja dentro de <c>ConfirmacionDeRecibo</c>, sin la pantalla de la caja, porque lo
/// que estas pruebas verifican es la respuesta del contrato, no el recibo. Los recorridos de punta a punta por la caja, los extractos, las pagadurías y los giros
/// reales están en las colecciones de Recaudo y Tesorería y en <see cref="RecaudoYTesoreriaE2E"/>. No usa nada del espacio de los dobles: sobrevive a T0307.
/// Se registra en <c>CentralIdentityApiFixture</c> (no es público: Carter no lo descubre por su cuenta). Escrito, sin correr. (nuevo)
/// </summary>
internal sealed class RecaudoEnsayoDelContrato : ICarterModule
{
    public const string Ruta = "/api/e2e/recaudo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup(Ruta).RequireAuthorization();
        var recaudador = ClavesDeRecaudo.Recaudador;

        // ---- contrato de obligaciones ----
        g.MapGet("/obligations", async (Guid personPublicId, bool? includeCoDebtor, IContratoDeObligaciones contrato, IDateTimeService reloj, CancellationToken ct) =>
            Results.Ok(await contrato.ListarAsync(new SolicitudDeListado(recaudador, personPublicId, reloj.HoyLocal, includeCoDebtor ?? false), ct)));

        g.MapPost("/simulations", async (SimulacionRequest body, IContratoDeObligaciones contrato, IDateTimeService reloj, CancellationToken ct) =>
            Results.Ok(await contrato.SimularAsync(new SolicitudDeSimulacion(recaudador, body.ObligationPublicId, body.Modality ?? ModalidadDePago.FreeAmount,
                body.Amount ?? 0m, body.ValueDate ?? reloj.HoyLocal, body.PrepaymentChoice, body.InstallmentNumber, body.PaymentIdempotencyKeyToReverse), ct)));

        g.MapPost("/payments", async (PagosRequest body, IApplicationDbContext db, IContratoDeObligaciones contrato, CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.AplicarAsync(new SolicitudDeAplicacion(recaudador,
                body.Interactive ? FormaDeAplicacion.Interactive : FormaDeAplicacion.NonInteractive, body.Items ?? [], []), ct), ct)));

        g.MapPost("/payments/{key}/reverse", async (string key, ReversoRequest body, IApplicationDbContext db, IContratoDeObligaciones contrato, CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.ReversarAsync(
                new SolicitudDeReverso(recaudador, Uri.UnescapeDataString(key), (body.Reason ?? string.Empty).Trim()), ct), ct)));

        g.MapPost("/credit-balances/resolve", async (ResolucionRequest body, IApplicationDbContext db, IContratoDeObligaciones contrato, IDateTimeService reloj,
            CancellationToken ct) =>
        {
            var clave = body.IdempotencyKey ?? $"{ClavesDeRecaudo.Codigo}:{Guid.NewGuid():N}";
            var concepto = body.Concept ?? ConceptoDeObligacion.CreditBalance;
            var forma = body.Kind ?? FormaDeResolucion.RefundThroughCollector;
            var monto = body.Amount ?? await DisponibleAsync(db, contrato, body.ObligationPublicId, concepto, reloj.HoyLocal, ct);
            var recibo = new ReferenciaDeRecibo($"REC-{clave[^Math.Min(8, clave.Length)..]}", reloj.HoyLocal, null, null,
                forma == FormaDeResolucion.RefundThroughCollector ? "Egreso" : "Recibo");
            return Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.ResolverSaldoAFavorAsync(new SolicitudDeResolucion(recaudador, clave,
                body.ObligationPublicId, concepto, monto, forma, recibo, body.TargetObligationPublicId, body.TargetModality, body.PrepaymentChoice), ct), ct));
        });

        // Los PagoReversadoPorCartera que Cartera emitió hacia Recaudo (lo que la bandeja muestra con destino Receipts).
        g.MapGet("/reversals", async (IApplicationDbContext db, CancellationToken ct) =>
        {
            var mensajes = await db.IntegrationMessages.AsNoTracking().Where(m => m.Type == PagoReversadoPorCarteraV1.Type).OrderByDescending(m => m.Id).Take(200)
                .Select(m => new
                {
                    m.PublicId, m.PayloadJson, m.EmittedAt,
                    Estado = db.IntegrationMessageDeliveries.Where(d => d.MessageId == m.Id && d.Destination == IntegrationDestinations.Receipts)
                        .Select(d => d.Status.ToString()).FirstOrDefault(),
                }).ToListAsync(ct);
            return Results.Ok(mensajes.Select(m =>
            {
                var c = JsonSerializer.Deserialize<PagoReversadoPorCarteraV1>(m.PayloadJson, Json) ?? new PagoReversadoPorCarteraV1();
                return new
                {
                    messagePublicId = m.PublicId, loanNumber = c.Loan.LoanNumber, reversalNumber = c.ReversalNumber, paymentNumber = c.Payment.Number,
                    idempotencyKey = c.Collector.IdempotencyKey, reversedAmount = c.ReversedAmount, reason = c.Reason, reopened = c.Reopened, emittedAt = m.EmittedAt,
                    deliveryStatus = m.Estado,
                };
            }).ToList());
        });

        // ---- contrato de cuentas ----
        g.MapGet("/accounts/installments", async (Guid personPublicId, DateOnly? date, IContratoDeCuentas contrato, IDateTimeService reloj, CancellationToken ct) =>
            Results.Ok(await contrato.ListarExigiblesAsync(new SolicitudDeExigibles(recaudador, [personPublicId], date ?? reloj.HoyLocal), ct)));

        g.MapPost("/accounts/deposits", async (ConsignacionesRequest body, IApplicationDbContext db, IContratoDeCuentas contrato, CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.ConsignarAsync(
                new SolicitudDeConsignacion(recaudador, FormaDeAplicacion.NonInteractive, body.Deposits ?? [], body.NotDeducted ?? []), ct), ct)));

        g.MapPost("/accounts/operations/{key}/reverse", async (string key, ReversoRequest body, IApplicationDbContext db, IContratoDeCuentas contrato,
                CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.ReversarAsync(
                new SolicitudDeReversoDeCuenta(recaudador, Uri.UnescapeDataString(key), (body.Reason ?? string.Empty).Trim()), ct), ct)));

        g.MapGet("/accounts", async (Guid personPublicId, DateOnly? date, bool? includeAsSignatory, IContratoDeCuentas contrato, IDateTimeService reloj,
                CancellationToken ct) =>
            Results.Ok(await contrato.ConsultarAsync(new SolicitudDeConsultaDeCuentas(recaudador, personPublicId, date ?? reloj.HoyLocal, includeAsSignatory ?? false), ct)));

        g.MapPost("/accounts/simulations", async (SolicitudDeSimulacionDeCuenta body, IContratoDeCuentas contrato, CancellationToken ct) =>
            Results.Ok(await contrato.SimularAsync(body with { Collector = recaudador }, ct)));

        g.MapPost("/accounts/withdrawals", async (SolicitudDeRetiro body, IApplicationDbContext db, IContratoDeCuentas contrato, CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.RetirarAsync(body with { Collector = recaudador }, ct), ct)));

        g.MapPost("/accounts/transfers", async (SolicitudDeTraslado body, IApplicationDbContext db, IContratoDeCuentas contrato, CancellationToken ct) =>
            Results.Ok(await TransaccionExplicita.EjecutarAsync(db, () => contrato.TrasladarAsync(body with { Collector = recaudador }, ct), ct)));
    }

    private static async Task<decimal> DisponibleAsync(IApplicationDbContext db, IContratoDeObligaciones contrato, Guid obligacion, ConceptoDeObligacion concepto,
        DateOnly hoy, CancellationToken ct)
    {
        var persona = await (from l in db.Loans.AsNoTracking()
                             join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.DebtorPersonId equals p.Id
                             where l.PublicId == obligacion
                             select (Guid?)p.PublicId).FirstOrDefaultAsync(ct);
        if (persona is null) return 0m;
        var listado = await contrato.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, persona.Value, hoy), ct);
        var o = listado.Value?.Obligations.FirstOrDefault(x => x.ObligationPublicId == obligacion);
        return o is null ? 0m : concepto == ConceptoDeObligacion.Advance ? o.Advance : o.CreditBalance;
    }

    public sealed record SimulacionRequest(Guid ObligationPublicId, ModalidadDePago? Modality, decimal? Amount, DateOnly? ValueDate,
        EleccionDeAbono? PrepaymentChoice, int? InstallmentNumber, string? PaymentIdempotencyKeyToReverse);

    public sealed record PagosRequest(IReadOnlyList<PagoAAplicar>? Items, bool Interactive);

    public sealed record ReversoRequest(string? Reason);

    public sealed record ConsignacionesRequest(IReadOnlyList<ConsignacionAAplicar>? Deposits, IReadOnlyList<NoDescontadoDeCuenta>? NotDeducted);

    public sealed record ResolucionRequest(Guid ObligationPublicId, FormaDeResolucion? Kind, string? IdempotencyKey, decimal? Amount,
        ConceptoDeObligacion? Concept, Guid? TargetObligationPublicId, ModalidadDePago? TargetModality, EleccionDeAbono? PrepaymentChoice);
}
