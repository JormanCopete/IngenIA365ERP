using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Entities.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.RestrictiveLists;

/// <summary>
/// La plantilla <c>compliance.restrictive-lists</c> (feature 013, T70, T0087; contracts/plantillas.md §4): una hoja <c>Datos</c> (admite
/// <c>.csv</c>) con una carga de <b>una</b> lista: lista, fuente, publicación y vigencia iguales en todas las filas, y una entrada por fila.
/// La lista lleva hasta 20 caracteres (data-model §4.8; plantillas.md dice 10: manda la columna). (nuevo)
/// </summary>
public static class PlantillaDeListasRestrictivas
{
    public const string Clave = "compliance.restrictive-lists";

    public const string Lista = "lista";
    public const string Fuente = "fuente";
    public const string PublicadaEl = "publicadaEl";
    public const string VigenteDesde = "vigenteDesde";
    public const string TipoDeEntidad = "tipoDeEntidad";
    public const string TipoDeDocumento = "tipoDeDocumento";
    public const string NumeroDeDocumento = "numeroDeDocumento";
    public const string NombreCompleto = "nombreCompleto";
    public const string Alias = "alias";
    public const string Programa = "programa";
    public const string Notas = "notas";

    /// <summary>Lo que admite <see cref="TipoDeEntidad"/> además del nombre del enum.</summary>
    public static readonly IReadOnlyDictionary<string, RestrictiveListEntityKind> EtiquetasDeEntidad = new Dictionary<string, RestrictiveListEntityKind>
    {
        ["1"] = RestrictiveListEntityKind.Person, ["Persona"] = RestrictiveListEntityKind.Person,
        ["2"] = RestrictiveListEntityKind.Organization, ["Organizacion"] = RestrictiveListEntityKind.Organization,
        ["3"] = RestrictiveListEntityKind.Other, ["Otro"] = RestrictiveListEntityKind.Other,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Listas restrictivas", ModuloDeAuditoria.Compliance,
    [
        new HojaDePlantilla("Datos",
        [
            new(Lista, TipoDeValor.Codigo, Obligatoria: true, Largo: 20, Reglas: "OFAC, ONU, PROPIA u otra; la misma en todas las filas.", Ejemplo: "OFAC"),
            new(Fuente, TipoDeValor.Texto, Obligatoria: true, Largo: 300, Reglas: "De dónde se descargó; la misma en todas las filas.", Ejemplo: "https://sanctionssearch.ofac.treas.gov"),
            new(PublicadaEl, TipoDeValor.Fecha, Obligatoria: true, Reglas: "La misma en todas las filas.", Ejemplo: "2026-10-01"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "La misma en todas las filas; no anterior a la carga vigente de la lista.", Ejemplo: "2026-10-06"),
            new(TipoDeEntidad, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Person (1), Organization (2) u Other (3).", Ejemplo: "Person"),
            new(TipoDeDocumento, TipoDeValor.Texto, Largo: 10, Reglas: "Obligatorio con número de documento.", Ejemplo: "CC"),
            new(NumeroDeDocumento, TipoDeValor.Texto, Largo: 30, Reglas: "Se normaliza sin puntos, guiones ni espacios; dos filas con el mismo documento se rechazan.", Ejemplo: "79.123.456"),
            new(NombreCompleto, TipoDeValor.Texto, Obligatoria: true, Largo: 300, Reglas: "Como viene en la lista.", Ejemplo: "JUAN PÉREZ GÓMEZ"),
            new(Alias, TipoDeValor.Texto, Largo: 1000, Reglas: "Separados por punto y coma.", Ejemplo: "JUANCHO; EL MONO"),
            new(Programa, TipoDeValor.Texto, Largo: 100, Reglas: "Programa o motivo de la lista.", Ejemplo: "SDNTK"),
            new(Notas, TipoDeValor.Texto, Largo: 1000),
        ]),
    ]);
}

/// <summary>Los errores de las listas restrictivas (data-model parte 3 §10.2). (nuevo)</summary>
public static class ErroresDeListasRestrictivas
{
    public const string CodigoNoEncontrada = "Compliance.RestrictiveList.NotFound";

    /// <summary><b>(nuevo, T0087)</b> El encabezado de la carga (lista, fuente, publicación, vigencia) cambia entre filas.</summary>
    public const string CodigoEncabezadoDistinto = "Compliance.RestrictiveList.HeaderMismatch";

    /// <summary><b>(nuevo, T0087)</b> La vigencia es anterior a la de la carga vigente de la lista.</summary>
    public const string CodigoVigenciaAnterior = "Compliance.RestrictiveList.ValidFromBeforeCurrent";

    public static readonly Error NoEncontrada = new(CodigoNoEncontrada, "La carga de la lista restrictiva no existe.");
}

/// <summary>
/// Carga una versión de una lista restrictiva (feature 013, T70, T0087; <c>POST /api/compliance/restrictive-lists/import?mode=</c>,
/// <c>Compliance.RestrictiveLists.Load</c>; contracts/plantillas.md §4). Sobre <see cref="EjecutorDeImportacion"/>: <c>review</c> no guarda;
/// <c>apply</c> es todo o nada. Escribe siempre una <b>carga nueva</b> (<c>Version</c> consecutiva por lista) con sus entradas inmutables; la
/// anterior queda <c>Replaced</c> por derivación. Dos filas con el mismo documento normalizado son <c>Import.Row.Duplicate</c>. La auditoría
/// es <b>un evento por carga</b> (<c>Import.Applied</c> en el módulo <c>Compliance</c>, con conteo y SHA-256); las entradas no dejan
/// diferencias fila por fila (<c>[SinDiffDeAuditoria]</c>). El re-escaneo de deudores vivos es de US2 (C90). (nuevo)
/// </summary>
public sealed record ImportRestrictiveListCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportRestrictiveListCommandValidator : AbstractValidator<ImportRestrictiveListCommand>
{
    public ImportRestrictiveListCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportRestrictiveListCommand>.LargoMaximo);
    }
}

public sealed class ImportRestrictiveListCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IActorActual actorActual)
    : IRequestHandler<ImportRestrictiveListCommand, Result<ImportResultDto>>
{
    private const string P = PlantillaDeListasRestrictivas.Lista;

    public Task<Result<ImportResultDto>> Handle(ImportRestrictiveListCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeListasRestrictivas.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, ImportRestrictiveListCommand request, CancellationToken ct)
    {
        var hoja = ctx.Datos;
        var filas = hoja.Filas;
        if (filas.Count == 0)
        {
            ctx.ErrorSinFila(null, PlantillaDeListasRestrictivas.NombreCompleto, ImportErrors.CellRequired, "El archivo no trae entradas de la lista.");
            return;
        }

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario)
        {
            ctx.ErrorSinFila(null, P, ImportErrors.CellPermissionRequired, "Una lista restrictiva la carga una persona, no un proceso.");
            return;
        }

        // El encabezado de la carga: el de la primera fila, igual en todas.
        string? lista = null, fuente = null;
        DateOnly? publicada = null, desde = null;
        var entradas = new List<RestrictiveListEntry>();
        foreach (var fila in filas)
        {
            var suLista = fila.Codigo(P, 20);
            var suFuente = fila.Texto(PlantillaDeListasRestrictivas.Fuente, 300);
            var suPublicada = fila.Fecha(PlantillaDeListasRestrictivas.PublicadaEl);
            var suDesde = fila.Fecha(PlantillaDeListasRestrictivas.VigenteDesde);
            fila.Requerido(P);
            fila.Requerido(PlantillaDeListasRestrictivas.Fuente);
            fila.Requerido(PlantillaDeListasRestrictivas.PublicadaEl);
            fila.Requerido(PlantillaDeListasRestrictivas.VigenteDesde);
            lista ??= suLista;
            fuente ??= suFuente;
            publicada ??= suPublicada;
            desde ??= suDesde;
            Igual(fila, P, lista, suLista);
            Igual(fila, PlantillaDeListasRestrictivas.Fuente, fuente, suFuente);
            Igual(fila, PlantillaDeListasRestrictivas.PublicadaEl, publicada?.ToString("yyyy-MM-dd"), suPublicada?.ToString("yyyy-MM-dd"));
            Igual(fila, PlantillaDeListasRestrictivas.VigenteDesde, desde?.ToString("yyyy-MM-dd"), suDesde?.ToString("yyyy-MM-dd"));

            var clase = fila.Enumeracion(PlantillaDeListasRestrictivas.TipoDeEntidad, PlantillaDeListasRestrictivas.EtiquetasDeEntidad);
            fila.Requerido(PlantillaDeListasRestrictivas.TipoDeEntidad);
            var tipoDeDocumento = fila.Texto(PlantillaDeListasRestrictivas.TipoDeDocumento, 10)?.ToUpperInvariant();
            var numero = fila.Texto(PlantillaDeListasRestrictivas.NumeroDeDocumento, 30);
            var documento = numero is null ? null : CoincidenciaDeNombres.NormalizarDocumento(numero);
            if (documento is { Length: 0 }) documento = null;
            if (documento is not null && tipoDeDocumento is null)
                fila.Error(PlantillaDeListasRestrictivas.TipoDeDocumento, ImportErrors.CellRequired, "Con número de documento, indique su tipo.");
            var nombre = fila.Texto(PlantillaDeListasRestrictivas.NombreCompleto, 300);
            fila.Requerido(PlantillaDeListasRestrictivas.NombreCompleto);
            var alias = (fila.Texto(PlantillaDeListasRestrictivas.Alias, 1000) ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(CoincidenciaDeNombres.Normalizar)
                .Where(a => a.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var programa = fila.Texto(PlantillaDeListasRestrictivas.Programa, 100);
            var notas = fila.Texto(PlantillaDeListasRestrictivas.Notas, 1000);

            if (!hoja.LlaveUnica(fila, documento, PlantillaDeListasRestrictivas.NumeroDeDocumento) || fila.TieneErrores || clase is null || nombre is null)
                continue;

            var normalizado = CoincidenciaDeNombres.Normalizar(nombre);
            entradas.Add(new RestrictiveListEntry
            {
                EntityKind = clase.Value,
                DocumentType = tipoDeDocumento,
                DocumentNumberNormalized = documento,
                FullName = nombre,
                NameNormalized = normalizado.Length > 300 ? normalizado[..300] : normalizado,
                Aliases = alias.Count == 0 ? null : Recortar(JsonSerializer.Serialize(alias), 2000),
                Program = programa,
                Notes = notas,
            });
            ctx.Registrar(fila, documento ?? normalizado, AccionDeImportacion.Create, [new(PlantillaDeListasRestrictivas.NombreCompleto, null, nombre)]);
        }

        if (lista is null || fuente is null || desde is null || ctx.HayErrores) return;

        var anteriores = await db.RestrictiveListLoads.AsNoTracking()
            .Where(c => c.ListCode == lista)
            .Select(c => new { c.Version, c.ValidFrom })
            .ToListAsync(ct);
        if (anteriores.Count > 0 && desde.Value < anteriores.Max(c => c.ValidFrom))
        {
            ctx.ErrorSinFila(null, PlantillaDeListasRestrictivas.VigenteDesde, ErroresDeListasRestrictivas.CodigoVigenciaAnterior,
                $"La lista {lista} ya tiene una carga vigente desde el {anteriores.Max(c => c.ValidFrom):yyyy-MM-dd}: la nueva no puede empezar antes.");
            return;
        }

        var version = (anteriores.Count == 0 ? 0 : anteriores.Max(c => c.Version)) + 1;
        var carga = new RestrictiveListLoad
        {
            ListCode = lista,
            Version = version,
            Source = fuente,
            PublishedOn = publicada,
            ValidFrom = desde.Value,
            FileName = Recortar(request.File.NombreArchivo, 260),
            FileSha256 = request.File.Sha256,
            EntryCount = entradas.Count,
            LoadedByUserId = usuario,
            LoadedByName = Recortar(actor.Name, 150),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : Recortar(request.Reason, 300),
            Entries = entradas,
        };
        db.RestrictiveListLoads.Add(carga);
        ctx.Extra["listCode"] = lista;
        ctx.Extra["version"] = version;
        ctx.Extra["entryCount"] = entradas.Count;
        ctx.Extra["loadPublicId"] = carga.PublicId;
    }

    private static void Igual(FilaDeImportacion fila, string columna, string? esperado, string? valor)
    {
        if (esperado is null || valor is null || string.Equals(esperado, valor, StringComparison.Ordinal)) return;
        fila.Error(columna, ErroresDeListasRestrictivas.CodigoEncabezadoDistinto,
            $"«{valor}» no coincide con «{esperado}» de la primera fila: un archivo carga una sola lista, con la misma fuente, publicación y vigencia.");
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}
