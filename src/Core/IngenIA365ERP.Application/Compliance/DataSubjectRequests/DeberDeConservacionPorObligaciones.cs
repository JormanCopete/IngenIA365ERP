using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Common.Compliance;

namespace IngenIA365ERP.Application.Compliance.DataSubjectRequests;

/// <summary>
/// El deber de conservar los datos del titular leído de Cartera (feature 013, T0150; contracts/interfaces.md §10.3): pregunta
/// <c>IConsultaDeObligaciones.ObligacionesAsync([titular], hoy, IncludeAsCoDebtor: true, IncludeClosed: true)</c> y traduce cada
/// obligación —viva o cerrada, como deudor o codeudor— a <see cref="ObligacionParaRevocacion"/>, más la marca «migrada, sin activar».
/// Con Cartera sin activar y sin nada que la ate, la constancia dice lo mismo que el defecto anterior
/// (<see cref="SinObligacionesRegistradas.Fuente"/>).
/// <para>
/// El DTO de obligaciones (§6.1) no trae los días de mora del dato negativo de una cerrada (T0028): mientras Cartera no los exponga se
/// toman los <see cref="ObligacionDto.DaysPastDue"/> que informe la consulta (cero en una cerrada al día). (nuevo)
/// </para>
/// </summary>
public sealed class DeberDeConservacionPorObligaciones(IConsultaDeObligaciones obligaciones) : IDeberDeConservacionDelTitular
{
    public async Task<DeberDelTitular> ConsultarAsync(Guid personPublicId, DateOnly hoy, CancellationToken ct)
    {
        var respuesta = await obligaciones.ObligacionesAsync(
            new ConsultaDeObligaciones([personPublicId], hoy, IncludeAsCoDebtor: true, IncludeClosed: true), ct);
        var persona = respuesta.People.FirstOrDefault(p => p.PersonPublicId == personPublicId);
        var lista = persona?.Obligations
            .Select(o => new ObligacionParaRevocacion(o.Number, o.IsLive, o.ClosedOn, Math.Max(0, o.DaysPastDue)))
            .ToList() ?? [];
        var migrada = persona?.MigratedNotActivated ?? false;

        var fuente = respuesta.LendingStatus == EstadoDeCartera.NotActivated && lista.Count == 0 && !migrada
            ? SinObligacionesRegistradas.Fuente
            : $"Cartera ({(respuesta.LendingStatus == EstadoDeCartera.Active ? "activada" : "sin activar")}): {lista.Count} obligación(es) como deudor o codeudor, vivas y cerradas"
              + (migrada ? "; en una carga de Cartera por activar." : ".");
        return new DeberDelTitular(migrada, lista, fuente, hoy);
    }
}
