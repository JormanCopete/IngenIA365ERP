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
        "Lending.Disbursement.NetNotPositive" => "Lo recogido no deja neto por girar",
        "Lending.Prevalidation.NotPostable" => "Contabilidad no tiene regla para contabilizar el desembolso",
        "Lending.Security.UserWithoutPerson" => "Su usuario no está asociado a una persona del maestro",
        _ => "Hay un requisito sin cumplir: revise el detalle al confirmar",
    };
}
