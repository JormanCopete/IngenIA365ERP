using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Cartera;
using IngenIA365ERP.Application.Tests.Lending.Reports;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Tablero.Cartera;

/// <summary>
/// 2026-11-17 (pedido del dueño): las cifras de Cartera del tablero de inicio sobre el escenario de <see cref="InformesDePrueba"/> (hoy
/// 2026-12-01, el crédito de Ana de 3.000.000 desembolsado hoy) más un crédito de 2.000.000 en la oficina Sur con 40 días de mora hoy. Deben
/// dar lo mismo que el tablero de Cartera (GetLendingDashboardQueryTests) y respetar el alcance de oficina.
/// </summary>
public class TableroDeCarteraTests : IAsyncLifetime
{
    private const int OtroUsuario = 987_654;
    private static readonly DateOnly Hoy = InformesDePrueba.Hoy;

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

    private static ContextoDeIndicador Contexto(bool soloLoMio = false, int? usuario = null) => new(soloLoMio, usuario, null, Hoy);

    private static ResultadoDeIndicador Valor(Result<ResultadoDeIndicador> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>
    /// Un crédito de 2.000.000 en la oficina Sur desembolsado el 15 de noviembre, al día el 30 de noviembre y con 40 días hoy. Hoy tiene dos
    /// versiones de la posición: la vigente es la mayor (la de 40 días), la otra quedó corregida.
    /// </summary>
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
        foreach (var (fecha, version, capital, dias) in new[]
                 {
                     (new DateOnly(2026, 11, 30), 1, 2_000_000m, 0), (Hoy, 1, 9_999_999m, 0), (Hoy, 2, 2_000_000m, 40),
                 })
            _e.Db.LoanDailyPositions.Add(new LoanDailyPosition
            {
                LoanId = credito.Id, PositionDate = fecha, Version = version, Status = LoanStatus.Active, BranchId = _otra.Id,
                CreditLineId = credito.CreditLineId, ModalityCode = "CONSUMO", Principal = capital, DaysPastDue = dias, InputsSha256 = "x",
            });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
        return credito;
    }

    [Fact]
    public async Task El_saldo_es_el_capital_de_la_posicion_vigente_y_se_compara_con_el_cierre_del_mes_anterior()
    {
        await DelSurAsync();

        var r = Valor(await new SaldoDeCartera(_e.Db, _e.G.Alcance).CalcularAsync(Contexto(), default));

        r.Valor.Should().Be(5_000_000m, "3.000.000 de Ana y 2.000.000 del Sur en su última versión");
        r.Anterior.Should().Be(2_000_000m, "al 30 de noviembre sólo existía el del Sur");
        r.EtiquetaDelAnterior.Should().Be("mes anterior");
    }

    [Fact]
    public async Task El_saldo_respeta_el_alcance_de_oficina()
    {
        await DelSurAsync();
        _e.G.AlcanceActual = InformesDePrueba.SoloLaOficina(_e.Credito.BranchId);

        var r = Valor(await new SaldoDeCartera(_e.Db, _e.G.Alcance).CalcularAsync(Contexto(), default));

        r.Valor.Should().Be(3_000_000m, "el de la oficina Sur no está en su alcance");
    }

    [Fact]
    public async Task La_vencida_es_el_porcentaje_con_mas_de_los_dias_del_parametro_y_dice_cuanto_es()
    {
        await DelSurAsync();

        var r = Valor(await new CarteraVencida(_e.Db, _e.G.Alcance, _e.D.Parametros()).CalcularAsync(Contexto(), default));

        r.Valor.Should().Be(40m, "2.000.000 con 40 días sobre 5.000.000, lo mismo que la ficha «calidad» del tablero de Cartera");
        r.Anterior.Should().Be(0m);
        r.Nota.Should().Contain("2.000.000").And.Contain("30 días");
    }

    [Fact]
    public async Task La_mora_por_rangos_reparte_el_capital_en_los_rangos_del_parametro()
    {
        await DelSurAsync();

        var r = Valor(await new MoraPorRangos(_e.Db, _e.G.Alcance, _e.D.Parametros()).CalcularAsync(Contexto(), default));

        r.Serie.Should().NotBeNull();
        r.Serie!.Select(p => p.Etiqueta).Should().Equal("Al día", "1 a 30", "31 a 60", "61 a 90", "91 a 180", "181 a 360", "Más de 360");
        r.Serie!.Single(p => p.Etiqueta == "Al día").Valor.Should().Be(3_000_000m);
        r.Serie!.Single(p => p.Etiqueta == "31 a 60").Valor.Should().Be(2_000_000m);
        r.Serie!.Sum(p => p.Valor).Should().Be(5_000_000m);
        r.Valor.Should().Be(5_000_000m);
    }

    [Fact]
    public async Task Los_desembolsos_del_mes_se_comparan_con_el_mismo_tramo_del_mes_anterior()
    {
        await DelSurAsync();

        var r = Valor(await new DesembolsosDelMes(_e.Db, _e.G.Alcance).CalcularAsync(Contexto(), default));

        r.Valor.Should().Be(3_000_000m, "el de Ana, hoy (el del Sur fue en noviembre)");
        r.Anterior.Should().Be(0m, "del 1 al 1 de noviembre no hubo desembolsos");
        r.Nota.Should().Be("1 desembolso");
    }

    [Fact]
    public async Task Solo_lo_mio_cuenta_los_desembolsos_que_registro_el_usuario()
    {
        var sur = await DelSurAsync();
        _e.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = sur.Id, Kind = LoanOperationKind.Disbursement, Number = "OP-SUR-2", Sequence = 2, ValueDate = Hoy, AccountingDate = Hoy,
            RegisteredAt = new DateTime(2026, 12, 1), Amount = 500_000m, ActorKind = ActorKind.Person, ActorUserId = OtroUsuario,
        });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();
        var indicador = new DesembolsosDelMes(_e.Db, _e.G.Alcance);

        indicador.Definicion.AdmiteSoloLoMio.Should().BeTrue();
        Valor(await indicador.CalcularAsync(Contexto(), default)).Valor.Should().Be(3_500_000m);
        Valor(await indicador.CalcularAsync(Contexto(soloLoMio: true, usuario: OtroUsuario), default)).Valor.Should().Be(500_000m);
        Valor(await indicador.CalcularAsync(Contexto(soloLoMio: true, usuario: null), default)).Valor.Should().Be(0m,
            "sin usuario resuelto no hay nada «mío»");
    }
}
