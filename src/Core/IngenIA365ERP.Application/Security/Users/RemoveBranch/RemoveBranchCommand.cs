using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.RemoveBranch;

/// <summary>
/// Quita una oficina asignada a un usuario (baja lógica de <c>SEC_UserBranchAssignments</c>; volver a asignarla
/// revive la misma fila). Si era la de por defecto, el usuario queda sin oficina por defecto. Si el vínculo no
/// existe o ya estaba quitado, devuelve <c>Security.Users.NotAssignedBranch</c>. (nuevo)
/// </summary>
public sealed record RemoveBranchCommand(
    Guid UserPublicId,
    Guid BranchPublicId) : IRequest<Result>;
