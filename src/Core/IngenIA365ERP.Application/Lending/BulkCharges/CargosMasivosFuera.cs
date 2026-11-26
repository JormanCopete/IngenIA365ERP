using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>
/// Qué obligaciones de cargo masivo quedan fuera de centrales y de cobranza (2026-11-25, feature 026, D4): las de origen <c>BulkCharge</c> cuya versión de
/// línea con que nacieron (sus primeras condiciones) no dice «se reporta a centrales» o «entra a cobranza». De fábrica, ninguna va. Consultas que se
/// componen en las de centrales y cobranza, sin leer nada aparte. (nuevo)
/// </summary>
public static class CargosMasivosFuera
{
    /// <summary>Los créditos de cargo masivo que no se reportan a centrales.</summary>
    public static IQueryable<int> DeCentrales(IApplicationDbContext db) =>
        from l in db.Loans
        where l.Origin == LoanOrigin.BulkCharge
              && !(from t in db.LoanTermsVersions
                   join v in db.CreditLineVersions on t.CreditLineVersionId equals v.Id
                   where t.LoanId == l.Id && t.VersionNumber == 1 && v.BulkChargeReportsToBureaus
                   select t.Id).Any()
        select l.Id;

    /// <summary>Los créditos de cargo masivo que no entran a cobranza.</summary>
    public static IQueryable<int> DeCobranza(IApplicationDbContext db) =>
        from l in db.Loans
        where l.Origin == LoanOrigin.BulkCharge
              && !(from t in db.LoanTermsVersions
                   join v in db.CreditLineVersions on t.CreditLineVersionId equals v.Id
                   where t.LoanId == l.Id && t.VersionNumber == 1 && v.BulkChargeEntersCollection
                   select t.Id).Any()
        select l.Id;
}
