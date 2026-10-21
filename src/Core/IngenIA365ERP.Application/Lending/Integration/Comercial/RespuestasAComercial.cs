using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Lo que dice una <c>ObligacionCreada</c> (mensajes.md §11.1). (nuevo)</summary>
public sealed record ObligacionCreadaAEmitir(
    LendingInboundMessage Recibo,
    MensajeEntrante Mensaje,
    Guid CreditPaymentPublicId,
    Loan Credito,
    string CodigoDeLinea,
    decimal Monto,
    DateOnly FechaDeOrigen,
    string Estado,
    SaleValidationV1? Validacion,
    LoanPlanSummaryV1? Plan,
    string FuenteDelNumero,
    Guid? ReservaPublicId,
    LoanRefV1? Reemplaza,
    Guid SucursalPublicId);

/// <summary>Lo que dice una <c>ObligacionRechazada</c> (mensajes.md §11.2). (nuevo)</summary>
public sealed record ObligacionRechazadaAEmitir(
    LendingInboundMessage Recibo,
    MensajeEntrante Mensaje,
    Guid CreditPaymentPublicId,
    Guid EvaluacionPublicId,
    string Motivo,
    string Codigo,
    string Razon,
    Guid SucursalPublicId);

/// <summary>
/// Las respuestas a Comercial (feature 013, US6, T0593; mensajes.md §11.1, §11.2; data-model §21.3; T11, T18, C17, C95): las emite el consumo de
/// Cartera por la bandeja, en el mismo guardado que su efecto, con destino <c>Inventory</c> y entrega <c>Always</c>:
/// <list type="bullet">
/// <item><c>ObligacionCreada</c>: origen el recibo (<c>LND_InboundMessages</c>, clase <c>InboundMessage</c>, número = el del documento de Comercial),
/// clave <c>Confirmation</c>;</item>
/// <item><c>ObligacionRechazada</c>: origen el recibo, clave <c>Confirmation:{evaluationPublicId:N}</c> (una por evaluación: un reproceso que vuelve a
/// fallar emite otra con el motivo vigente), siempre no reprocesable.</item>
/// </list>
/// Las dos con la venta como raíz de cadena (<c>CadenaDeOrden</c>, para que lleguen en orden), el comprador de tercero, <c>AdmiteProceso</c> y el
/// usuario de Comercial del mensaje consumido como usuario de origen (T11). Comercial guarda número y estado, nunca saldos ni cuotas. (nuevo)
/// </summary>
public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de una respuesta a Comercial: el recibo del mensaje.</summary>
    public const string ClaseRecibo = "InboundMessage";

    /// <summary>El tipo de documento del recibo en el sobre (<c>origin.documentTypeCode</c>): los recibos de Cartera no tienen consecutivo propio.</summary>
    public const string TipoDeRecibo = "LIM";

    public const string MotivoNoConvertible = "NoConvertible";
    public const string MotivoAceptadaSinMigrar = "AceptadaSinMigrar";
    public const string MotivoAceptadaFueraDeCartera = "AceptadaFueraDeCartera";
    public const string MotivoSinObligacion = "SinObligacion";

    public const string FuenteReserva = "Reservation";
    public const string FuenteAsignado = "Assigned";
    public const string FuenteMigracion = "Migration";

    /// <summary><c>ObligacionCreada</c> v1 (§11.1). Sin guardar.</summary>
    public async Task<IntegrationMessage> ObligacionCreadaAsync(ObligacionCreadaAEmitir o, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(o);
        var contenido = new ObligacionCreadaV1
        {
            SaleMessage = Referencia(o.Mensaje),
            Invoice = Factura(o.Mensaje),
            CreditPaymentPublicId = o.CreditPaymentPublicId,
            Loan = new LoanRefV1 { LoanPublicId = o.Credito.PublicId, LoanNumber = o.Credito.Number },
            CreditLineCode = o.CodigoDeLinea,
            Amount = o.Monto,
            OriginDate = o.FechaDeOrigen,
            Status = o.Estado,
            Validation = o.Validacion,
            Plan = o.Plan,
            NumberSource = o.FuenteDelNumero,
            ReservationPublicId = o.ReservaPublicId,
            ReplacesLoan = o.Reemplaza,
        };
        var mensajes = await emisor.EmitirAsync(Solicitud(o.Recibo, o.Mensaje, o.SucursalPublicId, ClavesDeEvento.Confirmacion, contenido), ct);
        return mensajes[0];
    }

    /// <summary><c>ObligacionRechazada</c> v1 (§11.2), siempre <c>retryable = false</c> (C95, C102). Sin guardar.</summary>
    public async Task<IntegrationMessage> ObligacionRechazadaAsync(ObligacionRechazadaAEmitir o, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(o);
        var contenido = new ObligacionRechazadaV1
        {
            SaleMessage = Referencia(o.Mensaje),
            Invoice = Factura(o.Mensaje),
            CreditPaymentPublicId = o.CreditPaymentPublicId,
            EvaluationPublicId = o.EvaluacionPublicId,
            Motive = o.Motivo,
            Code = o.Codigo,
            Reason = o.Razon,
            Retryable = false,
        };
        var mensajes = await emisor.EmitirAsync(Solicitud(o.Recibo, o.Mensaje, o.SucursalPublicId, ClavesDeEvento.ConfirmacionPor(o.EvaluacionPublicId),
            contenido), ct);
        return mensajes[0];
    }

    /// <summary>La referencia al mensaje de Comercial al que se responde.</summary>
    public static SaleMessageRefV1 Referencia(MensajeEntrante m) => new() { MessageId = m.MessageId, Type = m.Type, Version = m.Version };

    /// <summary>La venta como documento externo (FR-053): el origen del sobre del mensaje de Comercial.</summary>
    public static ExternalDocumentRefV1 Factura(MensajeEntrante m) => new()
    {
        Module = m.OriginModule,
        PublicId = m.Envelope.Origin.PublicId,
        DocumentClass = m.OriginDocumentClassText ?? m.Envelope.Origin.DocumentClass?.ToString() ?? string.Empty,
        Number = m.Envelope.Origin.Number,
    };

    /// <summary>El documento relacionado de un mensaje de Comercial (el original que un ajuste afecta) como documento externo.</summary>
    public static ExternalDocumentRefV1? Relacionado(MensajeEntrante m) => m.Envelope.Related is { } r
        ? new ExternalDocumentRefV1
        {
            Module = m.OriginModule,
            PublicId = r.PublicId,
            DocumentClass = m.RelatedDocumentClassText ?? r.DocumentClass.ToString(),
            Number = r.Number,
        }
        : null;

    private static SolicitudDeEmision Solicitud(LendingInboundMessage recibo, MensajeEntrante m, Guid sucursal, string clave, object contenido)
    {
        var origen = m.Envelope.OriginUser;
        return new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, recibo.PublicId, ClaseRecibo, TipoDeRecibo, m.Envelope.Origin.Number, m.Envelope.Origin.OperationDate,
                sucursal, PersonPublicId: m.Envelope.PersonPublicId),
            clave, [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            UsuarioDeOrigen: origen.CentralUserId is null ? null : new UserRefV1 { CentralUserId = origen.CentralUserId, Name = origen.Name },
            CadenaDeOrden: recibo.ChainRootPublicId);
    }
}
