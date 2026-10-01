using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.ListApprovalPolicies;

/// <summary>
/// Las políticas de aprobación (feature 012, T084; contracts/api.md §15.1,
/// <c>GET /api/inventory/approval-policies?subject=&amp;documentTypePublicId=&amp;asOf=&amp;includeHistory=</c>). Sin
/// historia, la versión vigente a <see cref="AsOf"/> (hoy local si no viene) de cada (sujeto, tipo); con historia,
/// todas las versiones. Catálogo chico: arreglo sin paginar, por sujeto, tipo y fecha. (nuevo)
/// </summary>
public sealed record ListApprovalPoliciesQuery(
    string? Subject = null,
    Guid? DocumentTypePublicId = null,
    DateOnly? AsOf = null,
    bool? IncludeHistory = null) : IRequest<Result<IReadOnlyList<ApprovalPolicyDto>>>;

public sealed class ListApprovalPoliciesQueryHandler(IApplicationDbContext db, IReglasDePoliticaDeAprobacion reglas, IDateTimeService reloj)
    : IRequestHandler<ListApprovalPoliciesQuery, Result<IReadOnlyList<ApprovalPolicyDto>>>
{
    public async Task<Result<IReadOnlyList<ApprovalPolicyDto>>> Handle(ListApprovalPoliciesQuery request, CancellationToken ct)
    {
        var consulta = db.ApprovalPolicies.AsNoTracking().Include(p => p.Levels)
            .Where(p => p.Module == ApprovalPolicy.ModuloInventario);
        if (!string.IsNullOrWhiteSpace(request.Subject)) consulta = consulta.Where(p => p.Subject == request.Subject);
        if (request.DocumentTypePublicId is { } tipo) consulta = consulta.Where(p => p.DocumentTypePublicId == tipo);

        var politicas = await consulta.ToListAsync(ct);
        if (request.IncludeHistory != true)
        {
            var fecha = request.AsOf ?? reloj.HoyLocal;
            politicas = politicas.Where(p => p.VigenteEn(fecha)).ToList();
        }

        var tipos = await reglas.DescribirTiposAsync(
            politicas.Where(p => p.DocumentTypePublicId is not null).Select(p => p.DocumentTypePublicId!.Value).Distinct().ToList(), ct);

        IReadOnlyList<ApprovalPolicyDto> dtos = politicas
            .OrderBy(p => p.Subject, StringComparer.Ordinal)
            .ThenBy(p => p.DocumentTypePublicId is null ? 0 : 1)
            .ThenBy(p => p.PolicyKey, StringComparer.Ordinal)
            .ThenBy(p => p.ValidFrom)
            .Select(p => PoliticasDeAprobacion.ADto(p, tipos))
            .ToList();
        return Result.Success(dtos);
    }
}
