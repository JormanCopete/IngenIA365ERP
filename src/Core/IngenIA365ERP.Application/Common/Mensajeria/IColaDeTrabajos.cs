namespace IngenIA365ERP.Application.Common.Mensajeria;

/// <summary>
/// El puerto de la mensajería (feature 023; research R5; contracts/mensajeria.md §3). Dos implementaciones en
/// <c>Infrastructure/IngenIA365ERP.Messaging</c>: RabbitMQ (colas quorum, confirmación del broker) y en proceso (un canal por réplica,
/// para desarrollo, pruebas y el modo sin broker). Application nunca conoce la librería del broker
/// (<c>SoloElAdaptadorDeMensajeriaConoceAlBroker</c>).
/// </summary>
public interface IColaDeTrabajos
{
    /// <summary>¿Hay a dónde publicar ahora? Falso con el broker caído: el barrido hace el trabajo él mismo.</summary>
    bool Disponible { get; }

    /// <summary>
    /// Cuánto puede quedar un aviso confirmado sin atenderse antes de que el barrido lo vuelva a publicar (<c>Mensajeria:RepublicarTrasSegundos</c>).
    /// </summary>
    TimeSpan RepublicarTras { get; }

    /// <summary>
    /// Publica un aviso persistente y espera la confirmación. Verdadero sólo si quedó confirmado; falso ante nack, conexión caída o tiempo
    /// agotado. <b>Nunca lanza</b> por el broker: quien publica deja la orden en la base y el barrido la toma.
    /// </summary>
    Task<bool> PublicarAsync(Aviso aviso, CancellationToken ct);
}

/// <summary>
/// Quien atiende un tipo de aviso (feature 023; contracts/mensajeria.md §4). Corre ya dentro de la cooperativa del aviso
/// (<see cref="ConsumoDeAvisos"/> lo pone en <c>IEjecutorEnCooperativa</c>) con los servicios de un ámbito nuevo. Debe ser idempotente:
/// relee la base y no hace nada si la fila ya no es elegible. Si lanza, el aviso vuelve a la cola.
/// </summary>
public interface IManejadorDeAviso
{
    /// <summary>Uno de <see cref="TiposDeAviso"/>.</summary>
    string Tipo { get; }

    /// <summary>
    /// Verdadero (el defecto): <see cref="ConsumoDeAvisos"/> lo corre dentro de <c>IEjecutorEnCooperativa</c>. Falso: el manejador recorre la
    /// cooperativa él mismo (la pasada del despachador, que abre un ámbito por unidad) y recibe un ámbito sin cooperativa.
    /// </summary>
    bool PorCooperativa => true;

    Task ManejarAsync(Aviso aviso, IServiceProvider servicios, CancellationToken ct);
}

/// <summary>
/// Lo que la API mide de los trabajos de fondo (feature 023; research R8). Application avisa; la API lo expone en Prometheus. Una
/// implementación nula sirve en pruebas y en el DbMigrator.
/// </summary>
public interface IMedidorDeTrabajos
{
    /// <summary>Pendientes, edad del más viejo y muertos de una cola en una cooperativa, según la base (lo llama el barrido).</summary>
    void Estado(string cola, Guid cooperativa, string nombreCooperativa, int pendientes, double edadDelMasViejoSegundos, int muertos);

    /// <summary>Una publicación terminó (<c>confirmada</c>, <c>rechazada</c>, <c>sin-broker</c>).</summary>
    void Publicacion(string cola, string resultado);

    /// <summary>Un consumo terminó (<c>ok</c>, <c>reencolado</c>, <c>ilegible</c>, <c>cooperativa-inactiva</c>).</summary>
    void Consumo(string cola, string resultado);

    /// <summary>Cuánto esperó un cobro del POS a la validación.</summary>
    void EsperaDelCobro(TimeSpan espera, bool validado);
}

/// <summary>El medidor que no mide (pruebas, DbMigrator, o la API con métricas apagadas). (nuevo)</summary>
public sealed class MedidorNulo : IMedidorDeTrabajos
{
    public static readonly MedidorNulo Instancia = new();

    public void Estado(string cola, Guid cooperativa, string nombreCooperativa, int pendientes, double edadDelMasViejoSegundos, int muertos) { }

    public void Publicacion(string cola, string resultado) { }

    public void Consumo(string cola, string resultado) { }

    public void EsperaDelCobro(TimeSpan espera, bool validado) { }
}

/// <summary>
/// La cola que no publica (feature 023): lo que queda registrado si nadie llama a <c>AddMessaging</c> —el DbMigrator, pruebas que no la
/// necesitan—. <see cref="Disponible"/> es falso, así que el barrido hace el trabajo él mismo, como antes de la 023. (nuevo)
/// </summary>
public sealed class ColaNula : IColaDeTrabajos
{
    public bool Disponible => false;

    public TimeSpan RepublicarTras => TimeSpan.Zero;

    public Task<bool> PublicarAsync(Aviso aviso, CancellationToken ct) => Task.FromResult(false);
}
