using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// 2026-11-24 (pedido del dueño): el <b>cambio de plazo</b> de un crédito sin pagos ni causación es una operación <c>TermsChange</c> de mecanismo
/// <see cref="NoveltyMechanism.TermCorrection"/> con fecha valor = la del desembolso. La reconstrucción lo aplica como cualquier cambio de plan y el
/// crédito queda exactamente como si hubiera nacido con las cuotas nuevas: mismo capital, mismo primer vencimiento, la cuota recalculada. Acortar y
/// alargar funcionan igual.
/// </summary>
public class CambioDePlazoEnLaReconstruccionTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);

    private static EntradaDeReconstruccion ConCambio(int cuotas, DateOnly? primerVencimiento, int cuotasOriginales = 12, int? diaDePago = null)
    {
        var entrada = CreditosDePrueba.Simple(cuotas: Math.Max(cuotas, cuotasOriginales));
        entrada = entrada with { Plan = entrada.Plan! with { Periodos = cuotasOriginales } };
        return entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("CPZ", LoanOperationKind.TermsChange, Desembolso, CreditosDePrueba.Instante(Desembolso, 16), 2)
            {
                Contenido = new ContenidoDeNovedad(NoveltyMechanism.TermCorrection, cuotas) { PrimerVencimiento = primerVencimiento, DiaDePago = diaDePago },
            }],
        };
    }

    [Theory]
    [InlineData(6)]
    [InlineData(24)]
    public void Desde_el_desembolso_el_plan_es_el_de_un_credito_que_nacio_con_las_cuotas_nuevas(int cuotas)
    {
        var original = GeneradorDePlan.Generar(CreditosDePrueba.Simple().Plan!);
        var primer = original.Cuotas[0].Vencimiento;

        var estado = MotorDeCartera.Reconstruir(ConCambio(cuotas, primer), Desembolso);
        var comoSiHubieraNacidoAsi = GeneradorDePlan.Generar(CreditosDePrueba.Simple().Plan! with { Periodos = cuotas, PrimerVencimiento = primer });

        estado.Plan!.Cuotas.Should().HaveCount(cuotas);
        estado.Plan.Cuotas.Select(q => (q.Numero, q.Vencimiento, q.Capital, q.Interes, q.Total))
            .Should().Equal(comoSiHubieraNacidoAsi.Cuotas.Select(q => (q.Numero, q.Vencimiento, q.Capital, q.Interes, q.Total)));
        estado.Plan.Cuotas[0].Vencimiento.Should().Be(primer, "el primer vencimiento se conserva: el día de pago no se corre");
        estado.Plan.TotalCapital.Should().Be(3_000_000m);
        estado.Saldo(LoanConcept.Principal).Should().Be(3_000_000m, "cambiar el plazo no mueve el capital");
    }

    [Fact]
    public void Acortar_sube_la_cuota_y_alargar_la_baja()
    {
        var primer = GeneradorDePlan.Generar(CreditosDePrueba.Simple().Plan!).Cuotas[0].Vencimiento;
        var hoy = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(), Desembolso).Plan!.Cuotas[0].Total;

        var corto = MotorDeCartera.Reconstruir(ConCambio(6, primer), Desembolso).Plan!.Cuotas[0].Total;
        var largo = MotorDeCartera.Reconstruir(ConCambio(24, primer), Desembolso).Plan!.Cuotas[0].Total;

        corto.Should().BeGreaterThan(hoy);
        largo.Should().BeLessThan(hoy);
    }

    [Fact]
    public void Despues_del_cambio_la_cuota_que_vence_es_la_del_plan_nuevo()
    {
        var primer = GeneradorDePlan.Generar(CreditosDePrueba.Simple().Plan!).Cuotas[0].Vencimiento;
        var entrada = ConCambio(6, primer);

        var vencida = MotorDeCartera.Reconstruir(entrada, primer.AddDays(1));

        vencida.Suspendida.Should().BeFalse();
        vencida.CapitalVencido.Should().Be(vencida.Plan!.Cuotas[0].Capital, "lo que vence es el capital de la primera cuota del plan nuevo");
    }

    [Fact]
    public void Con_otro_primer_vencimiento_y_su_dia_de_pago_las_fechas_corren_desde_ese_dia()
    {
        // El cambio de plazo manda el día del primer vencimiento nuevo como día de pago; sin él, las demás seguirían el día de pago viejo.
        var nuevoPrimero = new DateOnly(2027, 3, 5);

        var estado = MotorDeCartera.Reconstruir(ConCambio(10, nuevoPrimero, diaDePago: nuevoPrimero.Day), Desembolso);

        estado.Plan!.Cuotas[0].Vencimiento.Should().Be(nuevoPrimero);
        estado.Plan.Cuotas[1].Vencimiento.Should().Be(new DateOnly(2027, 4, 5));
        estado.Plan.Cuotas.Should().HaveCount(10);
    }

    [Fact]
    public void La_explicacion_dice_que_es_un_cambio_de_plazo_y_no_una_novedad()
    {
        var plan = GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(NoveltyMechanism.TermCorrection, Desembolso,
            CreditosDePrueba.Simple().Plan!, 3_000_000m, 18));

        plan.Explicacion.ToString().Should().Contain("Cambio de plazo");
        plan.Cuotas.Should().HaveCount(18);
    }
}
