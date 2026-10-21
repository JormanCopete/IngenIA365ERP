using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_CollectionStageHistory</c> (feature 013, K4, T0936; data-model §26.5; FR-117): la entrada de un crédito a una etapa —la versión de
/// la etapa y su código, el día, los días de mora al entrar y de dónde vino: el ciclo diario (paso 7, con su corrida), una persona con motivo
/// o el registro de la etapa migrada al desplegar K4 (C64)—. La etapa vigente es la última entrada, salvo que sea una <b>salida</b>
/// (<see cref="FuenteSalida"/>, K4 bloque D): el ciclo la escribe cuando el crédito se pone al día y no le toca una etapa preventiva; lleva la etapa de
/// la que sale y deja al crédito fuera de cobranza hasta que vuelva a entrar, como episodio nuevo. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>). (nuevo)
/// </summary>
public class CollectionStageHistoryEntry : AuditableEntity, IHechoInmutable
{
    public const string FuenteCicloDiario = "DailyCycle";
    public const string FuenteManual = "Manual";
    public const string FuenteMigracion = "Migration";

    /// <summary>K4, bloque D: la salida de cobranza del crédito que se puso al día (la escribe el ciclo diario, con su corrida). (nuevo)</summary>
    public const string FuenteSalida = "Exit";

    /// <summary>El motivo de la salida por pago al día.</summary>
    public const string MotivoSalidaAlDia = "Salida por pago al día";

    /// <summary>Los valores cerrados de <see cref="Source"/> (data-model «Valores de texto cerrados»).</summary>
    public static IReadOnlyList<string> Fuentes { get; } = [FuenteCicloDiario, FuenteManual, FuenteMigracion, FuenteSalida];

    /// <summary>¿Es la salida de cobranza? Entonces el crédito no tiene etapa vigente.</summary>
    public bool EsSalida => Source == FuenteSalida;

    public int LoanId { get; init; }

    /// <summary>La versión de la etapa.</summary>
    public int CollectionStageId { get; init; }

    public string StageCode { get; init; } = string.Empty;

    public DateOnly EnteredOn { get; init; }

    public int DaysPastDueAtEntry { get; init; }

    public string Source { get; init; } = string.Empty;

    public int? ProcessRunId { get; init; }

    public int? ActorUserId { get; init; }

    public string? Reason { get; init; }

    public int? PreviousEntryId { get; init; }

    /// <summary>
    /// La entrada, validada: la fuente es una de las cerradas; una manual lleva persona y motivo; el ciclo y la migración, su corrida
    /// (<c>LND_ProcessRuns</c>).
    /// </summary>
    public static CollectionStageHistoryEntry Registrar(int creditoId, CollectionStage etapa, DateOnly dia, int diasDeMora, string fuente,
        int? corridaId = null, int? usuarioId = null, string? motivo = null, int? anteriorId = null)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        if (!Fuentes.Contains(fuente, StringComparer.Ordinal))
            throw new ArgumentException($"«{fuente}» no es una fuente de entrada a etapa.", nameof(fuente));
        if (fuente == FuenteManual && (usuarioId is null || string.IsNullOrWhiteSpace(motivo)))
            throw new ArgumentException("Un cambio manual de etapa lleva la persona y el motivo.", nameof(motivo));
        if (fuente != FuenteManual && corridaId is null)
            throw new ArgumentException("El ciclo diario y el registro de la etapa migrada llevan su corrida.", nameof(corridaId));
        return new CollectionStageHistoryEntry
        {
            LoanId = creditoId,
            CollectionStageId = etapa.Id,
            StageCode = etapa.Code,
            EnteredOn = dia,
            DaysPastDueAtEntry = diasDeMora,
            Source = fuente,
            ProcessRunId = corridaId,
            ActorUserId = usuarioId,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            PreviousEntryId = anteriorId,
        };
    }

    /// <summary>
    /// La salida de cobranza (K4, bloque D): el crédito deja la etapa de <paramref name="vigente"/> el <paramref name="dia"/>, con los días que tiene (cero o
    /// negativos), la corrida del ciclo y el motivo. Lleva la etapa de la que sale para que la historia diga de dónde.
    /// </summary>
    public static CollectionStageHistoryEntry Salir(CollectionStageHistoryEntry vigente, DateOnly dia, int dias, int corridaId, string motivo)
    {
        ArgumentNullException.ThrowIfNull(vigente);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (vigente.EsSalida) throw new ArgumentException("El crédito ya salió de cobranza.", nameof(vigente));
        return new CollectionStageHistoryEntry
        {
            LoanId = vigente.LoanId,
            CollectionStageId = vigente.CollectionStageId,
            StageCode = vigente.StageCode,
            EnteredOn = dia,
            DaysPastDueAtEntry = dias,
            Source = FuenteSalida,
            ProcessRunId = corridaId,
            Reason = motivo.Trim(),
            PreviousEntryId = vigente.Id > 0 ? vigente.Id : null,
        };
    }
}
