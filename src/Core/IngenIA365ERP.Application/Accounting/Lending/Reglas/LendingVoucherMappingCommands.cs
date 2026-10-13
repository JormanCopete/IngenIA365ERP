using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

// Tipos de comprobante de Cartera (feature 013, T0642, T54; contracts/contabilidad.md §4.2 y §5; api.md §23.2): el mapeo hecho (y clase de
// recaudador) → tipo de comprobante y documento cruce, su consulta y el resolutor del tipo de una unidad. Molde: InventoryVoucherMappingCommands. (nuevo)

/// <summary><c>LendingVoucherMappingDto</c> de api.md §23.2. (nuevo)</summary>
public sealed record LendingVoucherMappingDto(
    Guid MappingPublicId,
    string Operation,
    string CollectorClass,
    TipoDelMapeoDeCarteraDto VoucherType,
    CruceDelMapeoDeCarteraDto? CrossDocumentType,
    bool IsSeeded,
    bool ReviewPending);

public sealed record TipoDelMapeoDeCarteraDto(Guid PublicId, string Code, string Name);

public sealed record CruceDelMapeoDeCarteraDto(Guid PublicId, string Code);

// ============================================================================================================ guardar --

/// <summary>
/// <c>PUT /api/accounting/lending/voucher-mappings</c>: agrega o reemplaza, en su sitio, la fila de <c>(hecho, clase de recaudador)</c>
/// (<c>*</c> o vacío = toda la operación). El tipo de comprobante tiene que ser <c>Usage = Module</c>, <c>ModuleCode = CAR</c> y activo
/// (<c>Accounting.VoucherType.NotAllowedForModule</c>). Guardarlo es la conformidad de la contadora: deja de estar «pendiente de validar».
/// Con motivo (el diff queda en la auditoría: el mapeo no tiene vigencia). (nuevo)
/// </summary>
public sealed record SetLendingVoucherMappingCommand(
    string Operation,
    string? CollectorClass,
    Guid VoucherTypePublicId,
    Guid? CrossDocumentTypePublicId,
    string Reason) : IRequest<Result<Guid>>, IConMotivo;

public sealed class SetLendingVoucherMappingCommandValidator : ValidadorConMotivo<SetLendingVoucherMappingCommand>
{
    public SetLendingVoucherMappingCommandValidator()
    {
        RuleFor(x => x.Operation).NotEmpty().WithMessage("Indique el hecho.")
            .Must(o => OperacionesDeCartera.Buscar(o) is { EsInformativo: false })
            .WithMessage(x => $"«{x.Operation}» no es un hecho de Cartera con comprobante.");
        RuleFor(x => x.CollectorClass).MaximumLength(20)
            .Must(c => string.IsNullOrWhiteSpace(c) || c.Trim() == LendingVoucherMapping.Cualquiera || R.ClasesDeRecaudador.Contains(c.Trim(), StringComparer.Ordinal))
            .WithMessage(x => $"«{x.CollectorClass}» no es una clase de recaudador.");
        RuleFor(x => x.VoucherTypePublicId).NotEmpty().WithMessage("Indique el tipo de comprobante.");
    }
}

public sealed class SetLendingVoucherMappingCommandHandler(IApplicationDbContext db, ICurrentUserService usuario, IDateTimeService reloj)
    : IRequestHandler<SetLendingVoucherMappingCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(SetLendingVoucherMappingCommand request, CancellationToken ct)
    {
        var operacion = OperacionesDeCartera.Buscar(request.Operation);
        if (operacion is null || operacion.EsInformativo) return Result.Failure<Guid>(AccountingErrors.LendingRuleOperationUnknown(request.Operation));

        var tipo = await db.VoucherTypes.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.VoucherTypePublicId && !v.IsDeleted, ct);
        if (tipo is null) return Result.Failure<Guid>(AccountingErrors.VoucherTypeNotFound(request.VoucherTypePublicId.ToString()));
        if (!TiposDeComprobanteDeCartera.EsDeCartera(tipo.Usage, tipo.ModuleCode, tipo.IsActive))
            return Result.Failure<Guid>(AccountingErrors.VoucherTypeNotAllowedForModule(tipo.Code, ModuloContable.Cartera));

        int? cruce = null;
        if (request.CrossDocumentTypePublicId is { } cp)
        {
            cruce = await db.CrossDocumentTypes.AsNoTracking().Where(c => c.PublicId == cp && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
            if (cruce is null) return Result.Failure<Guid>(AccountingErrors.CrossDocumentTypeNotFound);
        }

        var clave = LendingVoucherMapping.ClaveDe(operacion.Codigo, request.CollectorClass);
        var mapeo = await db.LendingVoucherMappings.FirstOrDefaultAsync(m => m.MappingKey == clave && !m.IsDeleted, ct);
        if (mapeo is null)
        {
            mapeo = new LendingVoucherMapping(operacion.Codigo, request.CollectorClass, tipo.Id, cruce);
            db.LendingVoucherMappings.Add(mapeo);
        }
        else
        {
            mapeo.VoucherTypeId = tipo.Id;
            mapeo.CrossDocumentTypeId = cruce;
        }
        mapeo.Validar(reloj.UtcNow, usuario.UserName ?? "(sin usuario)");
        await db.SaveChangesAsync(ct);
        return Result.Success(mapeo.PublicId);
    }
}

// ============================================================================================================ consulta --

public sealed record ListLendingVoucherMappingsQuery : IRequest<Result<IReadOnlyList<LendingVoucherMappingDto>>>;

public sealed class ListLendingVoucherMappingsQueryValidator : AbstractValidator<ListLendingVoucherMappingsQuery>;

public sealed class ListLendingVoucherMappingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListLendingVoucherMappingsQuery, Result<IReadOnlyList<LendingVoucherMappingDto>>>
{
    public async Task<Result<IReadOnlyList<LendingVoucherMappingDto>>> Handle(ListLendingVoucherMappingsQuery request, CancellationToken ct)
    {
        var filas = await db.LendingVoucherMappings.AsNoTracking().Where(m => !m.IsDeleted)
            .Select(m => new
            {
                m.PublicId, m.Operation, m.CollectorClass, m.IsSeeded, m.ValidatedAt,
                Tipo = db.VoucherTypes.Where(v => v.Id == m.VoucherTypeId).Select(v => new TipoDelMapeoDeCarteraDto(v.PublicId, v.Code, v.Name)).FirstOrDefault(),
                Cruce = db.CrossDocumentTypes.Where(c => c.Id == m.CrossDocumentTypeId).Select(c => new CruceDelMapeoDeCarteraDto(c.PublicId, c.Code)).FirstOrDefault(),
            })
            .ToListAsync(ct);
        var orden = OperacionesDeCartera.Todas.Select((o, i) => (o.Codigo, i)).ToDictionary(x => x.Codigo, x => x.i, StringComparer.Ordinal);
        var items = filas
            .OrderBy(f => orden.GetValueOrDefault(f.Operation, int.MaxValue)).ThenBy(f => f.CollectorClass ?? string.Empty)
            .Select(f => new LendingVoucherMappingDto(f.PublicId, f.Operation, f.CollectorClass ?? LendingVoucherMapping.Cualquiera,
                f.Tipo ?? new TipoDelMapeoDeCarteraDto(Guid.Empty, string.Empty, string.Empty), f.Cruce, f.IsSeeded, f.ValidatedAt is null))
            .ToList();
        return Result.Success<IReadOnlyList<LendingVoucherMappingDto>>(items);
    }
}

// ============================================================================================================ resolutor --

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
