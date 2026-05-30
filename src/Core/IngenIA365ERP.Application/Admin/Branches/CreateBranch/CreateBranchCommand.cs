using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

/// <summary>
/// Crea una sucursal dentro de la cooperativa indicada. El <c>Code</c> es
/// único por tenant. Si <see cref="IsHeadquarters"/>=true, el tenant no debe
/// tener ya otra sucursal matriz activa.
/// </summary>
public sealed record CreateBranchCommand(
    Guid TenantPublicId,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    bool IsHeadquarters) : IRequest<Result<Guid>>;
