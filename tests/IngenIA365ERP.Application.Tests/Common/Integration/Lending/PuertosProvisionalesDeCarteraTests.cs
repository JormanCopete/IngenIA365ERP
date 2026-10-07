using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Compliance.DataSubjectRequests;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Feature 013 (T0101; T85, contracts/interfaces.md §3.17, §5.2, §6.1, §7): lo que responden los puertos de Cartera mientras Cartera
/// no exista en el despliegue. La consulta de obligaciones dice «sin activar» con una entrada vacía por persona; el contrato atiende
/// listar y simular vacíos y rechaza cada ítem de las escrituras con <c>Lending.NotActivated</c>; las reservas responden
/// <c>NotEnabled</c>. Ninguno escribe en el contexto. Y el deber de conservación del titular (fase 2) lee la consulta.
/// </summary>
public class PuertosProvisionalesDeCarteraTests
{
    private static readonly Recaudador Nomina = Recaudador.Nomina;
    private static readonly DateOnly Hoy = new(2026, 10, 6);

    [Fact]
    public async Task La_consulta_de_obligaciones_responde_sin_activar_una_entrada_vacia_por_persona()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var r = await new ConsultaDeObligacionesSinActivar().ObligacionesAsync(new ConsultaDeObligaciones([a, b, a], Hoy, IncludeAsCoDebtor: true), CancellationToken.None);

        r.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        r.People.Select(p => p.PersonPublicId).Should().Equal(a, b);
        r.People.Should().OnlyContain(p => !p.MigratedNotActivated && p.Obligations.Count == 0);
    }

    [Fact]
    public async Task Las_cuotas_de_nomina_salen_vacias_y_sin_activar()
    {
        var r = await new ConsultaDeObligacionesSinActivar().CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina([Guid.NewGuid()], Hoy, Hoy), CancellationToken.None);

        r.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        r.Installments.Should().BeEmpty();
        r.MigratedNotActivated.Should().BeEmpty();
    }

    [Fact]
    public async Task El_contrato_atiende_listar_y_simular_vacios_con_Lending_NotActivated()
    {
        var contrato = new ContratoDeObligacionesSinActivar();
        var persona = Guid.NewGuid();

        var listado = await contrato.ListarAsync(new SolicitudDeListado(Nomina, persona, Hoy), CancellationToken.None);
        listado.Result.Should().Be(ResultadoDelContrato.Answered);
        listado.Code.Should().Be("Lending.NotActivated");
        listado.Value!.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        listado.Value.Obligations.Should().BeEmpty();

        var simulacion = await contrato.SimularAsync(new SolicitudDeSimulacion(Nomina, Guid.NewGuid(), ModalidadDePago.FreeAmount, 1m, Hoy), CancellationToken.None);
        simulacion.Result.Should().Be(ResultadoDelContrato.Answered);
        simulacion.Code.Should().Be("Lending.NotActivated");
        simulacion.Value.Should().BeNull();
        simulacion.Message.Should().Contain("todavía no está activada");
    }

    [Fact]
    public async Task Aplicar_rechaza_cada_pago_y_cada_no_descontado_con_Lending_NotActivated()
    {
        var recibo = new ReferenciaDeRecibo("NM-1", Hoy, null, null, "NM");
        var pagos = Enumerable.Range(1, 3).Select(i => new PagoAAplicar($"NOM:x:{i}", Guid.NewGuid(), ModalidadDePago.CycleInstallment, 10m * i, Hoy,
            ClaseDeMedioDeRecaudo.OwnPayrollDeduction, Guid.NewGuid(), recibo)).ToList();
        var noDescontados = new[] { new NoDescontado("NOM:x:1", pagos[0].ObligationPublicId, 1, 20m, 10m, MotivoDeNoDescuento.DeductionCap, null, null) };

        var r = await new ContratoDeObligacionesSinActivar().AplicarAsync(new SolicitudDeAplicacion(Nomina, FormaDeAplicacion.NonInteractive, pagos, noDescontados), CancellationToken.None);

        r.Payments.Should().HaveCount(3).And.OnlyContain(x => x.Result == ResultadoDelContrato.Rejected && x.Code == "Lending.NotActivated" && x.Value == null);
        r.NotDeducted.Should().ContainSingle().Which.Should().Match<RespuestaDelContratoDto<NoDescontadoRegistradoDto>>(x => x.Result == ResultadoDelContrato.Rejected && x.Code == "Lending.NotActivated");
    }

    [Fact]
    public async Task Reversar_y_resolver_responden_Rejected_con_Lending_NotActivated()
    {
        var contrato = new ContratoDeObligacionesSinActivar();

        var reverso = await contrato.ReversarAsync(new SolicitudDeReverso(Nomina, "NOM:x:1", "error"), CancellationToken.None);
        var resolucion = await contrato.ResolverSaldoAFavorAsync(new SolicitudDeResolucion(Nomina, "NOM:x:2", Guid.NewGuid(), ConceptoDeObligacion.CreditBalance, 1m,
            FormaDeResolucion.RefundThroughCollector, new ReferenciaDeRecibo("E-1", Hoy, null, null, null)), CancellationToken.None);

        reverso.Result.Should().Be(ResultadoDelContrato.Rejected);
        reverso.Code.Should().Be("Lending.NotActivated");
        resolucion.Result.Should().Be(ResultadoDelContrato.Rejected);
        resolucion.Code.Should().Be("Lending.NotActivated");
    }

    [Fact]
    public async Task Las_reservas_responden_NotEnabled_sin_numero()
    {
        var reservas = new ReservasDeCarteraNoHabilitada();

        var reserva = await reservas.ReservarAsync(new SolicitudDeReserva("INV:x:10.00", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PaymentMeansClass.AssociateCredit, 10m, Hoy, "CONSUMO", null), CancellationToken.None);
        var liberacion = await reservas.LiberarAsync(new SolicitudDeLiberacion("INV:x:10.00", Guid.NewGuid(), "descartada"), CancellationToken.None);

        reserva.Status.Should().Be(EstadosDeReserva.NotEnabled);
        reserva.LoanNumber.Should().BeNull();
        reserva.ReservationPublicId.Should().BeNull();
        liberacion.Status.Should().Be(EstadosDeReserva.NotEnabled);
    }

    [Fact]
    public async Task Los_provisionales_no_escriben_en_el_contexto()
    {
        using var db = TestDbContextFactory.Create();
        var antes = db.ChangeTracker.Entries().Count();
        var persona = Guid.NewGuid();

        await new ConsultaDeObligacionesSinActivar().ObligacionesAsync(new ConsultaDeObligaciones([persona], Hoy, true), CancellationToken.None);
        await new ContratoDeObligacionesSinActivar().AplicarAsync(new SolicitudDeAplicacion(Nomina, FormaDeAplicacion.NonInteractive, [], []), CancellationToken.None);
        await new ReservasDeCarteraNoHabilitada().ReservarAsync(new SolicitudDeReserva("k", Guid.NewGuid(), Guid.NewGuid(), persona, PaymentMeansClass.CustomerCredit, 1m, Hoy, "X", null), CancellationToken.None);

        db.ChangeTracker.Entries().Count().Should().Be(antes, "los provisionales ni siquiera reciben el contexto");
        typeof(ConsultaDeObligacionesSinActivar).GetConstructors().Should().OnlyContain(c => c.GetParameters().Length == 0);
        typeof(ContratoDeObligacionesSinActivar).GetConstructors().Should().OnlyContain(c => c.GetParameters().Length == 0);
        typeof(ReservasDeCarteraNoHabilitada).GetConstructors().Should().OnlyContain(c => c.GetParameters().Length == 0);
    }

    [Fact]
    public async Task El_deber_del_titular_pregunta_la_consulta_con_codeudor_y_cerradas_y_traduce_sus_obligaciones()
    {
        var titular = Guid.NewGuid();
        var sinCartera = await new DeberDeConservacionPorObligaciones(new ConsultaDeObligacionesSinActivar()).ConsultarAsync(titular, Hoy, CancellationToken.None);
        sinCartera.Obligaciones.Should().BeEmpty();
        sinCartera.MigradaSinActivar.Should().BeFalse();
        sinCartera.Fuente.Should().Be(SinObligacionesRegistradas.Fuente);

        var doble = new ConsultaDeObligacionesDePrueba();
        doble.Agregar(titular, ConsultaDeObligacionesDePrueba.Obligacion("V-1", 100m));
        doble.Agregar(titular, ConsultaDeObligacionesDePrueba.Obligacion("C-1", 0m, rol: RolEnLaObligacion.CoDebtor, estado: EstadoDeObligacion.PaidOff) with { ClosedOn = new DateOnly(2025, 1, 1), DaysPastDue = 45 });
        var deber = await new DeberDeConservacionPorObligaciones(doble).ConsultarAsync(titular, Hoy, CancellationToken.None);

        var consulta = doble.Consultas.Should().ContainSingle().Subject;
        consulta.IncludeAsCoDebtor.Should().BeTrue();
        consulta.IncludeClosed.Should().BeTrue();
        consulta.Date.Should().Be(Hoy);
        deber.Obligaciones.Should().BeEquivalentTo(new[]
        {
            new Domain.Common.Compliance.ObligacionParaRevocacion("V-1", true, null, 0),
            new Domain.Common.Compliance.ObligacionParaRevocacion("C-1", false, new DateOnly(2025, 1, 1), 45),
        });
        deber.Fuente.Should().Contain("Cartera");
    }
}
