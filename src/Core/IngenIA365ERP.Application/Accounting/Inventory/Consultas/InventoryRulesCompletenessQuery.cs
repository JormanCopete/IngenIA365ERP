using FluentValidation;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// La completitud de la matriz a una fecha (feature 012, T516; contracts/contabilidad.md §7.1; api.md §26.3; FR-082, SC-024),
/// también detrás de <see cref="IContabilidadParaInventario.CompletitudAsync"/>. Recibe de Inventario, por
/// <see cref="IDimensionesDeInventario"/>, las combinaciones operación × grupo × bodega en uso y los medios de pago; de Core y
/// Contabilidad, las tarifas y las cuentas. Devuelve:
/// <list type="bullet">
///   <item>combinaciones y roles exigidos sin regla vigente (una dimensión que la combinación no conoce —medio, causa, tarifa— cuenta
///         como cualquiera);</item>
///   <item>medios de pago activos sin regla <c>MedioDePago</c> vigente;</item>
///   <item>reglas vigentes o futuras cuya cuenta dejó de ser elegible (<see cref="AccountEligibility.Reparo"/>);</item>
///   <item>reglas de impuesto cuya cuenta tiene, en algún tramo de su vigencia, otra tarifa o ninguna (C8);</item>
///   <item>operaciones sin tipo de comprobante mapeado o mapeadas a uno que ya no sirve;</item>
///   <item>avisos: impuesto por unidad con cuenta que exige base, mercancía por facturar con cuenta que exige cruce y, con el
///         costo por cooperativa (<c>Costeo.Ambito</c>), un grupo cuyas bodegas van a cuentas de inventario distintas (D2).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record InventoryRulesCompletenessQuery(DateOnly Date) : IRequest<Result<CompletitudDeLaMatrizDto>>;

public sealed class InventoryRulesCompletenessQueryValidator : AbstractValidator<InventoryRulesCompletenessQuery>
{
    public InventoryRulesCompletenessQueryValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Indique la fecha.");
    }
}

public sealed class InventoryRulesCompletenessQueryHandler(
    IApplicationDbContext db, IDimensionesDeInventario dimensiones, TiposDeComprobanteDeInventario tipos, ILectorDeParametros parametros)
    : IRequestHandler<InventoryRulesCompletenessQuery, Result<CompletitudDeLaMatrizDto>>
{
    public const string AvisoImpuestoPorUnidad = "UnitTaxAccountRequiresBase";
    public const string AvisoMercanciaConCruce = "GoodsNotInvoicedRequiresCrossDocument";
    public const string AvisoGrupoConVariasCuentas = "GroupWithSeveralInventoryAccounts";

    public async Task<Result<CompletitudDeLaMatrizDto>> Handle(InventoryRulesCompletenessQuery request, CancellationToken ct)
    {
        var fecha = request.Date;
        // Vigentes a la fecha y las que empiezan después: una cuenta no elegible o una tarifa distinta en una regla futura también estorba.
        var reglas = await db.InventoryPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.ValidTo == null || r.ValidTo >= fecha)).ToListAsync(ct);
        var vigentes = reglas.Where(r => r.VigenteEn(fecha)).ToList();
        var idsDeCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates).Where(a => idsDeCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var catalogo = await dimensiones.CatalogoAsync(ct);
        var combinaciones = await dimensiones.CombinacionesEnUsoAsync(fecha, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var bodegas = catalogo.Warehouses.ToDictionary(w => w.Code, StringComparer.OrdinalIgnoreCase);

        var faltantes = Faltantes(combinaciones, vigentes, bodegas, sucursales);
        var medios = (catalogo.PaymentMeans ?? [])
            .Where(m => !vigentes.Any(r => r.Role == R.MedioDePago && string.Equals(r.PaymentMeansCode, m.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(m => new MedioSinCuentaDto(m.Code, m.Name, null))
            .ToList();
        var noElegibles = reglas
            .Select(r => (Regla: r, Reparo: AccountEligibility.Reparo(cuentas.GetValueOrDefault(r.AccountId), ModuloContable.Inventario)))
            .Where(x => x.Reparo is not null)
            .Select(x => new ReglaNoElegibleDto(x.Regla.PublicId, x.Regla.Operation, x.Regla.Role, cuentas.GetValueOrDefault(x.Regla.AccountId)?.Code ?? string.Empty, x.Reparo!))
            .ToList();
        var tarifas = reglas
            .Where(r => r.Role is R.Impuesto or R.Retencion && r.TaxRate is not null && cuentas.ContainsKey(r.AccountId))
            .SelectMany(r => TramosDistintos(r, cuentas[r.AccountId]))
            .ToList();
        var sinMapeo = await SinMapeoAsync(combinaciones, ct);
        var avisos = await AvisosAsync(reglas, cuentas, ct);
        avisos.AddRange(await GruposConVariasCuentasAsync(vigentes, cuentas, fecha, ct));

        var porTipo = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["missingRules"] = faltantes.Count,
            ["paymentMeansWithoutAccount"] = medios.Count,
            ["ineligibleRules"] = noElegibles.Count,
            ["taxRateMismatches"] = tarifas.Count,
            ["unmappedOperations"] = sinMapeo.Count,
        };
        return Result.Success(new CompletitudDeLaMatrizDto(fecha, faltantes, medios, noElegibles, tarifas, sinMapeo,
            new ResumenDeCompletitudDto(porTipo.Values.Sum(), porTipo), avisos));
    }

    /// <summary>
    /// Las combinaciones en uso sin regla vigente para alguno de los roles exigidos de su operación. Una línea en una bodega de
    /// tránsito pide <c>Transito</c> donde la operación dice <c>Inventario</c>.
    /// </summary>
    public static List<ReglaFaltanteDto> Faltantes(
        IReadOnlyList<CombinacionEnUsoDto> combinaciones, IReadOnlyList<InventoryPostingRule> vigentes,
        IReadOnlyDictionary<string, BodegaDeDimensionDto> bodegas, IReadOnlyDictionary<Guid, int> sucursales)
    {
        var faltantes = new List<ReglaFaltanteDto>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in combinaciones)
        {
            var operacion = OperacionesDeInventario.Buscar(c.Operation);
            if (operacion is null) continue;
            var bodega = c.WarehouseCode is null ? null : bodegas.GetValueOrDefault(c.WarehouseCode);
            int? sucursal = bodega?.BranchPublicId is { } bp && sucursales.TryGetValue(bp, out var id) ? id : null;
            foreach (var exigido in operacion.RolesExigidos)
            {
                var rol = R.RolDeLaBodega(exigido, bodega?.Behavior ?? WarehouseBehavior.Operational);
                var hay = vigentes.Any(r => r.Operation == operacion.Codigo && r.Role == rol
                    && (r.AccountingGroupCode is null || c.AccountingGroupCode is null || string.Equals(r.AccountingGroupCode, c.AccountingGroupCode, StringComparison.OrdinalIgnoreCase))
                    && (r.WarehouseCode is null || c.WarehouseCode is null || string.Equals(r.WarehouseCode, c.WarehouseCode, StringComparison.OrdinalIgnoreCase))
                    && (r.BranchId is null || sucursal is null || r.BranchId == sucursal));
                if (hay || !vistos.Add($"{operacion.Codigo}|{rol}|{c.AccountingGroupCode}|{c.WarehouseCode}")) continue;
                faltantes.Add(new ReglaFaltanteDto(operacion.Codigo, rol,
                    RolTieneGrupo(rol) ? c.AccountingGroupCode : null, RolTieneGrupo(rol) ? c.WarehouseCode : null,
                    null, null, null, null, c.DocumentTypeCodes, c.LastUsedAt));
            }
        }
        return faltantes;
    }

    /// <summary>
    /// Los tramos de la vigencia de una regla de impuesto en los que la cuenta tiene otra tarifa o ninguna (C8): la regla se
    /// guardó con la del catálogo y la regla 10 de la 009 compara contra la de la cuenta.
    /// </summary>
    public static IEnumerable<TarifaDistintaDto> TramosDistintos(InventoryPostingRule regla, ChartOfAccount cuenta)
    {
        var tarifas = cuenta.TaxRates.Where(t => !t.IsDeleted).OrderBy(t => t.ValidFrom).ToList();
        var cortes = new List<DateOnly> { regla.ValidFrom };
        cortes.AddRange(tarifas.Select(t => t.ValidFrom).Where(d => d > regla.ValidFrom && (regla.ValidTo is null || d <= regla.ValidTo)));
        for (var i = 0; i < cortes.Count; i++)
        {
            var desde = cortes[i];
            DateOnly? hasta = i + 1 < cortes.Count ? cortes[i + 1].AddDays(-1) : regla.ValidTo;
            var vigente = tarifas.Where(t => t.ValidFrom <= desde).Select(t => (decimal?)t.Rate).LastOrDefault();
            if (vigente != regla.TaxRate)
                yield return new TarifaDistintaDto(regla.TaxRateCode ?? string.Empty, regla.TaxRate!.Value, cuenta.Code, vigente, desde, hasta);
        }
    }

    private async Task<List<OperacionSinMapeoDto>> SinMapeoAsync(IReadOnlyList<CombinacionEnUsoDto> combinaciones, CancellationToken ct)
    {
        var mapeos = await tipos.CargarAsync(ct);
        var sinMapeo = OperacionesDeInventario.Todas
            .Where(o => mapeos.Resolver(o.Codigo, null).IsFailure)
            .Select(o => new OperacionSinMapeoDto(o.Codigo, null))
            .ToList();
        foreach (var c in combinaciones)
        {
            if (OperacionesDeInventario.Buscar(c.Operation) is null || sinMapeo.Any(s => s.Operation == c.Operation && s.InventoryDocumentTypeCode is null)) continue;
            foreach (var tipo in c.DocumentTypeCodes)
            {
                if (mapeos.Resolver(c.Operation, tipo).IsFailure && !sinMapeo.Any(s => s.Operation == c.Operation && s.InventoryDocumentTypeCode == tipo))
                    sinMapeo.Add(new OperacionSinMapeoDto(c.Operation, tipo));
            }
        }
        return sinMapeo;
    }

    private async Task<List<AvisoDeCompletitudDto>> AvisosAsync(
        IReadOnlyList<InventoryPostingRule> reglas, IReadOnlyDictionary<int, ChartOfAccount> cuentas, CancellationToken ct)
    {
        var avisos = new List<AvisoDeCompletitudDto>();
        var codigos = reglas.Where(r => r.TaxRateCode is not null).Select(r => r.TaxRateCode!).Distinct().ToList();
        var porUnidad = codigos.Count == 0
            ? []
            : (await db.TaxRates.AsNoTracking().Where(t => codigos.Contains(t.Code) && t.AmountPerUnit != null).Select(t => t.Code).ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in reglas)
        {
            if (!cuentas.TryGetValue(r.AccountId, out var cuenta)) continue;
            if (r.TaxRateCode is { } tarifa && porUnidad.Contains(tarifa) && cuenta.RequiresTaxBase)
                avisos.Add(new AvisoDeCompletitudDto(AvisoImpuestoPorUnidad,
                    $"La tarifa {tarifa} es de valor por unidad y la cuenta {cuenta.Code} exige base gravable: base × tarifa no aplica.", cuenta.Code, r.PublicId));
            if (r.Role == R.MercanciaPorFacturar && cuenta.RequiresCrossDocument)
                avisos.Add(new AvisoDeCompletitudDto(AvisoMercanciaConCruce,
                    $"La cuenta {cuenta.Code} de mercancía por facturar exige documento cruce: la recepción no conoce el número de la factura.", cuenta.Code, r.PublicId));
        }
        return avisos;
    }

    /// <summary>
    /// D2: con el costo promedio por cooperativa, un grupo cuyas bodegas van a cuentas de inventario distintas reparte un solo
    /// promedio entre varias cuentas y la conciliación por cuenta no cuadra bodega a bodega. Con el costo por bodega no aplica.
    /// </summary>
    private async Task<IEnumerable<AvisoDeCompletitudDto>> GruposConVariasCuentasAsync(
        IReadOnlyList<InventoryPostingRule> vigentes, IReadOnlyDictionary<int, ChartOfAccount> cuentas, DateOnly fecha, CancellationToken ct)
    {
        var ambito = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoAmbito, fecha, ct: ct);
        if (ambito.IsSuccess && ambito.Value.Texto == "Bodega") return [];
        return vigentes
            .Where(r => r.Role == R.Inventario && r.AccountingGroupCode is not null)
            .GroupBy(r => r.AccountingGroupCode!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Grupo: g.Key, Cuentas: g.Select(r => cuentas.GetValueOrDefault(r.AccountId)?.Code).Where(c => c is not null).Distinct().Order().ToList()))
            .Where(x => x.Cuentas.Count > 1)
            .Select(x => new AvisoDeCompletitudDto(AvisoGrupoConVariasCuentas,
                $"El costo es por cooperativa y el grupo {x.Grupo} va a {x.Cuentas.Count} cuentas de inventario según la bodega ({string.Join(", ", x.Cuentas)}).",
                x.Cuentas[0], null));
    }

    private static bool RolTieneGrupo(string rol) => R.Buscar(rol) is { } r && r.Admite(DimensionDeRegla.AccountingGroupCode);
}
