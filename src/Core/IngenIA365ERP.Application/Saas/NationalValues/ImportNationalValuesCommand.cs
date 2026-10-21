using System.Globalization;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Saas.NationalValues;

/// <summary>
/// La plantilla <c>saas.national-values</c> (feature 013, T37, C82; contracts/plantillas.md §10): una hoja <c>Datos</c> con las
/// columnas del contrato más <c>etiqueta</c> (el nombre del festivo, que Nómina proyecta) y <c>motivo</c> (el de una corrección
/// con <c>corrigeA</c>, que se exige fila por fila). Los documentos normativos son JSON y no van por plantilla. (nuevo)
/// </summary>
public static class PlantillaDeValoresNacionales
{
    public const string Clave = "saas.national-values";

    public const string Codigo = "codigo";
    public const string ClaveDelValor = "clave";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";
    public const string FechaDeReferencia = "fechaDeReferencia";
    public const string Valor = "valor";
    public const string Unidad = "unidad";
    public const string Fuente = "fuente";
    public const string Etiqueta = "etiqueta";
    public const string CorrigeA = "corrigeA";
    public const string Motivo = "motivo";

    /// <summary>Hasta 10 decimales (contracts/plantillas.md §10): tasas y fracciones como fracción.</summary>
    public const int DecimalesDelValor = 10;

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Valores nacionales", AuditoriaDelRegistroCentral.Modulo,
    [
        new HojaDePlantilla("Datos",
        [
            new(Codigo, TipoDeValor.Texto, Obligatoria: true, Largo: 30, Reglas: "USURA, IBR, IPC, TOPE_TASA_VIVIENDA, UVR, SMMLV, UVT, FESTIVO o LIMITE (TOPE_TASA_VIVIENDA y UVR desde K6b; la UVR, un valor por día, sin clave).", Ejemplo: "USURA"),
            new(ClaveDelValor, TipoDeValor.Texto, Largo: 60, Reglas: "USURA: modalidad certificada del catálogo vigente; IBR/IPC: plazo o serie; FESTIVO: la fecha; LIMITE: código del límite.", Ejemplo: "CONSUMO"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-11-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "Sólo cuando la norma lo trae (período certificado de la usura).", Ejemplo: "2026-11-30"),
            new(FechaDeReferencia, TipoDeValor.Fecha, Reglas: "El período que mide un índice, distinto de cuándo se publica."),
            new(Valor, TipoDeValor.Costo, Reglas: "Hasta 10 decimales. Tasas y fracciones como fracción: 26,50 % se escribe 0,2650. FESTIVO: 1 (se ignora).", Ejemplo: "0,2650000000"),
            new(Unidad, TipoDeValor.Texto, Largo: 12, Reglas: "EA, NominalMV, Pesos, Fraccion, Dias, Meses, Horas o Veces; la del código (y la del límite en LIMITE).", Ejemplo: "EA"),
            new(Fuente, TipoDeValor.Texto, Obligatoria: true, Largo: 300, Reglas: "Norma, resolución o certificación, con su enlace.", Ejemplo: "Certificación de la Superintendencia Financiera"),
            new(Etiqueta, TipoDeValor.Texto, Largo: 200, Reglas: "Nombre del festivo o descripción del límite."),
            new(CorrigeA, TipoDeValor.Texto, Reglas: "PublicId del valor que esta fila corrige; exige motivo."),
            new(Motivo, TipoDeValor.Texto, Largo: 500, Reglas: "Obligatorio con corrigeA."),
        ]),
    ]);
}

/// <summary>
/// La importación de valores nacionales al registro central (feature 013, T37, C82; contracts/api.md §21.5; contracts/plantillas.md
/// §10). Dos formatos: la plantilla <c>saas.national-values</c> (.xlsx o .csv), leída con el mismo lector y las mismas reglas de
/// celda del ejecutor común, y el paquete <c>.json</c> de <see cref="ExportNationalValuesQuery"/> para llevar los valores entre
/// ambientes (B5). Reglas: sólo inserción; una fila igual a una vigente es «sin cambio»; la misma llave con otro valor y sin
/// <c>corrigeA</c> es <c>NationalValues.ConflictingValue</c>; cada fila nueva toma su <c>Sequence</c> en el orden del archivo;
/// <c>review</c> no escribe; <c>apply</c> es todo o nada en <b>su propia transacción sobre <see cref="IAdminDbContext"/></b> (un
/// solo guardado). No implementa <c>IComandoDeImportacion</c>: la idempotencia del ejecutor común vive en la base de una
/// cooperativa y aquí no hay ninguna. (nuevo)
/// </summary>
public sealed record ImportNationalValuesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File) : IRequest<Result<ImportResultDto>>
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportNationalValuesCommandHandler(
    IAdminDbContext db,
    ITabularFileReader lector,
    ICurrentCentralUserContext usuario,
    IDateTimeService reloj,
    IEnumerable<IValidadorDeDocumentoNormativo> validadores,
    IAuditAppendOnlyWriter? auditoria = null,
    ILogger<ImportNationalValuesCommandHandler>? logger = null)
    : IRequestHandler<ImportNationalValuesCommand, Result<ImportResultDto>>
{
    public const string HojaDeValores = "valores";
    public const string HojaDeDocumentos = "documentos";

    public async Task<Result<ImportResultDto>> Handle(ImportNationalValuesCommand request, CancellationToken ct)
    {
        var maestro = MaestroQueRegistra.De(usuario);
        if (maestro.IsFailure) return Result.Failure<ImportResultDto>(maestro.Error);
        if (request.Mode is not { } modo) return Result.Failure<ImportResultDto>(ImportErrors.ModeRequired);
        var archivo = request.File;
        if (archivo?.Contenido is not { Length: > 0 }) return Result.Failure<ImportResultDto>(ArchivosTabulares.Vacio);
        if (archivo.Contenido.Length > EjecutorDeImportacion.MaximoDeBytes)
            return Result.Failure<ImportResultDto>(ArchivosTabulares.Ilegible($"el archivo pesa más de {EjecutorDeImportacion.MaximoDeBytes / (1024 * 1024)} MB."));

        var esPaquete = archivo.NombreArchivo.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        var resultado = await NumeracionCentral.ConReintentoAsync(db, async () =>
        {
            db.DescartarCambios();
            var r = esPaquete
                ? await PaqueteAsync(archivo, modo, maestro.Value, ct)
                : await PlantillaAsync(archivo, modo, maestro.Value, ct);
            if (r.IsFailure || modo == ModoDeImportacion.Review || !r.Value.Valid)
            {
                db.DescartarCambios();
                return r.IsSuccess && modo == ModoDeImportacion.Apply ? Result.Failure<ImportResultDto>(ImportErrors.Invalid(r.Value)) : r;
            }
            await db.SaveChangesAsync(ct);
            return r;
        }, logger, ct);

        if (resultado.IsSuccess && resultado.Value.Applied)
            await AuditoriaDelRegistroCentral.RegistrarAsync(auditoria, maestro.Value, AuditoriaDelRegistroCentral.Importados, PlantillaDeValoresNacionales.Clave, null,
                new { file = archivo.NombreArchivo, sha256 = archivo.Sha256, sheets = resultado.Value.Sheets }, reloj.UtcNow, logger, ct);
        return resultado;
    }

    // -------------------------------------------------------------------------------------------- plantilla --

    private async Task<Result<ImportResultDto>> PlantillaAsync(ArchivoDeImportacion archivo, ModoDeImportacion modo, MaestroQueRegistra maestro, CancellationToken ct)
    {
        var preparado = await EjecutorDeImportacion.PrepararContextoAsync(lector, PlantillaDeValoresNacionales.Definicion, modo, archivo,
            motivo: null, new HashSet<string>(), todosLosPermisos: true, ct);
        if (preparado.IsFailure) return Result.Failure<ImportResultDto>(preparado.Error);
        var ctx = preparado.Value;
        var hoja = ctx.Datos;
        var ahora = reloj.UtcNow;

        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Texto(PlantillaDeValoresNacionales.Codigo)?.ToUpperInvariant();
            var clave = fila.Texto(PlantillaDeValoresNacionales.ClaveDelValor) ?? string.Empty;
            var desde = fila.Fecha(PlantillaDeValoresNacionales.VigenteDesde);
            var hasta = fila.Fecha(PlantillaDeValoresNacionales.VigenteHasta);
            var referencia = fila.Fecha(PlantillaDeValoresNacionales.FechaDeReferencia);
            var valor = fila.Decimal(PlantillaDeValoresNacionales.Valor, PlantillaDeValoresNacionales.DecimalesDelValor);
            var unidad = fila.Texto(PlantillaDeValoresNacionales.Unidad);
            var fuente = fila.Texto(PlantillaDeValoresNacionales.Fuente);
            var etiqueta = fila.Texto(PlantillaDeValoresNacionales.Etiqueta);
            var motivo = fila.Texto(PlantillaDeValoresNacionales.Motivo);
            var corrigeTexto = fila.Texto(PlantillaDeValoresNacionales.CorrigeA);
            Guid? corrige = null;
            if (corrigeTexto is not null)
            {
                if (Guid.TryParse(corrigeTexto, out var g)) corrige = g;
                else fila.Error(PlantillaDeValoresNacionales.CorrigeA, ImportErrors.CellFormat, $"«{corrigeTexto}» no es un identificador (PublicId) de valor nacional.");
                if (motivo is null)
                    fila.Error(PlantillaDeValoresNacionales.Motivo, ImportErrors.CellRequired, "Una corrección (corrigeA) lleva su motivo.");
            }
            if (fila.TieneErrores || codigo is null || desde is null || fuente is null) continue;

            // K6b (T1050): un código de una entrega posterior a la vigente (UVR y TOPE_TASA_VIVIENDA antes de K6b) todavía no se carga.
            if (CodigosDeValorNacional.Buscar(codigo) is { } definicion && !definicion.DisponibleEn())
            {
                fila.TodaviaNoDisponible(PlantillaDeValoresNacionales.Codigo, definicion.DisponibleDesde.ToString());
                continue;
            }

            if (corrige is { } anteriorId)
            {
                var anterior = db.PlatformNationalValues.Local.FirstOrDefault(v => v.PublicId == anteriorId)
                               ?? await db.PlatformNationalValues.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == anteriorId, ct);
                if (anterior is null)
                {
                    fila.Error(PlantillaDeValoresNacionales.CorrigeA, ErroresDeValoresNacionales.NotFoundCode, ErroresDeValoresNacionales.NoEncontrado.Message);
                    continue;
                }
                if (anterior.Code != codigo || anterior.Key != clave)
                {
                    fila.Error(PlantillaDeValoresNacionales.CorrigeA, ImportErrors.CellFormat,
                        $"corrigeA apunta a {anterior.Code} {anterior.Key}; la fila dice {codigo} {clave}.");
                    continue;
                }
                if (await ReglasDelRegistroCentral.ReemplazadaPorAsync(db, anteriorId, ct) is { } por)
                {
                    var e = ErroresDeValoresNacionales.YaCorregido(por);
                    fila.Error(PlantillaDeValoresNacionales.CorrigeA, e.Code, e.Message);
                    continue;
                }
            }

            var validado = await ReglasDelRegistroCentral.ValidarAsync(db, codigo, clave, desde.Value, hasta, valor, unidad, ct);
            if (validado.IsFailure)
            {
                fila.Error(ColumnaDe(validado.Error.Code), validado.Error.Code, validado.Error.Message);
                continue;
            }
            var v = validado.Value;
            if (!hoja.LlaveUnica(fila, $"{v.Codigo}|{v.Clave}|{v.ValidFrom:yyyy-MM-dd}", PlantillaDeValoresNacionales.Codigo)) continue;

            var llave = $"{v.Codigo} {v.Clave} {v.ValidFrom:yyyy-MM-dd}".Replace("  ", " ");
            if (corrige is null && await ReglasDelRegistroCentral.VigenteConLaMismaLlaveAsync(db, v.Codigo, v.Clave, v.ValidFrom, ct) is { } existente)
            {
                if (existente.Value == v.Valor && existente.Unit == v.Unidad && existente.ValidTo == v.ValidTo)
                    ctx.Registrar(fila, llave, AccionDeImportacion.Unchanged);
                else
                    fila.Error(PlantillaDeValoresNacionales.Valor, ErroresDeValoresNacionales.ConflictingValueCode,
                        ErroresDeValoresNacionales.MensajeDeValorEnConflicto(v.Codigo, v.Clave, v.ValidFrom, existente.Value));
                continue;
            }

            var nueva = RegisterNationalValueCommandHandler.Nueva(v, referencia, etiqueta, fuente, motivo, corrige,
                await NumeracionCentral.TomarAsync(db, ct), ahora, maestro);
            db.PlatformNationalValues.Add(nueva);
            ctx.Registrar(fila, llave, AccionDeImportacion.Create,
                [new(PlantillaDeValoresNacionales.Valor, null, v.Valor?.ToString(CultureInfo.InvariantCulture)), new("sequence", null, nueva.Sequence.ToString(CultureInfo.InvariantCulture))]);
        }

        return Result.Success(ctx.Resultado(archivo, aplicado: modo == ModoDeImportacion.Apply && !ctx.HayErrores));
    }

    private static string ColumnaDe(string codigo) => codigo switch
    {
        ErroresDeValoresNacionales.CodeUnknownCode or ErroresDeValoresNacionales.CodeNotAvailableCode => PlantillaDeValoresNacionales.Codigo,
        ErroresDeValoresNacionales.KeyInvalidCode => PlantillaDeValoresNacionales.ClaveDelValor,
        ErroresDeValoresNacionales.UnitInvalidCode => PlantillaDeValoresNacionales.Unidad,
        _ => PlantillaDeValoresNacionales.Valor,
    };

    // ---------------------------------------------------------------------------------------------- paquete --

    private async Task<Result<ImportResultDto>> PaqueteAsync(ArchivoDeImportacion archivo, ModoDeImportacion modo, MaestroQueRegistra maestro, CancellationToken ct)
    {
        PaqueteDeValoresNacionales? paquete;
        try
        {
            paquete = JsonSerializer.Deserialize<PaqueteDeValoresNacionales>(archivo.Contenido, PaqueteDeValoresNacionales.Json);
        }
        catch (JsonException ex)
        {
            return Result.Failure<ImportResultDto>(ArchivosTabulares.Ilegible($"el paquete no es JSON válido: {ex.Message}"));
        }
        if (paquete is null || paquete.Format != PaqueteDeValoresNacionales.Formato)
            return Result.Failure<ImportResultDto>(ArchivosTabulares.Ilegible($"no es un paquete {PaqueteDeValoresNacionales.Formato}."));

        var errores = new List<ErrorDeFila>();
        var cambios = new List<CambioDeFilaDto>();
        int vCreadas = 0, vIguales = 0, dCreados = 0, dIguales = 0;
        var ahora = reloj.UtcNow;

        // El orden del paquete es el de la Sequence de origen: los documentos y los valores se intercalan como se registraron.
        var pasos = paquete.Values.Select((v, i) => (Seq: v.Sequence, Valor: v, Documento: (DocumentoDelPaquete?)null, Fila: i + 1))
            .Concat(paquete.Documents.Select((d, i) => (Seq: d.Sequence, Valor: (ValorDelPaquete?)null, Documento: (DocumentoDelPaquete?)d, Fila: i + 1)))
            .OrderBy(p => p.Seq).ToList();

        foreach (var paso in pasos)
        {
            if (paso.Documento is { } d)
            {
                if (db.PlatformRegulatoryDocuments.Local.Any(x => x.PublicId == d.PublicId)
                    || await db.PlatformRegulatoryDocuments.AsNoTracking().AnyAsync(x => x.PublicId == d.PublicId, ct))
                {
                    dIguales++;
                    continue;
                }
                var canonico = ContenidoCanonico.Canonizar(d.Content);
                if (ContenidoCanonico.Sha256(canonico) != d.ContentSha256)
                {
                    errores.Add(new ErrorDeFila(paso.Fila, "contentSha256", ErroresDeValoresNacionales.DocumentInvalidCode,
                        $"El contenido de {d.Kind} {d.Code} no corresponde a su huella: el paquete se alteró.", HojaDeDocumentos));
                    continue;
                }
                var r = await RegisterRegulatoryDocumentCommandHandler.RegistrarAsync(db, validadores,
                    new RegisterRegulatoryDocumentCommand(d.Kind, d.Code, d.ValidFrom, d.ValidTo, d.SchemaVersion, d.Content, d.Verified, d.Source, d.SupersedesPublicId, d.Reason),
                    maestro, ahora, ct, d.PublicId, guardar: false);
                if (r.IsFailure)
                {
                    errores.Add(new ErrorDeFila(paso.Fila, "content", r.Error.Code, r.Error.Message, HojaDeDocumentos));
                    continue;
                }
                dCreados++;
                cambios.Add(new CambioDeFilaDto(HojaDeDocumentos, paso.Fila, $"{d.Kind} {d.Code}", AccionDeImportacion.Create, [new("contentSha256", null, d.ContentSha256)]));
                continue;
            }

            var v = paso.Valor!;
            if (db.PlatformNationalValues.Local.Any(x => x.PublicId == v.PublicId)
                || await db.PlatformNationalValues.AsNoTracking().AnyAsync(x => x.PublicId == v.PublicId, ct))
            {
                vIguales++;
                continue;
            }
            if (v.SupersedesPublicId is { } anterior)
            {
                var existe = db.PlatformNationalValues.Local.Any(x => x.PublicId == anterior)
                             || await db.PlatformNationalValues.AsNoTracking().AnyAsync(x => x.PublicId == anterior, ct);
                if (!existe)
                {
                    errores.Add(new ErrorDeFila(paso.Fila, "supersedesPublicId", ErroresDeValoresNacionales.NotFoundCode,
                        $"La corrección de {v.Code} {v.Key} apunta a un valor que no está en esta base ni en el paquete.", HojaDeValores));
                    continue;
                }
                if (await ReglasDelRegistroCentral.ReemplazadaPorAsync(db, anterior, ct) is { } por)
                {
                    var e = ErroresDeValoresNacionales.YaCorregido(por);
                    errores.Add(new ErrorDeFila(paso.Fila, "supersedesPublicId", e.Code, e.Message, HojaDeValores));
                    continue;
                }
            }
            var validado = await ReglasDelRegistroCentral.ValidarAsync(db, v.Code, v.Key, v.ValidFrom, v.ValidTo, v.Value, v.Unit, ct);
            if (validado.IsFailure)
            {
                errores.Add(new ErrorDeFila(paso.Fila, ColumnaDe(validado.Error.Code), validado.Error.Code, validado.Error.Message, HojaDeValores));
                continue;
            }
            var ok = validado.Value;
            if (v.SupersedesPublicId is null && await ReglasDelRegistroCentral.VigenteConLaMismaLlaveAsync(db, ok.Codigo, ok.Clave, ok.ValidFrom, ct) is { } existente)
            {
                if (existente.Value == ok.Valor) vIguales++;
                else errores.Add(new ErrorDeFila(paso.Fila, "value", ErroresDeValoresNacionales.ConflictingValueCode,
                    ErroresDeValoresNacionales.MensajeDeValorEnConflicto(ok.Codigo, ok.Clave, ok.ValidFrom, existente.Value), HojaDeValores));
                continue;
            }
            db.PlatformNationalValues.Add(RegisterNationalValueCommandHandler.Nueva(ok, v.ReferenceDate, v.Label, v.Source, v.Reason, v.SupersedesPublicId,
                await NumeracionCentral.TomarAsync(db, ct), ahora, maestro, v.PublicId));
            vCreadas++;
            cambios.Add(new CambioDeFilaDto(HojaDeValores, paso.Fila, $"{ok.Codigo} {ok.Clave} {ok.ValidFrom:yyyy-MM-dd}".Replace("  ", " "),
                AccionDeImportacion.Create, [new("value", null, ok.Valor?.ToString(CultureInfo.InvariantCulture))]));
        }

        return Result.Success(new ImportResultDto
        {
            Template = PlantillaDeValoresNacionales.Clave,
            Mode = modo,
            Valid = errores.Count == 0,
            Applied = modo == ModoDeImportacion.Apply && errores.Count == 0,
            FileName = archivo.NombreArchivo,
            FileSha256 = archivo.Sha256,
            Sheets =
            [
                new ResumenDeHojaDto(HojaDeValores, paquete.Values.Count, vCreadas, 0, vIguales),
                new ResumenDeHojaDto(HojaDeDocumentos, paquete.Documents.Count, dCreados, 0, dIguales),
            ],
            Changes = cambios.Take(EjecutorDeImportacion.MaximoDeCambios).ToList(),
            ChangesTruncated = cambios.Count > EjecutorDeImportacion.MaximoDeCambios,
            Errors = errores.Take(EjecutorDeImportacion.MaximoDeErrores).ToList(),
            TotalErrors = errores.Count,
        });
    }
}

// ---------------------------------------------------------------------------------------------------- exportar --

/// <summary>
/// El paquete <c>.json</c> del registro central (feature 013, B5; contracts/api.md §21.5): valores y documentos con su
/// <c>PublicId</c>, su <c>Sequence</c> de origen (el orden) y, los documentos, su contenido y SHA-256. Importarlo en otro ambiente
/// produce las mismas filas con los mismos <c>PublicId</c> (así una corrección posterior apunta a lo mismo en los dos). (nuevo)
/// </summary>
public sealed record PaqueteDeValoresNacionales(
    string Format,
    int Version,
    DateTime GeneratedAt,
    IReadOnlyList<ValorDelPaquete> Values,
    IReadOnlyList<DocumentoDelPaquete> Documents)
{
    public const string Formato = "ingenia365.national-values";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
}

public sealed record ValorDelPaquete(
    Guid PublicId, long Sequence, string Code, string Key, DateOnly ValidFrom, DateOnly? ValidTo, DateOnly? ReferenceDate,
    decimal? Value, string? Unit, string? Label, string Source, string? Reason, Guid? SupersedesPublicId);

public sealed record DocumentoDelPaquete(
    Guid PublicId, long Sequence, string Kind, string Code, DateOnly ValidFrom, DateOnly? ValidTo, short SchemaVersion, bool Verified,
    string Source, string? Reason, Guid? SupersedesPublicId, string Content, string ContentSha256);

/// <summary>El archivo exportado: nombre, tipo y bytes. (nuevo)</summary>
public sealed record ArchivoDePaquete(string NombreArchivo, string TipoDeContenido, byte[] Contenido);

/// <summary>
/// Exporta el registro central a un paquete <c>.json</c> (feature 013, B5): los valores con <c>ValidFrom</c> desde
/// <see cref="From"/> de los <see cref="Codes"/> pedidos (todos si no vienen) más las filas que sus correcciones reemplazan, para
/// que el paquete se importe solo; y los documentos normativos desde la misma fecha. (nuevo)
/// </summary>
public sealed record ExportNationalValuesQuery(DateOnly? From = null, IReadOnlyList<string>? Codes = null) : IRequest<Result<ArchivoDePaquete>>;

public sealed class ExportNationalValuesQueryHandler(IAdminDbContext db, IDateTimeService reloj)
    : IRequestHandler<ExportNationalValuesQuery, Result<ArchivoDePaquete>>
{
    public async Task<Result<ArchivoDePaquete>> Handle(ExportNationalValuesQuery request, CancellationToken ct)
    {
        var todos = await db.PlatformNationalValues.AsNoTracking().ToListAsync(ct);
        var codigos = request.Codes?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
        var elegidos = todos.Where(v => (request.From is null || v.ValidFrom >= request.From) && (codigos is null || codigos.Count == 0 || codigos.Contains(v.Code)))
            .ToDictionary(v => v.PublicId);

        // Lo que reemplazan las correcciones elegidas, aunque sea anterior a la fecha: sin ello el paquete no se importa.
        var porId = todos.ToDictionary(v => v.PublicId);
        var pendientes = new Queue<PlatformNationalValue>(elegidos.Values);
        while (pendientes.Count > 0)
        {
            var v = pendientes.Dequeue();
            if (v.SupersedesPublicId is { } anterior && !elegidos.ContainsKey(anterior) && porId.TryGetValue(anterior, out var fila))
            {
                elegidos[anterior] = fila;
                pendientes.Enqueue(fila);
            }
        }

        var documentos = await db.PlatformRegulatoryDocuments.AsNoTracking()
            .Where(d => request.From == null || d.ValidFrom >= request.From)
            .OrderBy(d => d.Sequence).ToListAsync(ct);

        var paquete = new PaqueteDeValoresNacionales(PaqueteDeValoresNacionales.Formato, 1, reloj.UtcNow,
            elegidos.Values.OrderBy(v => v.Sequence).Select(v => new ValorDelPaquete(v.PublicId, v.Sequence, v.Code, v.Key, v.ValidFrom, v.ValidTo,
                v.ReferenceDate, v.Value, v.Unit, v.Label, v.Source, v.Reason, v.SupersedesPublicId)).ToList(),
            documentos.Select(d => new DocumentoDelPaquete(d.PublicId, d.Sequence, d.Kind, d.Code, d.ValidFrom, d.ValidTo, d.SchemaVersion, d.Verified,
                d.Source, d.Reason, d.SupersedesPublicId, d.Content, d.ContentSha256)).ToList());

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(paquete, PaqueteDeValoresNacionales.Json));
        return Result.Success(new ArchivoDePaquete($"valores-nacionales-{reloj.HoyLocal:yyyyMMdd}.json", "application/json", bytes));
    }
}
