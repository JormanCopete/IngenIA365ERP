using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010, US8 (T146; quickstart §3.8) por HTTP sobre la cooperativa compartida de nómina: dos
/// bancos con código ACH, una cuenta bancaria del plan como cuenta origen, el formato de demostración
/// <c>DEMO-ANCHOFIJO</c> cargado por <c>POST /api/core/bank-file-formats</c> (D-42), una nómina
/// mensual aprobada con tres empleados —dos con cuenta, uno sin— en un plan propio → vista previa →
/// generar → dos líneas y un pendiente <c>NoBankAccount</c> → descarga con la codificación del formato
/// y la huella → «marcar enviado» → la relación de pago muestra dos pagados con la misma referencia →
/// la reversa de la corrida responde 422 <c>Payroll.PaymentBlocksReversal</c> → editar la estructura del
/// formato responde 422 <c>Core.BankFileFormat.InUse</c> → el Operador no marca enviado (404) → un
/// segundo archivo sólo puede llevar al pendiente y responde <c>NothingToPay</c>. El formato genérico
/// sembrado <c>CSV-GENERICO</c> se comprueba en la lista.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class DispersionTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/payroll/disbursements";
    private const string Formatos = "/api/core/bank-file-formats";

    [Fact]
    public async Task El_archivo_de_dispersion_se_genera_descarga_marca_enviado_y_bloquea_la_reversa()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // --- la empresa (NIT y razón social van en la cabecera del archivo; la cooperativa de prueba no la trae) ---
        var empresa = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/companies",
            new { code = "COOP", name = "Cooperativa de prueba de nómina", shortName = "COOP", taxId = "900123456", taxIdCheckDigit = "7", city = "Cali" });
        empresa.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity], $"empresa: «{await empresa.Content.ReadAsStringAsync()}»");

        // --- bancos con código de transferencia (ACH) y la cuenta bancaria del plan (cuenta origen) ---
        var avVillas = await CrearBancoAsync(http, admin, "AVV", "Banco AV Villas (e2e)", "0052");
        var bancolombia = await CrearBancoAsync(http, admin, "BCL", "Bancolombia (e2e)", "0007");
        var cuentaOrigen = await CrearCuentaBancariaAsync(http, admin, "11100599", "Banco AV Villas cta. corriente (e2e)", avVillas, "1234567890");

        // --- el formato de demostración del contrato, cargado como dato y ligado al banco pagador (D-42) ---
        var formatos = await NominaE2E.GetAsync(http, admin, $"{Formatos}?scope=PayrollDisbursement");
        formatos.EnumerateArray().Should().Contain(f => f.GetProperty("code").GetString() == "CSV-GENERICO", "la semilla deja el formato genérico");
        var creacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Formatos, JsonDocument.Parse(DemoAnchoFijo(avVillas)).RootElement);
        creacion.StatusCode.Should().Be(HttpStatusCode.Created, await creacion.Content.ReadAsStringAsync());
        var formatoId = (await NominaE2E.LeerAsync(creacion)).GetProperty("formatPublicId").GetGuid();
        var origenes = await NominaE2E.GetAsync(http, admin, $"{Formatos}/sources");
        origenes.EnumerateArray().Should().Contain(o => o.GetProperty("code").GetString() == "PayeeAccountNumber" && o.GetProperty("detail").GetBoolean());

        // --- un plan propio con tres empleados y su nómina mensual aprobada ---
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "DISPERS", name = "Plan dispersión e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();
        var (ana, docAna) = await CrearEmpleadoAsync(http, admin, "Anabel", 2_000_000m, planId, avVillas, 1, "9876543210");
        var (carlos, _) = await CrearEmpleadoAsync(http, admin, "Carmelo", 1_750_905m, planId, bancolombia, 2, "1234567890");
        var (beatriz, _) = await CrearEmpleadoAsync(http, admin, "Beatriz", 1_500_000m, planId, null, 0, null);

        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2026, 4, 1), endDate = new DateTime(2026, 4, 30), description = "Abril 2026 (dispersión e2e)", statusMessage = string.Empty });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
        var periodoId = (await NominaE2E.LeerAsync(periodo)).GetGuid();
        var calculada = await NominaE2E.CalcularAsync(http, admin, periodoId);
        var runId = calculada.GetProperty("runPublicId").GetGuid();
        calculada.GetProperty("employeeCount").GetInt32().Should().Be(3);
        await NominaE2E.AprobarAsync(http, admin, runId);

        // ------------------------------------------------------------ vista previa --
        var vista = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/preview",
            new { runPublicId = runId, formatPublicId = formatoId, paymentDate = new DateOnly(2026, 5, 5), sourceAccountPublicId = cuentaOrigen, reference = "NOMINA ABR" });
        vista.StatusCode.Should().Be(HttpStatusCode.OK, await vista.Content.ReadAsStringAsync());
        var previa = await NominaE2E.LeerAsync(vista);
        previa.GetProperty("lineCount").GetInt32().Should().Be(2);
        previa.GetProperty("lines").GetArrayLength().Should().Be(4, "cabecera, dos detalles y totales");
        previa.GetProperty("excluded").EnumerateArray().Should().ContainSingle(e => e.GetProperty("employeePublicId").GetGuid() == beatriz && e.GetProperty("reasonCode").GetString() == "NoBankAccount");
        (await NominaE2E.GetAsync(http, admin, $"{Ruta}?runId={runId}")).GetArrayLength().Should().Be(0, "la vista previa no persiste");

        // ---------------------------------------------------------------- generar --
        var generar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta,
            new { runPublicId = runId, formatPublicId = formatoId, paymentDate = new DateOnly(2026, 5, 5), sourceAccountPublicId = cuentaOrigen, reference = "NOMINA ABR" });
        generar.StatusCode.Should().Be(HttpStatusCode.Created, await generar.Content.ReadAsStringAsync());
        var generado = await NominaE2E.LeerAsync(generar);
        var archivoId = generado.GetProperty("filePublicId").GetGuid();
        generado.GetProperty("fileName").GetString().Should().Be("DEMO20260505.txt");
        generado.GetProperty("lineCount").GetInt32().Should().Be(2);
        var total = generado.GetProperty("totalAmount").GetDecimal();
        generado.GetProperty("excluded").GetArrayLength().Should().Be(1);

        var detalle = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{archivoId}");
        detalle.GetProperty("summary").GetProperty("status").GetString().Should().Be("Generated");
        detalle.GetProperty("summary").GetProperty("formatCode").GetString().Should().Be("DEMO-ANCHOFIJO");
        var lineas = detalle.GetProperty("lines").EnumerateArray().ToList();
        lineas.Should().HaveCount(2).And.OnlyContain(l => !l.GetProperty("paid").GetBoolean());
        lineas.Select(l => l.GetProperty("recordText").GetString()!.Length).Should().OnlyContain(n => n == 114, "cada detalle del formato de ancho fijo mide 114 posiciones");
        var relacion = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        var netoAna = relacion.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeePublicId").GetGuid() == ana).GetProperty("netPay").GetDecimal();
        var netoCarlos = relacion.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeePublicId").GetGuid() == carlos).GetProperty("netPay").GetDecimal();
        total.Should().Be(netoAna + netoCarlos, "el total del archivo es la suma de los netos incluidos");

        // --------------------------------------------------------------- descargar --
        using var descarga = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"{Ruta}/{archivoId}/file", null);
        descarga.StatusCode.Should().Be(HttpStatusCode.OK, await descarga.Content.ReadAsStringAsync());
        descarga.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        descarga.Content.Headers.ContentType.CharSet.Should().Be("us-ascii");
        descarga.Content.Headers.ContentDisposition?.FileName?.Trim('"').Should().Be("DEMO20260505.txt");
        var bytes = await descarga.Content.ReadAsByteArrayAsync();
        var texto = Encoding.ASCII.GetString(bytes);
        var renglones = texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        renglones.Should().HaveCount(4);
        renglones[0].Should().HaveLength(48).And.StartWith("10900123456").And.Contain("00000001234567890").And.Contain("20260505");
        renglones[1].Should().HaveLength(114).And.StartWith("21");
        renglones[3].Should().HaveLength(24).And.StartWith("3000002");
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant().Should().Be(detalle.GetProperty("summary").GetProperty("fileSha256").GetString());
        texto.Should().Contain(docAna.PadLeft(15, '0'), "el documento va a la derecha con ceros");

        // El centro de reportes: la vista «dispersion» en JSON y en Excel.
        (await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/dispersion?fileId={archivoId}")).GetProperty("filas").GetArrayLength().Should().Be(4, "dos líneas, el encabezado de pendientes y el pendiente");
        using var excel = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/dispersion?fileId={archivoId}&format=xlsx", null);
        excel.StatusCode.Should().Be(HttpStatusCode.OK);

        // ------------------------------------------ sin el permiso no se marca enviado --
        var prohibido = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"{Ruta}/{archivoId}/mark-sent", new { sentAt = new DateTime(2026, 5, 5), bankReference = "X" });
        prohibido.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin permiso la respuesta es la misma que si no existiera");
        (await NominaE2E.CodigoDeErrorAsync(prohibido)).Should().Be("Generic.NotFound");

        // ----------------------------------------------------------- marcar enviado --
        var enviado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{archivoId}/mark-sent", new { sentAt = new DateTime(2026, 5, 5, 8, 30, 0), bankReference = "AVV-2026-0505-77" });
        enviado.StatusCode.Should().Be(HttpStatusCode.OK, await enviado.Content.ReadAsStringAsync());
        var marcado = await NominaE2E.LeerAsync(enviado);
        marcado.GetProperty("markedPaid").GetInt32().Should().Be(2);
        marcado.GetProperty("alreadyMarked").GetArrayLength().Should().Be(0);

        relacion = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        relacion.GetProperty("paidCount").GetInt32().Should().Be(2);
        var pagados = relacion.GetProperty("rows").EnumerateArray().Where(r => r.GetProperty("paid").GetBoolean()).ToList();
        pagados.Select(r => r.GetProperty("employeePublicId").GetGuid()).Should().BeEquivalentTo([ana, carlos]);
        pagados.Should().OnlyContain(r => r.GetProperty("reference").GetString() == "AVV-2026-0505-77" && r.GetProperty("paymentMethod").GetString() == "Transfer");
        relacion.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeePublicId").GetGuid() == beatriz).GetProperty("paid").GetBoolean().Should().BeFalse("la pendiente sigue sin pagar");
        (await NominaE2E.GetAsync(http, admin, $"{Ruta}/{archivoId}")).GetProperty("summary").GetProperty("status").GetString().Should().Be("Sent");

        // La corrida ya no se reversa: hay pagos marcados.
        var reversa = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/reverse", new { reason = "prueba" });
        reversa.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await reversa.Content.ReadAsStringAsync());
        (await NominaE2E.CodigoDeErrorAsync(reversa)).Should().Be("Payroll.PaymentBlocksReversal");

        // Un enviado no se anula; el formato usado no cambia de estructura; un segundo archivo no tiene a quién llevar.
        (await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{archivoId}/cancel", new { reason = "x" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var edicion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"{Formatos}/{formatoId}", JsonDocument.Parse(DemoAnchoFijo(avVillas, sinConcepto: true)).RootElement);
        edicion.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await edicion.Content.ReadAsStringAsync());
        (await NominaE2E.CodigoDeErrorAsync(edicion)).Should().Be("Core.BankFileFormat.InUse");
        var otro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, Ruta,
            new { runPublicId = runId, formatPublicId = formatoId, paymentDate = new DateOnly(2026, 5, 6), sourceAccountPublicId = cuentaOrigen });
        otro.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await NominaE2E.CodigoDeErrorAsync(otro)).Should().Be("Payroll.Disbursement.NothingToPay");
    }

    // ------------------------------------------------------------------ helpers --

    private static async Task<Guid> CrearBancoAsync(HttpClient http, string token, string code, string name, string transferCode)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/banks", new { code, name, shortName = code, transferCode });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"banco {code}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetGuid();
    }

    private static async Task<Guid> CrearCuentaBancariaAsync(HttpClient http, string token, string codigo, string nombre, Guid bancoId, string numero)
    {
        var busqueda = await NominaE2E.LeerAsync(await NominaE2E.EnviarAsync(http, token, HttpMethod.Get, "/api/accounting/accounts/search?q=111005&onlyMovement=false", null));
        var padre = busqueda.EnumerateArray().First(c => c.GetProperty("code").GetString() == "111005");
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = codigo, name = nombre, parentPublicId = padre.GetProperty("publicId").GetGuid(),
            enabledModules = new[] { "CNT", "NOM", "TES" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
            bank = new { bankPublicId = bancoId, accountNumber = numero },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"cuenta bancaria: «{await resp.Content.ReadAsStringAsync()}»");
        var cuentas = await NominaE2E.GetAsync(http, token, "/api/accounting/accounts/bank-accounts");
        return cuentas.EnumerateArray().Single(c => c.GetProperty("code").GetString() == codigo).GetProperty("accountPublicId").GetGuid();
    }

    private static async Task<(Guid EmpleadoId, string Documento)> CrearEmpleadoAsync(HttpClient http, string token, string nombre, decimal salario, Guid planId, Guid? bancoId, int tipoCuenta, string? cuenta)
    {
        var documento = (700_000_000 + Random.Shared.Next(1, 99_999_999)).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Dispersión", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = new DateTime(2025, 1, 15), payrollPlanPublicId = planId,
            payrollBankPublicId = bancoId, payrollBankAccountType = tipoCuenta, payrollBankAccountNumber = cuenta,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return ((await NominaE2E.LeerAsync(empleado)).GetGuid(), documento);
    }

    /// <summary>El formato ficticio de <c>contracts/archivos.md</c> §2.2 (el mismo JSON de FlatFileWriterTests), ligado al banco indicado (dos genéricos del mismo ámbito no pueden cruzarse con CSV-GENERICO) y vigente desde abril de 2026.</summary>
    private static string DemoAnchoFijo(Guid? bankPublicId, bool sinConcepto = false)
    {
        var banco = bankPublicId is { } b ? $"\"{b}\"" : "null";
        var concepto = sinConcepto ? "" : """
            ,{ "order": 9, "name": "Concepto", "source": "PaymentConcept", "length": 20, "align": "Left", "pad": " ", "truncate": true }
            """;
        return $$"""
        { "code": "DEMO-ANCHOFIJO", "name": "Demostración ancho fijo", "bankPublicId": {{banco}}, "scope": "PayrollDisbursement",
          "validFrom": "2026-04-01", "kind": "FixedWidth", "encoding": "us-ascii", "lineEnding": "CRLF",
          "uppercase": true, "stripAccents": true, "amountFormat": "ImplicitCents",
          "fileName": "DEMO{PaymentDate:yyyyMMdd}.txt",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo", "source": "Constant", "value": "1", "length": 1 },
              { "order": 2, "name": "NIT", "source": "CompanyNit", "length": 10, "align": "Right", "pad": "0" },
              { "order": 3, "name": "Cuenta origen", "source": "SourceAccountNumber", "length": 17, "align": "Right", "pad": "0" },
              { "order": 4, "name": "Fecha de pago", "source": "PaymentDate", "format": "yyyyMMdd", "length": 8 },
              { "order": 5, "name": "Referencia", "source": "BatchReference", "length": 12, "align": "Left", "pad": " ", "truncate": true } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo", "source": "Constant", "value": "2", "length": 1 },
              { "order": 2, "name": "Tipo doc.", "source": "EmployeeDocumentType", "length": 1, "map": { "CC": "1", "CE": "2", "PA": "4", "PE": "5" } },
              { "order": 3, "name": "Documento", "source": "EmployeeDocument", "length": 15, "align": "Right", "pad": "0", "required": true },
              { "order": 4, "name": "Nombre", "source": "EmployeeFullName", "length": 40, "align": "Left", "pad": " ", "truncate": true },
              { "order": 5, "name": "Banco destino", "source": "EmployeeBankCode", "length": 4, "align": "Right", "pad": "0", "required": true },
              { "order": 6, "name": "Tipo cuenta", "source": "EmployeeAccountType", "length": 1, "map": { "1": "S", "2": "D" } },
              { "order": 7, "name": "Cuenta", "source": "EmployeeAccountNumber", "length": 17, "align": "Right", "pad": "0", "required": true },
              { "order": 8, "name": "Valor", "source": "NetAmount", "length": 15, "align": "Right", "pad": "0" }{{concepto}} ] },
            "trailer": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo", "source": "Constant", "value": "3", "length": 1 },
              { "order": 2, "name": "Registros", "source": "LineCount", "length": 6, "align": "Right", "pad": "0" },
              { "order": 3, "name": "Total", "source": "TotalAmount", "length": 17, "align": "Right", "pad": "0" } ] } } }
        """;
    }
}
