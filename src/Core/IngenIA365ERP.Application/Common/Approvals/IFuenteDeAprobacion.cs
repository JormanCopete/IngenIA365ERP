using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Approvals;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// El gancho de lo que se aprueba (feature 012, T33, T083; nuevo): uno por <c>SourceType</c>
/// (<c>ApprovalSourceTypes</c>), que implementa cada historia —el documento de inventario en la fase 3, el descuento y
/// el crédito provisional en US5/US6, la diferencia de traslado en US2—. El motor no conoce los módulos: les pide
/// confirmar, devolver a borrador, decir si están al alcance y describirse para la bandeja.
/// </summary>
public interface IFuenteDeAprobacion
{
    /// <summary>La <c>SourceType</c> que atiende.</summary>
    string SourceType { get; }

    /// <summary>
    /// La última aprobación: confirma lo aprobado en la transacción del aprobador, repitiendo la confirmación (existencia,
    /// período, validación previa, numeración). Si falla, la decisión no queda y la respuesta es este error.
    /// </summary>
    Task<Result<EstadoDeFuenteDto>> AlAprobarAsync(ApprovalRequest solicitud, CancellationToken ct);

    /// <summary>Rechazo o retiro: devuelve lo aprobado a borrador para corregirlo.</summary>
    Task<Result<EstadoDeFuenteDto>> AlDevolverAsync(ApprovalRequest solicitud, string motivo, CancellationToken ct);

    /// <summary>
    /// Si lo aprobado está al alcance (bodegas o punto) de quien decide o consulta. El motor sólo lo pregunta cuando la
    /// solicitud tiene bodega o punto y el alcance no es total: la fuente conoce el Id interno de sus bodegas.
    /// </summary>
    Task<bool> EnAlcanceAsync(ApprovalRequest solicitud, AlcanceDeInventario alcance, CancellationToken ct);

    /// <summary>Lo que la bandeja muestra de cada fuente (clase, tipo, número, bodega, resumen, pantalla).</summary>
    Task<IReadOnlyDictionary<Guid, OrigenDeAprobacionDto>> DescribirAsync(IReadOnlyCollection<Guid> sourcePublicIds, CancellationToken ct);
}

/// <summary>
/// Aviso a los titulares del nivel que queda pendiente (alerta <c>Aprobaciones.Pendiente</c>; nuevo). La sección de
/// alertas (T093) lo implementa con <c>IAlertas</c>; hasta entonces la API registra <see cref="SinAvisosDeAprobacion"/>.
/// </summary>
public interface IAvisosDeAprobacion
{
    Task PendienteAsync(ApprovalRequest solicitud, string permisoDelNivel, CancellationToken ct);
}

/// <summary>El aviso vacío mientras no existan las alertas (T093). (nuevo)</summary>
public sealed class SinAvisosDeAprobacion : IAvisosDeAprobacion
{
    public Task PendienteAsync(ApprovalRequest solicitud, string permisoDelNivel, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Permiso y alcance de un aprobador que <b>no</b> es quien tiene la sesión: el supervisor presente en la caja,
/// identificado por su passkey o su TOTP (contracts/api.md §15.2). Cumple las mismas reglas que desde su sesión. La
/// implementación vive en la API. (nuevo)
/// </summary>
public interface IAutoridadDeOtroAprobador
{
    Task<bool> TienePermisoAsync(int userId, string permiso, CancellationToken ct);

    Task<AlcanceDeInventario> AlcanceAsync(int userId, CancellationToken ct);
}

/// <summary>
/// Las reglas del módulo sobre una versión de política (nuevo): el tipo de documento existe y se describe, los tipos
/// que siempre se aprueban no admiten una política vacía (<c>Approvals.Policy.RequiredForClass</c>) y
/// <c>validFrom</c> no cae en un período de inventario cerrado (<c>Approvals.Policy.ValidFromInClosedPeriod</c>).
/// Inventario las implementa cuando existan sus tipos y períodos (fases 3 a 6); hasta entonces rige
/// <see cref="ReglasDePoliticaDeAprobacionVacias"/>.
/// </summary>
public interface IReglasDePoliticaDeAprobacion
{
    Task<Result> EvaluarAsync(AltaDePoliticaDeAprobacion alta, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, TipoDeDocumentoDeAprobacionDto>> DescribirTiposAsync(IReadOnlyCollection<Guid> tipos, CancellationToken ct);
}

/// <summary>Lo que se va a registrar, para las reglas del módulo. (nuevo)</summary>
public sealed record AltaDePoliticaDeAprobacion(string Subject, Guid? DocumentTypePublicId, DateOnly ValidFrom, int CantidadDeNiveles);

/// <summary>
/// Sin módulo que conozca los tipos de documento, falla cerrado: una política de un tipo concreto no se puede
/// registrar (404, como un tipo inexistente); la de todos los tipos sí. (nuevo)
/// </summary>
public sealed class ReglasDePoliticaDeAprobacionVacias : IReglasDePoliticaDeAprobacion
{
    public Task<Result> EvaluarAsync(AltaDePoliticaDeAprobacion alta, CancellationToken ct) =>
        Task.FromResult(alta.DocumentTypePublicId is null ? Result.Success() : Result.Failure(Error.NotFound));

    public Task<IReadOnlyDictionary<Guid, TipoDeDocumentoDeAprobacionDto>> DescribirTiposAsync(IReadOnlyCollection<Guid> tipos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, TipoDeDocumentoDeAprobacionDto>>(new Dictionary<Guid, TipoDeDocumentoDeAprobacionDto>());
}
