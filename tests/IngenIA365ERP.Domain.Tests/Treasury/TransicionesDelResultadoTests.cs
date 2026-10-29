using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Resultados;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, T0151 (R-18; FR-215; data-model §19.9): las transiciones fijas del resultado del giro coinciden con <c>Casos/resultado-*.json</c>: enviada por
/// transferencia admite girada o rechazada; en cheque sólo girada; un cheque girado sólo «no cobrado»; una transferencia girada, nada; la anulación del origen
/// antes de enviar deja anulada; antes de enviar sin anulación, <c>Treasury.Result.NotSent</c>. Prueba escrita, sin correr.
/// </summary>
public class TransicionesDelResultadoTests
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    private static string Directorio => Path.Combine(AppContext.BaseDirectory, "Treasury", "Casos");

    public static IEnumerable<object[]> Casos() =>
        Directory.EnumerateFiles(Directorio, "resultado-*.json").OrderBy(f => f, StringComparer.Ordinal).Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_minimos_de_T0151() =>
        Directory.EnumerateFiles(Directorio, "resultado-*.json").Should().HaveCountGreaterThanOrEqualTo(8, "≈8 casos dorados de las transiciones (T0151)");

    [Theory]
    [MemberData(nameof(Casos))]
    public void La_transicion_coincide_con_la_tabla(string archivo)
    {
        var caso = JsonSerializer.Deserialize<Caso>(File.ReadAllText(Path.Combine(Directorio, archivo)), Opciones)!;
        var r = TransicionesDelResultado.Evaluar(Enum.Parse<PaymentOrderState>(caso.Estado), Enum.Parse<PaymentForm>(caso.Forma), caso.PagadaPorArchivo,
            caso.AnulacionDelOrigen, Enum.Parse<DisbursementOutcome>(caso.Resultado));

        using (new AssertionScope($"{archivo} — {caso.Nombre}"))
        {
            r.Admite.Should().Be(caso.Esperado.Admite);
            r.Hacia.Should().Be(caso.Esperado.Hacia is null ? null : Enum.Parse<PaymentOrderState>(caso.Esperado.Hacia));
            r.Codigo.Should().Be(caso.Esperado.Codigo);
        }
    }

    [Theory]
    [InlineData(PaymentOrderState.Received)]
    [InlineData(PaymentOrderState.Pending)]
    [InlineData(PaymentOrderState.Ready)]
    [InlineData(PaymentOrderState.InBatch)]
    [InlineData(PaymentOrderState.Approved)]
    public void Antes_de_enviar_ningun_resultado_salvo_la_anulacion_del_origen(PaymentOrderState estado)
    {
        foreach (var resultado in new[] { DisbursementOutcome.Disbursed, DisbursementOutcome.Rejected, DisbursementOutcome.CheckNotCashed })
            TransicionesDelResultado.Evaluar(estado, PaymentForm.Transfer, false, false, resultado).Admite.Should().BeFalse();
        TransicionesDelResultado.Evaluar(estado, PaymentForm.Transfer, false, true, DisbursementOutcome.Voided).Hacia.Should().Be(PaymentOrderState.Voided);
    }

    [Fact]
    public void Nunca_rechazada_despues_de_girada()
    {
        foreach (var forma in Enum.GetValues<PaymentForm>())
            TransicionesDelResultado.Evaluar(PaymentOrderState.Disbursed, forma, false, false, DisbursementOutcome.Rejected).Admite.Should().BeFalse();
    }

    private sealed record Caso(string Nombre, string Estado, string Forma, bool PagadaPorArchivo, bool AnulacionDelOrigen, string Resultado, Esperado Esperado);

    private sealed record Esperado(bool Admite, string? Hacia, string? Codigo);
}
