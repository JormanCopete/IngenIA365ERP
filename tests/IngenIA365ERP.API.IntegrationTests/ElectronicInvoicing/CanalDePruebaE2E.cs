using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;

namespace IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;

/// <summary>
/// Un segundo canal para las e2e del cambio de canal (T687; quickstart §6.10): responde exactamente como <see cref="CanalSimulado"/> —la
/// misma memoria por cooperativa, ambiente y número, así un documento que cambia de canal se reconoce— pero se llama <c>PRUEBA</c>. Sólo lo
/// registra la fixture de las pruebas de integración; la raíz de composición de la API registra únicamente <c>SIMULADO</c>. (nuevo)
/// </summary>
public sealed class CanalDePruebaE2E(CanalSimulado simulado) : ICanalDeEmisionElectronica
{
    public const string Codigo = "PRUEBA";

    public string ChannelCode => Codigo;

    public CapacidadesDelCanal Capacidades => simulado.Capacidades;

    public Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct) =>
        simulado.EmitirAsync(documento, contexto, ct);

    public Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct) =>
        simulado.ConsultarEstadoAsync(referencia, contexto, ct);

    public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) =>
        simulado.EmitirEventoAsync(evento, contexto, ct);

    public Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct) =>
        simulado.DescargarArtefactoAsync(referencia, tipo, contexto, ct);

    public Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct) => simulado.ProbarAsync(contexto, ct);

    public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct) => simulado.ConsultarRangosAsync(contexto, ct);

    public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) =>
        simulado.ConsultarAdquirenteAsync(tipoDeIdentificacionDian, numero, contexto, ct);
}
