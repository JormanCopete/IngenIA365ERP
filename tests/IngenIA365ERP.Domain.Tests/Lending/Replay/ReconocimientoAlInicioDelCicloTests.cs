using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;
using Xunit;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// 2026-11-16 (pedido del dueño; feature 024, fase 4): el reconocimiento al inicio del ciclo en la reconstrucción, sobre el caso dorado 37. Lo demás (las
/// filas por día, lo que queda y lo que se devuelve) lo fijan los casos 37 y 38 con calcular.py. (nuevo)
/// </summary>
public sealed class ReconocimientoAlInicioDelCicloTests
{
    private static CasoDoradoDeCartera Caso37() =>
        CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "37-reconocimiento-al-inicio-del-ciclo-diferido.json"));

    [Theory]
    [InlineData(LoanConcept.CurrentInterest, ModoDeReconocimiento.IngresoDiferido, LoanConcept.RecognizedInterest)]
    [InlineData(LoanConcept.Insurance, ModoDeReconocimiento.IngresoDiferido, LoanConcept.RecognizedInsurance)]
    [InlineData(LoanConcept.Administration, ModoDeReconocimiento.IngresoDiferido, LoanConcept.RecognizedAdministration)]
    [InlineData(LoanConcept.CurrentInterest, ModoDeReconocimiento.IngresoDirecto, LoanConcept.RecognizedInterestDirect)]
    [InlineData(LoanConcept.Insurance, ModoDeReconocimiento.IngresoDirecto, LoanConcept.RecognizedInsuranceDirect)]
    [InlineData(LoanConcept.Administration, ModoDeReconocimiento.IngresoDirecto, LoanConcept.RecognizedAdministrationDirect)]
    public void El_modo_va_sellado_en_el_concepto(LoanConcept rubro, ModoDeReconocimiento modo, LoanConcept esperado)
    {
        var concepto = ConceptosReconocidos.De(rubro, modo);
        concepto.Should().Be(esperado);
        ConceptosReconocidos.Es(concepto).Should().BeTrue();
        ConceptosReconocidos.RubroDe(concepto).Should().Be(rubro);
        ConceptosReconocidos.ModoDe(concepto).Should().Be(modo);
    }

    [Fact]
    public void Ni_el_capital_ni_la_mora_se_reconocen()
    {
        FluentActions.Invoking(() => ConceptosReconocidos.De(LoanConcept.DefaultInterest, ModoDeReconocimiento.IngresoDiferido))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => ConceptosReconocidos.De(LoanConcept.Principal, ModoDeReconocimiento.IngresoDirecto))
            .Should().Throw<ArgumentOutOfRangeException>();
        Enum.GetValues<LoanConcept>().Where(ConceptosReconocidos.Es).Should().BeEquivalentTo(ConceptosReconocidos.Todos);
    }

    [Fact]
    public void Lo_reconocido_no_es_saldo_ni_cambia_lo_que_se_debe()
    {
        var caso = Caso37();
        var r = new DateTime(2027, 2, 18, 23, 0, 0, DateTimeKind.Utc);
        var con = caso.Entrada(caso.Condiciones, caso.Operaciones, r);
        var sin = con with { Reconocimientos = [] };
        var fecha = new DateOnly(2027, 2, 18);

        var reconocido = MotorDeCartera.Reconstruir(con, fecha, new DateOnly(2027, 2, 16));
        var diario = MotorDeCartera.Reconstruir(sin, fecha, new DateOnly(2027, 2, 16));

        reconocido.Saldos.Should().BeEquivalentTo(diario.Saldos, "el cálculo no cambia");
        reconocido.ValorParaCancelar.Should().Be(diario.ValorParaCancelar);
        reconocido.Saldos.Should().NotContain(s => ConceptosReconocidos.Es(s.Concepto));
        reconocido.Reconocidos.Should().HaveCount(3);
        reconocido.ReconocidoPendiente(2).Should().Be(reconocido.Reconocidos.Sum(x => x.Pendiente));
        reconocido.Libro.Movimientos.Where(m => !ConceptosReconocidos.Es(m.Concepto))
            .Should().BeEquivalentTo(diario.Libro.Movimientos, o => o.WithStrictOrdering(), "la causación del día es la misma");
        reconocido.Huella.Should().NotBe(diario.Huella, "lo reconocido entra en la huella sólo cuando lo hay");
        MotorDeCartera.Reconstruir(sin, fecha).Huella.Should().Be(diario.Huella);
    }

    [Fact]
    public void Un_reconocimiento_registrado_despues_de_R_no_cuenta()
    {
        var caso = Caso37();
        // RC1 se registró el 17-02 a las 05:00: con la información del 16-02 no existe.
        var antes = caso.Entrada(caso.Condiciones, caso.Operaciones, new DateTime(2027, 2, 16, 23, 0, 0, DateTimeKind.Utc));
        var estado = MotorDeCartera.Reconstruir(antes, new DateOnly(2027, 2, 16), new DateOnly(2027, 2, 16));
        estado.Reconocidos.Should().BeEmpty();
        estado.Libro.Movimientos.Should().NotContain(m => ConceptosReconocidos.Es(m.Concepto));
    }

    [Fact]
    public void El_pago_de_la_cuota_reconocida_antes_del_vencimiento_no_lleva_el_aviso_del_anticipo()
    {
        var caso = Caso37();
        var r = new DateTime(2027, 3, 5, 23, 0, 0, DateTimeKind.Utc);
        var fecha = new DateOnly(2027, 3, 5);
        var con = MotorDeCartera.Reconstruir(caso.Entrada(caso.Condiciones, caso.Operaciones, r), fecha);
        var sin = MotorDeCartera.Reconstruir(caso.Entrada(caso.Condiciones, caso.Operaciones, r) with { Reconocimientos = [] }, fecha);
        var solicitud = new SolicitudDeDistribucion(PaymentModality.CycleInstallment, 215255.72m, fecha);
        var redondeo = caso.Condiciones.Credito(VersionesDeReglas.Vigente).Redondeo;

        AplicadorDePagos.Reconocida(con, 2).Should().BeTrue();
        var reconocida = AplicadorDePagos.Distribuir(con, solicitud, Prelacion.PorDefecto, redondeo);
        var diaria = AplicadorDePagos.Distribuir(sin, solicitud, Prelacion.PorDefecto, redondeo);
        reconocida.Anticipo.Should().Be(diaria.Anticipo, "el pago se aplica igual: queda como anticipo de la cuota y la cubre al vencer");
        reconocida.Avisos.Should().NotContain(AplicadorDePagos.AvisoAnticipoNoReduceIntereses);
        diaria.Avisos.Should().Contain(AplicadorDePagos.AvisoAnticipoNoReduceIntereses);
    }
}
