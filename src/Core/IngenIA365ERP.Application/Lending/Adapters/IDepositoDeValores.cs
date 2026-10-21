using IngenIA365ERP.Application.Common.ExternalChannels;

namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>Lo que ofrece un depósito de valores (§3.2): endoso, anulación y certificado de custodia. (nuevo)</summary>
public sealed record CapacidadesDeDeposito(bool Endoso, bool Anulacion, bool CertificadoDeCustodia);

/// <summary>
/// Un canal de depósito de valores para el pagaré desmaterializado (feature 013, US3, T0436; T71–T73; contracts/adaptadores-y-archivos.md §3.1,
/// §3.2, §3.8; FR-044): uno por depósito y el <c>SIMULADO</c> (<c>DepositoDeValoresSimulado</c>, en <c>ExternalChannels/Securities</c>). Cumple
/// las reglas de §3.1 (batería <c>ConformidadDelDepositoDeValores</c>):
/// <list type="number">
/// <item>nunca lanza: un rechazo es <c>Rejected</c>, un dato mal formado <c>InvalidData</c>;</item>
/// <item>el transporte no sube: si no salió, <c>ChannelUnavailable</c>; si pudo llegar, <c>InProcess</c> (se resuelve consultando);</item>
/// <item>«ya existe» no es error: crear dos veces el mismo pagaré (misma <see cref="SolicitudDePagare.PromissoryNotePublicId"/>) responde lo que
/// diga la consulta;</item>
/// <item>las escrituras (crear, endosar, anular) no se reintentan solas: las reintenta quien llama (la tarea <c>cartera.pagares</c>);</item>
/// <item>trae su <see cref="ChannelCode"/> y marca <c>Simulado</c> sólo en el simulado.</item>
/// </list>
/// El efecto legal de lo que responde (el pagaré perfeccionado) lo decide <see cref="GuardiaDeCanalesExternos"/>, nunca el adaptador. (nuevo)
/// </summary>
public interface IDepositoDeValores
{
    string ChannelCode { get; }

    CapacidadesDeDeposito Capacidades { get; }

    Task<ResultadoExterno<EstadoDePagare>> CrearPagareAsync(SolicitudDePagare solicitud, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<EstadoDePagare>> ConsultarPagareAsync(string idExterno, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<EstadoDePagare>> EndosarPagareAsync(string idExterno, string endosatario, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<EstadoDePagare>> AnularPagareAsync(string idExterno, string motivo, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct);
}
