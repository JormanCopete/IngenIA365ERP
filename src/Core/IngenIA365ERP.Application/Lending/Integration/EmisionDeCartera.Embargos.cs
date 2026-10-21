using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La reclasificación de los saldos de una cuenta cuando cambia su condición (feature 014, US11, T189; research C12, C20, E109, E159): el embargo y su
/// levantamiento, la exención del GMF y —cuando la construya US14— la inactividad. Los saldos que se trasladan, las dimensiones de las que salen y la causa.
/// (nuevo)
/// </summary>
public sealed record ReclasificacionAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    DepositFactDimensionsV1 DimensionesAnteriores,
    IReadOnlyList<DepositConceptAmountV1> Saldos,
    string Causa);

/// <summary>Una consignación al juzgado tal como se emite (mensajes.md §4.1 <c>EmbargoConsignado</c> y §5.3 <c>OrdenDeGiroEmitida</c> v2). (nuevo)</summary>
public sealed record ConsignacionDeEmbargoAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    Garnishment Embargo,
    Guid JuzgadoPublicId,
    AccountPayoutOrder Orden,
    PayeeV1 Beneficiario,
    PrevalidationOutcome ValidacionPrevia);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de la reclasificación de una cuenta (operación <c>Reclassification</c>).</summary>
    public const string ClaseReclasificacionDeCuenta = "AccountReclassification";

    /// <summary>La clase del origen de la consignación de un embargo (operación <c>GarnishmentConsignment</c>).</summary>
    public const string ClaseConsignacionDeEmbargo = "GarnishmentConsignment";

    /// <summary>Las causas de <c>CaptacionReclasificada</c> (mensajes.md §4.1).</summary>
    public const string CausaEmbargo = "Garnishment";
    public const string CausaDesembargo = "GarnishmentRelease";
    public const string CausaExencionDeGmf = "GmfExemption";
    public const string CausaFinDeExencionDeGmf = "GmfExemptionEnd";
    public const string CausaInactividad = "Inactivity";
    public const string CausaReactivacion = "Reactivation";

    /// <summary>
    /// US11 (T189): <c>CaptacionReclasificada</c> con los saldos de la cuenta de las dimensiones anteriores a las vigentes. Origen la operación
    /// <c>Reclassification</c> (valor 0, sin filas en el libro: los saldos no cambian, cambia su marca). Admite proceso (el recálculo del ciclo diario). Sin
    /// guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> CaptacionReclasificadaAsync(ReclasificacionAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var contenido = new CaptacionReclasificadaV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = r.Operacion.ValueDate, RegisteredAt = Instante(r.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = r.Saldos, OperationNumber = r.Operacion.Number, RulesVersion = r.Operacion.RulesVersion, Cause = r.Causa,
            PreviousDimensions = r.DimensionesAnteriores,
        };
        return await EmitirDeCuentaAsync(contenido, CaptacionReclasificadaV1.Type, h, r.Operacion, ClaseReclasificacionDeCuenta, h.TitularPublicId,
            PrevalidationOutcome.NotApplicable, ct);
    }

    /// <summary>
    /// US11 (T189): <c>EmbargoConsignado</c> (origen la operación, tercero el juzgado) y <c>OrdenDeGiroEmitida</c> v2 (<c>GarnishmentConsignment</c>, origen la
    /// orden) a Tesorería. Lo origina una persona. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> EmbargoConsignadoAsync(ConsignacionDeEmbargoAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var contenido = new EmbargoConsignadoV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = c.Operacion.ValueDate, RegisteredAt = Instante(c.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(), Amounts = c.Valores, OperationNumber = c.Operacion.Number, RulesVersion = c.Operacion.RulesVersion,
            Garnishment = new GarnishmentRefV1
            {
                PublicId = c.Embargo.PublicId, OfficeReference = c.Embargo.OrderNumber, CourtPersonPublicId = c.JuzgadoPublicId, IsAlimony = c.Embargo.IsChildSupport,
            },
            OrderPublicId = c.Orden.PublicId,
        };
        var unidad = await EmitirDeCuentaAsync(contenido, EmbargoConsignadoV1.Type, h, c.Operacion, ClaseConsignacionDeEmbargo, c.JuzgadoPublicId,
            c.ValidacionPrevia, ct);
        if (unidad.IsFailure) return unidad;

        var orden = new OrdenDeGiroEmitidaV2
        {
            OrderPublicId = c.Orden.PublicId,
            OrderNumber = c.Orden.Number,
            OrderKind = nameof(Domain.Enums.Lending.PayoutPurpose.GarnishmentConsignment),
            Account = h.Referencia,
            SourceDocumentPublicId = c.Operacion.PublicId,
            Form = "Transfer",
            Payee = c.Beneficiario,
            GrossAmount = c.Orden.Amount,
            NetAmount = c.Orden.Amount,
            PayDate = h.FechaContable,
            Concept = $"Embargo {c.Embargo.OrderNumber} (proceso {c.Embargo.ProcessNumber})",
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, c.Orden.PublicId, ClaseOrdenDeGiro, Prefijo(c.Orden.Number), c.Orden.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: c.JuzgadoPublicId),
            ClavesDeEvento.Confirmacion, [orden], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return Result.Success(new UnidadEmitida([.. unidad.Value.Mensajes, .. mensajes]));
    }
}
