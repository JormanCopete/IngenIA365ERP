using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Cash;

namespace IngenIA365ERP.Domain.Tests.Sales.Cash;

/// <summary>
/// Feature 012, I3, T547 (FR-099, FR-100, SC-025, T50; data-model §15 «Arqueo»; contracts/api.md §21.2): los motores puros de
/// la caja coinciden con los casos calculados a mano de <c>Casos/</c>. <see cref="CalculadoraDeEsperado"/>: esperado por medio =
/// base (sólo el efectivo) + recibido − reintegrado ± movimientos confirmados ± reclasificaciones; <c>WithdrawalToRegister</c>
/// resta en la sesión origen y suma en la destino; <c>BaseIncome</c> entra; un movimiento sin confirmar no cuenta; las tarjetas
/// llevan su esperado por datáfono. <see cref="EvaluadorDeArqueo"/>: diferencia = contado − esperado; los créditos
/// (<c>CashCountMethod.None</c>) cuentan lo esperado; dentro de la tolerancia <b>copiada</b>; el sobrante siempre
/// <c>Surplus</c> y el faltante según el tratamiento sellado en la sesión; monto a aprobar = Σ |diferencia| de las líneas
/// sobre la tolerancia; toda diferencia exige motivo. El caso 09 es SC-025 (300 ventas, 6 medios).
/// </summary>
public class CalculadoraDeEsperadoCasosTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Sales", "Cash", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T547()
    {
        var nombres = Directory.EnumerateFiles(Directorio, "*.json").Select(Path.GetFileName).ToList();
        foreach (var prefijo in new[] { "01-", "02-", "03-", "04-", "05-", "06-", "07-", "08-", "09-" })
            nombres.Should().Contain(n => n!.StartsWith(prefijo, StringComparison.Ordinal), $"falta el caso {prefijo}*");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_caja_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var medios = caso.Medios.ToDictionary(m => m.Codigo, m => new MedioDeArqueo(
            m.Id, m.Codigo, Enum.Parse<PaymentMeansClass>(m.Clase), Enum.Parse<CashCountMethod>(m.Arqueo), m.Tolerancia));
        int Id(string codigo) => medios[codigo].PaymentMeansId;

        var pagos = new List<PagoDeLaSesion>();
        foreach (var p in caso.Pagos)
            for (var i = 0; i < p.Repetir; i++)
                pagos.Add(new PagoDeLaSesion(pagos.Count + 1, Id(p.Medio), Enum.Parse<PaymentDirection>(p.Sentido), p.Valor,
                    p.Datafono, p.Referencia, $"POS-{pagos.Count + 1}"));

        var movimientos = caso.Movimientos.Select((m, i) => new MovimientoDeCaja(i + 1, Enum.Parse<CashMovementKind>(m.Tipo), m.Confirmado,
            m.SesionOrigen, m.SesionDestino, Id(m.Medio), m.MedioDestino is null ? null : Id(m.MedioDestino), m.Valor,
            m.DatafonoOrigen, m.DatafonoDestino)).ToList();

        var esperado = CalculadoraDeEsperado.Calcular(new PedidoDeEsperado(caso.Sesion, medios.Values.ToList(),
            caso.Base is null ? null : new BaseDeApertura(Id(caso.Base.Medio), caso.Base.Valor), pagos, movimientos));

        using (new AssertionScope($"{archivo} — {caso.Nombre} — esperado"))
        {
            esperado.Lines.Select(l => l.Medio.Code).Should().Equal(caso.Esperado.Select(e => e.Medio), "una línea por medio con saldo, en el orden de los medios");
            foreach (var e in caso.Esperado)
            {
                var l = esperado.Lines.Single(x => x.Medio.Code == e.Medio);
                l.OpeningBase.Should().Be(e.Base, $"base de {e.Medio}");
                l.Sales.Should().Be(e.Ventas, $"ventas de {e.Medio}");
                l.Refunds.Should().Be(e.Devoluciones, $"devoluciones de {e.Medio}");
                l.MovementsIn.Should().Be(e.Entradas, $"entradas de {e.Medio}");
                l.MovementsOut.Should().Be(e.Salidas, $"salidas de {e.Medio}");
                l.ReclassificationsIn.Should().Be(e.ReclasificacionesEntrada, $"reclasificaciones que entran a {e.Medio}");
                l.ReclassificationsOut.Should().Be(e.ReclasificacionesSalida, $"reclasificaciones que salen de {e.Medio}");
                l.Expected.Should().Be(e.Esperado, $"esperado de {e.Medio}");
                l.PaymentsCount.Should().Be(e.Pagos, $"pagos de {e.Medio}");
                l.Tolerance.Should().Be(medios[e.Medio].ToleranceAmount, "la tolerancia es la del medio al cerrar");
                if (e.Datafonos is not null)
                {
                    l.Terminals.Select(t => (t.CardTerminalId, t.Expected, t.PaymentsCount)).Should()
                        .Equal(e.Datafonos.Select(d => (d.Datafono, d.Esperado, d.Pagos)), $"datáfonos de {e.Medio}");
                }
                if (e.Referencias is not null)
                    l.References.Select(r => r.Reference).Should().Equal(e.Referencias, $"referencias para cotejar de {e.Medio}");
            }
            esperado.Total.Should().Be(caso.Total);
        }

        var arqueo = EvaluadorDeArqueo.Evaluar(esperado,
            caso.Conteo.Select(c => new ConteoDeMedio(Id(c.Medio), c.Contado, c.Motivo)).ToList(),
            Enum.Parse<CashDifferenceTreatment>(caso.TratamientoDelFaltante));

        using (new AssertionScope($"{archivo} — {caso.Nombre} — arqueo"))
        {
            var a = caso.Arqueo;
            arqueo.Lines.Select(l => l.Code).Should().Equal(a.Lineas.Select(l => l.Medio));
            foreach (var e in a.Lineas)
            {
                var l = arqueo.Lines.Single(x => x.Code == e.Medio);
                l.Counted.Should().Be(e.Contado, $"contado de {e.Medio}");
                l.Difference.Should().Be(e.Diferencia, $"diferencia de {e.Medio}");
                l.WithinTolerance.Should().Be(e.DentroDeTolerancia, $"tolerancia de {e.Medio}");
                l.Treatment?.ToString().Should().Be(e.Tratamiento, $"tratamiento de {e.Medio}");
                if (e.Tratamiento is null) l.Treatment.Should().BeNull($"{e.Medio} no tiene diferencia");
            }
            arqueo.TotalExpected.Should().Be(a.TotalEsperado);
            arqueo.TotalCounted.Should().Be(a.TotalContado);
            arqueo.TotalDifference.Should().Be(a.TotalDiferencia);
            arqueo.AmountToApprove.Should().Be(a.MontoAAprobar);
            arqueo.RequiresApproval.Should().Be(a.MontoAAprobar > 0m);
            arqueo.HasDifference.Should().Be(a.LineasDelDocumento.Count > 0);
            arqueo.DifferenceLines.Select(d => (d.Code, (int)d.Sign, d.Amount, d.Treatment.ToString(), d.WithinTolerance)).Should()
                .Equal(a.LineasDelDocumento.Select(d => (d.Medio, d.Signo, d.Valor, d.Tratamiento, d.DentroDeTolerancia)));
            arqueo.DifferenceLines.Select(d => (int)d.LineNumber).Should().Equal(Enumerable.Range(1, a.LineasDelDocumento.Count));
            arqueo.MissingReasons.Should().Equal(a.SinMotivo);
        }
    }

    private sealed class Caso
    {
        public string Nombre { get; set; } = string.Empty;
        public int Sesion { get; set; }
        public string TratamientoDelFaltante { get; set; } = string.Empty;
        public List<MedioJson> Medios { get; set; } = [];
        public BaseJson? Base { get; set; }
        public List<PagoJson> Pagos { get; set; } = [];
        public List<MovimientoJson> Movimientos { get; set; } = [];
        public List<EsperadoJson> Esperado { get; set; } = [];
        public decimal Total { get; set; }
        public List<ConteoJson> Conteo { get; set; } = [];
        public ArqueoJson Arqueo { get; set; } = new();
    }

    private sealed class MedioJson
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Clase { get; set; } = string.Empty;
        public string Arqueo { get; set; } = string.Empty;
        public decimal Tolerancia { get; set; }
    }

    private sealed class BaseJson
    {
        public string Medio { get; set; } = string.Empty;
        public decimal Valor { get; set; }
    }

    private sealed class PagoJson
    {
        public string Medio { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public string Sentido { get; set; } = string.Empty;
        public int? Datafono { get; set; }
        public string? Referencia { get; set; }
        public int Repetir { get; set; } = 1;
    }

    private sealed class MovimientoJson
    {
        public string Tipo { get; set; } = string.Empty;
        public string Medio { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public bool Confirmado { get; set; }
        public string? MedioDestino { get; set; }
        public int SesionOrigen { get; set; }
        public int? SesionDestino { get; set; }
        public int? DatafonoOrigen { get; set; }
        public int? DatafonoDestino { get; set; }
    }

    private sealed class EsperadoJson
    {
        public string Medio { get; set; } = string.Empty;
        public decimal Base { get; set; }
        public decimal Ventas { get; set; }
        public decimal Devoluciones { get; set; }
        public decimal Entradas { get; set; }
        public decimal Salidas { get; set; }
        public decimal ReclasificacionesEntrada { get; set; }
        public decimal ReclasificacionesSalida { get; set; }
        public decimal Esperado { get; set; }
        public int Pagos { get; set; }
        public List<DatafonoJson>? Datafonos { get; set; }
        public List<string>? Referencias { get; set; }
    }

    private sealed class DatafonoJson
    {
        public int Datafono { get; set; }
        public decimal Esperado { get; set; }
        public int Pagos { get; set; }
    }

    private sealed class ConteoJson
    {
        public string Medio { get; set; } = string.Empty;
        public decimal? Contado { get; set; }
        public string? Motivo { get; set; }
    }

    private sealed class ArqueoJson
    {
        public List<LineaJson> Lineas { get; set; } = [];
        public decimal TotalEsperado { get; set; }
        public decimal TotalContado { get; set; }
        public decimal TotalDiferencia { get; set; }
        public decimal MontoAAprobar { get; set; }
        public List<DiferenciaJson> LineasDelDocumento { get; set; } = [];
        public List<string> SinMotivo { get; set; } = [];
    }

    private sealed class LineaJson
    {
        public string Medio { get; set; } = string.Empty;
        public decimal Contado { get; set; }
        public decimal Diferencia { get; set; }
        public bool DentroDeTolerancia { get; set; }
        public string? Tratamiento { get; set; }
    }

    private sealed class DiferenciaJson
    {
        public string Medio { get; set; } = string.Empty;
        public int Signo { get; set; }
        public decimal Valor { get; set; }
        public string Tratamiento { get; set; } = string.Empty;
        public bool DentroDeTolerancia { get; set; }
    }
}
