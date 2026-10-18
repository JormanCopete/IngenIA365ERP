using System.Globalization;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>El estado de una cuota exigible de una cuenta (data-model §3.5; api.md §22): se calcula al leer, nunca se guarda. (nuevo)</summary>
public enum EstadoDeCuotaDeCuenta
{
    Pending = 1,
    PartiallyPaid = 2,
    Paid = 3,
    NotDeducted = 4,
    Cancelled = 5,
}

/// <summary>
/// <c>LND_AccountInstallments</c> (feature 014, US3, T081; data-model §3.5; FR-040, FR-087, FR-089, D12): la cuota exigible de una cuenta de aportes, de
/// ahorro permanente o del contractual, por período. <b>No es crédito</b>: sin mora, calificación ni deterioro. Lo pagado de la cuota es la suma de las
/// filas del libro con su <c>InstallmentId</c> y su estado (<see cref="EstadoDeCuotaDeCuenta"/>) se calcula al leer, como la cuota del crédito. La cuota
/// combinada de un fondo de empleados reparte su valor entre aportes y ahorro permanente (FR-049): la de aportes lleva su parte en <see cref="Amount"/>
/// y las dos partes en <see cref="ContributionShare"/> y <see cref="PermanentSavingsShare"/>; la del permanente es otra cuota, de la cuenta permanente,
/// con <see cref="LinkedInstallmentId"/> = la de aportes. Hecho de sólo inserción (<see cref="IHechoInmutable"/>, <c>bigint</c>) que escribe
/// <c>RegistroDeCuentas</c>; sólo la anulación (<see cref="CancelledAt"/> y compañía) se escribe una vez. Único por cuenta y período. (nuevo)
/// </summary>
public class AccountInstallment : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Largo del motivo de la anulación (<c>nvarchar(300)</c>).</summary>
    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; init; }

    /// <summary>Navegación para nacer en el mismo guardado que una cuenta todavía sin Id.</summary>
    public MemberAccount? MemberAccount { get; init; }

    public short PeriodYear { get; init; }

    public byte PeriodMonth { get; init; }

    /// <summary>1 en las mensuales; el subperíodo del mes en las quincenales, decadales y semanales.</summary>
    public byte SubPeriodNumber { get; init; } = 1;

    public DateOnly DueDate { get; init; }

    /// <summary>Lo exigible de la cuota en esta cuenta; mayor que cero.</summary>
    public decimal Amount { get; init; }

    /// <summary>La parte de aportes de una cuota combinada (sólo en la de aportes).</summary>
    public decimal? ContributionShare { get; init; }

    /// <summary>La parte del ahorro permanente de una cuota combinada (sólo en la de aportes).</summary>
    public decimal? PermanentSavingsShare { get; init; }

    /// <summary>La cuota de aportes de la que es hermana la del ahorro permanente.</summary>
    public long? LinkedInstallmentId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado una cuota hermana con la de aportes todavía sin Id.</summary>
    public AccountInstallment? LinkedInstallment { get; init; }

    /// <summary>La corrida que la generó (el paso de captaciones); excluyente con <see cref="GeneratedByOperationId"/>.</summary>
    public int? GeneratedByProcessRunId { get; init; }

    /// <summary>La operación que la generó (apertura, migración); excluyente con <see cref="GeneratedByProcessRunId"/>.</summary>
    public long? GeneratedByOperationId { get; init; }

    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>La huella de lo que la generó: la versión de la línea, el modo, la base y el reparto.</summary>
    public string InputsSha256 { get; init; } = string.Empty;

    /// <summary>Cuota futura anulada por retiro, cancelación o cambio de cuota; se escribe una vez.</summary>
    [EscrituraUnica]
    public DateTime? CancelledAt { get; set; }

    [EscrituraUnica]
    public string? CancelReason { get; set; }

    [EscrituraUnica]
    public long? CancelledByOperationId { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>
    /// El período como texto (contrato-de-cuentas §3.6): <c>AAAA-MM</c> en el primer subperíodo del mes y <c>AAAA-MM-s</c> en los siguientes (la
    /// quincena 2, la década 3).
    /// </summary>
    public string Periodo => Texto(PeriodYear, PeriodMonth, SubPeriodNumber, conSubperiodo: SubPeriodNumber > 1);

    /// <summary>¿Está anulada?</summary>
    public bool Anulada => CancelledAt is not null;

    /// <summary>El período como texto ordenable.</summary>
    public static string Texto(int anio, int mes, int subperiodo, bool conSubperiodo) => conSubperiodo
        ? string.Create(CultureInfo.InvariantCulture, $"{anio:D4}-{mes:D2}-{subperiodo}")
        : string.Create(CultureInfo.InvariantCulture, $"{anio:D4}-{mes:D2}");

    /// <summary>Lo que falta de la cuota (nunca negativo) dado lo pagado.</summary>
    public decimal Pendiente(decimal pagado) => Anulada ? 0m : Math.Max(0m, Amount - pagado);

    /// <summary>
    /// El estado a partir de lo pagado y de si un recaudador informó que no la descontó (data-model §3.5): anulada, pagada, parcialmente pagada, no
    /// descontada (sin nada pagado y con un no descontado informado) o pendiente.
    /// </summary>
    public EstadoDeCuotaDeCuenta Estado(decimal pagado, bool conNoDescontado)
    {
        if (Anulada) return EstadoDeCuotaDeCuenta.Cancelled;
        if (pagado >= Amount) return EstadoDeCuotaDeCuenta.Paid;
        if (pagado > 0m) return EstadoDeCuotaDeCuenta.PartiallyPaid;
        return conNoDescontado ? EstadoDeCuotaDeCuenta.NotDeducted : EstadoDeCuotaDeCuenta.Pending;
    }

    /// <summary>Anula una cuota futura (retiro, cancelación, cambio de cuota): una vez, con motivo.</summary>
    public void Anular(DateTime instante, string motivo, long? operacionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Anulada) throw new InvalidOperationException($"La cuota {Periodo} de la cuenta {MemberAccountId} ya está anulada.");
        CancelledAt = instante;
        var texto = motivo.Trim();
        CancelReason = texto.Length <= LargoDelMotivo ? texto : texto[..LargoDelMotivo];
        CancelledByOperationId = operacionId;
    }
}
