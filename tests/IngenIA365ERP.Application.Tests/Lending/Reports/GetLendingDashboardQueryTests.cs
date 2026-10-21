using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// T0978 (feature 013, US15, K5; FR-135; US15-1; contracts/api.md §17 «Tablero»): <see cref="GetLendingDashboardQueryHandler"/> sobre el escenario
/// de <see cref="InformesDePrueba"/> (hoy 2026-12-01, el crédito de Ana de 3.000.000 desembolsado hoy) más un crédito sembrado en otra oficina: las
/// nueve fichas con su <c>previousValue</c>, <c>unit</c>, <c>severity</c> y enlace; un usuario de una oficina sólo suma la suya (US15-1); recaudo
/// frente al esperado con el plan vigente; el deterioro del último cierre; promesas «disponible con cobranza» antes de K4; y ningún dato personal.
/// </summary>
public class GetLendingDashboardQueryTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = InformesDePrueba.Hoy;

    private static readonly string[] Fichas =
    [
        "saldo", "colocacion-del-mes", "calidad", "deterioro", "recaudo-vs-esperado", "cosechas", "cumplimiento-de-promesas",
        "mensajes-pendientes-y-rechazados", "alertas-pendientes",
    ];

    private InformesDePrueba _e = null!;
    private Branch _otra = null!;

    public async Task InitializeAsync()
    {
        _e = await InformesDePrueba.CrearAsync();
        _otra = new Branch { Name = "Oficina Sur", CreatedBy = "test" };
        _e.Db.Branches.Add(_otra);
        await _e.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _e.Dispose();
        return Task.CompletedTask;
    }

    private GetLendingDashboardQueryHandler Tablero(EntregaDeCartera? entrega = null) =>
        new(_e.Db, _e.G.Alcance, _e.D.Parametros(), _e.G.Reloj, [], null, entrega);

    private async Task<LendingDashboardDto> LeerAsync(GetLendingDashboardQuery? q = null, EntregaDeCartera? entrega = null)
    {
        var r = await Tablero(entrega).Handle(q ?? new GetLendingDashboardQuery(), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private static LendingDashboardTileDto Ficha(LendingDashboardDto t, string clave) => t.Tiles.Single(f => f.Key == clave);

    /// <summary>Un crédito de 2.000.000 en la oficina Sur, desembolsado el 15 de noviembre, con su posición al 30 de noviembre y a hoy (40 días de mora).</summary>
    private async Task<Loan> DelSurAsync()
    {
        var credito = new Loan
        {
            Number = "SUR-1", DebtorPersonId = _e.D.O.Beto.Id, DebtorKind = DebtorKind.Associate, BranchId = _otra.Id, CreditLineId = _e.Credito.CreditLineId,
            Origin = LoanOrigin.Origination, Status = LoanStatus.Active, ApprovedAmount = 2_000_000m, OpenedOn = new DateOnly(2026, 11, 15),
        };
        _e.Db.Loans.Add(credito);
        await _e.Db.SaveChangesAsync();
        _e.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Disbursement, Number = "OP-SUR-1", Sequence = 1, ValueDate = credito.OpenedOn,
            AccountingDate = credito.OpenedOn, RegisteredAt = new DateTime(2026, 11, 15), Amount = 2_000_000m,
        });
        foreach (var (fecha, dias) in new[] { (new DateOnly(2026, 11, 30), 0), (Hoy, 40) })
            _e.Db.LoanDailyPositions.Add(new LoanDailyPosition
            {
                LoanId = credito.Id, PositionDate = fecha, Version = 1, Status = LoanStatus.Active, BranchId = _otra.Id, CreditLineId = credito.CreditLineId,
                ModalityCode = "CONSUMO", Principal = 2_000_000m, DaysPastDue = dias, InputsSha256 = "x",
            });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
        return credito;
    }

    [Fact]
    public async Task Trae_las_nueve_fichas_con_su_unidad_su_anterior_y_su_enlace()
    {
        await DelSurAsync();

        var t = await LeerAsync();

        t.AsOf.Should().Be(Hoy);
        t.Tiles.Select(f => f.Key).Should().Equal(Fichas);
        Ficha(t, "saldo").Should().Match<LendingDashboardTileDto>(f => f.Value == 5_000_000m && f.PreviousValue == 2_000_000m && f.Unit == "Money");
        Ficha(t, "colocacion-del-mes").Value.Should().Be(3_000_000m, "el de Ana, hoy");
        Ficha(t, "colocacion-del-mes").PreviousValue.Should().Be(0m, "del 1 de noviembre al 1 de noviembre no hubo desembolsos");
        Ficha(t, "calidad").Should().Match<LendingDashboardTileDto>(f => f.Value == 40m && f.PreviousValue == 0m && f.Unit == "Percent",
            "2.000.000 con 40 días (más de 30) sobre 5.000.000");
        Ficha(t, "cosechas").Value.Should().Be(40m, "2.000.000 con 30 días o más de los 5.000.000 desembolsados en el año");
        Ficha(t, "deterioro").Value.Should().BeNull("todavía no hay cierre");
        Ficha(t, "deterioro").Note.Should().Contain("cierre");
        Ficha(t, "cumplimiento-de-promesas").Value.Should().BeNull("sin promesas en el mes");
        Ficha(t, "mensajes-pendientes-y-rechazados").Should().Match<LendingDashboardTileDto>(f => f.Unit == "Count" && f.Link.Page == "/cartera/bandeja-de-mensajes");
        Ficha(t, "alertas-pendientes").Link.Page.Should().Be("/cartera/alertas");
        Ficha(t, "saldo").Link.Should().Match<LendingDashboardLinkDto>(l => l.Page == "/cartera/informes" && l.Query["vista"] == "aging");
        Ficha(t, "calidad").Link.Query["vista"].Should().Be("quality");
        Ficha(t, "cosechas").Link.Query["vista"].Should().Be("vintages");
        t.Scope.Branches.Select(b => b.Name).Should().Contain("Oficina Sur");
        t.Scope.Lines.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Un_usuario_de_una_oficina_solo_ve_cifras_de_su_oficina()
    {
        await DelSurAsync();
        _e.G.AlcanceActual = InformesDePrueba.SoloLaOficina(_e.Credito.BranchId);

        var t = await LeerAsync();

        Ficha(t, "saldo").Value.Should().Be(3_000_000m, "sólo el de Ana: el de la oficina Sur no está en su alcance (US15-1)");
        Ficha(t, "calidad").Value.Should().Be(0m);
        t.Scope.Branches.Should().ContainSingle().Which.PublicId.Should().NotBe(_otra.PublicId);

        var pidiendoLaOtra = await LeerAsync(new GetLendingDashboardQuery(Branch: _otra.PublicId));
        Ficha(pidiendoLaOtra, "saldo").Value.Should().Be(0m, "pedir una oficina fuera del alcance no la trae");
        Ficha(pidiendoLaOtra, "colocacion-del-mes").Value.Should().Be(0m);
    }

    [Fact]
    public async Task Recaudo_frente_al_esperado_con_el_plan_vigente()
    {
        var cuota = await _e.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync();
        await _e.P.CausarHastaAsync(cuota.DueDate.AddDays(1));
        await _e.P.AplicadoAsync(_e.P.Pago(ModalidadDePago.FreeAmount, 100_000m, cuota.DueDate));

        var t = await LeerAsync(new GetLendingDashboardQuery(AsOf: cuota.DueDate.AddDays(1)));

        Ficha(t, "recaudo-vs-esperado").Value.Should().Be(Math.Round(100_000m / cuota.Total * 100m, 2, MidpointRounding.AwayFromZero),
            "lo pagado en el mes sobre la cuota que vence en el mes");
        Ficha(t, "recaudo-vs-esperado").Unit.Should().Be("Percent");
    }

    [Fact]
    public async Task El_deterioro_sale_del_ultimo_cierre_y_su_anterior_del_cierre_previo()
    {
        foreach (var (mes, deterioro) in new[] { (10, 30_000m), (11, 45_000m) })
        {
            var cierre = new MonthlyClose
            {
                Kind = MonthlyCloseKind.Monthly, PeriodYear = 2026, PeriodMonth = (byte)mes, Version = 1, CloseDate = MonthlyClose.UltimoDiaDe(2026, mes),
                ProcessRunId = 7000 + mes,
            };
            _e.Db.MonthlyCloses.Add(cierre);
            await _e.Db.SaveChangesAsync();
            _e.Db.MonthlyCloseLoans.Add(new MonthlyCloseLoan
            {
                ProcessRunId = 7000 + mes, LoanId = _e.Credito.Id, PersonId = _e.Credito.DebtorPersonId, BranchId = _e.Credito.BranchId,
                CreditLineId = _e.Credito.CreditLineId, ModalityCode = "CONSUMO", SarcSegmentCode = "CONSUMO", PrincipalBalance = 3_000_000m,
                TotalImpairment = deterioro, FinalRating = RatingCategory.A,
            });
            _e.Db.MonthlyCloseGeneralImpairments.Add(new MonthlyCloseGeneralImpairment
            {
                MonthlyCloseId = cierre.Id, BranchId = _e.Credito.BranchId, ModalityCode = "CONSUMO", TotalAmount = 1_000m,
            });
            await _e.Db.SaveChangesAsync();
        }
        _e.Db.ChangeTracker.Clear();

        var t = await LeerAsync();

        Ficha(t, "deterioro").Should().Match<LendingDashboardTileDto>(f => f.Value == 46_000m && f.PreviousValue == 31_000m && f.Unit == "Money");
        Ficha(t, "deterioro").Note.Should().Contain("2026-11");
    }

    [Fact]
    public async Task Antes_de_K4_las_promesas_dicen_disponible_con_cobranza()
    {
        var t = await LeerAsync(entrega: EntregaDeCartera.K3);

        var promesas = Ficha(t, "cumplimiento-de-promesas");
        promesas.Value.Should().BeNull();
        promesas.Note.Should().Be(GetLendingDashboardQueryHandler.SinCobranza);
        Ficha(t, "mensajes-pendientes-y-rechazados").Value.Should().BeNull("sin la vista de la bandeja de Cartera no hay conteo");
    }

    [Fact]
    public async Task El_tablero_no_trae_ningun_dato_personal()
    {
        await DelSurAsync();
        var t = await LeerAsync();

        var json = JsonSerializer.Serialize(t);
        foreach (var p in new Person[] { _e.D.O.Ana, _e.D.O.Beto })
        {
            json.Should().NotContain(p.PublicId.ToString());
            if (!string.IsNullOrWhiteSpace(p.TaxId)) json.Should().NotContain(p.TaxId);
            json.Should().NotContain(p.FirstName ?? "—sin nombre—");
        }
        json.Should().NotContain(_e.Credito.Number);
        json.Should().NotContain(_e.Credito.PublicId.ToString());
    }
}
