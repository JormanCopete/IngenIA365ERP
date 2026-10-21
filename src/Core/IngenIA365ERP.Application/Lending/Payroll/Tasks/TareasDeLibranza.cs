using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Payroll.Tasks;

// Feature 013, K2 (US10, bloque B; T0845; DT §2.13; data-model §24.6, §24.8; FR-113, FR-114): las dos tareas diarias de la libranza. Corren por
// IEjecutorEnCooperativa con el actor «Proceso de integración» (las lanza el ProgramadorDeTareas) y salen si Cartera no está activada. (nuevo)

/// <summary>
/// Alerta los archivos de descuento por generar (<c>cartera.archivos-de-descuento</c>; FR-113): por cada convenio activo de una pagaduría externa,
/// los períodos de este mes y del siguiente cuya antelación ya llegó —el día de envío de la versión vigente (el de corte si no tiene) menos
/// <c>SendLeadDays</c>— y que no tienen archivo generado ni enviado levantan <c>Cartera.ArchivoDeDescuentosPendiente</c> (<c>DedupKey</c> convenio y
/// período: repetir suma la ocurrencia). Devuelve cuántos alertó. Sin ruta. (nuevo)
/// </summary>
public sealed record AlertPendingPayrollDeductionFilesCommand : IRequest<Result<int>>;

public sealed class AlertPendingPayrollDeductionFilesCommandValidator : AbstractValidator<AlertPendingPayrollDeductionFilesCommand>;

public sealed class AlertPendingPayrollDeductionFilesCommandHandler(IApplicationDbContext db, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<AlertPendingPayrollDeductionFilesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AlertPendingPayrollDeductionFilesCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var convenios = await db.PayrollAgreements.AsNoTracking()
            .Where(a => !a.IsDeleted && !a.IsOwnPayroll && a.Status == PayrollAgreementStatus.Active).OrderBy(a => a.Code).ToListAsync(ct);
        var ids = convenios.Select(c => c.Id).ToList();
        var versiones = (await db.PayrollAgreementVersions.AsNoTracking().Where(v => ids.Contains(v.PayrollAgreementId) && !v.IsDeleted).ToListAsync(ct))
            .ToLookup(v => v.PayrollAgreementId);
        var archivos = (await db.PayrollDeductionFiles.AsNoTracking()
                .Where(f => ids.Contains(f.PayrollAgreementId) && !f.IsDeleted && (f.Status == PayrollFileStatus.Generated || f.Status == PayrollFileStatus.Sent))
                .Select(f => new { f.PayrollAgreementId, f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber }).ToListAsync(ct))
            .Select(f => (f.PayrollAgreementId, (int)f.PeriodYear, (int)f.PeriodMonth, (int)f.SubPeriodNumber)).ToHashSet();

        var alertados = 0;
        foreach (var convenio in convenios)
        {
            foreach (var mes in new[] { new DateOnly(hoy.Year, hoy.Month, 1), new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1) })
            {
                var version = PayrollAgreementVersion.Vigente(versiones[convenio.Id], mes);
                if (version is null) continue;
                var subperiodos = version.Periodicity == RepaymentPeriodicity.Biweekly ? new[] { 1, 2 } : new[] { 1 };
                foreach (var sub in subperiodos)
                {
                    if (PeriodoDeLibranza.De(version.Periodicity, mes.Year, mes.Month, sub) is not { } periodo) continue;
                    var enviarA = periodo.Dia(ReglasDelConvenio.Leer(version.NoveltyRulesJson).SendDay ?? version.CutoffDay);
                    if (hoy < enviarA.AddDays(-version.SendLeadDays) || archivos.Contains((convenio.Id, periodo.Anio, periodo.Mes, periodo.SubPeriodo))) continue;
                    var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ArchivoDeDescuentosPendiente,
                        $"Archivo de descuentos pendiente: {convenio.Code} {periodo.Texto}",
                        $"El convenio {convenio.Code} ({convenio.Description}) debe enviar a la pagaduría el archivo del período {periodo.Texto} el "
                        + $"{enviarA:yyyy-MM-dd} y todavía no se ha generado.",
                        EntityType: nameof(PayrollAgreement), EntityPublicId: convenio.PublicId,
                        DedupKey: $"{TiposDeAlerta.ArchivoDeDescuentosPendiente}:{convenio.PublicId:D}:{periodo.Texto}",
                        RecipientPermissions: ["Lending.PayrollDeductionFiles.Generate"], ModuloDeOrigen: "Lending"), ct);
                    if (r.IsSuccess && r.Value.Desenlace != DesenlaceDeAlerta.TipoInactivo) alertados++;
                }
            }
        }
        return Result.Success(alertados);
    }
}

/// <summary>
/// Vigila los giros de las pagadurías (<c>cartera.giros-de-pagaduria</c>; FR-114, C71): lo descontado pendiente de giro con la ventana vencida levanta
/// <c>Cartera.GiroDePagaduriaAtrasado</c> por pagaduría y archivo, con el alcance de la oficina y la línea del crédito (<c>DedupKey</c> pagaduría, archivo y
/// oficina); y, superado el umbral sellado, avisa al deudor <b>una sola vez</b> por resultado (informativo por la cola de K1): desde ahí la cuota vuelve a
/// contar para la altura de mora (lo decide la reconstrucción con el umbral sellado). Devuelve cuántas alertas levantó. Sin ruta. (nuevo)
/// </summary>
public sealed record AlertOverduePayrollTransfersCommand : IRequest<Result<int>>;

public sealed class AlertOverduePayrollTransfersCommandValidator : AbstractValidator<AlertOverduePayrollTransfersCommand>;

public sealed class AlertOverduePayrollTransfersCommandHandler(
    IApplicationDbContext db,
    IAlertas alertas,
    ColaDeMensajesAlDeudor cola,
    IDateTimeService reloj,
    ILogger<AlertOverduePayrollTransfersCommandHandler>? logger = null) : IRequestHandler<AlertOverduePayrollTransfersCommand, Result<int>>
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public async Task<Result<int>> Handle(AlertOverduePayrollTransfersCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var atrasados = await (
            from r in PendientesDeGiro.Consulta(db).AsNoTracking()
            join f in db.PayrollDeductionFiles.AsNoTracking() on r.PayrollDeductionFileId equals f.Id
            join a in db.PayrollAgreements.AsNoTracking() on f.PayrollAgreementId equals a.Id
            join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PaymasterPersonId equals p.Id
            join l in db.Loans.AsNoTracking() on r.LoanId equals l.Id
            join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on l.BranchId equals b.Id into oficinas
            from b in oficinas.DefaultIfEmpty()
            join c in db.CreditLines.AsNoTracking().IgnoreQueryFilters() on l.CreditLineId equals c.Id into lineas
            from c in lineas.DefaultIfEmpty()
            where r.TransferDueDate != null && r.TransferDueDate < hoy
            select new
            {
                Resultado = r, Archivo = f.Number, ArchivoPublicId = f.PublicId, Convenio = a.Code, Pagaduria = p.PublicId, Credito = l, Oficina = (Guid?)b.PublicId,
                Linea = (Guid?)c.PublicId,
            }).ToListAsync(ct);

        var alertadas = 0;
        foreach (var g in atrasados.GroupBy(x => (x.Pagaduria, x.ArchivoPublicId, x.Oficina)))
        {
            var primero = g.First();
            var dias = g.Max(x => PendientesDeGiro.DiasDeMora(x.Resultado.TransferDueDate, hoy));
            var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.GiroDePagaduriaAtrasado,
                $"Giro atrasado: convenio {primero.Convenio}, archivo {primero.Archivo}",
                $"La pagaduría del convenio {primero.Convenio} no ha girado {g.Count()} descuento(s) del archivo {primero.Archivo} por "
                + $"{ErroresDeLibranza.Pesos(g.Sum(x => x.Resultado.DeductedAmount))}; lleva {dias} día(s) en mora. Gestione el giro ante la pagaduría.",
                EntityType: nameof(PayrollDeductionFile), EntityPublicId: primero.ArchivoPublicId,
                DedupKey: $"{TiposDeAlerta.GiroDePagaduriaAtrasado}:{g.Key.Pagaduria:D}:{g.Key.ArchivoPublicId:D}:{g.Key.Oficina:D}",
                RecipientPermissions: ["Lending.PayrollTransfers.Manage"], ModuloDeOrigen: "Lending", ScopeBranchPublicId: g.Key.Oficina,
                ScopeCreditLinePublicId: primero.Linea), ct);
            if (r.IsSuccess && r.Value.Desenlace != DesenlaceDeAlerta.TipoInactivo) alertadas++;
        }

        // Superado el umbral: el aviso al deudor, una sola vez por resultado.
        var superados = atrasados.Where(x => PendientesDeGiro.SuperaElUmbral(x.Resultado.TransferDueDate, x.Resultado.PaymasterArrearsThresholdDays, hoy)).ToList();
        var publicos = superados.Select(x => (Guid?)x.Resultado.PublicId).ToList();
        var avisados = (await db.DebtorMessages.AsNoTracking().Where(m => publicos.Contains(m.OriginPublicId)).Select(m => m.OriginPublicId).ToListAsync(ct))
            .ToHashSet();
        foreach (var x in superados.Where(x => !avisados.Contains(x.Resultado.PublicId)))
            await AvisarAsync(x.Resultado, x.Credito.Id, x.Credito.DebtorPersonId, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(alertadas);
    }

    private async Task AvisarAsync(PayrollDeductionResult resultado, int creditoId, int deudorId, CancellationToken ct)
    {
        try
        {
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(deudorId, creditoId, DebtorMessagePurpose.Informative, MessageChannel.Email,
                SeguimientoDeNoAplicados.PlantillaDeNoAplicado, SeguimientoDeNoAplicados.OrigenDelMensaje, $"{resultado.PublicId:N}:giro", ActorKind.Process, null)
            {
                OriginPublicId = resultado.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["MotivoDeNoDescuento"] = "la pagaduría descontó la cuota de su nómina pero no la ha girado a la cooperativa",
                    ["FormaDePagoNueva"] = "caja o los canales de pago de la cooperativa, mientras la pagaduría gira lo descontado",
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDeGiroAtrasado] No se encoló el aviso del resultado {Resultado}: {Codigo} {Mensaje}", resultado.PublicId,
                    r.Error.Code, r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDeGiroAtrasado] El aviso del resultado {Resultado} falló.", resultado.PublicId);
        }
    }
}

/// <summary>La tarea <c>cartera.archivos-de-descuento</c> (DT §2.13): una vez al día; escribe sólo por <see cref="AlertPendingPayrollDeductionFilesCommand"/>. (nuevo)</summary>
public sealed class TareaDeArchivosDeDescuento : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.archivos-de-descuento";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new AlertPendingPayrollDeductionFilesCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La revisión de archivos de descuento falló: {r.Error.Code} {r.Error.Message}");
        await servicios.GetRequiredService<ISender>().Send(new Processes.Tareas.RegisterDailyLendingTaskRunCommand(Nombre), ct);
    }
}

/// <summary>La tarea <c>cartera.giros-de-pagaduria</c> (DT §2.13): una vez al día; escribe sólo por <see cref="AlertOverduePayrollTransfersCommand"/>. (nuevo)</summary>
public sealed class TareaDeGirosDePagaduria : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.giros-de-pagaduria";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new AlertOverduePayrollTransfersCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La revisión de giros de pagaduría falló: {r.Error.Code} {r.Error.Message}");
        await servicios.GetRequiredService<ISender>().Send(new Processes.Tareas.RegisterDailyLendingTaskRunCommand(Nombre), ct);
    }
}
