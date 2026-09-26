using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// Los errores de aprobaciones y montos máximos (feature 012, T33, T34; contracts/api.md §15.4, §1.3). Los
/// <c>*.NotFound</c> son 404 —también sin el permiso del nivel o fuera del alcance, con el mismo mensaje—; los demás,
/// 422 con <c>data</c> para que la pantalla diga qué hacer. Las fallas de la aprobación presencial son 422 y
/// <b>nunca 401</b>: un 401 haría que <c>RenovacionDeSesionHandler</c> renovara la sesión del cajero. (nuevo)
/// </summary>
public static class ErroresDeAprobaciones
{
    public const string CodigoSolicitudInexistente = "Approvals.Request.NotFound";
    public const string CodigoSolicitudNoPendiente = "Approvals.Request.NotPending";
    public const string CodigoContenidoCambiado = "Approvals.Request.ContentChanged";
    public const string CodigoAutoaprobacion = "Approvals.SelfApprovalForbidden";
    public const string CodigoPoliticaInexistente = "Approvals.Policy.NotFound";
    public const string CodigoPoliticaSeCruza = "Approvals.Policy.Overlaps";
    public const string CodigoPoliticaEnPeriodoCerrado = "Approvals.Policy.ValidFromInClosedPeriod";
    public const string CodigoNivelesInvalidos = "Approvals.Policy.LevelsInvalid";
    public const string CodigoPermisoDesconocido = "Approvals.Policy.PermissionUnknown";
    public const string CodigoPoliticaRequerida = "Approvals.Policy.RequiredForClass";
    public const string CodigoPresenciaNoSolicitante = "Approvals.Presence.NotRequester";
    public const string CodigoPresenciaInvalida = "Approvals.Presence.Invalid";
    public const string CodigoPresenciaVencida = "Approvals.Presence.Expired";
    public const string CodigoTotpReusado = "Approvals.Presence.TotpReused";
    public const string CodigoNoLimitable = "Approvals.AmountLimit.NotLimitable";
    public const string CodigoRolSinPermiso = "Approvals.AmountLimit.RoleLacksPermission";
    public const string CodigoLimiteSeCruza = "Approvals.AmountLimit.Overlaps";
    public const string CodigoExcedeLimite = "Inventory.Approval.AmountExceedsLimit";

    /// <summary>Inexistente, sin el permiso del nivel o fuera del alcance: el mismo 404 (contracts/api.md §2.2).</summary>
    public static Error SolicitudInexistente() => new(CodigoSolicitudInexistente, "La solicitud de aprobación no existe.");

    public static Error SolicitudNoPendiente(ApprovalRequestStatus estado) => new ErrorConDatos(CodigoSolicitudNoPendiente,
        "La solicitud ya no está pendiente: otra persona la decidió o se retiró.",
        new { status = estado.ToString() });

    public static Error ContenidoCambiado(string huellaActual) => new ErrorConDatos(CodigoContenidoCambiado,
        "Lo que se va a aprobar cambió desde que lo viste. Volvé a abrir la solicitud.",
        new { currentSha256 = huellaActual });

    public static Error Autoaprobacion(MotivoDeExclusion motivo) => new ErrorConDatos(CodigoAutoaprobacion,
        motivo switch
        {
            MotivoDeExclusion.Creator => "Creaste el documento: no podés aprobarlo. Lo decide otra persona con el permiso del nivel.",
            MotivoDeExclusion.Requester => "Pediste esta aprobación: no podés decidirla. Lo decide otra persona con el permiso del nivel.",
            MotivoDeExclusion.Participant => "Participaste en el documento: no podés aprobarlo. Lo decide otra persona con el permiso del nivel.",
            _ => "Ya aprobaste otro nivel de esta solicitud: el siguiente lo decide otra persona.",
        },
        new { reason = motivo.ToString() });

    public static Error PoliticaSeCruza(DateOnly existente) => new ErrorConDatos(CodigoPoliticaSeCruza,
        $"Ya hay una versión de esta política que empieza el {existente:yyyy-MM-dd}. Registrá la nueva desde después de esa fecha.",
        new { existingValidFrom = existente });

    /// <summary>§15.1 (US3, T286): la versión no puede empezar en un período de inventario cerrado.</summary>
    public static Error PoliticaEnPeriodoCerrado(DateOnly ultimoCierre) => new ErrorConDatos(CodigoPoliticaEnPeriodoCerrado,
        $"La política no puede empezar en un período de inventario cerrado (el último cierre es del {ultimoCierre:yyyy-MM-dd}): use una fecha posterior.",
        new { lastClosedDate = ultimoCierre });

    /// <summary>§15.1 (US3, T286): los tipos que siempre se aprueban (saldo inicial, ajuste de conteo) no admiten una política vacía.</summary>
    public static Error PoliticaRequerida(string clase) => new ErrorConDatos(CodigoPoliticaRequerida,
        "Este tipo de documento siempre se aprueba: su política necesita al menos un nivel.",
        new { @class = clase });

    public static Error NivelesInvalidos(string motivo) => new ErrorConDatos(CodigoNivelesInvalidos,
        $"Los niveles de la política no son válidos. {motivo}",
        new { reason = motivo });

    public static Error PermisoDesconocido(string permiso) => new ErrorConDatos(CodigoPermisoDesconocido,
        $"El permiso «{permiso}» no existe en el catálogo.",
        new { permissionCode = permiso });

    public static Error NoLimitable(string permiso) => new ErrorConDatos(CodigoNoLimitable,
        $"El permiso «{permiso}» no admite monto máximo: sólo los de acciones con valor.",
        new { limitable = PermisosLimitables.Todos });

    public static Error RolSinPermiso(string rol, string permiso) => new ErrorConDatos(CodigoRolSinPermiso,
        $"El rol «{rol}» no concede «{permiso}»: primero asignale el permiso.",
        new { permissionCode = permiso });

    public static Error LimiteSeCruza(DateOnly existente) => new ErrorConDatos(CodigoLimiteSeCruza,
        $"Ya hay un monto máximo de este rol y permiso que empieza el {existente:yyyy-MM-dd}. Registrá el nuevo desde después de esa fecha.",
        new { existingValidFrom = existente });

    public static Error ExcedeLimite(decimal monto, decimal maximo, string moneda, string permiso) => new ErrorConDatos(CodigoExcedeLimite,
        $"El monto supera tu máximo de {maximo:N2} {moneda} y el tipo de documento no tiene política de aprobación. Pedíselo a quien tenga un monto mayor.",
        new { amount = monto, maxAmount = maximo, currency = moneda, permissionCode = permiso });

    public static Error PresenciaNoSolicitante() => new(CodigoPresenciaNoSolicitante,
        "La aprobación presencial se hace en el equipo de quien pidió la aprobación.");

    public static Error PresenciaInvalida() => new(CodigoPresenciaInvalida,
        "No se pudo verificar al aprobador. Volvé a intentarlo con su llave o su código.");

    public static Error PresenciaVencida() => new(CodigoPresenciaVencida,
        "El desafío venció (dura dos minutos). Pedí uno nuevo.");

    public static Error TotpReusado() => new(CodigoTotpReusado,
        "Ese código ya se usó. Esperá el siguiente de la aplicación del aprobador.");

    public static Error MotivoRequerido() => new(Error.Validation.Code, "Indicá el motivo del rechazo.");
}

/// <summary>
/// Los permisos que admiten monto máximo (FR-009; contracts/api.md §1.3): los de acciones con valor. (nuevo)
/// </summary>
public static class PermisosLimitables
{
    public static readonly IReadOnlyList<string> Todos =
    [
        "Inventory.Purchases.Confirm",
        "Inventory.Adjustments.Confirm",
        "Inventory.Sales.Confirm",
        "Inventory.Sales.SellOnCredit",
    ];

    public static bool Admite(string permiso) => Todos.Contains(permiso, StringComparer.Ordinal);
}
