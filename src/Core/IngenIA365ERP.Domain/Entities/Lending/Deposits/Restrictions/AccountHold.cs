using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;

/// <summary>
/// <c>LND_AccountHolds</c> (feature 014, US11, T187; data-model §5.1; FR-054, FR-057): un bloqueo de una cuenta por motivo, por valor o total
/// (<see cref="Amount"/> nulo), con vigencia. El disponible resta los vigentes (FR-057): los de motivo <c>Garnishment</c> como «embargado», los demás como
/// «bloqueado». Estado con fechas: nunca se borra, se levanta (<see cref="Levantar"/>) y queda la historia. Lo que retiene un embargo lo escribe sólo
/// <c>RetencionDeEmbargos</c> (un recálculo levanta la fila anterior y deja otra con el valor nuevo); los demás, <c>Lending.Holds.Manage</c>. Un bloqueo
/// <c>LoanCollateral</c> sólo se levanta con el crédito pagado o sin la garantía. (nuevo)
/// </summary>
public class AccountHold : AuditableEntity
{
    public const int LargoDeLasNotas = 500;
    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; set; }

    public HoldReason Reason { get; set; }

    /// <summary>Nulo = bloqueo total (no admite retiros ni cancelación).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Con <c>LoanCollateral</c>: el crédito de la Cartera que garantiza.</summary>
    public int? LoanId { get; set; }

    /// <summary>Con <c>LoanCollateral</c>, opcional: la garantía registrada en la originación.</summary>
    public int? CollateralId { get; set; }

    /// <summary>Obligatorio con <c>Garnishment</c>.</summary>
    public int? GarnishmentId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? Notes { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    /// <summary>Nulo = el proceso (el recálculo del embargo en el ciclo diario o la migración).</summary>
    public int? PlacedByUserId { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public int? ReleasedByUserId { get; set; }

    public string? ReleaseReason { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>¿Rige el <paramref name="dia"/>? Sin levantar y dentro de sus fechas.</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ReleasedAt is null && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>¿Es total? (sin valor).</summary>
    public bool EsTotal => Amount is null;

    /// <summary>Un bloqueo nuevo. El de un embargo nombra su embargo; el de garantía, su crédito.</summary>
    public static AccountHold Poner(int cuentaId, HoldReason motivo, decimal? valor, DateOnly desde, DateOnly? hasta, string? notas, Guid? soporte,
        int? usuario, int? garnishmentId = null, int? loanId = null, int? collateralId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cuentaId, 1);
        if (valor is <= 0m) throw new ArgumentOutOfRangeException(nameof(valor), valor, "Un bloqueo por valor es mayor que cero.");
        if (hasta is { } h && h < desde) throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "El bloqueo no termina antes de empezar.");
        if (motivo == HoldReason.Garnishment && garnishmentId is null)
            throw new ArgumentException("El bloqueo de un embargo nombra su embargo.", nameof(garnishmentId));
        if (motivo != HoldReason.Garnishment && garnishmentId is not null)
            throw new ArgumentException("Sólo el bloqueo de un embargo nombra un embargo.", nameof(garnishmentId));
        if (motivo == HoldReason.LoanCollateral && loanId is null)
            throw new ArgumentException("El bloqueo de garantía nombra el crédito que garantiza.", nameof(loanId));
        return new AccountHold
        {
            MemberAccountId = cuentaId,
            Reason = motivo,
            Amount = valor,
            ValidFrom = desde,
            ValidTo = hasta,
            Notes = Recortar(notas, LargoDeLasNotas),
            SupportAttachmentPublicId = soporte,
            PlacedByUserId = usuario,
            GarnishmentId = garnishmentId,
            LoanId = loanId,
            CollateralId = collateralId,
        };
    }

    /// <summary>Levanta el bloqueo (una vez): queda la fila con quién, cuándo y por qué.</summary>
    public void Levantar(DateTime instante, int? usuario, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (ReleasedAt is not null) throw new InvalidOperationException("El bloqueo ya se levantó.");
        ReleasedAt = instante;
        ReleasedByUserId = usuario;
        ReleaseReason = Recortar(motivo, LargoDelMotivo);
    }

    private static string? Recortar(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
