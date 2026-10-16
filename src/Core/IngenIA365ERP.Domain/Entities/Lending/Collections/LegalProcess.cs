using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_LegalProcesses</c> (feature 013, K4, T0936; data-model §26.10; FR-124): el cobro jurídico de un crédito entregado a un abogado, que
/// es una persona del maestro. El ciclo:
/// <code>
/// Assigned ──▶ Filed ──▶ InProgress ──▶ Concluded
///    │           │           │
///    └───────────┴───────────┴──▶ Suspended (la insolvencia, FR-098) ──reanudar a mano──▶ Assigned · Filed · InProgress
/// cualquiera abierto ──▶ Withdrawn · Concluded (cerrar con motivo)
/// </code>
/// Reanudar (K4, bloque D, decisión del dueño del 2026-10-16) es de una persona con motivo y nunca automático: el proceso vuelve al estado que dicen sus
/// actuaciones (<see cref="EstadoAlReanudar"/>), que es el que tenía al suspenderse porque suspendido no recibe actuaciones que lo muevan.
/// Sus actuaciones son hechos (<see cref="LegalProcessAction"/>). (nuevo)
/// </summary>
public class LegalProcess : AuditableEntity
{
    public const string CodigoEstadoInvalido = "Lending.LegalProcess.InvalidStatus";

    public string Number { get; init; } = string.Empty;

    public int LoanId { get; init; }

    /// <summary>El demandado.</summary>
    public int PersonId { get; init; }

    public int LawyerPersonId { get; init; }

    public DateOnly AssignedOn { get; init; }

    public string? Authority { get; private set; }

    public string? CaseNumber { get; private set; }

    public string? ProcessType { get; private set; }

    public LegalProcessStatus Status { get; private set; } = LegalProcessStatus.Assigned;

    public int? SuspendedByInsolvencyId { get; private set; }

    public string? FeeAgreementJson { get; init; }

    public DateTime? ClosedAt { get; private set; }

    public string? CloseReason { get; private set; }

    private static readonly IReadOnlyDictionary<LegalProcessStatus, LegalProcessStatus[]> Transiciones = new Dictionary<LegalProcessStatus, LegalProcessStatus[]>
    {
        [LegalProcessStatus.Assigned] = [LegalProcessStatus.Filed, LegalProcessStatus.Suspended, LegalProcessStatus.Withdrawn, LegalProcessStatus.Concluded],
        [LegalProcessStatus.Filed] = [LegalProcessStatus.InProgress, LegalProcessStatus.Suspended, LegalProcessStatus.Withdrawn, LegalProcessStatus.Concluded],
        [LegalProcessStatus.InProgress] = [LegalProcessStatus.Suspended, LegalProcessStatus.Withdrawn, LegalProcessStatus.Concluded],
        [LegalProcessStatus.Suspended] = [LegalProcessStatus.Assigned, LegalProcessStatus.Filed, LegalProcessStatus.InProgress, LegalProcessStatus.Withdrawn,
            LegalProcessStatus.Concluded],
    };

    public static bool PuedePasar(LegalProcessStatus desde, LegalProcessStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    public bool Abierto => Status is not (LegalProcessStatus.Concluded or LegalProcessStatus.Withdrawn);

    public static LegalProcess Abrir(string numero, int creditoId, int demandadoId, int abogadoId, DateOnly asignadoEl, string? despacho = null,
        string? radicado = null, string? clase = null, string? honorariosJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        if (abogadoId == demandadoId) throw new ArgumentException("El abogado no es el demandado.", nameof(abogadoId));
        return new LegalProcess
        {
            Number = numero.Trim(),
            LoanId = creditoId,
            PersonId = demandadoId,
            LawyerPersonId = abogadoId,
            AssignedOn = asignadoEl,
            Authority = Limpio(despacho),
            CaseNumber = Limpio(radicado),
            ProcessType = Limpio(clase),
            FeeAgreementJson = honorariosJson,
        };
    }

    public ProblemaDeCartera? PuedeIrA(LegalProcessStatus hacia) => PuedePasar(Status, hacia)
        ? null
        : new ProblemaDeCartera(CodigoEstadoInvalido, $"El proceso está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });

    /// <summary>El despacho, el radicado y la clase de proceso, mientras está abierto.</summary>
    public void ActualizarDatos(string? despacho, string? radicado, string? clase)
    {
        if (!Abierto)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoEstadoInvalido, "El proceso está cerrado.", "status", Status.ToString()));
        Authority = Limpio(despacho);
        CaseNumber = Limpio(radicado);
        ProcessType = Limpio(clase);
    }

    /// <summary>Radicado, en curso o reanudado (<c>Filed</c> o <c>InProgress</c>); reanudar suelta la insolvencia que lo suspendió.</summary>
    public void Avanzar(LegalProcessStatus hacia)
    {
        if (hacia is not (LegalProcessStatus.Filed or LegalProcessStatus.InProgress))
            throw new ArgumentOutOfRangeException(nameof(hacia), hacia, "Avanzar es radicar o seguir en curso; suspender y cerrar tienen su método.");
        ReglaDeCarteraException.SiHay(PuedeIrA(hacia));
        Status = hacia;
        SuspendedByInsolvencyId = null;
    }

    /// <summary>Suspende el proceso; si es por una insolvencia (FR-098), la deja dicha.</summary>
    public void Suspender(int? insolvenciaId = null)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(LegalProcessStatus.Suspended));
        Status = LegalProcessStatus.Suspended;
        SuspendedByInsolvencyId = insolvenciaId;
    }

    /// <summary>
    /// El estado al que vuelve un proceso suspendido al reanudarlo (K4, bloque D): el que tenía antes de suspenderse, derivado de sus actuaciones con
    /// la misma regla que lo mueve al registrarlas —sin radicación, <c>Assigned</c>; radicado sin actuación de fondo después, <c>Filed</c>; con una,
    /// <c>InProgress</c>—. No se guarda el estado anterior: las actuaciones son el hecho, y mientras está suspendido ninguna lo mueve.
    /// </summary>
    public static LegalProcessStatus EstadoAlReanudar(IEnumerable<LegalActionKind> actuacionesEnOrden)
    {
        ArgumentNullException.ThrowIfNull(actuacionesEnOrden);
        var estado = LegalProcessStatus.Assigned;
        foreach (var clase in actuacionesEnOrden)
        {
            if (clase == LegalActionKind.Filing && estado == LegalProcessStatus.Assigned) estado = LegalProcessStatus.Filed;
            else if (estado == LegalProcessStatus.Filed && clase is not (LegalActionKind.Fee or LegalActionKind.Expense or LegalActionKind.Other))
                estado = LegalProcessStatus.InProgress;
        }
        return estado;
    }

    /// <summary>
    /// Reanuda a mano un proceso suspendido (K4, bloque D): vuelve a <paramref name="hacia"/> (<see cref="EstadoAlReanudar"/>) y suelta la insolvencia que lo
    /// suspendió. Que esa insolvencia ya no esté en negociación lo comprueba el comando (<c>Lending.LegalProcess.InsolvencyNegotiating</c>).
    /// </summary>
    public void Reanudar(LegalProcessStatus hacia)
    {
        if (hacia is not (LegalProcessStatus.Assigned or LegalProcessStatus.Filed or LegalProcessStatus.InProgress))
            throw new ArgumentOutOfRangeException(nameof(hacia), hacia, "Un proceso se reanuda asignado, radicado o en curso.");
        if (Status != LegalProcessStatus.Suspended)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoEstadoInvalido, $"El proceso está en «{Status}»: sólo uno suspendido se reanuda.",
                new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() }));
        Status = hacia;
        SuspendedByInsolvencyId = null;
    }

    /// <summary>Cierra con resultado: <c>Concluded</c> o <c>Withdrawn</c>, con motivo.</summary>
    public void Cerrar(LegalProcessStatus resultado, string motivo, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (resultado is not (LegalProcessStatus.Concluded or LegalProcessStatus.Withdrawn))
            throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Un proceso se cierra concluido o retirado.");
        ReglaDeCarteraException.SiHay(PuedeIrA(resultado));
        Status = resultado;
        ClosedAt = instante;
        CloseReason = motivo.Trim();
    }

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// <c>LND_LegalProcessActions</c> (feature 013, K4, T0936; data-model §26.10): una actuación del proceso jurídico —auto, embargo, audiencia,
/// sentencia, honorarios, gasto…— con su soporte (<c>LendingLegalProcessDocument</c>). Si es un honorario o gasto que se cobra al deudor,
/// sigue la regla de gastos de cobranza (FR-121, T77): la operación <c>Charge</c> llega después, cuando el aviso <c>CollectionFeeNotice</c>
/// salió, y se escribe una sola vez (<see cref="ChargeOperationId"/>). Hecho de sólo inserción (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class LegalProcessAction : AuditableEntity, IHechoInmutable
{
    public int LegalProcessId { get; init; }

    public DateOnly ActionDate { get; init; }

    public LegalActionKind Kind { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal? Amount { get; init; }

    public bool ChargeableToDebtor { get; init; }

    /// <summary>La operación <c>Charge</c> que lo cobra al deudor; se escribe una sola vez.</summary>
    [EscrituraUnica]
    public long? ChargeOperationId { get; set; }

    public Guid? AttachmentPublicId { get; init; }

    public int RecordedByUserId { get; init; }

    public static LegalProcessAction Registrar(int procesoId, DateOnly fecha, LegalActionKind clase, string descripcion, int usuarioId,
        decimal? monto = null, bool cobrableAlDeudor = false, Guid? adjunto = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descripcion);
        if (!Enum.IsDefined(clase)) throw new ArgumentOutOfRangeException(nameof(clase), clase, "Clase de actuación desconocida.");
        if (monto is { } m && m <= 0m) throw new ArgumentOutOfRangeException(nameof(monto), monto, "El monto de una actuación es mayor que cero.");
        if (cobrableAlDeudor && (monto is null || clase is not (LegalActionKind.Fee or LegalActionKind.Expense)))
            throw new ArgumentException("Sólo un honorario o un gasto con monto se cobra al deudor.", nameof(cobrableAlDeudor));
        return new LegalProcessAction
        {
            LegalProcessId = procesoId,
            ActionDate = fecha,
            Kind = clase,
            Description = descripcion.Trim(),
            Amount = monto,
            ChargeableToDebtor = cobrableAlDeudor,
            AttachmentPublicId = adjunto,
            RecordedByUserId = usuarioId,
        };
    }
}
