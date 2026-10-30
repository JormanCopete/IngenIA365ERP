using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Receipts.Imputacion;

namespace IngenIA365ERP.Domain.Tests.Receipts;

/// <summary>
/// Feature 015, T0101 (R-11; FR-041, FR-188; D35): el motor puro que reparte los medios de un recibo entre sus aplicaciones coincide con los casos de
/// <c>Casos/imputacion-*.json</c>. Por defecto los medios inmediatos cubren primero, en el orden de la pantalla, y el cheque cubre lo restante;
/// <c>Recibo.OrdenDeImputacion = ElegidoEnCaja</c> respeta el orden en que se registraron; el cajero puede fijar la imputación a mano y el motor sólo la
/// valida. Total de medios = aplicado + vueltas; vueltas sólo en efectivo y nunca más que el efectivo; ninguna porción en cero.
/// </summary>
public class ImputacionDeMediosTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Receipts", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "imputacion-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0101()
    {
        Directory.EnumerateFiles(Directorio, "imputacion-*.json").Should().HaveCountGreaterThanOrEqualTo(10, "≈10 casos dorados de la imputación (T0101)");
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_imputacion_coincide_con_el_calculo_manual(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var pedido = new PedidoDeImputacion(
            caso.Aplicaciones.Select(a => new AplicacionAImputar(a.Id, a.Valor)).ToList(),
            caso.Medios.Select(m => new MedioAImputar(m.Id, Enum.Parse<PaymentMeansClass>(m.Clase), m.Valor)).ToList(),
            Enum.Parse<OrdenDeImputacion>(caso.Orden),
            caso.Fijada?.Select(p => new PorcionImputada(p.Aplicacion, p.Medio, p.Valor)).ToList());

        var r = ImputacionDeMedios.Imputar(pedido);

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            r.Error.Should().Be(caso.Error);
            if (caso.Error is not null) return;
            r.Porciones.Select(p => (p.Aplicacion, p.Medio, p.Valor)).Should().Equal(caso.Porciones.Select(p => (p.Aplicacion, p.Medio, p.Valor)));
            r.Vueltas.Should().Be(caso.Vueltas);
            r.Porciones.Should().OnlyContain(p => p.Valor > 0, "ninguna porción en cero");
        }
    }

    private sealed record Caso(string Nombre, string Descripcion, string Orden, List<Aplicacion> Aplicaciones, List<Medio> Medios, List<Porcion>? Fijada,
        List<Porcion> Porciones, decimal Vueltas, string? Error);

    private sealed record Aplicacion(string Id, decimal Valor);

    private sealed record Medio(string Id, string Clase, decimal Valor);

    private sealed record Porcion(string Aplicacion, string Medio, decimal Valor);
}
