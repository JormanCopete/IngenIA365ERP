using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Platform.Common;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Platform.Connectors;

// Feature 020 (US7; R15, R17; T131–T134; FR-035 a FR-041; contracts/api.md §1): los conectores HTTP/JSON que define el maestro, como datos.
// Cambiar la definición publica una versión nueva (hecho); la habilitación por cooperativa tiene motivo y se audita. La copia en cada
// cooperativa la deja al día la tarea de réplica (ReplicaDeConectores): una petición no escribe en otra cooperativa.

/// <summary>
/// Las opciones de los conectores (sección <c>Conectores</c>). <see cref="PermitirRedesPrivadas"/> existe sólo para el proveedor falso de las
/// pruebas de integración; el arranque falla si vale <c>true</c> en Production (R17).
/// </summary>
public sealed class OpcionesDeConectores
{
    public const string Seccion = "Conectores";

    public bool PermitirRedesPrivadas { get; set; }
}

public sealed record PlatformConnectorDto(Guid PublicId, string Code, string Name, string? Description, bool IsActive, int? CurrentVersion, int EnabledTenants);

public sealed record PlatformConnectorVersionDto(int Number, string BaseUrl, string Definition, string Sha256, DateTime PublishedAt, string PublishedBy, string? Note);

public sealed record TenantConnectorDto(Guid TenantPublicId, string TenantName, bool Enabled, DateTime? ChangedAt, string? ChangedBy, string? Reason);

public sealed record SimulationDto(string Method, string Url, IReadOnlyDictionary<string, string> Headers, string? Body, IReadOnlyList<string> Missing);

public sealed record ListPlatformConnectorsQuery : IRequest<Result<IReadOnlyList<PlatformConnectorDto>>>;

public sealed class ListPlatformConnectorsQueryHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario)
    : IRequestHandler<ListPlatformConnectorsQuery, Result<IReadOnlyList<PlatformConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<PlatformConnectorDto>>> Handle(ListPlatformConnectorsQuery request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<IReadOnlyList<PlatformConnectorDto>>(m.Error);
        var filas = await admin.PlatformConnectors.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new PlatformConnectorDto(c.PublicId, c.Code, c.Name, c.Description, c.IsActive, c.CurrentVersionNumber,
                admin.TenantConnectors.Count(t => t.ConnectorId == c.Id && t.IsEnabled)))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<PlatformConnectorDto>>(filas);
    }
}

/// <summary>Crea un conector (sin versión: la primera se publica aparte).</summary>
public sealed record CreatePlatformConnectorCommand(string Code, string Name, string? Description) : IRequest<Result<Guid>>;

public sealed class CreatePlatformConnectorCommandValidator : AbstractValidator<CreatePlatformConnectorCommand>
{
    public CreatePlatformConnectorCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoLargo).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public sealed class CreatePlatformConnectorCommandHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario, IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null, ILogger<CreatePlatformConnectorCommandHandler>? logger = null)
    : IRequestHandler<CreatePlatformConnectorCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePlatformConnectorCommand request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<Guid>(m.Error);
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await admin.PlatformConnectors.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo, ct) is { } existente)
            return Result.Failure<Guid>(CodigoDeCatalogo.Duplicado("un conector", codigo, existente.Name, existente.PublicId));
        var c = new PlatformConnector { Code = codigo, Name = request.Name.Trim(), Description = request.Description?.Trim(), IsActive = true, CreatedBy = m.Value.Nombre };
        admin.PlatformConnectors.Add(c);
        await admin.SaveChangesAsync(ct);
        await AuditoriaDePlataforma.RegistrarAsync(auditoria, m.Value, AuditoriaDePlataforma.ConectorCreado, nameof(PlatformConnector), c.PublicId, null,
            new { c.Code, c.Name }, reloj.UtcNow, logger, ct);
        return Result.Success(c.PublicId);
    }
}

/// <summary>Cambia nombre, descripción o estado. Inactivo, ninguna cooperativa lo usa.</summary>
public sealed record UpdatePlatformConnectorCommand(Guid PublicId, string Name, string? Description, bool IsActive) : IRequest<Result>;

public sealed class UpdatePlatformConnectorCommandHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario, IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null, ILogger<UpdatePlatformConnectorCommandHandler>? logger = null)
    : IRequestHandler<UpdatePlatformConnectorCommand, Result>
{
    public async Task<Result> Handle(UpdatePlatformConnectorCommand request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure(m.Error);
        var c = await admin.PlatformConnectors.FirstOrDefaultAsync(x => x.PublicId == request.PublicId, ct);
        if (c is null) return Result.Failure("Connector.NotFound", "No existe ese conector.");
        c.Name = request.Name.Trim();
        c.Description = request.Description?.Trim();
        c.IsActive = request.IsActive;
        await admin.SaveChangesAsync(ct);
        await AuditoriaDePlataforma.RegistrarAsync(auditoria, m.Value, AuditoriaDePlataforma.ConectorActualizado, nameof(PlatformConnector), c.PublicId, null,
            new { c.Name, c.IsActive }, reloj.UtcNow, logger, ct);
        return Result.Success();
    }
}

/// <summary>
/// Publica una versión del conector (T131; FR-035, FR-038, FR-039): valida la URL base (https y host público), la definición y sus variables,
/// la numera y la deja vigente. Una versión publicada no cambia: los envíos hechos siguen con la suya.
/// </summary>
public sealed record PublishConnectorVersionCommand(Guid ConnectorPublicId, string BaseUrl, string Definition, string? Note) : IRequest<Result<int>>;

public sealed class PublishConnectorVersionCommandHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario, IDateTimeService reloj,
    IOptions<OpcionesDeConectores> opciones, IAuditAppendOnlyWriter? auditoria = null, ILogger<PublishConnectorVersionCommandHandler>? logger = null)
    : IRequestHandler<PublishConnectorVersionCommand, Result<int>>
{
    public async Task<Result<int>> Handle(PublishConnectorVersionCommand request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<int>(m.Error);
        var c = await admin.PlatformConnectors.FirstOrDefaultAsync(x => x.PublicId == request.ConnectorPublicId, ct);
        if (c is null) return Result.Failure<int>("Connector.NotFound", "No existe ese conector.");

        if (DireccionSegura.ValidarUrlBase(request.BaseUrl, opciones.Value.PermitirRedesPrivadas) is { } url)
            return Result.Failure<int>(url.Codigo, url.Mensaje);
        var definicion = DefinicionDeConector.Leer(request.Definition, out var lectura);
        if (definicion is null) return Result.Failure<int>(lectura!.Codigo, lectura.Mensaje);
        var problemas = definicion.Validar();
        if (problemas.Count > 0)
            return Result.Failure<int>(new ErrorConDatos(problemas[0].Codigo, problemas[0].Mensaje, new { problems = problemas.Select(p => new { code = p.Codigo, message = p.Mensaje }) }));

        var canonica = definicion.Escribir();
        var numero = (await admin.PlatformConnectorVersions.Where(v => v.ConnectorId == c.Id).MaxAsync(v => (int?)v.Number, ct) ?? 0) + 1;
        var ahora = reloj.UtcNow;
        admin.PlatformConnectorVersions.Add(new PlatformConnectorVersion
        {
            ConnectorId = c.Id, Number = numero, BaseUrl = request.BaseUrl.Trim().TrimEnd('/'), DefinitionJson = canonica,
            DefinitionSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonica))), PublishedAt = ahora, PublishedBy = m.Value.Nombre,
            Note = request.Note?.Trim(), CreatedAt = ahora, CreatedBy = m.Value.Nombre,
        });
        c.CurrentVersionNumber = numero;
        await admin.SaveChangesAsync(ct);
        await AuditoriaDePlataforma.RegistrarAsync(auditoria, m.Value, AuditoriaDePlataforma.VersionPublicada, nameof(PlatformConnectorVersion), c.PublicId, null,
            new { connector = c.Code, version = numero, baseUrl = request.BaseUrl, note = request.Note }, ahora, logger, ct);
        return Result.Success(numero);
    }
}

public sealed record ListConnectorVersionsQuery(Guid ConnectorPublicId) : IRequest<Result<IReadOnlyList<PlatformConnectorVersionDto>>>;

public sealed class ListConnectorVersionsQueryHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario)
    : IRequestHandler<ListConnectorVersionsQuery, Result<IReadOnlyList<PlatformConnectorVersionDto>>>
{
    public async Task<Result<IReadOnlyList<PlatformConnectorVersionDto>>> Handle(ListConnectorVersionsQuery request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<IReadOnlyList<PlatformConnectorVersionDto>>(m.Error);
        var filas = await admin.PlatformConnectorVersions.AsNoTracking().Where(v => v.Connector!.PublicId == request.ConnectorPublicId)
            .OrderByDescending(v => v.Number)
            .Select(v => new PlatformConnectorVersionDto(v.Number, v.BaseUrl, v.DefinitionJson, v.DefinitionSha256, v.PublishedAt, v.PublishedBy, v.Note))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<PlatformConnectorVersionDto>>(filas);
    }
}

/// <summary>
/// La simulación de una versión (T134; FR-040): renderiza con variables de ejemplo el mismo cuerpo que el envío y devuelve método, URL y
/// cuerpo, sin credenciales y sin abrir ninguna conexión.
/// </summary>
public sealed record SimulateConnectorQuery(Guid ConnectorPublicId, int Version, IReadOnlyDictionary<string, string> Variables) : IRequest<Result<SimulationDto>>;

public sealed class SimulateConnectorQueryHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario) : IRequestHandler<SimulateConnectorQuery, Result<SimulationDto>>
{
    public async Task<Result<SimulationDto>> Handle(SimulateConnectorQuery request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<SimulationDto>(m.Error);
        var v = await admin.PlatformConnectorVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Connector!.PublicId == request.ConnectorPublicId && x.Number == request.Version, ct);
        if (v is null) return Result.Failure<SimulationDto>("Connector.VersionNotFound", "No existe esa versión del conector.");
        var d = DefinicionDeConector.Leer(v.DefinitionJson, out _)!;

        var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var variable in VariablesDeConector.Todas)
            valores[variable.Nombre] = request.Variables.TryGetValue(variable.Nombre, out var dato) ? dato : Ejemplo(variable);
        var (ruta, faltaRuta) = RenderizadorDePlantilla.Renderizar(d.Operation.Path, valores);
        var (cuerpo, faltaCuerpo) = RenderizadorDePlantilla.Renderizar(d.Operation.Body, valores);
        var cabeceras = new Dictionary<string, string> { ["Content-Type"] = "application/json" };
        if (d.Auth.Kind != Domain.Enums.Connectors.ConnectorAuthKind.None) cabeceras[d.Auth.Header ?? "Authorization"] = $"{d.Auth.Prefix}*** (credencial)";
        return Result.Success(new SimulationDto(d.Operation.Method.ToUpperInvariant(), v.BaseUrl + (ruta ?? d.Operation.Path), cabeceras, cuerpo,
            faltaRuta.Concat(faltaCuerpo).Distinct().ToList()));
    }

    private static object Ejemplo(VariableDeConector v) => v.Tipo switch
    {
        TipoDeVariable.Entero => 1,
        TipoDeVariable.Decimal => 50000m,
        TipoDeVariable.Fecha => new DateOnly(2026, 12, 5),
        _ => v.Nombre switch
        {
            "vehiculo.placa" => "ABC123",
            "vehiculo.numeroInterno" => "0001",
            "vehiculo.radioMovil" => "101",
            "envio.id" => "00000000-0000-0000-0000-000000000001",
            "periodo.texto" => "diciembre 2026",
            _ => $"ejemplo-{v.Nombre}",
        },
    };
}

public sealed record ListConnectorTenantsQuery(Guid ConnectorPublicId) : IRequest<Result<IReadOnlyList<TenantConnectorDto>>>;

public sealed class ListConnectorTenantsQueryHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario)
    : IRequestHandler<ListConnectorTenantsQuery, Result<IReadOnlyList<TenantConnectorDto>>>
{
    public async Task<Result<IReadOnlyList<TenantConnectorDto>>> Handle(ListConnectorTenantsQuery request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure<IReadOnlyList<TenantConnectorDto>>(m.Error);
        var c = await admin.PlatformConnectors.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ConnectorPublicId, ct);
        if (c is null) return Result.Failure<IReadOnlyList<TenantConnectorDto>>("Connector.NotFound", "No existe ese conector.");
        var habilitaciones = await admin.TenantConnectors.AsNoTracking().Where(t => t.ConnectorId == c.Id).ToListAsync(ct);
        var cooperativas = await admin.Tenants.AsNoTracking().OrderBy(t => t.Name).Select(t => new { t.PublicId, t.Name }).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TenantConnectorDto>>(cooperativas.Select(t =>
        {
            var h = habilitaciones.FirstOrDefault(x => x.TenantId == t.PublicId);
            return new TenantConnectorDto(t.PublicId, t.Name, h?.IsEnabled ?? false, h?.ChangedAt, h?.ChangedBy, h?.Reason);
        }).ToList());
    }
}

/// <summary>Habilita o deshabilita un conector para una cooperativa, con motivo (T131; FR-041). La réplica la pone al día la tarea.</summary>
public sealed record SetTenantConnectorCommand(Guid ConnectorPublicId, Guid TenantPublicId, bool Enabled, string Reason) : IRequest<Result>;

public sealed class SetTenantConnectorCommandValidator : AbstractValidator<SetTenantConnectorCommand>
{
    public SetTenantConnectorCommandValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo.").MaximumLength(500);
}

public sealed class SetTenantConnectorCommandHandler(IAdminDbContext admin, ICurrentCentralUserContext usuario, IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null, ILogger<SetTenantConnectorCommandHandler>? logger = null)
    : IRequestHandler<SetTenantConnectorCommand, Result>
{
    public async Task<Result> Handle(SetTenantConnectorCommand request, CancellationToken ct)
    {
        var m = MaestroDePlataforma.De(usuario);
        if (m.IsFailure) return Result.Failure(m.Error);
        var c = await admin.PlatformConnectors.FirstOrDefaultAsync(x => x.PublicId == request.ConnectorPublicId, ct);
        if (c is null) return Result.Failure("Connector.NotFound", "No existe ese conector.");
        if (request.Enabled && c.CurrentVersionNumber is null) return Result.Failure("Connector.NoVersion", "Publique una versión del conector antes de habilitarlo.");
        if (!await admin.Tenants.AsNoTracking().AnyAsync(t => t.PublicId == request.TenantPublicId, ct))
            return Result.Failure("Tenant.NotFound", "No existe esa cooperativa.");

        var ahora = reloj.UtcNow;
        var h = await admin.TenantConnectors.FirstOrDefaultAsync(x => x.ConnectorId == c.Id && x.TenantId == request.TenantPublicId, ct);
        if (h is null)
        {
            h = new TenantConnector { ConnectorId = c.Id, TenantId = request.TenantPublicId, CreatedBy = m.Value.Nombre };
            admin.TenantConnectors.Add(h);
        }
        h.IsEnabled = request.Enabled;
        h.ChangedAt = ahora;
        h.ChangedBy = m.Value.Nombre;
        h.Reason = request.Reason.Trim();
        await admin.SaveChangesAsync(ct);
        await AuditoriaDePlataforma.RegistrarAsync(auditoria, m.Value,
            request.Enabled ? AuditoriaDePlataforma.ConectorHabilitado : AuditoriaDePlataforma.ConectorDeshabilitado,
            nameof(TenantConnector), c.PublicId, request.TenantPublicId, new { connector = c.Code, enabled = request.Enabled, reason = h.Reason }, ahora, logger, ct);
        return Result.Success();
    }
}
