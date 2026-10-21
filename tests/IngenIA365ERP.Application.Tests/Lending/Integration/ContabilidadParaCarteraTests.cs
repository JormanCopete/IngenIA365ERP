using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Lending;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0629 (contabilidad.md §1.1, §16.1; C29): el adaptador real <see cref="ContabilidadParaCartera"/> corre en proceso, sin HTTP,
/// y delega cada método en su consulta de §1.1 por <see cref="ISender"/> (para que pase por la validación de siempre); el validador de los saldos
/// rechaza en <c>CombinacionDeSaldo.Role</c> un rol de resultado o un nombre desconocido; <c>CuentasBancariasAsync</c> reutiliza
/// <see cref="ListBankAccountsQuery"/>.
/// </summary>
public class ContabilidadParaCarteraTests
{
    private readonly ISender _sender = Substitute.For<ISender>();

    /// <summary>Lo que el adaptador mandó por el <see cref="ISender"/>, en orden.</summary>
    private List<object> Enviados => _sender.ReceivedCalls().Select(c => c.GetArguments()[0]!).ToList();

    private ContabilidadParaCartera Adaptador() => new(_sender);

    private static CombinacionDeSaldo Combinacion(string rol) =>
        new(rol, "CONLIB", "CONSUMO", null, "A", null, null, null, null, null, null, null, Guid.NewGuid());

    [Fact]
    public async Task Cada_metodo_delega_en_su_consulta()
    {
        var hechos = new[] { new MensajeContableDto(new IntegrationEnvelopeV1 { Type = "PagoAplicado", OriginModule = "CAR" }, new object()) };
        _sender.Send(Arg.Any<EvaluateLendingPostingQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(true, [], [])));
        _sender.Send(Arg.Any<GetLendingAccountBalancesQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new SaldosMapeadosDeCarteraDto([], [])));
        _sender.Send(Arg.Any<GetAccountingPeriodStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null)));
        _sender.Send(Arg.Any<GetEntityIndicatorsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new IndicadoresDeLaEntidadDto(new DateOnly(2026, 12, 1), 0m, 0m, null, null)));

        (await Adaptador().EvaluarAsync(hechos, default)).Value.IsPostable.Should().BeTrue();
        await Adaptador().SaldosDeCuentasMapeadasAsync(new DateOnly(2026, 11, 30), [Combinacion("Capital")], default);
        await Adaptador().EstadoDelPeriodoAsync(new DateOnly(2026, 12, 1), default);
        await Adaptador().IndicadoresDeLaEntidadAsync(new DateOnly(2026, 12, 1), default);

        Enviados.Select(e => e.GetType()).Should().Equal(typeof(EvaluateLendingPostingQuery), typeof(GetLendingAccountBalancesQuery),
            typeof(GetAccountingPeriodStatusQuery), typeof(GetEntityIndicatorsQuery));
        ((EvaluateLendingPostingQuery)Enviados[0]).Hechos.Should().BeSameAs(hechos);
        ((GetLendingAccountBalancesQuery)Enviados[1]).Corte.Should().Be(new DateOnly(2026, 11, 30));
    }

    [Fact]
    public async Task Las_cuentas_bancarias_reutilizan_la_lista_del_plan()
    {
        var banco = Guid.NewGuid();
        var cuenta = new CuentaBancariaDelPlanDto(Guid.NewGuid(), "11100501", "Banco Uno ahorros", banco, "Banco Uno", "123-4", "1001", true);
        _sender.Send(Arg.Any<ListBankAccountsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CuentaBancariaDelPlanDto>>([cuenta]));

        var r = await Adaptador().CuentasBancariasAsync(banco, default);

        ((ListBankAccountsQuery)Enviados.Single()).BankPublicId.Should().Be(banco);
        r.Value.Should().ContainSingle().Which.Should().Be(new CuentaBancariaDeOrigenDto(cuenta.AccountPublicId, "11100501", "Banco Uno ahorros", banco,
            "Banco Uno", "123-4", "1001"));
    }

    [Theory]
    [InlineData("Capital", true)]
    [InlineData("RecaudoPorAplicar", true)]
    [InlineData("IngresoDiferido", true)]
    [InlineData("ContingenteAcreedora", true)]
    [InlineData("Ingreso", false)]
    [InlineData("GastoDeterioro", false)]
    [InlineData("RecuperacionDeterioro", false)]
    [InlineData("GastoDeCastigoOCondonacion", false)]
    [InlineData("IngresoPorRecuperacion", false)]
    [InlineData("NoExiste", false)]
    public void El_validador_de_los_saldos_solo_admite_roles_de_saldo(string rol, bool admitido)
    {
        var r = new GetLendingAccountBalancesQueryValidator().Validate(new GetLendingAccountBalancesQuery(new DateOnly(2026, 11, 30), [Combinacion(rol)]));

        r.IsValid.Should().Be(admitido, "C29: un rol de resultado o un nombre desconocido es defecto del llamador");
    }
}
