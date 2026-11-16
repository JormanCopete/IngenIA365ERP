using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>
/// Los códigos y mensajes del proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3). Los de las transiciones los dice
/// <see cref="AccrualProcess"/> y el del rango, <see cref="PeriodoDeCausacion"/>. Todos son 422 salvo el inexistente (404). (nuevo)
/// </summary>
public static class ErroresDeProcesosDeCausacion
{
    public const string CodigoInexistente = "Lending.AccrualProcess.NotFound";
    public const string CodigoNoBorrador = AccrualProcess.CodigoNoBorrador;
    public const string CodigoEstadoInvalido = AccrualProcess.CodigoEstadoInvalido;
    public const string CodigoRangoInvalido = PeriodoDeCausacion.CodigoRangoInvalido;
    public const string CodigoSinConceptos = "Lending.AccrualProcess.ConceptsRequired";
    public const string CodigoConceptoNoManual = "Lending.AccrualProcess.ConceptNotManual";
    public const string CodigoOtroAbierto = "Lending.AccrualProcess.AnotherOpen";
    public const string CodigoSeCruza = "Lending.AccrualProcess.Overlaps";
    public const string CodigoFiltroDeAlcance = "Lending.AccrualProcess.ScopeFilterRequired";
    public const string CodigoContenidoCambiado = "Lending.AccrualProcess.ContentChanged";
    public const string CodigoDesactualizado = "Lending.AccrualProcess.Stale";
    public const string CodigoBloqueado = "Lending.AccrualProcess.Blocked";
    public const string CodigoNadaQueContabilizar = "Lending.AccrualProcess.NothingToPost";
    public const string CodigoSinPolitica = "Lending.AccrualProcess.ApprovalPolicyNotRequired";
    public const string CodigoAutoaprobacion = "Lending.AccrualProcess.SelfApproval";
    public const string CodigoPresenciaRequerida = "Lending.AccrualProcess.ApproverNotPresent";
    public const string CodigoNoEnFirme = "Lending.AccrualProcess.NotConfirmed";
    public const string CodigoReprocesoFijo = "Lending.AccrualProcess.ReprocessScopeFixed";
    public const string CodigoFiltroInexistente = "Lending.AccrualProcess.FilterNotFound";

    public static Error Inexistente() => new(CodigoInexistente, "El proceso de causación no existe.");

    public static Error SinConceptos() => new(CodigoSinConceptos, "Elija al menos un concepto: interés corriente, de mora, seguro, administración u otros rubros.");

    public static Error ConceptoNoManual(string concepto) => new ErrorConDatos(CodigoConceptoNoManual,
        $"«{concepto}» se contabiliza automáticamente cada noche y no tiene nada esperando: el proceso sólo toma los conceptos en modo manual. Cámbielo en los "
        + "parámetros de Cartera (Causación) si quiere contabilizarlo por proceso.", new { concept = concepto });

    public static Error OtroAbierto(string numero, Guid proceso) => new ErrorConDatos(CodigoOtroAbierto,
        $"Ya hay un proceso de causación abierto de alguno de esos conceptos ({numero}): póngalo en firme o anúlelo antes de armar otro.",
        new { processNumber = numero, processPublicId = proceso });

    public static Error SeCruza(string numero, Guid proceso) => new ErrorConDatos(CodigoSeCruza,
        $"El proceso {numero}, ya en firme, cubre esos conceptos en parte de ese rango: para volver a contabilizarlo, reprocéselo.",
        new { processNumber = numero, processPublicId = proceso });

    public static Error FiltroDeAlcance(bool oficina) => new(CodigoFiltroDeAlcance, oficina
        ? "Su alcance no cubre todas las oficinas: elija en el filtro una oficina de su alcance."
        : "Su alcance no cubre todas las líneas: elija en el filtro una línea de su alcance.");

    public static Error FiltroInexistente(string que) => new ErrorConDatos(CodigoFiltroInexistente,
        $"No se encontró {que} del filtro.", new { filter = que });

    public static Error ContenidoCambiado(string? actual) => new ErrorConDatos(CodigoContenidoCambiado,
        "El borrador cambió desde que lo revisó: vuelva a mirarlo antes de pedir que lo pongan en firme.", new { contentSha256 = actual });

    public static Error Desactualizado() => new(CodigoDesactualizado,
        "Lo que espera contabilizarse cambió desde el cálculo (llegó causación nueva, otro lote la tomó o cambió un período o una regla): recalcule el "
        + "borrador y vuelva a pedir la firma.");

    public static Error Bloqueado(IReadOnlyList<string> motivos) => new ErrorConDatos(CodigoBloqueado,
        "El proceso no se puede poner en firme todavía: " + string.Join(" ", motivos), new { reasons = motivos });

    public static Error NadaQueContabilizar() => new(CodigoNadaQueContabilizar,
        "El borrador no tiene nada para contabilizar: todo lo del rango ya está contabilizado o quedó fuera con su motivo.");

    public static Error SinPolitica() => new(CodigoSinPolitica,
        "La política de aprobaciones de la cooperativa para el proceso de causación no pide aprobación: poner en firme exige siempre otra persona. "
        + "Corrija la política (el primer nivel desde cero).");

    public static Error Autoaprobacion() => new(CodigoAutoaprobacion,
        "Quien aprueba es la misma persona que pidió poner en firme el proceso: lo aprueba otra persona con el permiso.");

    public static Error PresenciaRequerida() => new(CodigoPresenciaRequerida,
        "El proceso de causación lo aprueba otra persona presente, con su llave o su código, en el equipo de quien lo pidió; no desde la bandeja.");

    public static Error NoEnFirme(string estado) => new ErrorConDatos(CodigoNoEnFirme,
        "Sólo se reprocesa un proceso en firme.", new { status = estado });

    public static Error ReprocesoFijo() => new(CodigoReprocesoFijo,
        "Un reproceso cubre lo mismo que el proceso que reprocesa: no se le cambian la periodicidad, el rango, los conceptos ni los filtros.");
}
