using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El modo de paso a Contabilidad que sella cada hecho de Cartera al emitirse (feature 013, T0228, C106; T56, FR-128; contracts/
/// contabilidad.md §10.1). Esqueleto con firma estable:
/// <list type="bullet">
/// <item>los informativos y los mensajes a otros destinos (Comercial, Tesorería, Recaudo) se entregan siempre: <c>Always</c>;</item>
/// <item>un hecho de negocio a Contabilidad toma el defecto de su tipo (<c>LND_FactTypes.DefaultMode</c>, D8): <c>EnLinea</c>,
/// <c>PorLotes</c> (con la clave <c>CAR.{ShortCode}|{disparador}||PorDia</c>) o <c>NoPasa</c>;</item>
/// <item>con la contabilidad sin iniciar (<see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>), <c>NoPasa</c>.</item>
/// </list>
/// US7 suma las vigencias de <c>Contabilidad.ModoDePaso</c> por tipo de hecho y generales (un modo guardado a propósito se respeta), la
/// granularidad y el disparador parametrizados, la herencia de los reversos y el modo del principal en los acompañantes. (nuevo)
/// </summary>
public sealed class ModoDePasoDeCartera(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
{
    public const string EnLinea = "EnLinea";
    public const string PorLotes = "PorLotes";
    public const string NoPasa = "NoPasa";

    /// <summary>La granularidad por defecto de un lote de Cartera (contabilidad.md §10.1).</summary>
    public const string GranularidadPorDefecto = "PorDia";

    /// <summary>El disparador por defecto si el tipo de hecho no trae el suyo.</summary>
    public const string DisparadorPorDefecto = "FinDeCausacion";

    /// <summary>
    /// El modo con que nace la entrega del hecho <paramref name="tipoDeHecho"/> (el <c>Type</c> del mensaje) con fecha contable
    /// <paramref name="fechaContable"/>. Un tipo fuera del catálogo de Cartera es un defecto del emisor (excepción).
    /// </summary>
    public Task<Result<ModoDeEntrega>> ResolverAsync(string tipoDeHecho, DateOnly fechaContable, CancellationToken ct) =>
        ResolverAsync(tipoDeHecho, fechaContable, emitidoPorElProceso: false, ct);

    /// <summary>
    /// Como el anterior, sabiendo quién lo emite (feature 013, US4, T0497; T56): <c>DimensionContableCambiada</c> sin vigencia propia sale
    /// <c>EnLinea</c> si la emite una operación y <c>PorLotes</c> (con su disparador) si la detecta el ciclo diario
    /// (<c>ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite</c>).
    /// </summary>
    public async Task<Result<ModoDeEntrega>> ResolverAsync(string tipoDeHecho, DateOnly fechaContable, bool emitidoPorElProceso, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeHecho);
        var tipo = CatalogoDeMensajes.DelModulo(ModulosDeOrigen.Cartera).FirstOrDefault(t => t.Type == tipoDeHecho)
            ?? throw new ArgumentException($"«{tipoDeHecho}» no es un mensaje de Cartera (CatalogoDeMensajesDeCarteraV1).", nameof(tipoDeHecho));

        if (tipo.Kind == IntegrationMessageKind.Informational || !tipo.AdmiteDestino(IntegrationDestinations.Accounting))
            return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.Always));

        var estado = await contabilidad.EstadoDelPeriodoAsync(fechaContable, ct);
        if (estado.IsFailure) return Result.Failure<ModoDeEntrega>(estado.Error);
        if (!estado.Value.AccountingInitialized) return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));

        var defecto = await db.LendingFactTypes.AsNoTracking()
            .Where(f => f.Code == tipoDeHecho && !f.IsDeleted)
            .Select(f => new { f.DefaultMode, f.DefaultBatchTrigger, f.ShortCode })
            .FirstOrDefaultAsync(ct);

        var modo = defecto?.DefaultMode ?? EnLinea;
        if (emitidoPorElProceso && modo == EnLinea && tipoDeHecho == IngenIA365ERP.Application.Common.Integration.Contracts.Lending.DimensionContableCambiadaV1.Type)
            modo = PorLotes;
        var disparador = defecto?.DefaultBatchTrigger is { Length: > 0 } propio ? propio : DisparadorPorDefecto;
        var codigoCorto = defecto?.ShortCode is { Length: > 0 } corto ? corto : tipo.CodigoCorto ?? tipoDeHecho;
        return Result.Success<ModoDeEntrega>(modo switch
        {
            NoPasa => new ModoDeEntrega.Sellado(DeliveryMode.NotPosted),
            PorLotes => new ModoDeEntrega.Sellado(DeliveryMode.Batch,
                ClavesDeLote.Horario(ClavesDeLote.PrefijoDeCartera + codigoCorto, disparador, null, GranularidadPorDefecto)),
            _ => new ModoDeEntrega.Sellado(DeliveryMode.Online),
        });
    }
}
