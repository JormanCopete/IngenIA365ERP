using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using Microsoft.EntityFrameworkCore;
using CoreTaxKind = IngenIA365ERP.Domain.Enums.Core.TaxKind;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

/// <summary>
/// Una regla tal como se quiere guardar, con sucursal y centro ya traducidos a <c>Id</c> y los códigos como llegaron
/// (vacío o <c>*</c> = comodín). La construyen los comandos de alta y versión y la importación. (nuevo)
/// </summary>
public sealed record ReglaPropuesta(
    string Operation,
    string Role,
    int AccountId,
    DateOnly ValidFrom,
    DateOnly? ValidTo = null,
    string? AccountingGroupCode = null,
    string? WarehouseCode = null,
    string? PointOfSaleCode = null,
    string? PaymentMeansCode = null,
    string? TaxRateCode = null,
    decimal? TaxRate = null,
    string? ReasonCode = null,
    int? BranchId = null,
    int? CostCenterId = null)
{
    /// <summary>La <c>DimensionKey</c> que tendría (la de la entidad).</summary>
    public string Clave => InventoryPostingRule.ClaveDe(Operation, Role, Codigo(AccountingGroupCode), Codigo(WarehouseCode), Codigo(PointOfSaleCode),
        Codigo(PaymentMeansCode), Codigo(TaxRateCode), TaxRate, Motivo(ReasonCode), BranchId, CostCenterId);

    /// <summary>Las dimensiones que fija (para comprobar las exigidas y las que el rol no admite).</summary>
    public IReadOnlySet<DimensionDeRegla> Presentes => ReglasDeLaMatriz.Presentes(
        AccountingGroupCode, WarehouseCode, PointOfSaleCode, PaymentMeansCode, TaxRateCode, TaxRate, ReasonCode, BranchId is not null, CostCenterId is not null);

    public InventoryPostingRule ComoRegla(string? notas) => new(
        Operation.Trim(), Role.Trim(), AccountId, ValidFrom, Codigo(AccountingGroupCode), Codigo(WarehouseCode), Codigo(PointOfSaleCode),
        Codigo(PaymentMeansCode), Codigo(TaxRateCode), TaxRate, Motivo(ReasonCode), BranchId, CostCenterId, ValidTo, notas);

    internal static string? Codigo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == InventoryPostingRule.Cualquiera ? null : valor.Trim().ToUpperInvariant();

    internal static string? Motivo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == InventoryPostingRule.Cualquiera ? null : valor.Trim();
}

/// <summary>Un aviso al guardar una regla: no impide guardarla (C8, la tarifa de la cuenta en algún tramo). (nuevo)</summary>
public sealed record AvisoDeReglaDto(string Code, string Message);

/// <summary>
/// El resultado de <see cref="ReglasDeLaMatriz.ValidarAsync"/> (nuevo): los errores (la regla no se guarda), los avisos y
/// las versiones vivas de la clave que hay que cerrar la víspera de la nueva (<see cref="ACerrar"/>).
/// </summary>
public sealed record ValidacionDeRegla(IReadOnlyList<Error> Errores, IReadOnlyList<AvisoDeReglaDto> Avisos, IReadOnlyList<InventoryPostingRule> ACerrar)
{
    public bool EsValida => Errores.Count == 0;
}

/// <summary>
/// Lo que la validación de una regla lee una sola vez (nuevo): los códigos de Inventario, las tarifas del catálogo de
/// Core y el último mensaje contabilizado de cada operación (memorizado). La importación lo carga una vez para todo el
/// archivo (plantillas.md §0.3: «nunca una consulta por fila»).
/// </summary>
public sealed class CatalogosDeLaMatriz
{
    private readonly Dictionary<string, MensajeContabilizado?> _ultimos = new(StringComparer.Ordinal);

    internal CatalogosDeLaMatriz(CatalogoDeDimensionesDto dimensiones, IReadOnlyList<TarifaDelCatalogo> tarifas)
    {
        Dimensiones = dimensiones;
        Tarifas = tarifas;
        Grupos = Conjunto(dimensiones.AccountingGroups.Select(g => g.Code));
        Bodegas = Conjunto(dimensiones.Warehouses.Select(w => w.Code));
        Puntos = Conjunto(dimensiones.PointsOfSale.Select(p => p.Code));
        Causas = Conjunto(dimensiones.AdjustmentCauses.Select(c => c.Code));
        Medios = dimensiones.PaymentMeans is { } medios ? Conjunto(medios.Select(m => m.Code)) : null;
    }

    public CatalogoDeDimensionesDto Dimensiones { get; }

    public IReadOnlyList<TarifaDelCatalogo> Tarifas { get; }

    internal IReadOnlySet<string> Grupos { get; }
    internal IReadOnlySet<string> Bodegas { get; }
    internal IReadOnlySet<string> Puntos { get; }
    internal IReadOnlySet<string> Causas { get; }

    /// <summary>Nulo mientras no exista el catálogo de medios de pago (I3): el código no se puede comprobar.</summary>
    internal IReadOnlySet<string>? Medios { get; }

    /// <summary>La tarifa del catálogo con ese código vigente en la fecha.</summary>
    public TarifaDelCatalogo? TarifaVigente(string codigo, DateOnly fecha) =>
        Tarifas.Where(t => string.Equals(t.Code, codigo, StringComparison.OrdinalIgnoreCase) && t.VigenteEn(fecha))
            .OrderByDescending(t => t.ValidFrom).FirstOrDefault();

    internal async Task<MensajeContabilizado?> UltimoAsync(ResolutorDeReglas resolutor, string operacion, CancellationToken ct)
    {
        if (_ultimos.TryGetValue(operacion, out var memorizado)) return memorizado;
        var ultimo = await resolutor.UltimoContabilizadoAsync(operacion, ct);
        _ultimos[operacion] = ultimo;
        return ultimo;
    }

    private static HashSet<string> Conjunto(IEnumerable<string> codigos) => new(codigos.Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase);
}

/// <summary>Una tarifa del catálogo de Core, con la clase de su impuesto (nuevo).</summary>
public sealed record TarifaDelCatalogo(string Code, string Name, CoreTaxKind Kind, decimal? Rate, decimal? AmountPerUnit, DateOnly ValidFrom, DateOnly? ValidTo)
{
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || fecha <= ValidTo.Value);
}

/// <summary>
/// El punto único de las reglas de la matriz (feature 012, T27, T507; contracts/contabilidad.md §2.4 y §2.5): lo usan el
/// alta, la versión, la desactivación y la importación, así la plantilla responde lo mismo que el alta una a una
/// (plantillas.md §16). Comprueba la forma (<see cref="Forma"/>: operación y rol de los catálogos, exigidas presentes, no
/// admitidas ausentes), los códigos (Inventario por <see cref="IDimensionesDeInventario"/>, Core en Core, motivos fijos en
/// su catálogo), la cuenta (<see cref="AccountEligibility.Verificar"/> para INV y, en los roles de impuesto, una clase
/// compatible con la tarifa), la tarifa del catálogo en <c>ValidFrom</c>, los cruces de vigencia de la misma clave
/// (<c>Overlaps</c>) y la retroactividad sobre lo contabilizado (<c>RetroactiveOverPosted</c>, con la excepción de la
/// clave nueva que no coincide con nada contabilizado). (nuevo)
/// </summary>
public sealed class ReglasDeLaMatriz(IApplicationDbContext db, IDimensionesDeInventario dimensiones, ResolutorDeReglas resolutor)
{
    /// <summary>Lo que las validaciones leen una vez.</summary>
    public async Task<CatalogosDeLaMatriz> CargarAsync(CancellationToken ct)
    {
        var catalogo = await dimensiones.CatalogoAsync(ct);
        var tarifas = await db.TaxRates.AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new TarifaDelCatalogo(t.Code, t.Name, t.TaxDefinition!.Kind, t.Rate, t.AmountPerUnit, t.ValidFrom, t.ValidTo))
            .ToListAsync(ct);
        return new CatalogosDeLaMatriz(catalogo, tarifas);
    }

    /// <summary>Las dimensiones fijadas, a partir de sus valores (vacío o <c>*</c> = no fija).</summary>
    public static IReadOnlySet<DimensionDeRegla> Presentes(
        string? grupo, string? bodega, string? punto, string? medio, string? tarifa, decimal? valorTarifa, string? motivo, bool sucursal, bool centro)
    {
        var presentes = new HashSet<DimensionDeRegla>();
        if (ReglaPropuesta.Codigo(grupo) is not null) presentes.Add(DimensionDeRegla.AccountingGroupCode);
        if (ReglaPropuesta.Codigo(bodega) is not null) presentes.Add(DimensionDeRegla.WarehouseCode);
        if (ReglaPropuesta.Codigo(punto) is not null) presentes.Add(DimensionDeRegla.PointOfSaleCode);
        if (ReglaPropuesta.Codigo(medio) is not null) presentes.Add(DimensionDeRegla.PaymentMeansCode);
        if (ReglaPropuesta.Codigo(tarifa) is not null) presentes.Add(DimensionDeRegla.TaxRateCode);
        if (valorTarifa is not null) presentes.Add(DimensionDeRegla.TaxRate);
        if (ReglaPropuesta.Motivo(motivo) is not null) presentes.Add(DimensionDeRegla.ReasonCode);
        if (sucursal) presentes.Add(DimensionDeRegla.Branch);
        if (centro) presentes.Add(DimensionDeRegla.CostCenter);
        return presentes;
    }

    /// <summary>
    /// La forma, sin leer nada: la operación y el rol existen y el rol es de la operación; las exigidas están; las que el
    /// rol no admite, no. La tarifa (valor) no se exige aquí: un impuesto de valor por unidad no la tiene, y la compara
    /// <see cref="ValidarAsync"/> con el catálogo. La usan los validadores (400) y el handler (422).
    /// </summary>
    public static IReadOnlyList<Error> Forma(string? operacion, string? rol, IReadOnlySet<DimensionDeRegla> presentes)
    {
        var errores = new List<Error>();
        var op = OperacionesDeInventario.Buscar(operacion);
        if (op is null)
        {
            errores.Add(AccountingErrors.InventoryRuleOperationUnknown(operacion ?? string.Empty));
            return errores;
        }
        var definicion = RolesDeCuenta.Buscar(rol);
        if (definicion is null || !op.TieneRol(definicion.Codigo))
        {
            errores.Add(AccountingErrors.InventoryRuleRoleNotInOperation(op.Codigo, rol ?? string.Empty, op.Roles));
            return errores;
        }

        var (exigidas, opcionales) = RolesDeCuenta.DimensionesDe(op.Codigo, definicion);
        foreach (var exigida in exigidas.Where(d => d != DimensionDeRegla.TaxRate && !presentes.Contains(d)))
            errores.Add(AccountingErrors.InventoryRuleDimensionRequired(op.Codigo, definicion.Codigo, RolesDeCuenta.NombreDe(exigida)));
        foreach (var presente in presentes.Where(d => !exigidas.Contains(d) && !opcionales.Contains(d)).OrderBy(d => d))
            errores.Add(AccountingErrors.InventoryRuleDimensionNotAllowed(op.Codigo, definicion.Codigo, RolesDeCuenta.NombreDe(presente)));
        return errores;
    }

    /// <summary>
    /// Todas las reglas de §2.4 y §2.5 sobre <paramref name="propuesta"/>. <paramref name="versionesDeLaClave"/> son las
    /// reglas vivas con la misma <c>DimensionKey</c> (seguidas, porque las que haya que cerrar se cierran sobre ellas; la
    /// importación agrega las que ya creó el mismo archivo). Con <paramref name="esVersion"/> la vigente que empieza antes
    /// se cierra la víspera; sin él, cualquier cruce es <c>Overlaps</c>. <paramref name="excluir"/> es la regla que se está
    /// cambiando (desactivar), que no se compara consigo misma.
    /// </summary>
    public async Task<ValidacionDeRegla> ValidarAsync(
        ReglaPropuesta propuesta,
        CatalogosDeLaMatriz catalogos,
        IReadOnlyList<InventoryPostingRule> versionesDeLaClave,
        bool esVersion,
        CancellationToken ct,
        InventoryPostingRule? excluir = null)
    {
        var errores = new List<Error>(Forma(propuesta.Operation, propuesta.Role, propuesta.Presentes));
        var avisos = new List<AvisoDeReglaDto>();
        if (errores.Count > 0) return new ValidacionDeRegla(errores, avisos, []);

        var operacion = OperacionesDeInventario.Buscar(propuesta.Operation)!.Codigo;
        var rol = RolesDeCuenta.Buscar(propuesta.Role)!;

        if (propuesta.ValidTo is { } hasta && hasta < propuesta.ValidFrom.AddDays(-1))
            errores.Add(AccountingErrors.InventoryRuleValidToInvalid(propuesta.ValidFrom, hasta));

        Codigos(propuesta, operacion, rol, catalogos, errores);

        // La cuenta: de movimiento, activa y habilitada para INV; en los de impuesto, de una clase compatible con la tarifa.
        var cuenta = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates)
            .FirstOrDefaultAsync(a => a.Id == propuesta.AccountId && !a.IsDeleted, ct);
        var elegible = AccountEligibility.Verificar(cuenta, propuesta.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture), ModuloContable.Inventario);
        if (elegible.IsFailure) errores.Add(elegible.Error);

        if (rol.EsDeImpuesto && ReglaPropuesta.Codigo(propuesta.TaxRateCode) is { } codigoTarifa)
            Tarifa(propuesta, codigoTarifa, elegible.IsSuccess ? cuenta : null, catalogos, errores, avisos);

        var aCerrar = new List<InventoryPostingRule>();
        Vigencias(propuesta, versionesDeLaClave, esVersion, excluir, errores, aCerrar);

        if (errores.Count == 0)
        {
            var retro = await RetroactividadAsync(propuesta, operacion, catalogos, claveExistente: versionesDeLaClave.Any(v => !v.IsDeleted && !ReferenceEquals(v, excluir)), ct);
            if (retro is not null) errores.Add(retro);
        }
        return new ValidacionDeRegla(errores, avisos, errores.Count == 0 ? aCerrar : []);
    }

    /// <summary>
    /// La retroactividad al desactivar (§2.4): cerrar una regla antes de la fecha de lo ya contabilizado de su operación
    /// dejaría sin regla el espejo de una anulación futura de esos documentos (G2).
    /// </summary>
    public async Task<Error?> RetroactividadDelCierreAsync(InventoryPostingRule regla, DateOnly hasta, CatalogosDeLaMatriz catalogos, CancellationToken ct)
    {
        var ultimo = await catalogos.UltimoAsync(resolutor, regla.Operation, ct);
        if (ultimo is null || hasta >= ultimo.OperationDate || regla.ValidFrom > ultimo.OperationDate) return null;
        return AccountingErrors.InventoryRuleRetroactiveOverPosted(ultimo.OperationDate, ultimo.Documento);
    }

    private static void Codigos(ReglaPropuesta p, string operacion, RolDeCuenta rol, CatalogosDeLaMatriz catalogos, List<Error> errores)
    {
        void Exige(IReadOnlySet<string>? conjunto, string? valor, DimensionDeRegla dimension)
        {
            if (conjunto is null || ReglaPropuesta.Codigo(valor) is not { } codigo) return;
            if (!conjunto.Contains(codigo)) errores.Add(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(dimension), codigo));
        }

        Exige(catalogos.Grupos, p.AccountingGroupCode, DimensionDeRegla.AccountingGroupCode);
        Exige(catalogos.Bodegas, p.WarehouseCode, DimensionDeRegla.WarehouseCode);
        Exige(catalogos.Puntos, p.PointOfSaleCode, DimensionDeRegla.PointOfSaleCode);
        Exige(catalogos.Medios, p.PaymentMeansCode, DimensionDeRegla.PaymentMeansCode);

        if (ReglaPropuesta.Codigo(p.TaxRateCode) is { } tarifa && !catalogos.Tarifas.Any(t => string.Equals(t.Code, tarifa, StringComparison.OrdinalIgnoreCase)))
            errores.Add(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(DimensionDeRegla.TaxRateCode), tarifa));

        if (ReglaPropuesta.Motivo(p.ReasonCode) is { } motivo)
        {
            var admitidos = RolesDeCuenta.MotivosAdmitidos(operacion, rol);
            var existe = admitidos is null
                ? catalogos.Causas.Contains(motivo)
                : admitidos.Contains(motivo, StringComparer.OrdinalIgnoreCase);
            if (!existe) errores.Add(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(DimensionDeRegla.ReasonCode), motivo));
        }
    }

    /// <summary>
    /// C8 (§2.5): la tarifa de la regla es la del catálogo para ese código en <c>ValidFrom</c> (error si no); la cuenta es de
    /// una clase compatible (error si no); si la cuenta tiene otra tarifa en algún tramo de la vigencia, aviso.
    /// </summary>
    private static void Tarifa(ReglaPropuesta p, string codigo, ChartOfAccount? cuenta, CatalogosDeLaMatriz catalogos, List<Error> errores, List<AvisoDeReglaDto> avisos)
    {
        var delCatalogo = catalogos.TarifaVigente(codigo, p.ValidFrom);
        if (delCatalogo is null)
        {
            if (catalogos.Tarifas.Any(t => string.Equals(t.Code, codigo, StringComparison.OrdinalIgnoreCase)))
                errores.Add(AccountingErrors.InventoryRuleDimensionCodeUnknown(RolesDeCuenta.NombreDe(DimensionDeRegla.TaxRateCode), $"{codigo} (sin vigencia el {p.ValidFrom:yyyy-MM-dd})"));
            return;
        }
        if (delCatalogo.Rate != p.TaxRate)
        {
            errores.Add(AccountingErrors.InventoryRuleTaxRateMismatch(codigo, cuenta?.Code ?? string.Empty, p.TaxRate ?? 0m, delCatalogo.Rate ?? 0m));
            return;
        }
        if (cuenta is null) return;

        if (!RolesDeCuenta.CuentaCompatibleConTarifa(cuenta.TaxKind, delCatalogo.Kind))
        {
            errores.Add(AccountingErrors.AccountNotEligible(cuenta.Code, ModuloContable.Inventario,
                $"es de impuesto «{cuenta.TaxKind}» y la tarifa {codigo} es de «{delCatalogo.Kind}»"));
            return;
        }

        // Los de valor por unidad no tienen tarifa que comparar con la de la cuenta.
        if (p.TaxRate is not { } esperada) return;
        var hasta = p.ValidTo ?? DateOnly.MaxValue;
        var desdeLaCuenta = Rules.CuentaParaReglas.TarifaVigenteDe(cuenta, p.ValidFrom);
        var tramos = cuenta.TaxRates.Where(t => !t.IsDeleted && t.ValidFrom > p.ValidFrom && t.ValidFrom <= hasta).Select(t => (decimal?)t.Rate)
            .Prepend(desdeLaCuenta);
        if (tramos.Any(t => t != esperada))
            avisos.Add(new AvisoDeReglaDto("Accounting.InventoryRule.TaxRateMismatch",
                $"La cuenta {cuenta.Code} tiene otra tarifa (o ninguna) en algún tramo de la vigencia de la regla; la tarifa {codigo} es {esperada:0.######}. "
                + "La validación previa lo bloqueará al confirmar un documento; corríjalo en la cuenta."));
    }

    private static void Vigencias(
        ReglaPropuesta p, IReadOnlyList<InventoryPostingRule> versiones, bool esVersion, InventoryPostingRule? excluir,
        List<Error> errores, List<InventoryPostingRule> aCerrar)
    {
        var hasta = p.ValidTo ?? DateOnly.MaxValue;
        foreach (var v in versiones.Where(v => !v.IsDeleted && !ReferenceEquals(v, excluir)))
        {
            var seCruza = v.ValidFrom <= hasta && (v.ValidTo ?? DateOnly.MaxValue) >= p.ValidFrom;
            if (!seCruza) continue;
            if (esVersion && v.ValidFrom < p.ValidFrom)
            {
                aCerrar.Add(v);
                continue;
            }
            errores.Add(AccountingErrors.InventoryRuleOverlaps(new
            {
                rulePublicId = v.PublicId, validFrom = v.ValidFrom, validTo = v.ValidTo, dimensionKey = v.DimensionKey,
            }));
            return;
        }
    }

    private async Task<Error?> RetroactividadAsync(ReglaPropuesta p, string operacion, CatalogosDeLaMatriz catalogos, bool claveExistente, CancellationToken ct)
    {
        var ultimo = await catalogos.UltimoAsync(resolutor, operacion, ct);
        if (ultimo is null || p.ValidFrom > ultimo.OperationDate) return null;
        if (claveExistente) return AccountingErrors.InventoryRuleRetroactiveOverPosted(ultimo.OperationDate, ultimo.Documento);

        // Clave nueva: puede empezar antes si nada contabilizado desde su vigencia coincide con ella (§2.4, FR-078).
        var coincide = await resolutor.ContabilizadoQueCoincideAsync(p.ComoRegla(null), ct);
        return coincide is null ? null : AccountingErrors.InventoryRuleRetroactiveOverPosted(coincide.OperationDate, coincide.Documento);
    }
}
