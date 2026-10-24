using System.Text.Json;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Activation;

// Feature 015, T0183b (R-40; TR42; FR-147, FR-150, FR-159; contracts/api.md §14; mensajes.md §7): la activación mínima de Recaudo del bloque 2 —los requisitos
// básicos, sin cuadre—, que deja RCD_Setup activo, guarda RCD_Activations con IsMinimal y registra en el mismo guardado al recaudador REC y su destino Receipts. En
// producción no hay activación mínima: la completa (con el cuadre) la arma la fase 19 sobre este mismo comando.

/// <summary>Un requisito de la activación. (nuevo)</summary>
public sealed record RequisitoDeActivacionDto(string Code, bool Met, string Description);

/// <summary>Los requisitos de la activación de un módulo y si se puede activar. (nuevo)</summary>
public sealed record EstadoDeActivacionDto(string Status, DateOnly? ActivatedOn, bool IsMinimal, bool CanActivate, IReadOnlyList<RequisitoDeActivacionDto> Requirements);

/// <summary>Los requisitos básicos de Recaudo (activación mínima). (nuevo)</summary>
public sealed class RequisitosDeActivacionDeRecaudo(IApplicationDbContext db, IAmbienteDeDespliegue ambiente)
{
    public const string Punto = "CollectionPoint";
    public const string CajaConCajero = "CashBoxWithCashier";
    public const string Efectivo = "CashMeans";
    public const string Transferencia = "TransferMeans";
    public const string TiposDeDocumento = "DocumentTypes";
    public const string Cuadre = "OpeningSquare";

    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(DateOnly fecha, CancellationToken ct)
    {
        var puntos = (await db.CollectionPoints.AsNoTracking().ToListAsync(ct)).Where(p => p.VigenteEn(fecha)).Select(p => p.Id).ToList();
        var cajas = (await db.CashBoxes.AsNoTracking().Where(c => puntos.Contains(c.CollectionPointId)).ToListAsync(ct)).Where(c => c.VigenteEn(fecha)).Select(c => c.Id).ToList();
        var conCajero = (await db.CashBoxCashiers.AsNoTracking().Where(c => cajas.Contains(c.CashBoxId)).ToListAsync(ct)).Any(c => c.VigenteEn(fecha));
        var ofrecidos = (await db.PaymentMeansCollectionPoints.AsNoTracking().Where(o => puntos.Contains(o.CollectionPointId)).ToListAsync(ct))
            .Where(o => o.VigenteEn(fecha)).Select(o => o.PaymentMeansId).ToList();
        var clases = await db.PaymentMeans.AsNoTracking().Where(m => ofrecidos.Contains(m.Id) && m.IsActive).Select(m => m.Class).Distinct().ToListAsync(ct);
        var tipos = await db.ReceiptDocumentTypes.AsNoTracking().Where(t => t.Status == ReceiptsCatalogStatus.Active).Select(t => t.Kind).Distinct().ToListAsync(ct);
        var lista = new List<RequisitoDeActivacionDto>
        {
            new(Punto, puntos.Count > 0, "Un punto de recaudo vigente."),
            new(CajaConCajero, conCajero, "Una caja vigente con un cajero asignado."),
            new(Efectivo, clases.Contains(PaymentMeansClass.Cash), "El medio efectivo ofrecido en un punto."),
            new(Transferencia, clases.Contains(PaymentMeansClass.Transfer), "El medio transferencia ofrecido en un punto."),
            new(TiposDeDocumento, tipos.Contains(ReceiptDocumentKind.CashReceipt) && tipos.Contains(ReceiptDocumentKind.Voiding),
                "Los tipos de documento del recibo de caja y de la anulación."),
        };
        // En producción no hay activación mínima: la completa exige el cuadre de la puesta en marcha (fase 19).
        if (ambiente.EsProduccion) lista.Add(new(Cuadre, false, "El cuadre definitivo de la puesta en marcha (se habilita con la activación completa)."));
        return lista;
    }
}

/// <summary><c>GET /api/receipts/activation/requirements</c> (<c>Receipts.Activation.View</c>). (nuevo)</summary>
public sealed record GetReceiptsActivationRequirementsQuery : IRequest<Result<EstadoDeActivacionDto>>;

public sealed class GetReceiptsActivationRequirementsQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeRecaudo requisitos, IDateTimeService reloj)
    : IRequestHandler<GetReceiptsActivationRequirementsQuery, Result<EstadoDeActivacionDto>>
{
    public async Task<Result<EstadoDeActivacionDto>> Handle(GetReceiptsActivationRequirementsQuery request, CancellationToken ct)
    {
        var setup = await db.ReceiptsSetups.AsNoTracking().FirstAsync(ct);
        var ultima = await db.ReceiptsActivations.AsNoTracking().OrderByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        var lista = await requisitos.EvaluarAsync(reloj.HoyLocal, ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, ultima?.IsMinimal ?? false,
            !setup.Activo && lista.All(r => r.Met), lista));
    }
}

/// <summary>
/// <c>POST /api/receipts/activation</c> (<c>Receipts.Activation.Activate</c>): con los requisitos básicos, deja <c>RCD_Setup</c> activo (la hora de corte sellada de
/// <c>Recaudo.HoraDeCorte</c>), guarda <c>RCD_Activations</c> con <c>IsMinimal = true</c> y registra por <see cref="EscritorDelRegistroDeIntegracion"/> en el mismo
/// guardado <c>REC × ObligationsCollector</c> (<c>ProveedorDeObligaciones</c> y <c>ProveedorDeCuentas</c> v1) y <c>REC × MessageDestination × Receipts</c>
/// (<c>PagoReversadoPorCartera</c> v1, <c>ConsignacionReversadaPorCartera</c> v1). Sin un requisito, <c>Receipts.Activation.RequirementsMissing</c> con
/// <c>data.missing[]</c>. Rige desde el día siguiente al corte (FR-159); sin corte, desde hoy. (nuevo)
/// </summary>
public sealed record ActivateReceiptsCommand(DateOnly? CutoffDate = null) : IRequest<Result<EstadoDeActivacionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateReceiptsCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    RequisitosDeActivacionDeRecaudo requisitos,
    ParametrosDeCaja parametros,
    EscritorDelRegistroDeIntegracion registro,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ActivateReceiptsCommand, Result<EstadoDeActivacionDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<EstadoDeActivacionDto>> Handle(ActivateReceiptsCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        await cerrojo.TomarSetupAsync(exclusivo: true, ct: ct);
        var setup = await db.ReceiptsSetups.FirstAsync(ct);
        if (setup.Activo) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.YaActivado());
        var hoy = reloj.HoyLocal;
        var lista = await requisitos.EvaluarAsync(hoy, ct);
        var faltan = lista.Where(r => !r.Met).Select(r => r.Code).ToList();
        if (faltan.Count > 0) return Result.Failure<EstadoDeActivacionDto>(ErroresDeRecaudo.RequisitosDeActivacion(faltan));

        var corte = request.CutoffDate ?? hoy.AddDays(-1);
        var desde = corte.AddDays(1);
        var motivo = "Activación mínima de Recaudo (bloque 2)";
        var recaudador = await registro.RegistrarAsync(new NuevoParticipante(ClavesDeRecaudo.Codigo, IntegrationParticipantClass.Receipts,
            IntegrationParticipantRole.ObligationsCollector, null, true, false,
            [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1), new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 1)], desde, motivo), ct);
        if (recaudador.IsFailure) return Result.Failure<EstadoDeActivacionDto>(recaudador.Error);
        var destino = await registro.RegistrarAsync(new NuevoParticipante(ClavesDeRecaudo.Codigo, IntegrationParticipantClass.Receipts,
            IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Receipts, true, false,
            [new VersionAdoptada(PagoReversadoPorCarteraV1.Type, 1), new VersionAdoptada(ConsignacionReversadaPorCarteraV1.Type, 1)], desde, motivo), ct);
        if (destino.IsFailure) return Result.Failure<EstadoDeActivacionDto>(destino.Error);

        setup.Status = ReceiptsModuleStatus.Active;
        setup.ActivatedOn = desde;
        setup.CutoffTime = await parametros.HoraAsync(ParametrosDeRecaudoYTesoreria.RecaudoHoraDeCorte, hoy, ct);
        db.ReceiptsActivations.Add(new ReceiptsActivation
        {
            ActivatedOn = desde, CutoffDate = corte, ActivatedByUserId = usuario, RequirementsJson = JsonSerializer.Serialize(lista, Json), IsMinimal = true,
        });
        await auditoria.AgregarAsync("Receipts.Activated", "ReceiptsSetup", setup.PublicId, null, new { activatedOn = desde, minimal = true, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new EstadoDeActivacionDto(setup.Status.ToString(), setup.ActivatedOn, true, false, lista));
    }
}
