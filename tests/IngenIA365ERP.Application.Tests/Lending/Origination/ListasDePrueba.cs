using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using Microsoft.Extensions.Options;
using NSubstitute;
using ResultadoDeLaLista = IngenIA365ERP.Domain.Enums.Compliance.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Un canal de listas restrictivas de prueba (feature 013, bloque 6B): responde por documento lo que la prueba diga (por defecto <c>NoMatch</c>),
/// informa una versión de lista y, con <see cref="Salida"/>, un canal caído. Con <see cref="ConsultaReal"/> arma la consulta de listas de Cartera
/// real sobre el escenario de originación. (nuevo)
/// </summary>
internal sealed class ListasDePrueba(string canal = CodigosDeCanalExterno.CargaPropia, bool simulado = false) : IListasRestrictivas
{
    public Dictionary<string, ResultadoDeLaLista> PorDocumento { get; } = new(StringComparer.Ordinal);

    public ExternalOutcome Salida { get; set; } = ExternalOutcome.Ok;

    public VersionDeListaConsultada Version { get; set; } = new("OFAC", 3, new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 30));

    public int Consultas { get; private set; }

    public string ChannelCode { get; } = canal;

    public CapacidadesDeListas Capacidades { get; } = new(true, true, true);

    public Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
    {
        Consultas++;
        if (Salida != ExternalOutcome.Ok) return Task.FromResult(ResultadoExterno<ResultadoDeListas>.Sin(Salida, ChannelCode, simulado, "caído"));
        var resultado = PorDocumento.GetValueOrDefault(consulta.NumeroDeDocumento, ResultadoDeLaLista.NoMatch);
        IReadOnlyList<CoincidenciaEnLista> coincidencias = resultado == ResultadoDeLaLista.NoMatch
            ? []
            : [new CoincidenciaEnLista(Version.ListCode, Version.Version, consulta.NombreCompleto.ToUpperInvariant(),
                resultado == ResultadoDeLaLista.Match ? consulta.NumeroDeDocumento : null, resultado == ResultadoDeLaLista.Match,
                resultado == ResultadoDeLaLista.Match ? 1m : 0.9m, "SDN")];
        return Task.FromResult(ResultadoExterno<ResultadoDeListas>.Ok(new ResultadoDeListas(resultado, coincidencias, [Version]), ChannelCode, simulado));
    }

    public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Task.FromResult(ResultadoExterno<bool>.Ok(true, ChannelCode, simulado));

    /// <summary>La consulta de listas de Cartera real sobre el escenario, con este canal (sin tareas tras el commit: la alerta sale en el acto).</summary>
    public ConsultaDeListasDeCartera ConsultaReal(OriginacionDePrueba o)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(Guid.NewGuid().ToString());
        return new ConsultaDeListasDeCartera(o.Db, new ProveedoresDeListas([this], new CircuitoSiempreCerrado()), Substitute.For<ICredencialesDeCanalExterno>(),
            tenant, Options.Create(o.Opciones), o.L.Alertas, o.G.Actor, o.G.Reloj);
    }
}
