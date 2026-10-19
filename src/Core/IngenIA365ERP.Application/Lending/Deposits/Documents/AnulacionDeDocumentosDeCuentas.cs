using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// La anulación automática de documentos por el reverso de una operación de cuenta (feature 014, US3, T087; US13, T196; contracts/documentos.md §4;
/// FR-101): la llama <c>ReversionDeCuentas</c> <b>dentro</b> de su unidad de trabajo —el reverso por el contrato, desde la pantalla de la Cartera, por un
/// giro rechazado, por el arrastre del reverso de un pago—. Entre los documentos que cubren alguna cuenta de la operación
/// (<c>LND_IssuedDocumentAccounts</c>), que la vieron —<c>InformationCutoff</c> igual o posterior a su registro— y que no están anulados, decide por clase
/// con <see cref="VigenciaDeDocumentosDeCuentas"/> (motor puro): el extracto cuyo período alcanza la fecha valor, los certificados con corte posterior,
/// los tributarios del año o del período si la operación es tributaria, el título que selló la operación y el paz y salvo si la operación pagó cuotas.
/// Agrega el evento <c>Voided</c> con la operación de cuenta causa. Sin guardar: guarda quien reversa. Devuelve los <c>PublicId</c> anulados para
/// <c>VoidedDocumentPublicIds</c>. (nuevo)
/// </summary>
public sealed class AnulacionDeDocumentosDeCuentas(IApplicationDbContext db)
{
    /// <summary>Las filas que hacen tributaria una operación: rendimientos abonados o pagados y bonificación (además, toda fila de retención o de GMF).</summary>
    private static readonly MemberAccountTransactionKind[] Tributarias =
    [
        MemberAccountTransactionKind.InterestCredited, MemberAccountTransactionKind.InterestPaid, MemberAccountTransactionKind.BonusRecognized,
    ];

    /// <summary>Los conceptos de lo retenido: una fila que los toca es tributaria (la retención y el GMF los arman sus motores; aquí sólo se leen).</summary>
    private static readonly MemberAccountConcept[] Retenidos = [MemberAccountConcept.Withholding, MemberAccountConcept.Gmf];

    public async Task<IReadOnlyList<Guid>> AnularAsync(MemberAccountOperation original, MemberAccountOperation reverso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(reverso);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == original.Id)
            .Select(t => new { t.MemberAccountId, t.InstallmentId, t.Kind, t.Concept }).ToListAsync(ct);
        var cuentas = filas.Select(f => f.MemberAccountId).Append(original.MemberAccountId).Distinct().ToList();
        var operacion = new OperacionDeCuentaReversada(original.Id, original.ValueDate, filas.Any(f => f.InstallmentId is not null),
            filas.Any(f => Tributarias.Contains(f.Kind) || Retenidos.Contains(f.Concept)) || original.Kind is MemberAccountOperationKind.InterestSettlement);
        var cubiertas = await db.IssuedDocumentAccounts.AsNoTracking().Where(x => cuentas.Contains(x.MemberAccountId))
            .Select(x => new { x.IssuedDocumentId, x.LastOperationId, x.TermsVersionId }).ToListAsync(ct);
        var cubren = cubiertas.Select(x => x.IssuedDocumentId).Distinct().ToList();
        var candidatos = await db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => cubren.Contains(d.Id) && d.InformationCutoff >= original.RegisteredAt).OrderBy(d => d.Id).ToListAsync(ct);
        if (candidatos.Count == 0) return [];
        var ids = candidatos.Select(d => d.Id).ToList();
        var yaAnulados = (await db.IssuedDocumentEvents.AsNoTracking()
                .Where(e => ids.Contains(e.IssuedDocumentId) && e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId).ToListAsync(ct))
            .Concat(db.IssuedDocumentEvents.Local.Where(e => e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId))
            .ToHashSet();

        var motivo = $"Se reversó la operación {original.Number} de la cuenta.";
        var (actor, usuario) = reverso.ActorUserId is { } id ? (ActorKind.Person, (int?)id) : (ActorKind.Process, (int?)null);
        var anulados = new List<Guid>();
        foreach (var documento in candidatos.Where(d => !yaAnulados.Contains(d.Id)))
        {
            // El título nombra la operación que lo selló en su fila de cuenta (LastOperationId, T196).
            var sello = cubiertas.Where(x => x.IssuedDocumentId == documento.Id && x.TermsVersionId is not null).Select(x => x.LastOperationId).FirstOrDefault();
            var visto = new DocumentoDeCuentaQueCubre(documento.Kind, documento.AsOfDate, documento.PeriodFrom, documento.PeriodTo, documento.TaxYear, sello);
            if (!VigenciaDeDocumentosDeCuentas.Anula(visto, operacion)) continue;
            db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Anulado(documento.Id, motivo, reverso.RegisteredAt, actor, usuario,
                operacionDeCuentaCausa: reverso.Id == 0 ? null : reverso.Id));
            anulados.Add(documento.PublicId);
        }
        return anulados;
    }
}
