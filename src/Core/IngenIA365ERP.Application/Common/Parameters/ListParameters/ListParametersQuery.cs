using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Common.Parameters.ListParameters;

/// <summary>
/// Las claves del catálogo con su valor a <see cref="AsOf"/> (por defecto <c>HoyLocal</c>), las excepciones por
/// ámbito vigentes y lo programado (feature 012, T21, T072; contracts/api.md §7, <c>GET /api/inventory/parameters</c>).
/// Lee por <see cref="ILectorDeParametros"/>: nunca la tabla directamente. (nuevo)
/// </summary>
public sealed record ListParametersQuery(string? Module = null, string? Key = null, DateOnly? AsOf = null)
    : IRequest<Result<IReadOnlyList<ParameterDto>>>;

/// <summary>El valor general a la fecha: el de una vigencia (<c>Source = "Version"</c>) o el defecto (<c>"Default"</c>). (nuevo)</summary>
public sealed record ParameterCurrentValueDto(
    string Value,
    string Source,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string? Reason,
    string? LegalSource,
    string? ChangedBy,
    DateTime? ChangedAt);

/// <summary>Una excepción vigente de un ámbito. <see cref="Scope"/> es nulo si el usuario no alcanza la entidad. (nuevo)</summary>
public sealed record ParameterOverrideDto(ParameterScopeKind ScopeKind, ReferenciaDeAmbitoDto? Scope, string Value, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>Una vigencia que empieza después de la fecha consultada. (nuevo)</summary>
public sealed record ParameterScheduledDto(ParameterScopeKind ScopeKind, ReferenciaDeAmbitoDto? Scope, string Value, DateOnly ValidFrom);

/// <summary>
/// Una clave para la pantalla de parámetros (contracts/api.md §7). <see cref="Type"/> y <see cref="AvailableFrom"/>
/// son texto de presentación, no enums. (nuevo)
/// </summary>
public sealed record ParameterDto(
    string Module,
    string Key,
    string Description,
    string Type,
    IReadOnlyList<string>? AllowedValues,
    string DefaultValue,
    IReadOnlyList<ParameterScopeKind> AllowedScopes,
    bool SealedOnConfirm,
    string? RequiredPermission,
    bool RequiresLegalSource,
    string AvailableFrom,
    ParameterCurrentValueDto Current,
    IReadOnlyList<ParameterOverrideDto> Overrides,
    IReadOnlyList<ParameterScheduledDto> Scheduled);

public sealed class ListParametersQueryHandler(
    ILectorDeParametros lector,
    IResolutorDeAmbitoDeParametro resolutor,
    IDateTimeService reloj)
    : IRequestHandler<ListParametersQuery, Result<IReadOnlyList<ParameterDto>>>
{
    public async Task<Result<IReadOnlyList<ParameterDto>>> Handle(ListParametersQuery request, CancellationToken ct)
    {
        var fecha = request.AsOf ?? reloj.HoyLocal;

        IReadOnlyList<DefinicionDeParametro> definiciones;
        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var una = CatalogoDeParametros.Buscar(request.Module, request.Key);
            if (una is null)
                return Result.Failure<IReadOnlyList<ParameterDto>>(ErroresDeParametros.ClaveInexistente(request.Module, request.Key));
            definiciones = [una];
        }
        else
        {
            definiciones = CatalogoDeParametros.Todas
                .Where(d => string.IsNullOrWhiteSpace(request.Module) || string.Equals(d.Modulo, request.Module.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var modulo = definiciones.Select(d => d.Modulo).Distinct().Count() == 1 ? definiciones[0].Modulo : null;
        IReadOnlyList<VigenciaDeParametro> vigencias = definiciones.Count == 0 ? [] : await lector.VigenciasAsync(modulo, definiciones.Count == 1 ? definiciones[0].Clave : null, ct);
        var ambitos = await DescribirAmbitosAsync(resolutor, vigencias.Where(v => v.ScopeKind != ParameterScopeKind.None), ct);

        var lista = new List<ParameterDto>(definiciones.Count);
        foreach (var d in definiciones)
        {
            var deLaClave = vigencias.Where(v => v.Module == d.Modulo && v.Key == d.Clave).ToList();
            var general = LectorDeParametros.VigenteA(deLaClave, fecha, ParameterScopeKind.None, 0);
            var actual = general is null
                ? new ParameterCurrentValueDto(d.DefectoSeguro, "Default", null, null, null, null, null, null)
                : new ParameterCurrentValueDto(general.Value, "Version", general.ValidFrom, general.ValidTo, general.Reason,
                    general.LegalSource, general.CreatedBy, general.CreatedAt);

            var excepciones = deLaClave
                .Where(v => v.ScopeKind != ParameterScopeKind.None && v.VigenteEn(fecha))
                .OrderBy(v => v.ScopeKind).ThenBy(v => v.ScopeId)
                .Select(v => new ParameterOverrideDto(v.ScopeKind, ambitos.GetValueOrDefault((v.ScopeKind, v.ScopeId)), v.Value, v.ValidFrom, v.ValidTo))
                .ToList();

            var programadas = deLaClave
                .Where(v => v.ValidFrom > fecha)
                .OrderBy(v => v.ValidFrom).ThenBy(v => v.ScopeKind).ThenBy(v => v.ScopeId)
                .Select(v => new ParameterScheduledDto(v.ScopeKind,
                    v.ScopeKind == ParameterScopeKind.None ? null : ambitos.GetValueOrDefault((v.ScopeKind, v.ScopeId)), v.Value, v.ValidFrom))
                .ToList();

            lista.Add(new ParameterDto(
                d.Modulo, d.Clave, d.Descripcion, d.Tipo.ToString(),
                d.Tipo == TipoDeParametro.Choice ? d.Admitidos(CatalogoDeParametros.EntregaVigente) : null,
                d.DefectoSeguro, d.AmbitosAdmitidos, d.SelladoAlConfirmar, d.PermisoAdicional, d.ExigeFuenteLegal,
                d.DisponibleDesde.ToString(), actual, excepciones, programadas));
        }

        return Result.Success<IReadOnlyList<ParameterDto>>(lista);
    }

    /// <summary>Cómo mostrar las entidades de ámbito de las vigencias, pidiéndoselas al resolutor por tipo.</summary>
    internal static async Task<Dictionary<(ParameterScopeKind, int), ReferenciaDeAmbitoDto>> DescribirAmbitosAsync(
        IResolutorDeAmbitoDeParametro resolutor, IEnumerable<VigenciaDeParametro> vigencias, CancellationToken ct)
    {
        var resultado = new Dictionary<(ParameterScopeKind, int), ReferenciaDeAmbitoDto>();
        foreach (var grupo in vigencias.GroupBy(v => v.ScopeKind))
        {
            var ids = grupo.Select(v => v.ScopeId).Distinct().ToList();
            foreach (var a in await resolutor.DescribirAsync(grupo.Key, ids, ct))
                resultado[(grupo.Key, a.Id)] = new ReferenciaDeAmbitoDto(a.PublicId, a.Code, a.Name);
        }
        return resultado;
    }
}
