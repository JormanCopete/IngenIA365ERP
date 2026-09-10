using IngenIA365ERP.Application.Common.Configuration;
using MediatR;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Identity.Auth.SessionPolicy;

/// <summary>
/// Los límites de sesión vigentes, para que el cliente cuente con los mismos
/// números que el servidor hace cumplir. Anónima: son dos enteros de
/// configuración, no dicen nada de nadie.
/// </summary>
public sealed record GetSessionPolicyQuery : IRequest<SessionPolicyResult>;

public sealed record SessionPolicyResult(
    int InactivityMinutes,
    int MaxDurationHours);

public sealed class GetSessionPolicyQueryHandler(IOptions<PoliticaDeSesionOptions> options)
    : IRequestHandler<GetSessionPolicyQuery, SessionPolicyResult>
{
    public Task<SessionPolicyResult> Handle(GetSessionPolicyQuery request, CancellationToken ct) =>
        Task.FromResult(new SessionPolicyResult(
            options.Value.InactividadMinutos,
            options.Value.DuracionMaximaHoras));
}
