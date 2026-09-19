using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Audit.RegisterOptionAccess;

/// <summary>
/// Feature 009, US7 (FR-051): el ingreso a cada opción del ERP queda en la auditoría. El comando
/// no escribe nada en SQL: existe para que <c>AuditBehavior</c> —que audita todo comando— deje el
/// evento <c>RegisterOptionAccess</c> con módulo <c>Navigation</c>, ruta y título en
/// <c>newValuesJson</c>. El cliente lo manda al cambiar de pantalla (<c>RegistroDeAccesos</c>) y
/// la API responde 202 sin esperar la escritura en Mongo.
/// </summary>
public sealed record RegisterOptionAccessCommand(string Route, string Title) : IRequest<Result>;

public sealed class RegisterOptionAccessCommandValidator : AbstractValidator<RegisterOptionAccessCommand>
{
    public RegisterOptionAccessCommandValidator()
    {
        RuleFor(x => x.Route).NotEmpty().MaximumLength(300).Must(r => r.StartsWith('/')).WithMessage("La ruta empieza con «/».");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
    }
}

public sealed class RegisterOptionAccessCommandHandler : IRequestHandler<RegisterOptionAccessCommand, Result>
{
    public Task<Result> Handle(RegisterOptionAccessCommand request, CancellationToken ct) => Task.FromResult(Result.Success());
}
