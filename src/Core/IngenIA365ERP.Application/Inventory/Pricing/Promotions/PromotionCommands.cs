using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pricing.Promotions;

// Promociones (feature 012, I6, T873; contracts/api.md §19.4; data-model §14 «Promociones»; FR-055, F9). Toda escritura lleva motivo
// (IConMotivo) y clave de operación (IOperacionIdempotente). La regla de coherencia entre la clase y sus campos vive en
// ReglasDePromociones y la usan el validador y el handler. (nuevo)

/// <summary>
/// Un ámbito de la promoción en la entrada (§19.4 <c>scopes[]</c>): exactamente uno de los cuatro destinos; la clase se deduce de cuál
/// viene. <see cref="RequiredQuantity"/> sólo con producto y sólo en <see cref="PromotionKind.BundlePrice"/>. (nuevo)
/// </summary>
public sealed record PromotionScopeInput(
    Guid? ProductPublicId = null,
    Guid? CategoryPublicId = null,
    string? Segment = null,
    Guid? SalesChannelPublicId = null,
    decimal? RequiredQuantity = null)
{
    /// <summary>La clase del ámbito según el destino que trae; nulo si trae ninguno o más de uno.</summary>
    public PromotionScopeKind? Kind =>
        new (bool Trae, PromotionScopeKind Clase)[]
        {
            (ProductPublicId is not null, PromotionScopeKind.Product),
            (CategoryPublicId is not null, PromotionScopeKind.Category),
            (!string.IsNullOrWhiteSpace(Segment), PromotionScopeKind.Segment),
            (SalesChannelPublicId is not null, PromotionScopeKind.Channel),
        }.Where(x => x.Trae).Select(x => (PromotionScopeKind?)x.Clase).ToList() is [var una] ? una : null;
}

/// <summary>Un tramo del precio por cantidad (§19.4 <c>tiers[]</c>): desde <see cref="MinQuantity"/> (unidad base), <see cref="Price"/> sin impuestos. (nuevo)</summary>
public sealed record PromotionTierInput(decimal MinQuantity, decimal Price);

/// <summary>Un ámbito visto (§19.4), con el código del destino. (nuevo)</summary>
public sealed record PromotionScopeDto(
    PromotionScopeKind Kind,
    Guid? ProductPublicId,
    string? ProductCode,
    Guid? CategoryPublicId,
    string? CategoryCode,
    string? Segment,
    Guid? SalesChannelPublicId,
    string? SalesChannelCode,
    decimal? RequiredQuantity);

/// <summary>Un tramo visto (§19.4). (nuevo)</summary>
public sealed record PromotionTierDto(decimal MinQuantity, decimal Price);

/// <summary>
/// <c>PromotionDto</c> de §19.4. Los valores de la clase van en la cabecera, como en <c>INV_Promotions</c> —<see cref="Percent"/>
/// (fracción: 0,10 = 10 %), <see cref="Amount"/> (por unidad), <see cref="BuyQuantity"/>/<see cref="PayQuantity"/>,
/// <see cref="BundlePrice"/>— y <see cref="Tiers"/> son sólo los escalones del precio por cantidad. <see cref="InUse"/>: ya se aplicó
/// en un documento confirmado (la pantalla limita la edición). (nuevo)
/// </summary>
public sealed record PromotionDto(
    Guid PromotionPublicId,
    string Code,
    string Name,
    PromotionKind Kind,
    decimal? Percent,
    decimal? Amount,
    decimal? BuyQuantity,
    decimal? PayQuantity,
    decimal? BundlePrice,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    bool Cumulative,
    bool IsActive,
    string? Notes,
    bool InUse,
    IReadOnlyList<PromotionScopeDto> Scopes,
    IReadOnlyList<PromotionTierDto> Tiers);

/// <summary>Alta de una promoción (§19.4, <c>POST /api/inventory/promotions</c> → 201 <c>PromotionDto</c>). (nuevo)</summary>
public sealed record CreatePromotionCommand(
    string Code,
    string Name,
    PromotionKind Kind,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    string Reason,
    decimal? Percent = null,
    decimal? Amount = null,
    decimal? BuyQuantity = null,
    decimal? PayQuantity = null,
    decimal? BundlePrice = null,
    bool Cumulative = false,
    bool IsActive = true,
    IReadOnlyList<PromotionScopeInput>? Scopes = null,
    IReadOnlyList<PromotionTierInput>? Tiers = null,
    string? Notes = null)
    : IRequest<Result<PromotionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreatePromotionCommandValidator : ValidadorConMotivo<CreatePromotionCommand>
{
    public CreatePromotionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(Promotion.LargoDelCodigo).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Promotion.LargoDelNombre);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ValidFrom).NotEmpty();
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).WithMessage("La vigencia termina antes de empezar.");
        RuleFor(x => x.Notes).MaximumLength(Promotion.LargoDeLasNotas);
        RuleFor(x => x).Custom((x, ctx) =>
        {
            foreach (var problema in ReglasDePromociones.Coherencia(new MecanicaDePromocion(x.Kind, x.Percent, x.Amount, x.BuyQuantity, x.PayQuantity,
                         x.BundlePrice, x.Scopes ?? [], x.Tiers ?? [])))
                ctx.AddFailure(problema);
        });
    }
}

public sealed class CreatePromotionCommandHandler(IApplicationDbContext db) : IRequestHandler<CreatePromotionCommand, Result<PromotionDto>>
{
    public async Task<Result<PromotionDto>> Handle(CreatePromotionCommand request, CancellationToken ct)
    {
        var mecanica = new MecanicaDePromocion(request.Kind, request.Percent, request.Amount, request.BuyQuantity, request.PayQuantity, request.BundlePrice,
            request.Scopes ?? [], request.Tiers ?? []);
        if (ReglasDePromociones.Coherencia(mecanica) is [var primero, ..]) return Result.Failure<PromotionDto>(new Error(Error.Validation.Code, primero));
        if (request.ValidTo < request.ValidFrom) return Result.Failure<PromotionDto>(new Error(Error.Validation.Code, "La vigencia termina antes de empezar."));

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var existente = await db.Promotions.AsNoTracking().Where(p => !p.IsDeleted && p.Code == codigo).Select(p => new { p.PublicId, p.Name }).FirstOrDefaultAsync(ct);
        if (existente is not null) return Result.Failure<PromotionDto>(CodigoDeCatalogo.Duplicado("una promoción", codigo, existente.Name, existente.PublicId));

        var ambitos = await ReglasDePromociones.ResolverAmbitosAsync(db, mecanica.Scopes, ct);
        if (ambitos.IsFailure) return Result.Failure<PromotionDto>(ambitos.Error);

        var promocion = new Promotion
        {
            Code = codigo,
            Name = request.Name.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            IsActive = request.IsActive,
            IsCumulative = request.Cumulative,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        ReglasDePromociones.FijarMecanica(promocion, mecanica);
        foreach (var a in ambitos.Value) promocion.Scopes.Add(a);
        foreach (var t in mecanica.Tiers) promocion.Tiers.Add(new PromotionTier { MinQuantity = t.MinQuantity, UnitPrice = t.Price });
        db.Promotions.Add(promocion);
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDePromociones.ListarAsync(db, db.Promotions.AsNoTracking().Where(p => p.Id == promocion.Id), ct))[0]);
    }
}

/// <summary>
/// Edición de una promoción (§19.4, <c>PUT /{id}</c>): nombre, fin de vigencia y activo siempre; lo demás —clase, sus valores,
/// acumulable, inicio de vigencia, ámbitos, tramos y notas— sólo mientras no se haya aplicado en un documento confirmado (lo nulo no
/// se pidió cambiar). Si ya se aplicó y el cuerpo cambia algo de eso → <c>Inventory.Promotion.InUse</c> con los campos. Al cambiar la
/// clase, los valores de la anterior se limpian. El código no cambia. (nuevo)
/// </summary>
public sealed record UpdatePromotionCommand(
    Guid PromotionPublicId,
    string Name,
    DateOnly ValidTo,
    bool IsActive,
    string Reason,
    PromotionKind? Kind = null,
    decimal? Percent = null,
    decimal? Amount = null,
    decimal? BuyQuantity = null,
    decimal? PayQuantity = null,
    decimal? BundlePrice = null,
    bool? Cumulative = null,
    DateOnly? ValidFrom = null,
    IReadOnlyList<PromotionScopeInput>? Scopes = null,
    IReadOnlyList<PromotionTierInput>? Tiers = null,
    string? Notes = null)
    : IRequest<Result<PromotionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdatePromotionCommandValidator : ValidadorConMotivo<UpdatePromotionCommand>
{
    public UpdatePromotionCommandValidator()
    {
        RuleFor(x => x.PromotionPublicId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Promotion.LargoDelNombre);
        RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
        RuleFor(x => x.Notes).MaximumLength(Promotion.LargoDeLasNotas);
    }
}

public sealed class UpdatePromotionCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePromotionCommand, Result<PromotionDto>>
{
    public async Task<Result<PromotionDto>> Handle(UpdatePromotionCommand request, CancellationToken ct)
    {
        var promocion = await db.Promotions.FirstOrDefaultAsync(p => p.PublicId == request.PromotionPublicId && !p.IsDeleted, ct);
        if (promocion is null) return Result.Failure<PromotionDto>(ErroresDePrecios.PromotionNotFound());
        var ambitosVivos = await db.PromotionScopes.Where(s => s.PromotionId == promocion.Id && !s.IsDeleted).ToListAsync(ct);
        var tramosVivos = await db.PromotionTiers.Where(t => t.PromotionId == promocion.Id && !t.IsDeleted).ToListAsync(ct);

        // Lo pedido sobre lo que hay; una clase nueva limpia los valores de la anterior.
        var clase = request.Kind ?? promocion.Kind;
        var cambiaClase = clase != promocion.Kind;
        var mecanica = new MecanicaDePromocion(
            clase,
            request.Percent ?? (cambiaClase ? null : promocion.Rate),
            request.Amount ?? (cambiaClase ? null : promocion.Amount),
            request.BuyQuantity ?? (cambiaClase ? null : promocion.BuyQuantity),
            request.PayQuantity ?? (cambiaClase ? null : promocion.PayQuantity),
            request.BundlePrice ?? (cambiaClase ? null : promocion.BundlePrice),
            request.Scopes ?? [],
            request.Tiers ?? (cambiaClase ? [] : tramosVivos.Select(t => new PromotionTierInput(t.MinQuantity, t.UnitPrice)).ToList()));

        IReadOnlyList<PromotionScope>? nuevosAmbitos = null;
        if (request.Scopes is not null)
        {
            var resueltos = await ReglasDePromociones.ResolverAmbitosAsync(db, request.Scopes, ct);
            if (resueltos.IsFailure) return Result.Failure<PromotionDto>(resueltos.Error);
            nuevosAmbitos = resueltos.Value;
        }
        var desde = request.ValidFrom ?? promocion.ValidFrom;
        var cumulativa = request.Cumulative ?? promocion.IsCumulative;
        var notas = request.Notes is null ? promocion.Notes : string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        var cambios = new List<string>();
        if (cambiaClase) cambios.Add("kind");
        if (mecanica.Percent != promocion.Rate) cambios.Add("percent");
        if (mecanica.Amount != promocion.Amount) cambios.Add("amount");
        if (mecanica.BuyQuantity != promocion.BuyQuantity) cambios.Add("buyQuantity");
        if (mecanica.PayQuantity != promocion.PayQuantity) cambios.Add("payQuantity");
        if (mecanica.BundlePrice != promocion.BundlePrice) cambios.Add("bundlePrice");
        if (cumulativa != promocion.IsCumulative) cambios.Add("cumulative");
        if (desde != promocion.ValidFrom) cambios.Add("validFrom");
        if (notas != promocion.Notes) cambios.Add("notes");
        if (nuevosAmbitos is not null && !ReglasDePromociones.MismosAmbitos(ambitosVivos, nuevosAmbitos)) cambios.Add("scopes");
        if (!ReglasDePromociones.MismosTramos(tramosVivos, mecanica.Tiers)) cambios.Add("tiers");

        if (cambios.Count > 0 && await VistaDePromociones.EnUsoAsync(db, promocion.Id, ct))
            return Result.Failure<PromotionDto>(ErroresDePrecios.PromotionInUse(cambios));

        // La coherencia se mide con los ámbitos que quedarán.
        var ambitosFinales = nuevosAmbitos ?? ambitosVivos;
        var paraCoherencia = mecanica with
        {
            Scopes = request.Scopes ?? ambitosFinales.Select(a => new PromotionScopeInput(
                a.ProductId is null ? null : Guid.Empty, a.ProductCategoryId is null ? null : Guid.Empty, a.Segment,
                a.SalesChannelId is null ? null : Guid.Empty, a.RequiredQuantity)).ToList(),
        };
        if (ReglasDePromociones.Coherencia(paraCoherencia) is [var primero, ..]) return Result.Failure<PromotionDto>(new Error(Error.Validation.Code, primero));
        if (request.ValidTo < desde) return Result.Failure<PromotionDto>(new Error(Error.Validation.Code, "La vigencia termina antes de empezar."));

        promocion.Name = request.Name.Trim();
        promocion.ValidTo = request.ValidTo;
        promocion.IsActive = request.IsActive;
        promocion.ValidFrom = desde;
        promocion.IsCumulative = cumulativa;
        promocion.Notes = notas;
        promocion.Kind = clase;
        ReglasDePromociones.FijarMecanica(promocion, mecanica);
        if (nuevosAmbitos is not null && cambios.Contains("scopes"))
        {
            foreach (var viejo in ambitosVivos) viejo.IsDeleted = true;
            foreach (var nuevo in nuevosAmbitos)
            {
                nuevo.PromotionId = promocion.Id;
                db.PromotionScopes.Add(nuevo);
            }
        }
        if (cambios.Contains("tiers"))
        {
            foreach (var viejo in tramosVivos) viejo.IsDeleted = true;
            foreach (var t in mecanica.Tiers) db.PromotionTiers.Add(new PromotionTier { PromotionId = promocion.Id, MinQuantity = t.MinQuantity, UnitPrice = t.Price });
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await VistaDePromociones.ListarAsync(db, db.Promotions.AsNoTracking().Where(p => p.Id == promocion.Id), ct))[0]);
    }
}

/// <summary>Las promociones (§19.4, <c>GET /?asOf=&amp;active=</c>): vivas, vigentes a <see cref="AsOf"/> si viene, por código. (nuevo)</summary>
public sealed record ListPromotionsQuery(DateOnly? AsOf = null, bool? Active = null) : IRequest<Result<IReadOnlyList<PromotionDto>>>;

public sealed class ListPromotionsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListPromotionsQuery, Result<IReadOnlyList<PromotionDto>>>
{
    public async Task<Result<IReadOnlyList<PromotionDto>>> Handle(ListPromotionsQuery request, CancellationToken ct)
    {
        var consulta = db.Promotions.AsNoTracking().Where(p => !p.IsDeleted);
        if (request.AsOf is { } fecha) consulta = consulta.Where(p => p.ValidFrom <= fecha && p.ValidTo >= fecha);
        if (request.Active is { } activa) consulta = consulta.Where(p => p.IsActive == activa);
        return Result.Success(await VistaDePromociones.ListarAsync(db, consulta, ct));
    }
}

/// <summary>Una promoción (§19.4, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record GetPromotionQuery(Guid PromotionPublicId) : IRequest<Result<PromotionDto>>;

public sealed class GetPromotionQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPromotionQuery, Result<PromotionDto>>
{
    public async Task<Result<PromotionDto>> Handle(GetPromotionQuery request, CancellationToken ct)
    {
        var lista = await VistaDePromociones.ListarAsync(db, db.Promotions.AsNoTracking().Where(p => p.PublicId == request.PromotionPublicId && !p.IsDeleted), ct);
        return lista.Count == 0 ? Result.Failure<PromotionDto>(ErroresDePrecios.PromotionNotFound()) : Result.Success(lista[0]);
    }
}

// ================================================================================================ reglas --

/// <summary>La clase de una promoción con sus valores, ámbitos y tramos, tal como se piden (nuevo).</summary>
public sealed record MecanicaDePromocion(
    PromotionKind Kind,
    decimal? Percent,
    decimal? Amount,
    decimal? BuyQuantity,
    decimal? PayQuantity,
    decimal? BundlePrice,
    IReadOnlyList<PromotionScopeInput> Scopes,
    IReadOnlyList<PromotionTierInput> Tiers);

/// <summary>
/// La regla única de las promociones (feature 012, I6, T873): la coherencia entre la clase y sus campos (la usan el validador y el
/// handler, que puede llamarse sin la tubería), la resolución de los ámbitos a Id (el segmento es una clase de asociado existente,
/// T51) y la comparación de ámbitos y tramos para saber si una edición cambia la mecánica. (nuevo)
/// </summary>
public static class ReglasDePromociones
{
    private const int DecimalesDeTarifa = TopeDeDescuento.DecimalesDeTarifa;

    /// <summary>Los problemas de coherencia, en español; vacía si la mecánica es coherente.</summary>
    public static IReadOnlyList<string> Coherencia(MecanicaDePromocion m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var problemas = new List<string>();
        void Solo(bool lleno, string campo)
        {
            if (lleno) problemas.Add($"«{campo}» no va en una promoción de clase {m.Kind}.");
        }

        switch (m.Kind)
        {
            case PromotionKind.Percent:
                if (m.Percent is not { } tasa || tasa <= 0m || tasa > 1m || decimal.Round(tasa, DecimalesDeTarifa) != tasa)
                    problemas.Add("Un porcentaje es una fracción mayor que 0 y hasta 1 (0,10 = 10 %), con hasta seis decimales.");
                break;
            case PromotionKind.Amount:
                if (m.Amount is not > 0m) problemas.Add("Un descuento por valor necesita un valor por unidad mayor que 0.");
                break;
            case PromotionKind.BuyNPayM:
                if (m.BuyQuantity is not > 0m || m.PayQuantity is not > 0m || m.BuyQuantity <= m.PayQuantity)
                    problemas.Add("En «lleve N pague M» las dos cantidades son mayores que 0 y se lleva más de lo que se paga (3 y 2 en un 3×2).");
                break;
            case PromotionKind.QuantityPrice:
                if (m.Tiers.Count == 0) problemas.Add("El precio por cantidad necesita al menos un tramo.");
                if (m.Tiers.Any(t => t.MinQuantity <= 0m || t.Price < 0m)) problemas.Add("Cada tramo empieza en una cantidad mayor que 0 y tiene un precio no negativo.");
                if (m.Tiers.Select(t => t.MinQuantity).Distinct().Count() != m.Tiers.Count) problemas.Add("Dos tramos no empiezan en la misma cantidad.");
                break;
            case PromotionKind.BundlePrice:
                if (m.BundlePrice is not > 0m) problemas.Add("El precio de paquete necesita el precio del paquete, mayor que 0.");
                if (!m.Scopes.Any(s => s.Kind == PromotionScopeKind.Product && s.RequiredQuantity is > 0m))
                    problemas.Add("El paquete necesita los productos que lo forman con la cantidad de cada uno.");
                break;
            default:
                problemas.Add("La clase de promoción no existe.");
                break;
        }
        Solo(m.Kind != PromotionKind.Percent && m.Percent is not null, "percent");
        Solo(m.Kind != PromotionKind.Amount && m.Amount is not null, "amount");
        Solo(m.Kind != PromotionKind.BuyNPayM && (m.BuyQuantity is not null || m.PayQuantity is not null), "buyQuantity/payQuantity");
        Solo(m.Kind != PromotionKind.BundlePrice && m.BundlePrice is not null, "bundlePrice");
        Solo(m.Kind != PromotionKind.QuantityPrice && m.Tiers.Count > 0, "tiers");

        foreach (var s in m.Scopes)
        {
            if (s.Kind is null) problemas.Add("Cada ámbito nombra exactamente uno: producto, categoría, segmento o canal.");
            else if (s.RequiredQuantity is not null && (m.Kind != PromotionKind.BundlePrice || s.Kind != PromotionScopeKind.Product))
                problemas.Add("La cantidad del ámbito sólo va en un producto de un precio de paquete.");
            if (s.Segment is { Length: > PromotionScope.LargoDelSegmento }) problemas.Add($"El segmento admite hasta {PromotionScope.LargoDelSegmento} caracteres.");
        }
        return problemas;
    }

    /// <summary>Escribe la clase y sus valores en la promoción (los de otras clases quedan nulos).</summary>
    public static void FijarMecanica(Promotion p, MecanicaDePromocion m)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(m);
        p.Kind = m.Kind;
        p.Rate = m.Kind == PromotionKind.Percent ? m.Percent : null;
        p.Amount = m.Kind == PromotionKind.Amount ? m.Amount : null;
        p.BuyQuantity = m.Kind == PromotionKind.BuyNPayM ? m.BuyQuantity : null;
        p.PayQuantity = m.Kind == PromotionKind.BuyNPayM ? m.PayQuantity : null;
        p.BundlePrice = m.Kind == PromotionKind.BundlePrice ? m.BundlePrice : null;
    }

    /// <summary>Resuelve cada ámbito a su Id (sin guardar); un destino inexistente o un segmento desconocido es un error.</summary>
    public static async Task<Result<IReadOnlyList<PromotionScope>>> ResolverAmbitosAsync(IApplicationDbContext db, IReadOnlyList<PromotionScopeInput> entradas,
        CancellationToken ct)
    {
        var salida = new List<PromotionScope>(entradas.Count);
        IReadOnlyList<string>? segmentos = null;
        foreach (var e in entradas)
        {
            switch (e.Kind)
            {
                case PromotionScopeKind.Product:
                    var producto = await db.Products.AsNoTracking().Where(p => p.PublicId == e.ProductPublicId && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
                    if (producto is null) return Result.Failure<IReadOnlyList<PromotionScope>>(ErroresDePrecios.PromotionScopeTargetNotFound("producto", e.ProductPublicId!.Value));
                    salida.Add(new PromotionScope { ScopeKind = PromotionScopeKind.Product, ProductId = producto, RequiredQuantity = e.RequiredQuantity });
                    break;
                case PromotionScopeKind.Category:
                    var categoria = await db.ProductCategories.AsNoTracking().Where(c => c.PublicId == e.CategoryPublicId && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
                    if (categoria is null) return Result.Failure<IReadOnlyList<PromotionScope>>(ErroresDePrecios.PromotionScopeTargetNotFound("categoría", e.CategoryPublicId!.Value));
                    salida.Add(new PromotionScope { ScopeKind = PromotionScopeKind.Category, ProductCategoryId = categoria });
                    break;
                case PromotionScopeKind.Channel:
                    var canal = await db.SalesChannels.AsNoTracking().Where(c => c.PublicId == e.SalesChannelPublicId && !c.IsDeleted).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
                    if (canal is null) return Result.Failure<IReadOnlyList<PromotionScope>>(ErroresDePrecios.PromotionScopeTargetNotFound("canal", e.SalesChannelPublicId!.Value));
                    salida.Add(new PromotionScope { ScopeKind = PromotionScopeKind.Channel, SalesChannelId = canal });
                    break;
                case PromotionScopeKind.Segment:
                    var segmento = AmbitoDeLista.NormalizarSegmento(e.Segment)!;
                    segmentos ??= await ReglasDeListaDePrecios.SegmentosAsync(db, ct);
                    if (!segmentos.Contains(segmento, StringComparer.Ordinal))
                        return Result.Failure<IReadOnlyList<PromotionScope>>(ErroresDePrecios.SegmentUnknown(segmento, segmentos));
                    salida.Add(new PromotionScope { ScopeKind = PromotionScopeKind.Segment, Segment = segmento });
                    break;
                default:
                    return Result.Failure<IReadOnlyList<PromotionScope>>(new Error(Error.Validation.Code,
                        "Cada ámbito nombra exactamente uno: producto, categoría, segmento o canal."));
            }
        }
        return Result.Success<IReadOnlyList<PromotionScope>>(salida);
    }

    public static bool MismosAmbitos(IReadOnlyCollection<PromotionScope> a, IReadOnlyCollection<PromotionScope> b) =>
        Clave(a).SequenceEqual(Clave(b), StringComparer.Ordinal);

    public static bool MismosTramos(IReadOnlyCollection<PromotionTier> vivos, IReadOnlyCollection<PromotionTierInput> pedidos) =>
        vivos.Select(t => (t.MinQuantity, t.UnitPrice)).OrderBy(t => t.MinQuantity)
            .SequenceEqual(pedidos.Select(t => (t.MinQuantity, UnitPrice: t.Price)).OrderBy(t => t.MinQuantity));

    private static IEnumerable<string> Clave(IEnumerable<PromotionScope> ambitos) =>
        ambitos.Select(s => $"{s.ScopeKind}|{s.ProductId}|{s.ProductCategoryId}|{s.Segment}|{s.SalesChannelId}|{s.RequiredQuantity:0.####}")
            .Order(StringComparer.Ordinal);
}

/// <summary>La lectura de las promociones para la respuesta (§19.4) y si están en uso en un documento confirmado. (nuevo)</summary>
public static class VistaDePromociones
{
    /// <summary>¿Hay un descuento vivo de la promoción en un documento confirmado (o anulado, que lo estuvo)?</summary>
    public static Task<bool> EnUsoAsync(IApplicationDbContext db, int promotionId, CancellationToken ct) =>
        (from d in db.DocumentLineDiscounts.AsNoTracking()
         join doc in db.InventoryDocuments.AsNoTracking() on d.DocumentId equals doc.Id
         where d.PromotionId == promotionId && !d.IsDeleted && (doc.Status == DocumentStatus.Confirmed || doc.Status == DocumentStatus.Voided)
         select d.Id).AnyAsync(ct);

    public static async Task<IReadOnlyList<PromotionDto>> ListarAsync(IApplicationDbContext db, IQueryable<Promotion> consulta, CancellationToken ct)
    {
        var promociones = await consulta.OrderBy(p => p.Code).ToListAsync(ct);
        if (promociones.Count == 0) return [];
        var ids = promociones.Select(p => p.Id).ToList();
        var ambitos = (await db.PromotionScopes.AsNoTracking().Where(s => ids.Contains(s.PromotionId) && !s.IsDeleted).ToListAsync(ct)).ToLookup(s => s.PromotionId);
        var tramos = (await db.PromotionTiers.AsNoTracking().Where(t => ids.Contains(t.PromotionId) && !t.IsDeleted).ToListAsync(ct)).ToLookup(t => t.PromotionId);
        var enUso = (await (from d in db.DocumentLineDiscounts.AsNoTracking()
                            join doc in db.InventoryDocuments.AsNoTracking() on d.DocumentId equals doc.Id
                            where d.PromotionId != null && ids.Contains(d.PromotionId.Value) && !d.IsDeleted
                                  && (doc.Status == DocumentStatus.Confirmed || doc.Status == DocumentStatus.Voided)
                            select d.PromotionId!.Value).Distinct().ToListAsync(ct)).ToHashSet();

        var productoIds = ambitos.SelectMany(g => g).Select(s => s.ProductId).OfType<int>().Distinct().ToList();
        var categoriaIds = ambitos.SelectMany(g => g).Select(s => s.ProductCategoryId).OfType<int>().Distinct().ToList();
        var canalIds = ambitos.SelectMany(g => g).Select(s => s.SalesChannelId).OfType<int>().Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => (p.PublicId, p.Code), ct);
        var categorias = await db.ProductCategories.AsNoTracking().Where(c => categoriaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.PublicId, c.Code), ct);
        var canales = await db.SalesChannels.AsNoTracking().Where(c => canalIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.PublicId, c.Code), ct);

        return promociones.Select(p => new PromotionDto(p.PublicId, p.Code, p.Name, p.Kind, p.Rate, p.Amount, p.BuyQuantity, p.PayQuantity, p.BundlePrice,
                p.ValidFrom, p.ValidTo, p.IsCumulative, p.IsActive, p.Notes, enUso.Contains(p.Id),
                ambitos[p.Id].OrderBy(s => s.ScopeKind).ThenBy(s => s.Id).Select(s => new PromotionScopeDto(s.ScopeKind,
                    s.ProductId is int pi ? productos[pi].PublicId : null, s.ProductId is int pc ? productos[pc].Code : null,
                    s.ProductCategoryId is int ci ? categorias[ci].PublicId : null, s.ProductCategoryId is int cc ? categorias[cc].Code : null,
                    s.Segment,
                    s.SalesChannelId is int si ? canales[si].PublicId : null, s.SalesChannelId is int sc ? canales[sc].Code : null,
                    s.RequiredQuantity)).ToList(),
                tramos[p.Id].OrderBy(t => t.MinQuantity).Select(t => new PromotionTierDto(t.MinQuantity, t.UnitPrice)).ToList()))
            .ToList();
    }
}
