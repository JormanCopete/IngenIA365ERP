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
    DateOnly Fecha,
    string? CodigoDeMotivo = null);

/// <summary>El débito de una cuenta en el cruce contra un crédito: su hecho, su operación y lo que sale por concepto (D45). (nuevo)</summary>
public sealed record ParteDelCruceAEmitir(HechoDeCuenta Hecho, MemberAccountOperation Operacion, IReadOnlyList<DepositConceptAmountV1> Valores);

/// <summary>
/// El cruce al retiro contra <b>un</b> crédito, tal como se emite (mensajes.md §4.1, <c>CruceAlRetiroAplicado</c>; decisión del dueño del 2026-10-18,
/// D45): la operación de pago del crédito (el origen de la unidad del <c>PagoAplicado</c>), el retiro, el cruce y la parte de cada cuenta. (nuevo)
/// </summary>
public sealed record CruceAlRetiroAEmitir(
    Guid PagoPublicId,
    string PagoNumero,
    MembershipWithdrawal Retiro,
    IReadOnlyList<ParteDelCruceAEmitir> Partes,
    WithdrawalOffsetV1 Cruce);

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
            WithdrawalReasonCode = r.CodigoDeMotivo,
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
    /// <c>CruceAlRetiroAplicado</c> (US10, T100; mensajes.md §4.1): el débito de las cuentas que pagan <b>un</b> crédito, con el trámite y el crédito cruzado.
    /// Decisión del dueño del 2026-10-18 (D45): el cruce y el pago van en el mismo comprobante, así que el origen es la <b>operación de pago del crédito</b>
    /// (la del <c>PagoAplicado</c> que el contrato de obligaciones ya emitió, <c>OwnLoanPayoff</c>) con su misma clave <c>Confirmation</c> y el modo que
    /// rige al pago a esa fecha: los dos hechos forman una sola unidad (el emisor no hace depender un mensaje de su propia unidad) y el cruce es el
    /// principal. Tercero el titular. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> CruceAlRetiroAsync(CruceAlRetiroAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (c.Partes.Count == 0) throw new ArgumentException("El cruce trae al menos una cuenta.", nameof(c));
        var primera = c.Partes[0];
        var h = primera.Hecho;
        var contenido = new CruceAlRetiroAplicadoV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = primera.Operacion.ValueDate,
            RegisteredAt = Instante(primera.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = primera.Valores,
            OperationNumber = primera.Operacion.Number,
            RulesVersion = primera.Operacion.RulesVersion,
            Withdrawal = new MembershipWithdrawalRefV1 { PublicId = c.Retiro.PublicId, Number = c.Retiro.Number, Cause = c.Retiro.Cause.ToString() },
            Offsets = [c.Cruce],
            Parts = c.Partes.Select(p => new WithdrawalAccountPartV1
            {
                Account = p.Hecho.Referencia, Dimensions = p.Hecho.Dimensiones(), Amounts = p.Valores, OperationNumber = p.Operacion.Number,
            }).ToList(),
        };
        var modo = await modoDePaso.ResolverAsync(IngenIA365ERP.Application.Common.Integration.Contracts.Lending.PagoAplicadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, c.PagoPublicId, ClasePago, Prefijo(c.PagoNumero), c.PagoNumero, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: PrevalidationOutcome.NotApplicable), ct);
        return Result.Success(new UnidadEmitida(mensajes));
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
