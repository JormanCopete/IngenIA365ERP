using FluentAssertions;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Assignments;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// El escenario de la cobranza de K4 (feature 013, US12, bloque C; T0924–T0930): el de <see cref="MensajesDePrueba"/> —hoy martes 1 de diciembre de 2026,
/// 10:00 a. m., Ana deudora, Beto codeudor, Carla referencia, plantilla <c>MORAADMIN</c> por SMS y correo— con las etapas de la semilla, el salario mínimo de
/// la cooperativa y un crédito <b>reconstruido por el motor</b> con los días de mora que pida la prueba (<see cref="EstadoEnMora"/>): el estado que recibe el
/// paso 7 del ciclo diario es el del motor y no uno armado a mano. (nuevo)
/// </summary>
internal sealed class CobranzaDePrueba : IDisposable
{
    public CobranzaDePrueba(bool activada = true)
    {
        E = new MensajesDePrueba(activada);
        Gestor = G.Usuario(null, "gestor@coop.test");
        Gestora = G.Usuario(null, "gestora@coop.test");
        G.Valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Application.Common.Models.Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Smmlv, string.Empty,
                1_750_905m, "Pesos", null, new DateOnly(2026, 1, 1), null, null, "Decreto de prueba", 1, G.UtcNow.AddYears(-1), EstadoDeRevision.Validada)));
        G.Limite(CatalogoDeLimitesLegales.AvisoPrevioDias, 20m, "Ley 1266 de 2008; Ley 2157 de 2021");
        G.Limite(CatalogoDeLimitesLegales.AvisoPrevioUmbralSmmlv, 5m, "Ley 2157 de 2021");
        E.Plantilla("GASTOCOBR", DebtorMessagePurpose.CollectionFeeNotice, MessageChannel.Sms, null,
            "{{NombreDelTitular}}: se causa un gasto de cobranza de {{ValorDelGasto}} por la etapa {{Etapa}} el {{FechaDeCausacion}}.");
        E.Plantilla("GASTOCOBR", DebtorMessagePurpose.CollectionFeeNotice, MessageChannel.Email, "Gasto de cobranza",
            "{{NombreDelTitular}}: se causa un gasto de cobranza de {{ValorDelGasto}} por la etapa {{Etapa}} el {{FechaDeCausacion}}.");
        E.Plantilla("AVISOPREV", DebtorMessagePurpose.PriorNotice, MessageChannel.Sms, null,
            "{{NombreDelTitular}}: su obligacion {{NumeroDelCredito}} ({{ValorDeLaObligacion}}) sera reportada desde {{FechaMasTempranaDeReporte}}.");
        E.Plantilla("AVISOPREV", DebtorMessagePurpose.PriorNotice, MessageChannel.Email, "Aviso previo al reporte",
            "{{NombreDelTitular}}: su obligacion {{NumeroDelCredito}} ({{ValorDeLaObligacion}}) sera reportada desde {{FechaMasTempranaDeReporte}}.");
        G.ActuarComo(Gestor);
    }

    public MensajesDePrueba E { get; }
    public ICerrojoDeCartera Cerrojo { get; } = Substitute.For<ICerrojoDeCartera>();
    public NumeradorDeCartera Numerador() => new(Db, Cerrojo);
    public ReglasDeParametrosDeCartera Parametros() => new(G.Valores, G.Parametros());
    public Sessions.GobiernoDePrueba G => E.G;
    public TestApplicationDbContext Db => E.Db;
    public Person Ana => E.Ana;
    public Person Beto => E.Beto;
    public Person Carla => E.Carla;
    public Loan Credito => E.Credito;
    public User Gestor { get; }
    public User Gestora { get; }
    public DateOnly Hoy => G.Reloj.HoyLocal;

    public void Dispose() => E.Dispose();

    // ------------------------------------------------------------------------------------------------------------ etapas --

    /// <summary>Las etapas de la semilla de la prueba: preventiva, administrativa (mensaje), prejurídica (aviso previo y gasto) y jurídica.</summary>
    public async Task SembrarEtapasAsync(decimal tarifaDelGasto = 0.10m, decimal tope = 50_000m)
    {
        var etapas = new[]
        {
            new CollectionStageInput("PREVENTIVA", "Preventiva", -5, 0),
            new CollectionStageInput("ADMIN", "Administrativa", 1, 30, SuggestedActions: [AccionesDeEtapaCodigos.Mensaje], TemplateCodes: ["MORAADMIN"]),
            new CollectionStageInput("PREJURID", "Prejurídica", 31, 60, SuggestedActions: [AccionesDeEtapaCodigos.AvisoPrevio],
                FeeRule: new CollectionFeeRuleDto("PastDueAmount", tarifaDelGasto, tope, "OnStageEntry")),
            new CollectionStageInput("JURIDICA", "Jurídica", 61, null, IsLegal: true, SuggestedActions: [AccionesDeEtapaCodigos.EntregaAAbogado]),
        };
        (await EtapasPorDefecto.SembrarAsync(Db, etapas, default)).Should().Be(4);
    }

    // ------------------------------------------------------------------------------------------------------------ crédito --

    /// <summary>
    /// El estado que reconstruye el motor de un crédito de 3.000.000 a 12 cuotas mensuales cuya primera cuota venció hace
    /// <paramref name="dias"/> días y que no ha recibido pagos (<paramref name="pagos"/> los agrega como abonos de la fecha valor dada).
    /// </summary>
    public EstadoDelCredito EstadoEnMora(int dias, params (DateOnly Fecha, decimal Valor)[] pagos)
    {
        var desembolso = Hoy.AddDays(-dias).AddMonths(-1);
        return MotorDeCartera.Reconstruir(Entrada(desembolso, pagos), Hoy);
    }

    public static EntradaDeReconstruccion Entrada(DateOnly desembolso, params (DateOnly Fecha, decimal Valor)[] pagos)
    {
        var redondeo = RedondeoDeCartera.AlCentavo;
        var plan = new EntradaDePlan(3_000_000m, desembolso, 12, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, 0.24m,
            DayCountConvention.Thirty360, RateEquivalence.Compound, redondeo);
        var condiciones = new CondicionesDeCredito(VersionesDeReglas.Vigente, 0.24m, DefaultRateKind.RelativeToCurrent, DayCountConvention.Thirty360,
            RateEquivalence.Compound, RepaymentPeriodicity.Monthly, redondeo)
        {
            FactorDeMora = 1.5m,
        };
        var instante = (DateOnly d) => d.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
        var operaciones = new List<EntradaDeOperacion>
        {
            new("D1", LoanOperationKind.Disbursement, desembolso, instante(desembolso), 1) { Monto = 3_000_000m },
        };
        var n = 1;
        foreach (var (fecha, valor) in pagos)
            operaciones.Add(new EntradaDeOperacion($"P{++n}", LoanOperationKind.Payment, fecha, instante(fecha), n) { Monto = valor });
        var primero = new DateOnly(desembolso.Year, desembolso.Month, 1);
        var usura = Enumerable.Range(0, 18).Select(i =>
        {
            var inicio = primero.AddMonths(i);
            return new ValorDeUsura(inicio, 0.30m, instante(inicio.AddDays(-1)), i + 1, new Guid(i + 1, 0, 0, new byte[8])) { ValidTo = inicio.AddMonths(1).AddDays(-1) };
        }).ToList();
        return new EntradaDeReconstruccion(condiciones, plan, operaciones, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        {
            Usura = usura,
            UmbralesDeSuspension = [new ValorConVigencia<int>(new DateOnly(2020, 1, 1), 61)],
            Tolerancias = [new ValorConVigencia<decimal>(new DateOnly(2020, 1, 1), 1000m)],
        };
    }

    public CreditoParaCobranza EnMora(int dias, Loan? credito = null, params (DateOnly Fecha, decimal Valor)[] pagos) =>
        new(credito ?? Credito, EstadoEnMora(dias, pagos));

    // ------------------------------------------------------------------------------------------------------------ ciclo --

    public CobranzaDelCicloDiario Ciclo(EntregaDeCartera? entrega = null) =>
        new(Db, E.Cola(), new ReglasDeParametrosDeCartera(G.Valores, G.Parametros()), G.Valores, null!, G.Reloj, null, entrega);

    public async Task<LendingProcessRun> CorridaAsync()
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = Hoy, Status = LendingProcessRunStatus.Running, ExecutionOrigin = "Tarea:cartera.diario:prueba",
            RequestedByKind = ActorKind.Process, InformationCutoffAt = G.UtcNow,
        };
        Db.LendingProcessRuns.Add(corrida);
        await Db.SaveChangesAsync();
        return corrida;
    }

    /// <summary>Corre el paso 7 sobre <paramref name="creditos"/> y guarda, como lo hace el ciclo.</summary>
    public async Task<ResultadoDeCobranzaDelDia> CorrerAsync(params CreditoParaCobranza[] creditos)
    {
        var corrida = await CorridaAsync();
        var r = await Ciclo().CreditosAsync(corrida, Hoy, creditos, [], default);
        await Db.SaveChangesAsync();
        return r;
    }

    /// <summary>Un mensaje de cobranza de la etapa en la cola del crédito.</summary>
    public Task<int> MensajesDeEtapaAsync(Loan? credito = null) =>
        Db.DebtorMessages.AsNoTracking().CountAsync(m => m.LoanId == (credito ?? Credito).Id && m.OriginKind == "CollectionStage");

    /// <summary>La regla de reparto de un solo gestor (peso uno) para todos los créditos desde hoy.</summary>
    public async Task ReglaDeUnGestorAsync(User? gestor = null, string? etapa = null, int prioridad = 1)
    {
        var r = await new CreateCollectorAssignmentRuleCommandHandler(Db).Handle(new CreateCollectorAssignmentRuleCommand(prioridad, AssignmentDistribution.ByCount,
            [new CollectorWeightInput((gestor ?? Gestor).PublicId, 1m)], new DateOnly(2026, 1, 1), "Regla de la prueba", StageCode: etapa)
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    /// <summary>Las cuentas del código de acción de la etapa (los nombres de <c>AccionesDeEtapa</c>).</summary>
    public static class AccionesDeEtapaCodigos
    {
        public const string Mensaje = "Message";
        public const string AvisoPrevio = "PriorNotice";
        public const string EntregaAAbogado = "LegalHandover";
    }
}
