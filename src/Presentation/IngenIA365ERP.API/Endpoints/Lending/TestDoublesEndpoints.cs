using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los dobles de prueba de Cartera (feature 013, US3, T0457; contracts/api.md §20, FR-089, T20): <b>sólo se mapean</b> con
/// <c>Lending:TestDoubles:Enabled = true</c> en un despliegue que no es de producción —el mismo criterio con que
/// <c>DoblesDePruebaDeCartera</c> registra <see cref="TesoreriaDePrueba"/>—; en producción no existen (404). Todas con
/// <c>Lending.TestDoubles.Operate</c>: <c>GET /</c> dice qué dobles hay (la pantalla y el menú lo preguntan), la tesorería de prueba lista
/// las órdenes y anulaciones recibidas y emite el <c>ResultadoDelGiro</c> de una orden como <c>TES</c> (con <c>Idempotency-Key</c>), con la
/// persona que prueba como usuario de origen. US5 (T0559) suma el recaudador de prueba (<c>/collector/*</c>): lista y simula por el contrato,
/// aplica un lote en forma interactiva o no interactiva, reversa por la clave del pago, resuelve saldos y muestra los <c>PagoReversadoPorCartera</c>
/// recibidos; las escrituras con <c>Idempotency-Key</c> de pantalla, aparte de la clave de negocio <c>TST:{guid:N}</c> que la pantalla propone y deja
/// repetir (C35). Si la guarda no registró el recaudador, sus rutas responden el 404 genérico. (nuevo)
/// </summary>
public class TestDoublesEndpoints : ICarterModule
{
    public const string Operar = "Lending.TestDoubles.Operate";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var configuracion = app.ServiceProvider.GetRequiredService<IConfiguration>();
        var ambiente = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!Publicados(configuracion, ambiente)) return;

        var dobles = LendingGroup.Grupo(app, "/test-doubles", "Cartera Dobles de prueba");

        dobles.MapGet("/", (IEnumerable<IDestinoDeMensajes> destinos) =>
                Results.Ok(new DoblesDisponiblesDto(Treasury: true, Collector: RecaudadorDePruebaRegistrado.Esta(destinos))))
            .WithName("Lending_TestDoubles_Available")
            .RequirePermission(Operar);

        dobles.MapGet("/treasury/orders", async (DisbursementOrderStatus? status, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTestTreasuryOrdersQuery(status), ct))
            .WithName("Lending_TestDoubles_TreasuryOrders")
            .RequirePermission(Operar);

        dobles.MapPost("/treasury/orders/{orderPublicId:guid}/results", async (Guid orderPublicId, ResultadoRequest body, HttpContext http,
                IDateTimeService reloj, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new EmitTestDisbursementResultCommand(orderPublicId, body.Outcome ?? string.Empty,
                    body.Reference ?? string.Empty, body.Date ?? reloj.HoyLocal, body.Amount, body.Reason, body.CheckNumber)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/test-doubles/treasury/orders?orderPublicId={orderPublicId}"))
            .WithName("Lending_TestDoubles_TreasuryResult")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        // US5 (T0559): el recaudador de prueba, recaudador TST, clase Test.
        dobles.MapGet("/collector/obligations", async (Guid personPublicId, bool? includeCoDebtor, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTestCollectorObligationsQuery(personPublicId, includeCoDebtor ?? false), ct))
            .WithName("Lending_TestDoubles_CollectorObligations")
            .RequirePermission(Operar);

        dobles.MapPost("/collector/simulations", async (SimulacionRequest body, IDateTimeService reloj, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulateTestCollectorPaymentQuery(body.ObligationPublicId, body.Modality ?? ModalidadDePago.FreeAmount,
                    body.Amount ?? 0m, body.ValueDate ?? reloj.HoyLocal, body.PrepaymentChoice, body.InstallmentNumber, body.PaymentIdempotencyKeyToReverse), ct))
            .WithName("Lending_TestDoubles_CollectorSimulate")
            .RequirePermission(Operar);

        dobles.MapPost("/collector/payments", async (PagosRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyTestCollectorPaymentCommand(body.Items ?? [], body.Interactive) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_TestDoubles_CollectorPayments")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        dobles.MapPost("/collector/payments/{key}/reverse", async (string key, ReversoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ReverseTestCollectorPaymentCommand(Uri.UnescapeDataString(key), body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_TestDoubles_CollectorReverse")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        dobles.MapPost("/collector/credit-balances/resolve", async (ResolucionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResolveTestCollectorCreditBalanceCommand(body.ObligationPublicId, body.Kind ?? FormaDeResolucion.RefundThroughCollector,
                    body.IdempotencyKey ?? $"TST:{Guid.NewGuid():N}", body.Amount, body.Concept ?? ConceptoDeObligacion.CreditBalance,
                    body.TargetObligationPublicId, body.TargetModality, body.PrepaymentChoice) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_TestDoubles_CollectorResolve")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        dobles.MapGet("/collector/reversals", async (ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTestCollectorReversalsQuery(), ct))
            .WithName("Lending_TestDoubles_CollectorReversals")
            .RequirePermission(Operar);

        // Feature 014, US3 (T093; api.md §18.4): el recaudador de prueba sobre el contrato de cuentas —cuotas exigibles, consignar y reversar—.
        dobles.MapGet("/collector/accounts/installments", async (Guid personPublicId, DateOnly? date, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListTestCollectorAccountInstallmentsQuery(personPublicId, date), ct))
            .WithName("Lending_TestDoubles_CollectorAccountInstallments")
            .RequirePermission(Operar);

        dobles.MapPost("/collector/accounts/deposits", async (ConsignacionesRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ApplyTestCollectorAccountDepositsCommand(body.Deposits ?? [], body.NotDeducted) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_TestDoubles_CollectorAccountDeposits")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        dobles.MapPost("/collector/accounts/operations/{key}/reverse", async (string key, ReversoRequest body, HttpContext http, ISender sender,
                    CancellationToken ct) =>
                await sender.Send(new ReverseTestCollectorAccountOperationCommand(Uri.UnescapeDataString(key), body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_TestDoubles_CollectorAccountReverse")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        // K2 (US10, T0850): el recaudador de prueba confirma el envío de un archivo de descuentos y devuelve la respuesta de la pagaduría, como REC.
        dobles.MapPost("/collector/payroll-deduction-files/{id:guid}/confirm-sent", async (Guid id, ConfirmacionDeEnvioRequest? body, HttpContext http,
                ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ConfirmTestPayrollFileSentCommand(id, body?.Channel ?? "Portal", body?.Reference)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/payroll-deduction-files/{id}"))
            .WithName("Lending_TestDoubles_CollectorPayrollFileSent")
            .ConClaveDeOperacion()
            .RequirePermission(Operar);

        dobles.MapPost("/collector/payroll-deduction-results", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                if (!http.Request.HasFormContentType)
                    return Results.Json(new { code = ArchivosTabulares.Vacio.Code, message = ArchivosTabulares.Vacio.Message, traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                var formulario = await http.Request.ReadFormAsync(ct);
                var archivo = formulario.Files.FirstOrDefault();
                if (archivo is null || archivo.Length == 0)
                    return Results.Json(new { code = ArchivosTabulares.Vacio.Code, message = ArchivosTabulares.Vacio.Message, traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status400BadRequest);
                if (archivo.Length > EjecutorDeImportacion.MaximoDeBytes)
                    return Results.Json(new { code = "Archivo.DemasiadoGrande", message = "El archivo pesa más de 16 MB.", traceId = http.TraceIdentifier },
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                if (!Guid.TryParse(formulario["filePublicId"].FirstOrDefault(), out var deducciones))
                    return Results.Json(new
                    {
                        code = "Validation.Invalid", message = "Indique el archivo de descuentos al que responde (filePublicId).", traceId = http.TraceIdentifier,
                    }, statusCode: StatusCodes.Status400BadRequest);
                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms, ct);
                return LendingGroup.Creado(await sender.Send(new EmitTestPayrollDeductionResultCommand(deducciones, archivo.FileName, ms.ToArray())
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), $"/api/lending/payroll-deduction-files/{deducciones}");
            })
            .WithName("Lending_TestDoubles_CollectorPayrollResult")
            .DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(EjecutorDeImportacion.MaximoDeBytes + 256 * 1024))
            .ConClaveDeOperacion()
            .RequirePermission(Operar);
    }

    /// <summary>La confirmación del envío (§20): el canal (<c>Email</c>, <c>Portal</c>, <c>Physical</c>, <c>Other</c>) y el radicado de la pagaduría.</summary>
    public sealed record ConfirmacionDeEnvioRequest(string? Channel, string? Reference);

    /// <summary>Si las rutas se publican: pedidas por configuración y fuera de producción (la guarda que registra los dobles).</summary>
    public static bool Publicados(IConfiguration configuracion, IHostEnvironment ambiente) =>
        configuracion.GetValue(DoblesDePruebaDeCarteraEnDi.ClaveDeConfiguracion, false) && !ambiente.IsProduction();

    /// <summary>Qué dobles hay en este despliegue.</summary>
    public sealed record DoblesDisponiblesDto(bool Treasury, bool Collector);

    /// <summary>
    /// El resultado del giro (§20): <c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>, la referencia de Tesorería, la
    /// fecha (hoy si no viene), el valor (el neto de la orden si no viene; otro valor prueba <c>InvalidResult</c>), el motivo y el cheque.
    /// </summary>
    public sealed record ResultadoRequest(string? Outcome, string? Reference, DateOnly? Date, decimal? Amount, string? Reason, string? CheckNumber);

    /// <summary>
    /// La simulación del recaudador (§20): la obligación, la modalidad, el valor, la fecha valor (hoy si no viene), la elección y la cuota; con la clave
    /// de un pago, la redistribución de su reverso (C38).
    /// </summary>
    public sealed record SimulacionRequest(Guid ObligationPublicId, ModalidadDePago? Modality, decimal? Amount, DateOnly? ValueDate,
        EleccionDeAbono? PrepaymentChoice, int? InstallmentNumber, string? PaymentIdempotencyKeyToReverse);

    /// <summary>Un lote de pagos (§20): los ítems del contrato con su clave <c>TST:</c> y la forma.</summary>
    public sealed record PagosRequest(IReadOnlyList<PagoAAplicar>? Items, bool Interactive);

    /// <summary>El motivo del reverso por la clave del pago.</summary>
    public sealed record ReversoRequest(string? Reason);

    /// <summary>El cuerpo de las consignaciones del recaudador de prueba en cuentas (feature 014).</summary>
    public sealed record ConsignacionesRequest(IReadOnlyList<ConsignacionAAplicar>? Deposits, IReadOnlyList<NoDescontadoDeCuenta>? NotDeducted);

    /// <summary>
    /// La resolución de un saldo (§20, contradicción 6: los valores de <see cref="FormaDeResolucion"/>): la obligación, la forma, la clave (una
    /// <c>TST:</c> nueva si no viene), el valor (todo lo disponible si no viene), el concepto (saldo a favor o anticipo) y, al aplicarlo, la
    /// obligación destino, su modalidad y la elección del abono.
    /// </summary>
    public sealed record ResolucionRequest(Guid ObligationPublicId, FormaDeResolucion? Kind, string? IdempotencyKey, decimal? Amount,
        ConceptoDeObligacion? Concept, Guid? TargetObligationPublicId, ModalidadDePago? TargetModality, EleccionDeAbono? PrepaymentChoice);
}
