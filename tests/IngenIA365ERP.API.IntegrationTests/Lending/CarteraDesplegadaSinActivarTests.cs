using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Feature 013, T0145 (T85; interfaces.md §12; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): con Cartera
/// desplegada y <b>sin activar</b> —hoy por los puertos provisionales y, sin cambiar la prueba, después por los reales sin activar—
/// nada de lo que ya funcionaba se detiene: la liquidación definitiva se aprueba sin descuentos de Cartera; la nómina ordinaria
/// calcula y aprueba sin descuentos ni filas de <c>PAY_PayrollLoanDeductions</c>; borrar a una persona sin nada funciona; una venta a
/// crédito confirma como crédito provisional, sin número reservado, emite <c>VentaACreditoRegistrada</c> v2 y queda sin intentos ni
/// alertas hacia <c>Lending</c> (el destino no está habilitado); y el detalle de la persona dice <c>NotActivated</c>.
///
/// <para>
/// US8 (bloque 13C, T0730; T85, FR-142): con una carga de migración <b>aplicada sin activar</b> en una cooperativa aislada con nómina y contabilidad,
/// la ordinaria y la definitiva con deudores de la carga exigen <c>acknowledgeLendingPendingActivation</c>
/// (<c>Payroll.LendingPendingActivationAckRequired</c>) y aprueban con él; Personas no deja borrar al deudor ni al codeudor
/// (<c>Person.HasActiveLoans</c>) y el detalle dice «migrada, sin activar» mientras el contrato sigue «Cartera no activada»; y Contabilidad, que
/// recibió <c>CargaDeCarteraAplicada</c>, avisa <c>Accounting.Period.LendingLoadWillExpire</c> al cerrar el mes que contiene C+1.
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CarteraDesplegadaSinActivarTests(CentralIdentityApiFixture fx)
{
    private static int _sufijo = 500;

    [Fact]
    public async Task La_persona_sin_nada_se_borra_y_su_detalle_dice_Cartera_sin_activar()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "sinactivar");
        using var http = fx.CreateClient();
        var documento = (920_000_000 + Interlocked.Increment(ref _sufijo)).ToString();
        var alta = await CarteraE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = "Sofía", lastName = "Sinactivar", email = $"sofia.{documento}@coop.cartera.test",
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var persona = (await CarteraE2E.LeerAsync(alta)).GetGuid();

        var detalle = await CarteraE2E.GetAsync(http, coop.TokenAdmin, $"/api/core/people/{persona}/detail");
        detalle.GetProperty("portfolioSummary").GetProperty("lendingStatus").GetString().Should().Be("NotActivated");

        var borrar = await CarteraE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Delete, $"/api/core/people/{persona}", null);
        borrar.IsSuccessStatusCode.Should().BeTrue($"una persona sin obligaciones se borra aunque Cartera no esté activada: «{await borrar.Content.ReadAsStringAsync()}»");
    }

    [Fact]
    public async Task La_nomina_ordinaria_calcula_y_aprueba_sin_descuentos_de_Cartera()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();

        var (_, _, runId, _) = await NominaE2E.CicloAprobadoAsync(http, ctx.TokenAdmin, 10, "Sinactivar");

        var descuentos = await NominaE2E.GetAsync(http, ctx.TokenAdmin, $"/api/payroll/runs/{runId}/loan-deductions");
        descuentos.ValueKind.Should().Be(JsonValueKind.Array);
        descuentos.GetArrayLength().Should().Be(0, "sin Cartera activada no hay filas de PAY_PayrollLoanDeductions");
        var corrida = await NominaE2E.GetAsync(http, ctx.TokenAdmin, $"/api/payroll/runs/{runId}");
        corrida.GetProperty("status").GetString().Should().Be("Approved");
    }

    [Fact]
    public async Task La_liquidacion_definitiva_se_aprueba_sin_descuentos_de_Cartera()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        var sufijo = Interlocked.Increment(ref _sufijo);

        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = $"SA{sufijo}", name = $"Sin activar {sufijo}", periodicity = "Biweekly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();
        var documento = (930_000_000 + sufijo).ToString();
        var persona = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = "Rosa", lastName = "Definitiva", email = $"rosa.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, await persona.Content.ReadAsStringAsync());
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var eps = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/health-insurance", new { code = $"ES{sufijo}", name = $"EPS sin activar {sufijo}", taxId = $"8011{sufijo:00000}", checkDigit = 1 });
        var afp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pension-providers", new { code = $"AS{sufijo}", name = $"Fondo sin activar {sufijo}", taxId = $"8012{sufijo:00000}", checkDigit = 2 });
        var empleado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = 2_000_000m, contractType = 1, hireDate = new DateTime(2025, 1, 2), payrollPlanPublicId = planId,
            healthInsurancePublicId = (await NominaE2E.LeerAsync(eps)).GetGuid(), pensionProviderPublicId = (await NominaE2E.LeerAsync(afp)).GetGuid(),
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, await empleado.Content.ReadAsStringAsync());
        var empleadoId = (await NominaE2E.LeerAsync(empleado)).GetGuid();
        // La quincena que contiene hoy y el retiro hoy: la regla de Nómina no admite un retiro con fecha futura
        // (Payroll.Termination.DateInFuture), así que la prueba no puede fijar fechas del calendario.
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioQuincena = new DateOnly(hoy.Year, hoy.Month, hoy.Day <= 15 ? 1 : 16);
        var finQuincena = hoy.Day <= 15 ? new DateOnly(hoy.Year, hoy.Month, 15) : new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = planId, startDate = inicioQuincena.ToDateTime(TimeOnly.MinValue), endDate = finQuincena.ToDateTime(TimeOnly.MinValue), description = "Quincena sin activar", statusMessage = string.Empty,
        });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());

        var registro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/settlements/terminations", new
        {
            employeePublicId = empleadoId, terminationDate = hoy, reasonCode = "RENUNCIA", contractType = "Indefinite",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var runId = (await NominaE2E.LeerAsync(registro)).GetProperty("runPublicId").GetGuid();

        var descuentos = await NominaE2E.GetAsync(http, admin, $"/api/payroll/settlements/terminations/{runId}/deductions");
        descuentos.GetProperty("items").GetArrayLength().Should().Be(0, "la consulta de obligaciones responde «Cartera no activada» sin obligaciones");

        var aprobar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/settlements/terminations/{runId}/approve",
            new { confirm = true, confirmWithoutSegregation = true });
        aprobar.StatusCode.Should().Be(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());
        (await NominaE2E.LeerAsync(aprobar)).GetProperty("portfolioPayments").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task La_venta_a_credito_queda_provisional_sin_reserva_y_su_v2_sin_intentos_hacia_Cartera()
    {
        var esc = await ContabilidadDeVentasE2E.PrepararAsync(fx, "sinactivar");
        using var http = fx.CreateClient();
        var cajero = esc.Cajero1.Token;
        var sesion = await EscenarioDeVentas.SesionAsync(http, cajero, esc.Caja1);
        var asociado = await ContabilidadDeVentasE2E.AsociadoAsync(http, esc.Admin, "Samuel", "72000001");

        var evaluacion = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Post, "/api/inventory/sales/credit-evaluations",
            new { personPublicId = asociado, paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 100_000m });
        evaluacion.GetProperty("lendingEnabled").GetBoolean().Should().BeFalse("el destino de Cartera no está habilitado: crédito provisional");

        var venta = await EscenarioDeVentas.NuevaVentaAsync(http, cajero, sesion, esc.Inv.P("P7").CodigoDeBarras);
        var id = EscenarioDeVentas.VentaId(venta);
        await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{EscenarioDeVentas.Borradores}/{id}", new { customerPersonPublicId = asociado });
        var cobro = await EscenarioDeVentas.PedirCobrarAsync(http, cajero, id, 100_000m,
            [new { paymentMeansPublicId = esc.Medio("CREDASOC"), amount = 100_000m, credit = new { installments = 2, termDays = 60, periodicityDays = 30 } }]);
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        var cobrada = await InventarioE2E.LeerAsync(cobro);
        if (cobrada.GetProperty("status").GetInt32() == 1)
            await EscenarioDeVentas.AprobadaAsync(await InventarioE2E.DecidirAsync(http, esc.Admin, esc.Supervisor.Token,
                cobrada.GetProperty("approvalRequestPublicId").GetGuid(), aprobar: true));

        var credito = await InventarioE2E.GetAsync(http, cajero, $"/api/inventory/sales/documents/{id}/credit");
        var pago = credito.GetProperty("payments").EnumerateArray().Single();
        pago.GetProperty("pendingValidation").GetBoolean().Should().BeTrue("crédito provisional");
        (pago.TryGetProperty("reservedLoanNumber", out var reservado) ? reservado.ValueKind : JsonValueKind.Null)
            .Should().Be(JsonValueKind.Null, "sin Cartera habilitada no se reserva número");

        var mensajes = (await ContabilidadDeInventarioE2E.MensajesAsync(http, esc.Admin, id))
            .Where(m => m.GetProperty("type").GetString() == "VentaACreditoRegistrada").ToList();
        mensajes.Should().ContainSingle();
        mensajes[0].GetProperty("version").GetInt32().Should().Be(2, "Comercial emite la v2 desde la enmienda E-02 (T18)");
        ContabilidadDeInventarioE2E.Estado(mensajes[0]).Should().Be(0, "Pending: el destino Lending no está habilitado");

        await fx.Despachador.PasadasAsync(esc.Coop.TenantPublicId, 2);
        var intentos = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_IntegrationDeliveryAttempts" a JOIN dbo."COR_IntegrationMessageDeliveries" d ON d."Id" = a."DeliveryId" WHERE d."Destination" = 'Lending'""",
            "SELECT COUNT(*) FROM [dbo].[COR_IntegrationDeliveryAttempts] a JOIN [dbo].[COR_IntegrationMessageDeliveries] d ON d.[Id] = a.[DeliveryId] WHERE d.[Destination] = 'Lending'");
        intentos.Should().Be(0, "el despachador no intenta entregar a un destino deshabilitado");
        var alertas = await ContabilidadDeInventarioE2E.EnteroAsync(fx, esc.Inv,
            """SELECT COUNT(*) FROM dbo."COR_Alerts" WHERE "TypeCode" LIKE 'Integracion.%'""",
            "SELECT COUNT(*) FROM [dbo].[COR_Alerts] WHERE [TypeCode] LIKE 'Integracion.%'");
        alertas.Should().Be(0);
    }

    [Fact]
    public async Task Con_una_carga_aplicada_sin_activar_Nomina_Personas_y_Contabilidad_lo_reconocen()
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "carga_sin_activar", hoy.Year);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var sufijo = Interlocked.Increment(ref _sufijo);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);

        // ------------------------------------------------------------------------- nómina mínima de la cooperativa --
        await NominaMinimaAsync(http, admin);
        var eps = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/health-insurance", new { code = $"EM{sufijo}", name = $"EPS carga {sufijo}", taxId = $"8013{sufijo:00000}", checkDigit = 1 });
        var afp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pension-providers", new { code = $"AM{sufijo}", name = $"Fondo carga {sufijo}", taxId = $"8014{sufijo:00000}", checkDigit = 2 });
        var epsId = (await NominaE2E.LeerAsync(eps)).GetGuid();
        var afpId = (await NominaE2E.LeerAsync(afp)).GetGuid();
        var planOrdinario = await PlanAsync(http, admin, $"MO{sufijo}", "Monthly");
        var planDefinitiva = await PlanAsync(http, admin, $"MD{sufijo}", "Biweekly");
        var (_, docOrdinario) = await EmpleadoAsync(http, admin, "Orlando", planOrdinario, epsId, afpId);
        var (empleadoDefinitiva, docDefinitiva) = await EmpleadoAsync(http, admin, "Delia", planDefinitiva, epsId, afpId);
        var (docBorrable, borrable) = await MigracionE2E.PersonaAsync(http, admin, "Bruno");
        var (docCodeudor, codeudor) = await MigracionE2E.PersonaAsync(http, admin, "Camila");

        // ------------------------------------------------------------------------------ la carga aplicada, sin activar --
        await MigracionE2E.LineaAsync(fx, coop, hoy);
        var oficina = await MigracionE2E.OficinaAsync(http, admin, coop.TenantPublicId, $"MS{sufijo}");
        var carga = await MigracionE2E.CrearCargaAsync(http, admin, hoy, "Ensayo en producción sin activar (e2e)");
        var creditos = new[] { ("SA-0001", docOrdinario), ("SA-0002", docDefinitiva), ("SA-0003", docBorrable) };
        await MigracionE2E.CargarAsync(http, admin, carga, P.Loans,
            creditos.Select(c => MigracionE2E.Credito(c.Item1, c.Item2, oficina, hoy.AddDays(-60), 1_200_000m, 12)));
        await MigracionE2E.CargarAsync(http, admin, carga, P.Installments, creditos.SelectMany(c => MigracionE2E.Cuotas(c.Item1, hoy, 10, 100_000m)));
        await MigracionE2E.CargarAsync(http, admin, carga, P.Balances, creditos.Select(c => MigracionE2E.Saldo(c.Item1, "Principal", 1_000_000m)));
        await MigracionE2E.CargarAsync(http, admin, carga, P.RiskData, creditos.Select(c => MigracionE2E.Riesgo(c.Item1)));
        await MigracionE2E.CargarAsync(http, admin, carga, P.CoDebtors, [MigracionE2E.Fila((C.NumeroDeCredito, "SA-0003"), (C.Codeudor, docCodeudor))]);
        var aplicar = await MigracionE2E.AplicarAsync(http, admin, carga, "Carga de ensayo sin activar");
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());

        // ------------------------------------------------------------------------------------------------- Personas --
        foreach (var persona in new[] { borrable, codeudor })
        {
            var borrar = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Delete, $"/api/core/people/{persona}", null);
            borrar.IsSuccessStatusCode.Should().BeFalse(await borrar.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(borrar)).Should().Be("Person.HasActiveLoans", "un deudor o codeudor de la carga aplicada no se borra");
        }
        var detalle = await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{borrable}/detail");
        detalle.GetProperty("portfolioSummary").GetProperty("lendingStatus").GetString()
            .Should().Be("MigratedNotActivated", "el contrato sigue «Cartera no activada» y la persona es deudora de la carga");

        // ------------------------------------------------------------------------------------------ Nómina ordinaria --
        var inicioDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = planOrdinario, startDate = inicioDelMes.ToDateTime(TimeOnly.MinValue), endDate = inicioDelMes.AddMonths(1).AddDays(-1).ToDateTime(TimeOnly.MinValue),
            description = "Mes con carga sin activar", statusMessage = string.Empty,
        });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
        var corrida = (await NominaE2E.CalcularAsync(http, admin, (await NominaE2E.LeerAsync(periodo)).GetGuid())).GetProperty("runPublicId").GetGuid();
        var excepciones = await NominaE2E.ExcepcionesParaAsync(http, admin, corrida);
        var sinReconocer = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{corrida}/approve", new
        {
            confirm = true, exceptions = excepciones, confirmEmpty = false, confirmWithoutSegregation = true,
        });
        sinReconocer.IsSuccessStatusCode.Should().BeFalse(await sinReconocer.Content.ReadAsStringAsync());
        (await NominaE2E.CodigoDeErrorAsync(sinReconocer)).Should().Be("Payroll.LendingPendingActivationAckRequired");
        var reconocida = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{corrida}/approve", new
        {
            confirm = true, exceptions = excepciones, confirmEmpty = false, confirmWithoutSegregation = true, acknowledgeLendingPendingActivation = true,
        });
        reconocida.StatusCode.Should().Be(HttpStatusCode.OK, await reconocida.Content.ReadAsStringAsync());

        // ---------------------------------------------------------------------------------------- Nómina definitiva --
        var inicioQuincena = new DateOnly(hoy.Year, hoy.Month, hoy.Day <= 15 ? 1 : 16);
        var finQuincena = hoy.Day <= 15 ? new DateOnly(hoy.Year, hoy.Month, 15) : new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        var quincena = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            planPublicId = planDefinitiva, startDate = inicioQuincena.ToDateTime(TimeOnly.MinValue), endDate = finQuincena.ToDateTime(TimeOnly.MinValue),
            description = "Quincena con carga sin activar", statusMessage = string.Empty,
        });
        quincena.StatusCode.Should().Be(HttpStatusCode.Created, await quincena.Content.ReadAsStringAsync());
        var registro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/settlements/terminations", new
        {
            employeePublicId = empleadoDefinitiva, terminationDate = hoy, reasonCode = "RENUNCIA", contractType = "Indefinite",
        });
        registro.StatusCode.Should().Be(HttpStatusCode.Created, await registro.Content.ReadAsStringAsync());
        var definitiva = (await NominaE2E.LeerAsync(registro)).GetProperty("runPublicId").GetGuid();
        var definitivaSinReconocer = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/settlements/terminations/{definitiva}/approve",
            new { confirm = true, confirmWithoutSegregation = true });
        definitivaSinReconocer.IsSuccessStatusCode.Should().BeFalse(await definitivaSinReconocer.Content.ReadAsStringAsync());
        (await NominaE2E.CodigoDeErrorAsync(definitivaSinReconocer)).Should().Be("Payroll.LendingPendingActivationAckRequired");
        var definitivaReconocida = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/settlements/terminations/{definitiva}/approve",
            new { confirm = true, confirmWithoutSegregation = true, acknowledgeLendingPendingActivation = true });
        definitivaReconocida.StatusCode.Should().Be(HttpStatusCode.OK, await definitivaReconocida.Content.ReadAsStringAsync());

        // -------------------------------------------------------------------------------------------- Contabilidad --
        await CarteraE2E.DespacharAsync(fx, coop.TenantPublicId, 3);
        var mesDeC1 = hoy.AddDays(1);
        var cierre = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/periods/{mesDeC1.Year}/{mesDeC1.Month}/close",
            new { acknowledgeLendingPending = true, acknowledgeInventoryPending = true });
        cierre.IsSuccessStatusCode.Should().BeFalse(await cierre.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(cierre)).Should().Be("Accounting.Period.LendingLoadWillExpire",
            "Contabilidad recibió CargaDeCarteraAplicada y el mes contiene el día siguiente al corte");
    }

    /// <summary>Lo que la nómina necesita en una cooperativa aislada: centro de costo, dos auxiliares, cuentas por concepto y que quien calcula apruebe.</summary>
    private static async Task NominaMinimaAsync(HttpClient http, string admin)
    {
        await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/cost-centers", new { name = "Administración", payrollType = 1, period = 1, payrollPeriodicity = 30 });
        string[] modulos = ["CNT", "NOM"];
        await ContabilidadE2E.CrearCuentaAsync(http, admin, NominaE2E.CuentaDebito, "Sueldos y salarios", "510503", modulos);
        await ContabilidadE2E.CrearCuentaAsync(http, admin, NominaE2E.CuentaCredito, "Salarios por pagar", "271005", modulos);
        foreach (var c in (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions")).EnumerateArray())
        {
            var resp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/concept-definitions/{c.GetProperty("code").GetString()}/accounts",
                new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = NominaE2E.CuentaDebito, creditAccountCode = NominaE2E.CuentaCredito } } });
            resp.IsSuccessStatusCode.Should().BeTrue($"cuentas de {c.GetProperty("code").GetString()}: «{await resp.Content.ReadAsStringAsync()}»");
        }
        var vigencia = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/company-policies/AllowSameUserApproval/versions", new
        {
            value = "true", validFrom = new DateOnly(2000, 1, 1), reason = "Pruebas e2e: quien calcula aprueba con segunda confirmación", closePrevious = true,
        });
        vigencia.IsSuccessStatusCode.Should().BeTrue($"vigencia de AllowSameUserApproval: «{await vigencia.Content.ReadAsStringAsync()}»");
    }

    private static async Task<Guid> PlanAsync(HttpClient http, string admin, string codigo, string periodicidad)
    {
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = codigo, name = $"Plan {codigo}", periodicity = periodicidad });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        return (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();
    }

    /// <summary>Una persona empleada con su ficha en el plan, EPS y fondo; devuelve la ficha y el documento (el deudor de la carga).</summary>
    private static async Task<(Guid Empleado, string Documento)> EmpleadoAsync(HttpClient http, string admin, string nombre, Guid plan, Guid eps, Guid afp)
    {
        var documento = MigracionE2E.Documento();
        var persona = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Carga", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, await persona.Content.ReadAsStringAsync());
        var empleado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = (await NominaE2E.LeerAsync(persona)).GetGuid(), baseSalary = 2_000_000m, contractType = 1, hireDate = new DateTime(2025, 1, 2),
            payrollPlanPublicId = plan, healthInsurancePublicId = eps, pensionProviderPublicId = afp,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, await empleado.Content.ReadAsStringAsync());
        return ((await NominaE2E.LeerAsync(empleado)).GetGuid(), documento);
    }
}
