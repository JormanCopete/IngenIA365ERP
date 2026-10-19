using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>Una fila de preparación con la tanda de la que viene (para nombrar parte, tanda y fila en los errores entre partes). (nuevo)</summary>
public sealed record FilaConTanda<T>(T Fila, int Tanda);

/// <summary>
/// Una cuenta de la carga, de cuentas de ahorro o de aportes, con lo que necesitan la validación entre partes, el cuadre y la activación: su número (el
/// que conserva, o nulo en una de aportes sin número), la línea, la clase, la oficina, el titular y sus saldos por concepto al corte. (nuevo)
/// </summary>
public sealed record CuentaDeLaCarga(
    string Parte,
    int Tanda,
    int Fila,
    string? Numero,
    int TitularId,
    int LineaId,
    string LineaCodigo,
    LineNature Naturaleza,
    DepositClass? Clase,
    Guid OficinaPublicId,
    bool Inactiva,
    IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos,
    IssueTermBand? PlazoDeEmision = null)
{
    /// <summary>La llave de la cuenta en la carga: su número o, en una de aportes sin número, el titular.</summary>
    public string Llave => Numero ?? $"ASO:{TitularId}";

    /// <summary>El concepto al que se aplican las cuotas atrasadas: los aportes ordinarios o el capital.</summary>
    public MemberAccountConcept ConceptoDeCuotas => Naturaleza == LineNature.Contribution ? MemberAccountConcept.ContributionOrdinary : MemberAccountConcept.Principal;
}

/// <summary>
/// Las filas de las tandas vivas de una carga de aportes o captaciones, leídas una vez (feature 014, US9): las usan la validación entre partes, el cuadre,
/// las cifras de referencia y la activación. Sólo lee. (nuevo)
/// </summary>
public sealed class FilasDeLaCarga
{
    public required MigrationLoad Carga { get; init; }
    public required IReadOnlyList<MigrationLoadPart> Tandas { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationLineEquivalenceRow>> Equivalencias { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationAccountRow>> CuentasDeAhorro { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationContributionAccountRow>> CuentasDeAportes { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationAccountPartyRow>> PartesDeCuenta { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationAccountInstallmentRow>> Cuotas { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationGmfExemptionRow>> Exenciones { get; init; }
    public required IReadOnlyList<FilaConTanda<MigrationTaxYearRow>> Tributario { get; init; }

    /// <summary>US6 (pendiente de la fase 10): los CDAT vigentes al corte (parte <c>term-deposits</c>).</summary>
    public IReadOnlyList<FilaConTanda<MigrationTermDepositRow>> Cdat { get; init; } = [];

    /// <summary>US6: el saldo del interés por pagar con que nace cada CDAT, por número (ver <see cref="CdatMigrado.SaldoDeInteres"/>).</summary>
    public IReadOnlyDictionary<string, decimal> InteresDeCdat { get; init; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>Las cuentas de las dos partes, en el orden de la carga.</summary>
    public IReadOnlyList<CuentaDeLaCarga> Cuentas => _cuentas ??= ArmarCuentas();

    private IReadOnlyList<CuentaDeLaCarga>? _cuentas;

    public bool Vacia => Tandas.Count == 0;

    public static async Task<FilasDeLaCarga> LeerAsync(IApplicationDbContext db, MigrationLoad carga, CancellationToken ct)
    {
        var tandas = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).ToListAsync(ct);
        var numeroDe = tandas.ToDictionary(t => t.Id, t => t.BatchNumber);
        List<int> Ids(string parte) => tandas.Where(t => t.PartKey == parte).Select(t => t.Id).ToList();
        IReadOnlyList<FilaConTanda<T>> Con<T>(IEnumerable<T> filas, Func<T, int> parte) =>
            filas.Select(f => new FilaConTanda<T>(f, numeroDe[parte(f)])).ToList();

        var eq = Ids(P.LineEquivalences);
        var ah = Ids(P.MemberAccounts);
        var ap = Ids(P.ContributionAccounts);
        var pa = Ids(P.MemberAccountParties);
        var cu = Ids(P.AccountInstallments);
        var ex = Ids(P.GmfExemptions);
        var tr = Ids(P.TaxYearToDate);
        var td = Ids(P.TermDeposits);
        var cdat = Con(td.Count == 0 ? [] : await db.MigrationTermDepositRows.AsNoTracking().Where(r => td.Contains(r.MigrationLoadPartId))
            .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId);
        var interesDeCdat = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (cdat.Count > 0)
        {
            var lineas = await LineasDeLaCarga.LeerAsync(db, LineNature.Deposit, carga.CutoffDate, cdat.Select(c => c.Fila.CreditLineCode).Distinct().ToList(), ct);
            foreach (var c in cdat)
                interesDeCdat[c.Fila.SourceAccountNumber] = CdatMigrado.SaldoDeInteres(c.Fila, lineas.GetValueOrDefault(c.Fila.CreditLineCode)?.DeCaptacion,
                    carga.CutoffDate);
        }
        return new FilasDeLaCarga
        {
            Carga = carga,
            Tandas = tandas,
            Equivalencias = Con(eq.Count == 0 ? [] : await db.MigrationLineEquivalenceRows.AsNoTracking().Where(r => eq.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            CuentasDeAhorro = Con(ah.Count == 0 ? [] : await db.MigrationAccountRows.AsNoTracking().Where(r => ah.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            CuentasDeAportes = Con(ap.Count == 0 ? [] : await db.MigrationContributionAccountRows.AsNoTracking().Where(r => ap.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            PartesDeCuenta = Con(pa.Count == 0 ? [] : await db.MigrationAccountPartyRows.AsNoTracking().Where(r => pa.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            Cuotas = Con(cu.Count == 0 ? [] : await db.MigrationAccountInstallmentRows.AsNoTracking().Where(r => cu.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            Exenciones = Con(ex.Count == 0 ? [] : await db.MigrationGmfExemptionRows.AsNoTracking().Where(r => ex.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            Tributario = Con(tr.Count == 0 ? [] : await db.MigrationTaxYearRows.AsNoTracking().Where(r => tr.Contains(r.MigrationLoadPartId))
                .OrderBy(r => r.MigrationLoadPartId).ThenBy(r => r.RowNumber).ToListAsync(ct), r => r.MigrationLoadPartId),
            Cdat = cdat,
            InteresDeCdat = interesDeCdat,
        };
    }

    private IReadOnlyList<CuentaDeLaCarga> ArmarCuentas()
    {
        var lista = new List<CuentaDeLaCarga>(CuentasDeAhorro.Count + CuentasDeAportes.Count);
        foreach (var (f, tanda) in CuentasDeAhorro.Select(x => (x.Fila, x.Tanda)))
            lista.Add(new CuentaDeLaCarga(P.MemberAccounts, tanda, f.RowNumber, f.SourceAccountNumber, f.PersonId, f.CreditLineId, f.CreditLineCode,
                LineNature.Deposit, f.DepositClass, f.OfficePublicId, f.Status == MemberAccountStatus.Inactive,
                Sin0(new Dictionary<MemberAccountConcept, decimal>
                {
                    [MemberAccountConcept.Principal] = f.Principal,
                    [MemberAccountConcept.InterestPayable] = f.InterestPayable,
                    [MemberAccountConcept.ChargeReceivable] = f.ChargeReceivable,
                }), ContractualMigrado.PlazoDeEmision(f)));
        foreach (var (f, tanda) in CuentasDeAportes.Select(x => (x.Fila, x.Tanda)))
            lista.Add(new CuentaDeLaCarga(P.ContributionAccounts, tanda, f.RowNumber, f.SourceAccountNumber, f.PersonId, f.CreditLineId, f.CreditLineCode,
                LineNature.Contribution, null, f.OfficePublicId, false,
                Sin0(new Dictionary<MemberAccountConcept, decimal>
                {
                    [MemberAccountConcept.ContributionMinimum] = f.MinimumBalance,
                    [MemberAccountConcept.ContributionOrdinary] = f.OrdinaryBalance,
                    [MemberAccountConcept.ContributionExtraordinary] = f.ExtraordinaryBalance,
                    [MemberAccountConcept.ContributionRevaluation] = f.RevaluedBalance,
                })));
        // US6: los CDAT van al final (nacen después de las cuentas a la vista a las que abonan), con su plazo de emisión.
        foreach (var (f, tanda) in Cdat.Select(x => (x.Fila, x.Tanda)))
            lista.Add(new CuentaDeLaCarga(P.TermDeposits, tanda, f.RowNumber, f.SourceAccountNumber, f.PersonId, f.CreditLineId, f.CreditLineCode,
                LineNature.Deposit, DepositClass.TermDeposit, f.OfficePublicId, false,
                Sin0(new Dictionary<MemberAccountConcept, decimal>
                {
                    [MemberAccountConcept.Principal] = f.PrincipalAmount,
                    [MemberAccountConcept.InterestPayable] = InteresDeCdat.GetValueOrDefault(f.SourceAccountNumber, f.InterestPayable),
                }), f.IssueTermBand));
        return lista;
    }

    private static Dictionary<MemberAccountConcept, decimal> Sin0(Dictionary<MemberAccountConcept, decimal> saldos) =>
        saldos.Where(kv => kv.Value != 0m).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Las cuentas por su número (las de aportes sin número no están: no se pueden citar desde otra parte).</summary>
    public IReadOnlyDictionary<string, CuentaDeLaCarga> PorNumero() =>
        Cuentas.Where(c => c.Numero is not null).GroupBy(c => c.Numero!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    /// <summary>
    /// Los totales cargados por línea y concepto (las cifras de la carga, FR-118): Σ de cada concepto y cuántas cuentas lo traen. El cargo por cobrar es lo que
    /// el titular debe: va en positivo como total de su concepto.
    /// </summary>
    public IReadOnlyList<TotalDeCargaDto> Totales() =>
        Cuentas.SelectMany(c => c.Saldos.Select(s => (c.LineaCodigo, s.Key, s.Value)))
            .GroupBy(x => (x.LineaCodigo, x.Key))
            .OrderBy(g => g.Key.LineaCodigo, StringComparer.Ordinal).ThenBy(g => (int)g.Key.Key)
            .Select(g => new TotalDeCargaDto(g.Key.LineaCodigo, g.Key.Key.ToString(), g.Sum(x => x.Value), g.Count()))
            .ToList();
}
