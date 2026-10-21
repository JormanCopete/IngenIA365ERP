using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Feature 014, T165 (US16, escenarios 1 y 2 y su prueba independiente; FR-003, FR-004, FR-005; contracts/api.md §1.2, §1.7 y §4): la seguridad de
/// captaciones y aportes por HTTP, en una cooperativa aislada con aportes y captaciones activados.
/// <list type="bullet">
/// <item>Escenario 1: <c>lectura</c> (sin <c>Lending.Accounts.Withdraw</c>) pide un retiro por giro de una cuenta que existe y recibe lo mismo que si no
/// existiera (404 <c>Generic.NotFound</c>), y el intento queda <c>Lending.Access.Denied</c>.</item>
/// <item>Prueba independiente: <c>asesor.o2</c> (asesor de captaciones de la oficina O2) abre por URL una cuenta de la oficina O1 —el detalle, el saldo y el
/// extracto— y recibe el mismo 404 que una inexistente, con <c>Lending.Access.OutOfScope</c>; la lista no se la muestra. <c>asesor.o1</c> sí la ve, y el
/// saldo y el extracto quedan como lecturas sensibles (<c>Lending.Savings.BalanceViewed</c>, <c>.StatementViewed</c>).</item>
/// <item>Fuera de alcance también el destino: <c>asesor.o2</c> no traslada desde su cuenta a una de O1.</item>
/// <item>Segregación (FR-005): quien registra un cargo no lo condona, y quien abrió la cuenta sí condona un cargo que registró otra persona (los cargos
/// tienen su propio expediente).</item>
/// <item>Escenario 2: la cadena de auditoría del día se verifica sin incidentes, y un evento de lectura alterado y otro borrado en Mongo salen
/// <c>Altered</c> y <c>Deleted</c>.</item>
/// </list>
/// <b>e2e en la corrida final</b> (decisión del dueño): se escriben y compilan; no se corren por fase. (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public class SeguridadDeCaptacionesTests(CentralIdentityApiFixture fx)
{
    private const string Cuentas = "/api/lending/accounts";
    private const string Verificar = "/api/audit/integrity/verify";
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";

    private static readonly object[] CatalogoDeCargos =
    [
        new
        {
            code = "CERTADIC", name = "Certificado adicional", kind = "ExtraDocument", trigger = "ExtraDocument", enabled = true, calculation = "FixedAmount",
            fixedAmount = 3_000m, uncollectedPolicy = "KeepPending",
        },
    ];

    // ---------------------------------------------------------------------------------------------------- escenario --

    private sealed record Escenario(
        CaptacionesE2E.CooperativaDeCaptaciones Coop,
        UsuariosDeCartera Usuarios,
        DateOnly Hoy,
        Guid Linea,
        Guid Ana,
        Guid CuentaDeO1,
        Guid Beto,
        Guid CuentaDeO2,
        string AsesorO1,
        string AsesorO2)
    {
        public string Admin => Coop.TokenAdmin;
    }

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();

    private Task<Escenario> EscenarioAsync()
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue(fx, out var tarea))
            {
                tarea = ArmarAsync();
                Escenarios[fx] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>
    /// La cooperativa «seguridad» con captaciones: una línea a la vista con un cargo por evento, los asesores de O1 y O2 con la plantilla
    /// <c>cartera.asesor-captaciones</c> (sin alcance total: sus oficinas y la línea), Ana con una cuenta en O1 y Beto con una en O2, las dos con saldo.
    /// </summary>
    private async Task<Escenario> ArmarAsync()
    {
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "seguridad");
        using var http = fx.CreateClient();
        var hoy = CarteraE2E.Hoy(fx);
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHSEGUR", CatalogoDeCargos);

        var asesorDeCaptaciones = usuarios.RolesSugeridos["cartera.asesor-captaciones"];
        foreach (var asesor in new[] { usuarios.AsesorO1, usuarios.AsesorO2 })
        {
            var rol = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{asesor.PublicId}/roles", new { rolePublicId = asesorDeCaptaciones });
            if (!rol.IsSuccessStatusCode)
                (await CarteraE2E.CodigoDeErrorAsync(rol)).Should().Be("Security.Users.AlreadyAssignedRole", await rol.Content.ReadAsStringAsync());
            await UsuariosDeCartera.FijarLineasAsync(http, admin, asesor, linea);
        }

        var ana = await MovimientosTests.AsociadoAsync(http, admin, "AnaSeg");
        var beto = await MovimientosTests.AsociadoAsync(http, admin, "BetoSeg");
        var deO1 = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, ana);
        var deO2 = await MovimientosTests.CuentaAsync(http, admin, usuarios.O2.AgenciaPublicId, linea, beto);
        await MovimientosTests.ConsignarAsync(http, admin, deO1, ana, 500_000m, hoy);
        await MovimientosTests.ConsignarAsync(http, admin, deO2, beto, 300_000m, hoy);

        // Los roles nuevos rigen desde el próximo ingreso.
        var o1 = await UsuariosDeCartera.IngresarAsync(http, usuarios.AsesorO1);
        var o2 = await UsuariosDeCartera.IngresarAsync(http, usuarios.AsesorO2);
        return new Escenario(coop, usuarios, hoy, linea, ana, deO1, beto, deO2, o1, o2);
    }

    // --------------------------------------------------------------------------------------- escenario 1: sin permiso --

    [Fact]
    public async Task US16_1_sin_permiso_de_retiro_la_ruta_responde_como_si_no_existiera_y_queda_Lending_Access_Denied()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);
        var lectura = esc.Usuarios.Lectura.Token;
        var retiro = new
        {
            amount = 10_000m, payee = new { personPublicId = esc.Ana, bankPublicId = (Guid?)null, accountType = (byte?)null, accountNumber = (string?)null },
            signatures = new[] { esc.Ana }, reason = "Intento sin permiso",
        };

        var real = await CarteraE2E.ConClaveAsync(http, lectura, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO1}/operations/withdrawals", retiro);
        var inexistente = await CarteraE2E.ConClaveAsync(http, lectura, HttpMethod.Post, $"{Cuentas}/{Guid.NewGuid()}/operations/withdrawals", retiro);

        real.StatusCode.Should().Be(HttpStatusCode.NotFound, await real.Content.ReadAsStringAsync());
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await CarteraE2E.CodigoDeErrorAsync(real)).Should().Be(await CarteraE2E.CodigoDeErrorAsync(inexistente), "sin permiso es lo mismo que inexistente");
        (await CarteraE2E.CodigoDeErrorAsync(real)).Should().Be("Generic.NotFound");
        (await EventosAsync(http, esc, desde)).Should().Contain(e => Texto(e, "action") == "Lending.Access.Denied"
                                                                   && Texto(e, "userName")!.Contains("lectura", StringComparison.OrdinalIgnoreCase),
            "el intento sin permiso queda auditado antes del 404");
    }

    // ------------------------------------------------------------------------------ prueba independiente: fuera de alcance --

    [Fact]
    public async Task US16_el_asesor_de_O2_no_ve_ni_el_saldo_ni_el_extracto_de_una_cuenta_de_O1_y_queda_Lending_Access_OutOfScope()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);

        foreach (var sufijo in new[] { string.Empty, "/balance", "/statement" })
        {
            var ajena = await CarteraE2E.MandarGetAsync(http, esc.AsesorO2, $"{Cuentas}/{esc.CuentaDeO1}{sufijo}");
            var inexistente = await CarteraE2E.MandarGetAsync(http, esc.AsesorO2, $"{Cuentas}/{Guid.NewGuid()}{sufijo}");
            ajena.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{sufijo}: «{await ajena.Content.ReadAsStringAsync()}»");
            (await CarteraE2E.CodigoDeErrorAsync(ajena)).Should().Be(await CarteraE2E.CodigoDeErrorAsync(inexistente),
                $"{sufijo}: fuera del alcance es lo mismo que inexistente");
        }
        (await CarteraE2E.GetAsync(http, esc.AsesorO2, Cuentas)).GetProperty("items").EnumerateArray()
            .Should().NotContain(c => c.GetProperty("publicId").GetGuid() == esc.CuentaDeO1, "las listas filtran en silencio");

        // Ni siquiera como destino: un traslado desde su propia cuenta de O2 a una de O1.
        var traslado = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO2}/operations/simulate", new
        {
            kind = "Transfer", amount = 1_000m, targetAccountPublicId = esc.CuentaDeO1, signatures = new[] { esc.Beto },
        });
        traslado.StatusCode.Should().Be(HttpStatusCode.OK, "el administrador tiene todas las oficinas");
        var ajeno = await CarteraE2E.ConClaveAsync(http, esc.AsesorO2, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO2}/operations/simulate", new
        {
            kind = "Transfer", amount = 1_000m, targetAccountPublicId = esc.CuentaDeO1, signatures = new[] { esc.Beto },
        });
        ajeno.StatusCode.Should().Be(HttpStatusCode.NotFound, "el destino fuera del alcance tampoco se revela (T162)");

        var eventos = await EventosAsync(http, esc, desde);
        eventos.Where(e => Texto(e, "action") == "Lending.Access.OutOfScope" && Texto(e, "userName")!.Contains("asesor.o2", StringComparison.OrdinalIgnoreCase))
            .Should().HaveCountGreaterThanOrEqualTo(4, "el detalle, el saldo, el extracto y el destino del traslado");
        eventos.Should().NotContain(e => Texto(e, "action") == AuditEventTypes.LendingSavingsBalanceViewed
                                         && Texto(e, "userName")!.Contains("asesor.o2", StringComparison.OrdinalIgnoreCase),
            "lo que no se mostró no es una lectura");
    }

    [Fact]
    public async Task US16_el_asesor_de_O1_ve_el_saldo_a_D_y_el_extracto_y_cada_lectura_queda_auditada()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var desde = DateTime.UtcNow.AddMinutes(-1);

        var saldo = await CarteraE2E.GetAsync(http, esc.AsesorO1, $"{Cuentas}/{esc.CuentaDeO1}/balance?date={esc.Hoy:yyyy-MM-dd}");
        saldo.GetProperty("holderBalance").GetDecimal().Should().Be(500_000m);
        saldo.GetProperty("fingerprint").GetString().Should().NotBeNullOrWhiteSpace();
        var antes = await CarteraE2E.GetAsync(http, esc.AsesorO1,
            $"{Cuentas}/{esc.CuentaDeO1}/balance?date={esc.Hoy:yyyy-MM-dd}&recordedAsOf={DateTime.UtcNow.AddYears(-1):O}");
        antes.GetProperty("holderBalance").GetDecimal().Should().Be(0m, "hace un año no se sabía de la consignación (R)");

        var extracto = await CarteraE2E.GetAsync(http, esc.AsesorO1, $"{Cuentas}/{esc.CuentaDeO1}/statement?from={esc.Hoy:yyyy-MM-dd}&to={esc.Hoy:yyyy-MM-dd}");
        extracto.GetProperty("closingBalance").GetDecimal().Should().Be(500_000m);
        extracto.GetProperty("lines").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        var eventos = (await EventosAsync(http, esc, desde)).Where(e => Texto(e, "userName")!.Contains("asesor.o1", StringComparison.OrdinalIgnoreCase)).ToList();
        eventos.Count(e => Texto(e, "action") == AuditEventTypes.LendingSavingsBalanceViewed).Should().BeGreaterThanOrEqualTo(2);
        eventos.Should().Contain(e => Texto(e, "action") == AuditEventTypes.LendingSavingsStatementViewed);
    }

    // ----------------------------------------------------------------------------------------------------- segregación --

    [Fact]
    public async Task FR_005_quien_registra_un_cargo_no_lo_condona_y_quien_abrio_la_cuenta_si()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var registra = esc.Usuarios.Administrador.Token;

        var alta = await CarteraE2E.ConClaveAsync(http, registra, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO1}/charges",
            new { chargeCode = "CERTADIC", eventReference = "CERT-SEG", reason = "Certificado bancario adicional" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var cargo = (await CarteraE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var propio = await CarteraE2E.ConClaveAsync(http, registra, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO1}/charges/{cargo}/waive",
            new { reason = "Cortesía (e2e)" });
        propio.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await propio.Content.ReadAsStringAsync());
        (await CarteraE2E.CodigoDeErrorAsync(propio)).Should().Be("Lending.Segregation.Conflict");

        // El administrador de la cooperativa abrió la cuenta: abrir no es registrar el cargo.
        var otro = await CarteraE2E.ConClaveAsync(http, esc.Admin, HttpMethod.Post, $"{Cuentas}/{esc.CuentaDeO1}/charges/{cargo}/waive",
            new { reason = "Cortesía comercial (e2e)" });
        otro.IsSuccessStatusCode.Should().BeTrue(await otro.Content.ReadAsStringAsync());
    }

    // ----------------------------------------------------------------------------------------- escenario 2: la cadena --

    [Fact]
    public async Task US16_2_la_cadena_del_dia_se_verifica_y_un_evento_alterado_y_otro_borrado_salen_en_la_verificacion()
    {
        var esc = await EscenarioAsync();
        using var http = fx.CreateClient();
        var tenant = esc.Coop.TenantPublicId;

        // Tres lecturas sensibles nuevas, reenviadas a Mongo selladas, y la verificación sin incidentes.
        for (var i = 0; i < 3; i++) await CarteraE2E.GetAsync(http, esc.AsesorO1, $"{Cuentas}/{esc.CuentaDeO1}/balance");
        await fx.ReenviarAuditoriaAsync(tenant);
        var limpia = await VerificarAsync(http, esc);
        limpia.GetProperty("incidents").GetArrayLength().Should().Be(0, $"la cadena del día está íntegra: {limpia}");

        var lecturas = (await OutboxAsync(tenant)).Where(f => f.Module == "Lending" && f.Seq != null).OrderBy(f => f.Seq).ToList();
        lecturas.Should().HaveCountGreaterThanOrEqualTo(3, "los eventos de captaciones van por COR_AuditOutbox y se sellan");
        var (alterado, borrado) = (lecturas[^3], lecturas[^2]);

        await CarteraE2E.GetAsync(http, esc.AsesorO1, $"{Cuentas}/{esc.CuentaDeO1}/statement");
        await fx.ReenviarAuditoriaAsync(tenant);

        var coleccion = fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, tenant.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
        (await coleccion.UpdateOneAsync(Filtro(alterado), Builders<BsonDocument>.Update.Set("userName", "otra persona"))).ModifiedCount.Should().Be(1);
        (await coleccion.DeleteOneAsync(Filtro(borrado))).DeletedCount.Should().Be(1);

        var incidentes = (await VerificarAsync(http, esc)).GetProperty("incidents").EnumerateArray()
            .Select(i => (Kind: i.GetProperty("kind").GetString(), Seq: i.GetProperty("seq").GetInt64())).ToList();
        incidentes.Should().Contain(("Altered", alterado.Seq!.Value));
        incidentes.Should().Contain(("Deleted", borrado.Seq!.Value));
    }

    // ------------------------------------------------------------------------------------------------------ auditoría --

    private static async Task<JsonElement> VerificarAsync(HttpClient http, Escenario esc)
    {
        var resp = await CarteraE2E.EnviarAsync(http, esc.Usuarios.Auditor.Token, HttpMethod.Post, Verificar,
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Lleva la auditoría de la cooperativa a Mongo y devuelve los eventos de Cartera desde <paramref name="desde"/>.</summary>
    private async Task<List<JsonElement>> EventosAsync(HttpClient http, Escenario esc, DateTime desde)
    {
        await fx.ReenviarAuditoriaAsync(esc.Coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var hasta = DateTime.UtcNow.AddMinutes(1);
        var eventos = new List<JsonElement>();
        for (var pagina = 1; pagina <= 20; pagina++)
        {
            var lote = await CarteraE2E.GetAsync(http, esc.Admin, $"/api/audit/logs?modules=Lending&from={desde:O}&to={hasta:O}&page={pagina}&pageSize=100");
            var items = lote.GetProperty("items").EnumerateArray().ToList();
            eventos.AddRange(items);
            if (items.Count < 100) break;
        }
        return eventos;
    }

    private static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async Task<List<AuditOutboxEntry>> OutboxAsync(Guid tenantPublicId)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await ambito.Db.AuditOutbox.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
    }

    private static FilterDefinition<BsonDocument> Filtro(AuditOutboxEntry fila) => Builders<BsonDocument>.Filter.Eq("_id", fila.EventId.ToString("D"));
}
