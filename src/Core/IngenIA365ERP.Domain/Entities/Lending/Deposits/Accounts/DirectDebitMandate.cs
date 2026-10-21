using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_DirectDebitMandates</c> (feature 014, US4, T118; data-model §3.6; FR-090, D25, R16): la autorización del titular (o su representante) para que
/// el ciclo diario debite una cuenta de ahorro a la vista o contractual y pague, en la fecha exigible, la cuota de un crédito suyo o la de todos sus
/// créditos. Sin disponible no debita, salvo que admita el débito parcial. Estado con fechas: se revoca con motivo, nunca se borra; una vigente por cuenta
/// y crédito. La escribe quien tiene <c>Lending.DirectDebits.Manage</c>. (nuevo)
/// </summary>
public class DirectDebitMandate : AuditableEntity
{
    public const string CodigoNoEsTitular = "Lending.DirectDebit.NotHolder";
    public const string CodigoCuentaNoAdmitida = "Lending.DirectDebit.AccountNotDemand";
    public const string CodigoSeCruza = "Lending.DirectDebit.Overlaps";
    public const string CodigoNoEncontrada = "Lending.DirectDebit.NotFound";
    public const string CodigoYaRevocada = "Lending.DirectDebit.AlreadyRevoked";

    public const int LargoDelMotivo = 300;

    /// <summary>La cuenta que se debita (a la vista o contractual).</summary>
    public int MemberAccountId { get; set; }

    /// <summary>El titular o el representante que autoriza.</summary>
    public int AuthorizedByPersonId { get; set; }

    public DirectDebitScope Scope { get; set; }

    /// <summary>Obligatorio con <c>SingleLoan</c>.</summary>
    public int? LoanId { get; set; }

    /// <summary>Sin disponible suficiente: falso = no debita (FR-090).</summary>
    public bool AllowsPartialDebit { get; set; }

    /// <summary>El soporte firmado.</summary>
    public Guid AuthorizationAttachmentPublicId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public DateTime? RevokedAt { get; set; }

    public int? RevokedByUserId { get; set; }

    public string? RevokeReason { get; set; }

    /// <summary>¿Rige el <paramref name="dia"/>?</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && RevokedAt is null && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>¿Cubre <paramref name="credito"/>? (el suyo, o cualquiera del titular).</summary>
    public bool Cubre(int credito) => Scope == DirectDebitScope.AllLoansOfHolder || LoanId == credito;

    /// <summary>Revoca desde <paramref name="hasta"/> (incluido), con motivo y quién.</summary>
    public void Revocar(DateOnly hasta, DateTime instante, int? usuario, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (RevokedAt is not null) throw new InvalidOperationException("La autorización ya está revocada.");
        if (hasta < ValidFrom.AddDays(-1)) throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "No se revoca antes de empezar.");
        ValidTo = ValidTo is { } v && v < hasta ? v : hasta;
        RevokedAt = instante;
        RevokedByUserId = usuario;
        var texto = motivo.Trim();
        RevokeReason = texto.Length <= LargoDelMotivo ? texto : texto[..LargoDelMotivo];
    }
}
