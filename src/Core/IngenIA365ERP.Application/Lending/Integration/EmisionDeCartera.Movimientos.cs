using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El GMF de una disposición tal como se emite como acompañante (mensajes.md §3, §4.1, <c>GmfRetenido</c>): el cálculo del motor, la cuenta marcada
/// exenta del titular si la hay y la clase del producto que generó el hecho. (nuevo)
/// </summary>
public sealed record GmfAEmitir(GmfCalculado Calculo, Guid? CuentaExentaPublicId, string? ClaseGeneradora)
{
    public GmfDetailV1 Detalle => new()
    {
        Base = Calculo.Base,
        Policy = Calculo.Politica.ToString(),
        ExemptAccountPublicId = CuentaExentaPublicId,
        ExemptUsedBefore = Calculo.ExentoUsadoAntes,
        ExemptApplied = Calculo.ExentoAplicado,
        TaxedBase = Calculo.BaseGravada,
        Rate = Calculo.Tarifa,
        GeneratingEvent = Calculo.HechoGenerador,
        GeneratingDepositClass = ClaseGeneradora,
    };
}

/// <summary>
/// Un retiro de una cuenta de captación tal como se emite (feature 014, US4, T112; mensajes.md §2, §4.1, <c>RetiroDeCuentaAplicado</c>): por el contrato
/// (con el recaudador), por giro (con su orden de giro v2) o por débito automático (con el crédito que paga); con su GMF como acompañante. (nuevo)
/// </summary>
public sealed record RetiroAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    string ClaseDeMedio,
    Guid Tercero,
    GmfAEmitir? Gmf,
    PrevalidationOutcome ValidacionPrevia)
{
    public CollectorRefV1? Recaudador { get; init; }

    public InternalWithdrawalV1? Interno { get; init; }

    public bool CierraCuenta { get; init; }

    /// <summary>La orden de giro de un retiro por giro (su <c>OrdenDeGiroEmitida</c> v2 sale con origen la orden).</summary>
    public AccountPayoutOrder? Orden { get; init; }

    public PayeeV1? Beneficiario { get; init; }
}

/// <summary>Un cargo de una cuenta tal como se emite (mensajes.md §4.1 <c>CargoDeCuentaCausado</c> y §5.5 <c>CargoDeCaptacionCausado</c>). (nuevo)</summary>
public sealed record CargoAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    AccountCharge Cargo,
    string Nombre,
    string? CodigoComercial,
    string? Evento,
    IReadOnlyList<DepositConceptAmountV1> Valores);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un retiro de cuenta (por el contrato, por giro o por débito automático; mensajes.md §2).</summary>
    public const string ClaseRetiroDeCuenta = "AccountWithdrawal";

    /// <summary>La clase del origen de un cargo de cuenta (mensajes.md §2).</summary>
    public const string ClaseCargoDeCuenta = "AccountCharge";

    /// <summary>Quién inició el reverso de una operación de cuenta: un usuario de la Cartera (desde la pantalla).</summary>
    public const string ReversoIniciadoPorLaCartera = "Lending";

    /// <summary>El acompañante <c>GmfRetenido</c> de una disposición (débito al capital del titular).</summary>
    private static GmfRetenidoV1 GmfRetenido(HechoDeCuenta h, MemberAccountOperation op, GmfAEmitir g) => new()
    {
        Account = h.Referencia,
        HolderPersonPublicId = h.TitularPublicId,
        ValueDate = op.ValueDate,
        RegisteredAt = Instante(op.RegisteredAt),
        Dimensions = h.Dimensiones(),
        Amounts = [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Gmf), Amount = g.Calculo.Valor }],
        OperationNumber = op.Number,
        RulesVersion = op.RulesVersion,
        Gmf = g.Detalle,
    };

    /// <summary>
    /// <c>RetiroDeCuentaAplicado</c> con su <c>GmfRetenido</c> (si hubo GMF) en una unidad (mensajes.md §2.1) y, si es por giro, la
    /// <c>OrdenDeGiroEmitida</c> v2 con origen la orden. Origen la operación (<c>AccountWithdrawal</c>), clave <c>Confirmation</c>, tercero quien recibe.
    /// Admite proceso (el débito automático). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> RetiroDeCuentaAsync(RetiroAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var principal = new RetiroDeCuentaAplicadoV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = r.Operacion.ValueDate,
            RegisteredAt = Instante(r.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = r.Valores,
            OperationNumber = r.Operacion.Number,
            RulesVersion = r.Operacion.RulesVersion,
            Collector = r.Recaudador,
            DisbursementOrderPublicId = r.Orden?.PublicId,
            Internal = r.Interno,
            MeansClass = r.ClaseDeMedio,
            ClosesAccount = r.CierraCuenta,
        };
        List<object> contenidos = [principal];
        if (r.Gmf is { Calculo.Valor: > 0m } gmf) contenidos.Add(GmfRetenido(h, r.Operacion, gmf));
        var unidad = await EmitirUnidadDeCuentaAsync(contenidos, RetiroDeCuentaAplicadoV1.Type, h, r.Operacion, ClaseRetiroDeCuenta, r.Tercero, r.ValidacionPrevia, ct);
        if (unidad.IsFailure || r.Orden is null || r.Beneficiario is null) return unidad;

        var orden = new OrdenDeGiroEmitidaV2
        {
            OrderPublicId = r.Orden.PublicId,
            OrderNumber = r.Orden.Number,
            OrderKind = TipoDeOrden(r.Orden),
            Account = h.Referencia,
            SourceDocumentPublicId = r.Operacion.PublicId,
            Form = "Transfer",
            Payee = r.Beneficiario,
            GrossAmount = r.Orden.Amount,
            NetAmount = r.Orden.Amount,
            PayDate = h.FechaContable,
            Concept = $"Retiro de la cuenta {h.Cuenta.Number}",
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, r.Orden.PublicId, ClaseOrdenDeGiro, Prefijo(r.Orden.Number), r.Orden.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: r.Beneficiario.PersonPublicId),
            ClavesDeEvento.Confirmacion, [orden], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return Result.Success(new UnidadEmitida([.. unidad.Value.Mensajes, .. mensajes]));
    }

    /// <summary>
    /// US4 (T111): un traslado entre cuentas con su <c>GmfRetenido</c> (si hubo) en una unidad. Origen la operación (<c>AccountTransfer</c>), tercero el
    /// titular de origen. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> TrasladoConGmfAsync(TrasladoAEmitir t, GmfAEmitir? gmf, PrevalidationOutcome validacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(t);
        var contenido = new TrasladoEntreCuentasAplicadoV1
        {
            Account = t.Origen.Referencia,
            HolderPersonPublicId = t.Origen.TitularPublicId,
            ValueDate = t.Operacion.ValueDate,
            RegisteredAt = Instante(t.Operacion.RegisteredAt),
            Dimensions = t.Origen.Dimensiones(),
            Amounts = t.ValoresDelOrigen,
            OperationNumber = t.Operacion.Number,
            RulesVersion = t.Operacion.RulesVersion,
            Target = t.Destino.Referencia,
            TargetHolderPersonPublicId = t.Destino.TitularPublicId,
            TargetDimensions = t.Destino.Dimensiones(),
            TargetAmounts = t.ValoresDelDestino,
        };
        List<object> contenidos = [contenido];
        if (gmf is { Calculo.Valor: > 0m }) contenidos.Add(GmfRetenido(t.Origen, t.Operacion, gmf));
        return await EmitirUnidadDeCuentaAsync(contenidos, TrasladoEntreCuentasAplicadoV1.Type, t.Origen, t.Operacion, ClaseTrasladoDeCuenta,
            t.Origen.TitularPublicId, validacion, ct);
    }

    /// <summary>US4 (T116): el acompañante <c>CargoPendienteCobrado</c> de una consignación que cobró cargos pendientes (mensajes.md §2.1).</summary>
    public static CargoPendienteCobradoV1 CargoPendienteCobrado(HechoDeCuenta h, MemberAccountOperation op, IReadOnlyList<(AccountCharge Cargo, decimal Monto)> cobrados) =>
        new()
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = Instante(op.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts =
            [
                new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.ChargeReceivable), Amount = -cobrados.Sum(c => c.Monto) },
                new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -cobrados.Sum(c => c.Monto) },
            ],
            OperationNumber = op.Number,
            RulesVersion = op.RulesVersion,
            Charges = cobrados.Select(c => new ChargeCollectedV1 { ChargePublicId = c.Cargo.PublicId, Amount = c.Monto }).ToList(),
        };

    /// <summary>
    /// US4 (T116): <c>CargoDeCuentaCausado</c> (a Contabilidad) y, si está gravado con IVA, <c>CargoDeCaptacionCausado</c> (a Comercial, que lo factura)
    /// con origen la operación del cargo (clase <c>AccountCharge</c>), clave <c>Confirmation</c>, tercero el titular. Admite proceso (cargos periódicos).
    /// Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> CargoDeCuentaAsync(CargoAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var cargo = new AccountChargeRefV1
        {
            ChargePublicId = c.Cargo.PublicId, Kind = c.Cargo.Kind.ToString(), Code = c.Cargo.ChargeCode, Event = c.Evento, TaxedWithVat = c.Cargo.IsVatTaxed,
        };
        List<object> contenidos =
        [
            new CargoDeCuentaCausadoV1
            {
                Account = h.Referencia,
                HolderPersonPublicId = h.TitularPublicId,
                ValueDate = c.Operacion.ValueDate,
                RegisteredAt = Instante(c.Operacion.RegisteredAt),
                Dimensions = h.Dimensiones(),
                Amounts = c.Valores,
                OperationNumber = c.Operacion.Number,
                RulesVersion = c.Operacion.RulesVersion,
                Charge = cargo,
                Status = c.Cargo.Status.ToString(),
            },
        ];
        if (c.Cargo.IsVatTaxed)
            contenidos.Add(new CargoDeCaptacionCausadoV1
            {
                Charge = RefDeFactura(c.Cargo, c.Nombre, c.CodigoComercial, c.Evento),
                Account = h.Referencia,
                HolderPersonPublicId = h.TitularPublicId,
                AmountBeforeTax = c.Cargo.Amount,
                TaxConceptCode = c.CodigoComercial ?? string.Empty,
                Status = c.Cargo.Status.ToString(),
                CausedOn = c.Operacion.ValueDate,
            });
        var modo = await modoDePaso.ResolverAsync(CargoDeCuentaCausadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, c.Operacion.PublicId, ClaseCargoDeCuenta, Prefijo(c.Operacion.Number), c.Operacion.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>
    /// US4 (T116): <c>CargoDeCuentaCondonado</c> con origen la operación de la condonación (clase <c>AccountCharge</c>) y clave <c>Confirmation</c>; lo
    /// origina la persona que condona. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> CargoCondonadoAsync(HechoDeCuenta h, MemberAccountOperation op, AccountCharge cargo, AccountChargeStatus previo,
        IReadOnlyList<DepositConceptAmountV1> valores, CancellationToken ct)
    {
        var contenido = new CargoDeCuentaCondonadoV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = Instante(op.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = valores,
            OperationNumber = op.Number,
            RulesVersion = op.RulesVersion,
            Charge = new AccountChargeRefV1 { ChargePublicId = cargo.PublicId, Kind = cargo.Kind.ToString(), Code = cargo.ChargeCode, TaxedWithVat = cargo.IsVatTaxed },
            PreviousStatus = previo.ToString(),
        };
        var modo = await modoDePaso.ResolverAsync(CargoDeCuentaCondonadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseCargoDeCuenta, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>
    /// US4 (T116): <c>CargoDeCaptacionReversado</c> a Comercial (nota crédito) con origen el reverso, clave <c>Confirmation:{reverso:N}</c> y
    /// <c>original</c> = el <c>CargoDeCaptacionCausado</c> de la operación del cargo. Sin guardar.
    /// </summary>
    public async Task<Guid?> CargoReversadoAComercialAsync(HechoDeCuenta h, MemberAccountOperation reverso, MemberAccountOperation operacionDelCargo, AccountCharge cargo,
        string nombre, string? codigoComercial, string motivo, CancellationToken ct)
    {
        if (!cargo.IsVatTaxed) return null;
        var original = await HechoOriginalAsync(operacionDelCargo.PublicId, CargoDeCaptacionCausadoV1.Type, operacionDelCargo.Number, ct);
        var contenido = new CargoDeCaptacionReversadoV1
        {
            Charge = RefDeFactura(cargo, nombre, codigoComercial, null),
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            AmountBeforeTax = cargo.Amount,
            TaxConceptCode = codigoComercial ?? string.Empty,
            Status = cargo.Status.ToString(),
            CausedOn = reverso.ValueDate,
            Original = original ?? new OriginalFactRefV1
            {
                Type = CargoDeCaptacionCausadoV1.Type, OriginPublicId = operacionDelCargo.PublicId, Number = operacionDelCargo.Number,
            },
            Reason = motivo,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, reverso.PublicId, ClaseCargoDeCuenta, Prefijo(reverso.Number), reverso.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.ConfirmacionPor(reverso.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return mensajes.FirstOrDefault()?.PublicId;
    }

    /// <summary>
    /// US4 (T112; mensajes.md §5.2; FR-092): <c>ConsignacionReversadaPorCartera</c> al destino del recaudador (Recaudo y el doble de prueba →
    /// <c>Receipts</c>; Tesorería → <c>Treasury</c>), con origen el reverso, <c>related</c> = la consignación y tercero el depositante. Lo de Nómina nunca
    /// (lo reversa Nómina). Sin guardar.
    /// </summary>
    public async Task<Guid?> ConsignacionReversadaPorCarteraAsync(HechoDeCuenta h, MemberAccountOperation reverso, MemberAccountOperation consignacion,
        CollectorRefV1 recaudador, Guid depositante, string motivo, UserRefV1 usuario, CancellationToken ct)
    {
        var destino = recaudador.CollectorClass switch
        {
            nameof(ClaseDeRecaudador.Treasury) => IntegrationDestinations.Treasury,
            nameof(ClaseDeRecaudador.Receipts) or nameof(ClaseDeRecaudador.Test) => IntegrationDestinations.Receipts,
            _ => null,
        };
        if (destino is null) return null;
        var tipo = h.Cuenta.Nature == LineNature.Contribution ? AporteRecibidoV1.Type : ConsignacionEnCuentaAplicadaV1.Type;
        var original = await HechoOriginalAsync(consignacion.PublicId, tipo, consignacion.Number, ct)
                       ?? new OriginalFactRefV1 { Type = tipo, OriginPublicId = consignacion.PublicId, Number = consignacion.Number };
        var contenido = new ConsignacionReversadaPorCarteraV1
        {
            Account = h.Referencia,
            Deposit = original,
            ReversalOperationPublicId = reverso.PublicId,
            ReversalNumber = reverso.Number,
            Collector = recaudador,
            ReversedAmount = consignacion.Amount,
            ValueDate = consignacion.ValueDate,
            Reason = motivo,
            ReversedBy = usuario,
            LeftTechnicalOverdraft = false,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, reverso.PublicId, ClaseReversoDeCuenta, Prefijo(reverso.Number), reverso.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: depositante),
            ClavesDeEvento.Confirmacion, [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(consignacion.PublicId, ClaseConsignacionDeCuenta, consignacion.Number), Destino: destino), ct);
        return mensajes.FirstOrDefault()?.PublicId;
    }

    private static DepositChargeInvoiceRefV1 RefDeFactura(AccountCharge cargo, string nombre, string? codigoComercial, string? evento) => new()
    {
        ChargePublicId = cargo.PublicId,
        Kind = cargo.Kind.ToString(),
        Code = cargo.ChargeCode,
        Name = nombre,
        CommercialItemCode = codigoComercial,
        Event = evento,
        PeriodFrom = cargo.PeriodFrom,
        PeriodTo = cargo.PeriodTo,
    };

    /// <summary>
    /// Una unidad de hechos de una operación de cuenta —el principal y sus acompañantes— con el modo del principal (mensajes.md §2.1, C01 de la 013).
    /// </summary>
    private async Task<Result<UnidadEmitida>> EmitirUnidadDeCuentaAsync(IReadOnlyList<object> contenidos, string tipoPrincipal, HechoDeCuenta h,
        MemberAccountOperation op, string clase, Guid tercero, PrevalidationOutcome validacion, CancellationToken ct)
    {
        var modo = await modoDePaso.ResolverAsync(tipoPrincipal, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, clase, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: tercero),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value, ValidacionPrevia: validacion), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }
}
