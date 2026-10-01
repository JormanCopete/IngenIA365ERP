using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Queries;

/// <summary>Los filtros de la lista de documentos (§9.2). Todos opcionales y combinables. (nuevo)</summary>
public sealed record FiltrosDeDocumentos(
    DocumentClassGroup? Group = null,
    DocumentClass? Class = null,
    Guid? DocumentTypePublicId = null,
    DocumentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    Guid? WarehousePublicId = null,
    Guid? CounterpartyPersonPublicId = null,
    string? Number = null,
    string? Search = null);

/// <summary>
/// <c>GET /api/inventory/documents</c> y la lista de cada grupo (feature 012, T148; contracts/api.md §9.2): sólo de los
/// grupos cuyo <c>View</c> tiene quien pregunta (<c>Costing</c> exige además <c>Inventory.Costs.Read</c>) y de las bodegas
/// de su alcance (origen o destino; sin bodega, por sus orígenes). Orden: fecha de operación y número, descendente.
/// Lo que está fuera no aparece ni cuenta. (nuevo)
/// </summary>
public sealed record ListInventoryDocumentsQuery(FiltrosDeDocumentos Filtros, PageRequest Pagina)
    : IRequest<Result<PagedResult<DocumentSummaryDto>>>;

public sealed class ListInventoryDocumentsQueryValidator : AbstractValidator<ListInventoryDocumentsQuery>
{
    public ListInventoryDocumentsQueryValidator()
    {
        RuleFor(x => x.Filtros).NotNull();
        RuleFor(x => x.Pagina).NotNull();
        RuleFor(x => x.Filtros.Search).MaximumLength(100);
        RuleFor(x => x.Filtros)
            .Must(f => f.From is null || f.To is null || f.From <= f.To)
            .WithMessage("La fecha inicial es posterior a la final.");
    }
}

public sealed class ListInventoryDocumentsQueryHandler(
    IApplicationDbContext db,
    IMaestrosDelDocumento maestros,
    IAlcanceDeInventario alcanceDeLaPeticion,
    VistaDeDocumentos vista)
    : IRequestHandler<ListInventoryDocumentsQuery, Result<PagedResult<DocumentSummaryDto>>>
{
    public async Task<Result<PagedResult<DocumentSummaryDto>>> Handle(ListInventoryDocumentsQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var pagina = request.Pagina.SafePage;
        var tamano = request.Pagina.SafePageSize;
        var vacia = Result.Success(new PagedResult<DocumentSummaryDto>([], pagina, tamano, 0));

        // Las clases que puede ver: las de los grupos cuyo View tiene (y el pedido, si viene).
        var clases = new List<DocumentClass>();
        foreach (var grupo in Enum.GetValues<DocumentClassGroup>())
        {
            if (f.Group is { } pedido && pedido != grupo) continue;
            if (await vista.VeGrupoAsync(grupo, ct)) clases.AddRange(ClasesDeDocumento.DelGrupo(grupo));
        }
        if (f.Class is { } clase)
            clases = clase == DocumentClass.Voiding ? clases : clases.Where(c => c == clase).ToList();
        if (clases.Count == 0) return vacia;

        var visibles = clases.ToArray();
        var soloAnulaciones = f.Class == DocumentClass.Voiding;
        var consulta = db.InventoryDocuments.AsNoTracking().Where(d =>
            (!soloAnulaciones && visibles.Contains(d.Class))
            || (d.Class == DocumentClass.Voiding && db.InventoryDocuments.Any(o => o.Id == d.VoidsDocumentId && visibles.Contains(o.Class))));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        consulta = consulta.DocumentosVisibles(alcance, db.DocumentLinks.AsNoTracking(), db.InventoryDocuments.AsNoTracking());

        if (f.DocumentTypePublicId is { } tipo) consulta = consulta.Where(d => d.DocumentType!.PublicId == tipo);
        if (f.Status is { } estado) consulta = consulta.Where(d => d.Status == estado);
        if (f.From is { } desde) consulta = consulta.Where(d => d.OperationDate >= desde);
        if (f.To is { } hasta) consulta = consulta.Where(d => d.OperationDate <= hasta);
        if (f.WarehousePublicId is { } bodegaPedida)
        {
            var bodega = (await maestros.BodegasAsync([bodegaPedida], ct)).FirstOrDefault();
            if (bodega is null) return vacia;
            consulta = consulta.Where(d => d.WarehouseId == bodega.Id || d.DestinationWarehouseId == bodega.Id);
        }
        if (f.CounterpartyPersonPublicId is { } persona)
            consulta = consulta.Where(d => db.People.Any(p => p.Id == d.CounterpartyPersonId && p.PublicId == persona));
        if (!string.IsNullOrWhiteSpace(f.Number))
        {
            var texto = f.Number.Trim();
            var digitos = new string(texto.SkipWhile(c => !char.IsDigit(c)).ToArray());
            var prefijo = texto[..(texto.Length - digitos.Length)].ToUpperInvariant();
            if (!long.TryParse(digitos, out var numero)) return vacia;
            consulta = consulta.Where(d => d.Number == numero && (prefijo == string.Empty || d.Prefix == prefijo));
        }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var buscado = f.Search.Trim();
            consulta = consulta.Where(d => (d.ExternalReference != null && d.ExternalReference.Contains(buscado))
                                           || (d.Notes != null && d.Notes.Contains(buscado))
                                           || (d.Reason != null && d.Reason.Contains(buscado)));
        }

        var total = await consulta.LongCountAsync(ct);
        var documentos = await consulta
            .OrderByDescending(d => d.OperationDate).ThenByDescending(d => d.Number).ThenByDescending(d => d.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .ToListAsync(ct);
        return Result.Success(new PagedResult<DocumentSummaryDto>(await vista.ResumenesAsync(documentos, ct), pagina, tamano, total));
    }
}

/// <summary>
/// <c>GET /api/inventory/documents/{id}</c> y el detalle de cada grupo (T148; §9.2): exige además el <c>View</c> del grupo
/// del documento; fuera del alcance, de otro grupo o sin permiso, el mismo 404. (nuevo)
/// </summary>
public sealed record GetInventoryDocumentQuery(Guid DocumentPublicId, DocumentClassGroup? ExpectedGroup = null)
    : IRequest<Result<InventoryDocumentDto>>;

public sealed class GetInventoryDocumentQueryValidator : AbstractValidator<GetInventoryDocumentQuery>
{
    public GetInventoryDocumentQueryValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
    }
}

/// <summary>El alcance lo aplica <see cref="VistaDeDocumentos.BuscarAsync"/> con <see cref="IAlcanceDeInventario"/>.</summary>
public sealed class GetInventoryDocumentQueryHandler(VistaDeDocumentos vista)
    : IRequestHandler<GetInventoryDocumentQuery, Result<InventoryDocumentDto>>
{
    public async Task<Result<InventoryDocumentDto>> Handle(GetInventoryDocumentQuery request, CancellationToken ct)
    {
        var documento = await vista.BuscarAsync(request.DocumentPublicId, request.ExpectedGroup, seguir: false, ct);
        if (documento is null) return Result.Failure<InventoryDocumentDto>(InventoryErrors.DocumentNotFound());

        var grupo = VistaDeDocumentos.GrupoDe(documento.Class, await vista.ClaseDelOriginalAsync(documento, ct));
        if (!await vista.VeGrupoAsync(grupo, ct)) return Result.Failure<InventoryDocumentDto>(InventoryErrors.DocumentNotFound());

        return Result.Success(await vista.DetalleAsync(documento, [], ct));
    }
}
