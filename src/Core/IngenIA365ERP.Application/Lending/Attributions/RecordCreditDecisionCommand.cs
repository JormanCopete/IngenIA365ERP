using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Attributions;

// Feature 013, US2 (bloque 6B; T0398; FR-038, FR-042, T63, T65; api.md §4.3): la decisión individual sobre cualquier sujeto de
// LendingDecisionSubjects. Las rutas de K1 son la solicitud, el cupo y la excepción de validación fallida (ésta llega con US3); las colegiadas
// nacen al cerrar el acta (IAsuntoDeActa). (nuevo)

/// <summary>Una garantía adicional que pide una aprobación con condiciones (api.md §4.3). (nuevo)</summary>
public sealed record AdditionalCollateralInput(CollateralKind Kind, Guid? PersonPublicId = null);

/// <summary>
/// Las condiciones menores de una aprobación (api.md §4.3, <c>conditions</c>): monto y cuotas que no pasan de lo pedido y garantías adicionales.
/// Se revalidan contra la usura y la concentración (FR-038). Es también la forma del JSON de condiciones de un punto de acta. (nuevo)
/// </summary>
public sealed record DecisionConditionsInput(decimal? Amount = null, int? Installments = null, IReadOnlyList<AdditionalCollateralInput>? AdditionalCollaterals = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El JSON canónico que se sella en la decisión y en la solicitud (nulo sin condiciones).</summary>
    public string? ComoJson() => Amount is null && Installments is null && AdditionalCollaterals is not { Count: > 0 } ? null : JsonSerializer.Serialize(this, Json);

    /// <summary>Las condiciones de un JSON de punto de acta (nulo o ilegible = sin condiciones).</summary>
    public static DecisionConditionsInput? DeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<DecisionConditionsInput>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Lo que se pide decidir, sin el sujeto (api.md §4.3, <c>DecisionInput</c>). (nuevo)</summary>
public sealed record DecisionPedida(
    Guid SubjectPublicId,
    CreditDecisionOutcome Outcome,
    string? Reason,
    DecisionConditionsInput? Conditions,
    string? ExpectedContentSha256);

/// <summary>La respuesta de una decisión (api.md §4.3): la decisión, el resultado, el estado del sujeto y la vigencia de la aprobación. (nuevo)</summary>
public sealed record CreditDecisionResultDto(
    Guid DecisionPublicId,
    CreditDecisionOutcome Outcome,
    string SubjectStatus,
    DateOnly? ApprovalExpiresAt,
    decimal? ApprovedAmount,
    Guid? ResultingSubjectPublicId = null);

/// <summary>
/// El gancho de cada sujeto con la decisión individual (feature 013, T0398): lo implementan la solicitud (<c>DecisionSobreSolicitud</c>) y el
/// cupo (<c>DecisionSobreCupo</c>); K3 suma la novedad y la condonación, US3 la excepción de validación fallida. (nuevo)
/// </summary>
public interface IDecisionIndividual
{
    string SubjectType { get; }

    Task<Result<CreditDecisionResultDto>> DecidirAsync(DecisionPedida pedido, CancellationToken ct);
}

/// <summary>
/// Decide un sujeto (api.md §4.3, <c>POST /applications/{id}/decisions</c>, <c>/credit-limits/{id}/decisions</c>, con clave): la ruta exige ver el
/// sujeto y el permiso del nivel efectivo lo valida el sujeto (sin él, el 404 del sujeto; con uno menor o colegiado,
/// <c>Lending.Attribution.LevelRequired</c>). <c>Deny</c>, <c>Return</c> y toda excepción llevan motivo; <c>ApproveWithConditions</c>, sus
/// condiciones; <see cref="ExpectedContentSha256"/> es la huella del enrutamiento que vio quien decide. Un sujeto sin decisión individual
/// todavía → <c>Lending.Session.SubjectNotSupported</c>. (nuevo)
/// </summary>
public sealed record RecordCreditDecisionCommand(
    string Subject,
    Guid SubjectPublicId,
    CreditDecisionOutcome Outcome,
    string? Reason = null,
    DecisionConditionsInput? Conditions = null,
    string? ExpectedContentSha256 = null) : IRequest<Result<CreditDecisionResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RecordCreditDecisionCommandValidator : AbstractValidator<RecordCreditDecisionCommand>
{
    public RecordCreditDecisionCommandValidator()
    {
        RuleFor(x => x.Subject).Must(LendingDecisionSubjects.Existe).WithMessage("Elija un asunto que se decida en Cartera.");
        RuleFor(x => x.SubjectPublicId).NotEmpty();
        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().When(x => x.Outcome is CreditDecisionOutcome.Deny or CreditDecisionOutcome.Return)
            .WithMessage("Negar o devolver exige el motivo de la decisión.").MaximumLength(500);
        RuleFor(x => x.Conditions).NotNull().When(x => x.Outcome == CreditDecisionOutcome.ApproveWithConditions)
            .WithMessage("Aprobar con condiciones exige las condiciones (monto, cuotas o garantía adicional).");
        RuleFor(x => x.Conditions!.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).When(x => x.Conditions?.Amount is not null)
            .WithMessage("El monto aprobado es mayor que cero y admite hasta 2 decimales.");
        RuleFor(x => x.Conditions!.Installments).GreaterThan(0).When(x => x.Conditions?.Installments is not null);
        RuleFor(x => x.ExpectedContentSha256).NotEmpty().Matches("^[0-9a-f]{64}$")
            .WithMessage("Mande la huella del enrutamiento que revisó (64 caracteres hexadecimales).");
    }
}

public sealed class RecordCreditDecisionCommandHandler(IEnumerable<IDecisionIndividual> sujetos)
    : IRequestHandler<RecordCreditDecisionCommand, Result<CreditDecisionResultDto>>
{
    public Task<Result<CreditDecisionResultDto>> Handle(RecordCreditDecisionCommand request, CancellationToken ct)
    {
        var sujeto = sujetos.FirstOrDefault(s => string.Equals(s.SubjectType, request.Subject, StringComparison.Ordinal));
        return sujeto is null
            ? Task.FromResult(Result.Failure<CreditDecisionResultDto>(ErroresDeGobierno.SujetoNoDisponible(request.Subject)))
            : sujeto.DecidirAsync(new DecisionPedida(request.SubjectPublicId, request.Outcome, request.Reason, request.Conditions,
                request.ExpectedContentSha256), ct);
    }
}

/// <summary>
/// La atribución de quien decide (feature 013, T0398; T63, C86; api.md §1.3, §4.3): decide el nivel efectivo quien tiene su permiso o el de un
/// nivel individual de mayor rango (los niveles no se acumulan: el asunto va solo a su nivel, y una atribución mayor lo cubre). Un nivel
/// colegiado no se decide individualmente (<c>Lending.Attribution.LevelRequired</c> con el órgano); con sólo un permiso de un nivel menor, el
/// mismo código con el nivel; sin ningún permiso de decisión, el 404 del sujeto. El maestro global no decide: sólo cuentan los permisos
/// concedidos en la cooperativa (C86, <c>CompanyAdmin</c> y <c>cartera.administrador</c> no tienen <c>Lending.Decisions.*</c>). (nuevo)
/// </summary>
public sealed class AtribucionDelDecisor(IApplicationDbContext db, ICurrentUserPermissions permisos)
{
    /// <summary>Nulo si puede decidir el nivel; si no, el error que corresponde.</summary>
    public async Task<Error?> ExigirAsync(AttributionLevel nivel, IReadOnlyList<AttributionLevel> niveles, Error noEncontrado, CancellationToken ct)
    {
        var concedidos = await ConcedidosAsync(ct);
        var suyos = niveles.Where(n => n.Kind == AttributionLevelKind.Individual && n.PermissionCode is { } p && concedidos.Contains(p)).ToList();
        if (nivel.Kind == AttributionLevelKind.Collegiate)
        {
            var organo = nivel.GoverningBodyId is { } id
                ? await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == id).Select(b => b.Name).FirstOrDefaultAsync(ct)
                : null;
            return ErroresDeDecisionesYCupos.NivelRequerido(nivel.Code, nivel.Name, organo ?? nivel.Name);
        }
        if (suyos.Any(n => n.Rank >= nivel.Rank)) return null;
        return suyos.Count > 0 ? ErroresDeDecisionesYCupos.NivelRequerido(nivel.Code, nivel.Name, null) : noEncontrado;
    }

    /// <summary>¿Puede decidir individualmente el nivel? (para «Mis pendientes»).</summary>
    public async Task<bool> PuedeDecidirAsync(AttributionLevel nivel, IReadOnlyList<AttributionLevel> niveles, CancellationToken ct)
    {
        if (nivel.Kind != AttributionLevelKind.Individual) return false;
        var concedidos = await ConcedidosAsync(ct);
        return niveles.Any(n => n.Kind == AttributionLevelKind.Individual && n.PermissionCode is { } p && concedidos.Contains(p) && n.Rank >= nivel.Rank);
    }

    /// <summary>Los permisos de decisión concedidos (sin el atajo del maestro global).</summary>
    public async Task<IReadOnlySet<string>> ConcedidosAsync(CancellationToken ct) =>
        new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
}
