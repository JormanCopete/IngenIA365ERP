using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Rules;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// La matriz de Cartera cargada para un rango de fechas (feature 013, T0641; contracts/contabilidad.md §3.3 y §3.4). <see cref="Resolver"/>
/// es puro y es el mismo en la validación previa, el consumo, la vista previa, la conciliación y <c>ResolveLendingRuleQuery</c>:
/// <list type="number">
///   <item>con importe cero no se pide regla (un pago sin mora no pide regla de mora);</item>
///   <item>candidatas: reglas del rol vigentes en la fecha contable del hecho, con hecho igual o <c>*</c>, cuyas dimensiones fijadas son
///         iguales a las buscadas —las exigidas siempre están fijadas en la regla, así que se comparan por igualdad exacta; las opcionales
///         casan por igualdad o con <c>*</c>—; el centro de costo no se compara (C77);</item>
///   <item>gana la de mayor peso. No hay empate posible: los pesos son potencias de dos, así que dos candidatas con el mismo peso fijan las
///         mismas dimensiones y, si las dos casan, tienen la misma clave, cuyas vigencias no se cruzan;</item>
///   <item>sin candidata, <c>Accounting.LendingRule.Missing</c> con <c>{ operation, role, values, date, loans }</c>.</item>
/// </list>
/// La categoría se compara por su letra (§3.4: <c>E1</c> y <c>E2</c> → <c>E</c>). (nuevo)
/// </summary>
public sealed class MatrizVigenteDeCartera
{
    private readonly ILookup<string, LendingPostingRule> _porRol;
    private readonly IReadOnlyDictionary<int, ChartOfAccount> _cuentas;
    private readonly IReadOnlyDictionary<Guid, int> _sucursales;
    private readonly IReadOnlyDictionary<int, Guid> _centros;

    internal MatrizVigenteDeCartera(
        DateOnly desde, DateOnly hasta, IReadOnlyList<LendingPostingRule> reglas,
        IReadOnlyDictionary<int, ChartOfAccount> cuentas, IReadOnlyDictionary<Guid, int> sucursales, IReadOnlyDictionary<int, Guid> centros)
    {
        Desde = desde;
        Hasta = hasta;
        Reglas = reglas;
        _porRol = reglas.ToLookup(r => r.Role, StringComparer.Ordinal);
        _cuentas = cuentas;
        _sucursales = sucursales;
        _centros = centros;
    }

    public DateOnly Desde { get; }

    public DateOnly Hasta { get; }

    /// <summary>Las reglas cargadas (para la completitud y las consultas).</summary>
    public IReadOnlyList<LendingPostingRule> Reglas { get; }

    /// <summary>La cuenta de una regla cargada.</summary>
    public ChartOfAccount? CuentaDe(LendingPostingRule regla) => _cuentas.GetValueOrDefault(regla.AccountId);

    /// <summary>El <c>PublicId</c> del centro de costo de una regla (para mostrarla); nulo si no fija centro.</summary>
    public Guid? CentroDe(LendingPostingRule regla) => regla.CostCenterId is { } id ? _centros.GetValueOrDefault(id) : null;

    /// <summary>
    /// La regla que gana para <paramref name="peticion"/>, o <c>Success(null)</c> si el importe es cero. La fecha tiene que estar dentro del
    /// rango cargado.
    /// </summary>
    public Result<ReglaDeCarteraResuelta?> Resolver(PeticionDeReglaDeCartera peticion)
    {
        ArgumentNullException.ThrowIfNull(peticion);
        if (peticion.Importe == 0m) return Result.Success<ReglaDeCarteraResuelta?>(null);
        RevisarFecha(peticion.Fecha);

        var sucursal = Sucursal(peticion.Valores);
        var ganadora = Candidatas(peticion.Operation, peticion.Role, peticion.Fecha)
            .Where(r => Coincide(r, peticion.Valores, sucursal))
            .OrderByDescending(r => r.SpecificityWeight)
            .ThenByDescending(r => r.ValidFrom)
            .ThenBy(r => r.Id)
            .FirstOrDefault();

        if (ganadora is null)
            return Result.Failure<ReglaDeCarteraResuelta?>(AccountingErrors.LendingRuleMissing(peticion.Operation, peticion.Role,
                Valores(peticion.Valores), peticion.Fecha, peticion.Loans));

        var cuenta = _cuentas.GetValueOrDefault(ganadora.AccountId)
            ?? throw new InvalidOperationException($"La cuenta {ganadora.AccountId} de la regla {ganadora.PublicId} no se cargó.");
        return Result.Success<ReglaDeCarteraResuelta?>(new ReglaDeCarteraResuelta(
            peticion, ganadora, cuenta, CuentaParaReglas.De(cuenta, peticion.Fecha), sucursal, ganadora.CostCenterId));
    }

    /// <summary>
    /// Todas las reglas del rol vigentes en la fecha con hecho igual o <c>*</c>, marcadas si casan con los valores, de la de mayor peso a la
    /// de menor: lo que <c>ResolveLendingRuleQuery</c> muestra a la contadora («¿a qué cuenta va esta combinación?»).
    /// </summary>
    public IReadOnlyList<CandidataDeCartera> CandidatasDe(PeticionDeReglaDeCartera peticion)
    {
        RevisarFecha(peticion.Fecha);
        var sucursal = Sucursal(peticion.Valores);
        return Candidatas(peticion.Operation, peticion.Role, peticion.Fecha)
            .Select(r => new CandidataDeCartera(r, Coincide(r, peticion.Valores, sucursal)))
            .OrderByDescending(c => c.Coincide).ThenByDescending(c => c.Regla.SpecificityWeight).ThenByDescending(c => c.Regla.ValidFrom)
            .ToList();
    }

    private IEnumerable<LendingPostingRule> Candidatas(string operacion, string rol, DateOnly fecha) =>
        _porRol[rol].Where(r => r.VigenteEn(fecha)
            && (r.Operation == LendingPostingRule.Cualquiera || string.Equals(r.Operation, operacion, StringComparison.Ordinal)));

    private int? Sucursal(ValoresDeCartera v) =>
        v.BranchPublicId is { } bp && _sucursales.TryGetValue(bp, out var b) ? b : null;

    private void RevisarFecha(DateOnly fecha)
    {
        if (fecha < Desde || fecha > Hasta)
            throw new ArgumentOutOfRangeException(nameof(fecha), fecha, $"La matriz de Cartera se cargó del {Desde:yyyy-MM-dd} al {Hasta:yyyy-MM-dd}.");
    }

    /// <summary>¿La regla casa con los valores? Cada dimensión que la regla fija tiene que ser igual a la buscada.</summary>
    public static bool Coincide(LendingPostingRule r, ValoresDeCartera v, int? sucursal) =>
        Igual(r.CreditLineCode, v.CreditLineCode)
        && Igual(r.ModalityCode, v.ModalityCode)
        && Igual(r.SubsegmentCode, v.SubsegmentCode)
        && Igual(r.RiskCategory, LendingPostingRule.Categoria(v.RiskCategory))
        && Igual(r.ConceptCode, v.ConceptCode)
        && Igual(r.IsEmployee, v.IsEmployee)
        && Igual(r.InGracePeriod, v.InGracePeriod)
        && Igual(r.OnBehalfOfAssociate, v.OnBehalfOfAssociate)
        && Igual(r.Restructured, v.Restructured)
        && Igual(r.CollectorClass, v.CollectorClass)
        && Igual(r.ReasonCode, v.ReasonCode)
        && (r.BranchId is null || r.BranchId == sucursal)
        // Feature 014 (T049): las cinco de captaciones, por igualdad cuando la regla las fija (las exigidas siempre lo están).
        && Igual(r.LineNature, v.LineNature)
        && Igual(r.DepositClass, v.DepositClass)
        && Igual(r.IssueTermBand, v.IssueTermBand)
        && Igual(r.ContributionClass, v.ContributionClass)
        && Igual(r.AccountFlag, v.AccountFlag);

    private static bool Igual(string? deLaRegla, string? buscado) =>
        deLaRegla is null || string.Equals(deLaRegla, buscado?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool Igual(bool? deLaRegla, bool? buscado) => deLaRegla is null || deLaRegla == buscado;

    /// <summary>Los valores buscados como los nombra el error (<c>data.values</c>).</summary>
    private static object Valores(ValoresDeCartera v) => new
    {
        creditLineCode = v.CreditLineCode, modalityCode = v.ModalityCode, subsegmentCode = v.SubsegmentCode,
        riskCategory = LendingPostingRule.Categoria(v.RiskCategory), conceptCode = v.ConceptCode, isEmployee = v.IsEmployee,
        inGracePeriod = v.InGracePeriod, onBehalfOfAssociate = v.OnBehalfOfAssociate, restructured = v.Restructured,
        collectorClass = v.CollectorClass, reasonCode = v.ReasonCode, branch = v.BranchPublicId,
        lineNature = v.LineNature, depositClass = v.DepositClass, issueTermBand = v.IssueTermBand, contributionClass = v.ContributionClass,
        accountFlag = v.AccountFlag,
    };
}
