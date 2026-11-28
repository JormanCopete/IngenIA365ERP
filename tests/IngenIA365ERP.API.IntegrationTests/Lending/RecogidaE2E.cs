using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de las e2e de la recogida de créditos y productos en el desembolso (feature 028, fase 13: T096, T097, T098). Cada prueba corre en su
/// cooperativa aislada del escenario del desembolso (<see cref="DesembolsoE2E.EscenarioAsync"/>) y usa los ayudantes de siempre de la originación, el
/// desembolso, los pagos y la libranza. Lo propio de la recogida:
/// <list type="bullet">
/// <item><see cref="RefrescarAsync"/>: accesos nuevos del administrador y de los usuarios del ensayo (las pruebas son largas y el access dura 15 minutos,
/// medidos con el reloj real aunque el de la suite se adelante);</item>
/// <item><see cref="SolicitudQueRecoge"/> y <see cref="AprobadaQueRecogeAsync"/>: la solicitud con <c>payoffs</c> y su proyección (api.md §2), radicada por
/// <c>asesor.o1</c> y aprobada por <c>analista</c>, otra persona;</item>
/// <item><see cref="ParametroAsync"/>: una versión general de un parámetro de Cartera (<c>Recogida.*</c>);</item>
/// <item><see cref="HechoProcesadoAsync"/> y <see cref="LineasDelComprobanteAsync"/>: el hecho contable de un mensaje ya contabilizado y las líneas de su
/// comprobante.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class RecogidaE2E
{
    public const string Candidatos = "/api/lending/payoffs/candidates";
    public const string Hechos = "/api/lending/accounting/facts";

    /// <summary>
    /// El mismo escenario con accesos nuevos: el administrador de la cooperativa <paramref name="nombre"/> y <c>administrador</c>, <c>asesor.o1</c>,
    /// <c>analista</c>, <c>jefe</c> y <c>pruebas</c>. Los ayudantes de siempre leen los tokens del escenario.
    /// </summary>
    public static async Task<DesembolsoE2E.Escenario> RefrescarAsync(HttpClient http, DesembolsoE2E.Escenario esc, string nombre)
    {
        var admin = await LibranzaE2E.AdministradorDeLaCooperativaAsync(http, nombre);
        var todos = new Dictionary<string, UsuariosDeCartera.Usuario>(esc.Usuarios.Todos, StringComparer.Ordinal);
        foreach (var clave in new[] { "administrador", "asesor.o1", "analista", "jefe", "pruebas" })
            todos[clave] = todos[clave] with { Token = await UsuariosDeCartera.IngresarAsync(http, todos[clave]) };
        var usuarios = new UsuariosDeCartera { Todos = todos, O1 = esc.Usuarios.O1, O2 = esc.Usuarios.O2, RolesSugeridos = esc.Usuarios.RolesSugeridos };
        var c = esc.Coop;
        var coop = new CarteraE2E.CooperativaAislada
        {
            TokenAdmin = admin, TenantPublicId = c.TenantPublicId, SucursalPrincipal = c.SucursalPrincipal, PrimerEjercicioContable = c.PrimerEjercicioContable,
            CorreoAdmin = c.CorreoAdmin,
        };
        return esc with { Originacion = esc.Originacion with { Coop = coop, Usuarios = usuarios } };
    }

    /// <summary>
    /// El cuerpo de una solicitud de la línea ORIGEN (o <paramref name="linea"/>) por caja, en la oficina O1, con la capacidad holgada, que recoge
    /// <paramref name="recogidas"/> (api.md §2 <c>payoffs</c>) proyectadas a <paramref name="fechaProyectada"/>.
    /// </summary>
    public static object SolicitudQueRecoge(DesembolsoE2E.Escenario esc, Guid persona, decimal monto, DateOnly fechaProyectada, object[] recogidas,
        Guid? linea = null) => new
    {
        applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = linea ?? esc.Originacion.Origen,
        purpose = "Libre inversión que recoge productos (e2e 028)", amount = monto, installments = 36, periodicity = "Monthly",
        amortizationSystem = "LevelPayment", paymentForm = "Cashier", prepaymentPreference = "ReduceTerm",
        applicantCapacity = new { monthlyIncome = 40_000_000m, monthlyExpenses = 2_000_000m, externalInstallments = 0m },
        payoffs = recogidas,
        payoffProjection = new { projectedDisbursementDate = fechaProyectada },
    };

    /// <summary>Una recogida de un crédito (api.md §2): total o parcial, con el tratamiento de la cuota causada si se pide.</summary>
    public static object Credito(Guid credito, string modo = "Total", decimal? valor = null, string? cuotaCausada = null) => new
    {
        kind = "Loan", productPublicId = credito, mode = modo, amount = valor, reference = (string?)null, installmentTreatment = cuotaCausada,
    };

    /// <summary>Una recogida de una cuenta (aportes o ahorro): por sus cuotas pendientes o como abono.</summary>
    public static object Cuenta(Guid cuenta, string modo, decimal? valor = null) => new
    {
        kind = "Account", productPublicId = cuenta, mode = modo, amount = valor, reference = (string?)null, installmentTreatment = (string?)null,
    };

    /// <summary>Una recogida de una factura a crédito de Comercial, por su número.</summary>
    public static object Factura(string numero, string modo = "Total", decimal? valor = null) => new
    {
        kind = "CommercialReceivable", productPublicId = (Guid?)null, mode = modo, amount = valor, reference = numero, installmentTreatment = (string?)null,
    };

    /// <summary>Radica con <c>asesor.o1</c> y aprueba con <c>analista</c> (nivel ANALISTA); devuelve la solicitud radicada.</summary>
    public static async Task<JsonElement> AprobadaQueRecogeAsync(HttpClient http, DesembolsoE2E.Escenario esc, object cuerpo)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var radicada = await OriginacionE2E.RadicarAsync(http, asesor, cuerpo);
        var id = radicada.GetProperty("publicId").GetGuid();
        var (nivel, huella) = await OriginacionE2E.EnrutamientoAsync(http, asesor, id);
        nivel.Should().Be("ANALISTA", "el ensayo aprueba con el analista lo consolidado de hasta 5 millones");
        var aprobada = await OriginacionE2E.DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        return radicada;
    }

    /// <summary>Una versión general de un parámetro de Cartera desde <paramref name="desde"/>.</summary>
    public static async Task ParametroAsync(HttpClient http, string token, string clave, string valor, DateOnly desde)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"/api/lending/parameters/{clave}/versions", new
        {
            scopeKind = 0, value = valor, validFrom = desde, reason = "Ensayo de la recogida de créditos y productos (e2e 028)",
        });
        resp.IsSuccessStatusCode.Should().BeTrue($"el parámetro {clave} = {valor}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>El mensaje de ese tipo que nació de la operación <paramref name="origen"/> (origen o relacionado).</summary>
    public static Task<Guid> MensajeAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, Guid origen, string tipo) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.IntegrationMessages.AsNoTracking()
            .Where(m => m.Type == tipo && (m.OriginPublicId == origen || m.RelatedPublicId == origen)).OrderBy(m => m.Id).Select(m => m.PublicId).FirstAsync());

    /// <summary>
    /// El hecho contable de Cartera del mensaje <paramref name="mensaje"/> (<c>GET /api/lending/accounting/facts</c> del crédito), con pasadas del despachador
    /// hasta que deje de estar pendiente o en lote; exige que quede <c>Processed</c>.
    /// </summary>
    public static async Task<JsonElement> HechoProcesadoAsync(CentralIdentityApiFixture fx, HttpClient http, string token, DesembolsoE2E.Escenario esc,
        Guid credito, string tipo, Guid mensaje)
    {
        JsonElement? hecho = null;
        for (var i = 0; i < 10; i++)
        {
            await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId, 3);
            var lista = await CarteraE2E.GetAsync(http, token, $"{Hechos}?type={tipo}&loanPublicId={credito}&pageSize=100");
            hecho = lista.GetProperty("items").EnumerateArray().Cast<JsonElement?>()
                .FirstOrDefault(h => h!.Value.GetProperty("messagePublicId").GetGuid() == mensaje);
            if (hecho is { } h && h.GetProperty("status").GetString() is not ("Pending" or "InBatch")) break;
        }
        hecho.Should().NotBeNull($"el hecho {tipo} del mensaje {mensaje} está en la lista del crédito");
        var final = hecho!.Value;
        if (final.GetProperty("status").GetString() != "Processed")
        {
            var detalle = await CarteraE2E.GetAsync(http, token, $"/api/lending/messages/{mensaje}");
            throw new Xunit.Sdk.XunitException($"El hecho {tipo} no quedó contabilizado: {final} / {detalle}");
        }
        return final;
    }

    /// <summary>Las líneas del comprobante contable del mensaje (su recibo en <c>/api/accounting/lending/postings</c>).</summary>
    public static async Task<(Guid Comprobante, List<JsonElement> Lineas)> LineasDelComprobanteAsync(HttpClient http, string token, Guid mensaje)
    {
        var recibos = (await CarteraE2E.GetAsync(http, token, $"/api/accounting/lending/postings?message={mensaje}"))
            .GetProperty("items").EnumerateArray().Where(r => r.GetProperty("messagePublicId").GetGuid() == mensaje).ToList();
        recibos.Should().ContainSingle($"el mensaje {mensaje} tiene un recibo de Contabilidad");
        var comprobante = recibos[0].GetProperty("accountingDocumentPublicId").GetGuid();
        var documento = await CarteraE2E.GetAsync(http, token, $"/api/accounting/documents/{comprobante}");
        return (comprobante, documento.GetProperty("lines").EnumerateArray().ToList());
    }

    /// <summary>La suma de débitos de las líneas de esa cuenta.</summary>
    public static decimal DebitosEnLaCuenta(IEnumerable<JsonElement> lineas, string cuenta) =>
        lineas.Where(l => l.GetProperty("accountCode").GetString() == cuenta).Sum(l => l.GetProperty("debit").GetDecimal());

    /// <summary>La suma de créditos de las líneas de esa cuenta.</summary>
    public static decimal CreditosEnLaCuenta(IEnumerable<JsonElement> lineas, string cuenta) =>
        lineas.Where(l => l.GetProperty("accountCode").GetString() == cuenta).Sum(l => l.GetProperty("credit").GetDecimal());

    /// <summary>El saldo (débitos − créditos) de una cuenta del plan en el libro contabilizado de la cooperativa.</summary>
    public static Task<decimal> SaldoDeLaCuentaAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, string cuenta) =>
        DesembolsoE2E.EnLaBaseAsync(fx, esc, db => (from j in db.JournalEntries.AsNoTracking()
                                                    join a in db.ChartOfAccounts.AsNoTracking() on j.AccountId equals a.Id
                                                    where a.Code == cuenta && j.IsPosted && !j.IsDeleted
                                                    select j.Debit - j.Credit).SumAsync());
}
