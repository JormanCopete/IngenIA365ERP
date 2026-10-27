using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Risk;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0686 (feature 013, US9; quickstart §3.11, Independent Test de la fase 12; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): el
/// cierre mensual de Cartera por sus rutas, con el proceso conducido a mano (<see cref="CierreE2E.CorrerAsync"/>, la misma pasada del
/// <c>ProcesadorDeCartera</c>). Sobre una cooperativa aislada activada (<see cref="CarteraE2E.ActivarCarteraAsync"/>, C107: nace el cierre base sin
/// líneas), con la contabilidad iniciada y la matriz contable completa:
/// <list type="bullet">
/// <item>un deudor con dos consumos y un comercial; la calificación por análisis que no empeora se rechaza (<c>NotWorse</c> con
/// <c>data.ruleCategory</c>) y la que empeora el primer consumo a C se registra (US9-4);</item>
/// <item>antes de cerrar Cartera, Contabilidad no cierra el mes (<c>Accounting.Period.LendingNotClosed</c>); pasado el mes y causado hasta su último
/// día, la preparación lista sus cinco requisitos cumplidos, la orden responde 202 <c>Requested</c> y una segunda <c>Lending.Process.AlreadyRunning</c>
/// con la corrida;</item>
/// <item>el cierre deja el consumo por análisis en C, el otro consumo del mismo deudor en C por <b>arrastre</b> y el comercial en A (US9-1); el
/// reporte de cambios compara con el base (US9-3, primer cierre en delta, US8-5); el modelo por segmento marca «modelo obligatorio no disponible»
/// en una cooperativa de ahorro y crédito y el cierre lleva la marca (US9-8, D28);</item>
/// <item>reabrir el base se rechaza (<c>BaseNotReopenable</c>); reabrir el último deja la versión 1 <c>Reopened</c> con sus reversos y volver a
/// cerrar crea la versión 2 que la reemplaza; reabrir la versión 1 otra vez, que ya no es la última, se rechaza (<c>NotLast</c>);</item>
/// <item>un pago con fecha valor del mes cerrado sin <c>Lending.Payments.BackdateBeforeClose</c> se rechaza
/// (<c>Lending.Payment.ValueDateBeforeClosing</c>); con la atribución se aplica, el cierre no cambia (misma huella de líneas) y su hecho se
/// reconoce en el mes siguiente (US9-2);</item>
/// <item>con el cierre de Cartera vigente, Contabilidad cierra el mes; desde ahí reabrir el cierre se rechaza
/// (<c>Lending.MonthlyClose.AccountingPeriodClosed</c>).</item>
/// </list>
/// Lo que no recorre y fijan los casos dorados y las pruebas de Application (anotado en tasks.md, T0686): las alturas de mora por modalidad (r01),
/// el pago único vencido (r06, US9-5) y el reestructurado (r05) —pedirían meses de mora y un cierre por cada mes—; el contenido contable del
/// <c>PV</c> lo recorre <c>ContabilizacionDeCarteraPorMensajesTests</c> (T0687). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CierreMensualDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "cierre_cartera";
    private const string Cierres = CierreE2E.Cierres;

    [Fact]
    public async Task El_cierre_mensual_califica_arrastra_congela_se_reabre_y_deja_cerrar_a_Contabilidad()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var jefe = esc.Usuarios.Jefe.Token;
        var tenant = esc.Coop.TenantPublicId;
        var despachador = new ConductorDelDespachador(fx);
        var hoy = esc.Hoy;
        var (anio, mes) = (hoy.Year, hoy.Month);
        var finDelMes = new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes));
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);

        // ----------------------------------------------------------- el base de la activación: un cierre sin líneas --
        var bases = (await CarteraE2E.GetAsync(http, jefe, $"{Cierres}?kind=Base")).EnumerateArray().ToList();
        bases.Should().ContainSingle("la activación deja el cierre base (C107)");
        var cierreBase = bases[0].GetProperty("publicId").GetGuid();

        // ----------------------------------------------- un deudor con dos consumos y un comercial; otro que paga después --
        var comercial = await CierreE2E.LineaComercialAsync(http, esc);
        var (deudor, _) = await DeudorAsync(http, admin, "Arrastre");
        var consumo1 = await CierreE2E.DesembolsadoAsync(http, esc, deudor, 1_500_000m);
        var consumo2 = await CierreE2E.DesembolsadoAsync(http, esc, deudor, 1_000_000m);
        var credComercial = await CierreE2E.DesembolsadoAsync(http, esc, deudor, 2_000_000m, comercial);
        var (pagador, _) = await DeudorAsync(http, admin, "FechaValor");
        var dePago = await CierreE2E.DesembolsadoAsync(http, esc, pagador, 1_000_000m);

        // ------------------------------------------------- la calificación por análisis: nunca mejora, sí empeora (US9-4) --
        var igual = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{CierreE2E.Riesgo}/analysis-ratings", new
        {
            loanPublicId = consumo1, category = "A", reason = "Análisis que no empeora (e2e)",
        });
        igual.IsSuccessStatusCode.Should().BeFalse(await igual.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(igual)).Should().Be("Lending.AnalysisRating.NotWorse");
        (await CarteraE2E.LeerAsync(igual)).GetProperty("data").GetProperty("ruleCategory").GetString().Should().Be("A");
        var analisis = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{CierreE2E.Riesgo}/analysis-ratings", new
        {
            loanPublicId = consumo1, category = "C", reason = "Deudor reportado en mora con otra entidad (e2e)",
        });
        analisis.StatusCode.Should().Be(HttpStatusCode.Created, await analisis.Content.ReadAsStringAsync());
        var calificacion = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{consumo1}/rating");
        calificacion.GetProperty("label").GetString().Should().Be("Informativa");
        calificacion.GetProperty("category").GetString().Should().Be("C", "la informativa ya lee el análisis vigente");
        calificacion.GetProperty("analysis").GetProperty("category").GetString().Should().Be("C");

        // ------------------------------------------------------- todavía en el mes: Contabilidad no cierra, Cartera tampoco --
        // Contabilidad sabe de la activación por el recibo de CarteraActivada: el despachador lo entrega antes.
        await despachador.PasadasAsync(tenant, 3);
        await CierreE2E.CerrarContabilidadAsync(http, admin, anio, mes, "Accounting.Period.LendingNotClosed");
        var temprano = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Cierres}/{anio}/{mes}/close", new { });
        (await CarteraE2E.CodigoDeErrorAsync(temprano)).Should().Be("Lending.MonthlyClose.MonthNotEnded");

        using (RelojEn(fx, finDelMes.AddDays(1)))
        {
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            await despachador.PasadasAsync(tenant, 3);

            // -------------------------------------------------------------------------- preparación y orden (T47) --
            var preparacion = await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{anio}/{mes}/readiness");
            preparacion.GetProperty("ready").GetBoolean().Should().BeTrue(preparacion.ToString());
            preparacion.GetProperty("requirements").EnumerateArray().Select(r => r.GetProperty("code").GetString())
                .Should().Equal("PreviousClose", "AccountingPeriodOpen", "AccrualUpToDate", "ReplicaSynced", "NoRunInProgress");
            var corrida1 = await CierreE2E.OrdenarAsync(http, jefe, anio, mes);
            var otra = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, $"{Cierres}/{anio}/{mes}/close", new { });
            (await CarteraE2E.CodigoDeErrorAsync(otra)).Should().Be("Lending.Process.AlreadyRunning");
            (await CarteraE2E.LeerAsync(otra)).GetProperty("data").GetProperty("processRunPublicId").GetGuid().Should().Be(corrida1);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, corrida1);

            // --------------------------------------------------------- el cierre: arrastre, cambios y marcas (US9-1, US9-3) --
            var v1 = await CierreE2E.MensualAsync(http, jefe, anio, mes, version: 1);
            var encabezado = await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{v1}");
            encabezado.GetProperty("close").GetProperty("status").GetString().Should().Be("Closed");
            encabezado.GetProperty("previousClosePublicId").GetGuid().Should().Be(cierreBase, "el primer cierre tras activar se compara con el base (T49)");
            encabezado.GetProperty("close").GetProperty("closeDate").GetString().Should().Be(finDelMes.ToString("yyyy-MM-dd"));
            var lineas = await CierreE2E.LineasAsync(http, jefe, v1);
            (lineas[consumo1].Categoria, lineas[consumo1].Causa).Should().Be(("C", "Analysis"));
            (lineas[consumo2].Categoria, lineas[consumo2].Causa).Should().Be(("C", "Drag"), "el otro consumo del deudor se arrastra a C (US9-1)");
            lineas[credComercial].Categoria.Should().Be("A", "el arrastre no cruza de modalidad SARC");
            lineas[dePago].Categoria.Should().Be("A");
            lineas[consumo1].Deterioro.Should().BeGreaterThan(0m, "un consumo en C se deteriora");
            var cambios = (await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{v1}/changes?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
            cambios.Should().Contain(c => c.GetProperty("loanPublicId").GetGuid() == consumo2 && c.GetProperty("current").GetString() == "C"
                                          && c.GetProperty("cause").GetString() == "Drag", "el reporte de cambios dice la anterior, la nueva y la causa (US9-3)");

            // US9-8 (D28) con K6a desplegada (fase 22, bloque 3): la cooperativa de ahorro y crédito está obligada y ya no queda la marca «modelo
            // obligatorio no disponible»: los segmentos de pérdida esperada van por el Anexo 2 (los demás, por el Anexo 1). El recorrido completo de la
            // pérdida esperada lo hace PerdidaEsperadaTests (T1023).
            var modelo = (await CarteraE2E.GetAsync(http, jefe, $"{CierreE2E.Riesgo}/impairment-model")).EnumerateArray().ToList();
            modelo.Should().OnlyContain(s => s.GetProperty("mark").ValueKind != JsonValueKind.String, $"con K6a no hay marca: {string.Join(", ", modelo)}");
            encabezado.GetProperty("close").GetProperty("marks").GetProperty("obligatoryModelUnavailable").GetBoolean().Should().BeFalse(
                "la marca del cierre es la del modelo por segmento");
            modelo.Where(s => s.GetProperty("basis").GetString() == "Obligated" && s.GetProperty("model").GetString() == "ExpectedLossAnnex2")
                .Should().NotBeEmpty($"COOP_AHORRO_CREDITO está obligada: {string.Join(", ", modelo)}");

            // ------------------------------------------------------------------------- reabrir: nunca el base (C58) --
            var alBase = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cierres}/{cierreBase}/reopen", new { reason = "Intento sobre el base (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(alBase)).Should().Be("Lending.MonthlyClose.BaseNotReopenable");

            var reapertura = await CierreE2E.ReabrirAsync(http, admin, v1, "Análisis registrado tarde (e2e)");
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, reapertura);
            var reabierto = await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{v1}");
            reabierto.GetProperty("close").GetProperty("status").GetString().Should().Be("Reopened");
            reabierto.GetProperty("reopenings").EnumerateArray().Should().ContainSingle()
                .Which.GetProperty("reversalPartCount").GetInt32().Should().BeGreaterThan(0, "la reapertura reversa cada parte del cierre");
            (await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{consumo1}/rating")).GetProperty("official").ValueKind
                .Should().Be(JsonValueKind.Null, "con el mensual reabierto no queda cierre vigente con su línea (el base es anterior al crédito)");

            // ------------------------------------------------------------------------- volver a cerrar: Version + 1 --
            var corrida2 = await CierreE2E.OrdenarAsync(http, jefe, anio, mes);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, corrida2);
            var v2 = await CierreE2E.MensualAsync(http, jefe, anio, mes, version: 2);
            var segundo = await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{v2}");
            segundo.GetProperty("supersedesClosePublicId").GetGuid().Should().Be(v1);
            var huella = segundo.GetProperty("linesSha256").GetString();
            var noEsElUltimo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cierres}/{v1}/reopen", new { reason = "El reabierto otra vez (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(noEsElUltimo)).Should().BeOneOf("Lending.MonthlyClose.NotLast", "Lending.MonthlyClose.AlreadyReopened");

            // ----------------------------------------------- un pago con fecha valor del mes cerrado (FR-012, D16, US9-2) --
            var sinAtribucion = await AplicarUnoAsync(http, esc.Usuarios, Pago(dePago, pagador, "FreeAmount", 50_000m, finDelMes));
            Rechazada(sinAtribucion, "Lending.Payment.ValueDateBeforeClosing");
            var rol = await OriginacionE2E.RolPropioAsync(http, admin, "ENS-CIERRE-FECHAVALOR", "Lending.Payments.BackdateBeforeClose");
            await OriginacionE2E.DarRolAsync(http, admin, esc.Usuarios.Pruebas, rol);
            await AplicadoAsync(http, esc.Usuarios, Pago(dePago, pagador, "FreeAmount", 50_000m, finDelMes));
            (await CarteraE2E.GetAsync(http, jefe, $"{Cierres}/{v2}")).GetProperty("linesSha256").GetString()
                .Should().Be(huella, "el pago con fecha valor del mes cerrado no reescribe el cierre");
            var hechoDelPago = (await CarteraE2E.GetAsync(http, admin, $"/api/lending/accounting/facts?type=PagoAplicado&loanPublicId={dePago}&pageSize=5"))
                .GetProperty("items").EnumerateArray().First();
            hechoDelPago.GetProperty("accountingDate").GetString().Should().Be(finDelMes.AddDays(1).ToString("yyyy-MM-dd"),
                "se reconoce en el primer mes sin cierre de Cartera (FechaContable)");
            await despachador.PasadasAsync(tenant, 3);

            // ---------------------------------- con el cierre vigente Contabilidad cierra el mes; después no se reabre --
            await Receipts.RecaudoYTesoreriaE2E.CuadresDefinitivosAsync(http, admin);
            await CierreE2E.CerrarContabilidadAsync(http, admin, anio, mes, codigoEsperado: null);
            var tarde = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cierres}/{v2}/reopen", new { reason = "Después del cierre contable (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(tarde)).Should().Be("Lending.MonthlyClose.AccountingPeriodClosed");
        }
    }
}

/// <summary>
/// Los ayudantes de las e2e del cierre mensual de Cartera (feature 013, US9, T0686–T0688): ordenar y reabrir por las rutas (202), conducir la corrida
/// con la misma pasada del <c>ProcesadorDeCartera</c> —que la fixture deja apagado— hasta que termine, leer las líneas del cierre y cerrar el mes en
/// Contabilidad. (nuevo)
/// </summary>
public static class CierreE2E
{
    public const string Riesgo = "/api/lending/risk";
    public const string Cierres = Riesgo + "/monthly-closes";

    /// <summary>Una línea del cierre: categoría, causa y deterioro total.</summary>
    public sealed record Linea(string Categoria, string Causa, decimal Deterioro);

    /// <summary>Ordena el cierre del mes con <paramref name="token"/>; exige el 202 <c>Requested</c> y devuelve la corrida.</summary>
    public static async Task<Guid> OrdenarAsync(HttpClient http, string token, int anio, int mes)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Cierres}/{anio}/{mes}/close", new { reason = "Cierre del mes (e2e)" });
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, $"ordenar el cierre: «{await resp.Content.ReadAsStringAsync()}»");
        var cuerpo = await CarteraE2E.LeerAsync(resp);
        cuerpo.GetProperty("status").GetString().Should().Be("Requested", "ordenar sólo encola");
        return cuerpo.GetProperty("processRunPublicId").GetGuid();
    }

    /// <summary>Reabre el cierre con <paramref name="token"/>; exige el 202 y devuelve la corrida <c>CloseReopen</c>.</summary>
    public static async Task<Guid> ReabrirAsync(HttpClient http, string token, Guid cierre, string motivo)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Cierres}/{cierre}/reopen", new { reason = motivo });
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, $"reabrir: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("processRunPublicId").GetGuid();
    }

    /// <summary>
    /// Conduce la corrida con la pasada del procesador (la misma que corre el <c>ProcesadorDeCartera</c>) hasta que termine; exige
    /// <c>Completed</c>. Una pasada por cooperativa corre todo lo pendiente dentro de su presupuesto, en el orden de §22.1.
    /// </summary>
    public static async Task CorrerAsync(CentralIdentityApiFixture fx, HttpClient http, string token, Guid tenant, Guid corrida, TimeSpan? presupuesto = null)
    {
        var tiempo = presupuesto ?? TimeSpan.FromMinutes(5);
        var parametros = new ParametrosDeLaPasada(1, tiempo, tiempo + TimeSpan.FromMinutes(1)) { HoraDeInicio = TimeOnly.MinValue };
        JsonElement detalle = default;
        for (var i = 0; i < 20; i++)
        {
            using (var alcance = fx.Factory.Services.CreateScope())
                await alcance.ServiceProvider.GetRequiredService<PasadaDeCartera>().CorrerAsync(parametros, tenant, CancellationToken.None);
            detalle = await CorridaAsync(http, token, corrida);
            var estado = detalle.GetProperty("run").GetProperty("status").GetString();
            if (estado is "Completed") return;
            if (estado is "Failed" or "CompletedWithErrors") break;
        }
        throw new Xunit.Sdk.XunitException($"La corrida {corrida} no terminó: {detalle}");
    }

    /// <summary>
    /// Una tanda del cierre mensual con <c>ExecuteMonthlyCloseCommand</c>, dentro de <c>IEjecutorEnCooperativa</c> y con el tamaño de tanda dado
    /// (la prueba de volumen mide cada una); devuelve si la corrida quedó pendiente o terminó.
    /// </summary>
    public static async Task<AvanceDeCorrida> TandaDelCierreAsync(CentralIdentityApiFixture fx, Guid tenant, Guid corrida, int tamano = 250)
    {
        TenantDirectoryEntry cooperativa;
        using (var alcance = fx.Factory.Services.CreateScope())
            cooperativa = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
                .Single(c => c.PublicId == tenant);
        Result<AvanceDeCorrida>? r = null;
        var origen = PasadaDeCartera.Origen;
        var ejecutada = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa,
            Actor.ProcesoDeIntegracion(origen), origen, async (servicios, ct) =>
            {
                servicios.GetRequiredService<ParametrosDeLaTandaDeCartera>().TamanoDeTanda = tamano;
                r = await servicios.GetRequiredService<ISender>().Send(new ExecuteMonthlyCloseCommand(corrida), ct);
            }, CancellationToken.None);
        ejecutada.Should().Be(ResultadoEnCooperativa.Ejecutada);
        r!.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>El cierre mensual del mes en la versión dada, de la lista.</summary>
    public static async Task<Guid> MensualAsync(HttpClient http, string token, int anio, int mes, int version)
    {
        var lista = (await CarteraE2E.GetAsync(http, token, $"{Cierres}?year={anio}&kind=Monthly")).EnumerateArray().ToList();
        var cierre = lista.Where(c => c.GetProperty("month").GetInt32() == mes && c.GetProperty("version").GetInt32() == version).ToList();
        cierre.Should().ContainSingle($"el cierre de {anio}-{mes:00} versión {version}: {string.Join(", ", lista)}");
        return cierre[0].GetProperty("publicId").GetGuid();
    }

    /// <summary>Las líneas del cierre por crédito.</summary>
    public static async Task<Dictionary<Guid, Linea>> LineasAsync(HttpClient http, string token, Guid cierre)
    {
        var pagina = await CarteraE2E.GetAsync(http, token, $"{Cierres}/{cierre}/loans?pageSize=200");
        return pagina.GetProperty("items").EnumerateArray().ToDictionary(l => l.GetProperty("loanPublicId").GetGuid(),
            l => new Linea(l.GetProperty("category").GetString()!, l.GetProperty("cause").GetString()!, l.GetProperty("totalImpairment").GetDecimal()));
    }

    /// <summary>Cierra el mes en Contabilidad reconociendo los avisos; con <paramref name="codigoEsperado"/>, exige ese rechazo.</summary>
    public static async Task CerrarContabilidadAsync(HttpClient http, string token, int anio, int mes, string? codigoEsperado)
    {
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/accounting/periods/{anio}/{mes}/close",
            new { acknowledgeLendingPending = true, acknowledgeLendingLoad = true });
        if (codigoEsperado is null)
        {
            resp.IsSuccessStatusCode.Should().BeTrue($"cerrar {anio}-{mes:00} en Contabilidad: «{await resp.Content.ReadAsStringAsync()}»");
            return;
        }
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resp.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be(codigoEsperado);
    }

    /// <summary>La línea comercial persona natural del ensayo, en el alcance de quien radica y de quien decide.</summary>
    public static async Task<Guid> LineaComercialAsync(HttpClient http, DesembolsoE2E.Escenario esc)
    {
        var admin = esc.Coop.TokenAdmin;
        var version = OriginacionE2E.VersionDeOriginacion(esc.Hoy, conDocumento: false, modalidad: "COMERCIAL");
        version["subsegmentCode"] = "PersonaNatural";
        var linea = await LineasE2E.CrearExitoAsync(http, admin, "COMERC", "Comercial persona natural (cierre)", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, linea };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);
        return linea;
    }

    /// <summary>Una solicitud de <paramref name="linea"/> (ORIGEN si no viene) aprobada y desembolsada por cheque; devuelve el crédito.</summary>
    public static async Task<Guid> DesembolsadoAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid persona, decimal monto, Guid? linea = null)
    {
        var (solicitud, _) = await DesembolsoE2E.AprobadaAsync(http, esc, persona, monto, linea);
        var hecho = await DesembolsoE2E.DesembolsarAsync(http, esc, DesembolsoE2E.Desembolso(esc, solicitud, persona));
        return hecho.GetProperty("loanPublicId").GetGuid();
    }
}
