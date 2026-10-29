using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0481 (feature 013, US4; quickstart §1.7, nombre fijo; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la causación diaria y la
/// liquidación por HTTP con tokens reales, en cooperativas aisladas activadas con <see cref="CarteraE2E.ActivarCarteraAsync"/> (C107), con el
/// procesador apagado (<c>Lending:Process:Enabled = false</c>) y el ciclo ordenado por <c>POST /process-runs</c> y corrido a mano con
/// <c>RunDailyAccrualCommand</c> y el reloj de la suite. Cada prueba tiene su cooperativa: el ciclo causa todos los créditos vivos de la
/// cooperativa y la fecha de cada ciclo es única.
/// <list type="bullet">
/// <item><see cref="El_ciclo_causa_se_pone_al_dia_suspende_y_liquida_igual_a_cualquier_fecha"/>: 202 y <c>AlreadyRunning</c>; repetir la tanda no
/// escribe; un día sin correr se pone al día con un día por fila; la vencida causa mora sólo sobre lo vencido; a los 61 días de consumo lo nuevo
/// va a suspenso con <c>CausacionSuspendida</c>; la liquidación pasada, de hoy y futura repetida es idéntica, y con R anterior a un reverso
/// registrado después no cambia (C83, SC-001); una modalidad sin usura frena su crédito y no a los demás; la verificación del libro no encuentra
/// diferencias; la auditoría queda en la base de auditoría de la cooperativa (por su PublicId).</item>
/// <item><see cref="Los_dos_motores_saltan_lo_ocupado_versionan_la_corrida_y_guardan_lo_que_InMemory_no_prueba"/>: <c>SKIP LOCKED</c> /
/// <c>READPAST</c> con un crédito ocupado por otra transacción; dos corridas a la vez sobre los mismos créditos no causan dos veces; la corrida
/// se guarda tanda tras tanda después de soltar y volver a adjuntarla, con su <c>RowVersion</c>; el índice único de las partes y la precisión de
/// las columnas.</item>
/// </list>
/// (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CausacionYLiquidacionTests(CentralIdentityApiFixture fx)
{
    /// <summary>La modalidad sin usura a mitad del ensayo: ningún otro ensayo del host «Cartera e2e» la usa.</summary>
    private const string ModalidadSinUsura = "PRODUCTIVO_URBANO";

    [Fact]
    public async Task El_ciclo_causa_se_pone_al_dia_suspende_y_liquida_igual_a_cualquier_fecha()
    {
        var esc = await EscenarioAsync(fx, "causacion");
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var jefe = esc.Usuarios.Jefe.Token;
        var admin = esc.Coop.TokenAdmin;
        var h = esc.Hoy;
        var desde = DateTime.UtcNow.AddMinutes(-1);

        // La línea de una modalidad cuya usura certificada termina a los 20 días (C107: la central es una por host; nadie más la usa).
        var finDeLaUsura = h.AddDays(20);
        var lineaSinUsura = await LineaConUsuraHastaAsync(http, esc, finDeLaUsura);

        var (_, _, a) = await CreditoDesembolsadoAsync(http, esc, "Causa", 3_000_000m);
        var (_, _, b) = await CreditoDesembolsadoAsync(http, esc, "Pone", 2_000_000m);
        var (_, _, c) = await CreditoDesembolsadoAsync(http, esc, "Reversa", 1_500_000m, transferencia: true); // T0299: rechazo de una transferencia
        var (personaD, _) = await DeudorAsync(http, admin, "SinUsura");
        var (solicitudD, _) = await AprobadaAsync(http, esc, personaD, 2_000_000m, lineaSinUsura);
        var d = await DesembolsarAsync(http, esc, Desembolso(esc, solicitudD, personaD));
        var (creditoA, creditoB, creditoC, creditoD) = (Credito(a), Credito(b), Credito(c), Credito(d));
        foreach (var x in new[] { creditoA, creditoB, creditoC, creditoD })
            (await MarcaAsync(fx, tenant, x)).Should().Be(h, "el desembolso deja la marca en su fecha valor");

        // (1) La liquidación de hoy y la futura: repetida, idéntica al centavo y con la misma huella; la futura dice hasta cuándo vale.
        var hoy1 = await LiquidacionAsync(http, jefe, creditoA, h);
        var hoy2 = await LiquidacionAsync(http, jefe, creditoA, h);
        Huella(hoy2).Should().Be(Huella(hoy1));
        hoy1.GetProperty("toPayOff").GetDecimal().Should().Be(3_000_000m, "el día del desembolso no hay interés");
        hoy1.GetProperty("validUntil").ValueKind.Should().Be(JsonValueKind.Null);
        var futura1 = await LiquidacionAsync(http, jefe, creditoA, h.AddDays(40));
        var futura2 = await LiquidacionAsync(http, jefe, creditoA, h.AddDays(40));
        Huella(futura2).Should().Be(Huella(futura1));
        futura2.GetProperty("toPayOff").GetDecimal().Should().Be(futura1.GetProperty("toPayOff").GetDecimal()).And.BeGreaterThan(3_000_000m);
        futura1.GetProperty("validUntil").ValueKind.Should().Be(JsonValueKind.Null, "la usura de consumo de este host no tiene fin: no hay hasta cuándo");
        futura1.GetProperty("explanation").GetArrayLength().Should().BePositive();
        // Con la usura certificada hasta un día, la fecha futura se proyecta con la última conocida y dice hasta cuándo vale (D11).
        var futuraD = await LiquidacionAsync(http, jefe, creditoD, h.AddDays(40));
        futuraD.GetProperty("validUntil").GetString().Should().Be(finDeLaUsura.ToString("yyyy-MM-dd"));
        Huella(await LiquidacionAsync(http, jefe, creditoD, h.AddDays(40))).Should().Be(Huella(futuraD));

        Guid primera;
        DateTime informacionHasta;
        JsonElement liquidacionDeC;
        using (RelojEn(fx, h.AddDays(3)))
        {
            // (2) Ordenar responde 202 y sólo encola; una segunda orden del mismo día, AlreadyRunning; mañana no se ordena.
            primera = await OrdenarCicloAsync(http, jefe);
            var otra = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Corridas, new { kind = "DailyCycle" });
            await InventarioE2E.FallaAsync(otra, "Lending.Process.AlreadyRunning");
            var futura = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Corridas, new { kind = "DailyCycle", date = h.AddDays(4) });
            await InventarioE2E.FallaAsync(futura, "Lending.ProcessRun.FutureDate");
            (await CorridaAsync(http, jefe, primera)).GetProperty("run").GetProperty("status").GetString().Should().Be("Requested");

            var tandas = await CorrerHastaTerminarAsync(fx, tenant, primera, tamano: 2);
            tandas.Should().BeGreaterThanOrEqualTo(3, "cuatro créditos en tandas de dos y la que cierra");
            var corrida = await CorridaAsync(http, jefe, primera);
            corrida.GetProperty("run").GetProperty("status").GetString().Should().Be("Completed");
            corrida.GetProperty("run").GetProperty("requestedBy").GetProperty("kind").GetString().Should().Be("Person", "la ordenó el jefe (T8)");
            corrida.GetProperty("processedLoans").GetInt32().Should().Be(4);
            corrida.GetProperty("executionOrigin").GetString().Should().StartWith("Orden:");

            foreach (var x in new[] { creditoA, creditoB, creditoC, creditoD })
            {
                (await MarcaAsync(fx, tenant, x)).Should().Be(h.AddDays(3));
                var filas = await CausacionAsync(fx, tenant, x);
                filas.Where(f => f.Concepto == LoanConcept.CurrentInterest).Select(f => f.Dia).Distinct().Should().Equal(h.AddDays(1), h.AddDays(2), h.AddDays(3));
                SinDuplicados(filas);
            }

            // (3) Repetir la tanda o la orden no escribe nada.
            var antes = (await CausacionAsync(fx, tenant, creditoA)).Count;
            (await TandaAsync(fx, tenant, primera)).Should().Be(Application.Lending.Processes.AvanceDeCorrida.Terminada);
            (await CausacionAsync(fx, tenant, creditoA)).Should().HaveCount(antes, "repetir la tanda no causa dos veces (US4-1)");
            var repetida = await CarteraE2E.ConClaveAsync(http, jefe, HttpMethod.Post, Corridas, new { kind = "DailyCycle" });
            await InventarioE2E.FallaAsync(repetida, "Lending.ProcessRun.AlreadyCompleted");

            // (4) La liquidación de C a hoy, y el instante hasta el que se sabía (R).
            liquidacionDeC = await LiquidacionAsync(http, jefe, creditoC, h.AddDays(3));
            informacionHasta = new Integration.ConductorDelDespachador(fx).Reloj.UtcNow;
            await Task.Delay(TimeSpan.FromMilliseconds(50));

            // (5) Tesorería rechaza el giro de C: el desembolso se reversa desde su origen, registrado DESPUÉS de R (con el reloj del mismo día:
            // el registro nunca va hacia atrás, RegisteredAt = max(ahora, último + 1 tick)). Con R la liquidación no cambia.
            var mensaje = await ResultadoDelGiroAsync(fx, http, esc, c.GetProperty("order").GetProperty("publicId").GetGuid(), "Rejected");
            (await ConsumirAsync(fx, esc, mensaje)).Should().BeOfType<ResultadoDeConsumo.Processed>();
            var conR = await LiquidacionAsync(http, jefe, creditoC, h.AddDays(3), informacionHasta);
            Huella(conR).Should().Be(Huella(liquidacionDeC), "con la información hasta R, el reverso registrado después no cuenta (US4-6, C83)");
            conR.GetProperty("toPayOff").GetDecimal().Should().Be(liquidacionDeC.GetProperty("toPayOff").GetDecimal());
            using var sinR = await CarteraE2E.MandarGetAsync(http, jefe, $"{Creditos}/{creditoC}/settlement?date={h.AddDays(3):yyyy-MM-dd}");
            if (sinR.StatusCode == HttpStatusCode.OK)
                Huella(await CarteraE2E.LeerAsync(sinR)).Should().NotBe(Huella(liquidacionDeC), "con toda la información el desembolso ya no está");
        }

        // (6) Un día sin correr: el ciclo de h+6 pone al día h+4, h+5 y h+6, cada día en su fila.
        using (RelojEn(fx, h.AddDays(6)))
        {
            var salto = await CicloDeHoyAsync(fx, http, jefe, tenant);
            foreach (var x in new[] { creditoA, creditoB })
            {
                (await MarcaAsync(fx, tenant, x)).Should().Be(h.AddDays(6));
                var filas = await CausacionAsync(fx, tenant, x);
                filas.Where(f => f.Concepto == LoanConcept.CurrentInterest).Select(f => f.Dia).Distinct().Order()
                    .Should().Equal(Enumerable.Range(1, 6).Select(i => h.AddDays(i)), "cada día causado una vez, el saltado también (US4-4)");
                SinDuplicados(filas);
            }
            (await MarcaAsync(fx, tenant, creditoC)).Should().Be(h.AddDays(3), "un crédito pendiente de giro no se causa");
            var partes = await EnLaBaseAsync(fx, tenant, async db =>
            {
                var id = await db.LendingProcessRuns.Where(r => r.PublicId == salto).Select(r => r.Id).SingleAsync();
                return await db.AccountingFactParts.AsNoTracking().Where(p => p.ProcessRunId == id && p.FactType == "InteresCausado")
                    .Select(p => new { p.ValueDate, p.BatchNumber, p.SourceKind }).ToListAsync();
            });
            partes.Select(p => p.ValueDate).Distinct().Should().BeEquivalentTo([h.AddDays(4), h.AddDays(5), h.AddDays(6)], "una parte por fecha causada");
            partes.Should().OnlyContain(p => p.SourceKind == AccountingFactPartSource.DailyCycleBatch);
        }

        // (7) La cuota vencida: pasada su fecha exigible el ciclo causa mora sólo sobre lo vencido; la modalidad sin usura frena sólo su crédito.
        var plan = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{creditoA}/schedule");
        var exigible = DateOnly.Parse(plan.GetProperty("installments")[0].GetProperty("exigibleDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        exigible.Should().BeAfter(finDeLaUsura);
        using (RelojEn(fx, exigible.AddDays(5)))
        {
            var vencida = await CicloDeHoyAsync(fx, http, jefe, tenant);
            var filas = await CausacionAsync(fx, tenant, creditoA);
            var mora = filas.Where(f => f.Concepto == LoanConcept.DefaultInterest).ToList();
            mora.Should().NotBeEmpty().And.OnlyContain(f => f.Cuota == 1, "la mora corre sólo sobre la cuota vencida (US4-2)");
            mora.Min(f => f.Dia).Should().Be(exigible.AddDays(1), "el capital vence el día siguiente a la fecha exigible");
            SinDuplicados(filas);
            var lista = await CarteraE2E.GetAsync(http, jefe, $"{Creditos}?search={Uri.EscapeDataString(a.GetProperty("loanNumber").GetString()!)}");
            var enLista = lista.GetProperty("items").EnumerateArray().Single();
            enLista.GetProperty("daysPastDue").GetInt32().Should().Be(5, "la posición diaria vigente lleva la mora");
            enLista.GetProperty("positionDate").GetString().Should().Be(exigible.AddDays(5).ToString("yyyy-MM-dd"));

            // NationalValues.Missing: D se causa hasta el último día con usura y la corrida no falla por él; los demás siguen.
            (await MarcaAsync(fx, tenant, creditoD)).Should().Be(finDeLaUsura);
            (await MarcaAsync(fx, tenant, creditoB)).Should().Be(exigible.AddDays(5));
            var detalle = await CorridaAsync(http, jefe, vencida);
            detalle.GetProperty("run").GetProperty("status").GetString().Should().Be("Completed", "un faltante de grupo no es un crédito fallido");
            detalle.GetProperty("errors").GetProperty("items").EnumerateArray().Should().Contain(e =>
                e.GetProperty("code").GetString() == "NationalValues.Missing" && e.GetProperty("loanPublicId").ValueKind == JsonValueKind.Null);
            using var sinUsura = await CarteraE2E.MandarGetAsync(http, jefe, $"{Creditos}/{creditoD}/settlement?date={exigible.AddDays(5):yyyy-MM-dd}");
            await InventarioE2E.FallaAsync(sinUsura, "NationalValues.Missing");
        }

        // (8) Al cruzar los 61 días de consumo lo nuevo va a suspenso; lo causado antes no se reclasifica.
        using (RelojEn(fx, exigible.AddDays(62)))
        {
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            var filas = await CausacionAsync(fx, tenant, creditoA);
            var suspenso = filas.Where(f => f.Concepto == LoanConcept.CurrentInterestInSuspense).ToList();
            suspenso.Should().NotBeEmpty("desde el umbral, el interés corriente va a suspenso (US4-3)");
            suspenso.Min(f => f.Dia).Should().Be(exigible.AddDays(61));
            filas.Should().NotContain(f => f.Clase == LoanTransactionKind.SuspenseReclassification, "lo causado antes no se reclasifica");
            filas.Where(f => f.Concepto == LoanConcept.CurrentInterest && f.Clase == LoanTransactionKind.Accrual).Max(f => f.Dia)
                .Should().BeBefore(exigible.AddDays(61));
            var suspendida = await EnLaBaseAsync(fx, tenant, db => db.IntegrationMessages.AsNoTracking()
                .Where(m => m.Type == "CausacionSuspendida" && m.OriginEventKey == $"Confirmation:{creditoA:N}").CountAsync());
            suspendida.Should().Be(1, "un CausacionSuspendida por crédito (C02)");
            (await CarteraE2E.GetAsync(http, jefe, $"{Creditos}/{creditoA}")).GetProperty("marks").GetProperty("suspended").GetBoolean().Should().BeTrue();

            // (9) La liquidación de una fecha pasada, repetida, es idéntica; la verificación del libro no encuentra diferencias.
            var pasada1 = await LiquidacionAsync(http, jefe, creditoA, exigible.AddDays(10));
            var pasada2 = await LiquidacionAsync(http, jefe, creditoA, exigible.AddDays(10));
            Huella(pasada2).Should().Be(Huella(pasada1));
            pasada1.GetProperty("toPayOff").GetDecimal().Should().Be(pasada2.GetProperty("toPayOff").GetDecimal());
            pasada1.GetProperty("daysPastDue").GetInt32().Should().Be(10);

            var verificacion = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Integridad, new { loanPublicIds = new[] { creditoA, creditoB, creditoD } });
            verificacion.GetProperty("checked").GetInt32().Should().Be(3);
            verificacion.GetProperty("mismatches").GetArrayLength().Should().Be(0, "el libro es igual a la reconstrucción (T33)");
            var muestra = await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Integridad, new { sampleSize = 10 });
            muestra.GetProperty("mismatches").GetArrayLength().Should().Be(0);
        }

        // (10) Las corridas por la ruta, sin datos del deudor.
        var corridas = await CarteraE2E.GetAsync(http, jefe, $"{Corridas}?kind=DailyCycle&pageSize=50");
        corridas.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("publicId").GetGuid()).Should().Contain(primera);
        corridas.GetProperty("items").EnumerateArray().Should().OnlyContain(r => r.GetProperty("kind").GetString() == "DailyCycle");

        // (11) La auditoría quedó en la base de auditoría de esta cooperativa (se escribe y se lee por su PublicId; lección de la 009).
        await fx.ReenviarAuditoriaAsync(tenant);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var eventos = await EventosAsync(http, admin, desde);
        eventos.Should().Contain(e => Texto(e, "action") == AuditEventTypes.LendingPayoffViewed && e.GetRawText().Contains(creditoA.ToString(), StringComparison.OrdinalIgnoreCase),
            "la liquidación es una lectura auditada (T67)");
        eventos.Should().Contain(e => e.GetRawText().Contains(primera.ToString(), StringComparison.OrdinalIgnoreCase),
            "la orden de la corrida deja su rastro en la auditoría de la cooperativa");
    }

    [Fact]
    public async Task Los_dos_motores_saltan_lo_ocupado_versionan_la_corrida_y_guardan_lo_que_InMemory_no_prueba()
    {
        var esc = await EscenarioAsync(fx, "causacion_motores");
        var tenant = esc.Coop.TenantPublicId;
        using var http = fx.CreateClient();
        var jefe = esc.Usuarios.Jefe.Token;
        var h = esc.Hoy;
        var creditos = new List<Guid>();
        foreach (var nombre in new[] { "Ocupado", "Libre", "Tercero" })
            creditos.Add(Credito((await CreditoDesembolsadoAsync(http, esc, nombre, 1_000_000m)).Desembolso));
        var ocupado = creditos[0];

        // (1) SKIP LOCKED / READPAST: con el crédito ocupado por otra transacción, la tanda lo salta y causa los demás, en tandas de uno.
        Guid primera = Guid.Empty;
        using (RelojEn(fx, h.AddDays(1)))
        {
            await ConCreditosOcupadosAsync(fx, tenant, [ocupado], async () =>
            {
                primera = await OrdenarCicloAsync(http, jefe);
                (await CorrerHastaTerminarAsync(fx, tenant, primera, tamano: 1)).Should().BeGreaterThanOrEqualTo(4, "tres créditos en tandas de uno y la que cierra");
            });
            (await MarcaAsync(fx, tenant, ocupado)).Should().Be(h, "el ocupado se saltó: lo toma el ciclo siguiente");
            (await MarcaAsync(fx, tenant, creditos[1])).Should().Be(h.AddDays(1));
            (await MarcaAsync(fx, tenant, creditos[2])).Should().Be(h.AddDays(1));
            (await CausacionAsync(fx, tenant, ocupado)).Should().BeEmpty();
            var corrida = await CorridaAsync(http, jefe, primera);
            corrida.GetProperty("run").GetProperty("status").GetString().Should().Be("Completed");
            corrida.GetProperty("skippedLoans").GetInt32().Should().BeGreaterThanOrEqualTo(1);

            // La corrida se guardó tanda tras tanda después de soltar y volver a adjuntarla: cursor al final, tandas contadas y su RowVersion.
            var guardada = await EnLaBaseAsync(fx, tenant, db => db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.PublicId == primera));
            var ultimo = await EnLaBaseAsync(fx, tenant, db => db.Loans.AsNoTracking().Where(l => creditos.Contains(l.PublicId)).MaxAsync(l => l.Id));
            guardada.CursorLoanId.Should().BeGreaterThanOrEqualTo(ultimo);
            guardada.BatchCount.Should().BeGreaterThanOrEqualTo(4);
            guardada.FinishedAt.Should().NotBeNull();
            guardada.ResultSha256.Should().NotBeNullOrWhiteSpace();
        }

        // (2) Dos corridas a la vez sobre los mismos créditos (la de ayer, ordenada tarde, y la de hoy) no causan ningún día dos veces.
        using (RelojEn(fx, h.AddDays(3)))
        {
            var ayer = await OrdenarCicloAsync(http, jefe, h.AddDays(2));
            var hoy = await OrdenarCicloAsync(http, jefe);
            await Task.WhenAll(CorrerHastaTerminarAsync(fx, tenant, ayer, tamano: 1), CorrerHastaTerminarAsync(fx, tenant, hoy, tamano: 1));
        }
        using (RelojEn(fx, h.AddDays(4)))
        {
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            foreach (var x in creditos)
            {
                (await MarcaAsync(fx, tenant, x)).Should().Be(h.AddDays(4));
                var filas = await CausacionAsync(fx, tenant, x);
                SinDuplicados(filas);
                filas.Where(f => f.Concepto == LoanConcept.CurrentInterest).Select(f => f.Dia).Distinct().Order()
                    .Should().Equal(Enumerable.Range(1, 4).Select(i => h.AddDays(i)), "cada día una vez, aunque dos tandas compitan (T34)");
            }
            var verificacion = await CarteraE2E.ExitoAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, Integridad, new { loanPublicIds = creditos });
            verificacion.GetProperty("mismatches").GetArrayLength().Should().Be(0);
        }

        // (3) RowVersion: dos lectores de la misma corrida; el segundo que guarda pierde (DbUpdateConcurrencyException) en los dos motores.
        await EnLaBaseAsync(fx, tenant, async uno =>
        {
            await EnLaBaseAsync(fx, tenant, async otro =>
            {
                var a = await uno.LendingProcessRuns.SingleAsync(r => r.PublicId == primera);
                var b = await otro.LendingProcessRuns.SingleAsync(r => r.PublicId == primera);
                a.RetryCount += 1;
                await uno.SaveChangesAsync();
                b.RetryCount += 2;
                var perdio = async () => await otro.SaveChangesAsync();
                (await perdio.Should().ThrowAsync<Domain.Exceptions.ConcurrencyConflictException>("la corrida lleva RowVersion (data-model §0.1)"))
                    .WithInnerException<DbUpdateConcurrencyException>();
                return 0;
            });
            return 0;
        });

        // (4) El índice único de las partes: otra parte de la misma corrida, tanda, sucursal y tipo no entra.
        await EnLaBaseAsync(fx, tenant, async db =>
        {
            var id = await db.LendingProcessRuns.Where(r => r.PublicId == primera).Select(r => r.Id).SingleAsync();
            var parte = await db.AccountingFactParts.AsNoTracking().FirstAsync(p => p.ProcessRunId == id && p.FactType == "InteresCausado");
            db.AccountingFactParts.Add(new AccountingFactPart
            {
                SourceKind = parte.SourceKind, ProcessRunId = parte.ProcessRunId, BatchNumber = parte.BatchNumber, FactType = parte.FactType,
                PerLoanMessages = parte.PerLoanMessages, ValueDate = parte.ValueDate, AccountingDate = parte.AccountingDate, BranchId = parte.BranchId,
                OriginEventKey = $"Prueba:{Guid.NewGuid():N}", MessageCount = parte.MessageCount, LoanCount = parte.LoanCount, TotalAmount = parte.TotalAmount,
                TotalsByDimensionJson = parte.TotalsByDimensionJson, LoanListSha256 = parte.LoanListSha256,
            });
            var duplicada = async () => await db.SaveChangesAsync();
            (await duplicada.Should().ThrowAsync<DbUpdateException>("UK_LND_AccountingFactParts_Batch (T14)")).Which.InnerException!.Message
                .Should().Contain("UK_LND_AccountingFactParts_Batch");
            return 0;
        });

        // (5) La precisión de las columnas que InMemory no tiene: pesos (18,2), tasas (9,6) y tasas derivadas (28,18).
        foreach (var (tabla, columna, precision, escala) in new[]
                 {
                     ("LND_LoanTransactions", "Amount", 18, 2), ("LND_LoanDailyPositions", "Principal", 18, 2),
                     ("LND_LoanDailyPositions", "AppliedAnnualRate", 9, 6), ("LND_AccountingFactParts", "TotalAmount", 18, 2),
                     ("LND_LoanTermsVersions", "AnnualRate", 9, 6), ("LND_LoanTermsVersions", "ResultingEffectiveRate", 28, 18),
                     ("LND_RepaymentInstallments", "Interest", 18, 2),
                 })
        {
            var inv = new InventarioE2E.CooperativaAislada { TokenAdmin = esc.Coop.TokenAdmin, TenantPublicId = tenant, SucursalPrincipal = esc.Coop.SucursalPrincipal };
            var sql = $"SELECT CAST(numeric_precision AS int) * 100 + CAST(numeric_scale AS int) FROM information_schema.columns "
                      + $"WHERE table_name = '{tabla}' AND column_name = '{columna}'";
            var valor = Convert.ToInt32(await InventarioE2E.EscalarEnLaCooperativaAsync(fx, inv, sql, sql));
            valor.Should().Be(precision * 100 + escala, $"{tabla}.{columna} es ({precision},{escala})");
        }
        var tasa = await EnLaBaseAsync(fx, tenant, db => db.LoanDailyPositions.AsNoTracking().Where(p => p.AppliedAnnualRate != null)
            .Select(p => p.AppliedAnnualRate).FirstAsync());
        tasa.Should().Be(0.22m, "la tasa pactada de la línea vuelve exacta de la base");
    }

    // ------------------------------------------------------------------------------------------------- apoyo --

    private static Guid Credito(JsonElement desembolso) => desembolso.GetProperty("loanPublicId").GetGuid();

    private static string Huella(JsonElement liquidacion) => liquidacion.GetProperty("fingerprint").GetString()!;

    private static void SinDuplicados(List<(DateOnly Dia, LoanConcept Concepto, int? Cuota, LoanTransactionKind Clase, decimal Valor)> filas) =>
        filas.Where(f => f.Clase == LoanTransactionKind.Accrual).GroupBy(f => (f.Dia, f.Concepto, f.Cuota)).Where(g => g.Count() > 1)
            .Select(g => g.Key).Should().BeEmpty("ningún día, concepto y cuota se causa dos veces (SC-020)");

    /// <summary>
    /// La línea <c>PRODUCTIVO_URBANO</c> con su usura certificada hasta <paramref name="hasta"/> (y nada después), en el alcance de quien radica y
    /// decide.
    /// </summary>
    private async Task<Guid> LineaConUsuraHastaAsync(HttpClient http, Escenario esc, DateOnly hasta)
    {
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        await LineasE2E.RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = ModalidadSinUsura, validFrom = new DateOnly(esc.Hoy.Year, esc.Hoy.Month, 1), validTo = hasta, value = 0.60m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba de la causación (e2e)",
        });
        await ValoresNacionalesE2E.ReplicarAsync(fx, esc.Coop.TenantPublicId);
        var version = LineasE2E.VersionFija(esc.Hoy, ModalidadSinUsura, null, 0.30m, "Versión de la causación (e2e)");
        version["amounts"] = new { min = 500_000m, max = 100_000_000m };
        var linea = await LineasE2E.CrearExitoAsync(http, esc.Coop.TokenAdmin, "PRODURB", "Productivo urbano", version);
        var lineas = new List<Guid> { esc.Originacion.Origen, esc.LineaConPagare, linea };
        if (esc.Originacion.ConDocumento is { } d) lineas.Add(d);
        foreach (var u in new[] { esc.Usuarios.AsesorO1, esc.Usuarios.Analista })
            await UsuariosDeCartera.FijarLineasAsync(http, esc.Coop.TokenAdmin, u, [.. lineas]);
        return linea;
    }

    private static async Task<List<JsonElement>> EventosAsync(HttpClient http, string token, DateTime desde)
    {
        // El ciclo corrió con el reloj de la suite hasta unos noventa días adelante; la consulta admite a lo sumo 180 días.
        var hasta = desde.AddDays(175);
        var eventos = new List<JsonElement>();
        for (var pagina = 1; pagina <= 30; pagina++)
        {
            var lote = await CarteraE2E.GetAsync(http, token, $"/api/audit/logs?modules=Lending&from={desde:O}&to={hasta:O}&page={pagina}&pageSize=100");
            var items = lote.GetProperty("items").EnumerateArray().ToList();
            eventos.AddRange(items);
            if (items.Count < 100) break;
        }
        return eventos;
    }

    private static string? Texto(JsonElement e, string campo) =>
        e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
