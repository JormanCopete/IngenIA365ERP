namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de la libranza (feature 013, K2, US10, T0852): estados del convenio y del archivo, novedades, resultados de la pagaduría,
/// motivos y seguimiento del no aplicado y formas de pago, con los nombres de <c>Domain/Enums/Lending</c> que viajan como texto. Ninguno muestra un
/// código técnico (FR-013). (nuevo)
/// </summary>
public static class TextosDeLibranza
{
    public static string EstadoDeConvenio(string? v) => v switch
    {
        "Active" => "Activo",
        "Suspended" => "Suspendido",
        "Closed" => "Cerrado",
        _ => v ?? "—",
    };

    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeArchivo { get; } =
        [("Generated", "Generado"), ("Superseded", "Reemplazado"), ("Sent", "Enviado"), ("Voided", "Anulado")];

    public static string EstadoDeArchivo(string? v) => EstadosDeArchivo.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static string Novedad(string? v) => v switch
    {
        "New" => "Alta",
        "Change" => "Cambio",
        "Cancellation" => "Retiro",
        "Unchanged" => "Sin cambio",
        _ => v ?? "—",
    };

    public static IReadOnlyList<(string Valor, string Texto)> Resultados { get; } =
        [("Deducted", "Descontado"), ("PartiallyDeducted", "Descontado en parte"), ("NotDeducted", "No descontado")];

    public static string Resultado(string? v) => Resultados.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static IReadOnlyList<(string Valor, string Texto)> Motivos { get; } =
    [
        ("DeductionCap", "Supera el tope de descuento"), ("InsufficientNet", "Neto insuficiente"), ("Termination", "Retiro del empleado"),
        ("Leave", "Licencia"), ("Disability", "Incapacidad"), ("DeductionPriority", "Prelación de otros descuentos"), ("Other", "Otro motivo"),
    ];

    public static string Motivo(string? v) => Motivos.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static IReadOnlyList<(string Valor, string Texto)> Origenes { get; } =
        [("Contract", "Nómina propia"), ("Activation", "Puesta en marcha"), ("PaymasterResult", "Respuesta de la pagaduría")];

    public static string Origen(string? v) => Origenes.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    /// <summary>Las reglas del convenio para el no aplicado (lo que se hace solo al llegar la respuesta).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> ReglasDeNoAplicado { get; } =
        [("MovedToCashPayment", "Pasar a pago por caja"), ("SentToCollection", "Enviar a cobranza"), ("IncludedInNextFile", "Incluir en el archivo siguiente")];

    /// <summary>Lo que una persona puede hacer con un no aplicado abierto.</summary>
    public static IReadOnlyList<(string Valor, string Texto)> Resoluciones { get; } =
        [("MovedToCashPayment", "Pasar a pago por caja"), ("SentToCollection", "Enviar a cobranza"), ("Closed", "Cerrar")];

    public static string Seguimiento(string? v) => v switch
    {
        null or "" => "Abierto",
        "DebtorNotified" => "Deudor avisado",
        "IncludedInNextFile" => "Va en el archivo siguiente",
        "MovedToCashPayment" => "Pasa a pago por caja",
        "SentToCollection" => "Enviado a cobranza",
        "Closed" => "Cerrado",
        _ => v,
    };

    public static IReadOnlyList<(string Valor, string Texto)> FormasAlternas { get; } = [("Cashier", "Caja"), ("DirectDebit", "Débito automático")];

    public static string FormaDePago(string? v) => v switch
    {
        "Cashier" => "Caja",
        "DirectDebit" => "Débito automático",
        "PaymasterDeduction" => "Libranza",
        "OwnPayroll" => "Nómina propia",
        _ => v ?? "—",
    };

    public static string Periodicidad(string? v) => v switch
    {
        "Monthly" => "Mensual",
        "Biweekly" => "Quincenal",
        _ => v ?? "—",
    };

    public static string Periodo(int anio, int mes, int subPeriodo) =>
        $"{anio}-{mes:00}" + (subPeriodo > 1 ? $" (quincena {subPeriodo})" : string.Empty);

    public static string Tope(decimal? fraccion) => fraccion is { } f ? f.ToString("P0") : "Sin tope propio";
}
