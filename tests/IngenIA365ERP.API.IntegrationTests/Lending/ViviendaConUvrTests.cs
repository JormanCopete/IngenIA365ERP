using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Housing;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T1047 (feature 013, K6b; FR-024, D18; quickstart §9; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): vivienda con la Ley 546 y
/// UVR por HTTP con tokens reales, en una cooperativa aislada propia («vivienda»), con el procesador apagado y el ciclo diario corrido a mano.
/// <para>
/// <b>Valores de prueba.</b> La serie <c>UVR</c> y el <c>TOPE_TASA_VIVIENDA</c> se registran en la central <b>de este host</b> (uno por host, C107;
/// §7.6 de las transversales): son <b>sintéticos</b>, con la fuente «serie de prueba (e2e), no publicada», y no se parecen a los del Banco de la
/// República más que en la forma (cuatro decimales, un valor por día). Nunca se registran en DEV ni QA. Sólo los créditos en UVR leen la UVR y sólo
/// los de vivienda leen el tope, así que ninguna otra prueba del host los ve. Los cinco límites de vivienda los trae la semilla central.
/// </para>
/// <list type="bullet">
/// <item><see cref="La_guarda_de_entrega_deja_vivienda_fuera_antes_de_K6b"/>: antes de K6b, vivienda no se origina ni se migra (la guarda que
/// leen la línea, la solicitud y la migración); por HTTP no hay forma de bajar la entrega desplegada, así que se fija la guarda misma y el
/// comportamiento con K6b.</item>
/// <item><see cref="Una_linea_de_vivienda_en_UVR_se_origina_desembolsa_y_causa_a_la_UVR_del_dia"/>: la línea rechaza tasa variable y tasa sobre el
/// tope con su fuente; muestra el tope y los límites; la simulación trae el plan en UVR y sella <c>LND-2026.2</c>; la solicitud dice si es VIS; sin
/// hipoteca de primer grado no se desembolsa; con ella sí, y treinta días de causación llevan el capital en pesos a la UVR de cada día con la
/// corrección monetaria en el libro y su hecho <c>CorreccionMonetariaUvrCausada</c>; la liquidación y el estado de cuenta traen la UVR del día y los
/// saldos en UVR (proyectada a una fecha sin UVR publicada); un día sin UVR frena sólo al crédito en UVR.</item>
/// </list>
/// La migración de un crédito de vivienda al cuadre y las subcategorías E1/E2 por tiempo en E las fijan Application
/// (<c>ViviendaEnLaMigracionTests</c>) y Domain (<c>r21</c>); la mora sobre intereses, el caso dorado 64. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class ViviendaConUvrTests(CentralIdentityApiFixture fx)
{
    /// <summary>La fuente con que se registran los valores sintéticos: dice que son de prueba.</summary>
    private const string FuenteDePrueba = "Serie de prueba (e2e), no publicada: valores sintéticos de la suite de integración";

    /// <summary>El tope de tasa de vivienda de prueba (EA).</summary>
    private const decimal TopeDePrueba = 0.12m;

    /// <summary>La tasa fija de la línea de vivienda (bajo el tope).</summary>
    private const decimal TasaDeVivienda = 0.10m;

    /// <summary>La UVR sintética del día anterior al primer día de la serie, y su factor diario.</summary>
    private const decimal UvrBase = 405.1234m;

    private const decimal FactorDiario = 1.0002m;

    /// <summary>Hasta cuántos días después de hoy hay UVR registrada (el día siguiente queda sin UVR a propósito).</summary>
    private const int DiasConUvr = 30;

    [Fact]
    public void La_guarda_de_entrega_deja_vivienda_fuera_antes_de_K6b()
    {
        LectorDeVivienda.Disponible(EntregaDeCartera.K6a).Should().BeFalse("antes de K6b la línea, la solicitud y la migración rechazan vivienda (D18)");
        LectorDeVivienda.Disponible(EntregaDeCartera.K6b).Should().BeTrue();
        ((int)CatalogoDeCartera.EntregaVigente).Should().BeGreaterThanOrEqualTo((int)EntregaDeCartera.K6b, "esta prueba recorre vivienda por HTTP con la entrega desplegada");
    }

    [Fact]
    public async Task Una_linea_de_vivienda_en_UVR_se_origina_desembolsa_y_causa_a_la_UVR_del_dia()
    {
        var esc = await EscenarioAsync(fx, "vivienda");
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var jefe = esc.Usuarios.Jefe.Token;
        var asesor = esc.Usuarios.AsesorO1.Token;
        var h = esc.Hoy;

        // (0) La central de este host: el tope y la UVR sintéticos, de ayer a hoy + 30 (el día 31 queda sin UVR), y su réplica.
        var serie = await RegistrarValoresDePruebaAsync(http, h);
        await ValoresNacionalesE2E.ReplicarAsync(fx, tenant);

        // (1) La línea de vivienda: tasa variable → rechazo; tasa sobre el tope → rechazo con el tope y su fuente; fija bajo el tope en UVR → 201.
        var variable = LineasE2E.VersionVariable(h);
        variable["modalityCode"] = "VIVIENDA";
        variable["subsegmentCode"] = null;
        variable["terms"] = new { minMonths = 60, maxMonths = 360 };
        await InventarioE2E.FallaAsync(await LineasE2E.CrearAsync(http, admin, "VIVVAR", "Vivienda variable", variable), "Lending.Housing.VariableRateNotAllowed");

        using (var sobreElTope = await LineasE2E.CrearAsync(http, admin, "VIVALTA", "Vivienda sobre el tope", VersionDeVivienda(h, TopeDePrueba + 0.01m, enUvr: false)))
        {
            sobreElTope.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await CarteraE2E.CodigoDeErrorAsync(sobreElTope)).Should().Be("Lending.Housing.RateCapExceeded");
        }

        var lineaUvr = await LineasE2E.CrearExitoAsync(http, admin, "VIVUVR", "Vivienda en UVR", VersionDeVivienda(h, TasaDeVivienda, enUvr: true));
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, lineaUvr };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        var linea = await CarteraE2E.GetAsync(http, admin, $"{LineasE2E.Lineas}/{lineaUvr}");
        linea.GetProperty("currentVersion").GetProperty("denominationUnit").GetInt32().Should().Be((int)DenominationUnit.Uvr);
        var vivienda = linea.GetProperty("housing");
        vivienda.GetProperty("rateCap").GetProperty("rate").GetDecimal().Should().Be(TopeDePrueba);
        vivienda.GetProperty("rateCap").GetProperty("source").GetString().Should().Be(FuenteDePrueba);
        vivienda.GetProperty("limits").GetArrayLength().Should().Be(5, "plazo mínimo y máximo, financiable general y VIS, cuota-ingreso");
        vivienda.GetProperty("missing").GetArrayLength().Should().Be(0);

        // (2) La simulación: el plan en UVR a la UVR publicada del desembolso, con su leyenda, y la versión de reglas de vivienda.
        const decimal monto = 5_000_000m;
        var simulacion = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, $"{OriginacionE2E.Simulaciones}/schedule", new
        {
            creditLinePublicId = lineaUvr, amount = monto, installments = 120, periodicity = "Monthly", amortizationSystem = "LevelPayment",
        });
        simulacion.GetProperty("rulesVersion").GetString().Should().Be(VersionesDeReglas.Vivienda);
        simulacion.GetProperty("isHousing").GetBoolean().Should().BeTrue();
        var planEnUvr = simulacion.GetProperty("uvr");
        planEnUvr.GetProperty("uvrValue").GetDecimal().Should().Be(serie[h]);
        planEnUvr.GetProperty("amountUvr").GetDecimal().Should().Be(Math.Round(monto / serie[h], 4, MidpointRounding.AwayFromZero));
        planEnUvr.GetProperty("installments").GetArrayLength().Should().Be(120);
        planEnUvr.GetProperty("legend").GetString().Should().Contain("UVR");

        // (3) Dos solicitudes: A con hipoteca de segundo grado (no se desembolsa) y B con la de primer grado (se desembolsa). Las dos no VIS.
        var (personaA, _) = await DeudorAsync(http, admin, "ViviendaA");
        var (personaB, _) = await DeudorAsync(http, admin, "ViviendaB");
        var solicitudA = await AprobadaDeViviendaAsync(http, esc, personaA, lineaUvr, monto, gradoDeLaHipoteca: 2);
        var solicitudB = await AprobadaDeViviendaAsync(http, esc, personaB, lineaUvr, monto, gradoDeLaHipoteca: 1);
        (await CarteraE2E.GetAsync(http, jefe, $"{OriginacionE2E.Solicitudes}/{solicitudB}")).GetProperty("isSocialHousing").GetBoolean().Should().BeFalse();

        using (var sinPrimerGrado = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Desembolsos, Desembolso(esc, solicitudA, personaA)))
        {
            sinPrimerGrado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "la Ley 546 exige hipoteca de primer grado");
            (await CarteraE2E.CodigoDeErrorAsync(sinPrimerGrado)).Should().Be("Lending.Housing.FirstRankMortgageRequired");
        }
        var desembolso = await DesembolsarAsync(http, esc, Desembolso(esc, solicitudB, personaB));
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();
        // Un crédito de consumo de la misma cooperativa: un día sin UVR no lo detiene.
        var (_, _, consumo) = await CreditoDesembolsadoAsync(http, esc, "ConsumoJunto", 2_000_000m);
        var creditoDeConsumo = consumo.GetProperty("loanPublicId").GetGuid();

        var detalle = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}");
        detalle.GetProperty("terms").GetProperty("rulesVersion").GetString().Should().Be(VersionesDeReglas.Vivienda);
        detalle.GetProperty("terms").GetProperty("denominationUnit").GetString().Should().Be(nameof(DenominationUnit.Uvr));
        var plan = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}/schedule");
        plan.GetProperty("uvrValue").GetDecimal().Should().Be(serie[h]);
        plan.GetProperty("installments")[0].GetProperty("uvr").GetProperty("total").GetDecimal().Should().BePositive();
        var capitalEnUvr = Math.Round(monto / serie[h], 4, MidpointRounding.AwayFromZero);

        // (4) Treinta días de causación: el capital en pesos sigue a la UVR de cada día y la corrección monetaria queda en el libro y en su hecho.
        var ultimo = h.AddDays(DiasConUvr - 1);
        Guid corrida;
        using (RelojEn(fx, ultimo))
        {
            corrida = await CicloDeHoyAsync(fx, http, jefe, tenant);
            (await MarcaAsync(fx, tenant, credito)).Should().Be(ultimo, "la corrida causa hasta hoy: {0}",
                (await CorridaAsync(http, jefe, corrida)).GetProperty("errors").GetRawText());

            var filas = await CausacionAsync(fx, tenant, credito);
            var correcciones = filas.Where(f => f.Clase == LoanTransactionKind.MonetaryCorrection).ToList();
            correcciones.Should().NotBeEmpty("la UVR sube cada día: el capital en pesos crece con ella");
            correcciones.Select(f => f.Dia).Distinct().Should().HaveCount(DiasConUvr - 1, "una corrección por día causado");
            filas.Should().NotContain(f => f.Concepto == LoanConcept.DefaultInterest, "nada vence en el primer mes");

            var libro = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}/transactions?pageSize=100");
            libro.GetProperty("items").EnumerateArray().Where(t => t.GetProperty("kind").GetString() == nameof(LoanTransactionKind.MonetaryCorrection))
                .Should().OnlyContain(t => t.GetProperty("uvrValue").ValueKind == JsonValueKind.Number, "cada fila del libro de un crédito en UVR lleva su UVR");

            var partes = await EnLaBaseAsync(fx, tenant, async db =>
            {
                var id = await db.LendingProcessRuns.Where(r => r.PublicId == corrida).Select(r => r.Id).SingleAsync();
                return await db.AccountingFactParts.AsNoTracking()
                    .Where(p => p.ProcessRunId == id && p.FactType == CorreccionMonetariaUvrCausadaV1.Type).Select(p => p.ValueDate).ToListAsync();
            });
            partes.Should().NotBeEmpty("la corrección monetaria llega a Contabilidad como hecho propio (E12, CMU)");

            // La liquidación y el estado de cuenta: la UVR publicada del día, el capital en UVR intacto y su valor en pesos a esa UVR.
            var liquidacion = await LiquidacionAsync(http, jefe, credito, ultimo);
            var uvr = liquidacion.GetProperty("uvr");
            uvr.GetProperty("uvrValue").GetDecimal().Should().Be(serie[ultimo]);
            uvr.GetProperty("projected").GetBoolean().Should().BeFalse();
            uvr.GetProperty("principalUvr").GetDecimal().Should().Be(capitalEnUvr, "sin pagos, el capital en UVR no cambia");
            liquidacion.GetProperty("byConcept").GetProperty(nameof(LoanConcept.Principal)).GetDecimal()
                .Should().BeApproximately(capitalEnUvr * serie[ultimo], 0.05m, "el capital en pesos es el de UVR a la UVR del día (al centavo, por cuota)");
            liquidacion.GetProperty("rulesVersion").GetString().Should().Be(VersionesDeReglas.Vivienda);
            var estado = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{credito}/statement?date={ultimo:yyyy-MM-dd}");
            estado.GetProperty("uvr").GetProperty("uvrValue").GetDecimal().Should().Be(serie[ultimo]);

            // Una fecha sin UVR publicada: la liquidación la proyecta desde la última publicada, sólo para mostrar, y lo avisa.
            var futura = await LiquidacionAsync(http, jefe, credito, h.AddDays(DiasConUvr + 10));
            futura.GetProperty("uvr").GetProperty("projected").GetBoolean().Should().BeTrue();
            futura.GetProperty("uvr").GetProperty("lastPublished").GetString().Should().Be(h.AddDays(DiasConUvr).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            futura.GetProperty("warnings").EnumerateArray().Should().Contain(w => w.GetString()!.Contains("proyectada"));

            // El crédito de consumo no tiene nada de UVR.
            (await LiquidacionAsync(http, jefe, creditoDeConsumo, ultimo)).GetProperty("uvr").ValueKind.Should().Be(JsonValueKind.Null);
        }

        // (5) Un día sin UVR publicada: el ciclo no causa el crédito en UVR desde ese día y lo dice como error de grupo; el de consumo sigue.
        var sinUvr = h.AddDays(DiasConUvr + 1);
        using (RelojEn(fx, sinUvr.AddDays(1)))
        {
            var otra = await CicloDeHoyAsync(fx, http, jefe, tenant);
            (await MarcaAsync(fx, tenant, credito)).Should().Be(h.AddDays(DiasConUvr), "el último día con UVR publicada");
            (await MarcaAsync(fx, tenant, creditoDeConsumo)).Should().Be(sinUvr.AddDays(1), "la falta de UVR detiene sólo los créditos en UVR");
            var detalleDeLaCorrida = await CorridaAsync(http, jefe, otra);
            detalleDeLaCorrida.GetProperty("run").GetProperty("status").GetString().Should().Be("Completed", "un faltante de grupo no es un crédito fallido");
            detalleDeLaCorrida.GetProperty("errors").GetProperty("items").EnumerateArray().Should().Contain(e =>
                e.GetProperty("code").GetString() == "NationalValues.Missing" && e.GetProperty("loanPublicId").ValueKind == JsonValueKind.Null);
        }
    }

    // ------------------------------------------------------------------------------------------------ ayudantes --

    /// <summary>
    /// Registra en la central de este host el tope de vivienda (desde el primero del mes, sin fin) y la UVR sintética de ayer a hoy + 30, un valor por
    /// día con <see cref="FactorDiario"/>; devuelve la serie por fecha. Si otra corrida del host ya los registró, sigue (los valores son los mismos).
    /// </summary>
    private async Task<IReadOnlyDictionary<DateOnly, decimal>> RegistrarValoresDePruebaAsync(HttpClient http, DateOnly hoy)
    {
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "TOPE_TASA_VIVIENDA", validFrom = new DateOnly(hoy.Year, hoy.Month, 1), value = TopeDePrueba, unit = "EA", source = FuenteDePrueba,
        });
        var serie = new Dictionary<DateOnly, decimal>();
        var valor = UvrBase;
        for (var dia = hoy.AddDays(-1); dia <= hoy.AddDays(DiasConUvr); dia = dia.AddDays(1))
        {
            serie[dia] = valor;
            await LineasE2E.RegistrarAsync(http, maestro, new { code = "UVR", validFrom = dia, validTo = dia, value = valor, unit = "Pesos", source = FuenteDePrueba });
            valor = Math.Round(valor * FactorDiario, 4, MidpointRounding.AwayFromZero);
        }
        return serie;
    }

    /// <summary>
    /// Una versión de vivienda de tasa fija: plazos de 60 a 360 meses, montos de 1 a 500 millones, sólo hipoteca como garantía y, si
    /// <paramref name="enUvr"/>, capital en UVR (en un solo tramo).
    /// </summary>
    private static Dictionary<string, object?> VersionDeVivienda(DateOnly desde, decimal tasa, bool enUvr)
    {
        var v = LineasE2E.VersionFija(desde, "VIVIENDA", null, tasa, "Vivienda Ley 546 (e2e)");
        v["amounts"] = new { min = 1_000_000m, max = 500_000_000m };
        v["terms"] = new { minMonths = 60, maxMonths = 360 };
        v["denominationUnit"] = enUvr ? "Uvr" : "Pesos";
        v["collateralRules"] = new[] { new { coDebtors = 0, admittedKinds = new[] { "Mortgage" } } };
        return v;
    }

    /// <summary>
    /// El borrador de vivienda (no VIS, 120 cuotas, ingresos holgados), la hipoteca del grado pedido sobre un inmueble del doble del monto, la
    /// radicación con <c>asesor.o1</c> y la aprobación con <c>analista</c>; devuelve la solicitud.
    /// </summary>
    private static async Task<Guid> AprobadaDeViviendaAsync(HttpClient http, Escenario esc, Guid persona, Guid linea, decimal monto, byte gradoDeLaHipoteca)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var borrador = await CarteraE2E.ExitoAsync(http, asesor, HttpMethod.Post, OriginacionE2E.Solicitudes, new
        {
            applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = linea,
            purpose = "Compra de vivienda (e2e)", amount = monto, installments = 120, periodicity = "Monthly", amortizationSystem = "LevelPayment",
            paymentForm = "Cashier", prepaymentPreference = "ReduceTerm", isSocialHousing = false,
            applicantCapacity = new { monthlyIncome = 40_000_000m, monthlyExpenses = 2_000_000m, externalInstallments = 0m },
        });
        var id = borrador.GetProperty("publicId").GetGuid();

        await CarteraE2E.ExitoAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, Garantias, new
        {
            ownerKind = "Application", ownerPublicId = id, kind = "Mortgage", description = $"Apartamento, matrícula 050-{id.ToString("N")[..8]} (e2e)",
            value = monto * 2, mortgageRank = gradoDeLaHipoteca, registrationReference = $"050-{id.ToString("N")[..10]}",
        });

        var vigente = await CarteraE2E.GetAsync(http, asesor, $"{OriginacionE2E.Solicitudes}/{id}");
        var radicada = await CarteraE2E.ConClaveAsync(http, asesor, HttpMethod.Post, $"{OriginacionE2E.Solicitudes}/{id}/submit",
            new { rowVersion = vigente.GetProperty("rowVersion").GetString() });
        radicada.StatusCode.Should().Be(HttpStatusCode.OK, $"radicar la de vivienda: «{await radicada.Content.ReadAsStringAsync()}»");

        var (nivel, huella) = await OriginacionE2E.EnrutamientoAsync(http, asesor, id);
        nivel.Should().Be("ANALISTA");
        var aprobada = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        return id;
    }
}
