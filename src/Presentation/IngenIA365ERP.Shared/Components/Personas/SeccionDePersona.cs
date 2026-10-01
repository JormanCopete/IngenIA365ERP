namespace IngenIA365ERP.Shared.Components.Personas;

/// <summary>Las secciones del formulario de persona que <c>PersonaCampos</c> sabe pintar.</summary>
public enum SeccionDePersona
{
    Identificacion,
    Contacto,
    Demografia,
    Roles,

    /// <summary>Feature 012 (T174): el perfil tributario de la persona.</summary>
    DatosTributarios,
}
