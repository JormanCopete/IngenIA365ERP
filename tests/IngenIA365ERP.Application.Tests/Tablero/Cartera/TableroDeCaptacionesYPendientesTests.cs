using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Cartera;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Tablero.Cartera;

/// <summary>2026-11-17 (pedido del dueño): los saldos de captaciones y aportes y los pendientes de Cartera del tablero de inicio.</summary>
public sealed class TableroDeCaptacionesYPendientesTests : IDisposable
{
    private const int Oficina = 3;
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAlcanceDeCartera _alcance = Substitute.For<IAlcanceDeCartera>();
    private AlcanceDeCartera _alcanceActual = AlcanceDeCartera.Total;

    public TableroDeCaptacionesYPendientesTests() =>
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_alcanceActual));

    public void Dispose() => _db.Dispose();

    private static ContextoDeIndicador Contexto() => new(false, null, null, Hoy);

    private static AlcanceDeCartera OtraOficina() => new(false, new HashSet<int> { Oficina + 1000 }, null, true, new HashSet<int>());

    private MemberAccount Cuenta(LineNature naturaleza, DepositClass? clase, string numero)
    {
        var cuenta = MemberAccount.Abrir(1, naturaleza, clase, 1, Oficina, HandlingMode.Indistinct, Hoy.AddMonths(-3), 5, Ahora);
        cuenta.Number = numero;
        _db.MemberAccounts.Add(cuenta);
        _db.SaveChanges();
        return cuenta;
    }

    private void Fila(MemberAccount c, MemberAccountConcept concepto, decimal monto, DateOnly dia) => _db.MemberAccountTransactions.Add(new MemberAccountTransaction
    {
        MemberAccountId = c.Id, OperationId = 1, Kind = MemberAccountTransactionKind.Deposit, Concept = concepto, Amount = monto,
        ValueDate = dia, AccountingDate = dia, RegisteredAt = Ahora,
    });

    private static ResultadoDeIndicador Valor(Result<ResultadoDeIndicador> r)
    {
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Los_aportes_suman_sus_cuatro_clases_al_dia_y_al_cierre_del_mes_anterior()
    {
        var aportes = Cuenta(LineNature.Contribution, null, "AP-1");
        var ahorro = Cuenta(LineNature.Deposit, DepositClass.Demand, "AH-1");
        Fila(aportes, MemberAccountConcept.ContributionMinimum, 300_000m, new DateOnly(2026, 11, 10));
        Fila(aportes, MemberAccountConcept.ContributionOrdinary, 40_000m, Hoy);
        Fila(aportes, MemberAccountConcept.ContributionOrdinary, 1m, Hoy.AddDays(1));
        Fila(ahorro, MemberAccountConcept.Principal, 500_000m, Hoy);
        _db.SaveChanges();

        var r = Valor(await new SaldoDeAportes(_db, _alcance).CalcularAsync(Contexto(), default));

        r.Valor.Should().Be(340_000m, "mínimo y ordinario hasta hoy; ni lo de mañana ni el ahorro");
        r.Anterior.Should().Be(300_000m);
    }

    [Fact]
    public async Task Los_ahorros_suman_capital_e_interes_por_pagar_y_respetan_el_alcance()
    {
        var ahorro = Cuenta(LineNature.Deposit, DepositClass.Demand, "AH-1");
        var aportes = Cuenta(LineNature.Contribution, null, "AP-1");
        Fila(ahorro, MemberAccountConcept.Principal, 500_000m, new DateOnly(2026, 11, 20));
        Fila(ahorro, MemberAccountConcept.InterestPayable, 12.5m, Hoy);
        Fila(aportes, MemberAccountConcept.ContributionMinimum, 300_000m, Hoy);
        _db.SaveChanges();
        var indicador = new SaldoDeCaptaciones(_db, _alcance);

        var r = Valor(await indicador.CalcularAsync(Contexto(), default));
        r.Valor.Should().Be(500_012.5m);
        r.Anterior.Should().Be(500_000m);

        _alcanceActual = OtraOficina();
        Valor(await indicador.CalcularAsync(Contexto(), default)).Valor.Should().Be(0m, "la cuenta es de una oficina fuera de su alcance");
    }

    // ------------------------------------------------------------------------------------------------------ pendientes --

    private void Solicitud(CreditApplicationStatus estado, ApplicationKind clase = ApplicationKind.Loan, DateOnly? vigenteHasta = null, bool borrada = false) =>
        _db.CreditApplications.Add(new CreditApplication
        {
            ApplicantPersonId = 1, DebtorKind = DebtorKind.Associate, BranchId = Oficina, CreditLineId = 1, CreditLineVersionId = 1, RequestedAmount = 1_000_000m,
            RepaymentChannel = RepaymentChannel.Cashier, Status = estado, Kind = clase, ApprovalValidUntil = vigenteHasta, IsDeleted = borrada, CreatedBy = "test",
        });

    [Fact]
    public async Task Las_solicitudes_en_decision_cuentan_las_del_alcance_que_esperan_decision()
    {
        Solicitud(CreditApplicationStatus.PendingDecision);
        Solicitud(CreditApplicationStatus.PendingDecision);
        Solicitud(CreditApplicationStatus.PendingDecision, borrada: true);
        Solicitud(CreditApplicationStatus.Draft);
        Solicitud(CreditApplicationStatus.Approved);
        _db.SaveChanges();
        var pendiente = new SolicitudesEnDecision(_db, _alcance);

        (await pendiente.ContarAsync(Contexto(), default)).Should().Be(2);

        _alcanceActual = OtraOficina();
        (await pendiente.ContarAsync(Contexto(), default)).Should().Be(0);
    }

    [Fact]
    public async Task Las_aprobadas_sin_desembolsar_son_creditos_con_la_aprobacion_vigente()
    {
        Solicitud(CreditApplicationStatus.Approved);
        Solicitud(CreditApplicationStatus.Approved, vigenteHasta: Hoy);
        Solicitud(CreditApplicationStatus.Approved, vigenteHasta: Hoy.AddDays(-1));
        Solicitud(CreditApplicationStatus.Approved, ApplicationKind.RevolvingLimit);
        Solicitud(CreditApplicationStatus.Disbursed);
        _db.SaveChanges();

        (await new AprobadasSinDesembolsar(_db, _alcance).ContarAsync(Contexto(), default)).Should().Be(2,
            "la vencida no se desembolsa, el cupo se otorga y la desembolsada ya salió");
    }

    [Fact]
    public async Task Mis_decisiones_cuenta_lo_que_el_usuario_puede_decidir_en_todas_las_paginas()
    {
        static PendingLendingDecisionDto Item(bool puede) => new("CreditApplication", Guid.NewGuid(), "S-1", "Solicitud", 1m, "N1", "Nivel 1", null,
            puede ? RutasDePendiente.Individual : RutasDePendiente.Sesion, null, puede, puede ? null : RutasDePendiente.ExcluidoPorRadicar);
        var sender = Substitute.For<ISender>();
        var primera = Enumerable.Range(0, PageRequest.MaxPageSize).Select(i => Item(i % 2 == 0)).ToList();
        var segunda = new List<PendingLendingDecisionDto> { Item(true), Item(false) };
        sender.Send(Arg.Is<ListPendingLendingDecisionsQuery>(q => q.Page == 1), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<PendingLendingDecisionDto>(primera, 1, PageRequest.MaxPageSize, primera.Count + segunda.Count)));
        sender.Send(Arg.Is<ListPendingLendingDecisionsQuery>(q => q.Page == 2), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<PendingLendingDecisionDto>(segunda, 2, PageRequest.MaxPageSize, primera.Count + segunda.Count)));

        (await new MisDecisionesDeCartera(sender).ContarAsync(Contexto(), default)).Should().Be(PageRequest.MaxPageSize / 2 + 1);
        await sender.DidNotReceive().Send(Arg.Is<ListPendingLendingDecisionsQuery>(q => q.Page == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Cada_ruta_de_cartera_es_una_pagina_del_menu()
    {
        var rutas = new[]
        {
            "/cartera/tablero", "/cartera/informes", "/cartera/aportes", "/cartera/solicitudes", "/cartera/aprobaciones", "/cartera/desembolsos",
        };
        var raiz = RaizDelRepositorio();
        var paginas = Directory.EnumerateFiles(Path.Combine(raiz, "src", "Presentation", "IngenIA365ERP.Shared", "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).Where(l => l.StartsWith("@page \"", StringComparison.Ordinal)).Select(l => l[7..l.IndexOf('"', 7)]).ToHashSet();

        var definidas = new IIndicadorDelTablero[]
            {
                new SaldoDeCartera(null!, null!), new CarteraVencida(null!, null!, null!), new MoraPorRangos(null!, null!, null!),
                new DesembolsosDelMes(null!, null!), new SaldoDeAportes(null!, null!), new SaldoDeCaptaciones(null!, null!),
            }.Select(i => i.Definicion.Ruta!)
            .Concat(new IPendienteDelTablero[]
            {
                new SolicitudesEnDecision(null!, null!), new MisDecisionesDeCartera(null!), new AprobadasSinDesembolsar(null!, null!),
            }.Select(p => p.Definicion.Ruta))
            .Select(r => r.Split('?')[0]).ToList();

        definidas.Should().BeSubsetOf(rutas);
        rutas.Should().OnlyContain(r => paginas.Contains(r), "cada tarjeta lleva a una pantalla que existe");
    }

    private static string RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Presentation"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}
