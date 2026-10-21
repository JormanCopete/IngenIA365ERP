using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Ventas.EscenarioDeVentas;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// La cooperativa de las ventas a crédito con Cartera (feature 013, US6, T0578–T0580; quickstart §3.7; C107): el escenario contable de ventas de la 012
/// (<see cref="ContabilidadDeVentasE2E"/>: POS <c>PV1</c> con sus cajas, el medio <c>CREDASOC</c> con la línea sugerida <c>CONSUMO</c>, cajero con
/// <c>SellOnCredit</c> hasta 100.000 y supervisor hasta 1.000.000) más lo que Cartera necesita para recibir sus ventas, todo por HTTP:
/// <list type="bullet">
/// <item>los valores nacionales (usura de consumo), el patrimonio técnico del doble, las políticas de datos y una lista propia sin los deudores;</item>
/// <item>las equivalencias de Comercial (<c>Monthly</c> → mensual, <c>SinglePayment</c> → pago único, la unidad <c>Days</c> = 1 día) y la línea
/// <c>CONSUMO</c> de <b>origen comercial</b> por defecto para asociados (23 % EA, de 100.000 a 30.000.000, de 1 a 60 meses, mensual);</item>
/// <item>la venta de <c>Valeria</c> (asociada con autorización; después de activar recibe un cupo de 100.000, menor que su venta) cobrada en el
/// POS <b>antes</b> de activar: crédito provisional,
/// pendiente de validar, aprobado por el supervisor; al activar, su <c>VentaACreditoRegistrada</c> v2 queda esperando a Cartera;</item>
/// <item>la activación de prueba (<see cref="CarteraE2E.ActivarCarteraAsync"/>), la matriz de atribuciones (analista y excepción hasta 100.000.000 y el
/// consejo de la activación) y los cupos rotativos de <c>Ximena</c> (1.000.000) y <c>Rodrigo</c> (400.000), los dos con autorización de estudio del crédito;</item>
/// <item>los parámetros de Comercial: la integración con Cartera habilitada desde hoy y la cuenta por cobrar registrada por Cartera.</item>
/// </list>
/// Una por fixture; las pruebas de la clase la comparten. (nuevo)
/// </summary>
public static class VentasConCarteraE2E
{
    public const string Evaluaciones = "/api/inventory/sales/credit-evaluations";
    public const string Recibos = "/api/lending/inbound-messages";
    public const string Reservas = "/api/lending/number-reservations";
    public const string Creditos = "/api/lending/loans";
    public const string Linea = "CONSUMO";

    /// <summary>El escenario: el de ventas, la cooperativa vista por Cartera y sus personas.</summary>
    public sealed record Escenario(
        EscenarioDeVentas Ventas,
        CarteraE2E.CooperativaAislada Coop,
        DateOnly Hoy,
        Guid Ximena,
        Guid Rodrigo,
        Guid Valeria,
        Guid VentaDeValeria,
        Guid Sesion)
    {
        public string Admin => Coop.TokenAdmin;
        public string Cajero => Ventas.Cajero1.Token;
        public Guid TenantPublicId => Coop.TenantPublicId;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Preparados = [];
    private static readonly object Cerrojo = new();

    public static Task<Escenario> EscenarioAsync(CentralIdentityApiFixture fx, string nombre = "ventas_cartera")
    {
        lock (Cerrojo)
        {
            if (!Preparados.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararAsync(fx, nombre);
                Preparados[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Escenario> PrepararAsync(CentralIdentityApiFixture fx, string nombre)
    {
        var ventas = await ContabilidadDeVentasE2E.PrepararAsync(fx, nombre);
        var coop = new CarteraE2E.CooperativaAislada
        {
            TokenAdmin = ventas.Admin, TenantPublicId = ventas.Coop.TenantPublicId, SucursalPrincipal = ventas.Coop.SucursalPrincipal,
            PrimerEjercicioContable = ventas.Coop.PrimerEjercicioContable ?? CarteraE2E.Hoy(fx).Year, CorreoAdmin = $"admin.{nombre}@coop.inventario.test",
        };
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);

        await LineasE2E.PrepararAsync(fx, coop);
        fx.Dobles.PatrimonioTecnico(coop.TenantPublicId, OriginacionE2E.PatrimonioTecnico, hoy.AddDays(-1));
        foreach (var finalidad in new[] { "GeneralTreatment", "CreditProcessing" })
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
            {
                title = $"Política de datos: {finalidad}", contentMarkdown = "Texto de la política para las e2e de ventas a crédito.", effectiveFrom = DateTime.UtcNow,
                purpose = finalidad,
            });
        var lista = await OriginacionE2E.ImportarListaAsync(http, admin, OriginacionE2E.LibroDeListas(hoy, ("99000001", "PERSONA AJENA AL ENSAYO")), "apply");
        lista.StatusCode.Should().Be(HttpStatusCode.OK, $"cargar la lista propia: «{await lista.Content.ReadAsStringAsync()}»");

        // Equivalencias de Comercial y la línea de origen comercial que sugiere CREDASOC.
        var equivalencias = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Put, "/api/lending/commercial-term-equivalences", new
        {
            items = new object[]
            {
                new { kind = "Periodicity", commercialCode = "Monthly", periodicity = "Monthly" },
                new { kind = "Periodicity", commercialCode = "SinglePayment", periodicity = "SinglePayment" },
                new { kind = "TermUnit", commercialCode = "Days", daysPerUnit = 1 },
            },
            validFrom = hoy.AddDays(-1), reason = "Equivalencias de Comercial del ensayo (e2e)",
        });
        equivalencias.IsSuccessStatusCode.Should().BeTrue($"equivalencias: «{await equivalencias.Content.ReadAsStringAsync()}»");
        var version = LineasE2E.VersionFija(hoy, "CONSUMO", "SinLibranza", 0.23m, "Línea de las ventas a crédito (e2e)");
        version["allowsCommercialOrigin"] = true;
        version["commercialDefaultFor"] = "Associate";
        version["interestTaxConcept"] = new { taxed = false, supportAttachmentPublicId = Guid.NewGuid() };
        version["amounts"] = new { min = 100_000m, max = 30_000_000m };
        version["periodicities"] = new[] { "Monthly", "SinglePayment" };
        await LineasE2E.CrearExitoAsync(http, admin, Linea, "Consumo de almacén", version);

        // Valeria compra antes de activar: crédito provisional, pendiente de validar y aprobado por el supervisor (sin cupo en Cartera).
        var ximena = await AsociadoAsync(http, admin, "Ximena", "72100001");
        var rodrigo = await AsociadoAsync(http, admin, "Rodrigo", "72100002");
        var valeria = await AsociadoAsync(http, admin, "Valeria", "72100003");
        var sesion = await SesionAsync(http, ventas.Cajero1.Token, ventas.Caja1);
        var (ventaDeValeria, cobro) = await CobrarACreditoAsync(http, ventas, ventas.Cajero1.Token, sesion, valeria, 3, Credito(ventas, 300_000m));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK, await cobro.Content.ReadAsStringAsync());
        var enAprobacion = await InventarioE2E.LeerAsync(cobro);
        enAprobacion.GetProperty("status").GetInt32().Should().Be(1, "sin Cartera, el crédito provisional pide aprobación");
        await AprobadaAsync(await InventarioE2E.DecidirAsync(http, admin, ventas.Supervisor.Token, enAprobacion.GetProperty("approvalRequestPublicId").GetGuid(),
            aprobar: true));

        await CarteraE2E.ActivarCarteraAsync(fx, coop);

        // La matriz: el analista hasta 100.000.000 y el consejo de la activación como nivel final.
        var consejo = (await CarteraE2E.GetAsync(http, admin, OriginacionE2E.Organos)).EnumerateArray()
            .First(o => o.GetProperty("kind").GetInt32() == (int)IngenIA365ERP.Domain.Enums.Lending.GoverningBodyKind.BoardOfDirectors && o.GetProperty("isActive").GetBoolean())
            .GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/attribution-matrices", new
        {
            validFrom = hoy, reason = "Reglamento de crédito de las ventas a crédito", source = "Acuerdo del consejo (e2e)",
            levels = new object[]
            {
                new { code = "ANALISTA", name = "Analista", order = 10, kind = "Individual", permissionCode = "Lending.Decisions.Analyst", consolidatedAmountCeiling = 100_000_000m },
                new { code = "EXCEP", name = "Excepción", order = 15, kind = "Individual", permissionCode = "Lending.Decisions.Exceptions", consolidatedAmountCeiling = 100_000_000m, isExceptionLevel = true },
                new { code = "CONSEJO", name = "Consejo de administración", order = 40, kind = "Collegiate", governingBodyPublicId = consejo, isBoardLevel = true },
            },
            rules = Array.Empty<object>(),
        });
        // Valeria recibe un cupo menor que la venta que hizo antes de activar: al evaluarla a su fecha, falla por cupo (que admite excepción).
        foreach (var (persona, cupo) in new[] { (ximena, 1_000_000m), (rodrigo, 400_000m), (valeria, 100_000m) })
        {
            var otorgado = await OriginacionE2E.OtorgarCupoAsync(http, admin, persona, cupo, hoy, "Revolving");
            otorgado.StatusCode.Should().Be(HttpStatusCode.Created, await otorgado.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(otorgado)).GetProperty("status").GetString().Should().Be("Active");
        }

        // Comercial: la integración rige desde hoy y la cuenta por cobrar la registra Cartera.
        await ParametroAsync(http, admin, "Cartera.IntegracionHabilitadaDesde", hoy.ToString("yyyy-MM-dd"), hoy);
        await ParametroAsync(http, admin, "Cartera.CuentaPorCobrarRegistradaPor", "Cartera", hoy);

        return new Escenario(ventas, coop, hoy, ximena, rodrigo, valeria, ventaDeValeria, sesion);
    }

    /// <summary>Un asociado con su autorización de estudio del crédito (con evidencia).</summary>
    public static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre, string documento)
    {
        var persona = await ContabilidadDeVentasE2E.AsociadoAsync(http, admin, nombre, documento);
        await OriginacionE2E.AutorizarAsync(http, admin, persona, "CreditProcessing");
        return persona;
    }

    private static async Task ParametroAsync(HttpClient http, string admin, string clave, string valor, DateOnly desde)
    {
        var resp = await InventarioE2E.MandarAsync(http, admin, HttpMethod.Post, $"/api/inventory/parameters/INV/{clave}/versions", new
        {
            scopeKind = "None", value = valor, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Ventas a crédito con Cartera (e2e)",
        });
        resp.IsSuccessStatusCode.Should().BeTrue($"parámetro {clave}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>El pago a crédito con <c>CREDASOC</c>: tres cuotas mensuales a 90 días salvo que se diga otra cosa.</summary>
    public static object Credito(EscenarioDeVentas v, decimal valor, int cuotas = 3, int plazo = 90, int cada = 30) => new
    {
        paymentMeansPublicId = v.Medio("CREDASOC"), amount = valor, credit = new { installments = cuotas, termDays = plazo, periodicityDays = cada },
    };

    /// <summary>
    /// Abre una venta en el POS con <paramref name="unidades"/> de P7 (100.000 cada una) para <paramref name="persona"/> y la cobra con
    /// <paramref name="pagos"/> (sin juzgar la respuesta); devuelve la venta y la respuesta del cobro.
    /// </summary>
    public static async Task<(Guid Venta, HttpResponseMessage Cobro)> CobrarACreditoAsync(HttpClient http, EscenarioDeVentas v, string cajero, Guid sesion,
        Guid persona, int unidades, params object[] pagos)
    {
        var venta = await NuevaVentaAsync(http, cajero, sesion, v.Inv.P("P7").CodigoDeBarras);
        var id = VentaId(venta);
        venta = await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}", new { customerPersonPublicId = persona });
        if (unidades != 1)
            await InventarioE2E.ExitoAsync(http, cajero, HttpMethod.Patch, $"{Borradores}/{id}/lines/{venta.GetProperty("lines")[0].GetProperty("linePublicId").GetGuid()}",
                new { quantity = (decimal)unidades });
        return (id, await PedirCobrarAsync(http, cajero, id, 100_000m * unidades, pagos));
    }

    /// <summary>La pestaña «Crédito» de la venta.</summary>
    public static Task<JsonElement> CreditoDeLaVentaAsync(HttpClient http, string token, Guid venta) =>
        InventarioE2E.GetAsync(http, token, $"/api/inventory/sales/documents/{venta}/credit");

    /// <summary>El único pago a crédito de la pestaña «Crédito».</summary>
    public static async Task<JsonElement> PagoACreditoAsync(HttpClient http, string token, Guid venta) =>
        (await CreditoDeLaVentaAsync(http, token, venta)).GetProperty("payments").EnumerateArray().Single();

    /// <summary>Pasadas del despachador sobre la cooperativa: Cartera consume las ventas y Comercial, las respuestas.</summary>
    public static Task DespacharAsync(CentralIdentityApiFixture fx, Escenario esc, int pasadas = 4) =>
        new ConductorDelDespachador(fx).PasadasAsync(esc.TenantPublicId, pasadas);

    /// <summary>
    /// Pide o libera una reserva por el puerto de Cartera como lo hace la confirmación de Comercial: en la cooperativa, a nombre de la cajera
    /// (la reserva la pide el usuario que confirma la venta) y dentro de una transacción propia que guarda y confirma al terminar (exige una en curso; el puerto nunca guarda).
    /// </summary>
    public static async Task<ResultadoDeReservaDto> EnLaTransaccionAsync(CentralIdentityApiFixture fx, Escenario esc,
        Func<IReservasDeCartera, Task<ResultadoDeReservaDto>> trabajo)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var entrada = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == esc.TenantPublicId);
        var cajera = esc.Ventas.Cajero1;
        var usuario = await EnLaBaseAsync(fx, esc, db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.Users.Where(u => u.PublicId == cajera.PublicId).Select(u => new { u.Id, u.CentralUserId })));
        var actor = new Actor(IngenIA365ERP.Domain.Enums.Integration.ActorKind.Person, usuario.Id, cajera.PublicId, usuario.CentralUserId, cajera.Correo,
            cajera.Correo, IngenIA365ERP.Domain.Enums.Integration.ExecutionChannel.Pos, "POST /api/inventory/pos/drafts/{id}/checkout", "127.0.0.1", null);
        ResultadoDeReservaDto? resultado = null;
        var r = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(entrada,
            actor, "Tarea:prueba-reservas", async (servicios, ct) =>
            {
                var db = servicios.GetRequiredService<IApplicationDbContext>();
                // Como la confirmación de Comercial: el puerto agrega lo suyo y quien llama guarda en su unidad de trabajo.
                resultado = await TransaccionExplicita.EjecutarAsync(db, async () =>
                {
                    var r = await trabajo(servicios.GetRequiredService<IReservasDeCartera>());
                    await db.SaveChangesAsync(ct);
                    return r;
                }, ct);
            });
        r.Should().Be(ResultadoEnCooperativa.Ejecutada);
        return resultado!;
    }

    /// <summary>Una consulta tipada sobre la base de la cooperativa.</summary>
    public static async Task<T> EnLaBaseAsync<T>(CentralIdentityApiFixture fx, Escenario esc, Func<IApplicationDbContext, Task<T>> consulta)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == esc.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await consulta(ambito.Db);
    }
}
