using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Security.Users.UpdateUser;

/// <summary>
/// Actualiza datos demográficos / identificación del usuario. NO toca
/// contraseña ni roles — usar <c>AdminResetPasswordCommand</c> y
/// <c>AssignRoleCommand</c>/<c>RemoveRoleCommand</c> respectivamente.
///
/// El <c>Username</c> NO es editable (es identidad de máquina). El
/// <c>Email</c> sí, pero queda <c>IsEmailVerified=false</c> hasta nueva
/// verificación (no automatizada en Phase 0).
///
/// <para>
/// Feature 013 (T0267, F5): la persona del maestro se asocia por <see cref="PersonPublicId"/>, que manda sobre
/// <see cref="PersonId"/>. Cartera exige <c>SEC_Users.PersonId</c> a quien radica, decide, vota o desembolsa
/// (<c>Lending.Security.UserWithoutPerson</c>), y hasta el 2026-10-10 la única forma de escribirlo era el entero interno,
/// que la API no publica: nadie podía asociarla. Una persona inexistente o eliminada → <c>Core.Person.NotFound</c>.
/// </para>
/// </summary>
public sealed record UpdateUserCommand(
    Guid UserPublicId,
    string Email,
    string? IdentificationNumber,
    int? PersonId,
    Guid? PersonPublicId = null) : IRequest<Result>;
