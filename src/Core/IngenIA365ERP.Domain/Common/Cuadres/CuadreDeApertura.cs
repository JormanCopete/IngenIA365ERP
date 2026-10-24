using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Common.Cuadres;

// Feature 015, US13 (T0286; FR-158, FR-160, FR-228; data-model §15.2, §15.3, §20): lo común del cuadre de apertura y de las cifras de referencia de Recaudo y de
// Tesorería. Cada módulo tiene sus tablas (RCD_ y TES_) con clases concretas que heredan de estas bases, como las matrices de la fase 16; las bases no se mapean solas.

/// <summary>El tipo del cuadre de apertura (data-model §20). Se guarda como int y un valor nunca se renumera.</summary>
public enum OpeningSquareKind
{
    Definitive = 1,
    Provisional = 2,
}

/// <summary>El estado de un cuadre de apertura (data-model §15.2). <c>Superseded</c>: otro lo reemplazó (el provisional queda enlazado a su definitivo). (nuevo)</summary>
public enum OpeningSquareState
{
    Pending = 1,
    Squared = 2,
    AcceptedWithMinutes = 3,
    ProvisionalApproved = 4,
    Superseded = 5,
}

/// <summary>
/// La cabecera de un cuadre de apertura (data-model §15.2): definitivo (diferencia cero o acta aceptada) o provisional (decisión del dueño: con aprobación, motivo y
/// fecha límite; el cierre contable del mes de la activación se niega mientras siga provisional, FR-228). (nuevo)
/// </summary>
public abstract class CuadreDeAperturaBase : AuditableEntity
{
    public OpeningSquareKind Kind { get; set; }

    public OpeningSquareState Status { get; set; } = OpeningSquareState.Pending;

    /// <summary>La fecha de corte: la puesta en marcha cuadra los saldos a ese día.</summary>
    public DateOnly CutoffDate { get; set; }

    public decimal TotalLoaded { get; set; }

    /// <summary>Lo contable de las líneas comparables con dato; las líneas «sin dato» no suman.</summary>
    public decimal TotalLedger { get; set; }

    public decimal TotalDifference { get; set; }

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    /// <summary>Quien aprobó el provisional o aceptó el acta del definitivo con diferencia.</summary>
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? Reason { get; set; }

    /// <summary>Provisional: la fecha límite del definitivo (del 02/12 al 04/12 en el plan de implantación); al vencer, la alerta escala (fase 21).</summary>
    public DateOnly? DeadlineOn { get; set; }

    public Guid? MinutesAttachmentPublicId { get; set; }

    /// <summary>El definitivo que reemplazó a este provisional.</summary>
    public int? DefinitiveSquareId { get; set; }
}

/// <summary>Una línea del cuadre por concepto y dimensión: lo cargado, lo contable (nulo = «sin dato») y la diferencia. Las informativas no se comparan. (nuevo)</summary>
public abstract class LineaDeCuadreDeAperturaBase : AuditableEntity, IHechoInmutable
{
    public int OpeningSquareId { get; set; }

    /// <summary>El concepto: el rol de la matriz contable que lo lleva (<c>Boveda</c>, <c>Banco</c>, <c>PartidasPorIdentificar</c>…) o uno informativo.</summary>
    public string Concept { get; set; } = string.Empty;

    /// <summary>La dimensión por código: la oficina en Recaudo, la cuenta propia (o la oficina) en Tesorería.</summary>
    public string Dimension { get; set; } = string.Empty;

    public string? DimensionLabel { get; set; }

    public decimal Loaded { get; set; }

    public decimal? Ledger { get; set; }

    public decimal Difference { get; set; }

    public bool IsComparable { get; set; } = true;
}

/// <summary>
/// Una cifra de referencia (data-model §15.3; FR-160, FR-226): el total que la cooperativa trae de su sistema de origen por concepto y dimensión, para cotejar lo
/// cargado y lo contable y el informe del paralelo. Nunca nombra al sistema de origen. Volver a cargar el mismo concepto, dimensión y corte da de baja lógica la
/// anterior. (nuevo)
/// </summary>
public abstract class CifraDeReferenciaBase : AuditableEntity
{
    public DateOnly CutoffDate { get; set; }

    public string Concept { get; set; } = string.Empty;

    public string Dimension { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    public Guid ImportBatchPublicId { get; set; }

    public string? SourceFileName { get; set; }
}

/// <summary>Una línea propuesta por el módulo antes de calcular: lo cargado y lo contable (nulo si Contabilidad no tiene dato). (nuevo)</summary>
public sealed record LineaPropuestaDeCuadre(string Concepto, string Dimension, string? Etiqueta, decimal Cargado, decimal? Contable, bool Comparable = true);

/// <summary>Una línea calculada: con su diferencia y si cuadra. (nuevo)</summary>
public sealed record LineaCalculadaDeCuadre(string Concepto, string Dimension, string? Etiqueta, decimal Cargado, decimal? Contable, decimal Diferencia, bool Comparable,
    bool Cuadra);

/// <summary>El resultado del cálculo: las líneas y los totales. (nuevo)</summary>
public sealed record CalculoDeCuadre(IReadOnlyList<LineaCalculadaDeCuadre> Lineas, decimal TotalCargado, decimal TotalContable, decimal TotalDiferencia)
{
    /// <summary>Todas las líneas comparables cuadran: el definitivo puede quedar <c>Squared</c> sin acta.</summary>
    public bool Cuadra => Lineas.All(l => l.Cuadra);
}

/// <summary>
/// El motor puro del cuadre de apertura (feature 015, US13, T0288; FR-158, FR-228): no lee ni escribe nada. Una línea comparable cuadra si Contabilidad tiene dato y
/// la diferencia es cero, o si lo cargado es cero y no hay dato; una informativa (cheques girados en tránsito, que el saldo en libros ya descuenta) siempre cuadra y
/// no suma diferencia. El estado del definitivo: <c>Squared</c> si todo cuadra; con diferencia, sólo con acta aceptada (<c>AcceptedWithMinutes</c>); sin acta, no se
/// registra (quien llama lo rechaza). (nuevo)
/// </summary>
public static class CalculoDelCuadreDeApertura
{
    public static CalculoDeCuadre Calcular(IEnumerable<LineaPropuestaDeCuadre> propuestas)
    {
        ArgumentNullException.ThrowIfNull(propuestas);
        var lineas = propuestas
            .OrderBy(p => p.Concepto, StringComparer.Ordinal).ThenBy(p => p.Dimension, StringComparer.Ordinal)
            .Select(p =>
            {
                if (!p.Comparable) return new LineaCalculadaDeCuadre(p.Concepto, p.Dimension, p.Etiqueta, p.Cargado, p.Contable, 0m, false, true);
                var diferencia = p.Cargado - (p.Contable ?? 0m);
                var cuadra = p.Contable is null ? p.Cargado == 0m : diferencia == 0m;
                return new LineaCalculadaDeCuadre(p.Concepto, p.Dimension, p.Etiqueta, p.Cargado, p.Contable, diferencia, true, cuadra);
            })
            .ToList();
        var comparables = lineas.Where(l => l.Comparable).ToList();
        return new CalculoDeCuadre(lineas, comparables.Sum(l => l.Cargado), comparables.Sum(l => l.Contable ?? 0m), comparables.Sum(l => l.Diferencia));
    }

    /// <summary>El estado del definitivo; nulo si tiene diferencia y no trae acta (no se registra).</summary>
    public static OpeningSquareState? EstadoDelDefinitivo(CalculoDeCuadre calculo, bool conActa)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        if (calculo.Cuadra) return OpeningSquareState.Squared;
        return conActa ? OpeningSquareState.AcceptedWithMinutes : null;
    }

    /// <summary>La fecha límite de un provisional es válida si cae después del corte.</summary>
    public static bool FechaLimiteValida(DateOnly corte, DateOnly fechaLimite) => fechaLimite > corte;
}
