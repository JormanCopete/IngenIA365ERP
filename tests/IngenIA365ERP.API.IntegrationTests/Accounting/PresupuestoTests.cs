using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Accounting.ContabilidadE2E;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Feature 009 E2, US9 (T141): el presupuesto del ejercicio de punta a punta por HTTP —crear,
/// aprobar, versionar con motivo, distribuir, ejecutar contra el libro y copiar al año siguiente
/// con ajuste—. Es un solo recorrido porque el año tiene un solo presupuesto y cada paso depende
/// del anterior; los pasos negativos (cuenta de agrupación, sin motivo) van intercalados donde el
/// estado del presupuesto los hace posibles.
/// </summary>
[Collection(ContabilidadCollection.Nombre)]
public class PresupuestoTests(CentralIdentityApiFixture fx)
{
    private const int Anio = 2026;
    private const decimal AseoMensual = 100_000m;
    private const decimal CafeteriaMensual = 50_000m;

    private static decimal[] Mensual(decimal valor) => Enumerable.Repeat(valor, 12).ToArray();

    private static JsonElement LineaDe(JsonElement presupuesto, string cuenta)
    {
        var linea = presupuesto.GetProperty("lines").EnumerateArray().FirstOrDefault(l => l.GetProperty("accountCode").GetString() == cuenta);
        linea.ValueKind.Should().Be(JsonValueKind.Object, $"el presupuesto trae la cuenta {cuenta}");
        return linea;
    }

    private static decimal[] Montos(JsonElement linea) => linea.GetProperty("amounts").EnumerateArray().Select(a => a.GetDecimal()).ToArray();

    [Fact]
    public async Task El_presupuesto_se_crea_aprueba_versiona_distribuye_ejecuta_y_copia_al_ano_siguiente()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        var aseo = await CuentaAsync(http, admin, GastoAseo);
        var cafeteria = await CuentaAsync(http, admin, GastoCafeteria);
        var agrupacion = await CuentaAsync(http, admin, SubcuentaDeAgrupacion);

        // Sin presupuesto el año responde 200 con Status None (la pantalla ofrece «Crear» o «Copiar»).
        var vacio = await GetAsync(http, admin, $"/api/accounting/budgets?year={Anio}");
        vacio.GetProperty("status").GetString().Should().Be("None");
        vacio.GetProperty("lines").GetArrayLength().Should().Be(0);

        // Presupuestar una cuenta de agrupación es 422 con el código de la cuenta en data.
        var malo = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/budgets", new
        {
            year = Anio,
            lines = new[] { new { accountPublicId = agrupacion, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, amounts = Mensual(1_000m) } },
        });
        malo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await malo.Content.ReadAsStringAsync());
        var sobre = await LeerAsync(malo);
        sobre.GetProperty("code").GetString().Should().Be("Accounting.Budget.AccountNotMovement");
        sobre.GetProperty("data").GetProperty("accountCode").GetString().Should().Be(SubcuentaDeAgrupacion);
        (await GetAsync(http, admin, $"/api/accounting/budgets?year={Anio}")).GetProperty("status").GetString().Should().Be("None", "no se guardó nada");

        // Crear: dos cuentas de movimiento de gasto → 201, versión 1 en borrador.
        var alta = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/budgets", new
        {
            year = Anio,
            lines = new[]
            {
                new { accountPublicId = aseo, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, amounts = Mensual(AseoMensual) },
                new { accountPublicId = cafeteria, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, amounts = Mensual(CafeteriaMensual) },
            },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var creado = await LeerAsync(alta);
        creado.GetProperty("year").GetInt32().Should().Be(Anio);
        creado.GetProperty("version").GetInt32().Should().Be(1);
        creado.GetProperty("status").GetString().Should().Be("Draft");
        creado.GetProperty("lines").GetArrayLength().Should().Be(2);
        LineaDe(creado, GastoAseo).GetProperty("total").GetDecimal().Should().Be(AseoMensual * 12);

        // Crear otra vez es 422 AlreadyExists.
        var repetido = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/budgets", new { year = Anio, lines = Array.Empty<object>() });
        repetido.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(repetido)).Should().Be("Accounting.Budget.AlreadyExists");

        // Aprobar → Approved, con quién y cuándo.
        var aprobar = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio}/approve", null);
        aprobar.StatusCode.Should().Be(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());
        var aprobado = await LeerAsync(aprobar);
        aprobado.GetProperty("status").GetString().Should().Be("Approved");
        aprobado.GetProperty("approvedBy").GetString().Should().NotBeNullOrWhiteSpace();
        aprobado.GetProperty("approvedAt").ValueKind.Should().Be(JsonValueKind.String);

        // Aprobar lo aprobado es 422 NotDraft.
        var otraVez = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio}/approve", null);
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(otraVez)).Should().Be("Accounting.Budget.NotDraft");

        // Modificar un aprobado sin motivo → 422 ReasonRequired; nada cambia.
        var lineasV2 = new[]
        {
            new { accountPublicId = aseo, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, amounts = Mensual(AseoMensual + 20_000m) },
            new { accountPublicId = cafeteria, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, amounts = Mensual(CafeteriaMensual) },
        };
        var sinMotivo = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/accounting/budgets/{Anio}", new { lines = lineasV2, reason = (string?)null });
        sinMotivo.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await sinMotivo.Content.ReadAsStringAsync());
        (await CodigoDeErrorAsync(sinMotivo)).Should().Be("Accounting.Budget.ReasonRequired");
        (await GetAsync(http, admin, $"/api/accounting/budgets?year={Anio}")).GetProperty("version").GetInt32().Should().Be(1);

        // Con motivo → versión 2 aprobada; la 1 queda Superseded y GET ?version=1 sigue mostrando la inicial.
        var conMotivo = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/accounting/budgets/{Anio}", new { lines = lineasV2, reason = "Alza del contrato de aseo" });
        conMotivo.StatusCode.Should().Be(HttpStatusCode.OK, await conMotivo.Content.ReadAsStringAsync());
        var v2 = await LeerAsync(conMotivo);
        v2.GetProperty("version").GetInt32().Should().Be(2);
        v2.GetProperty("status").GetString().Should().Be("Approved");
        v2.GetProperty("changeReason").GetString().Should().Be("Alza del contrato de aseo");
        Montos(LineaDe(v2, GastoAseo)).Should().AllBeEquivalentTo(AseoMensual + 20_000m);
        v2.GetProperty("versions").EnumerateArray().Select(x => (x.GetProperty("version").GetInt32(), x.GetProperty("status").GetString()))
            .Should().BeEquivalentTo([(1, "Superseded"), (2, "Approved")]);

        var inicial = await GetAsync(http, admin, $"/api/accounting/budgets?year={Anio}&version=1");
        inicial.GetProperty("version").GetInt32().Should().Be(1);
        inicial.GetProperty("status").GetString().Should().Be("Superseded");
        Montos(LineaDe(inicial, GastoAseo)).Should().AllBeEquivalentTo(AseoMensual, "la versión inicial no se toca");

        // Distribuir 100 en partes iguales sobre cafetería: doce cuotas de 8 y el resto (12) en diciembre; sobre un aprobado versiona con motivo.
        var distribuir = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio}/distribute", new
        {
            accountPublicId = cafeteria, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null,
            total = 100m, mode = "equal", values = (decimal[]?)null, reason = "Cafetería se reparte en partes iguales",
        });
        distribuir.StatusCode.Should().Be(HttpStatusCode.OK, await distribuir.Content.ReadAsStringAsync());
        var v3 = await LeerAsync(distribuir);
        v3.GetProperty("version").GetInt32().Should().Be(3);
        var cuotas = Montos(LineaDe(v3, GastoCafeteria));
        cuotas.Should().HaveCount(12);
        cuotas.Take(11).Should().AllBeEquivalentTo(8m);
        cuotas[11].Should().Be(12m, "el resto va en diciembre");
        cuotas.Sum().Should().Be(100m);
        Montos(LineaDe(v3, GastoAseo)).Should().AllBeEquivalentTo(AseoMensual + 20_000m, "las otras cuentas siguen como estaban");

        // Con decimales o en modo desconocido no se distribuye.
        var conCentavos = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio}/distribute", new
        {
            accountPublicId = cafeteria, branchPublicId = (Guid?)null, costCenterPublicId = (Guid?)null, total = 100.5m, mode = "equal", values = (decimal[]?)null, reason = "x",
        });
        conCentavos.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(conCentavos)).Should().Be("Accounting.Budget.InvalidDistribution");

        // Ejecución: un gasto de 40.000 en aseo en septiembre → mes 40 %, acumulado 40.000 / (9 × 120.000).
        const decimal gasto = 40_000m;
        await ContabilizarAsync(http, admin, new DateOnly(Anio, 9, 8), "Compra de elementos de aseo", new object[]
        {
            Linea(GastoAseo, ctx.SucursalPrincipal, gasto, 0m),
            Linea(HonorariosPorPagar, ctx.SucursalPrincipal, 0m, gasto),
        });

        var ejecucion = await LeerTablaAsync(http, admin, $"/api/accounting/budgets/execution?year={Anio}&month=9");
        var filaAseo = ejecucion.PorCodigo(GastoAseo);
        filaAseo.Should().NotBeNull("la cuenta presupuestada y ejecutada aparece");
        var pptoMes = AseoMensual + 20_000m;
        ejecucion.Numero(filaAseo!.Value, "Ppto. mes").Should().Be(pptoMes, "contra la versión vigente");
        ejecucion.Numero(filaAseo.Value, "Ejec. mes").Should().Be(gasto);
        ejecucion.Numero(filaAseo.Value, "Var. mes").Should().Be(gasto - pptoMes);
        ejecucion.Numero(filaAseo.Value, "% mes").Should().Be(Math.Round(gasto / pptoMes * 100m, 2));
        ejecucion.Numero(filaAseo.Value, "Ppto. acum.").Should().Be(pptoMes * 9);
        ejecucion.Numero(filaAseo.Value, "Ejec. acum.").Should().Be(gasto);
        ejecucion.Numero(filaAseo.Value, "% acum.").Should().Be(Math.Round(gasto / (pptoMes * 9) * 100m, 2));
        ejecucion.Numero(filaAseo.Value, "Ppto. inicial acum.").Should().Be(AseoMensual * 9, "la columna inicial es la versión 1");
        var filaCafeteria = ejecucion.PorCodigo(GastoCafeteria);
        filaCafeteria.Should().NotBeNull();
        ejecucion.Numero(filaCafeteria!.Value, "Ppto. mes").Should().Be(8m);
        ejecucion.Numero(filaCafeteria.Value, "Ejec. mes").Should().Be(0m);
        ejecucion.Notas.Should().Contain(n => n.Contains("versión 3"));

        // La misma vista por el centro de informes, y los dos caminos dicen lo mismo.
        var porInformes = await LeerTablaAsync(http, admin, $"/api/reports/accounting/budget-execution?year={Anio}&month=9");
        porInformes.Numero(porInformes.PorCodigo(GastoAseo)!.Value, "Ejec. acum.").Should().Be(gasto);

        // Quien sólo lee ve la ejecución (Budget.View) pero no la exporta.
        var lectura = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"/api/accounting/budgets/execution?year={Anio}&month=9", null);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);
        var exportar = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"/api/accounting/budgets/execution?year={Anio}&month=9&format=xlsx", null);
        exportar.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var escribir = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"/api/accounting/budgets/{Anio}/approve", null);
        escribir.StatusCode.Should().Be(HttpStatusCode.NotFound, "Budget.Manage no lo tiene un ReadOnly");

        // Copiar al año siguiente con +5 %: el ejercicio tiene que existir; los valores se redondean a pesos.
        var sinEjercicio = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio + 1}/copy-from/{Anio}?adjustPercent=5", null);
        if (sinEjercicio.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            (await CodigoDeErrorAsync(sinEjercicio)).Should().Be("Accounting.Budget.FiscalYearNotFound");
            var abrir = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = Anio + 1 });
            abrir.StatusCode.Should().Be(HttpStatusCode.OK, $"abrir {Anio + 1}: «{await abrir.Content.ReadAsStringAsync()}»");
        }
        var copiar = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio + 1}/copy-from/{Anio}?adjustPercent=5", null);
        copiar.StatusCode.Should().Be(HttpStatusCode.OK, await copiar.Content.ReadAsStringAsync());
        var copia = await LeerAsync(copiar);
        copia.GetProperty("year").GetInt32().Should().Be(Anio + 1);
        copia.GetProperty("version").GetInt32().Should().Be(1);
        copia.GetProperty("status").GetString().Should().Be("Draft");
        Montos(LineaDe(copia, GastoAseo)).Should().AllBeEquivalentTo(Math.Round((AseoMensual + 20_000m) * 1.05m, 0, MidpointRounding.AwayFromZero));
        var cafeteriaCopiada = Montos(LineaDe(copia, GastoCafeteria));
        cafeteriaCopiada.Take(11).Should().AllBeEquivalentTo(8m, "8 × 1,05 = 8,4 → 8 pesos");
        cafeteriaCopiada[11].Should().Be(13m, "12 × 1,05 = 12,6 → 13 pesos");
        cafeteriaCopiada.Should().OnlyContain(v => v == decimal.Truncate(v), "todo en pesos, sin decimales");

        // Copiar de un año sin presupuesto es 422 SourceNotFound.
        var sinOrigen = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/budgets/{Anio + 1}/copy-from/{Anio - 1}", null);
        sinOrigen.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDeErrorAsync(sinOrigen)).Should().Be("Accounting.Budget.SourceNotFound");
    }

    private static async Task<Tabla> LeerTablaAsync(HttpClient http, string token, string url)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Get, url, null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}: «{await resp.Content.ReadAsStringAsync()}»");
        return new Tabla(await LeerAsync(resp));
    }
}
