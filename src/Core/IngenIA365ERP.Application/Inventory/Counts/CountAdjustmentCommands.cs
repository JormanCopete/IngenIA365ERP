using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// La fecha y el costo del ajuste de un conteo (feature 012, US11, T396; FR-041; data-model §8): la fecha la fija
/// <c>Conteo.FechaDelAjuste</c> —<c>Foto</c> (defecto): la de la foto; <c>Aprobacion</c>: la de la última aprobación— y, si ese mes
/// ya se cerró, el primer día abierto; el valor es el costo promedio vigente del ámbito en esa fecha (el kardex hasta ese día). Lo
/// usan la vista previa, la generación y la última aprobación (<see cref="FechaDelAjusteDeConteo"/>). (nuevo)
/// </summary>
public sealed class ReglaDelAjusteDeConteo(IApplicationDbContext db, IMaestrosDelDocumento maestros, ILectorDeParametros parametros, IDateTimeService reloj)
{
    /// <summary>La causa sembrada «diferencia de conteo» (<c>AdjustmentCausesSeeder.CodigoDiferenciaDeConteo</c>).</summary>
    public const string CausaDiferenciaDeConteo = "DIFCONTEO";

    /// <summary>La regla de fecha vigente a la fecha de la foto del conteo.</summary>
    public async Task<Result<string>> ReglaAsync(InventoryDocument conteo, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ConteoFechaDelAjuste, conteo.OperationDate, ct: ct);
        return leido.IsFailure
            ? Result.Failure<string>(leido.Error)
            : Result.Success(leido.Value.Texto == ReglasDeFechaDelAjuste.Aprobacion ? ReglasDeFechaDelAjuste.Aprobacion : ReglasDeFechaDelAjuste.Foto);
    }

    /// <summary>La fecha del ajuste hoy: la de la foto o la de hoy según la regla; si ese mes cerró, el día siguiente al último cierre.</summary>
    public async Task<DateOnly> FechaAsync(InventoryDocument conteo, string regla, CancellationToken ct)
    {
        var fecha = regla == ReglasDeFechaDelAjuste.Aprobacion ? reloj.HoyLocal : conteo.OperationDate;
        var corte = await maestros.CorteAsync(ct);
        return corte.LastClosedDate is { } cerrado && fecha <= cerrado ? cerrado.AddDays(1) : fecha;
    }

    /// <summary>El costo promedio vigente del producto en su ámbito a <paramref name="fecha"/> (o el último costo de entrada sin existencia).</summary>
    public async Task<decimal> CostoALaFechaAsync(int productoId, int bodegaId, DateOnly fecha, CancellationToken ct)
    {
        var ambito = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CosteoAmbito, fecha, ct: ct);
        var ambitoId = ambito.IsSuccess && ambito.Value.Texto == "Bodega" ? bodegaId : 0;
        var delAmbito = db.KardexEntries.AsNoTracking().Where(k => k.ProductId == productoId && k.CostScopeWarehouseId == ambitoId && k.OperationDate <= fecha);
        var suma = await delAmbito.GroupBy(k => 1).Select(g => new { Cantidad = g.Sum(k => k.QuantityBase), Valor = g.Sum(k => k.TotalCost) }).FirstOrDefaultAsync(ct);
        if (suma is { Cantidad: > 0m }) return Math.Round(suma.Valor / suma.Cantidad, 6, MidpointRounding.AwayFromZero);
        return await delAmbito.Where(k => k.Kind == KardexEntryKind.Entry).OrderByDescending(k => k.OperationDate).ThenByDescending(k => k.Id)
            .Select(k => (decimal?)k.UnitCost).FirstOrDefaultAsync(ct) ?? 0m;
    }

    /// <summary>El conteo del que nace un ajuste (vínculo <c>CountAdjustmentOf</c>), o nulo si no es un ajuste de conteo.</summary>
    public async Task<InventoryDocument?> ConteoDelAjusteAsync(int ajusteId, CancellationToken ct) =>
        await db.DocumentLinks.AsNoTracking().Where(l => l.TargetDocumentId == ajusteId && l.Kind == DocumentLinkKind.CountAdjustmentOf)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, d => d.Id, (l, d) => d)
            .FirstOrDefaultAsync(ct);
}

// -------------------------------------------------------------------------------------------- vista previa --

/// <summary>
/// <c>GET /api/inventory/counts/{id}/adjustment</c> (feature 012, US11, T396; contracts/api.md §12, permiso <c>Inventory.Counts.View</c>):
/// lo que el ajuste haría —fecha según la regla, las líneas con diferencia y su valor al costo de esa fecha (valores con
/// <c>Inventory.Costs.Read</c>)— y los ajustes que ya tiene. Sólo de un conteo cerrado. (nuevo)
/// </summary>
public sealed record GetCountAdjustmentPreviewQuery(Guid CountPublicId) : IRequest<Result<CountAdjustmentPreviewDto>>;

public sealed class GetCountAdjustmentPreviewQueryValidator : AbstractValidator<GetCountAdjustmentPreviewQuery>
{
    public GetCountAdjustmentPreviewQueryValidator() => RuleFor(x => x.CountPublicId).NotEmpty();
}

/// <summary>El alcance del conteo lo aplica <see cref="VistaDeConteos.BuscarAsync"/> (por <see cref="IAlcanceDeInventario"/>).</summary>
public sealed class GetCountAdjustmentPreviewQueryHandler(
    IApplicationDbContext db,
    VistaDeConteos conteos,
    VistaDeDocumentos vista,
    ReglaDelAjusteDeConteo regla)
    : IRequestHandler<GetCountAdjustmentPreviewQuery, Result<CountAdjustmentPreviewDto>>
{
    public async Task<Result<CountAdjustmentPreviewDto>> Handle(GetCountAdjustmentPreviewQuery request, CancellationToken ct)
    {
        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: false, ct);
        if (conteo is null) return Result.Failure<CountAdjustmentPreviewDto>(ErroresDeConteos.NotFound());
        if (conteo.Status != DocumentStatus.Confirmed) return Result.Failure<CountAdjustmentPreviewDto>(InventoryErrors.NotConfirmed(conteo.Status));

        var laRegla = await regla.ReglaAsync(conteo, ct);
        if (laRegla.IsFailure) return Result.Failure<CountAdjustmentPreviewDto>(laRegla.Error);
        var fecha = await regla.FechaAsync(conteo, laRegla.Value, ct);
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);

        var lineas = await db.CountSnapshotLines.AsNoTracking().Where(l => l.DocumentId == conteo.Id && l.Difference != null && l.Difference != 0m)
            .OrderBy(l => l.Id).ToListAsync(ct);
        var productos = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => lineas.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new ReferenciaDto(p.PublicId, p.Code, p.Name), ct);
        var ubicaciones = await db.WarehouseLocations.AsNoTracking().IgnoreQueryFilters().Where(u => lineas.Select(l => l.LocationId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new ReferenciaDto(u.PublicId, u.Code, u.Name), ct);

        var filas = new List<CountAdjustmentPreviewLineDto>(lineas.Count);
        foreach (var l in lineas)
        {
            var costo = costos ? await regla.CostoALaFechaAsync(l.ProductId, conteo.WarehouseId ?? 0, fecha, ct) : (decimal?)null;
            filas.Add(new CountAdjustmentPreviewLineDto(
                productos.GetValueOrDefault(l.ProductId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                ubicaciones.GetValueOrDefault(l.LocationId) ?? new ReferenciaDto(Guid.Empty, string.Empty, string.Empty),
                null, l.TheoreticalQuantity + (l.MovementsAfterSnapshot ?? 0m), l.CountedQuantity ?? 0m, l.Difference!.Value,
                costo, costo is { } c ? Math.Round(l.Difference.Value * c, 2, MidpointRounding.AwayFromZero) : null));
        }

        var ajustes = await conteos.AjustesAsync(conteo.Id, ct);
        return Result.Success(new CountAdjustmentPreviewDto(fecha, laRegla.Value, filas,
            costos ? filas.Where(f => f.Difference > 0m).Sum(f => f.Value ?? 0m) : null,
            costos ? filas.Where(f => f.Difference < 0m).Sum(f => -(f.Value ?? 0m)) : null,
            ajustes.Select(a => new CountAdjustmentRefDto(a.PublicId, a.Class, VistaDeDocumentos.NumeroVisible(a.Prefix, a.Number), a.Status, a.OperationDate, null))
                .ToList()));
    }
}

// --------------------------------------------------------------------------------------------- generación --

/// <summary>
/// Genera el ajuste de un conteo cerrado (feature 012, US11, T396; FR-041, US11-4; contracts/api.md §12, <c>POST /counts/{id}/adjustment</c>
/// con <c>{ notes? }</c>, permiso <c>Inventory.Counts.Close</c>): hasta dos documentos —<c>PositiveAdjustment</c> con los sobrantes y
/// <c>NegativeAdjustment</c> con los faltantes— del tipo de ajuste de conteo, con la causa «diferencia de conteo», enlazados
/// <c>CountAdjustmentOf</c> y <b>en aprobación</b>. (nuevo)
/// </summary>
public sealed record GenerateCountAdjustmentCommand(Guid CountPublicId, string? Notes = null) : IRequest<Result<CountAdjustmentResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateCountAdjustmentCommandValidator : AbstractValidator<GenerateCountAdjustmentCommand>
{
    public GenerateCountAdjustmentCommandValidator()
    {
        RuleFor(x => x.CountPublicId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>
/// Dentro de una <see cref="TransaccionExplicita"/>: el conteo existe en el alcance (404) y está cerrado
/// (<c>Inventory.Document.NotConfirmed</c>); si ya tiene un ajuste en borrador, en aprobación o confirmado,
/// <c>Inventory.Count.AdjustmentInProgress</c>; sin diferencias no crea nada (el conteo queda <c>Adjusted</c>). Cada documento va en la
/// fecha de <see cref="ReglaDelAjusteDeConteo"/> y se valora al costo vigente —el de esa fecha: el registro lo inserta en su lugar,
/// exento de <c>Costeo.RetroactivosPermitidos</c> por su vínculo (<c>RegistroDeKardex.EsExentoAsync</c>, D9)—. La solicitud es la de
/// confirmación del tipo (su política sembrada: un nivel, umbral 0, <c>Inventory.Counts.Approve</c>) con excluidos quien genera, quien
/// creó y quien abrió el conteo, los contadores declarados y todos los que capturaron (FR-010). Si la política vigente no pidiera
/// niveles, el ajuste se confirma en la misma transacción.
/// </summary>
public sealed class GenerateCountAdjustmentCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IMotorDeAprobaciones motor,
    VistaDeConteos conteos,
    ReglaDelAjusteDeConteo regla,
    ConfirmacionDeDocumento confirmacion)
    : IRequestHandler<GenerateCountAdjustmentCommand, Result<CountAdjustmentResultDto>>
{
    public Task<Result<CountAdjustmentResultDto>> Handle(GenerateCountAdjustmentCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => GenerarAsync(request, ct), ct);

    private async Task<Result<CountAdjustmentResultDto>> GenerarAsync(GenerateCountAdjustmentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var conteo = await conteos.BuscarAsync(request.CountPublicId, seguir: false, ct);
        if (conteo is null) return Falla(ErroresDeConteos.NotFound());
        if (conteo.Status != DocumentStatus.Confirmed) return Falla(InventoryErrors.NotConfirmed(conteo.Status));
        var vigentes = VistaDeConteos.AjustesVigentes(await conteos.AjustesAsync(conteo.Id, ct));
        if (vigentes.Count > 0) return Falla(ErroresDeConteos.AdjustmentInProgress(vigentes.Select(a => a.PublicId).ToList()));

        var lineas = await db.CountSnapshotLines.AsNoTracking().Where(l => l.DocumentId == conteo.Id && l.Difference != null && l.Difference != 0m)
            .OrderBy(l => l.ProductId).ThenBy(l => l.LocationId).ToListAsync(ct);
        if (lineas.Count == 0) return Result.Success(new CountAdjustmentResultDto([]));

        var laRegla = await regla.ReglaAsync(conteo, ct);
        if (laRegla.IsFailure) return Falla(laRegla.Error);
        var fecha = await regla.FechaAsync(conteo, laRegla.Value, ct);
        var causa = await db.AdjustmentCauses.AsNoTracking().FirstOrDefaultAsync(c => c.Code == ReglaDelAjusteDeConteo.CausaDiferenciaDeConteo, ct);
        if (causa is null) return Falla(ErroresDelDocumento.CausaInexistente());
        var participantes = (await conteos.ParticipantesAsync(conteo, ct)).Append(usuario).Distinct().ToList();
        var bodega = await db.Warehouses.AsNoTracking().Where(w => w.Id == conteo.WarehouseId).Select(w => new { w.Id, w.PublicId, w.BranchId }).FirstAsync(ct);
        var baseDe = await db.Products.AsNoTracking().IgnoreQueryFilters().Where(p => lineas.Select(l => l.ProductId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.BaseUnitId, ct);
        var numeroDelConteo = VistaDeDocumentos.NumeroVisible(conteo.Prefix, conteo.Number);

        var generados = new List<GeneratedAdjustmentDto>();
        foreach (var (clase, delSigno) in new[]
                 {
                     (DocumentClass.PositiveAdjustment, lineas.Where(l => l.Difference > 0m).ToList()),
                     (DocumentClass.NegativeAdjustment, lineas.Where(l => l.Difference < 0m).ToList()),
                 })
        {
            if (delSigno.Count == 0) continue;
            var tipo = await TipoDeAjusteDeConteoAsync(clase, fecha, ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentClassNotAvailable(clase));

            var ajuste = new InventoryDocument
            {
                Class = clase,
                DocumentTypeId = tipo.Id,
                DocumentType = tipo,
                OperationDate = fecha,
                CreatedByUserId = usuario,
                WarehouseId = bodega.Id,
                BranchId = bodega.BranchId,
                Reason = $"Diferencia del conteo {numeroDelConteo}",
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            };
            var numero = 0;
            var valor = 0m;
            foreach (var l in delSigno)
            {
                var cantidad = Math.Abs(l.Difference!.Value);
                ajuste.Lines.Add(new InventoryDocumentLine
                {
                    Document = ajuste,
                    LineNumber = ++numero,
                    ProductId = l.ProductId,
                    UnitId = baseDe[l.ProductId],
                    Quantity = cantidad,
                    Factor = 1m,
                    QuantityBase = cantidad,
                    LocationId = l.LocationId,
                    LotId = l.LotId,
                    AdjustmentCauseId = causa.Id,
                });
                valor += cantidad * await regla.CostoALaFechaAsync(l.ProductId, bodega.Id, fecha, ct);
            }
            ajuste.CostTotal = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
            db.InventoryDocuments.Add(ajuste);
            db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = conteo.Id, TargetDocument = ajuste, Kind = DocumentLinkKind.CountAdjustmentOf });
            await db.SaveChangesAsync(ct);

            ajuste.EnviarAAprobacion();
            var solicitud = await motor.SolicitarAsync(new SolicitudDeAprobacion(
                ApprovalSubjects.DocumentConfirmation,
                ApprovalSourceTypes.InventoryDocument,
                ajuste.PublicId,
                $"{tipo.Code} {tipo.Name} del conteo {numeroDelConteo}",
                tipo.PublicId,
                bodega.PublicId,
                null,
                ajuste.CostTotal,
                ajuste.OperationDate,
                usuario,
                participantes,
                ConfirmacionDeDocumento.Huella(ajuste),
                null), ct);
            if (solicitud.IsFailure) return Falla(solicitud.Error);
            await db.SaveChangesAsync(ct);

            if (solicitud.Value is null)
            {
                // La política vigente no pide niveles para este monto: se confirma ya, con la fecha de la regla.
                var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(ajuste.PublicId, null, PorAprobacion: true), ct);
                if (confirmada.IsFailure) return Falla(confirmada.Error);
            }

            generados.Add(new GeneratedAdjustmentDto(ajuste.PublicId, clase, delSigno.Count, ajuste.CostTotal, ajuste.Status, solicitud.Value?.PublicId));
        }

        return Result.Success(new CountAdjustmentResultDto(generados));
    }

    /// <summary>
    /// El tipo de ajuste de conteo de la clase: activo y con una política de confirmación vigente a la fecha que tiene un nivel con
    /// <c>Inventory.Counts.Approve</c> (así lo siembra <c>InventoryDocumentTypesSeeder</c>); entre varios, el de menor código.
    /// </summary>
    private async Task<InventoryDocumentType?> TipoDeAjusteDeConteoAsync(DocumentClass clase, DateOnly fecha, CancellationToken ct)
    {
        var tipos = await db.InventoryDocumentTypes.Where(t => t.Class == clase && t.IsActive).OrderBy(t => t.Code).ToListAsync(ct);
        foreach (var tipo in tipos)
        {
            var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, tipo.PublicId);
            var versiones = await db.ApprovalPolicies.AsNoTracking().Include(p => p.Levels).Where(p => p.PolicyKey == clave).ToListAsync(ct);
            var vigente = versiones.Where(v => v.VigenteEn(fecha)).OrderByDescending(v => v.ValidFrom).FirstOrDefault()
                ?? versiones.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            if (vigente is not null && vigente.Levels.Any(l => !l.IsDeleted && l.PermissionCode == ReglasDePlataformaDeInventario.PermisoDeConteo))
                return tipo;
        }
        return null;
    }

    private static Result<CountAdjustmentResultDto> Falla(Error error) => Result.Failure<CountAdjustmentResultDto>(error);
}

// ----------------------------------------------------------------------------------- última aprobación --

/// <summary>
/// Antes de que la última aprobación de un ajuste de conteo lo confirme (feature 012, US11, T396; FR-041): le pone la fecha de la regla
/// —con <c>Aprobacion</c>, hoy; con <c>Foto</c>, la de la foto— y, si ese mes cerró mientras esperaba, el primer día abierto. Los demás
/// documentos no se tocan. (nuevo)
/// </summary>
public sealed class FechaDelAjusteDeConteo(IApplicationDbContext db, ReglaDelAjusteDeConteo regla) : IAntesDeConfirmarPorAprobacion
{
    public async Task<Result> PrepararAsync(Guid documentoPublicId, CancellationToken ct)
    {
        var ajuste = await db.InventoryDocuments.FirstOrDefaultAsync(d => d.PublicId == documentoPublicId, ct);
        if (ajuste is null || ajuste.Class is not (DocumentClass.PositiveAdjustment or DocumentClass.NegativeAdjustment)) return Result.Success();
        var conteo = await regla.ConteoDelAjusteAsync(ajuste.Id, ct);
        if (conteo is null) return Result.Success();

        var laRegla = await regla.ReglaAsync(conteo, ct);
        if (laRegla.IsFailure) return Result.Failure(laRegla.Error);
        ajuste.OperationDate = await regla.FechaAsync(conteo, laRegla.Value, ct);
        return Result.Success();
    }
}
