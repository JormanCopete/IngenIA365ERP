using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.OriginacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de la libranza (feature 013, K2, US10, T0819, T0820): la cooperativa del desembolso (<see cref="DesembolsoE2E.EscenarioAsync"/>,
/// activada) más lo que pide la libranza, todo por HTTP:
/// <list type="bullet">
/// <item>los límites legales sembrados <c>LIBRANZA_CAPACIDAD_DESCUENTO</c> (0,5) y <c>LIBRANZA_VENTANA_GIRO_DIAS</c> (3) y el
/// <c>LIBRANZA_UMBRAL_MORA_PAGADURIA</c> (30, que la semilla no trae) registrado en la central del host y replicado; el parámetro
/// <c>Libranza.UmbralDeMoraDePagaduria</c> en 30 días. <b>El 30 es del ensayo</b>: el valor real lo coteja el dueño contra la Circular Básica
/// Contable y Financiera (T0855);</item>
/// <item>el perfil regulatorio corregido con la inscripción como operador de libranza (FR-108);</item>
/// <item>dos pagadurías (personas jurídicas del maestro) y un formato de ida (<c>PayrollDeductionRequest</c>) y uno de vuelta
/// (<c>PayrollDeductionResult</c>) genéricos y delimitados —<b>formatos del ensayo</b>: los layouts reales de cada pagaduría los aporta el dueño
/// (T0856)—;</item>
/// <item>los convenios <c>PAGSED</c> (Secretaría de Educación) y <c>PAGGOB</c> (Gobernación): corte 20, envío 25, pago 30, tope 40 %, ventana de giro de
/// 3 días, regla «pasar a caja» y los códigos 00 (descontado), 07 (licencia) y 08 (retiro);</item>
/// <item>la línea <c>LIBRANZA</c> (consumo con libranza) que admite la caja y la libranza, en el alcance de quien radica y decide, y una certificación
/// de la pagaduría subida como adjunto.</item>
/// </list>
/// Un crédito por libranza se radica con <c>asesor.o1</c>, lo aprueba <c>analista</c> y lo desembolsa <c>jefe</c> por cheque. Lo de la libranza lo
/// opera <c>administrador</c> (plantilla <c>cartera.administrador</c>: todo <c>Lending.*</c>) con un ingreso nuevo cada vez, porque las pruebas son
/// largas y el access dura 15 minutos. (nuevo)
/// </summary>
public static class LibranzaE2E
{
    public const string Convenios = "/api/lending/payroll-agreements";
    public const string Archivos = "/api/lending/payroll-deduction-files";
    public const string NoAplicados = "/api/lending/payroll-not-applied";
    public const string Recaudador = "/api/lending/test-doubles/collector";
    public const string Informes = "/api/reports/lending";
    public const decimal Tope = 0.4m;
    public const int VentanaDeGiro = 3;
    public const int Umbral = 30;
    public const decimal IngresoCertificado = 2_000_000m;

    /// <summary>El escenario de la libranza.</summary>
    public sealed record Escenario(
        DesembolsoE2E.Escenario Desembolso,
        string Nombre,
        Guid Linea,
        Guid Convenio,
        Guid Pagaduria,
        Guid OtroConvenio,
        Guid OtraPagaduria,
        Guid Certificacion)
    {
        public CarteraE2E.CooperativaAislada Coop => Desembolso.Coop;
        public UsuariosDeCartera Usuarios => Desembolso.Usuarios;
        public DateOnly Hoy => Desembolso.Hoy;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();
    private static int _ingresos, _nit, _empleado;

    public static Task<Escenario> EscenarioAsync(CentralIdentityApiFixture fx, string nombre = "libranza")
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararAsync(fx, nombre);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Escenario> PrepararAsync(CentralIdentityApiFixture fx, string nombre)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, nombre);
        using var http = fx.CreateClient();
        var admin = await AdministradorDeLaCooperativaAsync(http, nombre);
        var hoy = esc.Hoy;

        // El umbral de mora de la pagaduría en la central y su copia en la cooperativa: la semilla no lo trae (C71, «a cotejar en la CBCF»; T0855),
        // así que el 30 es del ensayo. La capacidad de descuento (0,5) y la ventana de giro (3 días) sí vienen sembradas.
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "LIMITE", key = "LIBRANZA_UMBRAL_MORA_PAGADURIA", validFrom = new DateOnly(2026, 1, 1), value = (decimal)Umbral, unit = "Dias",
            source = "Circular Básica Contable y Financiera, valor del ensayo (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, esc.Coop.TenantPublicId);
        var umbral = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/parameters/Libranza.UmbralDeMoraDePagaduria/versions", new
        {
            scopeKind = 0, value = Umbral.ToString(CultureInfo.InvariantCulture), validFrom = hoy, reason = "Umbral de mora de la pagaduría del ensayo (e2e)",
        });
        umbral.IsSuccessStatusCode.Should().BeTrue($"el umbral de mora de la pagaduría: «{await umbral.Content.ReadAsStringAsync()}»");

        // El perfil regulatorio con la inscripción como operador de libranza: una corrección de la versión de hoy.
        var perfil = await CarteraE2E.GetAsync(http, admin, "/api/core/regulatory-profile");
        var vigente = perfil.GetProperty("current");
        var corregido = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
        {
            organizationType = vigente.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2,
            payrollOperatorRegistration = new { number = "OPL-E2E-001", date = hoy.AddDays(-30) },
            validFrom = DateOnly.Parse(vigente.GetProperty("validFrom").GetString()!, CultureInfo.InvariantCulture),
            source = "Certificado de la Supersolidaria con la inscripción de operador de libranza (e2e)",
            reason = "La cooperativa opera libranzas", supersedesPublicId = vigente.GetProperty("versionPublicId").GetGuid(),
        });
        corregido.StatusCode.Should().Be(HttpStatusCode.Created, $"el perfil con la inscripción: «{await corregido.Content.ReadAsStringAsync()}»");

        // Las pagadurías y los formatos del ensayo (T0856: los reales los aporta el dueño).
        var pagaduria = await PagaduriaAsync(http, admin, "Secretaría de Educación (e2e)");
        var otraPagaduria = await PagaduriaAsync(http, admin, "Gobernación (e2e)");
        var ida = await FormatoAsync(http, admin, $"LIBIDA{nombre.Length:00}", "PayrollDeductionRequest", hoy,
            ("Documento", "PayeeDocument", true), ("Empleado", "PayerEmployeeId", false), ("Credito", "LoanNumber", true),
            ("Cuota", "InstallmentNumber", false), ("Valor", "Amount", true), ("Novedad", "NoveltyKind", true));
        var vuelta = await FormatoAsync(http, admin, $"LIBVUE{nombre.Length:00}", "PayrollDeductionResult", hoy,
            ("Credito", "LoanNumber", true), ("Codigo", "ResultCode", true), ("Descontado", "DeductedAmount", false), ("Fecha", "DeductionDate", false));

        var convenio = await ConvenioAsync(http, admin, "PAGSED", "Libranza Secretaría de Educación", pagaduria, ida, vuelta, hoy);
        var otroConvenio = await ConvenioAsync(http, admin, "PAGGOB", "Libranza Gobernación", otraPagaduria, ida, vuelta, hoy);

        // La línea con libranza, en el alcance de quien radica y decide.
        var version = VersionDeOriginacion(hoy, conDocumento: false);
        version["subsegmentCode"] = "ConLibranza";
        version["paymentForms"] = new[] { "Cashier", "PaymasterDeduction" };
        var linea = await LineasE2E.CrearExitoAsync(http, admin, "LIBRANZA", "Consumo por libranza", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, linea };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        var certificacion = await SubirEvidenciaAsync(http, admin, pagaduria);
        return new Escenario(esc, nombre, linea, convenio, pagaduria, otroConvenio, otraPagaduria, certificacion);
    }

    // ------------------------------------------------------------------------------------------------ créditos --

    /// <summary>El cuerpo de una solicitud por libranza del convenio, con la certificación del ensayo y el ingreso neto certificado.</summary>
    public static object Solicitud(Escenario esc, Guid persona, decimal monto, decimal ingreso, string empleado, Guid? convenio = null) => new
    {
        applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = esc.Linea,
        purpose = "Libre inversión por libranza (e2e)", amount = monto, installments = 12, periodicity = "Monthly", amortizationSystem = "LevelPayment",
        paymentForm = "PaymasterDeduction", prepaymentPreference = "ReduceTerm",
        payroll = new { payrollAgreementPublicId = convenio ?? esc.Convenio, employeeId = empleado, certificationAttachmentPublicId = esc.Certificacion,
            certificationDate = esc.Hoy.AddDays(-2) },
        applicantCapacity = new { monthlyIncome = ingreso, monthlyExpenses = 0m, externalInstallments = 0m },
    };

    /// <summary>Accesos nuevos de quienes originan (las pruebas son largas y el access dura 15 minutos).</summary>
    public sealed record Accesos(string Admin, string Asesor, string Analista, string Jefe);

    public static async Task<Accesos> AccesosAsync(HttpClient http, Escenario esc) => new(
        await AdministradorDeLaCooperativaAsync(http, esc.Nombre),
        await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.AsesorO1),
        await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Analista),
        await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Jefe));

    /// <summary>Un crédito por libranza: radica <c>asesor.o1</c>, aprueba <c>analista</c>, desembolsa <c>jefe</c> por cheque. Devuelve el crédito y su número.</summary>
    public static async Task<(Guid Credito, string Numero, Guid Persona, string Empleado)> CreditoPorLibranzaAsync(HttpClient http, Escenario esc,
        Accesos a, string nombre, decimal monto = 1_000_000m)
    {
        var (persona, _) = await DesembolsoE2E.DeudorAsync(http, a.Admin, nombre);
        var empleado = $"EMP-{Interlocked.Increment(ref _empleado):D5}";
        var radicada = await RadicarAsync(http, a.Asesor, Solicitud(esc, persona, monto, IngresoCertificado, empleado));
        var solicitud = radicada.GetProperty("publicId").GetGuid();
        var (_, huella) = await EnrutamientoAsync(http, a.Asesor, solicitud);
        var aprobada = await DecidirAsync(http, a.Analista, "applications", solicitud, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var resp = await CarteraE2E.ConClaveAsync(http, a.Jefe, HttpMethod.Post, Desembolsos, Desembolso(esc.Desembolso, solicitud, persona));
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"desembolsar: «{await resp.Content.ReadAsStringAsync()}»");
        var hecho = await CarteraE2E.LeerAsync(resp);
        return (hecho.GetProperty("loanPublicId").GetGuid(), hecho.GetProperty("loanNumber").GetString()!, persona, empleado);
    }

    // ------------------------------------------------------------------------------------------------ archivo --

    /// <summary>Genera el archivo del período con <paramref name="token"/> y exige el 201.</summary>
    public static async Task<JsonElement> GenerarAsync(HttpClient http, string token, Guid convenio, int anio, int mes)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Archivos, new { agreementPublicId = convenio, year = anio, month = mes });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"generar el archivo: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>El archivo con todas sus líneas (páginas de 200).</summary>
    public static async Task<(JsonElement Archivo, List<JsonElement> Lineas)> ArchivoAsync(HttpClient http, string token, Guid archivo)
    {
        var lineas = new List<JsonElement>();
        JsonElement cabecera = default;
        for (var pagina = 1; ; pagina++)
        {
            var detalle = await CarteraE2E.GetAsync(http, token, $"{Archivos}/{archivo}?page={pagina}&pageSize=200");
            cabecera = detalle.GetProperty("file");
            lineas.AddRange(detalle.GetProperty("lines").EnumerateArray());
            if (lineas.Count >= detalle.GetProperty("totalLines").GetInt32() || detalle.GetProperty("lines").GetArrayLength() == 0) break;
        }
        return (cabecera, lineas);
    }

    /// <summary>El recaudador de prueba confirma el envío (usuario <c>pruebas</c>) y el despachador lo lleva a Cartera.</summary>
    public static async Task ConfirmarEnvioAsync(CentralIdentityApiFixture fx, HttpClient http, Escenario esc, Guid archivo, string radicado)
    {
        var pruebas = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Pruebas);
        var resp = await CarteraE2E.ConClaveAsync(http, pruebas, HttpMethod.Post, $"{Recaudador}/payroll-deduction-files/{archivo}/confirm-sent",
            new { channel = "Portal", reference = radicado });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"confirmar el envío: «{await resp.Content.ReadAsStringAsync()}»");
        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId);
    }

    /// <summary>
    /// La respuesta de la pagaduría en el formato de vuelta del ensayo (crédito;código;descontado;fecha) que el recaudador de prueba emite como
    /// <c>ResultadoDeDescuentos</c>; el despachador la lleva a Cartera. Devuelve el mensaje.
    /// </summary>
    public static async Task<Guid> ResponderAsync(CentralIdentityApiFixture fx, HttpClient http, Escenario esc, Guid archivo,
        IEnumerable<(string Credito, string Codigo, decimal Descontado, DateOnly? Fecha)> filas)
    {
        var texto = string.Join("\n", filas.Select(f =>
            $"{f.Credito};{f.Codigo};{f.Descontado.ToString("0.00", CultureInfo.InvariantCulture)};{f.Fecha?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? string.Empty}")) + "\n";
        var pruebas = await UsuariosDeCartera.IngresarAsync(http, esc.Usuarios.Pruebas);
        using var formulario = new MultipartFormDataContent();
        formulario.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(texto)), "file", "respuesta-pagaduria.csv");
        formulario.Add(new StringContent(archivo.ToString()), "filePublicId");
        using var pedido = new HttpRequestMessage(HttpMethod.Post, $"{Recaudador}/payroll-deduction-results") { Content = formulario };
        pedido.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", pruebas);
        pedido.Headers.Add(CarteraE2E.CabeceraDeClave, Guid.NewGuid().ToString());
        var resp = await http.SendAsync(pedido);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"la respuesta de la pagaduría: «{await resp.Content.ReadAsStringAsync()}»");
        var mensaje = (await CarteraE2E.LeerAsync(resp)).GetProperty("messagePublicId").GetGuid();
        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId);
        return mensaje;
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>Un ingreso nuevo del administrador de la cooperativa (el que aceptó la invitación al crearla).</summary>
    public static async Task<string> AdministradorDeLaCooperativaAsync(HttpClient http, string nombre)
    {
        var n = Interlocked.Increment(ref _ingresos);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = $"admin.{nombre}@coop.cartera.test", password = $"Car-{nombre}-2026!" }),
        };
        pedido.Headers.Add("CF-Connecting-IP", $"10.253.{n / 250 % 250}.{n % 250 + 1}");
        var resp = await http.SendAsync(pedido);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"ingresar como administrador de {nombre}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> PagaduriaAsync(HttpClient http, string token, string razonSocial)
    {
        var nit = (890_400_000 + Interlocked.Increment(ref _nit)).ToString(CultureInfo.InvariantCulture);
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "N", taxId = nit, firstName = "Pagaduría", lastName = "Del Ensayo", businessName = razonSocial, email = $"nomina.{nit}@pagaduria.test",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"pagaduría {razonSocial}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetGuid();
    }

    public static async Task<Guid> FormatoAsync(HttpClient http, string token, string codigo, string ambito, DateOnly hoy,
        params (string Nombre, string Origen, bool Obligatorio)[] campos)
    {
        var definicion = new
        {
            code = codigo, name = $"Libranza {ambito} del ensayo", scope = ambito, validFrom = hoy.AddDays(-1), kind = "Delimited", delimiter = ";",
            encoding = "utf-8", amountFormat = "Point2", fileName = codigo + "-{BatchReference}.csv",
            records = new
            {
                header = new { enabled = false, fields = Array.Empty<object>() },
                detail = new { enabled = true, fields = campos.Select((c, i) => new { order = i + 1, name = c.Nombre, source = c.Origen, required = c.Obligatorio }).ToList() },
                trailer = new { enabled = false, fields = Array.Empty<object>() },
            },
        };
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/bank-file-formats", definicion);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"formato {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("formatPublicId").GetGuid();
    }

    private static async Task<Guid> ConvenioAsync(HttpClient http, string token, string codigo, string nombre, Guid pagaduria, Guid ida, Guid vuelta, DateOnly hoy)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Convenios, new
        {
            code = codigo, name = nombre, payerPersonPublicId = pagaduria, isOwnPayroll = false,
            firstVersion = new
            {
                validFrom = hoy.AddDays(-1), cutoffDay = 20, sendDay = 25, payDay = 30, sendLeadDays = 5, outboundFormatPublicId = ida, resultFormatPublicId = vuelta,
                deductionCap = Tope, transferWindowDays = VentanaDeGiro, notAppliedAction = "MovedToCashPayment", alternateRepaymentChannel = "Cashier",
                resultCodes = new[]
                {
                    new { code = "00", description = "Descontado", outcome = "Deducted", reason = (string?)null },
                    new { code = "07", description = "Licencia no remunerada", outcome = "NotDeducted", reason = (string?)"Leave" },
                    new { code = "08", description = "Retiro del empleado", outcome = "NotDeducted", reason = (string?)"Termination" },
                },
                reason = "Convenio de libranza firmado (e2e)",
            },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"convenio {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }
}
