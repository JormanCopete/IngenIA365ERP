using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;

/// <summary>
/// <c>TES_UserBankAccountScopes</c> (feature 015, T0057; US12; R-30; FR-011, FR-106, FR-175; data-model §9.3): una cuenta bancaria propia que opera el
/// usuario en Tesorería, y con ella los canales sin caja de Recaudo (recibos de banco, partidas, saldos a favor de esa cuenta). Falla cerrado: sin
/// filas no hay cuentas. Lo retirado se da de baja lógica; una fila viva por usuario y cuenta. La clave foránea a <c>TES_BankAccounts</c> nace con
/// la cuenta propia (US10, fase 7): hasta entonces el número es el <c>Id</c> que tendrá. (nuevo)
/// </summary>
public class UserBankAccountScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    /// <summary><c>TES_BankAccounts.Id</c>.</summary>
    public int BankAccountId { get; set; }
}
