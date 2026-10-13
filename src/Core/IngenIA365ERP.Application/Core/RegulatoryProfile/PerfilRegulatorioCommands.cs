using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Core.RegulatoryProfile;

/// <summary>Los errores del perfil regulatorio (feature 013, T50; contracts/api.md §21.4). (nuevo)</summary>
public static class ErroresDelPerfilRegulatorio
{
    public const string CodigoSeCruza = "Core.RegulatoryProfile.Overlaps";
    public const string CodigoTipoDesconocido = "Core.RegulatoryProfile.OrganizationTypeUnknown";
    public const string CodigoNoEncontrado = "Core.RegulatoryProfile.NotFound";

    public static Error SeCruza(DateOnly desde) => new ErrorConDatos(CodigoSeCruza,
        $"Ya hay una versión del perfil que rige desde el {desde:yyyy-MM-dd}. Si está mal, regístrela como corrección de esa versión.",
        new { existingValidFrom = desde });

    public static Error YaCorregida(Guid version) => new ErrorConDatos(CodigoSeCruza,
        "Esa versión del perfil ya fue corregida: corrija la corrección, que es la que rige.",
        new { supersededPublicId = version });

    public static Error TipoDesconocido(string codigo, DateOnly desde) => new ErrorConDatos(CodigoTipoDesconocido,
        $"«{codigo}» no es un tipo de organización del catálogo de la Superintendencia vigente el {desde:yyyy-MM-dd}. Elija uno de la lista.",
        new { organizationType = codigo, validFrom = desde });

    public static Error CategoriaInvalida(string tipo) => new(Error.Validation.Code,
        $"La categoría no corresponde al tipo «{tipo}»: un fondo de empleados declara una categoría del catálogo y los demás tipos ninguna.");

    public static readonly Error NoEncontrado = new(CodigoNoEncontrado, "La versión del perfil regulatorio no existe.");
}

/// <summary>Las alertas del perfil regulatorio (data-model parte 3 §11). (nuevo)</summary>
public static class AlertasDelPerfilRegulatorio
{
    /// <summary><b>(fijado en data-model §11)</b> El perfil y <c>AccountingSetup.NiifGroup</c> difieren (pregunta E3).</summary>
    public const string GrupoNiifDistinto = "Contabilidad.GrupoNiifDistinto";
}

/// <summary>
/// La evaluación de la obligación sobrevenida (D28) que acompaña al perfil. La conecta la fase de US9
/// (<c>EfectoDeReplicacionObligacionSobrevenida</c>); hasta entonces <see cref="Obligated"/> es nulo y la base lo dice.
/// </summary>
public sealed record ExpectedLossEvaluationDto(bool? Obligated, IReadOnlyList<string> Segments, DateOnly? From, string Basis)
{
    public const string SinEvaluar = "Sin evaluar: la obligación de aplicar el modelo de pérdida esperada (D28) la evalúa la entrega de riesgo (US9).";

    public static ExpectedLossEvaluationDto NoEvaluada { get; } = new(null, [], null, SinEvaluar);
}

/// <summary>
/// El gancho con que el alta del perfil evalúa la obligación de pérdida esperada (feature 013, US9, T0710; T50, D28). Lo implementa Cartera
/// (<c>EvaluadorDeObligacionDePerdidaEsperada</c>); Core no conoce Cartera: sin implementación registrada, la evaluación queda «sin evaluar».
/// </summary>
public interface IEvaluadorDeObligacionDePerdidaEsperada
{
    /// <summary>Evalúa con la versión ya guardada y, si corresponde, levanta las alertas de la obligación sobrevenida.</summary>
    Task<ExpectedLossEvaluationDto> EvaluarAsync(RegulatoryProfileVersion version, CancellationToken ct);
}

/// <summary>Lo que responde el alta: la versión, si el grupo NIIF difiere del de la contabilidad y la evaluación de obligación.</summary>
public sealed record RegulatoryProfileVersionCreatedDto(
    Guid VersionPublicId,
    bool NiifGroupMismatch,
    byte? AccountingNiifGroup,
    ExpectedLossEvaluationDto ExpectedLoss);

/// <summary>
/// Registra una versión del perfil regulatorio (feature 013, FR-018, FR-108; T50; contracts/api.md §21.4; data-model parte 3 §4.5). Es el
/// único escritor de <c>COR_RegulatoryProfileVersions</c>. Reglas:
/// <list type="bullet">
/// <item>el tipo de organización existe en el documento <c>TiposDeOrganizacion</c> vigente en <see cref="ValidFrom"/>
/// (<c>Core.RegulatoryProfile.OrganizationTypeUnknown</c>) y la categoría corresponde al tipo;</item>
/// <item>inscrita como operador de libranza exige el número de la inscripción;</item>
/// <item>corregir es una fila nueva con <see cref="SupersedesPublicId"/> (una corrección por versión); sin corrección, dos versiones no
/// empiezan el mismo día (<c>Core.RegulatoryProfile.Overlaps</c>);</item>
/// <item>sin grupo NIIF se propone el de <c>AccountingSetup.NiifGroup</c>; si difieren, alerta <c>Contabilidad.GrupoNiifDistinto</c>
/// (pregunta E3).</item>
/// </list>
/// La evaluación de la obligación sobrevenida (D28) la conecta US9 por <see cref="IEvaluadorDeObligacionDePerdidaEsperada"/>; sin ella responde
/// «sin evaluar».
/// </summary>
public sealed record AddRegulatoryProfileVersionCommand(
    string OrganizationTypeCode,
    bool FinancialActivityAuthorized,
    byte SupervisionLevel,
    string? EmployeeFundCategory,
    byte? NiifGroup,
    bool LibranzaOperatorRegistered,
    string? LibranzaOperatorRegistrationNumber,
    DateOnly? LibranzaOperatorRegistrationDate,
    DateOnly ValidFrom,
    string Source,
    Guid? SupportAttachmentPublicId,
    string Reason,
    Guid? SupersedesPublicId = null)
    : IRequest<Result<RegulatoryProfileVersionCreatedDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AddRegulatoryProfileVersionCommandValidator : ValidadorConMotivo<AddRegulatoryProfileVersionCommand>
{
    public AddRegulatoryProfileVersionCommandValidator()
    {
        RuleFor(x => x.OrganizationTypeCode).NotEmpty().WithMessage("Indique el tipo de organización.").MaximumLength(40);
        RuleFor(x => x.SupervisionLevel).InclusiveBetween((byte)1, (byte)3).WithMessage("El nivel de supervisión es 1, 2 o 3.");
        RuleFor(x => x.NiifGroup).InclusiveBetween((byte)1, (byte)3).When(x => x.NiifGroup is not null)
            .WithMessage("El grupo NIIF es 1, 2 o 3.");
        RuleFor(x => x.EmployeeFundCategory).MaximumLength(20);
        RuleFor(x => x.LibranzaOperatorRegistrationNumber)
            .NotEmpty().When(x => x.LibranzaOperatorRegistered)
            .WithMessage("Indique el número de la inscripción como operador de libranza.")
            .MaximumLength(40);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.Source).NotEmpty().WithMessage("Indique el soporte: resolución, certificado de existencia o acta.").MaximumLength(300);
    }
}

public sealed class AddRegulatoryProfileVersionCommandHandler(
    IApplicationDbContext db,
    INormativaVigente normativa,
    IActorActual actor,
    IDateTimeService reloj,
    IAlertas alertas,
    ILogger<AddRegulatoryProfileVersionCommandHandler>? logger = null,
    IEvaluadorDeObligacionDePerdidaEsperada? obligacion = null)
    : IRequestHandler<AddRegulatoryProfileVersionCommand, Result<RegulatoryProfileVersionCreatedDto>>
{
    private readonly ILogger _log = logger ?? NullLogger<AddRegulatoryProfileVersionCommandHandler>.Instance;

    public async Task<Result<RegulatoryProfileVersionCreatedDto>> Handle(AddRegulatoryProfileVersionCommand request, CancellationToken ct)
    {
        var codigo = request.OrganizationTypeCode.Trim().ToUpperInvariant();
        var categoria = string.IsNullOrWhiteSpace(request.EmployeeFundCategory) ? null : request.EmployeeFundCategory.Trim().ToUpperInvariant();

        // 1. El tipo de organización, del documento vigente en ValidFrom (no es enum: la taxonomía del supervisor cambia).
        var tipos = await normativa.TiposDeOrganizacionAsync(request.ValidFrom, null, ct);
        if (tipos.IsFailure) return Result.Failure<RegulatoryProfileVersionCreatedDto>(tipos.Error);
        if (tipos.Value.Documento.Buscar(codigo) is null)
            return Result.Failure<RegulatoryProfileVersionCreatedDto>(ErroresDelPerfilRegulatorio.TipoDesconocido(codigo, request.ValidFrom));
        if (!tipos.Value.Documento.CategoriaValida(codigo, categoria))
            return Result.Failure<RegulatoryProfileVersionCreatedDto>(ErroresDelPerfilRegulatorio.CategoriaInvalida(codigo));

        // 2. Corrección o versión nueva.
        var existentes = await db.RegulatoryProfileVersions.AsNoTracking().Where(v => !v.IsDeleted).ToListAsync(ct);
        var corregidas = existentes.Where(v => v.SupersedesPublicId is not null).Select(v => v.SupersedesPublicId!.Value).ToHashSet();
        if (request.SupersedesPublicId is { } corrige)
        {
            if (existentes.All(v => v.PublicId != corrige))
                return Result.Failure<RegulatoryProfileVersionCreatedDto>(ErroresDelPerfilRegulatorio.NoEncontrado);
            if (corregidas.Contains(corrige))
                return Result.Failure<RegulatoryProfileVersionCreatedDto>(ErroresDelPerfilRegulatorio.YaCorregida(corrige));
        }
        else if (existentes.FirstOrDefault(v => v.ValidFrom == request.ValidFrom && !corregidas.Contains(v.PublicId)) is { } mismoDia)
        {
            return Result.Failure<RegulatoryProfileVersionCreatedDto>(ErroresDelPerfilRegulatorio.SeCruza(mismoDia.ValidFrom));
        }

        var quien = await actor.ObtenerAsync(ct);
        if (quien.UserId is not { } usuario)
            return Result.Failure<RegulatoryProfileVersionCreatedDto>("Identity.Unauthenticated",
                "No se sabe quién registra: el perfil regulatorio lo firma una persona de la cooperativa.");

        // 3. El grupo NIIF: se propone el de la contabilidad y se avisa si difiere (pregunta E3).
        var grupoContable = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted)
            .Select(s => (byte?)s.NiifGroup).FirstOrDefaultAsync(ct);
        var grupo = request.NiifGroup ?? grupoContable;
        var distinto = grupo is not null && grupoContable is not null && grupo != grupoContable;

        var ahora = reloj.UtcNow;
        var nombre = quien.Name.Length > 150 ? quien.Name[..150] : quien.Name;
        var version = new RegulatoryProfileVersion
        {
            OrganizationTypeCode = codigo,
            FinancialActivityAuthorized = request.FinancialActivityAuthorized,
            SupervisionLevel = request.SupervisionLevel,
            EmployeeFundCategory = categoria,
            NiifGroup = grupo,
            LibranzaOperatorRegistered = request.LibranzaOperatorRegistered,
            LibranzaOperatorRegistrationNumber = request.LibranzaOperatorRegistered ? request.LibranzaOperatorRegistrationNumber?.Trim() : null,
            LibranzaOperatorRegistrationDate = request.LibranzaOperatorRegistered ? request.LibranzaOperatorRegistrationDate : null,
            ValidFrom = request.ValidFrom,
            Source = request.Source.Trim(),
            SupportAttachmentPublicId = request.SupportAttachmentPublicId,
            Reason = request.Reason.Trim(),
            SupersedesPublicId = request.SupersedesPublicId,
            RegisteredByUserId = usuario,
            RegisteredByName = nombre,
            CreatedAt = ahora,
            CreatedBy = nombre,
        };
        db.RegulatoryProfileVersions.Add(version);
        await db.SaveChangesAsync(ct);

        if (distinto)
        {
            var alerta = await alertas.LevantarAsync(new AlertaALevantar(
                AlertasDelPerfilRegulatorio.GrupoNiifDistinto,
                "El grupo NIIF del perfil regulatorio no coincide con el de la contabilidad",
                $"El perfil regulatorio desde el {request.ValidFrom:yyyy-MM-dd} declara el grupo NIIF {grupo}, pero la configuración contable "
                + $"usa el grupo {grupoContable}. Revise cuál es el correcto con el contador.",
                EntityType: nameof(RegulatoryProfileVersion), EntityPublicId: version.PublicId,
                DedupKey: AlertasDelPerfilRegulatorio.GrupoNiifDistinto), ct);
            if (alerta.IsFailure)
                _log.LogWarning("[PerfilRegulatorio.AlertaNoLevantada] {Tipo}: {Codigo} {Mensaje}",
                    AlertasDelPerfilRegulatorio.GrupoNiifDistinto, alerta.Error.Code, alerta.Error.Message);
        }

        // US9 (T0710, T50): la obligación de pérdida esperada con la regla vigente y, si Cartera está activada, sus alertas.
        var evaluacion = obligacion is null ? ExpectedLossEvaluationDto.NoEvaluada : await obligacion.EvaluarAsync(version, ct);
        return Result.Success(new RegulatoryProfileVersionCreatedDto(version.PublicId, distinto, grupoContable, evaluacion));
    }
}

/// <summary>El perfil regulatorio a una fecha: el vigente, todas las versiones y lo que se sabe de la obligación de pérdida esperada.</summary>
public sealed record RegulatoryProfileDto(
    PerfilRegulatorioDto? Current,
    IReadOnlyList<PerfilRegulatorioDto> Versions,
    ExpectedLossEvaluationDto ExpectedLoss,
    bool? NiifGroupMismatch,
    byte? AccountingNiifGroup);

/// <summary>
/// El perfil regulatorio a <see cref="Date"/> (por defecto, hoy en la fecha local) con la regla bitemporal y todas sus versiones, la
/// más reciente primero (feature 013, T50; contracts/api.md §21.4, <c>GET /api/core/regulatory-profile?date=</c>). Con
/// <see cref="RecordedAsOf"/> (R) responde lo que se sabía entonces. (nuevo)
/// </summary>
public sealed record GetRegulatoryProfileQuery(DateOnly? Date = null, DateTime? RecordedAsOf = null) : IRequest<Result<RegulatoryProfileDto>>;

public sealed class GetRegulatoryProfileQueryHandler(IApplicationDbContext db, IPerfilRegulatorio perfil, IDateTimeService reloj)
    : IRequestHandler<GetRegulatoryProfileQuery, Result<RegulatoryProfileDto>>
{
    public async Task<Result<RegulatoryProfileDto>> Handle(GetRegulatoryProfileQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var vigente = await perfil.VigenteAsync(fecha, request.RecordedAsOf, ct);

        var versiones = (await db.RegulatoryProfileVersions.AsNoTracking().Where(v => !v.IsDeleted).ToListAsync(ct))
            .Where(v => request.RecordedAsOf is null || v.CreatedAt <= request.RecordedAsOf)
            .OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.Id)
            .Select(PerfilRegulatorioDto.Desde)
            .ToList();

        var grupoContable = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted)
            .Select(s => (byte?)s.NiifGroup).FirstOrDefaultAsync(ct);
        bool? distinto = vigente?.NiifGroup is { } g && grupoContable is { } c ? g != c : null;

        return Result.Success(new RegulatoryProfileDto(vigente, versiones, ExpectedLossEvaluationDto.NoEvaluada, distinto, grupoContable));
    }
}
