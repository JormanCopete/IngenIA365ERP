using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// Contabiliza una <b>unidad</b> de mensajes de Inventario (feature 012, T512; contracts/contabilidad.md §3.1; FR-074, FR-077,
/// FR-083). <b>Sin ruta ni permisos</b>: lo envía sólo el despachador, en un ámbito DI propio por unidad y con el actor del
/// proceso o de la persona que ordenó el lote (<c>LosComandosDeConsumoNoTienenRuta</c>). Los once pasos van <b>dentro</b> del
/// handler porque el reintento por concurrencia descarta el contexto entero y vuelve a empezar.
///
/// <para>
/// Un solo camino al libro: el comprobante lo prepara <see cref="AccountingPoster.PrepareAsync"/> y se guarda con los recibos
/// en <b>un</b> <c>SaveChangesAsync</c>. El consumidor no toca tablas de la plataforma: devuelve un
/// <see cref="ResultadoDeConsumo"/> y el despachador lo registra con <c>RegisterDeliveryResultCommand</c>. (nuevo)
/// </para>
/// </summary>
public sealed record PostInventoryMessagesCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class PostInventoryMessagesCommandValidator : AbstractValidator<PostInventoryMessagesCommand>
{
    public PostInventoryMessagesCommandValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("La unidad no trae mensajes.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty).When(x => x.BatchPublicId is not null);
    }
}

public sealed class PostInventoryMessagesCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeInventario consumo,
    AccountingPoster poster,
    ResolutorDeReglas resolutor,
    TiposDeComprobanteDeInventario tipos,
    IDateTimeService reloj) : IRequestHandler<PostInventoryMessagesCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(PostInventoryMessagesCommand request, CancellationToken ct)
    {
        // 1. lee la unidad
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Success<ResultadoDeConsumo>(ConsumoDeInventario.Rechazo(leidos.Error));
        var unidad = leidos.Value;
        if (unidad.Select(m => (m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey)).Distinct().Count() != 1)
            throw new InvalidOperationException("Los mensajes pedidos no forman una unidad (mismo origen y mismo evento): es un defecto del despachador.");

        // 2 a 5. versión, moneda, recibo existente, original pendiente
        var alto = await consumo.RevisarAsync(unidad, ct);
        if (alto is not null)
        {
            if (alto is ResultadoDeConsumo.Rejected r) await consumo.AuditarRechazoAsync(unidad, r, ct);
            return Result.Success(alto);
        }

        var mensajes = unidad.Select(MensajeDeUnidad.De).ToList();

        // 6. informativo: recibo sin comprobante (§3.9)
        if (ConstructorDeLineasDeInventario.EsInformativa(mensajes))
            return await SinComprobanteAsync(unidad, InventoryPosting.SinComprobanteInformativo, MotivoSinComprobante.Informational,
                mensajes[0].Sobre.Origin.OperationDate, request.BatchPublicId, ct);

        // 7. arma el comprobante con la matriz
        var catalogos = await CatalogosDelConstructor.CargarAsync(db, resolutor, tipos, [mensajes], reloj.TodayUtc, ct);
        var construccion = ConstructorDeLineasDeInventario.Construir(mensajes, catalogos);
        if (!construccion.EsValida)
        {
            var rechazo = ConsumoDeInventario.RechazoDe(construccion);
            await consumo.AuditarRechazoAsync(unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }

        // 8. sin líneas distintas de cero
        if (construccion.ValorCero)
            return await SinComprobanteAsync(unidad, InventoryPosting.SinComprobanteValorCero, MotivoSinComprobante.ZeroValue,
                construccion.Fecha, request.BatchPublicId, ct);

        // 9. el contrato de la 009: valida, numera y agrega sin guardar
        var preparado = await poster.PrepareAsync(construccion.Request!, ct);
        if (preparado.IsFailure)
        {
            var validacion = await poster.ValidarAsync(construccion.Request!, ct);
            var rechazo = validacion.Errores.Count == 0
                ? ConsumoDeInventario.Rechazo(preparado.Error)
                : ConsumoDeInventario.RechazoDe(validacion, construccion.Mapa, preparado.Error);
            db.DescartarCambios();
            await consumo.AuditarRechazoAsync(unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }
        var comprobante = preparado.Value;

        // 10 y 11. recibos y un solo guardado; la colisión del recibo es «ya procesado»
        await consumo.AgregarRecibosAsync(unidad, comprobante, null, request.BatchPublicId, construccion.Fecha, ct);
        if (!await GuardarAsync(ct))
            return Result.Success(await consumo.YaProcesadoAsync(request.MessagePublicIds, ct) ?? new ResultadoDeConsumo.Retry("Otra réplica procesó la unidad."));

        await consumo.AuditarContabilizacionAsync(comprobante, unidad, request.BatchPublicId, ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(
            comprobante.PublicId, comprobante.VoucherType?.Code, comprobante.Number?.ToString(CultureInfo.InvariantCulture)));
    }

    private async Task<Result<ResultadoDeConsumo>> SinComprobanteAsync(
        IReadOnlyList<MensajeEntrante> unidad, string motivo, MotivoSinComprobante sinComprobante, DateOnly fecha, Guid? lote, CancellationToken ct)
    {
        await consumo.AgregarRecibosAsync(unidad, null, motivo, lote, fecha, ct);
        if (!await GuardarAsync(ct))
            return Result.Success(await consumo.YaProcesadoAsync(unidad.Select(m => m.MessageId).ToList(), ct) ?? new ResultadoDeConsumo.Retry("Otra réplica procesó la unidad."));
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, sinComprobante));
    }

    /// <summary>Guarda; <c>false</c> si chocó contra el recibo único (otra réplica ganó) y descarta lo pendiente.</summary>
    private async Task<bool> GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ConsumoDeInventario.EsColisionDelRecibo(ex))
        {
            db.DescartarCambios();
            return false;
        }
    }
}
