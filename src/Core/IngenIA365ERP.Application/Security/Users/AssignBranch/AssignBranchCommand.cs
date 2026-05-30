using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.AssignBranch;

/// <summary>
/// Asocia un usuario con una sucursal de la cooperativa. Si <c>IsDefault=true</c>
/// y ya hay otra sucursal por defecto, la rebaja a no-default.
/// </summary>
public sealed record AssignBranchCommand(
    Guid UserPublicId,
    Guid BranchPublicId,
    bool IsDefault) : IRequest<Result>;
