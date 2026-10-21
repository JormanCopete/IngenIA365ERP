using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration;

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase de documento con que se nombra una parte en el <c>related</c> de su reverso (mensajes.md §10.18).</summary>
    public const string ClaseDeParte = nameof(AccountingFactPart);

    /// <summary>La clase del cierre en el <c>related</c> del informativo de la reapertura.</summary>
    public const string ClaseDeCierre = nameof(MonthlyClose);

    /// <summary>El número de los informativos del cierre: el mes, <c>aaaa-mm</c> (mensajes.md §10.23).</summary>
    public static string NumeroDelCierre(MonthlyClose cierre) =>
        string.Create(CultureInfo.InvariantCulture, $"{cierre.PeriodYear:0000}-{cierre.PeriodMonth:00}");

    /// <summary>
    /// Un hecho de una parte del cierre (feature 013, US9, T0705; mensajes.md §10.17; T47, T48): <c>CarteraReclasificadaPorCalificacion</c> o
    /// <c>DeterioroCalculado</c>, origen la parte, clave <c>Close:{versión}</c>, sin tercero, fechado el último día del mes —que el cierre ya
    /// comprobó abierto: no pasa por <see cref="FechaContable"/>, que lo saltaría por el cierre mismo— y con el modo de su tipo, que por defecto es
    /// por lotes con disparador <c>CierreDeCartera</c> y alcance <c>LendingClose:{aaaa-mm}</c>. A nombre de quien ordenó. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> HechoDelCierreAsync(EmisionDeParte parte, int version, string tipo, object contenido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parte);
        ArgumentNullException.ThrowIfNull(contenido);
        var modo = await modoDePaso.ResolverAsync(tipo, parte.FechaContable, emitidoPorElProceso: true, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, parte.PartePublicId, null, null, parte.Rotulo, parte.FechaContable, parte.SucursalPublicId),
            ClavesDeEvento.Cierre(version), [contenido], modo.Value, UsuarioDeOrigen: parte.UsuarioDeOrigen), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// Un <c>ReversoDeCierre</c> (feature 013, US9, T0706; mensajes.md §10.18; C08, C58): origen la parte de la reapertura, clave
    /// <c>Reopen:{versión}</c>, <c>related</c> la parte del cierre que revierte, la misma fecha contable y el modo <b>heredado</b> de esa parte, con
    /// el alcance de lote del mes. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> ReversoDeCierreAsync(EmisionDeParte parte, int version, ReversoDeCierreV1 contenido, AccountingFactPart original,
        string rotuloDelOriginal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parte);
        ArgumentNullException.ThrowIfNull(original);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, parte.PartePublicId, null, null, parte.Rotulo, parte.FechaContable, parte.SucursalPublicId),
            ClavesDeEvento.Reapertura(version), [contenido],
            new ModoDeEntrega.Heredado(original.PublicId, ModoDePasoDeCartera.AlcanceDeCierre(parte.FechaContable)),
            Relacionado: new DocumentoRelacionado(original.PublicId, ClaseDeParte, rotuloDelOriginal),
            UsuarioDeOrigen: parte.UsuarioDeOrigen), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// <c>CierreDeCarteraRealizado</c> (feature 013, US9, T0705; mensajes.md §10.23): origen el cierre, número <c>aaaa-mm</c>, clave
    /// <c>Close:{versión}</c>, fechado el último día del mes, sucursal principal, siempre entregado. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CierreRealizadoAsync(MonthlyClose cierre, CierreDeCarteraRealizadoV1 contenido, UserRefV1? usuario,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        var sucursal = await SucursalPrincipalAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, cierre.PublicId, null, null, NumeroDelCierre(cierre), cierre.CloseDate, sucursal),
            ClavesDeEvento.Cierre(cierre.Version), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always), UsuarioDeOrigen: usuario), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// <c>CierreDeCarteraReabierto</c> (feature 013, US9, T0706; mensajes.md §10.23): origen el cierre que se reabre, número <c>aaaa-mm</c>, clave
    /// <c>Reopen:{versión}</c>, <c>related</c> el mismo cierre (así queda nombrado el <c>CierreDeCarteraRealizado</c> que deja sin efecto), fechado el
    /// último día del mes, siempre entregado. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CierreReabiertoAsync(MonthlyClose cierre, CierreDeCarteraReabiertoV1 contenido, UserRefV1? usuario,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cierre);
        var sucursal = await SucursalPrincipalAsync(ct);
        var numero = NumeroDelCierre(cierre);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, cierre.PublicId, null, null, numero, cierre.CloseDate, sucursal),
            ClavesDeEvento.Reapertura(cierre.Version), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(cierre.PublicId, ClaseDeCierre, numero), UsuarioDeOrigen: usuario), ct);
        return Result.Success(mensajes[0]);
    }
}
