using System.Reflection;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Exogenous;

// La parametrización de la exógena por año gravable (feature 009 E4, US11, FR-071): formatos con su disposición, conceptos y cuentas.
// Un año se llena de tres maneras, todas sin pisar lo que ya existe: la semilla del año (Semillas/exogena-{año}.json, pendiente de
// validar), la copia del año anterior o a mano. «No aplica» deja el formato sin generar.

/// <summary>La semilla embebida de un año: formatos, columnas, conceptos y cuentas propuestas por catálogo.</summary>
public static class SemillaDeExogena
{
    public sealed record Cuenta(string Field, string Selector, IReadOnlyList<string>? Solidario, IReadOnlyList<string>? Comercial);
    public sealed record Concepto(string Code, string Name, IReadOnlyList<Cuenta> Accounts);
    public sealed record Formato(string Code, int Version, string Name, string Element, string? ConceptAttribute, decimal MinAmount, bool MinorAmountsRule,
        IReadOnlyList<ColumnaDeExogena> Columns, IReadOnlyList<Concepto> Concepts);
    public sealed record Archivo(int TaxYear, bool Verified, string MinorAmountsTaxId, IReadOnlyList<string> Identity, IReadOnlyList<Formato> Formats);

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web) { ReadCommentHandling = JsonCommentHandling.Skip };

    /// <summary>La semilla del año, o nula si no la hay.</summary>
    public static Archivo? DelAnio(int anio)
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var nombre = ensamblado.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith($".exogena-{anio}.json", StringComparison.OrdinalIgnoreCase));
        if (nombre is null) return null;
        using var stream = ensamblado.GetManifestResourceStream(nombre)!;
        return JsonSerializer.Deserialize<Archivo>(stream, Opciones);
    }

    /// <summary>Los prefijos de una cuenta de la semilla para el catálogo de la empresa (vacío si el catálogo no es ninguno de los dos).</summary>
    public static IReadOnlyList<string> Prefijos(Cuenta c, string? catalogo) =>
        string.Equals(catalogo, "PUC-SOLIDARIO", StringComparison.OrdinalIgnoreCase) ? c.Solidario ?? []
        : string.Equals(catalogo, "PUC-COMERCIAL", StringComparison.OrdinalIgnoreCase) ? c.Comercial ?? []
        : [];
}

internal static class ArmadoDeExogena
{
    public static FormatoDeExogenaDto Dto(ExogenousFormat f) => new(f.PublicId, f.TaxYear, f.FormatCode, f.FormatVersion, f.Name, f.Applies, f.Origin.ToString(),
        f.MinAmount, f.MinorAmountsRule, f.MinorAmountsTaxId, DisposicionDeExogena.Leer(f.LayoutJson),
        f.Concepts.Where(c => !c.IsDeleted).OrderBy(c => c.ConceptCode)
            .Select(c => new ConceptoDeExogenaDto(c.ConceptCode, c.Name,
                c.Accounts.Where(a => !a.IsDeleted).OrderBy(a => a.ValueField).ThenBy(a => a.AccountPrefix)
                    .Select(a => new CuentaDeConceptoDto(a.ValueField, a.AccountPrefix, a.Selector)).ToList()))
            .ToList());

    public static Task<List<ExogenousFormat>> DelAnioAsync(IApplicationDbContext db, int anio, bool seguimiento, CancellationToken ct)
    {
        var q = db.ExogenousFormats.Include(f => f.Concepts).ThenInclude(c => c.Accounts).Where(f => f.TaxYear == anio && !f.IsDeleted).OrderBy(f => f.FormatCode);
        return seguimiento ? q.ToListAsync(ct) : q.AsNoTracking().ToListAsync(ct);
    }
}

/// <summary>Los formatos de un año gravable con su disposición, conceptos y cuentas; dice si hay semilla para cargar.</summary>
public sealed record ListExogenousFormatsQuery(int Year) : IRequest<Result<IReadOnlyList<FormatoDeExogenaDto>>>;

public sealed class ListExogenousFormatsQueryValidator : AbstractValidator<ListExogenousFormatsQuery>
{
    public ListExogenousFormatsQueryValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class ListExogenousFormatsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListExogenousFormatsQuery, Result<IReadOnlyList<FormatoDeExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<FormatoDeExogenaDto>>> Handle(ListExogenousFormatsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<FormatoDeExogenaDto>>((await ArmadoDeExogena.DelAnioAsync(db, request.Year, false, ct)).Select(ArmadoDeExogena.Dto).ToList());
}

/// <summary>
/// Carga la semilla del año (FR-071): los formatos que el año todavía no tiene, con las cuentas propuestas para el catálogo de la
/// empresa. Lo ya parametrizado no se toca. Responde los formatos del año.
/// </summary>
public sealed record LoadExogenousSeedCommand(int Year) : IRequest<Result<IReadOnlyList<FormatoDeExogenaDto>>>;

public sealed class LoadExogenousSeedCommandValidator : AbstractValidator<LoadExogenousSeedCommand>
{
    public LoadExogenousSeedCommandValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class LoadExogenousSeedCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<LoadExogenousSeedCommand, Result<IReadOnlyList<FormatoDeExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<FormatoDeExogenaDto>>> Handle(LoadExogenousSeedCommand request, CancellationToken ct)
    {
        var semilla = SemillaDeExogena.DelAnio(request.Year);
        if (semilla is null) return Result.Failure<IReadOnlyList<FormatoDeExogenaDto>>(ExogenousErrors.SeedNotFound);
        var catalogo = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => s.Catalog!.Code).FirstOrDefaultAsync(ct);
        var existentes = (await ArmadoDeExogena.DelAnioAsync(db, request.Year, false, ct)).Select(f => f.FormatCode).ToHashSet();
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var cargados = new List<string>();
        foreach (var s in semilla.Formats.Where(s => !existentes.Contains(s.Code)))
        {
            var formato = new ExogenousFormat
            {
                TaxYear = request.Year, FormatCode = s.Code, FormatVersion = s.Version, Name = s.Name, Applies = true, Origin = ExogenousOrigin.Seed,
                MinAmount = s.MinAmount, MinorAmountsRule = s.MinorAmountsRule, MinorAmountsTaxId = semilla.MinorAmountsTaxId,
                LayoutJson = new DisposicionDeExogena(s.Element, s.ConceptAttribute, semilla.Identity, s.Columns).ComoJson(),
                CreatedAt = ahora, CreatedBy = quien,
            };
            foreach (var c in s.Concepts)
            {
                var concepto = new ExogenousConcept { ConceptCode = c.Code, Name = c.Name, CreatedAt = ahora, CreatedBy = quien };
                foreach (var cuenta in c.Accounts)
                    foreach (var prefijo in SemillaDeExogena.Prefijos(cuenta, catalogo))
                        concepto.Accounts.Add(new ExogenousConceptAccount { ValueField = cuenta.Field, AccountPrefix = prefijo, Selector = cuenta.Selector, CreatedAt = ahora, CreatedBy = quien });
                formato.Concepts.Add(concepto);
            }
            db.ExogenousFormats.Add(formato);
            cargados.Add(s.Code);
        }
        if (cargados.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.EmitAsync("Accounting.Exogenous.SeedLoaded", nameof(ExogenousFormat), null, null, new { request.Year, catalog = catalogo, formats = cargados }, ct);
        }
        return Result.Success<IReadOnlyList<FormatoDeExogenaDto>>((await ArmadoDeExogena.DelAnioAsync(db, request.Year, false, ct)).Select(ArmadoDeExogena.Dto).ToList());
    }
}

/// <summary>Copia al año <paramref name="ToYear"/> los formatos de <paramref name="FromYear"/> que el destino no tenga, con sus conceptos y cuentas (FR-071).</summary>
public sealed record CopyExogenousYearCommand(int FromYear, int ToYear) : IRequest<Result<IReadOnlyList<FormatoDeExogenaDto>>>;

public sealed class CopyExogenousYearCommandValidator : AbstractValidator<CopyExogenousYearCommand>
{
    public CopyExogenousYearCommandValidator()
    {
        RuleFor(x => x.FromYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.ToYear).InclusiveBetween(2000, 2100).NotEqual(x => x.FromYear);
    }
}

public sealed class CopyExogenousYearCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<CopyExogenousYearCommand, Result<IReadOnlyList<FormatoDeExogenaDto>>>
{
    public async Task<Result<IReadOnlyList<FormatoDeExogenaDto>>> Handle(CopyExogenousYearCommand request, CancellationToken ct)
    {
        var origen = await ArmadoDeExogena.DelAnioAsync(db, request.FromYear, false, ct);
        if (origen.Count == 0) return Result.Failure<IReadOnlyList<FormatoDeExogenaDto>>(ExogenousErrors.SourceYearEmpty);
        var existentes = (await ArmadoDeExogena.DelAnioAsync(db, request.ToYear, false, ct)).Select(f => f.FormatCode).ToHashSet();
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var copiados = new List<string>();
        foreach (var f in origen.Where(f => !existentes.Contains(f.FormatCode)))
        {
            var copia = new ExogenousFormat
            {
                TaxYear = request.ToYear, FormatCode = f.FormatCode, FormatVersion = f.FormatVersion, Name = f.Name, Applies = f.Applies, Origin = ExogenousOrigin.Custom,
                MinAmount = f.MinAmount, MinorAmountsRule = f.MinorAmountsRule, MinorAmountsTaxId = f.MinorAmountsTaxId, LayoutJson = f.LayoutJson,
                CreatedAt = ahora, CreatedBy = quien,
            };
            foreach (var c in f.Concepts.Where(c => !c.IsDeleted))
            {
                var concepto = new ExogenousConcept { ConceptCode = c.ConceptCode, Name = c.Name, CreatedAt = ahora, CreatedBy = quien };
                foreach (var a in c.Accounts.Where(a => !a.IsDeleted))
                    concepto.Accounts.Add(new ExogenousConceptAccount { ValueField = a.ValueField, AccountPrefix = a.AccountPrefix, Selector = a.Selector, CreatedAt = ahora, CreatedBy = quien });
                copia.Concepts.Add(concepto);
            }
            db.ExogenousFormats.Add(copia);
            copiados.Add(f.FormatCode);
        }
        if (copiados.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.EmitAsync("Accounting.Exogenous.YearCopied", nameof(ExogenousFormat), null, null, new { request.FromYear, request.ToYear, formats = copiados }, ct);
        }
        return Result.Success<IReadOnlyList<FormatoDeExogenaDto>>((await ArmadoDeExogena.DelAnioAsync(db, request.ToYear, false, ct)).Select(ArmadoDeExogena.Dto).ToList());
    }
}

/// <summary>
/// Crea o reemplaza un formato del año: si aplica, cuantía mínima, regla de menores cuantías, la disposición y los conceptos con sus
/// cuentas. Los conceptos y cuentas anteriores quedan de baja lógica.
/// </summary>
public sealed record SaveExogenousFormatCommand(
    int Year, string FormatCode, int FormatVersion, string Name, bool Applies, decimal MinAmount, bool MinorAmountsRule, string MinorAmountsTaxId,
    DisposicionDeExogena Layout, IReadOnlyList<ConceptoDeExogenaDto> Concepts) : IRequest<Result<FormatoDeExogenaDto>>;

public sealed class SaveExogenousFormatCommandValidator : AbstractValidator<SaveExogenousFormatCommand>
{
    public SaveExogenousFormatCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.FormatCode).NotEmpty().MaximumLength(10).Matches("^[0-9]+$").WithMessage("El código del formato son dígitos (1001, 1007…).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MinorAmountsTaxId).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Layout).NotNull();
        RuleFor(x => x.Concepts).NotNull();
        RuleForEach(x => x.Concepts).ChildRules(c =>
        {
            c.RuleFor(x => x.ConceptCode).NotEmpty().MaximumLength(10);
            c.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            c.RuleForEach(x => x.Accounts).ChildRules(a =>
            {
                a.RuleFor(x => x.AccountPrefix).NotEmpty().MaximumLength(12).Matches("^[0-9]+$").WithMessage("El prefijo de cuenta son dígitos.");
                a.RuleFor(x => x.ValueField).NotEmpty().MaximumLength(40);
            });
        });
    }
}

public sealed class SaveExogenousFormatCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<SaveExogenousFormatCommand, Result<FormatoDeExogenaDto>>
{
    public async Task<Result<FormatoDeExogenaDto>> Handle(SaveExogenousFormatCommand request, CancellationToken ct)
    {
        if (request.Layout.Columns.Count > DisposicionDeExogena.MaximoDeColumnas) return Result.Failure<FormatoDeExogenaDto>(ExogenousErrors.TooManyColumns);
        foreach (var a in request.Concepts.SelectMany(c => c.Accounts))
        {
            if (!SelectoresDeExogena.Todos.Contains(a.Selector)) return Result.Failure<FormatoDeExogenaDto>(ExogenousErrors.SelectorUnknown);
            if (request.Layout.Indice(a.ValueField) < 0) return Result.Failure<FormatoDeExogenaDto>(ExogenousErrors.ColumnUnknown);
        }

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var formato = await db.ExogenousFormats.Include(f => f.Concepts).ThenInclude(c => c.Accounts)
            .FirstOrDefaultAsync(f => f.TaxYear == request.Year && f.FormatCode == request.FormatCode && !f.IsDeleted, ct);
        var nuevo = formato is null;
        if (formato is null)
        {
            formato = new ExogenousFormat { TaxYear = request.Year, FormatCode = request.FormatCode, Origin = ExogenousOrigin.Custom, CreatedAt = ahora, CreatedBy = quien };
            db.ExogenousFormats.Add(formato);
        }
        else
        {
            foreach (var c in formato.Concepts.Where(c => !c.IsDeleted))
            {
                c.IsDeleted = true; c.DeletedAt = ahora; c.DeletedBy = quien;
                foreach (var a in c.Accounts.Where(a => !a.IsDeleted)) { a.IsDeleted = true; a.DeletedAt = ahora; a.DeletedBy = quien; }
            }
            formato.UpdatedAt = ahora;
            formato.UpdatedBy = quien;
        }
        formato.FormatVersion = request.FormatVersion;
        formato.Name = request.Name.Trim();
        formato.Applies = request.Applies;
        formato.MinAmount = request.MinAmount;
        formato.MinorAmountsRule = request.MinorAmountsRule;
        formato.MinorAmountsTaxId = request.MinorAmountsTaxId.Trim();
        formato.LayoutJson = request.Layout.ComoJson();
        // La baja lógica de los conceptos viejos se guarda antes de agregar los nuevos del mismo código (UK filtrado por IsDeleted).
        if (!nuevo) await db.SaveChangesAsync(ct);
        foreach (var c in request.Concepts)
        {
            var concepto = new ExogenousConcept { ConceptCode = c.ConceptCode.Trim(), Name = c.Name.Trim(), CreatedAt = ahora, CreatedBy = quien };
            foreach (var a in c.Accounts)
                concepto.Accounts.Add(new ExogenousConceptAccount { ValueField = a.ValueField.Trim(), AccountPrefix = a.AccountPrefix.Trim(), Selector = a.Selector, CreatedAt = ahora, CreatedBy = quien });
            formato.Concepts.Add(concepto);
        }
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync(nuevo ? "Accounting.Exogenous.FormatCreated" : "Accounting.Exogenous.FormatUpdated", nameof(ExogenousFormat), formato.PublicId, null,
            new { request.Year, request.FormatCode, request.Applies, concepts = request.Concepts.Count }, ct);
        return Result.Success(ArmadoDeExogena.Dto(formato));
    }
}
