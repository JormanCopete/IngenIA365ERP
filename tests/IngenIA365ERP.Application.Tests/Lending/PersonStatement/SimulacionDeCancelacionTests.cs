using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.PersonStatement;

/// <summary>
/// 2026-11-16 (pedido del dueño): la simulación de cancelación a una fecha. Rechaza una fecha pasada (en el validador y en el handler) y un crédito que no
/// es de la persona como deudora; con el corrido (el de fábrica, o por ley dentro del tope del prepago) cada crédito cuesta lo que dice su liquidación;
/// con la cuota completa, lo que la liquidación dice que cobraría el <c>FullPayoff</c>: le suma lo que falta por causar del interés de la cuota en curso.
/// (nuevo)
/// </summary>
public sealed class SimulacionDeCancelacionTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 11, 16);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Person _ana;
    private readonly LoanListItemDto _credito = new(Guid.NewGuid(), "CR-000007", new RefDto(Guid.NewGuid(), "ANA PÉREZ"),
        new LoanLineDto(Guid.NewGuid(), "CONSUMO", "Consumo"), new RefDto(Guid.NewGuid(), "Principal"), "Active", "Origination", 1_000_000m, 800_000m, 0,
        Hoy, null, []);

    public SimulacionDeCancelacionTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _ana = new Person { FirstName = "ANA", LastName = "PÉREZ", TaxId = "52123456", IdType = "C", Email = "ana@correo.co", CreatedBy = "test" };
        _db.People.Add(_ana);
        _db.SaveChanges();
        _sender.Send(Arg.Is<ListLoansQuery>(q => q.Role == "Debtor"), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new PagedResult<LoanListItemDto>([_credito], 1, 200, 1)));
    }

    public void Dispose() => _db.Dispose();

    private SimulatePersonPayoffQueryHandler Handler() => new(_db, _sender, _reloj, Substitute.For<IBlobStore>());

    /// <summary>La liquidación de <see cref="_credito"/> a la fecha: 800.000 de capital, 9.000 corrientes, 1.000 de mora, 2.000 de seguro, 500 de gastos y
    /// 300 de anticipo; para cancelar, 812.200.</summary>
    private LoanSettlementDto Liquidacion(DateOnly fecha, params SettlementExplanationDto[] tramos) => new(_credito.PublicId, _credito.Number, fecha, null,
        "LND-2026.1", "sha256:prueba", 812_200m,
        new Dictionary<string, decimal>
        {
            ["Principal"] = 800_000m, ["CurrentInterest"] = 9_000m, ["DefaultInterest"] = 1_000m, ["CurrentInterestInSuspense"] = 0m,
            ["DefaultInterestInSuspense"] = 0m, ["Insurance"] = 2_000m, ["CollectionFee"] = 500m, ["OtherCharge"] = 0m,
        },
        new Dictionary<string, decimal> { ["Advance"] = 300m, ["CreditBalance"] = 0m }, [], 3, false, false, tramos, [], null, []);

    private void LiquidacionA(DateOnly fecha, params SettlementExplanationDto[] tramos) =>
        _sender.Send(Arg.Is<GetLoanSettlementQuery>(q => q.LoanPublicId == _credito.PublicId && q.Date == fecha), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Liquidacion(fecha, tramos)));

    [Fact]
    public async Task Rechaza_una_fecha_pasada_en_el_validador_y_en_el_handler()
    {
        var ayer = new SimulatePersonPayoffQuery(_ana.PublicId, [_credito.PublicId], Hoy.AddDays(-1));
        var validador = new SimulatePersonPayoffQueryValidator(_reloj);

        validador.Validate(ayer).Errors.Should().ContainSingle(e => e.PropertyName == nameof(SimulatePersonPayoffQuery.Date));
        validador.Validate(ayer with { Date = Hoy }).IsValid.Should().BeTrue();
        validador.Validate(ayer with { Date = Hoy, Loans = [] }).IsValid.Should().BeFalse();
        validador.Validate(ayer with { Date = Hoy, Loans = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToList() }).IsValid.Should().BeFalse();

        var r = await Handler().Handle(ayer, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SimulatePersonPayoffQueryHandler.CodigoFechaPasada);
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Con_el_interes_corrido_el_total_es_el_de_la_liquidacion()
    {
        var fecha = Hoy.AddDays(10);
        LiquidacionA(fecha);

        var r = await Handler().Handle(new SimulatePersonPayoffQuery(_ana.PublicId, [_credito.PublicId], fecha), default);

        r.IsSuccess.Should().BeTrue();
        var s = r.Value;
        s.ModoDeIntereses.Should().Be(ParametrosDeCartera.InteresesCorridos);
        s.Fecha.Should().Be(fecha);
        s.Persona.Nombre.Should().Be("ANA PÉREZ");
        s.Membrete!.Cooperativa.Should().Be("Cooperativa");
        var c = s.Creditos.Should().ContainSingle().Subject;
        c.Should().BeEquivalentTo(new
        {
            LoanPublicId = _credito.PublicId, Numero = "CR-000007", Linea = "CONSUMO · Consumo", Capital = 800_000m, InteresCorriente = 9_000m,
            InteresDeMora = 1_000m, Seguro = 2_000m, Otros = 500m, Anticipo = 300m, Total = 812_200m, InteresNoCausadoDeLaCuotaEnCurso = (decimal?)null,
        });
        s.Total.Should().Be(812_200m, "con «Corrido» la simulación es la liquidación");
        (c.Capital + c.InteresCorriente + c.InteresDeMora + c.Seguro + c.Otros - c.Anticipo).Should().Be(c.Total);
        await _sender.DidNotReceive().Send(Arg.Any<GetLoanStatementQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_la_cuota_completa_suma_lo_que_la_liquidacion_dice_que_falta_por_causar_de_la_cuota_en_curso()
    {
        var fecha = Hoy.AddDays(5);
        // 2026-11-16: la liquidación ya resuelve el modo (parámetro, tope legal, vivienda) y proyecta lo que falta con la misma función que la caja.
        _sender.Send(Arg.Is<GetLoanSettlementQuery>(q => q.LoanPublicId == _credito.PublicId && q.Date == fecha), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Liquidacion(fecha) with
            {
                InterestBeforeDueDate = ParametrosDeCartera.InteresesDeLaCuotaCompleta, CurrentInstallmentInterestToAccrue = 18_000m,
                ToPayOffWithCurrentInstallment = 830_200m,
                Warnings = ["Para cancelar se cobra el interés completo de la cuota 2 (vence el 01/12/2026): 18.000,00 que todavía no se ha causado."],
            }));

        var r = await Handler().Handle(new SimulatePersonPayoffQuery(_ana.PublicId, [_credito.PublicId], fecha), default);

        r.IsSuccess.Should().BeTrue();
        var s = r.Value;
        s.ModoDeIntereses.Should().Be(ParametrosDeCartera.InteresesDeLaCuotaCompleta);
        var c = s.Creditos.Should().ContainSingle().Subject;
        c.InteresNoCausadoDeLaCuotaEnCurso.Should().Be(18_000m);
        c.InteresCorriente.Should().Be(27_000m, "9.000 causados más 18.000 que faltaban de la cuota en curso");
        c.Total.Should().Be(830_200m, "lo que cobrará el FullPayoff");
        s.Total.Should().Be(830_200m);
        (c.Capital + c.InteresCorriente + c.InteresDeMora + c.Seguro + c.Otros - c.Anticipo).Should().Be(c.Total);
        c.Avisos.Should().Contain(a => a.Contains("cuota 2"));
        await _sender.DidNotReceive().Send(Arg.Any<GetLoanStatementQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dentro_del_tope_legal_se_simula_con_el_corrido_y_se_dice_por_que()
    {
        var fecha = Hoy.AddDays(5);
        const string porLey = "Se cobra el interés corrido: el crédito está dentro del tope legal del prepago sin penalidad (Ley 1555 de 2012) aunque la "
            + "empresa cobre la cuota completa.";
        _sender.Send(Arg.Is<GetLoanSettlementQuery>(q => q.LoanPublicId == _credito.PublicId && q.Date == fecha), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Liquidacion(fecha) with { Warnings = [porLey] }));

        var r = await Handler().Handle(new SimulatePersonPayoffQuery(_ana.PublicId, [_credito.PublicId], fecha), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.ModoDeIntereses.Should().Be(ParametrosDeCartera.InteresesCorridos);
        var c = r.Value.Creditos.Should().ContainSingle().Subject;
        c.InteresNoCausadoDeLaCuotaEnCurso.Should().BeNull();
        c.Total.Should().Be(812_200m);
        c.Avisos.Should().Contain(porLey);
    }

    [Fact]
    public async Task Un_credito_que_no_es_de_la_persona_como_deudora_no_se_simula()
    {
        LiquidacionA(Hoy);

        var r = await Handler().Handle(new SimulatePersonPayoffQuery(_ana.PublicId, [_credito.PublicId, Guid.NewGuid()], Hoy), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(SimulatePersonPayoffQueryHandler.CodigoCreditoAjeno);
        await _sender.DidNotReceive().Send(Arg.Any<GetLoanSettlementQuery>(), Arg.Any<CancellationToken>());
    }
}
