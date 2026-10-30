using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Huellas;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Reconciliation.Acquirers;

// Feature 015, T2 (fase 44, T0464; FR-133, FR-218; US22; contracts/api.md §13): la conciliación de una red de tarjetas. Se carga el archivo de liquidación de la red (un
// formato del ámbito AcquirerSettlement = 11), cada lote se cruza con lo cobrado en Recaudo y en el punto de venta por datáfono y número de lote (o por datáfono y día), y
// la diferencia —lo cobrado que la red no liquidó más lo que el banco abonó de más o de menos— se compara con Adquirentes.ToleranciaDeConciliacion: dentro, la liquidación
// queda explicada y nace su hecho; fuera, la aprueba otra persona. El hecho desagrega comisión, IVA y retenciones con la red como tercero. (nuevo)

/// <summary>Una línea de la liquidación con lo cobrado. (nuevo)</summary>
public sealed record AcquirerSettlementLineDto(int LineNumber, string TerminalCode, string? BatchNumber, DateOnly BatchDate, decimal Gross, decimal Commission, decimal Vat,
    decimal Withholding, decimal Net, AcquirerBatchSource? Source, decimal? Collected, decimal Difference);

/// <summary>Una liquidación de la red. (nuevo)</summary>
public sealed record AcquirerSettlementDto(Guid PublicId, string Number, Guid AcquirerPublicId, string AcquirerCode, Guid BankAccountPublicId, string FileName,
    DateOnly SettlementDate, Guid? StatementLinePublicId, decimal? StatementAmount, decimal Gross, decimal Commission, decimal Vat, decimal WithholdingTax,
    decimal WithholdingVat, decimal WithholdingIca, decimal Net, decimal Collected, decimal Difference, decimal Tolerance, AcquirerSettlementState Status,
    string? DecisionReason, Guid? FactMessagePublicId, IReadOnlyList<AcquirerSettlementLineDto> Lines);

/// <summary>Una línea del archivo que no se pudo leer. (nuevo)</summary>
public sealed record LineaIlegibleDeAdquirenteDto(int LineNumber, string? Field, string Message);

/// <summary>
/// <c>POST /api/treasury/acquirer-settlements</c> (<c>Treasury.AcquirerSettlements.Load</c>, con clave; FR-133): carga el archivo de liquidación de una red abonada en una cuenta
/// propia y la concilia. Con una línea ilegible o que no suma (bruto − comisión − IVA − retenciones ≠ neto), no se guarda nada. (nuevo)
/// </summary>
public sealed record LoadAcquirerSettlementCommand(Guid AcquirerPublicId, Guid BankAccountPublicId, Guid FormatPublicId, string FileName, byte[] Content,
    DateOnly? SettlementDate = null, Guid? StatementLinePublicId = null) : IRequest<Result<AcquirerSettlementDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadAcquirerSettlementCommandValidator : AbstractValidator<LoadAcquirerSettlementCommand>
{
    public LoadAcquirerSettlementCommandValidator()
    {
        RuleFor(x => x.AcquirerPublicId).NotEmpty();
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.FormatPublicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Content).NotEmpty();
    }
}

public sealed class LoadAcquirerSettlementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    NumeradorDeTesoreria numerador,
    ParametrosDeTesoreria parametros,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj,
    ILotesDeDatafonoDeRecaudo? deRecaudo = null,
    ILotesDeDatafonoDelPuntoDeVenta? delPuntoDeVenta = null) : IRequestHandler<LoadAcquirerSettlementCommand, Result<AcquirerSettlementDto>>
{
    public const string CodigoDeLiquidacion = "AQ";

    /// <summary>Cuántos días antes del lote más antiguo se buscan lotes cobrados (la red liquida días después del cierre del datáfono).</summary>
    private const int DiasDeBusqueda = 15;

    public async Task<Result<AcquirerSettlementDto>> Handle(LoadAcquirerSettlementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AcquirerSettlementDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var red = await db.CardAcquirers.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.AcquirerPublicId, ct);
        if (red is null) return Result.Failure<AcquirerSettlementDto>(ErroresDeTesoreria.NoEncontrado("La red de tarjetas"));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id))
            return Result.Failure<AcquirerSettlementDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields).FirstOrDefaultAsync(f => f.PublicId == request.FormatPublicId, ct);
        if (formato is null || formato.Scope != BankFileScope.AcquirerSettlement) return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.FormatoInvalido());

        var huella = HuellaDeMovimientoBancario.DelArchivo(request.Content);
        if (await db.TreasuryAcquirerSettlements.AsNoTracking().Where(s => s.CardAcquirerId == red.Id && s.FileSha256 == huella).Select(s => s.Number).FirstOrDefaultAsync(ct)
            is { } yaCargada)
            return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.ArchivoYaCargado(yaCargada));

        // 1. El archivo: cada lote con su bruto, comisión, IVA, retenciones y neto; todo o nada.
        var leido = FlatFileReader.Leer(formato, request.Content);
        var ilegibles = leido.Errors.Select(e => new LineaIlegibleDeAdquirenteDto(e.LineNumber, e.Field, e.Message)).ToList();
        var lineas = new List<AcquirerSettlementLine>();
        decimal retefuente = 0m, reteiva = 0m, reteica = 0m;
        foreach (var fila in leido.Rows)
        {
            var bruto = fila.Monto(BankFieldSource.GrossAmount);
            var terminal = Texto(fila[BankFieldSource.TerminalCode]);
            var fecha = fila.Fecha(BankFieldSource.MovementDate) ?? fila.Fecha(BankFieldSource.PaymentDate) ?? request.SettlementDate;
            if (bruto is not > 0m || terminal is null || fecha is null)
            {
                ilegibles.Add(new LineaIlegibleDeAdquirenteDto(fila.LineNumber, bruto is not > 0m ? nameof(BankFieldSource.GrossAmount)
                    : terminal is null ? nameof(BankFieldSource.TerminalCode) : nameof(BankFieldSource.MovementDate), "Sin datáfono, sin fecha o sin bruto positivo."));
                continue;
            }
            var comision = fila.Monto(BankFieldSource.CommissionAmount) ?? 0m;
            var iva = fila.Monto(BankFieldSource.CommissionVatAmount) ?? 0m;
            var (rf, ri, rc) = (fila.Monto(BankFieldSource.WithholdingTaxAmount) ?? 0m, fila.Monto(BankFieldSource.WithholdingVatAmount) ?? 0m,
                fila.Monto(BankFieldSource.WithholdingIcaAmount) ?? 0m);
            var calculado = bruto.Value - comision - iva - rf - ri - rc;
            var abonadoDelLote = fila.Monto(BankFieldSource.SettledAmount) ?? calculado;
            if (abonadoDelLote != calculado)
            {
                ilegibles.Add(new LineaIlegibleDeAdquirenteDto(fila.LineNumber, nameof(BankFieldSource.SettledAmount), string.Create(CultureInfo.GetCultureInfo("es-CO"),
                    $"Lo abonado ({abonadoDelLote:N2}) no es el bruto menos comisión, IVA y retenciones ({calculado:N2}).")));
                continue;
            }
            (retefuente, reteiva, reteica) = (retefuente + rf, reteiva + ri, reteica + rc);
            lineas.Add(new AcquirerSettlementLine
            {
                LineNumber = fila.LineNumber, TerminalCode = Recortar(terminal, 30), BatchNumber = Texto(fila[BankFieldSource.TerminalBatchNumber]) is { } l ? Recortar(l, 30) : null,
                BatchDate = fecha.Value, GrossAmount = bruto.Value, CommissionAmount = comision, CommissionVatAmount = iva, WithholdingAmount = rf + ri + rc, NetAmount = abonadoDelLote,
            });
        }
        if (ilegibles.Count > 0) return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.Ilegible(ilegibles));
        if (lineas.Count == 0) return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.Vacio());

        // 2. El abono del extracto, si se indicó: de la cuenta, un abono.
        decimal? abono = null;
        if (request.StatementLinePublicId is { } lp)
        {
            var linea = await db.TreasuryBankStatementLines.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == lp, ct);
            if (linea is null || linea.BankAccountId != cuenta.Id || linea.Direction != StatementLineDirection.Credit)
                return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.LineaDelExtractoInvalida());
            abono = linea.Amount;
        }

        // 3. Lo cobrado: los lotes de la red en Recaudo y en el punto de venta.
        var desde = lineas.Min(l => l.BatchDate).AddDays(-DiasDeBusqueda);
        var hasta = lineas.Max(l => l.BatchDate);
        var cobrados = new List<(LoteDeDatafonoDto Lote, AcquirerBatchSource Origen)>();
        if (deRecaudo is not null) cobrados.AddRange((await deRecaudo.PorFechaAsync(desde, hasta, ct)).Select(l => (l, AcquirerBatchSource.Receipts)));
        if (delPuntoDeVenta is not null) cobrados.AddRange((await delPuntoDeVenta.PorFechaAsync(desde, hasta, ct)).Select(l => (l, AcquirerBatchSource.PointOfSale)));
        cobrados = cobrados.Where(c => c.Lote.AcquirerPublicId == red.PublicId).ToList();
        var usados = new HashSet<(Guid, string, AcquirerBatchSource)>();
        foreach (var l in lineas)
        {
            var candidato = cobrados.FirstOrDefault(c => !usados.Contains((c.Lote.CardTerminalPublicId, c.Lote.BatchNumber, c.Origen))
                                                         && string.Equals(c.Lote.TerminalCode, l.TerminalCode, StringComparison.OrdinalIgnoreCase)
                                                         && (l.BatchNumber is not null
                                                             ? string.Equals(c.Lote.BatchNumber, l.BatchNumber, StringComparison.OrdinalIgnoreCase)
                                                             : c.Lote.Date == l.BatchDate));
            if (candidato.Lote is not null)
            {
                usados.Add((candidato.Lote.CardTerminalPublicId, candidato.Lote.BatchNumber, candidato.Origen));
                (l.Source, l.CollectedAmount, l.Difference) = (candidato.Origen, candidato.Lote.Amount, candidato.Lote.Amount - l.GrossAmount);
            }
            else
                (l.Source, l.CollectedAmount, l.Difference) = (null, null, -l.GrossAmount);
        }

        // 4. La diferencia y la tolerancia.
        var hoy = reloj.HoyLocal;
        var tolerancia = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.AdquirentesToleranciaDeConciliacion, hoy, ct) ?? 0m;
        var neto = lineas.Sum(l => l.NetAmount);
        var cobrado = lineas.Sum(l => l.CollectedAmount ?? 0m);
        var diferencia = lineas.Sum(l => l.Difference) + (abono is { } a ? a - neto : 0m);
        var liquidacion = new AcquirerSettlement
        {
            Number = await numerador.AsignarAsync(CodigoDeLiquidacion, CodigoDeLiquidacion, ct), CardAcquirerId = red.Id, BankAccountId = cuenta.Id, BankFileFormatId = formato.Id,
            FileName = Recortar(request.FileName, 255), FileSha256 = huella, SettlementDate = request.SettlementDate ?? hasta, StatementLinePublicId = request.StatementLinePublicId,
            StatementAmount = abono, GrossAmount = lineas.Sum(l => l.GrossAmount), CommissionAmount = lineas.Sum(l => l.CommissionAmount),
            CommissionVatAmount = lineas.Sum(l => l.CommissionVatAmount), WithholdingTaxAmount = retefuente, WithholdingVatAmount = reteiva, WithholdingIcaAmount = reteica,
            NetAmount = neto, CollectedAmount = cobrado, Difference = diferencia, Tolerance = tolerancia,
            Status = Math.Abs(diferencia) <= tolerancia ? AcquirerSettlementState.Explained : AcquirerSettlementState.PendingApproval, RegisteredByUserId = usuario,
            RegisteredAt = reloj.UtcNow, Lines = lineas,
        };
        if (liquidacion.Status == AcquirerSettlementState.Explained)
        {
            var previa = await emision.ValidacionPreviaAsync([IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.ConciliacionDeAdquirenteRegistradaV1.Type], cuenta,
                liquidacion.SettlementDate, ct);
            if (previa.IsFailure) return Result.Failure<AcquirerSettlementDto>(previa.Error);
        }
        db.TreasuryAcquirerSettlements.Add(liquidacion);
        if (liquidacion.Status == AcquirerSettlementState.Explained)
            liquidacion.FactMessagePublicId = (await emision.ConciliacionDeAdquirenteAsync(cuenta, liquidacion, red.PublicId, red.Code, red.PersonId, false, ct)).PublicId;
        await auditoria.AgregarAsync("Treasury.AcquirerSettlement.Loaded", "AcquirerSettlement", liquidacion.PublicId, null,
            new { number = liquidacion.Number, acquirer = red.Code, gross = liquidacion.GrossAmount, net = neto, collected = cobrado, difference = diferencia, tolerance = tolerancia,
                status = liquidacion.Status.ToString(), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(Adquirentes.Dto(liquidacion, red.PublicId, red.Code, cuenta.PublicId));
    }

    private static string? Texto(object? valor) => valor switch
    {
        null => null,
        string s when string.IsNullOrWhiteSpace(s) => null,
        string s => s.Trim(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString(),
    };

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>
/// <c>POST /api/treasury/acquirer-settlements/{id}/approve</c> y <c>/reject</c> (<c>Treasury.AcquirerSettlements.Approve</c>, con clave; FR-133): la diferencia sobre la
/// tolerancia la decide otra persona; al aprobarla nace el hecho. Rechazarla exige motivo y deja la liquidación sin hecho (se carga de nuevo corregida). (nuevo)
/// </summary>
public sealed record DecideAcquirerSettlementCommand(Guid SettlementPublicId, bool Approve, string? Reason) : IRequest<Result<AcquirerSettlementDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideAcquirerSettlementCommandValidator : AbstractValidator<DecideAcquirerSettlementCommand>
{
    public DecideAcquirerSettlementCommandValidator()
    {
        RuleFor(x => x.SettlementPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Diga el motivo de la decisión.");
    }
}

public sealed class DecideAcquirerSettlementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<DecideAcquirerSettlementCommand, Result<AcquirerSettlementDto>>
{
    public async Task<Result<AcquirerSettlementDto>> Handle(DecideAcquirerSettlementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AcquirerSettlementDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var liquidacion = await db.TreasuryAcquirerSettlements.Include(s => s.Lines).FirstOrDefaultAsync(s => s.PublicId == request.SettlementPublicId, ct);
        if (liquidacion is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(liquidacion.BankAccountId))
            return Result.Failure<AcquirerSettlementDto>(ErroresDeTesoreria.NoEncontrado("La liquidación de la red"));
        if (liquidacion.Status != AcquirerSettlementState.PendingApproval) return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.NoPorAprobar());
        if (liquidacion.RegisteredByUserId == usuario) return Result.Failure<AcquirerSettlementDto>(ErroresDeAdquirentes.Autoaprobada());
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == liquidacion.BankAccountId, ct);
        var red = await db.CardAcquirers.AsNoTracking().IgnoreQueryFilters().FirstAsync(a => a.Id == liquidacion.CardAcquirerId, ct);
        if (request.Approve)
        {
            var previa = await emision.ValidacionPreviaAsync([IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.ConciliacionDeAdquirenteRegistradaV1.Type], cuenta,
                liquidacion.SettlementDate, ct);
            if (previa.IsFailure) return Result.Failure<AcquirerSettlementDto>(previa.Error);
        }
        (liquidacion.Status, liquidacion.DecidedByUserId, liquidacion.DecidedAt, liquidacion.DecisionReason) =
            (request.Approve ? AcquirerSettlementState.Approved : AcquirerSettlementState.Rejected, usuario, reloj.UtcNow, request.Reason!.Trim());
        if (request.Approve)
            liquidacion.FactMessagePublicId = (await emision.ConciliacionDeAdquirenteAsync(cuenta, liquidacion, red.PublicId, red.Code, red.PersonId, true, ct)).PublicId;
        await auditoria.AgregarAsync(request.Approve ? "Treasury.AcquirerSettlement.Approved" : "Treasury.AcquirerSettlement.Rejected", "AcquirerSettlement",
            liquidacion.PublicId, null, new { number = liquidacion.Number, difference = liquidacion.Difference, reason = liquidacion.DecisionReason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(Adquirentes.Dto(liquidacion, red.PublicId, red.Code, cuenta.PublicId));
    }
}

/// <summary><c>GET /api/treasury/acquirer-settlements?account=&amp;state=</c> (<c>Treasury.Reconciliation.View</c> o <c>Treasury.AcquirerSettlements.*</c>). (nuevo)</summary>
public sealed record ListAcquirerSettlementsQuery(Guid? BankAccountPublicId = null, AcquirerSettlementState? State = null)
    : IRequest<Result<IReadOnlyList<AcquirerSettlementDto>>>;

public sealed class ListAcquirerSettlementsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListAcquirerSettlementsQuery, Result<IReadOnlyList<AcquirerSettlementDto>>>
{
    public async Task<Result<IReadOnlyList<AcquirerSettlementDto>>> Handle(ListAcquirerSettlementsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TreasuryAcquirerSettlements.AsNoTracking().Include(s => s.Lines).AsQueryable();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(s => s.BankAccountId == id);
        }
        if (request.State is { } st) consulta = consulta.Where(s => s.Status == st);
        var filas = (await consulta.OrderByDescending(s => s.Id).Take(200).ToListAsync(ct)).Where(s => mio.IncluyeCuenta(s.BankAccountId)).ToList();
        var redes = await db.CardAcquirers.AsNoTracking().IgnoreQueryFilters().ToDictionaryAsync(a => a.Id, a => (a.PublicId, a.Code), ct);
        var cuentas = await db.OwnBankAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.PublicId, ct);
        return Result.Success<IReadOnlyList<AcquirerSettlementDto>>(filas.Select(s =>
        {
            var (red, codigo) = redes.GetValueOrDefault(s.CardAcquirerId, (Guid.Empty, string.Empty));
            return Adquirentes.Dto(s, red, codigo, cuentas.GetValueOrDefault(s.BankAccountId));
        }).ToList());
    }
}

/// <summary>Lo común de los adquirentes. (nuevo)</summary>
public static class Adquirentes
{
    public static AcquirerSettlementDto Dto(AcquirerSettlement s, Guid red, string codigo, Guid cuenta) => new(s.PublicId, s.Number, red, codigo, cuenta, s.FileName,
        s.SettlementDate, s.StatementLinePublicId, s.StatementAmount, s.GrossAmount, s.CommissionAmount, s.CommissionVatAmount, s.WithholdingTaxAmount,
        s.WithholdingVatAmount, s.WithholdingIcaAmount, s.NetAmount, s.CollectedAmount, s.Difference, s.Tolerance, s.Status, s.DecisionReason, s.FactMessagePublicId,
        s.Lines.OrderBy(l => l.LineNumber).Select(l => new AcquirerSettlementLineDto(l.LineNumber, l.TerminalCode, l.BatchNumber, l.BatchDate, l.GrossAmount,
            l.CommissionAmount, l.CommissionVatAmount, l.WithholdingAmount, l.NetAmount, l.Source, l.CollectedAmount, l.Difference)).ToList());
}

/// <summary>Los errores de la conciliación de adquirentes (feature 015, T2, fase 44; nuevos, DT §2.7). (nuevo)</summary>
public static class ErroresDeAdquirentes
{
    public static Error FormatoInvalido() => new("Treasury.AcquirerSettlement.InvalidFormat",
        "El formato no existe o no es de liquidación de adquirentes (ámbito «AcquirerSettlement»).");

    public static Error ArchivoYaCargado(string numero) => new ErrorConDatos("Treasury.AcquirerSettlement.AlreadyLoaded",
        $"Ese archivo de la red ya se cargó en la liquidación {numero}.", new { number = numero });

    public static Error Ilegible(IReadOnlyList<LineaIlegibleDeAdquirenteDto> lineas) => new ErrorConDatos("Treasury.AcquirerSettlement.Illegible",
        "Hay líneas del archivo que no se pudieron leer o no suman: corrija el archivo y cárguelo de nuevo (no se guardó nada).", new { lines = lineas });

    public static Error Vacio() => new("Treasury.AcquirerSettlement.Empty", "El archivo no trae lotes.");

    public static Error LineaDelExtractoInvalida() => new("Treasury.AcquirerSettlement.StatementLineMismatch",
        "La línea del extracto no es un abono de esa cuenta.");

    public static Error NoPorAprobar() => new("Treasury.AcquirerSettlement.NotPendingApproval", "La liquidación no está por aprobar.");

    public static Error Autoaprobada() => new("Treasury.AcquirerSettlement.SelfApproval", "Quien cargó la liquidación no decide su diferencia: la decide otra persona.");
}
