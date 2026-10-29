namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 015, US3 (T0110; FR-002; data-model §0.2, §3.3 a §3.6 «[columna mutable]»): propiedad de un <see cref="IHechoInmutable"/> de Recaudo o Tesorería
/// que cambia por el ciclo de estado que data-model declara —el estado del recibo al anularse, el de una aplicación que el módulo reversa, el de un medio que
/// se castiga por cheque devuelto—. Todo lo demás del hecho sigue siendo de sólo inserción.
///
/// <para>
/// Quien lo hace cumplir es <c>GuardaDeInmutabilidad</c>: admite un <c>Modified</c> sobre un hecho si toda propiedad modificada lleva este atributo o
/// <see cref="EscrituraUnicaAttribute"/> (con original nulo). <c>LosHechosInmutablesNoSeModifican</c> fija qué propiedades lo llevan y que sólo lo usen
/// los hechos de Recaudo y Tesorería. (nuevo)
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ColumnaMutableAttribute : Attribute;
