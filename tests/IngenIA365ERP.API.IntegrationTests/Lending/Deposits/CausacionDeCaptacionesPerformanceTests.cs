using System.Diagnostics;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Ventas;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// T141 (feature 014, US5; SC-006), en el motor de <c>DB_PROVIDER</c> y sólo con <c>RUN_PERF_TESTS=1</c> (sin ella se reporta omitida con su motivo): la
/// causación de 100.000 cuentas de ahorro a la vista en el paso de captaciones del ciclo diario termina en menos de 30 minutos. Las cuentas nacen en la base
/// de la cooperativa en bloque —cuenta activa, su fila de control y una consignación de apertura en el libro—, porque abrir 100.000 por la API mediría la
/// apertura y no la causación; después se ordena el ciclo del día siguiente por la ruta y se corre a mano, y se comprueba que todas quedaron causadas. El
/// tamaño se baja con <c>CAPTACIONES_CUENTAS_DE_VOLUMEN</c> para probar el arnés.
/// <b>e2e en la corrida final</b> (decisión del dueño): se escribe y compila; no se corre por fase. (nuevo)
/// </summary>
[Collection(CaptacionesCollection.Nombre)]
public sealed class CausacionDeCaptacionesPerformanceTests(CentralIdentityApiFixture fx, ITestOutputHelper salida)
{
    private const int CuentasPorDefecto = 100_000;
    private const int Bloque = 5_000;
    private static readonly TimeSpan Limite = TimeSpan.FromMinutes(30);

    [FactDeRendimiento]
    public async Task La_causacion_de_cien_mil_cuentas_termina_en_menos_de_treinta_minutos()
    {
        var cuantas = int.TryParse(Environment.GetEnvironmentVariable("CAPTACIONES_CUENTAS_DE_VOLUMEN"), out var n) && n > 0 ? n : CuentasPorDefecto;
        var coop = await CaptacionesE2E.CooperativaConCaptacionesAsync(fx, "volumen_causacion");
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop.Cooperativa);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop.Cooperativa);
        var linea = await MovimientosTests.LineaALaVistaAsync(http, admin, hoy, "AHVOLUMEN");
        var titular = await MovimientosTests.AsociadoAsync(http, admin, "Volumen");
        var modelo = await MovimientosTests.CuentaAsync(http, admin, usuarios.O1.AgenciaPublicId, linea, titular);
        await MovimientosTests.ConsignarAsync(http, admin, modelo, titular, 1_000_000m, hoy, medio: "Transfer");

        // Las cuentas en bloque, copiando la línea, la oficina y el titular de la cuenta modelo.
        var reloj = Stopwatch.StartNew();
        await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, async db =>
        {
            var base_ = await db.MemberAccounts.AsNoTracking().SingleAsync(a => a.PublicId == modelo);
            var ahora = DateTime.UtcNow;
            for (var inicio = 0; inicio < cuantas; inicio += Bloque)
            {
                var cuentas = Enumerable.Range(inicio, Math.Min(Bloque, cuantas - inicio)).Select(i =>
                {
                    var c = MemberAccount.Abrir(base_.CreditLineId, LineNature.Deposit, DepositClass.Demand, base_.HolderPersonId, base_.BranchId,
                        HandlingMode.Indistinct, hoy, null, ahora);
                    c.Number = $"VOL-{i:0000000}";
                    c.Activar(hoy, ahora);
                    c.CreatedBy = "volumen";
                    return c;
                }).ToList();
                db.MemberAccounts.AddRange(cuentas);
                await db.SaveChangesAsync();
                foreach (var c in cuentas)
                {
                    db.MemberAccountControls.Add(new MemberAccountControl { MemberAccountId = c.Id, LastAccruedDate = hoy, Sequence = 1, LastRegisteredAt = ahora });
                    var op = new MemberAccountOperation
                    {
                        MemberAccountId = c.Id, Kind = MemberAccountOperationKind.Deposit, Number = $"VOP-{c.Number}", Sequence = 1, ValueDate = hoy,
                        AccountingDate = hoy, RegisteredAt = ahora, Amount = 1_000_000m, ActorKind = ActorKind.Process, Payload = "{}", InputsSha256 = "volumen",
                        RulesVersion = "volumen", CreatedBy = "volumen",
                    };
                    db.MemberAccountOperations.Add(op);
                    db.MemberAccountTransactions.Add(new MemberAccountTransaction
                    {
                        MemberAccountId = c.Id, Operation = op, Kind = MemberAccountTransactionKind.Deposit, Concept = MemberAccountConcept.Principal,
                        Amount = 1_000_000m, ValueDate = hoy, AccountingDate = hoy, RegisteredAt = ahora, CreatedBy = "volumen",
                    });
                }
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
            return 0;
        });
        salida.WriteLine($"{cuantas} cuentas creadas en {reloj.Elapsed}.");

        reloj.Restart();
        using (CausacionE2E.RelojEn(fx, hoy.AddDays(1)))
            await CausacionE2E.CicloDeHoyAsync(fx, http, admin, coop.TenantPublicId, tamano: 1_000);
        reloj.Stop();
        salida.WriteLine($"Ciclo con la causación de {cuantas} cuentas: {reloj.Elapsed}.");

        var causadas = await CausacionE2E.EnLaBaseAsync(fx, coop.TenantPublicId, db =>
            db.MemberAccountControls.AsNoTracking().CountAsync(c => c.LastAccruedDate == hoy.AddDays(1)));
        causadas.Should().BeGreaterThanOrEqualTo(cuantas, "todas las cuentas quedan causadas al día");
        reloj.Elapsed.Should().BeLessThan(Limite, "SC-006: 100.000 cuentas en menos de 30 minutos");
    }
}
