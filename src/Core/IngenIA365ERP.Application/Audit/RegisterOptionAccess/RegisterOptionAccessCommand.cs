using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Audit.RegisterOptionAccess;

/// <summary>
/// Feature 009, US7 (FR-051): el ingreso a cada opción del ERP queda en la auditoría, con módulo
/// <c>Navigation</c>, ruta y título en <c>newValuesJson</c>. El cliente lo manda al cambiar de
/// pantalla (<c>RegistroDeAccesos</c>) y la API responde 202.
///
/// <para>
/// Feature 012 (T37, T38; T062): <c>Navigation</c> es un módulo encadenado, así que el handler inserta
/// su propia fila en <c>COR_AuditOutbox</c> (<see cref="AuditoriaEncadenada.RegistrarAsync"/>) y
/// <c>AuditBehavior</c> no vuelve a auditarlo. Sin cooperativa resuelta va a Mongo como antes.
/// </para>
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

public sealed class RegisterOptionAccessCommandHandler(IServiceProvider servicios) : IRequestHandler<RegisterOptionAccessCommand, Result>
{
    public async Task<Result> Handle(RegisterOptionAccessCommand request, CancellationToken ct)
    {
        await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
        {
            Action = "RegisterOptionAccess",
            EntityType = "RegisterOptionAccess",
            Module = ModuloDeAuditoria.Navigation,
            NewValues = request,
            HttpStatusCode = 202,
        }, ct);
        return Result.Success();
    }
}
