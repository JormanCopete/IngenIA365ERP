using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>Lo que se pidió calcular: clase, periodicidad, subperíodo, rango de días causados, conceptos y filtros (Ids internos). (nuevo)</summary>
public sealed record ParametrosDelProceso(
    AccrualProcessKind Clase,
    AccrualProcessPeriodicity Periodicidad,
    byte? Subperiodo,
    DateOnly Desde,
    DateOnly Hasta,
    AccrualProcessConcepts Conceptos,
    int? LineaId,
    int? OficinaId,
    int? PagaduriaId)
{
    /// <summary>Los parámetros sellados en un proceso.</summary>
    public static ParametrosDelProceso De(AccrualProcess p) =>
        new(p.Kind, p.Periodicity, p.SubPeriod, p.DateFrom, p.DateTo, p.Concepts, p.CreditLineId, p.BranchId, p.PaymasterPersonId);
}

/// <summary>
/// El contexto de un cálculo: los parámetros, el alcance de quien opera (el cálculo deja fuera lo que no ve), el proceso que se reprocesa —con los
/// créditos que tuvieron abonos después de su puesta en firme y lo que contabilizó de cada crédito— y hoy. (nuevo)
/// </summary>
public sealed record ContextoDelCalculo(
    ParametrosDelProceso Parametros,
    AlcanceDeCartera Alcance,
    DateOnly Hoy,
    AccrualProcess? Reprocesado = null)
{
    /// <summary>Reproceso: los créditos con abonos registrados después de la puesta en firme del reprocesado (quedan intactos).</summary>
    public IReadOnlySet<int> CreditosConAbonos { get; init; } = new HashSet<int>();

    /// <summary>Reproceso: lo que el reprocesado contabilizó de cada (crédito, tipo de hecho, concepto).</summary>
    public IReadOnlyDictionary<(int Credito, string? Tipo, LoanConcept? Concepto), decimal> Anteriores { get; init; } =
        new Dictionary<(int, string?, LoanConcept?), decimal>();
}

/// <summary>Un hecho del cálculo (la unidad que se contabiliza), con su estado, motivo y si bloquea. (nuevo)</summary>
public sealed record HechoDelCalculo(
    Guid? MensajePublicId,
    string Tipo,
    DateOnly DiaCausado,
    DateOnly FechaContable,
    int? OficinaId,
    int Creditos,
    decimal Monto,
    AccrualProcessItemStatus Estado,
    AccrualExclusionReason? Motivo,
    bool Bloquea,
    string? Detalle);

/// <summary>Lo de un crédito y un concepto en el cálculo. (nuevo)</summary>
public sealed record LineaDelCalculo(
    int CreditoId,
    string? Tipo,
    LoanConcept? Concepto,
    decimal Monto,
    decimal? Anterior,
    AccrualProcessItemStatus Estado,
    AccrualExclusionReason? Motivo,
    int OficinaId,
    int LineaId)
{
    /// <summary>2026-11-16 (feature 024, fase 4): en la clase <c>PeriodRecognition</c>, la cuota que se reconoce. (nuevo)</summary>
    public int? Cuota { get; init; }

    /// <summary>2026-11-16 (feature 024, fase 4): en la clase <c>PeriodRecognition</c>, el día desde el que se reconoce. (nuevo)</summary>
    public DateOnly? FechaDeReconocimiento { get; init; }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 3): en la clase <c>CutoverRecalculation</c>, el detalle del crédito en JSON (<see cref="DetalleDelCorte"/>): entra en la
    /// huella. (nuevo)
    /// </summary>
    public string? Detalle { get; init; }
}

/// <summary>Un bloqueo (impide ponerlo en firme) o un aviso del cálculo. (nuevo)</summary>
public sealed record AvisoDelCalculo(string Code, string Message, bool Blocking);

/// <summary>
/// El resultado de un cálculo: hechos, líneas, bloqueos y avisos. <see cref="Huella"/> es lo que se pide aprobar: los parámetros, cada hecho con su estado,
/// motivo y valor, los bloqueos y avisos y los totales de las líneas. Un recálculo con el mismo resultado da la misma huella. (nuevo)
/// </summary>
public sealed record CalculoDelProceso(IReadOnlyList<HechoDelCalculo> Hechos, IReadOnlyList<LineaDelCalculo> Lineas, IReadOnlyList<AvisoDelCalculo> Avisos)
{
    /// <summary>¿Hay algo que impida ponerlo en firme?</summary>
    public bool Bloquea => Avisos.Any(a => a.Blocking) || Hechos.Any(h => h.Bloquea);

    public IEnumerable<HechoDelCalculo> Incluidos => Hechos.Where(h => h.Estado == AccrualProcessItemStatus.Included);

    public IEnumerable<HechoDelCalculo> Excluidos => Hechos.Where(h => h.Estado == AccrualProcessItemStatus.Excluded);

    /// <summary>Los créditos distintos con algo en el borrador.</summary>
    public int Creditos => Lineas.Select(l => l.CreditoId).Distinct().Count();

    /// <summary>Lo mismo con un aviso o bloqueo más.</summary>
    public CalculoDelProceso Con(IEnumerable<AvisoDelCalculo> avisos) => this with { Avisos = [.. Avisos, .. avisos] };

    public string Huella(ParametrosDelProceso p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new HuellaDeCartera()
            .Enumeracion<AccrualProcessKind>("kind", p.Clase)
            .Enumeracion<AccrualProcessPeriodicity>("periodicity", p.Periodicidad)
            .Entero("subPeriod", p.Subperiodo)
            .Fecha("from", p.Desde)
            .Fecha("to", p.Hasta)
            .Entero("concepts", (int)p.Conceptos)
            .Entero("creditLine", p.LineaId)
            .Entero("branch", p.OficinaId)
            .Entero("paymaster", p.PagaduriaId)
            .Lista("facts", Hechos.OrderBy(h => h.MensajePublicId).ThenBy(h => h.Tipo, StringComparer.Ordinal).ThenBy(h => h.DiaCausado), (b, h) => b
                .Identificador("message", h.MensajePublicId)
                .Texto("type", h.Tipo)
                .Enumeracion<AccrualProcessItemStatus>("status", h.Estado)
                .Enumeracion("reason", h.Motivo)
                .Logico("blocking", h.Bloquea)
                .Decimal("amount", h.Monto, 2))
            .Lista("issues", Avisos.OrderBy(a => a.Code, StringComparer.Ordinal).ThenBy(a => a.Message, StringComparer.Ordinal), (b, a) => b
                .Texto("code", a.Code)
                .Texto("message", a.Message)
                .Logico("blocking", a.Blocking))
            .Entero("lineCount", Lineas.Count)
            .Decimal("linesIncluded", Lineas.Where(l => l.Estado == AccrualProcessItemStatus.Included).Sum(l => l.Monto), 2)
            .Decimal("linesExcluded", Lineas.Where(l => l.Estado == AccrualProcessItemStatus.Excluded).Sum(l => l.Monto), 2)
            .Entero("laterPayments", Lineas.Count(l => l.Motivo == AccrualExclusionReason.LaterPayment))
            // 2026-11-16 (feature 024, fase 4): lo reconocido, crédito por crédito (cuota, concepto, día y valor), sólo si lo hay.
            .Si(Lineas.Any(l => l.Cuota is not null), b => b.Lista("recognized",
                Lineas.Where(l => l.Cuota is not null && l.Estado == AccrualProcessItemStatus.Included)
                    .OrderBy(l => l.CreditoId).ThenBy(l => l.Cuota).ThenBy(l => l.Concepto),
                (h, l) => h.Entero("loan", l.CreditoId).Entero("installment", l.Cuota).Enumeracion("concept", l.Concepto)
                    .Fecha("from", l.FechaDeReconocimiento).Decimal("amount", l.Monto, 2)))
            // 2026-11-24 (feature 025, fase 3): el detalle de cada crédito del recálculo de la carga desde el corte (tasa, días, ajuste y diferencias).
            .Si(Lineas.Any(l => l.Detalle is not null), b => b.Lista("cutover",
                Lineas.Where(l => l.Detalle is not null).OrderBy(l => l.CreditoId).ThenBy(l => l.Tipo, StringComparer.Ordinal),
                (h, l) => h.Entero("loan", l.CreditoId).Enumeracion<AccrualProcessItemStatus>("status", l.Estado).Texto("detail", l.Detalle)))
            .Calcular();
    }
}

/// <summary>
/// El lote que ordenó la puesta en firme. 2026-11-16 (fase 4): nulo si lo que se puso en firme no pasa a Contabilidad (el reconocimiento con su hecho en
/// «No pasa»). (nuevo)
/// </summary>
public sealed record LoteDelProceso(Guid? LotePublicId, long? Numero, int Hechos);

/// <summary>
/// El punto de extensión de la clase de un proceso de causación (feature 024, fase 3). La cabecera, el borrador, los filtros, el alcance, el cerrojo, la
/// huella y el control de cuatro ojos son comunes (<see cref="ProcesosDeCausacion"/>); cada clase dice cómo se calcula su borrador y qué hace al ponerse
/// en firme. La fase 3 trae <see cref="ContabilizacionDeLoCausado"/> (<c>Posting</c>): toma lo causado que espera y ordena su lote. La fase 4 agrega el
/// reconocimiento de la cuota del período (<c>PeriodRecognition</c>): su cálculo arma las líneas por crédito con lo que reconoce (sin mensaje: el hecho
/// todavía no existe) y su puesta en firme emite los hechos en la misma transacción y ordena el lote; nada de lo común cambia. (nuevo)
/// </summary>
public interface IClaseDeProcesoDeCausacion
{
    AccrualProcessKind Clase { get; }

    /// <summary>El borrador: hechos, líneas y lo que bloquea o avisa la clase. No escribe.</summary>
    Task<Result<CalculoDelProceso>> CalcularAsync(ContextoDelCalculo contexto, CancellationToken ct);

    /// <summary>
    /// Pone en firme lo calculado (sus hechos incluidos), dentro de la transacción de la aprobación: ordena el lote con <paramref name="motivo"/> y
    /// devuelve su número. Si algo ya no está como en el borrador, falla y la aprobación no queda.
    /// </summary>
    Task<Result<LoteDelProceso>> PonerEnFirmeAsync(AccrualProcess proceso, IReadOnlyList<AccrualProcessFact> incluidos, string motivo, CancellationToken ct);
}
