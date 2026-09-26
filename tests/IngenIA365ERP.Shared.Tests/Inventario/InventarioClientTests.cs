using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// T183 (T13, T45; contracts/api.md §2.3, §8, §9, contracts/plantillas.md §0.5): el cliente base de Inventario. Toda
/// escritura lleva la <c>Idempotency-Key</c> de la operación de pantalla —la misma en el reintento con el mismo
/// contenido, otra tras el éxito—; las consultas no la llevan; ninguna pone <c>Authorization</c> a mano
/// (<c>ElTokenDeSesionLoPoneElHandler</c>); un 422 conserva su <c>data</c> (existencias, errores de importación) y la
/// revisión y la aplicación de una plantilla son dos operaciones con su propia clave.
/// </summary>
public class InventarioClientTests
{
    private static readonly DateTime Ahora = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly Servidor _servidor = new();
    private readonly InventarioClient _cliente;

    public InventarioClientTests()
    {
        var almacen = new AlmacenEnMemoria();
        var sesion = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), new RelojFijo(Ahora));
        // Sin AuthBearerHandler en la cadena, a propósito: si el cliente pusiera Authorization, se vería aquí.
        var http = new HttpClient(_servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());
        auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12)).GetAwaiter().GetResult();
        _servidor.Vistas.Clear();
        _cliente = new InventarioClient(http, auth);
    }

    private static MotivoDeInventarioRequest Motivo(string texto) => new(texto);

    [Fact]
    public async Task Una_escritura_lleva_la_clave_de_la_operacion_y_no_pone_Authorization()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, TipoJson());
        var clave = new ClaveDeOperacion();

        var r = await _cliente.InactivarTipoAsync(Guid.NewGuid(), Motivo("Ya no se usa"), clave);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task El_reintento_con_el_mismo_contenido_repite_la_clave_y_tras_el_exito_cambia()
    {
        var respuestas = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            Servidor.Json(HttpStatusCode.OK, TipoJson()),
            Servidor.Json(HttpStatusCode.OK, TipoJson()),
        ]);
        _servidor.Responder = _ => respuestas.Dequeue();
        var clave = new ClaveDeOperacion();
        var id = Guid.NewGuid();

        (await _cliente.InactivarTipoAsync(id, Motivo("Duplicado"), clave)).IsSuccess.Should().BeFalse();
        (await _cliente.InactivarTipoAsync(id, Motivo("Duplicado"), clave)).IsSuccess.Should().BeTrue();
        (await _cliente.InactivarTipoAsync(id, Motivo("Duplicado"), clave)).IsSuccess.Should().BeTrue();

        var claves = _servidor.Vistas.Select(v => v.Clave).ToList();
        claves[1].Should().Be(claves[0], "el reintento del mismo intento conserva la clave: el servidor ejecuta una vez");
        claves[2].Should().NotBe(claves[1], "tras un éxito la siguiente es otra operación");
    }

    [Fact]
    public async Task Las_consultas_no_llevan_clave()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, new object[] { TipoJson() });

        var r = await _cliente.ListarTiposAsync(grupo: 2, incluirInactivos: true);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value.Should().ContainSingle().Which.Code.Should().Be("AJP");
        var vista = _servidor.Vistas.Single();
        vista.Clave.Should().BeNull();
        vista.Ruta.Should().Be("/api/inventory/document-types?group=2&includeInactive=true");
    }

    [Fact]
    public async Task Un_422_conserva_su_data_para_que_la_pantalla_la_muestre()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Inventory.Stock.Insufficient",
            message = "No alcanza la existencia.",
            data = new { lineNumber = 3, productCode = "ARZ-001", requested = 10, available = 4 },
        });

        var r = await _cliente.ConfirmarAsync(InventarioClient.RutasDeGrupo.Ajustes, Guid.NewGuid(), [1, 2, 3], new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Inventory.Stock.Insufficient");
        r.StatusCode.Should().Be(422);
        r.Entero("available").Should().Be(4);
        r.Entero("lineNumber").Should().Be(3);
        _servidor.Vistas.Single().Ruta.Should().EndWith("/confirm").And.StartWith("/api/inventory/adjustments/");
    }

    [Fact]
    public async Task Revisar_una_plantilla_manda_el_archivo_con_mode_review_y_su_propia_clave()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, ImportacionJson(valid: true, mode: "Review"));
        var clave = new ClaveDeOperacion();

        var r = await _cliente.RevisarPlantillaAsync("/api/inventory/document-types", "tipos.xlsx", [1, 2, 3], null, clave);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Valid.Should().BeTrue();
        r.Value.Sheets.Should().ContainSingle().Which.Created.Should().Be(2);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/document-types/import?mode=review");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.TipoDeContenido.Should().StartWith("multipart/form-data");
        vista.Cuerpo.Should().Contain("tipos.xlsx");
    }

    [Fact]
    public async Task Aplicar_con_errores_devuelve_la_revision_que_viaja_en_data()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Import.Invalid",
            message = "El archivo tiene errores.",
            data = ImportacionJson(valid: false, mode: "Apply"),
        });

        var r = await _cliente.AplicarPlantillaAsync("/api/core/taxes", "impuestos.xlsx", [9], "Tarifas 2027", new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Import.Invalid");
        var revision = r.ComoImportacion();
        revision.Should().NotBeNull();
        revision!.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Sheet = "Tarifas", Row = 9, Column = "tarifa", Code = "Import.Cell.Format",
        });
        _servidor.Vistas.Single().Ruta.Should().Be("/api/core/taxes/import?mode=apply");
        _servidor.Vistas.Single().Cuerpo.Should().Contain("Tarifas 2027", "el motivo viaja en el formulario (reason)");
    }

    [Fact]
    public async Task Sin_sesion_no_sale_nada()
    {
        var almacen = new AlmacenEnMemoria();
        var sesion = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), new RelojFijo(Ahora));
        var http = new HttpClient(_servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var cliente = new InventarioClient(http, new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso()));

        var r = await cliente.ClasesAsync();

        r.IsSuccess.Should().BeFalse();
        r.StatusCode.Should().Be(401);
        _servidor.Vistas.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------- catálogo y bodegas (T232) --

    [Fact]
    public async Task La_lectura_exacta_de_un_empaque_trae_su_unidad_y_la_busqueda_no_lleva_clave()
    {
        var producto = Guid.NewGuid();
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.OK, new
        {
            exact = new
            {
                publicId = producto, code = "P1", name = "Aceite", baseUnitCode = "UND", status = 1, matchedBarcode = "7702001000012",
                packUnit = new { productUnitPublicId = Guid.NewGuid(), unitCode = "CAJA12", factor = 12m },
            },
            items = Array.Empty<object>(),
        });

        var r = await _cliente.BuscarProductosAsync("7702001000012", clases: [1]);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Exact!.PublicId.Should().Be(producto);
        r.Value.Exact.PackUnit!.UnitCode.Should().Be("CAJA12");
        r.Value.Exact.PackUnit.Factor.Should().Be(12m);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/products/search?q=7702001000012&kinds=1");
        vista.Clave.Should().BeNull("la búsqueda es una consulta");
    }

    [Fact]
    public async Task Borrar_un_producto_con_historia_conserva_las_alternativas()
    {
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Inventory.Product.HasHistory", message = "El producto tiene historia.", data = new { alternatives = new[] { "Inactive", "Blocked" } },
        });

        var r = await _cliente.BorrarProductoAsync(Guid.NewGuid(), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Inventory.Product.HasHistory");
        r.Dato<string[]>("alternatives").Should().Equal("Inactive", "Blocked");
        _servidor.Vistas.Single().Clave.Should().NotBeNullOrWhiteSpace("borrar es una escritura");
    }

    [Fact]
    public async Task Crear_una_bodega_lleva_su_clave_y_devuelve_el_transito_que_nacio()
    {
        var transito = Guid.NewGuid();
        _servidor.Responder = _ => Servidor.Json(HttpStatusCode.Created, new
        {
            warehouse = new { publicId = Guid.NewGuid(), code = "PRIN", name = "Principal", activationStatus = 0, isActive = true },
            transitWarehouseCreated = new { publicId = transito, code = "TR01", name = "Tránsito Florida" },
            warnings = new[] { new { code = "Inventory.Branch.MunicipalityMissing", message = "Sin municipio." } },
        });

        var r = await _cliente.CrearBodegaAsync(new CrearBodegaRequest("PRIN", "Principal", Guid.NewGuid(), Guid.NewGuid(), null, null), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.TransitWarehouseCreated!.Code.Should().Be("TR01");
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Inventory.Branch.MunicipalityMissing");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/inventory/warehouses");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Inactivar_una_fila_de_catalogo_manda_el_motivo_a_su_ruta()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        var r = await _cliente.CambiarActivoAsync(InventarioClient.RutaDeMarcas, id, activar: false, "Ya no se vende", new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be($"/api/inventory/brands/{id}/deactivate");
        vista.Cuerpo.Should().Contain("Ya no se vende");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static object TipoJson() => new
    {
        publicId = Guid.NewGuid(), code = "AJP", name = "Ajuste positivo", @class = 10, group = 2, isFiscal = false, numberedBy = 1,
        prefix = "", requiredFields = new { counterparty = false, costCenter = false, reason = true, externalReference = false },
        warehouses = Array.Empty<object>(), isTaxableWithdrawal = false, vatNonDeductible = false, allowsFutureDate = false,
        isSeeded = true, isActive = true,
    };

    private static object ImportacionJson(bool valid, string mode) => new
    {
        template = "inventory.document-types",
        mode,
        valid,
        applied = false,
        fileName = "x.xlsx",
        fileSha256 = "abc",
        requiresReason = false,
        sheets = new[] { new { sheet = "Tarifas", rows = 3, created = 2, updated = 1, unchanged = 0 } },
        changes = Array.Empty<object>(),
        changesTruncated = false,
        warnings = Array.Empty<object>(),
        errors = valid ? Array.Empty<object>() : [new { row = 9, column = "tarifa", code = "Import.Cell.Format", message = "No es un porcentaje.", sheet = "Tarifas" }],
        totalErrors = valid ? 0 : 1,
    };

    /// <summary>El servidor, con las cabeceras que el cliente pone (la clave y la autorización) y el cuerpo.</summary>
    private sealed class Servidor : HttpMessageHandler
    {
        public List<Vista> Vistas { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Vistas.Add(new Vista(
                request.RequestUri!.PathAndQuery,
                request.Headers.TryGetValues(ClaveDeOperacion.Cabecera, out var claves) ? claves.Single() : null,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                cuerpo));
            return Responder(request);
        }

        public static HttpResponseMessage Json(HttpStatusCode status, object cuerpo) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(cuerpo), Encoding.UTF8, "application/json"),
        };
    }

    private sealed record Vista(string Ruta, string? Clave, string? Authorization, string? TipoDeContenido, string? Cuerpo);
}
