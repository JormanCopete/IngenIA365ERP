using System.Diagnostics;
using System.Globalization;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.ExternalChannels.RestrictiveLists;

/// <summary>
/// El canal <c>CARGA-PROPIA</c> de listas restrictivas (feature 013, T0081; T70, T71; contracts/adaptadores-y-archivos.md §3.8; data-model
/// §4.8): consulta la carga vigente que mantiene el oficial de cumplimiento (<see cref="ICargaDeListasRestrictivas"/>). <b>No</b> es
/// simulado: vale en producción.
/// <list type="bullet">
/// <item>Coincidencia exacta por documento normalizado (<see cref="CoincidenciaDeNombres.NormalizarDocumento"/>) → <c>Match</c>.</item>
/// <item>Posible por nombre (contra el nombre y los alias de la entrada) con <see cref="CoincidenciaDeNombres.Evaluar"/>, en memoria —SQL
/// Server no tiene <c>pg_trgm</c>—, sobre el umbral <c>Listas.UmbralDeCoincidenciaDeNombre</c> (LND, defecto 0,90) → <c>PossibleMatch</c>.</item>
/// <item>Sin listas cargadas, <c>NoMatch</c> sin versiones.</item>
/// </list>
/// Nunca lanza: si la carga no se pudo leer, <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public sealed class CanalDeListasCargadas(
    ICargaDeListasRestrictivas carga,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ILogger<CanalDeListasCargadas> logger) : IListasRestrictivas
{
    public const string Codigo = CodigosDeCanalExterno.CargaPropia;

    public string ChannelCode => Codigo;

    public CapacidadesDeListas Capacidades { get; } = new(PorDocumento: true, PorNombre: true, InformaVersion: true);

    public async Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        var cronometro = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(consulta.NumeroDeDocumento) && string.IsNullOrWhiteSpace(consulta.NombreCompleto))
            return ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.InvalidData, Codigo, false, "La consulta no trae documento ni nombre.",
                duracionMs: cronometro.ElapsedMilliseconds);

        CargaDeListasRestrictivas vigente;
        decimal umbral;
        try
        {
            var hoy = reloj.HoyLocal;
            vigente = await carga.LeerVigenteAsync(hoy, ct);
            umbral = await UmbralAsync(hoy, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "[Listas.CargaIlegible] No se pudo leer la carga vigente de las listas restrictivas.");
            return ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.ChannelUnavailable, Codigo, false,
                $"No se pudo leer la carga de las listas ({ex.GetType().Name}).", duracionMs: cronometro.ElapsedMilliseconds);
        }

        var documento = CoincidenciaDeNombres.NormalizarDocumento(consulta.NumeroDeDocumento);
        var coincidencias = new List<CoincidenciaEnLista>();
        foreach (var entrada in vigente.Entradas)
        {
            if (documento.Length > 0 && string.Equals(entrada.DocumentNumberNormalized, documento, StringComparison.Ordinal))
            {
                coincidencias.Add(new CoincidenciaEnLista(entrada.ListCode, entrada.Version, entrada.FullName, entrada.DocumentNumberNormalized, true, 1m,
                    entrada.Program));
                continue;
            }

            if (string.IsNullOrWhiteSpace(consulta.NombreCompleto)) continue;
            var porNombre = CoincidenciaDeNombres.Evaluar(consulta.NombreCompleto, entrada.FullName, entrada.Aliases, umbral);
            if (porNombre.Coincide)
                coincidencias.Add(new CoincidenciaEnLista(entrada.ListCode, entrada.Version, entrada.FullName, entrada.DocumentNumberNormalized, false,
                    porNombre.Similitud, entrada.Program));
        }

        var resultado = coincidencias.Any(c => c.PorDocumento) ? ScreeningOutcome.Match
            : coincidencias.Count > 0 ? ScreeningOutcome.PossibleMatch
            : ScreeningOutcome.NoMatch;
        var ordenadas = coincidencias.OrderByDescending(c => c.PorDocumento).ThenByDescending(c => c.Similitud)
            .ThenBy(c => c.ListCode, StringComparer.Ordinal).ToList();
        return ResultadoExterno<ResultadoDeListas>.Ok(new ResultadoDeListas(resultado, ordenadas, vigente.Versiones), Codigo, false,
            duracionMs: cronometro.ElapsedMilliseconds);
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, Codigo, false));

    private async Task<decimal> UmbralAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ListasUmbralDeCoincidenciaDeNombre, hoy, ct: ct);
        var umbral = leido.IsSuccess ? leido.Value.Como<decimal>() : 0m;
        if (umbral is > 0m and <= 1m) return umbral;
        return decimal.Parse(ParametrosDeCartera.Definiciones
            .First(d => d.Clave == ParametrosDeCartera.ListasUmbralDeCoincidenciaDeNombre).DefectoSeguro, CultureInfo.InvariantCulture);
    }
}
