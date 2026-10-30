using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

// Feature 015, T0057 (US12; R-30; FR-011, FR-175; data-model §1.11): el alcance de Recaudo de un usuario, por punto, por caja y por pagaduría.
// Las oficinas no viven aquí: salen de Seguridad (SEC_UserBranchAssignments por OficinasDelUsuario). Falla cerrado: sin filas no hay puntos,
// cajas ni pagadurías, salvo Receipts.Scope.All. Lo retirado se da de baja lógica; una fila viva por usuario y objeto. Molde: UserCreditLineScope.

/// <summary><c>RCD_UserCollectionPointScopes</c>: un punto de recaudo que opera el usuario (y con él, todas sus cajas). (nuevo)</summary>
public class UserCollectionPointScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    /// <summary><c>RCD_CollectionPoints.Id</c>.</summary>
    public int CollectionPointId { get; set; }
}

/// <summary><c>RCD_UserCashBoxScopes</c>: una caja que opera el usuario sin tener todo su punto. (nuevo)</summary>
public class UserCashBoxScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    /// <summary><c>RCD_CashBoxes.Id</c>.</summary>
    public int CashBoxId { get; set; }
}

/// <summary>
/// <c>RCD_UserPaymasterScopes</c>: una pagaduría (la persona jurídica del maestro) cuyos archivos, respuestas, giros, saldos a favor y recibos ve
/// el usuario (FR-175). (nuevo)
/// </summary>
public class UserPaymasterScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    /// <summary><c>COR_People.Id</c> de la pagaduría.</summary>
    public int PaymasterPersonId { get; set; }
}
