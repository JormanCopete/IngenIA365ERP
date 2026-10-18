using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Results;

// Feature 013, K2 (US10, bloque B; T0838; mensajes.md §12.5, §12.6, §15; data-model §24.6–§24.10; FR-113–FR-115): lo que Recaudo devuelve del
// archivo de descuentos. Tres comandos sin ruta (LosComandosDeConsumoNoTienenRuta): el que reparte los mensajes de Recaudo por su tipo y los dos
// consumidores. Responden siempre un ResultadoDeConsumo; la falla del Result se reserva para lo que no se pudo leer. (nuevo)

/// <summary>
/// Reparte una unidad de Recaudo (<c>REC</c>) a su consumidor por el tipo del mensaje (mensajes.md §15.1): <c>EnvioDeDescuentosConfirmado</c> a
/// <see cref="ConsumePayrollFileSentCommand"/> y <c>ResultadoDeDescuentos</c> a <see cref="ConsumePayrollDeductionResultCommand"/>. Lo manda
/// <see cref="DestinoCartera"/>; sin ruta. (nuevo)
/// </summary>
public sealed record ConsumeReceiptsMessageCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>;

public sealed class ConsumeReceiptsMessageCommandValidator : AbstractValidator<ConsumeReceiptsMessageCommand>
{
    public ConsumeReceiptsMessageCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeReceiptsMessageCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<ConsumeReceiptsMessageCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeReceiptsMessageCommand request, CancellationToken ct)
    {
        var ids = request.MessagePublicIds.ToList();
        var tipo = await db.IntegrationMessages.AsNoTracking().Where(m => ids.Contains(m.PublicId)).Select(m => m.Type).FirstOrDefaultAsync(ct);
        return tipo switch
        {
            EnvioDeDescuentosConfirmadoV1.Type => await sender.Send(new ConsumePayrollFileSentCommand(request.MessagePublicIds), ct),
            ResultadoDeDescuentosV1.Type => await sender.Send(new ConsumePayrollDeductionResultCommand(request.MessagePublicIds), ct),
            null => Result.Failure<ResultadoDeConsumo>(new Error("Integration.Message.NotFound", "El mensaje no existe en la bandeja.")),
            _ => Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Cartera no acepta {tipo} de Recaudo.")),
        };
    }
}

/// <summary>El recibo y su evaluación, comunes a los dos consumidores de Recaudo (como <see cref="ConsumeTreasuryResultCommandHandler"/>). (nuevo)</summary>
internal static class RecibosDeLibranza
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static LendingInboundMessage Nuevo(MensajeEntrante m, DateTime ahora) => new()
    {
        MessagePublicId = m.MessageId,
        MessageType = m.Type,
        MessageVersion = (short)m.Version,
        OriginModule = m.OriginModule,
        OriginPublicId = m.Envelope.Origin.PublicId,
        OriginNumber = m.Envelope.Origin.Number,
        OriginEventKey = m.Envelope.OriginEventKey,
        RelatedPublicId = m.Envelope.Related?.PublicId,
        ChainRootPublicId = m.Envelope.ChainRootPublicId == Guid.Empty ? m.Envelope.Origin.PublicId : m.Envelope.ChainRootPublicId,
        OperationDate = m.Envelope.Origin.OperationDate,
        Status = InboundMessageStatus.Rejected,
        FirstReceivedAt = ahora,
        LastProcessedAt = ahora,
    };

    /// <summary>Deja el recibo con su resultado y su evaluación, y guarda (la transacción confirma: el resultado del comando es un éxito).</summary>
    public static async Task<ResultadoDeConsumo> CerrarAsync(IApplicationDbContext db, LendingInboundMessage recibo, MensajeEntrante m, string chequeo,
        Error? rechazo, DateTime ahora, CancellationToken ct)
    {
        if (recibo.Id == 0) db.LendingInboundMessages.Add(recibo);
        recibo.Status = rechazo is null ? InboundMessageStatus.Processed : InboundMessageStatus.Rejected;
        recibo.LastProcessedAt = ahora;
        recibo.EvaluationCount += 1;
        await db.SaveChangesAsync(ct);
        var datos = rechazo is ErrorConDatos d ? JsonSerializer.Serialize(d.Data, Json) : null;
        var evaluacion = new LendingInboundMessageEvaluation
        {
            InboundMessageId = recibo.Id,
            EvaluationNumber = recibo.EvaluationCount,
            AsOfDate = m.Envelope.Origin.OperationDate,
            EvaluatedAt = ahora,
            Outcome = rechazo is null ? InboundEvaluationOutcome.Accepted : InboundEvaluationOutcome.NotConvertible,
            ChecksJson = datos is null ? "[]" : $"[{{\"check\":\"{chequeo}\",\"evidence\":{datos}}}]",
            FailureCode = rechazo?.Code,
            FailureReason = rechazo?.Message,
            Retryable = rechazo is not null,
            PrevalidationOutcome = null,
        };
        db.LendingInboundMessageEvaluations.Add(evaluacion);
        await db.SaveChangesAsync(ct);
        recibo.LastEvaluationId = evaluacion.Id;
        await db.SaveChangesAsync(ct);
        return rechazo is null
            ? new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational)
            : new ResultadoDeConsumo.Rejected(rechazo.Code, rechazo.Message, datos);
    }

    /// <summary>¿El recibo ya está procesado? Entonces se responde <c>AlreadyProcessed</c> sin efecto (SC-010).</summary>
    public static async Task<LendingInboundMessage?> ReciboAsync(IApplicationDbContext db, Guid mensaje, CancellationToken ct) =>
        await db.LendingInboundMessages.FirstOrDefaultAsync(r => r.MessagePublicId == mensaje, ct);
}

// ----------------------------------------------------------------------------------------------------------------- envío confirmado --

/// <summary>
/// Consume un <c>EnvioDeDescuentosConfirmado</c> v1 (K2, T0838; mensajes.md §12.5): la versión <c>Generated</c> pasa a <c>Sent</c> con la fecha, el
/// radicado y el mensaje; ya <c>Sent</c>, <c>Processed</c> sin efecto; <c>Superseded</c> o <c>Voided</c>, <c>Rejected</c>
/// <c>Lending.PayrollFile.Superseded</c>; huella distinta, <c>Rejected</c> <c>Lending.PayrollFile.HashMismatch</c>; archivo inexistente,
/// <c>Rejected</c> <c>Generic.NotFound</c>. Recibo único por mensaje con el archivo. Sin ruta. (nuevo)
/// </summary>
public sealed record ConsumePayrollFileSentCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePayrollFileSentCommandValidator : AbstractValidator<ConsumePayrollFileSentCommand>
{
    public ConsumePayrollFileSentCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePayrollFileSentCommandHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IDateTimeService reloj)
    : IRequestHandler<ConsumePayrollFileSentCommand, Result<ResultadoDeConsumo>>
{
    public const string Chequeo = "PayrollFileSent";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePayrollFileSentCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Lending, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (mensaje.Payload is not EnvioDeDescuentosConfirmadoV1 envio || !VersionesAceptadasDeCartera.Acepta(mensaje.Type, mensaje.Version))
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted",
                $"Cartera no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var recibo = await RecibosDeLibranza.ReciboAsync(db, mensaje.MessageId, ct);
            if (recibo is not null && recibo.Status != InboundMessageStatus.Rejected)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational));
            var ahora = reloj.UtcNow;
            recibo ??= RecibosDeLibranza.Nuevo(mensaje, ahora);

            var archivo = await db.PayrollDeductionFiles.FirstOrDefaultAsync(f => f.PublicId == envio.FilePublicId && !f.IsDeleted, ct);
            if (archivo is null)
                return Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, mensaje, Chequeo,
                    new ErrorConDatos("Generic.NotFound", "El archivo de descuentos no existe en Cartera.", new { filePublicId = envio.FilePublicId }), ahora, ct));
            recibo.PayrollDeductionFileId = archivo.Id;
            if (archivo.Status is PayrollFileStatus.Superseded or PayrollFileStatus.Voided)
                return Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, mensaje, Chequeo,
                    ErroresDeLibranza.ArchivoReemplazado(archivo.Number, archivo.Status.ToString()), ahora, ct));
            if (!string.Equals(envio.FileSha256, archivo.FileSha256, StringComparison.OrdinalIgnoreCase))
                return Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, mensaje, Chequeo, ErroresDeLibranza.HuellaDistinta(archivo.Number), ahora, ct));

            archivo.MarcarEnviado(envio.SentAt.UtcDateTime, mensaje.MessageId, envio.Reference);
            return Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, mensaje, Chequeo, null, ahora, ct));
        }, ct);
    }
}

// ------------------------------------------------------------------------------------------------------------- resultado de descuentos --

/// <summary>
/// Consume un <c>ResultadoDeDescuentos</c> v1 (K2, T0838; mensajes.md §12.6; FR-114, FR-115), todo o nada:
/// <list type="bullet">
/// <item>la versión citada existe (<c>Generic.NotFound</c>), es la vigente (<c>Lending.PayrollFile.Superseded</c>) y su huella casa
/// (<c>.HashMismatch</c>); toda línea es del archivo y espera lo enviado (<c>.LineMismatch</c> con las líneas en <c>data</c>) —con un error no se
/// escribe nada y el recibo queda <c>Rejected</c>, reprocesable—;</item>
/// <item>lo descontado: <c>LND_PayrollDeductionResults</c> con la fecha del descuento, el vencimiento de la ventana de giro y el umbral de mora de la
/// pagaduría sellados (queda «pendiente de giro», sin mora para el deudor dentro del umbral);</item>
/// <item>lo no aplicado: <c>LND_InstallmentNonDeductions</c> con origen <c>PaymasterResult</c> y el motivo traducido por los códigos de la versión,
/// la alerta <c>Cartera.NoAplicadoDePagaduria</c> (una por archivo), el aviso al deudor y la regla del convenio (<see cref="SeguimientoDeNoAplicados"/>);
/// una parcial deja las dos cosas;</item>
/// <item>la versión <c>Generated</c> pasa a <c>Sent</c> (el resultado prueba el envío); un resultado posterior de la misma línea reemplaza al anterior
/// (<c>SupersedesResultId</c>).</item>
/// </list>
/// Recibo único por mensaje. Sin ruta. (nuevo)
/// </summary>
public sealed record ConsumePayrollDeductionResultCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePayrollDeductionResultCommandValidator : AbstractValidator<ConsumePayrollDeductionResultCommand>
{
    public ConsumePayrollDeductionResultCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePayrollDeductionResultCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeCartera cerrojo,
    ConveniosDeLibranza convenios,
    SeguimientoDeNoAplicados seguimiento,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ConsumePayrollDeductionResultCommand, Result<ResultadoDeConsumo>>
{
    public const string Chequeo = "PayrollDeductionResult";

    /// <summary>Quienes reciben la alerta del no aplicado (TiposDeAlerta).</summary>
    public const string PermisoDeLaAlerta = "Lending.PayrollNotApplied.Manage";

    private sealed record LineaLeida(DeductionResultLineV1 Entrada, PayrollDeductionFileLine Linea, PayrollResultOutcome Resultado, decimal Descontado,
        decimal NoDescontado);

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePayrollDeductionResultCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Lending, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (mensaje.Payload is not ResultadoDeDescuentosV1 resultado || !VersionesAceptadasDeCartera.Acepta(mensaje.Type, mensaje.Version))
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted",
                $"Cartera no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var recibo = await RecibosDeLibranza.ReciboAsync(db, mensaje.MessageId, ct);
            if (recibo is not null && recibo.Status != InboundMessageStatus.Rejected)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational));
            var ahora = reloj.UtcNow;
            recibo ??= RecibosDeLibranza.Nuevo(mensaje, ahora);

            var archivo = await db.PayrollDeductionFiles.FirstOrDefaultAsync(f => f.PublicId == resultado.FilePublicId && !f.IsDeleted, ct);
            if (archivo is null)
                return await RechazarAsync(recibo, mensaje, new ErrorConDatos("Generic.NotFound", "El archivo de descuentos no existe en Cartera.",
                    new { filePublicId = resultado.FilePublicId }), ahora, ct);
            recibo.PayrollDeductionFileId = archivo.Id;
            if (archivo.Status is PayrollFileStatus.Superseded or PayrollFileStatus.Voided)
                return await RechazarAsync(recibo, mensaje, ErroresDeLibranza.ArchivoReemplazado(archivo.Number, archivo.Status.ToString()), ahora, ct);
            if (!string.Equals(resultado.FileSha256, archivo.FileSha256, StringComparison.OrdinalIgnoreCase))
                return await RechazarAsync(recibo, mensaje, ErroresDeLibranza.HuellaDistinta(archivo.Number), ahora, ct);

            // Todo o nada: cada línea es del archivo y espera lo que se envió.
            var lineas = await db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id).ToDictionaryAsync(l => l.PublicId, ct);
            var leidas = new List<LineaLeida>();
            var malas = new List<object>();
            foreach (var e in resultado.Lines)
            {
                if (!lineas.TryGetValue(e.DeductionFileLinePublicId, out var linea))
                {
                    malas.Add(new { deductionFileLinePublicId = e.DeductionFileLinePublicId, reason = "NotInFile" });
                    continue;
                }
                if (e.ExpectedAmount != linea.TotalAmount)
                {
                    malas.Add(new { deductionFileLinePublicId = e.DeductionFileLinePublicId, reason = "ExpectedAmount", expected = linea.TotalAmount,
                        received = e.ExpectedAmount });
                    continue;
                }
                var problema = Leer(e, linea, out var leida);
                if (problema is not null)
                {
                    malas.Add(new { deductionFileLinePublicId = e.DeductionFileLinePublicId, reason = problema });
                    continue;
                }
                leidas.Add(leida!);
            }
            if (malas.Count > 0) return await RechazarAsync(recibo, mensaje, ErroresDeLibranza.LineasQueNoCasan(malas), ahora, ct);

            var version = await db.PayrollAgreementVersions.AsNoTracking().FirstAsync(v => v.Id == archivo.PayrollAgreementVersionId, ct);
            var codigos = await db.PayrollAgreementResultCodes.AsNoTracking().Where(c => c.PayrollAgreementVersionId == version.Id && !c.IsDeleted)
                .ToDictionaryAsync(c => c.PaymasterCode, StringComparer.OrdinalIgnoreCase, ct);
            var parametros = await convenios.ParametrosAsync(version, reloj.HoyLocal, ct);
            if (parametros.IsFailure) return Result.Failure<ResultadoDeConsumo>(parametros.Error);

            var creditos = leidas.Select(l => l.Linea.LoanId).Distinct().Order().ToList();
            var deCredito = await db.Loans.AsNoTracking().Where(l => creditos.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(deCredito.Values.Select(l => l.DebtorPersonId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(creditos, ModoDeEspera.Esperar, ct);

            if (archivo.Status == PayrollFileStatus.Generated) archivo.MarcarEnviado(ahora, mensaje.MessageId, null);
            archivo.AnotarPrimerResultado(ahora);
            recibo.Status = InboundMessageStatus.Processed;
            if (recibo.Id == 0) db.LendingInboundMessages.Add(recibo);
            await db.SaveChangesAsync(ct);

            var idsDeLinea = leidas.Select(l => l.Linea.Id).ToList();
            var anteriores = (await db.PayrollDeductionResults.AsNoTracking()
                    .Where(r => idsDeLinea.Contains(r.PayrollDeductionFileLineId) && !r.IsDeleted
                                && !db.PayrollDeductionResults.Any(x => x.SupersedesResultId == r.Id && !x.IsDeleted))
                    .ToListAsync(ct))
                .GroupBy(r => r.PayrollDeductionFileLineId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Id).First().Id);

            var noAplicados = new List<(InstallmentNonDeduction Fila, int DeudorId)>();
            Guid? alerta = null;
            foreach (var l in leidas)
            {
                var codigo = l.Entrada.PaymasterResultCode?.Trim();
                var motivo = MotivoDe(l, codigo, codigos);
                var fila = new PayrollDeductionResult
                {
                    PayrollDeductionFileLineId = l.Linea.Id,
                    PayrollDeductionFileId = archivo.Id,
                    LoanId = l.Linea.LoanId,
                    InboundMessageId = recibo.Id,
                    Outcome = l.Resultado,
                    DeductedAmount = l.Descontado,
                    NotDeductedAmount = l.NoDescontado,
                    DeductionDate = l.Descontado > 0m ? l.Entrada.DeductionDate : null,
                    PaymasterResultCode = string.IsNullOrEmpty(codigo) ? null : codigo.Length > 20 ? codigo[..20] : codigo,
                    NonDeductionReason = l.NoDescontado > 0m ? motivo : null,
                    TransferDueDate = l.Descontado > 0m ? PayrollDeductionResult.VencimientoDelGiro(l.Entrada.DeductionDate, parametros.Value.VentanaDeGiroDias) : null,
                    PaymasterArrearsThresholdDays = l.Descontado > 0m ? parametros.Value.UmbralDeMoraDias : null,
                    SupersedesResultId = anteriores.TryGetValue(l.Linea.Id, out var previo) ? previo : null,
                };
                db.PayrollDeductionResults.Add(fila);
                await db.SaveChangesAsync(ct);
                if (l.NoDescontado <= 0m) continue;

                alerta ??= await AlertarAsync(archivo, leidas.Count(x => x.NoDescontado > 0m), ct);
                var cuota = await CuotaAsync(l.Linea, archivo, ct);
                if (cuota is null) continue;
                var credito = deCredito[l.Linea.LoanId];
                var noAplicado = new InstallmentNonDeduction
                {
                    LoanId = credito.Id,
                    RepaymentInstallmentId = cuota.Value.Id,
                    InstallmentNumber = cuota.Value.Numero,
                    DueDate = cuota.Value.Vencimiento,
                    Origin = NonDeductionOrigin.PaymasterResult,
                    Reason = motivo,
                    ReasonText = Recortar(l.Entrada.Detail ?? (codigo is not null && codigos.TryGetValue(codigo, out var c) ? c.Description : null), 300),
                    ExpectedAmount = l.Linea.TotalAmount,
                    NotDeductedAmount = l.NoDescontado,
                    CollectorCode = mensaje.OriginModule,
                    PayrollDeductionResultId = fila.Id,
                    AlertPublicId = alerta,
                };
                db.InstallmentNonDeductions.Add(noAplicado);
                noAplicados.Add((noAplicado, credito.DebtorPersonId));
            }
            await db.SaveChangesAsync(ct);
            foreach (var (fila, deudor) in noAplicados)
                await seguimiento.AplicarReglaAsync(fila, deudor, version.NonDeductionAction, ct);

            return Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, mensaje, Chequeo, null, ahora, ct));
        }, ct);
    }

    /// <summary>Lee el resultado de una línea; devuelve el motivo si no casa (resultado desconocido, valores fuera de rango, sin fecha de descuento).</summary>
    private static string? Leer(DeductionResultLineV1 e, PayrollDeductionFileLine linea, out LineaLeida? leida)
    {
        leida = null;
        if (e.DeductedAmount < 0m || e.DeductedAmount > linea.TotalAmount) return "DeductedAmount";
        PayrollResultOutcome resultado;
        switch (e.Outcome)
        {
            case "Deducted":
                if (e.DeductedAmount != linea.TotalAmount) return "DeductedAmount";
                resultado = PayrollResultOutcome.Deducted;
                break;
            case "PartiallyDeducted":
                if (e.DeductedAmount <= 0m || e.DeductedAmount >= linea.TotalAmount) return "DeductedAmount";
                resultado = PayrollResultOutcome.PartiallyDeducted;
                break;
            case "NotApplied":
                if (e.DeductedAmount != 0m) return "DeductedAmount";
                resultado = PayrollResultOutcome.NotDeducted;
                break;
            default:
                return "Outcome";
        }
        if (e.DeductedAmount > 0m && e.DeductionDate is null) return "DeductionDate";
        leida = new LineaLeida(e, linea, resultado, e.DeductedAmount, linea.TotalAmount - e.DeductedAmount);
        return null;
    }

    /// <summary>El motivo del no aplicado: el del código de la pagaduría (traducido por la versión), el que informó Recaudo, u «otro».</summary>
    private static NonDeductionReason MotivoDe(LineaLeida l, string? codigo, IReadOnlyDictionary<string, PayrollAgreementResultCode> codigos)
    {
        if (codigo is not null && codigos.TryGetValue(codigo, out var c) && c.NonDeductionReason is { } traducido) return traducido;
        if (Enum.TryParse<MotivoDeNoDescuento>(l.Entrada.Reason, ignoreCase: false, out var informado)) return (NonDeductionReason)(int)informado;
        return NonDeductionReason.Other;
    }

    /// <summary>La alerta del archivo con no aplicados (una por archivo: el <c>DedupKey</c> lleva su <c>PublicId</c>).</summary>
    private async Task<Guid?> AlertarAsync(PayrollDeductionFile archivo, int cuantos, CancellationToken ct)
    {
        var r = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.NoAplicadoDePagaduria,
            $"No aplicados en el archivo {archivo.Number}",
            $"La pagaduría no descontó {cuantos} línea(s) del archivo {archivo.Number} ({archivo.PeriodYear}-{archivo.PeriodMonth:00}). Revise los no aplicados: "
            + "las cuotas siguen exigibles y la regla del convenio ya se aplicó.",
            EntityType: nameof(PayrollDeductionFile), EntityPublicId: archivo.PublicId,
            DedupKey: $"{TiposDeAlerta.NoAplicadoDePagaduria}:{archivo.PublicId:D}",
            RecipientPermissions: [PermisoDeLaAlerta], ModuloDeOrigen: "Lending"), ct);
        return r.IsSuccess ? r.Value.AlertPublicId : null;
    }

    /// <summary>
    /// La cuota del no aplicado: la de la línea; si la línea no la nombra (sólo vencido o cancelación), la primera cuota del plan vigente que vence
    /// en el período o después.
    /// </summary>
    private async Task<(long Id, int Numero, DateOnly Vencimiento)?> CuotaAsync(PayrollDeductionFileLine linea, PayrollDeductionFile archivo, CancellationToken ct)
    {
        if (linea.RepaymentInstallmentId is { } id && linea.InstallmentNumber is { } numero && linea.DueDate is { } vence) return (id, numero, vence);
        var version = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == linea.LoanId && !v.IsDeleted)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (version is null) return null;
        var desde = new DateOnly(archivo.PeriodYear, archivo.PeriodMonth, 1);
        var cuotas = await db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == version.Value).OrderBy(i => i.InstallmentNumber)
            .Select(i => new { i.Id, i.InstallmentNumber, i.DueDate }).ToListAsync(ct);
        var cuota = cuotas.FirstOrDefault(c => c.DueDate >= desde) ?? cuotas.LastOrDefault();
        return cuota is null ? null : (cuota.Id, cuota.InstallmentNumber, cuota.DueDate);
    }

    private async Task<Result<ResultadoDeConsumo>> RechazarAsync(LendingInboundMessage recibo, MensajeEntrante m, Error e, DateTime ahora, CancellationToken ct) =>
        Result.Success(await RecibosDeLibranza.CerrarAsync(db, recibo, m, Chequeo, e, ahora, ct));

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim().Length > largo ? texto.Trim()[..largo] : texto.Trim();
}
