using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// Las 34 clases fijas con su comportamiento (feature 012, T150; contracts/api.md §8, <c>GET /document-types/classes</c>).
/// <c>operable</c> = la entrega de la clase está en este despliegue. (nuevo)
/// </summary>
public sealed record ListDocumentClassesQuery : IRequest<Result<IReadOnlyList<DocumentClassDto>>>;

public sealed class ListDocumentClassesQueryValidator : AbstractValidator<ListDocumentClassesQuery> { }

public sealed class ListDocumentClassesQueryHandler : IRequestHandler<ListDocumentClassesQuery, Result<IReadOnlyList<DocumentClassDto>>>
{
    public Task<Result<IReadOnlyList<DocumentClassDto>>> Handle(ListDocumentClassesQuery request, CancellationToken ct) =>
        Task.FromResult(Result.Success<IReadOnlyList<DocumentClassDto>>(ClasesDeDocumento.Todas.Select(c => new DocumentClassDto(
            c.Class, c.Group, c.Description, c.IsFiscal, c.FiscalDirection, c.Messages, c.Chain, c.NumberedBy,
            c.AvailableFrom.ToString(), c.Operable())).ToList()));
}

/// <summary>Los tipos de documento, por clase y código (T150; §8, <c>GET /?group=&amp;class=&amp;includeInactive=</c>). (nuevo)</summary>
public sealed record ListInventoryDocumentTypesQuery(DocumentClassGroup? Group = null, DocumentClass? Class = null, bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<DocumentTypeDto>>>;

public sealed class ListInventoryDocumentTypesQueryValidator : AbstractValidator<ListInventoryDocumentTypesQuery> { }

public sealed class ListInventoryDocumentTypesQueryHandler(IApplicationDbContext db, VistaDeTiposDeDocumento vista)
    : IRequestHandler<ListInventoryDocumentTypesQuery, Result<IReadOnlyList<DocumentTypeDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentTypeDto>>> Handle(ListInventoryDocumentTypesQuery request, CancellationToken ct)
    {
        var consulta = db.InventoryDocumentTypes.AsNoTracking();
        if (!request.IncludeInactive) consulta = consulta.Where(t => t.IsActive);
        if (request.Class is { } clase) consulta = consulta.Where(t => t.Class == clase);
        if (request.Group is { } grupo)
        {
            var clases = ClasesDeDocumento.DelGrupo(grupo).ToArray();
            consulta = consulta.Where(t => clases.Contains(t.Class));
        }
        var tipos = await consulta.OrderBy(t => t.Class).ThenBy(t => t.Code).ToListAsync(ct);
        return Result.Success(await vista.ArmarAsync(tipos, conHistorial: false, ct));
    }
}

/// <summary>Un tipo con el historial de sus consecutivos (T150; §8, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetInventoryDocumentTypeQuery(Guid DocumentTypePublicId) : IRequest<Result<DocumentTypeDto>>;

public sealed class GetInventoryDocumentTypeQueryValidator : AbstractValidator<GetInventoryDocumentTypeQuery>
{
    public GetInventoryDocumentTypeQueryValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEmpty();
    }
}

public sealed class GetInventoryDocumentTypeQueryHandler(IApplicationDbContext db, VistaDeTiposDeDocumento vista)
    : IRequestHandler<GetInventoryDocumentTypeQuery, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(GetInventoryDocumentTypeQuery request, CancellationToken ct)
    {
        var tipo = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == request.DocumentTypePublicId, ct);
        return tipo is null
            ? Result.Failure<DocumentTypeDto>(InventoryErrors.DocumentTypeNotFound())
            : Result.Success((await vista.ArmarAsync([tipo], conHistorial: true, ct))[0]);
    }
}
