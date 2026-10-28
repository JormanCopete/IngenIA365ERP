namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// El valor de un <c>Result&lt;T&gt;</c> que dice si la operación <b>cambió algo</b>. Lo implementan sólo los resultados de los
/// comandos de proceso que corren en bucle (cada pocos segundos o cada minuto) y que en la mayoría de las vueltas no tienen nada
/// que hacer. <see cref="AuditBehavior{TRequest, TResponse}"/> no registra el éxito de una vuelta con <see cref="HuboEfecto"/>
/// falso, y al de una vuelta con efecto le agrega el resultado (<c>{ solicitud, resultado }</c> en <c>NewValues</c>), para que
/// el evento diga qué se hizo y no sólo que se pidió.
///
/// <para>
/// Principio X: los rechazos y las excepciones se siguen auditando siempre, y una vuelta que hizo algo también. Lo único que se
/// deja de registrar es la vuelta vacía. Incidente del 2026-10-26: <c>ScheduleIntegrationBatchesCommand</c> se auditaba en cada
/// vuelta del despachador (cada 5 s, ~34.000 eventos al día por cooperativa) sin programar ningún lote.
/// </para>
/// <para>
/// No lo implementes en el resultado de una acción de una persona: lo que pide una persona se audita aunque no cambie nada.
/// </para>
/// </summary>
public interface IResultadoConEfecto
{
    /// <summary>Verdadero si la vuelta creó, cambió o anotó algo.</summary>
    bool HuboEfecto { get; }
}
