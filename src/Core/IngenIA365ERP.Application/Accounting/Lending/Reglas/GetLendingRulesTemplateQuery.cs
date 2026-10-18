using FluentValidation;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Accounting.Lending.Reglas.PlantillaDeMatrizDeCartera;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// <c>GET /api/accounting/lending/rules/template.xlsx</c> (feature 013, T0643; contracts/contabilidad.md §3.7; plantillas.md §0.6 y §9): la
/// plantilla llena, en el orden de <see cref="PlantillaDeMatrizDeCartera.Columnas"/>. Trae las reglas vigentes hoy y las que empiezan
/// después —así se exporta, se corrige y se vuelve a importar con el mismo libro— y, debajo, las <b>filas sugeridas sin cuenta</b> que todavía
/// no tienen regla: los roles por modalidad y categoría de las líneas activas (y los puentes, pasivos, contras de orden y deterioro general que
/// no dependen de la línea), marcadas «pendiente de validar». No se siembra ninguna regla (C70): una regla necesita cuenta, y la cuenta la
/// elige la contadora. Una fila sugerida que vuelve sin cuenta se ignora al importar. (nuevo)
/// </summary>
public sealed record GetLendingRulesTemplateQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetLendingRulesTemplateQueryValidator : AbstractValidator<GetLendingRulesTemplateQuery>;

public sealed class GetLendingRulesTemplateQueryHandler(IApplicationDbContext db, IDimensionesDeCartera dimensiones, IDateTimeService reloj)
    : IRequestHandler<GetLendingRulesTemplateQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetLendingRulesTemplateQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var reglas = await db.LendingPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.ValidTo == null || r.ValidTo >= hoy))
            .OrderBy(r => r.Role).ThenBy(r => r.DimensionKey).ThenBy(r => r.ValidFrom)
            .ToListAsync(ct);
        var idsCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code, ct);
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.LegacyCode ?? b.Name, ct);
        var centros = await db.CostCenters.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.LegacyCode ?? c.Name, ct);

        var filas = reglas.Select(r => (IReadOnlyList<object?>)
        [
            r.Operation, r.Role, r.CreditLineCode, r.ModalityCode, r.SubsegmentCode, r.RiskCategory, r.ConceptCode,
            SiNo(r.IsEmployee), SiNo(r.InGracePeriod), SiNo(r.OnBehalfOfAssociate), SiNo(r.Restructured), r.CollectorClass, r.ReasonCode,
            r.BranchId is { } b ? sucursales.GetValueOrDefault(b) : null,
            r.CostCenterId is { } c ? centros.GetValueOrDefault(c) : null,
            cuentas.GetValueOrDefault(r.AccountId), r.ValidFrom, r.ValidTo, r.Notes,
        ]).ToList();

        var catalogo = await dimensiones.CatalogoAsync(ct);
        var claves = reglas.Select(r => r.DimensionKey).ToHashSet(StringComparer.Ordinal);
        var desde = new DateOnly(hoy.Year, hoy.Month, 1);
        foreach (var s in Sugeridas(catalogo).Where(s => !claves.Contains(s.Clave)))
        {
            filas.Add(
            [
                LendingPostingRule.Cualquiera, s.Rol, null, s.Modalidad, null, s.Categoria, s.Concepto, null, null, null, null, s.Clase, s.Motivo,
                null, null, null, desde, null, P.PendienteDeValidar,
            ]);
        }

        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Datos"] = filas,
        }));
    }

    /// <summary>Una fila sugerida sin cuenta. (nuevo)</summary>
    public sealed record FilaSugerida(string Rol, string? Modalidad, string? Categoria, string? Concepto, string? Clase, string? Motivo)
    {
        public string Clave => LendingPostingRule.ClaveDe(LendingPostingRule.Cualquiera, Rol, null, Modalidad, null, Categoria, Concepto,
            null, null, null, null, Clase, Motivo, null);
    }

    /// <summary>
    /// Las filas sugeridas: por cada modalidad de las líneas activas y cada categoría, los roles de saldo y de orden con sus conceptos; por
    /// modalidad, el ingreso y el deterioro individual; y una vez, los roles que no dependen de la línea (puentes por clase de recaudador
    /// —<c>OwnLoanPayoff</c> siempre, T91— y por forma de giro, pasivos, contras de orden y deterioro general).
    /// </summary>
    public static IReadOnlyList<FilaSugerida> Sugeridas(CatalogoDeDimensionesDeCarteraDto catalogo)
    {
        var filas = new List<FilaSugerida>();
        var modalidades = catalogo.CreditLines.Where(l => l.Active).Select(l => l.ModalityCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        foreach (var m in modalidades)
        {
            foreach (var k in R.Categorias)
            {
                filas.Add(new(R.Capital, m, k, null, null, null));
                filas.Add(new(R.InteresPorCobrar, m, k, R.CurrentInterest, null, null));
                filas.Add(new(R.InteresPorCobrar, m, k, R.DefaultInterest, null, null));
                filas.Add(new(R.PagoPorCuentaDelAsociado, m, k, R.Insurance, null, null));
                foreach (var concepto in new[] { R.Principal, R.CurrentInterest, R.DefaultInterest, R.Insurance })
                    filas.Add(new(R.DeterioroIndividual, m, k, concepto, null, null));
                filas.Add(new(R.SuspensoEnOrden, m, k, R.CurrentInterestInSuspense, null, null));
                filas.Add(new(R.SuspensoEnOrden, m, k, R.DefaultInterestInSuspense, null, null));
            }
            foreach (var concepto in new[] { R.CurrentInterest, R.DefaultInterest, R.CurrentInterestInSuspense })
                filas.Add(new(R.Ingreso, m, null, concepto, null, null));
            foreach (var concepto in new[] { R.Principal, R.CurrentInterest, R.DefaultInterest, R.Insurance })
            {
                filas.Add(new(R.GastoDeterioro, m, null, concepto, null, R.Individual));
                filas.Add(new(R.RecuperacionDeterioro, m, null, concepto, null, R.Individual));
            }
            // K3 (US11, US14; 2026-10-15): el ingreso diferido de la capitalización en una reestructuración.
            filas.Add(new(R.IngresoDiferido, m, null, null, null, null));
        }

        // La castigada en orden por concepto (K1: la recuperación de lo migrado castigado; K3: el castigo) y el gasto del castigo o la condonación
        // por motivo y concepto (C7, «pendiente de validar»). Hasta el 2026-10-15 la plantilla no los sugería y un CreditoCastigado o una
        // CondonacionAprobada sin el faltante cubierto por el deterioro quedaban sin regla.
        filas.AddRange(R.Buscar(R.CastigadaEnOrden)!.Conceptos!.Select(c => new FilaSugerida(R.CastigadaEnOrden, null, null, c, null, null)));
        var gasto = R.Buscar(R.GastoDeCastigoOCondonacion)!;
        filas.AddRange(gasto.Motivos!.SelectMany(mo => gasto.Conceptos!.Select(c => new FilaSugerida(R.GastoDeCastigoOCondonacion, null, null, c, null, mo))));

        var clases = catalogo.CollectorClasses.Append(nameof(ClaseDeRecaudador.OwnLoanPayoff)).Distinct(StringComparer.Ordinal).ToList();
        filas.AddRange(clases.Select(c => new FilaSugerida(R.RecaudoPorAplicar, null, null, null, c, null)));
        filas.AddRange(R.Buscar(R.DesembolsoPorGirar)!.Motivos!.Select(mo => new FilaSugerida(R.DesembolsoPorGirar, null, null, null, null, mo)));
        filas.AddRange(R.Buscar(R.DeterioroGeneral)!.Motivos!.Select(mo => new FilaSugerida(R.DeterioroGeneral, null, null, null, null, mo)));
        filas.Add(new(R.GastoDeterioro, null, null, null, null, R.General));
        filas.Add(new(R.GastoDeterioro, null, null, null, null, R.Additional));
        filas.Add(new(R.RecuperacionDeterioro, null, null, null, null, R.General));
        foreach (var rol in new[] { R.ContraOrdenContingente, R.ContraOrdenControl, R.Anticipo, R.SaldoAFavor, R.SeguroPorPagar, R.CuentaPorPagarATercero,
                     R.PuenteComercial, R.CuentaPorCobrarComercial, R.IngresoPorRecuperacion })
            filas.Add(new(rol, null, null, null, null, null));
        return filas;
    }

    private static string? SiNo(bool? valor) => valor is null ? null : valor.Value ? "S" : "N";
}
