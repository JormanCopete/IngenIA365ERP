using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Messaging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ExternalChannels.CreditBureaus;

/// <summary>
/// Lo que el simulado de centrales ya respondió, por cooperativa y documento (feature 013, T0401; §3.6: «recuerda por cooperativa lo que respondió,
/// para que la consulta siguiente sea coherente»). Singleton. (nuevo)
/// </summary>
public sealed class MemoriaDeCentralSimulada
{
    private readonly ConcurrentDictionary<(Guid Cooperativa, string Documento), InformeRecordado> _informes = new();

    internal InformeRecordado Recordar(Guid cooperativa, string documento, Func<InformeRecordado> crear) =>
        _informes.GetOrAdd((cooperativa, documento), _ => crear());
}

/// <summary>Un informe del simulado ya entregado: el informe y su referencia <c>SIM-…</c>. (nuevo)</summary>
internal sealed record InformeRecordado(InformeDeCentral Informe, string Referencia);

/// <summary>
/// El canal <c>SIMULADO</c> de centrales de riesgo (feature 013, T0401; T71, T73; contracts/adaptadores-y-archivos.md §3.6, §3.7; DT §2.22). Decide
/// por el último dígito del documento consultado: 1 <c>Ok</c> con reporte negativo, 2 <c>Ok</c> sin historia, 3 <c>Ok</c> con puntaje bajo, 5
/// <c>ChannelUnavailable</c>, cualquier otro <c>Ok</c> sin marcas. Marca todo como simulado (en producción la guardia le quita el efecto de evidencia
/// para aprobar), sus referencias empiezan con <c>SIM-</c> y su informe crudo dice «SIMULADO». Recuerda por cooperativa y documento lo que respondió.
/// Con un retardo mayor que el tiempo total responde <c>ChannelUnavailable</c>. Nunca lanza. (nuevo)
/// </summary>
public sealed class CentralDeRiesgoSimulada(IOptions<ExternalChannelsOptions> opciones, MemoriaDeCentralSimulada memoria) : ICentralDeRiesgo
{
    public const string Codigo = CodigosDeCanalExterno.Simulado;

    /// <summary>El puntaje del simulado sin marcas, con reporte negativo y bajo (escala 0–1000).</summary>
    public const int PuntajeNormal = 780;
    public const int PuntajeConReporteNegativo = 520;
    public const int PuntajeBajo = 310;

    public string ChannelCode => Codigo;

    public CapacidadesDeCentral Capacidades { get; } = new(Puntaje: true, MarcasNegativas: true, ArtefactoCrudo: true);

    public async Task<ResultadoExterno<InformeDeCentral>> ConsultarAsync(ConsultaACentral consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ArgumentNullException.ThrowIfNull(contexto);
        var cronometro = Stopwatch.StartNew();
        var canal = opciones.Value.Canal(Codigo);
        if (canal.DelayMilliseconds > 0)
        {
            await Task.Delay(Math.Min(canal.DelayMilliseconds, canal.TotalTimeoutMilliseconds), ct);
            if (canal.DelayMilliseconds > canal.TotalTimeoutMilliseconds)
                return ResultadoExterno<InformeDeCentral>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true, "SIMULADO: la central no respondió a tiempo.",
                    duracionMs: cronometro.ElapsedMilliseconds);
        }

        var documento = consulta.NumeroDeDocumento?.Trim();
        if (string.IsNullOrEmpty(documento))
            return ResultadoExterno<InformeDeCentral>.Sin(ExternalOutcome.InvalidData, Codigo, true, "SIMULADO: la consulta no trae documento.",
                codigoDelProveedor: "SIM-0", duracionMs: cronometro.ElapsedMilliseconds);

        var digito = MensajeriaSimulada.UltimoDigito(documento);
        if (digito == '5')
            return ResultadoExterno<InformeDeCentral>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true, "SIMULADO: la central no está disponible.",
                codigoDelProveedor: "SIM-5", duracionMs: cronometro.ElapsedMilliseconds);

        var recordado = memoria.Recordar(contexto.TenantPublicId, documento, () =>
        {
            var informe = digito switch
            {
                '1' => new InformeDeCentral(PuntajeConReporteNegativo, false, ["SIMULADO: obligación en mora reportada por otra entidad"], DateTimeOffset.UtcNow),
                '2' => new InformeDeCentral(null, true, [], DateTimeOffset.UtcNow),
                '3' => new InformeDeCentral(PuntajeBajo, false, [], DateTimeOffset.UtcNow),
                _ => new InformeDeCentral(PuntajeNormal, false, [], DateTimeOffset.UtcNow),
            };
            return new InformeRecordado(informe, $"SIM-{Guid.NewGuid():N}");
        });

        var crudo = JsonSerializer.Serialize(new
        {
            aviso = "SIMULADO: informe de prueba sin validez, generado por el canal simulado de centrales de riesgo.",
            referencia = recordado.Referencia,
            central = contexto.BureauCode,
            documento,
            puntaje = recordado.Informe.Puntaje,
            sinHistoria = recordado.Informe.SinHistoria,
            marcasNegativas = recordado.Informe.MarcasNegativas,
            consultadoEn = recordado.Informe.ConsultadoEn,
        }, new JsonSerializerOptions { WriteIndented = true });
        var artefacto = new ArtefactoExterno("InformeCrudo", $"informe-central-SIMULADO-{recordado.Referencia}.json", "application/json",
            Encoding.UTF8.GetBytes(crudo));
        return ResultadoExterno<InformeDeCentral>.Ok(recordado.Informe, Codigo, true, recordado.Referencia, cronometro.ElapsedMilliseconds, [artefacto]);
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, Codigo, true));
}
