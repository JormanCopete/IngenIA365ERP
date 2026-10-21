using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Tests.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending;

/// <summary>
/// Feature 013, US4 (T0468; nombre fijo de DT §2.25; SC-020, FR-066, T6, R33): causar es idempotente y en orden. Causar d dos veces con la
/// misma marca da exactamente lo mismo (es una función: la base no escribe dos veces porque, tras causar d, la marca ya no es d − 1);
/// causar d sin <c>LastAccruedDate = d − 1</c> se rechaza con <c>Lending.Accrual.OutOfOrder</c>; causar día a día desde el origen
/// deja en el libro lo mismo que la reconstrucción y sus saldos. Créditos al azar con semilla fija.
/// </summary>
public class CausacionIdempotenteTests
{
    private const int Semilla = 20261012;

    [Fact]
    public void Causar_un_dia_dos_veces_equivale_a_una()
    {
        var azar = new Random(Semilla);
        using var _ = new AssertionScope();
        for (var i = 0; i < 100; i++)
        {
            var entrada = CreditosDePrueba.AlAzar(azar);
            var d = entrada.Operaciones[0].FechaValor.AddDays(azar.Next(1, 500));
            var una = MotorDeCartera.Causar(entrada, d.AddDays(-1), d);
            var otra = MotorDeCartera.Causar(entrada, d.AddDays(-1), d);
            otra.Filas.Should().Equal(una.Filas, $"crédito {i} el {d:yyyy-MM-dd}");
            otra.Transicion.Should().Be(una.Transicion);
            otra.Estado.Huella.Should().Be(una.Estado.Huella);
            una.Filas.Should().OnlyContain(f => f.Dia == d && f.Clase == LoanTransactionKind.Accrual);
        }
    }

    [Fact]
    public void Causar_sin_la_marca_en_la_vispera_se_rechaza()
    {
        var entrada = CreditosDePrueba.Simple();
        var d = new DateOnly(2027, 2, 10);
        foreach (var marca in new[] { d, d.AddDays(-3), d.AddDays(1) })
        {
            var causar = () => MotorDeCartera.Causar(entrada, marca, d);
            causar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(CausacionDeCredito.CodigoFueraDeOrden,
                $"con la marca en {marca:yyyy-MM-dd} no se causa el {d:yyyy-MM-dd}");
        }
    }

    [Fact]
    public void Causar_dia_a_dia_desde_el_origen_deja_el_libro_de_la_reconstruccion()
    {
        var azar = new Random(Semilla + 1);
        using var _ = new AssertionScope();
        for (var i = 0; i < 25; i++)
        {
            var entrada = CreditosDePrueba.AlAzar(azar);
            var origen = entrada.Operaciones[0].FechaValor;
            var hasta = origen.AddDays(azar.Next(30, 200));
            var causado = new List<MovimientoDeLibro>();
            var transiciones = new List<TransicionDeEstado>();
            for (var d = origen.AddDays(1); d <= hasta; d = d.AddDays(1))
            {
                var r = MotorDeCartera.Causar(entrada, d.AddDays(-1), d);
                r.Causo.Should().BeTrue();
                causado.AddRange(r.Filas);
                if (r.Transicion is { } t) transiciones.Add(t);
            }
            var estado = MotorDeCartera.Reconstruir(entrada, hasta, origen);
            var donde = $"crédito {i} hasta {hasta:yyyy-MM-dd}";
            causado.Should().Equal(estado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.Accrual), donde);
            transiciones.Should().Equal(estado.Transiciones, $"{donde}: las transiciones salen igual de a un día");
            foreach (var s in estado.Saldos.Where(s => s.Concepto != LoanConcept.Principal))
                causado.Where(m => m.Concepto == s.Concepto && m.Cuota == s.Cuota).Sum(m => m.Monto).Should().Be(s.Valor, $"{donde}: {s.Concepto} cuota {s.Cuota}");
            MotorDeCartera.Verificar(entrada, hasta, [.. estado.Libro.Movimientos.Where(m => m.Clase != LoanTransactionKind.Accrual), .. causado])
                .Should().BeEmpty($"{donde}: el libro causado de a un día cuadra con la reconstrucción");
        }
    }

    [Fact]
    public void Un_dia_sin_usura_certificada_no_se_causa()
    {
        var entrada = CreditosDePrueba.Simple();
        var ultima = entrada.Usura.Max(u => u.ValidTo!.Value);
        var r = MotorDeCartera.Causar(entrada, ultima, ultima.AddDays(1));
        r.Causo.Should().BeFalse();
        r.Filas.Should().BeEmpty();
        r.Problema!.Codigo.Should().Be(CausacionDeCredito.CodigoValorNacionalFaltante);
        MotorDeCartera.Causar(entrada, ultima.AddDays(-1), ultima).Causo.Should().BeTrue("el último día certificado sí se causa");
        var noRequerida = entrada with { Condiciones = entrada.Condiciones with { UsuraRequerida = false } };
        MotorDeCartera.Causar(noRequerida, ultima, ultima.AddDays(1)).Causo.Should().BeTrue("una modalidad sin usura no se bloquea");
    }
}
