using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Identity.Profile.Preferencias;

/// <summary>
/// Guarda preferencias del usuario autenticado.
///
/// <para>
/// El diccionario lleva SÓLO las claves que cambiaron; lo que no venga se deja
/// como está. Así la pantalla de ajustes y el botón de tema de la barra
/// superior pueden escribir por separado sin pisarse: si el cuerpo fuera un
/// objeto completo, guardar el tema desde la barra borraría los favoritos.
/// </para>
///
/// <para>Para borrar una preferencia se manda la clave con valor vacío.</para>
/// </summary>
public sealed record SaveMyPreferencesCommand(
    IReadOnlyDictionary<string, string?> Preferencias) : IRequest<Result>;
