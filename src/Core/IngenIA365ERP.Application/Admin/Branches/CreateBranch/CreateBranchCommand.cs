using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Admin.Branches.CreateBranch;

/// <summary>
/// Crea una oficina en la cooperativa activa. El <c>Code</c> es único por cooperativa. Si
/// <see cref="IsHeadquarters"/>=true, la cooperativa no debe tener ya otra oficina principal activa. Con
/// <see cref="CrearAgencia"/>, crea también su agencia vinculada, con el mismo código y nombre.
/// </summary>
public sealed record CreateBranchCommand(
    string Code,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    bool IsHeadquarters,
    bool CrearAgencia = false) : IRequest<Result<Guid>>;
