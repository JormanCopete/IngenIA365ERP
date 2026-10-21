using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

// Feature 013, T0647 (contracts/contabilidad.md §1.1 y §4.2): el resolutor del tipo de comprobante de una unidad vive con el consumidor;
// nació en Reglas/LendingVoucherMappingCommands.cs (T0642) y se mudó aquí sin cambiar su comportamiento. (nuevo)


/// <summary>El tipo de comprobante y el cruce por defecto de una unidad de Cartera. (nuevo)</summary>
public sealed record TipoDeLaUnidadDeCartera(
    string Operation,
    string? CollectorClass,
    int VoucherTypeId,
    string VoucherTypeCode,
    int? CrossDocumentTypeId,
    string? CrossDocumentTypeCode);

/// <summary>
/// El resolutor del tipo de comprobante de una unidad de Cartera (feature 013, T0642, T54; §4.2): primero <c>(hecho, clase)</c>, después
/// <c>(hecho, *)</c>; sin fila, <c>Accounting.VoucherType.NotFound</c>; un tipo que ya no es <c>Module</c>/<c>CAR</c> activo,
/// <c>Accounting.VoucherType.NotAllowedForModule</c>. El hecho es el <b>principal</b> de la unidad (C01,
/// <see cref="OperacionesDeCartera.Principal"/>). El consumidor (T0647) lo usa desde aquí. (nuevo)
/// </summary>
public sealed class TiposDeComprobanteDeCartera(IApplicationDbContext db)
{
    public static bool EsDeCartera(VoucherUsage uso, string? modulo, bool activo) =>
        uso == VoucherUsage.Module && string.Equals(modulo, ModuloContable.Cartera, StringComparison.OrdinalIgnoreCase) && activo;

    /// <summary>Todos los mapeos vivos, con su tipo y su cruce, para resolver sin más consultas.</summary>
    public async Task<MapeosDeCartera> CargarAsync(CancellationToken ct)
    {
        var filas = await db.LendingVoucherMappings.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new FilaDeMapeoDeCartera(
                m.MappingKey, m.Operation,
                m.VoucherTypeId,
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.Code).FirstOrDefault() ?? string.Empty,
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.Usage).FirstOrDefault(),
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.ModuleCode).FirstOrDefault(),
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.IsActive && !v.IsDeleted).FirstOrDefault(),
                m.CrossDocumentTypeId,
                db.CrossDocumentTypes.Where(c => c.Id == m.CrossDocumentTypeId).Select(c => c.Code).FirstOrDefault()))
            .ToListAsync(ct);
        return new MapeosDeCartera(filas.ToDictionary(f => f.Clave, StringComparer.Ordinal));
    }
}

internal sealed record FilaDeMapeoDeCartera(
    string Clave, string Operation, int VoucherTypeId, string VoucherTypeCode, VoucherUsage Usage, string? ModuleCode, bool Activo,
    int? CrossDocumentTypeId, string? CrossDocumentTypeCode);

/// <summary>Los mapeos de Cartera cargados: <see cref="Resolver"/> aplica §4.2 sin más consultas. (nuevo)</summary>
public sealed class MapeosDeCartera
{
    private readonly IReadOnlyDictionary<string, FilaDeMapeoDeCartera> _porClave;

    internal MapeosDeCartera(IReadOnlyDictionary<string, FilaDeMapeoDeCartera> porClave) => _porClave = porClave;

    public Result<TipoDeLaUnidadDeCartera> Resolver(string operacion, string? claseDeRecaudador)
    {
        var fila = (string.IsNullOrWhiteSpace(claseDeRecaudador) ? null : _porClave.GetValueOrDefault(LendingVoucherMapping.ClaveDe(operacion, claseDeRecaudador)))
            ?? _porClave.GetValueOrDefault(LendingVoucherMapping.ClaveDe(operacion, null));
        if (fila is null) return Result.Failure<TipoDeLaUnidadDeCartera>(AccountingErrors.VoucherTypeNotFound($"del hecho {operacion}"));
        if (!TiposDeComprobanteDeCartera.EsDeCartera(fila.Usage, fila.ModuleCode, fila.Activo))
            return Result.Failure<TipoDeLaUnidadDeCartera>(AccountingErrors.VoucherTypeNotAllowedForModule(fila.VoucherTypeCode, ModuloContable.Cartera));
        return Result.Success(new TipoDeLaUnidadDeCartera(operacion, claseDeRecaudador, fila.VoucherTypeId, fila.VoucherTypeCode,
            fila.CrossDocumentTypeId, fila.CrossDocumentTypeCode));
    }
}
