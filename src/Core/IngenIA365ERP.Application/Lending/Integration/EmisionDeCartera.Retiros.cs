using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>El retiro aprobado tal como se emite a Asociados (mensajes.md §5.1). (nuevo)</summary>
public sealed record RetiroAprobadoAEmitir(
    MembershipWithdrawal Retiro,
    Guid PersonaPublicId,
    Guid SucursalPublicId,
    MembershipWithdrawalApprovalV1 Aprobacion,
    UserRefV1 AprobadoPor,
    DateOnly Fecha);

/// <summary>El débito de una cuenta que paga un crédito en el cruce al retiro, tal como se emite (mensajes.md §4.1, <c>CruceAlRetiroAplicado</c>). (nuevo)</summary>
public sealed record CruceAlRetiroAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    MembershipWithdrawal Retiro,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    IReadOnlyList<WithdrawalOffsetV1> Cruces);

/// <summary>El descuento por pérdidas al retiro, tal como se emite (mensajes.md §4.1, <c>PerdidaAplicadaAlRetiro</c>). (nuevo)</summary>
public sealed record PerdidaAlRetiroAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    WithdrawalLossV1 Perdida);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen del retiro del asociado al aprobarse (mensajes.md §2).</summary>
    public const string ClaseRetiroDeAsociado = "MembershipWithdrawal";

    /// <summary>La clase del origen de la liquidación del retiro: el cruce, la pérdida y la devolución (mensajes.md §2).</summary>
    public const string ClaseLiquidacionDeRetiro = "MembershipWithdrawalSettlement";

    /// <summary>La clase del origen de una orden de giro de cuentas (<c>LND_AccountPayoutOrders</c>).</summary>
    public const string ClaseOrdenDeGiroDeCuenta = "AccountPayoutOrder";

    /// <summary>
    /// <c>RetiroDeAsociadoAprobado</c> v1 (US10, T098; mensajes.md §5.1): origen el trámite (<c>MembershipWithdrawal</c>), clave <c>Confirmation</c>,
    /// tercero el asociado; nace siempre (<c>Always</c>). Lo origina la persona que aprueba. Sin guardar.
    /// </summary>
    public async Task<Guid> RetiroAprobadoAsync(RetiroAprobadoAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var contenido = new RetiroDeAsociadoAprobadoV1
        {
            WithdrawalPublicId = r.Retiro.PublicId,
            WithdrawalNumber = r.Retiro.Number,
            PersonPublicId = r.PersonaPublicId,
            Cause = r.Retiro.Cause.ToString(),
            CauseDetail = r.Retiro.CauseDetail,
            EffectiveDate = r.Retiro.EffectiveDate ?? r.Fecha,
            Approval = r.Aprobacion,
            ApprovedBy = r.AprobadoPor,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, r.Retiro.PublicId, ClaseRetiroDeAsociado, Prefijo(r.Retiro.Number), r.Retiro.Number, r.Fecha,
                r.SucursalPublicId, PersonPublicId: r.PersonaPublicId),
            ClavesDeEvento.Confirmacion, [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always), UsuarioDeOrigen: r.AprobadoPor), ct);
        return mensajes[0].PublicId;
    }

    /// <summary>
    /// <c>CruceAlRetiroAplicado</c> (US10, T100; mensajes.md §4.1): el débito de una cuenta que paga un crédito, con el trámite y el crédito cruzado.
    /// Origen la operación de cuenta (<c>MembershipWithdrawalSettlement</c>), tercero el titular; el <c>PagoAplicado</c> del crédito sale aparte con su
    /// propia operación (<c>OwnLoanPayoff</c>). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> CruceAlRetiroAsync(CruceAlRetiroAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var contenido = new CruceAlRetiroAplicadoV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = c.Operacion.ValueDate,
            RegisteredAt = Instante(c.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = c.Valores,
            OperationNumber = c.Operacion.Number,
            RulesVersion = c.Operacion.RulesVersion,
            Withdrawal = new MembershipWithdrawalRefV1 { PublicId = c.Retiro.PublicId, Number = c.Retiro.Number, Cause = c.Retiro.Cause.ToString() },
            Offsets = c.Cruces,
        };
        return await EmitirDeCuentaAsync(contenido, CruceAlRetiroAplicadoV1.Type, h, c.Operacion, ClaseLiquidacionDeRetiro, h.TitularPublicId,
            PrevalidationOutcome.NotApplicable, ct);
    }

    /// <summary>
    /// <c>PerdidaAplicadaAlRetiro</c> (US10, T099; mensajes.md §4.1; FR-078): el descuento proporcional de las pérdidas sobre los aportes, con el año, la
    /// proporción y el punto del acta. Va en su propia operación de cuenta (research E68). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> PerdidaAlRetiroAsync(PerdidaAlRetiroAEmitir p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var h = p.Hecho;
        var contenido = new PerdidaAplicadaAlRetiroV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = p.Operacion.ValueDate,
            RegisteredAt = Instante(p.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = p.Valores,
            OperationNumber = p.Operacion.Number,
            RulesVersion = p.Operacion.RulesVersion,
            Loss = p.Perdida,
        };
        return await EmitirDeCuentaAsync(contenido, PerdidaAplicadaAlRetiroV1.Type, h, p.Operacion, ClaseLiquidacionDeRetiro, h.TitularPublicId,
            PrevalidationOutcome.NotApplicable, ct);
    }

    /// <summary>
    /// <c>DevolucionGirada</c> (US10, T101; mensajes.md §4.1, §5.3): el consumo de un <c>ResultadoDelGiro</c> <c>Disbursed</c> de una orden de cuentas;
    /// informativo, sin importes. Origen la orden, clave <c>Confirmation:{orden:N}</c>, siempre (<c>Always</c>). Sin guardar.
    /// </summary>
    public async Task<Guid> DevolucionGiradaAsync(AccountPayoutOrder orden, string tipoDeOrden, string referenciaDeTesoreria, DateOnly ocurrida,
        Guid sucursalPublicId, Guid beneficiarioPublicId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var contenido = new DevolucionGiradaV1
        {
            OrderPublicId = orden.PublicId,
            OrderKind = tipoDeOrden,
            Outcome = nameof(DisbursementOrderStatus.Disbursed),
            TreasuryReference = referenciaDeTesoreria,
            OccurredOn = ocurrida,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, orden.PublicId, ClaseOrdenDeGiroDeCuenta, Prefijo(orden.Number), orden.Number, ocurrida,
                sucursalPublicId, PersonPublicId: beneficiarioPublicId),
            ClavesDeEvento.ConfirmacionPor(orden.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return mensajes[0].PublicId;
    }

    /// <summary>
    /// El tipo de orden de una orden de cuentas (mensajes.md §5.3, <c>OrderKind</c>): la devolución del retiro es <c>MembershipRefund</c>; la devolución
    /// sin retiro, <c>ContributionRefund</c>; las demás, su propósito.
    /// </summary>
    public static string TipoDeOrden(AccountPayoutOrder orden)
    {
        ArgumentNullException.ThrowIfNull(orden);
        if (orden.MembershipWithdrawalId is not null) return "MembershipRefund";
        return orden.Purpose switch
        {
            PayoutPurpose.Withdrawal => "DepositWithdrawal",
            PayoutPurpose.Cancellation => "DepositWithdrawal",
            _ => orden.Purpose.ToString(),
        };
    }
}
