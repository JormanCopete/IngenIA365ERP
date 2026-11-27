namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español del desembolso, las órdenes de giro, los archivos, las garantías y el pagaré (feature 013, US3, T0459–T0461), con los
/// nombres de <c>Domain/Enums/Lending</c> que viajan como texto. Ninguno muestra un código técnico (FR-013). (nuevo)
/// </summary>
public static class TextosDeDesembolso
{
    public static IReadOnlyList<(string Valor, string Texto)> Formas { get; } =
        [("Transfer", "Transferencia"), ("Check", "Cheque"), ("ThirdPartyPayment", "Giro a un tercero")];

    public static string Forma(string? v) => v switch
    {
        "Transfer" => "Transferencia",
        "Check" => "Cheque",
        "ThirdPartyPayment" => "Giro a un tercero",
        "InKind" => "En especie",
        // 2026-11-25 (feature 026): el cargo masivo no gira dinero.
        "Charge" => "Cargo masivo (sin giro)",
        _ => v ?? "—",
    };

    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeOrden { get; } =
        [("Issued", "Emitida"), ("Disbursed", "Girada"), ("Rejected", "Rechazada"), ("CheckNotCashed", "Cheque no cobrado"), ("Voided", "Anulada")];

    public static string EstadoDeOrden(string? v) => EstadosDeOrden.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static string TipoDeCuenta(string? v) => v switch { "Savings" => "Ahorros", "Checking" => "Corriente", _ => "—" };

    public static string EstadoDeArchivo(string? v) => v switch { "Generated" => "Generado", "Voided" => "Anulado", _ => v ?? "—" };

    public static string MotivoDePendiente(string? v) => v switch
    {
        "BankCodeMissing" => "El banco no tiene código de transferencia (ACH)",
        "NoBankAccount" => "Sin cuenta destino",
        "AlreadyInFile" => "Ya va en otro archivo vivo",
        null or "" => "—",
        _ => $"Falta el campo «{v}»",
    };

    public static IReadOnlyList<(string Valor, string Texto)> ClasesDeGarantia { get; } =
        [("Pledge", "Prenda"), ("Mortgage", "Hipoteca"), ("GuaranteeFund", "Fondo de garantías"), ("Contributions", "Aportes")];

    public static string ClaseDeGarantia(string? v) => v switch
    {
        "PromissoryNote" => "Pagaré",
        "CoDebtor" => "Codeudor",
        "Pledge" => "Prenda",
        "Mortgage" => "Hipoteca",
        "GuaranteeFund" => "Fondo de garantías",
        "Contributions" => "Aportes",
        _ => v ?? "—",
    };

    public static string EstadoDeGarantia(string? v) => v switch
    {
        "Constituted" => "Constituida",
        "Valid" => "Perfeccionada",
        "Released" => "Liberada",
        "Extinguished" => "Extinguida",
        "Executed" => "Ejecutada",
        _ => v ?? "—",
    };

    /// <summary>Las transiciones que la pantalla ofrece desde cada estado (el servidor las valida igual).</summary>
    public static IReadOnlyList<string> SiguientesEstados(string? estado) => estado switch
    {
        "Constituted" => ["Valid", "Extinguished"],
        "Valid" => ["Extinguished", "Executed"],
        _ => [],
    };

    public static string FormaDePagare(string? v) => v switch { "Physical" => "Físico", "Dematerialized" => "Desmaterializado", _ => v ?? "—" };

    public static string EstadoDePagare(string? v) => v switch
    {
        "Created" => "Creado",
        "PendingSignature" => "Pendiente de firma",
        "Signed" => "Firmado",
        "InCustody" => "En custodia",
        "Endorsed" => "Endosado",
        "Voided" => "Anulado",
        _ => v ?? "—",
    };

    /// <summary>Qué significa cada código que impide desembolsar, en palabras de quien opera.</summary>
    public static string Bloqueo(string codigo) => codigo switch
    {
        "Lending.Segregation.Conflict" => "Quien decidió el crédito no puede desembolsarlo",
        "Lending.Usury.Exceeded" => "La tasa con cargos supera la usura del día del desembolso",
        "Lending.Disbursement.GuaranteesPending" => "Faltan garantías o el pagaré por perfeccionar",
        "Lending.Collateral.CoDebtorsInsufficient" => "Faltan codeudores para este monto y plazo",
        "Lending.Collateral.CoverageInsufficient" => "Las garantías no cubren lo que exige la línea",
        "Lending.Adapter.SimulatedInProduction" => "El pagaré es del depósito simulado: en producción no tiene efecto legal",
        "Compliance.RestrictiveList.Match" => "Coincidencia en listas restrictivas sin liberar",
        "Lending.Debtor.Blocked" => "La persona tiene un bloqueo o embargo vigente",
        "Lending.Concentration.LimitExceeded" => "Supera el límite de concentración",
        "Lending.Concentration.TechnicalCapitalUnavailable" => "No hay patrimonio técnico vigente para medir la concentración",
        "Lending.AmountLimit.Exceeded" => "Supera su monto operativo para desembolsar",
        "Lending.Branch.AccountingBranchMissing" => "La oficina no tiene sucursal contable",
        "Lending.Disbursement.BeneficiaryAccountMissing" => "La transferencia necesita la cuenta del beneficiario",
        "Lending.Payoff.RequiresRestructuring" => "Un crédito a recoger está en mora: requiere reestructuración",
        "Lending.Payoff.NotRequested" => "La solicitud no autorizó recoger ese crédito",
        // 2026-11-27 (feature 028, T042): las recogidas salen de la solicitud y se comparan con lo proyectado.
        "Lending.Payoff.DifferenceAboveTolerance" => "Lo que se recoge cambió más de lo tolerado: devuelva la solicitud para corregirla",
        "Lending.Payoff.MustMatchApplication" => "Las recogidas pedidas no son las que autorizó la solicitud",
        "Lending.Payoff.NotPayable" => "Un producto a recoger no se puede pagar (castigado, en insolvencia o pendiente de giro)",
        "Lending.Disbursement.NetNotPositive" => "Lo recogido no deja neto por girar",
        "Lending.Prevalidation.NotPostable" => "Contabilidad no tiene regla para contabilizar el desembolso",
        "Lending.Security.UserWithoutPerson" => "Su usuario no está asociado a una persona del maestro",
        _ => "Hay un requisito sin cumplir: revise el detalle al confirmar",
    };

    // ----------------------------------------------------------------- 2026-11-27 (feature 028, HU3, T042): lo que recoge el desembolso --

    /// <summary>La causa de una diferencia entre lo proyectado y lo de ahora (<c>Payments</c>, <c>Accrual</c>…), en palabras.</summary>
    public static string CausaDeDiferencia(string? causa) => causa switch
    {
        "Payments" => "Pagos recibidos",
        "Accrual" => "Causación",
        "Arrears" => "Mora",
        "CurrentInstallment" => "Cuota causada",
        "Parameters" => "Parámetros",
        "ProductClosed" => "Ya cancelado",
        "Other" => "Otros movimientos",
        null or "" => "—",
        _ => causa,
    };

    /// <summary>Las causas de una diferencia, separadas por coma; «—» si no hay.</summary>
    public static string Causas(IReadOnlyList<string>? causas) =>
        causas is { Count: > 0 } ? string.Join(", ", causas.Select(CausaDeDiferencia)) : "—";

    /// <summary>La clase del producto que se recoge.</summary>
    public static string ClaseDeProducto(string? clase) => clase switch
    {
        "Loan" => "Crédito",
        "Account" => "Cuenta",
        "CommercialReceivable" => "Factura a crédito",
        _ => clase ?? "—",
    };

    /// <summary>Cómo se recoge: la cancelación o el abono (lo exigible y el resto a capital).</summary>
    public static string ModoDeRecogida(string? modo) => modo switch
    {
        "Total" => "Total",
        "Partial" => "Parcial (abono a capital)",
        "Installments" => "Cuotas",
        "Deposit" => "Abono",
        _ => modo ?? "—",
    };

    /// <summary>Un concepto del detalle de la diferencia (los nombres de <c>breakdown</c>).</summary>
    public static string ConceptoDeDiferencia(string? concepto) => concepto switch
    {
        "principal" => "Capital",
        "currentInterest" => "Interés corriente",
        "interestReturned" => "Interés devuelto por proporcionales",
        "interestCharged" => "Interés cobrado por proporcionales",
        "defaultInterest" => "Interés de mora",
        "insurance" => "Seguro",
        "administration" => "Administración",
        "otherCharges" => "Otros cargos",
        "advance" => "Anticipo a favor",
        "creditBalance" => "Saldo a favor",
        _ => concepto ?? "—",
    };

    /// <summary>
    /// Una diferencia con su signo delante del valor: <c>+$ 25.000</c> (recoger cuesta más que lo proyectado: el neto baja), <c>-$ 25.000</c> (cuesta menos:
    /// el neto sube), <c>$ 0</c> sin diferencia y «—» sin proyección.
    /// </summary>
    public static string Diferencia(decimal? diferencia) => diferencia switch
    {
        null => "—",
        > 0m => "+" + TextosDeOriginacion.Pesos(diferencia.Value),
        < 0m => "-" + TextosDeOriginacion.Pesos(-diferencia.Value),
        _ => TextosDeOriginacion.Pesos(0m),
    };

    /// <summary>
    /// La clase de <c>componentes.css</c> de una diferencia: <c>diferencia-en-contra</c> si recoger cuesta más (el asociado recibe menos),
    /// <c>diferencia-a-favor</c> si cuesta menos; sin clase si no hay diferencia.
    /// </summary>
    public static string ClaseDeDiferencia(decimal? diferencia) => diferencia switch
    {
        > 0m => "diferencia-en-contra",
        < 0m => "diferencia-a-favor",
        _ => string.Empty,
    };

    /// <summary>Qué pasa con un producto en la vista previa: se omite, se bloquea o se recoge.</summary>
    public static string EstadoDeRecogida(RecogidaPropuestaDto recogida)
    {
        ArgumentNullException.ThrowIfNull(recogida);
        if (recogida.Omitted) return "Ya cancelado: no se recoge";
        if (recogida.RequiresRestructuring) return "Requiere reestructuración";
        if (recogida.AboveTolerance) return "Pasa de la tolerancia";
        return recogida.Mode == "Partial" ? "Se abona" : "Se cancela";
    }
}
