using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// T532, T537, T539 (contracts/api.md §25): la parte de mensajes de <c>InventarioClient</c>. La bandeja arma los filtros de §25.2
/// con los enums por nombre y lee los números; reprocesar y el envío posterior llevan la clave de la operación y la conservan en el
/// reintento; la validación previa es una consulta sin clave; un 422 <c>Inventory.Prevalidation.NotPostable</c> se lee como la
/// misma lista de hallazgos que la validación previa, con línea, cuenta, regla y quién corrige.
/// </summary>
public class InventarioClientMensajesTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly InventarioClient _cliente;

    public InventarioClientMensajesTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new InventarioClient(http, auth);
    }

    [Fact]
    public async Task La_bandeja_manda_los_filtros_por_nombre_y_lee_los_contadores()
    {
        var mensaje = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            messages = new { items = new[] { MensajeJson(mensaje, estado: 3) }, page = 1, pageSize = 50, totalCount = 1 },
            countsByStatus = new { pending = 4, inBatch = 1, processed = 20, rejected = 1, notApplicable = 2, validationFailed = 0 },
            cutoffMessagePublicId = (Guid?)null,
        });

        var r = await _cliente.MensajesAsync(new FiltroDeMensajes(Estado: 3, Destino: "Accounting", TipoDeDocumento: "AJP",
            Desde: new DateOnly(2026, 9, 1), Hasta: new DateOnly(2026, 9, 30), ValidacionPrevia: 2));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/messages?status=Rejected&destination=Accounting&documentType=AJP&from=2026-09-01&to=2026-09-30"
                               + "&prevalidationOutcome=NoResponse&page=1&pageSize=50");
        vista.Clave.Should().BeNull();
        r.Value!.CountsByStatus.Pending.Should().Be(4);
        var fila = r.Value.Messages.Items.Single();
        fila.MessagePublicId.Should().Be(mensaje);
        fila.DeliveryStatus.Should().Be(3);
        fila.BlockedBy.Single().DeliveryStatus.Should().Be(3);
        fila.Origin.OperationDate.Should().Be(new DateOnly(2026, 9, 15));
    }

    [Fact]
    public async Task La_vista_previa_del_envio_posterior_pide_la_clausura_y_trae_el_corte()
    {
        var corte = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            messages = new { items = Array.Empty<object>(), page = 1, pageSize = 200, totalCount = 0 },
            countsByStatus = new { pending = 0, inBatch = 0, processed = 0, rejected = 0, notApplicable = 5, validationFailed = 0 },
            cutoffMessagePublicId = corte,
        });

        var r = await _cliente.MensajesAsync(new FiltroDeMensajes(Estado: 4, Destino: "Accounting", Desde: new DateOnly(2026, 8, 1),
            Hasta: new DateOnly(2026, 8, 31), Clausura: true, TamanoDePagina: 200));

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.CutoffMessagePublicId.Should().Be(corte);
        _servidor.Vistas.Single().Ruta.Should().Contain("status=NotApplicable").And.Contain("closure=true").And.Contain("pageSize=200");
    }

    [Fact]
    public async Task Reprocesar_lleva_la_clave_y_el_reintento_la_repite()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.GatewayTimeout),
            ServidorDeIntegracion.Json(HttpStatusCode.Accepted, new { batchPublicId = Guid.NewGuid(), number = 9, trigger = 5, messages = 2, dragged = 1 }),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        (await _cliente.ReprocesarMensajesAsync(ids, "Se cargó la regla que faltaba", clave)).IsSuccess.Should().BeFalse();
        var r = await _cliente.ReprocesarMensajesAsync(ids, "Se cargó la regla que faltaba", clave);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Dragged.Should().Be(1);
        _servidor.Vistas[0].Ruta.Should().Be("/api/inventory/messages/reprocess");
        _servidor.Vistas[1].Clave.Should().Be(_servidor.Vistas[0].Clave).And.NotBeNull();
    }

    [Fact]
    public async Task El_envio_posterior_lleva_el_corte_de_la_vista_previa_y_su_clave()
    {
        var corte = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Accepted, new { batchPublicId = Guid.NewGuid(), number = 3, trigger = 6, documents = 4, messages = 6 });

        var r = await _cliente.EnviarNoAplicaAsync(new EnvioPosteriorRequest(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), null, corte, "Los traslados ahora pasan"),
            new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Documents.Should().Be(4);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/messages/send-not-applicable");
        vista.Clave.Should().NotBeNull();
        vista.Cuerpo.Should().Contain(corte.ToString());
    }

    [Fact]
    public async Task La_validacion_previa_es_una_consulta_sin_clave()
    {
        var documento = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            applies = true, postingMode = 1, responded = true, isPostable = false,
            errors = new[]
            {
                new { messageType = "AjusteInventarioAprobado", lineNumber = 2, account = new { code = "613520", name = (string?)null }, rule = "Accounting.Line.CostCenterRequired",
                      message = "La cuenta 613520 exige centro de costo.", whoFixes = new { module = "Inventario", page = "/inventario/ajustes", permission = "Inventory.Adjustments.Create" } },
            },
            warnings = Array.Empty<object>(),
            elapsedMs = 42,
        });

        var r = await _cliente.ValidarConContabilidadAsync(documento);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be($"/api/inventory/documents/{documento}/prevalidate");
        vista.Clave.Should().BeNull("la validación previa no guarda nada");
        var hallazgo = r.Value!.Hallazgos().Single();
        hallazgo.LineNumber.Should().Be(2);
        hallazgo.Account.Should().Be("613520");
        hallazgo.WhoFixes!.Page.Should().Be("/inventario/ajustes");
    }

    [Fact]
    public async Task El_422_de_la_confirmacion_se_lee_como_la_misma_lista_de_hallazgos()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Inventory.Prevalidation.NotPostable",
            message = "Contabilidad no puede contabilizar el documento.",
            data = new
            {
                errors = new[]
                {
                    new { lineNumber = (int?)null, account = (string?)null, rule = "Accounting.InventoryRule.Missing", message = "No hay regla vigente.",
                          whoFixes = new { module = "Contabilidad", page = "/contabilidad/inventario/matriz", permission = "Accounting.InventoryRules.Manage" } },
                },
            },
        });

        var r = await _cliente.ConfirmarAjusteAsync(Guid.NewGuid(), [1], new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        var hallazgos = HallazgosDeValidacionPrevia.DelRechazo(r.ErrorCode, r.Data);
        hallazgos.Should().NotBeNull();
        hallazgos!.Single().Rule.Should().Be("Accounting.InventoryRule.Missing");
        hallazgos.Single().WhoFixes!.Module.Should().Be("Contabilidad");
        HallazgosDeValidacionPrevia.DelRechazo("Inventory.Stock.Insufficient", r.Data).Should().BeNull("sólo los dos códigos de la validación previa");
    }

    private static object MensajeJson(Guid id, int estado) => new
    {
        messagePublicId = id, emittedAt = ServidorDeIntegracion.Ahora, type = "DevolucionRegistrada", version = 1, kind = 1, destination = "Accounting",
        deliveryStatus = estado, mode = 1, scheduleKey = (string?)null, batch = (object?)null, attempts = 3, nextAttemptAt = (DateTime?)null,
        lastError = new { code = "Accounting.InventoryRule.Missing", message = "Falta la regla.", dataJson = (string?)null },
        processedAt = (DateTime?)null, result = (object?)null,
        origin = new { kind = 1, documentClass = "9", documentTypeCode = "DP", number = "DP-12", publicId = Guid.NewGuid(), operationDate = "2026-09-15" },
        related = (object?)null, originUserName = "Ana", prevalidationOutcome = 1,
        blockedBy = new[] { new { messagePublicId = Guid.NewGuid(), type = "CompraRecibida", deliveryStatus = 3 } },
        destinationAvailable = true, pendingValidation = (bool?)null,
    };
}
