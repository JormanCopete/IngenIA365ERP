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
}
