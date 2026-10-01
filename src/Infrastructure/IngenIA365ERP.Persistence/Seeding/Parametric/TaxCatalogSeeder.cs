using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T168; T22; decisiones-transversales §2.14, Order 81; contracts/plantillas.md §1): el catálogo tributario
/// inicial de cada cooperativa desde <c>Data/impuestos-co.json</c> (embebido): IVA 19 %, 5 %, exento y excluido; INC;
/// bolsas por unidad; ReteFuente por concepto con base mínima en UVT; ReteIVA; los conceptos de retención. ReteICA no se
/// siembra (es municipal). Cada tarifa lleva su norma y queda <b>«pendiente de validar por la contadora»</b>
/// (<c>ReviewPending = true</c>, A8) hasta que alguien con <c>Core.Taxes.Manage</c> la marque revisada.
///
/// <para>
/// Idempotente por código (conceptos e impuestos) y por código y vigencia (tarifas): lo que la cooperativa ya tiene —vivo
/// o de baja— no se toca, y una tarifa sembrada no se inserta si su código ya tiene otra vigencia viva que se cruce con
/// ella (la cooperativa la reemplazó con la plantilla). La tabla llega con el par <c>PlataformaParaInventario</c> (T186).
/// </para>
/// </summary>
public sealed class TaxCatalogSeeder : IDataSeeder
{
    public int Order => 81;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public const string Archivo = "impuestos-co.json";

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>La semilla leída del recurso embebido.</summary>
    public static SemillaTributaria Semilla() => RecursoJson.Leer<SemillaTributaria>(Archivo);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var semilla = Semilla();
        var insertadas = 0;

        var conceptos = await db.WithholdingConcepts.IgnoreQueryFilters().ToListAsync(ct);
        var porConcepto = conceptos.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var c in semilla.Conceptos)
        {
            if (porConcepto.ContainsKey(c.Codigo)) continue;
            var nuevo = new WithholdingConcept { Code = c.Codigo, Name = c.Nombre, IsActive = true, CreatedBy = SeedContext.ParametricCreatedBy };
            db.WithholdingConcepts.Add(nuevo);
            porConcepto[c.Codigo] = nuevo;
            insertadas++;
        }

        var impuestos = await db.TaxDefinitions.IgnoreQueryFilters().ToListAsync(ct);
        var porImpuesto = impuestos.GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var nuevos = new HashSet<TaxDefinition>(ReferenceEqualityComparer.Instance);
        foreach (var i in semilla.Impuestos)
        {
            if (porImpuesto.ContainsKey(i.Codigo)) continue;
            var nuevo = new TaxDefinition
            {
                Code = i.Codigo,
                Name = i.Nombre,
                Kind = i.Clase,
                CalculationForm = i.FormaDeCalculo,
                IsWithholding = i.Clase is TaxKind.ReteFuente or TaxKind.ReteIva or TaxKind.ReteIca,
                DianTaxCode = i.CodigoDian,
                IsActive = true,
                CreatedBy = SeedContext.ParametricCreatedBy,
            };
            db.TaxDefinitions.Add(nuevo);
            porImpuesto[i.Codigo] = nuevo;
            nuevos.Add(nuevo);
            insertadas++;
        }
        foreach (var i in semilla.Impuestos.Where(i => i.CalculadoSobre is not null))
        {
            var impuesto = porImpuesto[i.Codigo];
            if (nuevos.Contains(impuesto)) impuesto.TaxedOnDefinition = porImpuesto[i.CalculadoSobre!];
        }

        var tarifas = await db.TaxRates.IgnoreQueryFilters().ToListAsync(ct);
        foreach (var t in semilla.Tarifas)
        {
            var mismoCodigo = tarifas.Where(r => string.Equals(r.Code, t.Codigo, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mismoCodigo.Any(r => r.ValidFrom == t.VigenteDesde)) continue;
            if (mismoCodigo.Any(r => !r.IsDeleted && r.SeCruzaCon(t.VigenteDesde, t.VigenteHasta))) continue;

            var c = t.Condiciones ?? new CondicionesSembradas();
            var tarifa = new TaxRate
            {
                TaxDefinition = porImpuesto[t.Impuesto],
                Code = t.Codigo,
                Name = t.Nombre,
                Rate = t.Tarifa,
                AmountPerUnit = t.ValorPorUnidad,
                WithholdingConcept = t.Concepto is null ? null : porConcepto[t.Concepto],
                MunicipalityDaneCode = t.Municipio,
                ActivityCode = t.Actividad,
                MinimumBaseUvt = t.BaseMinimaUvt,
                MinimumBasePesos = t.BaseMinimaPesos,
                SubjectPersonType = c.SujetoTipoPersona,
                SubjectIsIncomeTaxFiler = c.SujetoDeclarante,
                SubjectIsVatResponsible = c.SujetoResponsableIva,
                SubjectIsLargeContributor = c.SujetoGranContribuyente,
                SubjectIsSelfWithholder = c.SujetoAutorretenedor,
                SubjectIsSimpleTaxRegime = c.SujetoRegimenSimple,
                AgentIsLargeContributor = c.AgenteGranContribuyente,
                AgentIsVatWithholdingAgent = c.AgenteRetenedorIva,
                AppliesTo = t.AplicaA,
                Priority = t.Prioridad,
                ValidFrom = t.VigenteDesde,
                ValidTo = t.VigenteHasta,
                LegalSource = t.Norma,
                ReviewPending = true,
                Notes = t.Notas,
                CreatedBy = SeedContext.ParametricCreatedBy,
            };
            db.TaxRates.Add(tarifa);
            tarifas.Add(tarifa);
            insertadas++;
        }

        if (insertadas > 0) await db.SaveChangesAsync(ct);
        return insertadas;
    }

    // ------------------------------------------------------------------------------------------ el archivo --

    public sealed class SemillaTributaria
    {
        public string Version { get; set; } = string.Empty;
        public List<ConceptoSembrado> Conceptos { get; set; } = [];
        public List<ImpuestoSembrado> Impuestos { get; set; } = [];
        public List<TarifaSembrada> Tarifas { get; set; } = [];
    }

    public sealed class ConceptoSembrado
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class ImpuestoSembrado
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public TaxKind Clase { get; set; }
        public TaxCalculationForm FormaDeCalculo { get; set; }
        public string? CalculadoSobre { get; set; }
        public string? CodigoDian { get; set; }
    }

    public sealed class TarifaSembrada
    {
        public string Codigo { get; set; } = string.Empty;
        public string Impuesto { get; set; } = string.Empty;
        public string? Concepto { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal? Tarifa { get; set; }
        public decimal? ValorPorUnidad { get; set; }
        public string? Municipio { get; set; }
        public string? Actividad { get; set; }
        public decimal? BaseMinimaUvt { get; set; }
        public decimal? BaseMinimaPesos { get; set; }
        public CondicionesSembradas? Condiciones { get; set; }
        public TaxAppliesTo AplicaA { get; set; } = TaxAppliesTo.Both;
        public short Prioridad { get; set; }
        public DateOnly VigenteDesde { get; set; }
        public DateOnly? VigenteHasta { get; set; }
        public string Norma { get; set; } = string.Empty;
        public string? Notas { get; set; }
    }

    public sealed class CondicionesSembradas
    {
        public string? SujetoTipoPersona { get; set; }
        public bool? SujetoDeclarante { get; set; }
        public bool? SujetoResponsableIva { get; set; }
        public bool? SujetoGranContribuyente { get; set; }
        public bool? SujetoAutorretenedor { get; set; }
        public bool? SujetoRegimenSimple { get; set; }
        public bool? AgenteGranContribuyente { get; set; }
        public bool? AgenteRetenedorIva { get; set; }
    }
}
