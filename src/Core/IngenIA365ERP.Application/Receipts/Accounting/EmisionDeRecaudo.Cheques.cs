using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US15 (T0218; FR-060, FR-085; contracts/contabilidad.md §3): el cheque recibido devuelto, en línea, enlazado al recibo. Sin guardar.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseChequeDevuelto = "ReceivedCheckReturn";
    public const string TipoChequeDevuelto = "CD";

    /// <summary>
    /// <c>ChequeRecibidoDevuelto</c>: por porción reversada, débito al puente del módulo (o al concepto vario) y crédito a <c>ChequesEnCanje</c>; lo que el cheque respaldaba y
    /// ya se entregó, débito a <c>CuentaPorCobrarPagador</c> (con el pagador de tercero) y crédito a <c>ChequesEnCanje</c>.
    /// </summary>
    public async Task<IntegrationMessage> ChequeRecibidoDevueltoAsync(ReceivedCheck cheque, string numero, Receipt recibo, ReceiptMeans medio,
        IReadOnlyList<(ReceiptApplicationPortion Porcion, ReceiptApplication Aplicacion)> reversadas, decimal porCobrar, DateOnly fecha, CancellationToken ct)
    {
        var dim = recibo.CashBoxId is { } caja ? await DimensionesDeCajaAsync(caja, medio.PaymentMeansId, ct) : await DimensionesDeOficinaAsync(recibo.BranchId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == recibo.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var conceptos = reversadas.Where(p => p.Aplicacion.MiscConceptId is not null).Select(p => p.Aplicacion.MiscConceptId!.Value).Distinct().ToList();
        var codigos = await db.MiscConcepts.AsNoTracking().Where(c => conceptos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var lineas = new List<FactLineV1>();
        foreach (var (p, a) in reversadas)
        {
            lineas.Add(a.Target == ApplicationTarget.MiscConcept
                ? Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Debito, p.Amount, dim with { MiscConceptCode = codigos.GetValueOrDefault(a.MiscConceptId!.Value) }, cruce: numero)
                : Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Debito, p.Amount, dim with
                {
                    TargetModule = ModulosDeOrigen.Cartera, TargetNature = Naturaleza(a.Target, null),
                }, cruce: numero));
            lineas.Add(Linea(RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.Credito, p.Amount, dim, cruce: cheque.CheckNumber));
        }
        if (porCobrar > 0m)
        {
            lineas.Add(Linea(RolesDeRecaudo.CuentaPorCobrarPagador, RolesDeRecaudo.Debito, porCobrar, dim, pagador, numero));
            lineas.Add(Linea(RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.Credito, porCobrar, dim, cruce: cheque.CheckNumber));
        }
        var contenido = new ChequeRecibidoDevueltoV1
        {
            ReceivedCheckPublicId = cheque.PublicId, Number = numero, CheckNumber = cheque.CheckNumber, ReceiptPublicId = recibo.PublicId, ReceiptNumber = recibo.Number,
            OriginalFact = await HechoDelReciboAsync(recibo, ct), ReturnCauseCode = cheque.ReturnCauseCode ?? string.Empty, Amount = cheque.Amount, PayerReceivable = porCobrar,
            ReturnedOn = fecha, RegisteredBy = await UsuarioAsync(ct), Lines = lineas,
        };
        return await EmitirAsync(contenido, cheque.PublicId, ClaseChequeDevuelto, TipoChequeDevuelto, numero, fecha, recibo.BranchId, pagador, ct);
    }
}
