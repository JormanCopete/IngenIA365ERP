namespace IngenIA365ERP.Application.Common.Execution;

/// <summary>
/// Arrendamientos de trabajos de fondo por cooperativa en <c>COR_BackgroundLeases</c> (feature 012,
/// T10, T47). Sirven para que dos réplicas no hagan a la vez el mismo trabajo sobre la misma
/// cooperativa; <b>la exactitud nunca depende de ellos</b> (la dan el <c>RowVersion</c> de cada
/// entrega, el recibo único del destino y la cabeza de cadena). Se piden dentro de
/// <see cref="IEjecutorEnCooperativa"/>, así que operan sobre la base de la cooperativa en curso.
///
/// <para>
/// Nombres fijos: <c>integration.dispatch</c>, <c>audit.forward</c>, <c>einvoicing.process</c>,
/// <c>scheduled.tasks</c>, <c>email.dispatch</c>. La implementación es de T048
/// (<c>Persistence/Services/ArrendamientosEnBase</c>).
/// </para>
/// </summary>
public interface IArrendamientos
{
    /// <summary>Toma el arrendamiento si está libre, vencido o ya es de este dueño; dice si lo tomó.</summary>
    Task<bool> ArrendarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default);

    /// <summary>Extiende el arrendamiento propio entre tandas; falso si ya no es de este dueño.</summary>
    Task<bool> RenovarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default);

    /// <summary>Lo libera al terminar, si todavía es de este dueño.</summary>
    Task SoltarAsync(string nombre, CancellationToken ct = default);
}
