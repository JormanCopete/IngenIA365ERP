using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.GetUserByPublicId;

/// <summary>
/// Detalle completo de un usuario por <c>PublicId</c>. Si está soft-deleted
/// devuelve <c>Generic.NotFound</c> (mismo trato que inexistente para no
/// filtrar información — coherente con FR-017).
/// </summary>
public sealed record GetUserByPublicIdQuery(Guid UserPublicId) : IRequest<Result<UserDetailDto>>;
