using System.Diagnostics;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels.Messaging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ExternalChannels.RestrictiveLists;

/// <summary>
/// El canal <c>SIMULADO</c> de listas restrictivas (feature 013, T0081; T71; contracts/adaptadores-y-archivos.md §3.7; DT §2.22). Decide por
/// el último dígito del documento consultado: 8 <c>PossibleMatch</c> (por nombre), 9 <c>Match</c> (por documento), cualquier otro
/// <c>NoMatch</c>. Marca todo como simulado: en producción una coincidencia simulada se muestra pero ningún resultado del simulado tiene
/// efecto legal (T73). Informa una versión «SIMULADA» de la lista. Con un retardo mayor que el tiempo total responde
/// <c>ChannelUnavailable</c> (una consulta no deja nada a medias). (nuevo)
/// </summary>
public sealed class ListasSimuladas(IOptions<ExternalChannelsOptions> opciones) : IListasRestrictivas
{
    public const string Codigo = CodigosDeCanalExterno.Simulado;

    /// <summary>La lista que informa el simulado.</summary>
    public const string ListaSimulada = "SIMULADA";

    private static readonly VersionDeListaConsultada Version = new(ListaSimulada, 1, new DateOnly(2000, 1, 1), null);

    public string ChannelCode => Codigo;

    public CapacidadesDeListas Capacidades { get; } = new(PorDocumento: true, PorNombre: true, InformaVersion: true);

    public async Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var cronometro = Stopwatch.StartNew();
        var canal = opciones.Value.Canal(Codigo);
        if (canal.DelayMilliseconds > 0)
        {
            await Task.Delay(Math.Min(canal.DelayMilliseconds, canal.TotalTimeoutMilliseconds), ct);
            if (canal.DelayMilliseconds > canal.TotalTimeoutMilliseconds)
                return ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, true,
                    "SIMULADO: el canal no respondió a tiempo.", duracionMs: cronometro.ElapsedMilliseconds);
        }

        if (string.IsNullOrWhiteSpace(consulta.NumeroDeDocumento) && string.IsNullOrWhiteSpace(consulta.NombreCompleto))
            return ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.InvalidData, Codigo, true, "SIMULADO: la consulta no trae documento ni nombre.",
                duracionMs: cronometro.ElapsedMilliseconds);

        var resultado = MensajeriaSimulada.UltimoDigito(consulta.NumeroDeDocumento) switch
        {
            '8' => new ResultadoDeListas(ScreeningOutcome.PossibleMatch,
                [new CoincidenciaEnLista(ListaSimulada, 1, consulta.NombreCompleto.ToUpperInvariant(), null, false, 0.92m, "SIMULADO")], [Version]),
            '9' => new ResultadoDeListas(ScreeningOutcome.Match,
                [new CoincidenciaEnLista(ListaSimulada, 1, consulta.NombreCompleto.ToUpperInvariant(), consulta.NumeroDeDocumento, true, 1m, "SIMULADO")],
                [Version]),
            _ => new ResultadoDeListas(ScreeningOutcome.NoMatch, [], [Version]),
        };
        return ResultadoExterno<ResultadoDeListas>.Ok(resultado, Codigo, true, $"SIM-{consulta.PersonaPublicId:N}", cronometro.ElapsedMilliseconds);
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, Codigo, true));
}
