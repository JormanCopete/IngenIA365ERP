using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

/// <summary>
/// Crea una persona en la tabla maestra COR_People: datos personales y las banderas de
/// rol que son sólo una marca. Las derivadas (Empleado, Asociado, Vendedor) no están en el
/// contrato —ver <see cref="PersonInput"/>—: las enciende el módulo que crea la fila hija.
/// Hereda de <see cref="PersonInput"/> para que el JSON siga siendo plano: las pantallas y
/// los e2e que mandan <c>isEmployee</c> siguen funcionando, sólo que ese campo ya no llega.
/// </summary>
public record CreatePersonCommand : PersonInput, IRequest<Result<Guid>>;
