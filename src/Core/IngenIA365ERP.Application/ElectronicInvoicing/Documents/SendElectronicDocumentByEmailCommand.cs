using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>A dónde salió el correo y si era la dirección de recepción de la copia fiscal. (nuevo)</summary>
public sealed record EnvioPorCorreoDto(Guid ElectronicDocumentPublicId, string Email, bool ToReceptionEmail);

/// <summary>
/// 2026-11-25 (pedido del dueño): enviar por correo un documento electrónico desde su detalle en la bandeja
/// (<c>POST /api/electronic-invoicing/documents/{id}/send-email</c>): el ZIP con el PDF de la representación gráfica y el <c>AttachedDocument</c>,
/// al correo de recepción de la copia fiscal o, si se escribe, a otro (<paramref name="Email"/>). Lo arma <see cref="EntregaAlComprador"/>, el
/// mismo correo de la entrega automática. Sólo validado o en contingencia (<c>NotDeliverable</c>); con el alcance de la bandeja y la regla del
/// dueño de los adjuntos (<c>Inventory.Sales.View</c> o <c>Inventory.Purchases.View</c>), y sin ella el mismo 404 que si no existiera. Cada
/// envío queda en la auditoría encadenada con la dirección. Hasta ese día «Reenviar correo» de la venta pasaba por la entrega en carta y,
/// después de la primera, respondía «ya entregado». (nuevo)
/// </summary>
public sealed record SendElectronicDocumentByEmailCommand(Guid ElectronicDocumentPublicId, string? Email = null)
    : IRequest<Result<EnvioPorCorreoDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendElectronicDocumentByEmailCommandValidator : AbstractValidator<SendElectronicDocumentByEmailCommand>
{
    public SendElectronicDocumentByEmailCommandValidator()
    {
        RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Escriba un correo válido.");
    }
}

public sealed class SendElectronicDocumentByEmailCommandHandler(
    IApplicationDbContext db,
    IEnumerable<IConsultaDeFuenteElectronica> fuentes,
    IPermissionChecker permisos,
    EntregaAlComprador entrega,
    IServiceProvider servicios)
    : IRequestHandler<SendElectronicDocumentByEmailCommand, Result<EnvioPorCorreoDto>>
{
    /// <summary>La acción auditada.</summary>
    public const string Accion = "ElectronicInvoicing.Document.EmailSent";

    public async Task<Result<EnvioPorCorreoDto>> Handle(SendElectronicDocumentByEmailCommand request, CancellationToken ct)
    {
        var q = await ConsultaDeDocumentosElectronicos.ConAlcanceAsync(db.ElectronicDocuments, fuentes, ct);
        var documento = await q.Include(d => d.Versions).FirstOrDefaultAsync(d => d.PublicId == request.ElectronicDocumentPublicId, ct);
        if (documento is null) return Result.Failure<EnvioPorCorreoDto>(ErroresDeDocumentosElectronicos.NotFound());
        // Sin la regla del dueño de los adjuntos, lo mismo que si no existiera (como el enlace de descarga).
        if (!await AdjuntosDeModulo.PuedeLeerAsync(permisos, GuardadoDeArtefactos.DuenoDe(documento.Kind), ct))
            return Result.Failure<EnvioPorCorreoDto>(ErroresDeDocumentosElectronicos.NotFound());

        var version = documento.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (version is null) return Result.Failure<EnvioPorCorreoDto>(ErroresDeDocumentosElectronicos.RepresentationPending());

        var envio = await entrega.EnviarAPedidoAsync(documento, version, request.Email, ct);
        if (envio.IsFailure) return Result.Failure<EnvioPorCorreoDto>(envio.Error);
        var (destino, alCorreoDeRecepcion) = envio.Value;

        await AuditarAsync(documento.PublicId, destino, alCorreoDeRecepcion, version.VersionNumber, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new EnvioPorCorreoDto(documento.PublicId, destino, alCorreoDeRecepcion));
    }

    /// <summary>Anota el envío en la auditoría encadenada (<c>COR_AuditOutbox</c>) o, sin cooperativa, por <see cref="IAuditService"/>.</summary>
    private async Task AuditarAsync(Guid documento, string destino, bool alCorreoDeRecepcion, int version, CancellationToken ct)
    {
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct);
        var evento = new AuditLogCommand
        {
            Action = Accion,
            EntityType = GuardadoDeArtefactos.EntidadDelDocumento,
            EntityId = documento.ToString(),
            Module = ModuloDeAuditoria.ElectronicInvoicing,
            NewValues = new { email = destino, toReceptionEmail = alCorreoDeRecepcion, version },
            HttpStatusCode = 200,
        };
        var flujo = AuditoriaEncadenada.Flujo(contexto.TenantId);
        if (flujo is null)
        {
            if (servicios.GetService<IAuditService>() is { } mongo) await mongo.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
            return;
        }
        db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento)));
    }
}
