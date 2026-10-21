using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>Un pago anterior o posterior cuya distribución cambió por la operación (FR-081): su Id en la entrada y sus dos distribuciones.</summary>
public sealed record ReasignacionDePago(string OperacionId, DistribucionDePago Antes, DistribucionDePago Despues);

/// <summary>
/// Lo que una operación de pago, de reverso o de resolución le hace al libro (feature 013, US5, bloque 9B; T33, D4, FR-080, FR-081): el estado
/// del crédito con ella a <see cref="Hasta"/>, sus filas propias (las del libro teórico con su Id), las diferencias que deja en los días ya
/// causados —por familia: los hechos de otros pagos redistribuidos y la causación— y los pagos cuya distribución cambió. (nuevo)
/// </summary>
public sealed record EfectoEnElLibro(
    EstadoDelCredito Despues,
    IReadOnlyList<MovimientoDeLibro> Propias,
    IReadOnlyList<DiferenciaDeLibro> Diferencias,
    IReadOnlyList<ReasignacionDePago> Reasignaciones,
    DateOnly Hasta)
{
    /// <summary>
    /// La operación reanuda la causación suspendida (FR-065): sin ella el crédito seguiría suspendido el día siguiente a <see cref="Hasta"/> y con ella
    /// no (un hecho con fecha valor v rige desde v + 1).
    /// </summary>
    public bool Reanuda { get; init; }

    /// <summary>Las diferencias de causación (para el acompañante <c>AjusteDeCausacionPorFechaValor</c>).</summary>
    public IReadOnlyList<DiferenciaDeLibro> DeCausacion => Diferencias.Where(d => !d.DeHechos && d.Concepto != Domain.Enums.Lending.LoanConcept.Principal).ToList();

    /// <summary>
    /// El efecto de pasar de <paramref name="antes"/> a <paramref name="despues"/> (la entrada con la operación nueva, Id
    /// <paramref name="idNueva"/>), desde su fecha valor <paramref name="desde"/>:
    /// <list type="bullet">
    /// <item>se reconstruye con la operación hasta la marca de causación (o la fecha valor, si la marca no llega), con libro desde la fecha valor;</item>
    /// <item>si la marca ya pasó la fecha valor, el libro real de esos días (<paramref name="libroReal"/>), más las filas propias y las
    /// <paramref name="adicionales"/> (los espejos de un reverso), se compara por familia contra el teórico (<see cref="LibroTeorico.DiferenciasPorFamilia"/>);
    /// si no la pasó, no hay nada causado que corregir (la puesta al día que falta causa ya con la operación);</item>
    /// <item>los pagos que siguen en las dos reconstrucciones con otra distribución son los redistribuidos.</item>
    /// </list>
    /// Puro.
    /// </summary>
    public static EfectoEnElLibro Calcular(EntradaDeReconstruccion antes, EntradaDeReconstruccion despues, string idNueva, DateOnly desde, DateOnly marca,
        IReadOnlyList<MovimientoDeLibro> libroReal, IReadOnlyList<MovimientoDeLibro> adicionales)
    {
        ArgumentNullException.ThrowIfNull(antes);
        ArgumentNullException.ThrowIfNull(despues);
        ArgumentNullException.ThrowIfNull(libroReal);
        ArgumentNullException.ThrowIfNull(adicionales);
        var hasta = marca > desde ? marca : desde;
        var estado = MotorDeCartera.Reconstruir(despues, hasta, desde);
        var propias = estado.Libro.Movimientos.Where(m => m.OperacionId == idNueva).ToList();

        IReadOnlyList<DiferenciaDeLibro> diferencias = [];
        if (marca >= desde)
        {
            var teorico = new LibroTeorico(estado.Libro.Movimientos.Where(m => m.Dia >= desde && m.Dia <= marca));
            var real = libroReal.Where(m => m.Dia >= desde && m.Dia <= marca)
                .Concat(propias.Where(m => m.Dia <= marca))
                .Concat(adicionales.Where(m => m.Dia >= desde && m.Dia <= marca))
                .ToList();
            diferencias = teorico.DiferenciasPorFamilia(real, marca);
        }

        var previo = MotorDeCartera.Reconstruir(antes, hasta);
        var reasignaciones = estado.Pagos
            .Where(kv => kv.Key != idNueva && previo.Pagos.TryGetValue(kv.Key, out var a) && !ComprobantesDePago.MismaDistribucion(a, kv.Value))
            .Select(kv => new ReasignacionDePago(kv.Key, previo.Pagos[kv.Key], kv.Value))
            .OrderBy(r => r.OperacionId, StringComparer.Ordinal)
            .ToList();
        var reanuda = false;
        if (previo.Suspendida || estado.Suspendida)
        {
            var siguiente = hasta.AddDays(1);
            reanuda = MotorDeCartera.Reconstruir(antes, siguiente).Suspendida && !MotorDeCartera.Reconstruir(despues, siguiente).Suspendida;
        }
        return new EfectoEnElLibro(estado, propias, diferencias, reasignaciones, hasta) { Reanuda = reanuda };
    }
}
