namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Lo que la API necesita saber de la idempotencia de la petición en curso (feature 012, T055, T056). Es
/// <b>por petición</b> (Scoped): el filtro <c>ClaveDeOperacionFilter</c> pone la <see cref="Clave"/> que leyó
/// de la cabecera y, después de ejecutar, mira <see cref="EsRepeticion"/> para agregar
/// <c>Idempotent-Replayed: true</c>; <see cref="IdempotencyBehavior{TRequest, TResponse}"/> la marca cuando
/// devuelve un resultado guardado en vez de ejecutar.
/// </summary>
public sealed class EstadoDeLaOperacion
{
    /// <summary>La clave que trajo la cabecera <c>Idempotency-Key</c>; nula fuera de una ruta con clave.</summary>
    public Guid? Clave { get; set; }

    /// <summary>La respuesta es la guardada de una ejecución anterior con la misma clave.</summary>
    public bool EsRepeticion { get; private set; }

    /// <summary>Cuándo se usó la clave por primera vez, si es una repetición.</summary>
    public DateTime? PrimerUso { get; private set; }

    public void MarcarRepeticion(DateTime primerUso)
    {
        EsRepeticion = true;
        PrimerUso = primerUso;
    }
}
