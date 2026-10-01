using FluentValidation;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// <c>POST /api/inventory/documents/{id}/prevalidate</c> (feature 012, T521; FR-074; api.md §25.1; <c>Inventory.Documents.View</c>):
/// «¿Contabilidad podría contabilizar este borrador tal como está?». Es una <b>consulta</b>: sin clave de operación, sin cerrojo,
/// sin guardar nada. Arma los mensajes que emitiría la confirmación con los mismos pasos de ella —el modo que sellaría
/// (<see cref="MensajesDelDocumento.ModoAsync"/>), las reglas de la clase que preparan lo necesario para armarlos y los contenidos
/// provisionales (<see cref="IEfectoDeClase.MensajesProvisionalesAsync"/>)— y los pregunta por
/// <see cref="ValidacionPreviaContable.PreguntarAsync"/>, con el mismo tiempo máximo. La confirmación repite la evaluación.
/// <list type="bullet">
/// <item>modo <c>NotPosted</c>, clase que no pasa a Contabilidad o sólo informativos → <c>applies = false</c>;</item>
/// <item>sin respuesta a tiempo → <c>responded = false</c> (la pantalla muestra la política vigente);</item>
/// <item>fuera del alcance del documento, 404 como el detalle.</item>
/// </list>
/// Los pagos propuestos del POS (<c>payments?</c> del cuerpo) llegan con las ventas (I3), que son las únicas que los llevan en su
/// mensaje. (nuevo)
/// </summary>
public sealed record PrevalidateInventoryDocumentQuery(Guid DocumentPublicId) : IRequest<Result<PrevalidationResultDto>>;

/// <summary>El resultado de la validación previa (api.md §25.1). (nuevo)</summary>
public sealed record PrevalidationResultDto(
    bool Applies,
    PostingMode? PostingMode,
    bool Responded,
    bool? IsPostable,
    IReadOnlyList<ErrorDePrevalidacionDto> Errors,
    IReadOnlyList<AvisoDePrevalidacionDto> Warnings,
    long ElapsedMs);

/// <summary>Un error de la validación previa: el mensaje, la línea del documento, la cuenta, la regla y quién corrige. (nuevo)</summary>
public sealed record ErrorDePrevalidacionDto(
    string MessageType,
    int? LineNumber,
    CuentaDePrevalidacionDto? Account,
    string Rule,
    string Message,
    QuienCorrigeDto WhoFixes);

/// <summary>
/// La cuenta de un hallazgo: el código que dice Contabilidad. El nombre va nulo porque Inventario no lee el plan de cuentas (FR-014).
/// (nuevo)
/// </summary>
public sealed record CuentaDePrevalidacionDto(string Code, string? Name);

/// <summary>Un aviso que no impide confirmar (<c>Accounting.Line.TaxAmountDiffers</c>). (nuevo)</summary>
public sealed record AvisoDePrevalidacionDto(string MessageType, string Code, string Message);

public sealed class PrevalidateInventoryDocumentQueryValidator : AbstractValidator<PrevalidateInventoryDocumentQuery>
{
    public PrevalidateInventoryDocumentQueryValidator() => RuleFor(x => x.DocumentPublicId).NotEmpty();
}

public sealed class PrevalidateInventoryDocumentQueryHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    EfectosDeClase efectos,
    MensajesDelDocumento mensajes,
    ValidacionPreviaContable validacion,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<PrevalidateInventoryDocumentQuery, Result<PrevalidationResultDto>>
{
    public async Task<Result<PrevalidationResultDto>> Handle(PrevalidateInventoryDocumentQuery request, CancellationToken ct)
    {
        var documento = await vista.BuscarAsync(request.DocumentPublicId, null, seguir: false, ct);
        if (documento is null) return Falla(InventoryErrors.DocumentNotFound());
        if (documento.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval)) return Falla(InventoryErrors.NotDraft(documento.Status));

        var tipo = documento.DocumentType ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == documento.DocumentTypeId, ct);
        InventoryDocument? original = null;
        if (documento.Class == DocumentClass.Voiding)
        {
            original = await db.InventoryDocuments.AsNoTracking().Include(d => d.Lines).Include(d => d.DocumentType)
                .FirstOrDefaultAsync(d => d.Id == documento.VoidsDocumentId, ct);
            if (original is null) return Falla(InventoryErrors.DocumentNotFound());
        }

        var estrategia = efectos.Para(original?.Class ?? documento.Class);
        if (estrategia.IsFailure) return Falla(estrategia.Error);
        var efecto = estrategia.Value;
        var contexto = new ContextoDeEfecto(documento, tipo, ClasesDeDocumento.De(documento.Class), original);

        var modo = await mensajes.ModoAsync(contexto, efecto, reloj.HoyLocal, ct);
        if (modo.IsFailure) return Falla(modo.Error);
        if (modo.Value.Modo is not { } m || m == PostingMode.NotPosted) return Result.Success(NoAplica(modo.Value.Modo));

        // Las reglas de la clase preparan lo que algunas estrategias necesitan para armar sus mensajes (la factura, el despacho).
        var reglas = await efecto.ValidarAsync(contexto, ct);
        if (reglas.IsFailure) return Falla(reglas.Error);

        var contenidos = await efecto.MensajesProvisionalesAsync(contexto, ct);
        if (contenidos.Count == 0) return Result.Success(NoAplica(m));
        var bodega = documento.WarehouseId is int b
            ? await db.Warehouses.AsNoTracking().Where(w => w.Id == b).Select(w => w.Code).FirstOrDefaultAsync(ct)
            : null;
        var origen = await mensajes.OrigenAsync(documento, tipo, bodega, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var sobres = MensajesDelDocumento.Sobres(
            MensajesDelDocumento.Solicitudes(origen, original, modo.Value.Origenes, contenidos, new ModoDeEntrega.Sellado(DeliveryMode.Online),
                PrevalidationOutcome.NotApplicable),
            actor.CentralUserId, actor.Name, new DateTimeOffset(reloj.UtcNow, TimeSpan.Zero));
        if (!MensajesDelDocumento.HayNegocioAContabilidad(sobres)) return Result.Success(NoAplica(m));

        var respuesta = await validacion.PreguntarAsync(sobres, ct);
        if (!respuesta.Respondio || respuesta.Resultado is not { } resultado)
            return Result.Success(new PrevalidationResultDto(true, m, false, null, [], [], respuesta.ElapsedMs));

        var errores = resultado.Errors
            .SelectMany(h => (h.DocumentLines.Count == 0 ? [(int?)null] : h.DocumentLines.Distinct().Order().Select(n => (int?)n))
                .Select(linea => new ErrorDePrevalidacionDto(h.MessageType, linea,
                    h.AccountCode is null ? null : new CuentaDePrevalidacionDto(h.AccountCode, null), h.Rule, h.Message, h.WhoFixes)))
            .ToList();
        var avisos = resultado.Warnings.Select(h => new AvisoDePrevalidacionDto(h.MessageType, h.Rule, h.Message)).ToList();
        return Result.Success(new PrevalidationResultDto(true, m, true, resultado.IsPostable, errores, avisos, respuesta.ElapsedMs));
    }

    private static PrevalidationResultDto NoAplica(PostingMode? modo) => new(false, modo, true, null, [], [], 0);

    private static Result<PrevalidationResultDto> Falla(Error error) => Result.Failure<PrevalidationResultDto>(error);
}
