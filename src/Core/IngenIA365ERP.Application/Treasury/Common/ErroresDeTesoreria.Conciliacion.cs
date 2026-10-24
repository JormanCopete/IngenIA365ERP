using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US25 (T0278; FR-131, FR-216, FR-217; data-model §21): los errores de la conciliación mínima. SegregationConflict está en ErroresDeTesoreria.Seguridad; los
// que data-model §21 no nombraba son (nuevo) y quedan en DT §2.7.
public static partial class ErroresDeTesoreria
{
    public const string CodigoConciliacionYaCruzada = "Treasury.Reconciliation.AlreadyMatched";
    public const string CodigoConciliacionNoCuadra = "Treasury.Reconciliation.NotSquared";
    public const string CodigoConciliacionCerrada = "Treasury.Reconciliation.Closed";
    public const string CodigoConciliacionNoAbierta = "Treasury.Reconciliation.NotOpen";
    public const string CodigoConciliacionValorNoCuadra = "Treasury.Reconciliation.AmountMismatch";
    public const string CodigoConciliacionRegistroNoDisponible = "Treasury.Reconciliation.RecordNotAvailable";
    public const string CodigoConciliacionPeriodoInvalido = "Treasury.Reconciliation.InvalidPeriod";
    public const string CodigoConciliacionPartidaInvalida = "Treasury.Reconciliation.ItemInvalid";

    public static Error ConciliacionYaCruzada(string que) => new ErrorConDatos(CodigoConciliacionYaCruzada,
        $"{que} ya está cruzado en esta conciliación: deshaga el cruce antes de volver a cruzarlo.", new { record = que });

    public static Error ConciliacionNoCuadra(decimal diferencia) => new ErrorConDatos(CodigoConciliacionNoCuadra,
        $"La conciliación no cuadra: la diferencia es {diferencia:N2}. Cruce lo que falta o registre las partidas conciliatorias que la explican.",
        new { difference = diferencia });

    public static Error ConciliacionCerrada() => new(CodigoConciliacionCerrada,
        "La conciliación ya está cerrada: para cambiarla, reábrala con su motivo (queda una versión nueva).");

    public static Error ConciliacionNoAbierta(string estado) => new ErrorConDatos(CodigoConciliacionNoAbierta,
        $"La conciliación está «{estado}»: sólo se trabaja una abierta.", new { state = estado });

    public static Error ConciliacionValorNoCuadra(decimal linea, decimal registros) => new ErrorConDatos(CodigoConciliacionValorNoCuadra,
        $"Los registros elegidos suman {registros:N2} y la línea del extracto es por {linea:N2}: un cruce tiene que ser exacto.",
        new { line = linea, records = registros });

    public static Error ConciliacionRegistroNoDisponible() => new(CodigoConciliacionRegistroNoDisponible,
        "Uno de los registros elegidos no es de esta cuenta y período, o ya está cruzado en otra conciliación.");

    public static Error ConciliacionPeriodoInvalido() => new(CodigoConciliacionPeriodoInvalido,
        "El mes de la conciliación no puede ser posterior al mes en curso.");

    public static Error ConciliacionPartidaInvalida(string motivo) => new(CodigoConciliacionPartidaInvalida, motivo);
}
