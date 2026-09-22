using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// T084 — US4 (feature 009, FR-036..FR-041) por HTTP en la cooperativa de nómina: aprobar una
/// nómina deja un <c>NM</c> contabilizado por el contrato, con el empleado como tercero, que
/// Contabilidad muestra por <c>by-source</c> y no reversa (<c>ModuleOwned</c>); una cuenta de
/// agrupación no se parametriza en un concepto; reversar desde Nómina deja el neto en cero; y una
/// nómina cuyo corte cae en un mes cerrado no se aprueba (<c>Accounting.Period.Closed</c>).
/// Usa junio y julio de 2027, meses que ninguna otra e2e de nómina toca.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class ContabilidadDeNominaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_nomina_aprobada_queda_en_el_libro_por_el_contrato_y_solo_nomina_la_reversa()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        await Accounting.ContabilidadE2E.AbrirEjercicioAsync(http, admin, 2027);

        // (a) aprobar junio de 2027 → NM contabilizado, fechado al fin del período, cuadrado y con el empleado como tercero
        var (periodoId, empleadoId, runId, _) = await CicloAprobado2027Async(http, admin, 6, "Contable");
        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/by-source?module=NOM&sourcePublicId={runId}");
        comprobante.GetProperty("voucherTypeCode").GetString().Should().Be("NM");
        comprobante.GetProperty("status").GetString().Should().Be("Posted");
        comprobante.GetProperty("date").GetString().Should().Be("2027-06-30");
        comprobante.GetProperty("origin").GetProperty("module").GetString().Should().Be("NOM");
        comprobante.GetProperty("origin").GetProperty("sourcePublicId").GetGuid().Should().Be(runId);
        comprobante.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal()).And.BeGreaterThan(0m);
        var lineas = comprobante.GetProperty("lines").EnumerateArray().ToList();
        lineas.Should().Contain(l => l.GetProperty("personPublicId").ValueKind == JsonValueKind.String, "los devengos y deducciones llevan al empleado como tercero");
        var documentoId = comprobante.GetProperty("publicId").GetGuid();

        // (b) Contabilidad lo muestra pero no lo reversa: 422 ModuleOwned con el origen
        var desdeContabilidad = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{documentoId}/reverse", new { reason = "desde contabilidad" });
        desdeContabilidad.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var rechazo = await NominaE2E.LeerAsync(desdeContabilidad);
        rechazo.GetProperty("code").GetString().Should().Be("Accounting.Document.ModuleOwned");
        rechazo.GetProperty("data").GetProperty("origin").GetProperty("module").GetString().Should().Be("NOM");
        (await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{documentoId}")).GetProperty("status").GetString().Should().Be("Posted");

        // (c) una cuenta de agrupación no se parametriza en un concepto: el contrato la rechaza antes de que haya una nómina huérfana
        var agrupacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, "/api/payroll/concept-definitions/SALARIO/accounts",
            new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = "510503", creditAccountCode = NominaE2E.CuentaCredito } } });
        agrupacion.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await agrupacion.Content.ReadAsStringAsync()}»");
        (await NominaE2E.CodigoDeErrorAsync(agrupacion)).Should().Be("Accounting.Account.NotEligible");
        var vigente = await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/SALARIO/accounts");
        vigente.ToString().Should().Contain(NominaE2E.CuentaDebito, "la parametrización buena sigue intacta");

        // (d) reversar desde Nómina: el NM queda Reversed, la reversión existe y el neto del mes es cero
        var reversa = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/reverse", new { reason = "Faltó una novedad" });
        reversa.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar desde nómina: «{await reversa.Content.ReadAsStringAsync()}»");
        var reversado = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{documentoId}");
        reversado.GetProperty("status").GetString().Should().Be("Reversed");
        var reversionId = reversado.GetProperty("reversal").GetProperty("reversedByPublicId").GetGuid();
        var reversion = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{reversionId}");
        reversion.GetProperty("kind").GetString().Should().Be("Reversal");
        reversion.GetProperty("origin").GetProperty("module").GetString().Should().Be("NOM");
        // El espejo se fecha el día de la reversión (hoy), no en junio: lo que netea a cero es cuenta por cuenta, lado contra lado.
        reversion.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal());
        decimal Suma(JsonElement doc, string cuenta, string lado) => doc.GetProperty("lines").EnumerateArray()
            .Where(l => l.GetProperty("accountCode").GetString() == cuenta).Sum(l => l.GetProperty(lado).GetDecimal());
        Suma(reversion, NominaE2E.CuentaDebito, "credit").Should().Be(Suma(comprobante, NominaE2E.CuentaDebito, "debit")).And.BeGreaterThan(0m);
        Suma(reversion, NominaE2E.CuentaCredito, "debit").Should().Be(Suma(comprobante, NominaE2E.CuentaCredito, "credit"));
        periodoId.Should().NotBeEmpty();
        empleadoId.Should().NotBeEmpty();

        // (e) una nómina con corte en un mes cerrado no se aprueba: se cierra julio de 2027, se intenta, se reabre
        await Accounting.ContabilidadE2E.CerrarMesAsync(http, admin, 2027, 7);
        try
        {
            var (empleadoJulio, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Cerrado", 2_500_000m, new DateTime(2025, 1, 15));
            var julio = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2027, 7, 1), new DateTime(2027, 7, 31));
            await NominaE2E.RegistrarNovedadAsync(http, admin, julio, new { employeePublicId = empleadoJulio, conceptCode = "HEX_NOCTURNA", quantity = 2 });
            var calculo = await NominaE2E.CalcularAsync(http, admin, julio);
            var runJulio = calculo.GetProperty("runPublicId").GetGuid();
            var aprobacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runJulio}/approve", new
            {
                confirm = true, exceptions = await NominaE2E.ExcepcionesParaAsync(http, admin, runJulio), confirmEmpty = false, confirmWithoutSegregation = true,
            });
            aprobacion.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, $"«{await aprobacion.Content.ReadAsStringAsync()}»");
            (await NominaE2E.CodigoDeErrorAsync(aprobacion)).Should().Be("Accounting.Period.Closed");
            var enContabilidad = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/accounting/documents/by-source?module=NOM&sourcePublicId={runJulio}", null);
            enContabilidad.StatusCode.Should().Be(HttpStatusCode.NotFound, "nada entró al libro");
        }
        finally
        {
            await Accounting.ContabilidadE2E.ReabrirMesAsync(http, admin, 2027, 7, "Prueba e2e de nómina en mes cerrado");
        }
    }

    /// <summary>Como <see cref="NominaE2E.CicloAprobadoAsync"/>, pero en 2027: un período mensual con un empleado y una novedad, calculado y aprobado.</summary>
    private static async Task<(Guid PeriodoId, Guid EmpleadoId, Guid RunId, string Documento)> CicloAprobado2027Async(HttpClient http, string token, int mes, string nombre)
    {
        var (empleadoId, documento) = await NominaE2E.CrearEmpleadoAsync(http, token, nombre, 2_500_000m, new DateTime(2025, 1, 15));
        var desde = new DateTime(2027, mes, 1);
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, token, desde, desde.AddMonths(1).AddDays(-1));
        await NominaE2E.RegistrarNovedadAsync(http, token, periodoId, new { employeePublicId = empleadoId, conceptCode = "HEX_NOCTURNA", quantity = 4 });
        var calculo = await NominaE2E.CalcularAsync(http, token, periodoId);
        var runId = calculo.GetProperty("runPublicId").GetGuid();
        await NominaE2E.AprobarAsync(http, token, runId);
        return (periodoId, empleadoId, runId, documento);
    }
}
