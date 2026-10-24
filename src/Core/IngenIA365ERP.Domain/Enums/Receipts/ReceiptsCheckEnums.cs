namespace IngenIA365ERP.Domain.Enums.Receipts;

// Feature 015, US15 (T0216; data-model §4.5): el estado de un cheque recibido. Se guarda como int y un valor nunca se renumera. (nuevo; DT §2.7)
public enum ReceivedCheckState
{
    Received = 1,
    InCustody = 2,
    Deposited = 3,
    InClearing = 4,
    Paid = 5,
    Returned = 6,
}
