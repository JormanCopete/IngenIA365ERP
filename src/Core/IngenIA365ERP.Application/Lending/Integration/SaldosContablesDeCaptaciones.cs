using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El saldo de captaciones y aportes por combinación (feature 014, US8, T146; contabilidad.md §8.1 de la 014; FR-100): la suma de las filas del libro de
/// cuentas (<c>LND_MemberAccountTransactions</c>) con <b>fecha contable</b> ≤ corte, con la combinación que la Cartera emite de la cuenta —línea, naturaleza,
/// clase, marca (embargada, exenta del GMF al corte o estándar), clase de aporte de cada concepto y la sucursal contable de su oficina—. Los nombres de los
/// roles son texto (la Cartera no conoce el catálogo de roles de Contabilidad, <c>CarteraNoConoceContabilidad</c>).
/// <list type="bullet">
/// <item>Concepto → rol: capital → <c>CapitalDeCaptacion</c> o, si la cuenta está inactiva, <c>CapitalDeCaptacionInactiva</c>; interés por pagar y
/// bonificación → <c>InteresPorPagar</c>; aportes de toda clase → <c>AportesSociales</c> con su clase; cargo por cobrar → <c>CargoDeCuentaPorCobrar</c>.</item>
/// <item>La retención y el GMF <b>no</b> se concilian aquí: en el libro de la cuenta sólo crecen (es lo que se le retuvo al titular), mientras la cuenta
/// contable baja cuando la cooperativa paga a la DIAN; esa conciliación es la de la declaración (research E109).</item>
/// <item>La marca y la inactividad son las de la cuenta <b>hoy</b> (la exención, la vigente al corte): si cambiaron después del corte, la diferencia la
/// explica la reclasificación (<c>CaptacionReclasificada</c>), que todavía no se emite (research E109).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class SaldosContablesDeCaptaciones(IApplicationDbContext db)
{
    public const string Capital = "CapitalDeCaptacion";
    public const string CapitalInactiva = "CapitalDeCaptacionInactiva";
    public const string InteresPorPagar = "InteresPorPagar";
    public const string Aportes = "AportesSociales";
    public const string CargoPorCobrar = "CargoDeCuentaPorCobrar";

    /// <summary>Los roles de saldo de captaciones que la Cartera concilia.</summary>
    public static IReadOnlyList<string> RolesDelLibro { get; } = [Capital, CapitalInactiva, InteresPorPagar, Aportes, CargoPorCobrar];

    /// <summary>Los de naturaleza crédito (su saldo contable, débito − crédito, es negativo).</summary>
    public static IReadOnlySet<string> NaturalezaCredito { get; } = new HashSet<string>(StringComparer.Ordinal) { Capital, CapitalInactiva, InteresPorPagar, Aportes };

    private static readonly MemberAccountConcept[] Conciliados =
    [
        MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.Bonus, MemberAccountConcept.ContributionMinimum,
        MemberAccountConcept.ContributionOrdinary, MemberAccountConcept.ContributionExtraordinary, MemberAccountConcept.ContributionRevaluation,
        MemberAccountConcept.ChargeReceivable,
    ];

    /// <summary>El rol de saldo de un concepto del libro de cuentas (contabilidad.md §2 de la 014); nulo si no se concilia.</summary>
    public static string? RolDelConcepto(MemberAccountConcept concepto, bool inactiva) => concepto switch
    {
        MemberAccountConcept.Principal => inactiva ? CapitalInactiva : Capital,
        MemberAccountConcept.InterestPayable or MemberAccountConcept.Bonus => InteresPorPagar,
        MemberAccountConcept.ContributionMinimum or MemberAccountConcept.ContributionOrdinary or MemberAccountConcept.ContributionExtraordinary
            or MemberAccountConcept.ContributionRevaluation => Aportes,
        MemberAccountConcept.ChargeReceivable => CargoPorCobrar,
        _ => null,
    };

    /// <summary>
    /// La combinación de un concepto de una cuenta (contabilidad.md §8.1 de la 014): el rol del concepto, la línea, la naturaleza, la clase (salvo en aportes),
    /// la clase de aporte, la marca (sólo en el capital) y la sucursal contable de la oficina; nula si el concepto no se concilia. La comparten la conciliación
    /// y el cuadre de la migración (US9, T154), así los dos preguntan a Contabilidad por la misma combinación.
    /// </summary>
    public static ClaveDeSaldoDeCartera? Clave(MemberAccountConcept concepto, bool inactiva, string linea, LineNature naturaleza, DepositClass? clase,
        AccountFlag marca, Guid sucursal)
    {
        var rol = RolDelConcepto(concepto, inactiva);
        if (rol is null) return null;
        var marcaDelRol = rol == Capital ? marca.ToString() : null;
        var claseDeAporte = rol == Aportes ? EmisionDeCartera.ClaseDeAporte(concepto)?.ToString() : null;
        return new ClaveDeSaldoDeCartera(rol, linea, null, null, null, null, null, null, null, null, null, sucursal,
            naturaleza.ToString(), rol == Aportes ? null : clase?.ToString(), null, claseDeAporte, marcaDelRol);
    }

    /// <summary>Los movimientos del libro de cuentas hasta el corte con su combinación (por fecha contable), dentro del alcance si viene.</summary>
    public async Task<IReadOnlyList<MovimientoDeCombinacionDeCartera>> MovimientosAsync(DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var filas = await (from t in db.MemberAccountTransactions.AsNoTracking()
                           join o in db.MemberAccountOperations.AsNoTracking() on t.OperationId equals (long?)o.Id into ops
                           from o in ops.DefaultIfEmpty()
                           where t.AccountingDate <= corte && !t.IsDeleted && Conciliados.Contains(t.Concept)
                           select new { t.MemberAccountId, t.Concept, t.Amount, t.AccountingDate, t.MessagePublicId, Operacion = o == null ? (Guid?)null : o.PublicId })
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        var ids = filas.Select(f => f.MemberAccountId).Distinct().ToArray();
        var cuentas = await (from a in db.MemberAccounts.AsNoTracking()
                             join l in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on a.CreditLineId equals l.Id
                             join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on a.BranchId equals b.Id
                             where ids.Contains(a.Id)
                             select new { a.Id, a.BranchId, a.CreditLineId, Linea = l.Code, a.Nature, a.DepositClass, a.Status, a.IsGarnished, Sucursal = b.PublicId })
            .ToListAsync(ct);
        if (alcance is not null) cuentas = cuentas.Where(c => alcance.IncluyeOficina(c.BranchId) && alcance.IncluyeLinea(c.CreditLineId)).ToList();
        var exentas = (await db.GmfExemptAccounts.AsNoTracking()
                .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && g.ValidFrom <= corte && (g.ValidTo == null || g.ValidTo >= corte))
                .Select(g => g.MemberAccountId).ToListAsync(ct))
            .ToHashSet();
        var porId = cuentas.ToDictionary(c => c.Id);

        var resultado = new List<MovimientoDeCombinacionDeCartera>(filas.Count);
        foreach (var f in filas)
        {
            if (!porId.TryGetValue(f.MemberAccountId, out var c)) continue;
            var rol = RolDelConcepto(f.Concept, c.Status == MemberAccountStatus.Inactive);
            if (rol is null) continue;
            var marca = c.IsGarnished ? AccountFlag.Garnished : exentas.Contains(c.Id) ? AccountFlag.GmfExempt : AccountFlag.Standard;
            var clave = Clave(f.Concept, c.Status == MemberAccountStatus.Inactive, c.Linea, c.Nature, c.DepositClass, marca, c.Sucursal)!;
            resultado.Add(new MovimientoDeCombinacionDeCartera(clave, c.Id, f.Amount, f.AccountingDate, f.MessagePublicId, f.Operacion));
        }
        return resultado;
    }
}
