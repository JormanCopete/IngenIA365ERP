using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

/// <summary>
/// Consume en Recaudo el archivo de descuentos que generó Cartera para una pagaduría (feature 015, US7, T0206; R-16; FR-075; mensajes.md §3.2): <c>DescuentosPorPagaduria</c> v1
/// (sólo créditos) o v2 (créditos y cuotas de cuentas, por grupo y concepto de la pagaduría). Guarda el archivo y sus líneas con la versión del mensaje y su recibo único
/// (<c>RCD_InboundMessages</c>). Una versión que reemplaza a otra todavía no enviada la deja <c>Superseded</c>; si la anterior ya se envió, el mensaje queda <c>Rejected</c> con
/// <c>Receipts.Paymaster.SupersedesSentFile</c> y se alerta (Cartera ya lo impide). Sin ruta: lo manda <c>DestinoRecaudo</c>. (nuevo)
/// </summary>
public sealed record ConsumePayrollDeductionFileCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePayrollDeductionFileCommandValidator : AbstractValidator<ConsumePayrollDeductionFileCommand>
{
    public ConsumePayrollDeductionFileCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePayrollDeductionFileCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeRecaudo cerrojo,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ConsumePayrollDeductionFileCommand, Result<ResultadoDeConsumo>>
{
    /// <summary>Una línea del archivo, de la v1 o de la v2.</summary>
    private sealed record Linea(Guid LineaPublicId, PayrollDeductionLineKind Clase, Guid Producto, string Numero, Guid? Cuota, int? NumeroDeCuota, Guid Persona, string Empleado,
        string? Concepto, string Novedad, DateOnly Vence, decimal Valor, decimal Vencido);

    /// <summary>El archivo, de la v1 o de la v2.</summary>
    private sealed record Archivo(PayrollAgreementRefV1 Convenio, string? Grupo, PayrollPeriodRefV1 Periodo, int Version, DeductionFileRefV1 Adjunto, DateOnly EnviarA,
        DeductionFileTotalsV1 Totales, SupersededDeductionFileV1? Reemplaza, IReadOnlyList<Linea> Lineas);

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePayrollDeductionFileCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Receipts, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await db.ReceiptsInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == mensaje.MessageId, ct) is { } previo)
            return Result.Success<ResultadoDeConsumo>(previo.Outcome == InboundMessageOutcome.Processed
                ? new ResultadoDeConsumo.AlreadyProcessed(previo.PublicId, SinComprobante: MotivoSinComprobante.Informational)
                : new ResultadoDeConsumo.Rejected(previo.Code ?? "Receipts.Paymaster.Rejected", previo.Detail ?? "El archivo fue rechazado al recibirlo."));
        Archivo? archivo = mensaje.Payload switch
        {
            DescuentosPorPagaduriaV1 v1 when mensaje.Version == 1 => new(v1.Agreement, null, v1.Period, v1.FileVersion, v1.File, v1.SendBy, v1.Totals, v1.SupersedesFile,
                v1.Lines.Select(l => new Linea(l.DeductionFileLinePublicId, PayrollDeductionLineKind.Loan, l.Loan.LoanPublicId, l.Loan.LoanNumber, null, l.InstallmentNumber,
                    l.DebtorPersonPublicId, l.PayerEmployeeId, null, l.NoveltyKind, l.DueDate, l.Amount, l.PastDueAmount)).ToList()),
            DescuentosPorPagaduriaV2 v2 when mensaje.Version == 2 => new(v2.Agreement, v2.FileGroup.Code, v2.Period, v2.FileVersion, v2.File, v2.SendBy, v2.Totals,
                v2.SupersedesFile, v2.Lines.Select(l => new Linea(l.DeductionFileLinePublicId,
                    l.Kind == "AccountInstallment" ? PayrollDeductionLineKind.AccountInstallment : PayrollDeductionLineKind.Loan,
                    l.Kind == "AccountInstallment" ? l.Account?.AccountPublicId ?? Guid.Empty : l.Loan?.LoanPublicId ?? Guid.Empty,
                    l.Kind == "AccountInstallment" ? l.Account?.AccountNumber ?? string.Empty : l.Loan?.LoanNumber ?? string.Empty,
                    l.InstallmentPublicId, l.InstallmentNumber, l.PersonPublicId, l.PayerEmployeeId, l.PaymasterConceptCode, l.NoveltyKind, l.DueDate, l.Amount,
                    l.PastDueAmount)).ToList()),
            _ => null,
        };
        if (archivo is null)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Recaudo no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            await cerrojo.TomarSetupAsync(ct: ct);
            var pagaduria = await db.People.AsNoTracking().Where(p => p.PublicId == archivo.Convenio.PaymasterPersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (pagaduria is null) return await RechazarAsync(mensaje, ahora, ErroresDeRecaudo.PagaduriaDesconocida(), ct);
            var publicas = archivo.Lineas.Select(l => l.Persona).Distinct().ToList();
            var personas = await db.People.AsNoTracking().Where(p => publicas.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
            if (personas.Count != publicas.Count) return await RechazarAsync(mensaje, ahora, ErroresDeRecaudo.PersonaDelArchivoDesconocida(), ct);
            var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.PublicId == mensaje.Envelope.BranchPublicId).Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(ct) ?? await db.Branches.AsNoTracking().OrderBy(b => b.Id).Select(b => b.Id).FirstAsync(ct);

            if (archivo.Reemplaza is { } reemplaza
                && await db.ReceiptsPayrollDeductionFiles.FirstOrDefaultAsync(f => f.FileMessagePublicId == reemplaza.MessageId || (f.FilePublicId == reemplaza.FilePublicId
                    && f.FileVersion == reemplaza.FileVersion), ct) is { } anterior)
            {
                if (anterior.Status is PayrollDeductionFileState.Sent or PayrollDeductionFileState.Responded)
                {
                    await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoNoAplicadoDePagaduria, "Versión del archivo de descuentos rechazada",
                        $"Llegó la versión {archivo.Version} del archivo {anterior.FileNumber}, pero la anterior ya se envió a la pagaduría: no se reemplaza.",
                        "PayrollDeductionFile", anterior.PublicId, DedupKey: $"Recaudo.ReemplazoDeArchivoEnviado:{mensaje.MessageId:N}", ModuloDeOrigen: "Receipts"), ct);
                    return await RechazarAsync(mensaje, ahora, ErroresDeRecaudo.ReemplazaArchivoEnviado(anterior.FileNumber), ct);
                }
                anterior.Status = PayrollDeductionFileState.Superseded;
            }

            var nuevo = new PayrollDeductionFile
            {
                FileMessagePublicId = mensaje.MessageId, MessageVersion = (short)mensaje.Version, PaymasterPersonId = pagaduria.Value, AgreementCode = Recortar(archivo.Convenio.AgreementCode, 20),
                AgreementVersion = archivo.Convenio.AgreementVersion, FileGroupCode = archivo.Grupo, PeriodYear = archivo.Periodo.Year, PeriodMonth = archivo.Periodo.Month,
                SubPeriod = archivo.Periodo.SubPeriod, FileVersion = archivo.Version, FilePublicId = mensaje.Envelope.Origin.PublicId,
                FileNumber = Recortar(mensaje.Envelope.Origin.Number, 40), FileAttachmentPublicId = archivo.Adjunto.AttachmentPublicId, FileName = Recortar(archivo.Adjunto.FileName, 200),
                FileSha256 = archivo.Adjunto.Sha256, FormatCode = Recortar(archivo.Adjunto.FormatCode, 20), SendBy = archivo.EnviarA, LineCount = archivo.Totales.LineCount,
                Amount = archivo.Totales.Amount, PastDueAmount = archivo.Totales.PastDueAmount, SupersedesFileMessagePublicId = archivo.Reemplaza?.MessageId, BranchId = oficina,
            };
            db.ReceiptsPayrollDeductionFiles.Add(nuevo);
            await db.SaveChangesAsync(ct);
            db.ReceiptsPayrollDeductionFileLines.AddRange(archivo.Lineas.Select(l => new PayrollDeductionFileLine
            {
                PayrollDeductionFileId = nuevo.Id, DeductionFileLinePublicId = l.LineaPublicId, Kind = l.Clase, ProductPublicId = l.Producto, ProductNumber = Recortar(l.Numero, 30),
                InstallmentPublicId = l.Cuota, InstallmentNumber = l.NumeroDeCuota, PersonId = personas[l.Persona], PayerEmployeeId = Recortar(l.Empleado, 30),
                PaymasterConceptCode = l.Concepto, NoveltyKind = Recortar(l.Novedad, 30), DueDate = l.Vence, Amount = l.Valor, PastDueAmount = l.Vencido,
            }));
            db.ReceiptsInboundMessages.Add(Recibo(mensaje, ahora, InboundMessageOutcome.Processed, null,
                $"Archivo {nuevo.FileNumber} v{nuevo.FileVersion} de la pagaduría: {archivo.Lineas.Count} líneas."));
            await db.SaveChangesAsync(ct);
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(nuevo.PublicId, null, null, MotivoSinComprobante.Informational));
        }, ct);
    }

    private async Task<Result<ResultadoDeConsumo>> RechazarAsync(MensajeEntrante m, DateTime ahora, Error error, CancellationToken ct)
    {
        db.ReceiptsInboundMessages.Add(Recibo(m, ahora, InboundMessageOutcome.Rejected, error.Code, error.Message));
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(error.Code, error.Message));
    }

    private static ReceiptsInboundMessage Recibo(MensajeEntrante m, DateTime ahora, InboundMessageOutcome resultado, string? codigo, string? detalle) => new()
    {
        MessagePublicId = m.MessageId, Type = m.Type, Version = (short)m.Version, OriginModule = m.Envelope.OriginModule, ReceivedAt = ahora, Outcome = resultado, Code = codigo,
        Detail = detalle is { Length: > 1000 } ? detalle[..1000] : detalle,
    };

    private static string Recortar(string? texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
