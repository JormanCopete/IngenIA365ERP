using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Taxes;

// Formularios tributarios (feature 009 E4, US10, FR-066): cada renglón suma, sobre las cuentas cuyos códigos empiezan por sus prefijos,
// la base, el valor neto o un solo lado. Son configuración de la empresa: un formulario nuevo de la DIAN o del municipio se arma sin
// programa. Los sugeridos (350 retención en la fuente y 300 IVA, decisión del dueño del 2026-10-30) se cargan a pedido según el
// catálogo con que se inició la contabilidad, y quedan «pendientes de validar» por el contador: los renglones son los conceptos, no la
// numeración oficial del formulario, que cambia con cada versión.

public sealed record TaxFormLineInput(string LineCode, string Description, string Selector, string AccountPrefixes, short Sign = 1);

/// <summary>Crea o reemplaza un formulario por su código: nombre, si está activo y sus renglones en el orden en que llegan.</summary>
public sealed record SaveTaxFormCommand(string Code, string Name, bool IsActive, IReadOnlyList<TaxFormLineInput> Lines) : IRequest<Result<TaxFormDto>>;

public sealed class SaveTaxFormCommandValidator : AbstractValidator<SaveTaxFormCommand>
{
    public SaveTaxFormCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre del formulario.").MaximumLength(100);
        RuleFor(x => x.Lines).NotNull();
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.LineCode).NotEmpty().MaximumLength(10);
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(150);
            l.RuleFor(x => x.AccountPrefixes).NotEmpty().MaximumLength(400);
            l.RuleFor(x => x.Sign).Must(s => s is 1 or -1).WithMessage("El signo es 1 o -1.");
        });
    }
}

public sealed class SaveTaxFormCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<SaveTaxFormCommand, Result<TaxFormDto>>
{
    public async Task<Result<TaxFormDto>> Handle(SaveTaxFormCommand request, CancellationToken ct)
    {
        var lineas = new List<TaxFormLine>();
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var orden = 0;
        foreach (var l in request.Lines)
        {
            if (!Enum.TryParse<TaxFormSelector>(l.Selector, ignoreCase: true, out var selector) || !Enum.IsDefined(selector)) return Result.Failure<TaxFormDto>(TaxErrors.SelectorUnknown);
            var prefijos = FormulariosTributarios.Prefijos(l.AccountPrefixes);
            if (prefijos.Count == 0) return Result.Failure<TaxFormDto>(TaxErrors.PrefixesRequired);
            lineas.Add(new TaxFormLine
            {
                LineCode = l.LineCode.Trim(), Description = l.Description.Trim(), Selector = selector, AccountPrefixes = string.Join(',', prefijos),
                Sign = l.Sign, Order = ++orden, CreatedAt = ahora, CreatedBy = quien,
            });
        }
        if (lineas.GroupBy(l => l.LineCode, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) return Result.Failure<TaxFormDto>(TaxErrors.FormDuplicateLine);

        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var formulario = await db.TaxForms.Include(f => f.Lines).FirstOrDefaultAsync(f => f.Code == codigo && !f.IsDeleted, ct);
        var nuevo = formulario is null;
        if (formulario is null)
        {
            formulario = new TaxForm { Code = codigo, CreatedAt = ahora, CreatedBy = quien };
            db.TaxForms.Add(formulario);
        }
        else
        {
            // Los renglones anteriores quedan de baja lógica (Principio VII), no se borran.
            foreach (var l in formulario.Lines.Where(l => !l.IsDeleted)) { l.IsDeleted = true; l.DeletedAt = ahora; l.DeletedBy = quien; }
            formulario.UpdatedAt = ahora;
            formulario.UpdatedBy = quien;
        }
        formulario.Name = request.Name.Trim();
        formulario.IsActive = request.IsActive;
        foreach (var l in lineas) formulario.Lines.Add(l);
        await db.SaveChangesAsync(ct);
        await audit.EmitAsync(nuevo ? "Accounting.TaxForm.Created" : "Accounting.TaxForm.Updated", nameof(TaxForm), formulario.PublicId, null,
            new { formulario.Code, formulario.Name, formulario.IsActive, lines = lineas.Count }, ct);
        return Result.Success(FormulariosTributarios.Dto(formulario));
    }
}

/// <summary>Los formularios de la empresa, activos e inactivos.</summary>
public sealed record ListTaxFormsQuery : IRequest<Result<IReadOnlyList<TaxFormDto>>>;

public sealed class ListTaxFormsQueryValidator : AbstractValidator<ListTaxFormsQuery>;

public sealed class ListTaxFormsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListTaxFormsQuery, Result<IReadOnlyList<TaxFormDto>>>
{
    public async Task<Result<IReadOnlyList<TaxFormDto>>> Handle(ListTaxFormsQuery request, CancellationToken ct)
    {
        var formularios = await db.TaxForms.AsNoTracking().Include(f => f.Lines).Where(f => !f.IsDeleted).OrderBy(f => f.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TaxFormDto>>(formularios.Select(FormulariosTributarios.Dto).ToList());
    }
}

/// <summary>
/// Carga los formularios sugeridos del catálogo de la empresa (350 y 300) que todavía no existan; los que ya existen no se tocan.
/// Responde los formularios que quedaron.
/// </summary>
public sealed record LoadSuggestedTaxFormsCommand : IRequest<Result<IReadOnlyList<TaxFormDto>>>;

public sealed class LoadSuggestedTaxFormsCommandValidator : AbstractValidator<LoadSuggestedTaxFormsCommand>;

public sealed class LoadSuggestedTaxFormsCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, AccountingAuditEmitter audit)
    : IRequestHandler<LoadSuggestedTaxFormsCommand, Result<IReadOnlyList<TaxFormDto>>>
{
    public async Task<Result<IReadOnlyList<TaxFormDto>>> Handle(LoadSuggestedTaxFormsCommand request, CancellationToken ct)
    {
        var catalogo = await db.AccountingSetups.AsNoTracking().Where(s => !s.IsDeleted).Select(s => s.Catalog!.Code).FirstOrDefaultAsync(ct);
        if (catalogo is null) return Result.Failure<IReadOnlyList<TaxFormDto>>(new Error("Accounting.NotInitialized", "La contabilidad no está iniciada."));
        var sugeridos = FormulariosTributarios.Sugeridos(catalogo);
        if (sugeridos.Count == 0) return Result.Failure<IReadOnlyList<TaxFormDto>>(TaxErrors.CatalogWithoutSuggestion);

        var existentes = await db.TaxForms.Where(f => !f.IsDeleted).Select(f => f.Code).ToListAsync(ct);
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        var cargados = new List<string>();
        foreach (var s in sugeridos.Where(s => !existentes.Contains(s.Code)))
        {
            var formulario = new TaxForm { Code = s.Code, Name = s.Name, IsActive = true, CreatedAt = ahora, CreatedBy = quien };
            var orden = 0;
            foreach (var l in s.Lines)
                formulario.Lines.Add(new TaxFormLine
                {
                    LineCode = l.LineCode, Description = l.Description, Selector = Enum.Parse<TaxFormSelector>(l.Selector), AccountPrefixes = l.AccountPrefixes,
                    Sign = l.Sign, Order = ++orden, CreatedAt = ahora, CreatedBy = quien,
                });
            db.TaxForms.Add(formulario);
            cargados.Add(s.Code);
        }
        if (cargados.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            await audit.EmitAsync("Accounting.TaxForm.SuggestedLoaded", nameof(TaxForm), null, null, new { catalog = catalogo, forms = cargados }, ct);
        }
        var todos = await db.TaxForms.AsNoTracking().Include(f => f.Lines).Where(f => !f.IsDeleted).OrderBy(f => f.Code).ToListAsync(ct);
        return Result.Success<IReadOnlyList<TaxFormDto>>(todos.Select(FormulariosTributarios.Dto).ToList());
    }
}

/// <summary>Lo común de los formularios: prefijos, el DTO y los sugeridos por catálogo.</summary>
public static class FormulariosTributarios
{
    public sealed record Sugerido(string Code, string Name, IReadOnlyList<TaxFormLineInput> Lines);

    /// <summary>Los prefijos de una cadena «236505, 243505»: sólo dígitos, sin repetir, en el orden dado.</summary>
    public static IReadOnlyList<string> Prefijos(string? texto) =>
        (texto ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.All(char.IsDigit)).Distinct().ToList();

    public static TaxFormDto Dto(TaxForm f) => new(f.PublicId, f.Code, f.Name, f.IsActive,
        f.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.Order)
            .Select(l => new TaxFormLineDto(l.LineCode, l.Description, l.Selector.ToString(), l.AccountPrefixes, l.Sign, l.Order)).ToList());

    // Los conceptos de la retención en la fuente con la subcuenta que les corresponde en cada catálogo (CUIF 2435xx, PUC comercial 2365xx);
    // nulo donde el catálogo no la trae.
    private static readonly (string Codigo, string Concepto, string? Solidario, string? Comercial)[] ConceptosDeRetencion =
    [
        ("SAL", "Salarios y pagos laborales", "243505", "236505"),
        ("DIV", "Dividendos y participaciones", "243510", "236510"),
        ("HON", "Honorarios", "243515", "236515"),
        ("COM", "Comisiones", "243520", "236520"),
        ("SER", "Servicios", "243525", "236525"),
        ("ARR", "Arrendamientos", "243530", "236530"),
        ("REN", "Rendimientos financieros", "243535", "236535"),
        ("CMP", "Compras", "243540", "236540"),
        ("LOT", "Loterías, rifas y apuestas", "243545", "236545"),
        ("EXT", "Pagos al exterior", "243550", "236550"),
        ("PPE", "Enajenación de activos fijos de personas naturales", "243555", "236560"),
        ("OBR", "Contratos de obra", "243570", null),
        ("OTR", "Otras retenciones", "243585", "236570"),
        ("AUT", "Autorretenciones", "243565", "236575"),
    ];

    /// <summary>Los formularios sugeridos para el catálogo <paramref name="catalogo"/> (<c>PUC-SOLIDARIO</c> o <c>PUC-COMERCIAL</c>); vacío para otro.</summary>
    public static IReadOnlyList<Sugerido> Sugeridos(string catalogo)
    {
        var solidario = string.Equals(catalogo, "PUC-SOLIDARIO", StringComparison.OrdinalIgnoreCase);
        var comercial = string.Equals(catalogo, "PUC-COMERCIAL", StringComparison.OrdinalIgnoreCase);
        if (!solidario && !comercial) return [];

        var retencion = new List<TaxFormLineInput>();
        var prefijosDeValor = new List<string>();
        foreach (var (codigo, concepto, sol, com) in ConceptosDeRetencion)
        {
            var prefijo = solidario ? sol : com;
            if (prefijo is null) continue;
            retencion.Add(new($"{codigo}-B", $"{concepto}: base sometida a retención", nameof(TaxFormSelector.Base), prefijo));
            retencion.Add(new($"{codigo}-R", $"{concepto}: retención practicada", nameof(TaxFormSelector.Amount), prefijo));
            prefijosDeValor.Add(prefijo);
        }
        var reteIva = solidario ? "243560" : "2367";
        retencion.Add(new("IVA-B", "Retención de IVA: base", nameof(TaxFormSelector.Base), reteIva));
        retencion.Add(new("IVA-R", "Retención de IVA practicada", nameof(TaxFormSelector.Amount), reteIva));
        prefijosDeValor.Add(reteIva);
        retencion.Add(new("TOTAL", "Total retenciones", nameof(TaxFormSelector.Amount), string.Join(',', prefijosDeValor)));

        var ivaPorPagar = solidario ? "244010" : "2408";
        var iva = new List<TaxFormLineInput>
        {
            new("ING-GRAV", "Ingresos gravados (base del IVA generado)", nameof(TaxFormSelector.BaseCredit), ivaPorPagar),
            new("IVA-GEN", "IVA generado", nameof(TaxFormSelector.Credit), ivaPorPagar),
            new("CMP-GRAV", "Compras y gastos gravados (base del IVA descontable)", nameof(TaxFormSelector.BaseDebit), ivaPorPagar),
            new("IVA-DESC", "IVA descontable", nameof(TaxFormSelector.Debit), ivaPorPagar),
            new("SALDO", "Saldo a pagar (IVA generado menos descontable)", nameof(TaxFormSelector.Amount), ivaPorPagar),
        };
        if (comercial) iva.Add(new("RET-IVA", "Retenciones de IVA que le practicaron", nameof(TaxFormSelector.Amount), "135517"));

        return
        [
            new("350", "Retenciones en la fuente (formulario 350) — pendiente de validar", retencion),
            new("300", "Impuesto sobre las ventas (formulario 300) — pendiente de validar", iva),
        ];
    }
}
