using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Arqueo;

namespace IngenIA365ERP.Domain.Tests.Receipts;

/// <summary>
/// Feature 015, T0079 (R-14; FR-031, FR-032; US2): el motor puro del arqueo de la caja coincide con los casos calculados a mano de <c>Casos/arqueo-*.json</c>.
/// Esperado por medio = base (el medio de la base) + recibos − anulaciones en la sesión ± movimientos (abastecimiento, retiro a bóveda, traslados entre
/// cajas) ± reclasificaciones; contado según el método del medio (efectivo por denominaciones o total, tarjetas por lote, transferencias por referencias
/// confirmadas, sin método = lo esperado); diferencia = contado − esperado; dentro de la tolerancia del medio; el faltante va al tratamiento sellado
/// (cuenta por cobrar al cajero o gasto) y el sobrante a sobrantes; a aprobar = Σ |diferencia| de las líneas sobre la tolerancia; toda diferencia lleva
/// motivo. El arqueo ciego no cambia el cálculo: sólo cuándo se ve lo esperado (lo prueba la sesión).
/// </summary>
public class ArqueoDeCajaTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Receipts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "arqueo-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0079()
    {
        Directory.EnumerateFiles(Directorio, "arqueo-*.json").Should().HaveCountGreaterThanOrEqualTo(12, "≈12 casos dorados del arqueo (T0079)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_arqueo_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var medios = caso.Medios.ToDictionary(m => m.Codigo, m => new MedioDeArqueo(m.Id, m.Codigo, Enum.Parse<PaymentMeansClass>(m.Clase),
            Enum.Parse<CashCountMethod>(m.Arqueo), m.Tolerancia));
        int Id(string codigo) => medios[codigo].PaymentMeansId;

        var esperado = ArqueoDeCaja.Esperado(new PedidoDeArqueo(
            medios.Values.ToList(),
            caso.MedioDeLaBase is null ? null : Id(caso.MedioDeLaBase),
            caso.Base,
            caso.Cobros.Select(c => new CobroDeLaSesion(Id(c.Medio), c.Valor, c.Anulado, c.Referencia)).ToList(),
            caso.Movimientos.Select(m => new MovimientoDeLaSesion(Enum.Parse<CashMovementKindR>(m.Tipo), Id(m.Medio), m.MedioDestino is null ? null : Id(m.MedioDestino),
                m.Valor, m.EsOrigen)).ToList()));

        var resultado = ArqueoDeCaja.Arquear(esperado, caso.Conteo.Select(k => new ConteoDeMedio(Id(k.Medio), k.Total,
                k.Denominaciones?.Select(d => new DenominacionContada(d.Valor, d.Cantidad)).ToList(),
                k.Referencias?.Select(r => new ReferenciaContada(r.Referencia, r.Valor, r.Confirmada)).ToList(),
                k.Lotes?.Select(l => new LoteContado(l.Datafono, l.Lote, l.Valor)).ToList(),
                k.Motivo)).ToList(),
            Enum.Parse<CountTreatment>(caso.TratamientoDelFaltante));

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            resultado.Lineas.Select(l => l.Code).Should().Equal(caso.Esperado.Select(e => e.Medio), "una línea por medio sellado, en su orden");
            foreach (var e in caso.Esperado)
            {
                var x = esperado.Single(l => l.Medio.Code == e.Medio);
                x.Base.Should().Be(e.Base, $"base de {e.Medio}");
                x.Cobros.Should().Be(e.Cobros, $"recibos de {e.Medio}");
                x.Anulaciones.Should().Be(e.Anulaciones, $"anulaciones de {e.Medio}");
                x.Entradas.Should().Be(e.Entradas, $"entradas de {e.Medio}");
                x.Salidas.Should().Be(e.Salidas, $"salidas de {e.Medio}");
                var l = resultado.Lineas.Single(y => y.Code == e.Medio);
                l.Expected.Should().Be(e.Esperado, $"esperado de {e.Medio}");
                l.Counted.Should().Be(e.Contado, $"contado de {e.Medio}");
                l.Difference.Should().Be(e.Diferencia, $"diferencia de {e.Medio}");
                l.WithinTolerance.Should().Be(e.Dentro, $"tolerancia de {e.Medio}");
                l.Treatment.Should().Be(Enum.Parse<CountTreatment>(e.Tratamiento), $"tratamiento de {e.Medio}");
            }
            resultado.TotalEsperado.Should().Be(caso.TotalEsperado);
            resultado.TotalContado.Should().Be(caso.TotalContado);
            resultado.MontoAAprobar.Should().Be(caso.AAprobar);
            resultado.RequiereAprobacion.Should().Be(caso.AAprobar > 0);
            resultado.SinMotivo.Should().Equal(caso.SinMotivo);
        }
    }

    [Fact]
    public void El_faltante_va_al_cajero_o_al_gasto()
    {
        var act = () => ArqueoDeCaja.Arquear([], [], CountTreatment.Surplus);
        act.Should().Throw<ArgumentOutOfRangeException>("el faltante sólo va a cuenta por cobrar al cajero o a gasto");
    }

    [Fact]
    public void Un_conteo_de_un_medio_que_no_esta_en_la_sesion_es_un_defecto()
    {
        var act = () => ArqueoDeCaja.Arquear([], [new ConteoDeMedio(99, 10m, null, null, null, null)], CountTreatment.CashierReceivable);
        act.Should().Throw<ArgumentException>();
    }

    private sealed record Caso(
        string Nombre, string Descripcion, string TratamientoDelFaltante, List<Medio> Medios, string? MedioDeLaBase, decimal Base, List<Cobro> Cobros,
        List<Movimiento> Movimientos, List<Conteo> Conteo, List<EsperadoDelCaso> Esperado, decimal TotalEsperado, decimal TotalContado, decimal AAprobar,
        List<string> SinMotivo);

    private sealed record Medio(int Id, string Codigo, string Clase, string Arqueo, decimal Tolerancia);

    private sealed record Cobro(string Medio, decimal Valor, bool Anulado, string? Referencia);

    private sealed record Movimiento(string Tipo, string Medio, string? MedioDestino, decimal Valor, bool EsOrigen);

    private sealed record Conteo(string Medio, decimal? Total, List<Denominacion>? Denominaciones, List<ReferenciaDelCaso>? Referencias, List<LoteDelCaso>? Lotes, string? Motivo);

    private sealed record Denominacion(decimal Valor, int Cantidad);

    private sealed record ReferenciaDelCaso(string Referencia, decimal Valor, bool Confirmada);

    private sealed record LoteDelCaso(int Datafono, string Lote, decimal Valor);

    private sealed record EsperadoDelCaso(string Medio, decimal Base, decimal Cobros, decimal Anulaciones, decimal Entradas, decimal Salidas, decimal Esperado,
        decimal Contado, decimal Diferencia, bool Dentro, string Tratamiento);
}
