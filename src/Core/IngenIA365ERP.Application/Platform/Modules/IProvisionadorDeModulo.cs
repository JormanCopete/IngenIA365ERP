using IngenIA365ERP.Application.Common.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Platform.Modules;

/// <summary>
/// Lo que un módulo habilitable deja listo en la base de una cooperativa al encenderse o apagarse (feature 020, R1, R6, R12): sus
/// participantes de la bandeja, sus catálogos sembrados. Corre dentro de la cooperativa (lo llama
/// <see cref="TareaDeAprovisionamientoDeModulos"/>) y es <b>idempotente</b>: no hace nada si ya está como debe. Guarda él mismo.
/// </summary>
public interface IProvisionadorDeModulo
{
    /// <summary>El código de <c>ModulosHabilitables</c> que aprovisiona.</summary>
    string Modulo { get; }

    /// <summary>Deja el módulo listo para usar en la cooperativa en curso.</summary>
    Task AsegurarEncendidoAsync(CancellationToken ct);

    /// <summary>Deja de escuchar lo que el módulo escuchaba; los datos no se tocan (FR-003).</summary>
    Task AsegurarApagadoAsync(CancellationToken ct);
}

/// <summary>
/// Pone al día, en cada cooperativa y en cada pasada del programador, lo que cada módulo habilitable necesita según esté encendido o
/// apagado en la base administrativa (feature 020, R1). Así el maestro habilita desde su consola sin que una petición escriba en otra
/// cooperativa, y lo hecho a mano o perdido se recompone solo. Es barato: lee los módulos de la caché y cada provisionador sale sin
/// escribir si ya está como debe.
/// </summary>
public sealed class TareaDeAprovisionamientoDeModulos : ITareaProgramada
{
    public const string NombreDeLaTarea = "modulos.aprovisionar";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => true;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var modulos = servicios.GetRequiredService<IModulosHabilitados>();
        var encendidos = await modulos.DeLaCooperativaActualAsync(ct);
        foreach (var provisionador in servicios.GetServices<IProvisionadorDeModulo>())
        {
            try
            {
                if (encendidos.Contains(provisionador.Modulo)) await provisionador.AsegurarEncendidoAsync(ct);
                else await provisionador.AsegurarApagadoAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                servicios.GetService<ILogger<TareaDeAprovisionamientoDeModulos>>()?.LogError(ex,
                    "[Modulos.AprovisionamientoFallido] {Modulo}: se reintenta en la siguiente pasada.", provisionador.Modulo);
            }
        }
    }
}
