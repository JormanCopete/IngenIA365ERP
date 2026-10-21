using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Un ajuste de Comercial tal como se emite (mensajes.md §10.3, §10.12). (nuevo)</summary>
public sealed record AjusteComercialAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Ajuste,
    LoanOperation Desembolso,
    string ClaseDeAjuste,
    Guid MensajeDelAjuste,
    ExternalDocumentRefV1 DocumentoDelAjuste,
    ExternalDocumentRefV1 Factura,
    string CuentaPorCobrarRegistradaPor,
    IReadOnlyList<MovimientoDeLibro> Filas,
    IReadOnlyList<DiferenciaDeLibro> AjustesDeCausacion,
    bool Anulacion,
    LoanPlanSummaryV1? PlanNuevo,
    bool ValidacionFallida,
    UserRefV1? UsuarioDeOrigen,
    IReadOnlyList<ReallocationV1>? Reasignaciones = null);

/// <summary>Un cobro provisional incorporado tal como se emite (mensajes.md §10.16; C62). (nuevo)</summary>
public sealed record CobroProvisionalAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    IReadOnlyList<MovimientoDeLibro> Filas,
    ExternalDocumentRefV1 Factura,
    string CuentaPorCobrarRegistradaPor,
    string ReferenciaContable,
    Guid ActivacionPublicId,
    int FilaDeLaPlantilla,
    UserRefV1? UsuarioDeOrigen);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un ajuste de Comercial (mensajes.md §3).</summary>
    public const string ClaseAjusteComercial = "CommercialAdjustment";

    /// <summary>La clase del origen de un cobro provisional incorporado.</summary>
    public const string ClaseCobroProvisional = "ProvisionalCollection";

    /// <summary>
    /// US6 (T0590; mensajes.md §10.1; FR-050, FR-056): el <c>CreditoDesembolsado</c> de un desembolso <b>en especie</b> que nace de una venta: origen la
    /// operación (<c>LoanDisbursement</c>), clave <c>Confirmation</c>, modo del tipo, tercero el deudor, sin orden de giro (neto cero) y con el bloque
    /// <c>inKind</c> (la factura, el pago, quién registró la cuenta por cobrar y, en un reemplazo, el crédito anulado). Lo origina el consumo con el
    /// usuario de Comercial como usuario de origen (T11). Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> DesembolsoEnEspecieAsync(HechoDeCredito h, LoanOperation op, decimal bruto, LoanPlanSummaryV1 plan,
        InKindDisbursementV1 enEspecie, PrevalidationOutcome validacionPrevia, UserRefV1? usuarioDeOrigen, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(op);
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CreditoDesembolsadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = Dimensiones(h, categoria),
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = bruto, ReasonCode = enEspecie.AccountsReceivableRecordedBy }],
            RulesVersion = h.Condiciones.RulesVersion,
            Tranche = new TrancheV1 { Number = 1, IsLast = true },
            DisbursementForm = nameof(DisbursementForm.InKind),
            GrossAmount = bruto,
            NetToDisburse = 0m,
            InKind = enEspecie,
            Plan = plan,
        };
        var modo = await modoDePaso.ResolverAsync(CreditoDesembolsadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseDesembolso, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: validacionPrevia, UsuarioDeOrigen: usuarioDeOrigen), ct);
        return Result.Success(mensajes[0]);
    }

    /// <summary>
    /// US6 (T0591; mensajes.md §10.3, §10.12; FR-054): <c>CreditoAjustadoDesdeComercial</c> (origen el ajuste, <c>CommercialAdjustment</c>, clave
    /// <c>Confirmation</c>, <c>related</c> = el desembolso en especie, valores con signo) y, si deja saldo a favor, su acompañante
    /// <c>SaldoAFavorReconocido</c> (<c>cause = CommercialAdjustment</c>); y el ajuste de causación por fecha valor si la reconstrucción deja diferencias
    /// en los días ya causados. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> AjusteComercialAsync(AjusteComercialAEmitir a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var h = a.Hecho;
        var op = a.Ajuste;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var dimensiones = Dimensiones(h, categoria);
        var registrado = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc));
        var original = await HechoOriginalAsync(a.Desembolso.PublicId, CreditoDesembolsadoV1.Type, a.Desembolso.Number, ct);
        var deuda = a.Filas.Where(f => f.Clase == LoanTransactionKind.CommercialAdjustment).ToList();
        var saldo = a.Filas.Where(f => f.Clase == LoanTransactionKind.CreditBalanceRecognized).ToList();
        var principal = new CreditoAjustadoDesdeComercialV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = registrado,
            Dimensions = dimensiones,
            Amounts = Montos(deuda, f => f.Monto),
            Original = original,
            RulesVersion = h.Condiciones.RulesVersion,
            AdjustmentClass = a.ClaseDeAjuste,
            AdjustmentMessageId = a.MensajeDelAjuste,
            AdjustmentDocument = a.DocumentoDelAjuste,
            Invoice = a.Factura,
            AccountsReceivableRecordedBy = a.CuentaPorCobrarRegistradaPor,
            CreditBalanceRecognized = saldo.Sum(f => f.Monto),
            VoidedFromOrigin = a.Anulacion,
            NewPlan = a.PlanNuevo,
            ValidationFailed = a.ValidacionFallida,
            // T1098 (decisión del dueño del 2026-10-18): los pagos posteriores que el ajuste redistribuye, como en PagoAplicado.
            Reallocations = a.Reasignaciones ?? [],
        };
        List<object> contenidos = [principal];
        if (saldo.Count > 0)
            contenidos.Add(new SaldoAFavorReconocidoV1
            {
                Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = op.ValueDate, RegisteredAt = registrado, Dimensions = dimensiones,
                Amounts = Montos(saldo, f => f.Monto), RulesVersion = h.Condiciones.RulesVersion, Cause = ClaseAjusteComercial,
            });
        if (a.AjustesDeCausacion.Count > 0)
            contenidos.Add(Ajuste(h, dimensiones, op, ClaseAjusteComercial, a.AjustesDeCausacion, original));

        var modo = await modoDePaso.ResolverAsync(CreditoAjustadoDesdeComercialV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseAjusteComercial, Prefijo(op.Number), op.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value,
            Relacionado: new DocumentoRelacionado(a.Desembolso.PublicId, ClaseDesembolso, a.Desembolso.Number),
            UsuarioDeOrigen: a.UsuarioDeOrigen), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>
    /// US6 (T0590; mensajes.md §10.16; C62): <c>CobroProvisionalIncorporado</c>: origen la operación <c>ProvisionalCollection</c>, clave
    /// <c>Confirmation</c>, tercero el deudor, con la distribución del cobro por concepto, la factura, la referencia contable que redujo la cuenta por
    /// cobrar provisional, la activación y la fila de la plantilla. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CobroProvisionalAsync(CobroProvisionalAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var op = c.Operacion;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CobroProvisionalIncorporadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = Dimensiones(h, categoria),
            Amounts = Montos(c.Filas.Where(f => f.Clase is LoanTransactionKind.PaymentAllocation or LoanTransactionKind.AdvanceReceived
                or LoanTransactionKind.CreditBalanceRecognized), f => Math.Abs(f.Monto)),
            RulesVersion = h.Condiciones.RulesVersion,
            Invoice = c.Factura,
            AccountsReceivableRecordedBy = c.CuentaPorCobrarRegistradaPor,
            AccountingReference = c.ReferenciaContable,
            ActivationPublicId = c.ActivacionPublicId,
            TemplateRow = c.FilaDeLaPlantilla,
        };
        var modo = await modoDePaso.ResolverAsync(CobroProvisionalIncorporadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseCobroProvisional, Prefijo(op.Number), op.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, UsuarioDeOrigen: c.UsuarioDeOrigen), ct);
        return Result.Success(mensajes[0]);
    }
}
