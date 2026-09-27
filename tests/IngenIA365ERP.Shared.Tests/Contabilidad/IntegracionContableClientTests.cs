using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Contabilidad;

/// <summary>
/// T532 (contracts/api.md §26; T13): el cliente del lado contable de la integración con Inventario. Las consultas y la matriz no
/// llevan clave de operación (la 009 no la usa); ordenar un lote sí, y la conserva en el reintento; los filtros de enum viajan
/// por nombre y las respuestas los traen como número; un 422 conserva su <c>data</c>; nadie pone <c>Authorization</c> a mano.
/// </summary>
public class IntegracionContableClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly IntegracionContableClient _cliente;

    public IntegracionContableClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new IntegracionContableClient(http, auth);
    }

    [Fact]
    public async Task La_lista_de_reglas_arma_sus_filtros_y_no_lleva_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { items = Array.Empty<object>(), page = 1, pageSize = 50, totalCount = 0 });

        var r = await _cliente.ReglasAsync(new FiltroDeReglasDeInventario(Operacion: "CompraRecibida", Rol: "Inventario", Cuenta: "1435", SoloVigentes: true));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/accounting/inventory/rules?operation=CompraRecibida&role=Inventario&account=1435&onlyCurrent=true&page=1&pageSize=50");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Crear_una_regla_no_lleva_clave_y_devuelve_los_avisos()
    {
        var id = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new
        {
            rulePublicId = id, warnings = new[] { new { code = "Accounting.InventoryRule.TaxRateMismatch", message = "La tarifa cambia en marzo." } },
        });

        var r = await _cliente.CrearReglaAsync(new CrearReglaDeInventarioRequest("CompraRecibida", "Inventario",
            new DimensionesDeReglaDeInventarioRequest(AccountingGroupCode: "ABARROTES"), Guid.NewGuid(), new DateOnly(2026, 10, 1), null, "Alta inicial"));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.RulePublicId.Should().Be(id);
        r.Value.Warnings.Should().ContainSingle().Which.Code.Should().Be("Accounting.InventoryRule.TaxRateMismatch");
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/accounting/inventory/rules");
        vista.Clave.Should().BeNull("la matriz sigue la mecánica de la 009: sin clave de operación");
        vista.Cuerpo.Should().Contain("\"accountingGroupCode\":\"ABARROTES\"");
    }

    [Fact]
    public async Task Los_filtros_de_lote_viajan_por_nombre_y_la_respuesta_trae_numeros()
    {
        var lote = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            items = new[] { LoteJson(lote, status: 3, trigger: 4) }, page = 1, pageSize = 20, totalCount = 1,
        });

        var r = await _cliente.LotesAsync(new FiltroDeLotesDeIntegracion(Estado: 3, Disparador: 4, Desde: new DateOnly(2026, 9, 1)));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        _servidor.Vistas.Single().Ruta.Should().Be("/api/accounting/inventory/batches?from=2026-09-01&status=CompletedWithRejections&trigger=Manual&page=1&pageSize=20");
        var fila = r.Value!.Items.Single();
        fila.BatchPublicId.Should().Be(lote);
        fila.Status.Should().Be(3);
        fila.Trigger.Should().Be(4);
        fila.Totals.Rejected.Should().Be(2);
        fila.RequestedBy.Kind.Should().Be(1);
    }

    [Fact]
    public async Task Ordenar_un_lote_lleva_la_clave_y_la_conserva_en_el_reintento()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            ServidorDeIntegracion.Json(HttpStatusCode.Accepted, new { batchPublicId = Guid.NewGuid(), number = 7, status = 0 }),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var orden = new OrdenDeLoteDeIntegracionRequest(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), ["AJP"], null, null, "Cierre de septiembre");

        (await _cliente.OrdenarLoteAsync(orden, clave)).IsSuccess.Should().BeFalse();
        var r = await _cliente.OrdenarLoteAsync(orden, clave);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Number.Should().Be(7);
        _servidor.Vistas.Should().HaveCount(2);
        _servidor.Vistas[0].Clave.Should().NotBeNullOrWhiteSpace();
        _servidor.Vistas[1].Clave.Should().Be(_servidor.Vistas[0].Clave, "el reintento del mismo intento no ordena dos lotes");
        _servidor.Vistas[0].Ruta.Should().Be("/api/accounting/inventory/batches");
    }

    [Fact]
    public async Task Un_lote_ya_en_curso_conserva_su_data()
    {
        var enCurso = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Accounting.InventoryBatch.AlreadyRunning", message = "Ya hay un lote en curso.", data = new { batchPublicId = enCurso, number = 12 },
        });

        var r = await _cliente.OrdenarLoteAsync(new OrdenDeLoteDeIntegracionRequest(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30),
            null, null, null, "Otra vez"), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Accounting.InventoryBatch.AlreadyRunning");
        r.Dato<Guid>("batchPublicId").Should().Be(enCurso);
        r.Entero("number").Should().Be(12);
    }

    [Fact]
    public async Task La_vista_previa_es_una_consulta_sin_clave_y_trae_el_corte()
    {
        var corte = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            cutoffMessagePublicId = corte,
            documents = Array.Empty<object>(),
            vouchers = new[]
            {
                new
                {
                    voucherTypeCode = "AC", operationDate = "2026-09-15", branch = "PPAL", costCenter = (string?)null, granularity = 2, documentsCount = 3,
                    lines = new[] { new { account = new { code = "143501", name = "Mercancías" }, debit = 100m, credit = 0m, thirdParty = (string?)null, crossDocument = (string?)null, taxBase = (decimal?)null } },
                    totals = new { debit = 100m, credit = 100m },
                },
            },
            excluded = Array.Empty<object>(),
        });

        var r = await _cliente.VistaPreviaDeLoteAsync(new VistaPreviaDeLoteDeIntegracionRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), null, null, null));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.CutoffMessagePublicId.Should().Be(corte);
        r.Value.Vouchers.Single().Granularity.Should().Be(2);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/accounting/inventory/batches/preview");
        vista.Clave.Should().BeNull("previsualizar no guarda nada");
    }

    [Fact]
    public async Task La_completitud_pide_su_fecha_y_trae_las_cinco_listas()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            date = "2026-09-26",
            missingRules = new[] { new { operation = "CompraRecibida", role = "Inventario", accountingGroupCode = "ABARROTES", warehouseCode = (string?)null, pointOfSaleCode = (string?)null, paymentMeansCode = (string?)null, taxRateCode = (string?)null, reasonCode = (string?)null, usedByDocumentTypes = new[] { "EC" }, lastUsedAt = (string?)null } },
            paymentMeansWithoutAccount = Array.Empty<object>(),
            ineligibleRules = Array.Empty<object>(),
            taxRateMismatches = Array.Empty<object>(),
            unmappedOperations = new[] { new { operation = "AjusteDeCostoReconocido", inventoryDocumentTypeCode = (string?)null } },
            summary = new { total = 2, byKind = new Dictionary<string, int> { ["missingRules"] = 1, ["unmappedOperations"] = 1 } },
            warnings = Array.Empty<object>(),
        });

        var r = await _cliente.CompletitudAsync(new DateOnly(2026, 9, 26));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        _servidor.Vistas.Single().Ruta.Should().Be("/api/accounting/inventory/completeness?date=2026-09-26");
        r.Value!.MissingRules.Single().UsedByDocumentTypes.Should().Equal("EC");
        r.Value.UnmappedOperations.Single().Operation.Should().Be("AjusteDeCostoReconocido");
        r.Value.Summary.Total.Should().Be(2);
    }

    [Fact]
    public async Task Fijar_un_mapeo_usa_PUT_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { mappingPublicId = Guid.NewGuid() });

        var r = await _cliente.FijarMapeoAsync(new MapeoDeComprobanteDeInventarioRequest("AjusteInventarioAprobado", "AJP", Guid.NewGuid(), null, "Ajustes a su propio tipo"));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("PUT");
        vista.Ruta.Should().Be("/api/accounting/inventory/voucher-mappings");
        vista.Clave.Should().BeNull();
    }

    private static object LoteJson(Guid id, int status, int trigger) => new
    {
        batchPublicId = id, number = 5L, destination = "Accounting", trigger, scheduleKey = (string?)null, scheduledFor = (DateTime?)null,
        cashSessionPublicId = (Guid?)null, period = (object?)null, dateFrom = "2026-09-01", dateTo = "2026-09-30", granularity = 2, status,
        requestedBy = new { kind = 1, name = "Rafaela" }, requestedAt = ServidorDeIntegracion.Ahora, startedAt = (DateTime?)null, finishedAt = (DateTime?)null,
        totals = new { messages = 10, documents = 8, vouchers = 3, rejected = 2, debit = 1000m, credit = 1000m }, late = false,
    };
}
