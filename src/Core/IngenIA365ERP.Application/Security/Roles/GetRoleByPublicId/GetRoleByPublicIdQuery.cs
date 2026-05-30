using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Roles.GetRoleByPublicId;

public sealed record GetRoleByPublicIdQuery(Guid RolePublicId) : IRequest<Result<RoleDto>>;
