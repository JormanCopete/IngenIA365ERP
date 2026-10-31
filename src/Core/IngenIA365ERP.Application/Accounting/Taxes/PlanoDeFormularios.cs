using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Taxes;

// El plano de los formularios tributarios (feature 009 E4, 2026-10-31, pedido del dueño: se siembra la primera vez y cada empresa lo
// actualiza a lo que necesite, por pantalla o subiendo un archivo). Se baja lleno con lo configurado, se edita y se sube. Mecánica de la
// carga de auxiliares: encabezados en la fila 1, una fila por renglón, todo o nada con los errores por fila y columna. Cada formulario
// que viene en el archivo se reemplaza entero con las mismas reglas de la pantalla (SaveTaxFormCommand); los que no vienen no se tocan.

public sealed record ImportacionDeFormulariosDto(int Forms, int Lines, IReadOnlyList<ErrorDeFila> Errors);

public static class PlanoDeFormularios
{
    public const string Formulario = "formulario";
    public const string NombreDelFormulario = "nombreFormulario";
    public const string Activo = "activo";
    public const string Renglon = "renglon";
    public const string Descripcion = "descripcion";
    public const string Suma = "suma";
    public const string Prefijos = "prefijos";
    public const string Signo = "signo";

    public static readonly IReadOnlyList<string> Columnas = [Formulario, NombreDelFormulario, Activo, Renglon, Descripcion, Suma, Prefijos, Signo];

    public static readonly IReadOnlyList<string> Obligatorias = [Formulario, Renglon, Suma, Prefijos];

    public static Error Invalido(IReadOnlyList<ErrorDeFila> errores) => new ErrorConDatos("Accounting.Taxes.FormsFileInvalid",
        $"El archivo tiene {errores.Count} error(es) (el primero, fila {errores[0].Row}: {errores[0].Message}); no se guardó nada.", new { errors = errores });
}

/// <summary>Los formularios de la empresa como plano (una fila por renglón); sin formularios, la plantilla vacía.</summary>
public sealed record ExportTaxFormsQuery : IRequest<Result<TablaExportable>>;

public sealed class ExportTaxFormsQueryValidator : AbstractValidator<ExportTaxFormsQuery>;

public sealed class ExportTaxFormsQueryHandler(IApplicationDbContext db) : IRequestHandler<ExportTaxFormsQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(ExportTaxFormsQuery request, CancellationToken ct)
    {
        var formularios = await db.TaxForms.AsNoTracking().Include(f => f.Lines).Where(f => !f.IsDeleted).OrderBy(f => f.Code).ToListAsync(ct);
        var filas = formularios.SelectMany(f => f.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.Order)
                .Select(l => new FilaExportable([f.Code, f.Name, f.IsActive ? "sí" : "no", l.LineCode, l.Description, l.Selector.ToString(), l.AccountPrefixes, (int)l.Sign])))
            .ToList();
        return Result.Success(new TablaExportable("Formularios tributarios", "Una fila por renglón; el formulario que viene en el archivo se reemplaza entero.",
            PlanoDeFormularios.Columnas.Select(c => new ColumnaExportable(c)).ToList(), filas, null,
            [
                "formulario: el código (350, 300, ICA-CALI…), hasta 10 caracteres. nombreFormulario y activo («sí»/«no») se toman de su primera fila.",
                "renglon: el código del renglón dentro del formulario, único en él. descripcion: hasta 150 caracteres.",
                "suma: Amount (valor neto según la naturaleza de la cuenta), Base (base gravable neta), Debit, Credit, BaseDebit (base de los débitos) o BaseCredit (base de los créditos).",
                "prefijos: los códigos de cuenta por los que empieza lo que suma, separados por coma (243515, 236515). signo: 1 o -1 (vacío = 1).",
                "Los formularios que no vienen en el archivo no se tocan. Con un solo error no se guarda nada.",
            ]));
    }
}

/// <summary>Sube el plano de formularios: valida todo y, sin errores, reemplaza cada formulario del archivo.</summary>
public sealed record ImportTaxFormsCommand(string FileName, byte[] Content) : IRequest<Result<ImportacionDeFormulariosDto>>;

public sealed class ImportTaxFormsCommandValidator : AbstractValidator<ImportTaxFormsCommand>
{
    public ImportTaxFormsCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Indique el archivo.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportTaxFormsCommandHandler(ITabularFileReader lector, ISender sender) : IRequestHandler<ImportTaxFormsCommand, Result<ImportacionDeFormulariosDto>>
{
    private sealed record Fila(int Numero, string Formulario, string? Nombre, bool? Activo, TaxFormLineInput Renglon);

    public async Task<Result<ImportacionDeFormulariosDto>> Handle(ImportTaxFormsCommand request, CancellationToken ct)
    {
        var lectura = await lector.LeerAsync(request.Content, request.FileName, 1, ct);
        if (lectura.IsFailure) return Result.Failure<ImportacionDeFormulariosDto>(lectura.Error);
        var tabla = lectura.Value;
        var indices = PlanoDeFormularios.Columnas.ToDictionary(c => c, tabla.IndiceDe, StringComparer.Ordinal);
        foreach (var obligatoria in PlanoDeFormularios.Obligatorias)
            if (indices[obligatoria] < 0) return Result.Failure<ImportacionDeFormulariosDto>(ArchivosTabulares.SinEncabezado(obligatoria));
        var crudas = tabla.Filas.Where(f => !f.EstaVacia).ToList();
        if (crudas.Count == 0) return Result.Failure<ImportacionDeFormulariosDto>(ArchivosTabulares.Vacio);
        string Celda(FilaLeida f, string c) => indices[c] < 0 ? string.Empty : (f[indices[c]] ?? string.Empty).Trim();

        var errores = new List<ErrorDeFila>();
        var filas = new List<Fila>();
        var vistos = new Dictionary<(string, string), int>();
        foreach (var f in crudas)
        {
            var n = f.Numero;
            var antes = errores.Count;
            var codigo = (CodigoDeCatalogo.Normalizar(Celda(f, PlanoDeFormularios.Formulario)) ?? string.Empty);
            if (codigo.Length is 0 or > 10 || !System.Text.RegularExpressions.Regex.IsMatch(codigo, CodigoDeCatalogo.Patron))
                errores.Add(new(n, PlanoDeFormularios.Formulario, "Accounting.Taxes.FormCodeInvalid", "El código del formulario es obligatorio, de hasta 10 caracteres y sin espacios."));
            var renglon = Celda(f, PlanoDeFormularios.Renglon);
            if (renglon.Length is 0 or > 10) errores.Add(new(n, PlanoDeFormularios.Renglon, "Accounting.Taxes.LineCodeInvalid", "El renglón es obligatorio y de hasta 10 caracteres."));
            else if (vistos.TryGetValue((codigo, renglon.ToUpperInvariant()), out var otra))
                errores.Add(new(n, PlanoDeFormularios.Renglon, TaxErrors.FormDuplicateLine.Code, $"El renglón {renglon} del formulario {codigo} ya viene en la fila {otra}."));
            else vistos[(codigo, renglon.ToUpperInvariant())] = n;
            var descripcion = Celda(f, PlanoDeFormularios.Descripcion);
            if (descripcion.Length > 150) errores.Add(new(n, PlanoDeFormularios.Descripcion, "Accounting.Taxes.DescriptionTooLong", "La descripción admite hasta 150 caracteres."));
            var suma = Celda(f, PlanoDeFormularios.Suma);
            if (!Enum.TryParse<TaxFormSelector>(suma, ignoreCase: true, out var selector) || !Enum.IsDefined(selector))
                errores.Add(new(n, PlanoDeFormularios.Suma, TaxErrors.SelectorUnknown.Code, TaxErrors.SelectorUnknown.Message));
            var prefijos = FormulariosTributarios.Prefijos(Celda(f, PlanoDeFormularios.Prefijos));
            if (prefijos.Count == 0) errores.Add(new(n, PlanoDeFormularios.Prefijos, TaxErrors.PrefixesRequired.Code, TaxErrors.PrefixesRequired.Message));
            var textoSigno = Celda(f, PlanoDeFormularios.Signo);
            short signo = textoSigno is "" or "1" or "+" or "+1" ? (short)1 : textoSigno is "-1" or "-" ? (short)-1 : (short)0;
            if (signo == 0) errores.Add(new(n, PlanoDeFormularios.Signo, "Accounting.Taxes.SignInvalid", "El signo es 1 o -1."));
            var textoActivo = Celda(f, PlanoDeFormularios.Activo);
            bool? activo = textoActivo.Length == 0 ? null : Accounts.PlantillaDeCuentas.Booleano(textoActivo);
            if (textoActivo.Length > 0 && activo is null) errores.Add(new(n, PlanoDeFormularios.Activo, "Accounting.Taxes.ActiveInvalid", $"«{textoActivo}» no es sí ni no."));
            if (errores.Count == antes)
                filas.Add(new Fila(n, codigo, Celda(f, PlanoDeFormularios.NombreDelFormulario) is { Length: > 0 } nombre ? nombre : null, activo,
                    new TaxFormLineInput(renglon, descripcion.Length == 0 ? renglon : descripcion, selector.ToString(), string.Join(',', prefijos), signo)));
        }
        if (errores.Count > 0) return Result.Failure<ImportacionDeFormulariosDto>(PlanoDeFormularios.Invalido(errores.OrderBy(e => e.Row).ToList()));

        // Sin errores: cada formulario se guarda por la misma puerta que la pantalla.
        var guardados = 0;
        foreach (var grupo in filas.GroupBy(f => f.Formulario))
        {
            var primera = grupo.First();
            var r = await sender.Send(new SaveTaxFormCommand(grupo.Key, primera.Nombre ?? $"Formulario {grupo.Key}", primera.Activo ?? true,
                grupo.Select(g => g.Renglon).ToList()), ct);
            if (r.IsFailure)
                return Result.Failure<ImportacionDeFormulariosDto>(PlanoDeFormularios.Invalido([new ErrorDeFila(primera.Numero, PlanoDeFormularios.Formulario, r.Error.Code,
                    $"{r.Error.Message} Los formularios anteriores del archivo ya quedaron guardados ({guardados}).")]));
            guardados++;
        }
        return Result.Success(new ImportacionDeFormulariosDto(guardados, filas.Count, []));
    }
}
