using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

// Tipos de comprobante de Inventario (feature 012, T28, T510; contracts/contabilidad.md §2.6 y §9; api.md §26.2): el
// mapeo operación (y tipo de documento) → tipo de comprobante y documento cruce, su consulta y el resolutor del tipo de una
// unidad. (nuevo)

/// <summary><c>InventoryVoucherMappingDto</c> de api.md §26.2 (nuevo).</summary>
public sealed record InventoryVoucherMappingDto(
    Guid MappingPublicId,
    string Operation,
    string? InventoryDocumentTypeCode,
    TipoDelMapeoDto VoucherType,
    CruceDelMapeoDto? CrossDocumentType,
    bool IsSeeded);

public sealed record TipoDelMapeoDto(Guid PublicId, string Code, string Name);

public sealed record CruceDelMapeoDto(Guid PublicId, string Code);

// ============================================================================================================ guardar --

/// <summary>
/// <c>PUT /api/accounting/inventory/voucher-mappings</c>: agrega o reemplaza, en su sitio, la fila de
/// <c>(operación, tipo de documento)</c>. El tipo de comprobante tiene que ser <c>Usage = Module</c>,
/// <c>ModuleCode = INV</c> y activo (<c>Accounting.VoucherType.NotAllowedForModule</c>). Con motivo (el diff queda en
/// la auditoría: el mapeo no tiene vigencia). (nuevo)
/// </summary>
public sealed record SetInventoryVoucherMappingCommand(
    string Operation,
    string? InventoryDocumentTypeCode,
    Guid VoucherTypePublicId,
    Guid? CrossDocumentTypePublicId,
    string Reason) : IRequest<Result<Guid>>, IConMotivo;

public sealed class SetInventoryVoucherMappingCommandValidator : ValidadorConMotivo<SetInventoryVoucherMappingCommand>
{
    public SetInventoryVoucherMappingCommandValidator()
    {
        RuleFor(x => x.Operation).NotEmpty().WithMessage("Indicá la operación.")
            .Must(o => OperacionesDeInventario.Buscar(o) is not null).WithMessage(x => $"«{x.Operation}» no es una operación de la matriz de Inventario.");
        RuleFor(x => x.InventoryDocumentTypeCode).MaximumLength(10);
        RuleFor(x => x.VoucherTypePublicId).NotEmpty().WithMessage("Indicá el tipo de comprobante.");
    }
}

public sealed class SetInventoryVoucherMappingCommandHandler(IApplicationDbContext db, IDimensionesDeInventario dimensiones)
    : IRequestHandler<SetInventoryVoucherMappingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SetInventoryVoucherMappingCommand request, CancellationToken ct)
    {
        var operacion = OperacionesDeInventario.Buscar(request.Operation);
        if (operacion is null) return Result.Failure<Guid>(AccountingErrors.InventoryRuleOperationUnknown(request.Operation));

        var tipoDeDocumento = string.IsNullOrWhiteSpace(request.InventoryDocumentTypeCode) ? null : request.InventoryDocumentTypeCode.Trim().ToUpperInvariant();
        if (tipoDeDocumento is not null)
        {
            var catalogo = await dimensiones.CatalogoAsync(ct);
            if (!catalogo.DocumentTypes.Any(t => string.Equals(t.Code, tipoDeDocumento, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<Guid>(AccountingErrors.InventoryRuleDimensionCodeUnknown("inventoryDocumentTypeCode", tipoDeDocumento));
        }

        var tipo = await db.VoucherTypes.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoucherTypePublicId && !v.IsDeleted, ct);
        if (tipo is null) return Result.Failure<Guid>(AccountingErrors.VoucherTypeNotFound(request.VoucherTypePublicId.ToString()));
        if (!TiposDeComprobanteDeInventario.EsDeInventario(tipo.Usage, tipo.ModuleCode, tipo.IsActive))
            return Result.Failure<Guid>(AccountingErrors.VoucherTypeNotAllowedForModule(tipo.Code, ModuloContable.Inventario));

        int? cruce = null;
        if (request.CrossDocumentTypePublicId is { } cp)
        {
            cruce = await db.CrossDocumentTypes.AsNoTracking().Where(c => c.PublicId == cp && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (cruce is null) return Result.Failure<Guid>(AccountingErrors.CrossDocumentTypeNotFound);
        }

        var clave = InventoryVoucherMapping.ClaveDe(operacion.Codigo, tipoDeDocumento);
        var mapeo = await db.InventoryVoucherMappings.FirstOrDefaultAsync(m => m.MappingKey == clave && !m.IsDeleted, ct);
        if (mapeo is null)
        {
            mapeo = new InventoryVoucherMapping(operacion.Codigo, tipoDeDocumento, tipo.Id, cruce);
            db.InventoryVoucherMappings.Add(mapeo);
        }
        else
        {
            mapeo.VoucherTypeId = tipo.Id;
            mapeo.CrossDocumentTypeId = cruce;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(mapeo.PublicId);
    }
}

// ============================================================================================================ consulta --

public sealed record ListInventoryVoucherMappingsQuery : IRequest<Result<IReadOnlyList<InventoryVoucherMappingDto>>>;

public sealed class ListInventoryVoucherMappingsQueryValidator : AbstractValidator<ListInventoryVoucherMappingsQuery>;

public sealed class ListInventoryVoucherMappingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListInventoryVoucherMappingsQuery, Result<IReadOnlyList<InventoryVoucherMappingDto>>>
{
    public async Task<Result<IReadOnlyList<InventoryVoucherMappingDto>>> Handle(ListInventoryVoucherMappingsQuery request, CancellationToken ct)
    {
        var filas = await db.InventoryVoucherMappings.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new
            {
                m.PublicId, m.Operation, m.InventoryDocumentTypeCode, m.CreatedBy,
                Tipo = db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => new TipoDelMapeoDto(v.PublicId, v.Code, v.Name)).FirstOrDefault(),
                Cruce = db.CrossDocumentTypes.Where(c => c.Id == m.CrossDocumentTypeId).Select(c => new CruceDelMapeoDto(c.PublicId, c.Code)).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var orden = OperacionesDeInventario.Todas.Select((o, i) => (o.Codigo, i)).ToDictionary(x => x.Codigo, x => x.i, StringComparer.Ordinal);
        var items = filas
            .OrderBy(f => orden.GetValueOrDefault(f.Operation, int.MaxValue)).ThenBy(f => f.InventoryDocumentTypeCode ?? string.Empty)
            .Select(f => new InventoryVoucherMappingDto(f.PublicId, f.Operation, f.InventoryDocumentTypeCode,
                f.Tipo ?? new TipoDelMapeoDto(Guid.Empty, string.Empty, string.Empty), f.Cruce,
                string.Equals(f.CreatedBy, TiposDeComprobanteDeInventario.CreadoPorLaSemilla, StringComparison.Ordinal)))
            .ToList();
        return Result.Success<IReadOnlyList<InventoryVoucherMappingDto>>(items);
    }
}

// ============================================================================================================ resolutor --

/// <summary>El tipo de comprobante y el cruce por defecto de una unidad (nuevo).</summary>
public sealed record TipoDeLaUnidad(
    string Operation,
    int VoucherTypeId,
    string VoucherTypeCode,
    int? CrossDocumentTypeId,
    string? CrossDocumentTypeCode);

/// <summary>La operación y el tipo de documento que deciden el tipo de comprobante de una unidad (nuevo).</summary>
public sealed record OperacionDeLaUnidad(string Operation, string? InventoryDocumentTypeCode);

/// <summary>
/// El resolutor del tipo de comprobante de una unidad (feature 012, T28, T510; §2.6): primero
/// <c>(operación, tipo del documento)</c>, después <c>(operación, nulo)</c>; sin fila,
/// <c>Accounting.VoucherType.NotFound</c>; un tipo que ya no es <c>Module</c>/<c>INV</c> activo,
/// <c>Accounting.VoucherType.NotAllowedForModule</c>. <see cref="OperacionDe"/> dice cuál: la del mensaje principal (el
/// comercial antes que el de costo) y, en un <c>DocumentoAnulado</c>, la de su original. (nuevo)
/// </summary>
public sealed class TiposDeComprobanteDeInventario(IApplicationDbContext db)
{
    /// <summary>El <c>CreatedBy</c> de lo que sembró la semilla paramétrica (<c>SeedContext.ParametricCreatedBy</c>).</summary>
    public const string CreadoPorLaSemilla = "system:seed";

    /// <summary>Los mensajes que nunca son el principal si la unidad trae otro: el costo y la devolución física.</summary>
    private static readonly HashSet<string> Secundarios = new(StringComparer.Ordinal)
    {
        CostoDeVentaReconocidoV1.Type,
        DevolucionRegistradaV1.Type,
    };

    public static bool EsDeInventario(VoucherUsage uso, string? modulo, bool activo) =>
        uso == VoucherUsage.Module && string.Equals(modulo, ModuloContable.Inventario, StringComparison.OrdinalIgnoreCase) && activo;

    /// <summary>Todos los mapeos vivos, con su tipo y su cruce, para resolver sin más consultas.</summary>
    public async Task<MapeosDeInventario> CargarAsync(CancellationToken ct)
    {
        var filas = await db.InventoryVoucherMappings.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new FilaDeMapeo(
                m.MappingKey, m.Operation,
                m.VoucherTypeId,
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.Code).FirstOrDefault() ?? string.Empty,
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.Usage).FirstOrDefault(),
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.ModuleCode).FirstOrDefault(),
                db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => v.IsActive && !v.IsDeleted).FirstOrDefault(),
                m.CrossDocumentTypeId,
                db.CrossDocumentTypes.Where(c => c.Id == m.CrossDocumentTypeId).Select(c => c.Code).FirstOrDefault()))
            .ToListAsync(ct);
        return new MapeosDeInventario(filas.ToDictionary(f => f.Clave, StringComparer.Ordinal));
    }

    /// <summary>
    /// La operación y el tipo de documento de una unidad: los de su mensaje principal, el primero emitido salvo que sea de
    /// costo o de devolución física y la unidad traiga otro (T28). En un <c>DocumentoAnulado</c>, la operación de su primer
    /// contenido y <c>voidedDocumentTypeCode</c>.
    /// </summary>
    public static OperacionDeLaUnidad? OperacionDe(IReadOnlyList<(IntegrationEnvelopeV1 Sobre, object? Contenido)> mensajes)
    {
        if (mensajes.Count == 0) return null;
        var principal = mensajes.Select((m, i) => (m, i))
            .OrderBy(x => Secundarios.Contains(x.m.Sobre.Type) ? 1 : 0).ThenBy(x => x.i)
            .First().m;

        if (principal.Contenido is DocumentoAnuladoV1 anulado)
        {
            var primero = anulado.VoidedContents.FirstOrDefault();
            var operacion = primero is null ? null : OperacionDelContenido(primero.Content);
            return operacion is null ? null : new OperacionDeLaUnidad(operacion, NoVacio(anulado.VoidedDocumentTypeCode));
        }

        var suya = OperacionDelContenido(principal.Contenido);
        return suya is null ? null : new OperacionDeLaUnidad(suya, NoVacio(principal.Sobre.Origin.DocumentTypeCode));
    }

    /// <summary>La <c>operation</c> de un contenido de negocio, tipado o como JSON (el contenido anulado llega sin tipo).</summary>
    public static string? OperacionDelContenido(object? contenido) => contenido switch
    {
        VentaFacturadaV1 x => x.Operation,
        CostoDeVentaReconocidoV1 x => x.Operation,
        CompraRecibidaV1 x => x.Operation,
        FacturaProveedorRegistradaV1 x => x.Operation,
        AjusteInventarioAprobadoV1 x => x.Operation,
        TrasladoDespachadoV1 x => x.Operation,
        TrasladoRecibidoV1 x => x.Operation,
        DevolucionRegistradaV1 x => x.Operation,
        AjusteDeCostoReconocidoV1 x => x.Operation,
        NotaCreditoEmitidaV1 x => x.Operation,
        NotaDebitoEmitidaV1 x => x.Operation,
        GrupoContableReclasificadoV1 x => x.Operation,
        MovimientoDeCajaRegistradoV1 x => x.Operation,
        DiferenciaDeArqueoAprobadaV1 x => x.Operation,
        JsonElement { ValueKind: JsonValueKind.Object } e when e.TryGetProperty("operation", out var op) && op.ValueKind == JsonValueKind.String => op.GetString(),
        System.Text.Json.Nodes.JsonObject o when o["operation"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) => s,
        _ => null,
    };

    private static string? NoVacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

internal sealed record FilaDeMapeo(
    string Clave, string Operation, int VoucherTypeId, string VoucherTypeCode, VoucherUsage Usage, string? ModuleCode, bool Activo,
    int? CrossDocumentTypeId, string? CrossDocumentTypeCode);

/// <summary>Los mapeos cargados (nuevo): <see cref="Resolver"/> aplica §2.6 sin más consultas.</summary>
public sealed class MapeosDeInventario
{
    private readonly IReadOnlyDictionary<string, FilaDeMapeo> _porClave;

    internal MapeosDeInventario(IReadOnlyDictionary<string, FilaDeMapeo> porClave) => _porClave = porClave;

    public Result<TipoDeLaUnidad> Resolver(OperacionDeLaUnidad unidad) => Resolver(unidad.Operation, unidad.InventoryDocumentTypeCode);

    public Result<TipoDeLaUnidad> Resolver(string operacion, string? tipoDeDocumento)
    {
        var fila = (tipoDeDocumento is null ? null : _porClave.GetValueOrDefault(InventoryVoucherMapping.ClaveDe(operacion, tipoDeDocumento)))
            ?? _porClave.GetValueOrDefault(InventoryVoucherMapping.ClaveDe(operacion, null));
        if (fila is null) return Result.Failure<TipoDeLaUnidad>(AccountingErrors.VoucherTypeNotFound($"de la operación {operacion}"));
        if (!TiposDeComprobanteDeInventario.EsDeInventario(fila.Usage, fila.ModuleCode, fila.Activo))
            return Result.Failure<TipoDeLaUnidad>(AccountingErrors.VoucherTypeNotAllowedForModule(fila.VoucherTypeCode, ModuloContable.Inventario));
        return Result.Success(new TipoDeLaUnidad(operacion, fila.VoucherTypeId, fila.VoucherTypeCode, fila.CrossDocumentTypeId, fila.CrossDocumentTypeCode));
    }
}
