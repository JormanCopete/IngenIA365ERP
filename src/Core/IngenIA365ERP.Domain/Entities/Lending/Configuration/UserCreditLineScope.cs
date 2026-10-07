using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_UserCreditLineScopes</c> (feature 013, T0210; data-model §3.14; FR-004, SC-015, T62): las líneas que opera un usuario.
/// Falla cerrado: sin filas no hay líneas, salvo <c>Lending.Scope.AllLines</c>. Lo retirado se da de baja lógica.
/// </summary>
public class UserCreditLineScope : AuditableEntity
{
    public int UserId { get; set; }

    public int CreditLineId { get; set; }

    public bool IsDefault { get; set; }
}
