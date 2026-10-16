using System.Globalization;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos de los documentos al deudor (feature 013, US13, T0792, T0793): clases, estados, eventos y entrega como los ve la persona, y lo que muestra
/// la verificación a partir de sus datos esenciales. (nuevo)
/// </summary>
public static class TextosDeDocumentos
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Las clases de K1: plan de pagos, paz y salvo de una obligación o general y certificado de saldo.</summary>
    public static IReadOnlyList<(string Valor, string Texto)> ClasesDeK1 { get; } =
    [
        ("PaymentPlan", "Plan de pagos"),
        ("LoanClearance", "Paz y salvo de una obligación"),
        ("GeneralClearance", "Paz y salvo general"),
        ("BalanceCertificate", "Certificado de saldo"),
    ];

    /// <summary>Todas las clases, las que se emiten hoy (K5, T0975: el extracto y el certificado tributario se suman a las de K1).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> Clases { get; } =
        [.. ClasesDeK1, ("Statement", "Extracto"), ("TaxCertificate", "Certificado tributario")];

    /// <summary>Las clases que la pantalla ofrece para emitir.</summary>
    public static IReadOnlyList<(string Valor, string Texto)> ClasesQueSeEmiten => Clases;

    /// <summary>¿La clase se pide sin crédito? (el paz y salvo general y el certificado tributario se emiten a nombre de la persona).</summary>
    public static bool EsDeLaPersona(string clase) => clase is "GeneralClearance" or "TaxCertificate";

    /// <summary>Lo que dice la verificación tras comparar un PDF (K5): nada si no se comparó.</summary>
    public static string? ComparacionDelPdf(bool? coincide) => coincide switch
    {
        true => "El PDF es idéntico al emitido: su huella coincide con la guardada.",
        false => "El PDF NO es el emitido: su huella no coincide con la guardada. Puede estar alterado o ser de otro documento.",
        null => null,
    };

    public static IReadOnlyList<(string Valor, string Texto)> Estados { get; } =
        [("Valid", "Vigente"), ("SubjectToFinality", "Sujeto a firmeza"), ("Voided", "Anulado")];

    public static string Clase(string? clase) => Clases.FirstOrDefault(c => c.Valor == clase).Texto ?? clase ?? "—";

    public static string Estado(string? estado) => Estados.FirstOrDefault(e => e.Valor == estado).Texto ?? estado ?? "—";

    /// <summary>La clase de aviso del estado (sistema de diseño).</summary>
    public static string AvisoDeEstado(string? estado) => estado switch
    {
        "Valid" => "aviso-ok",
        "SubjectToFinality" => "aviso-alerta",
        _ => "aviso-error",
    };

    public static string Evento(string? evento) => evento switch
    {
        "Issued" => "Emitido",
        "Delivered" => "Entregado por correo",
        "Voided" => "Anulado",
        _ => evento ?? "—",
    };

    public static string Entrega(string? entrega) => entrega switch
    {
        "Download" => "Descarga",
        "Email" => "Correo",
        _ => entrega ?? "—",
    };

    /// <summary>¿La clase se emite sobre un crédito? (todas menos el paz y salvo general).</summary>
    public static bool LlevaCredito(string clase) => clase is "PaymentPlan" or "LoanClearance" or "BalanceCertificate" or "Statement";

    public static string Fecha(DateOnly? fecha) => fecha?.ToString("dd/MM/yyyy", Co) ?? "—";

    public static string Momento(DateTime momento) => momento.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Co);

    public static string Pesos(decimal? valor) => valor is { } v ? "$ " + v.ToString("#,0", Co) : "—";

    /// <summary>
    /// Los datos esenciales que guardó la emisión, como pares «rótulo: valor» legibles (corte, créditos, saldo para cancelar, firmeza…). Lo que no
    /// reconoce lo muestra con su clave.
    /// </summary>
    public static IReadOnlyList<(string Rotulo, string Valor)> DatosEsenciales(JsonElement datos)
    {
        var filas = new List<(string, string)>();
        if (datos.ValueKind != JsonValueKind.Object) return filas;
        foreach (var p in datos.EnumerateObject())
        {
            if (p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
            filas.Add((Rotulo(p.Name), Valor(p.Value)));
        }
        return filas;
    }

    private static string Rotulo(string clave) => clave switch
    {
        "asOf" => "Corte",
        "loan" => "Crédito",
        "loans" => "Créditos",
        "scheduleVersion" => "Versión del plan",
        "effectiveFrom" => "Vigente desde",
        "installments" => "Cuotas",
        "effectiveAnnualRate" => "Tasa efectiva anual",
        "finalityUntil" => "Sujeto a firmeza hasta",
        "coversCoDebts" => "Incluye codeudas",
        "outsideLending" => "Ventas fuera de Cartera",
        "date" => "Fecha de la liquidación",
        "toPayOff" => "Para cancelar",
        "validUntil" => "Válido hasta",
        "periodFrom" => "Desde",
        "periodTo" => "Hasta",
        "openingBalance" => "Saldo inicial",
        "closingBalance" => "Saldo final",
        "movements" => "Operaciones del período",
        "year" => "Año",
        "totalInterestPaid" => "Intereses pagados",
        "totalHousingInterestPaid" => "Intereses pagados en vivienda",
        "asOfDate" => "Fecha de la información",
        _ => clave,
    };

    private static string Valor(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var f) ? Fecha(f) : v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.TryGetDecimal(out var d) ? (d == decimal.Truncate(d) && Math.Abs(d) >= 1000 ? Pesos(d) : d.ToString("0.######", Co)) : v.GetRawText(),
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Array => string.Join("; ", v.EnumerateArray().Select(Elemento)),
        _ => v.GetRawText(),
    };

    private static string Elemento(JsonElement e) => e.ValueKind == JsonValueKind.Object
        ? string.Join(" · ", e.EnumerateObject().Where(p => p.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            .Select(p => p.Name switch
            {
                "number" => p.Value.GetString() ?? string.Empty,
                "role" => p.Value.GetString() ?? string.Empty,
                "balance" => $"saldo {Valor(p.Value)}",
                "interestPaid" => $"intereses pagados {Valor(p.Value)}",
                "balanceAtYearEnd" => $"saldo al 31/12 {Valor(p.Value)}",
                "housing" => p.Value.ValueKind == JsonValueKind.True ? "vivienda" : string.Empty,
                _ => Valor(p.Value),
            }))
        : Valor(e);
}
