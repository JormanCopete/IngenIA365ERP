using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.SetDefaultBranch;

/// <summary>
/// Marca como por defecto una oficina ya asignada al usuario y rebaja la que lo era. Es la oficina del claim
/// <c>branch_id</c> y la que proponen Contabilidad y Cartera. (nuevo)
/// </summary>
public sealed record SetDefaultBranchCommand(
    Guid UserPublicId,
    Guid BranchPublicId) : IRequest<Result>;
