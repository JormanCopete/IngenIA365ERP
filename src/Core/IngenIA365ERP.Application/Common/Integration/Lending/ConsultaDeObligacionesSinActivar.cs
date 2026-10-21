namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// La implementación provisional de <see cref="IConsultaDeObligaciones"/> mientras Cartera no exista en el despliegue (feature 013,
/// T0152; T85, contracts/interfaces.md §3.17, §6.1, §7): <see cref="EstadoDeCartera.NotActivated"/>, una entrada por persona pedida
/// sin obligaciones y sin «migrada, sin activar», y ninguna cuota de nómina. Así Nómina, Personas y Cumplimiento funcionan igual que
/// sin cartera. No lee ni escribe nada. La real (<c>Application/Lending/Integration</c>) la reemplaza en <c>DependencyInjection</c>
/// cuando nace <c>CarteraNucleo</c>. (nuevo)
/// </summary>
public sealed class ConsultaDeObligacionesSinActivar : IConsultaDeObligaciones
{
    public Task<ObligacionesDePersonasDto> ObligacionesAsync(ConsultaDeObligaciones consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var personas = consulta.PersonPublicIds.Distinct()
            .Select(p => new ObligacionesDePersonaDto(p, MigratedNotActivated: false, Obligations: []))
            .ToList();
        return Task.FromResult(new ObligacionesDePersonasDto(EstadoDeCartera.NotActivated, personas));
    }

    public Task<CuotasDeNominaDto> CuotasDeNominaAsync(ConsultaDeCuotasDeNomina consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        return Task.FromResult(new CuotasDeNominaDto(EstadoDeCartera.NotActivated, [], []));
    }
}
