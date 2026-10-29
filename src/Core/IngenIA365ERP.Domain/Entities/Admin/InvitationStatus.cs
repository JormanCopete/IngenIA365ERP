namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Estados de una invitación. Terminales: <see cref="Accepted"/>,
/// <see cref="Expired"/>, <see cref="Revoked"/>, <see cref="Superseded"/>.
///
/// <para>
/// <see cref="Superseded"/> se aplica cuando se emite una NUEVA invitación
/// activa para el mismo email+tenant: la anterior queda obsoleta y su
/// token deja de ser válido (T051 — protege contra confusión del
/// destinatario si recibe dos correos seguidos).
/// </para>
/// </summary>
public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Revoked = 3,
    Superseded = 4,

    /// <summary>
    /// La persona agotó los intentos de digitar su documento al activar (feature 017, D-06). No sirve
    /// para activar; el administrador la corrige (queda <see cref="Superseded"/> y sale otra) o la cancela.
    /// </summary>
    UnderReview = 5,
}
