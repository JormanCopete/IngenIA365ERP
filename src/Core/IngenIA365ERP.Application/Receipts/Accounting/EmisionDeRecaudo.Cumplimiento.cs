using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US14 (T0242; FR-189, FR-203; contracts/contabilidad.md §3): el dinero retenido en custodia por una coincidencia no vinculante y su liberación. La tarea los
// pedía en EmisionDeRecaudo.Caja.cs; van en su propio archivo de la familia (desviación de forma, mismo contrato). En línea, sin guardar.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseCustodia = "ComplianceCustody";

    /// <summary><c>DineroRetenidoEnCustodia</c> (DCU): débito <c>Caja</c>, crédito <c>CustodiaPorLista</c> con el pagador.</summary>
    public async Task<IntegrationMessage> DineroRetenidoEnCustodiaAsync(CashMovement movimiento, CashSession sesion, CashOperation operacion, DateOnly fecha, string codigoDelTipo,
        CancellationToken ct)
    {
        var caja = await DimensionesDeCajaAsync(sesion.CashBoxId, movimiento.FromMeansId, ct);
        var oficina = await OficinaDeLaCajaAsync(sesion.CashBoxId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == operacion.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var contenido = new DineroRetenidoEnCustodiaV1
        {
            MovementPublicId = movimiento.PublicId, Number = movimiento.Number, OperationPublicId = operacion.PublicId, SessionPublicId = sesion.PublicId,
            PayerPersonPublicId = pagador, Amount = movimiento.Amount, OperationDate = fecha, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Debito, movimiento.Amount, caja, cruce: movimiento.Number),
                Linea(RolesDeRecaudo.CustodiaPorLista, RolesDeRecaudo.Credito, movimiento.Amount, caja, pagador, movimiento.Number),
            ],
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseCustodia, codigoDelTipo, movimiento.Number, fecha, oficina, pagador, ct);
    }

    /// <summary>
    /// <c>CustodiaLiberada</c> (CLB): débito <c>CustodiaPorLista</c>; crédito <c>Caja</c> (la operación sigue: sale de la custodia y entra con el recibo) o
    /// <c>PuenteDeDevolucion</c> (se devuelve por orden de pago).
    /// </summary>
    public async Task<IntegrationMessage> CustodiaLiberadaAsync(Guid liberacion, string numero, CashSession sesion, CashOperation operacion, decimal valor, bool devolucion,
        Guid? orden, Guid soporte, DateOnly fecha, string codigoDelTipo, int? medioDelEfectivo, CancellationToken ct)
    {
        var caja = await DimensionesDeCajaAsync(sesion.CashBoxId, medioDelEfectivo, ct);
        var oficina = await OficinaDeLaCajaAsync(sesion.CashBoxId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == operacion.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var contenido = new CustodiaLiberadaV1
        {
            ReleasePublicId = liberacion, Number = numero, OperationPublicId = operacion.PublicId, Destination = devolucion ? "RefundByOrder" : "Continue",
            PaymentOrderPublicId = orden, PayerPersonPublicId = pagador, Amount = valor, OperationDate = fecha, SupportAttachmentPublicId = soporte,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.CustodiaPorLista, RolesDeRecaudo.Debito, valor, caja, pagador, numero),
                devolucion
                    ? Linea(RolesDeRecaudo.PuenteDeDevolucion, RolesDeRecaudo.Credito, valor, caja, pagador, numero)
                    : Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Credito, valor, caja, cruce: numero),
            ],
        };
        return await EmitirAsync(contenido, liberacion, ClaseCustodia, codigoDelTipo, numero, fecha, oficina, pagador, ct);
    }
}
