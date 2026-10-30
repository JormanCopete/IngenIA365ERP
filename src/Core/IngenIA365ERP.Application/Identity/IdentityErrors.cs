using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Identity;

/// <summary>
/// Errores de la identidad anclada al documento (feature 017, contracts/api.md). Ninguno revela si
/// una persona pertenece a otra cooperativa ni a quién pertenece un correo o un documento.
/// </summary>
public static class IdentityErrors
{
    public static Error DocumentoNoCoincide() =>
        new("Identity.Document.Mismatch", "Los dos números de documento no coinciden.");

    public static Error DocumentoInvalido(string motivo) =>
        new("Identity.Document.Invalid", motivo);

    public static Error TipoNoAceptado(string tipo) =>
        new("Identity.Document.TypeNotAccepted", $"La cooperativa no acepta el tipo de documento {tipo}.");

    public static Error DocumentoNoDisponible() =>
        new("Identity.Document.Unavailable",
            "No se pudo registrar ese documento. Comuníquese con el administrador de su cooperativa.");

    public static Error CorreoYaRegistrado() =>
        new("Invitation.EmailTaken", "Correo ya registrado.");

    public static Error YaEsMiembro() =>
        new("Invitation.AlreadyMember", "La persona ya es miembro activo de esta cooperativa.");

    public static Error RolNoEncontrado() =>
        new("Invitation.RoleNotFound", "El rol elegido no existe o no se puede asignar en esta cooperativa.");

    public static Error CargoNoEncontrado() =>
        new("Invitation.PositionNotFound", "El cargo elegido no existe en esta cooperativa.");

    public static Error EnRevision() =>
        new("Invitation.UnderReview",
            "Esta invitación está en revisión. El administrador de la cooperativa debe corregirla o enviar una nueva.");
}
