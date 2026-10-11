using IngenIA365ERP.Application.Common.ExternalChannels;

namespace IngenIA365ERP.Application.Lending.Adapters;

// Feature 013, US2 (bloque 6B; T0400; T71, T73; contracts/adaptadores-y-archivos.md §3.2, §3.8): el puerto de las centrales de riesgo. Nombres en
// español y sin el sufijo Dto (precedente de ResultadoDeCanal de la 012, §3.1).

/// <summary>A quién se consulta, para qué y con qué autorización (§3.2). (nuevo)</summary>
public sealed record ConsultaACentral(Guid PersonaPublicId, string TipoDeDocumento, string NumeroDeDocumento, string Proposito, Guid AutorizacionPublicId);

/// <summary>
/// El informe de una central (§3.2): el puntaje, si la persona no tiene historia y las marcas negativas. El informe crudo viaja en
/// <see cref="ResultadoExterno{T}.Artefactos"/>. (nuevo)
/// </summary>
public sealed record InformeDeCentral(int? Puntaje, bool SinHistoria, IReadOnlyList<string> MarcasNegativas, DateTimeOffset ConsultadoEn);

/// <summary>Lo que ofrece una central (§3.2): puntaje, marcas negativas y el artefacto crudo. (nuevo)</summary>
public sealed record CapacidadesDeCentral(bool Puntaje, bool MarcasNegativas, bool ArtefactoCrudo);

/// <summary>
/// Un canal de una central de riesgo (feature 013, T0400; T71; §3.2): uno por central (<c>BureauCode</c>) y el <c>SIMULADO</c>
/// (<c>CentralDeRiesgoSimulada</c>). Cumple las reglas de §3.1 (batería <c>ConformidadDeCentral</c>): nunca lanza por un rechazo ni por el
/// transporte, trae su <see cref="ChannelCode"/> y marca <c>Simulado</c> sólo en el simulado. Ningún puerto <b>reporta</b> a centrales: el
/// reporte es el archivo de §7. (nuevo)
/// </summary>
public interface ICentralDeRiesgo
{
    string ChannelCode { get; }

    CapacidadesDeCentral Capacidades { get; }

    Task<ResultadoExterno<InformeDeCentral>> ConsultarAsync(ConsultaACentral consulta, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct);
}
