using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I3, T646 (contracts/api.md §23.1; T32): la evaluación de una venta a crédito mientras IC está pendiente. Con Cartera no
/// habilitada, <c>lendingEnabled = false</c>, <c>origin = ProvisionalCredit</c>, <c>requiresApproval = true</c> y <c>lending</c> nulo; la
/// elegibilidad provisional con los mismos códigos del cobro (consumidor final, inactiva, no asociado o retirado, no cliente); las
/// condiciones propuestas salen del medio y la aprobación dice el nivel 1 con <c>Inventory.Sales.SellOnCredit</c> y el mayor monto que
/// alguien de la cooperativa puede aprobar.
/// </summary>
public class EvaluateSaleCreditQueryTests
{
    private static async Task<SaleCreditEvaluationDto> EvaluarAsync(CreditoDePrueba c, Person persona, PaymentMeans medio, decimal monto = 120_000m)
    {
        var r = await c.Evaluar().Handle(new EvaluateSaleCreditQuery(persona.PublicId, medio.PublicId, monto), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    [Fact]
    public async Task Con_Cartera_no_habilitada_rige_el_credito_provisional_con_aprobacion()
    {
        var c = await CreditoDePrueba.CrearAsync();
        (await c.Cartera.EstadoCrediticioAsync(new ConsultaCrediticia(c.AsociadoX.PublicId, PaymentMeansClass.AssociateCredit, 1m, default, null), default))
            .Should().BeOfType<CarteraNoHabilitada>();

        var e = await EvaluarAsync(c, c.AsociadoX, c.CredAsoc);

        e.LendingEnabled.Should().BeFalse();
        e.Origin.Should().Be(CreditOrigin.ProvisionalCredit);
        e.RequiresApproval.Should().BeTrue();
        e.Lending.Should().BeNull();
        e.Person.Eligible.Should().BeTrue();
        e.Person.IsAssociate.Should().BeTrue();
        e.Person.AssociateActive.Should().BeTrue();
        e.Person.Reasons.Should().BeEmpty();
    }

    [Fact]
    public async Task Las_condiciones_propuestas_salen_del_medio_y_la_aprobacion_del_nivel_fijo()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var e = await EvaluarAsync(c, c.AsociadoX, c.CredAsoc);

        e.CreditDefaults.Should().BeEquivalentTo(new CreditDefaultsDto(6, 30, 30, "CONSUMO", 90, 1));
        e.Approval!.Levels.Should().ContainSingle().Which.Should().Be(new CreditApprovalLevelDto(1, "Inventory.Sales.SellOnCredit", 0m));
        e.Approval.ApproverMaxAmount.Should().Be(1_000_000m, "el supervisor puede aprobar hasta un millón; quien consulta no cuenta");
    }

    [Fact]
    public async Task El_consumidor_final_no_esta_identificado()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var e = await EvaluarAsync(c, c.V.ConsumidorFinal, c.CredCli);

        e.Person.Eligible.Should().BeFalse();
        e.Person.Reasons.Select(x => x.Code).Should().Equal(ErroresDeCredito.PersonNotIdentifiedCode);
    }

    [Fact]
    public async Task La_persona_inactiva_no_es_elegible()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var e = await EvaluarAsync(c, c.InactivaY, c.CredCli);

        e.Person.Reasons.Select(x => x.Code).Should().Equal(ErroresDeCredito.PersonInactiveCode);
    }

    [Fact]
    public async Task El_credito_a_asociados_exige_asociado_sin_retiro()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var noAsociada = await EvaluarAsync(c, c.V.Ana, c.CredAsoc);
        var retirado = await EvaluarAsync(c, c.AsociadoRetiradoZ, c.CredAsoc);

        noAsociada.Person.Reasons.Select(x => x.Code).Should().Equal(ErroresDeCredito.NotAssociateCode);
        retirado.Person.Reasons.Select(x => x.Code).Should().Equal(ErroresDeCredito.NotAssociateCode);
        retirado.Person.AssociateActive.Should().BeFalse();
    }

    [Fact]
    public async Task El_credito_comercial_exige_cliente()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var sinCliente = await EvaluarAsync(c, c.AsociadoX, c.CredCli);
        var cliente = await EvaluarAsync(c, c.V.Ana, c.CredCli);

        sinCliente.Person.Reasons.Select(x => x.Code).Should().Equal(ErroresDeCredito.NotCustomerCode);
        cliente.Person.Eligible.Should().BeTrue();
    }

    [Fact]
    public async Task Un_medio_que_no_es_de_credito_no_se_evalua()
    {
        var c = await CreditoDePrueba.CrearAsync();

        var r = await c.Evaluar().Handle(new EvaluateSaleCreditQuery(c.AsociadoX.PublicId, c.V.Efectivo.PublicId, 1000m), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeCredito.MeansNotCreditCode);
    }
}
