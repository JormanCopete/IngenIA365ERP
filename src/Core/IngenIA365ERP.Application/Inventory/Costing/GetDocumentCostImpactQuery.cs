using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Costing;

/// <summary>
/// <c>POST /api/inventory/documents/{id}/cost-impact</c> (feature 012, I5, US16, T840; FR-045; contracts/api.md §9.3): el impacto en
/// costos de un borrador antes de confirmarlo. Es <b>consulta</b>: sin clave de idempotencia y sin guardar nada. Corre el mismo camino que
/// la confirmación —las reglas de la clase y su efecto hasta el registro del kardex, en modo simulación (<see cref="RegistroDeKardex.Simular"/>),
/// que para cada (producto, ámbito) retroactivo llama <c>MotorDeCosteo.SimularImpacto</c>— y descarta lo que el efecto haya tocado en el
/// seguimiento. Así lo que muestra es lo que luego escribe la confirmación: los mismos rechazos (<c>Inventory.Costing.RetroactiveNotAllowed</c>,
/// <c>.RetroactiveTooOld</c>, <c>.RetroactiveRequiresWeightedAverage</c>, <c>Inventory.Stock.Insufficient</c>) y los mismos documentos
/// afectados. Exige el <c>{Grupo}.Create</c> del grupo del documento y <c>Inventory.Costs.Read</c>; sin ellos, o fuera de alcance, el 404
/// del documento. (nuevo)
/// </summary>
public sealed record GetDocumentCostImpactQuery(Guid DocumentPublicId) : IRequest<Result<CostImpactDto>>;

/// <summary>Un documento afectado por el retroactivo, por producto: su porción en existencia y la vendida (api.md §9.3). (nuevo)</summary>
public sealed record CostImpactAffectedDto(Guid DocumentPublicId, string? DisplayNumber, CostImpactProductDto Product, decimal InventoryAmount, decimal SoldAmount);

/// <summary>El producto de un documento afectado. (nuevo)</summary>
public sealed record CostImpactProductDto(Guid PublicId, string Code, string Name);

/// <summary>La respuesta de <c>cost-impact</c>: si el borrador es retroactivo, los afectados y el total (api.md §9.3). (nuevo)</summary>
public sealed record CostImpactDto(bool Retroactive, IReadOnlyList<CostImpactAffectedDto> Affected, decimal Total);

public sealed class GetDocumentCostImpactQueryHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    EfectosDeClase efectos,
    RegistroDeKardex registro)
    : IRequestHandler<GetDocumentCostImpactQuery, Result<CostImpactDto>>
{
    public async Task<Result<CostImpactDto>> Handle(GetDocumentCostImpactQuery request, CancellationToken ct)
    {
        try
        {
            return await CalcularAsync(request, ct);
        }
        finally
        {
            // Nada de lo que el efecto tocó se guarda: es una consulta.
            db.DescartarCambios();
        }
    }

    private async Task<Result<CostImpactDto>> CalcularAsync(GetDocumentCostImpactQuery request, CancellationToken ct)
    {
        var documento = await vista.BuscarAsync(request.DocumentPublicId, null, seguir: true, ct);
        if (documento is null || documento.Class == DocumentClass.Voiding) return Result.Failure<CostImpactDto>(InventoryErrors.DocumentNotFound());
        var grupo = VistaDeDocumentos.GrupoDe(documento.Class, null);
        if (!await vista.TieneAsync(PermisosDeGrupo.De(grupo).Create, ct) || !await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct))
            return Result.Failure<CostImpactDto>(InventoryErrors.DocumentNotFound());
        if (documento.Status != DocumentStatus.Draft) return Result.Failure<CostImpactDto>(InventoryErrors.NotDraft(documento.Status));

        var tipo = documento.DocumentType ?? await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstAsync(t => t.Id == documento.DocumentTypeId, ct);
        var estrategia = efectos.Para(documento.Class);
        if (estrategia.IsFailure) return Result.Failure<CostImpactDto>(estrategia.Error);
        var efecto = estrategia.Value;
        var contexto = new ContextoDeEfecto(documento, tipo, ClasesDeDocumento.De(documento.Class));

        var reglas = await efecto.ValidarAsync(contexto, ct);
        if (reglas.IsFailure) return Result.Failure<CostImpactDto>(reglas.Error);

        registro.Simular();
        var aplicado = await efecto.AplicarAsync(contexto, ct);
        var impacto = registro.Simulado;
        if (impacto is null)
            return aplicado.IsFailure ? Result.Failure<CostImpactDto>(aplicado.Error) : Result.Success(new CostImpactDto(false, [], 0m));

        var filas = impacto.PorAmbito.SelectMany(a => a.Impacto.Afectados.Select(d => (a.ProductId, Afectado: d))).ToList();
        var documentos = filas.Select(f => (int)f.Afectado.DocumentId).Distinct().ToList();
        var productos = filas.Select(f => f.ProductId).Distinct().ToList();
        var porDocumento = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => documentos.Contains(d.Id))
            .Select(d => new { d.Id, d.PublicId, d.Prefix, d.Number }).ToDictionaryAsync(d => d.Id, ct);
        var porProducto = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => productos.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.Code, p.Name }).ToDictionaryAsync(p => p.Id, ct);

        var afectados = filas
            .GroupBy(f => (Documento: (int)f.Afectado.DocumentId, f.ProductId))
            .Select(g =>
            {
                var d = porDocumento[g.Key.Documento];
                var p = porProducto[g.Key.ProductId];
                return new CostImpactAffectedDto(d.PublicId, VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number),
                    new CostImpactProductDto(p.PublicId, p.Code, p.Name),
                    g.Sum(x => x.Afectado.EnExistencia), g.Sum(x => x.Afectado.Vendida));
            })
            .ToList();
        return Result.Success(new CostImpactDto(impacto.EsRetroactivo, afectados, afectados.Sum(a => a.InventoryAmount + a.SoldAmount)));
    }
}
