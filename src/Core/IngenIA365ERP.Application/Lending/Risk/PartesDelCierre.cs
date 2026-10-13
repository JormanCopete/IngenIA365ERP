using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Las referencias públicas de las líneas de un cierre (crédito, deudor, línea, sucursal), cargadas en bloque. (nuevo)</summary>
public sealed record ReferenciasDelCierre(
    IReadOnlyDictionary<int, LoanRefV1> Creditos,
    IReadOnlyDictionary<int, Guid> Personas,
    IReadOnlyDictionary<int, string> Lineas,
    IReadOnlyDictionary<int, Guid> Sucursales);

/// <summary>Una partida de una línea de hecho del cierre: el crédito y sus importes, con la dimensión de antes si es un traslado. (nuevo)</summary>
public sealed record PartidaDelCierre(int LoanId, int BranchId, int Tanda, LoanFactLineV1 Linea);

/// <summary>
/// Cómo se arman los contenidos del cierre (feature 013, US9, T0705; mensajes.md §10.17, §10.18; contracts/contabilidad.md §11.2, §11.3): las
/// dimensiones de una línea con su categoría contable, los saldos que se reclasifican (todo saldo con categoría: capital, intereses, pagos por
/// cuenta del asociado, suspenso en orden y el deterioro individual de cada concepto con <c>reasonCode = Individual</c>), el deterioro en delta con
/// su movimiento y los totales y la huella de una parte. Puro salvo <see cref="ReferenciasAsync"/>. (nuevo)
/// </summary>
public static class PartesDelCierre
{
    /// <summary>El <c>reasonCode</c> con que viaja el saldo del deterioro individual en un traslado (el constructor contable lo busca así).</summary>
    public const string Individual = "Individual";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Los saldos que se reclasifican, en orden (§11.2): los cargos por cobrar, los anticipos y saldos a favor no tienen categoría.</summary>
    public static IReadOnlyList<LoanConcept> ConceptosConCategoria { get; } =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.Insurance,
        LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense,
    ];

    public static async Task<ReferenciasDelCierre> ReferenciasAsync(IApplicationDbContext db, IReadOnlyCollection<MonthlyCloseLoan> lineas, CancellationToken ct)
    {
        var creditos = lineas.Select(l => l.LoanId).Distinct().ToList();
        var personas = lineas.Select(l => l.PersonId).Distinct().ToList();
        var lineasDeCredito = lineas.Select(l => l.CreditLineId).Distinct().ToList();
        var sucursales = lineas.Select(l => l.BranchId).Distinct().ToList();
        return new ReferenciasDelCierre(
            await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => new LoanRefV1 { LoanPublicId = l.PublicId, LoanNumber = l.Number }, ct),
            await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct),
            await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(c => lineasDeCredito.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct),
            await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => sucursales.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct));
    }

    /// <summary>Las dimensiones de la línea con la categoría contable <paramref name="categoria"/> (las mismas de <c>EmisionDeCartera.Dimensiones</c>).</summary>
    public static LoanFactDimensionsV1 Dimensiones(MonthlyCloseLoan l, RatingCategory categoria, ReferenciasDelCierre r) => new()
    {
        CreditLineCode = r.Lineas.GetValueOrDefault(l.CreditLineId) ?? string.Empty,
        ModalityCode = l.ModalityCode,
        SubsegmentCode = l.SubsegmentCode,
        RiskCategory = categoria.ToString(),
        RiskSubcategory = nameof(RatingSubcategory.None),
        IsEmployee = l.IsEmployee,
        InGracePeriod = false,
        Restructured = l.IsRestructured,
        WrittenOff = l.IsWrittenOff,
        CollectorClass = null,
        BranchPublicId = r.Sucursales.GetValueOrDefault(l.BranchId),
        ImpairmentModel = l.ImpairmentModel.ToString(),
        RiskSegment = l.ModalityCode,
        ValueUnit = "Pesos",
    };

    /// <summary>Los saldos por concepto que la línea guardó.</summary>
    public static IReadOnlyDictionary<LoanConcept, decimal> Saldos(MonthlyCloseLoan l)
    {
        var leidos = string.IsNullOrWhiteSpace(l.BalancesByConceptJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>(l.BalancesByConceptJson, Json) ?? [];
        var saldos = new Dictionary<LoanConcept, decimal>();
        foreach (var (clave, valor) in leidos)
            if (Enum.TryParse<LoanConcept>(clave, ignoreCase: true, out var concepto)) saldos[concepto] = valor;
        // Las líneas del base sin el detalle: lo que sí tiene la línea.
        if (saldos.Count == 0)
        {
            saldos[LoanConcept.Principal] = l.PrincipalBalance;
            saldos[LoanConcept.CurrentInterest] = l.CurrentInterestBalance;
            saldos[LoanConcept.DefaultInterest] = l.DefaultInterestBalance;
        }
        return saldos;
    }

    /// <summary>El deterioro individual de la línea por concepto.</summary>
    public static IReadOnlyDictionary<LoanConcept, decimal> Deterioro(MonthlyCloseLoan? l) =>
        l is null
            ? new Dictionary<LoanConcept, decimal>()
            : DeterioroPorConcepto.Repartir(l.PrincipalImpairment, l.InterestImpairment, l.OtherImpairment, Saldos(l))
                .ToDictionary(x => x.Concepto, x => x.Importe);

    /// <summary>
    /// Los importes de la reclasificación de un crédito que cambió de letra (§11.2): cada saldo con categoría al corte y el deterioro individual
    /// de cada concepto que traía del cierre anterior (el saldo vigente), éste con <c>reasonCode = Individual</c>.
    /// </summary>
    public static IReadOnlyList<ConceptAmountV1> Reclasificacion(MonthlyCloseLoan nueva, MonthlyCloseLoan anterior)
    {
        var saldos = Saldos(nueva);
        var importes = new List<ConceptAmountV1>();
        foreach (var concepto in ConceptosConCategoria)
        {
            var saldo = saldos.GetValueOrDefault(concepto);
            if (saldo != 0m)
                importes.Add(new ConceptAmountV1 { Concept = concepto.ToString(), Amount = saldo, OnBehalfOfAssociate = concepto == LoanConcept.Insurance });
        }
        foreach (var (concepto, deterioro) in Deterioro(anterior))
            if (deterioro != 0m) importes.Add(new ConceptAmountV1 { Concept = concepto.ToString(), Amount = deterioro, ReasonCode = Individual });
        return importes;
    }

    /// <summary>Los importes del deterioro en delta de un crédito: por concepto, cada movimiento con su código (C22).</summary>
    public static IReadOnlyList<ConceptAmountV1> DeterioroEnDelta(IReadOnlyDictionary<LoanConcept, MovimientoDelDeterioro> movimientos) =>
        movimientos.OrderBy(m => (int)m.Key)
            .SelectMany(m => m.Value.Partidas().Select(p => new ConceptAmountV1 { Concept = m.Key.ToString(), Amount = p.Importe, ReasonCode = p.Codigo }))
            .ToList();

    /// <summary>Los totales por combinación de dimensiones, concepto y movimiento.</summary>
    public static IReadOnlyList<LoanFactTotalV1> Totales(IReadOnlyList<LoanFactLineV1> lineas) =>
        lineas.GroupBy(l => l.Dimensions)
            .Select(g => new LoanFactTotalV1
            {
                Dimensions = g.Key,
                Amounts = g.SelectMany(l => l.Amounts).GroupBy(a => (a.Concept, a.ReasonCode, a.OnBehalfOfAssociate))
                    .OrderBy(a => a.Key.Concept, StringComparer.Ordinal).ThenBy(a => a.Key.ReasonCode, StringComparer.Ordinal)
                    .Select(a => new ConceptAmountV1 { Concept = a.Key.Concept, ReasonCode = a.Key.ReasonCode, OnBehalfOfAssociate = a.Key.OnBehalfOfAssociate, Amount = a.Sum(x => x.Amount) })
                    .ToList(),
                LoanCount = g.Select(l => l.Loan.LoanPublicId).Distinct().Count(),
            }).ToList();

    /// <summary>La huella de la lista por crédito que lleva el contenido.</summary>
    public static string Huella<T>(T lista) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(lista, Json)));

    /// <summary>El rótulo visible de una parte (<c>origin.number</c>, 30 caracteres): <c>CIE-{aaaamm}-v{versión}-{parte}</c> o <c>REA-…</c>.</summary>
    public static string Rotulo(bool reapertura, int anio, int mes, int version, int parte) =>
        string.Create(CultureInfo.InvariantCulture, $"{(reapertura ? "REA" : "CIE")}-{anio:0000}{mes:00}-v{version}-{parte:0000}");

    /// <summary>La huella de las líneas de un cierre, en orden de crédito (data-model §23.1, <c>LinesSha256</c>).</summary>
    public static string HuellaDeLineas(IEnumerable<MonthlyCloseLoan> lineas)
    {
        var texto = new StringBuilder();
        foreach (var l in lineas.OrderBy(l => l.LoanId))
            texto.Append(CultureInfo.InvariantCulture, $"{l.LoanId}|{(int)l.FinalRating}|{(int)l.FinalSubcategory}|{(int)l.AccountingRating}|{(int)l.RatingCause}|")
                .Append(CultureInfo.InvariantCulture, $"{l.PrincipalBalance}|{l.TotalImpairment}|{l.DaysPastDueAtClose}\n");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }
}
