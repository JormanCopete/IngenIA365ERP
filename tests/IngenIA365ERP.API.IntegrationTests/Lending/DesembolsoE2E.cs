using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.LineasE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.OriginacionE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes del desembolso (feature 013, US3, T0429, T0430): la cooperativa «desembolso» es el escenario de la originación activado
/// (<see cref="OriginacionE2E.EscenarioAsync"/>, C107) más lo que pide el desembolso, todo por HTTP:
/// <list type="bullet">
/// <item>el depósito de valores <c>SIMULADO</c> (<c>COR_ExternalChannelSettings</c>, clase <c>SecuritiesDepository</c>);</item>
/// <item>la línea <c>PAGARE</c> —la de la originación con un pagaré desmaterializado obligatorio— en el alcance de quien radica y decide;</item>
/// <item>un banco con código de transferencia (ACH) para la cuenta del beneficiario y la cuenta bancaria del plan que sirve de origen;</item>
/// <item>el rol del analista que además desembolsa (para la segregación <c>Decidir × Desembolsar</c>).</item>
/// </list>
/// Una solicitud aprobada sale de radicarla con <c>asesor.o1</c> y aprobarla con <c>analista</c> (nivel ANALISTA, hasta 5 millones); desembolsa
/// <c>jefe</c> (límite operativo de 50 millones). El resultado del giro lo emite la tesorería de prueba por su ruta (<c>pruebas</c>) y lo consume
/// <see cref="ConsumeTreasuryResultCommand"/> como lo haría <c>DestinoCartera</c> en el despachador: el destino <c>Lending</c> no cuenta mientras
/// la fila <c>CAR</c> adopte la familia de Comercial que llega con US6. (nuevo)
/// </summary>
public static class DesembolsoE2E
{
    public const string Desembolsos = "/api/lending/disbursements";
    public const string Ordenes = "/api/lending/disbursement-orders";
    public const string Archivos = "/api/lending/disbursement-files";
    public const string Pagares = "/api/lending/promissory-notes";
    public const string Garantias = "/api/lending/collaterals";
    public const string Creditos = "/api/lending/loans";
    public const string CodigoDelBanco = "0007";

    /// <summary>El escenario del desembolso.</summary>
    public sealed record Escenario(
        OriginacionE2E.Escenario Originacion,
        Guid LineaConPagare,
        Guid Banco,
        Guid CuentaDeOrigen)
    {
        public CarteraE2E.CooperativaAislada Coop => Originacion.Coop;
        public UsuariosDeCartera Usuarios => Originacion.Usuarios;
        public DateOnly Hoy => Originacion.Hoy;
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();
    private static int _documento;

    /// <summary>
    /// El escenario del desembolso en la cooperativa <paramref name="nombre"/> («desembolso» la de las e2e de la fase 7; US4 usa otras, porque
    /// su ciclo diario causa todos los créditos vivos de la cooperativa y mueve sus marcas).
    /// </summary>
    /// <remarks>Con <paramref name="modalidad"/>, las líneas son de esa modalidad (ver <see cref="OriginacionE2E.EscenarioAsync"/>).</remarks>
    public static Task<Escenario> EscenarioAsync(CentralIdentityApiFixture fx, string nombre = "desembolso", string? modalidad = null)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararAsync(fx, nombre, modalidad);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Escenario> PrepararAsync(CentralIdentityApiFixture fx, string nombre, string? modalidad)
    {
        var orig = await OriginacionE2E.EscenarioAsync(fx, nombre, activada: true, modalidad);
        using var http = fx.CreateClient();
        var admin = orig.Coop.TokenAdmin;
        var hoy = orig.Hoy;

        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/core/external-channels", new
        {
            kind = "SecuritiesDepository", channelCode = "SIMULADO", validFrom = hoy, reason = "Depósito de valores simulado para el desembolso (e2e)",
        });

        var version = VersionDeOriginacion(hoy, conDocumento: false, modalidad);
        version["collateralRules"] = new[] { new { coDebtors = 0, promissoryNoteKind = "Dematerialized" } };
        var pagare = await CrearExitoAsync(http, admin, "PAGARE", "Consumo con pagaré desmaterializado", version);
        var lineas = new List<Guid> { orig.Origen, pagare };
        if (orig.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { orig.Usuarios.AsesorO1, orig.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, [.. lineas]);

        var banco = await CrearBancoAsync(http, admin, "BCODES", "Banco del desembolso (e2e)", CodigoDelBanco);
        var cuenta = await CuentaBancariaAsync(http, admin, "11100598", "Banco del desembolso cta. corriente (e2e)", banco, "0099887766");

        // La segregación: el analista que decide también tiene el permiso de desembolsar.
        var desembolsa = await RolPropioAsync(http, admin, "ENS-DES-ANALISTA", "Lending.Disbursements.Execute");
        await DarRolAsync(http, admin, orig.Usuarios.Analista, desembolsa);

        return new Escenario(orig, pagare, banco, cuenta);
    }

    // ------------------------------------------------------------------------------------------- solicitudes --

    /// <summary>
    /// Un asociado con autorización de estudio cuyo documento termina en <paramref name="ultimoDigito"/>: el depósito simulado responde por él
    /// (3: firma a la tercera consulta; 0: firma en la consulta siguiente).
    /// </summary>
    public static async Task<(Guid Persona, string Documento)> DeudorAsync(HttpClient http, string admin, string nombre, int ultimoDigito = 0)
    {
        var documento = (6_600_000_000L + Interlocked.Increment(ref _documento) * 10L + ultimoDigito).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new
            {
                idType = "C", taxId = documento, firstName = nombre, lastName = "Desembolso", mobile = "3001234560",
                email = $"{nombre.ToLowerInvariant()}.{documento}@coop.desembolso.test",
            },
            associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var persona = (await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid();
        await AutorizarAsync(http, admin, persona, "CreditProcessing");
        return (persona, documento);
    }

    /// <summary>Radica con <c>asesor.o1</c> y aprueba con <c>analista</c>; devuelve la solicitud y lo que dijo la decisión.</summary>
    public static async Task<(Guid Solicitud, JsonElement Decision)> AprobadaAsync(HttpClient http, Escenario esc, Guid persona, decimal monto, Guid? linea = null)
    {
        var asesor = esc.Usuarios.AsesorO1.Token;
        var radicada = await RadicarAsync(http, asesor, Solicitud(esc.Originacion, persona, monto, linea));
        var id = radicada.GetProperty("publicId").GetGuid();
        var (nivel, huella) = await EnrutamientoAsync(http, asesor, id);
        nivel.Should().Be("ANALISTA", "el ensayo aprueba montos de hasta 5 millones con el analista");
        var aprobada = await DecidirAsync(http, esc.Usuarios.Analista.Token, "applications", id, "Approve", huella);
        aprobada.StatusCode.Should().Be(HttpStatusCode.Created, await aprobada.Content.ReadAsStringAsync());
        return (id, await CarteraE2E.LeerAsync(aprobada));
    }

    /// <summary>El cuerpo de un desembolso: por cheque (sin cuenta) o por transferencia a la cuenta del deudor en el banco del ensayo.</summary>
    public static Dictionary<string, object?> Desembolso(Escenario esc, Guid solicitud, Guid persona, bool transferencia = false, bool porArchivo = false,
        DateOnly? fechaValor = null)
    {
        var cuerpo = new Dictionary<string, object?>
        {
            ["applicationPublicId"] = solicitud,
            ["form"] = transferencia ? "Transfer" : "Check",
            ["valueDate"] = fechaValor,
            ["paidByFile"] = porArchivo,
        };
        if (transferencia)
            cuerpo["beneficiary"] = new { personPublicId = persona, bankPublicId = esc.Banco, accountType = "Savings", accountNumber = "0123456789" };
        return cuerpo;
    }

    /// <summary>Desembolsa con <c>jefe</c> y exige el 201; devuelve el <c>DisbursementResultDto</c>.</summary>
    public static async Task<JsonElement> DesembolsarAsync(HttpClient http, Escenario esc, object cuerpo, Guid? clave = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Jefe.Token, HttpMethod.Post, Desembolsos, cuerpo, clave);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"desembolsar: «{await resp.Content.ReadAsStringAsync()}»");
        return await CarteraE2E.LeerAsync(resp);
    }

    /// <summary>Una solicitud de la línea ORIGEN aprobada y desembolsada por cheque: (solicitud, persona, resultado).</summary>
    public static async Task<(Guid Solicitud, Guid Persona, JsonElement Desembolso)> CreditoDesembolsadoAsync(HttpClient http, Escenario esc, string nombre,
        decimal monto = 3_000_000m, bool transferencia = false, bool porArchivo = false)
    {
        var (persona, _) = await DeudorAsync(http, esc.Coop.TokenAdmin, nombre);
        var (solicitud, _) = await AprobadaAsync(http, esc, persona, monto);
        var hecho = await DesembolsarAsync(http, esc, Desembolso(esc, solicitud, persona, transferencia, porArchivo));
        return (solicitud, persona, hecho);
    }

    // --------------------------------------------------------------------------------------- tesorería --

    /// <summary>
    /// Feature 015, T0299 (FR-224): el resultado del giro lo da <b>Tesorería real</b>, no la tesorería de prueba. El despachador le entrega la
    /// <c>OrdenDeGiroEmitida</c>; si la orden quedó pendiente por la cuenta destino del beneficiario, se registra y aprueba esa cuenta (con su certificación, el
    /// jefe aprueba) y se revalida; después se gira por sus rutas reales —por transferencia: lote de la cuenta de pagos de Recaudo y Tesorería, aprobado por el
    /// jefe, archivo y enviado, y el resultado manual con su soporte; por cheque: lote de cheques, chequera, emisión y entrega (sólo «girada»: un cheque no se
    /// «rechaza» en Tesorería, se anula o no se cobra)—. Tesorería informa el <c>ResultadoDelGiro</c> a nombre de quien registra; devuelve ese mensaje, que la
    /// prueba consume (<see cref="ConsumirAsync"/>) o deja al despachador. <paramref name="valor"/> se ignora: Tesorería gira exactamente el neto (FR-116).
    /// </summary>
    public static async Task<Guid> ResultadoDelGiroAsync(CentralIdentityApiFixture fx, HttpClient http, Escenario esc, Guid orden, string resultado, decimal? valor = null)
    {
        _ = valor;
        var admin = esc.Coop.TokenAdmin;
        var jefe = esc.Usuarios.Jefe.Token;
        var deTesoreria = await OrdenDeTesoreriaAsync(fx, esc, orden);
        if (deTesoreria.Estado is 1 or 2)
        {
            var cuenta = await IngenIA365ERP.API.IntegrationTests.Inventory.InventarioE2E.MandarAsync(http, admin, HttpMethod.Get,
                $"/api/core/people/{deTesoreria.Beneficiario}/bank-accounts");
            var yaTiene = cuenta.IsSuccessStatusCode && (await CarteraE2E.LeerAsync(cuenta)).ValueKind == JsonValueKind.Array
                          && (await CarteraE2E.LeerAsync(cuenta)).GetArrayLength() > 0;
            if (!yaTiene) await IngenIA365ERP.API.IntegrationTests.Treasury.TesoreriaE2E.CuentaDestinoAprobadaAsync(http, esc, deTesoreria.Beneficiario, "0123456789");
            (await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/treasury/payment-orders/{deTesoreria.PublicId}/revalidate", new { }))
                .IsSuccessStatusCode.Should().BeTrue("revalidar la orden con la cuenta destino aprobada");
        }

        var hoy = esc.Hoy;
        if (deTesoreria.Forma == 2)
        {
            resultado.Should().Be("Disbursed", "un giro por cheque no se rechaza en Tesorería real: se gira por transferencia para probar el rechazo (T0299)");
            await GirarPorChequeAsync(http, admin, jefe, deTesoreria.PublicId, hoy);
        }
        else
        {
            await Receipts.RecaudoYTesoreriaE2E.GirarOrdenAsync(http, admin, jefe, deTesoreria.PublicId);
            await Receipts.RecaudoYTesoreriaE2E.RegistrarResultadoAsync(http, admin, deTesoreria.PublicId, resultado, hoy);
        }
        return await EnLaBaseAsync(fx, esc, db => (from r in db.DisbursementResults.AsNoTracking()
                                                   join o in db.PaymentOrders.AsNoTracking() on r.PaymentOrderId equals o.Id
                                                   where o.OriginOrderPublicId == orden && r.EmittedMessagePublicId != null
                                                   orderby r.Id descending
                                                   select r.EmittedMessagePublicId!.Value).FirstAsync());
    }

    /// <summary>La orden de pago de Tesorería real que nació de la orden de giro del origen: su <c>PublicId</c>, su estado y su forma (1 transferencia, 2 cheque).</summary>
    public static async Task<(Guid PublicId, int Estado, int Forma, Guid Beneficiario, bool ConMensaje)> OrdenDeTesoreriaAsync(CentralIdentityApiFixture fx, Escenario esc,
        Guid ordenDelOrigen)
    {
        await CarteraE2E.DespacharAsync(fx, esc.Coop.TenantPublicId, 3);
        var o = await EnLaBaseAsync(fx, esc, db => (from x in db.PaymentOrders.AsNoTracking()
                                                    join p in db.People.AsNoTracking().IgnoreQueryFilters() on x.BeneficiaryPersonId equals p.Id
                                                    where x.OriginOrderPublicId == ordenDelOrigen
                                                    orderby x.Id descending
                                                    select new { x.PublicId, x.Status, x.Form, Persona = p.PublicId, x.OriginMessagePublicId }).FirstAsync());
        return (o.PublicId, (int)o.Status, (int)o.Form, o.Persona, o.OriginMessagePublicId is not null);
    }

    /// <summary>El giro por cheque real: lote de cheques, la chequera de la cuenta de pagos (la registra si falta), la emisión y la entrega con su constancia.</summary>
    private static async Task GirarPorChequeAsync(HttpClient http, string admin, string aprobador, Guid ordenDeTesoreria, DateOnly hoy)
    {
        var cuenta = await Receipts.RecaudoYTesoreriaE2E.CuentaDePagosAsync(http, admin);
        var chequeras = await CarteraE2E.GetAsync(http, admin, $"/api/treasury/checkbooks?account={cuenta}");
        var chequera = chequeras.EnumerateArray().FirstOrDefault();
        var chequeraId = chequera.ValueKind == JsonValueKind.Object
            ? chequera.GetProperty("publicId").GetGuid()
            : (await IngenIA365ERP.API.IntegrationTests.Inventory.InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/treasury/checkbooks",
                new { bankAccountPublicId = cuenta, fromNumber = 100001L, toNumber = 100500L, custodianUserPublicId = (Guid?)null, custodyAttachmentPublicId = (Guid?)null }))
                .GetProperty("publicId").GetGuid();
        var lote = await IngenIA365ERP.API.IntegrationTests.Inventory.InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = cuenta, form = 3, payDate = hoy, orderPublicIds = new[] { ordenDeTesoreria }, liquidityFundUsePublicId = (Guid?)null,
        });
        var id = lote.GetProperty("publicId").GetGuid();
        (await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit", null)).IsSuccessStatusCode.Should().BeTrue("enviar a aprobación");
        (await CarteraE2E.ConClaveAsync(http, aprobador, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { })).IsSuccessStatusCode.Should().BeTrue("aprobar");
        var emitidos = await IngenIA365ERP.API.IntegrationTests.Inventory.InventarioE2E.ExitoAsync(http, admin, HttpMethod.Post, $"/api/treasury/issued-checks/batches/{id}",
            new { checkbookPublicId = chequeraId, crossed = true, firstBeneficiaryOnly = true });
        foreach (var cheque in emitidos.EnumerateArray())
        {
            var chequeId = cheque.GetProperty("publicId").GetGuid();
            var constancia = await IngenIA365ERP.API.IntegrationTests.Attachments.AdjuntosE2E.SubirAsync(http, admin, chequeId,
                System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 constancia de entrega del cheque (e2e)"), "constancia.pdf", "application/pdf", "TreasuryDeliveryReceipt");
            (await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"/api/treasury/issued-checks/{chequeId}/deliver",
                new { deliveryReceiptAttachmentPublicId = constancia.Adjunto, deliveredOn = hoy })).IsSuccessStatusCode.Should().BeTrue("entregar el cheque");
        }
    }

    /// <summary>Cartera consume el <c>ResultadoDelGiro</c> como lo haría <c>DestinoCartera</c> (actor «Proceso de integración»).</summary>
    public static async Task<ResultadoDeConsumo> ConsumirAsync(CentralIdentityApiFixture fx, Escenario esc, Guid mensaje)
    {
        var r = await new ConductorDelDespachador(fx).EnviarComoProcesoAsync(esc.Coop.TenantPublicId, $"Mensaje:{mensaje:N}",
            new ConsumeTreasuryResultCommand([mensaje]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>
    /// Desde US6 el destino <c>Lending</c> tiene consumidor y el despachador entrega el <c>ResultadoDelGiro</c> solo: unas pasadas sobre la cooperativa y
    /// el estado en que quedó la entrega del mensaje a Cartera (<c>Processed</c> si se consumió). Las pruebas que necesitan ver la respuesta del
    /// comando —el segundo consumo de un reproceso, <c>AlreadyProcessed</c>— siguen con <see cref="ConsumirAsync"/>.
    /// </summary>
    public static async Task<IngenIA365ERP.Domain.Enums.Integration.DeliveryStatus> EntregarPorElDespachadorAsync(CentralIdentityApiFixture fx, Escenario esc,
        Guid mensaje)
    {
        await new ConductorDelDespachador(fx).PasadasAsync(esc.Coop.TenantPublicId, 2);
        return await EnLaBaseAsync(fx, esc, db => (from m in db.IntegrationMessages.AsNoTracking()
                                                   join d in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals d.MessageId
                                                   where m.PublicId == mensaje && d.Destination == IngenIA365ERP.Domain.Enums.Integration.IntegrationDestinations.Lending
                                                   select d.Status).SingleAsync());
    }

    // ------------------------------------------------------------------------------------------ la base --

    /// <summary>Una consulta tipada sobre la base de la cooperativa (lo que la API todavía no expone: el estado del crédito, el libro, la bandeja).</summary>
    public static async Task<T> EnLaBaseAsync<T>(CentralIdentityApiFixture fx, Escenario esc, Func<IApplicationDbContext, Task<T>> consulta)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == esc.Coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await consulta(ambito.Db);
    }

    // ------------------------------------------------------------------------------------------- apoyo --

    private static async Task<Guid> CrearBancoAsync(HttpClient http, string token, string code, string name, string transferCode)
    {
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/banks", new { code, name, shortName = code, transferCode });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"banco {code}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetGuid();
    }

    private static async Task<Guid> CuentaBancariaAsync(HttpClient http, string token, string codigo, string nombre, Guid banco, string numero)
    {
        var busqueda = await CarteraE2E.GetAsync(http, token, "/api/accounting/accounts/search?q=111005&onlyMovement=false");
        var padre = busqueda.EnumerateArray().First(c => c.GetProperty("code").GetString() == "111005");
        var resp = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = codigo, name = nombre, parentPublicId = padre.GetProperty("publicId").GetGuid(),
            enabledModules = new[] { "CNT", "CAR", "TES" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false,
            requiresBranch = false, bank = new { bankPublicId = banco, accountNumber = numero },
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"cuenta bancaria: «{await resp.Content.ReadAsStringAsync()}»");
        var cuentas = await CarteraE2E.GetAsync(http, token, "/api/accounting/accounts/bank-accounts");
        return cuentas.EnumerateArray().Single(c => c.GetProperty("code").GetString() == codigo).GetProperty("accountPublicId").GetGuid();
    }

    /// <summary>
    /// El formato <c>CSV-DESEMBOLSOS</c> de adaptadores-y-archivos.md §6.2 (genérico, ámbito <c>LoanDisbursement</c>). El contrato lo da como
    /// ejemplo ilustrativo —el layout real lo aporta la cooperativa—, así que no es semilla: lo carga la prueba por la ruta de formatos.
    /// </summary>
    public static async Task<Guid> FormatoDeDesembolsosAsync(HttpClient http, Escenario esc)
    {
        var admin = esc.Coop.TokenAdmin;
        var existentes = await CarteraE2E.GetAsync(http, admin, "/api/core/bank-file-formats?scope=LoanDisbursement");
        var ya = existentes.EnumerateArray().FirstOrDefault(f => f.GetProperty("code").GetString() == "CSV-DESEMBOLSOS");
        if (ya.ValueKind == JsonValueKind.Object) return ya.GetProperty("formatPublicId").GetGuid();

        var definicion = $$"""
            { "code": "CSV-DESEMBOLSOS", "name": "Desembolsos de crédito, CSV genérico", "scope": "LoanDisbursement",
              "validFrom": "{{esc.Hoy.AddDays(-1):yyyy-MM-dd}}", "kind": "Delimited", "delimiter": ";", "encoding": "utf-8", "amountFormat": "Point2",
              "fileName": "DESEMB{PaymentDate:yyyyMMdd}-{BatchReference}.csv",
              "records": {
                "header":  { "enabled": false, "fields": [] },
                "detail":  { "enabled": true, "fields": [
                  { "order": 1, "name": "Tipo doc",   "source": "PayeeDocumentType", "required": true },
                  { "order": 2, "name": "Documento",  "source": "PayeeDocument",     "required": true },
                  { "order": 3, "name": "Nombre",     "source": "PayeeFullName",     "required": true },
                  { "order": 4, "name": "Banco ACH",  "source": "PayeeBankCode",     "required": true },
                  { "order": 5, "name": "Tipo cta",   "source": "PayeeAccountType",  "required": true },
                  { "order": 6, "name": "Cuenta",     "source": "PayeeAccountNumber","required": true },
                  { "order": 7, "name": "Valor",      "source": "Amount",            "required": true },
                  { "order": 8, "name": "Referencia", "source": "LoanNumber",        "required": true } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """;
        var resp = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/bank-file-formats", JsonDocument.Parse(definicion).RootElement);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"formato CSV-DESEMBOLSOS: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("formatPublicId").GetGuid();
    }
}
