using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;

namespace IngenIA365ERP.Domain.Entities.Lending.Payoffs;

/// <summary>
/// <c>LND_DisbursementPayoffs</c> (feature 028, R2, R9; data-model): un producto que recogió un desembolso. Lo proyectado en la solicitud (copia de la
/// fila de <see cref="CreditApplicationPayoff"/>, cero si no había), lo que se liquidó a la fecha valor, lo que aplicó de verdad el contrato o el
/// registro, la diferencia con su causa y su detalle por concepto, quién la reconoció, la clave con que se aplicó (<c>CAR:{op:N}:{producto:N}</c>) y
/// la operación resultante. Un producto que ya estaba cancelado queda <see cref="Omitted"/>, sin aplicar. Al anular el crédito nuevo antes del giro se
/// marca reversado con su clave de reverso. 2026-11-27 (feature 028, E2, T063, T066; HU4): con «todo menos la cuota causada» guarda la cuota que se dejó a
/// su canal (la pagaduría, la nómina o la caja: número y valor) y, cuando su canal la pague, el instante (<see cref="LeftInstallmentSettledAt"/>). (nuevo)
/// </summary>
public class DisbursementPayoff : AuditableEntity
{
    /// <summary>El largo de la clave del contrato (el de <c>LND_LoanOperations.IdempotencyKey</c>).</summary>
    public const int LargoDeLaClave = 150;

    /// <summary>La clave de la aplicación más el sufijo del reverso.</summary>
    public const int LargoDeLaClaveDeReverso = 160;

    public const int LargoDeQuienReconoce = 100;

    /// <summary>La operación <c>LoanDisbursement</c> del crédito nuevo (<c>LND_LoanOperations</c>).</summary>
    public long DisbursementOperationId { get; init; }

    /// <summary>La recogida proyectada de la que viene; nula si la solicitud no la tenía.</summary>
    public int? CreditApplicationPayoffId { get; init; }

    public PayoffProductKind ProductKind { get; init; }

    public int? LoanId { get; init; }

    public int? AccountId { get; init; }

    public string? CommercialReceivableReference { get; init; }

    public string ProductNumber { get; init; } = string.Empty;

    public PayoffMode Mode { get; init; }

    /// <summary>Sólo créditos: el tratamiento de la cuota causada que se aplicó.</summary>
    public InstallmentTreatment? InstallmentTreatment { get; init; }

    /// <summary>Sólo créditos: el modo de intereses que se aplicó (<c>Corrido</c> o <c>CuotaCompleta</c>).</summary>
    public string? InterestMode { get; init; }

    /// <summary>Copia de lo proyectado en la solicitud (cero si no había proyección).</summary>
    public decimal ProjectedAmount { get; init; }

    /// <summary>Lo que se liquidó a la fecha valor del desembolso.</summary>
    public decimal CurrentAmount { get; init; }

    /// <summary>Lo que aplicó de verdad el contrato de obligaciones o el registro de cuentas; cero hasta aplicarse y en un omitido.</summary>
    public decimal AppliedAmount { get; private set; }

    /// <summary><see cref="CurrentAmount"/> − <see cref="ProjectedAmount"/>.</summary>
    public decimal Difference { get; init; }

    public PayoffDifferenceCause DifferenceCause { get; init; }

    /// <summary>JSON de <see cref="DesgloseDeRecogida"/>: la diferencia por concepto (ahora − proyectado).</summary>
    public string DifferenceDetail { get; init; } = "{}";

    /// <summary>El producto ya estaba cancelado al desembolsar: no se recogió.</summary>
    public bool Omitted { get; init; }

    /// <summary>La clave con que se aplicó: <c>CAR:{operación:N}:{producto:N}</c>.</summary>
    public string IdempotencyKey { get; init; } = string.Empty;

    /// <summary>Créditos: la operación de pago del crédito recogido (<c>LND_LoanOperations</c>).</summary>
    public long? PaymentOperationId { get; private set; }

    /// <summary>Cuentas (entrega E3): la operación de la cuenta (<c>LND_MemberAccountOperations</c>).</summary>
    public long? AccountOperationId { get; private set; }

    /// <summary>Facturas (entrega E4): el recibo de Recaudo, como dato.</summary>
    public Guid? ReceiptPublicId { get; private set; }

    public DateTime? ReversedAt { get; private set; }

    /// <summary>La clave del reverso: <c>{clave}:reverso</c>.</summary>
    public string? ReversalKey { get; private set; }

    /// <summary>Quién reconoció la diferencia u omisión al confirmar; nulo si no había nada que reconocer.</summary>
    public string? AcknowledgedBy { get; init; }

    public DateTime? AcknowledgedAt { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T063): la cuota causada que se dejó a su canal («todo menos la cuota causada»); nula si se recogió todo. (nuevo)</summary>
    public int? LeftInstallmentNumber { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T063): lo que la cuota dejada debe al vencer (lo que su canal paga para cerrarla al peso). (nuevo)</summary>
    public decimal? LeftInstallmentAmount { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T063): cuando la cuota dejada quedó pagada; nula mientras se deba. (nuevo)</summary>
    public DateTime? LeftInstallmentSettledAt { get; private set; }

    public bool EstaReversada => ReversedAt is not null;

    /// <summary>2026-11-27 (feature 028, E2): ¿se dejó la cuota causada a su canal y todavía se debe? (nuevo)</summary>
    public bool CuotaDejadaPendiente => LeftInstallmentNumber is not null && LeftInstallmentSettledAt is null && !EstaReversada;

    /// <summary>
    /// La recogida evaluada al desembolsar, antes de aplicarse: lo proyectado, lo de ahora y la diferencia con su causa. Quien reconoció va con el
    /// instante, o los dos nulos. 2026-11-27 (feature 028, E2): con la cuota causada excluida, la cuota que se deja y su valor (los dos o ninguno; sólo en un
    /// crédito que se recoge excluyéndola).
    /// </summary>
    public static DisbursementPayoff Evaluar(long operacionDeDesembolsoId, int? proyectadaId, ProductoRecogido producto, PayoffMode modo,
        InstallmentTreatment? tratamientoDeCuota, string? modoDeIntereses, decimal proyectado, decimal actual, ResultadoDeDiferencia diferencia,
        bool omitido, string clave, string? reconocidoPor, DateTime? reconocidoEn, int? cuotaDejada = null, decimal? valorDejado = null)
    {
        ArgumentNullException.ThrowIfNull(producto);
        ArgumentNullException.ThrowIfNull(diferencia);
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        if (operacionDeDesembolsoId <= 0) throw new ArgumentOutOfRangeException(nameof(operacionDeDesembolsoId));
        if (!Enum.IsDefined(modo)) throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de recogida desconocido.");
        if (clave.Length > LargoDeLaClave) throw new ArgumentOutOfRangeException(nameof(clave), $"La clave pasa de {LargoDeLaClave} caracteres.");
        if (modoDeIntereses is { Length: > CreditApplicationPayoff.LargoDelModoDeIntereses })
            throw new ArgumentOutOfRangeException(nameof(modoDeIntereses));
        if ((reconocidoPor is null) != (reconocidoEn is null))
            throw new ArgumentException("Quién reconoció la diferencia va con el instante en que la reconoció.", nameof(reconocidoPor));
        if (reconocidoPor is { Length: > LargoDeQuienReconoce }) throw new ArgumentOutOfRangeException(nameof(reconocidoPor));
        if (producto.Clase != PayoffProductKind.Loan && tratamientoDeCuota is not null)
            throw new ArgumentException("El tratamiento de la cuota causada sólo aplica a la recogida de un crédito.", nameof(tratamientoDeCuota));
        if ((cuotaDejada is null) != (valorDejado is null))
            throw new ArgumentException("La cuota dejada va con su valor, o ninguno de los dos.", nameof(cuotaDejada));
        if (cuotaDejada is not null && (tratamientoDeCuota != Enums.Lending.InstallmentTreatment.Exclude || omitido))
            throw new ArgumentException("Sólo se deja la cuota causada de un crédito que se recoge excluyéndola.", nameof(cuotaDejada));
        if (cuotaDejada is <= 0) throw new ArgumentOutOfRangeException(nameof(cuotaDejada));
        if (valorDejado is < 0m) throw new ArgumentOutOfRangeException(nameof(valorDejado), "El valor de la cuota dejada no es negativo.");

        return new DisbursementPayoff
        {
            DisbursementOperationId = operacionDeDesembolsoId,
            CreditApplicationPayoffId = proyectadaId,
            ProductKind = producto.Clase,
            LoanId = producto.LoanId,
            AccountId = producto.AccountId,
            CommercialReceivableReference = producto.CommercialReceivableReference,
            ProductNumber = producto.Numero,
            Mode = modo,
            InstallmentTreatment = tratamientoDeCuota,
            InterestMode = modoDeIntereses,
            ProjectedAmount = proyectado,
            CurrentAmount = omitido ? 0m : actual,
            Difference = (omitido ? 0m : actual) - proyectado,
            DifferenceCause = diferencia.Causa,
            DifferenceDetail = diferencia.PorConcepto.Serializar(),
            Omitted = omitido,
            IdempotencyKey = clave,
            AcknowledgedBy = reconocidoPor,
            AcknowledgedAt = reconocidoEn,
            LeftInstallmentNumber = cuotaDejada,
            LeftInstallmentAmount = valorDejado,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2; data-model «Transiciones»): la cuota dejada quedó pagada por su canal. Una vez: repetirlo no cambia el instante. Sin cuota
    /// dejada es un error.
    /// </summary>
    public void MarcarCuotaDejadaPagada(DateTime instante)
    {
        if (LeftInstallmentNumber is null) throw Problema("Lending.Payoff.NoLeftInstallment", "Esta recogida no dejó ninguna cuota a su canal.");
        LeftInstallmentSettledAt ??= instante;
    }

    /// <summary>
    /// Lo que aplicó el contrato o el registro y la operación que resultó: la de pago del crédito, la de la cuenta o el recibo, según la clase. Un
    /// omitido no se aplica.
    /// </summary>
    public void Aplicar(decimal aplicado, long? operacionDePagoId = null, long? operacionDeCuentaId = null, Guid? recibo = null)
    {
        if (Omitted) throw Problema("Lending.Payoff.Omitted", "El producto ya estaba cancelado: no se recoge.");
        if (EstaReversada) throw Problema("Lending.Payoff.AlreadyReversed", "La recogida ya se reversó.");
        if (aplicado <= 0m) throw new ArgumentOutOfRangeException(nameof(aplicado), "Lo aplicado es mayor que cero.");
        var referencias = (operacionDePagoId is null ? 0 : 1) + (operacionDeCuentaId is null ? 0 : 1) + (recibo is null ? 0 : 1);
        if (referencias > 1) throw new ArgumentException("Una recogida resulta en una sola operación: la del crédito, la de la cuenta o el recibo.");
        if (operacionDePagoId is not null && ProductKind != PayoffProductKind.Loan
            || operacionDeCuentaId is not null && ProductKind != PayoffProductKind.Account
            || recibo is not null && ProductKind != PayoffProductKind.CommercialReceivable)
            throw new ArgumentException("La operación no corresponde a la clase del producto recogido.");
        AppliedAmount = aplicado;
        PaymentOperationId = operacionDePagoId;
        AccountOperationId = operacionDeCuentaId;
        ReceiptPublicId = recibo;
    }

    /// <summary>
    /// Al anular el crédito nuevo antes del giro: la recogida queda reversada con la clave de su reverso. Repetir con la misma clave no hace nada
    /// (la anulación se puede reintentar); con otra, es un error.
    /// </summary>
    public void Reversar(DateTime instante, string claveDeReverso)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claveDeReverso);
        if (claveDeReverso.Length > LargoDeLaClaveDeReverso)
            throw new ArgumentOutOfRangeException(nameof(claveDeReverso), $"La clave del reverso pasa de {LargoDeLaClaveDeReverso} caracteres.");
        if (EstaReversada)
        {
            if (string.Equals(ReversalKey, claveDeReverso, StringComparison.Ordinal)) return;
            throw Problema("Lending.Payoff.AlreadyReversed", "La recogida ya se reversó con otra clave.");
        }
        ReversedAt = instante;
        ReversalKey = claveDeReverso;
    }

    private static ReglaDeCarteraException Problema(string codigo, string mensaje) => new(new ProblemaDeCartera(codigo, mensaje));
}
