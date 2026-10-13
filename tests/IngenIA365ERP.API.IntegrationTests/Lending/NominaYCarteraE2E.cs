using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// La nómina propia como recaudador de Cartera (feature 013, US5, T0528): una cooperativa con Cartera activada (el escenario del desembolso) a la
/// que se le prepara la nómina como <see cref="NominaE2E"/> prepara la suya —centro de costo, dos auxiliares de Nómina, las cuentas de cada concepto
/// y la política «quien calcula aprueba»—, la línea <c>NOMINA</c> que admite la nómina propia como forma de pago, y empleados que son asociados con
/// un crédito desembolsado que se paga por nómina (<c>LND_LoanPayrollLinks</c> con <c>IsOwnPayroll</c>). Cada prueba usa su propio plan de nómina
/// para que el período de una no incluya a los empleados de la otra. (nuevo)
/// </summary>
public static class NominaYCarteraE2E
{
    public const string CuentaDebito = "51050399";
    public const string CuentaCredito = "27100599";
    public const string ConceptoDeCartera = "DESC_CARTERA";

    /// <summary>El escenario: el del desembolso con la nómina preparada y la línea que se paga por nómina.</summary>
    public sealed record Escenario(DesembolsoE2E.Escenario Desembolso, Guid LineaDeNomina)
    {
        public CarteraE2E.CooperativaAislada Coop => Desembolso.Coop;
        public UsuariosDeCartera Usuarios => Desembolso.Usuarios;
        public DateOnly Hoy => Desembolso.Hoy;
        public string Admin => Desembolso.Coop.TokenAdmin;
    }

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();
    private static int _sufijo;

    public static Task<Escenario> EscenarioAsync(CentralIdentityApiFixture fx)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararAsync(fx);
                Escenarios[fx] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Escenario> PrepararAsync(CentralIdentityApiFixture fx)
    {
        var esc = await DesembolsoE2E.EscenarioAsync(fx, "nomina_cartera");
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;

        // --- la nómina: centro de costo, auxiliares, cuentas de cada concepto salvo DESC_CARTERA (cada prueba la pone) y la política ---
        await CrearAsync(http, admin, "/api/core/cost-centers", new { name = "Administración", payrollType = 1, period = 1, payrollPeriodicity = 30 });
        await AuxiliarAsync(http, admin, CuentaDebito, "Sueldos y salarios (Cartera e2e)", "510503");
        await AuxiliarAsync(http, admin, CuentaCredito, "Salarios por pagar (Cartera e2e)", "271005");
        var conceptos = await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions");
        foreach (var c in conceptos.EnumerateArray().Select(c => c.GetProperty("code").GetString()!).Where(c => c != ConceptoDeCartera))
            await CuentasDelConceptoAsync(http, admin, c);
        var vigencia = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/company-policies/AllowSameUserApproval/versions", new
        {
            value = "true", validFrom = new DateOnly(2000, 1, 1), reason = "Pruebas e2e: quien calcula aprueba con segunda confirmación", closePrevious = true,
        });
        vigencia.IsSuccessStatusCode.Should().BeTrue($"AllowSameUserApproval: «{await vigencia.Content.ReadAsStringAsync()}»");

        // --- la línea que admite la nómina propia, en el alcance de quien radica y decide ---
        var version = OriginacionE2E.VersionDeOriginacion(esc.Hoy, conDocumento: false);
        version["paymentForms"] = new[] { "Cashier", "OwnPayroll" };
        var linea = await LineasE2E.CrearExitoAsync(http, admin, "NOMINA", "Consumo por nómina propia", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, linea };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        return new Escenario(esc, linea);
    }

    /// <summary>Las cuentas del concepto (débito y crédito de la cooperativa), sin centro de costo.</summary>
    public static async Task CuentasDelConceptoAsync(HttpClient http, string admin, string concepto)
    {
        var resp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/concept-definitions/{concepto}/accounts",
            new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = CuentaDebito, creditAccountCode = CuentaCredito } } });
        resp.IsSuccessStatusCode.Should().BeTrue($"cuentas de {concepto}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Quita las cuentas del concepto (la lista vacía las reemplaza todas).</summary>
    public static async Task SinCuentasDelConceptoAsync(HttpClient http, string admin, string concepto)
    {
        var resp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/concept-definitions/{concepto}/accounts", new { rows = Array.Empty<object>() });
        resp.IsSuccessStatusCode.Should().BeTrue($"quitar las cuentas de {concepto}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Un plan mensual propio.</summary>
    public static async Task<Guid> PlanAsync(HttpClient http, Escenario esc, string prefijo, string periodicidad = "Monthly")
    {
        var sufijo = Interlocked.Increment(ref _sufijo);
        var resp = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/payroll/plans",
            new { code = $"{prefijo}{sufijo}", name = $"Plan {prefijo} {sufijo} (Cartera e2e)", periodicity = periodicidad });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await NominaE2E.LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    /// <summary>Un período del plan.</summary>
    public static async Task<Guid> PeriodoAsync(HttpClient http, Escenario esc, Guid plan, DateOnly desde, DateOnly hasta)
    {
        var resp = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = plan, startDate = desde.ToDateTime(TimeOnly.MinValue), endDate = hasta.ToDateTime(TimeOnly.MinValue),
            description = $"Nómina {desde:dd/MM} – {hasta:dd/MM/yyyy} (Cartera e2e)", statusMessage = string.Empty,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"período: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetGuid();
    }

    /// <summary>
    /// Un asociado que es empleado del plan, con un crédito de la línea <c>NOMINA</c> que se paga por nómina propia, aprobado y desembolsado por
    /// cheque. Con <paramref name="afiliado"/>, la ficha lleva EPS y fondo de pensiones (la definitiva los exige). Devuelve persona, empleado y crédito.
    /// </summary>
    public static async Task<(Guid Persona, Guid Empleado, Guid Credito)> EmpleadoConCreditoAsync(HttpClient http, Escenario esc, Guid plan, string nombre,
        decimal monto = 1_500_000m, decimal salario = 4_000_000m, bool afiliado = false)
    {
        var (persona, _) = await DesembolsoE2E.DeudorAsync(http, esc.Admin, nombre);
        var empleado = await EmpleadoAsync(http, esc, plan, persona, salario, afiliado);
        var credito = await CreditoPorNominaAsync(http, esc, persona, monto);
        return (persona, empleado, credito);
    }

    /// <summary>La ficha de empleado de una persona que ya existe, en el plan.</summary>
    public static async Task<Guid> EmpleadoAsync(HttpClient http, Escenario esc, Guid plan, Guid persona, decimal salario, bool afiliado)
    {
        var sufijo = Interlocked.Increment(ref _sufijo);
        Guid? eps = null, afp = null;
        if (afiliado)
        {
            var e = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/payroll/health-insurance",
                new { code = $"EPSC{sufijo}", name = $"EPS de prueba {sufijo}", taxId = $"8301{sufijo:00000}", checkDigit = 1 });
            e.StatusCode.Should().Be(HttpStatusCode.Created, await e.Content.ReadAsStringAsync());
            eps = (await NominaE2E.LeerAsync(e)).GetGuid();
            var a = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/payroll/pension-providers",
                new { code = $"AFPC{sufijo}", name = $"Fondo de prueba {sufijo}", taxId = $"8302{sufijo:00000}", checkDigit = 2 });
            a.StatusCode.Should().Be(HttpStatusCode.Created, await a.Content.ReadAsStringAsync());
            afp = (await NominaE2E.LeerAsync(a)).GetGuid();
        }
        var resp = await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = persona, baseSalary = salario, contractType = 1, hireDate = new DateTime(2024, 10, 1), payrollPlanPublicId = plan,
            healthInsurancePublicId = eps, pensionProviderPublicId = afp,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetGuid();
    }

    /// <summary>Un crédito de la línea <c>NOMINA</c> por nómina propia: radica <c>asesor.o1</c>, aprueba <c>analista</c>, desembolsa <c>jefe</c>.</summary>
    public static async Task<Guid> CreditoPorNominaAsync(HttpClient http, Escenario esc, Guid persona, decimal monto)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var radicada = await OriginacionE2E.RadicarAsync(http, asesor, new
        {
            applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = esc.LineaDeNomina,
            purpose = "Libre inversión por nómina (e2e)", amount = monto, installments = 24, periodicity = "Monthly", amortizationSystem = "LevelPayment",
            paymentForm = "OwnPayroll", prepaymentPreference = "ReduceTerm",
            applicantCapacity = new { monthlyIncome = 40_000_000m, monthlyExpenses = 2_000_000m, externalInstallments = 0m },
        });
        var solicitud = radicada.GetProperty("publicId").GetGuid();
        var (_, huella) = await OriginacionE2E.EnrutamientoAsync(http, asesor, solicitud);
        var aprobada = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", solicitud, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        var hecho = await DesembolsarAsync(http, esc.Desembolso, Desembolso(esc.Desembolso, solicitud, persona));
        return hecho.GetProperty("loanPublicId").GetGuid();
    }

    /// <summary>Calcula el período (exige el 201) y devuelve la corrida.</summary>
    public static async Task<Guid> CalcularAsync(HttpClient http, Escenario esc, Guid periodo) =>
        (await NominaE2E.CalcularAsync(http, esc.Admin, periodo)).GetProperty("runPublicId").GetGuid();

    /// <summary>Pide aprobar la corrida ordinaria con las excepciones de sus bloqueos; devuelve la respuesta sin juzgarla.</summary>
    public static async Task<HttpResponseMessage> AprobarAsync(HttpClient http, Escenario esc, Guid corrida) =>
        await NominaE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, $"/api/payroll/runs/{corrida}/approve", new
        {
            confirm = true, exceptions = await NominaE2E.ExcepcionesParaAsync(http, esc.Admin, corrida), confirmEmpty = false, confirmWithoutSegregation = true,
        });

    /// <summary>Los descuentos de Cartera de la corrida (<c>GET /api/payroll/runs/{runId}/loan-deductions</c>).</summary>
    public static async Task<List<JsonElement>> DescuentosAsync(HttpClient http, Escenario esc, Guid corrida) =>
        (await NominaE2E.GetAsync(http, esc.Admin, $"/api/payroll/runs/{corrida}/loan-deductions")).EnumerateArray().ToList();

    private static async Task CrearAsync(HttpClient http, string token, string url, object cuerpo)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, url, cuerpo);
        if (resp.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return;
        resp.IsSuccessStatusCode.Should().BeTrue($"POST {url}: {(int)resp.StatusCode} «{await resp.Content.ReadAsStringAsync()}»");
    }

    private static async Task AuxiliarAsync(HttpClient http, string token, string codigo, string nombre, string codigoPadre)
    {
        var busqueda = await NominaE2E.GetAsync(http, token, $"/api/accounting/accounts/search?q={codigoPadre}&onlyMovement=false");
        var padre = busqueda.EnumerateArray().FirstOrDefault(c => c.GetProperty("code").GetString() == codigoPadre);
        padre.ValueKind.Should().Be(JsonValueKind.Object, $"la subcuenta {codigoPadre} viene del catálogo PUC solidario");
        await CrearAsync(http, token, "/api/accounting/accounts", new
        {
            code = codigo, name = nombre, parentPublicId = padre.GetProperty("publicId").GetGuid(),
            enabledModules = new[] { "CNT", "NOM" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
        });
    }
}
