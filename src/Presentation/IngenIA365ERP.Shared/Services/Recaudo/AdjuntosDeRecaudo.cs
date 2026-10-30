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

    /// <summary>Segunda ronda del 2026-10-27 (D-e): el soporte de la liberación de un falso positivo de lista vinculante (dueño: la operación de caja).</summary>
    public const string SoporteDeLiberacionDeLista = "ReceiptsScreeningReleaseSupport";

    /// <summary>Segunda ronda del 2026-10-27 (D-g): el acta del cuadre de apertura con diferencia (dueño: la configuración de Recaudo o de Tesorería).</summary>
    public const string ActaDelCuadreDeApertura = "OpeningSquareMinutes";

    /// <summary>La constancia de entrega de un cheque girado (dueño: el cheque).</summary>
    public const string ConstanciaDeEntregaDeCheque = "TreasuryDeliveryReceipt";
}
