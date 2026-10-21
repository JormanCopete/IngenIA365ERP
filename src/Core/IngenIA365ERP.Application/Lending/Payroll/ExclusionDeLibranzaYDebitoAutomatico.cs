using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll;

/// <summary>
/// Un crédito se paga por una cosa o por la otra (feature 014, decisión del dueño del 2026-10-21, E249): un crédito con <b>libranza o nómina propia</b>
/// (vínculo vigente con pagaduría, <c>LoanPayrollLink.ConLibranza</c>) no puede tener <b>débito automático</b> desde una cuenta, y uno con débito
/// automático vigente para él no puede pasar a libranza o nómina. La regla vive aquí, en un solo sitio, y la usan las dos puertas:
/// el registro de la autorización (<c>RegisterDirectDebitMandateCommand</c>) y el vínculo de libranza (<c>TransferLoanPayrollCommand</c>); y el ciclo del
/// débito automático la aplica también a las autorizaciones «todos mis créditos», que no nombran un crédito y por eso no se pueden rechazar al registrarlas:
/// ahí el crédito con libranza simplemente se salta. Con esto <c>TodosLosCanales</c> del débito automático no puede duplicar un descuento de nómina. (nuevo)
/// </summary>
public static class ExclusionDeLibranzaYDebitoAutomatico
{
    public const string CodigoCreditoConLibranza = "Lending.DirectDebit.LoanOnPayroll";
    public const string CodigoCreditoConDebitoAutomatico = "Lending.Payroll.LoanHasDirectDebit";

    /// <summary>Los créditos (de <paramref name="creditos"/>) con un vínculo de libranza o nómina vigente.</summary>
    public static async Task<HashSet<int>> ConLibranzaAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos, CancellationToken ct)
    {
        if (creditos.Count == 0) return [];
        var ids = creditos.ToArray();
        var vinculados = await db.LoanPayrollLinks.AsNoTracking()
            .Where(v => ids.Contains(v.LoanId) && !v.IsDeleted && v.ValidTo == null
                        && (v.RepaymentChannel == RepaymentChannel.OwnPayroll || v.RepaymentChannel == RepaymentChannel.PaymasterDeduction))
            .Select(v => v.LoanId).Distinct().ToListAsync(ct);
        return [.. vinculados];
    }

    /// <summary>El número de la cuenta con la autorización de débito automático vigente (hoy o más adelante) que nombra este crédito; nulo si no hay.</summary>
    public static async Task<string?> CuentaConDebitoAutomaticoAsync(IApplicationDbContext db, int credito, DateOnly hoy, CancellationToken ct)
    {
        var cuenta = await db.DirectDebitMandates.AsNoTracking()
            .Where(m => m.LoanId == credito && m.Scope == DirectDebitScope.SingleLoan && !m.IsDeleted && m.RevokedAt == null && (m.ValidTo == null || m.ValidTo >= hoy))
            .OrderBy(m => m.Id).Select(m => (int?)m.MemberAccountId).FirstOrDefaultAsync(ct);
        if (cuenta is not { } id) return null;
        return await db.MemberAccounts.AsNoTracking().Where(a => a.Id == id).Select(a => a.Number).FirstOrDefaultAsync(ct) ?? "—";
    }

    public static Error CreditoConLibranza(string credito) => new ErrorConDatos(CodigoCreditoConLibranza,
        $"El crédito {credito} se paga por libranza o nómina: no puede tener débito automático. Un crédito se paga por una cosa o por la otra; para debitarlo de una "
        + "cuenta, primero se pasa a otra forma de pago.", new { loan = credito });

    public static Error CreditoConDebitoAutomatico(string credito, string cuenta) => new ErrorConDatos(CodigoCreditoConDebitoAutomatico,
        $"El crédito {credito} tiene un débito automático vigente desde la cuenta {cuenta}: no puede pasar a libranza o nómina. Revoque primero esa autorización; un "
        + "crédito se paga por una cosa o por la otra.", new { loan = credito, account = cuenta });
}
