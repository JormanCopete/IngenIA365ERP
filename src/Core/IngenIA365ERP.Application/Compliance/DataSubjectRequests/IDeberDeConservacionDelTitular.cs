using IngenIA365ERP.Domain.Common.Compliance;

namespace IngenIA365ERP.Application.Compliance.DataSubjectRequests;

/// <summary>
/// Si hay un deber legal de conservar o reportar los datos del titular (feature 013, T69 b, T0088; contracts/interfaces.md §10.3): lo que
/// <c>ReglaDeRevocacion</c> necesita de sus obligaciones —vivas y cerradas, como deudor o codeudor, con los días de mora del dato negativo
/// de una cerrada— y si está «migrada, sin activar». La fase 3 lo conecta a
/// <c>IConsultaDeObligaciones.ObligacionesAsync([titular], hoy, IncludeAsCoDebtor: true, IncludeClosed: true)</c>, que hoy no existe (lo
/// crea la fase Fundacional B); hasta entonces el defecto <see cref="SinObligacionesRegistradas"/> responde «sin obligaciones registradas».
/// <para>
/// El DTO de obligaciones de interfaces §6.1 no trae los días de mora del dato negativo de una cerrada (T0028): la implementación de la fase
/// 3 los tiene que obtener para <see cref="ObligacionParaRevocacion.DiasDeMoraDelDatoNegativo"/>. (nuevo)
/// </para>
/// </summary>
public interface IDeberDeConservacionDelTitular
{
    Task<DeberDelTitular> ConsultarAsync(Guid personPublicId, DateOnly hoy, CancellationToken ct);
}

/// <summary>
/// Lo que respondió la consulta del deber: se sella tal cual en <c>COR_DataSubjectRequests.DutyEvidenceJson</c>. (nuevo)
/// </summary>
/// <param name="Fuente">Quién respondió y con qué alcance (la constancia dice de dónde salió la respuesta).</param>
public sealed record DeberDelTitular(
    bool MigradaSinActivar,
    IReadOnlyList<ObligacionParaRevocacion> Obligaciones,
    string Fuente,
    DateOnly ConsultadoEl);

/// <summary>El defecto hasta la fase 3: Cartera no tiene obligaciones registradas, así que no hay deber por obligaciones. (nuevo)</summary>
public sealed class SinObligacionesRegistradas : IDeberDeConservacionDelTitular
{
    public const string Fuente = "Sin obligaciones registradas: Cartera todavía no registra obligaciones de esta cooperativa.";

    public Task<DeberDelTitular> ConsultarAsync(Guid personPublicId, DateOnly hoy, CancellationToken ct) =>
        Task.FromResult(new DeberDelTitular(false, [], Fuente, hoy));
}
