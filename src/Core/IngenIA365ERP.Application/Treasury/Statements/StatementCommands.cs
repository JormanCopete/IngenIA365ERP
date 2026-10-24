using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Huellas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Statements;

// Feature 015, US6 (T0193; R-06; FR-065, FR-066, FR-196, FR-197; contracts/api.md §12): el extracto bancario se carga UNA vez, en Tesorería, desde su pantalla o desde la
// de Recaudo, con el formato del banco (BankStatement = 7), la plantilla genérica EXTRACTO-GENERICO o a mano, con revisión previa y error por línea. Recaudo identifica sus
// abonos por el puerto IMovimientosBancariosParaRecaudo.

/// <summary>Un movimiento capturado a mano (FR-196): fecha, valor con signo (positivo es un abono) y lo que se sepa del consignante. (nuevo)</summary>
public sealed record MovimientoCapturado(DateOnly MovementDate, decimal Amount, string? Reference = null, string? BankSequence = null, string? DepositorTaxId = null,
    string? DepositorName = null, string? Description = null, decimal? Balance = null);

/// <summary>Una línea de la revisión previa: lo leído, su huella y qué pasará con ella. (nuevo)</summary>
public sealed record LineaDeRevisionDto(int Line, DateOnly MovementDate, string Direction, decimal Amount, string? Reference, string? DepositorTaxId, string? DepositorName,
    string Destination, int? DuplicatesLine);

/// <summary>Una línea ilegible: la línea, el campo y el motivo. (nuevo)</summary>
public sealed record LineaIlegibleDto(int Line, string? Field, string Message);

/// <summary>La revisión previa o el resultado de la carga. (nuevo)</summary>
public sealed record BankStatementLoadDto(
    Guid? StatementPublicId,
    string? Number,
    bool Preview,
    string Source,
    string? FormatCode,
    int Lines,
    int Loaded,
    int Omitted,
    int PossibleDuplicates,
    int Illegible,
    decimal Credits,
    decimal Debits,
    IReadOnlyList<LineaDeRevisionDto> Review,
    IReadOnlyList<LineaIlegibleDto> IllegibleLines);

/// <summary>Un extracto cargado. (nuevo)</summary>
public sealed record BankStatementDto(
    Guid PublicId,
    string Number,
    Guid BankAccountPublicId,
    string BankAccountCode,
    string Source,
    string? FormatCode,
    string? FileName,
    Guid? FileAttachmentPublicId,
    DateOnly From,
    DateOnly To,
    decimal? OpeningBalance,
    decimal? ClosingBalance,
    int Lines,
    int Omitted,
    int Illegible,
    int PossibleDuplicates,
    DateTime LoadedAt,
    string LoadedBy);

/// <summary>Un movimiento del extracto. (nuevo)</summary>
public sealed record BankStatementLineDto(
    Guid PublicId,
    int LineNumber,
    DateOnly MovementDate,
    string Direction,
    decimal Amount,
    string? Reference,
    string? BankSequence,
    string? DepositorTaxId,
    string? DepositorName,
    string? Description,
    decimal? Balance,
    bool HasBankIdentifier,
    bool PossibleDuplicate,
    string? DuplicateDecision,
    string RecaudoStatus,
    decimal RecaudoIdentifiedAmount,
    string ReconciliationStatus);

/// <summary>
/// <c>POST /api/treasury/statements</c> (<c>Treasury.Statements.Load</c>, multipart o captura): carga el extracto de una cuenta propia (FR-065). Con <see cref="Content"/>
/// lo lee con <see cref="FormatPublicId"/>, el formato vigente del banco de la cuenta o <c>EXTRACTO-GENERICO</c>; sin archivo, toma <see cref="Captured"/> (a mano,
/// FR-196). <see cref="Preview"/> sólo revisa: no guarda nada. Las líneas ilegibles se informan con su línea y campo; si las hay, cargar las demás exige
/// <see cref="AcceptIllegible"/> (<c>Treasury.Statement.IllegibleLines</c>). Un archivo ya cargado se reconoce por su huella (<c>.AlreadyLoaded</c>); los movimientos
/// con la huella del banco ya cargada se omiten y se cuentan; los iguales sin identificador del banco se cargan como «posibles duplicados» para que una persona decida
/// (FR-197). El archivo se guarda como adjunto no borrable <c>TreasuryStatement</c> antes de su fila. (nuevo)
/// </summary>
public sealed record LoadBankStatementCommand(
    Guid BankAccountPublicId,
    string? FileName = null,
    byte[]? Content = null,
    Guid? FormatPublicId = null,
    IReadOnlyList<MovimientoCapturado>? Captured = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? OpeningBalance = null,
    decimal? ClosingBalance = null,
    bool Preview = false,
    bool AcceptIllegible = false) : IRequest<Result<BankStatementLoadDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadBankStatementCommandValidator : AbstractValidator<LoadBankStatementCommand>
{
    public LoadBankStatementCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(200);
        RuleFor(x => x.Captured!.Count).LessThanOrEqualTo(5000).When(x => x.Captured is not null);
        RuleForEach(x => x.Captured).ChildRules(c =>
        {
            c.RuleFor(m => m.Amount).NotEqual(0m).WithMessage("Un movimiento capturado tiene valor distinto de cero.");
            c.RuleFor(m => m.Reference).MaximumLength(100);
            c.RuleFor(m => m.DepositorName).MaximumLength(200);
            c.RuleFor(m => m.Description).MaximumLength(300);
        }).When(x => x.Captured is not null);
    }
}

public sealed class LoadBankStatementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    AuditoriaDeTesoreria auditoria,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<LoadBankStatementCommand, Result<BankStatementLoadDto>>
{
    public const string ClaseDeExtracto = "EX";
    public const string TipoDeAdjunto = AdjuntosDeModulo.ExtractoDeTesoreria;

    public async Task<Result<BankStatementLoadDto>> Handle(LoadBankStatementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id))
            return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        var hoy = reloj.HoyLocal;
        if (!cuenta.Activa(hoy)) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.CuentaNoAdmiteExtracto());
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BankId)
            .Select(b => new { b.TransferCode, b.LegacyCode }).FirstAsync(ct);
        var codigoDeBanco = banco.TransferCode ?? banco.LegacyCode ?? cuenta.BankId.ToString(CultureInfo.InvariantCulture);

        // 1. Lectura: archivo con su formato o captura a mano.
        BankStatementSource origen;
        BankFileFormat? formato = null;
        string? huellaDelArchivo = null;
        var leidas = new List<Leida>();
        var ilegibles = new List<LineaIlegibleDto>();
        if (request.Content is { Length: > 0 } contenido)
        {
            formato = await FormatoAsync(cuenta.BankId, request.FormatPublicId, hoy, ct);
            if (formato is null) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.ExtractoSinFormato());
            origen = formato.BankId is null ? BankStatementSource.GenericTemplate : BankStatementSource.BankFormat;
            huellaDelArchivo = HuellaDeMovimientoBancario.DelArchivo(contenido);
            var yaCargado = await db.TreasuryBankStatements.AsNoTracking()
                .Where(s => s.BankAccountId == cuenta.Id && s.FileSha256 == huellaDelArchivo).Select(s => s.Number).FirstOrDefaultAsync(ct);
            if (yaCargado is not null) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.ExtractoYaCargado(yaCargado));
            var leido = FlatFileReader.Leer(formato, contenido);
            ilegibles.AddRange(leido.Errors.Select(e => new LineaIlegibleDto(e.LineNumber, e.Field, e.Message)));
            var conError = leido.Errors.Select(e => e.LineNumber).ToHashSet();
            foreach (var fila in leido.Rows.Where(r => !conError.Contains(r.LineNumber)))
            {
                var valor = Valor(fila);
                var fecha = fila.Fecha(BankFieldSource.MovementDate);
                if (valor is null || valor == 0m || fecha is null)
                {
                    ilegibles.Add(new LineaIlegibleDto(fila.LineNumber, valor is null || valor == 0m ? nameof(BankFieldSource.Amount) : nameof(BankFieldSource.MovementDate),
                        "Sin fecha o sin valor: la línea no se carga."));
                    continue;
                }
                leidas.Add(new Leida(fila.LineNumber, fecha.Value, fila.Fecha(BankFieldSource.ValueDate), valor.Value, Texto(fila.Texto(BankFieldSource.MovementReference), 100),
                    Texto(fila.Texto(BankFieldSource.BankSequence), 60), Texto(fila.Texto(BankFieldSource.DepositorTaxId), 20),
                    Texto(fila.Texto(BankFieldSource.DepositorName), 200), Texto(fila.Texto(BankFieldSource.Description), 300), fila.Monto(BankFieldSource.Balance)));
            }
        }
        else if (request.Captured is { Count: > 0 } capturadas)
        {
            origen = BankStatementSource.Manual;
            var n = 0;
            foreach (var m in capturadas)
                leidas.Add(new Leida(++n, m.MovementDate, null, m.Amount, Texto(m.Reference, 100), Texto(m.BankSequence, 60), Texto(m.DepositorTaxId, 20),
                    Texto(m.DepositorName, 200), Texto(m.Description, 300), m.Balance));
        }
        else
        {
            return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.ExtractoSinOrigen());
        }
        if (leidas.Count == 0 && ilegibles.Count == 0) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.ExtractoVacio());

        // 2. Huella por movimiento contra lo ya cargado en la cuenta (FR-197).
        var movimientos = leidas.Select(l => new MovimientoParaHuella(l.Linea, codigoDeBanco, cuenta.AccountNumber, l.Fecha, l.Valor, l.Referencia, l.Secuencia)).ToList();
        var huellas = movimientos.Select(HuellaDeMovimientoBancario.Calcular).Select(h => h.Huella).Distinct().ToList();
        var existentes = await db.TreasuryBankStatementLines.AsNoTracking()
            .Where(l => l.BankAccountId == cuenta.Id && huellas.Contains(l.Fingerprint))
            .Select(l => new { l.Id, l.Fingerprint }).ToListAsync(ct);
        var clasificadas = HuellaDeMovimientoBancario.Clasificar(movimientos, existentes.Select(e => e.Fingerprint).ToHashSet(StringComparer.Ordinal));
        var revision = clasificadas.Select(c =>
        {
            var l = leidas.First(x => x.Linea == c.Linea);
            return new LineaDeRevisionDto(c.Linea, l.Fecha, l.Valor > 0m ? nameof(StatementLineDirection.Credit) : nameof(StatementLineDirection.Debit), Math.Abs(l.Valor),
                l.Referencia, l.Consignante, l.NombreDelConsignante, c.Destino.ToString(), c.DuplicaLineaDelArchivo);
        }).ToList();
        var aCargar = clasificadas.Where(c => c.Destino != DestinoDeLaLinea.OmitidaPorHuella).ToList();
        var resumen = new BankStatementLoadDto(null, null, request.Preview, origen.ToString(), formato?.Code, leidas.Count + ilegibles.Count, aCargar.Count,
            clasificadas.Count(c => c.Destino == DestinoDeLaLinea.OmitidaPorHuella), clasificadas.Count(c => c.Destino == DestinoDeLaLinea.PosibleDuplicado), ilegibles.Count,
            aCargar.Sum(c => Math.Max(0m, leidas.First(x => x.Linea == c.Linea).Valor)), aCargar.Sum(c => Math.Max(0m, -leidas.First(x => x.Linea == c.Linea).Valor)),
            revision, ilegibles);
        if (request.Preview) return Result.Success(resumen);
        if (ilegibles.Count > 0 && !request.AcceptIllegible)
            return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.LineasIlegibles(ilegibles.Take(100).Cast<object>().ToList()));
        if (aCargar.Count == 0 && ilegibles.Count == 0) return Result.Failure<BankStatementLoadDto>(ErroresDeTesoreria.ExtractoYaCargado("(todas sus líneas)"));

        // 3. Cerrojos (configuración y la cuenta, en exclusivo: dos cargas de la misma cuenta no se cruzan) y la fila del extracto.
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: true, ct);
        var desde = request.From ?? (leidas.Count > 0 ? leidas.Min(l => l.Fecha) : hoy);
        var hasta = request.To ?? (leidas.Count > 0 ? leidas.Max(l => l.Fecha) : hoy);
        var extracto = new BankStatement
        {
            BankAccountId = cuenta.Id, Source = origen, BankFileFormatId = formato?.Id, FileName = request.FileName is null ? null : Texto(request.FileName, 200),
            FileSha256 = huellaDelArchivo, From = desde, To = hasta, OpeningBalance = request.OpeningBalance, LoadedByUserId = usuario, LoadedAt = reloj.UtcNow,
            ClosingBalance = request.ClosingBalance ?? leidas.Where(l => l.Saldo is not null).OrderBy(l => l.Fecha).ThenBy(l => l.Linea).Select(l => l.Saldo).LastOrDefault(),
            Lines = aCargar.Count, Omitted = resumen.Omitted, Illegible = ilegibles.Count, PossibleDuplicates = resumen.PossibleDuplicates,
        };
        if (request.Content is { Length: > 0 } bytes)
        {
            var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, extracto.PublicId, extracto.FileName ?? "extracto.csv",
                formato?.ContentType ?? "text/csv", bytes), ct);
            if (subida.IsFailure) return Result.Failure<BankStatementLoadDto>(subida.Error);
            extracto.FileAttachmentPublicId = subida.Value;
        }
        extracto.Number = await numerador.AsignarAsync(ClaseDeExtracto, ClaseDeExtracto, ct);
        db.TreasuryBankStatements.Add(extracto);
        await db.SaveChangesAsync(ct);

        // 4. Las líneas: primero las nuevas (las que otras del mismo archivo pueden citar como duplicado), después los posibles duplicados con su referencia.
        var porLinea = new Dictionary<int, BankStatementLine>();
        foreach (var c in aCargar.Where(c => c.Destino == DestinoDeLaLinea.Nueva))
            porLinea[c.Linea] = Fila(extracto, cuenta.Id, leidas.First(x => x.Linea == c.Linea), c, null);
        db.TreasuryBankStatementLines.AddRange(porLinea.Values);
        await db.SaveChangesAsync(ct);
        foreach (var c in aCargar.Where(c => c.Destino == DestinoDeLaLinea.PosibleDuplicado))
        {
            var original = c.DuplicaLineaDelArchivo is { } del && porLinea.TryGetValue(del, out var f)
                ? f.Id
                : existentes.Where(e => e.Fingerprint == c.Huella.Huella).Select(e => (long?)e.Id).FirstOrDefault();
            var fila = Fila(extracto, cuenta.Id, leidas.First(x => x.Linea == c.Linea), c, original);
            db.TreasuryBankStatementLines.Add(fila);
            porLinea[c.Linea] = fila;
        }
        if (extracto.ClosingBalance is { } saldo && (cuenta.LastStatementDate is null || hasta >= cuenta.LastStatementDate))
        {
            var seguida = await db.OwnBankAccounts.FirstAsync(c => c.Id == cuenta.Id, ct);
            seguida.LastStatementBalance = saldo;
            seguida.LastStatementDate = hasta;
        }
        await auditoria.AgregarAsync("Treasury.Statement.Loaded", "BankStatement", extracto.PublicId, null,
            new { number = extracto.Number, account = cuenta.Code, source = origen.ToString(), lines = extracto.Lines, omitted = extracto.Omitted, illegible = extracto.Illegible,
                duplicates = extracto.PossibleDuplicates, sha256 = huellaDelArchivo, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(resumen with { StatementPublicId = extracto.PublicId, Number = extracto.Number, Preview = false });
    }

    private sealed record Leida(int Linea, DateOnly Fecha, DateOnly? FechaValor, decimal Valor, string? Referencia, string? Secuencia, string? Consignante,
        string? NombreDelConsignante, string? Descripcion, decimal? Saldo);

    private static BankStatementLine Fila(BankStatement extracto, int cuenta, Leida l, LineaClasificada c, long? duplicaDe) => new()
    {
        BankStatementId = extracto.Id, BankAccountId = cuenta, LineNumber = l.Linea, MovementDate = l.Fecha, ValueDate = l.FechaValor,
        Direction = l.Valor > 0m ? StatementLineDirection.Credit : StatementLineDirection.Debit, Amount = Math.Abs(l.Valor), Reference = l.Referencia,
        BankSequence = l.Secuencia, DepositorTaxId = l.Consignante, DepositorName = l.NombreDelConsignante, Description = l.Descripcion, Balance = l.Saldo,
        Fingerprint = c.Huella.Huella, HasBankIdentifier = c.Huella.ConIdentificadorDelBanco, PossibleDuplicateOfLineId = c.Destino == DestinoDeLaLinea.PosibleDuplicado ? duplicaDe : null,
        DuplicateDecision = c.Destino == DestinoDeLaLinea.PosibleDuplicado ? StatementLineDuplicateDecision.Pending : null,
        RecaudoStatus = l.Valor > 0m ? StatementLineRecaudoStatus.Pending : StatementLineRecaudoStatus.NotApplicable,
    };

    /// <summary>El valor con signo de una fila: el valor con signo, o crédito menos débito, o el valor con el sentido que diga el banco.</summary>
    public static decimal? Valor(FlatFileRow fila)
    {
        var credito = fila.Monto(BankFieldSource.Credit);
        var debito = fila.Monto(BankFieldSource.Debit);
        if (credito is not null || debito is not null) return (credito ?? 0m) - (debito ?? 0m);
        var valor = fila.Monto(BankFieldSource.Amount);
        if (valor is null) return null;
        var sentido = (fila.Texto(BankFieldSource.Direction) ?? string.Empty).Trim().ToUpperInvariant();
        return sentido switch
        {
            "D" or "DB" or "DEBITO" or "DÉBITO" or "DEBIT" or "-" or "CARGO" => -Math.Abs(valor.Value),
            "C" or "CR" or "CREDITO" or "CRÉDITO" or "CREDIT" or "+" or "ABONO" => Math.Abs(valor.Value),
            _ => valor,
        };
    }

    private static string? Texto(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    /// <summary>El formato del extracto: el pedido (del banco de la cuenta o genérico), el vigente del banco, o <c>EXTRACTO-GENERICO</c>.</summary>
    private async Task<BankFileFormat?> FormatoAsync(int bancoId, Guid? pedido, DateOnly hoy, CancellationToken ct)
    {
        var candidatos = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields)
            .Where(f => f.Scope == BankFileScope.BankStatement && f.IsActive && f.ValidFrom <= hoy && (f.ValidTo == null || f.ValidTo >= hoy))
            .ToListAsync(ct);
        if (pedido is { } id) return candidatos.FirstOrDefault(f => f.PublicId == id && (f.BankId == null || f.BankId == bancoId));
        return candidatos.Where(f => f.BankId == bancoId).OrderByDescending(f => f.ValidFrom).FirstOrDefault()
               ?? candidatos.FirstOrDefault(f => f.BankId == null && f.Code == FormatosGenericosDeRecaudoYTesoreria.Extracto)
               ?? candidatos.Where(f => f.BankId == null).OrderByDescending(f => f.ValidFrom).FirstOrDefault();
    }
}

/// <summary><c>GET /api/treasury/statements?account=</c> (<c>Treasury.Statements.View</c>): los extractos de las cuentas al alcance, los más recientes primero. (nuevo)</summary>
public sealed record ListBankStatementsQuery(Guid? BankAccountPublicId = null) : IRequest<Result<IReadOnlyList<BankStatementDto>>>;

public sealed class ListBankStatementsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListBankStatementsQuery, Result<IReadOnlyList<BankStatementDto>>>
{
    public async Task<Result<IReadOnlyList<BankStatementDto>>> Handle(ListBankStatementsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TreasuryBankStatements.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => s.BankAccountId == id);
        }
        var filas = (await consulta.OrderByDescending(s => s.Id).Take(200).ToListAsync(ct)).Where(s => mio.IncluyeCuenta(s.BankAccountId)).ToList();
        var lista = new List<BankStatementDto>(filas.Count);
        foreach (var s in filas) lista.Add(await ExtractosDeTesoreria.DtoAsync(db, s, ct));
        return Result.Success<IReadOnlyList<BankStatementDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/statements/{id}</c> (<c>Treasury.Statements.View</c>). (nuevo)</summary>
public sealed record GetBankStatementQuery(Guid StatementPublicId) : IRequest<Result<BankStatementDto>>;

public sealed class GetBankStatementQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<GetBankStatementQuery, Result<BankStatementDto>>
{
    public async Task<Result<BankStatementDto>> Handle(GetBankStatementQuery request, CancellationToken ct)
    {
        var s = await db.TreasuryBankStatements.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.StatementPublicId, ct);
        if (s is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(s.BankAccountId)) return Result.Failure<BankStatementDto>(ErroresDeTesoreria.NoEncontrado("El extracto"));
        return Result.Success(await ExtractosDeTesoreria.DtoAsync(db, s, ct));
    }
}

/// <summary><c>GET /api/treasury/statements/{id}/lines</c> (<c>Treasury.Statements.View</c>): los movimientos del extracto en su orden. (nuevo)</summary>
public sealed record ListBankStatementLinesQuery(Guid StatementPublicId) : IRequest<Result<IReadOnlyList<BankStatementLineDto>>>;

public sealed class ListBankStatementLinesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListBankStatementLinesQuery, Result<IReadOnlyList<BankStatementLineDto>>>
{
    public async Task<Result<IReadOnlyList<BankStatementLineDto>>> Handle(ListBankStatementLinesQuery request, CancellationToken ct)
    {
        var s = await db.TreasuryBankStatements.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.StatementPublicId, ct);
        if (s is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(s.BankAccountId))
            return Result.Failure<IReadOnlyList<BankStatementLineDto>>(ErroresDeTesoreria.NoEncontrado("El extracto"));
        var lineas = await db.TreasuryBankStatementLines.AsNoTracking().Where(l => l.BankStatementId == s.Id).OrderBy(l => l.LineNumber).ToListAsync(ct);
        return Result.Success<IReadOnlyList<BankStatementLineDto>>(lineas.Select(ExtractosDeTesoreria.Dto).ToList());
    }
}

/// <summary>Lo común de los extractos de Tesorería: sus DTO. (nuevo)</summary>
public static class ExtractosDeTesoreria
{
    public static async Task<BankStatementDto> DtoAsync(IApplicationDbContext db, BankStatement s, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == s.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        var formato = s.BankFileFormatId is { } f
            ? await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == f).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        var quien = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == s.LoadedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? string.Empty;
        return new BankStatementDto(s.PublicId, s.Number, cuenta.PublicId, cuenta.Code, s.Source.ToString(), formato, s.FileName, s.FileAttachmentPublicId, s.From, s.To,
            s.OpeningBalance, s.ClosingBalance, s.Lines, s.Omitted, s.Illegible, s.PossibleDuplicates, s.LoadedAt, quien);
    }

    public static BankStatementLineDto Dto(BankStatementLine l) => new(l.PublicId, l.LineNumber, l.MovementDate, l.Direction.ToString(), l.Amount, l.Reference, l.BankSequence,
        l.DepositorTaxId, l.DepositorName, l.Description, l.Balance, l.HasBankIdentifier, l.PossibleDuplicateOfLineId is not null || l.DuplicateDecision is not null,
        l.DuplicateDecision?.ToString(), l.RecaudoStatus.ToString(), l.RecaudoIdentifiedAmount, l.ReconciliationStatus.ToString());
}
