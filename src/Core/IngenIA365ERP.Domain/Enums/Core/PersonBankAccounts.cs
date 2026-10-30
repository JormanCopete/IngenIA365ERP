namespace IngenIA365ERP.Domain.Enums.Core;

/// <summary>
/// El estado de una cuenta bancaria de una persona (feature 015, T0028; R-21; FR-210; data-model §16.1, §20). La pide una persona
/// (<see cref="PendingApproval"/>), la aprueba <b>otra</b> (<see cref="Active"/>, y desde <c>PayableFrom</c> se le puede girar) o la rechaza
/// (<see cref="Rejected"/>); un cambio es una fila nueva y la anterior queda <see cref="Retired"/>.
/// </summary>
public enum PersonBankAccountState
{
    PendingApproval = 1,
    Active = 2,
    Rejected = 3,
    Retired = 4,
}

/// <summary>
/// De dónde salió la cuenta (data-model §16.1): <see cref="Registered"/> por la pantalla con doble control, o <see cref="Imported"/> por la
/// copia de los campos sueltos de proveedor de la persona (<c>COR_People.SupplierBank*</c>) que hizo la migración
/// <c>PlataformaParaRecaudoYTesoreria</c>, sin certificación (Q4): la verificación de la cuenta destino la marca.
/// </summary>
public enum PersonBankAccountOrigin
{
    Registered = 1,
    Imported = 2,
}
