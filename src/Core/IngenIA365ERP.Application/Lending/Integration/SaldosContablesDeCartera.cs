using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La combinación de Cartera de un saldo (contabilidad.md §2.2, §14.2): el rol de saldo y las dimensiones con que Cartera lo informó, con la
/// sucursal contable. Las dimensiones que el rol no admite viajan igual (la matriz las ignora). (nuevo)
/// </summary>
public sealed record ClaveDeSaldoDeCartera(
    string Rol,
    string? Linea,
    string? Modalidad,
    string? Subsegmento,
    string? Categoria,
    string? Concepto,
    bool? Empleado,
    bool? Gracia,
    bool? Reestructurado,
    string? ClaseDeRecaudador,
    string? Motivo,
    Guid Sucursal)
{
    /// <summary>La <see cref="CombinacionDeSaldo"/> del puerto (§16.1): el hecho es siempre <c>*</c>.</summary>
    public CombinacionDeSaldo ComoCombinacion() => new(Rol, Linea, Modalidad, Subsegmento, Categoria, Concepto, Empleado, Gracia, null, Reestructurado,
        ClaseDeRecaudador, Motivo, Sucursal);
}

/// <summary>El saldo de Cartera de una combinación a un corte, en el sentido de Cartera (positivo = lo que Cartera tiene), con sus créditos. (nuevo)</summary>
public sealed record SaldoDeCombinacionDeCartera(ClaveDeSaldoDeCartera Clave, decimal Saldo, IReadOnlyList<int> Creditos, DateOnly UltimoMovimiento);

/// <summary>Una fila del libro de un crédito, ya con su combinación (para la conciliación y la explicación por hecho). (nuevo)</summary>
public sealed record MovimientoDeCombinacionDeCartera(ClaveDeSaldoDeCartera Clave, int LoanId, decimal Monto, DateOnly FechaContable, Guid? Mensaje, Guid? Operacion);

/// <summary>
/// El saldo de Cartera por combinación (feature 013, US7, T0658; contracts/contabilidad.md §14.2 y §14.3; T58, C51): la suma de las filas del
/// libro (<c>LND_LoanTransactions</c>) con <b>fecha contable</b> ≤ corte, con la combinación que Cartera informa del crédito —línea,
/// modalidad, sub-segmento, categoría contable, empleado, reestructurado y la sucursal contable de su oficina; gracia en falso, como la emite
/// <see cref="EmisionDeCartera"/>—. Como cada cambio de combinación sale en un <c>DimensionContableCambiada</c> o una reclasificación, la
/// combinación vigente al corte es la del último hecho que movió o reclasificó ese saldo, y lo que todavía no pasó lo explica la conciliación.
/// <list type="bullet">
/// <item>Concepto → rol (§2.2): <c>Principal</c> → <c>Capital</c>; intereses → <c>InteresPorCobrar</c>; en suspenso → <c>SuspensoEnOrden</c>, o
/// <c>CastigadaEnOrden</c> si la fila está en control; en un crédito castigado al corte, todo el activo en <c>CastigadaEnOrden</c>; seguro → <c>PagoPorCuentaDelAsociado</c>; cobranza y otros cargos → <c>CargoPorCobrar</c>
/// (el libro no guarda «por cuenta del asociado»); anticipo → <c>Anticipo</c>; saldo a favor → <c>SaldoAFavor</c>. El deterioro llega con el
/// cierre de Cartera (fase 12).</item>
/// <item><c>DesembolsoPorGirar</c>: las órdenes de giro emitidas (no en especie) sin «girada» ni anulación y sin reemisión, por forma, línea y
/// sucursal.</item>
/// <item>Los puentes (<c>RecaudoPorAplicar</c> por clase, <c>PuenteComercial</c>, <c>CuentaPorCobrarComercial</c>) esperan cero: una combinación por
/// clase o motivo en la sucursal principal (el conjunto de cuentas reúne todas las sucursales).</item>
/// </list>
/// Sin alcance de oficinas cuando lo pide otro módulo (<see cref="DimensionesDeCartera"/>, T62); la conciliación pasa el de la petición. El
/// cuadre de activación (§14.3) arma sus combinaciones con <see cref="Clave"/> y <see cref="RolDelConcepto"/>, el mismo cálculo. (nuevo)
/// </summary>
public sealed class SaldosContablesDeCartera(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
{
    /// <summary>Los roles de saldo que Cartera concilia en K1, en el orden de §14.2.</summary>
    public static IReadOnlyList<string> RolesDelLibro { get; } =
        ["Capital", "InteresPorCobrar", "PagoPorCuentaDelAsociado", "CargoPorCobrar", "SuspensoEnOrden", "CastigadaEnOrden", "Anticipo", "SaldoAFavor"];

    /// <summary>Los puentes, que esperan cero (o las órdenes abiertas, en <c>DesembolsoPorGirar</c>).</summary>
    public static IReadOnlyList<string> Puentes { get; } = ["RecaudoPorAplicar", "DesembolsoPorGirar", "PuenteComercial", "CuentaPorCobrarComercial"];

    /// <summary>Los roles de naturaleza crédito entre los que Cartera concilia (§2.2): su saldo contable (débito − crédito) es negativo.</summary>
    public static IReadOnlySet<string> NaturalezaCredito { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "Anticipo", "SaldoAFavor", "DesembolsoPorGirar", "PuenteComercial", "CuentaPorCobrarComercial", "DeterioroIndividual", "DeterioroGeneral",
    };

    /// <summary>Los roles cuya regla exige el concepto (§2.2): la combinación lo lleva; en los demás va nulo.</summary>
    private static readonly HashSet<string> ConConcepto = new(StringComparer.Ordinal)
    {
        "InteresPorCobrar", "PagoPorCuentaDelAsociado", "CargoPorCobrar", "SuspensoEnOrden", "CastigadaEnOrden",
    };

    /// <summary>+1 en los roles de naturaleza débito, −1 en los de crédito: el saldo contable (débito − crédito) por éste es el de Cartera.</summary>
    public static int Signo(string rol) => NaturalezaCredito.Contains(rol) ? -1 : 1;

    /// <summary>
    /// El rol de saldo de un concepto del libro (§2.2, «de concepto a rol»). En un crédito <paramref name="castigado"/> al corte, todo lo del activo
    /// —capital, intereses, suspenso, seguro y cargos— está en <c>CastigadaEnOrden</c> con su concepto (contabilidad.md §2.1, <c>CreditoCastigado</c>,
    /// y el ejemplo 14: el castigo lo saca de su rol y lo lleva a control; la recuperación sale de ahí); los pasivos (anticipo, saldo a favor) no
    /// cambian. Hasta el 2026-10-15 la conciliación dejaba el capital y el interés del castigado en <c>Capital</c> e <c>InteresPorCobrar</c> y cada
    /// castigado (también los migrados) salía como diferencia.
    /// </summary>
    public static string RolDelConcepto(LoanConcept concepto, bool enControl, bool castigado = false) => concepto switch
    {
        LoanConcept.Principal or LoanConcept.CurrentInterest or LoanConcept.DefaultInterest or LoanConcept.CurrentInterestInSuspense
            or LoanConcept.DefaultInterestInSuspense or LoanConcept.Insurance or LoanConcept.CollectionFee or LoanConcept.OtherCharge
            when castigado => "CastigadaEnOrden",
        LoanConcept.Principal => "Capital",
        LoanConcept.CurrentInterest or LoanConcept.DefaultInterest => "InteresPorCobrar",
        LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense => enControl ? "CastigadaEnOrden" : "SuspensoEnOrden",
        LoanConcept.Insurance => "PagoPorCuentaDelAsociado",
        LoanConcept.CollectionFee or LoanConcept.OtherCharge => "CargoPorCobrar",
        LoanConcept.Advance => "Anticipo",
        LoanConcept.CreditBalance => "SaldoAFavor",
        _ => throw new ArgumentOutOfRangeException(nameof(concepto), concepto, "Concepto del libro sin rol de saldo."),
    };

    /// <summary>La combinación de un saldo de un crédito: el concepto sólo en los roles que lo exigen.</summary>
    public static ClaveDeSaldoDeCartera Clave(DimensionesDeCreditoDeCartera d, string rol, LoanConcept concepto) =>
        new(rol, d.Linea, d.Modalidad, d.Subsegmento, d.Categoria, ConConcepto.Contains(rol) ? concepto.ToString() : null, d.Empleado, false,
            d.Reestructurado, null, null, d.Sucursal);

    /// <summary>
    /// Los movimientos del libro hasta el corte con su combinación (por fecha contable). <paramref name="alcance"/> nulo: todas las oficinas y
    /// líneas (los puertos para otros módulos no aplican el alcance).
    /// </summary>
    public async Task<IReadOnlyList<MovimientoDeCombinacionDeCartera>> MovimientosAsync(DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var filas = await (from t in db.LoanTransactions.AsNoTracking()
                           join o in db.LoanOperations.AsNoTracking() on t.OperationId equals (long?)o.Id into ops
                           from o in ops.DefaultIfEmpty()
                           where t.AccountingDate <= corte
                           select new { t.LoanId, t.Concept, t.InControl, t.Amount, t.AccountingDate, t.MessagePublicId, Operacion = o == null ? (Guid?)null : o.PublicId })
            .ToListAsync(ct);
        if (filas.Count == 0) return [];

        var dimensiones = await DimensionesAsync(filas.Select(f => f.LoanId).Distinct().ToList(), corte, alcance, ct);
        return filas.Where(f => dimensiones.ContainsKey(f.LoanId))
            .Select(f => new MovimientoDeCombinacionDeCartera(Clave(dimensiones[f.LoanId], RolDelConcepto(f.Concept, f.InControl, dimensiones[f.LoanId].Castigado), f.Concept), f.LoanId,
                f.Amount, f.AccountingDate, f.MessagePublicId, f.Operacion))
            .ToList();
    }

    /// <summary>Los saldos de Cartera por combinación al corte (los del libro, las órdenes abiertas y los puentes en cero).</summary>
    public async Task<IReadOnlyList<SaldoDeCombinacionDeCartera>> PorCombinacionAsync(DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var movimientos = await MovimientosAsync(corte, alcance, ct);
        var saldos = Agrupar(movimientos).ToList();
        saldos.AddRange(await OrdenesAbiertasAsync(corte, alcance, ct));
        saldos.AddRange(await PuentesEnCeroAsync(corte, ct));
        return saldos;
    }

    /// <summary>Agrupa movimientos por combinación (las combinaciones en cero sin movimiento no se devuelven).</summary>
    public static IEnumerable<SaldoDeCombinacionDeCartera> Agrupar(IEnumerable<MovimientoDeCombinacionDeCartera> movimientos) =>
        movimientos.GroupBy(m => m.Clave)
            .Select(g => new SaldoDeCombinacionDeCartera(g.Key, g.Sum(m => m.Monto), g.Select(m => m.LoanId).Distinct().Order().ToList(),
                g.Max(m => m.FechaContable)));

    /// <summary>
    /// ¿Resuelve la regla <c>RecaudoPorAplicar</c> de la clase <paramref name="clase"/> al corte? (§14.3: la activación exige la de
    /// <c>Payroll</c>.) Lo responde Contabilidad: la combinación no está en <c>UnresolvedCombinations</c>.
    /// </summary>
    public async Task<Result<bool>> PuenteResuelveAsync(ClaseDeRecaudador clase, DateOnly corte, CancellationToken ct)
    {
        var principal = await SucursalPrincipalAsync(ct);
        if (principal is not { } sucursal) return Result.Success(false);
        var clave = new ClaveDeSaldoDeCartera("RecaudoPorAplicar", null, null, null, null, null, null, null, null, clase.ToString(), null, sucursal);
        var r = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, [clave.ComoCombinacion()], ct);
        return r.IsFailure ? Result.Failure<bool>(r.Error) : Result.Success(!r.Value.UnresolvedCombinations.Contains(0));
    }

    /// <summary><c>DesembolsoPorGirar</c>: las órdenes abiertas al corte por forma, línea y sucursal.</summary>
    public async Task<IReadOnlyList<SaldoDeCombinacionDeCartera>> OrdenesAbiertasAsync(DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var limite = corte.ToDateTime(TimeOnly.MaxValue);
        var abiertas = await db.DisbursementOrders.AsNoTracking()
            .Where(o => o.Form != DisbursementForm.InKind && o.IssuedAt <= limite
                && (o.Status == DisbursementOrderStatus.Issued || o.Status == DisbursementOrderStatus.Rejected || o.Status == DisbursementOrderStatus.CheckNotCashed)
                && !db.DisbursementOrders.Any(n => n.PreviousOrderId == o.Id))
            .Select(o => new { o.LoanId, o.Form, o.NetAmount, o.IssuedAt })
            .ToListAsync(ct);
        if (abiertas.Count == 0) return [];
        var dimensiones = await DimensionesAsync(abiertas.Select(o => o.LoanId).Distinct().ToList(), corte, alcance, ct);
        return abiertas.Where(o => dimensiones.ContainsKey(o.LoanId))
            .GroupBy(o => new ClaveDeSaldoDeCartera("DesembolsoPorGirar", dimensiones[o.LoanId].Linea, null, null, null, null, null, null, null, null,
                o.Form.ToString(), dimensiones[o.LoanId].Sucursal))
            .Select(g => new SaldoDeCombinacionDeCartera(g.Key, g.Sum(o => o.NetAmount), g.Select(o => o.LoanId).Distinct().Order().ToList(),
                DateOnly.FromDateTime(g.Max(o => o.IssuedAt))))
            .ToList();
    }

    /// <summary>Los puentes de recaudo por clase y los de Comercial, que esperan cero.</summary>
    public async Task<IReadOnlyList<SaldoDeCombinacionDeCartera>> PuentesEnCeroAsync(DateOnly corte, CancellationToken ct)
    {
        if (await SucursalPrincipalAsync(ct) is not { } sucursal) return [];
        var clases = await db.LoanOperations.AsNoTracking().Where(o => o.CollectorClass != null && o.AccountingDate <= corte)
            .Select(o => o.CollectorClass!).Distinct().ToListAsync(ct);
        var puentes = clases.Append(nameof(ClaseDeRecaudador.OwnLoanPayoff)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(c => new ClaveDeSaldoDeCartera("RecaudoPorAplicar", null, null, null, null, null, null, null, null, c, null, sucursal))
            .Append(new ClaveDeSaldoDeCartera("PuenteComercial", null, null, null, null, null, null, null, null, null, null, sucursal))
            .Append(new ClaveDeSaldoDeCartera("CuentaPorCobrarComercial", null, null, null, null, null, null, null, null, null, null, sucursal));
        return puentes.Select(p => new SaldoDeCombinacionDeCartera(p, 0m, [], corte)).ToList();
    }

    private async Task<Guid?> SucursalPrincipalAsync(CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Las dimensiones de cada crédito al corte, filtradas por el alcance si viene: línea, condiciones vigentes (la última versión con fecha
    /// efectiva ≤ corte, o la primera), empleado (estado actual de la persona, C8), reestructurado, categoría contable y sucursal contable.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, DimensionesDeCreditoDeCartera>> DimensionesAsync(
        IReadOnlyCollection<int> creditos, DateOnly corte, AlcanceDeCartera? alcance, CancellationToken ct)
    {
        var datos = await (from l in db.Loans.AsNoTracking()
                           join c in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on l.CreditLineId equals c.Id
                           join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on l.BranchId equals b.Id
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on l.DebtorPersonId equals p.Id
                           where creditos.Contains(l.Id)
                           select new { l.Id, l.BranchId, l.CreditLineId, Linea = c.Code, Sucursal = b.PublicId, p.IsEmployee, l.WrittenOffOn })
            .ToListAsync(ct);
        if (alcance is not null) datos = datos.Where(d => alcance.IncluyeOficina(d.BranchId) && alcance.IncluyeLinea(d.CreditLineId)).ToList();
        var ids = datos.Select(d => d.Id).ToList();
        var versiones = await db.LoanTermsVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId))
            .Select(v => new { v.LoanId, v.VersionNumber, v.EffectiveValueDate, v.ModalityCode, v.SubsegmentCode, v.RestructuringCount })
            .ToListAsync(ct);
        // US9 (T0702): la categoría del saldo al corte es la del último cierre vigente hasta ese día, incluido el que se fecha en él.
        var categorias = await new CategoriaContableDeCartera(db).AlCorteAsync(ids, corte, ct);
        var resultado = new Dictionary<int, DimensionesDeCreditoDeCartera>(datos.Count);
        foreach (var d in datos)
        {
            var delCredito = versiones.Where(v => v.LoanId == d.Id).OrderBy(v => v.VersionNumber).ToList();
            var vigente = delCredito.LastOrDefault(v => v.EffectiveValueDate <= corte) ?? delCredito.FirstOrDefault();
            var cat = categorias[d.Id];
            resultado[d.Id] = new DimensionesDeCreditoDeCartera(d.Linea, vigente?.ModalityCode, vigente?.SubsegmentCode, cat.Category, d.IsEmployee,
                (vigente?.RestructuringCount ?? 0) > 0, d.Sucursal, d.BranchId, Castigado: d.WrittenOffOn is { } castigo && castigo <= corte);
        }
        return resultado;
    }
}

/// <summary>
/// Las dimensiones de un crédito con que Cartera informa sus saldos a un corte. <see cref="Castigado"/>: castigado en o antes del corte
/// (<c>WrittenOffOn</c>, también el migrado), y entonces su activo está en <c>CastigadaEnOrden</c>. (nuevo)
/// </summary>
public sealed record DimensionesDeCreditoDeCartera(
    string Linea, string? Modalidad, string? Subsegmento, string Categoria, bool Empleado, bool Reestructurado, Guid Sucursal, int OficinaId,
    bool Castigado = false);
