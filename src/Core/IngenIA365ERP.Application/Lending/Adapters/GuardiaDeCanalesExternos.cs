using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Adapters;

// Feature 013 (T0079; T73, F-03, CH20; contracts/adaptadores-y-archivos.md §3.6): la guardia única del efecto legal de los canales
// simulados.

/// <summary>
/// El ambiente de los canales externos (§3.6). <see cref="AmbienteDePruebas"/> lo fija <c>Program.cs</c> con
/// <c>!builder.Environment.IsProduction()</c>; ninguna cooperativa lo cambia. Sin configurar es <c>false</c>: el defecto seguro es el de
/// producción, donde un resultado simulado no tiene efecto legal. (nuevo)
/// </summary>
public sealed class CanalesExternosOptions
{
    /// <summary>Fuera de producción: un resultado simulado sí tiene efecto (y todo documento lleva «AMBIENTE DE PRUEBAS – SIN VALIDEZ»).</summary>
    public bool AmbienteDePruebas { get; set; }
}

/// <summary>El efecto legal que una operación le pide a un resultado externo (§3.6, fijado allí). (nuevo)</summary>
public enum EfectoLegalDeCanal
{
    /// <summary>El pagaré desmaterializado queda perfeccionado para desembolsar.</summary>
    PromissoryNotePerfection = 1,

    /// <summary>El envío cuenta como constancia de aviso previo.</summary>
    PriorNoticeEvidence = 2,

    /// <summary>La consulta a centrales sirve de evidencia para aprobar.</summary>
    BureauEvidenceForApproval = 3,

    /// <summary>El mensaje salió de verdad hacia el titular.</summary>
    RealDelivery = 4,
}

/// <summary>
/// Lo que decide la guardia (§3.6): <see cref="ConEfecto"/> o <see cref="SinEfecto"/> con el motivo y qué configurar para que lo tenga.
/// (nuevo)
/// </summary>
public sealed record VeredictoDeEfectoLegal(bool TieneEfecto, string? Motivo, string? QueConfigurar)
{
    /// <summary>El resultado cuenta.</summary>
    public static VeredictoDeEfectoLegal ConEfecto { get; } = new(true, null, null);

    /// <summary>El resultado no cuenta: <paramref name="motivo"/> y <paramref name="queConfigurar"/> van al usuario.</summary>
    public static VeredictoDeEfectoLegal SinEfecto(string motivo, string queConfigurar) => new(false, motivo, queConfigurar);

    /// <summary>El error con que la operación se niega (<c>Lending.Adapter.SimulatedInProduction</c>); nulo con efecto.</summary>
    public Error? ComoError() => TieneEfecto
        ? null
        : new ErrorConDatos(ErroresDeCanalesExternos.CodigoSimuladoEnProduccion, $"{Motivo} {QueConfigurar}", new { reason = Motivo, configure = QueConfigurar });
}

/// <summary>
/// El <b>único</b> sitio que decide si un resultado externo cuenta (feature 013, T0079; T73; §3.6). En producción
/// (<see cref="CanalesExternosOptions.AmbienteDePruebas"/> en <c>false</c>) un resultado del canal <c>SIMULADO</c> nunca perfecciona un
/// pagaré, nunca es constancia de aviso previo ni evidencia de centrales para aprobar, y no es una entrega real; fuera de producción sí.
/// <c>CARGA-PROPIA</c> no es simulado y vale en producción. Simulado es el resultado que lo dice (<see cref="ResultadoExterno{T}.Simulado"/>)
/// <b>o</b> el que viene del canal <c>SIMULADO</c>: un adaptador que se equivoca en la bandera no le da efecto legal a un simulado.
/// (nuevo)
/// </summary>
public sealed class GuardiaDeCanalesExternos(IOptions<CanalesExternosOptions> opciones)
{
    /// <summary>¿El resultado tiene <paramref name="efecto"/>?</summary>
    public VeredictoDeEfectoLegal Evaluar<T>(EfectoLegalDeCanal efecto, ResultadoExterno<T> resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        return Evaluar(efecto, resultado.ChannelCode, resultado.Simulado);
    }

    /// <summary>Lo mismo para un resultado ya sellado en una operación (canal y bandera de simulado).</summary>
    public VeredictoDeEfectoLegal Evaluar(EfectoLegalDeCanal efecto, string? channelCode, bool simulado)
    {
        var esSimulado = simulado || CodigosDeCanalExterno.EsSimulado(channelCode);
        if (!esSimulado || opciones.Value.AmbienteDePruebas) return VeredictoDeEfectoLegal.ConEfecto;

        return efecto switch
        {
            EfectoLegalDeCanal.PromissoryNotePerfection => VeredictoDeEfectoLegal.SinEfecto(
                "El depósito de valores es el canal simulado: en producción no perfecciona el pagaré.",
                "Configure el depósito de valores en Administración › Canales externos o use pagaré físico."),
            EfectoLegalDeCanal.PriorNoticeEvidence => VeredictoDeEfectoLegal.SinEfecto(
                "El aviso salió por el canal simulado: en producción no es constancia de aviso previo.",
                "Configure el canal de mensajería en Administración › Canales externos o registre la constancia manual."),
            EfectoLegalDeCanal.BureauEvidenceForApproval => VeredictoDeEfectoLegal.SinEfecto(
                "La consulta a centrales es del canal simulado: en producción no sirve de evidencia para aprobar.",
                "Configure la central de riesgo en Administración › Canales externos."),
            EfectoLegalDeCanal.RealDelivery => VeredictoDeEfectoLegal.SinEfecto(
                "El canal es el simulado: en producción el mensaje no sale.",
                "Configure el canal de mensajería en Administración › Canales externos o use el correo."),
            _ => throw new ArgumentOutOfRangeException(nameof(efecto), efecto, "Efecto legal desconocido."),
        };
    }
}
