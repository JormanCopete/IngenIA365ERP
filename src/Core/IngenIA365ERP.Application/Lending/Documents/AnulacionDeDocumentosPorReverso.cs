using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// El escritor común de la anulación automática de documentos (feature 013, US13, T0787; T80; adaptadores-y-archivos.md §8.8; api.md §10): lo llaman
/// <b>dentro</b> de su unidad de trabajo los tres comandos que reversan —el reverso de un pago por pantalla (<c>ReverseLoanPaymentCommand</c>) y por el
/// contrato (<c>ReversarAsync</c>), los dos por <see cref="ReversionDePago"/>, y el reverso del desembolso (<c>ReversoDeDesembolso</c>)—. Con
/// <see cref="VigenciaDeDocumentos"/> decide qué documentos que cubren el crédito tienen un <c>InformationCutoff</c> igual o posterior al registro de lo
/// reversado y les agrega el evento <c>Voided</c> con el motivo («Se reversó el pago …») y la operación causa, sin guardar (guarda quien reversa).
/// Devuelve los <c>PublicId</c> anulados para el <c>voidedDocuments[]</c> de la respuesta del reverso. Reemplaza al esqueleto
/// <see cref="SinDocumentosQueAnular"/> de la fase de pagos. (nuevo)
/// </summary>
public sealed class AnulacionDeDocumentosPorReverso(IApplicationDbContext db) : IVigenciaDeDocumentosDeCartera
{
    public async Task<IReadOnlyList<(Guid PublicId, string Kind, string Number)>> AfectadosAsync(int loanId, LoanOperation reversada, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reversada);
        var (candidatos, aAnular) = await CalcularAsync(loanId, reversada, ct);
        return candidatos.Where(d => aAnular.Contains(d.Id)).Select(d => (d.PublicId, d.Kind.ToString(), d.Number)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> AnularAsync(int loanId, LoanOperation reversada, LoanOperation reverso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reversada);
        ArgumentNullException.ThrowIfNull(reverso);
        var (candidatos, aAnular) = await CalcularAsync(loanId, reversada, ct);
        if (aAnular.Count == 0) return [];

        var credito = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => l.Number).FirstOrDefaultAsync(ct) ?? string.Empty;
        var motivo = Motivo(reversada, credito);
        var (actor, usuario) = reverso.ActorUserId is { } id ? (ActorKind.Person, (int?)id) : (ActorKind.Process, (int?)null);
        var anulados = new List<Guid>();
        foreach (var documento in candidatos.Where(d => aAnular.Contains(d.Id)))
        {
            db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Anulado(documento.Id, motivo, reverso.RegisteredAt, actor, usuario, reverso.Id));
            anulados.Add(documento.PublicId);
        }
        return anulados;
    }

    /// <summary>El motivo que queda en el evento y que muestra la verificación.</summary>
    public static string Motivo(LoanOperation reversada, string credito) => reversada.Kind switch
    {
        LoanOperationKind.Payment => $"Se reversó el pago {reversada.Number} del crédito {credito}.",
        LoanOperationKind.Disbursement => $"Se reversó el desembolso {reversada.Number} del crédito {credito}.",
        _ => $"Se reversó la operación {reversada.Number} del crédito {credito}.",
    };

    private async Task<(IReadOnlyList<IssuedLendingDocument> Candidatos, IReadOnlyList<int> AAnular)> CalcularAsync(int loanId, LoanOperation reversada,
        CancellationToken ct)
    {
        var desde = reversada.RegisteredAt;
        var cubren = db.IssuedDocumentLoans.AsNoTracking().Where(x => x.LoanId == loanId).Select(x => x.IssuedDocumentId);
        var candidatos = await db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => cubren.Contains(d.Id) && d.InformationCutoff >= desde).OrderBy(d => d.Id).ToListAsync(ct);
        if (candidatos.Count == 0) return ([], []);
        var ids = candidatos.Select(d => d.Id).ToList();
        var anulados = (await db.IssuedDocumentEvents.AsNoTracking()
                .Where(e => ids.Contains(e.IssuedDocumentId) && e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId).ToListAsync(ct))
            .Concat(db.IssuedDocumentEvents.Local.Where(e => e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId))
            .ToHashSet();
        var aAnular = VigenciaDeDocumentos.AAnular(
            candidatos.Select(d => new DocumentoQueCubre(d.Id, [loanId], d.InformationCutoff, anulados.Contains(d.Id))), loanId, desde);
        return (candidatos, aAnular);
    }
}
