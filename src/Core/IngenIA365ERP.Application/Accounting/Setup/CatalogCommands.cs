using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Setup;

// ------------------------------------------------------------------- importar --

/// <summary>
/// Importa el catálogo propio de una cooperativa (feature 009, U2, FR-005) desde Excel o CSV con
/// columnas <c>codigo, nombre, naturaleza, rubro</c>. Toda fila se revisa antes de guardar
/// —código numérico de 1, 2, 4 o 6 dígitos, padre en el archivo, nombre, naturaleza D/C, rubro
/// existente, sin duplicados— y con un solo error no se guarda nada: la respuesta trae cada fila
/// con su columna y su porqué. El catálogo queda <c>Imported</c> con código <c>PROPIO-n</c>.
/// </summary>
public sealed record ImportAccountCatalogCommand(string Name, string FileName, byte[] Content) : IRequest<Result<ImportacionDeCatalogoDto>>;

public sealed class ImportAccountCatalogCommandValidator : AbstractValidator<ImportAccountCatalogCommand>
{
    public ImportAccountCatalogCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre del catálogo.").MaximumLength(150);
        RuleFor(x => x.FileName).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportAccountCatalogCommandHandler(
    IApplicationDbContext db,
    ITabularFileReader lector,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingAuditEmitter audit)
    : IRequestHandler<ImportAccountCatalogCommand, Result<ImportacionDeCatalogoDto>>
{
    public async Task<Result<ImportacionDeCatalogoDto>> Handle(ImportAccountCatalogCommand request, CancellationToken ct)
    {
        var lectura = await lector.LeerAsync(request.Content, request.FileName, 1, ct);
        if (lectura.IsFailure) return Result.Failure<ImportacionDeCatalogoDto>(lectura.Error);
        var tabla = lectura.Value;

        var iCodigo = tabla.IndiceDe("codigo");
        var iNombre = tabla.IndiceDe("nombre");
        var iNaturaleza = tabla.IndiceDe("naturaleza");
        var iRubro = tabla.IndiceDe("rubro");
        if (iCodigo < 0) return Result.Failure<ImportacionDeCatalogoDto>(ArchivosTabulares.SinEncabezado("codigo"));
        if (iNombre < 0) return Result.Failure<ImportacionDeCatalogoDto>(ArchivosTabulares.SinEncabezado("nombre"));
        if (iNaturaleza < 0) return Result.Failure<ImportacionDeCatalogoDto>(ArchivosTabulares.SinEncabezado("naturaleza"));
        if (iRubro < 0) return Result.Failure<ImportacionDeCatalogoDto>(ArchivosTabulares.SinEncabezado("rubro"));

        var rubros = (await db.FinancialStatementItems.AsNoTracking().Where(r => !r.IsDeleted).Select(r => r.Code).Distinct().ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errores = new List<ErrorDeFila>();
        var entradas = new List<AccountCatalogEntry>();
        var codigos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fila in tabla.Filas.Where(f => !f.EstaVacia))
        {
            var codigo = fila[iCodigo]?.Trim() ?? string.Empty;
            var nombre = fila[iNombre]?.Trim() ?? string.Empty;
            var naturaleza = fila[iNaturaleza]?.Trim().ToUpperInvariant() ?? string.Empty;
            var rubro = fila[iRubro]?.Trim().ToUpperInvariant() ?? string.Empty;

            if (codigo.Length == 0 || !codigo.All(char.IsDigit))
                errores.Add(new ErrorDeFila(fila.Numero, "codigo", "Accounting.Catalog.CodeInvalid", $"El código «{codigo}» no es numérico."));
            else if (codigo.Length is not (1 or 2 or 4 or 6))
                errores.Add(new ErrorDeFila(fila.Numero, "codigo", "Accounting.Catalog.LengthInvalid", $"El código {codigo} tiene {codigo.Length} dígitos; un catálogo lleva 1, 2, 4 o 6 (clase, grupo, cuenta, subcuenta)."));
            else if (!codigos.Add(codigo))
                errores.Add(new ErrorDeFila(fila.Numero, "codigo", "Accounting.Catalog.Duplicate", $"El código {codigo} está repetido."));

            if (nombre.Length == 0 || nombre.Length > 150)
                errores.Add(new ErrorDeFila(fila.Numero, "nombre", "Accounting.Catalog.NameInvalid", "El nombre es obligatorio y de hasta 150 caracteres."));
            if (naturaleza is not ("D" or "C"))
                errores.Add(new ErrorDeFila(fila.Numero, "naturaleza", "Accounting.Catalog.NatureInvalid", $"La naturaleza es D o C, no «{naturaleza}»."));
            if (rubro.Length == 0 || !rubros.Contains(rubro))
                errores.Add(new ErrorDeFila(fila.Numero, "rubro", "Accounting.Catalog.NiifItemInvalid", $"El rubro NIIF «{rubro}» no existe."));

            entradas.Add(new AccountCatalogEntry
            {
                Code = codigo, Name = nombre, Level = NivelDe(codigo), Nature = naturaleza == "C" ? AccountNature.Credit : AccountNature.Debit,
                NiifItemCode = rubro, ParentCode = PadreDe(codigo), CreatedAt = clock.UtcNow, CreatedBy = user.UserName ?? "system",
            });
            if (entradas[^1].ParentCode is { } padre && !codigos.Contains(padre) && !tabla.Filas.Any(f => (f[iCodigo]?.Trim() ?? string.Empty) == padre))
                errores.Add(new ErrorDeFila(fila.Numero, "codigo", "Accounting.Catalog.ParentMissing", $"La cuenta {codigo} cuelga de {padre}, que no está en el archivo."));
        }
        if (entradas.Count == 0) return Result.Failure<ImportacionDeCatalogoDto>(ArchivosTabulares.Vacio);
        if (errores.Count > 0) return Result.Failure<ImportacionDeCatalogoDto>(AccountingErrors.CatalogInvalid(errores));

        var consecutivo = await db.AccountCatalogs.IgnoreQueryFilters().CountAsync(c => c.Source == CatalogSource.Imported, ct) + 1;
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var catalogo = new AccountCatalog
        {
            Code = $"PROPIO-{consecutivo}",
            Name = request.Name.Trim(),
            Version = ahora.ToString("yyyy-MM-dd"),
            Source = CatalogSource.Imported,
            ImportedAt = ahora,
            ImportedBy = quien,
            EntryCount = entradas.Count,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        foreach (var e in entradas) catalogo.Entries.Add(e);
        db.AccountCatalogs.Add(catalogo);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Catalog.Imported", nameof(AccountCatalog), catalogo.PublicId, null,
            new { catalogo.Code, catalogo.Name, file = request.FileName, entries = entradas.Count }, ct);

        return Result.Success(new ImportacionDeCatalogoDto(catalogo.Code, entradas.Count));
    }

    private static byte NivelDe(string code) => code.Length switch { 1 => 1, 2 => 2, 4 => 3, _ => 4 };
    private static string? PadreDe(string code) => code.Length switch { 2 => code[..1], 4 => code[..2], 6 => code[..4], _ => null };
}

// -------------------------------------------------------------------- validar --

/// <summary>El contador deja constancia de que revisó el catálogo (FR-006): quién y cuándo, auditado.</summary>
public sealed record ValidateCatalogCommand(string Code) : IRequest<Result>;

public sealed class ValidateCatalogCommandValidator : AbstractValidator<ValidateCatalogCommand>
{
    public ValidateCatalogCommandValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
}

public sealed class ValidateCatalogCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<ValidateCatalogCommand, Result>
{
    public async Task<Result> Handle(ValidateCatalogCommand request, CancellationToken ct)
    {
        var codigo = request.Code.Trim().ToUpperInvariant();
        var catalogo = await db.AccountCatalogs.FirstOrDefaultAsync(c => c.Code == codigo && !c.IsDeleted, ct);
        if (catalogo is null) return Result.Failure(AccountingErrors.CatalogNotFound);

        var antes = new { catalogo.ValidatedAt, catalogo.ValidatedBy };
        catalogo.ValidatedAt = clock.UtcNow;
        catalogo.ValidatedBy = user.UserName ?? "system";
        catalogo.UpdatedAt = clock.UtcNow;
        catalogo.UpdatedBy = catalogo.ValidatedBy;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.AccountingCatalogValidated, nameof(AccountCatalog), catalogo.PublicId, antes,
            new { catalogo.Code, catalogo.Version, catalogo.ValidatedAt, catalogo.ValidatedBy }, ct);
        return Result.Success();
    }
}

// -------------------------------------------------------------------- retirar --

/// <summary>Sólo los importados se retiran, y nunca el que usa la empresa.</summary>
public sealed record RemoveCatalogCommand(string Code) : IRequest<Result>;

public sealed class RemoveCatalogCommandValidator : AbstractValidator<RemoveCatalogCommand>
{
    public RemoveCatalogCommandValidator() => RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
}

public sealed class RemoveCatalogCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<RemoveCatalogCommand, Result>
{
    public async Task<Result> Handle(RemoveCatalogCommand request, CancellationToken ct)
    {
        var codigo = request.Code.Trim().ToUpperInvariant();
        var catalogo = await db.AccountCatalogs.Include(c => c.Entries).FirstOrDefaultAsync(c => c.Code == codigo && !c.IsDeleted, ct);
        if (catalogo is null) return Result.Failure(AccountingErrors.CatalogNotFound);
        if (catalogo.Source != CatalogSource.Imported) return Result.Failure(AccountingErrors.CatalogNotImported);
        if (await db.AccountingSetups.AnyAsync(s => !s.IsDeleted && s.CatalogId == catalogo.Id, ct)) return Result.Failure(AccountingErrors.CatalogInUse);

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        catalogo.IsDeleted = true;
        catalogo.DeletedAt = ahora;
        catalogo.DeletedBy = quien;
        foreach (var e in catalogo.Entries) { e.IsDeleted = true; e.DeletedAt = ahora; e.DeletedBy = quien; }
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Catalog.Removed", nameof(AccountCatalog), catalogo.PublicId, new { catalogo.Code, catalogo.Name, catalogo.EntryCount }, null, ct);
        return Result.Success();
    }
}

// -------------------------------------------------------------------- adoptar --

/// <summary>FR-006: incorpora al plan las cuentas del catálogo que la empresa todavía no tiene (todas o las indicadas).</summary>
public sealed record AdoptCatalogUpdatesCommand(IReadOnlyList<string>? Codes) : IRequest<Result<InicializacionDto>>;

public sealed class AdoptCatalogUpdatesCommandValidator : AbstractValidator<AdoptCatalogUpdatesCommand>
{
    public AdoptCatalogUpdatesCommandValidator() => RuleForEach(x => x.Codes).NotEmpty().MaximumLength(6);
}

public sealed class AdoptCatalogUpdatesCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<AdoptCatalogUpdatesCommand, Result<InicializacionDto>>
{
    public async Task<Result<InicializacionDto>> Handle(AdoptCatalogUpdatesCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<InicializacionDto>(AccountingErrors.NotInitialized);

        var existentes = await db.ChartOfAccounts.Where(a => !a.IsDeleted).ToDictionaryAsync(a => a.Code, StringComparer.Ordinal, ct);
        var pedidas = request.Codes is { Count: > 0 } ? request.Codes.Select(c => c.Trim()).ToHashSet(StringComparer.Ordinal) : null;
        var entradas = await db.AccountCatalogEntries.AsNoTracking()
            .Where(e => e.CatalogId == setup.CatalogId && !e.IsDeleted)
            .ToListAsync(ct);
        var nuevas = entradas.Where(e => !existentes.ContainsKey(e.Code) && (pedidas is null || pedidas.Contains(e.Code))).ToList();

        // Una cuenta pedida cuyo padre tampoco está en la empresa arrastra al padre: se adopta el tramo completo.
        var porCodigo = entradas.ToDictionary(e => e.Code, StringComparer.Ordinal);
        var cierre = new Dictionary<string, AccountCatalogEntry>(StringComparer.Ordinal);
        foreach (var e in nuevas)
        {
            var actual = e;
            while (actual is not null && !existentes.ContainsKey(actual.Code) && cierre.TryAdd(actual.Code, actual))
                actual = actual.ParentCode is { } p && porCodigo.TryGetValue(p, out var padre) ? padre : null;
        }
        if (cierre.Count == 0) return Result.Success(new InicializacionDto(0, 0));

        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var cuentas = CopiaDelCatalogo.Copiar(cierre.Values, existentes, quien, ahora);
        foreach (var c in cuentas) db.ChartOfAccounts.Add(c);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Catalog.UpdatesAdopted", nameof(AccountingSetup), setup.PublicId, null,
            new { accounts = cuentas.Count, codes = cuentas.Select(c => c.Code).ToList() }, ct);
        return Result.Success(new InicializacionDto(cuentas.Count, 0));
    }
}
