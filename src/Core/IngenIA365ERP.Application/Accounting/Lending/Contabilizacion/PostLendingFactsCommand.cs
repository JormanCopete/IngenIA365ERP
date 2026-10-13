using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

/// <summary>
/// Contabiliza una <b>unidad</b> de hechos de Cartera (feature 013, T0647; contracts/contabilidad.md §7.1 y §7.2; molde
/// <c>PostInventoryMessagesCommand</c>): una operación y su modo (C01) → un comprobante del tipo del hecho principal. <b>Sin ruta ni permisos</b>:
/// lo envía sólo el despachador, por <c>DestinoContabilidad</c>, en un ámbito DI propio por unidad y con el actor del proceso o de la persona
/// que ordenó el lote (<c>LosComandosDeConsumoNoTienenRuta</c>). Los once pasos van <b>dentro</b> del handler porque el reintento por
/// concurrencia descarta el contexto entero y vuelve a empezar.
///
/// <para>
/// Un solo camino al libro: el comprobante lo prepara <see cref="AccountingPoster.PrepareAsync"/> y se guarda con los recibos de
/// <c>ACC_LendingPostings</c> en <b>un</b> <c>SaveChangesAsync</c>. El consumidor no toca tablas de la plataforma: devuelve un
/// <see cref="ResultadoDeConsumo"/> y el despachador lo registra con <c>RegisterDeliveryResultCommand</c>. (nuevo)
/// </para>
/// </summary>
public sealed record PostLendingFactsCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class PostLendingFactsCommandValidator : AbstractValidator<PostLendingFactsCommand>
{
    public PostLendingFactsCommandValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("La unidad no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty).When(x => x.BatchPublicId is not null);
    }
}

public sealed class PostLendingFactsCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeCartera consumo,
    AccountingPoster poster,
    ResolutorDeReglasDeCartera resolutor,
    TiposDeComprobanteDeCartera tipos,
    IDateTimeService reloj) : IRequestHandler<PostLendingFactsCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(PostLendingFactsCommand request, CancellationToken ct)
    {
        // 1. lee la unidad
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Success<ResultadoDeConsumo>(ConsumoDeCartera.Rechazo(leidos.Error));
        var unidad = leidos.Value;
        if (unidad.Select(m => (m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey)).Distinct().Count() != 1)
            throw new InvalidOperationException("Los hechos pedidos no forman una unidad (mismo origen y mismo evento): es un defecto del despachador.");

        // 2 a 5. versión, moneda, recibo existente, original pendiente
        var alto = await consumo.RevisarAsync(unidad, ct);
        if (alto is not null)
        {
            if (alto is ResultadoDeConsumo.Rejected r) await consumo.AuditarRechazoAsync(unidad, r, ct);
            return Result.Success(alto);
        }

        var hechos = unidad.Select(HechoDeCartera.De).ToList();

        // 6. informativo: recibo sin comprobante (§7.4)
        if (ConstructorDeLineasDeCartera.EsInformativa(hechos))
            return await SinComprobanteAsync(unidad, LendingPosting.SinComprobanteInformativo, MotivoSinComprobante.Informational, request.BatchPublicId, ct);

        // 7. arma el comprobante con la matriz de la fecha contable (también en un reverso, §9)
        var catalogos = await CatalogosDeCartera.CargarAsync(db, resolutor, tipos, [hechos], reloj.TodayUtc, ct);
        var construccion = ConstructorDeLineasDeCartera.Construir(hechos, catalogos);
        if (!construccion.EsValida)
        {
            var rechazo = ConsumoDeCartera.RechazoDe(construccion);
            await consumo.AuditarRechazoAsync(unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }

        // 8. sin líneas: valor cero o las dos puntas en las mismas cuentas (C21)
        if (construccion.SinLineas)
        {
            var motivo = construccion.SinComprobante == LendingPosting.SinComprobanteMismasCuentas ? MotivoSinComprobante.SameAccounts : MotivoSinComprobante.ZeroValue;
            return await SinComprobanteAsync(unidad, construccion.SinComprobante!, motivo, request.BatchPublicId, ct);
        }

        // 9. el contrato de la 009: valida, numera y agrega sin guardar
        var preparado = await poster.PrepareAsync(construccion.Request!, ct);
        if (preparado.IsFailure)
        {
            db.DescartarCambios();
            if (await consumo.PeriodoInexistenteAsync(preparado.Error, construccion.Fecha, ct) is { } reintento) return Result.Success<ResultadoDeConsumo>(reintento);
            var validacion = await poster.ValidarAsync(construccion.Request!, ct);
            var rechazo = validacion.Errores.Count == 0
                ? ConsumoDeCartera.Rechazo(preparado.Error)
                : ConsumoDeCartera.RechazoDe(validacion, construccion.Mapa, preparado.Error);
            await consumo.AuditarRechazoAsync(unidad, rechazo, ct);
            return Result.Success<ResultadoDeConsumo>(rechazo);
        }
        var comprobante = preparado.Value;

        // 10 y 11. un recibo por hecho y un solo guardado; la colisión del recibo es «ya procesado»
        await consumo.AgregarRecibosAsync(unidad, comprobante, null, request.BatchPublicId, ct);
        if (!await GuardarAsync(ct))
            return Result.Success(await consumo.YaProcesadoAsync(request.MessagePublicIds, ct) ?? new ResultadoDeConsumo.Retry("Otra réplica procesó la unidad."));

        await consumo.AuditarContabilizacionAsync(comprobante, unidad, request.BatchPublicId, ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(
            comprobante.PublicId, comprobante.VoucherType?.Code, comprobante.Number?.ToString(CultureInfo.InvariantCulture)));
    }

    private async Task<Result<ResultadoDeConsumo>> SinComprobanteAsync(
        IReadOnlyList<MensajeEntrante> unidad, string motivo, MotivoSinComprobante sinComprobante, Guid? lote, CancellationToken ct)
    {
        await consumo.AgregarRecibosAsync(unidad, null, motivo, lote, ct);
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
        catch (DbUpdateException ex) when (ConsumoDeCartera.EsColisionDelRecibo(ex))
        {
            db.DescartarCambios();
            return false;
        }
    }
}
