using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El <c>PagoAplicado</c> con que se le pregunta a Contabilidad si un pago se puede contabilizar (feature 013, interfaces §3.12, A8): uno por combinación de
/// dimensiones —línea, modalidad, sub-segmento y sucursal del crédito, y la clase del recaudador—, sin importes reales (la validación sólo mira hecho,
/// sucursal y dimensiones). Un crédito sin condiciones selladas no tiene combinación y no se pregunta. Lo usan el contrato de obligaciones antes de aplicar
/// un lote y, desde el 2026-11-27 (feature 028, T037, FR-027a), la vista previa del desembolso para los créditos que recoge: así lo que bloquearía al
/// aplicar se ve antes de confirmar. (nuevo)
/// </summary>
public static class PagosParaLaValidacionPrevia
{
    /// <summary>Las dimensiones de un crédito que deciden la regla de su pago.</summary>
    public sealed record Combinacion(string Linea, string Modalidad, string? Subsegmento, Guid Sucursal);

    /// <summary>Las combinaciones distintas de <paramref name="obligaciones"/>, cada una con las obligaciones que la comparten.</summary>
    public static async Task<IReadOnlyList<(Combinacion Combinacion, IReadOnlyList<Guid> Obligaciones)>> CombinacionesAsync(IApplicationDbContext db,
        IReadOnlyCollection<Guid> obligaciones, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (obligaciones.Count == 0) return [];
        var publicos = obligaciones.Distinct().ToList();
        var filas = await (from l in db.Loans.AsNoTracking()
                           join c in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on l.CreditLineId equals c.Id
                           join t in db.LoanTermsVersions.AsNoTracking() on l.Id equals t.LoanId
                           join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on l.BranchId equals b.Id
                           where publicos.Contains(l.PublicId) && t.VersionNumber == 1
                           select new { l.PublicId, c.Code, t.ModalityCode, t.SubsegmentCode, Branch = b.PublicId }).Distinct().ToListAsync(ct);
        return filas.GroupBy(f => new Combinacion(f.Code, f.ModalityCode, f.SubsegmentCode, f.Branch))
            .Select(g => (g.Key, (IReadOnlyList<Guid>)g.Select(f => f.PublicId).ToList()))
            .ToList();
    }

    /// <summary>El <c>PagoAplicado</c> de una combinación, con la clase del recaudador.</summary>
    public static MensajeContableDto Hecho(Recaudador recaudador, Combinacion c)
    {
        ArgumentNullException.ThrowIfNull(recaudador);
        ArgumentNullException.ThrowIfNull(c);
        return new(
            new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = PagoAplicadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = c.Sucursal,
            },
            new PagoAplicadoV1
            {
                Dimensions = new LoanFactDimensionsV1
                {
                    CreditLineCode = c.Linea, ModalityCode = c.Modalidad, SubsegmentCode = c.Subsegmento,
                    RiskCategory = CategoriaContableDeCartera.CategoriaDeCreditoNuevo, RiskSubcategory = "None", CollectorClass = recaudador.Class.ToString(),
                    BranchPublicId = c.Sucursal, ImpairmentModel = "ArrearsAnnex1", RiskSegment = c.Modalidad, ValueUnit = "Pesos",
                },
                Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = 1m }],
            });
    }
}
