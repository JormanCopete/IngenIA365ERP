namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// Los nombres de las tablas que los niveles del cerrojo bloquean por <c>Id</c> (feature 015, T0092; data-model §0.5); son los de
/// <c>SqlDelCerrojoDeRecaudo.TablasDelNivel</c>, que valida cada pedido. (nuevo)
/// </summary>
public static class TablasDeRecaudo
{
    public const string DiasDelPunto = "RCD_CollectionPointDays";
    public const string Sesiones = "RCD_CashSessions";
    public const string Bovedas = "RCD_Vaults";

    // Feature 015, fases 11 a 15: los orígenes del nivel 6.
    public const string SaldosAFavor = "RCD_PayerCreditBalances";
    public const string Partidas = "RCD_UnidentifiedItems";
    public const string LineasDeDescuento = "RCD_PayrollDeductionFileLines";
    public const string LineasDeGiro = "RCD_PaymasterRemittanceLines";
    public const string Cheques = "RCD_ReceivedChecks";
    public const string Recibos = "RCD_Receipts";
}
