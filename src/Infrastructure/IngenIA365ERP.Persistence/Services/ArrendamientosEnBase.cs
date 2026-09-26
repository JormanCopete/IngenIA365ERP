using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Services;

/// <summary>
/// <see cref="IArrendamientos"/> sobre <c>COR_BackgroundLeases</c>, en la base de la cooperativa del
/// ámbito (feature 012, T048; decisiones-transversales T10, T47). Se usa dentro de
/// <see cref="IEjecutorEnCooperativa"/>, cuyo ámbito ya trae el <see cref="ApplicationDbContext"/> de esa
/// cooperativa.
///
/// <para>
/// Tomar es <b>un solo</b> <c>UPDATE … SET Owner = @yo, LeaseUntil = @hasta WHERE Name = @n AND
/// (LeaseUntil &lt; @ahora OR Owner = @yo)</c> (<c>ExecuteUpdateAsync</c>, portable entre PostgreSQL y SQL
/// Server): si dos réplicas lo piden a la vez, la segunda espera el bloqueo de la fila, vuelve a evaluar
/// el <c>WHERE</c> sobre la versión nueva y no la toca; el número de filas dice quién ganó. No hay lectura
/// previa ni ventana entre leer y escribir.
/// </para>
///
/// <para>
/// <b>La exactitud nunca depende de esto.</b> Un arrendamiento puede vencer con el trabajo a medias (una
/// pausa larga del proceso, un reloj corrido) y entonces dos réplicas corren a la vez. Lo que impide un
/// efecto doble está en otro lado: el <c>RowVersion</c> de cada entrega, el recibo único del destino, la
/// cabeza de cadena de auditoría con <c>RowVersion</c> y el arrendamiento por fila del documento
/// electrónico. Esto sólo evita el trabajo repetido en el caso normal —p. ej. dos correos iguales—.
/// </para>
///
/// <para>
/// El dueño es la réplica: máquina + proceso + un Guid por arranque (<see cref="DuenoDelProceso"/>), así
/// que dos ámbitos del mismo proceso comparten el arrendamiento y lo pueden renovar entre tandas, y un
/// proceso reiniciado en la misma máquina no hereda el de su antecesor. Los instantes son UTC de la
/// aplicación: el TTL (por defecto <see cref="DuracionPorDefecto"/>) cubre de sobra la deriva entre réplicas.
/// </para>
/// </summary>
public sealed class ArrendamientosEnBase : IArrendamientos
{
    /// <summary>TTL por defecto (T10): dos minutos, renovado entre tandas.</summary>
    public static readonly TimeSpan DuracionPorDefecto = TimeSpan.FromSeconds(120);

    /// <summary>Identidad de esta réplica: máquina, proceso y un Guid por arranque; cabe en <c>Owner</c> (150).</summary>
    public static string DuenoDelProceso { get; } = ArmarDueno();

    private readonly ApplicationDbContext _db;
    private readonly IDateTimeService _reloj;
    private readonly ILogger<ArrendamientosEnBase> _logger;
    private readonly string _dueno;

    public ArrendamientosEnBase(ApplicationDbContext db, IDateTimeService reloj, ILogger<ArrendamientosEnBase> logger)
        : this(db, reloj, logger, DuenoDelProceso)
    {
    }

    /// <summary>Con un dueño explícito: lo usan las pruebas para simular dos réplicas en un mismo proceso.</summary>
    public ArrendamientosEnBase(ApplicationDbContext db, IDateTimeService reloj, ILogger<ArrendamientosEnBase> logger, string dueno)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dueno);
        if (dueno.Length > 150) throw new ArgumentException("El dueño de un arrendamiento cabe en 150 caracteres.", nameof(dueno));
        _db = db;
        _reloj = reloj;
        _logger = logger;
        _dueno = dueno;
    }

    public async Task<bool> ArrendarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default)
    {
        var ahora = _reloj.UtcNow;
        var hasta = ahora + Duracion(duracion);
        var dueno = _dueno;

        var tomadas = await _db.BackgroundLeases
            .Where(l => l.Name == nombre && (l.LeaseUntil < ahora || l.Owner == dueno))
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Owner, dueno)
                .SetProperty(l => l.LeaseUntil, hasta), ct);

        if (tomadas == 0 && !await _db.BackgroundLeases.AnyAsync(l => l.Name == nombre, ct))
        {
            // Sin fila no hay a quién ganarle ni con quién coordinar: el trabajo no corre, y eso tiene
            // que verse. La fila la siembra la migración; faltar es un defecto de despliegue, no una
            // carrera perdida.
            _logger.LogError(
                "[Arrendamiento.SinFila] No existe la fila «{Nombre}» en COR_BackgroundLeases de esta cooperativa; " +
                "el trabajo no corre hasta que la siembre la migración PlataformaParaInventario.", nombre);
        }

        return tomadas == 1;
    }

    public async Task<bool> RenovarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default)
    {
        var hasta = _reloj.UtcNow + Duracion(duracion);
        var dueno = _dueno;

        var renovadas = await _db.BackgroundLeases
            .Where(l => l.Name == nombre && l.Owner == dueno)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LeaseUntil, hasta), ct);

        if (renovadas == 0)
        {
            _logger.LogWarning(
                "[Arrendamiento.Perdido] {Dueno} ya no tiene «{Nombre}»: otra réplica lo tomó tras vencer. " +
                "Se detiene la tanda; lo hecho queda protegido por sus propias garantías.", dueno, nombre);
        }

        return renovadas == 1;
    }

    public async Task SoltarAsync(string nombre, CancellationToken ct = default)
    {
        // Vencido desde ya: el siguiente que pregunte lo toma sin esperar el TTL.
        var libre = _reloj.UtcNow.AddSeconds(-1);
        var dueno = _dueno;

        await _db.BackgroundLeases
            .Where(l => l.Name == nombre && l.Owner == dueno)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Owner, (string?)null)
                .SetProperty(l => l.LeaseUntil, libre), ct);
    }

    private static TimeSpan Duracion(TimeSpan? duracion) =>
        duracion is { } d && d > TimeSpan.Zero ? d : DuracionPorDefecto;

    private static string ArmarDueno()
    {
        var maquina = Environment.MachineName;
        if (maquina.Length > 60) maquina = maquina[..60];
        return $"{maquina}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    }
}
