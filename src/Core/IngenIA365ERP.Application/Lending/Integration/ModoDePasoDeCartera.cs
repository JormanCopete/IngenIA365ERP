using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El modo de paso a Contabilidad que sella cada hecho de Cartera al emitirse (feature 013, T0228, T0653, C106; T56, FR-128; contracts/
/// contabilidad.md §10.1 y §10.2; DT §2.25). La firma de la fase 3 no cambia; US7 completa la resolución:
/// <list type="number">
/// <item>los informativos y los mensajes a otros destinos (Comercial, Tesorería, Recaudo) se entregan siempre: <c>Always</c>;</item>
/// <item>la vigencia de <c>Contabilidad.ModoDePaso</c> del tipo de hecho (ámbito <c>LendingFactType</c> sobre <c>LND_FactTypes</c>); si no hay,
/// la vigencia general; <b>un modo guardado a propósito se respeta</b> aunque la contabilidad no esté iniciada (el hecho queda en la bandeja con
/// <c>Accounting.NotInitialized</c>);</item>
/// <item>sin vigencia guardada: con la contabilidad sin iniciar (<see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>), <c>NoPasa</c>; si
/// no, el defecto del tipo (<c>LND_FactTypes.DefaultMode</c>, D8). <c>DimensionContableCambiada</c> sin vigencia propia sale <c>EnLinea</c> si
/// la emite una operación y <c>PorLotes</c> si la detecta el proceso (<c>ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite</c>);</item>
/// <item>por lotes: <c>ScheduleKey</c> <c>CAR.{ShortCode}|{disparador}|{hora}|{granularidad}</c> (C06, C07; 60 caracteres como máximo) con el
/// disparador, la granularidad y la hora de sus vigencias o del defecto —la hora sólo con el disparador <c>Hora</c>, sellado como
/// <see cref="ClavesDeLote.HoraDiaria"/>—, y <c>BatchScopeKey</c> (C09): <c>AccrualRun:{d+1}</c> para un hecho <c>FinDeCausacion</c> nacido
/// fuera de la corrida el día <c>d</c> (las partes de la causación de <c>d</c> lo reemplazan por <c>AccrualRun:{d}</c>,
/// <see cref="EmisionDeCartera.HechoDeParteAsync"/>) y <c>LendingClose:{aaaa-mm}</c> para el cierre.</item>
/// </list>
/// Los acompañantes (<see cref="AcompanantesPorPrincipal"/>) no tienen modo propio: viajan en la unidad de su principal y sellan su modo con su
/// propio código corto (<see cref="DelAcompanante"/>; el emisor reescribe el corto por contenido); <c>Contabilidad.ModoDePaso</c> en el ámbito
/// <c>LendingFactType</c> se rechaza para los que no tienen otra vía de emisión (<see cref="SinModoPropio"/>; C01). Los reversos
/// (<c>DesembolsoReversado</c>, <c>PagoReversado</c>, <c>ReversoDeCierre</c>) no pasan por aquí: heredan el modo sellado de su original con
/// <see cref="ModoDeEntrega.Heredado"/> (C08). (nuevo)
/// </summary>
public sealed class ModoDePasoDeCartera(
    IApplicationDbContext db,
    IContabilidadParaCartera contabilidad,
    ILectorDeParametros? parametros = null,
    IDateTimeService? reloj = null)
{
    public const string EnLinea = "EnLinea";
    public const string PorLotes = "PorLotes";
    public const string NoPasa = "NoPasa";

    /// <summary>La granularidad por defecto de un lote de Cartera (contabilidad.md §10.1).</summary>
    public const string GranularidadPorDefecto = ClavesDeLote.GranularidadPorDia;

    /// <summary>El disparador por defecto si el tipo de hecho no trae el suyo.</summary>
    public const string DisparadorPorDefecto = ClavesDeLote.FinDeCausacion;

    /// <summary>El valor del parámetro <c>Contabilidad.DisparadorDeLote</c> que pide una hora (se sella como <see cref="ClavesDeLote.HoraDiaria"/>).</summary>
    public const string DisparadorHora = "Hora";

    /// <summary>El largo máximo de una <c>ScheduleKey</c> (C06).</summary>
    public const int LargoMaximoDeClave = 60;

    /// <summary>El prefijo del <c>BatchScopeKey</c> de la causación (<c>AccrualRun:{aaaa-mm-dd}</c>, C09).</summary>
    public const string PrefijoDeCausacion = "AccrualRun:";

    /// <summary>El prefijo del <c>BatchScopeKey</c> del cierre de Cartera (<c>LendingClose:{aaaa-mm}</c>).</summary>
    public const string PrefijoDeCierre = "LendingClose:";

    /// <summary>La lista fija de acompañantes por principal (mensajes.md §7.2): viajan en su unidad y sellan su modo.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AcompanantesPorPrincipal { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        [PagoAplicadoV1.Type] = [AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type,
            SaldoAFavorReconocidoV1.Type, AjusteDeCausacionPorFechaValorV1.Type],
        [PagoReversadoV1.Type] = [AjusteDeCausacionPorFechaValorV1.Type],
        [DesembolsoReversadoV1.Type] = [AjusteDeCausacionPorFechaValorV1.Type],
        [CreditoAjustadoDesdeComercialV1.Type] = [SaldoAFavorReconocidoV1.Type],
    };

    /// <summary>
    /// Los acompañantes que sólo viajan como tales (C01): <c>Contabilidad.ModoDePaso</c> no se ofrece para ellos en el ámbito <c>LendingFactType</c>.
    /// <c>AjusteDeCausacionPorFechaValor</c> y <c>SaldoAFavorReconocido</c> tienen además su corrida (<c>NationalValueCorrection</c>), donde
    /// sí sellan su propio modo.
    /// </summary>
    public static IReadOnlySet<string> SinModoPropio { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type,
    };

    /// <summary>Los reversos que heredan el modo sellado de su original (C08): nunca resuelven el suyo.</summary>
    public static IReadOnlySet<string> Heredan { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        DesembolsoReversadoV1.Type, PagoReversadoV1.Type, ReversoDeCierreV1.Type,
    };

    /// <summary>El principal de un acompañante que sólo viaja como tal (el pago), o nulo.</summary>
    public static string? PrincipalDe(string tipoDeHecho) => SinModoPropio.Contains(tipoDeHecho) ? PagoAplicadoV1.Type : null;

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

        // Un acompañante que sólo viaja como tal sella el modo de su principal, con su propio código corto.
        if (PrincipalDe(tipoDeHecho) is { } principal)
        {
            var delPrincipal = await ResolverAsync(principal, fechaContable, emitidoPorElProceso, ct);
            return delPrincipal.IsFailure ? delPrincipal : Result.Success(DelAcompanante(delPrincipal.Value, tipo.CodigoCorto ?? tipoDeHecho));
        }

        var fila = await db.LendingFactTypes.AsNoTracking()
            .Where(f => f.Code == tipoDeHecho && !f.IsDeleted)
            .Select(f => new { f.Id, f.DefaultMode, f.DefaultBatchTrigger, f.ShortCode })
            .FirstOrDefaultAsync(ct);
        var (ambito, ambitoId) = fila is null ? (ParameterScopeKind.None, 0) : (ParameterScopeKind.LendingFactType, fila.Id);
        var lector = parametros ?? new LectorDeParametros(db);

        var modoLeido = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadModoDePaso, fechaContable, ambito, ambitoId, ct);
        if (modoLeido.IsFailure) return Result.Failure<ModoDeEntrega>(modoLeido.Error);

        string modo;
        var vigencia = modoLeido.Value.Vigencia;
        var esDimension = tipoDeHecho == DimensionContableCambiadaV1.Type;
        if (vigencia is not null && esDimension && vigencia.ScopeKind == ParameterScopeKind.None)
            modo = emitidoPorElProceso ? PorLotes : EnLinea; // sin vigencia propia: según quién la emite (la general no la alcanza)
        else if (vigencia is not null)
            modo = modoLeido.Value.Texto;
        else
        {
            var estado = await contabilidad.EstadoDelPeriodoAsync(fechaContable, ct);
            if (estado.IsFailure) return Result.Failure<ModoDeEntrega>(estado.Error);
            if (!estado.Value.AccountingInitialized) return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
            modo = fila?.DefaultMode is { Length: > 0 } propio ? propio : EnLinea;
            if (esDimension) modo = emitidoPorElProceso ? PorLotes : EnLinea;
        }

        if (modo == NoPasa) return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        if (modo != PorLotes) return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.Online));

        // Por lotes: disparador, granularidad y hora.
        var disparadorLeido = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadDisparadorDeLote, fechaContable, ambito, ambitoId, ct);
        if (disparadorLeido.IsFailure) return Result.Failure<ModoDeEntrega>(disparadorLeido.Error);
        var disparador = disparadorLeido.Value.Vigencia is not null ? disparadorLeido.Value.Texto
            : fila?.DefaultBatchTrigger is { Length: > 0 } delTipo ? delTipo
            : DisparadorPorDefecto;

        var granularidadLeida = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadGranularidad, fechaContable, ambito, ambitoId, ct);
        if (granularidadLeida.IsFailure) return Result.Failure<ModoDeEntrega>(granularidadLeida.Error);
        var granularidad = granularidadLeida.Value.Texto is { Length: > 0 } g ? g : GranularidadPorDefecto;

        TimeOnly? hora = null;
        if (disparador == DisparadorHora)
        {
            var horaLeida = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.ContabilidadHoraDeLote, fechaContable, ambito, ambitoId, ct);
            if (horaLeida.IsFailure) return Result.Failure<ModoDeEntrega>(horaLeida.Error);
            hora = horaLeida.Value.Valor switch
            {
                TimeOnly t => t,
                _ => TimeOnly.TryParseExact(horaLeida.Value.Texto, "HH':'mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : null,
            };
            disparador = ClavesDeLote.HoraDiaria;
        }

        var codigoCorto = fila?.ShortCode is { Length: > 0 } corto ? corto : tipo.CodigoCorto ?? tipoDeHecho;
        var clave = ClavesDeLote.Horario(ClavesDeLote.PrefijoDeCartera + codigoCorto, disparador, hora, granularidad);
        if (clave.Length > LargoMaximoDeClave)
            throw new InvalidOperationException($"La ScheduleKey «{clave}» pasa de {LargoMaximoDeClave} caracteres (C06).");

        var alcance = disparador switch
        {
            ClavesDeLote.FinDeCausacion => AlcanceDeCausacion((reloj?.HoyLocal ?? fechaContable).AddDays(1)),
            ClavesDeLote.CierreDeCartera => AlcanceDeCierre(fechaContable),
            _ => null,
        };
        return Result.Success<ModoDeEntrega>(new ModoDeEntrega.Sellado(DeliveryMode.Batch, clave, alcance));
    }

    /// <summary>
    /// El modo de un acompañante a partir del de su principal (C01): el mismo <c>Mode</c>, disparador, hora, granularidad y alcance, con el
    /// código corto del acompañante en la <c>ScheduleKey</c>. Un modo sin clave queda igual.
    /// </summary>
    public static ModoDeEntrega DelAcompanante(ModoDeEntrega principal, string codigoCorto)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal is ModoDeEntrega.Sellado { ScheduleKey: { } clave } sellado
            ? sellado with { ScheduleKey = ClavesDeLote.ConCodigoCortoDeCartera(clave, codigoCorto) }
            : principal;
    }

    /// <summary><c>AccrualRun:{aaaa-mm-dd}</c>: el alcance de lote de la causación del día <paramref name="dia"/> (C09).</summary>
    public static string AlcanceDeCausacion(DateOnly dia) => PrefijoDeCausacion + dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary><c>LendingClose:{aaaa-mm}</c>: el alcance de lote del cierre de Cartera del mes de <paramref name="fecha"/>.</summary>
    public static string AlcanceDeCierre(DateOnly fecha) => PrefijoDeCierre + fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
