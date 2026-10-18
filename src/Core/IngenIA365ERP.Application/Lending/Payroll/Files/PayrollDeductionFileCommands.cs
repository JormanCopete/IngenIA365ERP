using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Payroll.Files;

// Feature 013, K2 (US10, bloque B; T0836; api.md §13.2; mensajes.md §11.6; data-model §24.6, §24.7, §32.4; FR-113, T78, C19, C46, I9): el archivo
// de descuentos de una pagaduría por período —generar, anular y el enlace de descarga—. (nuevo)

/// <summary>La preparación común de la vista previa y la generación: convenio operable con archivo, versión vigente y período. (nuevo)</summary>
public sealed class PreparacionDeArchivoDeDescuentos(IApplicationDbContext db, ConveniosDeLibranza convenios, PayrollDeductionLines lineas)
{
    public async Task<Result<ArchivoDeDescuentosPreparado>> PrepararAsync(Guid convenioPublicId, int anio, int mes, int? subPeriodo, string lote,
        CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == convenioPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Falla(ErroresDeLibranza.ConvenioInexistente());
        if (convenio.IsOwnPayroll) return Falla(ErroresDeLibranza.NominaPropiaSinArchivo(convenio.Code));
        if (!convenio.Operable) return Falla(ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, convenio.Status == PayrollAgreementStatus.Closed
            ? "está cerrado" : "está suspendido"));
        if (mes is < 1 or > 12 || anio is < 2000 or > 2999) return Falla(ErroresDeLibranza.PeriodoInvalido(anio, mes, subPeriodo));
        var inicio = new DateOnly(anio, mes, 1);
        var version = await convenios.VersionVigenteAsync(convenio.Id, inicio, ct);
        if (version is null) return Falla(ErroresDeLibranza.SinVersionVigente(convenio.Code, inicio));
        // Feature 014 (T091, parcial): la versión con grupos de archivo no lleva formatos propios; su generación por grupo está pendiente (research
        // §10, E61) y hasta entonces se responde el error en vez de fallar sin formato.
        if (version.UsesFileGroups) return Falla(ErroresDeLibranza.GruposPendientes(convenio.Code));
        var periodo = PeriodoDeLibranza.De(version.Periodicity, anio, mes, subPeriodo);
        if (periodo is null) return Falla(ErroresDeLibranza.PeriodoInvalido(anio, mes, subPeriodo));
        var preparado = await lineas.PrepararAsync(convenio, version, periodo, lote, ct);
        return preparado.Lineas.Count == 0 ? Falla(ErroresDeLibranza.ArchivoVacio(convenio.Code, anio, mes)) : Result.Success(preparado);
    }

    private static Result<ArchivoDeDescuentosPreparado> Falla(Error e) => Result.Failure<ArchivoDeDescuentosPreparado>(e);
}

// ------------------------------------------------------------------------------------------------------------------------- generar --

/// <summary>
/// Genera el archivo de descuentos de un convenio y período (api.md §13.2, <c>POST /api/lending/payroll-deduction-files</c>,
/// <c>Lending.PayrollDeductionFiles.Generate</c>, con clave; FR-113, T78). En una transacción:
/// <list type="number">
/// <item>la preparación (<see cref="PreparacionDeArchivoDeDescuentos"/>): convenio operable y no de la nómina propia
/// (<c>Lending.PayrollFile.OwnPayrollHasNoFile</c>), versión vigente (<c>Lending.PayrollAgreement.NoCurrentVersion</c>) y las líneas con sus
/// novedades contra el último enviado;</item>
/// <item>un archivo ya <c>Sent</c> del período: <c>Lending.PayrollFile.AlreadySent</c> (la corrección va en el período siguiente, I9); uno
/// <c>Generated</c>: se reemplaza (versión <c>n + 1</c>, el anterior <c>Superseded</c> y el mensaje nuevo lo cita en <c>related</c>);</item>
/// <item>el cerrojo de los créditos (T34) y el número al final por <see cref="NumeradorDeCartera"/> (clase <c>PayrollDeductionFile</c>);</item>
/// <item>el archivo por el motor común con su SHA-256, subido como adjunto <c>LendingPayrollDeductionFile</c> <b>antes</b> de guardar la fila;</item>
/// <item><c>DescuentosPorPagaduria</c> a Recaudo por <see cref="EmisionDeCartera"/> en la misma transacción; la fila y sus líneas; y el evento
/// resumen del archivo (las líneas van con <c>[SinDiffDeAuditoria]</c>, C46).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record GeneratePayrollDeductionFileCommand(Guid AgreementPublicId, int Year, int Month, int? SubPeriod = null, string? Reason = null)
    : IRequest<Result<PayrollDeductionFileDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GeneratePayrollDeductionFileCommandValidator : AbstractValidator<GeneratePayrollDeductionFileCommand>
{
    public GeneratePayrollDeductionFileCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty().WithMessage("Elija el convenio.");
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999).WithMessage("Indique el año del período.");
        RuleFor(x => x.Month).InclusiveBetween(1, 12).WithMessage("El mes del período va de 1 a 12.");
        RuleFor(x => x.SubPeriod).InclusiveBetween(1, 5).When(x => x.SubPeriod is not null);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class GeneratePayrollDeductionFileCommandHandler(
    IApplicationDbContext db,
    PreparacionDeArchivoDeDescuentos preparacion,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    EmisionDeCartera emision,
    ISender sender,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<GeneratePayrollDeductionFileCommand, Result<PayrollDeductionFileDto>>
{
    /// <summary>El prefijo del numerador de archivos de descuento.</summary>
    public const string Prefijo = "ADP-";

    public async Task<Result<PayrollDeductionFileDto>> Handle(GeneratePayrollDeductionFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var prep = await preparacion.PrepararAsync(request.AgreementPublicId, request.Year, request.Month, request.SubPeriod,
                PayrollDeductionLines.LoteDeVistaPrevia, ct);
            if (prep.IsFailure) return Falla(prep.Error);
            var p = prep.Value;
            var periodo = p.Periodo;

            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync(p.Lineas.Select(l => l.Credito.Id).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

            var delPeriodo = await db.PayrollDeductionFiles
                .Where(f => f.PayrollAgreementId == p.Convenio.Id && f.PeriodYear == periodo.Anio && f.PeriodMonth == periodo.Mes
                            && f.SubPeriodNumber == periodo.SubPeriodo && !f.IsDeleted)
                .ToListAsync(ct);
            if (delPeriodo.FirstOrDefault(f => f.Status == PayrollFileStatus.Sent) is { } enviado) return Falla(ErroresDeLibranza.ArchivoYaEnviado(enviado.Number));
            var anterior = delPeriodo.FirstOrDefault(f => f.Status == PayrollFileStatus.Generated);
            var version = delPeriodo.Count == 0 ? 1 : delPeriodo.Max(f => f.Version) + 1;

            // El número al final (T34): es el lote del archivo.
            var numero = await numerador.AsignarAsync(LendingNumberKind.PayrollDeductionFile, string.Empty, Prefijo, ct);
            var contexto = PayrollDeductionLines.ConLote(p.Contexto, numero);
            BankFileOutput salida;
            try
            {
                salida = FlatFileWriter.Escribir(p.Formato, contexto, p.Lineas.Select(l => l.Valores).ToList());
            }
            catch (BankFileWriteException ex)
            {
                var credito = ex.LineNumber >= 1 && ex.LineNumber <= p.Lineas.Count ? p.Lineas[ex.LineNumber - 1].Credito.Number : "?";
                return Falla(ErroresDeLibranza.NoCabe(ex.LineNumber, ex.Field, credito));
            }

            // El adjunto de módulo, antes de la fila: si el almacén falla no queda un archivo «generado» que nadie puede bajar.
            var publicId = Guid.NewGuid();
            var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.ArchivoDeDescuentos, publicId, salida.FileName, salida.ContentType,
                salida.Content), ct);
            if (subida.IsFailure) return Falla(subida.Error);

            var ahora = reloj.UtcNow;
            Guid? mensajeAnterior = null;
            if (anterior is not null)
            {
                anterior.MarcarReemplazado(ahora);
                mensajeAnterior = anterior.DeliveryMessagePublicId;
                await db.SaveChangesAsync(ct);
            }

            var publicosDeLinea = p.Lineas.Select(_ => Guid.NewGuid()).ToList();
            var mensaje = await emision.DescuentosPorPagaduriaAsync(new ArchivoDeDescuentosAEmitir(publicId, numero, version, periodo.Anio, periodo.Mes,
                periodo.SubPeriodo, subida.Value, salida.FileName, salida.Sha256, salida.Content.LongLength, p.Formato.Code,
                p.Lineas.Select((l, i) => new LineaDeDescuentoAEmitir(publicosDeLinea[i], new() { LoanPublicId = l.Credito.PublicId, LoanNumber = l.Credito.Number },
                    l.Deudor.PublicId, l.Novedad.EmployeeIdAtPaymaster, l.Novedad.InstallmentNumber, l.Novedad.DueDate, l.Novedad.Novedad, l.Novedad.Total,
                    l.Novedad.Vencido)).ToList(),
                p.Convenio.Code, p.NumeroDeVersion, p.Pagaduria.PublicId, p.EnviarA, anterior, mensajeAnterior, reloj.HoyLocal), ct);

            var t = p.Novedades.Totales;
            var archivo = new PayrollDeductionFile
            {
                PublicId = publicId,
                PayrollAgreementId = p.Convenio.Id,
                PayrollAgreementVersionId = p.Version.Id,
                PeriodYear = (short)periodo.Anio,
                PeriodMonth = (byte)periodo.Mes,
                SubPeriodNumber = (byte)periodo.SubPeriodo,
                Version = version,
                Number = numero,
                OutboundFormatId = p.Formato.Id,
                BaseFileId = p.Base?.Id,
                LineCount = t.Lineas,
                TotalAmount = t.Total,
                NewCount = t.Nuevas,
                ChangeCount = t.Cambios,
                CancellationCount = t.Cancelaciones,
                UnchangedCount = t.SinCambio,
                FileName = salida.FileName,
                FileSha256 = salida.Sha256,
                FileAttachmentPublicId = subida.Value,
                GeneratedByUserId = usuario,
                GeneratedAt = ahora,
                DeliveryMessagePublicId = mensaje.PublicId,
            };
            db.PayrollDeductionFiles.Add(archivo);
            await db.SaveChangesAsync(ct);

            for (var i = 0; i < p.Lineas.Count; i++)
            {
                var n = p.Lineas[i].Novedad;
                db.PayrollDeductionFileLines.Add(new PayrollDeductionFileLine
                {
                    PublicId = publicosDeLinea[i],
                    PayrollDeductionFileId = archivo.Id,
                    LineNumber = n.LineNumber,
                    LoanId = n.LoanId,
                    LoanPayrollLinkId = n.LoanPayrollLinkId,
                    PersonId = n.PersonId,
                    EmployeeIdAtPaymaster = n.EmployeeIdAtPaymaster,
                    RepaymentInstallmentId = n.RepaymentInstallmentId,
                    InstallmentNumber = n.InstallmentNumber,
                    DueDate = n.DueDate,
                    CurrentAmount = n.Corriente,
                    PastDueAmount = n.Vencido,
                    TotalAmount = n.Total,
                    NoveltyKind = n.Novedad,
                    PreviousAmount = n.Anterior,
                    LibranzaDate = n.LibranzaDate,
                });
            }
            anterior?.AnotarReemplazo(archivo.Id);
            await db.SaveChangesAsync(ct);

            await auditoria.EmitirArchivoDeDescuentosGeneradoAsync(archivo.PublicId, new
            {
                number = numero, agreement = p.Convenio.Code, period = periodo.Texto, version, lines = t.Lineas, total = t.Total,
                novelties = new { created = t.Nuevas, changed = t.Cambios, cancelled = t.Cancelaciones, unchanged = t.SinCambio },
                sha256 = salida.Sha256, supersedes = anterior?.Number, reason = request.Reason,
            }, ct);
            return Result.Success((await ArchivosDeDescuentos.DtosAsync(db, [archivo], ct))[0]);
        }, ct);
    }

    private static Result<PayrollDeductionFileDto> Falla(Error e) => Result.Failure<PayrollDeductionFileDto>(e);
}

// --------------------------------------------------------------------------------------------------------------------------- anular --

/// <summary>
/// Anula un archivo de descuentos (api.md §13.2, <c>POST /{id}/void</c>, <c>Lending.PayrollDeductionFiles.Generate</c>, con clave y motivo):
/// sólo antes del envío (<c>Lending.PayrollFile.AlreadySent</c>); uno reemplazado o ya anulado es <c>Lending.PayrollFile.Superseded</c>. El
/// archivo y sus líneas quedan. (nuevo)
/// </summary>
public sealed record VoidPayrollDeductionFileCommand(Guid FilePublicId, string Reason) : IRequest<Result<PayrollDeductionFileDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidPayrollDeductionFileCommandValidator : AbstractValidator<VoidPayrollDeductionFileCommand>
{
    public VoidPayrollDeductionFileCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se anula el archivo.").MaximumLength(500);
    }
}

public sealed class VoidPayrollDeductionFileCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<VoidPayrollDeductionFileCommand, Result<PayrollDeductionFileDto>>
{
    public async Task<Result<PayrollDeductionFileDto>> Handle(VoidPayrollDeductionFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PayrollDeductionFileDto>(ErroresDeGobierno.SinUsuario());
        var leido = await ArchivosDeDescuentos.LeerAsync(db, alcance, auditoria, request.FilePublicId, ct);
        if (leido.IsFailure) return Result.Failure<PayrollDeductionFileDto>(leido.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var archivo = await db.PayrollDeductionFiles.FirstAsync(f => f.Id == leido.Value.Id, ct);
            if (archivo.Status == PayrollFileStatus.Sent) return Result.Failure<PayrollDeductionFileDto>(ErroresDeLibranza.ArchivoYaEnviado(archivo.Number));
            if (archivo.Status != PayrollFileStatus.Generated)
                return Result.Failure<PayrollDeductionFileDto>(ErroresDeLibranza.ArchivoReemplazado(archivo.Number, archivo.Status.ToString()));
            archivo.Anular(usuario, reloj.UtcNow, request.Reason);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ArchivosDeDescuentos.DtosAsync(db, [archivo], ct))[0]);
        }, ct);
    }
}

// -------------------------------------------------------------------------------------------------------------------------- descargar --

/// <summary>
/// El enlace firmado de 60 s al archivo de descuentos (api.md §13.2, <c>POST /{id}/download-link</c>, <c>Lending.PayrollDeductionFiles.Read</c>, sin
/// clave: repetirlo emite otro): de un archivo con créditos del alcance, auditado (<c>Lending.PayrollDeductionFile.Downloaded</c>, con su SHA-256).
/// (nuevo)
/// </summary>
public sealed record IssuePayrollDeductionFileDownloadLinkCommand(Guid FilePublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class IssuePayrollDeductionFileDownloadLinkCommandValidator : AbstractValidator<IssuePayrollDeductionFileDownloadLinkCommand>
{
    public IssuePayrollDeductionFileDownloadLinkCommandValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class IssuePayrollDeductionFileDownloadLinkCommandHandler(
    IApplicationDbContext db,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<IssuePayrollDeductionFileDownloadLinkCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(IssuePayrollDeductionFileDownloadLinkCommand request, CancellationToken ct)
    {
        var leido = await ArchivosDeDescuentos.LeerAsync(db, alcance, auditoria, request.FilePublicId, ct);
        if (leido.IsFailure) return Result.Failure<EnlaceDeDescargaDto>(leido.Error);
        var archivo = leido.Value;
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == archivo.FileAttachmentPublicId, ct);
        if (adjunto is null || adjunto.Status != EstadoDeAdjunto.Available)
            return Result.Failure<EnlaceDeDescargaDto>(new ErrorConDatos(ErroresDeLibranza.CodigoArchivoInexistente,
                "El archivo no está disponible para descargar.", new { filePublicId = archivo.PublicId }));

        await auditoria.EmitirArchivoDeDescuentosDescargadoAsync(archivo.PublicId, archivo.Number, archivo.FileSha256, ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct));
    }
}
