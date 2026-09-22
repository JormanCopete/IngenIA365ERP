using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Feature 009 E2, US5 (T108): las consultas e informes contables recorridos por HTTP contra
/// PostgreSQL, Mongo y Redis reales, con los tokens de un administrador de cooperativa, un usuario
/// de sólo lectura y uno asignado a una sola sucursal. Cada prueba digita sus propios comprobantes
/// y afirma diferencias (antes/después) para no depender de lo que otra dejó en el libro.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class InformesTests(CentralIdentityApiFixture fx)
{
    private static readonly (string Formato, string TipoContenido, byte[] Firma)[] Formatos =
    [
        ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B]),
        ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B]),
        ("pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]),
    ];

    /// <summary>Saldo inicial, débitos, créditos y saldo final de una cuenta en el balance de prueba (cero si no aparece).</summary>
    private static async Task<(decimal Inicial, decimal Debitos, decimal Creditos, decimal Final)> SaldoAsync(HttpClient http, string token, string cuenta)
    {
        var balance = await InformeAsync(http, token, "trial-balance");
        var fila = balance.PorCodigo(cuenta);
        if (fila is null) return (0m, 0m, 0m, 0m);
        return (balance.Numero(fila.Value, "Saldo inicial"), balance.Numero(fila.Value, "Débitos"), balance.Numero(fila.Value, "Créditos"), balance.Numero(fila.Value, "Saldo final"));
    }

    private static void DebeCuadrar(Tabla balance)
    {
        balance.Totales.Should().NotBeNull("el balance de prueba trae la fila Totales");
        var totales = balance.Totales!.Value;
        Tabla.Texto(totales, 0).Should().Be("Totales");
        var debitos = balance.Numero(totales, "Débitos");
        var creditos = balance.Numero(totales, "Créditos");
        debitos.Should().BeGreaterThan(0m, "hay movimiento contabilizado");
        debitos.Should().Be(creditos, "Σ débitos = Σ créditos: cada comprobante entró cuadrado");
    }

    [Fact]
    public async Task El_balance_de_prueba_cuadra_tras_contabilizar_y_tras_reversar_y_la_cuenta_vuelve_a_su_saldo()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        const decimal importe = 1_500_000m;

        var gastoAntes = await SaldoAsync(http, admin, GastoSeguros);
        var pasivoAntes = await SaldoAsync(http, admin, HonorariosPorPagar);

        // (a) borrador CG con dos líneas, una con tercero; contabilizar
        var tercero = await CrearPersonaAsync(http, admin, "Asegurador");
        var comprobante = await ContabilizarAsync(http, admin, new DateOnly(2026, 9, 10), "Póliza de seguros del semestre", new object[]
        {
            Linea(GastoSeguros, ctx.SucursalPrincipal, importe, 0m, detalle: "Prima"),
            Linea(HonorariosPorPagar, ctx.SucursalPrincipal, 0m, importe, tercero, "FC", SiguienteCruce(), "Factura del asegurador"),
        });

        var balance = await InformeAsync(http, admin, "trial-balance");
        DebeCuadrar(balance);
        var gasto = await SaldoAsync(http, admin, GastoSeguros);
        var pasivo = await SaldoAsync(http, admin, HonorariosPorPagar);
        gasto.Debitos.Should().Be(gastoAntes.Debitos + importe, "el gasto recibió el débito del comprobante");
        gasto.Final.Should().Be(gastoAntes.Final + importe, "el saldo de la cuenta es la suma de sus líneas (naturaleza débito)");
        pasivo.Creditos.Should().Be(pasivoAntes.Creditos + importe);
        pasivo.Final.Should().Be(pasivoAntes.Final + importe, "naturaleza crédito: el crédito aumenta el saldo");

        // el comprobante quedó contabilizado con número y en la relación de comprobantes
        var detalle = await GetAsync(http, admin, $"/api/accounting/documents/{comprobante}");
        detalle.GetProperty("status").GetString().Should().Be("Posted");
        detalle.GetProperty("number").GetInt64().Should().BeGreaterThan(0);

        // reversar: el original y su espejo cuentan los dos, se netean y el balance sigue cuadrando (decisión 7)
        var espejo = await ReversarAsync(http, admin, comprobante, "Prueba e2e: la póliza se registró dos veces");
        espejo.Should().NotBe(comprobante);

        var balanceTrasReversa = await InformeAsync(http, admin, "trial-balance");
        DebeCuadrar(balanceTrasReversa);
        var gastoTrasReversa = await SaldoAsync(http, admin, GastoSeguros);
        var pasivoTrasReversa = await SaldoAsync(http, admin, HonorariosPorPagar);
        gastoTrasReversa.Debitos.Should().Be(gastoAntes.Debitos + importe, "el original sigue en el libro");
        gastoTrasReversa.Creditos.Should().Be(gastoAntes.Creditos + importe, "el espejo también");
        gastoTrasReversa.Final.Should().Be(gastoAntes.Final, "la cuenta vuelve al saldo anterior");
        pasivoTrasReversa.Final.Should().Be(pasivoAntes.Final);

        // y reversar la reversa no se puede
        var otraVez = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{espejo}/reverse", new { reason = "no debería" });
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(otraVez)).Should().Be("Accounting.Document.IsReversal");

        // cerrar el período y reabrirlo no mueve un peso: el balance sigue igual y cuadrado
        var cerrar = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/2026/9/close", null);
        cerrar.IsSuccessStatusCode.Should().BeTrue($"cerrar septiembre: «{await cerrar.Content.ReadAsStringAsync()}»");
        var cerrado = await InformeAsync(http, admin, "trial-balance");
        DebeCuadrar(cerrado);
        (await SaldoAsync(http, admin, GastoSeguros)).Should().Be(gastoTrasReversa);

        var enCerrado = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "CG", date = new DateOnly(2026, 9, 20), description = "en período cerrado",
            lines = new[] { Linea(GastoSeguros, ctx.SucursalPrincipal, 1m, 0m), Linea(HonorariosPorPagar, ctx.SucursalPrincipal, 0m, 1m) },
        });
        enCerrado.StatusCode.Should().Be(HttpStatusCode.Created, "el borrador se guarda con errores");
        var borradorCerrado = (await LeerAsync(enCerrado)).GetProperty("publicId").GetGuid();
        var contabilizarCerrado = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{borradorCerrado}/post", null);
        contabilizarCerrado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "pero no se contabiliza en un período cerrado");
        (await CodigoDeErrorAsync(contabilizarCerrado)).Should().Be("Accounting.Period.Closed");
        (await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/documents/drafts/{borradorCerrado}", null)).IsSuccessStatusCode.Should().BeTrue();

        var reabrir = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/2026/9/reopen", new { reason = "Prueba e2e: faltaban comprobantes" });
        reabrir.IsSuccessStatusCode.Should().BeTrue($"reabrir septiembre: «{await reabrir.Content.ReadAsStringAsync()}»");
        var reabierto = await InformeAsync(http, admin, "trial-balance");
        DebeCuadrar(reabierto);
        (await SaldoAsync(http, admin, GastoSeguros)).Should().Be(gastoTrasReversa);
    }

    [Fact]
    public async Task El_libro_auxiliar_baja_desde_las_clases_hasta_las_lineas_del_comprobante_siguiendo_el_nodo_oculto()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        const decimal importe = 725_000m;

        var tercero = await CrearPersonaAsync(http, admin, "Contador");
        var documento = await DocumentoDeAsync(http, admin, tercero);
        var numeroDeFactura = SiguienteCruce();
        var comprobante = await ContabilizarAsync(http, admin, new DateOnly(2026, 9, 12), "Honorarios de revisoría", new object[]
        {
            Linea(GastoSeguros, ctx.SucursalPrincipal, importe, 0m, tercero),
            Linea(HonorariosPorPagar, ctx.SucursalPrincipal, 0m, importe, tercero, "FC", numeroDeFactura),
        });

        // raíz → 2 → 24 → 2405 → 240505 → 24050501: en cada escalón la fila de mi cuenta y su _nodo
        var nivel = await InformeAsync(http, admin, "ledger");
        var camino = new List<string>();
        foreach (var codigo in new[] { "2", "24", "2405", "240505", HonorariosPorPagar })
        {
            var fila = nivel.PorCodigo(codigo);
            fila.Should().NotBeNull($"el nivel «{nivel.Titulo}» trae la cuenta {codigo} (trae: {string.Join(", ", nivel.Filas.Select(f => Tabla.Texto(f, 0)))})");
            nivel.Oculta(fila!.Value, "_tipo").Should().Be("account");
            var nodo = nivel.Oculta(fila.Value, "_nodo");
            nodo.Should().Be($"account:{codigo}");
            camino.Add(nodo);
            nivel = await InformeAsync(http, admin, $"ledger?node={Uri.EscapeDataString(nodo)}");
        }

        camino.Should().Equal("account:2", "account:24", "account:2405", "account:240505", $"account:{HonorariosPorPagar}");
        nivel.Titulo.Should().Contain(HonorariosPorPagar, "el título lleva el camino recorrido");

        // la auxiliar no tiene hijas: se abre por terceros
        var filaTercero = nivel.PorCodigo(documento);
        filaTercero.Should().NotBeNull($"el tercero {documento} tiene movimiento en {HonorariosPorPagar}");
        nivel.Oculta(filaTercero!.Value, "_tipo").Should().Be("person");
        var nodoTercero = nivel.Oculta(filaTercero.Value, "_nodo");
        nodoTercero.Should().Be($"person:{HonorariosPorPagar}|{tercero}");
        nivel.Numero(filaTercero.Value, "Créditos").Should().BeGreaterThanOrEqualTo(importe);

        // tercero → documentos cruce
        nivel = await InformeAsync(http, admin, $"ledger?node={Uri.EscapeDataString(nodoTercero)}");
        var filaDocumento = nivel.PorCodigo($"FC {numeroDeFactura}");
        filaDocumento.Should().NotBeNull($"el documento FC {numeroDeFactura} aparece bajo el tercero (hay: {string.Join(", ", nivel.Filas.Select(f => Tabla.Texto(f, 0)))})");
        nivel.Oculta(filaDocumento!.Value, "_tipo").Should().Be("document");
        var nodoDocumento = nivel.Oculta(filaDocumento.Value, "_nodo");
        nodoDocumento.Should().Be($"document:{HonorariosPorPagar}|{tercero}|FC|{numeroDeFactura}");

        // documento → comprobantes con saldo corrido
        nivel = await InformeAsync(http, admin, $"ledger?node={Uri.EscapeDataString(nodoDocumento)}");
        nivel.Filas.Should().ContainSingle("un solo comprobante tocó esa combinación");
        var filaComprobante = nivel.Filas[0];
        nivel.Oculta(filaComprobante, "_tipo").Should().Be("voucher");
        nivel.Oculta(filaComprobante, "_comprobante").Should().Be(comprobante.ToString());
        nivel.Numero(filaComprobante, "Créditos").Should().Be(importe);
        nivel.Numero(filaComprobante, "Saldo final").Should().Be(nivel.Numero(filaComprobante, "Saldo inicial") + importe);
        var nodoComprobante = nivel.Oculta(filaComprobante, "_nodo");
        nodoComprobante.Should().Be($"voucher:{comprobante}");

        // comprobante → sus líneas, todas con _comprobante
        nivel = await InformeAsync(http, admin, $"ledger?node={Uri.EscapeDataString(nodoComprobante)}");
        nivel.Filas.Should().HaveCount(2);
        nivel.Filas.Select(f => nivel.Oculta(f, "_comprobante")).Should().AllBe(comprobante.ToString(), "la última respuesta trae _comprobante en cada línea");
        nivel.Filas.Select(f => nivel.Oculta(f, "_tipo")).Should().AllBe("line");
        nivel.Filas.Select(f => Tabla.Texto(f, 0)).Should().BeEquivalentTo([GastoSeguros, HonorariosPorPagar]);
        nivel.Filas.Sum(f => nivel.Numero(f, "Débitos")).Should().Be(nivel.Filas.Sum(f => nivel.Numero(f, "Créditos")));

        // un nodo mal formado es 422 con su código
        var malo = await EnviarAsync(http, admin, HttpMethod.Get, "/api/reports/accounting/ledger?node=cuenta-1105", null);
        malo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(malo)).Should().Be("Accounting.Report.InvalidNode");
    }

    [Fact]
    public async Task Los_tres_formatos_responden_con_archivo_real_json_trae_las_mismas_filas_y_la_exportacion_queda_en_la_auditoria()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var json = await InformeAsync(http, admin, "trial-balance");
        json.Filas.Should().NotBeEmpty();
        json.Raiz.GetProperty("columnas")[0].GetProperty("tipo").ValueKind.Should().Be(JsonValueKind.String, "el tipo de columna viaja por nombre");
        json.Notas.Should().Contain(n => n.Contains("Totales"), "el encabezado obligatorio va en las notas");

        var desde = DateTime.UtcNow.AddMinutes(-1);
        foreach (var (formato, tipo, firma) in Formatos)
        {
            var resp = await EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/accounting/trial-balance?format={formato}", null);
            resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{formato}: «{await resp.Content.ReadAsStringAsync()}»");
            resp.Content.Headers.ContentType!.MediaType.Should().Be(tipo);
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            bytes.Length.Should().BeGreaterThan(500, $"{formato} tiene contenido");
            bytes.Take(firma.Length).Should().Equal(firma, $"{formato} es un archivo del tipo declarado");
        }

        // (d) cada exportación deja Accounting.Report.Exported con la vista, el formato y las filas: las mismas que devolvió json
        var auditoria = await GetAsync(http, admin, "/api/audit/logs?module=Accounting&action=Accounting.Report.Exported&pageSize=50");
        var eventos = auditoria.GetProperty("items").EnumerateArray()
            .Where(e => e.GetProperty("timestamp").GetDateTime() >= desde.AddMinutes(-5))
            .Where(e => (e.GetProperty("newValuesJson").GetString() ?? string.Empty).Contains("trial-balance"))
            .ToList();
        eventos.Should().HaveCountGreaterThanOrEqualTo(Formatos.Length, "una exportación por formato quedó en la auditoría de la cooperativa");
        foreach (var (formato, _, _) in Formatos)
        {
            var evento = eventos.FirstOrDefault(e => e.GetProperty("newValuesJson").GetString()!.Contains($"\"formato\":\"{formato}\""));
            evento.ValueKind.Should().Be(JsonValueKind.Object, $"la exportación a {formato} quedó auditada");
            var valores = JsonDocument.Parse(evento.GetProperty("newValuesJson").GetString()!).RootElement;
            valores.GetProperty("informe").GetString().Should().Be("trial-balance");
            valores.GetProperty("filas").GetInt32().Should().Be(json.Filas.Count, "el archivo llevó las mismas filas que la pantalla");
            evento.GetProperty("module").GetString().Should().Be("Accounting");
        }

        // un formato desconocido es 400 con sobre, no un archivo vacío: lo frena el validador de la
        // consulta (Validation.*) y, si llegara a la entrega, Reportes.FormatoInvalido; los dos son 400.
        var csv = await EnviarAsync(http, admin, HttpMethod.Get, "/api/reports/accounting/trial-balance?format=csv", null);
        csv.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodigoDeErrorAsync(csv)).Should().Match(c => c.StartsWith("Validation.") || c == "Reportes.FormatoInvalido");
    }

    [Fact]
    public async Task Quien_solo_lee_ve_la_pantalla_pero_no_descarga_el_archivo()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();

        var pantalla = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, "/api/reports/accounting/trial-balance?format=json", null);
        pantalla.StatusCode.Should().Be(HttpStatusCode.OK, $"Reports.View alcanza para json: «{await pantalla.Content.ReadAsStringAsync()}»");

        var archivo = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, "/api/reports/accounting/trial-balance?format=xlsx", null);
        archivo.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin Reports.Export la exportación es indistinguible de una ruta inexistente (FR-017)");

        // y un ReadOnly no digita
        var borrador = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "CG", date = new DateOnly(2026, 9, 1), description = "no debería", lines = new[] { Linea(GastoSeguros, ctx.SucursalPrincipal, 1m, 0m) },
        });
        borrador.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Las trece vistas de <c>/api/reports/accounting</c> (más el auxiliar abierto en la cuenta que sólo
    /// la Principal movió) con el usuario asignado a la sucursal Norte: en ninguna aparece el importe del
    /// comprobante de la Principal, ni solo ni sumado con el de Norte.
    /// </summary>
    [Theory]
    [InlineData("trial-balance")]
    [InlineData("ledger")]
    [InlineData("ledger?node=account:" + CajaPrincipal)]
    [InlineData("journal")]
    [InlineData("general-ledger")]
    [InlineData("voucher-list")]
    [InlineData("third-party-statement?person={terceroPrincipal}")]
    [InlineData("pending-documents")]
    [InlineData("daily-average?accountPublicId={cuentaCajaPrincipal}")]
    [InlineData("financial-position")]
    [InlineData("income-statement")]
    [InlineData("equity-changes")]
    [InlineData("cash-flow")]
    [InlineData("budget-execution?year=2026&month=9")]
    public async Task Quien_tiene_una_sola_sucursal_no_ve_movimientos_de_otra_en_ninguna_vista(string vista)
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var ruta = vista.Replace("{terceroPrincipal}", ctx.TerceroPrincipal.ToString()).Replace("{cuentaCajaPrincipal}", ctx.CuentaCajaPrincipal.ToString());

        var resp = await EnviarAsync(http, ctx.TokenNorte, HttpMethod.Get, $"/api/reports/accounting/{ruta}", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{ruta}: «{await resp.Content.ReadAsStringAsync()}»");
        var cuerpo = await resp.Content.ReadAsStringAsync();

        var ajeno = ImportePrincipal.ToString("0");
        var sumado = (ImportePrincipal + ImporteNorte).ToString("0");
        cuerpo.Should().NotContain(ajeno, $"{ruta}: el importe del comprobante de la Principal no puede aparecer para un usuario de Norte");
        cuerpo.Should().NotContain(sumado, $"{ruta}: tampoco sumado con el de Norte");
        cuerpo.Should().NotContain(ctx.ComprobantePrincipal.ToString(), $"{ruta}: ni el comprobante de la Principal como enlace");
    }

    [Fact]
    public async Task El_alcance_de_sucursal_deja_ver_lo_propio_y_el_administrador_lo_ve_todo()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();

        // El usuario de Norte sí ve su propio comprobante: la prueba anterior no pasa por casualidad con una tabla vacía.
        var propio = await InformeAsync(http, ctx.TokenNorte, "trial-balance");
        var caja = propio.PorCodigo(CajaNorte);
        caja.Should().NotBeNull("la caja de Norte tiene movimiento visible para Norte");
        propio.Numero(caja!.Value, "Débitos").Should().Be(ImporteNorte);
        propio.PorCodigo(CajaPrincipal).Should().BeNull("la caja menor sólo la movió la Principal");
        propio.Totales.Should().NotBeNull();
        propio.Numero(propio.Totales!.Value, "Débitos").Should().Be(propio.Numero(propio.Totales.Value, "Créditos"), "lo que ve también cuadra");

        var diario = await EnviarAsync(http, ctx.TokenNorte, HttpMethod.Get, "/api/reports/accounting/journal", null);
        (await diario.Content.ReadAsStringAsync()).Should().Contain(ImporteNorte.ToString("0"));

        // El administrador, sin sucursales asignadas, ve las dos.
        var todo = await InformeAsync(http, ctx.TokenAdmin, "trial-balance");
        todo.Numero(todo.PorCodigo(CajaNorte)!.Value, "Débitos").Should().Be(ImporteNorte);
        todo.Numero(todo.PorCodigo(CajaPrincipal)!.Value, "Débitos").Should().Be(ImportePrincipal);

        // Y el filtro branch= restringe al administrador igual que el alcance restringe a Norte.
        var soloNorte = await InformeAsync(http, ctx.TokenAdmin, $"trial-balance?branch={ctx.SucursalNorte}");
        soloNorte.PorCodigo(CajaPrincipal).Should().BeNull();
        soloNorte.Numero(soloNorte.PorCodigo(CajaNorte)!.Value, "Débitos").Should().Be(ImporteNorte);
    }

    [Fact]
    public async Task El_estado_de_cuenta_del_tercero_trae_su_movimiento_y_los_documentos_pendientes_traen_el_cruce()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        const decimal importe = 480_000m;

        var tercero = await CrearPersonaAsync(http, admin, "Proveedor");
        var numero = SiguienteCruce();
        var comprobante = await ContabilizarAsync(http, admin, new DateOnly(2026, 9, 14), "Factura de honorarios pendiente de pago", new object[]
        {
            Linea(GastoSeguros, ctx.SucursalPrincipal, importe, 0m, tercero),
            Linea(HonorariosPorPagar, ctx.SucursalPrincipal, 0m, importe, tercero, "FC", numero),
        });

        // (g) estado de cuenta: las dos líneas del comprobante con el tercero, el saldo por naturaleza de cada cuenta
        var estado = await InformeAsync(http, admin, $"third-party-statement?person={tercero}");
        estado.Titulo.Should().Contain("Estado de cuenta");
        var lineas = estado.Filas.Where(f => estado.Oculta(f, "_comprobante") == comprobante.ToString()).ToList();
        lineas.Should().HaveCount(2, "las dos líneas llevan el tercero");
        lineas.Sum(f => estado.Numero(f, "Débito")).Should().Be(importe);
        lineas.Sum(f => estado.Numero(f, "Crédito")).Should().Be(importe);
        lineas.Should().Contain(f => estado.Texto(f, "Documento cruce").Contains(numero), "la línea del pasivo lleva FC y número");

        // sin tercero la consulta no tiene sentido: 422 con su código
        var sinTercero = await EnviarAsync(http, admin, HttpMethod.Get, "/api/reports/accounting/third-party-statement", null);
        sinTercero.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(sinTercero)).Should().Be("Accounting.Report.PersonRequired");

        // un tercero inexistente es 404
        var fantasma = await EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/accounting/third-party-statement?person={Guid.NewGuid()}", null);
        fantasma.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // documentos pendientes: FC número con saldo por pagar, y filtrado por el tercero sólo el suyo
        var pendientes = await InformeAsync(http, admin, $"pending-documents?person={tercero}");
        pendientes.Filas.Should().ContainSingle("el tercero tiene un solo documento cruce abierto");
        var pendiente = pendientes.Filas[0];
        Tabla.Texto(pendiente, 0).Should().Be(HonorariosPorPagar);
        pendientes.Texto(pendiente, "Documento").Should().Be("FC");
        pendientes.Texto(pendiente, "Número").Should().Be(numero);
        pendientes.Numero(pendiente, "Saldo").Should().Be(importe, "pasivo: crédito − débito");
        pendientes.Oculta(pendiente, "_nodo").Should().Be($"document:{HonorariosPorPagar}|{tercero}|FC|{numero}");

        // y en la vista sin filtro también está, con el filtro crossDocument=TIPO|NÚMERO
        var porCruce = await InformeAsync(http, admin, $"pending-documents?crossDocument={Uri.EscapeDataString($"FC|{numero}")}");
        porCruce.Filas.Should().ContainSingle().Which.Should().Match<JsonElement>(f => porCruce.Texto(f, "Número") == numero);
    }
}
