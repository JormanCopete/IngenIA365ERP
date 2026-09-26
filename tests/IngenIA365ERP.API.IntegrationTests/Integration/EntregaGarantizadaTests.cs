using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadDeInventarioE2E;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T472 (feature 012, I2; quickstart §4.9; SC-002, SC-010; FR-015, FR-076, FR-083), en el motor de <c>DB_PROVIDER</c>, en la
/// cooperativa aislada «entrega» con la contabilidad iniciada y la matriz mínima: el despachador apagado no impide confirmar
/// y, al correr, procesa lo en línea; un mensaje entregado tres veces deja un solo comprobante y un solo recibo; con
/// Contabilidad sin responder (<see cref="DoblesDeIntegracion"/>) manda <c>Contabilidad.PoliticaSinRespuesta</c>; un fallo
/// transitorio del destino reintenta con espera creciente y alerta <c>Integracion.MensajeSinEntregar</c>; y la auditoría del
/// consumo la firma «Proceso de integración», canal proceso, origen <c>Mensaje:{id}</c>, sin IP, en la base de la cooperativa.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class EntregaGarantizadaTests(CentralIdentityApiFixture fx)
{
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";
    private const string Ajustes = "/api/inventory/adjustments";

    [Fact]
    public async Task Apagado_confirma_y_al_correr_procesa_una_sola_vez_aunque_se_entregue_tres_veces()
    {
        var esc = await PrepararAsync(fx, "entrega");
        using var http = fx.CreateClient();
        var coop = esc.Coop.TenantPublicId;

        // El despachador está apagado: confirmar no depende de él (SC-010).
        var ajuste = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P2", 3, 1_000m)]);
        var documento = ajuste.GetProperty("publicId").GetGuid();
        var mensaje = await MensajeAsync(http, esc.Admin, documento, "AjusteInventarioAprobado");
        Estado(mensaje).Should().Be(0, "pendiente, visible en la bandeja");

        // «Encenderlo»: una pasada procesa lo en línea sin que nadie lo ordene.
        await fx.Despachador.PasadaAsync(coop);
        mensaje = await MensajeAsync(http, esc.Admin, documento, "AjusteInventarioAprobado");
        Estado(mensaje).Should().Be(2, $"procesado: {mensaje}");
        var id = mensaje.GetProperty("messagePublicId").GetGuid();

        // Dos entregas más del mismo mensaje: el recibo único responde «ya procesado» (SC-002).
        for (var i = 0; i < 2; i++)
        {
            var otra = await fx.Despachador.EnviarComoProcesoAsync<Result<ResultadoDeConsumo>>(coop, Actor.OrigenDeMensaje(id),
                new PostInventoryMessagesCommand([id], null));
            otra.IsSuccess.Should().BeTrue();
            otra.Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>()
                .Which.AccountingDocumentPublicId.Should().Be(Comprobante(mensaje));
        }
        (await ComprobantesDelDocumentoAsync(fx, esc, documento)).Should().Be(1, "un solo comprobante");
        (await EnteroAsync(fx, esc,
            $"""SELECT COUNT(*) FROM dbo."ACC_InventoryPostings" WHERE "MessagePublicId" = '{id}'""",
            $"SELECT COUNT(*) FROM [dbo].[ACC_InventoryPostings] WHERE [MessagePublicId] = '{id}'")).Should().Be(1, "un solo recibo");

        // La auditoría del consumo: el proceso, en la base de la cooperativa, con canal y origen, sin IP (FR-083).
        var evento = await EventoDelProcesoAsync(coop, id.ToString());
        evento.Should().NotBeNull("el consumo audita en la base de la cooperativa");
        evento!["userName"].AsString.Should().Be(Actor.NombreDelProceso);
        (evento.Contains("ipAddress") && !evento["ipAddress"].IsBsonNull && evento["ipAddress"].AsString.Length > 0).Should().BeFalse("sin IP");
        var metadata = evento["metadata"].AsBsonDocument.Values.Select(v => v.ToString()).ToList();
        metadata.Should().Contain(v => string.Equals(v, "proceso", StringComparison.OrdinalIgnoreCase) || string.Equals(v, "Process", StringComparison.OrdinalIgnoreCase));
        metadata.Should().Contain(Actor.OrigenDeMensaje(id));
    }

    [Fact]
    public async Task Sin_respuesta_de_Contabilidad_manda_la_politica()
    {
        var esc = await PrepararAsync(fx, "entrega");
        using var http = fx.CreateClient();
        var coop = esc.Coop.TenantPublicId;
        var hoy = InventarioE2E.HoyEnColombia;

        fx.Dobles.ContabilidadNoResponde(coop);
        try
        {
            // ConfirmarConPendiente (el defecto): confirma, avisa y sella NoResponse.
            var confirmado = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 2, 1_000m)]);
            confirmado.GetProperty("prevalidation").GetProperty("outcome").GetInt32().Should().Be(2, "NoResponse");
            confirmado.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("code").GetString())
                .Should().Contain("Inventory.Prevalidation.NoResponse");
            var mensaje = await MensajeAsync(http, esc.Admin, confirmado.GetProperty("publicId").GetGuid(), "AjusteInventarioAprobado");
            mensaje.GetProperty("prevalidationOutcome").GetInt32().Should().Be(2);

            // Bloquear: no confirma.
            await ParametroAsync(http, esc.Admin, "Contabilidad.PoliticaSinRespuesta", "Bloquear", hoy);
            var borrador = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P3", 1, 1_000m)]);
            await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, Ajustes, borrador), "Inventory.Prevalidation.NoResponse");
            (await InventarioE2E.GetAsync(http, esc.Admin, $"{Ajustes}/{borrador}")).GetProperty("status").GetInt32().Should().Be(0);
        }
        finally
        {
            fx.Dobles.ContabilidadNoResponde(coop, false);
            await ParametroAsync(http, esc.Admin, "Contabilidad.PoliticaSinRespuesta", "ConfirmarConPendiente", hoy.AddDays(1));
        }

        // Cuando vuelve a responder, lo confirmado sin respuesta se contabiliza igual.
        await fx.Despachador.PasadaAsync(coop);
    }

    [Fact]
    public async Task Un_fallo_transitorio_reintenta_con_espera_creciente_y_alerta()
    {
        var esc = await PrepararAsync(fx, "entregareintento");
        using var http = fx.CreateClient();
        var coop = esc.Coop.TenantPublicId;
        var conductor = fx.Despachador;

        var ajuste = await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P4", 1, 1_000m)]);
        var documento = ajuste.GetProperty("publicId").GetGuid();

        fx.Dobles.DestinoFalla(coop, 3);
        var esperas = new List<TimeSpan>();
        using (conductor.Adelantar(TimeSpan.Zero))
        {
            for (var intento = 1; intento <= 3; intento++)
            {
                var antes = conductor.Reloj.UtcNow;
                await conductor.PasadaAsync(coop);
                var m = await MensajeAsync(http, esc.Admin, documento, "AjusteInventarioAprobado");
                Estado(m).Should().Be(0, $"sigue pendiente tras el intento {intento}: {m}");
                m.GetProperty("attempts").GetInt32().Should().Be(intento);
                var siguiente = m.GetProperty("nextAttemptAt").GetDateTime().ToUniversalTime();
                esperas.Add(siguiente - antes);
                conductor.Reloj.Desfase += (siguiente - antes) + TimeSpan.FromSeconds(1);
            }
        }
        esperas[1].Should().BeGreaterThan(esperas[0], "la espera crece");
        esperas[2].Should().BeGreaterThan(esperas[1], "la espera crece");

        var alertas = await InventarioE2E.GetAsync(http, esc.Admin, "/api/inventory/alerts?typeCode=Integracion.MensajeSinEntregar&pageSize=50");
        alertas.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0, "a los tres intentos se levanta la alerta");

        // Agotado el fallo, la siguiente pasada lo entrega (el reloj ya volvió al real: se fuerza el vencimiento).
        using (conductor.Adelantar(TimeSpan.FromHours(1)))
        {
            await conductor.PasadaAsync(coop);
        }
        Estado(await MensajeAsync(http, esc.Admin, documento, "AjusteInventarioAprobado")).Should().Be(2);
    }

    private async Task<BsonDocument?> EventoDelProcesoAsync(Guid cooperativa, string texto)
    {
        var coleccion = fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, cooperativa.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
        var auditoria = fx.Factory.Services.GetRequiredService<IAuditService>();
        var filtro = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Regex("newValuesJson", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(texto))),
            Builders<BsonDocument>.Filter.Eq("userName", Actor.NombreDelProceso),
            Builders<BsonDocument>.Filter.Exists("metadata"));
        var limite = DateTime.UtcNow.AddSeconds(20);
        do
        {
            await auditoria.FlushAsync();
            var evento = await coleccion.Find(filtro).FirstOrDefaultAsync();
            if (evento is not null && evento["metadata"].IsBsonDocument) return evento;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        return null;
    }
}
