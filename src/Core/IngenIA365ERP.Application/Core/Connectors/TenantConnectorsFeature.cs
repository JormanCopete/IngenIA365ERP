using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Core.Connectors;

// Feature 020, US8 (R16; FR-042, FR-043, FR-057; contracts/api.md §2): la cooperativa ve los conectores que el maestro le habilitó, pone sus
// credenciales —cifradas, nunca se vuelven a mostrar— y prueba la conexión. Ninguna respuesta lleva el contenido de una credencial.

/// <summary>Un campo de credencial que pide el conector (de su definición): nombre, etiqueta y si es secreto.</summary>
public sealed record CampoDeCredencialDto(string Name, string Label, bool Secret);

/// <summary>Un conector habilitado para la cooperativa, sin el contenido de sus credenciales.</summary>
public sealed record ConectorDeLaCooperativaDto(
    Guid PublicId,
    string Code,
    string Name,
    int? Version,
    string? AuthKind,
    IReadOnlyList<CampoDeCredencialDto> Fields,
    bool HasCredentials,
    string? SetBy,
    DateTime? SetAt,
    DateTime? LastVerifiedAt,
    bool? LastVerificationOk,
    string? LastVerificationMessage);

/// <summary>El resultado de «Probar conexión»: nunca el token.</summary>
public sealed record PruebaDeConectorDto(bool Ok, int StatusCode, string Message);

public sealed record ListTenantConnectorsQuery : IRequest<Result<IReadOnlyList<ConectorDeLaCooperativaDto>>>;

public sealed class ListTenantConnectorsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListTenantConnectorsQuery, Result<IReadOnlyList<ConectorDeLaCooperativaDto>>>
{
    public async Task<Result<IReadOnlyList<ConectorDeLaCooperativaDto>>> Handle(ListTenantConnectorsQuery request, CancellationToken ct)
    {
        var conectores = await db.Connectors.AsNoTracking().Where(c => c.IsEnabled).Include(c => c.Versions).OrderBy(c => c.Name).ToListAsync(ct);
        var ids = conectores.Select(c => c.Id).ToList();
        var credenciales = await db.ConnectorCredentials.AsNoTracking().Where(c => ids.Contains(c.ConnectorId))
            .Select(c => new { c.ConnectorId, c.SetBy, c.SetAt, c.LastVerifiedAt, c.LastVerificationOk, c.LastVerificationMessage })
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ConectorDeLaCooperativaDto>>(conectores.Select(c =>
        {
            var vigente = c.Versions.FirstOrDefault(v => v.Number == c.CurrentVersionNumber);
            var d = vigente is null ? null : DefinicionDeConector.Leer(vigente.DefinitionJson, out _);
            var cr = credenciales.FirstOrDefault(x => x.ConnectorId == c.Id);
            return new ConectorDeLaCooperativaDto(c.PublicId, c.Code, c.Name, c.CurrentVersionNumber, d?.Auth.Kind.ToString(),
                d?.CredentialFields.Select(f => new CampoDeCredencialDto(f.Name, f.Label, f.Secret)).ToList() ?? [],
                cr is not null, cr?.SetBy, cr?.SetAt, cr?.LastVerifiedAt, cr?.LastVerificationOk, cr?.LastVerificationMessage);
        }).ToList());
    }
}

/// <summary>
/// Pone las credenciales de un conector (T146). Los campos se validan contra los <c>credentialFields</c> de la versión vigente; uno vacío
/// conserva el valor anterior. Los valores <b>no viajan a la auditoría</b>: <see cref="Fields"/> lleva <c>[JsonIgnore]</c> y el evento sólo
/// dice qué conector y qué campos cambiaron.
/// </summary>
public sealed record SetConnectorCredentialsCommand(Guid ConnectorPublicId, [property: JsonIgnore] IReadOnlyDictionary<string, string> Fields) : IRequest<Result>
{
    /// <summary>Lo que ve la auditoría: los nombres de los campos que llegaron con valor, nunca los valores.</summary>
    public IReadOnlyList<string> CamposConValor => Fields.Where(f => !string.IsNullOrEmpty(f.Value)).Select(f => f.Key).OrderBy(f => f, StringComparer.Ordinal).ToList();
}

public sealed class SetConnectorCredentialsCommandValidator : AbstractValidator<SetConnectorCredentialsCommand>
{
    public SetConnectorCredentialsCommandValidator()
    {
        RuleFor(x => x.Fields).NotNull();
        RuleForEach(x => x.Fields).Must(f => f.Value is null || f.Value.Length <= 2000).WithMessage("Un campo de credencial tiene a lo sumo 2.000 caracteres.");
    }
}

public sealed class SetConnectorCredentialsCommandHandler(IApplicationDbContext db, AlmacenDeCredencialesDeConector almacen, ICurrentUserService usuario,
    IDateTimeService reloj) : IRequestHandler<SetConnectorCredentialsCommand, Result>
{
    public async Task<Result> Handle(SetConnectorCredentialsCommand request, CancellationToken ct)
    {
        var (conector, d, problema) = await ConectorHabilitado.LeerAsync(db, request.ConnectorPublicId, ct);
        if (problema is not null) return Result.Failure(problema);
        if (d!.Auth.Kind == ConnectorAuthKind.None || d.CredentialFields.Count == 0)
            return Result.Failure("Connector.NoCredentials", "Este conector no pide credenciales.");

        var declarados = d.CredentialFields.Select(f => f.Name).ToList();
        var ajenos = request.Fields.Keys.Where(k => !declarados.Contains(k, StringComparer.Ordinal)).ToList();
        if (ajenos.Count > 0)
            return Result.Failure("Connector.UnknownCredentialField", $"El conector no pide {string.Join(", ", ajenos)}: pide {string.Join(", ", declarados)}.");

        var ahora = reloj.UtcNow;
        var faltan = await almacen.PrepararAsync(conector!.Id, declarados, request.Fields, usuario.UserName ?? "sistema", ahora, ct);
        if (faltan.Count > 0)
        {
            var etiquetas = d.CredentialFields.Where(f => faltan.Contains(f.Name)).Select(f => f.Label);
            return Result.Failure("Connector.CredentialMissing", $"Falta: {string.Join(", ", etiquetas)}.");
        }
        // T164: los envíos que esperaban credenciales salen en la siguiente pasada.
        await EnviosEnEspera.DespertarAsync(db, conector.Id, ahora, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// «Probar conexión» (T146): se autentica contra el sistema externo con las credenciales guardadas —sin el token guardado, siempre de
/// nuevo— y guarda el resultado de la verificación. Responde código y mensaje, nunca el token ni el cuerpo del inicio de sesión.
/// </summary>
public sealed record TestConnectorCommand(Guid ConnectorPublicId) : IRequest<Result<PruebaDeConectorDto>>;

public sealed class TestConnectorCommandHandler(IApplicationDbContext db, SesionDeConector sesion, IDateTimeService reloj,
    ILogger<TestConnectorCommandHandler>? logger = null) : IRequestHandler<TestConnectorCommand, Result<PruebaDeConectorDto>>
{
    public async Task<Result<PruebaDeConectorDto>> Handle(TestConnectorCommand request, CancellationToken ct)
    {
        var (conector, d, problema) = await ConectorHabilitado.LeerAsync(db, request.ConnectorPublicId, ct);
        if (problema is not null) return Result.Failure<PruebaDeConectorDto>(problema);
        var version = conector!.Versions.First(v => v.Number == conector.CurrentVersionNumber);

        var r = await sesion.AutenticarAsync(conector.Id, version.Number, version.BaseUrl, d!, forzar: true, ct);
        var credencial = await db.ConnectorCredentials.FirstOrDefaultAsync(c => c.ConnectorId == conector.Id, ct);
        if (credencial is not null)
        {
            credencial.LastVerifiedAt = reloj.UtcNow;
            credencial.LastVerificationOk = r.Ok;
            credencial.LastVerificationMessage = r.Mensaje.Length > 500 ? r.Mensaje[..500] : r.Mensaje;
            await db.SaveChangesAsync(ct);
        }
        logger?.LogInformation("[Conectores.Prueba] {Conector}: {Ok} ({Codigo}).", conector.Code, r.Ok, r.Codigo);
        return Result.Success(new PruebaDeConectorDto(r.Ok, r.Codigo, r.Mensaje));
    }
}

/// <summary>El conector habilitado de la cooperativa con su definición vigente, o el error de por qué no se puede usar.</summary>
internal static class ConectorHabilitado
{
    public static async Task<(Domain.Entities.Core.Connectors.Connector? Conector, DefinicionDeConector? Definicion, Error? Problema)> LeerAsync(
        IApplicationDbContext db, Guid publicId, CancellationToken ct)
    {
        var c = await db.Connectors.Include(x => x.Versions).FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (c is null) return (null, null, new Error("Connector.NotFound", "No existe ese conector en la cooperativa."));
        if (!c.IsEnabled) return (c, null, new Error("Connector.NotEnabled", "El conector no está habilitado para la cooperativa."));
        var v = c.Versions.FirstOrDefault(x => x.Number == c.CurrentVersionNumber);
        var d = v is null ? null : DefinicionDeConector.Leer(v.DefinitionJson, out _);
        return d is null
            ? (c, null, new Error("Connector.NoVersion", "El conector no tiene una versión vigente."))
            : (c, d, null);
    }
}
