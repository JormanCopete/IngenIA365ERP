namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>
/// Feature 015, R2 (fase 38; pedido del dueño): los tipos de adjunto propios de Recaudo que suben las pantallas, con el mismo nombre que en el servidor
/// (<c>AdjuntosDeModulo</c>): la firma de quien recibe un egreso (dueño: la sesión), el soporte de una consignación reportada (dueño: la persona) y el comprobante del
/// cargue del reporte de transacciones a la UIAF (dueño: la generación). (nuevo)
/// </summary>
public static class AdjuntosDeRecaudo
{
    public const string FirmaDeEgreso = "ReceiptsDisbursementSignature";
    public const string SoporteDeConsignacionReportada = "ReceiptsReportedDepositSupport";
    public const string ComprobanteDeCargueUiaf = "ReceiptsCashTransactionReportReceipt";
}
