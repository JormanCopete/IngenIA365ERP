using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Taxation;

/// <summary>La UVT vigente a una fecha, con su vigencia y su norma (feature 012, T23, T163). (nuevo)</summary>
public sealed record UvtVigente(decimal Valor, DateOnly VigenteDesde, string? Fuente);

/// <summary>
/// El único lector de la UVT (feature 012, T23, T163; <c>LaUvtSeLeeEnUnSoloSitio</c>). La UVT vive en los parámetros
/// legales de nómina (<c>PAY_LegalParameters</c>, código <see cref="LegalParameterCodes.Uvt"/>) y no se crea otra:
/// cuando se promueva a Core, cambia sólo este lector.
/// </summary>
public interface IValorUvt
{
    /// <summary>La UVT vigente a <paramref name="fecha"/>; sin vigencia, <c>Taxation.Uvt.Missing</c>.</summary>
    Task<Result<UvtVigente>> LeerAsync(DateOnly fecha, CancellationToken ct = default);
}

/// <summary>
/// Implementación de <see cref="IValorUvt"/> (T163). Scoped: memoriza cada fecha por petición. Una vigencia sin valor
/// o de baja no cuenta: falla visible, nunca un cero que dejaría retener sobre cualquier base.
/// </summary>
public sealed class LectorDeUvt(IApplicationDbContext db) : IValorUvt
{
    public const string CodigoFaltante = "Taxation.Uvt.Missing";

    private readonly Dictionary<DateOnly, Result<UvtVigente>> _memoria = [];

    /// <summary>«No hay UVT vigente al {fecha}; regístrela en Parámetros legales» (T23).</summary>
    public static Error Faltante(DateOnly fecha) =>
        new(CodigoFaltante, $"No hay UVT vigente al {fecha.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture)}; regístrela en Parámetros legales.");

    public async Task<Result<UvtVigente>> LeerAsync(DateOnly fecha, CancellationToken ct = default)
    {
        if (_memoria.TryGetValue(fecha, out var memorizada)) return memorizada;

        var dia = fecha.ToDateTime(TimeOnly.MinValue);
        var vigente = await db.PayrollLegalParameters.AsNoTracking()
            .Where(p => !p.IsDeleted && p.Code == LegalParameterCodes.Uvt && p.Value != null
                && p.ValidFrom <= dia && (p.ValidTo == null || p.ValidTo >= dia))
            .OrderByDescending(p => p.ValidFrom)
            .Select(p => new { p.Value, p.ValidFrom, p.Source })
            .FirstOrDefaultAsync(ct);

        var resultado = vigente is { Value: { } valor } && valor > 0
            ? Result.Success(new UvtVigente(valor, DateOnly.FromDateTime(vigente.ValidFrom), vigente.Source))
            : Result.Failure<UvtVigente>(Faltante(fecha));
        _memoria[fecha] = resultado;
        return resultado;
    }
}
