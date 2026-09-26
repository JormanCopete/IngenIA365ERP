using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Las e2e que E1 dejó pendientes (T056, T066, T077, T091; feature 009, US1, US2, US3 y US7), por
/// HTTP sobre la cooperativa compartida de la colección: iniciar la contabilidad es una sola vez y
/// se bloquea con auxiliares; importar un catálogo con una fila mala no guarda nada y 2 000 filas
/// entran en menos de un minuto; las reglas de una cuenta con movimientos no cambian y una
/// parametrizada no se elimina; la digitación valida campo a campo, guarda descuadrado y no
/// contabiliza así, numera sin repetir bajo concurrencia, respeta cuatro ojos y reversa a cero;
/// todo queda auditado y sin permiso el servidor responde el mismo 404 que una ruta inexistente.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class ContabilidadNiifTests(CentralIdentityApiFixture fx)
{
    private const string CorreoOperador = "contabilidad.operador@coop.nomina.test";
    private const string ClaveOperador = "Contabilidad-Operador-2026!";
    private const string CuentaSoloNomina = "51050501";      // 510505 bajo SUELDOS: sólo Nómina la mueve
    private const string CuentaConTercero = "16050501";      // 160505 cuentas por cobrar vigentes: exige tercero
    private const string GastoConcurrencia = "51150501";     // 511505 gasto para numerar en paralelo

    // ----------------------------------------------------------------------------- US1 (T056) --

    [Fact]
    public async Task La_contabilidad_se_inicia_una_vez_y_su_estructura_se_bloquea_con_la_primera_auxiliar()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var otraVez = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/setup/initialize", new
        {
            catalogCode = "PUC-SOLIDARIO", movementLevel = 5, niifGroup = 2, firstFiscalYear = 2026, mainBranchPublicId = ctx.SucursalPrincipal, fourEyes = false,
        });
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(otraVez)).Should().Be("Accounting.Setup.AlreadyInitialized");

        // Con auxiliares (las creó el bootstrap) el nivel de movimiento ya no cambia; lo demás sí.
        var setup = await GetAsync(http, admin, "/api/accounting/setup");
        setup.GetProperty("initialized").GetBoolean().Should().BeTrue();
        setup.GetProperty("movementLevel").GetInt32().Should().Be(5);
        var cambioDeNivel = await EnviarAsync(http, admin, HttpMethod.Put, "/api/accounting/setup", new
        {
            movementLevel = 6, mainBranchPublicId = ctx.SucursalPrincipal, fourEyes = false,
            reconciliationDayTolerance = setup.GetProperty("reconciliationDayTolerance").GetInt32(), taxTolerance = setup.GetProperty("taxTolerance").GetDecimal(),
        });
        cambioDeNivel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var bloqueo = await LeerAsync(cambioDeNivel);
        bloqueo.GetProperty("code").GetString().Should().Be("Accounting.Setup.Locked");
        bloqueo.GetProperty("data").GetProperty("companyAccounts").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Importar_un_catalogo_con_una_fila_mala_no_guarda_nada_y_dos_mil_filas_entran_en_menos_de_un_minuto()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        var antes = (await GetAsync(http, admin, "/api/accounting/setup/catalogs")).GetArrayLength();

        // Una fila con naturaleza «X» y otra que cuelga de un padre ausente: 422 con la fila de cada error.
        var malo = await ImportarCatalogoAsync(http, admin, "Catálogo con errores",
            "codigo;nombre;naturaleza;rubro\n1;ACTIVO;D;ESF-A\n11;DISPONIBLE;X;ESF-A-EFE\n1205;INVERSIONES;D;ESF-A-INV\n");
        malo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await malo.Content.ReadAsStringAsync()}»");
        var cuerpo = await LeerAsync(malo);
        cuerpo.GetProperty("code").GetString().Should().Be("Accounting.Catalog.Invalid");
        var filas = cuerpo.GetProperty("data").GetProperty("errors").EnumerateArray().Select(e => (e.GetProperty("row").GetInt32(), e.GetProperty("column").GetString())).ToList();
        filas.Should().Contain((3, "naturaleza")).And.Contain((4, "codigo"));
        (await GetAsync(http, admin, "/api/accounting/setup/catalogs")).GetArrayLength().Should().Be(antes, "no se guardó nada");

        // SC-001: un catálogo generado de más de 2 000 filas (9 clases, 90 grupos, 450 cuentas, 1 800 subcuentas) en menos de un minuto.
        var sb = new StringBuilder("codigo;nombre;naturaleza;rubro\n");
        var total = 0;
        for (var clase = 1; clase <= 9; clase++)
        {
            sb.Append($"{clase};CLASE {clase};D;ESF-A\n"); total++;
            for (var grupo = 1; grupo <= 10; grupo++)
            {
                var g = $"{clase}{grupo - 1}";
                sb.Append($"{g};GRUPO {g};D;ESF-A\n"); total++;
                for (var cuenta = 1; cuenta <= 5; cuenta++)
                {
                    var c = $"{g}{cuenta:00}";
                    sb.Append($"{c};CUENTA {c};D;ESF-A-EFE\n"); total++;
                    for (var sub = 1; sub <= 4; sub++)
                    {
                        var s = $"{c}{sub:00}";
                        sb.Append($"{s};SUBCUENTA {s};D;ESF-A-EFE\n"); total++;
                    }
                }
            }
        }
        total.Should().BeGreaterThan(2000);
        var reloj = Stopwatch.StartNew();
        var grande = await ImportarCatalogoAsync(http, admin, "Catálogo generado e2e", sb.ToString());
        reloj.Stop();
        grande.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await grande.Content.ReadAsStringAsync()}»");
        (await LeerAsync(grande)).GetProperty("entryCount").GetInt32().Should().Be(total);
        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromMinutes(1), "SC-001");
    }

    [Fact]
    public async Task Las_auxiliares_se_cargan_en_masa_desde_la_plantilla_y_una_fila_mala_no_guarda_nada()
    {
        var coop = await CooperativaAisladaAsync(fx, "cuentas", 2026);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        string[] encabezados = ["cuenta", "nombre", "aplicaA", "exigeTercero", "exigeDocumento", "exigeCentro", "exigeSucursal", "banco", "numeroCuenta", "claseImpuesto", "conceptoTributario", "exigeBase", "tarifa", "tarifaDesde"];
        string Csv(params string[][] filas) => string.Join('\n', new[] { string.Join(';', encabezados) }.Concat(filas.Select(f => string.Join(';', f)))) + "\n";
        string[] Fila(string cuenta, string nombre, string modulos = "CNT", string tercero = "", string cruce = "", string impuesto = "", string concepto = "", string baseGravable = "", string tarifa = "", string desde = "") =>
            [cuenta, nombre, modulos, tercero, cruce, "", "", "", "", impuesto, concepto, baseGravable, tarifa, desde];

        // (a) la plantilla: catorce encabezados en la fila 1
        var plantilla = await EnviarAsync(http, admin, HttpMethod.Get, "/api/accounting/accounts/template.xlsx", null);
        plantilla.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await plantilla.Content.ReadAsStringAsync()}»");
        using (var libro = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await plantilla.Content.ReadAsByteArrayAsync())))
        {
            var hoja = libro.Worksheets.First();
            Enumerable.Range(1, encabezados.Length).Select(i => hoja.Cell(1, i).GetString()).Should().Equal(encabezados);
        }

        // (b) con una fila mala no se guarda nada
        var malo = await ImportarCuentasAsync(http, admin, Csv(
            Fila("11050501", "Caja general"),
            Fila("99999999", "Sin padre en el plan")));
        malo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await malo.Content.ReadAsStringAsync()}»");
        var cuerpo = await LeerAsync(malo);
        cuerpo.GetProperty("code").GetString().Should().Be("Accounting.Accounts.Invalid");
        cuerpo.GetProperty("data").GetProperty("errors").EnumerateArray().Should().ContainSingle(e => e.GetProperty("row").GetInt32() == 3);
        (await GetAsync(http, admin, "/api/accounting/accounts/search?q=11050501&onlyMovement=false")).GetArrayLength().Should().Be(0, "nada a medias");

        // (c) el archivo bueno: auxiliares con sus reglas, una cuenta propia y su auxiliar, y una de impuesto
        var bueno = await ImportarCuentasAsync(http, admin, Csv(
            Fila("11050501", "Caja general"),
            Fila("16050501", "Créditos de consumo", "CNT CAR", tercero: "sí", cruce: "sí"),
            Fila("350505", "Excedentes del ejercicio", ""),
            Fila("35050501", "Excedente del ejercicio", "CNT"),
            Fila("24358501", "Retención por honorarios", "CNT", impuesto: "Withholding", concepto: "2365", baseGravable: "sí", tarifa: "11", desde: "2026-01-01")));
        bueno.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await bueno.Content.ReadAsStringAsync()}»");
        var resumen = await LeerAsync(bueno);
        resumen.GetProperty("created").GetInt32().Should().Be(5);

        var cartera = (await GetAsync(http, admin, "/api/accounting/accounts/search?q=16050501&onlyMovement=false")).EnumerateArray().Single();
        cartera.GetProperty("requiresThirdParty").GetBoolean().Should().BeTrue();
        cartera.GetProperty("requiresCrossDocument").GetBoolean().Should().BeTrue();
        var retencionId = (await GetAsync(http, admin, "/api/accounting/accounts/search?q=24358501&onlyMovement=false")).EnumerateArray().Single().GetProperty("publicId").GetGuid();
        var retencion = await GetAsync(http, admin, $"/api/accounting/accounts/{retencionId}");
        retencion.GetProperty("tax").GetProperty("kind").GetString().Should().Be("Withholding");
        retencion.GetProperty("tax").GetProperty("rates").EnumerateArray().Single().GetProperty("rate").GetDecimal().Should().Be(0.11m);

        // (d) el mismo archivo otra vez no duplica; cambiar el nombre actualiza
        var otraVez = await ImportarCuentasAsync(http, admin, Csv(Fila("11050501", "Caja general")));
        (await LeerAsync(otraVez)).GetProperty("unchanged").GetInt32().Should().Be(1);
        var renombrada = await ImportarCuentasAsync(http, admin, Csv(Fila("11050501", "Caja principal")));
        (await LeerAsync(renombrada)).GetProperty("updated").GetInt32().Should().Be(1);

        // (e) con movimientos, las reglas quedan fijas
        await ContabilizarAsync(http, admin, new DateOnly(2026, 5, 4), "Aporte inicial",
            [Linea("11050501", coop.SucursalPrincipal, 100_000m, 0m), Linea("35050501", coop.SucursalPrincipal, 0m, 100_000m)]);
        var bloqueada = await ImportarCuentasAsync(http, admin, Csv(Fila("11050501", "Caja principal", "CNT NOM")));
        bloqueada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LeerAsync(bloqueada)).GetProperty("data").GetProperty("errors").EnumerateArray()
            .Should().ContainSingle(e => e.GetProperty("code").GetString() == "Accounting.Account.Locked");
    }

    // ----------------------------------------------------------------------------- US2 (T066) --

    [Fact]
    public async Task Una_auxiliar_solo_de_nomina_se_parametriza_alli_no_aparece_para_cartera_y_con_movimientos_no_cambia_de_reglas()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        await CrearCuentaAsync(http, admin, CuentaSoloNomina, "Sueldos administración", "510505", ["NOM"]);
        var cuenta = await CuentaAsync(http, admin, CuentaSoloNomina);

        // Parametrizar en nómina: OK (habilitada para NOM).
        var conceptos = await GetAsync(http, admin, "/api/payroll/concept-definitions");
        var concepto = conceptos.EnumerateArray().First(c => c.GetProperty("code").GetString() == "SALARIO").GetProperty("code").GetString();
        var parametrizar = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/concept-definitions/{concepto}/accounts",
            new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = CuentaSoloNomina, creditAccountCode = NominaE2E.CuentaCredito } } });
        parametrizar.IsSuccessStatusCode.Should().BeTrue($"parametrizar SALARIO: «{await parametrizar.Content.ReadAsStringAsync()}»");
        try
        {
            // El buscador por módulo: Cartera no la ve, Nómina sí.
            var paraCartera = await GetAsync(http, admin, $"/api/accounting/accounts/search?q={CuentaSoloNomina}&module=CAR");
            paraCartera.EnumerateArray().Should().NotContain(c => c.GetProperty("code").GetString() == CuentaSoloNomina);
            var paraNomina = await GetAsync(http, admin, $"/api/accounting/accounts/search?q={CuentaSoloNomina}&module=NOM");
            paraNomina.EnumerateArray().Should().Contain(c => c.GetProperty("code").GetString() == CuentaSoloNomina);

            // Eliminar una cuenta parametrizada: 422 con quién la referencia.
            var eliminar = await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/accounts/{cuenta}", null);
            eliminar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            var referida = await LeerAsync(eliminar);
            referida.GetProperty("code").GetString().Should().Be("Accounting.Account.Referenced");
            referida.GetProperty("data").GetProperty("references").GetArrayLength().Should().BeGreaterThan(0);
        }
        finally
        {
            // La cooperativa compartida vuelve a su parametrización estándar para las demás pruebas.
            (await EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/concept-definitions/{concepto}/accounts",
                new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = NominaE2E.CuentaDebito, creditAccountCode = NominaE2E.CuentaCredito } } })).IsSuccessStatusCode.Should().BeTrue();
        }

        // Tras un comprobante, las reglas de la cuenta quedan fijas: sólo nombre y estado.
        var conMovimientos = await GetAsync(http, admin, $"/api/accounting/accounts/{ctx.CuentaCajaPrincipal}");
        var cambio = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/accounting/accounts/{ctx.CuentaCajaPrincipal}", new
        {
            publicId = ctx.CuentaCajaPrincipal, name = conMovimientos.GetProperty("name").GetString(), enabledModules = new[] { "CNT" },
            requiresThirdParty = true, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false, bank = (object?)null, tax = (object?)null,
        });
        cambio.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(cambio)).Should().Be("Accounting.Account.Locked");
        var soloNombre = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/accounting/accounts/{ctx.CuentaCajaPrincipal}", new
        {
            publicId = ctx.CuentaCajaPrincipal, name = conMovimientos.GetProperty("name").GetString(), enabledModules = new[] { "CNT" },
            requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false, bank = (object?)null, tax = (object?)null,
        });
        soloNombre.IsSuccessStatusCode.Should().BeTrue($"sin cambiar reglas: «{await soloNombre.Content.ReadAsStringAsync()}»");
    }

    // ----------------------------------------------------------------------------- US3 (T077) --

    [Fact]
    public async Task La_digitacion_valida_campo_a_campo_guarda_descuadrado_numera_sin_repetir_y_reversa_a_cero()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        await CrearCuentaAsync(http, admin, CuentaConTercero, "Cuentas por cobrar asociados", "160505", ["CNT"], tercero: true);
        await CrearAuxiliarAsync(http, admin, GastoConcurrencia, "Gasto de concurrencia");
        var fecha = new DateOnly(2026, 9, 8);

        // (a) /validate señala la línea y el campo del tercero que falta, sin guardar nada
        var validacion = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/documents/validate", new
        {
            voucherTypeCode = "CG", date = fecha, description = "Sin tercero",
            lines = new[] { Linea(CuentaConTercero, ctx.SucursalPrincipal, 100m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 100m) },
        });
        validacion.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await validacion.Content.ReadAsStringAsync()}»");
        var v = await LeerAsync(validacion);
        v.GetProperty("errors").EnumerateArray().Should().Contain(e =>
            e.GetProperty("lineNumber").GetInt32() == 1 && e.GetProperty("field").GetString() == "Person" && e.GetProperty("code").GetString() == "Accounting.Line.ThirdPartyRequired");

        // (b) un borrador descuadrado se guarda (201) y no se contabiliza (422 Unbalanced)
        var descuadrado = await BorradorAsync(http, admin, fecha, "Descuadrado", [Linea(GastoConcurrencia, ctx.SucursalPrincipal, 100m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 90m)], admiteErrores: true);
        var postDescuadrado = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{descuadrado}/post", null);
        postDescuadrado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(postDescuadrado)).Should().Be("Accounting.Document.Unbalanced");

        // (c) el tipo NM es de Nómina: no se digita a mano
        var nm = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "NM", date = fecha, description = "A mano", lines = new[] { Linea(GastoConcurrencia, ctx.SucursalPrincipal, 10m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 10m) },
        });
        nm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(nm)).Should().Be("Accounting.Document.ManualOnly");

        // (d) cerrar el mes con el borrador descuadrado pendiente: 422 con la lista (y el mes sigue abierto)
        var cierre = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/2026/9/close", null);
        cierre.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var conBorradores = await LeerAsync(cierre);
        conBorradores.GetProperty("code").GetString().Should().Be("Accounting.Period.HasDrafts");
        conBorradores.GetProperty("data").GetProperty("drafts").EnumerateArray().Should().Contain(d => d.GetProperty("publicId").GetGuid() == descuadrado);
        (await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/documents/drafts/{descuadrado}", null)).IsSuccessStatusCode.Should().BeTrue();

        // (e) dos clientes contabilizan a la vez: números consecutivos distintos
        var b1 = await BorradorAsync(http, admin, fecha, "Paralelo 1", [Linea(GastoConcurrencia, ctx.SucursalPrincipal, 11m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 11m)]);
        var b2 = await BorradorAsync(http, admin, fecha, "Paralelo 2", [Linea(GastoConcurrencia, ctx.SucursalPrincipal, 22m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 22m)]);
        using var http2 = fx.CreateClient();
        var posts = await Task.WhenAll(
            EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{b1}/post", null),
            EnviarAsync(http2, admin, HttpMethod.Post, $"/api/accounting/documents/{b2}/post", null));
        foreach (var p in posts) p.StatusCode.Should().Be(HttpStatusCode.OK, $"contabilizar en paralelo: «{await p.Content.ReadAsStringAsync()}»");
        var numeros = new List<long>();
        foreach (var p in posts) numeros.Add((await LeerAsync(p)).GetProperty("number").GetInt64());
        numeros.Distinct().Should().HaveCount(2, "el consecutivo no se repite bajo concurrencia");
        Math.Abs(numeros[0] - numeros[1]).Should().Be(1, "y no deja huecos");

        // (f) cuatro ojos: quien registró no contabiliza; se apaga al terminar para no afectar a las demás pruebas
        var setup = await GetAsync(http, admin, "/api/accounting/setup");
        var tolerancias = new { reconciliationDayTolerance = setup.GetProperty("reconciliationDayTolerance").GetInt32(), taxTolerance = setup.GetProperty("taxTolerance").GetDecimal() };
        var cuatroOjos = await EnviarAsync(http, admin, HttpMethod.Put, "/api/accounting/setup", new
        {
            mainBranchPublicId = ctx.SucursalPrincipal, fourEyes = true, tolerancias.reconciliationDayTolerance, tolerancias.taxTolerance,
            resultAccountPublicId = setup.GetProperty("resultAccountPublicId").ValueKind == JsonValueKind.String ? setup.GetProperty("resultAccountPublicId").GetGuid() : (Guid?)null,
        });
        cuatroOjos.IsSuccessStatusCode.Should().BeTrue($"activar cuatro ojos: «{await cuatroOjos.Content.ReadAsStringAsync()}»");
        try
        {
            var propio = await BorradorAsync(http, admin, fecha, "Propio", [Linea(GastoConcurrencia, ctx.SucursalPrincipal, 33m, 0m), Linea(CajaPrincipal, ctx.SucursalPrincipal, 0m, 33m)]);
            var mismoUsuario = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{propio}/post", null);
            mismoUsuario.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CodigoDeErrorAsync(mismoUsuario)).Should().Be("Accounting.Document.FourEyes");
            (await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/documents/drafts/{propio}", null)).IsSuccessStatusCode.Should().BeTrue();
        }
        finally
        {
            (await EnviarAsync(http, admin, HttpMethod.Put, "/api/accounting/setup", new
            {
                mainBranchPublicId = ctx.SucursalPrincipal, fourEyes = false, tolerancias.reconciliationDayTolerance, tolerancias.taxTolerance,
                resultAccountPublicId = setup.GetProperty("resultAccountPublicId").ValueKind == JsonValueKind.String ? setup.GetProperty("resultAccountPublicId").GetGuid() : (Guid?)null,
            })).IsSuccessStatusCode.Should().BeTrue();
        }

        // (g) reversar deja el neto en cero; reversar dos veces rechaza; la reversión no se reversa
        var reversion = await ReversarAsync(http, admin, b1, "Se digitó de más");
        var original = await GetAsync(http, admin, $"/api/accounting/documents/{b1}");
        original.GetProperty("status").GetString().Should().Be("Reversed");
        original.GetProperty("reversal").GetProperty("reversedByPublicId").GetGuid().Should().Be(reversion);
        var reverso = await GetAsync(http, admin, $"/api/accounting/documents/{reversion}");
        reverso.GetProperty("kind").GetString().Should().Be("Reversal");
        reverso.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("accountCode").GetString() == GastoConcurrencia).GetProperty("credit").GetDecimal().Should().Be(11m);
        var otraVez = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{b1}/reverse", new { reason = "otra vez" });
        (await CodigoDeErrorAsync(otraVez)).Should().Be("Accounting.Document.AlreadyReversed");
        var laReversion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{reversion}/reverse", new { reason = "la reversión" });
        (await CodigoDeErrorAsync(laReversion)).Should().Be("Accounting.Document.IsReversal");
        var balance = await InformeAsync(http, admin, $"trial-balance?from=2026-09-01&to=2026-09-30&accountFrom={GastoConcurrencia}&accountTo={GastoConcurrencia}");
        var fila = balance.PorCodigo(GastoConcurrencia);
        fila.Should().NotBeNull();
        (balance.Numero(fila!.Value, "Débitos") - balance.Numero(fila.Value, "Créditos")).Should().Be(22m, "de los 11 + 22 contabilizados, los 11 quedaron reversados a cero");
    }

    // ----------------------------------------------------------------------------- US7 (T091) --

    [Fact]
    public async Task Todo_queda_auditado_y_sin_permiso_el_servidor_responde_el_mismo_404_que_una_ruta_inexistente()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // (a) abrir una opción del ERP queda en la auditoría con módulo Navigation
        var acceso = await EnviarAsync(http, admin, HttpMethod.Post, "/api/audit/access", new { route = "/contabilidad/libro-auxiliar", title = "Libro auxiliar" });
        acceso.StatusCode.Should().Be(HttpStatusCode.Accepted, $"«{await acceso.Content.ReadAsStringAsync()}»");
        // Desde la feature 012 (T37) Navigation es un módulo encadenado: el evento queda en COR_AuditOutbox y
        // llega a Mongo por el reenviador, que la fixture tiene apagado; se corre una pasada a mano.
        await fx.ReenviarAuditoriaAsync(ctx.TenantPublicId);
        var navegacion = await EventosDeAuditoriaAsync(http, admin, "Navigation", "RegisterOptionAccess",
            e => (e.GetProperty("newValuesJson").GetString() ?? string.Empty).Contains("libro-auxiliar"));
        navegacion.Should().NotBeEmpty("el acceso aparece en /api/audit/logs?module=Navigation en menos de 30 s");

        // (b) cambiar las reglas de una cuenta deja antes y después
        var codigo = "51101201"; // 511012 bajo gastos generales
        await CrearAuxiliarAsync(http, admin, codigo, "Auditoría de reglas");
        var cuenta = await CuentaAsync(http, admin, codigo);
        var cambio = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/accounting/accounts/{cuenta}", new
        {
            publicId = cuenta, name = "Auditoría de reglas (con centro)", enabledModules = new[] { "CNT" },
            requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = true, requiresBranch = false, bank = (object?)null, tax = (object?)null,
        });
        cambio.IsSuccessStatusCode.Should().BeTrue($"«{await cambio.Content.ReadAsStringAsync()}»");
        var cambios = await EventosDeAuditoriaAsync(http, admin, "Accounting", "Accounting.Account.Updated",
            e => e.GetProperty("entityId").GetString() == cuenta.ToString());
        cambios.Should().NotBeEmpty();
        var evento = cambios[0];
        JsonDocument.Parse(evento.GetProperty("oldValuesJson").GetString()!).RootElement.GetProperty("requiresCostCenter").GetBoolean().Should().BeFalse();
        JsonDocument.Parse(evento.GetProperty("newValuesJson").GetString()!).RootElement.GetProperty("requiresCostCenter").GetBoolean().Should().BeTrue();

        // (c) sólo lectura: toda escritura es 404 Generic.NotFound, idéntico al de una ruta que no existe; las lecturas, 200
        var lectura = ctx.TokenSoloLectura;
        var inexistente = await EnviarAsync(http, lectura, HttpMethod.Post, "/api/accounting/no-existe", new { });
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var borradorSinPermiso = await EnviarAsync(http, lectura, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "CG", date = new DateOnly(2026, 9, 9), description = "Sin permiso", lines = new[] { Linea(CajaPrincipal, ctx.SucursalPrincipal, 1m, 0m), Linea(IngresoPrincipal, ctx.SucursalPrincipal, 0m, 1m) },
        });
        borradorSinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CodigoDeErrorAsync(borradorSinPermiso)).Should().Be("Generic.NotFound");
        foreach (var (metodo, ruta) in new[]
                 {
                     (HttpMethod.Post, $"/api/accounting/documents/{ctx.ComprobantePrincipal}/reverse"),
                     (HttpMethod.Post, "/api/accounting/periods/2026/9/close"),
                     (HttpMethod.Post, "/api/accounting/periods/years/2026/close"),
                     (HttpMethod.Post, "/api/accounting/accounts"),
                     (HttpMethod.Put, "/api/accounting/setup"),
                     (HttpMethod.Get, "/api/accounting/opening"),
                 })
        {
            var resp = await EnviarAsync(http, lectura, metodo, ruta, new { });
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{metodo} {ruta} sin permiso");
        }
        (await EnviarAsync(http, lectura, HttpMethod.Get, "/api/accounting/documents?page=1&pageSize=5", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(http, lectura, HttpMethod.Get, "/api/accounting/periods?year=2026", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EnviarAsync(http, lectura, HttpMethod.Get, $"/api/accounting/documents/{ctx.ComprobantePrincipal}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // (d) Operador: digita (201) pero no contabiliza (404)
        var operador = await CrearOperadorAsync(http, admin, ctx.TenantPublicId);
        var borradorOperador = await EnviarAsync(http, operador, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "CG", date = new DateOnly(2026, 9, 9), description = "Del operador", lines = new[] { Linea(CajaPrincipal, ctx.SucursalPrincipal, 5m, 0m), Linea(IngresoPrincipal, ctx.SucursalPrincipal, 0m, 5m) },
        });
        borradorOperador.StatusCode.Should().Be(HttpStatusCode.Created, $"el Operador digita: «{await borradorOperador.Content.ReadAsStringAsync()}»");
        var idOperador = (await LeerAsync(borradorOperador)).GetProperty("publicId").GetGuid();
        (await EnviarAsync(http, operador, HttpMethod.Post, $"/api/accounting/documents/{idOperador}/post", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "el Operador no contabiliza");
        (await EnviarAsync(http, admin, HttpMethod.Delete, $"/api/accounting/documents/drafts/{idOperador}", null)).IsSuccessStatusCode.Should().BeTrue();
    }

    // -------------------------------------------------------------------------------- ayudantes --

    private static async Task<HttpResponseMessage> ImportarCuentasAsync(HttpClient http, string token, string csv)
    {
        using var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "cuentas.csv");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/accounting/accounts/import") { Content = contenido };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> ImportarCatalogoAsync(HttpClient http, string token, string nombre, string csv)
    {
        using var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "catalogo.csv");
        contenido.Add(new StringContent(nombre), "name");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/accounting/setup/catalogs/import") { Content = contenido };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }

    /// <summary>Un usuario con el rol Operador de la cooperativa compartida: invitado, acepta y recibe el rol built-in. Idempotente entre pruebas.</summary>
    private async Task<string> CrearOperadorAsync(HttpClient http, string admin, Guid tenantId)
    {
        var invitacion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{tenantId}/invitations", new { email = CorreoOperador });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, $"invitación del operador: «{await invitacion.Content.ReadAsStringAsync()}»");
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == CorreoOperador);
        mensaje.Should().NotBeNull();
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        var aceptar = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = ClaveOperador } });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar: «{await aceptar.Content.ReadAsStringAsync()}»");
        var acceso = (await LeerAsync(aceptar)).GetProperty("accessToken").GetString()!;

        var roles = await GetAsync(http, admin, "/api/admin/roles?includeBuiltIn=true&pageSize=50");
        var operador = roles.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == "Operator").GetProperty("publicId").GetGuid();
        var usuarios = await GetAsync(http, admin, $"/api/admin/users?search={Uri.EscapeDataString(CorreoOperador)}&pageSize=50");
        var usuario = usuarios.GetProperty("items").EnumerateArray().Single(u => string.Equals(u.GetProperty("email").GetString(), CorreoOperador, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
        var asignacion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = operador });
        asignacion.IsSuccessStatusCode.Should().BeTrue($"rol Operador: «{await asignacion.Content.ReadAsStringAsync()}»");
        var permisos = await GetAsync(http, acceso, "/api/admin/permissions/mine");
        var codigos = permisos.GetProperty("permissions").EnumerateArray().Select(c => c.GetString()).ToList();
        codigos.Should().Contain("Accounting.Vouchers.Create").And.NotContain("Accounting.Vouchers.Post");
        return acceso;
    }
}
