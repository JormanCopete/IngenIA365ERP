namespace IngenIA365ERP.Domain.Enums.Treasury;

// Feature 015, T1 (fases 33 a 35; data-model §13.1, §20): las enumeraciones de la Tesorería completa. Se guardan como int y un valor nunca se renumera. Todas son
// (nuevo) y quedan en DT §2.7.

/// <summary>
/// El estado de un traslado entre cuentas propias (FR-104): pedido y en aprobación; aprobado (no mueve dinero); enviado (sale de la cuenta de origen y queda en tránsito,
/// <c>TrasladoEntreCuentasEnviado</c>); confirmado por el extracto o con soporte (<c>TrasladoConfirmado</c>); rechazado en la aprobación; anulado antes de enviarse. (nuevo)
/// </summary>
public enum TransferState
{
    PendingApproval = 1,
    Approved = 2,
    Sent = 3,
    Confirmed = 4,
    Rejected = 5,
    Voided = 6,
}

/// <summary>El estado de una orden de pago manual antes de entrar a la bandeja (FR-211): espera la primera y la segunda aprobación, o se rechazó. (nuevo)</summary>
public enum ManualOrderApprovalState
{
    PendingFirstApproval = 1,
    PendingSecondApproval = 2,
    Approved = 3,
    Rejected = 4,
}

/// <summary>El estado de un firmante o un token de una cuenta propia (FR-208): pendiente de aprobar el alta, activo, pendiente de aprobar su retiro, retirado o rechazado. (nuevo)</summary>
public enum BankAccountAuthorityState
{
    PendingApproval = 1,
    Active = 2,
    PendingRetirement = 3,
    Retired = 4,
    Rejected = 5,
}
