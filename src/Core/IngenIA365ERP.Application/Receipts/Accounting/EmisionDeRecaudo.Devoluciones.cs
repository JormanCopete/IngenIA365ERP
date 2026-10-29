using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;

namespace IngenIA365ERP.Application.Receipts.Accounting;

/// <summary>
/// Feature 015, US8 (fase 16; contabilidad.md §3): <c>DevolucionNoGirada</c> (<c>DNG</c>). Tesorería informó rechazada, anulada o con cheque no cobrado una orden que
/// Recaudo pidió para devolver dinero: débito <c>PuenteDeDevolucion</c> (que el hecho de la devolución había acreditado) y crédito al rol de donde salió el dinero,
/// que vuelve a quedar ahí. Lleva en <c>Related</c> el documento del hecho de la devolución, así Contabilidad no lo contabiliza antes que el original. Sin guardar.
/// </summary>
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseDevolucionNoGirada = "RefundNotDisbursed";
    public const string TipoDevolucionNoGirada = "DNG";

    /// <summary>A dónde vuelve el dinero (<see cref="DevolucionNoGiradaV1.ReturnsTo"/>).</summary>
    public const string VuelveAlSaldo = "PayerCreditBalance";
    public const string VuelveALaPartida = "UnidentifiedItem";
    public const string VuelveALaCustodia = "ComplianceCustody";

    /// <param name="origen">El documento del rechazo (el movimiento o la resolución que lo registró en Recaudo).</param>
    /// <param name="rolDeOrigen">El rol que recibe el dinero de vuelta (<c>SaldoAFavorDePagadores</c>, <c>SaldoDePagadurias</c>, <c>PartidasPorIdentificar</c>, <c>CustodiaPorLista</c>).</param>
    public async Task<IntegrationMessage> DevolucionNoGiradaAsync(Guid origen, string numero, Guid ordenPublicId, string numeroDeOrden, string resultado, string vuelveA,
        string rolDeOrigen, Guid? beneficiario, decimal valor, DateOnly fecha, string? motivo, int branchId, Guid? cuentaPropia, DocumentoRelacionado original,
        CancellationToken ct)
    {
        var dim = cuentaPropia is not null
            ? await DimensionesDeBancoAsync(branchId, cuentaPropia, null, "Bank", ct)
            : await DimensionesDeOficinaAsync(branchId, ct);
        var contenido = new DevolucionNoGiradaV1
        {
            PaymentOrderPublicId = ordenPublicId, PaymentOrderNumber = numeroDeOrden, Outcome = resultado, ReturnsTo = vuelveA, BeneficiaryPersonPublicId = beneficiario,
            Amount = valor, OccurredOn = fecha, Reason = motivo,
            Lines =
            [
                Linea(RolesDeRecaudo.PuenteDeDevolucion, RolesDeRecaudo.Debito, valor, dim with { MiscConceptCode = null }, beneficiario, numeroDeOrden),
                Linea(rolDeOrigen, RolesDeRecaudo.Credito, valor, dim, beneficiario, numero),
            ],
        };
        return await EmitirAsync(contenido, origen, ClaseDevolucionNoGirada, TipoDevolucionNoGirada, numero, fecha, branchId, beneficiario, ct, original);
    }
}
