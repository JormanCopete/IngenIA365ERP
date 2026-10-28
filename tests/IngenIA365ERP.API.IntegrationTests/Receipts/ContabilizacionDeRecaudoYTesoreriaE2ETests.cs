using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Tareas;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0255 (US8 «Independent Test»; quickstart recorrido A paso 10; FR-140 a FR-146; contabilidad.md §3, §5, §7): con la contabilidad iniciada y las matrices
/// de Recaudo y Tesorería cargadas <b>antes</b> de activar (la activación ve la completitud al 100 % y no deja los módulos en «no pasa»), un día de caja que paga
/// cuotas de dos créditos de Cartera en efectivo se contabiliza en <b>un</b> comprobante <c>RR</c> resumido por día, punto y medio cuando corre el lote del día con
/// la sesión cerrada; la diferencia de arqueo aprobada por otra persona pasa <b>en línea</b> a un <c>MC</c>; el puente de recaudo de Cartera (clase de recaudador
/// <c>Receipts</c>) queda en cero en la conciliación contable de Cartera; y el <c>RR</c> no se reversa desde Comprobantes (<c>Accounting.Document.ModuleOwned</c>).
/// Desde la segunda ronda del 2026-10-27 (D-f) recorre también una transferencia: el cajero elige entre las cuentas propias habilitadas en la caja y el <c>RR</c> debita
/// el auxiliar bancario vinculado a la elegida. Lo que no recorre: el puente de Captaciones (lo fija <c>ContabilidadDeCaptacionesTests</c>). (nuevo)
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ContabilizacionDeRecaudoYTesoreriaE2ETests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_dia_de_caja_queda_en_un_RR_resumido_la_diferencia_en_linea_y_el_puente_de_Cartera_en_cero()
    {
        var hoy = CarteraE2E.Hoy(fx);
        using var http = fx.CreateClient();
        // La cooperativa del escenario de la caja (CajaE2E usa «recaudo_{nombre}»), con las matrices antes de activar Cartera, Recaudo y Tesorería.
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "recaudo_contab", hoy.Year);
        await MatricesDeRecaudoYTesoreriaE2E.CargarAsync(http, coop.TokenAdmin, hoy.AddDays(-10));
        var b5 = await CajaE2E.BilleteAsync(http, coop.TokenAdmin, 5_000m);
        var c = await CajaE2E.PreparadaAsync(fx, http, "contab");
        c.Escenario.Coop.TenantPublicId.Should().Be(coop.TenantPublicId);
        var admin = coop.TokenAdmin;
        var tenant = coop.TenantPublicId;
        await CajaE2E.SupervisorAsync(http, c);

        // La activación vio las matrices completas: ningún módulo quedó en «no pasa».
        foreach (var modulo in new[] { "receipts", "treasury" })
        {
            var completitud = await CarteraE2E.GetAsync(http, admin, $"/api/accounting/{modulo}/rules/completeness");
            completitud.GetProperty("percent").GetDecimal().Should().Be(100m, $"{modulo}: {completitud}");
            completitud.GetProperty("notPosted").GetBoolean().Should().BeFalse(modulo);
        }

        // Cartera con su matriz y el puente del recaudador real (Receipts) en la cuenta que comparte con la matriz de Recaudo.
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: true);
        var regla = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, ContabilidadDeCarteraE2E.Reglas, new
        {
            operation = "*", role = "RecaudoPorAplicar", dimensions = new { collectorClass = "Receipts" },
            accountPublicId = await ContabilidadE2E.CuentaAsync(http, admin, MatricesDeRecaudoYTesoreriaE2E.Puente), validFrom = new DateOnly(hoy.Year, hoy.Month, 1),
            reason = "El puente de recaudo de Cartera por la caja (e2e)",
        });
        regla.StatusCode.Should().Be(HttpStatusCode.Created, await regla.Content.ReadAsStringAsync());

        // Un día de caja: dos recibos en efectivo que pagan cuotas de dos créditos.
        var r1 = await CajaE2E.ReciboAsync(http, c, await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoA, 50_000m)], [CajaE2E.Medio(c.Efectivo, 50_000m)]));
        var r2 = await CajaE2E.ReciboAsync(http, c, await CajaE2E.OperacionAsync(http, c, [CajaE2E.Cuota(c.CreditoB, 70_000m)], [CajaE2E.Medio(c.Efectivo, 70_000m)]));

        // Segunda ronda del 2026-10-27 (D-f, T0492; FR-231): una transferencia a una cuenta propia de recaudo vinculada a su auxiliar bancario. Con dos cuentas
        // habilitadas el cajero elige; sin elegir, la caja la pide con las opciones.
        var banco = await RecaudoYTesoreriaE2E.BancoAsync(http, admin);
        var padre = (await CarteraE2E.GetAsync(http, admin, "/api/accounting/accounts/search?q=111005&onlyMovement=false")).EnumerateArray()
            .First(x => x.GetProperty("code").GetString() == "111005").GetProperty("publicId").GetGuid();
        var alta = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = CuentaBancariaDeLaTransferencia, name = "Banco del recaudo por transferencia (e2e)", parentPublicId = padre,
            enabledModules = new[] { "CNT", "REC", "TES" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
            bank = new { bankPublicId = banco, accountNumber = "5566778899" },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var auxiliar = (await CarteraE2E.GetAsync(http, admin, "/api/accounting/accounts/bank-accounts")).EnumerateArray()
            .Single(x => x.GetProperty("code").GetString() == CuentaBancariaDeLaTransferencia).GetProperty("accountPublicId").GetGuid();
        var vinculada = await CajaE2E.CuentaDeTransferenciaAsync(http, admin, c.Escenario, c.Punto, c.Transferencia, "TRVINC", auxiliar, "5566778899");
        var opTransferencia = (await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, "/api/receipts/cash-operations",
            new { sessionPublicId = c.Sesion, payerPersonPublicId = c.Persona }))).GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{opTransferencia}/items",
            new { items = new[] { CajaE2E.Cuota(c.CreditoA, 20_000m) } }), "ítems de la transferencia");
        var sinElegir = await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{opTransferencia}/means",
            new { means = new[] { CajaE2E.Medio(c.Transferencia, 20_000m, "TRF-CONTAB") } });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(sinElegir)).Should().Be("Receipts.Receipt.OwnBankAccountRequired", "con dos cuentas habilitadas el cajero elige");
        var opciones = await CarteraE2E.GetAsync(http, c.Token, $"/api/receipts/sessions/{c.Sesion}/transfer-accounts");
        opciones.EnumerateArray().Select(o => o.GetProperty("ownBankAccountPublicId").GetGuid()).Should().Contain(vinculada);
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Put, $"/api/receipts/cash-operations/{opTransferencia}/means",
            new { means = new[] { new { paymentMeansPublicId = c.Transferencia, amount = 20_000m, reference = "TRF-CONTAB", ownBankAccountPublicId = vinculada } } }),
            "la transferencia con su cuenta");
        var r3 = await CajaE2E.ReciboAsync(http, c, opTransferencia);
        await CarteraE2E.DespacharAsync(fx, tenant, 3);

        // Mientras la sesión sigue abierta, los recibos esperan en lote: nada en Contabilidad todavía.
        (await RecibosContablesAsync(tenant, [Recibo(r1), Recibo(r2), Recibo(r3)])).Should().OnlyContain(p => p.Documento == null, "el RR se arma al cerrar la sesión y correr el lote");

        // El cierre con un faltante de 5.000 (sobre la tolerancia del medio): queda por aprobar y lo aprueba otra persona.
        var conteo = await RecaudoYTesoreriaE2E.LeerAsync(await InventarioE2E.MandarAsync(http, c.Token, HttpMethod.Post, $"/api/receipts/sessions/{c.Sesion}/count", new
        {
            means = new object[]
            {
                new
                {
                    paymentMeansPublicId = c.Efectivo,
                    denominations = new[] { new { denominationPublicId = c.Billete, quantity = 12 }, new { denominationPublicId = b5, quantity = 3 } },
                    reason = "Faltante en el arqueo (e2e)",
                },
                new
                {
                    paymentMeansPublicId = c.Transferencia, counted = 20_000m,
                    references = new[] { new { reference = "TRF-CONTAB", amount = 20_000m, confirmed = true } },
                },
            },
        }));
        RecaudoYTesoreriaE2E.Enum<CashSessionState>(conteo.GetProperty("sessionStatus")).Should().Be(CashSessionState.ClosedWithDifferencePending, conteo.ToString());
        var linea = conteo.GetProperty("lines").EnumerateArray().First(l => l.GetProperty("pendingApproval").GetBoolean()).GetProperty("publicId").GetGuid();
        var aprobada = await InventarioE2E.MandarAsync(http, c.Escenario.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/receipts/sessions/{c.Sesion}/differences/{linea}/approve",
            new { reason = "Faltante revisado con la cajera (e2e)" });
        ((int)aprobada.StatusCode).Should().BeInRange(200, 299, await aprobada.Content.ReadAsStringAsync());

        // La diferencia pasa en línea: un MC con la caja acreditada por el faltante.
        await CarteraE2E.DespacharAsync(fx, tenant, 3);
        var diferencia = await DocumentosDeRecaudoAsync(tenant, "MC");
        diferencia.Should().ContainSingle("la diferencia de arqueo aprobada pasa en línea (DiferenciaDeArqueoDeRecaudoAprobada)");
        diferencia[0].Lineas.Should().Contain(l => l.Cuenta == MatricesDeRecaudoYTesoreriaE2E.Caja && l.Credito == 5_000m, "el faltante sale de la caja");

        // Corre el lote del día (pasada la hora de corte): un lote por sesión cerrada, y el despachador lo lleva a Contabilidad.
        await LoteDelDiaAsync(fx, tenant, hoy);
        for (var i = 0; i < 4; i++) await CarteraE2E.DespacharAsync(fx, tenant, 3);

        var recibos = await RecibosContablesAsync(tenant, [Recibo(r1), Recibo(r2), Recibo(r3)]);
        recibos.Should().HaveCount(3).And.OnlyContain(p => p.Documento != null && p.Lote != null, "los tres recibos quedan contabilizados por el lote");
        recibos.Select(p => p.Documento).Distinct().Should().ContainSingle("un comprobante resumido por día, punto y medio");
        var rr = await DocumentosDeRecaudoAsync(tenant, "RR");
        rr.Should().ContainSingle();
        rr[0].Fecha.Should().Be(hoy);
        rr[0].Lineas.Where(l => l.Cuenta == MatricesDeRecaudoYTesoreriaE2E.Caja).Should().ContainSingle("las líneas del medio se resumen en una")
            .Which.Debito.Should().Be(120_000m);
        rr[0].Lineas.Where(l => l.Cuenta == MatricesDeRecaudoYTesoreriaE2E.Puente).Sum(l => l.Credito - l.Debito).Should().Be(140_000m);
        rr[0].Lineas.Where(l => l.Cuenta == CuentaBancariaDeLaTransferencia).Should().ContainSingle("la transferencia debita el banco de la cuenta que eligió el cajero")
            .Which.Debito.Should().Be(20_000m);
        rr[0].Lineas.Should().OnlyContain(l => l.Tercero == null, "ninguna cuenta del ensayo exige tercero: el resumen no lo pone");

        var lotes = await CarteraE2E.GetAsync(http, admin, "/api/accounting/receipts/batches");
        lotes.EnumerateArray().Should().Contain(l => l.GetProperty("facts").GetInt32() == 3 && l.GetProperty("vouchers").GetInt32() == 1
            && l.GetProperty("totalDebit").GetDecimal() == l.GetProperty("totalCredit").GetDecimal(), lotes.ToString());

        // El puente de recaudo de Cartera por la caja queda en cero: lo que Recaudo acreditó, Cartera lo debitó al aplicar los pagos.
        var conciliacion = await CarteraE2E.GetAsync(http, admin, $"/api/lending/accounting/reconciliation?date={hoy:yyyy-MM-dd}");
        conciliacion.GetProperty("bridges").EnumerateArray().Should().Contain(b => b.GetProperty("collectorClass").GetString() == "Receipts"
            && b.GetProperty("balance").GetDecimal() == 0m, conciliacion.ToString());
        var puente = conciliacion.GetProperty("sets").EnumerateArray().Single(s => s.GetProperty("combinations").EnumerateArray()
            .Any(x => x.GetProperty("role").GetString() == "RecaudoPorAplicar" && x.GetProperty("collectorClass").GetString() == "Receipts"));
        puente.GetProperty("difference").GetDecimal().Should().Be(0m, puente.ToString());
        puente.GetProperty("unexplained").GetDecimal().Should().Be(0m);

        // Lo de Recaudo no se reversa desde Comprobantes: se corrige desde Recaudo.
        var reverso = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/documents/{rr[0].PublicId}/reverse",
            new { reason = "Intento desde Comprobantes (e2e)" });
        reverso.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await reverso.Content.ReadAsStringAsync());
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(reverso)).Should().Be("Accounting.Document.ModuleOwned");

        // El libro cuadra.
        (await CausacionE2E.EnLaBaseAsync(fx, tenant, db => db.JournalEntries.AsNoTracking().Where(j => j.IsPosted && !j.IsDeleted).SumAsync(j => j.Debit - j.Credit)))
            .Should().Be(0m);
    }

    /// <summary>El auxiliar bancario de la cuenta propia de la transferencia (segunda ronda, D-f).</summary>
    private const string CuentaBancariaDeLaTransferencia = "11100597";

    private static Guid Recibo(JsonElement recibo) => recibo.GetProperty("publicId").GetGuid();

    private sealed record ReciboContable(string Tipo, long? Documento, Guid? Lote);

    /// <summary>Los recibos de Contabilidad (<c>ACC_ReceiptsPostings</c>) de los hechos <c>ReciboEmitido</c> de esos recibos de caja.</summary>
    private async Task<List<ReciboContable>> RecibosContablesAsync(Guid tenant, IReadOnlyList<Guid> recibos) =>
        await CausacionE2E.EnLaBaseAsync(fx, tenant, async db =>
        {
            var mensajes = await db.IntegrationMessages.AsNoTracking().Where(m => m.OriginModule == "REC" && recibos.Contains(m.OriginPublicId) && m.Type == "ReciboEmitido")
                .Select(m => m.PublicId).ToListAsync();
            mensajes.Should().HaveCount(recibos.Count, "cada recibo emite su ReciboEmitido");
            var recibidos = await db.ReceiptsPostings.AsNoTracking().Where(p => mensajes.Contains(p.MessagePublicId))
                .Select(p => new ReciboContable(p.MessageType, p.AccountingDocumentId, p.BatchPublicId)).ToListAsync();
            return recibidos.Count == 0 ? mensajes.Select(_ => new ReciboContable("ReciboEmitido", null, null)).ToList() : recibidos;
        });

    private sealed record LineaContable(string Cuenta, decimal Debito, decimal Credito, int? Tercero);

    private sealed record DocumentoContable(Guid PublicId, DateOnly Fecha, List<LineaContable> Lineas);

    /// <summary>Los comprobantes de Recaudo de un tipo, con sus líneas por código de cuenta.</summary>
    private async Task<List<DocumentoContable>> DocumentosDeRecaudoAsync(Guid tenant, string tipo) =>
        await CausacionE2E.EnLaBaseAsync(fx, tenant, async db =>
        {
            var documentos = await db.AccountingDocuments.AsNoTracking().Include(d => d.VoucherType).Include(d => d.Lines).ThenInclude(l => l.Account)
                .Where(d => d.OriginModule == "REC" && !d.IsDeleted && d.VoucherType!.Code == tipo).ToListAsync();
            return documentos.Select(d => new DocumentoContable(d.PublicId, d.Date,
                d.Lines.Select(l => new LineaContable(l.Account!.Code, l.Debit, l.Credit, l.PersonId)).ToList())).ToList();
        });

    /// <summary>
    /// El lote del día de Recaudo (<c>recaudo.lote-del-dia</c>) como si hubiera pasado la hora de corte: el mismo servicio que corre la tarea, en la cooperativa y con el
    /// actor del proceso. La tarea mira la hora de corte del reloj; aquí no se espera a las 18:00.
    /// </summary>
    internal static async Task LoteDelDiaAsync(CentralIdentityApiFixture fx, Guid tenant, DateOnly dia)
    {
        TenantDirectoryEntry cooperativa;
        using (var alcance = fx.Factory.Services.CreateScope())
            cooperativa = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None)).Single(t => t.PublicId == tenant);
        var origen = Actor.OrigenDeTarea(TareaDeLoteDelDiaDeRecaudo.NombreDeLaTarea);
        await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(origen), origen,
            async (servicios, ct) => await servicios.GetRequiredService<LoteDelDiaDeRecaudo>().CorrerAsync(dia, ct));
    }
}

/// <summary>
/// Las matrices contables de Recaudo y Tesorería del ensayo (feature 015, T0255): una auxiliar por rol y una regla sin dimensiones por cada fila de la plantilla
/// (<c>GET /api/accounting/{receipts|treasury}/rules/template</c>) —por hecho en los roles de resultado—, así la completitud queda al 100 % con la configuración que
/// tenga la cooperativa. El puente del módulo de Recaudo va a <see cref="Puente"/>, la misma cuenta que el puente de recaudo de Cartera por la caja; la caja a
/// <see cref="Caja"/>. Idempotente: si la matriz de Recaudo ya tiene reglas, no hace nada. (nuevo)
/// </summary>
public static class MatricesDeRecaudoYTesoreriaE2E
{
    /// <summary>110505 CAJA GENERAL: la caja de los recibos (rol <c>Caja</c>).</summary>
    public const string Caja = "11050540";

    /// <summary>240505: el recaudo por aplicar de Cartera por la caja (rol <c>PuenteDelModulo</c> de Recaudo y <c>RecaudoPorAplicar|Receipts</c> de Cartera).</summary>
    public const string Puente = "24050599";

    public static async Task CargarAsync(HttpClient http, string admin, DateOnly desde)
    {
        if ((await CarteraE2E.GetAsync(http, admin, "/api/accounting/receipts/rules")).GetArrayLength() > 0) return;
        await ContabilidadE2E.CrearCuentaAsync(http, admin, Caja, "Caja de los recibos (e2e)", "110505", ["CNT", "REC"]);
        await ContabilidadE2E.CrearCuentaAsync(http, admin, Puente, "Recaudo por aplicar de Cartera por la caja (e2e)", "240505", ["CNT", "REC", "CAR"]);
        await MatrizAsync(http, admin, "receipts", ["CNT", "REC"], "110505", 41, "511005", 41, desde, new Dictionary<string, string>
        {
            ["Caja"] = Caja, ["PuenteDelModulo"] = Puente,
        });
        await MatrizAsync(http, admin, "treasury", ["CNT", "TES"], "110510", 41, "511010", 41, desde, []);
    }

    private static async Task MatrizAsync(HttpClient http, string admin, string modulo, string[] modulos, string padre, int desdeAuxiliar, string padreDeResultado,
        int desdeResultado, DateOnly desde, Dictionary<string, string> fijas)
    {
        var plantilla = await CarteraE2E.GetAsync(http, admin, $"/api/accounting/{modulo}/rules/template");
        var filas = plantilla.EnumerateArray().Select(f => (Hecho: f.GetProperty("factType").GetString()!, Rol: f.GetProperty("role").GetString()!)).Distinct().ToList();
        filas.Should().NotBeEmpty($"la plantilla de {modulo} sugiere filas");
        var cuentas = new Dictionary<string, Guid>(StringComparer.Ordinal);
        int siguiente = desdeAuxiliar, siguienteResultado = desdeResultado;
        foreach (var (hecho, rol) in filas)
        {
            if (!cuentas.TryGetValue(rol, out var cuenta))
            {
                var resultado = hecho != "*";
                var codigo = fijas.TryGetValue(rol, out var fija) ? fija : resultado ? $"{padreDeResultado}{siguienteResultado++:00}" : $"{padre}{siguiente++:00}";
                if (!fijas.ContainsKey(rol))
                    await ContabilidadE2E.CrearCuentaAsync(http, admin, codigo, $"{rol} de {modulo} (e2e)", resultado ? padreDeResultado : padre, modulos);
                cuenta = await ContabilidadE2E.CuentaAsync(http, admin, codigo);
                cuentas[rol] = cuenta;
            }
            var alta = await RecaudoYTesoreriaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/accounting/{modulo}/rules", new
            {
                factType = hecho, role = rol, dimensions = (object?)null, accountPublicId = cuenta, validFrom = desde, notes = "Matriz del ensayo (e2e)",
                reason = "La matriz contable completa antes de activar (e2e)",
            });
            alta.IsSuccessStatusCode.Should().BeTrue($"{modulo}: la regla {hecho}/{rol}: «{await alta.Content.ReadAsStringAsync()}»");
        }
    }
}
