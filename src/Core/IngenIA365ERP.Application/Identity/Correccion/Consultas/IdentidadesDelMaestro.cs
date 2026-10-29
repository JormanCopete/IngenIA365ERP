using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Correccion.Consultas;

// Feature 017 (US6, T070): lo que soporte de la plataforma ve de las identidades. Sólo el maestro. La búsqueda
// es exacta —un correo, un usuario o un número— y no por fragmentos: es para resolver un caso concreto, no
// para recorrer el directorio de personas de todas las cooperativas.

public sealed record IdentidadResumenDto(
    Guid Id, string Email, string? DocumentType, string? DocumentNumber, string? DocumentUsername,
    string Nombre, string Status, bool TwoFactorEnabled, bool IsGlobalMasterAdmin,
    IReadOnlyList<MembresiaDto> Memberships);

public sealed record MembresiaDto(Guid TenantId, string TenantName, string Status, bool IsTenantAdmin);

public sealed record IdentidadDetalleDto(
    IdentidadResumenDto Identidad,
    IReadOnlyList<CambioDeDocumentoDto> History,
    IReadOnlyList<CasoDeIdentidadDto> Cases);

public sealed record CambioDeDocumentoDto(
    DateTime OccurredAt, string Reason, string ActorKind, string? OldUsername, string NewUsername, string? Notes);

public sealed record CasoDeIdentidadDto(
    Guid PublicId, string Kind, string Status, Guid CentralUserId, string? Email,
    Guid? OtherCentralUserId, string? OtherEmail, string? Detail, DateTime CreatedAt, string? Resolution);

public sealed record BuscarIdentidadesQuery(string? Q) : IRequest<Result<IReadOnlyList<IdentidadResumenDto>>>;

public sealed record GetIdentidadQuery(Guid Id) : IRequest<Result<IdentidadDetalleDto>>;

public sealed record ListarCasosDeIdentidadQuery(string? Status) : IRequest<Result<IReadOnlyList<CasoDeIdentidadDto>>>;

public sealed record DescartarCasoDeIdentidadCommand(Guid CasePublicId, string? Reason) : IRequest<Result<CasoDeIdentidadDto>>;

internal static class SoloMaestro
{
    public static Error? Verificar(ICurrentCentralUserContext u) =>
        u.IsAuthenticated && u.Purpose == CentralJwtPurposes.Full && u.IsGlobalMasterAdmin
            ? null
            : new Error("Saas.MasterOnly", "Sólo el maestro de plataforma.");
}

public sealed class IdentidadesDelMaestroHandlers(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext db,
    IDateTimeService clock)
    : IRequestHandler<BuscarIdentidadesQuery, Result<IReadOnlyList<IdentidadResumenDto>>>,
      IRequestHandler<GetIdentidadQuery, Result<IdentidadDetalleDto>>,
      IRequestHandler<ListarCasosDeIdentidadQuery, Result<IReadOnlyList<CasoDeIdentidadDto>>>,
      IRequestHandler<DescartarCasoDeIdentidadCommand, Result<CasoDeIdentidadDto>>
{
    public async Task<Result<IReadOnlyList<IdentidadResumenDto>>> Handle(BuscarIdentidadesQuery request, CancellationToken ct)
    {
        if (SoloMaestro.Verificar(currentUser) is { } e) return Result.Failure<IReadOnlyList<IdentidadResumenDto>>(e);
        var texto = request.Q?.Trim() ?? string.Empty;
        if (texto.Length == 0) return Result.Success<IReadOnlyList<IdentidadResumenDto>>([]);

        var halladas = new Dictionary<Guid, CentralUser>();
        if (texto.Contains('@'))
        {
            // Por el resolvedor: un correo sin verificar no identifica a nadie. Esa cuenta se llega por su caso
            // o por su documento.
            if ((await new Auth.ResolutorDeIdentificador(centralIdentity).ResolverAsync(texto, ct)).Cuenta is { } porCorreo)
                halladas[porCorreo.Id] = porCorreo;
        }
        else
        {
            // El usuario tal cual, y el número con cada prefijo: «987654» encuentra la CC y la CE de ese número.
            var normal = UsuarioDeDocumento.NormalizarIdentificador(texto);
            var numero = UsuarioDeDocumento.NormalizarNumero(texto);
            var candidatos = new HashSet<string>(StringComparer.Ordinal) { normal };
            foreach (var tipo in TiposDeDocumento.DeAcceso) candidatos.Add(UsuarioDeDocumento.Derivar(tipo, numero));
            foreach (var usuario in candidatos.Where(c => c.Length > 0))
                if (await centralIdentity.FindByDocumentUsernameAsync(usuario, ct) is { } porUsuario)
                    halladas[porUsuario.Id] = porUsuario;
        }

        var lista = new List<IdentidadResumenDto>();
        foreach (var c in halladas.Values) lista.Add(await ResumenAsync(c, ct));
        return Result.Success<IReadOnlyList<IdentidadResumenDto>>(lista);
    }

    public async Task<Result<IdentidadDetalleDto>> Handle(GetIdentidadQuery request, CancellationToken ct)
    {
        if (SoloMaestro.Verificar(currentUser) is { } e) return Result.Failure<IdentidadDetalleDto>(e);
        var cuenta = await centralIdentity.FindByIdAsync(request.Id, ct);
        if (cuenta is null) return Result.Failure<IdentidadDetalleDto>("Identity.NotFound", "Identidad no encontrada.");

        var historial = await db.DocumentHistory.AsNoTracking()
            .Where(h => h.CentralUserId == cuenta.Id)
            .OrderByDescending(h => h.OccurredAt)
            .Select(h => new CambioDeDocumentoDto(h.OccurredAt, h.Reason.ToString(), h.ActorKind.ToString(), h.OldUsername, h.NewUsername, h.Notes))
            .ToListAsync(ct);
        var casos = await db.IdentityCases.AsNoTracking()
            .Where(c => c.CentralUserId == cuenta.Id || c.OtherCentralUserId == cuenta.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return Result.Success(new IdentidadDetalleDto(await ResumenAsync(cuenta, ct), historial, await CasosAsync(casos, ct)));
    }

    public async Task<Result<IReadOnlyList<CasoDeIdentidadDto>>> Handle(ListarCasosDeIdentidadQuery request, CancellationToken ct)
    {
        if (SoloMaestro.Verificar(currentUser) is { } e) return Result.Failure<IReadOnlyList<CasoDeIdentidadDto>>(e);
        var q = db.IdentityCases.AsNoTracking();
        q = Enum.TryParse<IdentityCaseStatus>(request.Status, ignoreCase: true, out var estado)
            ? q.Where(c => c.Status == estado)
            : q.Where(c => c.Status == IdentityCaseStatus.Open || c.Status == IdentityCaseStatus.InProgress);
        var casos = await q.OrderByDescending(c => c.CreatedAt).Take(500).ToListAsync(ct);
        return Result.Success(await CasosAsync(casos, ct));
    }

    public async Task<Result<CasoDeIdentidadDto>> Handle(DescartarCasoDeIdentidadCommand request, CancellationToken ct)
    {
        if (SoloMaestro.Verificar(currentUser) is { } e) return Result.Failure<CasoDeIdentidadDto>(e);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<CasoDeIdentidadDto>("Validation.Invalid", "El motivo es obligatorio.");
        var caso = await db.IdentityCases.FirstOrDefaultAsync(c => c.PublicId == request.CasePublicId, ct);
        if (caso is null) return Result.Failure<CasoDeIdentidadDto>("IdentityCase.NotFound", "Caso no encontrado.");
        if (caso.Status is IdentityCaseStatus.Done or IdentityCaseStatus.Dismissed)
            return Result.Failure<CasoDeIdentidadDto>("IdentityCase.Closed", "El caso ya está cerrado.");
        if (caso is { Kind: IdentityCaseKind.Merge, Status: IdentityCaseStatus.InProgress })
            return Result.Failure<CasoDeIdentidadDto>(
                "IdentityCase.MergeInProgress", "La fusión ya empezó: repítala para completarla, no se descarta.");

        caso.Cerrar(IdentityCaseStatus.Dismissed, currentUser.CentralUserId!.Value, request.Reason.Trim(), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success((await CasosAsync([caso], ct))[0]);
    }

    private async Task<IdentidadResumenDto> ResumenAsync(CentralUser c, CancellationToken ct)
    {
        var membresias = await (
                from m in db.TenantMemberships.AsNoTracking()
                join t in db.Tenants.AsNoTracking() on m.TenantId equals t.PublicId
                where m.CentralUserId == c.Id
                orderby t.Name
                select new MembresiaDto(m.TenantId, t.Name, m.Status.ToString(), m.IsTenantAdmin))
            .ToListAsync(ct);
        return new IdentidadResumenDto(c.Id, c.Email, c.DocumentType, c.DocumentNumber, c.DocumentUsername,
            c.NombreParaMostrar, c.Status.ToString(), c.TwoFactorEnabled, c.IsGlobalMasterAdmin, membresias);
    }

    private async Task<IReadOnlyList<CasoDeIdentidadDto>> CasosAsync(IReadOnlyList<IdentityCase> casos, CancellationToken ct)
    {
        var ids = casos.Select(c => c.CentralUserId)
            .Concat(casos.Where(c => c.OtherCentralUserId.HasValue).Select(c => c.OtherCentralUserId!.Value))
            .Distinct().ToList();
        var correos = ids.Count == 0 ? new Dictionary<Guid, string>() : await centralIdentity.GetEmailsByIdsAsync(ids, ct);
        return casos.Select(c => new CasoDeIdentidadDto(
                c.PublicId, c.Kind.ToString(), c.Status.ToString(),
                c.CentralUserId, correos.GetValueOrDefault(c.CentralUserId),
                c.OtherCentralUserId, c.OtherCentralUserId is { } o ? correos.GetValueOrDefault(o) : null,
                c.Detail, c.CreatedAt, c.Resolution))
            .ToList();
    }
}
