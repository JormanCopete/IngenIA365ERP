using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Una emisión cuyo origen es una parte de hechos (<c>LND_AccountingFactParts</c>, T12, T14; mensajes.md §10.0, §10.4–§10.6): la parte (su
/// <c>PublicId</c>, que se fija antes de crearla porque la parte guarda el mensaje), su rótulo, la fecha contable, la sucursal contable del
/// sobre, el tercero (el deudor en los hechos por crédito; nulo en los masivos), el crédito si el mensaje es por crédito (clave
/// <c>Confirmation:{crédito:N}</c>; si no, <c>Confirmation:{parte:N}</c>), la fecha de la causación que agrupa el lote y a nombre de quién va
/// si actúa el proceso. (nuevo)
/// </summary>
public sealed record EmisionDeParte(
    Guid PartePublicId,
    string Rotulo,
    DateOnly FechaContable,
    Guid SucursalPublicId,
    Guid? Tercero,
    Guid? CreditoPublicId,
    DateOnly? FechaDeLaCausacion,
    UserRefV1? UsuarioDeOrigen);

public sealed partial class EmisionDeCartera
{
    /// <summary>El <c>BatchScopeKey</c> de las entregas por lotes de la causación de un día: <c>AccrualRun:{aaaa-mm-dd}</c> (T56).</summary>
    public static string AlcanceDeLoteDeCausacion(DateOnly fecha) => ModoDePasoDeCartera.AlcanceDeCausacion(fecha);

    /// <summary>
    /// Un hecho de una parte (feature 013, US4, T0495): el masivo de la causación o de la corrección (<c>InteresCausado</c>, <c>MoraCausada</c>,
    /// <c>SeguroCausado</c>, <c>AjusteDeCausacionPorFechaValor</c>) o uno por crédito que emite el proceso (<c>CausacionSuspendida</c>,
    /// <c>CausacionReanudada</c>, <c>DimensionContableCambiada</c>, <c>SaldoAFavorReconocido</c>), con origen la parte (<c>Operation</c>), su
    /// clave, el modo de su tipo —una entrega por lotes queda con <c>BatchScopeKey</c> de la causación de su día— y, si actúa el proceso, el
    /// usuario de quien ordenó la corrida. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> HechoDeParteAsync(EmisionDeParte parte, string tipo, object contenido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parte);
        ArgumentNullException.ThrowIfNull(contenido);
        var modo = await modoDePaso.ResolverAsync(tipo, parte.FechaContable, emitidoPorElProceso: true, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var sellado = modo.Value;
        // Las partes de la causación de d van en el lote de d (C09); un hecho con otro disparador (el cierre) conserva su alcance.
        if (sellado is ModoDeEntrega.Sellado { Modo: DeliveryMode.Batch } lote && parte.FechaDeLaCausacion is { } dia
            && ClavesDeLote.Leer(lote.ScheduleKey)?.Disparador == ClavesDeLote.FinDeCausacion)
            sellado = lote with { BatchScopeKey = AlcanceDeLoteDeCausacion(dia) };

        var clave = ClavesDeEvento.ConfirmacionPor(parte.CreditoPublicId ?? parte.PartePublicId);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, parte.PartePublicId, null, null, parte.Rotulo, parte.FechaContable, parte.SucursalPublicId,
                PersonPublicId: parte.Tercero),
            clave, [contenido], sellado, UsuarioDeOrigen: parte.UsuarioDeOrigen), ct);
        return Result.Success(mensajes[0]);
    }
}
