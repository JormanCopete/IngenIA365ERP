using IngenIA365ERP.Application.Channel.Settings;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>
/// Feature 021 (FR-020, FR-057): la versión vigente de cada texto legal, para que el canal la muestre al activar y la registre aceptada
/// con su número. El tipo viaja por nombre (<c>Terms</c>, <c>DataPolicy</c>…) y no sale quién la publicó.
/// </summary>
public sealed record GetChannelLegalTextsForServiceQuery : IRequest<Result<IReadOnlyList<TextoLegalDelServicioDto>>>;

public sealed record TextoLegalDelServicioDto(string Kind, int Version, string Content, DateTime PublishedAt);

public sealed class GetChannelLegalTextsForServiceQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetChannelLegalTextsForServiceQuery, Result<IReadOnlyList<TextoLegalDelServicioDto>>>
{
    public async Task<Result<IReadOnlyList<TextoLegalDelServicioDto>>> Handle(GetChannelLegalTextsForServiceQuery request, CancellationToken ct)
    {
        var r = await new ListChannelLegalTextsQueryHandler(db).Handle(new ListChannelLegalTextsQuery(), ct);
        if (!r.IsSuccess) return Result.Failure<IReadOnlyList<TextoLegalDelServicioDto>>(r.Error);
        return Result.Success<IReadOnlyList<TextoLegalDelServicioDto>>(
            r.Value.Select(t => new TextoLegalDelServicioDto(t.Kind.ToString(), t.Version, t.Content, t.PublishedAt)).ToList());
    }
}
