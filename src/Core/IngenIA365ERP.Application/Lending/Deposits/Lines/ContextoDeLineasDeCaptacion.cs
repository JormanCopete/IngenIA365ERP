using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>Lo que el perfil regulatorio vigente dice a las líneas de captación y de aportes (FR-018). (nuevo)</summary>
public sealed record PerfilParaLineas(bool AutorizaCaptar, bool SoloAsociados, bool EsFondoDeEmpleados, string? TipoDeOrganizacion, Guid? PerfilPublicId)
{
    public static PerfilParaLineas SinPerfil { get; } = new(false, false, false, null, null);
}

/// <summary>
/// Los topes legales que acotan una línea de aportes, con su fuente (FR-014, FR-043; research E15): el tope de la cuota de un fondo de empleados, la
/// fracción mínima a aportes y el límite de participación (el más estricto entre persona natural y jurídica). Nulo = no hay. (nuevo)
/// </summary>
public sealed record TopesDeAportes(
    decimal? TopeDeCuota,
    string? FuenteDelTope,
    decimal? FraccionMinima,
    string? FuenteDeLaFraccion,
    decimal? LimiteDeParticipacion,
    string? ClaveDelLimite,
    string? FuenteDelLimite)
{
    public static TopesDeAportes Ninguno { get; } = new(null, null, null, null, null, null, null);
}

/// <summary>
/// Lo que las líneas de captación y de aportes leen fuera de sí (feature 014, US1, T064–T067): el perfil regulatorio vigente, los topes de aportes,
/// la anticipación del aviso de cambio de tasa y la entrega desplegada. Separado para que las pruebas de los comandos lo fijen. (nuevo)
/// </summary>
public interface IContextoDeLineasDeCaptacion
{
    /// <summary>La entrega de Cartera desplegada: sin K7 no se activan aportes y sin K8 no se activan captaciones (api.md §1.3).</summary>
    EntregaDeCartera Entrega { get; }

    Task<PerfilParaLineas> PerfilAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>Los topes que rigen <paramref name="fecha"/>; falla (<c>NationalValues.Missing</c>) si un límite legal que los acota no está replicado.</summary>
    Task<Result<TopesDeAportes>> TopesDeAportesAsync(DateOnly fecha, bool esFondoDeEmpleados, CancellationToken ct);

    /// <summary>Los días de anticipación del aviso de cambio de tasa del reglamento (<c>Captaciones.AnticipacionAvisoCambioDeTasaDias</c>); 0 sin valor.</summary>
    Task<int> AnticipacionDelAvisoAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>
/// La implementación de <see cref="IContextoDeLineasDeCaptacion"/>: el perfil por <see cref="IPerfilRegulatorio"/> y los parámetros por
/// <see cref="ReglasDeParametrosDeCartera"/> (lo más estricto entre la cooperativa y la ley, con la fuente del que rige). (nuevo)
/// </summary>
public sealed class ContextoDeLineasDeCaptacion(IPerfilRegulatorio perfiles, ReglasDeParametrosDeCartera parametros, EntregaDeCartera? entregaDesplegada = null)
    : IContextoDeLineasDeCaptacion
{
    public EntregaDeCartera Entrega => entregaDesplegada ?? CatalogoDeCartera.EntregaVigente;

    public async Task<PerfilParaLineas> PerfilAsync(DateOnly fecha, CancellationToken ct)
    {
        var perfil = await perfiles.VigenteAsync(fecha, null, ct);
        return perfil is null
            ? PerfilParaLineas.SinPerfil
            : new PerfilParaLineas(perfil.MayTakeDeposits, perfil.DepositsFromAssociatesOnly, perfil.EmployeeFundCategory is not null,
                perfil.OrganizationTypeCode, perfil.VersionPublicId);
    }

    public async Task<Result<TopesDeAportes>> TopesDeAportesAsync(DateOnly fecha, bool esFondoDeEmpleados, CancellationToken ct)
    {
        decimal? tope = null, fraccion = null;
        string? fuenteDelTope = null, fuenteDeLaFraccion = null;
        if (esFondoDeEmpleados)
        {
            var leidoTope = await LeerAsync(ParametrosDeCartera.AportesTopeCuotaFondoEmpleados, fecha, ct);
            if (leidoTope.IsFailure) return Result.Failure<TopesDeAportes>(leidoTope.Error);
            (tope, fuenteDelTope) = leidoTope.Value;
            var leidaFraccion = await LeerAsync(ParametrosDeCartera.AportesFraccionMinima, fecha, ct);
            if (leidaFraccion.IsFailure) return Result.Failure<TopesDeAportes>(leidaFraccion.Error);
            (fraccion, fuenteDeLaFraccion) = leidaFraccion.Value;
        }

        decimal? limite = null;
        string? clave = null, fuenteDelLimite = null;
        foreach (var claveDeLimite in new[] { ParametrosDeCartera.AportesLimiteParticipacionPersonaNatural, ParametrosDeCartera.AportesLimiteParticipacionPersonaJuridica })
        {
            var leido = await LeerAsync(claveDeLimite, fecha, ct);
            if (leido.IsFailure) return Result.Failure<TopesDeAportes>(leido.Error);
            if (leido.Value.Valor is { } valor && (limite is null || valor < limite))
                (limite, clave, fuenteDelLimite) = (valor, claveDeLimite, leido.Value.Fuente);
        }
        return Result.Success(new TopesDeAportes(tope, fuenteDelTope, fraccion, fuenteDeLaFraccion, limite, clave, fuenteDelLimite));
    }

    public async Task<int> AnticipacionDelAvisoAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CaptacionesAnticipacionAvisoCambioDeTasaDias, fecha, ct: ct);
        if (leido.IsFailure) return 0;
        return leido.Value.Valor switch
        {
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0,
        };
    }

    /// <summary>Un parámetro decimal con su fuente: la del límite legal si rigió, o la de la cooperativa.</summary>
    private async Task<Result<(decimal? Valor, string? Fuente)>> LeerAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        if (leido.IsFailure) return Result.Failure<(decimal?, string?)>(leido.Error);
        var valor = leido.Value.Valor switch
        {
            decimal d => d,
            _ => decimal.TryParse(leido.Value.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : (decimal?)null,
        };
        var fuente = leido.Value.Limites.FirstOrDefault(l => l.Rige)?.Fuente
                     ?? leido.Value.Limites.FirstOrDefault()?.Fuente
                     ?? $"parámetro {clave} de la cooperativa";
        return Result.Success<(decimal?, string?)>((valor, fuente));
    }
}

/// <summary>La entrega que trae cada naturaleza (aportes con K7, captaciones con K8) y el error si el despliegue no la trae. (nuevo)</summary>
public static class EntregaDeLineas
{
    public static Error? NoDisponible(LineNature naturaleza, EntregaDeCartera desplegada)
    {
        var necesaria = RequisitosDeActivacionDeCaptaciones.EntregaQueLaTrae(naturaleza);
        if (desplegada >= necesaria) return null;
        var que = naturaleza == LineNature.Contribution ? "Las líneas de aportes" : "Las líneas de captación";
        return new ErrorConDatos(RequisitosDeActivacionDeCaptaciones.CodigoEntregaNoDisponible,
            $"{que} se crean pero no se activan hasta la entrega {necesaria} de la Cartera, que todavía no está desplegada. Regístrela sin activar.",
            new { nature = naturaleza.ToString(), availableFrom = necesaria.ToString() });
    }
}
