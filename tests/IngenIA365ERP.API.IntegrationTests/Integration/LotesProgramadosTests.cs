using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadDeInventarioE2E;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T473 (feature 012, I2; contracts/contabilidad.md §5.4 y §5.6; T12; SC-020), en el motor de <c>DB_PROVIDER</c>, en la
/// cooperativa aislada «lotes» con los ajustes positivos por lotes resumidos a una hora propia: antes de la hora no hay lote;
/// a la hora (el reloj de la suite adelantado) nace un lote <c>Scheduled</c> por <c>ScheduleKey</c> y dos pasadas simultáneas
/// no lo duplican; un lote vencido más la tolerancia técnica levanta <c>Integracion.LoteNoCorrio</c>; ordenar otra vez lo ya
/// procesado deja un lote <c>Empty</c>. El volumen de SC-020 sólo corre con <c>RUN_PERF_TESTS=1</c>.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class LotesProgramadosTests(CentralIdentityApiFixture fx)
{
    private const string Lotes = "/api/accounting/inventory/batches";

    [Fact]
    public async Task A_la_hora_nace_un_lote_por_clave_sin_duplicarse_y_el_atrasado_alerta()
    {
        var esc = await PrepararAsync(fx, "lotes");
        using var http = fx.CreateClient();
        var coop = esc.Coop.TenantPublicId;
        var conductor = fx.Despachador;
        var t = esc.Admin;

        // El reloj de la suite a las 10:00 de hoy (hora de Colombia) y la hora del lote a las 10:10: el caso no depende de la
        // hora en que corra la prueba ni cruza el día.
        var hoy = InventarioE2E.HoyEnColombia;
        var ahora = conductor.Reloj.AhoraLocal;
        using var _ = conductor.Adelantar(new DateTimeOffset(hoy.ToDateTime(new TimeOnly(10, 0)), ahora.Offset) - ahora);
        var hora = new TimeOnly(10, 10);
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "PorLotes", new DateOnly(hoy.Year, 1, 1), tipo: esc.Tipos["AJP"]);
        await ParametroAsync(http, t, "Contabilidad.Granularidad", "Resumido", new DateOnly(hoy.Year, 1, 1), tipo: esc.Tipos["AJP"]);
        await ParametroAsync(http, t, "Contabilidad.HoraDeLote", hora.ToString("HH:mm"), hoy, tipo: esc.Tipos["AJP"]);

        var a1 = (await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P2", 2, 1_000m)])).GetProperty("publicId").GetGuid();
        var a2 = (await esc.AjusteConfirmadoAsync(http, t, "AJP", "PRIN", [new("P3", 2, 1_000m)])).GetProperty("publicId").GetGuid();
        var entrega = await MensajeAsync(http, t, a1, "AjusteInventarioAprobado");
        Estado(entrega).Should().Be(1, "InBatch esperando su franja");
        entrega.GetProperty("scheduleKey").GetString().Should().NotBeNullOrWhiteSpace();

        // Antes de la hora, nada.
        await conductor.PasadaAsync(coop);
        Programados(await InventarioE2E.GetAsync(http, t, $"{Lotes}?trigger=Scheduled&pageSize=50")).Should().BeEmpty();

        // El reloj sólo avanza: un arrendamiento soltado con el reloj adelantado no se podría volver a tomar si retrocediera.
        conductor.Reloj.Desfase += TimeSpan.FromMinutes(12);

        // Dos «réplicas» a la vez: el índice único de la franja deja uno solo.
        var dos = await Task.WhenAll(
            conductor.EnviarComoProcesoAsync<Result<LotesProgramadosDto>>(coop, "Tarea:prueba", new ScheduleIntegrationBatchesCommand(30)),
            conductor.EnviarComoProcesoAsync<Result<LotesProgramadosDto>>(coop, "Tarea:prueba", new ScheduleIntegrationBatchesCommand(30)));
        dos.Should().OnlyContain(r => r.IsSuccess);
        dos.Sum(r => r.Value.Created.Count).Should().Be(1, "una franja, un lote");
        var programados = Programados(await InventarioE2E.GetAsync(http, t, $"{Lotes}?trigger=Scheduled&pageSize=50"));
        programados.Should().ContainSingle();
        var lote = programados[0];
        lote.GetProperty("status").GetInt32().Should().Be(0, "Requested: todavía nadie lo corrió");

        // Vencido más la tolerancia (30 minutos) y sin correr: la pasada levanta la alerta y lo corre.
        conductor.Reloj.Desfase += TimeSpan.FromMinutes(38);
        await conductor.PasadasAsync(coop, 2);
        var alertas = await InventarioE2E.GetAsync(http, t, "/api/inventory/alerts?typeCode=Integracion.LoteNoCorrio&pageSize=50");
        alertas.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0, "el lote programado no corrió a su hora");
        var detalle = await InventarioE2E.GetAsync(http, t, $"{Lotes}/{lote.GetProperty("batchPublicId").GetGuid()}");
        detalle.GetProperty("batch").GetProperty("status").GetInt32().Should().Be(2, $"completo: {detalle}");
        Estado(await MensajeAsync(http, t, a2, "AjusteInventarioAprobado")).Should().Be(2);

        // Ordenar otra vez lo ya procesado: el lote queda vacío, sin comprobante nuevo.
        var orden = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, Lotes, new
        {
            cutoffMessagePublicId = entrega.GetProperty("messagePublicId").GetGuid(), from = hoy.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"),
            documentTypeCodes = new[] { "AJP" }, reason = "Repetir el rango",
        });
        if (orden.StatusCode == HttpStatusCode.Accepted)
        {
            var vacio = (await InventarioE2E.LeerAsync(orden)).GetProperty("batchPublicId").GetGuid();
            await conductor.PasadaAsync(coop);
            (await InventarioE2E.GetAsync(http, t, $"{Lotes}/{vacio}")).GetProperty("batch").GetProperty("status").GetInt32().Should().Be(4, "Empty");
        }
        else
        {
            // La orden sin nada que tomar puede responder de una vez que no hay qué procesar; en ningún caso hay comprobante nuevo.
            orden.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await orden.Content.ReadAsStringAsync());
        }
        (await ComprobantesDelDocumentoAsync(fx, esc, a1)).Should().Be(1);
    }

    [FactDeRendimiento]
    public async Task Cinco_mil_documentos_resumidos_en_menos_de_diez_minutos()
    {
        var esc = await PrepararAsync(fx, "lotes5k");
        using var http = fx.CreateClient();
        var coop = esc.Coop.TenantPublicId;
        var t = esc.Admin;
        var hoy = InventarioE2E.HoyEnColombia;
        await ParametroAsync(http, t, "Contabilidad.ModoDePaso", "PorLotes", new DateOnly(hoy.Year, 1, 1), tipo: esc.Tipos["AJP"]);
        await ParametroAsync(http, t, "Contabilidad.Granularidad", "Resumido", new DateOnly(hoy.Year, 1, 1), tipo: esc.Tipos["AJP"]);
        for (var i = 0; i < 5_000; i++)
            await esc.AjusteConfirmadoAsync(http, t, "AJP", i % 2 == 0 ? "PRIN" : "PV2", [new($"P{i % 5 + 2}", 1, 100m)]);

        var previa = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Lotes}/preview", new
        {
            from = hoy.ToString("yyyy-MM-dd"), to = hoy.ToString("yyyy-MM-dd"), documentTypeCodes = new[] { "AJP" },
        });
        var cuerpo = new
        {
            cutoffMessagePublicId = previa.GetProperty("cutoffMessagePublicId").GetGuid(), from = hoy.ToString("yyyy-MM-dd"),
            to = hoy.ToString("yyyy-MM-dd"), documentTypeCodes = new[] { "AJP" }, reason = "SC-020",
        };
        var lote = (await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, Lotes, cuerpo)).GetProperty("batchPublicId").GetGuid();
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        while (reloj.Elapsed < TimeSpan.FromMinutes(10))
        {
            await fx.Despachador.PasadaAsync(coop);
            if ((await InventarioE2E.GetAsync(http, t, $"{Lotes}/{lote}")).GetProperty("batch").GetProperty("status").GetInt32() == 2) break;
        }
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(10), "SC-020");
        var comprobantes = await EnteroAsync(fx, esc, """SELECT COUNT(*) FROM dbo."ACC_Documents" WHERE "OriginModule" = 'INV'""",
            "SELECT COUNT(*) FROM [dbo].[ACC_Documents] WHERE [OriginModule] = 'INV'");

        var otra = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, Lotes, cuerpo);
        if (otra.IsSuccessStatusCode) await fx.Despachador.PasadaAsync(coop);
        (await EnteroAsync(fx, esc, """SELECT COUNT(*) FROM dbo."ACC_Documents" WHERE "OriginModule" = 'INV'""",
            "SELECT COUNT(*) FROM [dbo].[ACC_Documents] WHERE [OriginModule] = 'INV'")).Should().Be(comprobantes, "repetirlo no crea nada");
    }

    private static List<System.Text.Json.JsonElement> Programados(System.Text.Json.JsonElement pagina) =>
        pagina.GetProperty("items").EnumerateArray().Where(b => b.GetProperty("trigger").GetInt32() == 1).ToList();
}
