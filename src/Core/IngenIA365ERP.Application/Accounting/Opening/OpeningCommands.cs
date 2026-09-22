using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Setup;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Opening;

// Saldos de apertura (feature 009 E2, US13; FR-084..FR-087; contracts/api.md §7).
//
// La cooperativa que llega desde SOLIDO carga una sola vez los saldos con que arranca: un archivo
// (o la digitación) con cuenta, tercero, documento cruce, centro de costo, sucursal y débito o
// crédito. El resultado es un BORRADOR de tipo `AP` (`Kind = Opening`) fechado la víspera del primer
// período —la única fecha que el contrato admite fuera de un período abierto— que sigue el flujo de
// cualquier comprobante manual: se revisa en Comprobantes, se contabiliza con `Vouchers.Post` (cuatro
// ojos si la empresa lo exige) y se corrige reversándolo. Las consultas lo tratan como saldo inicial y
// nunca como movimiento del mes (MovimientosContables). Importar valida cada fila con las mismas
// reglas de cuenta que la digitación (FR-014) y con un solo error no guarda nada; el descuadre no es
// error de importación —el borrador se guarda descuadrado y es contabilizarlo lo que exige cuadre—.
// A lo sumo una apertura contabilizada y no reversada por empresa (FR-087): `AccountingSetup.
// OpeningDocumentId` la referencia y la escribe sólo quien contabiliza o reversa (DocumentCommands).

/// <summary>Un borrador de apertura pendiente de contabilizar, o una apertura reversada, tal como los lista <see cref="EstadoDeAperturaDto"/>.</summary>
public sealed record AperturaResumenDto(Guid PublicId, long? Number, DateOnly Date, string Status, decimal TotalDebit, decimal TotalCredit, int Lines, string RegisteredBy, DateTime? PostedAt, string? PostedBy, Guid? ReversedByPublicId);

/// <summary>
/// <c>GET /api/accounting/opening</c>: la fecha propuesta y hasta cuándo se puede mover, la apertura
/// vigente si la hay, los borradores pendientes y las reversadas.
/// </summary>
public sealed record EstadoDeAperturaDto(
    DateOnly ExpectedDate,
    DateOnly MaxDate,
    int FirstFiscalYear,
    AperturaResumenDto? Posted,
    IReadOnlyList<AperturaResumenDto> Drafts,
    IReadOnlyList<AperturaResumenDto> Reversed);

/// <summary><c>POST /import</c> → 201: el borrador (nuevo o el que ya había, con sus líneas reemplazadas), cuántas líneas trae y su fecha; <c>Errors</c> va vacío (con errores no hay borrador: 422 <c>Accounting.Opening.Invalid</c>).</summary>
public sealed record AperturaImportadaDto(Guid DraftPublicId, int Lines, decimal TotalDebit, decimal TotalCredit, DateOnly Date, bool Replaced, IReadOnlyList<ErrorDeFila> Errors);

/// <summary>Las columnas de la plantilla, en su orden; los encabezados se comparan sin tildes ni mayúsculas.</summary>
public static class PlantillaDeApertura
{
    public const string Cuenta = "cuenta";
    public const string Tercero = "tercero";
    public const string TipoDocumento = "tipoDocumento";
    public const string NumeroDocumento = "numeroDocumento";
    public const string CentroCosto = "centroCosto";
    public const string Sucursal = "sucursal";
    public const string Debito = "debito";
    public const string Credito = "credito";
    public const string Detalle = "detalle";

    public static readonly IReadOnlyList<string> Columnas = [Cuenta, Tercero, TipoDocumento, NumeroDocumento, CentroCosto, Sucursal, Debito, Credito, Detalle];
    public static readonly IReadOnlyList<string> Obligatorias = [Cuenta, Debito, Credito];

    public const string Tipo = "AP";
    public const string Descripcion = "Saldos de apertura";
}

// ---------------------------------------------------------------------------------- estado --

public sealed record GetOpeningStatusQuery : IRequest<Result<EstadoDeAperturaDto>>;

public sealed class GetOpeningStatusQueryValidator : AbstractValidator<GetOpeningStatusQuery>;

public sealed class GetOpeningStatusQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOpeningStatusQuery, Result<EstadoDeAperturaDto>>
{
    public async Task<Result<EstadoDeAperturaDto>> Handle(GetOpeningStatusQuery request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<EstadoDeAperturaDto>(AccountingErrors.NotInitialized);
        var fecha = await Aperturas.FechaEsperadaAsync(db, ct);
        if (fecha.IsFailure) return Result.Failure<EstadoDeAperturaDto>(fecha.Error);
        var tope = await Aperturas.FechaMaximaAsync(db, ct);

        var aperturas = await db.AccountingDocuments.AsNoTracking()
            .Where(d => !d.IsDeleted && d.Kind == DocumentKind.Opening && d.ReversesDocumentId == null)
            .OrderBy(d => d.CreatedAt)
            .Select(d => new AperturaResumenDto(d.PublicId, d.Number, d.Date, d.Status.ToString(), d.TotalDebit, d.TotalCredit,
                d.Lines.Count(l => !l.IsDeleted), d.RegisteredBy, d.PostedAt, d.PostedBy, d.ReversedByDocument != null ? d.ReversedByDocument.PublicId : null))
            .ToListAsync(ct);

        return Result.Success(new EstadoDeAperturaDto(
            fecha.Value, tope, setup.FirstFiscalYear,
            aperturas.FirstOrDefault(a => a.Status == nameof(DocumentStatus.Posted)),
            aperturas.Where(a => a.Status == nameof(DocumentStatus.Draft)).ToList(),
            aperturas.Where(a => a.Status == nameof(DocumentStatus.Reversed)).ToList()));
    }
}

// -------------------------------------------------------------------------------- plantilla --

/// <summary>La plantilla vacía con los encabezados y sus notas; la API la entrega como <c>.xlsx</c> por el exportador de tablas.</summary>
public sealed record GetOpeningTemplateQuery : IRequest<Result<TablaExportable>>;

public sealed class GetOpeningTemplateQueryValidator : AbstractValidator<GetOpeningTemplateQuery>;

public sealed class GetOpeningTemplateQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOpeningTemplateQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(GetOpeningTemplateQuery request, CancellationToken ct)
    {
        if (!await db.AccountingSetups.AsNoTracking().AnyAsync(s => !s.IsDeleted, ct)) return Result.Failure<TablaExportable>(AccountingErrors.NotInitialized);
        var fecha = await Aperturas.FechaEsperadaAsync(db, ct);
        if (fecha.IsFailure) return Result.Failure<TablaExportable>(fecha.Error);

        var columnas = PlantillaDeApertura.Columnas.Select(c => new ColumnaExportable(c, c is PlantillaDeApertura.Debito or PlantillaDeApertura.Credito ? TipoDeColumna.Moneda : TipoDeColumna.Texto)).ToList();
        return Result.Success(new TablaExportable(
            "Plantilla de saldos de apertura",
            $"Una fila por cuenta de movimiento (y por tercero y documento donde la cuenta lo exige). El comprobante se fecha el {fecha.Value:yyyy-MM-dd}.",
            columnas,
            [],
            null,
            [
                "cuenta: el código completo de la auxiliar de movimiento (no una de agrupación).",
                "tercero: el documento de identidad de la persona (cédula o NIT sin dígito de verificación), sólo si la cuenta lo exige o se quiere el detalle.",
                "tipoDocumento y numeroDocumento: el documento cruce (por ejemplo PG 1234) donde la cuenta lo exige.",
                "centroCosto: su código o su nombre, sólo en cuentas que lo manejan. sucursal: su código, sigla o nombre; vacío = la principal.",
                "debito o credito: uno de los dos, mayor que cero, con hasta dos decimales y sin separador de miles (el decimal puede ir con punto o coma).",
                "El archivo no tiene que cuadrar para importarse: queda como borrador y es al contabilizar cuando se exige el cuadre.",
            ]));
    }
}

// --------------------------------------------------------------------------------- importar --

/// <summary>
/// Importa el archivo de saldos. <paramref name="Date"/> nula = la fecha propuesta (la víspera del
/// primer período); si se indica, manda la elegida. Si ya hay un borrador de apertura, sus líneas se
/// <b>reemplazan</b> por las del archivo (el borrador conserva su <c>PublicId</c> y sus adjuntos):
/// volver a subir el archivo corregido es la forma natural de rehacer la carga.
/// </summary>
public sealed record ImportOpeningBalancesCommand(string FileName, byte[] Content, DateOnly? Date = null) : IRequest<Result<AperturaImportadaDto>>;

public sealed class ImportOpeningBalancesCommandValidator : AbstractValidator<ImportOpeningBalancesCommand>
{
    public ImportOpeningBalancesCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Indique el archivo.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportOpeningBalancesCommandHandler(
    IApplicationDbContext db,
    ITabularFileReader lector,
    IDateTimeService clock,
    ICurrentUserService user,
    IUserBranchScope scope,
    AccountingPoster poster,
    AccountingAuditEmitter audit)
    : IRequestHandler<ImportOpeningBalancesCommand, Result<AperturaImportadaDto>>
{
    private sealed record FilaResuelta(FilaLeida Fila, PostingLine Linea, ChartOfAccount Cuenta, int? CrossDocumentTypeId);

    public async Task<Result<AperturaImportadaDto>> Handle(ImportOpeningBalancesCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Fallo(AccountingErrors.NotInitialized);

        var vigente = await Aperturas.VigenteAsync(db, ct);
        if (vigente is not null) return Fallo(Aperturas.YaExiste(vigente));

        var tipo = await db.VoucherTypes.AsNoTracking().FirstOrDefaultAsync(v => !v.IsDeleted && v.Usage == VoucherUsage.Opening, ct);
        if (tipo is null) return Fallo(AccountingErrors.VoucherTypeNotFound(PlantillaDeApertura.Tipo));
        if (!tipo.IsActive) return Fallo(AccountingErrors.VoucherTypeInactive(tipo.Code));

        var propuesta = await Aperturas.FechaEsperadaAsync(db, ct);
        if (propuesta.IsFailure) return Fallo(propuesta.Error);
        var fecha = request.Date ?? propuesta.Value;
        if (await AccountingPoster.FechaDeAperturaInvalidaAsync(db, fecha, ct) is { } reparoDeFecha) return Fallo(reparoDeFecha);

        var lectura = await lector.LeerAsync(request.Content, request.FileName, 1, ct);
        if (lectura.IsFailure) return Fallo(lectura.Error);
        var tabla = lectura.Value;
        var indices = PlantillaDeApertura.Columnas.ToDictionary(c => c, tabla.IndiceDe, StringComparer.Ordinal);
        foreach (var obligatoria in PlantillaDeApertura.Obligatorias)
            if (indices[obligatoria] < 0) return Fallo(ArchivosTabulares.SinEncabezado(obligatoria));

        var filas = tabla.Filas.Where(f => !f.EstaVacia).ToList();
        if (filas.Count == 0) return Fallo(ArchivosTabulares.Vacio);

        // --- referencias, consultadas una vez para todo el archivo ---
        string? Celda(FilaLeida f, string columna) => indices[columna] < 0 ? null : f[indices[columna]]?.Trim();

        var codigos = filas.Select(f => Celda(f, PlantillaDeApertura.Cuenta)).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var cuentas = (await db.ChartOfAccounts.Include(c => c.TaxRates).Where(c => !c.IsDeleted && codigos.Contains(c.Code)).ToListAsync(ct))
            .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        var documentos = filas.Select(f => Celda(f, PlantillaDeApertura.Tercero)).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var terceros = documentos.Count == 0
            ? new Dictionary<string, int>()
            : (await db.People.AsNoTracking().Where(p => !p.IsDeleted && documentos.Contains(p.TaxId)).Select(p => new { p.TaxId, p.Id }).ToListAsync(ct))
                .GroupBy(p => p.TaxId).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var sucursales = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).Select(b => new { b.Id, b.Name, b.ShortName, b.LegacyCode }).ToListAsync(ct);
        var centros = await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.Name, c.LegacyCode }).ToListAsync(ct);
        var tiposDeCruce = await db.CrossDocumentTypes.AsNoTracking().Where(t => !t.IsDeleted && t.IsActive).ToDictionaryAsync(t => t.Code, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);

        var alcance = await scope.ObtenerAsync(ct);
        var sucursalPropuesta = alcance.SucursalPorDefecto ?? setup.MainBranchId;

        // --- fila a fila: lo que no se puede ni guardar (referencias inexistentes, importes ilegibles) ---
        var errores = new List<ErrorDeFila>();
        var resueltas = new List<FilaResuelta>(filas.Count);
        foreach (var fila in filas)
        {
            var n = fila.Numero;
            var erroresAntes = errores.Count;
            var codigo = Celda(fila, PlantillaDeApertura.Cuenta) ?? string.Empty;
            var cuenta = codigo.Length == 0 ? null : cuentas.GetValueOrDefault(codigo);
            if (cuenta is null)
                errores.Add(new ErrorDeFila(n, PlantillaDeApertura.Cuenta, "Accounting.Line.AccountNotFound", codigo.Length == 0 ? "La fila no trae cuenta." : $"La cuenta {codigo} no existe."));

            int? tercero = null;
            var documento = Celda(fila, PlantillaDeApertura.Tercero);
            if (!string.IsNullOrEmpty(documento))
            {
                if (terceros.TryGetValue(documento, out var id)) tercero = id;
                else errores.Add(new ErrorDeFila(n, PlantillaDeApertura.Tercero, "Accounting.Line.ThirdPartyInvalid", $"No hay una persona con documento {documento}; créela en Personas antes de importar."));
            }

            int? centro = null;
            var centroTexto = Celda(fila, PlantillaDeApertura.CentroCosto);
            if (!string.IsNullOrEmpty(centroTexto))
            {
                var c = centros.FirstOrDefault(x => string.Equals(x.LegacyCode, centroTexto, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, centroTexto, StringComparison.OrdinalIgnoreCase));
                if (c is null) errores.Add(new ErrorDeFila(n, PlantillaDeApertura.CentroCosto, "Accounting.Line.CostCenterInvalid", $"No hay un centro de costo con código o nombre «{centroTexto}»."));
                else centro = c.Id;
            }

            int? sucursal = null;
            var sucursalTexto = Celda(fila, PlantillaDeApertura.Sucursal);
            if (!string.IsNullOrEmpty(sucursalTexto))
            {
                var s = sucursales.FirstOrDefault(x => string.Equals(x.LegacyCode, sucursalTexto, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(x.ShortName, sucursalTexto, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(x.Name, sucursalTexto, StringComparison.OrdinalIgnoreCase));
                if (s is null) errores.Add(new ErrorDeFila(n, PlantillaDeApertura.Sucursal, "Accounting.Line.BranchInvalid", $"No hay una sucursal con código, sigla o nombre «{sucursalTexto}»."));
                else sucursal = s.Id;
            }

            var debito = Importe(Celda(fila, PlantillaDeApertura.Debito), n, PlantillaDeApertura.Debito, errores);
            var credito = Importe(Celda(fila, PlantillaDeApertura.Credito), n, PlantillaDeApertura.Credito, errores);
            if (debito is { } d && credito is { } c2 && !Rules.AccountLineRules.ImporteValido(d, c2))
                errores.Add(new ErrorDeFila(n, PlantillaDeApertura.Debito, "Accounting.Line.AmountInvalid", "Cada fila lleva débito o crédito mayor que cero, nunca ambos ni ninguno."));

            var tipoCruce = Celda(fila, PlantillaDeApertura.TipoDocumento)?.ToUpperInvariant();
            int? tipoCruceId = null;
            if (!string.IsNullOrEmpty(tipoCruce))
            {
                if (tiposDeCruce.TryGetValue(tipoCruce, out var tid)) tipoCruceId = tid;
                else errores.Add(new ErrorDeFila(n, PlantillaDeApertura.TipoDocumento, "Accounting.Line.CrossDocumentTypeInvalid", $"El tipo de documento cruce {tipoCruce} no existe o está inactivo."));
            }

            // Una fila con referencias que no existen no llega a las reglas: ya se sabe qué le falta y
            // volver a evaluarla sólo repetiría el mismo problema con otras palabras.
            if (errores.Count > erroresAntes || cuenta is null || debito is null || credito is null) continue;
            resueltas.Add(new FilaResuelta(fila, new PostingLine
            {
                AccountId = cuenta.Id, AccountCode = cuenta.Code, Debit = debito.Value, Credit = credito.Value,
                Detail = Celda(fila, PlantillaDeApertura.Detalle), PersonId = tercero, BranchId = sucursal, CostCenterId = centro,
                CrossDocumentType = string.IsNullOrEmpty(tipoCruce) ? null : tipoCruce, CrossDocumentNumber = Celda(fila, PlantillaDeApertura.NumeroDocumento), TaxBase = null,
            }, cuenta, tipoCruceId));
        }

        // --- las reglas de cuenta del contrato (FR-014), fila por fila; el descuadre no es error de importación ---
        if (resueltas.Count > 0)
        {
            var validacion = await poster.ValidarAsync(new PostingRequest(tipo.Code, fecha, PlantillaDeApertura.Descripcion, AccountingOrigin.Manual(Guid.Empty),
                resueltas.Select(r => r.Linea).ToList(), DocumentKind.Opening), ct);
            foreach (var e in validacion.Errores)
            {
                if (e.EsDeEncabezado)
                {
                    if (e.Code == "Accounting.Document.Unbalanced") continue;
                    return Fallo(new Error(e.Code, e.Message));
                }
                errores.Add(new ErrorDeFila(resueltas[e.LineNumber - 1].Fila.Numero, ColumnaDe(e.Field), e.Code, e.Message));
            }
        }

        if (errores.Count > 0)
            return Fallo(AccountingErrors.OpeningInvalid(errores.OrderBy(e => e.Row).ThenBy(e => e.Column, StringComparer.Ordinal).ToList()));

        // --- el borrador AP, por el contrato (único sitio que instancia documentos y líneas) ---
        var ahora = clock.UtcNow;
        var quien = string.IsNullOrWhiteSpace(user.UserName) ? "system" : user.UserName;
        // Si ya había un borrador, se reutiliza y sus líneas se dan de baja (nunca Remove: Principio XI
        // lo vigila también en los borradores) para que el archivo nuevo sea el que manda.
        var anterior = await db.AccountingDocuments.Include(d => d.Lines)
            .FirstOrDefaultAsync(d => !d.IsDeleted && d.Kind == DocumentKind.Opening && d.Status == DocumentStatus.Draft, ct);
        var documentoAp = anterior ?? poster.NuevoBorrador();
        if (anterior is not null)
        {
            foreach (var vieja in anterior.Lines.Where(l => !l.IsDeleted))
            {
                vieja.IsDeleted = true;
                vieja.DeletedAt = ahora;
                vieja.UpdatedAt = ahora;
                vieja.UpdatedBy = quien;
            }
            anterior.UpdatedAt = ahora;
            anterior.UpdatedBy = quien;
            anterior.RegisteredByUserId = user.UserId ?? 0;
            anterior.RegisteredBy = quien;
        }
        documentoAp.Kind = DocumentKind.Opening;
        documentoAp.VoucherTypeId = tipo.Id;
        documentoAp.Date = fecha;
        documentoAp.PeriodId = null;
        documentoAp.Description = $"{PlantillaDeApertura.Descripcion} ({request.FileName})";
        documentoAp.TotalDebit = resueltas.Sum(r => r.Linea.Debit);
        documentoAp.TotalCredit = resueltas.Sum(r => r.Linea.Credit);
        var numero = 0;
        foreach (var r in resueltas)
        {
            var linea = poster.NuevaLineaDeBorrador();
            linea.LineNumber = ++numero;
            linea.AccountId = r.Cuenta.Id;
            linea.BranchId = r.Linea.BranchId ?? sucursalPropuesta;
            linea.CostCenterId = r.Linea.CostCenterId;
            linea.PersonId = r.Linea.PersonId;
            linea.CrossDocumentTypeId = r.CrossDocumentTypeId;
            linea.CrossDocumentNumber = string.IsNullOrWhiteSpace(r.Linea.CrossDocumentNumber) ? null : r.Linea.CrossDocumentNumber.Trim();
            linea.Debit = r.Linea.Debit;
            linea.Credit = r.Linea.Credit;
            linea.Description = string.IsNullOrWhiteSpace(r.Linea.Detail) ? null : r.Linea.Detail.Trim();
            linea.Date = fecha;
            linea.IsPosted = false;
            documentoAp.Lines.Add(linea);
        }
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync("Accounting.Opening.Imported", nameof(AccountingDocument), documentoAp.PublicId, null,
            new { file = request.FileName, lines = numero, totalDebit = documentoAp.TotalDebit, totalCredit = documentoAp.TotalCredit, date = fecha, replaced = anterior is not null }, ct);
        return Result.Success(new AperturaImportadaDto(documentoAp.PublicId, numero, documentoAp.TotalDebit, documentoAp.TotalCredit, fecha, anterior is not null, []));
    }

    /// <summary>Un importe de la plantilla: vacío = 0; decimal con punto o coma, sin miles; a lo sumo dos decimales.</summary>
    private static decimal? Importe(string? texto, int fila, string columna, List<ErrorDeFila> errores)
    {
        if (string.IsNullOrWhiteSpace(texto)) return 0m;
        var limpio = texto.Trim().Replace(" ", string.Empty);
        if (!limpio.Contains('.')) limpio = limpio.Replace(',', '.');
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor))
        {
            errores.Add(new ErrorDeFila(fila, columna, "Accounting.Line.AmountInvalid", $"«{texto}» no es un importe: use sólo dígitos y el separador decimal (punto o coma), sin miles."));
            return null;
        }
        if (valor < 0m)
        {
            errores.Add(new ErrorDeFila(fila, columna, "Accounting.Line.AmountInvalid", "Los importes van en positivo: el lado lo dice la columna."));
            return null;
        }
        if (decimal.Round(valor, 2) != valor)
        {
            errores.Add(new ErrorDeFila(fila, columna, "Accounting.Line.AmountInvalid", $"«{texto}» tiene más de dos decimales."));
            return null;
        }
        return valor;
    }

    private static string ColumnaDe(string campo) => campo switch
    {
        "Account" => PlantillaDeApertura.Cuenta,
        "Person" => PlantillaDeApertura.Tercero,
        "CrossDocument" => PlantillaDeApertura.TipoDocumento,
        "CostCenter" => PlantillaDeApertura.CentroCosto,
        "Branch" => PlantillaDeApertura.Sucursal,
        "Debit" => PlantillaDeApertura.Debito,
        "TaxBase" => PlantillaDeApertura.Debito,
        _ => campo,
    };

    private static Result<AperturaImportadaDto> Fallo(Error error) => Result.Failure<AperturaImportadaDto>(error);
}

// ------------------------------------------------------------------------------------ común --

/// <summary>Lo que comparten importar, contabilizar y consultar la apertura: su fecha y cuál es la vigente.</summary>
public static class Aperturas
{
    /// <summary>Hasta cuándo se puede mover la apertura: el último día del primer ejercicio (E2, 2026-09-22).</summary>
    public static async Task<DateOnly> FechaMaximaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var primero = await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.StartDate)
            .Select(p => (int?)p.FiscalYearId).FirstOrDefaultAsync(ct);
        if (primero is null) return default;
        return await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted && p.FiscalYearId == primero.Value).MaxAsync(p => p.EndDate, ct);
    }

    /// <summary>La víspera del primer período del primer ejercicio: la fecha que se propone (FR-084).</summary>
    public static async Task<Result<DateOnly>> FechaEsperadaAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var primero = await db.AccountingPeriods.AsNoTracking().Where(p => !p.IsDeleted).OrderBy(p => p.StartDate).Select(p => (DateOnly?)p.StartDate).FirstOrDefaultAsync(ct);
        return primero is { } inicio ? Result.Success(inicio.AddDays(-1)) : Result.Failure<DateOnly>(AccountingErrors.FiscalYearNotFound);
    }

    /// <summary>La apertura contabilizada y no reversada, si existe (FR-087). Con seguimiento: quien la encuentra puede tener que tocarla.</summary>
    public static Task<AccountingDocument?> VigenteAsync(IApplicationDbContext db, CancellationToken ct) =>
        db.AccountingDocuments.FirstOrDefaultAsync(d => !d.IsDeleted && d.Kind == DocumentKind.Opening && d.Status == DocumentStatus.Posted && d.ReversesDocumentId == null, ct);

    public static Error YaExiste(AccountingDocument vigente) =>
        new ErrorConDatos(AccountingErrors.OpeningAlreadyExists.Code, AccountingErrors.OpeningAlreadyExists.Message,
            new { openingDocumentPublicId = vigente.PublicId, number = vigente.Number, date = vigente.Date });
}
