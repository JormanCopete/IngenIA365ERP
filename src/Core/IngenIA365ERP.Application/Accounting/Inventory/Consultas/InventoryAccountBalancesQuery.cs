using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// El saldo contable de las cuentas de inventario y tránsito de la matriz, por conjunto, a una fecha de corte (feature 012,
/// T516; contracts/contabilidad.md §7.2; FR-081, FR-090), detrás de <see cref="IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync"/>.
///
/// <list type="number">
///   <item>toma las reglas vigentes al corte con rol <c>Inventario</c> o <c>Transito</c>;</item>
///   <item>arma los <b>conjuntos</b> como componentes conexos del grafo grupo ↔ cuenta: dos grupos que comparten una cuenta van
///         juntos, y un grupo cuya bodega tiene una regla propia junta las dos cuentas;</item>
///   <item>lee el saldo de cada cuenta y sucursal <b>sólo</b> por <see cref="MovimientosContables"/>, con alcance
///         <see cref="AlcanceDeSucursales.SinRestriccion"/> (la cifra se compara contra el valorizado total; el permiso lo pone la
///         ruta de Inventario, G5), hasta el corte inclusive: la apertura <c>AP</c> es el saldo inicial.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record InventoryAccountBalancesQuery(DateOnly Corte) : IRequest<Result<IReadOnlyList<ConjuntoDeCuentasDto>>>;

public sealed class InventoryAccountBalancesQueryValidator : AbstractValidator<InventoryAccountBalancesQuery>
{
    public InventoryAccountBalancesQueryValidator()
    {
        RuleFor(x => x.Corte).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de corte.");
    }
}

public sealed class InventoryAccountBalancesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<InventoryAccountBalancesQuery, Result<IReadOnlyList<ConjuntoDeCuentasDto>>>
{
    public async Task<Result<IReadOnlyList<ConjuntoDeCuentasDto>>> Handle(InventoryAccountBalancesQuery request, CancellationToken ct)
    {
        var corte = request.Corte;
        // Sin contabilidad iniciada no hay libros con qué comparar: quien pregunta (activación, conciliación) lo trata como «no
        // responde», no como «faltan reglas» (hallado por las e2e de I1 en I2, T471+).
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Failure<IReadOnlyList<ConjuntoDeCuentasDto>>(AccountingErrors.NotInitialized);
        var reglas =(await db.InventoryPostingRules.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.Role == R.Inventario || r.Role == R.Transito) && r.AccountingGroupCode != null
                            && r.ValidFrom <= corte && (r.ValidTo == null || r.ValidTo >= corte))
                .ToListAsync(ct))
            .OrderBy(r => r.Id)
            .ToList();
        if (reglas.Count == 0) return Result.Success<IReadOnlyList<ConjuntoDeCuentasDto>>([]);

        var ids = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        var contexto = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, reloj,
            new FiltrosDeInforme { From = corte, To = corte }, ct);
        if (contexto.IsFailure) return Result.Failure<IReadOnlyList<ConjuntoDeCuentasDto>>(contexto.Error);
        var saldos = await MovimientosContables.HastaInclusive(MovimientosContables.Base(db, contexto.Value), corte)
            .Where(e => ids.Contains(e.AccountId))
            .GroupBy(e => new { e.AccountId, e.BranchId })
            .Select(g => new { g.Key.AccountId, g.Key.BranchId, Saldo = g.Sum(e => e.Debit) - g.Sum(e => e.Credit) })
            .ToListAsync(ct);
        var idsDeSucursal = saldos.Select(s => s.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => idsDeSucursal.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);

        var conjuntos = new List<ConjuntoDeCuentasDto>();
        foreach (var componente in Componentes(reglas))
        {
            var cuentasDelConjunto = componente
                .GroupBy(r => r.AccountId)
                .Select(g =>
                {
                    var cuenta = cuentas.GetValueOrDefault(g.Key);
                    var rol = g.Any(r => r.Role == R.Inventario) ? R.Inventario : R.Transito;
                    var porSucursal = saldos.Where(s => s.AccountId == g.Key && s.Saldo != 0m)
                        .Select(s => new SaldoPorSucursalDto(sucursales.GetValueOrDefault(s.BranchId), s.Saldo))
                        .OrderBy(s => s.BranchPublicId)
                        .ToList();
                    return new CuentaDelConjuntoDto(cuenta?.Code ?? string.Empty, cuenta?.Name ?? string.Empty, rol, porSucursal);
                })
                .OrderBy(c => c.AccountCode, StringComparer.Ordinal)
                .ToList();
            var grupos = componente.Select(r => r.AccountingGroupCode!).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
            var pares = componente.Select(r => new ParGrupoBodegaDto(r.AccountingGroupCode!, r.WarehouseCode ?? "*")).Distinct()
                .OrderBy(p => p.AccountingGroupCode, StringComparer.Ordinal).ThenBy(p => p.WarehouseCode, StringComparer.Ordinal).ToList();
            conjuntos.Add(new ConjuntoDeCuentasDto(grupos, pares, cuentasDelConjunto, cuentasDelConjunto.Sum(c => c.BalanceByBranch.Sum(b => b.Balance))));
        }
        return Result.Success<IReadOnlyList<ConjuntoDeCuentasDto>>(conjuntos.OrderBy(c => c.AccountingGroupCodes[0], StringComparer.Ordinal).ToList());
    }

    /// <summary>Los componentes conexos del grafo grupo ↔ cuenta (unión por rango), cada uno con sus reglas.</summary>
    public static IReadOnlyList<IReadOnlyList<InventoryPostingRule>> Componentes(IReadOnlyList<InventoryPostingRule> reglas)
    {
        var padre = new Dictionary<string, string>(StringComparer.Ordinal);
        string Raiz(string x)
        {
            if (!padre.TryGetValue(x, out var p)) { padre[x] = x; return x; }
            if (p == x) return x;
            var r = Raiz(p);
            padre[x] = r;
            return r;
        }
        foreach (var r in reglas)
        {
            var grupo = Raiz("G:" + r.AccountingGroupCode!.ToUpperInvariant());
            var cuenta = Raiz("A:" + r.AccountId);
            if (grupo != cuenta) padre[cuenta] = grupo;
        }
        return reglas.GroupBy(r => Raiz("G:" + r.AccountingGroupCode!.ToUpperInvariant()))
            .Select(g => (IReadOnlyList<InventoryPostingRule>)g.ToList())
            .ToList();
    }
}
