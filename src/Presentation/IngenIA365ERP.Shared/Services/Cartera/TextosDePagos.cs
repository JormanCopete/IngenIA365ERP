namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de los pagos (feature 013, US5, T0562–T0563): las siete modalidades, la elección del abono, las clases de medio, los
/// resultados del contrato y las formas de resolver un saldo. Las rutas de la pantalla de Cartera devuelven el nombre; las del contrato, el
/// número (<c>EnumsDelContratoDeCartera</c>): aquí están los dos. Ningún texto muestra un código técnico (FR-013); un valor que no conoce se
/// muestra tal cual. (nuevo)
/// </summary>
public static class TextosDePagos
{
    /// <summary>Las modalidades: número del contrato, nombre y texto.</summary>
    public static IReadOnlyList<(int Numero, string Valor, string Texto)> Modalidades { get; } =
    [
        (1, "FullPayoff", "Cancelación total"),
        (2, "PastDue", "Lo vencido"),
        (3, "CycleInstallment", "Cuota del ciclo"),
        (4, "Advance", "Anticipo de cuotas"),
        (5, "ExtraordinaryPrincipal", "Abono extraordinario a capital"),
        (6, "SpecificInstallment", "Cuota específica"),
        (7, "FreeAmount", "Valor libre"),
    ];

    public static string Modalidad(string? v) => Modalidades.FirstOrDefault(m => m.Valor == v).Texto ?? v ?? "—";

    public static string Modalidad(int v) => Modalidades.FirstOrDefault(m => m.Numero == v).Texto ?? v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string NombreDeModalidad(int v) => Modalidades.FirstOrDefault(m => m.Numero == v).Valor ?? "FreeAmount";

    /// <summary>Las elecciones del deudor ante un abono extraordinario.</summary>
    public static IReadOnlyList<(int Numero, string Valor, string Texto)> Elecciones { get; } =
    [
        (1, "ReduceTerm", "Reducir el plazo"),
        (2, "ReduceInstallment", "Reducir la cuota"),
    ];

    public static string Eleccion(string? v) => v is null ? "Sin preferencia" : Elecciones.FirstOrDefault(e => e.Valor == v).Texto ?? v;

    public static string Eleccion(int? v) => v is { } n ? Elecciones.FirstOrDefault(e => e.Numero == n).Texto ?? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "—";

    /// <summary>De dónde nació una versión de la preferencia.</summary>
    public static string OrigenDePreferencia(string? v) => v switch
    {
        "Application" => "Solicitud",
        "CreditLimitGrant" => "Otorgamiento del cupo",
        "DebtorRequest" => "Instrucción del deudor",
        "Migration" => "Saldo inicial",
        "InheritedFromLimit" => "Heredada del cupo",
        _ => v ?? "—",
    };

    /// <summary>Las clases de medio que admite el recaudador de prueba (las de crédito las rechaza siempre el contrato).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> ClasesDeMedio { get; } =
    [
        ("Cash", "Efectivo"), ("CreditCard", "Tarjeta de crédito"), ("DebitCard", "Tarjeta débito"), ("BankDeposit", "Consignación"),
        ("Transfer", "Transferencia"), ("Voucher", "Bono"), ("Check", "Cheque"), ("ExternalPaymasterDeduction", "Descuento de pagaduría externa"),
        ("OwnPayrollDeduction", "Descuento de nómina propia"), ("InternalOffset", "Compensación interna"), ("Other", "Otro"),
        ("AssociateCredit", "Crédito de asociado (el contrato lo rechaza)"),
    ];

    public static string ClaseDeMedio(string? v) => ClasesDeMedio.FirstOrDefault(c => c.Valor == v).Texto ?? v ?? "—";

    /// <summary>Quién recaudó el pago (<c>ClaseDeRecaudador</c> por su nombre).</summary>
    public static string ClaseDeRecaudador(string? v) => v switch
    {
        "Receipts" => "Recaudo",
        "Payroll" => "Nómina",
        "Contributions" => "Aportes",
        "OwnLoanPayoff" => "Recogida de un desembolso",
        "Test" => "Recaudador de prueba",
        _ => v ?? "—",
    };

    /// <summary>
    /// Si Cartera reversa el pago por pantalla o lo reversa su recaudador (Nómina, Aportes, la recogida): nulo si se ofrece, o el texto que se
    /// muestra en su lugar. La regla es la del servidor (<c>Lending.Reversal.OwnedByCollector</c>); la pantalla sólo evita ofrecer lo que no puede.
    /// </summary>
    public static string? QuienReversa(string? claseDeRecaudador) => claseDeRecaudador switch
    {
        "Payroll" => "Sólo Nómina lo reversa, al reversar su nómina o su liquidación.",
        "Contributions" => "Sólo Aportes lo reversa.",
        "OwnLoanPayoff" => "Es la recogida de un desembolso: se reversa al anular el crédito que lo recogió.",
        _ => null,
    };

    /// <summary>El resultado de un ítem del contrato (<c>ResultadoDelContrato</c>).</summary>
    public static string Resultado(int v) => v switch
    {
        1 => "Aplicado",
        2 => "Ya estaba aplicado (misma clave)",
        3 => "Rechazado",
        4 => "Atendida",
        _ => v.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static bool EsRechazo(int resultado) => resultado == 3;

    /// <summary>Los conceptos del contrato por número, con el texto de <see cref="TextosDeCreditos.Concepto"/>.</summary>
    private static readonly string[] Conceptos =
    [
        "", "Principal", "CurrentInterest", "DefaultInterest", "CurrentInterestInSuspense", "DefaultInterestInSuspense", "Insurance", "CollectionFee",
        "OtherCharge", "Advance", "CreditBalance",
    ];

    public static string Concepto(int v) => TextosDeCreditos.Concepto(v > 0 && v < Conceptos.Length ? Conceptos[v] : v.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static string EstadoDeObligacion(int v) => v switch
    {
        1 => "Pendiente de giro",
        2 => "Vigente",
        3 => "Cancelado",
        4 => "Castigado",
        5 => "Anulado",
        6 => "Novado",
        _ => v.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string Rol(int v) => v == 2 ? "Codeudor" : "Deudor";

    public static string EstadoDeCartera(int v) => v == 2 ? "Activa" : "Sin activar";

    /// <summary>Las formas de resolver un saldo a favor o un anticipo.</summary>
    public static IReadOnlyList<(int Numero, string Valor, string Texto)> FormasDeResolucion { get; } =
    [
        (1, "RefundThroughCollector", "Devolver por el recaudador"),
        (2, "ApplyToOtherObligation", "Aplicar a otra obligación"),
        (3, "ApplyAsExtraordinaryPrincipal", "Aplicar como abono a capital"),
    ];

    public static string FormaDeResolucion(int v) => FormasDeResolucion.FirstOrDefault(f => f.Numero == v).Texto ?? v.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>El tipo de un hecho emitido por el reverso.</summary>
    public static string Hecho(string? v) => v switch
    {
        "PagoReversado" => "Pago reversado (Contabilidad)",
        "PagoReversadoPorCartera" => "Aviso al recaudador",
        _ => v ?? "—",
    };
}
