using System.Globalization;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T071 (US1, escenarios 1 a 4 de la spec; quickstart §2; contracts/api.md §3): las líneas de captación y de aportes por HTTP, en
/// cooperativas aisladas con créditos activados (<see cref="CaptacionesE2E.CooperativaConCreditosAsync"/>).
/// <list type="number">
/// <item>Con un perfil regulatorio que no autoriza captar, activar una línea a la vista responde <c>Lending.Savings.LineNotAllowedByProfile</c> y no
/// guarda nada; con el perfil que lo autoriza, se activa.</item>
/// <item>Una línea a la vista con tramos de saldo cambia su tasa con fecha futura: la consulta a una fecha anterior devuelve la versión anterior y
/// desde ese día la nueva (la causación de cada día usa la que rige ese día, FR-007).</item>
/// <item>En un fondo de empleados, una cuota de aportes y ahorro permanente sobre el tope del ingreso responde
/// <c>Lending.Contributions.InstallmentCapExceeded</c> con el tope y su fuente; la del 3 % pasa.</item>
/// <item>Un código que ya existe en el catálogo (de cualquier naturaleza) responde <c>Catalogo.CodigoDuplicado</c> con el nombre del existente.</item>
/// </list>
/// Exige un despliegue con K8 (las líneas de captación se activan desde K8 y las de aportes desde K7). <b>e2e en la corrida final</b> (decisión del dueño).
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class LineasDeCaptacionTests(CentralIdentityApiFixture fx)
{
    private const string Lineas = "/api/lending/lines";
    private const string Perfil = "/api/core/regulatory-profile";

    private static object ALaVista(DateOnly desde, decimal tasaAlta, string motivo = "Tasa aprobada por el consejo, acta 12 (e2e)") => new
    {
        validFrom = desde, reason = motivo, requiresMembership = true, allowsCoHolders = true, allowsMinors = true, allowsPartialWithdrawals = true,
        rate = new
        {
            kind = "Fixed", tierMode = "WholeBalance",
            tiers = new object[]
            {
                new { balanceFrom = 0m, balanceTo = 999_999.99m, effectiveAnnual = 0.01m },
                new { balanceFrom = 1_000_000m, effectiveAnnual = tasaAlta },
            },
        },
        balanceMethod = "DailyBalance",
        minimums = new { toOpen = 10_000m, toMaintain = 10_000m, toEarnInterest = 100_000m },
        dayCount = "Actual365", rateEquivalence = "Compound",
        rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" },
        settlementPeriodicity = "Monthly",
        tax = new { gmfApplies = true },
        limits = new { maxWithdrawalPerOperation = 20_000_000m, maxWithdrawalPerDay = 30_000_000m, maxCashPerOperation = 10_000_000m },
        inactivity = new { days = 360, keepsAccruing = true },
    };

    private static object Aportes(DateOnly desde, decimal fraccionDelIngreso, Guid? permanente = null, decimal? parteDelPermanente = null) => new
    {
        validFrom = desde, reason = "Estatutos, art. 20 (e2e)",
        minimumNonReducible = new { kind = "FixedAmount", amount = 500_000m },
        installment = new { kind = "IncomeFraction", fraction = fraccionDelIngreso, periodicity = "Monthly", dueDay = 5 },
        permanentSavingsShare = permanente is { } p ? new { depositLinePublicId = p, fraction = parteDelPermanente ?? 0m } : null,
        refundTermDays = 60,
    };

    /// <summary>Corrige el perfil regulatorio vigente desde su misma fecha con lo que se diga.</summary>
    private static async Task CorregirPerfilAsync(HttpClient http, string admin, bool captar, string? tipo = null, string? categoria = null)
    {
        var vigente = (await CarteraE2E.GetAsync(http, admin, Perfil)).GetProperty("current");
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Perfil}/versions", new
        {
            organizationType = tipo ?? vigente.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2, category = categoria,
            validFrom = DateOnly.Parse(vigente.GetProperty("validFrom").GetString()!, CultureInfo.InvariantCulture),
            source = "Certificado de la Supersolidaria (e2e)", reason = captar ? "La cooperativa capta ahorro (e2e)" : "La cooperativa no capta ahorro (e2e)",
            supersedesPublicId = vigente.GetProperty("versionPublicId").GetGuid(), mayTakeDeposits = captar,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"corregir el perfil: «{await resp.Content.ReadAsStringAsync()}»");
    }

    [Fact]
    public async Task Escenario_1_sin_perfil_que_autorice_captar_la_linea_a_la_vista_no_se_activa_y_con_el_si()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "lineas_perfil");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CorregirPerfilAsync(http, admin, captar: false);

        var rechazada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = "AHVISTA", name = "Ahorro a la vista", depositClass = "Demand", active = true, firstVersion = ALaVista(hoy, 0.03m),
        });

        rechazada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CarteraE2E.CodigoDeErrorAsync(rechazada)).Should().Be("Lending.Savings.LineNotAllowedByProfile");
        (await CarteraE2E.GetAsync(http, admin, $"{Lineas}?nature=Deposit&includeClosed=true")).GetArrayLength().Should().Be(0, "no se guarda nada");

        await CorregirPerfilAsync(http, admin, captar: true);
        var activa = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = "AHVISTA", name = "Ahorro a la vista", depositClass = "Demand", active = true, firstVersion = ALaVista(hoy, 0.03m),
        });
        activa.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await activa.Content.ReadAsStringAsync()}»");
        var linea = await CarteraE2E.LeerAsync(activa);
        linea.GetProperty("status").GetInt32().Should().Be(2, "activa");
        linea.GetProperty("nature").GetInt32().Should().Be(2, "captación");
    }

    [Fact]
    public async Task Escenario_2_el_cambio_de_tasa_con_fecha_futura_deja_la_version_anterior_hasta_la_vispera()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "lineas");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CorregirPerfilAsync(http, admin, captar: true);

        var creada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = "AHTRAMOS", name = "Ahorro a la vista por tramos", depositClass = "Demand", active = true, firstVersion = ALaVista(hoy, 0.03m),
        });
        creada.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await creada.Content.ReadAsStringAsync()}»");
        var id = (await CarteraE2E.LeerAsync(creada)).GetProperty("publicId").GetGuid();
        var desde = hoy.AddDays(15);

        var version = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{id}/deposit-versions",
            ALaVista(desde, 0.035m, "La tasa del tramo alto sube desde el día 15 (e2e)"));
        version.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await version.Content.ReadAsStringAsync()}»");

        var antes = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{id}?date={desde.AddDays(-1):yyyy-MM-dd}");
        var despues = await CarteraE2E.GetAsync(http, admin, $"{Lineas}/{id}?date={desde:yyyy-MM-dd}");
        antes.GetProperty("depositVersion").GetProperty("versionNumber").GetInt32().Should().Be(1);
        antes.GetProperty("depositVersion").GetProperty("validTo").GetString().Should().Be(desde.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        TasaDelTramoAlto(antes).Should().Be(0.03m);
        despues.GetProperty("depositVersion").GetProperty("versionNumber").GetInt32().Should().Be(2);
        TasaDelTramoAlto(despues).Should().Be(0.035m);
        despues.GetProperty("versions").GetArrayLength().Should().Be(2);

        var retroactiva = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/{id}/deposit-versions", ALaVista(hoy.AddDays(-1), 0.04m));
        (await CarteraE2E.CodigoDeErrorAsync(retroactiva)).Should().Be("Lending.DepositLine.Retroactive");
        var lista = await CarteraE2E.GetAsync(http, admin, $"{Lineas}?nature=Deposit");
        lista.EnumerateArray().Should().Contain(l => l.GetProperty("code").GetString() == "AHTRAMOS");
    }

    private static decimal TasaDelTramoAlto(JsonElement linea) =>
        linea.GetProperty("depositVersion").GetProperty("tiers")[1].GetProperty("effectiveAnnual").GetDecimal();

    [Fact]
    public async Task Escenario_3_en_un_fondo_de_empleados_la_cuota_sobre_el_tope_se_rechaza_con_el_tope_y_su_fuente()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "lineas_fondo");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CorregirPerfilAsync(http, admin, captar: true, tipo: "FONDO_EMPLEADOS", categoria: "BASICA");

        var permanente = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = "AHPERM", name = "Ahorro permanente", depositClass = "Permanent", active = true,
            firstVersion = new
            {
                validFrom = hoy, reason = "Alta (e2e)", allowsPartialWithdrawals = false, rate = new { kind = "Fixed", effectiveAnnual = 0.02m },
                permanent = new { withdrawableOnlyOnMembershipWithdrawal = true },
            },
        });
        permanente.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await permanente.Content.ReadAsStringAsync()}»");
        var lineaPermanente = (await CarteraE2E.LeerAsync(permanente)).GetProperty("publicId").GetGuid();

        var sobreElTope = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/contribution", new
        {
            code = "APORTES", name = "Aportes sociales", active = true, firstVersion = Aportes(hoy, 0.12m, lineaPermanente, 0.5m),
        });
        sobreElTope.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await CarteraE2E.LeerAsync(sobreElTope);
        error.GetProperty("code").GetString().Should().Be("Lending.Contributions.InstallmentCapExceeded");
        error.GetProperty("data").GetProperty("capPercent").GetDecimal().Should().BePositive();
        error.GetProperty("data").GetProperty("source").GetString().Should().NotBeNullOrWhiteSpace();

        var tresPorCiento = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/contribution", new
        {
            code = "APORTES", name = "Aportes sociales", active = true, firstVersion = Aportes(hoy, 0.03m),
        });
        tresPorCiento.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await tresPorCiento.Content.ReadAsStringAsync()}»");
        var aportes = await CarteraE2E.LeerAsync(tresPorCiento);
        aportes.GetProperty("contributionVersion").GetProperty("installment").GetProperty("fraction").GetDecimal().Should().Be(0.03m);
    }

    [Fact]
    public async Task Escenario_4_un_codigo_que_ya_existe_en_el_catalogo_es_CodigoDuplicado_con_el_nombre_del_existente()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "lineas");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var creditos = (await CarteraE2E.GetAsync(http, admin, "/api/lending/credit-lines?includeClosed=true")).EnumerateArray().ToList();
        string codigo, nombre;
        if (creditos.Count > 0)
        {
            codigo = creditos[0].GetProperty("code").GetString()!;
            nombre = creditos[0].GetProperty("name").GetString()!;
        }
        else
        {
            var aportes = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/contribution", new
            {
                code = "APDUP", name = "Aportes de la prueba de duplicado", active = false, firstVersion = Aportes(hoy, 0.03m),
            });
            aportes.StatusCode.Should().Be(HttpStatusCode.Created, $"«{await aportes.Content.ReadAsStringAsync()}»");
            (codigo, nombre) = ("APDUP", "Aportes de la prueba de duplicado");
        }

        var repetida = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Lineas}/deposit", new
        {
            code = codigo.ToLowerInvariant(), name = "Otra", depositClass = "Demand", active = false, firstVersion = ALaVista(hoy, 0.03m),
        });

        repetida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await CarteraE2E.LeerAsync(repetida);
        error.GetProperty("code").GetString().Should().Be("Catalogo.CodigoDuplicado");
        error.GetProperty("message").GetString().Should().Contain(nombre);
    }
}
