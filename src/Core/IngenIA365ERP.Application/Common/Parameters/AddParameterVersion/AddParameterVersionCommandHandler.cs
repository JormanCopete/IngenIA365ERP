using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;

/// <summary>
/// El único escritor de <c>COR_ParameterVersions</c> (feature 012, T21, T071; contracts/api.md §7;
/// <c>LosParametrosSeLeenEnUnSoloSitio</c>). En orden:
/// <list type="number">
/// <item>la clave existe en su catálogo (<c>Parameters.KeyNotFound</c>, 404);</item>
/// <item>el permiso propio de la clave, además del de la ruta (<c>Parameters.PermissionRequired</c>);</item>
/// <item>el ámbito y el valor son admitidos (<c>.ScopeNotAllowed</c>, <c>.ValueNotAllowed</c> con
/// <c>data.allowed</c>) y la fuente legal viene si la definición la exige (<c>Validation.Invalid</c>);</item>
/// <item>la entidad del ámbito existe y está al alcance (<see cref="IResolutorDeAmbitoDeParametro"/>, 404);</item>
/// <item>las reglas del módulo (<see cref="IReglasDeParametros"/>), que pueden rechazar o ampliar a una cadena;</item>
/// <item>sin cruces (<c>Parameters.Overlaps</c> si ya hay una vigencia que empieza ese día o después) y cierre de
/// la anterior la víspera.</item>
/// </list>
/// La idempotencia, la transacción y la auditoría (con el motivo y la diferencia de la vigencia cerrada) las ponen
/// los behaviors y el interceptor.
/// </summary>
public sealed class AddParameterVersionCommandHandler(
    IApplicationDbContext db,
    IPermissionChecker permisos,
    IResolutorDeAmbitoDeParametro resolutor,
    IReglasDeParametros reglas)
    : IRequestHandler<AddParameterVersionCommand, Result<AddParameterVersionResponse>>
{
    public async Task<Result<AddParameterVersionResponse>> Handle(AddParameterVersionCommand request, CancellationToken ct)
    {
        var definicion = CatalogoDeParametros.Buscar(request.Module, request.Key);
        if (definicion is null)
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.ClaveInexistente(request.Module, request.Key));

        if (definicion.PermisoAdicional is { } permiso && !await permisos.HasPermissionAsync(permiso, ct))
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.PermisoRequerido(permiso));

        if (!definicion.AdmiteAmbito(request.ScopeKind))
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.AmbitoNoAdmitido(definicion));

        var valor = definicion.Interpretar(request.Value, CatalogoDeParametros.EntregaVigente);
        if (!valor.Admitido)
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.ValorNoAdmitido(definicion, request.Value));

        if (definicion.ExigeFuenteLegal && string.IsNullOrWhiteSpace(request.LegalSource))
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.FuenteLegalRequerida(definicion));

        AmbitoDeParametro? ambito = null;
        if (request.ScopeKind != ParameterScopeKind.None && request.ScopePublicId is { } publicId)
        {
            var resuelto = await resolutor.ResolverAsync(request.ScopeKind, publicId, ct);
            if (resuelto.IsFailure) return Result.Failure<AddParameterVersionResponse>(resuelto.Error);
            ambito = resuelto.Value;
        }

        var alta = new AltaDeParametro(definicion, request.ScopeKind, ambito, NuloSiVacio(request.Chain), valor.Texto!,
            request.ValidFrom, request.ConfirmFiscalWithoutPosting);
        var decision = await reglas.EvaluarAsync(alta, ct);
        if (decision.IsFailure) return Result.Failure<AddParameterVersionResponse>(decision.Error);

        // Los Ids de ámbito que se escriben: el general (0), la entidad pedida o, con una cadena, los que decidió el módulo.
        IReadOnlyList<int> ids;
        if (decision.Value.Ambitos is { } ambitos)
            ids = ambitos.Select(a => a.Id).Distinct().ToList();
        else if (alta.Chain is not null)
            return Result.Failure<AddParameterVersionResponse>(new Error(Error.Validation.Code,
                $"La cadena «{alta.Chain}» no se puede resolver: indicá el tipo de documento (scopePublicId)."));
        else if (request.ScopeKind != ParameterScopeKind.None && ambito is null)
            return Result.Failure<AddParameterVersionResponse>(new Error(Error.Validation.Code,
                "Indicá la entidad del ámbito (scopePublicId)."));
        else
            ids = [ambito?.Id ?? 0];

        var existentes = await db.ParameterVersions
            .Where(v => !v.IsDeleted && v.Module == definicion.Modulo && v.Key == definicion.Clave && v.ScopeKind == request.ScopeKind && ids.Contains(v.ScopeId))
            .ToListAsync(ct);

        var posterior = existentes.Where(v => v.ValidFrom >= request.ValidFrom).OrderBy(v => v.ValidFrom).FirstOrDefault();
        if (posterior is not null)
            return Result.Failure<AddParameterVersionResponse>(ErroresDeParametros.SeCruza(posterior.ValidFrom));

        var vispera = request.ValidFrom.AddDays(-1);
        DateOnly? cerradaEl = null;
        var creadas = new List<Guid>(ids.Count);
        foreach (var id in ids)
        {
            var anterior = existentes
                .Where(v => v.ScopeId == id)
                .OrderByDescending(v => v.ValidFrom)
                .FirstOrDefault();
            if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo > vispera))
            {
                anterior.ValidTo = vispera;
                cerradaEl = vispera;
            }

            var nueva = new ParameterVersion
            {
                Module = definicion.Modulo,
                Key = definicion.Clave,
                ScopeKind = request.ScopeKind,
                ScopeId = id,
                Value = valor.Texto!,
                ValidFrom = request.ValidFrom,
                Reason = request.Reason.Trim(),
                LegalSource = NuloSiVacio(request.LegalSource),
            };
            db.ParameterVersions.Add(nueva);
            creadas.Add(nueva.PublicId);
        }

        await db.SaveChangesAsync(ct);

        var afectados = decision.Value.AfectadosPorTipoDeDocumento?
            .Select(a => new ReferenciaDeAmbitoDto(a.PublicId, a.Code, a.Name))
            .ToList();
        return Result.Success(new AddParameterVersionResponse(creadas, cerradaEl, afectados));
    }

    private static string? NuloSiVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
