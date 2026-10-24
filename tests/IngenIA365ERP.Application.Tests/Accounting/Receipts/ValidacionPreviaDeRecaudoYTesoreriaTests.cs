using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using NSubstitute;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0250 (R-23; FR-143, FR-221): la validación previa de Recaudo y Tesorería dice si cada rol que el hecho siempre mueve tiene una regla que podría
/// casar; con <c>Contabilidad.FaltaDeRegla = Bloquear</c> lo informa para que la pantalla no confirme; sin contabilidad iniciada o con el modo de paso en «no pasa»
/// nada se bloquea; la política sin dato es «Bloquear». Prueba escrita, sin correr.
/// </summary>
public class ValidacionPreviaDeRecaudoYTesoreriaTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILectorDeParametros _lector = Substitute.For<ILectorDeParametros>();

    public ValidacionPreviaDeRecaudoYTesoreriaTests()
    {
        Contabilidad(iniciada: true);
        Parametro(ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, "PorLotes");
        Parametro(ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, "Bloquear");
    }

    private void Contabilidad(bool iniciada) =>
        _sender.Send(Arg.Any<GetAccountingPeriodStatusQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(iniciada, EstadoDePeriodoContable.Open, null)));

    private void Parametro(string clave, string valor)
    {
        var definicion = ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Modulo == ParametrosDeRecaudoYTesoreria.ModuloRecaudo && d.Clave == clave);
        _lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, clave, Arg.Any<DateOnly>(), Arg.Any<ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, valor, valor, null)));
    }

    private EvaluacionPreviaDeModulo Evaluacion => new(new ResolutorDeLaMatriz(_db), _lector, _sender);

    private static CombinacionDeHecho Recibo() => new("REC", ReciboEmitidoV1.Type, new Dictionary<string, string>(), Hoy);

    [Fact]
    public async Task Sin_reglas_el_recibo_no_es_contabilizable_y_la_politica_bloquea()
    {
        var r = await Evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, [Recibo()], default);

        r.Postable.Should().BeFalse();
        r.Policy.Should().Be("Bloquear");
        r.Missing.Select(m => m.Role).Should().BeEquivalentTo([RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDelModulo]);
    }

    [Fact]
    public async Task Con_las_reglas_de_sus_roles_obligatorios_es_contabilizable()
    {
        var cuenta = Cuenta("110505");
        _db.ChartOfAccounts.Add(cuenta);
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, cuenta, dimensiones: r => r.CashBoxCode = "C1"));
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.PuenteDelModulo, cuenta, dimensiones: r => r.TargetModule = "CAR"));
        await _db.SaveChangesAsync();

        var r = await Evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, [Recibo()], default);

        r.Postable.Should().BeTrue("una dimensión que la pregunta no trae no descarta la regla");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_o_con_el_modo_en_no_pasa_nada_se_bloquea()
    {
        Contabilidad(iniciada: false);
        (await Evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, [Recibo()], default)).Postable.Should().BeTrue();

        Contabilidad(iniciada: true);
        Parametro(ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, "NoPasa");
        (await Evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, [Recibo()], default)).Postable.Should().BeTrue();
    }

    [Fact]
    public async Task En_advertir_informa_lo_que_falta_sin_bloquear()
    {
        Parametro(ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, "Advertir");

        var r = await Evaluacion.EvaluarAsync(MatricesDeRecaudoYTesoreria.Recaudo, [Recibo()], default);

        r.Postable.Should().BeFalse();
        r.Policy.Should().Be("Advertir", "quien pregunta sólo bloquea con «Bloquear»");
    }
}
