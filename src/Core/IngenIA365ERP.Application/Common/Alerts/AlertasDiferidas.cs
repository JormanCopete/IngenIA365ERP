namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Las alertas que tienen que sobrevivir a la operación que las descubre (feature 013, US1-3; T0344). <see cref="IAlertas"/> guarda en el
/// contexto de la petición, así que dentro de un comando <c>IOperacionIdempotente</c> la alerta queda en la transacción de la operación: si
/// el comando falla —justo cuando falta el dato que la alerta avisa— se revierte con él y nadie se entera. Hasta el 2026-10-10 crear una
/// versión de línea un día sin usura respondía <c>NationalValues.Missing</c> y la alerta <c>Cartera.ValorNacionalFaltante</c> se perdía.
///
/// <para>
/// <c>IdempotencyBehavior</c> la <see cref="Abrir">abre</see> alrededor de la transacción; quien descubre algo que debe avisarse pase lo que
/// pase (hoy <c>LectorDeValoresNacionales</c>) la <see cref="Encolar">encola</see> mientras está <see cref="Activa"/>, y el behavior la
/// <see cref="Tomar">vacía</see> al terminar, ya fuera de la transacción: tras un fallo, después de descartar lo que el intento revertido
/// dejó en el contexto. Fuera de una operación idempotente (tareas de fondo, consultas) no está activa y la alerta se levanta en el acto,
/// como antes. Es por petición (scoped); una operación anidada no la vacía: lo hace la de afuera. (nuevo)
/// </para>
/// </summary>
public sealed class AlertasDiferidas
{
    private readonly List<AlertaALevantar> _pendientes = [];
    private int _profundidad;

    /// <summary>¿Hay una operación idempotente en curso que las levantará al terminar?</summary>
    public bool Activa => _profundidad > 0;

    /// <summary>Abre una operación; verdadero si es la de afuera (la que vacía al terminar).</summary>
    public bool Abrir() => ++_profundidad == 1;

    /// <summary>Cierra la operación que se abrió.</summary>
    public void Cerrar()
    {
        if (_profundidad > 0) _profundidad--;
    }

    public void Encolar(AlertaALevantar alerta)
    {
        ArgumentNullException.ThrowIfNull(alerta);
        _pendientes.Add(alerta);
    }

    /// <summary>Las encoladas, en su orden, y la cola queda vacía.</summary>
    public IReadOnlyList<AlertaALevantar> Tomar()
    {
        var todas = _pendientes.ToList();
        _pendientes.Clear();
        return todas;
    }
}
