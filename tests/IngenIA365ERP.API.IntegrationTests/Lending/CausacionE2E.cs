using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.DbContext;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de la causación diaria y la liquidación (feature 013, US4, T0481–T0483): ordenar el ciclo del día por la ruta
/// (<c>POST /process-runs</c>, 202) y correr sus tandas a mano con <see cref="RunDailyAccrualCommand"/> dentro de
/// <see cref="IEjecutorEnCooperativa"/> —como lo haría el procesador, que la fixture deja apagado— con el tamaño de tanda que pida la prueba;
/// leer el libro en la base de la cooperativa; y sostener el cerrojo de unos créditos en otra transacción para ver que la tanda los salta
/// (<c>SKIP LOCKED</c> / <c>READPAST</c>, T34). (nuevo)
/// </summary>
public static class CausacionE2E
{
    public const string Corridas = "/api/lending/process-runs";
    public const string Integridad = "/api/lending/integrity/verify";

    /// <summary>Ordena el ciclo del día del reloj de la suite (o de <paramref name="fecha"/>) con <paramref name="token"/>; exige el 202.</summary>
    public static async Task<Guid> OrdenarCicloAsync(HttpClient http, string token, DateOnly? fecha = null)
    {
        var resp = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Corridas, new { kind = "DailyCycle", date = fecha });
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted, $"ordenar el ciclo: «{await resp.Content.ReadAsStringAsync()}»");
        var cuerpo = await CarteraE2E.LeerAsync(resp);
        cuerpo.GetProperty("status").GetString().Should().Be("Requested", "ordenar sólo encola");
        resp.Headers.Location.Should().NotBeNull();
        return cuerpo.GetProperty("processRunPublicId").GetGuid();
    }

    /// <summary>Una tanda del ciclo diario, con el actor y el origen del proceso (<c>Tarea:lending.process</c>) y el tamaño dado.</summary>
    public static Task<AvanceDeCorrida> TandaAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, Guid corrida, int tamano = 250) =>
        TandaComoProcesoAsync(fx, tenantPublicId, new RunDailyAccrualCommand(corrida), tamano);

    /// <summary>Corre la corrección por valor nacional (T28) tanda por tanda hasta terminar.</summary>
    public static async Task CorrerCorreccionAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, Guid corrida, int tamano = 250)
    {
        for (var i = 1; i <= 10_000; i++)
            if (await TandaComoProcesoAsync(fx, tenantPublicId, new RunNationalValueCorrectionCommand(corrida), tamano) == AvanceDeCorrida.Terminada) return;
        throw new InvalidOperationException("La corrección no terminó en 10.000 tandas.");
    }

    private static async Task<AvanceDeCorrida> TandaComoProcesoAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, IRequest<Result<AvanceDeCorrida>> comando,
        int tamano)
    {
        var cooperativa = await CooperativaAsync(fx, tenantPublicId);
        Result<AvanceDeCorrida>? r = null;
        var origen = PasadaDeCartera.Origen;
        var ejecutada = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa,
            Actor.ProcesoDeIntegracion(origen), origen, async (servicios, ct) =>
            {
                servicios.GetRequiredService<ParametrosDeLaTandaDeCartera>().TamanoDeTanda = tamano;
                r = await servicios.GetRequiredService<ISender>().Send(comando, ct);
            }, CancellationToken.None);
        ejecutada.Should().Be(ResultadoEnCooperativa.Ejecutada);
        r!.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Corre las tandas hasta que la corrida termina; devuelve cuántas corrió (la última es la que cierra).</summary>
    public static async Task<int> CorrerHastaTerminarAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, Guid corrida, int tamano = 250)
    {
        for (var i = 1; i <= 10_000; i++)
            if (await TandaAsync(fx, tenantPublicId, corrida, tamano) == AvanceDeCorrida.Terminada) return i;
        throw new InvalidOperationException("La corrida no terminó en 10.000 tandas.");
    }

    /// <summary>Ordena el ciclo de hoy (reloj de la suite) y lo corre entero; devuelve la corrida.</summary>
    public static async Task<Guid> CicloDeHoyAsync(CentralIdentityApiFixture fx, HttpClient http, string token, Guid tenantPublicId, int tamano = 250)
    {
        var corrida = await OrdenarCicloAsync(http, token);
        await CorrerHastaTerminarAsync(fx, tenantPublicId, corrida, tamano);
        return corrida;
    }

    /// <summary>Lleva el reloj de la suite hasta <paramref name="fecha"/> (a la misma hora de hoy); se deshace al desecharlo.</summary>
    public static IDisposable RelojEn(CentralIdentityApiFixture fx, DateOnly fecha)
    {
        var conductor = new ConductorDelDespachador(fx);
        var hoy = conductor.Reloj.HoyLocal;
        return conductor.Adelantar(TimeSpan.FromDays(fecha.DayNumber - hoy.DayNumber));
    }

    /// <summary>Una consulta sobre la base de la cooperativa con el <see cref="ApplicationDbContext"/> concreto (SQL a mano, transacciones).</summary>
    public static async Task<T> EnLaBaseAsync<T>(CentralIdentityApiFixture fx, Guid tenantPublicId, Func<ApplicationDbContext, Task<T>> consulta)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None)).Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await consulta((ApplicationDbContext)ambito.Db);
    }

    /// <summary>
    /// Sostiene el cerrojo exclusivo de los <c>LND_LoanControls</c> de esos créditos en una transacción propia (el mismo SQL que espera una
    /// operación, <c>FOR UPDATE</c> / <c>UPDLOCK, ROWLOCK, HOLDLOCK</c>) mientras corre <paramref name="mientras"/>, y lo suelta al terminar.
    /// </summary>
    public static async Task ConCreditosOcupadosAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, IReadOnlyList<Guid> creditos, Func<Task> mientras)
    {
        await EnLaBaseAsync(fx, tenantPublicId, async db =>
        {
            var ids = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.PublicId)).Select(l => l.Id).ToListAsync();
            ids.Should().HaveCount(creditos.Count);
            var motor = CentralIdentityApiFixture.ProviderKey == "SqlServer"
                ? Persistence.Providers.DatabaseProvider.SqlServer
                : Persistence.Providers.DatabaseProvider.PostgreSql;
            var sentencia = Persistence.Lending.SqlDelCerrojoDeCartera.Creditos(motor, ids, Application.Lending.Common.ModoDeEspera.Esperar)!;
            // La estrategia de reintentos de los dos motores no admite una transacción abierta a mano fuera de ella.
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                var tomados = await db.Database.SqlQueryRaw<int>(sentencia.Sql).ToListAsync();
                tomados.Should().BeEquivalentTo(ids, "la prueba sostiene el cerrojo de esos créditos");
                await mientras();
                await tx.RollbackAsync();
            });
            return 0;
        });
    }

    /// <summary>La marca de agua de un crédito.</summary>
    public static Task<DateOnly> MarcaAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, Guid credito) =>
        EnLaBaseAsync(fx, tenantPublicId, db => (from c in db.LoanControls.AsNoTracking()
                                                 join l in db.Loans.AsNoTracking() on c.LoanId equals l.Id
                                                 where l.PublicId == credito
                                                 select c.LastAccruedDate).SingleAsync());

    /// <summary>Las filas de causación de un crédito: (día valor, concepto, cuota, clase, valor).</summary>
    public static Task<List<(DateOnly Dia, LoanConcept Concepto, int? Cuota, LoanTransactionKind Clase, decimal Valor)>> CausacionAsync(
        CentralIdentityApiFixture fx, Guid tenantPublicId, Guid credito) =>
        EnLaBaseAsync(fx, tenantPublicId, async db =>
            (await (from t in db.LoanTransactions.AsNoTracking()
                    join l in db.Loans.AsNoTracking() on t.LoanId equals l.Id
                    where l.PublicId == credito && t.Kind != LoanTransactionKind.Disbursement && t.Kind != LoanTransactionKind.OpeningBalance
                    select new { t.ValueDate, t.Concept, t.InstallmentNumber, t.Kind, t.Amount }).ToListAsync())
            .Select(x => (x.ValueDate, x.Concept, x.InstallmentNumber, x.Kind, x.Amount)).ToList());

    /// <summary>La corrida por la ruta (<c>GET /process-runs/{id}</c>).</summary>
    public static Task<JsonElement> CorridaAsync(HttpClient http, string token, Guid corrida) => CarteraE2E.GetAsync(http, token, $"{Corridas}/{corrida}");

    /// <summary>La liquidación por la ruta (exige el 200).</summary>
    public static Task<JsonElement> LiquidacionAsync(HttpClient http, string token, Guid credito, DateOnly? fecha = null, DateTime? informacionHasta = null)
    {
        var query = new List<string>();
        if (fecha is { } d) query.Add($"date={d:yyyy-MM-dd}");
        if (informacionHasta is { } r) query.Add($"recordedAsOf={Uri.EscapeDataString(DateTime.SpecifyKind(r, DateTimeKind.Utc).ToString("O"))}");
        return CarteraE2E.GetAsync(http, token, $"{DesembolsoE2E.Creditos}/{credito}/settlement{(query.Count > 0 ? "?" + string.Join("&", query) : string.Empty)}");
    }

    private static async Task<TenantDirectoryEntry> CooperativaAsync(CentralIdentityApiFixture fx, Guid tenantPublicId)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        return (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(c => c.PublicId == tenantPublicId);
    }
}
