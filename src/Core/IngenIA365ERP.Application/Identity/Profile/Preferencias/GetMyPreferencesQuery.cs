using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

/// <summary>
/// Devuelve las preferencias del usuario autenticado. La cooperativa activa
/// sale del token, no del cliente: si viniera por parámetro, un usuario podría
/// leer los favoritos que tiene en otra cooperativa pasando otro identificador.
/// </summary>
public sealed record GetMyPreferencesQuery : IRequest<Result<PreferenciasUsuarioDto>>;
