using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Banks;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

/// <summary>Una línea de una corrida, para la pantalla. (nuevo)</summary>
public sealed record ProcessingRunLineDto(Guid PublicId, string LineRef, string IdempotencyKey, ProcessingLineOutcome Outcome, decimal Amount, string? Code, string? Message,
    Guid? ReceiptPublicId, string? ReceiptNumber, int Attempts);

/// <summary>Una corrida no interactiva con sus conteos y sus líneas. (nuevo)</summary>
public sealed record ProcessingRunDto(Guid PublicId, ProcessingRunKind Kind, Guid SourcePublicId, string SourceNumber, int Lines, int Applied, int Rejected, int Pending,
    decimal Total, int Batches, DateTime StartedAt, DateTime? FinishedAt, IReadOnlyList<ProcessingRunLineDto> Items);

/// <summary>Un recibo de la tanda y la huella con que quedó (la verifica la auditoría recalculándola desde el recibo). (nuevo)</summary>
public sealed record ReciboDeLaTanda(Guid PublicId, string Sha256);

/// <summary>
/// Lo común de las corridas no interactivas de Recaudo (feature 015, US6, T0194; R-17, R-32; FR-014, FR-171; TR37; data-model §5.3): abrir o seguir una corrida, sus
/// líneas con su clave y, al terminar cada tanda, el evento <c>Receipts.ProcessingRun.Summary</c> por <c>COR_AuditOutbox</c> en la misma transacción, con la lista
/// <c>receipts[] { publicId, sha256 }</c> de cada recibo aplicado en la tanda (huella <see cref="HuellaDeRecibo.DelRecibo"/>) y un evento por línea rechazada. (nuevo)
/// </summary>
public static class CorridasNoInteractivas
{
    public const string EventoResumen = "Receipts.ProcessingRun.Summary";
    public const string EventoLineaRechazada = "Receipts.ProcessingRun.LineRejected";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>La corrida pedida (si es de esa fuente) o una nueva. Sin guardar.</summary>
    public static async Task<ProcessingRun> AbrirAsync(IApplicationDbContext db, ProcessingRunKind clase, Guid fuente, string numeroDeLaFuente, Guid? corrida, int usuario,
        DateTime ahora, CancellationToken ct)
    {
        if (corrida is { } c && await db.ProcessingRuns.FirstOrDefaultAsync(r => r.PublicId == c && r.Kind == clase, ct) is { } seguida) return seguida;
        var nueva = new ProcessingRun
        {
            Kind = clase, SourcePublicId = fuente, SourceNumber = numeroDeLaFuente.Length <= 40 ? numeroDeLaFuente : numeroDeLaFuente[..40], StartedByUserId = usuario,
            StartedAt = ahora, InputsHash = HuellaDeRecibo.Calcular(new { clase, fuente }),
        };
        db.ProcessingRuns.Add(nueva);
        return nueva;
    }

    /// <summary>La línea de la corrida con la clave (la existente al reprocesar) o una nueva pendiente con lo pedido. Sin guardar.</summary>
    public static async Task<ProcessingRunLine> LineaAsync(IApplicationDbContext db, ProcessingRun corrida, string referencia, string clave, decimal valor, object pedido,
        CancellationToken ct)
    {
        if (corrida.Id != 0 && await db.ProcessingRunLines.FirstOrDefaultAsync(l => l.ProcessingRunId == corrida.Id && l.IdempotencyKey == clave, ct) is { } ya) return ya;
        var linea = new ProcessingRunLine
        {
            ProcessingRunId = corrida.Id, LineRef = referencia.Length <= 60 ? referencia : referencia[..60], IdempotencyKey = clave, Amount = valor,
            RequestJson = JsonSerializer.Serialize(pedido, Json),
        };
        db.ProcessingRunLines.Add(linea);
        corrida.Lines++;
        corrida.Total += valor;
        return linea;
    }

    /// <summary>Lo pedido de una línea, para reprocesarla igual.</summary>
    public static T? Pedido<T>(ProcessingRunLine linea) => JsonSerializer.Deserialize<T>(linea.RequestJson, Json);

    /// <summary>Anota el resultado de la línea en ella y en los conteos de la corrida. Sin guardar.</summary>
    public static void Anotar(ProcessingRun corrida, ProcessingRunLine linea, ResultadoNoInteractivo resultado, DateTime ahora)
    {
        var antes = linea.Outcome;
        linea.Attempts++;
        linea.LastAttemptAt = ahora;
        linea.Outcome = resultado.Outcome;
        linea.Code = resultado.Code;
        linea.Message = resultado.Message is { Length: > 1000 } m ? m[..1000] : resultado.Message;
        linea.ReceiptId = resultado.Receipt?.Id ?? linea.ReceiptId;
        if (antes == ProcessingLineOutcome.Rejected && resultado.Outcome != ProcessingLineOutcome.Rejected) corrida.Rejected--;
        if (resultado.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && antes is not (ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied))
            corrida.Applied++;
        if (resultado.Outcome == ProcessingLineOutcome.Rejected && antes != ProcessingLineOutcome.Rejected) corrida.Rejected++;
    }

    /// <summary>
    /// Cierra la tanda: el evento resumen con los recibos aplicados en ella y su huella, y uno por línea rechazada, por <c>COR_AuditOutbox</c> sin guardar (nacen en el
    /// guardado de la tanda). Termina la corrida cuando ya no queda nada pendiente.
    /// </summary>
    public static async Task CerrarTandaAsync(IApplicationDbContext db, AuditoriaDeRecaudo auditoria, ProcessingRun corrida, IReadOnlyList<Receipt> recibos,
        IReadOnlyList<ProcessingRunLine> rechazadas, DateTime ahora, CancellationToken ct)
    {
        corrida.Batches++;
        var lista = new List<ReciboDeLaTanda>(recibos.Count);
        foreach (var r in recibos) lista.Add(new ReciboDeLaTanda(r.PublicId, await HuellaDeRecibo.DelReciboAsync(db, r, ct)));
        var pendientes = corrida.Lines - corrida.Applied - corrida.Rejected;
        if (pendientes <= 0) corrida.FinishedAt ??= ahora;
        await auditoria.AgregarAsync(EventoResumen, "ProcessingRun", corrida.PublicId, null, new
        {
            runPublicId = corrida.PublicId, kind = corrida.Kind.ToString(), source = corrida.SourceNumber, batch = corrida.Batches, lines = corrida.Lines,
            applied = corrida.Applied, rejected = corrida.Rejected, inputsHash = corrida.InputsHash,
            receipts = lista.Select(x => new { publicId = x.PublicId, sha256 = x.Sha256 }).ToList(),
            rejectedLines = rechazadas.Select(l => new { publicId = l.PublicId, key = l.IdempotencyKey, code = l.Code }).ToList(),
        }, ct);
        foreach (var l in rechazadas)
            await auditoria.AgregarAsync(EventoLineaRechazada, "ProcessingRunLine", l.PublicId, null,
                new { runPublicId = corrida.PublicId, key = l.IdempotencyKey, amount = l.Amount, code = l.Code, message = l.Message }, ct);
    }

    public static async Task<ProcessingRunDto> DtoAsync(IApplicationDbContext db, ProcessingRun r, CancellationToken ct)
    {
        var lineas = await db.ProcessingRunLines.AsNoTracking().Where(l => l.ProcessingRunId == r.Id).OrderBy(l => l.Id).ToListAsync(ct);
        var recibos = lineas.Where(l => l.ReceiptId is not null).Select(l => l.ReceiptId!.Value).Distinct().ToList();
        var numeros = await db.IssuedReceipts.AsNoTracking().Where(x => recibos.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => (x.PublicId, x.Number), ct);
        return new ProcessingRunDto(r.PublicId, r.Kind, r.SourcePublicId, r.SourceNumber, r.Lines, r.Applied, r.Rejected,
            lineas.Count(l => l.Outcome == ProcessingLineOutcome.Pending), r.Total, r.Batches, r.StartedAt, r.FinishedAt,
            lineas.Select(l => new ProcessingRunLineDto(l.PublicId, l.LineRef, l.IdempotencyKey, l.Outcome, l.Amount, l.Code, l.Message,
                l.ReceiptId is { } id && numeros.TryGetValue(id, out var n) ? n.PublicId : null, l.ReceiptId is { } id2 && numeros.TryGetValue(id2, out var n2) ? n2.Number : null,
                l.Attempts)).ToList());
    }
}
