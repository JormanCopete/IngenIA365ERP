using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T125 (US4; quickstart §2; contracts/api.md §4 y §9; FR-090, FR-128): los cargos y el débito automático por HTTP.
/// <list type="bullet">
/// <item>Un cargo periódico de 5.000 en la versión de la línea lo causa el ciclo diario el último día del mes, una sola vez, y se ve en la ficha.</item>
/// <item>Un cargo por evento de pantalla (certificado adicional) lo registra una persona y ella misma no lo condona (segregación).</item>
/// <item>Con una autorización de débito automático, el ciclo diario del vencimiento de la primera cuota debita la cuenta y paga el crédito con la clave
/// <c>CAR:debito-automatico:…</c>; correrlo otra vez no debita dos veces.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase.
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class CargosYDebitoAutomaticoTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";

    private static readonly object[] CatalogoDeCargos =
    [
        new
        {
            code = "CUOTAMAN", name = "Cuota de manejo", kind = "MaintenanceFee", trigger = "Periodic", enabled = true, calculation = "FixedAmount",
            fixedAmount = 5_000m, periodicity = "Monthly", uncollectedPolicy = "KeepPending",
        },
        new
        {
            code = "CERTADIC", name = "Certificado adicional", kind = "ExtraDocument", trigger = "ExtraDocument", enabled = true, calculation = "FixedAmount",
            fixedAmount = 3_000m, uncollectedPolicy = "KeepPending",
        },
    ];

    [Fact]
    public async Task El_cargo_periodico_lo_causa_el_ciclo_el_ultimo_dia_del_mes_una_sola_vez()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHCARGO", CatalogoDeCargos);
        var catalogo = await CarteraE2E.GetAsync(http, admin, $"/api/lending/lines/{linea}/charges");
        catalogo.GetArrayLength().Should().Be(2);

        var ivan = await MovimientosTests.AsociadoAsync(http, admin, "Ivan");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, ivan);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, ivan, 100_000m, hoy);
        var fin = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        // El ciclo de un día ya terminado se rechaza (Lending.ProcessRun.AlreadyCompleted): la repetición no existe, y el cargo sale una sola vez.
        using (CausacionE2E.RelojEn(fx, fin)) await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);

        var cargos = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/charges");
        var items = cargos.GetProperty("items").EnumerateArray().ToList();
        items.Should().ContainSingle(c => c.GetProperty("code").GetString() == "CUOTAMAN");
        var cuota = items.Single(c => c.GetProperty("code").GetString() == "CUOTAMAN");
        cuota.GetProperty("amount").GetDecimal().Should().Be(5_000m);
        cuota.GetProperty("status").GetInt32().Should().Be((int)IngenIA365ERP.Domain.Enums.Lending.AccountChargeStatus.Collected);
    }

    private static object[] CuotaDe(decimal valor) =>
    [
        new
        {
            code = "CUOTAMAN", name = "Cuota de manejo", kind = "MaintenanceFee", trigger = "Periodic", enabled = true, calculation = "FixedAmount",
            fixedAmount = valor, periodicity = "Monthly", uncollectedPolicy = "KeepPending",
        },
    ];

    /// <summary>
    /// Respuesta 65 B del dueño (E255): cancelar una cuenta con un cargo por cobrar lo cobra del saldo dentro de la misma cancelación y entrega lo restante;
    /// si el saldo no alcanza a cubrirlo, la cancelación se rechaza diciendo cuál es el cargo (<c>PendingCharges</c>) y no queda nada a medias.
    /// </summary>
    [Fact]
    public async Task R65_cancelar_con_un_cargo_por_cobrar_lo_cobra_del_saldo_y_entrega_el_resto()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos65");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var lineaCargo = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHC65", CuotaDe(2_000m), minimoParaMantener: 3_000m);
        var lineaDestino = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHD65");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Olga");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, lineaCargo, titular, exenta: true);
        var destino = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, lineaDestino, titular);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 4_000m, hoy);
        var fin = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));

        using (CausacionE2E.RelojEn(fx, fin))
        {
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);
            var pendientes = (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/charges")).GetProperty("items").EnumerateArray().ToList();
            pendientes.Should().ContainSingle().Which.GetProperty("status").GetInt32().Should().Be((int)AccountChargeStatus.Pending,
                "el mínimo de la línea impide cobrarlo del capital");

            var previa = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/close-preview");
            previa.GetProperty("blockers").GetArrayLength().Should().Be(0, "el saldo cubre el cargo: ya no impide cancelar");
            previa.GetProperty("pendingCharges").GetDecimal().Should().Be(2_000m);

            var cierre = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/close", new
            {
                delivery = new { kind = "TransferToAccount", targetAccountPublicId = destino }, signatures = new[] { titular }, reason = "Retiro del servicio (e2e)",
            });
            cierre.StatusCode.Should().Be(HttpStatusCode.Created, await cierre.Content.ReadAsStringAsync());
            var resultado = await CarteraE2E.LeerAsync(cierre);
            resultado.GetProperty("status").GetInt32().Should().Be((int)MemberAccountStatus.Closed);
            resultado.GetProperty("delivered").GetDecimal().Should().BeInRange(2_000m, 2_100m, "se entrega lo que queda después de cobrar los 2.000 del cargo");

            var cargos = (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/charges")).GetProperty("items").EnumerateArray().ToList();
            cargos.Should().ContainSingle().Which.GetProperty("status").GetInt32().Should().Be((int)AccountChargeStatus.Collected);
            var cobros = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/operations?kind=ChargeCollection");
            cobros.GetProperty("items").GetArrayLength().Should().Be(1, "el cobro va en su propia operación CGC-");
        }
    }

    [Fact]
    public async Task R65_el_cargo_que_el_saldo_no_cubre_impide_cancelar_y_no_deja_nada_a_medias()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos65b");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var lineaCargo = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHC65B", CuotaDe(5_000m));
        var lineaDestino = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHD65B");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Pablo");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, lineaCargo, titular, exenta: true);
        var destino = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, lineaDestino, titular);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, titular, 4_000m, hoy);
        var fin = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));

        using (CausacionE2E.RelojEn(fx, fin))
        {
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId);
            var cierre = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/close", new
            {
                delivery = new { kind = "TransferToAccount", targetAccountPublicId = destino }, signatures = new[] { titular }, reason = "Retiro del servicio (e2e)",
            });
            (await CarteraE2E.CodigoDeErrorAsync(cierre)).Should().Be("Lending.Savings.CloseBlocked");
            (await cierre.Content.ReadAsStringAsync()).Should().Contain("PendingCharges");
            var cargos = (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/charges")).GetProperty("items").EnumerateArray().ToList();
            cargos.Should().ContainSingle().Which.GetProperty("status").GetInt32().Should().Be((int)AccountChargeStatus.Pending, "la cancelación rechazada no cobró nada");
            (await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/operations?kind=ChargeCollection")).GetProperty("items").GetArrayLength().Should().Be(0);
        }
    }

    [Fact]
    public async Task El_cargo_por_evento_de_pantalla_no_lo_condona_quien_lo_registro()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "cargos");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHEVENTO", CatalogoDeCargos);
        var juana = await MovimientosTests.AsociadoAsync(http, admin, "Juana");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, juana);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, juana, 50_000m, hoy);

        var alta = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges",
            new { chargeCode = "CERTADIC", eventReference = "CERT-77", reason = "Certificado bancario adicional pedido por la titular" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var cargo = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var condonar = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges/{cargo}/waive",
            new { reason = "Cortesía comercial (e2e)" });
        (await CarteraE2E.CodigoDeErrorAsync(condonar)).Should().Be("Lending.Segregation.Conflict");

        var ajeno = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Cuentas}/{cuenta}/charges",
            new { chargeCode = "NOEXISTE", reason = "Un cargo que la línea no tiene" });
        (await CarteraE2E.CodigoDeErrorAsync(ajeno)).Should().Be("Lending.Savings.ChargeNotInLine");
    }

    [Fact]
    public async Task El_debito_automatico_paga_la_cuota_del_credito_desde_la_cuenta_una_sola_vez()
    {
        var des = await DesembolsoE2E.EscenarioAsync(fx, "cap_debito");
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "debito");
        cap.TenantPublicId.Should().Be(des.Coop.TenantPublicId, "es la misma cooperativa aislada (cap_debito)");
        using var http = fx.CreateClient();
        var admin = des.Coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, des.Coop);
        var (_, deudor, desembolso) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, des, "Debito", 2_000_000m);
        var credito = desembolso.GetProperty("loanPublicId").GetGuid();
        await ConFechaDeNacimientoAsync(http, admin, deudor);
        await OriginacionE2E.AutorizarAsync(http, admin, deudor, "GeneralTreatment"); // abrir la cuenta exige la autorización de tratamiento de datos
        var (_, vence, total) = await PagosE2E.CuotaAsync(http, admin, credito);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, des.Hoy, "AHDEBITO");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, des.Usuarios.O1.AgenciaPublicId, linea, deudor);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, deudor, total + 500_000m, des.Hoy);

        var mandato = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/direct-debits", new
        {
            accountPublicId = cuenta, scope = "AllLoansOfHolder", allowsPartial = false, validFrom = des.Hoy,
            authorizationAttachmentPublicId = Guid.NewGuid(),
        });
        mandato.StatusCode.Should().Be(HttpStatusCode.Created, await mandato.Content.ReadAsStringAsync());

        using (CausacionE2E.RelojEn(fx, vence)) await CausacionE2E.CicloDeHoyAsync(fx, http, admin, des.Coop.TenantPublicId);
        using (CausacionE2E.RelojEn(fx, vence))
            (await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, CausacionE2E.Corridas, new { kind = "DailyCycle" }))
                .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "el ciclo del día ya terminó: no se corre otra vez");

        var debitos = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/operations?kind=DirectDebit");
        debitos.GetProperty("items").GetArrayLength().Should().Be(1, "correr el ciclo otra vez no debita dos veces");
        debitos.GetProperty("items")[0].GetProperty("amount").GetDecimal().Should().Be(total);
        var lista = await CarteraE2E.GetAsync(http, admin, $"/api/lending/direct-debits?accountPublicId={cuenta}");
        lista.EnumerateArray().Single().GetProperty("lastDebits").GetArrayLength().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// E249 (decisión del dueño del 2026-10-21): un crédito se paga por libranza o nómina, o por débito automático, nunca por las dos.
    /// (1) A un crédito por libranza no se le registra un débito automático (422 <c>Lending.DirectDebit.LoanOnPayroll</c>); la autorización «todos mis
    /// créditos» sí se registra, pero el ciclo salta el crédito con libranza el día de su cuota: el débito automático con <c>TodosLosCanales</c> no duplica el
    /// descuento. (2) Un crédito con un débito automático vigente no pasa a otra pagaduría (422 <c>Lending.Payroll.LoanHasDirectDebit</c>; el estado se arma
    /// en la base porque por la puerta ya no se puede llegar a él), y revocado el débito el traslado sí se hace.
    /// </summary>
    [Fact]
    public async Task Un_credito_se_paga_por_libranza_o_por_debito_automatico_no_por_las_dos()
    {
        // Primero las captaciones (su perfil regulatorio base) y después la libranza, que corrige ese perfil con la inscripción de operador de libranza.
        var cap = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "libdebito");
        var esc = await LibranzaE2E.EscenarioAsync(fx, "cap_libdebito");
        cap.TenantPublicId.Should().Be(esc.Coop.TenantPublicId, "es la misma cooperativa aislada (cap_libdebito)");
        using var http = fx.CreateClient();
        var accesos = await LibranzaE2E.AccesosAsync(http, esc);
        var admin = accesos.Admin;
        // La corrección del perfil de la libranza no trae la autorización de captar (y la de captaciones no trae la inscripción de operador): el perfil lleva las dos.
        var actual = (await CarteraE2E.GetAsync(http, admin, "/api/core/regulatory-profile")).GetProperty("current");
        var perfil = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
        {
            organizationType = actual.GetProperty("organizationTypeCode").GetString(), financialActivity = true, supervisionLevel = 2, mayTakeDeposits = true,
            payrollOperatorRegistration = new { number = "OPL-E2E-001", date = esc.Hoy.AddDays(-30) },
            validFrom = DateOnly.Parse(actual.GetProperty("validFrom").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            source = "Certificado de la Supersolidaria (e2e)", reason = "Capta ahorro y opera libranzas (e2e)",
            supersedesPublicId = actual.GetProperty("versionPublicId").GetGuid(),
        });
        perfil.StatusCode.Should().Be(HttpStatusCode.Created, await perfil.Content.ReadAsStringAsync());
        await CarteraE2E.PersonaDelAdministradorAsync(http, esc.Coop);
        var (credito, _, deudor, _) = await LibranzaE2E.CreditoPorLibranzaAsync(http, esc, accesos, "LibDeb", 1_000_000m);
        await ConFechaDeNacimientoAsync(http, admin, deudor);
        await OriginacionE2E.AutorizarAsync(http, admin, deudor, "GeneralTreatment");
        var (_, vence, total) = await PagosE2E.CuotaAsync(http, admin, credito);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, esc.Hoy, "AHLIBDEB");
        var cuenta = await MovimientosTests.CuentaAsync(http, admin, esc.Usuarios.O1.AgenciaPublicId, linea, deudor);
        await MovimientosTests.ConsignarAsync(http, admin, cuenta, deudor, total + 500_000m, esc.Hoy);

        // (1) Primero la libranza: el débito automático de ese crédito se rechaza.
        var mandatoDelCredito = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/direct-debits", new
        {
            accountPublicId = cuenta, scope = "SingleLoan", loanPublicId = credito, allowsPartial = false, validFrom = esc.Hoy,
            authorizationAttachmentPublicId = Guid.NewGuid(),
        });
        mandatoDelCredito.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await mandatoDelCredito.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(mandatoDelCredito)).Should().Be("Lending.DirectDebit.LoanOnPayroll");

        // «Todos mis créditos» se registra, pero el ciclo no debita el crédito que va por libranza.
        var todos = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/direct-debits", new
        {
            accountPublicId = cuenta, scope = "AllLoansOfHolder", allowsPartial = false, validFrom = esc.Hoy, authorizationAttachmentPublicId = Guid.NewGuid(),
        });
        todos.StatusCode.Should().Be(HttpStatusCode.Created, await todos.Content.ReadAsStringAsync());
        using (CausacionE2E.RelojEn(fx, vence)) await CausacionE2E.CicloDeHoyAsync(fx, http, admin, esc.Coop.TenantPublicId);
        var debitos = await CarteraE2E.GetAsync(http, admin, $"{Cuentas}/{cuenta}/operations?kind=DirectDebit");
        debitos.GetProperty("items").GetArrayLength().Should().Be(0, "la cuota de un crédito por libranza la descuenta la nómina: el débito automático no la paga también");

        // (2) Con un débito automático vigente (estado heredado armado en la base) el crédito no pasa a otra pagaduría.
        var mandatoHeredado = await CausacionE2E.EnLaBaseAsync(fx, esc.Coop.TenantPublicId, async db =>
        {
            var loanId = await db.Loans.Where(l => l.PublicId == credito).Select(l => l.Id).SingleAsync();
            var cuentaId = await db.MemberAccounts.Where(a => a.PublicId == cuenta).Select(a => a.Id).SingleAsync();
            var persona = await db.Loans.Where(l => l.Id == loanId).Select(l => l.DebtorPersonId).SingleAsync();
            var m = new DirectDebitMandate
            {
                MemberAccountId = cuentaId, AuthorizedByPersonId = persona, Scope = DirectDebitScope.SingleLoan, LoanId = loanId, ValidFrom = esc.Hoy,
                AuthorizationAttachmentPublicId = Guid.NewGuid(),
            };
            db.DirectDebitMandates.Add(m);
            await db.SaveChangesAsync();
            return m.PublicId;
        });
        object traslado() => new
        {
            newAgreementPublicId = esc.OtroConvenio, employeeId = "GOB-00099", certificationAttachmentPublicId = esc.Certificacion,
            certificationDate = esc.Hoy.AddDays(-1), certifiedNetIncome = LibranzaE2E.IngresoCertificado, reason = "Traslado a la Gobernación (e2e)",
        };
        var negado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/lending/loans/{credito}/payroll-transfer", traslado());
        (await CarteraE2E.CodigoDeErrorAsync(negado)).Should().Be("Lending.Payroll.LoanHasDirectDebit");

        var revocado = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/lending/direct-debits/{mandatoHeredado}/revoke",
            new { reason = "El asociado prefiere la libranza (e2e)" });
        revocado.StatusCode.Should().Be(HttpStatusCode.OK, await revocado.Content.ReadAsStringAsync());
        var hecho = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/lending/loans/{credito}/payroll-transfer", traslado());
        hecho.StatusCode.Should().Be(HttpStatusCode.OK, await hecho.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// La persona del crédito nace sin fecha de nacimiento y abrir una cuenta exige los datos de conocimiento
    /// (<c>Lending.Savings.MemberDataIncomplete</c>): se completan por la misma edición de la persona.
    /// </summary>
    private static async Task ConFechaDeNacimientoAsync(HttpClient http, string admin, Guid persona)
    {
        var actual = await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{persona}");
        var cuerpo = actual.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value.Clone());
        cuerpo["dateOfBirth"] = new DateOnly(1984, 5, 20);
        var resp = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{persona}", cuerpo);
        resp.IsSuccessStatusCode.Should().BeTrue($"completar la fecha de nacimiento: «{await resp.Content.ReadAsStringAsync()}»");
    }
}
