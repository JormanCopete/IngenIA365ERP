using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// El escenario de la causación (feature 013, US4, bloque 8B): el de <see cref="DesembolsoDePrueba"/> —InMemory, hoy local 2026-12-01, línea
/// <c>CONLIB</c> al 20 % EA (mora 1,5 veces la corriente, Actual/365), usura de consumo 25 %— con un crédito de Ana desembolsado hoy por
/// 3.000.000 a 24 cuotas mensuales, y las piezas reales del ciclo: el cerrojo deja tomar todo lo que se pide, el actor es el proceso y el reloj se
/// mueve con <see cref="Ir"/>. Las corridas se arman a mano y se conducen con <see cref="CorrerCicloAsync"/>. (nuevo)
/// </summary>
internal sealed class CausacionDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    private CausacionDePrueba(DesembolsoDePrueba d)
    {
        D = d;
        ActorDelProceso.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Actor.ProcesoDeIntegracion(CicloDiarioDeCartera.OrigenDelDia(Hoy))));
        D.O.Cerrojo.BloquearCreditosAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.Arg<IReadOnlyCollection<int>>().Except(Ocupados).ToList()));
    }

    public static async Task<CausacionDePrueba> CrearAsync(DateOnly? usuraHasta = null, bool conDesembolso = true,
        Application.Lending.Configuration.CreditLineVersionInput? version = null)
    {
        var c = new CausacionDePrueba(await DesembolsoDePrueba.CrearAsync(version, usuraHasta));
        if (conDesembolso) c.Credito = await c.DesembolsarAsync();
        return c;
    }

    public DesembolsoDePrueba D { get; }
    public TestApplicationDbContext Db => D.Db;
    public Loan Credito { get; private set; } = null!;
    public HashSet<int> Ocupados { get; } = [];
    public ParametrosDeLaTandaDeCartera Tanda { get; } = new();

    public void Dispose() => D.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public CargadorDeReconstruccion Cargador() => D.Cargador();
    /// <summary>El actor del proceso: las tandas emiten con él (el escenario de la persona sigue para desembolsar).</summary>
    public IActorActual ActorDelProceso { get; } = Substitute.For<IActorActual>();

    public EmisionDeCartera EmisionDelProceso() =>
        new(Db, new EmisorDeMensajes(Db, ActorDelProceso, D.G.Reloj), D.FechaContable(), new ModoDePasoDeCartera(Db, D.Contabilidad), ActorDelProceso);
    public PartesDeHechosDeCartera Partes() => new(Db, EmisionDelProceso(), D.FechaContable());
    public ReajusteDeTasa Reajuste() => new(Db, D.O.L.Valores(), D.Registro(), D.Numerador());
    public VerificacionDelLibro Verificacion() => new(Db, Cargador(), D.Alertas);
    public CicloDiarioDeCartera Ciclo() => new(Db, D.O.Cerrojo, Cargador(), D.Registro(), Partes(), Reajuste(), Verificacion(), D.Parametros(), D.Alertas,
        Tanda, D.G.Reloj, NullLogger<CicloDiarioDeCartera>.Instance);
    public RunDailyAccrualCommandHandler Causar() => new(Db, Ciclo(), D.G.Reloj);
    public CorreccionDeValorNacional Correccion() =>
        new(Db, D.O.Cerrojo, Cargador(), D.Registro(), Partes(), D.FechaContable(), Tanda, D.O.L.Valores(), D.G.Reloj);
    public RunNationalValueCorrectionCommandHandler Corregir() => new(Db, Correccion(), D.G.Reloj);
    public GetLoanSettlementQueryHandler Liquidar() => new(Db, Cargador(), D.G.Alcance, D.G.Auditoria, D.G.Reloj);
    public GetLoanStatementQueryHandler EstadoDeCuenta() => new(Db, Cargador(), D.G.Alcance, D.G.Auditoria, D.G.Reloj);
    public VerifyLendingLedgerIntegrityQueryHandler Verificar() => new(Db, Verificacion());

    // ------------------------------------------------------------------------------------------------------- escenario --


    /// <summary>Mueve el reloj a <paramref name="fecha"/> a las 10:00 locales (15:00 UTC).</summary>
    public void Ir(DateOnly fecha)
    {
        D.G.HoyLocal = fecha;
        D.G.UtcNow = fecha.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
        D.G.Reloj.AhoraLocal.Returns(_ => new DateTimeOffset(D.G.UtcNow).ToOffset(TimeSpan.FromHours(-5)));
    }

    /// <summary>Desembolsa la solicitud aprobada de Ana (o de quien diga la entrada) y devuelve el crédito.</summary>
    public async Task<Loan> DesembolsarAsync(Application.Lending.Origination.CreditApplicationInput? entrada = null)
    {
        var s = await D.AprobadaAsync(entrada);
        var r = await D.DesembolsadoAsync(D.Entrada(s));
        return await Db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == r.LoanPublicId);
    }

    /// <summary>Los tipos de hecho sembrados (InteresCausado por lotes con FinDeCausacion…), como en una cooperativa real.</summary>
    public Task SembrarTiposDeHechoAsync() => LendingFactTypesSeeder.AplicarAsync(Db, NullLogger.Instance, CancellationToken.None);

    /// <summary>El Anexo 1 sembrado (consumo suspende desde la categoría C: 61 días).</summary>
    public async Task AnexoAsync(DateOnly desde)
    {
        var contenido = PlatformNationalValuesSeeder.Contenido("sarc-anexo-1.json");
        var replica = new NationalValueReplication
        {
            FromSequence = 1, ToSequence = 1, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = D.G.UtcNow.AddDays(-1), FinishedAt = D.G.UtcNow.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.SarcAnexo1), Code = "ANEXO1", ValidFrom = desde, Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = true,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = 90_000,
            CentralRegisteredAt = D.G.UtcNow.AddDays(-1), KnownAt = D.G.UtcNow.AddDays(-1),
        });
        Db.NationalValueReplications.Add(replica);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Un festivo nacional replicado (clave = la fecha).</summary>
    public async Task FestivoAsync(DateOnly fecha)
    {
        await IngenIA365ERP.Application.Tests.Common.NationalValues.CopiasDePrueba.SembrarAsync(Db, D.G.UtcNow.AddDays(-1),
            IngenIA365ERP.Application.Tests.Common.NationalValues.CopiasDePrueba.Copia(CodigosDeValorNacional.Festivo, fecha.ToString("yyyy-MM-dd"), fecha, null, D.G.UtcNow.AddDays(-1)));
        Db.ChangeTracker.Clear();
    }

    /// <summary>Una corrida <c>DailyCycle</c> pedida para <paramref name="fecha"/> (como la crea el conductor).</summary>
    public async Task<LendingProcessRun> CicloAsync(DateOnly fecha, ActorKind quien = ActorKind.Process)
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = fecha, Status = LendingProcessRunStatus.Requested, RequestedByKind = quien,
            ExecutionOrigin = CicloDiarioDeCartera.OrigenDelDia(fecha), RequestedAt = D.G.UtcNow,
        };
        Db.LendingProcessRuns.Add(corrida);
        await Db.SaveChangesAsync();
        return corrida;
    }

    /// <summary>Conduce la corrida tanda por tanda hasta que termina (como el conductor) y la devuelve releída.</summary>
    public async Task<LendingProcessRun> CorrerCicloAsync(LendingProcessRun corrida, int maximoDeTandas = 50)
    {
        var terminada = false;
        for (var i = 0; i < maximoDeTandas && !terminada; i++)
        {
            var r = await Causar().Handle(new RunDailyAccrualCommand(corrida.PublicId), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
            terminada = r.Value == AvanceDeCorrida.Terminada;
        }
        terminada.Should().BeTrue($"la corrida termina en a lo sumo {maximoDeTandas} tandas");
        Db.ChangeTracker.Clear();
        return await Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == corrida.Id);
    }

    /// <summary>Pide y corre el ciclo de <paramref name="fecha"/> con el reloj en esa fecha.</summary>
    public async Task<LendingProcessRun> CausarHastaAsync(DateOnly fecha)
    {
        Ir(fecha);
        return await CorrerCicloAsync(await CicloAsync(fecha));
    }

    public async Task<LoanControl> ControlAsync(int? loanId = null) =>
        await Db.LoanControls.AsNoTracking().SingleAsync(c => c.LoanId == (loanId ?? Credito.Id));
}
