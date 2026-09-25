using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

/// <summary>
/// El alta va por <see cref="AltaConAutorizacion"/> (feature 012, T175): sin autorización, un <c>SaveChangesAsync</c> y la
/// traducción del choque del documento como siempre; con autorización, persona y consentimiento juntos.
/// </summary>
public class CreatePersonCommandHandler(AltaConAutorizacion altas)
    : IRequestHandler<CreatePersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        var alta = await altas.GuardarAsync(request, request.Authorization, AltaConAutorizacion.SinRol, ct);
        return alta.IsSuccess ? Result.Success(alta.Value.Persona.PublicId) : Result.Failure<Guid>(alta.Error);
    }
}
