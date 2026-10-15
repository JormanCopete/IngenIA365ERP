using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Lo que el sobre y las dimensiones de un hecho de un crédito necesitan (T13, T55): el crédito, sus condiciones vigentes, el código de su línea,
/// el deudor (y si es empleado), la sucursal contable de su oficina y la fecha contable ya calculada. (nuevo)
/// </summary>
public sealed record HechoDeCredito(
    Loan Credito,
    LoanTermsVersion Condiciones,
    string CodigoDeLinea,
    Guid DeudorPublicId,
    bool DeudorEsEmpleado,
    Guid SucursalPublicId,
    DateOnly FechaContable)
{
    public LoanRefV1 Referencia => new() { LoanPublicId = Credito.PublicId, LoanNumber = Credito.Number };

    /// <summary>K2 (T0843; FR-116, C44): el sub-segmento que rige tras un traslado de libranza o el paso a la forma alterna; nulo = el de las condiciones.</summary>
    public string? Subsegmento { get; init; }
}

/// <summary>Un tramo desembolsado tal como se emite (mensajes.md §10.1, §11.3). (nuevo)</summary>
public sealed record DesembolsoAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    int Tramo,
    bool EsElUltimo,
    DisbursementForm Forma,
    PayeeV1 Beneficiario,
    decimal Bruto,
    IReadOnlyList<PayoffDeductionV1> Recogidas,
    DisbursementOrder? Orden,
    LoanPlanSummaryV1 Plan,
    PrevalidationOutcome ValidacionPrevia,
    Guid? OrdenAnteriorPublicId = null,
    string? NumeroDeOrdenAnterior = null);

/// <summary>El reverso de un desembolso tal como se emite (mensajes.md §10.2, §10.5). (nuevo)</summary>
public sealed record ReversoAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Reverso,
    LoanOperation Desembolso,
    string Causa,
    Guid? MensajeDelResultado,
    string? Motivo,
    IReadOnlyList<ConceptAmountV1> Valores,
    LoanStatus EstadoDespues,
    DisbursementForm Forma,
    Guid? BeneficiarioPublicId,
    IReadOnlyList<ConceptAmountV1> AjusteDeCausacion,
    IReadOnlyList<OriginalFactRefV1> ReversosDeRecogidas,
    UserRefV1? UsuarioDeOrigen);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un tramo (mensajes.md §3).</summary>
    public const string ClaseDesembolso = "LoanDisbursement";

    /// <summary>La clase del origen del reverso de un tramo.</summary>
    public const string ClaseReversoDeDesembolso = "DisbursementReversal";

    /// <summary>La clase del origen de una orden de giro (interfaces.md §4.1).</summary>
    public const string ClaseOrdenDeGiro = "DisbursementOrder";

    /// <summary>
    /// La sucursal contable del sobre (T13): la fila de <c>COR_Branches</c> de la oficina del crédito. Una oficina sin ella no emite: la
    /// operación se rechaza antes de confirmar (<c>Lending.Branch.AccountingBranchMissing</c>, C9).
    /// </summary>
    public async Task<Result<Guid>> SucursalContableAsync(int oficinaId, CancellationToken ct)
    {
        var sucursal = await db.Branches.AsNoTracking().Where(b => b.Id == oficinaId && !b.IsDeleted).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        return sucursal is { } id
            ? Result.Success(id)
            : Result.Failure<Guid>(new Error(Disbursements.ErroresDeDesembolso.CodigoSinSucursalContable,
                "La oficina del crédito no tiene sucursal contable vinculada. Vincúlela en Agencias antes de desembolsar."));
    }

    /// <summary>Las dimensiones vigentes del hecho (T55); la categoría la pone <see cref="CategoriaContableDeCartera"/> (crédito nuevo hasta US9).</summary>
    public static LoanFactDimensionsV1 Dimensiones(HechoDeCredito h, CategoriaContableVigente categoria) => new()
    {
        CreditLineCode = h.CodigoDeLinea,
        ModalityCode = h.Condiciones.ModalityCode,
        SubsegmentCode = h.Subsegmento ?? h.Condiciones.SubsegmentCode,
        RiskCategory = categoria.Category,
        RiskSubcategory = "None",
        IsEmployee = h.DeudorEsEmpleado,
        InGracePeriod = false,
        Restructured = h.Condiciones.RestructuringCount > 0,
        WrittenOff = h.Credito.Status == LoanStatus.WrittenOff,
        CollectorClass = null,
        BranchPublicId = h.SucursalPublicId,
        ImpairmentModel = "ArrearsAnnex1",
        RiskSegment = h.Condiciones.ModalityCode,
        ValueUnit = "Pesos",
    };

    /// <summary>
    /// Un tramo (T0439; mensajes.md §10.1, §11.3): <c>CreditoDesembolsado</c> (origen la operación, clase <c>LoanDisbursement</c>, clave
    /// <c>Confirmation</c>, modo del tipo, tercero el deudor) y, si no es en especie, <c>OrdenDeGiroEmitida</c> (origen la orden, <c>Treasury</c>,
    /// <c>Always</c>, tercero el beneficiario, <c>related</c> = la orden anterior en un giro nuevo). En el mismo guardado; sin guardar.
    /// </summary>
    public async Task<Result<IReadOnlyList<IntegrationMessage>>> DesembolsoAsync(DesembolsoAEmitir d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(d);
        var h = d.Hecho;
        var op = d.Operacion;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var neto = d.Bruto - d.Recogidas.Sum(r => r.Amount);
        var contenido = new CreditoDesembolsadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = new DateTimeOffset(op.RegisteredAt, TimeSpan.Zero),
            Dimensions = Dimensiones(h, categoria),
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = d.Bruto, ReasonCode = d.Forma.ToString() }],
            RulesVersion = h.Condiciones.RulesVersion,
            Tranche = new TrancheV1 { Number = d.Tramo, IsLast = d.EsElUltimo },
            DisbursementForm = d.Forma.ToString(),
            PayeePersonPublicId = d.Orden is null ? null : d.Beneficiario.PersonPublicId,
            GrossAmount = d.Bruto,
            PayoffDeductions = d.Recogidas,
            NetToDisburse = d.Orden is null ? 0m : neto,
            DisbursementOrderPublicId = d.Orden?.PublicId,
            Plan = d.Plan,
        };
        var modo = await modoDePaso.ResolverAsync(CreditoDesembolsadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IReadOnlyList<IntegrationMessage>>(modo.Error);

        var emitidos = new List<IntegrationMessage>();
        emitidos.AddRange(await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseDesembolso, Prefijo(op.Number), op.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: d.ValidacionPrevia), ct));

        if (d.Orden is { } orden)
        {
            var giro = new OrdenDeGiroEmitidaV1
            {
                OrderPublicId = orden.PublicId,
                OrderNumber = orden.Number,
                Loan = h.Referencia,
                TrancheNumber = d.Tramo,
                Form = orden.Form.ToString(),
                Payee = d.Beneficiario,
                GrossAmount = orden.GrossAmount,
                PayoffDeductions = d.Recogidas,
                NetAmount = orden.NetAmount,
                PayDate = orden.ScheduledDate,
                Concept = h.Credito.Number,
                PaidByFile = orden.PaidByFile,
                ReplacesOrderPublicId = d.OrdenAnteriorPublicId,
            };
            emitidos.AddRange(await emisor.EmitirAsync(new SolicitudDeEmision(
                new OrigenDeEmision(MessageOriginKind.Document, orden.PublicId, ClaseOrdenDeGiro, Prefijo(orden.Number), orden.Number, h.FechaContable,
                    h.SucursalPublicId, PersonPublicId: d.Beneficiario.PersonPublicId),
                ClavesDeEvento.Confirmacion, [giro], new ModoDeEntrega.Sellado(DeliveryMode.Always),
                Relacionado: d.OrdenAnteriorPublicId is { } anterior ? new DocumentoRelacionado(anterior, ClaseOrdenDeGiro, d.NumeroDeOrdenAnterior ?? string.Empty) : null),
                ct));
        }
        return Result.Success<IReadOnlyList<IntegrationMessage>>(emitidos);
    }

    /// <summary>
    /// <c>AnulacionDeOrdenDeGiro</c> (mensajes.md §11.4, C12): origen la misma orden, clave <c>Confirmation:{orden:N}</c>, <c>related</c> = la
    /// orden como documento, tercero el beneficiario, <c>Always</c>. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> AnulacionDeOrdenAsync(DisbursementOrder orden, Guid beneficiarioPublicId, Guid sucursalPublicId, DateOnly fechaContable,
        string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        var contenido = new AnulacionDeOrdenDeGiroV1 { OrderPublicId = orden.PublicId, OrderNumber = orden.Number, Reason = motivo };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, orden.PublicId, ClaseOrdenDeGiro, Prefijo(orden.Number), orden.Number, fechaContable,
                sucursalPublicId, PersonPublicId: beneficiarioPublicId),
            ClavesDeEvento.ConfirmacionPor(orden.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(orden.PublicId, ClaseOrdenDeGiro, orden.Number)), ct);
        return mensajes[0];
    }

    /// <summary>
    /// <c>DesembolsoReversado</c> (mensajes.md §10.2; C08): origen el reverso (<c>DisbursementReversal</c>), clave <c>Confirmation</c>,
    /// <c>related</c> = el desembolso, <b>hereda el modo</b> del <c>CreditoDesembolsado</c> y, si el crédito tiene otros tramos y hay diferencia
    /// de causación, su acompañante <c>AjusteDeCausacionPorFechaValor</c> en la misma unidad (C49). Sin guardar.
    /// </summary>
    public async Task<IReadOnlyList<IntegrationMessage>> DesembolsoReversadoAsync(ReversoAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var original = await HechoOriginalAsync(r.Desembolso.PublicId, CreditoDesembolsadoV1.Type, r.Desembolso.Number, ct);
        var contenido = new DesembolsoReversadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = r.Desembolso.ValueDate,
            RegisteredAt = new DateTimeOffset(r.Reverso.RegisteredAt, TimeSpan.Zero),
            Dimensions = Dimensiones(h, categoria),
            Amounts = r.Valores,
            Original = original,
            RulesVersion = h.Condiciones.RulesVersion,
            Cause = r.Causa,
            TreasuryResultMessageId = r.MensajeDelResultado,
            Reason = r.Motivo,
            DisbursementForm = r.Forma.ToString(),
            PayeePersonPublicId = r.BeneficiarioPublicId,
            LoanStatusAfter = r.EstadoDespues.ToString(),
            PayoffReversals = r.ReversosDeRecogidas,
        };
        List<object> contenidos = [contenido];
        if (r.AjusteDeCausacion.Count > 0)
        {
            var linea = new LoanFactLineV1
            {
                Loan = h.Referencia,
                DebtorPersonPublicId = h.DeudorPublicId,
                Dimensions = contenido.Dimensions,
                Amounts = r.AjusteDeCausacion,
                ValueDate = r.Desembolso.ValueDate,
            };
            contenidos.Add(new AjusteDeCausacionPorFechaValorV1
            {
                FactDate = r.Desembolso.ValueDate,
                BatchNumber = 1,
                PartNumber = 1,
                CatchUpOperation = new CatchUpOperationV1 { OperationPublicId = r.Reverso.PublicId, DocumentClass = ClaseReversoDeDesembolso, Number = r.Reverso.Number },
                Cause = "ValueDatedOperation",
                CauseFact = original,
                Totals = [new LoanFactTotalV1 { Dimensions = contenido.Dimensions, Amounts = r.AjusteDeCausacion, LoanCount = 1 }],
                Lines = [linea],
                LineCount = 1,
                ControlTotal = r.AjusteDeCausacion.Sum(a => a.Amount),
            });
        }

        return await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, r.Reverso.PublicId, ClaseReversoDeDesembolso, Prefijo(r.Reverso.Number), r.Reverso.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, contenidos, new ModoDeEntrega.Heredado(r.Desembolso.PublicId),
            Relacionado: new DocumentoRelacionado(r.Desembolso.PublicId, ClaseDesembolso, r.Desembolso.Number),
            UsuarioDeOrigen: r.UsuarioDeOrigen), ct);
    }

    /// <summary>
    /// Con A3 (C15, C91; mensajes.md §10.24): la baja del contingente «aprobado sin desembolsar» que mueve el tramo, aparte de su unidad principal
    /// (clave <c>Confirmation:{tipoDeHecho:N}</c> con el <c>PublicId</c> sembrado del tipo en <c>LND_FactTypes</c>; <c>NoPasa</c> por defecto).
    /// Sin la fila del tipo (A3 rechazado o sin sembrar) no emite. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage?> BajaDeAprobadoSinDesembolsarAsync(HechoDeCredito h, LoanOperation desembolso, Guid solicitudPublicId,
        string numeroDeSolicitud, decimal tramo, decimal saldoDespues, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        var tipo = await db.LendingFactTypes.AsNoTracking()
            .Where(f => f.Code == CreditoAprobadoSinDesembolsarV1.Type && !f.IsDeleted)
            .Select(f => (Guid?)f.PublicId).FirstOrDefaultAsync(ct);
        if (tipo is not { } tipoDeHecho) return null;

        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CreditoAprobadoSinDesembolsarV1
        {
            DebtorPersonPublicId = h.DeudorPublicId,
            Loan = h.Referencia,
            Subject = new ContingentSubjectV1 { Kind = "CreditApplication", PublicId = solicitudPublicId, Number = numeroDeSolicitud },
            ValueDate = desembolso.ValueDate,
            RegisteredAt = new DateTimeOffset(desembolso.RegisteredAt, TimeSpan.Zero),
            Dimensions = Dimensiones(h, categoria),
            CreditLineCode = h.CodigoDeLinea,
            Movement = "Disbursed",
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = -tramo, ReasonCode = "ApprovedNotDisbursed" }],
            BalanceAfter = saldoDespues,
        };
        var modo = await modoDePaso.ResolverAsync(CreditoAprobadoSinDesembolsarV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return null;
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, desembolso.PublicId, ClaseDesembolso, Prefijo(desembolso.Number), desembolso.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.ConfirmacionPor(tipoDeHecho), [contenido], modo.Value), ct);
        return mensajes[0];
    }

    /// <summary>La referencia al hecho principal de una operación, para <c>original</c> (FR-126): el mensaje ya emitido de ese origen y tipo.</summary>
    public async Task<OriginalFactRefV1?> HechoOriginalAsync(Guid origen, string tipo, string numero, CancellationToken ct)
    {
        var id = db.IntegrationMessages.Local.Where(m => m.OriginPublicId == origen && m.Type == tipo).Select(m => (Guid?)m.PublicId).FirstOrDefault()
            ?? await db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == origen && m.Type == tipo)
                .OrderBy(m => m.Id).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
        return id is { } messageId ? new OriginalFactRefV1 { MessageId = messageId, Type = tipo, OriginPublicId = origen, Number = numero } : null;
    }

    /// <summary>El prefijo del numerador de la clase (<c>origin.documentTypeCode</c>): lo que va antes de los dígitos del número.</summary>
    public static string? Prefijo(string numero)
    {
        if (string.IsNullOrEmpty(numero)) return null;
        var i = numero.Length;
        while (i > 0 && char.IsDigit(numero[i - 1])) i--;
        return i == 0 ? null : numero[..i];
    }
}
