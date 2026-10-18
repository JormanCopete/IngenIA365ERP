using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// La anulación automática de documentos por el reverso de una operación de cuenta (feature 014, US3, T087; contracts/documentos.md §4; FR-101): la
/// llama <c>ReversionDeCuentas</c> <b>dentro</b> de su unidad de trabajo. Entre los documentos que cubren alguna cuenta de la operación
/// (<c>LND_IssuedDocumentAccounts</c>) y que la vieron —<c>InformationCutoff</c> igual o posterior a su registro—, anula con el evento <c>Voided</c> y la
/// operación de cuenta causa:
/// <list type="bullet">
/// <item>la constancia de aportes (y los demás documentos con corte) si la operación tiene fecha valor ≤ su corte;</item>
/// <item>el paz y salvo de aportes si la operación pagó cuotas (la que dejó las cuotas al día).</item>
/// </list>
/// Sin guardar: guarda quien reversa. Devuelve los <c>PublicId</c> anulados para <c>VoidedDocumentPublicIds</c>. (nuevo)
/// </summary>
public sealed class AnulacionDeDocumentosDeCuentas(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<Guid>> AnularAsync(MemberAccountOperation original, MemberAccountOperation reverso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(reverso);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == original.Id)
            .Select(t => new { t.MemberAccountId, t.InstallmentId }).ToListAsync(ct);
        var cuentas = filas.Select(f => f.MemberAccountId).Append(original.MemberAccountId).Distinct().ToList();
        var pagoCuotas = filas.Any(f => f.InstallmentId is not null);
        var cubren = db.IssuedDocumentAccounts.AsNoTracking().Where(x => cuentas.Contains(x.MemberAccountId)).Select(x => x.IssuedDocumentId);
        var candidatos = await db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => cubren.Contains(d.Id) && d.InformationCutoff >= original.RegisteredAt).OrderBy(d => d.Id).ToListAsync(ct);
        if (candidatos.Count == 0) return [];
        var ids = candidatos.Select(d => d.Id).ToList();
        var yaAnulados = (await db.IssuedDocumentEvents.AsNoTracking()
                .Where(e => ids.Contains(e.IssuedDocumentId) && e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId).ToListAsync(ct))
            .ToHashSet();

        var motivo = $"Se reversó la operación {original.Number} de la cuenta.";
        var (actor, usuario) = reverso.ActorUserId is { } id ? (ActorKind.Person, (int?)id) : (ActorKind.Process, (int?)null);
        var anulados = new List<Guid>();
        foreach (var documento in candidatos.Where(d => !yaAnulados.Contains(d.Id)))
        {
            var anula = documento.Kind switch
            {
                IssuedDocumentKind.ContributionClearance => pagoCuotas,
                _ => documento.AsOfDate is not { } corte || original.ValueDate <= corte,
            };
            if (!anula) continue;
            db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Anulado(documento.Id, motivo, reverso.RegisteredAt, actor, usuario,
                operacionDeCuentaCausa: reverso.Id == 0 ? null : reverso.Id));
            anulados.Add(documento.PublicId);
        }
        return anulados;
    }
}
