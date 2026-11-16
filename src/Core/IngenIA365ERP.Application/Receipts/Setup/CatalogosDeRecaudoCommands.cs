using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, US1 (T0072; FR-026, FR-027, FR-179; contracts/api.md §2; data-model §1.6, §1.7): los tipos de documento y los conceptos varios de Recaudo.
// La semilla de RecaudoNucleo deja un tipo por clase de R0 y los conceptos del sistema (T0075). (nuevo)

// ---------------------------------------------------------------------------------------------------- tipos de documento --

/// <summary><c>GET /api/receipts/document-types</c>. (nuevo)</summary>
public sealed record ListReceiptDocumentTypesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<ReceiptDocumentTypeDto>>>;

public sealed class ListReceiptDocumentTypesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListReceiptDocumentTypesQuery, Result<IReadOnlyList<ReceiptDocumentTypeDto>>>
{
    public async Task<Result<IReadOnlyList<ReceiptDocumentTypeDto>>> Handle(ListReceiptDocumentTypesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<ReceiptDocumentTypeDto>>(await db.ReceiptDocumentTypes.AsNoTracking()
            .Where(t => request.IncludeInactive || t.Status == ReceiptsCatalogStatus.Active)
            .OrderBy(t => t.Kind).ThenBy(t => t.Code)
            .Select(t => new ReceiptDocumentTypeDto(t.PublicId, t.Code, t.Name, t.Kind, t.UsesPointPrefix, t.Status))
            .ToListAsync(ct));
}

/// <summary>
/// <c>POST /api/receipts/document-types</c> (<c>Receipts.Setup.Manage</c>): un tipo de documento; a lo sumo uno activo por clase
/// (<c>Receipts.DocumentType.KindTaken</c>): es el que numera los documentos de esa clase. (nuevo)
/// </summary>
public sealed record CreateReceiptDocumentTypeCommand(string Code, string Name, ReceiptDocumentKind Kind, bool UsesPointPrefix)
    : IRequest<Result<ReceiptDocumentTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateReceiptDocumentTypeCommandValidator : AbstractValidator<CreateReceiptDocumentTypeCommand>
{
    public CreateReceiptDocumentTypeCommandValidator()
    {
        ReglasDeCodigo.Codigo(RuleFor(x => x.Code));
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.Kind).IsInEnum();
    }
}

public sealed class CreateReceiptDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateReceiptDocumentTypeCommand, Result<ReceiptDocumentTypeDto>>
{
    public async Task<Result<ReceiptDocumentTypeDto>> Handle(CreateReceiptDocumentTypeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.ReceiptDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == codigo, ct) is { } existente)
            return Result.Failure<ReceiptDocumentTypeDto>(CodigoDeCatalogo.Duplicado("un tipo de documento de Recaudo", codigo, existente.Name, existente.PublicId));
        if (await db.ReceiptDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Kind == request.Kind && t.Status == ReceiptsCatalogStatus.Active, ct) is { } ocupada)
            return Result.Failure<ReceiptDocumentTypeDto>(ErroresDeRecaudo.TipoDeClaseOcupada(ocupada.Code));
        var tipo = new ReceiptDocumentType { Code = codigo, Name = request.Name.Trim(), Kind = request.Kind, UsesPointPrefix = request.UsesPointPrefix };
        db.ReceiptDocumentTypes.Add(tipo);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReceiptDocumentTypeDto(tipo.PublicId, tipo.Code, tipo.Name, tipo.Kind, tipo.UsesPointPrefix, tipo.Status));
    }
}

/// <summary><c>PUT /api/receipts/document-types/{id}</c>: nombre, prefijo del punto y estado; el código y la clase no cambian (son la numeración). (nuevo)</summary>
public sealed record UpdateReceiptDocumentTypeCommand(Guid PublicId, string Name, bool UsesPointPrefix, ReceiptsCatalogStatus Status)
    : IRequest<Result<ReceiptDocumentTypeDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateReceiptDocumentTypeCommandValidator : AbstractValidator<UpdateReceiptDocumentTypeCommand>
{
    public UpdateReceiptDocumentTypeCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public sealed class UpdateReceiptDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateReceiptDocumentTypeCommand, Result<ReceiptDocumentTypeDto>>
{
    public async Task<Result<ReceiptDocumentTypeDto>> Handle(UpdateReceiptDocumentTypeCommand request, CancellationToken ct)
    {
        var tipo = await db.ReceiptDocumentTypes.FirstOrDefaultAsync(t => t.PublicId == request.PublicId, ct);
        if (tipo is null) return Result.Failure<ReceiptDocumentTypeDto>(ErroresDeRecaudo.NoEncontrado("El tipo de documento"));
        if (request.Status == ReceiptsCatalogStatus.Active && tipo.Status != ReceiptsCatalogStatus.Active
            && await db.ReceiptDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Kind == tipo.Kind && t.Status == ReceiptsCatalogStatus.Active && t.Id != tipo.Id, ct) is { } ocupada)
            return Result.Failure<ReceiptDocumentTypeDto>(ErroresDeRecaudo.TipoDeClaseOcupada(ocupada.Code));
        tipo.Name = request.Name.Trim();
        tipo.UsesPointPrefix = request.UsesPointPrefix;
        tipo.Status = request.Status;
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReceiptDocumentTypeDto(tipo.PublicId, tipo.Code, tipo.Name, tipo.Kind, tipo.UsesPointPrefix, tipo.Status));
    }
}

// ------------------------------------------------------------------------------------------------------- conceptos varios --

/// <summary><c>GET /api/receipts/misc-concepts</c>. (nuevo)</summary>
public sealed record ListMiscConceptsQuery(bool IncludeInactive = false, MiscConceptDirection? Direction = null) : IRequest<Result<IReadOnlyList<MiscConceptDto>>>;

/// <remarks>2026-11-16: con el tercero fijo de cada concepto y la cuenta de la matriz de Recaudo a hoy (por <see cref="IContabilidadParaRecaudo"/>).</remarks>
public sealed class ListMiscConceptsQueryHandler(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad = null, IDateTimeService? reloj = null)
    : IRequestHandler<ListMiscConceptsQuery, Result<IReadOnlyList<MiscConceptDto>>>
{
    public async Task<Result<IReadOnlyList<MiscConceptDto>>> Handle(ListMiscConceptsQuery request, CancellationToken ct)
    {
        var conceptos = await db.MiscConcepts.AsNoTracking()
            .Where(c => request.IncludeInactive || c.Status == ReceiptsCatalogStatus.Active)
            .Where(c => request.Direction == null || c.Direction == request.Direction)
            .OrderBy(c => c.Code).ToListAsync(ct);
        return Result.Success(await ConceptosDeRecaudo.DtosAsync(db, contabilidad, reloj, conceptos, ct));
    }
}

internal static class ConceptosDeRecaudo
{
    public static MiscConceptDto Dto(MiscConcept c, TerceroDeConcepto? fijo = null, CuentaDeConceptoVarioDto? cuenta = null) => new(c.PublicId, c.Code, c.Name,
        c.Direction, c.RequiresThirdParty, c.RequiresCrossDocument, c.RequiresInvoice, c.RequiresApproval, c.MaxAmount, c.IsSystem, c.Status, c.ThirdPartyMode,
        fijo?.PublicId, fijo?.Nombre, c.SuggestedAmount, cuenta?.AccountCode, cuenta?.AccountName);

    /// <summary>
    /// 2026-11-16: los conceptos con su tercero fijo y la cuenta a la que van según la matriz de Recaudo a hoy (informativa: la pregunta a Contabilidad por su puerto;
    /// sin él, nula).
    /// </summary>
    public static async Task<IReadOnlyList<MiscConceptDto>> DtosAsync(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad, IDateTimeService? reloj,
        IReadOnlyList<MiscConcept> conceptos, CancellationToken ct)
    {
        var fijos = await TercerosDeConceptos.FijosAsync(db, conceptos, ct);
        IReadOnlyDictionary<string, CuentaDeConceptoVarioDto> cuentas = contabilidad is null || conceptos.Count == 0
            ? new Dictionary<string, CuentaDeConceptoVarioDto>()
            : await contabilidad.CuentasDeConceptosVariosAsync(conceptos.Select(c => c.Code).ToList(),
                reloj?.HoyLocal ?? DateOnly.FromDateTime(DateTime.Now), ct);
        return conceptos.Select(c => Dto(c, fijos.GetValueOrDefault(c.Id), cuentas.GetValueOrDefault(c.Code))).ToList();
    }

    public static async Task<MiscConceptDto> DtoAsync(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad, IDateTimeService? reloj, MiscConcept c,
        CancellationToken ct) => (await DtosAsync(db, contabilidad, reloj, [c], ct))[0];

    /// <summary>
    /// 2026-11-16: el tercero fijo pedido, por su <c>PublicId</c>: obligatorio con «tercero fijo» y descartado en los demás modos (un concepto que deja de tener
    /// tercero fijo lo suelta). Nulo sin tercero fijo; el error si la persona no existe.
    /// </summary>
    public static async Task<Result<int?>> TerceroFijoAsync(IApplicationDbContext db, MiscConceptThirdPartyMode modo, Guid? publico, CancellationToken ct)
    {
        if (modo != MiscConceptThirdPartyMode.FixedThirdParty) return Result.Success<int?>(null);
        var id = publico is { } p ? await db.People.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) : null;
        return id is null ? Result.Failure<int?>(ErroresDeRecaudo.PersonaNoEncontrada()) : Result.Success(id);
    }
}

/// <summary>2026-11-16: las reglas comunes de crear y editar un concepto vario sobre su tercero y su valor sugerido. (nuevo)</summary>
internal static class ReglasDelTerceroDeConcepto
{
    public static void Aplicar<T>(AbstractValidator<T> v, System.Linq.Expressions.Expression<Func<T, MiscConceptThirdPartyMode>> modo,
        System.Linq.Expressions.Expression<Func<T, Guid?>> tercero, System.Linq.Expressions.Expression<Func<T, decimal?>> sugerido, Func<T, decimal?> maximo)
    {
        var elModo = modo.Compile();
        v.RuleFor(modo).IsInEnum().WithMessage("Diga cómo se elige el tercero del concepto.");
        v.RuleFor(tercero).NotEmpty().When(x => elModo(x) == MiscConceptThirdPartyMode.FixedThirdParty).WithMessage("Indique el tercero fijo del concepto.");
        v.RuleFor(sugerido).GreaterThanOrEqualTo(0m).WithMessage("El valor sugerido no puede ser negativo.");
        v.RuleFor(sugerido).Must((x, s) => s is null || maximo(x) is not { } m || s <= m)
            .WithMessage("El valor sugerido no puede pasar del valor máximo del concepto.");
    }
}

/// <summary>
/// <c>POST /api/receipts/misc-concepts</c> (<c>Receipts.Setup.Manage</c>): un concepto de ingreso o egreso no ligado a un producto. Uno gravado se marca
/// «exige factura» y no se cobra sin la factura de Comercial como documento cruce (FR-179, US1-5). (nuevo)
/// </summary>
/// <remarks>
/// 2026-11-16 (decisión del dueño): cómo se elige el tercero (<see cref="ThirdPartyMode"/>: quien paga, el tercero fijo <see cref="DefaultThirdPartyPublicId"/>
/// —obligatorio en ese modo; la persona tiene que existir— o el que elige el cajero) y el valor que la caja propone (<see cref="SuggestedAmount"/>, de cero al máximo).
/// </remarks>
public sealed record CreateMiscConceptCommand(
    string Code, string Name, MiscConceptDirection Direction, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresInvoice,
    bool RequiresApproval, decimal? MaxAmount, MiscConceptThirdPartyMode ThirdPartyMode = MiscConceptThirdPartyMode.Payer, Guid? DefaultThirdPartyPublicId = null,
    decimal? SuggestedAmount = null) : IRequest<Result<MiscConceptDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateMiscConceptCommandValidator : AbstractValidator<CreateMiscConceptCommand>
{
    public CreateMiscConceptCommandValidator()
    {
        ReglasDeCodigo.Codigo(RuleFor(x => x.Code));
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.MaxAmount).GreaterThan(0).When(x => x.MaxAmount is not null);
        ReglasDelTerceroDeConcepto.Aplicar(this, x => x.ThirdPartyMode, x => x.DefaultThirdPartyPublicId, x => x.SuggestedAmount, x => x.MaxAmount);
    }
}

public sealed class CreateMiscConceptCommandHandler(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad = null, IDateTimeService? reloj = null)
    : IRequestHandler<CreateMiscConceptCommand, Result<MiscConceptDto>>
{
    public async Task<Result<MiscConceptDto>> Handle(CreateMiscConceptCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.MiscConcepts.AsNoTracking().FirstOrDefaultAsync(c => c.Code == codigo, ct) is { } existente)
            return Result.Failure<MiscConceptDto>(CodigoDeCatalogo.Duplicado("un concepto vario", codigo, existente.Name, existente.PublicId));
        var tercero = await ConceptosDeRecaudo.TerceroFijoAsync(db, request.ThirdPartyMode, request.DefaultThirdPartyPublicId, ct);
        if (tercero.IsFailure) return Result.Failure<MiscConceptDto>(tercero.Error);
        var concepto = new MiscConcept
        {
            Code = codigo,
            Name = request.Name.Trim(),
            Direction = request.Direction,
            RequiresThirdParty = request.RequiresThirdParty || request.RequiresInvoice,
            RequiresCrossDocument = request.RequiresCrossDocument || request.RequiresInvoice,
            RequiresInvoice = request.RequiresInvoice,
            RequiresApproval = request.RequiresApproval,
            MaxAmount = request.MaxAmount,
            ThirdPartyMode = request.ThirdPartyMode,
            DefaultThirdPartyPersonId = tercero.Value,
            SuggestedAmount = request.SuggestedAmount,
        };
        db.MiscConcepts.Add(concepto);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConceptosDeRecaudo.DtoAsync(db, contabilidad, reloj, concepto, ct));
    }
}

/// <summary><c>PUT /api/receipts/misc-concepts/{id}</c>: todo menos el código y el sentido; un concepto del sistema conserva su forma. (nuevo)</summary>
/// <remarks>2026-11-16: también cómo se elige el tercero, el tercero fijo y el valor sugerido, con las reglas de <see cref="CreateMiscConceptCommand"/>.</remarks>
public sealed record UpdateMiscConceptCommand(
    Guid PublicId, string Name, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresInvoice, bool RequiresApproval, decimal? MaxAmount,
    MiscConceptThirdPartyMode ThirdPartyMode = MiscConceptThirdPartyMode.Payer, Guid? DefaultThirdPartyPublicId = null, decimal? SuggestedAmount = null)
    : IRequest<Result<MiscConceptDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateMiscConceptCommandValidator : AbstractValidator<UpdateMiscConceptCommand>
{
    public UpdateMiscConceptCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre.").MaximumLength(150);
        RuleFor(x => x.MaxAmount).GreaterThan(0).When(x => x.MaxAmount is not null);
        ReglasDelTerceroDeConcepto.Aplicar(this, x => x.ThirdPartyMode, x => x.DefaultThirdPartyPublicId, x => x.SuggestedAmount, x => x.MaxAmount);
    }
}

public sealed class UpdateMiscConceptCommandHandler(IApplicationDbContext db, IContabilidadParaRecaudo? contabilidad = null, IDateTimeService? reloj = null)
    : IRequestHandler<UpdateMiscConceptCommand, Result<MiscConceptDto>>
{
    public async Task<Result<MiscConceptDto>> Handle(UpdateMiscConceptCommand request, CancellationToken ct)
    {
        var concepto = await db.MiscConcepts.FirstOrDefaultAsync(c => c.PublicId == request.PublicId, ct);
        if (concepto is null) return Result.Failure<MiscConceptDto>(ErroresDeRecaudo.NoEncontrado("El concepto"));
        var tercero = await ConceptosDeRecaudo.TerceroFijoAsync(db, request.ThirdPartyMode, request.DefaultThirdPartyPublicId, ct);
        if (tercero.IsFailure) return Result.Failure<MiscConceptDto>(tercero.Error);
        concepto.Name = request.Name.Trim();
        concepto.RequiresThirdParty = request.RequiresThirdParty || request.RequiresInvoice;
        concepto.RequiresCrossDocument = request.RequiresCrossDocument || request.RequiresInvoice;
        concepto.RequiresInvoice = request.RequiresInvoice;
        concepto.RequiresApproval = request.RequiresApproval;
        concepto.MaxAmount = request.MaxAmount;
        concepto.ThirdPartyMode = request.ThirdPartyMode;
        concepto.DefaultThirdPartyPersonId = tercero.Value;
        concepto.SuggestedAmount = request.SuggestedAmount;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConceptosDeRecaudo.DtoAsync(db, contabilidad, reloj, concepto, ct));
    }
}

/// <summary><c>POST /api/receipts/misc-concepts/{id}/inactivate</c>: los del sistema no se inactivan (<c>Receipts.MiscConcept.SystemConcept</c>). (nuevo)</summary>
public sealed record InactivateMiscConceptCommand(Guid PublicId) : IRequest<Result<MiscConceptDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class InactivateMiscConceptCommandHandler(IApplicationDbContext db) : IRequestHandler<InactivateMiscConceptCommand, Result<MiscConceptDto>>
{
    public async Task<Result<MiscConceptDto>> Handle(InactivateMiscConceptCommand request, CancellationToken ct)
    {
        var concepto = await db.MiscConcepts.FirstOrDefaultAsync(c => c.PublicId == request.PublicId, ct);
        if (concepto is null) return Result.Failure<MiscConceptDto>(ErroresDeRecaudo.NoEncontrado("El concepto"));
        if (concepto.IsSystem) return Result.Failure<MiscConceptDto>(ErroresDeRecaudo.ConceptoDelSistema());
        concepto.Status = ReceiptsCatalogStatus.Inactive;
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConceptosDeRecaudo.DtoAsync(db, null, null, concepto, ct));
    }
}
