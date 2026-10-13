using FluentValidation;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Consultas;

/// <summary>
/// El saldo contable de las combinaciones de Cartera por conjunto de cuentas a un corte (feature 013, T0649; contracts/contabilidad.md §14.1;
/// FR-130, FR-141), detrás de <see cref="IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync"/>:
/// <list type="number">
///   <item>resuelve cada combinación (rol de saldo, dimensiones y sucursal; hecho <c>*</c>) con la matriz vigente al corte (§3.3); las que no
///         resuelven van en <c>UnresolvedCombinations</c> (índices de la entrada);</item>
///   <item>arma los conjuntos como componentes conexos del grafo combinación ↔ cuenta por familia de rol (<see cref="ConjuntosDeCuentas"/>;
///         la combinación es el rol y sus dimensiones, sin la sucursal): dos categorías en una auxiliar, o una combinación con reglas por
///         sucursal, quedan en un conjunto;</item>
///   <item>lee el saldo de cada cuenta y sucursal <b>sólo</b> por <see cref="MovimientosContables"/>, hasta el corte inclusive y con
///         <see cref="AlcanceDeSucursales.SinRestriccion"/>: la apertura <c>AP</c> es saldo inicial.</item>
/// </list>
/// Los roles de la entrada son sólo los de saldo (C29): un rol de resultado o desconocido es defecto del llamador y el validador lo rechaza.
/// Sin contabilidad iniciada, <c>Accounting.NotInitialized</c>. Las cuentas que devuelve son dato para mostrar: Cartera nunca las guarda.
/// (nuevo)
/// </summary>
public sealed record GetLendingAccountBalancesQuery(DateOnly Corte, IReadOnlyList<CombinacionDeSaldo> Combinaciones)
    : IRequest<Result<SaldosMapeadosDeCarteraDto>>;

public sealed class GetLendingAccountBalancesQueryValidator : AbstractValidator<GetLendingAccountBalancesQuery>
{
    public GetLendingAccountBalancesQueryValidator()
    {
        RuleFor(x => x.Corte).NotEqual(default(DateOnly)).WithMessage("Indique la fecha de corte.");
        RuleFor(x => x.Combinaciones).NotNull();
        RuleForEach(x => x.Combinaciones)
            .Must(c => c is not null && EsRolDeSaldo(c.Role))
            .WithMessage(c => "Cada combinación lleva un rol de saldo de la matriz de Cartera (Capital, InteresPorCobrar, …): un rol de resultado o desconocido no tiene saldo que conciliar.");
    }

    /// <summary>Los roles que admite el contrato (§16.1, C29): los de hecho <c>*</c>, es decir, todos menos los de resultado.</summary>
    public static bool EsRolDeSaldo(string? rol) => R.Buscar(rol) is { AdmiteHecho: false };
}

public sealed class GetLendingAccountBalancesQueryHandler(IApplicationDbContext db, ResolutorDeReglasDeCartera resolutor, IDateTimeService reloj)
    : IRequestHandler<GetLendingAccountBalancesQuery, Result<SaldosMapeadosDeCarteraDto>>
{
    public async Task<Result<SaldosMapeadosDeCarteraDto>> Handle(GetLendingAccountBalancesQuery request, CancellationToken ct)
    {
        var corte = request.Corte;
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct))
            return Result.Failure<SaldosMapeadosDeCarteraDto>(AccountingErrors.NotInitialized);
        if (request.Combinaciones.Count == 0) return Result.Success(new SaldosMapeadosDeCarteraDto([], []));

        var matriz = await resolutor.CargarAsync(corte, corte, ct);
        var aristas = new List<(int Indice, string Familia, string Rol, int AccountId)>();
        var sinResolver = new List<int>();
        for (var i = 0; i < request.Combinaciones.Count; i++)
        {
            var c = request.Combinaciones[i];
            var valores = new ValoresDeCartera(c.CreditLineCode, c.ModalityCode, c.SubsegmentCode, c.RiskCategory, c.ConceptCode, c.IsEmployee,
                c.InGracePeriod, c.OnBehalfOfAssociate, c.Restructured, c.CollectorClass, c.ReasonCode, c.BranchPublicId);
            var r = matriz.Resolver(new PeticionDeReglaDeCartera(LendingPostingRule.Cualquiera, c.Role, valores, corte, 1m, []));
            if (r.IsFailure || r.Value is null) sinResolver.Add(i);
            else aristas.Add((i, R.Buscar(c.Role)!.Familia.ToString(), c.Role, r.Value.Regla.AccountId));
        }
        if (aristas.Count == 0) return Result.Success(new SaldosMapeadosDeCarteraDto([], sinResolver));

        var ids = aristas.Select(a => a.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var contexto = await MovimientosContables.PrepararAsync(db, AlcanceDeSucursales.SinRestriccion, reloj,
            new FiltrosDeInforme { From = corte, To = corte }, ct);
        if (contexto.IsFailure) return Result.Failure<SaldosMapeadosDeCarteraDto>(contexto.Error);
        var saldos = await MovimientosContables.HastaInclusive(MovimientosContables.Base(db, contexto.Value), corte)
            .Where(e => ids.Contains(e.AccountId))
            .GroupBy(e => new { e.AccountId, e.BranchId })
            .Select(g => new { g.Key.AccountId, g.Key.BranchId, Saldo = g.Sum(e => e.Debit) - g.Sum(e => e.Credit) })
            .ToListAsync(ct);
        var idsDeSucursal = saldos.Select(s => s.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => idsDeSucursal.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);

        // La combinación es el rol con sus dimensiones, sin la sucursal: la misma combinación con reglas por sucursal junta sus cuentas.
        var claves = request.Combinaciones.Select(c => string.Join('|', c.Role, c.CreditLineCode, c.ModalityCode, c.SubsegmentCode,
            LendingPostingRule.Categoria(c.RiskCategory), c.ConceptCode, c.IsEmployee, c.InGracePeriod, c.OnBehalfOfAssociate, c.Restructured,
            c.CollectorClass, c.ReasonCode)).ToList();
        var conjuntos = ConjuntosDeCuentas.Componentes(aristas, a => claves[a.Indice], a => a.AccountId, a => a.Familia)
            .Select(componente =>
            {
                var cuentasDelConjunto = componente.GroupBy(a => a.AccountId).Select(g =>
                {
                    var cuenta = cuentas.GetValueOrDefault(g.Key);
                    var porSucursal = saldos.Where(s => s.AccountId == g.Key && s.Saldo != 0m)
                        .Select(s => new SaldoPorSucursalDto(sucursales.GetValueOrDefault(s.BranchId), s.Saldo))
                        .OrderBy(s => s.BranchPublicId).ToList();
                    return new CuentaDelConjuntoDto(cuenta?.Code ?? string.Empty, cuenta?.Name ?? string.Empty, g.First().Rol, porSucursal);
                }).OrderBy(c => c.AccountCode, StringComparer.Ordinal).ToList();
                return new ConjuntoDeCuentasDeCarteraDto(componente[0].Familia, componente.Select(a => a.Indice).Distinct().Order().ToList(),
                    cuentasDelConjunto, cuentasDelConjunto.Sum(c => c.BalanceByBranch.Sum(b => b.Balance)));
            })
            .OrderBy(c => c.Combinations[0])
            .ToList();
        return Result.Success(new SaldosMapeadosDeCarteraDto(conjuntos, sinResolver));
    }
}
