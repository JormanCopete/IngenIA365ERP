using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;

/// <summary>
/// Registra una vigencia de parámetro (feature 012, T21, T071; FR-012; contracts/api.md §7,
/// <c>POST /api/inventory/parameters/{module}/{key}/versions</c>). Es el único escritor de
/// <c>COR_ParameterVersions</c>: la nueva vigencia cierra la anterior del mismo (clave, ámbito) la víspera de
/// <see cref="ValidFrom"/>. El módulo y la clave vienen de la ruta; <see cref="Chain"/> reemplaza a
/// <see cref="ScopePublicId"/> para el modo de paso de una cadena (lo resuelven las reglas del módulo).
/// </summary>
public sealed record AddParameterVersionCommand(
    string Module,
    string Key,
    ParameterScopeKind ScopeKind,
    Guid? ScopePublicId,
    string? Chain,
    string Value,
    DateOnly ValidFrom,
    string Reason,
    string? LegalSource,
    bool ConfirmFiscalWithoutPosting = false)
    : IRequest<Result<AddParameterVersionResponse>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>Una entidad afectada por el alta (tipo de documento de una cadena). (nuevo)</summary>
public sealed record ReferenciaDeAmbitoDto(Guid PublicId, string Code, string Name);

/// <summary>
/// Respuesta del alta: las vigencias creadas (una, o una por tipo de la cadena), la víspera en que quedó cerrada la
/// anterior (nula si no había una abierta) y los tipos de documento afectados por una cadena. (nuevo)
/// </summary>
public sealed record AddParameterVersionResponse(
    IReadOnlyList<Guid> VersionPublicIds,
    DateOnly? PreviousClosedOn,
    IReadOnlyList<ReferenciaDeAmbitoDto>? AffectedDocumentTypes);

/// <summary>
/// La forma del alta (feature 012, T071): motivo obligatorio (heredado de <see cref="ValidadorConMotivo{T}"/>),
/// largos de data-model §4.1 y la coherencia del ámbito. Lo que depende de la definición de la clave (valor y
/// ámbito admitidos, fuente legal) lo responde el handler con su código propio.
/// </summary>
public sealed class AddParameterVersionCommandValidator : ValidadorConMotivo<AddParameterVersionCommand>
{
    public AddParameterVersionCommandValidator()
    {
        RuleFor(x => x.Module).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Key).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Value).NotNull().WithMessage("Indicá el valor.").MaximumLength(2000);
        RuleFor(x => x.LegalSource).MaximumLength(200);
        RuleFor(x => x.ScopeKind).IsInEnum();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");

        When(x => x.ScopeKind == ParameterScopeKind.None, () =>
        {
            RuleFor(x => x.ScopePublicId).Null().WithMessage("El valor general no lleva entidad de ámbito.");
            RuleFor(x => x.Chain).Empty().WithMessage("El valor general no lleva cadena.");
        });

        When(x => x.ScopeKind != ParameterScopeKind.None, () =>
        {
            RuleFor(x => x)
                .Must(x => (x.ScopePublicId is { } id && id != Guid.Empty) ^ !string.IsNullOrWhiteSpace(x.Chain))
                .WithName("scopePublicId")
                .WithMessage("Indicá la entidad del ámbito (scopePublicId) o la cadena (chain), no las dos.");
            RuleFor(x => x.Chain)
                .Empty()
                .When(x => x.ScopeKind != ParameterScopeKind.DocumentType)
                .WithMessage("Sólo el ámbito de tipo de documento admite una cadena.");
        });
    }
}
