using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Raíz de la jerarquía de credenciales de segundo factor. Mapea a
/// <c>[dbo].[ADM_MfaCredentials]</c>: UNA tabla con discriminador
/// <c>CredentialType</c> (TPH). Es la primera jerarquía mapeada del repositorio.
///
/// <para>
/// <b>Por qué existe.</b> Hasta ahora el segundo factor era una columna,
/// <c>ADM_CentralUsers.MfaSecret</c>: una persona, un autenticador, un método.
/// Con esto caben varias credenciales por persona y de tipos distintos sin volver
/// a tocar la tabla de usuarios cada vez.
/// </para>
///
/// <para>
/// <b>Sin navigation property hacia el usuario</b>, igual que
/// <see cref="PasswordResetToken"/>. EF mapea el bridge <c>CentralUserIdentity</c>,
/// que vive en Infrastructure, no el POCO <see cref="CentralUser"/> de Domain; y
/// las pruebas de arquitectura prohíben que Domain lo referencie. La FK es lógica.
/// </para>
///
/// <para>
/// <b>Revocar no es <c>Remove()</c>.</b> <c>AdminDbContext</c> no enchufa los
/// <c>ISaveChangesInterceptor</c> —sólo lo hace <c>ApplicationDbContext</c>—, así
/// que aquí no hay soft-delete automático ni relleno automático de auditoría. Un
/// <c>Remove()</c> sobre esta tabla sería un DELETE físico del segundo factor de
/// alguien. Se usa <see cref="Revocar"/>, y las factories ponen la auditoría a
/// mano: si no, las filas nacen con <c>CreatedAt</c> en el año 1.
/// </para>
/// </summary>
public abstract class MfaCredential : AuditableEntity
{
    /// <summary>
    /// FK lógica a <c>ADM_CentralUsers.Id</c>, que es <c>Guid</c> porque el bridge
    /// deriva de <c>IdentityUser&lt;Guid&gt;</c>.
    /// </summary>
    public Guid CentralUserId { get; protected set; }

    /// <summary>
    /// Nombre que la persona le da al dispositivo («iPhone de Ana»). Queda NULL en
    /// las credenciales trasladadas: antes no había dónde escribirlo, y ponerle
    /// uno sería inventar un dato.
    /// </summary>
    [MaxLength(100)]
    public string? Label { get; protected set; }

    /// <summary>
    /// Cuándo se confirmó la inscripción. NULL en las trasladadas, por lo mismo:
    /// esa fecha no existía en ninguna parte. Un hueco es honesto; una fecha
    /// inventada con aspecto de real, no.
    /// </summary>
    public DateTime? ConfirmedAt { get; protected set; }

    // EF Core
    protected MfaCredential() { }

    protected static void ExigirPersona(Guid centralUserId)
    {
        if (centralUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "CentralUserId es obligatorio: una credencial sin dueño no la puede usar nadie.",
                nameof(centralUserId));
        }
    }

    /// <summary>
    /// Sella la auditoría de alta. Hay que llamarlo desde las factories porque
    /// este contexto no tiene interceptores que lo hagan solo.
    /// </summary>
    protected void SellarAlta(DateTime utcNow, string? creadaPor)
    {
        CreatedAt = utcNow;
        CreatedBy = creadaPor;
    }

    /// <summary>
    /// Baja lógica. Idempotente. Es lo único que se debe usar: un <c>Remove()</c>
    /// sobre este contexto borra la fila de verdad.
    /// </summary>
    public void Revocar(DateTime utcNow, string? revocadaPor)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedAt = utcNow;
        DeletedBy = revocadaPor;
    }

    public void Renombrar(string? label, DateTime utcNow, string? modificadaPor)
    {
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        UpdatedAt = utcNow;
        UpdatedBy = modificadaPor;
    }
}
