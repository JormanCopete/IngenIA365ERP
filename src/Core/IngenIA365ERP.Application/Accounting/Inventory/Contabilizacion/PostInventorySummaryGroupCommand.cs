using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
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
/// Contabiliza un <b>grupo resumido</b> de un lote (feature 012, T513; contracts/contabilidad.md §5.3; FR-077): un comprobante
/// por fecha de operación, tipo de documento, tipo de comprobante, sucursal y centro. <b>Sin ruta ni permisos</b>; lo envía el
/// despachador por <c>DestinoContabilidad</c>.
///
/// <list type="number">
///   <item>arma el <see cref="PostingRequest"/> de cada documento como si fuera por documento y los valida todos con
///         <see cref="AccountingPoster.ValidarVariosAsync"/>;</item>
///   <item>excluye los que fallan (<c>Rejected</c>, con sus líneas del documento) y a sus relacionados del mismo grupo, que
///         esperan (<c>Retry</c>): un documento malo no tumba el resumen;</item>
///   <item>suma las líneas válidas por cuenta, lado, sucursal y centro, débitos y créditos por separado, sin netear, salvo las
///         que conservan tercero, cruce o base gravable;</item>
///   <item>un <see cref="AccountingPoster.PrepareAsync"/> fechado en la fecha del grupo, con origen
///         <c>InventoryPostingBatch</c> y <c>RegistradoPor</c> nulo, una fila de recibo por mensaje con el mismo comprobante, y un
///         <c>SaveChanges</c>.</item>
/// </list>
/// Devuelve un <see cref="ResultadoDeUnidad"/> por unidad, en el orden recibido. (nuevo)
/// </summary>
public sealed record PostInventorySummaryGroupCommand(Guid BatchPublicId, string GroupKey, IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<IReadOnlyList<ResultadoDeUnidad>>>, IReintentableAnteConcurrencia;

public sealed class PostInventorySummaryGroupCommandValidator : AbstractValidator<PostInventorySummaryGroupCommand>
{
    public PostInventorySummaryGroupCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.GroupKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("El grupo no trae mensajes.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PostInventorySummaryGroupCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeInventario consumo,
    AccountingPoster poster,
    ResolutorDeReglas resolutor,
    TiposDeComprobanteDeInventario tipos,
    IDateTimeService reloj) : IRequestHandler<PostInventorySummaryGroupCommand, Result<IReadOnlyList<ResultadoDeUnidad>>>
{
    public async Task<Result<IReadOnlyList<ResultadoDeUnidad>>> Handle(PostInventorySummaryGroupCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<ResultadoDeUnidad>>(leidos.Error);
        var unidades = AgrupadorDeResumidos.Unidades(leidos.Value, IntegrationDestinations.Accounting);
        var resultados = new Dictionary<UnidadDeConsumo, ResultadoDeConsumo>();

        // Pasos 2 a 5 por unidad.
        var vivas = new List<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)>();
        foreach (var (unidad, mensajes) in unidades)
        {
            var alto = await consumo.RevisarAsync(mensajes, ct);
            if (alto is not null)
            {
                resultados[unidad] = alto;
            }
            else if (ConstructorDeLineasDeInventario.EsInformativa(mensajes.Select(MensajeDeUnidad.De).ToList()))
            {
                // 6. informativo: su recibo, sin comprobante (§3.9).
                await consumo.AgregarRecibosAsync(mensajes, null, InventoryPosting.SinComprobanteInformativo, request.BatchPublicId,
                    mensajes[0].Envelope.Origin.OperationDate, ct);
                resultados[unidad] = new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational);
            }
            else
            {
                vivas.Add((unidad, mensajes));
            }
        }

        // 1. cada documento como si fuera por documento, validados todos juntos.
        var comoMensajes = vivas.Select(v => (IReadOnlyList<MensajeDeUnidad>)v.Mensajes.Select(MensajeDeUnidad.De).ToList()).ToList();
        var catalogos = await CatalogosDelConstructor.CargarAsync(db, resolutor, tipos, comoMensajes, reloj.TodayUtc, ct);
        var construidas = vivas.Select((v, i) => (v.Unidad, v.Mensajes, Construccion: ConstructorDeLineasDeInventario.Construir(comoMensajes[i], catalogos))).ToList();

        var excluidos = new HashSet<Guid>();
        foreach (var (unidad, mensajes, c) in construidas.Where(x => !x.Construccion.EsValida))
        {
            resultados[unidad] = ConsumoDeInventario.RechazoDe(c);
            excluidos.Add(unidad.OriginPublicId);
        }
        var conLineas = construidas.Where(x => x.Construccion.EsValida && !x.Construccion.ValorCero).ToList();
        var validaciones = await poster.ValidarVariosAsync(conLineas.Select(x => x.Construccion.Request!).ToList(), ct);
        for (var i = 0; i < conLineas.Count; i++)
        {
            if (validaciones[i].EsValido) continue;
            resultados[conLineas[i].Unidad] = ConsumoDeInventario.RechazoDe(validaciones[i], conLineas[i].Construccion.Mapa);
            excluidos.Add(conLineas[i].Unidad.OriginPublicId);
        }

        // 2. los relacionados de un excluido, transitivamente, esperan: no se contabiliza una corrección sin su original.
        bool cambio;
        do
        {
            cambio = false;
            foreach (var (unidad, mensajes, _) in construidas)
            {
                if (resultados.ContainsKey(unidad) || !ConsumoDeInventario.Originales(mensajes).Any(excluidos.Contains)) continue;
                var espera = ConsumoDeInventario.Originales(mensajes).First(excluidos.Contains);
                var error = AccountingErrors.InventoryMessageWaitingForOriginal(espera);
                resultados[unidad] = new ResultadoDeConsumo.Retry(error.Message, error.Code);
                excluidos.Add(unidad.OriginPublicId);
                cambio = true;
            }
        } while (cambio);

        foreach (var (unidad, mensajes, _) in construidas.Where(x => resultados.GetValueOrDefault(x.Unidad) is ResultadoDeConsumo.Rejected))
            await consumo.AuditarRechazoAsync(mensajes, (ResultadoDeConsumo.Rejected)resultados[unidad], ct);

        // 8. valor cero: recibo sin comprobante.
        foreach (var (unidad, mensajes, c) in construidas.Where(x => !resultados.ContainsKey(x.Unidad) && x.Construccion.ValorCero).ToList())
        {
            await consumo.AgregarRecibosAsync(mensajes, null, InventoryPosting.SinComprobanteValorCero, request.BatchPublicId, c.Fecha, ct);
            resultados[unidad] = new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.ZeroValue);
        }

        // 3 a 7. el resumen de los válidos.
        var incluidas = conLineas.Where(x => !resultados.ContainsKey(x.Unidad)).ToList();
        AccountingDocument? comprobante = null;
        if (incluidas.Count > 0)
        {
            var construcciones = incluidas.Select(x => x.Construccion).ToList();
            var fecha = construcciones[0].Fecha;
            var lote = await db.IntegrationBatches.AsNoTracking().Where(b => b.PublicId == request.BatchPublicId).Select(b => (long?)b.Number).FirstOrDefaultAsync(ct);
            var sucursal = await db.Branches.AsNoTracking().Where(b => b.PublicId == construcciones[0].Principal.Sobre.BranchPublicId).Select(b => b.Name).FirstOrDefaultAsync(ct);
            var request2 = Resumen(construcciones, request.BatchPublicId, lote, sucursal, fecha);
            var preparado = await poster.PrepareAsync(request2, ct);
            if (preparado.IsFailure)
            {
                db.DescartarCambios();
                var rechazo = ConsumoDeInventario.Rechazo(preparado.Error);
                foreach (var x in incluidas)
                {
                    resultados[x.Unidad] = rechazo;
                    await consumo.AuditarRechazoAsync(x.Mensajes, rechazo, ct);
                }
            }
            else
            {
                comprobante = preparado.Value;
                foreach (var x in incluidas) await consumo.AgregarRecibosAsync(x.Mensajes, comprobante, null, request.BatchPublicId, x.Construccion.Fecha, ct);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ConsumoDeInventario.EsColisionDelRecibo(ex))
        {
            // Otra réplica procesó alguno: nada de este grupo quedó guardado; la siguiente pasada ve lo ya procesado.
            db.DescartarCambios();
            return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades
                .Select(u => new ResultadoDeUnidad(u.Unidad, new ResultadoDeConsumo.Retry("Otra réplica procesó parte del grupo.")))
                .ToList());
        }

        if (comprobante is not null)
        {
            await consumo.AuditarContabilizacionAsync(comprobante, incluidas.SelectMany(x => x.Mensajes).ToList(), request.BatchPublicId, ct);
            var procesado = new ResultadoDeConsumo.Processed(comprobante.PublicId, comprobante.VoucherType?.Code, comprobante.Number?.ToString(CultureInfo.InvariantCulture));
            foreach (var x in incluidas) resultados[x.Unidad] = procesado;
        }

        return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades.Select(u => new ResultadoDeUnidad(u.Unidad, resultados[u.Unidad])).ToList());
    }

    /// <summary>
    /// El comprobante resumido: suma por cuenta, lado, sucursal y centro (débitos y créditos por separado, sin netear) salvo las
    /// líneas que conservan tercero, cruce o base, que van con su detalle (§5.3).
    /// </summary>
    public static PostingRequest Resumen(IReadOnlyList<ConstruccionDeUnidad> construcciones, Guid lote, long? numeroDeLote, string? sucursal, DateOnly fecha)
    {
        var lineas = new List<PostingLine>();
        var indice = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in construcciones)
        {
            for (var i = 0; i < c.Request!.Lines.Count; i++)
            {
                var linea = c.Request.Lines[i];
                if (c.Mapa[i].ConservaDetalle)
                {
                    lineas.Add(linea);
                    continue;
                }
                var clave = string.Join('|', linea.AccountCode, linea.Debit > 0m ? "D" : "C", linea.BranchId, linea.CostCenterId);
                if (indice.TryGetValue(clave, out var j))
                {
                    lineas[j] = lineas[j] with { Debit = lineas[j].Debit + linea.Debit, Credit = lineas[j].Credit + linea.Credit };
                    continue;
                }
                indice[clave] = lineas.Count;
                lineas.Add(linea with { PersonId = null, CrossDocumentType = null, CrossDocumentNumber = null, Detail = $"Lote {numeroDeLote} · {c.Mapa[i].AccountName}" });
            }
        }
        var primero = construcciones[0];
        var tipoDeDocumento = primero.Principal.Sobre.Origin.DocumentTypeCode ?? primero.Tipo!.VoucherTypeCode;
        var descripcion = $"Lote {numeroDeLote} · {tipoDeDocumento} · {sucursal} · {construcciones.Count} documentos";
        return new PostingRequest(primero.Tipo!.VoucherTypeCode, fecha, descripcion,
            new AccountingOrigin(ModuloContable.Inventario, OrigenesDeInventario.LoteResumido, lote), lineas);
    }
}
