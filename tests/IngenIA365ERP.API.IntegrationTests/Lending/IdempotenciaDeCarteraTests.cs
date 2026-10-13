using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0430 (feature 013, US3; quickstart §1.7, nombre fijo; FR-009, SC-010; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>), sobre
/// la cooperativa «desembolso» (<see cref="DesembolsoE2E"/>):
/// <list type="bullet">
/// <item>el desembolso enviado tres veces con la misma <c>Idempotency-Key</c> deja un crédito, una operación, una orden y los mismos mensajes, con
/// la misma respuesta y <c>Idempotent-Replayed: true</c> en las repeticiones; la misma clave con otro cuerpo → <c>Operation.KeyReused</c>;</item>
/// <item>el mismo <c>ResultadoDelGiro</c> consumido dos veces (el reproceso desde la bandeja) no reversa dos veces;</item>
/// <item>el archivo de desembolsos generado dos veces con la misma clave es el mismo archivo.</item>
/// <item>US5 (T0530), en la cooperativa «pagos»: el pago, el reverso y la resolución de un saldo por el contrato, tres veces con la misma clave de
/// negocio <c>TST:</c> → una operación y <c>AlreadyApplied</c> con el comprobante original; la misma clave con otro valor (u otro motivo en el
/// reverso, cuya clave es <c>{clave}:reverso</c>) → <c>Lending.Contract.KeyReused</c>; las rutas de pantalla (la preferencia y el reverso)
/// repiten con <c>Idempotent-Replayed: true</c> (SC-010).</item>
/// </list>
/// La crea esta fase; US5, US6 y US8 le suman sus casos (pago, reverso, reserva, mensaje, carga). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class IdempotenciaDeCarteraTests(CentralIdentityApiFixture fx)
{
    private static bool Repetida(HttpResponseMessage resp) =>
        resp.Headers.TryGetValues("Idempotent-Replayed", out var v) && v.Contains("true");

    [Fact]
    public async Task El_desembolso_tres_veces_con_la_misma_clave_deja_un_credito_y_otra_carga_con_ella_se_rechaza()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, "Idempotente");
        var (solicitud, _) = await AprobadaAsync(http, esc, persona, 1_200_000m);
        var cuerpo = Desembolso(esc, solicitud, persona);
        var clave = Guid.NewGuid();
        var jefe = esc.Usuarios.Jefe.Token;

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++) respuestas.Add(await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Desembolsos, cuerpo, clave));
        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        var textos = new List<string>();
        foreach (var r in respuestas) textos.Add(await r.Content.ReadAsStringAsync());
        textos.Distinct().Should().ContainSingle("las repeticiones devuelven la respuesta original");
        Repetida(respuestas[0]).Should().BeFalse();
        respuestas.Skip(1).Should().OnlyContain(r => Repetida(r), "las repeticiones llevan Idempotent-Replayed: true");

        var hecho = await CarteraE2E.LeerAsync(respuestas[0]);
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var conteo = await EnLaBaseAsync(fx, esc, async db =>
        {
            var solicitudId = await db.CreditApplications.AsNoTracking().Where(s => s.PublicId == solicitud).Select(s => s.Id).SingleAsync();
            var ids = await db.Loans.AsNoTracking().Where(l => l.ApplicationId == solicitudId).Select(l => l.Id).ToListAsync();
            var operaciones = await db.LoanOperations.AsNoTracking().CountAsync(o => ids.Contains(o.LoanId) && o.Kind == LoanOperationKind.Disbursement);
            var ordenes = await db.DisbursementOrders.AsNoTracking().CountAsync(o => ids.Contains(o.LoanId));
            var mensajes = await db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == "CreditoDesembolsado"
                && m.OriginPublicId == hecho.GetProperty("operationPublicId").GetGuid());
            return (Creditos: ids.Count, Operaciones: operaciones, Ordenes: ordenes, Mensajes: mensajes);
        });
        conteo.Should().Be((1, 1, 1, 1), "una clave, un desembolso (SC-010)");
        (await CarteraE2E.GetAsync(http, jefe, $"{Desembolsos}?loanPublicId={credito}")).GetProperty("items").GetArrayLength().Should().Be(1);

        var otraCarga = new Dictionary<string, object?>(cuerpo) { ["valueDate"] = esc.Hoy.AddDays(-1) };
        var reusada = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Desembolsos, otraCarga, clave);
        await InventarioE2E.FallaAsync(reusada, "Operation.KeyReused");
    }

    [Fact]
    public async Task El_mismo_resultado_del_giro_consumido_dos_veces_no_reversa_dos_veces()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "Reproceso");
        var credito = hecho.GetProperty("loanPublicId").GetGuid();
        var orden = hecho.GetProperty("order").GetProperty("publicId").GetGuid();

        var mensaje = await ResultadoDelGiroAsync(http, esc, orden, "Rejected");
        (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<ResultadoDeConsumo.Processed>();
        (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>("el recibo es único por mensaje");

        var conteo = await EnLaBaseAsync(fx, esc, async db =>
        {
            var loanId = await db.Loans.AsNoTracking().Where(l => l.PublicId == credito).Select(l => l.Id).SingleAsync();
            var reversos = await db.LoanOperations.AsNoTracking().CountAsync(o => o.LoanId == loanId && o.Kind == LoanOperationKind.DisbursementReversal);
            var ordenId = await db.DisbursementOrders.AsNoTracking().Where(o => o.PublicId == orden).Select(o => o.Id).SingleAsync();
            var resultados = await db.DisbursementOrderResults.AsNoTracking().CountAsync(r => r.OrderId == ordenId);
            var recibos = await db.LendingInboundMessages.AsNoTracking().CountAsync(r => r.MessagePublicId == mensaje);
            return (Reversos: reversos, Resultados: resultados, Recibos: recibos);
        });
        conteo.Should().Be((1, 1, 1));
    }

    [Fact]
    public async Task El_archivo_generado_dos_veces_con_la_misma_clave_es_el_mismo()
    {
        var esc = await EscenarioAsync(fx);
        using var http = fx.CreateClient();
        var formato = await FormatoDeDesembolsosAsync(http, esc);
        var (_, _, hecho) = await CreditoDesembolsadoAsync(http, esc, "ArchivoIdempotente", transferencia: true, porArchivo: true);
        var pedido = new
        {
            formatPublicId = formato, sourceAccountPublicId = esc.CuentaDeOrigen,
            orderPublicIds = new[] { hecho.GetProperty("order").GetProperty("publicId").GetGuid() },
        };
        var clave = Guid.NewGuid();
        var jefe = esc.Usuarios.Jefe.Token;

        var primero = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Archivos, pedido, clave);
        primero.StatusCode.Should().Be(HttpStatusCode.Created, await primero.Content.ReadAsStringAsync());
        var segundo = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Archivos, pedido, clave);
        segundo.StatusCode.Should().Be(HttpStatusCode.Created, await segundo.Content.ReadAsStringAsync());
        Repetida(segundo).Should().BeTrue();

        var uno = (await CarteraE2E.LeerAsync(primero)).GetProperty("file");
        var dos = (await CarteraE2E.LeerAsync(segundo)).GetProperty("file");
        dos.GetProperty("publicId").GetGuid().Should().Be(uno.GetProperty("publicId").GetGuid());
        dos.GetProperty("sha256").GetString().Should().Be(uno.GetProperty("sha256").GetString());
        (await EnLaBaseAsync(fx, esc, db => db.DisbursementFiles.AsNoTracking().CountAsync(f => f.PublicId == uno.GetProperty("publicId").GetGuid())))
            .Should().Be(1);
    }

    [Fact]
    public async Task El_pago_el_reverso_y_la_resolucion_tres_veces_con_la_misma_clave_de_negocio_quedan_una_vez()
    {
        var esc = await EscenarioAsync(fx, "pagos");
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "ClaveDeNegocio", 2_000_000m);
        var credito = d.GetProperty("loanPublicId").GetGuid();
        var hoy = esc.Hoy.AddDays(1);

        using (CausacionE2E.RelojEn(fx, hoy))
        {
            // El pago: tres veces la misma clave TST (cada envío con su propia Idempotency-Key de pantalla): una operación, el mismo comprobante.
            var clave = PagosE2E.Clave();
            var pago = PagosE2E.Pago(credito, persona, "FreeAmount", 60_000m, hoy, clave);
            var respuestas = new List<System.Text.Json.JsonElement>();
            for (var i = 0; i < 3; i++) respuestas.Add(await PagosE2E.AplicarUnoAsync(http, u, pago));
            respuestas.Select(PagosE2E.Resultado).Should().Equal(PagosE2E.Aplicado, PagosE2E.YaAplicado, PagosE2E.YaAplicado);
            respuestas.Select(r => r.GetProperty("value").GetProperty("number").GetString()).Distinct().Should().ContainSingle("el comprobante original");
            (await PagosE2E.OperacionesAsync(fx, esc, credito)).Count(o => o.Clave == clave).Should().Be(1, "una clave, un pago (SC-010)");
            var reusada = PagosE2E.Pago(credito, persona, "FreeAmount", 61_000m, hoy, clave);
            PagosE2E.Rechazada(await PagosE2E.AplicarUnoAsync(http, u, reusada), "Lending.Contract.KeyReused");

            // El reverso por la clave del pago ({clave}:reverso): repetido, el mismo; con otro motivo, la clave ya se usó.
            var r1 = await PagosE2E.ReversarPorClaveAsync(http, u, clave, "Pago duplicado en caja (e2e)");
            var r2 = await PagosE2E.ReversarPorClaveAsync(http, u, clave, "Pago duplicado en caja (e2e)");
            PagosE2E.Resultado(r1).Should().Be(PagosE2E.Aplicado, r1.ToString());
            PagosE2E.Resultado(r2).Should().Be(PagosE2E.YaAplicado, r2.ToString());
            r2.GetProperty("value").GetProperty("number").GetString().Should().Be(r1.GetProperty("value").GetProperty("number").GetString());
            PagosE2E.Rechazada(await PagosE2E.ReversarPorClaveAsync(http, u, clave, "Otro motivo (e2e)"), "Lending.Contract.KeyReused");
            (await PagosE2E.OperacionesAsync(fx, esc, credito)).Count(o => o.Clase == LoanOperationKind.PaymentReversal).Should().Be(1);

            // La resolución de un anticipo devuelto por el recaudador: tres veces, una; otro valor con la misma clave, KeyReused.
            await PagosE2E.AplicadoAsync(http, u, PagosE2E.Pago(credito, persona, "Advance", 200_000m, hoy));
            var claveDeResolucion = PagosE2E.Clave();
            object Resolucion(decimal valor) => new
            {
                obligationPublicId = credito, kind = "RefundThroughCollector", idempotencyKey = claveDeResolucion, amount = valor, concept = "Advance",
            };
            var resoluciones = new List<System.Text.Json.JsonElement>();
            for (var i = 0; i < 3; i++) resoluciones.Add(await PagosE2E.ResolverAsync(http, u, Resolucion(50_000m)));
            resoluciones.Select(PagosE2E.Resultado).Should().Equal(PagosE2E.Aplicado, PagosE2E.YaAplicado, PagosE2E.YaAplicado);
            PagosE2E.Rechazada(await PagosE2E.ResolverAsync(http, u, Resolucion(60_000m)), "Lending.Contract.KeyReused");
            (await PagosE2E.OperacionesAsync(fx, esc, credito)).Count(o => o.Clase == LoanOperationKind.CreditBalanceResolution).Should().Be(1);
            (await PagosE2E.ObligacionAsync(http, u, persona, credito)).GetProperty("advance").GetDecimal().Should().Be(150_000m);
        }
    }

    [Fact]
    public async Task La_preferencia_y_el_reverso_por_pantalla_repiten_con_la_misma_clave()
    {
        var esc = await EscenarioAsync(fx, "pagos");
        using var http = fx.CreateClient();
        var u = esc.Usuarios;
        var jefe = u.Jefe.Token;
        var (_, persona, d) = await CreditoDesembolsadoAsync(http, esc, "PantallaRepetida", 2_000_000m);
        var credito = d.GetProperty("loanPublicId").GetGuid();
        var hoy = esc.Hoy.AddDays(1);

        // La preferencia: tres veces la misma Idempotency-Key → una versión y las repeticiones con Idempotent-Replayed.
        var clave = Guid.NewGuid();
        var cuerpo = new { choice = "ReduceInstallment", reason = "El deudor prefiere bajar la cuota (e2e)" };
        var antes = (await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}/prepayment-preference")).GetProperty("history").GetArrayLength();
        var envios = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++) envios.Add(await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{credito}/prepayment-preference", cuerpo, clave));
        envios.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created);
        envios.Skip(1).Should().OnlyContain(r => Repetida(r));
        (await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}/prepayment-preference")).GetProperty("history").GetArrayLength().Should().Be(antes + 1);
        var otraCarga = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{credito}/prepayment-preference",
            new { choice = "ReduceTerm", reason = "Otra" }, clave);
        await InventarioE2E.FallaAsync(otraCarga, "Operation.KeyReused");

        // El reverso por pantalla: dos veces la misma Idempotency-Key → un reverso, la misma respuesta.
        using (CausacionE2E.RelojEn(fx, hoy))
        {
            var pago = await PagosE2E.AplicadoAsync(http, u, PagosE2E.Pago(credito, persona, "FreeAmount", 70_000m, hoy));
            var operacion = pago.GetProperty("operationPublicId").GetGuid();
            var previa = await CarteraE2E.ExitoAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{credito}/operations/{operacion}/reversal-preview", new { });
            var reverso = new { reason = "Error de caja (e2e)", expectedPreviewSha256 = previa.GetProperty("previewSha256").GetString() };
            var claveDeReverso = Guid.NewGuid();
            var uno = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{credito}/operations/{operacion}/reverse", reverso, claveDeReverso);
            var dos = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Creditos}/{credito}/operations/{operacion}/reverse", reverso, claveDeReverso);
            uno.StatusCode.Should().Be(HttpStatusCode.Created, await uno.Content.ReadAsStringAsync());
            dos.StatusCode.Should().Be(HttpStatusCode.Created, await dos.Content.ReadAsStringAsync());
            Repetida(dos).Should().BeTrue();
            (await dos.Content.ReadAsStringAsync()).Should().Be(await uno.Content.ReadAsStringAsync());
            (await PagosE2E.OperacionesAsync(fx, esc, credito)).Count(o => o.Clase == LoanOperationKind.PaymentReversal).Should().Be(1);
        }
    }
}
