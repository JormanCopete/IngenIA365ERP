using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;

/// <summary>
/// Registra una versión de política de aprobación (feature 012, T33, T084; contracts/api.md §15.1,
/// <c>POST /api/inventory/approval-policies</c>). Cada alta es una versión nueva del mismo (sujeto, tipo) que cierra la
/// anterior la víspera de <see cref="ValidFrom"/>; <c>levels: []</c> significa «sin aprobación» desde esa fecha.
/// </summary>
public sealed record SaveApprovalPolicyCommand(
    string Subject,
    Guid? DocumentTypePublicId,
    DateOnly ValidFrom,
    string Reason,
    IReadOnlyList<NivelDto> Levels)
    : IRequest<Result<ApprovalPolicyDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// La forma del alta: motivo (hasta 300, el largo de la columna), sujeto de <see cref="ApprovalSubjects"/> (otro:
/// <c>Validation.Invalid</c>), fecha y niveles presentes. Lo que depende de la base (permisos, cruces) y la forma de los
/// niveles (<c>Approvals.Policy.LevelsInvalid</c>) los responde el handler con su código.
/// </summary>
public sealed class SaveApprovalPolicyCommandValidator : ValidadorConMotivo<SaveApprovalPolicyCommand>
{
    public SaveApprovalPolicyCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300).WithMessage("El motivo admite hasta 300 caracteres.");
        RuleFor(x => x.Subject)
            .Must(ApprovalSubjects.EsValido)
            .WithMessage($"El sujeto es uno de: {string.Join(", ", ApprovalSubjects.Todos)}.");
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");
        RuleFor(x => x.Levels).NotNull().WithMessage("Indicá los niveles (una lista vacía = sin aprobación).");
        RuleForEach(x => x.Levels).ChildRules(n =>
        {
            n.RuleFor(l => l.PermissionCode).NotEmpty().MaximumLength(100);
            n.RuleFor(l => l.Threshold).PrecisionScale(18, 2, true).WithMessage("El umbral admite hasta 2 decimales.");
        });
    }
}

/// <summary>
/// El único escritor de <c>COR_ApprovalPolicies</c> y sus niveles (T084). En orden: niveles bien formados
/// (<c>LevelsInvalid</c>), permisos del catálogo (<c>PermissionUnknown</c>), reglas del módulo (tipo existente, tipos que
/// siempre se aprueban, período cerrado), sin cruces (<c>Overlaps</c>) y cierre de la anterior la víspera. La
/// idempotencia, la transacción y la auditoría (con el motivo) las ponen los behaviors.
/// </summary>
public sealed class SaveApprovalPolicyCommandHandler(IApplicationDbContext db, IReglasDePoliticaDeAprobacion reglas)
    : IRequestHandler<SaveApprovalPolicyCommand, Result<ApprovalPolicyDto>>
{
    public async Task<Result<ApprovalPolicyDto>> Handle(SaveApprovalPolicyCommand request, CancellationToken ct)
    {
        var niveles = request.Levels.Select(l => new NivelDeAprobacion(l.Order, l.Threshold, l.PermissionCode.Trim())).ToList();
        if (EvaluadorDePolitica.ValidarNiveles(niveles) is { } motivo)
            return Result.Failure<ApprovalPolicyDto>(ErroresDeAprobaciones.NivelesInvalidos(motivo));

        foreach (var permiso in niveles.Select(n => n.PermissionCode).Distinct(StringComparer.Ordinal))
        {
            if (!await db.Permissions.AnyAsync(p => p.Resource + "." + p.Action == permiso, ct))
                return Result.Failure<ApprovalPolicyDto>(ErroresDeAprobaciones.PermisoDesconocido(permiso));
        }

        var evaluada = await reglas.EvaluarAsync(
            new AltaDePoliticaDeAprobacion(request.Subject, request.DocumentTypePublicId, request.ValidFrom, niveles.Count), ct);
        if (evaluada.IsFailure) return Result.Failure<ApprovalPolicyDto>(evaluada.Error);

        var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, request.Subject, request.DocumentTypePublicId);
        var versiones = await db.ApprovalPolicies.Where(p => p.PolicyKey == clave).ToListAsync(ct);

        var posterior = versiones.Where(v => v.ValidFrom >= request.ValidFrom).OrderBy(v => v.ValidFrom).FirstOrDefault();
        if (posterior is not null)
            return Result.Failure<ApprovalPolicyDto>(ErroresDeAprobaciones.PoliticaSeCruza(posterior.ValidFrom));

        var vispera = request.ValidFrom.AddDays(-1);
        var anterior = versiones.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
        if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo > vispera))
            anterior.ValidTo = vispera;

        var nueva = new ApprovalPolicy
        {
            Module = ApprovalPolicy.ModuloInventario,
            Subject = request.Subject,
            DocumentTypePublicId = request.DocumentTypePublicId,
            PolicyKey = clave,
            Version = versiones.Count == 0 ? 1 : versiones.Max(v => v.Version) + 1,
            ValidFrom = request.ValidFrom,
            Reason = request.Reason.Trim(),
            Levels = niveles.OrderBy(n => n.Order)
                .Select(n => new ApprovalPolicyLevel { Order = (byte)n.Order, Threshold = n.Threshold, PermissionCode = n.PermissionCode })
                .ToList(),
        };
        db.ApprovalPolicies.Add(nueva);
        await db.SaveChangesAsync(ct);

        var tipos = request.DocumentTypePublicId is { } tipo
            ? await reglas.DescribirTiposAsync([tipo], ct)
            : new Dictionary<Guid, TipoDeDocumentoDeAprobacionDto>();
        return Result.Success(PoliticasDeAprobacion.ADto(nueva, tipos));
    }
}

/// <summary>El DTO de una versión de política (lo comparten el alta y la consulta). (nuevo)</summary>
internal static class PoliticasDeAprobacion
{
    public static ApprovalPolicyDto ADto(ApprovalPolicy p, IReadOnlyDictionary<Guid, TipoDeDocumentoDeAprobacionDto> tipos) => new(
        p.PublicId,
        p.Module,
        p.Subject,
        p.DocumentTypePublicId is { } tipo ? tipos.GetValueOrDefault(tipo) ?? new TipoDeDocumentoDeAprobacionDto(tipo, string.Empty, string.Empty, null) : null,
        p.Version,
        p.ValidFrom,
        p.ValidTo,
        p.Reason,
        p.Levels.Where(l => !l.IsDeleted).OrderBy(l => l.Order).Select(l => new NivelDto(l.Order, l.Threshold, l.PermissionCode)).ToList(),
        p.CreatedBy,
        p.CreatedAt);
}
