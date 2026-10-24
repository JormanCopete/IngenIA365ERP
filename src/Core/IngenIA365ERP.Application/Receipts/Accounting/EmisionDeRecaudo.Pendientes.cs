using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, R1, US3 (T0363; FR-046; contracts/contabilidad.md §3): los hechos del recaudo pendiente de aplicar, en línea. El recibo de caja no lleva estas porciones
// en su ReciboEmitido: las lleva RecaudoPendienteReconocido (rol del medio → RecaudoPendienteDeAplicar), y al aplicarse RecaudoPendienteAplicado
// (RecaudoPendienteDeAplicar → PuenteDelModulo). Así el dinero no pasa por el puente del módulo antes de que el módulo exista.
public sealed partial class EmisionDeRecaudo
{
    public const string ClasePendienteDeAplicar = "PendingApplication";

    /// <summary>El módulo del puente de un pendiente (<c>TargetModule</c> de la dimensión): los dos son de Cartera.</summary>
    private static string ModuloDelPendiente(ApplicationTarget destino) => ModulosDeOrigen.Cartera;

    /// <summary><c>RecaudoPendienteReconocido</c>: por porción, débito al rol del medio y crédito a <c>RecaudoPendienteDeAplicar</c> con la persona. Sin guardar.</summary>
    public async Task<IntegrationMessage> RecaudoPendienteReconocidoAsync(Receipt recibo, PendingApplication pendiente, IReadOnlyList<(ReceiptApplicationPortion Porcion,
        ReceiptMeans Medio)> porciones, string codigoDelTipo, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == pendiente.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var lineas = new List<FactLineV1>();
        foreach (var (p, medio) in porciones)
        {
            var dim = recibo.CashBoxId is { } caja ? await DimensionesDeCajaAsync(caja, medio.PaymentMeansId, ct) : await DimensionesDeOficinaAsync(recibo.BranchId, ct);
            lineas.Add(Linea(RolDelMedio(medio.MeansClass), RolesDeRecaudo.Debito, p.Amount, dim, cruce: recibo.Number));
            lineas.Add(Linea(RolesDeRecaudo.RecaudoPendienteDeAplicar, RolesDeRecaudo.Credito, p.Amount, dim with { TargetModule = ModuloDelPendiente(pendiente.TargetModule) },
                persona, recibo.Number));
        }
        var contenido = new RecaudoPendienteReconocidoV1
        {
            ReceiptPublicId = recibo.PublicId, ReceiptNumber = recibo.Number, PendingApplicationPublicId = pendiente.PublicId, PersonPublicId = persona,
            TargetModule = pendiente.TargetModule.ToString(), ProductReference = pendiente.ProductReference, Amount = pendiente.Amount, ValueDate = pendiente.ValueDate,
            RegisteredBy = await UsuarioAsync(ct), Lines = lineas,
        };
        return await EmitirAsync(contenido, pendiente.PublicId, ClasePendienteDeAplicar, codigoDelTipo, recibo.Number, recibo.ValueDate, recibo.BranchId, persona, ct);
    }

    /// <summary><c>RecaudoPendienteAplicado</c>: débito a <c>RecaudoPendienteDeAplicar</c> y crédito al puente del módulo. Sin guardar.</summary>
    public async Task<IntegrationMessage> RecaudoPendienteAplicadoAsync(PendingApplication pendiente, PendingApplicationResolution resolucion, Receipt recibo, Guid persona,
        CancellationToken ct)
    {
        var dim = await DimensionesDeOficinaAsync(recibo.BranchId, ct) with { TargetModule = ModuloDelPendiente(pendiente.TargetModule) };
        var naturaleza = pendiente.TargetModule == ApplicationTarget.Loan ? "Loan" : "Deposit";
        var contenido = new RecaudoPendienteAplicadoV1
        {
            PendingApplicationPublicId = pendiente.PublicId, ReceiptPublicId = recibo.PublicId, ReceiptNumber = recibo.Number, PersonPublicId = persona,
            ProductPublicId = resolucion.ProductPublicId, ProductNumber = resolucion.ProductNumber, ModuleVoucherNumber = resolucion.ModuleOperationNumber,
            Amount = resolucion.Amount, ValueDate = resolucion.ValueDate, AppliedBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.RecaudoPendienteDeAplicar, RolesDeRecaudo.Debito, resolucion.Amount, dim, persona, recibo.Number),
                Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Credito, resolucion.Amount, dim with { TargetNature = naturaleza, CollectorClass = ClaseDeRecaudador },
                    cruce: recibo.Number),
            ],
        };
        return await EmitirAsync(contenido, resolucion.PublicId, ClasePendienteDeAplicar, null, recibo.Number, resolucion.ValueDate, recibo.BranchId, persona, ct);
    }
}
