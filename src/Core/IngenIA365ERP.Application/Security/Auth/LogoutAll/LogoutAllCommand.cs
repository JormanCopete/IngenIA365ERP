using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.LogoutAll;

/// <summary>
/// Revoca todas las sesiones del usuario autenticado (todas las familias
/// de refresh tokens). Útil cuando se sospecha compromiso de credenciales.
/// </summary>
public sealed record LogoutAllCommand() : IRequest<Result>;
