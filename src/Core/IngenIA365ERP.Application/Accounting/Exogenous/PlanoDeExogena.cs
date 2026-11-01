using FluentValidation;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Exogenous;

// El plano de la exógena de un año (feature 009 E4, 2026-10-31, pedido del dueño): los conceptos y las cuentas de cada formato, una fila
// por cuenta. Se baja lleno con lo configurado, se edita y se sube; cada formato que viene en el archivo reemplaza sus conceptos y cuentas
// con las mismas reglas de la pantalla (SaveExogenousFormatCommand), conservando su disposición, cuantía y «aplica». Los formatos tienen que
// existir en el año (semilla, copia o «Nuevo formato» en la pantalla); los que no vienen no se tocan. Todo o nada, errores por fila y columna.

public sealed record ImportacionDeExogenaDto(int Formats, int Concepts, int Accounts, IReadOnlyList<ErrorDeFila> Errors);

public static class PlanoDeExogena
{
    public const string Formato = "formato";
    public const string Concepto = "concepto";
    public const string NombreDelConcepto = "nombreConcepto";
    public const string Columna = "columna";
    public const string Prefijo = "prefijo";
    public const string Suma = "suma";

    public static readonly IReadOnlyList<string> Columnas = [Formato, Concepto, NombreDelConcepto, Columna, Prefijo, Suma];

    public static readonly IReadOnlyList<string> Obligatorias = [Formato, Concepto];

    public static Error Invalido(IReadOnlyList<ErrorDeFila> errores) => new ErrorConDatos("Accounting.Exogenous.FileInvalid",
        $"El archivo tiene {errores.Count} error(es) (el primero, fila {errores[0].Row}: {errores[0].Message}); no se guardó nada.", new { errors = errores });
}

/// <summary>Los conceptos y cuentas de los formatos del año como plano; sin formatos, la plantilla vacía.</summary>
public sealed record ExportExogenousConceptsQuery(int Year) : IRequest<Result<TablaExportable>>;

public sealed class ExportExogenousConceptsQueryValidator : AbstractValidator<ExportExogenousConceptsQuery>
{
    public ExportExogenousConceptsQueryValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
}

public sealed class ExportExogenousConceptsQueryHandler(IApplicationDbContext db) : IRequestHandler<ExportExogenousConceptsQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(ExportExogenousConceptsQuery request, CancellationToken ct)
    {
        var formatos = await ArmadoDeExogena.DelAnioAsync(db, request.Year, false, ct);
        var filas = new List<FilaExportable>();
        foreach (var f in formatos)
            foreach (var c in f.Concepts.Where(c => !c.IsDeleted).OrderBy(c => c.ConceptCode))
            {
                var cuentas = c.Accounts.Where(a => !a.IsDeleted).OrderBy(a => a.ValueField).ThenBy(a => a.AccountPrefix).ToList();
                if (cuentas.Count == 0) filas.Add(new FilaExportable([f.FormatCode, c.ConceptCode, c.Name, null, null, null]));
                foreach (var a in cuentas) filas.Add(new FilaExportable([f.FormatCode, c.ConceptCode, c.Name, a.ValueField, a.AccountPrefix, a.Selector]));
            }
        var columnasPorFormato = formatos.Select(f => (f.FormatCode, Disposicion: DisposicionDeExogena.Leer(f.LayoutJson)))
            .Select(x => $"{x.FormatCode}: {string.Join(", ", x.Disposicion?.Columns.Select(c => c.Field) ?? [])}").ToList();
        return Result.Success(new TablaExportable($"Exógena {request.Year}: conceptos y cuentas", "Una fila por cuenta de un concepto; el formato que viene en el archivo reemplaza sus conceptos.",
            PlanoDeExogena.Columnas.Select(c => new ColumnaExportable(c)).ToList(), filas, null,
            [
                "formato: el código del formato del año (1001, 1007…). Tiene que existir: cárguelo con la semilla, cópielo del año anterior o créelo en la pantalla.",
                "concepto y nombreConcepto: el código DIAN del concepto (5002…) y su nombre; un concepto sin cuentas va en una fila con columna, prefijo y suma vacíos.",
                "columna: el campo del formato donde suma la cuenta. Las de cada formato: " + string.Join("; ", columnasPorFormato) + ".",
                "prefijo: el código de cuenta por el que empiezan las cuentas que suman (sólo dígitos).",
                "suma: Debit, Credit o Net (movimiento del año), Base, BaseDebit o BaseCredit (base gravable) o Balance (saldo al 31 de diciembre).",
                "Los formatos que no vienen en el archivo no se tocan. Con un solo error no se guarda nada.",
            ]));
    }
}

/// <summary>Sube el plano de conceptos y cuentas de la exógena de un año.</summary>
public sealed record ImportExogenousConceptsCommand(int Year, string FileName, byte[] Content) : IRequest<Result<ImportacionDeExogenaDto>>;

public sealed class ImportExogenousConceptsCommandValidator : AbstractValidator<ImportExogenousConceptsCommand>
{
    public ImportExogenousConceptsCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Indique el archivo.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportExogenousConceptsCommandHandler(IApplicationDbContext db, ITabularFileReader lector, ISender sender)
    : IRequestHandler<ImportExogenousConceptsCommand, Result<ImportacionDeExogenaDto>>
{
    private sealed record Fila(int Numero, string Formato, string Concepto, string Nombre, CuentaDeConceptoDto? Cuenta);

    public async Task<Result<ImportacionDeExogenaDto>> Handle(ImportExogenousConceptsCommand request, CancellationToken ct)
    {
        var lectura = await lector.LeerAsync(request.Content, request.FileName, 1, ct);
        if (lectura.IsFailure) return Result.Failure<ImportacionDeExogenaDto>(lectura.Error);
        var tabla = lectura.Value;
        var indices = PlanoDeExogena.Columnas.ToDictionary(c => c, tabla.IndiceDe, StringComparer.Ordinal);
        foreach (var obligatoria in PlanoDeExogena.Obligatorias)
            if (indices[obligatoria] < 0) return Result.Failure<ImportacionDeExogenaDto>(ArchivosTabulares.SinEncabezado(obligatoria));
        var crudas = tabla.Filas.Where(f => !f.EstaVacia).ToList();
        if (crudas.Count == 0) return Result.Failure<ImportacionDeExogenaDto>(ArchivosTabulares.Vacio);
        string Celda(FilaLeida f, string c) => indices[c] < 0 ? string.Empty : (f[indices[c]] ?? string.Empty).Trim();

        var formatos = (await ArmadoDeExogena.DelAnioAsync(db, request.Year, false, ct)).ToDictionary(f => f.FormatCode, StringComparer.OrdinalIgnoreCase);
        var errores = new List<ErrorDeFila>();
        var filas = new List<Fila>();
        foreach (var f in crudas)
        {
            var n = f.Numero;
            var antes = errores.Count;
            var codigo = Celda(f, PlanoDeExogena.Formato);
            formatos.TryGetValue(codigo, out var formato);
            if (formato is null)
                errores.Add(new(n, PlanoDeExogena.Formato, ExogenousErrors.FormatNotFound.Code, $"El formato «{codigo}» no existe en {request.Year}: cárguelo con la semilla, cópielo o créelo en la pantalla."));
            var concepto = Celda(f, PlanoDeExogena.Concepto);
            if (concepto.Length is 0 or > 10) errores.Add(new(n, PlanoDeExogena.Concepto, "Accounting.Exogenous.ConceptInvalid", "El concepto es obligatorio y de hasta 10 caracteres."));
            var nombre = Celda(f, PlanoDeExogena.NombreDelConcepto);
            if (nombre.Length > 200) errores.Add(new(n, PlanoDeExogena.NombreDelConcepto, "Accounting.Exogenous.ConceptNameTooLong", "El nombre del concepto admite hasta 200 caracteres."));
            var columna = Celda(f, PlanoDeExogena.Columna);
            var prefijo = Celda(f, PlanoDeExogena.Prefijo);
            var suma = Celda(f, PlanoDeExogena.Suma);
            CuentaDeConceptoDto? cuenta = null;
            if (columna.Length + prefijo.Length + suma.Length > 0)
            {
                if (formato is not null && DisposicionDeExogena.Leer(formato.LayoutJson) is { } d && d.Indice(columna) < 0)
                    errores.Add(new(n, PlanoDeExogena.Columna, ExogenousErrors.ColumnUnknown.Code,
                        $"El formato {formato.FormatCode} no tiene la columna «{columna}». Tiene: {string.Join(", ", d.Columns.Select(c => c.Field))}."));
                if (prefijo.Length is 0 or > 12 || !prefijo.All(char.IsDigit))
                    errores.Add(new(n, PlanoDeExogena.Prefijo, "Accounting.Exogenous.PrefixInvalid", "El prefijo de cuenta son de 1 a 12 dígitos."));
                var selector = SelectoresDeExogena.Todos.FirstOrDefault(s => string.Equals(s, suma, StringComparison.OrdinalIgnoreCase));
                if (selector is null) errores.Add(new(n, PlanoDeExogena.Suma, ExogenousErrors.SelectorUnknown.Code, ExogenousErrors.SelectorUnknown.Message));
                cuenta = new CuentaDeConceptoDto(columna, prefijo, selector ?? suma);
            }
            if (errores.Count == antes) filas.Add(new Fila(n, formato!.FormatCode, concepto, nombre, cuenta));
        }
        if (errores.Count > 0) return Result.Failure<ImportacionDeExogenaDto>(PlanoDeExogena.Invalido(errores.OrderBy(e => e.Row).ToList()));

        var guardados = 0;
        foreach (var grupo in filas.GroupBy(f => f.Formato))
        {
            var f = formatos[grupo.Key];
            var disposicion = DisposicionDeExogena.Leer(f.LayoutJson)!;
            var conceptos = grupo.GroupBy(x => x.Concepto, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ConceptoDeExogenaDto(c.Key, c.Select(x => x.Nombre).FirstOrDefault(x => x.Length > 0) ?? c.Key,
                    c.Where(x => x.Cuenta is not null).Select(x => x.Cuenta!).Distinct().ToList()))
                .ToList();
            var r = await sender.Send(new SaveExogenousFormatCommand(request.Year, f.FormatCode, f.FormatVersion, f.Name, f.Applies, f.MinAmount, f.MinorAmountsRule,
                f.MinorAmountsTaxId, disposicion, conceptos), ct);
            if (r.IsFailure)
                return Result.Failure<ImportacionDeExogenaDto>(PlanoDeExogena.Invalido([new ErrorDeFila(grupo.First().Numero, PlanoDeExogena.Formato, r.Error.Code,
                    $"{r.Error.Message} Los formatos anteriores del archivo ya quedaron guardados ({guardados}).")]));
            guardados++;
        }
        return Result.Success(new ImportacionDeExogenaDto(guardados, filas.Select(x => (x.Formato, x.Concepto)).Distinct().Count(), filas.Count(x => x.Cuenta is not null), []));
    }
}
