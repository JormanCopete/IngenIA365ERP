using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Los errores de alertas (feature 012, T39; contracts/api.md §16.1, §16.2). <c>*.NotFound</c> son 404 —una alerta que
/// no alcanza a quien pregunta es el mismo 404 que una inexistente—; los demás, 422 con <c>data</c>. (nuevo)
/// </summary>
public static class ErroresDeAlertas
{
    public const string CodigoAlertaInexistente = "Alerts.Alert.NotFound";
    public const string CodigoYaAtendida = "Alerts.Alert.AlreadyAttended";
    public const string CodigoTipoInexistente = "Alerts.Type.NotFound";
    public const string CodigoPermisoDesconocido = "Alerts.Type.PermissionUnknown";
    public const string CodigoPermisoDeConsulta = "Alerts.Type.ViewPermissionNotAllowed";
    public const string CodigoDestinatariosRequeridos = "Alerts.Type.RecipientsRequired";
    public const string CodigoAplicacionRequerida = "Alerts.Type.InAppRequired";
    public const string CodigoUmbralesInvalidos = "Alerts.Type.ThresholdsInvalid";
    public const string CodigoVigenciaSeCruza = "Alerts.Type.Overlaps";

    public static Error AlertaInexistente() => new(CodigoAlertaInexistente, "La alerta no existe.");

    public static Error YaAtendida(string? atendidaPor, DateTime? atendidaEn) => new ErrorConDatos(CodigoYaAtendida,
        $"La alerta ya la atendió {atendidaPor ?? "otra persona"}.",
        new { attendedBy = atendidaPor, attendedAt = atendidaEn });

    public static Error TipoInexistente(string typeCode) => new(CodigoTipoInexistente,
        $"«{typeCode}» no es un tipo de alerta del catálogo.");

    public static Error PermisoDesconocido(string permiso) => new ErrorConDatos(CodigoPermisoDesconocido,
        $"El permiso «{permiso}» no existe en el catálogo.",
        new { permissionCode = permiso });

    /// <summary>Un permiso de consulta lo reciben todos los roles por el glob <c>*.View</c>: la alerta iría a todos.</summary>
    public static Error PermisoDeConsulta(string permiso) => new ErrorConDatos(CodigoPermisoDeConsulta,
        $"«{permiso}» es un permiso de consulta y lo tienen todos los roles: elegí uno de acción.",
        new { permissionCode = permiso });

    public static Error DestinatariosRequeridos() => new(CodigoDestinatariosRequeridos,
        "Indicá al menos un permiso destinatario.");

    public static Error AplicacionRequerida() => new(CodigoAplicacionRequerida,
        "La notificación en la aplicación siempre va; el correo es opcional.");

    public static Error UmbralesInvalidos(string clave) => new ErrorConDatos(CodigoUmbralesInvalidos,
        $"El umbral «{clave}» no es de este tipo de alerta.",
        new { key = clave });

    public static Error VigenciaSeCruza(DateOnly existente) => new ErrorConDatos(CodigoVigenciaSeCruza,
        $"Ya hay una versión de este tipo que empieza el {existente:yyyy-MM-dd}. Registrá la nueva desde después de esa fecha.",
        new { existingValidFrom = existente });
}
