using System.Collections.Concurrent;
using System.Text.Json;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Core.Connectors;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// El sistema externo falso de las e2e de Transporte (feature 020, T180). Reemplaza la única salida HTTP de los conectores
/// (<see cref="IEjecutorDeConector"/>) dentro del host: todo lo demás —emisión, bandeja, despachador, consumidor, sesión, plantillas y mapa de
/// respuestas— corre de verdad. Responde el inicio de sesión con un token y la operación con los códigos que se le pidan, en orden (por defecto
/// 200), y anota el <c>externalID</c> de cada llamada. Va en proceso para no depender de un certificado TLS de prueba: la guarda de
/// direcciones la cubren sus pruebas y <c>ConectoresSeguridadTests</c>.
/// </summary>
public sealed class ProveedorFalso : IEjecutorDeConector
{
    private readonly ConcurrentQueue<int> _codigos = new();

    public ConcurrentQueue<string> ExternalIds { get; } = new();

    public int IniciosDeSesion;

    /// <summary>Las respuestas de las siguientes operaciones, en orden; agotadas, 200.</summary>
    public void Responder(params int[] codigos)
    {
        while (_codigos.TryDequeue(out _)) { }
        foreach (var c in codigos) _codigos.Enqueue(c);
    }

    public Task<RespuestaHttp> EjecutarAsync(SolicitudHttp solicitud, CancellationToken ct)
    {
        if (solicitud.Url.AbsolutePath.EndsWith("/login", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref IniciosDeSesion);
            return Task.FromResult(new RespuestaHttp(200, "{\"token\":\"token-de-prueba\"}", 5));
        }

        if (solicitud.Cuerpo is { } cuerpo)
        {
            using var doc = JsonDocument.Parse(cuerpo);
            if (doc.RootElement.TryGetProperty("externalID", out var id)) ExternalIds.Enqueue(id.GetString() ?? string.Empty);
        }
        var codigo = _codigos.TryDequeue(out var c) ? c : 200;
        var texto = codigo switch
        {
            200 => "{\"ok\":true}",
            400 => "{\"error\":\"El vehículo ya tiene vigencia hasta la fecha de corte\"}",
            _ => "{\"error\":\"falla interna\"}",
        };
        return Task.FromResult(new RespuestaHttp(codigo, texto, 7));
    }
}

/// <summary>La fixture de las e2e de Transporte: el host de siempre con el <see cref="ProveedorFalso"/> como salida de los conectores.</summary>
public sealed class ApiConProveedorFalsoFixture : CentralIdentityApiFixture
{
    public ProveedorFalso Proveedor { get; } = new();

    protected override void ConfigurarHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEjecutorDeConector>(Proveedor)));
}
