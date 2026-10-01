using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;

/// <summary>
/// El historial de vigencias de una clave, la más reciente primero (feature 012, T21, T072; contracts/api.md §7,
/// <c>GET /api/inventory/parameters/{module}/{key}/history</c>). Con <see cref="ScopeKind"/> filtra por ámbito, y
/// con <see cref="ScopePublicId"/> por su entidad (inexistente o fuera del alcance: el 404 del resolutor). (nuevo)
/// </summary>
public sealed record GetParameterHistoryQuery(string Module, string Key, ParameterScopeKind? ScopeKind = null, Guid? ScopePublicId = null)
    : IRequest<Result<IReadOnlyList<ParameterHistoryItemDto>>>;

/// <summary>Una vigencia del historial. (nuevo)</summary>
public sealed record ParameterHistoryItemDto(
    Guid VersionPublicId,
    ParameterScopeKind ScopeKind,
    ReferenciaDeAmbitoDto? Scope,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    string? LegalSource,
    string? CreatedBy,
    DateTime CreatedAt);

public sealed class GetParameterHistoryQueryHandler(ILectorDeParametros lector, IResolutorDeAmbitoDeParametro resolutor)
    : IRequestHandler<GetParameterHistoryQuery, Result<IReadOnlyList<ParameterHistoryItemDto>>>
{
    public async Task<Result<IReadOnlyList<ParameterHistoryItemDto>>> Handle(GetParameterHistoryQuery request, CancellationToken ct)
    {
        var definicion = CatalogoDeParametros.Buscar(request.Module, request.Key);
        if (definicion is null)
            return Result.Failure<IReadOnlyList<ParameterHistoryItemDto>>(ErroresDeParametros.ClaveInexistente(request.Module, request.Key));

        int? ambitoId = null;
        if (request.ScopePublicId is { } publicId && request.ScopeKind is { } kind && kind != ParameterScopeKind.None)
        {
            var resuelto = await resolutor.ResolverAsync(kind, publicId, ct);
            if (resuelto.IsFailure) return Result.Failure<IReadOnlyList<ParameterHistoryItemDto>>(resuelto.Error);
            ambitoId = resuelto.Value.Id;
        }

        var vigencias = (await lector.VigenciasAsync(definicion.Modulo, definicion.Clave, ct))
            .Where(v => request.ScopeKind is null || v.ScopeKind == request.ScopeKind)
            .Where(v => ambitoId is null || v.ScopeId == ambitoId)
            .ToList();

        var ambitos = await ListParametersQueryHandler.DescribirAmbitosAsync(
            resolutor, vigencias.Where(v => v.ScopeKind != ParameterScopeKind.None), ct);

        IReadOnlyList<ParameterHistoryItemDto> historial = vigencias
            .OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.CreatedAt)
            .Select(v => new ParameterHistoryItemDto(v.PublicId, v.ScopeKind,
                v.ScopeKind == ParameterScopeKind.None ? null : ambitos.GetValueOrDefault((v.ScopeKind, v.ScopeId)),
                v.Value, v.ValidFrom, v.ValidTo, v.Reason, v.LegalSource, v.CreatedBy, v.CreatedAt))
            .ToList();
        return Result.Success(historial);
    }
}
