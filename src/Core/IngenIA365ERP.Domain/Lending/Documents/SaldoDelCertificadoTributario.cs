namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>
/// Qué conceptos suma el <b>saldo al 31 de diciembre</b> del certificado tributario (decisión del dueño del 2026-10-16): cada cooperativa lo
/// configura con el parámetro <c>Documentos.CertificadoTributario.SaldoIncluye</c>. <see cref="MenosAnticipo"/> no es un concepto que se sume sino
/// el anticipo que se resta. El defecto, <see cref="Todos"/>, es lo que el certificado mostraba antes del parámetro: capital, intereses corrientes y
/// de mora (con sus suspensos) y los demás cargos (seguros, gastos de cobranza, otros) menos el anticipo. (nuevo)
/// </summary>
[Flags]
public enum ConceptosDelSaldoCertificado
{
    Ninguno = 0,

    /// <summary>El capital (<c>Principal</c>).</summary>
    Capital = 1,

    /// <summary>El interés corriente y el corriente en suspenso.</summary>
    InteresCorriente = 2,

    /// <summary>El interés de mora y el de mora en suspenso.</summary>
    InteresDeMora = 4,

    /// <summary>Seguros, gastos de cobranza y otros cargos.</summary>
    OtrosCargos = 8,

    /// <summary>Resta el anticipo del mismo crédito.</summary>
    MenosAnticipo = 16,

    Todos = Capital | InteresCorriente | InteresDeMora | OtrosCargos | MenosAnticipo,
}

/// <summary>
/// El texto del parámetro <c>Documentos.CertificadoTributario.SaldoIncluye</c>: los nombres de <see cref="ConceptosDelSaldoCertificado"/> separados
/// por coma (sin <c>Ninguno</c> ni <c>Todos</c>), en cualquier orden. (nuevo)
/// </summary>
public static class SaldoDelCertificadoTributario
{
    /// <summary>Los nombres admitidos, en el orden en que se escriben.</summary>
    public static IReadOnlyList<string> Nombres { get; } =
    [
        nameof(ConceptosDelSaldoCertificado.Capital), nameof(ConceptosDelSaldoCertificado.InteresCorriente),
        nameof(ConceptosDelSaldoCertificado.InteresDeMora), nameof(ConceptosDelSaldoCertificado.OtrosCargos),
        nameof(ConceptosDelSaldoCertificado.MenosAnticipo),
    ];

    /// <summary>El defecto: todos, que reproduce el saldo de antes del parámetro.</summary>
    public static string Defecto { get; } = string.Join(",", Nombres);

    /// <summary>La forma del texto: uno o más nombres admitidos separados por coma, sin espacios.</summary>
    public static string Patron { get; } = $"^({string.Join("|", Nombres)})(,({string.Join("|", Nombres)}))*$";

    /// <summary>Lee el texto; vacío o con un nombre desconocido, nulo (quien lee usa el defecto).</summary>
    public static ConceptosDelSaldoCertificado? Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var resultado = ConceptosDelSaldoCertificado.Ninguno;
        foreach (var parte in texto.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var i = Nombres.ToList().FindIndex(n => string.Equals(n, parte, StringComparison.OrdinalIgnoreCase));
            if (i < 0) return null;
            resultado |= Enum.Parse<ConceptosDelSaldoCertificado>(Nombres[i]);
        }
        return resultado == ConceptosDelSaldoCertificado.Ninguno ? null : resultado;
    }
}
