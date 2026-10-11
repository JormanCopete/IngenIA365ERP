using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using static IngenIA365ERP.API.IntegrationTests.Lending.LineasE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0343 (feature 013, US1; quickstart §1.7 y §3.2; colección «Cartera e2e», cooperativa aislada «lineas», los dos motores por
/// <c>DB_PROVIDER</c>), por HTTP con tokens reales y la usura de consumo registrada en la central del host y replicada:
/// <list type="bullet">
/// <item>US1-6: la plantilla de quickstart §2.8 con una modalidad inexistente en una fila: <c>?mode=review</c> lista el error con hoja, fila
/// y columna sin guardar; <c>?mode=apply</c> responde 422 <c>Import.Invalid</c>; corregida, entran las siete líneas.</item>
/// <item>US1-1: una línea de consumo cuya tasa queda bajo la usura pero cuya efectiva con el seguro que computa la supera → 422
/// <c>Lending.Usury.Exceeded</c> con la usura, la modalidad y la diferencia, y no se guarda nada.</item>
/// <item>Origen comercial sin concepto tributario → <c>Lending.CreditLine.InterestTaxConceptMissing</c>.</item>
/// <item>US1-2: una versión de <c>CONLIB</c> desde el mes siguiente queda en el historial y la de hoy rige hasta la víspera.</item>
/// <item><c>VARIBR</c> pasa y <c>usury-check</c> explica la efectiva y la usura, sin escribir.</item>
/// <item>Cerrar y reabrir con motivo; borrar una línea sin uso. El borrado negado con <c>HasLoans</c> lo suma US2, que trae solicitudes.</item>
/// <item>SC-018: <c>Cobranza.HorarioLunesAViernes</c> más amplio que el techo nacional → <c>Lending.Parameter.BelowLegalFloor</c> con el
/// límite y su fuente.</item>
/// </list>
/// El alcance por línea (<c>asesor.o1</c> no ve <c>VARIBR</c>) está en <c>SeguridadYHabeasDataDeCarteraTests</c>. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class LineasDeCreditoYUsuraTests(CentralIdentityApiFixture fx)
{
    private async Task<(CarteraE2E.CooperativaAislada Coop, DateOnly Hoy)> CooperativaAsync()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "lineas");
        await PrepararAsync(fx, coop);
        return (coop, CarteraE2E.Hoy(fx));
    }

    // ------------------------------------------------------------------------------------------------ US1-6 --

    [Fact]
    public async Task La_plantilla_con_un_error_no_guarda_nada_y_corregida_entran_las_siete_lineas()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var aseguradora = await AseguradoraAsync(http, admin);

        var conError = Libro(Hojas(hoy, aseguradora, modalidadDeMicro: "INEXISTENTE"));
        var revision = await ImportarAsync(http, admin, conError, "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var cuerpo = await CarteraE2E.LeerAsync(revision);
        cuerpo.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errores = cuerpo.GetProperty("errors").EnumerateArray().ToList();
        errores.Should().Contain(e => e.GetProperty("sheet").GetString() == "Lineas" && e.GetProperty("row").GetInt32() == 5
                                      && e.GetProperty("column").GetString() == "modalidad",
            $"la fila de MICRO (la cuarta de datos) con la modalidad inexistente: {cuerpo}");
        errores.Select(e => Tiene(e, "code") && Tiene(e, "message")).Should().OnlyContain(b => b);
        (await CodigosAsync(http, admin)).Should().BeEmpty("revisar no guarda nada");

        var aplicar = await ImportarAsync(http, admin, conError, "apply");
        aplicar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await aplicar.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(aplicar)).Should().Be("Import.Invalid");
        (await CodigosAsync(http, admin)).Should().BeEmpty("aplicar es todo o nada");

        var corregida = Libro(Hojas(hoy, aseguradora, modalidadDeMicro: "MICROCREDITO"));
        var bien = await ImportarAsync(http, admin, corregida, "review");
        var revisada = await CarteraE2E.LeerAsync(bien);
        revisada.GetProperty("valid").GetBoolean().Should().BeTrue($"corregida no tiene errores: {revisada}");
        var aplicada = await ImportarAsync(http, admin, corregida, "apply");
        aplicada.StatusCode.Should().Be(HttpStatusCode.OK, await aplicada.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(aplicada)).GetProperty("applied").GetBoolean().Should().BeTrue();

        (await CodigosAsync(http, admin)).Should().BeEquivalentTo(["CONLIB", "CONCAJA", "VARIBR", "MICRO", "COMER", "ROTATIVO", "EMPLEA"]);
        var equivalencias = await CarteraE2E.GetAsync(http, admin, "/api/lending/commercial-term-equivalences");
        equivalencias.EnumerateArray().Should().Contain(e => string.Equals(e.GetProperty("commercialCode").GetString(), "Monthly", StringComparison.OrdinalIgnoreCase)
    && e.GetProperty("periodicity").GetInt32() == 30, "la hoja Equivalencias también entra (el texto de Comercio se guarda normalizado)");
        var conlib = await LineaPorCodigoAsync(http, admin, "CONLIB");
        conlib.GetProperty("currentVersion").GetProperty("insurances").GetArrayLength().Should().Be(1, "el seguro de vida deudores de la hoja Seguros");
    }

    /// <summary>Las seis hojas de quickstart §2.8 con las siete líneas; la de <c>MICRO</c> con la modalidad dada.</summary>
    private static Dictionary<string, IReadOnlyList<Fila>> Hojas(DateOnly hoy, string aseguradora, string modalidadDeMicro)
    {
        Fila Linea(string codigo, string nombre, string modalidad, string subsegmento, string sistema, string convencion, string periodicidades,
            string formas, decimal? tasa)
        {
            var periodicidad = periodicidades.Split(',')[0].Trim();
            return new Fila
            {
                ["codigo"] = codigo, ["vigenteDesde"] = hoy, ["nombre"] = nombre, ["destino"] = $"Destino de {nombre}", ["modalidad"] = modalidad,
                ["subsegmento"] = subsegmento, ["tipoDeTasa"] = tasa is null ? "Variable" : "Fixed", ["tasaCorrienteEA"] = tasa,
                ["tipoDeTasaDeMora"] = "RelativeToCurrent", ["factorDeMora"] = 1.5m, ["convencionDeDias"] = convencion,
                ["sistemaDeAmortizacion"] = sistema, ["montoMinimo"] = 500_000m, ["montoMaximo"] = 30_000_000m, ["plazoMinimo"] = 1, ["plazoMaximo"] = 48,
                ["periodicidades"] = periodicidades, ["periodicidadPorDefecto"] = periodicidad, ["formasDePago"] = formas,
                ["formaDePagoPorDefecto"] = formas.Split(',')[0].Trim(),
            };
        }

        var conlib = Linea("CONLIB", "Consumo con libranza", "CONSUMO", "ConLibranza", "LevelPayment", "Thirty360", "Monthly, Biweekly", "OwnPayroll, Cashier", 24m);
        var concaja = Linea("CONCAJA", "Consumo por caja", "CONSUMO", "SinLibranza", "ConstantPrincipal", "Actual365", "Monthly", "Cashier", 22m);
        concaja["exigeConsultaACentrales"] = "sí";
        var varibr = Linea("VARIBR", "Consumo IBR más margen", "CONSUMO", "SinLibranza", "LevelPayment", "Thirty360", "Monthly", "Cashier", null);
        varibr["indice"] = "IBR";
        varibr["claveDelIndice"] = ClaveDelIbr;
        varibr["margen"] = 8m;
        varibr["mesesDeReajuste"] = 1;
        varibr["tasaPiso"] = 10m;
        varibr["tasaTecho"] = 24m;
        var micro = Linea("MICRO", "Microcrédito empresarial", modalidadDeMicro, "Empresarial", "LevelPayment", "Actual365", "Biweekly", "Cashier", 40m);
        micro["gracia"] = "Principal";
        micro["graciaMaximaCuotas"] = 2;
        var comer = Linea("COMER", "Comercial persona natural", "COMERCIAL", "PersonaNatural", "Bullet", "Actual365", "SinglePayment", "Cashier", 20m);
        comer["plazoMaximo"] = 12;
        var rotativo = Linea("ROTATIVO", "Cupo rotativo de almacén", "CONSUMO", "SinLibranza", "LevelPayment", "Actual365", "Monthly", "Cashier", 23m);
        rotativo["admiteOrigenComercial"] = "sí";
        rotativo["conceptoTributarioDelInteres"] = "NotTaxed";
        rotativo["soporteDelConceptoTributario"] = Guid.NewGuid().ToString();
        rotativo["recalculoPorNotas"] = "ReduceInstallment";
        var emplea = Linea("EMPLEA", "Empleados", "CONSUMO", "Empleados", "LevelPayment", "Actual365", "Biweekly", "OwnPayroll", 15m);
        emplea["paraEmpleados"] = "sí";

        return new Dictionary<string, IReadOnlyList<Fila>>(StringComparer.Ordinal)
        {
            ["Lineas"] = [conlib, concaja, varibr, micro, comer, rotativo, emplea],
            ["Tasas"] =
            [
                new Fila { ["linea"] = "CONCAJA", ["vigenteDesde"] = hoy, ["montoDesde"] = 0m, ["montoHasta"] = 5_000_000m, ["tasaCorrienteEA"] = 22m },
                new Fila { ["linea"] = "CONCAJA", ["vigenteDesde"] = hoy, ["montoDesde"] = 5_000_001m, ["montoHasta"] = 30_000_000m, ["tasaCorrienteEA"] = 20m },
            ],
            ["Documentos"] =
            [
                new Fila { ["linea"] = "CONLIB", ["vigenteDesde"] = hoy, ["codigo"] = "CEDULA", ["nombre"] = "Copia de cédula", ["obligatorio"] = "sí" },
            ],
            ["Seguros"] =
            [
                new Fila
                {
                    ["linea"] = "CONLIB", ["vigenteDesde"] = hoy, ["codigo"] = "VIDA", ["nombre"] = "Vida deudores", ["aseguradora"] = aseguradora,
                    ["base"] = "OutstandingBalance", ["tarifa"] = 0.045m, ["obligatorio"] = "sí", ["retieneParteDeLaPrima"] = "no",
                },
            ],
            ["Garantias"] =
            [
                new Fila { ["linea"] = "COMER", ["vigenteDesde"] = hoy, ["montoDesde"] = 10_000_001m, ["codeudoresMinimos"] = 1, ["tiposAdmitidos"] = "CoDebtor, Pledge" },
            ],
            ["Equivalencias"] =
            [
                new Fila { ["periodicidadComercial"] = "Monthly", ["periodicidad"] = "Monthly", ["vigenteDesde"] = hoy },
            ],
        };
    }

    // ------------------------------------------------------------------------------------- US1-1 y D20 --

    [Fact]
    public async Task Una_linea_cuya_efectiva_con_el_seguro_supera_la_usura_se_rechaza_sin_guardar_nada()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var aseguradora = await CarteraE2E.PersonaAsync(http, admin, "Aseguradora", "Usura");

        // 25 % EA queda bajo la usura de 25,62 %; el seguro que retiene la mitad de 0,5 % mensual sobre el saldo la empuja por encima.
        var version = VersionFija(hoy, "CONSUMO", "SinLibranza", 0.25m);
        version["insurances"] = new[]
        {
            new
            {
                code = "VIDA", name = "Vida deudores", insurerPersonPublicId = aseguradora, @base = "OutstandingBalance", rate = 0.005m,
                periodicity = "Monthly", mandatory = true, retainedFraction = 0.5m, isDebtorLifeInsurance = true,
            },
        };
        var resp = await CrearAsync(http, admin, "SOBRE", "Sobre la usura", version);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resp.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.Usury.Exceeded");
        var data = (await CarteraE2E.LeerAsync(resp)).GetProperty("data");
        data.GetProperty("usuryRate").GetDecimal().Should().Be(TasaDeUsuraDeConsumo);
        data.GetProperty("modalityKey").GetString().Should().Be(UsuraDeConsumo);
        var efectiva = data.GetProperty("effectiveRate").GetDecimal();
        efectiva.Should().BeGreaterThan(TasaDeUsuraDeConsumo, "la efectiva con el seguro que computa supera la usura, no la nominal");
        data.GetProperty("nationalValuePublicId").GetGuid().Should().NotBeEmpty();
        data.GetProperty("errors").EnumerateArray().Should().NotBeEmpty("con el campo que la supera");
        (await CodigosAsync(http, admin)).Should().NotContain("SOBRE", "no se guarda nada");

        // Sin el seguro, la misma tasa pasa: lo que la empujó fue el cargo que computa.
        var sinSeguro = await CrearAsync(http, admin, "BAJO", "Bajo la usura", VersionFija(hoy, "CONSUMO", "SinLibranza", 0.25m));
        sinSeguro.StatusCode.Should().Be(HttpStatusCode.Created, await sinSeguro.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Una_linea_de_origen_comercial_con_interes_sin_concepto_tributario_no_se_registra()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();

        var version = VersionFija(hoy, "CONSUMO", "SinLibranza", 0.23m);
        version["allowsCommercialOrigin"] = true;
        var resp = await CrearAsync(http, coop.TokenAdmin, "ROTSIN", "Rotativo sin concepto", version);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resp.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.CreditLine.InterestTaxConceptMissing");
    }

    // ----------------------------------------------------------------------------------------------- US1-2 --

    [Fact]
    public async Task Una_version_desde_el_mes_siguiente_queda_en_el_historial_y_la_de_hoy_rige_hasta_la_vispera()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var linea = await CrearExitoAsync(http, admin, "VERSIONA", "Consumo que cambia de tasa", VersionFija(hoy, "CONSUMO", "ConLibranza", 0.24m));
        var siguiente = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1);

        var nueva = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/versions",
            VersionFija(siguiente, "CONSUMO", "ConLibranza", 0.21m, "Tasa del comité para el mes siguiente"));
        nueva.StatusCode.Should().Be(HttpStatusCode.Created, await nueva.Content.ReadAsStringAsync());

        var hoyLeida = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");
        var vigente = hoyLeida.GetProperty("currentVersion");
        vigente.GetProperty("effectiveAnnual").GetDecimal().Should().Be(0.24m, "hoy rige la versión de hoy");
        vigente.GetProperty("validTo").GetString().Should().Be(siguiente.AddDays(-1).ToString("yyyy-MM-dd"), "hasta la víspera de la nueva");
        var historial = hoyLeida.GetProperty("versions").EnumerateArray().ToList();
        historial.Should().HaveCount(2);
        historial.Select(h => Tiene(h, "registeredAt") && Tiene(h, "reason")).Should().OnlyContain(b => b);

        var despues = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}?date={siguiente:yyyy-MM-dd}");
        despues.GetProperty("currentVersion").GetProperty("effectiveAnnual").GetDecimal().Should().Be(0.21m);

        // Una versión antes de hoy no se registra: lo pactado no se reescribe. En una línea con versiones, una fecha anterior a la última
        // choca primero con ella (VersionOverlaps); la regla de la fecha se ve en el alta.
        var anterior = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/versions",
            VersionFija(hoy.AddDays(-1), "CONSUMO", "ConLibranza", 0.2m));
        anterior.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(anterior)).Should().BeOneOf("Lending.CreditLine.VersionOverlaps", "Lending.CreditLine.RetroactiveVersion");
        var retroactiva = await CrearAsync(http, admin, "AYER", "Desde ayer", VersionFija(hoy.AddDays(-1), "CONSUMO", "ConLibranza", 0.2m));
        retroactiva.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(retroactiva)).Should().Be("Lending.CreditLine.RetroactiveVersion");
        (await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}")).GetProperty("versions").GetArrayLength().Should().Be(2, "nada más se guardó");
    }

    // ---------------------------------------------------------------------------------- VARIBR y usury-check --

    [Fact]
    public async Task Una_linea_variable_pasa_y_usury_check_explica_la_efectiva_sin_escribir()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var linea = await CrearExitoAsync(http, admin, "VARIBR2", "Consumo IBR más margen", VersionVariable(hoy));
        var antes = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");

        var consulta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/usury-check", new { amount = 10_000_000m });
        consulta.StatusCode.Should().Be(HttpStatusCode.OK, await consulta.Content.ReadAsStringAsync());
        var u = await CarteraE2E.LeerAsync(consulta);
        u.GetProperty("passes").GetBoolean().Should().BeTrue();
        u.GetProperty("usuryRate").GetDecimal().Should().Be(TasaDeUsuraDeConsumo);
        u.GetProperty("usuryKey").GetString().Should().Be(UsuraDeConsumo);
        var efectiva = u.GetProperty("effectiveRate").GetDecimal();
        efectiva.Should().BeGreaterThan(0.10m).And.BeLessThan(TasaDeUsuraDeConsumo, "IBR 9,25 % NMV más 8 puntos, a EA, entre piso y techo");
        u.GetProperty("headroom").GetDecimal().Should().Be(TasaDeUsuraDeConsumo - efectiva);
        u.GetProperty("explanation").EnumerateArray().Should().NotBeEmpty();

        var despues = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{linea}");
        despues.GetProperty("versions").GetArrayLength().Should().Be(antes.GetProperty("versions").GetArrayLength(), "usury-check no escribe");

        var lista = await CarteraE2E.GetAsync(http, admin, Lineas);
        var enLista = lista.EnumerateArray().Single(l => l.GetProperty("code").GetString() == "VARIBR2");
        enLista.GetProperty("currentVersion").GetProperty("indexCode").GetString().Should().Be("IBR");
        enLista.GetProperty("currentVersion").GetProperty("usuryHeadroom").GetDecimal().Should().BeGreaterThan(0m);
    }

    // ------------------------------------------------------------------------------ cerrar, reabrir y borrar --

    [Fact]
    public async Task Cerrar_y_reabrir_piden_motivo_y_una_linea_sin_uso_se_borra()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var linea = await CrearExitoAsync(http, admin, "CIERRA", "Línea que se cierra", VersionFija(hoy, "CONSUMO", "SinLibranza", 0.2m));

        var cerrada = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/close", new { reason = "Sale del portafolio" });
        cerrada.GetProperty("status").GetInt32().Should().Be(3);
        cerrada.GetProperty("admitsNewCredits").GetBoolean().Should().BeFalse();
        var otraVez = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/close", new { reason = "Otra vez" });
        (await CarteraE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Lending.CreditLine.AlreadyClosed");
        (await CarteraE2E.GetAsync(http, admin, Lineas)).EnumerateArray().Should().NotContain(l => l.GetProperty("code").GetString() == "CIERRA",
            "la lista por defecto no trae las cerradas");

        var reabierta = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Lineas}/{linea}/reopen", new { reason = "Vuelve al portafolio" });
        reabierta.GetProperty("status").GetInt32().Should().Be(2, "pasa la usura del día");

        var borrar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Delete, $"{Lineas}/{linea}?reason={Uri.EscapeDataString("Creada por error")}", null);
        borrar.IsSuccessStatusCode.Should().BeTrue(await borrar.Content.ReadAsStringAsync());
        (await CarteraE2E.MandarGetAsync(http, admin, $"{Lineas}/{linea}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "borrada ya no existe");
    }

    // -------------------------------------------------------------------------------------------- SC-018 --

    [Fact]
    public async Task Un_horario_de_cobranza_mas_amplio_que_el_techo_nacional_se_rechaza_con_el_limite_y_su_fuente()
    {
        var (coop, hoy) = await CooperativaAsync();
        using var http = fx.CreateClient();

        var resp = await CarteraE2E.ConClaveAsync(http, coop.TokenAdmin, HttpMethod.Post, "/api/lending/parameters/Cobranza.HorarioLunesAViernes/versions", new
        {
            scopeKind = 0, value = "06:00-20:00", validFrom = hoy, reason = "Horario ampliado de cobranza",
        });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resp.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(resp)).Should().Be("Lending.Parameter.BelowLegalFloor");
        var data = (await CarteraE2E.LeerAsync(resp)).GetProperty("data");
        data.GetProperty("limitCode").GetString().Should().BeOneOf("COBRANZA_HORA_INICIO_LV", "COBRANZA_HORA_FIN_LV");
        data.GetProperty("source").GetString().Should().NotBeNullOrWhiteSpace("el límite dice su fuente");
    }

    // ------------------------------------------------------------------------------------------- ayudantes --

    private static bool Tiene(JsonElement e, string propiedad) => e.TryGetProperty(propiedad, out _);

    private static async Task<List<string>> CodigosAsync(HttpClient http, string token) =>
        (await CarteraE2E.GetAsync(http, token, $"{Lineas}?includeClosed=true")).EnumerateArray().Select(l => l.GetProperty("code").GetString()!)
        .Where(c => c is "CONLIB" or "CONCAJA" or "VARIBR" or "MICRO" or "COMER" or "ROTATIVO" or "EMPLEA" or "SOBRE").ToList();

    private static async Task<JsonElement> LineaPorCodigoAsync(HttpClient http, string token, string codigo)
    {
        var id = (await CarteraE2E.GetAsync(http, token, $"{Lineas}?includeClosed=true")).EnumerateArray()
            .Single(l => l.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
        return await CarteraE2E.GetAsync(http, token, $"{Lineas}/{id}");
    }

    private static int _siguienteNit;

    /// <summary>Una aseguradora (persona jurídica del maestro) por su NIT, que es lo que la hoja Seguros cita.</summary>
    private static async Task<string> AseguradoraAsync(HttpClient http, string token)
    {
        var nit = (860_100_000 + Interlocked.Increment(ref _siguienteNit)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "N", taxId = nit, firstName = "Aseguradora", lastName = "Del Ensayo", businessName = "Aseguradora del Ensayo S.A.",
            email = $"seguros.{nit}@aseguradora.test",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"aseguradora: «{await resp.Content.ReadAsStringAsync()}»");
        return nit;
    }
}
