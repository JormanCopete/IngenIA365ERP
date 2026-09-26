using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Enums.Alerts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Alerts.SaveAlertType;

/// <summary>
/// Registra una versión de la configuración de un tipo de alerta (feature 012, T39, T094; contracts/api.md §16.2,
/// <c>POST /api/inventory/alert-types/{typeCode}/versions</c>, <c>Inventory.Alerts.Manage</c>, con motivo e
/// <c>Idempotency-Key</c>). El tipo es del catálogo cerrado; la versión nueva cierra la anterior la víspera. La
/// severidad y el módulo no se configuran: vienen del tipo. <see cref="IsEnabled"/> falso deja de levantarla desde la
/// fecha.
/// </summary>
public sealed record SaveAlertTypeCommand(
    string TypeCode,
    IReadOnlyList<string> RecipientPermissions,
    IReadOnlyList<AlertChannels> Channels,
    IReadOnlyDictionary<string, decimal>? Thresholds,
    DateOnly ValidFrom,
    string Reason,
    bool IsEnabled = true)
    : IRequest<Result<AlertTypeDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

/// <summary>
/// La forma: motivo (300, el largo de la columna), fecha, listas presentes. Lo que depende del catálogo o de la base
/// (tipo, permisos, canales, umbrales, cruces) lo responde el handler con su código.
/// </summary>
public sealed class SaveAlertTypeCommandValidator : ValidadorConMotivo<SaveAlertTypeCommand>
{
    public SaveAlertTypeCommandValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(300).WithMessage("El motivo admite hasta 300 caracteres.");
        RuleFor(x => x.TypeCode).NotEmpty().MaximumLength(60);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indicá desde cuándo rige.");
        RuleFor(x => x.RecipientPermissions).NotNull().WithMessage("Indicá los permisos destinatarios.");
        RuleForEach(x => x.RecipientPermissions).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Channels).NotNull().WithMessage("Indicá los canales.");
        RuleFor(x => x.RecipientPermissions)
            .Must(ps => ps is null || AlertType.PermisosComoJson(ps).Length <= 1000)
            .WithMessage("Demasiados permisos destinatarios.");
    }
}

/// <summary>
/// El único escritor de versiones de <c>COR_AlertTypes</c> fuera de la semilla. En orden: tipo del catálogo
/// (<c>Alerts.Type.NotFound</c>, 404), la aplicación siempre va (<c>InAppRequired</c>), destinatarios presentes si el
/// tipo usa lista (<c>RecipientsRequired</c>), ningún permiso de consulta (<c>ViewPermissionNotAllowed</c>: el glob
/// <c>*.View</c> la repartiría a todos los roles), permisos del catálogo (<c>PermissionUnknown</c>), umbrales del tipo
/// (<c>ThresholdsInvalid</c>), sin cruces (<c>Overlaps</c>) y cierre de la anterior la víspera.
/// </summary>
public sealed class SaveAlertTypeCommandHandler(IApplicationDbContext db, IDestinatariosPorPermiso destinatarios)
    : IRequestHandler<SaveAlertTypeCommand, Result<AlertTypeDto>>
{
    public async Task<Result<AlertTypeDto>> Handle(SaveAlertTypeCommand request, CancellationToken ct)
    {
        var definicion = TiposDeAlerta.Buscar(request.TypeCode);
        if (definicion is null) return Result.Failure<AlertTypeDto>(ErroresDeAlertas.TipoInexistente(request.TypeCode));

        if (!request.Channels.Contains(AlertChannels.InApp))
            return Result.Failure<AlertTypeDto>(ErroresDeAlertas.AplicacionRequerida());

        var permisos = request.RecipientPermissions.Select(p => p.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (definicion.UsaDestinatarios && permisos.Count == 0)
            return Result.Failure<AlertTypeDto>(ErroresDeAlertas.DestinatariosRequeridos());

        foreach (var permiso in permisos)
        {
            if (permiso.EndsWith(".View", StringComparison.OrdinalIgnoreCase))
                return Result.Failure<AlertTypeDto>(ErroresDeAlertas.PermisoDeConsulta(permiso));
            if (!await db.Permissions.AnyAsync(p => p.Resource + "." + p.Action == permiso, ct))
                return Result.Failure<AlertTypeDto>(ErroresDeAlertas.PermisoDesconocido(permiso));
        }

        var umbrales = request.Thresholds ?? new Dictionary<string, decimal>();
        foreach (var clave in umbrales.Keys)
        {
            if (!definicion.UmbralesAdmitidos.Contains(clave, StringComparer.Ordinal))
                return Result.Failure<AlertTypeDto>(ErroresDeAlertas.UmbralesInvalidos(clave));
        }

        var versiones = await db.AlertTypes.Where(t => t.TypeCode == request.TypeCode).ToListAsync(ct);
        var posterior = versiones.Where(v => v.ValidFrom >= request.ValidFrom).OrderBy(v => v.ValidFrom).FirstOrDefault();
        if (posterior is not null) return Result.Failure<AlertTypeDto>(ErroresDeAlertas.VigenciaSeCruza(posterior.ValidFrom));

        var vispera = request.ValidFrom.AddDays(-1);
        var anterior = versiones.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
        if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo > vispera))
            anterior.ValidTo = vispera;

        var canales = request.Channels.Aggregate(AlertChannels.InApp, (acumulado, c) => acumulado | c);
        var nueva = new AlertType
        {
            TypeCode = definicion.TypeCode,
            Module = definicion.Module,
            RecipientPermissions = AlertType.PermisosComoJson(permisos),
            Channels = canales,
            Severity = anterior?.Severity ?? definicion.Severidad,
            ThresholdsJson = umbrales.Count == 0 ? null : JsonSerializer.Serialize(umbrales),
            IsEnabled = request.IsEnabled,
            ValidFrom = request.ValidFrom,
            Reason = request.Reason.Trim(),
        };
        db.AlertTypes.Add(nueva);
        await db.SaveChangesAsync(ct);

        var activos = definicion.UsaDestinatarios ? await destinatarios.ContarActivosAsync(permisos, ct) : 0;
        return Result.Success(ProyeccionDeAlertas.ADto(nueva, activos));
    }
}
