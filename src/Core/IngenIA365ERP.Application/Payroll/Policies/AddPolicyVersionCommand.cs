using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Policies;

/// <summary>
/// Nueva vigencia de una política por empresa (feature 010, R4; contracts/api.md §10.1). Es una
/// fila nueva desde <see cref="ValidFrom"/>; si se cruza con la vigente y
/// <see cref="ClosePrevious"/> viene en <c>true</c>, la anterior se cierra el día antes; si no,
/// <c>Payroll.CompanyPolicy.VersionOverlaps</c>. Una vigencia anterior a una corrida aprobada
/// que ya leyó la clave se <b>avisa</b> en <see cref="AddPolicyVersionResult.Warnings"/>, salvo
/// <c>Exonerada114_1</c>, que se <b>bloquea</b> porque cambia aportes ya contabilizados.
/// El motivo (<see cref="Reason"/>) queda en <c>Notes</c>: quién decidió y por qué.
/// </summary>
public sealed record AddPolicyVersionCommand(
    string Key,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason,
    bool ClosePrevious = true) : IRequest<Result<AddPolicyVersionResult>>;

public sealed record AddPolicyVersionResult(Guid PublicId, IReadOnlyList<string> Warnings);

public sealed class AddPolicyVersionCommandValidator : AbstractValidator<AddPolicyVersionCommand>
{
    public AddPolicyVersionCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().WithMessage("La clave es obligatoria.").MaximumLength(60);
        RuleFor(x => x.Value).NotEmpty().WithMessage("El valor es obligatorio.").MaximumLength(400);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("La vigencia desde es obligatoria.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia hasta no puede ser anterior a la vigencia desde.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Indicá el motivo del cambio (quién lo decidió y por qué).").MaximumLength(300);
    }
}

public sealed class AddPolicyVersionCommandHandler(
    IApplicationDbContext db,
    IDateTimeService clock,
    ICurrentUserService user,
    IPayrollRunStaleMarker stale,
    PayrollAuditEmitter audit)
    : IRequestHandler<AddPolicyVersionCommand, Result<AddPolicyVersionResult>>
{
    public async Task<Result<AddPolicyVersionResult>> Handle(AddPolicyVersionCommand request, CancellationToken ct)
    {
        var clave = request.Key.Trim();
        var def = CompanyPolicyKeys.Buscar(clave);
        if (def is null) return Result.Failure<AddPolicyVersionResult>(CompanyPolicyErrors.KeyUnknown(clave));

        var valor = request.Value.Trim();
        var reparoDeValor = ValidarValor(def, valor);
        if (reparoDeValor is not null) return Result.Failure<AddPolicyVersionResult>(reparoDeValor);

        var desde = request.ValidFrom;
        var hasta = request.ValidTo;
        var versiones = await db.CompanyPolicies.Where(p => p.Key == clave && !p.IsDeleted).OrderBy(p => p.ValidFrom).ToListAsync(ct);

        // Dos vigencias de la misma clave no se cruzan (data-model §2.1). La que empieza antes
        // y sigue abierta o termina después de «desde» se puede cerrar el día antes si lo piden;
        // una que empieza después dentro del rango nuevo se cruza siempre.
        var aCerrar = new List<CompanyPolicy>();
        foreach (var v in versiones)
        {
            var seCruza = v.ValidFrom <= (hasta ?? DateOnly.MaxValue) && (v.ValidTo ?? DateOnly.MaxValue) >= desde;
            if (!seCruza) continue;
            if (v.ValidFrom < desde && request.ClosePrevious)
            {
                aCerrar.Add(v);
                continue;
            }
            return Result.Failure<AddPolicyVersionResult>(CompanyPolicyErrors.VersionOverlaps(clave, desde, v.ValidTo, v.ValidFrom));
        }

        // Retroactividad: una corrida aprobada cuya fecha cae dentro de la vigencia nueva leyó
        // el valor anterior. Se avisa; Exonerada114_1 se bloquea (cambia aportes contabilizados).
        var avisos = new List<string>();
        var (corridas, primera) = await CorridasAprobadasDesdeAsync(desde, ct);
        if (corridas > 0)
        {
            if (string.Equals(clave, CompanyPolicyKeys.Exonerada114_1, StringComparison.Ordinal))
                return Result.Failure<AddPolicyVersionResult>(CompanyPolicyErrors.RetroactiveNotAllowed(clave, desde, corridas, primera));
            avisos.Add($"Hay {corridas} corrida(s) aprobada(s) desde el {primera:dd/MM/yyyy} que leyeron el valor anterior de {clave}; el cambio no las recalcula.");
        }

        var ahora = clock.UtcNow;
        var vigenteAntes = versiones.Where(v => v.IsValidAt(desde)).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
        var antes = vigenteAntes is null ? null : new { key = clave, value = vigenteAntes.Value, validFrom = vigenteAntes.ValidFrom, validTo = vigenteAntes.ValidTo };

        foreach (var v in aCerrar)
        {
            v.ValidTo = desde.AddDays(-1);
            v.UpdatedAt = ahora;
            v.UpdatedBy = user.UserName;
        }

        var nueva = new CompanyPolicy
        {
            Key = clave,
            Value = valor,
            ValidFrom = desde,
            ValidTo = hasta,
            Notes = request.Reason.Trim(),
            CreatedAt = ahora,
            CreatedBy = user.UserName,
        };
        db.CompanyPolicies.Add(nueva);
        await stale.MarkAllDraftsStaleAsync($"política {clave} con vigencia nueva desde {desde:yyyy-MM-dd}", ct);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollCompanyPolicyChanged, nameof(CompanyPolicy), nueva.PublicId, antes,
            new { key = clave, value = valor, validFrom = desde, validTo = hasta, reason = nueva.Notes, closedPrevious = aCerrar.Select(c => c.PublicId).ToList() }, ct);

        return Result.Success(new AddPolicyVersionResult(nueva.PublicId, avisos));
    }

    /// <summary>
    /// Un valor admitido del catálogo, o, en las claves de texto libre, el formato que exigen:
    /// la fecha de arranque como <c>yyyy-MM-dd</c> y el mapa de medios de pago como un JSON
    /// con las tres formas de pago de la ficha.
    /// </summary>
    public static Error? ValidarValor(CompanyPolicyKeys.Definicion def, string valor)
    {
        if (!CompanyPolicyKeys.Admite(def.Clave, valor))
            return CompanyPolicyErrors.ValueInvalid(def.Clave, valor, def.Admitidos, def.Formato);
        if (def.Admitidos.Count > 0) return null;

        if (string.Equals(def.Clave, CompanyPolicyKeys.ArranqueNominaFecha, StringComparison.Ordinal)
            && !DateOnly.TryParseExact(valor, CompanyPolicyKeys.FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return CompanyPolicyErrors.ValueInvalid(def.Clave, valor, def.Admitidos, def.Formato);

        if (string.Equals(def.Clave, CompanyPolicyKeys.DianMedioPagoMapa, StringComparison.Ordinal) && !EsMapaDeMediosDePago(valor))
            return CompanyPolicyErrors.ValueInvalid(def.Clave, valor, def.Admitidos, def.Formato);

        return null;
    }

    private static bool EsMapaDeMediosDePago(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var forma in new[] { "Transfer", "Check", "Cash" })
            {
                if (!doc.RootElement.TryGetProperty(forma, out var codigo) || codigo.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(codigo.GetString()))
                    return false;
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Corridas aprobadas cuya fecha (fin del período en la ordinaria, corte en las especiales)
    /// es igual o posterior a la vigencia nueva, y la más antigua de ellas.
    /// </summary>
    private async Task<(int Corridas, DateOnly Primera)> CorridasAprobadasDesdeAsync(DateOnly desde, CancellationToken ct)
    {
        var desdeDt = desde.ToDateTime(TimeOnly.MinValue);
        var ordinarias = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Approved && r.Kind == PayrollRunKind.Ordinary && r.PayPeriod!.EndDate >= desdeDt)
            .Select(r => r.PayPeriod!.EndDate)
            .ToListAsync(ct);
        var especiales = await db.PayrollRuns.AsNoTracking()
            .Where(r => r.Status == PayrollRunStatus.Approved && r.Kind != PayrollRunKind.Ordinary && r.CutoffDate != null && r.CutoffDate >= desde)
            .Select(r => r.CutoffDate!.Value)
            .ToListAsync(ct);

        var fechas = ordinarias.Select(DateOnly.FromDateTime).Concat(especiales).ToList();
        return fechas.Count == 0 ? (0, default) : (fechas.Count, fechas.Min());
    }
}
