using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// T160 (feature 014, US9; quickstart US9; FR-113 a FR-118; colección «Captaciones e2e», los dos motores por <c>DB_PROVIDER</c>): la migración de los
/// saldos de aportes y su activación por las rutas, en una cooperativa aislada con los créditos activados y los aportes sin activar. El corte C es ayer (la
/// línea de aportes nace con el reloj de la suite en C) y la activación es hoy, C + 1.
/// <list type="number">
/// <item>US9-1: 40 cuentas de aportes con un documento inexistente en la fila 37: la revisión lo lista con fila y columna y no se guarda nada; corregida,
/// se guarda la tanda.</item>
/// <item>US9-2: aplicar calcula el cuadre contra Contabilidad —sin reglas para los aportes, cada combinación queda «sin regla» y con diferencia—; activar sin
/// aceptarlas responde <c>DifferencesNotAccepted</c>; la secretaría las acepta con el punto del acta y la activación procede.</item>
/// <item>US9-3: una carga nueva aplicada deja la anterior reemplazada, sin borrarla.</item>
/// <item>US9-4: cada cuenta nace con su saldo inicial por concepto fechado al corte y ningún hecho contable de saldo: sólo el informativo
/// <c>CaptacionesActivadas</c>.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escribe y compila con su historia y corre una sola vez al final de todas las fases. Exige el
/// despliegue con K11 (como la fixture de la colección). (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public sealed class MigracionDeCaptacionesTests(CentralIdentityApiFixture fx)
{
    private const string Nombre = "migracion_us9";
    private const string Cargas = "/api/lending/deposits/migration/loads";
    private const string Activacion = "/api/lending/deposits/activation";
    private const int Cuentas = 40;

    private static int _documento;

    private static string Documento() => (6_900_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(CultureInfo.InvariantCulture);

    private static string Fecha(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static Dictionary<string, string?> Fila(params (string Columna, string? Valor)[] celdas) => celdas.ToDictionary(c => c.Columna, c => c.Valor);

    internal static byte[] Csv(string parte, IEnumerable<IReadOnlyDictionary<string, string?>> filas)
    {
        var columnas = DefinicionesDeMigracionDeCaptaciones.Buscar(parte)!.Definicion!.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        var texto = new StringBuilder();
        texto.AppendLine(string.Join(';', columnas));
        foreach (var fila in filas)
            texto.AppendLine(string.Join(';', columnas.Select(c => fila.TryGetValue(c, out var v) ? v ?? string.Empty : string.Empty)));
        return Encoding.UTF8.GetBytes(texto.ToString());
    }

    internal static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, Guid carga, string parte, byte[] contenido, string modo = "apply",
        bool reemplazar = false)
    {
        var formulario = new MultipartFormDataContent();
        var bytes = new ByteArrayContent(contenido);
        bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        formulario.Add(bytes, "file", $"{parte}.csv");
        formulario.Add(new StringContent("Saldos iniciales de aportes (e2e)"), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{Cargas}/{carga}/parts/{parte}/import?mode={modo}{(reemplazar ? "&replace=true" : string.Empty)}")
        {
            Content = formulario,
        };
        peticion.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }

    internal static async Task<(string Documento, Guid Persona)> AsociadoAsync(HttpClient http, string admin, int i)
    {
        var documento = Documento();
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = $"Asociado{i}", lastName = "Migrado", personType = "01", dateOfBirth = new DateOnly(1980, 1, 1),
                email = $"asociado{i}.{documento}@migracion.test",
            },
            associate = new { joinDate = new DateOnly(2018, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {i}: «{await alta.Content.ReadAsStringAsync()}»");
        return (documento, (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid());
    }

    private static Dictionary<string, string?> Aporte(string documento, string numero, string agencia, string ordinarios = "1000000") => Fila(
        (C.Asociado, documento), (C.Numero, numero), (C.Oficina, agencia), (C.Linea, "APOMIG"), (C.FechaDeVinculacion, "2018-01-15"),
        (C.AportesMinimos, "100000"), (C.AportesOrdinarios, ordinarios));

    private static async Task<Guid> CrearCargaAsync(HttpClient http, string token, DateOnly corte)
    {
        var alta = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Cargas, new { nature = "Contribution", cutoffDate = corte, notes = "Saldos de aportes (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"crear la carga: «{await alta.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
    }

    [Fact]
    public async Task Los_saldos_de_aportes_se_cargan_con_revision_previa_cuadran_con_acta_y_nacen_sin_hechos_al_dia_siguiente_del_corte()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, Nombre);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var corte = hoy.AddDays(-1);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var agencia = await MigracionE2E.OficinaAsync(http, admin, coop.TenantPublicId, "MIGAPO");

        // La línea de aportes con versión vigente desde antes del corte de la carga de ensayo (el día anterior al corte), con el reloj de la suite ese día.
        using (CausacionE2E.RelojEn(fx, corte.AddDays(-1)))
        {
            var linea = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/lines/contribution", new
            {
                code = "APOMIG", name = "Aportes migrados", active = true,
                firstVersion = new
                {
                    validFrom = corte.AddDays(-1), reason = "Estatutos, art. 20 (e2e)", minimumNonReducible = new { kind = "FixedAmount", amount = 100_000m },
                    installment = new { kind = "FixedAmount", amount = 50_000m, periodicity = "Monthly", dueDay = 5 }, allowsExtraordinary = true,
                    refundTermDays = 60,
                },
            });
            linea.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea: «{await linea.Content.ReadAsStringAsync()}»");
        }
        await CarteraE2E.RecaudoYTesoreriaRealesAsync(fx, http, coop.Cooperativa, hoy);
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);

        var asociados = new List<(string Documento, Guid Persona)>();
        for (var i = 1; i <= Cuentas; i++) asociados.Add(await AsociadoAsync(http, admin, i));

        // ------------------------------------------------------------------------------------- US9-3: una carga de ensayo --
        var ensayo = await CrearCargaAsync(http, admin, corte.AddDays(-1));
        var importadoEnsayo = await ImportarAsync(http, admin, ensayo, P.ContributionAccounts, Csv(P.ContributionAccounts, [Aporte(asociados[0].Documento, "APE-1", agencia)]));
        importadoEnsayo.StatusCode.Should().Be(HttpStatusCode.OK, await importadoEnsayo.Content.ReadAsStringAsync());
        var aplicarEnsayo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargas}/{ensayo}/apply", new { reason = "Ensayo (e2e)" });
        aplicarEnsayo.StatusCode.Should().Be(HttpStatusCode.OK, await aplicarEnsayo.Content.ReadAsStringAsync());

        // ------------------------------------------------------------------------------------ US9-1: el error de la fila 37 --
        var carga = await CrearCargaAsync(http, admin, corte);
        var filas = asociados.Select((a, i) => Aporte(a.Documento, $"AP-{i + 1:000}", agencia)).ToList();
        filas[35] = Aporte("99999999999", "AP-036", agencia);
        var revision = await ImportarAsync(http, admin, carga, P.ContributionAccounts, Csv(P.ContributionAccounts, filas), "review");
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var revisada = await CarteraE2E.LeerAsync(revision);
        revisada.GetProperty("valid").GetBoolean().Should().BeFalse();
        var error = MigracionE2E.Errores(revisada).Should().ContainSingle().Subject;
        error.GetProperty("row").GetInt32().Should().Be(37);
        error.GetProperty("column").GetString().Should().Be(C.Asociado);
        error.GetProperty("code").GetString().Should().Be("Import.Cell.NotFound");
        (await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}")).GetProperty("parts").GetArrayLength().Should().Be(0, "revisar no guarda nada");

        filas[35] = Aporte(asociados[35].Documento, "AP-036", agencia);
        var corregida = await ImportarAsync(http, admin, carga, P.ContributionAccounts, Csv(P.ContributionAccounts, filas));
        corregida.StatusCode.Should().Be(HttpStatusCode.OK, await corregida.Content.ReadAsStringAsync());

        // ------------------------------------------------------------------- US9-2: el cuadre con diferencias y el acta --
        var aplicar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargas}/{carga}/apply", new { reason = "Saldos de aportes aprobados (e2e)" });
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
        var aplicada = await CarteraE2E.LeerAsync(aplicar);
        aplicada.GetProperty("replacedLoadPublicId").GetGuid().Should().Be(ensayo, "US9-3: la carga de ensayo queda reemplazada");
        aplicada.GetProperty("balanceCheck").GetProperty("isBalanced").GetBoolean().Should().BeFalse();
        (await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{ensayo}")).GetProperty("status").GetInt32().Should().Be(3, "reemplazada, no borrada");

        var figuras = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Put, $"{Cargas}/{carga}/reference-figures", new
        {
            figures = new[] { new { lineCode = "APOMIG", concept = "ContributionOrdinary", amount = 40_000_000m, source = "Balance de prueba al corte (e2e)" } },
        });
        figuras.StatusCode.Should().Be(HttpStatusCode.OK, await figuras.Content.ReadAsStringAsync());
        (await CarteraE2E.LeerAsync(figuras)).GetProperty("comparison").EnumerateArray()
            .Single(c => c.GetProperty("concept").GetString() == "ContributionOrdinary").GetProperty("difference").GetDecimal().Should().Be(0m);

        var punto = await CarteraE2E.ActaDeActivacionAsync(http, coop.Cooperativa, carga, hoy);
        object Orden() => new
        {
            nature = "Contribution", migrationLoadPublicId = carga, activationDate = hoy, sessionItemPublicId = punto, acknowledgeWarnings = true,
            reason = "Activación de aportes con la carga aprobada (e2e)",
        };
        var fueraDeFecha = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, new
        {
            nature = "Contribution", migrationLoadPublicId = carga, activationDate = hoy.AddDays(1), sessionItemPublicId = punto, acknowledgeWarnings = true, reason = "x",
        });
        (await CarteraE2E.CodigoDeErrorAsync(fueraDeFecha)).Should().Be("Lending.Activation.CutoffInvalid", await fueraDeFecha.Content.ReadAsStringAsync());
        var sinAceptar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden());
        (await CarteraE2E.CodigoDeErrorAsync(sinAceptar)).Should().Be("Lending.Activation.DifferencesNotAccepted");

        var cuadre = await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}/balance-check");
        var diferencias = cuadre.GetProperty("differences").EnumerateArray().Where(d => d.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null)
            .Select(d => d.GetProperty("publicId").GetGuid()).ToList();
        diferencias.Should().NotBeEmpty();
        var secretaria = await UsuariosDeCartera.IngresarAsync(http, usuarios.Secretaria);
        var aceptar = await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, $"{Cargas}/{carga}/differences/accept", new
        {
            differencePublicIds = diferencias, sessionItemPublicId = punto, reason = "Diferencias conciliadas con la contadora (e2e)",
        });
        aceptar.StatusCode.Should().Be(HttpStatusCode.OK, await aceptar.Content.ReadAsStringAsync());

        // ------------------------------------------------------------------------- US9-4: el saldo inicial sin hechos --
        var activar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, Orden());
        activar.StatusCode.Should().Be(HttpStatusCode.Created, await activar.Content.ReadAsStringAsync());
        var activacion = await CarteraE2E.LeerAsync(activar);
        activacion.GetProperty("accounts").GetInt32().Should().Be(Cuentas);
        activacion.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("type").GetString()).Should().Equal("CaptacionesActivadas");
        (await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}")).GetProperty("status").GetInt32().Should().Be(4);
        var estado = await CarteraE2E.GetAsync(http, admin, $"{Activacion}?nature=Contribution");
        estado.GetProperty("status").GetString().Should().Be("Activated");

        var (transacciones, deSaldoInicial, hechosDeSaldo) = await MigracionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, async db =>
        {
            var filasDelLibro = await db.MemberAccountTransactions.AsNoTracking().ToListAsync();
            var hechos = await db.IntegrationMessages.AsNoTracking()
                .CountAsync(m => m.Type == "AporteRecibido" || m.Type == "ConsignacionEnCuentaAplicada");
            return (filasDelLibro.Count, filasDelLibro.Count(t => t.Kind == Domain.Enums.Lending.MemberAccountTransactionKind.OpeningBalance
                                                                  && t.ValueDate == corte && t.AccountingDate == corte && t.MessagePublicId == null), hechos);
        });
        transacciones.Should().Be(Cuentas * 2, "un saldo inicial por concepto: mínimos y ordinarios");
        deSaldoInicial.Should().Be(transacciones);
        hechosDeSaldo.Should().Be(0, "Contabilidad ya tiene esos saldos (FR-114)");
    }

    /// <summary>
    /// US6 (pendiente de la fase 10; plantillas-y-archivos.md §4.3; research E148): un CDAT vigente al corte se carga en la parte <c>term-deposits</c> con el
    /// interés causado que regenera su plan (con cinco pesos de más se rechaza al aplicar con <c>Lending.TermDeposit.ScheduleMismatch</c>) y, al activar las
    /// captaciones con la carga, nace con su versión 1 sellada por la migración, su plan de seis períodos y su saldo inicial sin hechos.
    /// </summary>
    [Fact]
    public async Task Un_CDAT_vigente_al_corte_se_migra_con_su_plan_regenerado_y_nace_con_su_version_sellada()
    {
        var coop = await CaptacionesE2E.CooperativaConCreditosAsync(fx, "migracion_cdat");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var corte = hoy.AddDays(-1);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var agencia = await MigracionE2E.OficinaAsync(http, admin, coop.TenantPublicId, "MIGCDT");
        // La línea a la vista exige el perfil que autoriza captar: se deja antes de crearla (y otra vez, con la matriz, al activar).
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);
        var inicio = corte.AddDays(-50);

        using (CausacionE2E.RelojEn(fx, corte))
        {
            await MovimientosTests.LineaALaVistaAsync(http, admin, corte, "AHMIGC");
            var cdat = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/lines/deposit", new
            {
                code = "CDMIG", name = "CDAT migrado", depositClass = "TermDeposit", active = true,
                firstVersion = new
                {
                    validFrom = corte, reason = "Reglamento de CDAT (e2e)", allowsPartialWithdrawals = false, rate = new { kind = "Fixed", effectiveAnnual = 0.10m },
                    balanceMethod = "DailyBalance", minimums = new { toOpen = 1_000_000m, toMaintain = 0m, toEarnInterest = 0m }, dayCount = "Actual360",
                    rateEquivalence = "Compound", rounding = new { unit = 1m, mode = "AwayFromZero", moment = "PerTotal" }, settlementPeriodicity = "SinglePayment",
                    tax = new { gmfApplies = false },
                    termDeposit = new { minTermDays = 30, maxTermDays = 720, interestPeriodicities = new[] { "SinglePayment", "Monthly" }, timings = new[] { "Arrears" } },
                },
            });
            cdat.StatusCode.Should().Be(HttpStatusCode.Created, $"la línea de CDAT: «{await cdat.Content.ReadAsStringAsync()}»");
        }
        await CarteraE2E.RecaudoYTesoreriaRealesAsync(fx, http, coop.Cooperativa, hoy);
        await CaptacionesE2E.RequisitosDeCaptacionesAsync(http, admin, hoy);
        await CaptacionesE2E.MatrizDeCdatAsync(http, admin, hoy);
        var (documento, _) = await AsociadoAsync(http, admin, 900);

        // El interés causado al corte que regenera el plan (mensual vencido, base 360), calculado con el mismo motor que valida la carga.
        var plan = Domain.Lending.Deposits.PlanDeCdat.Generar(new Domain.Lending.Deposits.CondicionesDeCdat(10_000_000m, 0.10m, inicio, 180,
                Domain.Enums.Lending.RepaymentPeriodicity.Monthly, Domain.Enums.Lending.InterestTiming.Arrears, Domain.Enums.Lending.DayCountConvention.Actual360,
                new Domain.Lending.RedondeoDeCartera(1m, Domain.Enums.Lending.RoundingMode.AwayFromZero, Domain.Enums.Lending.RoundingMoment.PerTotal)),
            new Domain.Common.Calendar.CalendarioHabil([], [DayOfWeek.Saturday, DayOfWeek.Sunday]));
        var pagados = plan.Periodos.Count(p => p.FechaDePago <= corte);
        var causado = Application.Lending.Deposits.Migration.CdatMigrado.EsperadoAlCorte(plan, corte);
        Dictionary<string, string?> Cdat(decimal interes) => Fila((C.Numero, "CDM-1"), (C.Titular, documento), (C.Oficina, agencia), (C.Linea, "CDMIG"),
            (C.NumeroDeTitulo, "T-CDM-1"), (C.Capital, "10000000"), (C.FechaDeConstitucion, Fecha(inicio)), (C.FechaDeInicioVigente, Fecha(inicio)),
            (C.PlazoDias, "180"), (C.FechaDeVencimiento, Fecha(plan.Vencimiento)), (C.TasaPactadaEA, "10"), (C.PeriodicidadDeIntereses, "Monthly"),
            (C.Modalidad, "Arrears"), (C.PeriodosPagados, pagados.ToString(CultureInfo.InvariantCulture)),
            (C.InteresCausadoNoPagado, interes.ToString("0.######", CultureInfo.InvariantCulture).Replace(".", ",")), (C.DestinoDeIntereses, "Account"),
            (C.CuentaDestino, "AHM-1"), (C.InstruccionAlVencimiento, "Rollover"));

        var alta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Cargas, new { nature = "Deposit", cutoffDate = corte, notes = "Saldos de captaciones con CDAT (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var carga = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
        (await ImportarAsync(http, admin, carga, P.MemberAccounts, Csv(P.MemberAccounts, [Fila((C.Numero, "AHM-1"), (C.Titular, documento), (C.Oficina, agencia),
            (C.Linea, "AHMIGC"), (C.Clase, "Demand"), (C.FechaDeApertura, "2020-05-14"), (C.Estado, "Active"), (C.SaldoCapital, "500000"))])))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImportarAsync(http, admin, carga, P.TermDeposits, Csv(P.TermDeposits, [Cdat(causado + 5m)]))).StatusCode.Should().Be(HttpStatusCode.OK);

        var conError = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargas}/{carga}/apply", new { reason = "Carga con el interés mal" });
        conError.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await conError.Content.ReadAsStringAsync()).Should().Contain("Lending.TermDeposit.ScheduleMismatch");

        (await ImportarAsync(http, admin, carga, P.TermDeposits, Csv(P.TermDeposits, [Cdat(causado)]), reemplazar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        var aplicar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cargas}/{carga}/apply", new { reason = "Saldos de captaciones aprobados (e2e)" });
        aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());

        var punto = await CarteraE2E.ActaDeActivacionAsync(http, coop.Cooperativa, carga, hoy);
        var diferencias = (await CarteraE2E.GetAsync(http, admin, $"{Cargas}/{carga}/balance-check")).GetProperty("differences").EnumerateArray()
            .Where(d => d.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null).Select(d => d.GetProperty("publicId").GetGuid()).ToList();
        if (diferencias.Count > 0)
        {
            var secretaria = await UsuariosDeCartera.IngresarAsync(http, usuarios.Secretaria);
            (await CarteraE2E.ConClaveAsync(http, secretaria, HttpMethod.Post, $"{Cargas}/{carga}/differences/accept", new
            {
                differencePublicIds = diferencias, sessionItemPublicId = punto, reason = "Diferencias conciliadas con la contadora (e2e)",
            })).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var activar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Activacion, new
        {
            nature = "Deposit", migrationLoadPublicId = carga, activationDate = hoy, sessionItemPublicId = punto, acknowledgeWarnings = true,
            reason = "Activación de captaciones con la carga aprobada (e2e)",
        });
        activar.StatusCode.Should().Be(HttpStatusCode.Created, await activar.Content.ReadAsStringAsync());

        var (version, periodos, interesInicial, hechos) = await MigracionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, async db =>
        {
            var cuenta = await db.MemberAccounts.AsNoTracking().SingleAsync(a => a.Number == "CDM-1");
            var v = await db.MemberAccountTermsVersions.AsNoTracking().SingleAsync(x => x.MemberAccountId == cuenta.Id);
            var p = await db.TermDepositScheduleItems.AsNoTracking().CountAsync(x => x.TermsVersionId == v.Id);
            var i = await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == Domain.Enums.Lending.MemberAccountConcept.InterestPayable).SumAsync(t => t.Amount);
            var h = await db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == "CdatConstituido");
            return (v, p, i, h);
        });
        (version.Cause, version.VersionNumber, version.MaturityDate).Should().Be((Domain.Enums.Lending.AccountTermsCause.Migration, 1, (DateOnly?)plan.Vencimiento));
        periodos.Should().Be(6);
        interesInicial.Should().Be(causado);
        hechos.Should().Be(0, "el CDAT migrado nace sin hechos (FR-114)");
    }
}
