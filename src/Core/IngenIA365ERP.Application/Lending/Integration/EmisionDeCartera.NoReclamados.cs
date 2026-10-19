using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El traslado de un saldo a no reclamados tal como se emite (feature 014, US14, T209; mensajes.md §4.1 <c>SaldoTrasladadoANoReclamados</c>): los valores
/// por concepto —lo que sale del capital o de los aportes y lo que entra al concepto <c>Unclaimed</c>—, la fuente y el saldo no reclamado. (nuevo)
/// </summary>
public sealed record TrasladoANoReclamadosAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    string Origen,
    Guid NoReclamadoPublicId);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen del traslado a no reclamados (operación <c>ToUnclaimed</c>).</summary>
    public const string ClaseTrasladoANoReclamados = "UnclaimedTransfer";

    /// <summary>La clase del origen del pago de un saldo no reclamado (operación <c>UnclaimedPayment</c>).</summary>
    public const string ClasePagoDeNoReclamados = "UnclaimedPayment";

    /// <summary>La clase del origen de la entrega por fallecimiento (operación <c>DeceasedDelivery</c>).</summary>
    public const string ClaseEntregaPorFallecimiento = "DeceasedDelivery";

    /// <summary>
    /// US14 (T209): <c>SaldoTrasladadoANoReclamados</c> (origen la operación <c>ToUnclaimed</c>, tercero el titular). Los valores cuadran solos —lo que sale
    /// del capital (o de los aportes) entra a <c>Unclaimed</c>, cuyo rol es <c>NoReclamadosPorPagar</c>—. Admite proceso (el ciclo diario). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> SaldoTrasladadoANoReclamadosAsync(TrasladoANoReclamadosAEmitir t, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(t);
        var h = t.Hecho;
        var contenido = new SaldoTrasladadoANoReclamadosV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = t.Operacion.ValueDate, RegisteredAt = Instante(t.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = t.Valores, OperationNumber = t.Operacion.Number, RulesVersion = t.Operacion.RulesVersion, Origin = t.Origen,
            UnclaimedPublicId = t.NoReclamadoPublicId,
        };
        return await EmitirDeCuentaAsync(contenido, SaldoTrasladadoANoReclamadosV1.Type, h, t.Operacion, ClaseTrasladoANoReclamados, h.TitularPublicId,
            PrevalidationOutcome.NotApplicable, ct);
    }
}
