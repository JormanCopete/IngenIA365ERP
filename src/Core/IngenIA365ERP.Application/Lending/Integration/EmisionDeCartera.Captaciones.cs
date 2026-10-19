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
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Lo que un hecho de una cuenta necesita saber de ella (feature 014, US3, T085; mensajes.md §4.0): la cuenta, el código de su línea, el titular, si es
/// empleado, la sucursal contable de su oficina y la fecha contable del hecho. (nuevo)
/// </summary>
public sealed record HechoDeCuenta(MemberAccount Cuenta, string CodigoDeLinea, Guid TitularPublicId, bool TitularEsEmpleado, Guid SucursalPublicId,
    DateOnly FechaContable)
{
    public MemberAccountRefV1 Referencia => new() { AccountPublicId = Cuenta.PublicId, AccountNumber = Cuenta.Number };

    /// <summary>
    /// US8 (T143; research E107): si la cuenta es la marcada exenta del GMF a la fecha contable del hecho. La lee <c>HechoDeCuentaAsync</c> para que todos
    /// los hechos de la cuenta lleven la misma marca (la consignación, el retiro, el GMF, el cargo): con la marca en «Standard» en unos y «GmfExempt» en
    /// otros, una contadora que separe las cuentas por marca vería el saldo partido en dos.
    /// </summary>
    public bool ExentaDeGmf { get; init; }

    /// <summary>
    /// Las dimensiones vigentes de la cuenta (mensajes.md §3; research C20): la marca con precedencia embargada &gt; exenta del GMF &gt; estándar; los
    /// aportes nunca se embargan ni se marcan exentos. Sin <paramref name="exentaDeGmf"/>, la de <see cref="ExentaDeGmf"/>.
    /// </summary>
    public DepositFactDimensionsV1 Dimensiones(bool? exentaDeGmf = null) => new()
    {
        CreditLineCode = CodigoDeLinea,
        LineNature = Cuenta.Nature.ToString(),
        DepositClass = Cuenta.DepositClass?.ToString(),
        AccountFlag = (Cuenta.IsGarnished ? AccountFlag.Garnished : exentaDeGmf ?? ExentaDeGmf ? AccountFlag.GmfExempt : AccountFlag.Standard).ToString(),
        Inactive = Cuenta.Status == MemberAccountStatus.Inactive,
        IsEmployee = TitularEsEmpleado,
        BranchPublicId = SucursalPublicId,
    };
}

/// <summary>Una consignación de aportes tal como se emite (mensajes.md §4.1, <c>AporteRecibido</c>). (nuevo)</summary>
public sealed record AporteAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    Recaudador Recaudador,
    string Clave,
    ClaseDeMedioDeRecaudo ClaseDeMedio,
    Guid DepositantePublicId,
    ReferenciaDeRecibo Recibo,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    IReadOnlyList<CoveredInstallmentV1> Cuotas,
    string? ExcedenteComo,
    PrevalidationOutcome ValidacionPrevia);

/// <summary>Una consignación en una cuenta de captación tal como se emite (mensajes.md §4.1, <c>ConsignacionEnCuentaAplicada</c>). (nuevo)</summary>
public sealed record ConsignacionAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    Recaudador Recaudador,
    string Clave,
    ClaseDeMedioDeRecaudo ClaseDeMedio,
    Guid DepositantePublicId,
    ReferenciaDeRecibo Recibo,
    string Modalidad,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    bool Reactivada,
    PrevalidationOutcome ValidacionPrevia)
{
    /// <summary>US4 (T111): lo que queda en canje (C14).</summary>
    public decimal EnCanje { get; init; }

    /// <summary>US4 (T111): el depósito inicial que activa una cuenta en apertura.</summary>
    public bool EsDepositoInicial { get; init; }

    /// <summary>US4 (T116): los cargos pendientes que la consignación cobró (acompañante <c>CargoPendienteCobrado</c>).</summary>
    public IReadOnlyList<(AccountCharge Cargo, decimal Monto)> Cobrados { get; init; } = [];
}

/// <summary>El reverso de una operación de cuenta tal como se emite (mensajes.md §4.1, <c>OperacionDeCuentaReversada</c>). (nuevo)</summary>
public sealed record ReversoDeCuentaAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Reverso,
    MemberAccountOperation Original,
    string TipoDelOriginal,
    string ClaseDelOriginal,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    string IniciadoPor,
    string Motivo,
    UserRefV1? ReversadoPor)
{
    /// <summary>US8 (T143; research E106): la clase del recaudador del original (la del puente que la contrapartida restituye).</summary>
    public string? ClaseDeRecaudador { get; init; }

    /// <summary>US8 (T143): el <c>OrderKind</c> de la orden de giro del original, si salió por giro.</summary>
    public string? TipoDeOrden { get; init; }

    /// <summary>US8 (T143): el <c>ChargeKind</c> del cargo del original (un cargo o su condonación).</summary>
    public string? MotivoDeLaContrapartida { get; init; }

    /// <summary>US8 (T143): los espejos de las otras cuentas del original (el destino de un traslado), cada una con sus dimensiones vigentes.</summary>
    public IReadOnlyList<(HechoDeCuenta Hecho, IReadOnlyList<DepositConceptAmountV1> Valores)> OtrasCuentas { get; init; } = [];
}

/// <summary>Un traslado entre dos cuentas tal como se emite (mensajes.md §4.1, <c>TrasladoEntreCuentasAplicado</c>; la cesión de aportes). (nuevo)</summary>
public sealed record TrasladoAEmitir(
    HechoDeCuenta Origen,
    HechoDeCuenta Destino,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> ValoresDelOrigen,
    IReadOnlyList<DepositConceptAmountV1> ValoresDelDestino);

/// <summary>Una devolución ordenada tal como se emite (mensajes.md §4.1, <c>DevolucionOrdenada</c>), con su orden de giro v2 si sale por Tesorería. (nuevo)</summary>
public sealed record DevolucionAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    IReadOnlyList<DepositConceptAmountV1> Valores,
    string TipoDeOrden,
    AccountPayoutOrder? Orden,
    PayeeV1 Beneficiario,
    Guid BeneficiarioPublicId,
    string Fuente,
    string Concepto)
{
    /// <summary>US10 (T101): la clase del origen de la devolución (por defecto la devolución sin retiro; la del retiro, <c>MembershipWithdrawalSettlement</c>).</summary>
    public string Clase { get; init; } = EmisionDeCartera.ClaseDevolucionDeAportes;
}

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de una consignación de cuenta (mensajes.md §2).</summary>
    public const string ClaseConsignacionDeCuenta = "AccountDeposit";

    /// <summary>La clase del origen de un traslado entre cuentas.</summary>
    public const string ClaseTrasladoDeCuenta = "AccountTransfer";

    /// <summary>La clase del origen del reverso de una operación de cuenta.</summary>
    public const string ClaseReversoDeCuenta = "AccountReversal";

    /// <summary>La clase del origen de una devolución de aportes sin retiro.</summary>
    public const string ClaseDevolucionDeAportes = "ContributionRefund";

    /// <summary>Quién inició el reverso de una operación de cuenta: el recaudador (por el contrato).</summary>
    public const string ReversoIniciadoPorElRecaudador = "Collector";

    /// <summary>
    /// Lo que todo hecho de una cuenta necesita: el código de la línea, el titular y si es empleado, la sucursal contable de la oficina de la cuenta (sin
    /// ella la operación se rechaza, C9 de la 013) y la fecha contable de la fecha valor.
    /// </summary>
    public async Task<Result<HechoDeCuenta>> HechoDeCuentaAsync(MemberAccount cuenta, DateOnly fechaValor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var sucursal = await SucursalContableAsync(cuenta.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<HechoDeCuenta>(sucursal.Error);
        var fecha = await fechaContable.CalcularAsync(fechaValor, ct);
        if (fecha.IsFailure) return Result.Failure<HechoDeCuenta>(fecha.Error);
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Code)
            .FirstAsync(ct);
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == cuenta.HolderPersonId)
            .Select(p => new { p.PublicId, p.IsEmployee }).FirstAsync(ct);
        var contable = fecha.Value;
        var exenta = cuenta.Nature != LineNature.Contribution && await db.GmfExemptAccounts.AsNoTracking()
            .AnyAsync(g => g.MemberAccountId == cuenta.Id && !g.IsDeleted && g.ValidFrom <= contable && (g.ValidTo == null || g.ValidTo >= contable), ct);
        return Result.Success(new HechoDeCuenta(cuenta, linea, titular.PublicId, titular.IsEmployee, sucursal.Value, contable) { ExentaDeGmf = exenta });
    }

    /// <summary>Los valores de unas filas del libro por concepto, con su clase de aporte (mensajes.md §3), sin los que se anulan.</summary>
    public static IReadOnlyList<DepositConceptAmountV1> ValoresDeCuenta(IEnumerable<MemberAccountTransaction> filas, Func<MemberAccountTransaction, decimal>? signo = null) =>
        filas.GroupBy(f => f.Concept)
            .Select(g => new DepositConceptAmountV1
            {
                Concept = g.Key.ToString(), ContributionClass = ClaseDeAporte(g.Key)?.ToString(), Amount = g.Sum(signo ?? (f => f.Amount)),
            })
            .Where(v => v.Amount != 0m)
            .OrderBy(v => v.Concept, StringComparer.Ordinal)
            .ToList();

    /// <summary>La clase de aporte de un concepto del libro (nula si no es de aportes).</summary>
    public static ContributionClass? ClaseDeAporte(MemberAccountConcept concepto) => concepto switch
    {
        MemberAccountConcept.ContributionMinimum => ContributionClass.Minimum,
        MemberAccountConcept.ContributionOrdinary => ContributionClass.Ordinary,
        MemberAccountConcept.ContributionExtraordinary => ContributionClass.Extraordinary,
        MemberAccountConcept.ContributionRevaluation => ContributionClass.Revalued,
        _ => null,
    };

    /// <summary>
    /// <c>AporteRecibido</c> (T085; mensajes.md §2, §4.1): origen la operación (<c>AccountDeposit</c>), clave <c>Confirmation</c>, tercero el depositante,
    /// con las cuotas que cubre y qué se hizo con el excedente; comprobante <c>AO</c> (o <c>DN</c> si recauda Nómina, por el mapeo). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> AporteRecibidoAsync(AporteAEmitir a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var h = a.Hecho;
        var contenido = new AporteRecibidoV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = a.Operacion.ValueDate,
            RegisteredAt = Instante(a.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = a.Valores,
            OperationNumber = a.Operacion.Number,
            RulesVersion = a.Operacion.RulesVersion,
            Collector = Recaudador(a.Recaudador, a.Clave, a.ClaseDeMedio, a.DepositantePublicId, a.Recibo),
            InstallmentsCovered = a.Cuotas,
            OverpaymentAs = a.ExcedenteComo,
        };
        return await EmitirDeCuentaAsync(contenido, AporteRecibidoV1.Type, h, a.Operacion, ClaseConsignacionDeCuenta, a.DepositantePublicId,
            a.ValidacionPrevia, ct);
    }

    /// <summary>
    /// <c>ConsignacionEnCuentaAplicada</c> (mensajes.md §4.1): la cuota del ahorro permanente que paga un recaudador. Origen la operación
    /// (<c>AccountDeposit</c>), tercero el depositante. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> ConsignacionEnCuentaAsync(ConsignacionAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var contenido = new ConsignacionEnCuentaAplicadaV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = c.Operacion.ValueDate,
            RegisteredAt = Instante(c.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = c.Valores,
            OperationNumber = c.Operacion.Number,
            RulesVersion = c.Operacion.RulesVersion,
            Collector = Recaudador(c.Recaudador, c.Clave, c.ClaseDeMedio, c.DepositantePublicId, c.Recibo),
            Modality = c.Modalidad,
            Reactivated = c.Reactivada,
            PendingClearance = c.EnCanje,
            IsOpeningDeposit = c.EsDepositoInicial,
        };
        List<object> contenidos = [contenido];
        if (c.Cobrados.Count > 0) contenidos.Add(CargoPendienteCobrado(h, c.Operacion, c.Cobrados));
        return await EmitirUnidadDeCuentaAsync(contenidos, ConsignacionEnCuentaAplicadaV1.Type, h, c.Operacion, ClaseConsignacionDeCuenta,
            c.DepositantePublicId, c.ValidacionPrevia, ct);
    }

    /// <summary>
    /// <c>TrasladoEntreCuentasAplicado</c> (mensajes.md §4.1): la cesión de aportes entre asociados (FR-048), con el origen y el destino y sus
    /// dimensiones. Origen la operación (<c>AccountTransfer</c>), tercero el titular de origen. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> TrasladoEntreCuentasAsync(TrasladoAEmitir t, CancellationToken ct)
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
        return await EmitirDeCuentaAsync(contenido, TrasladoEntreCuentasAplicadoV1.Type, t.Origen, t.Operacion, ClaseTrasladoDeCuenta,
            t.Origen.TitularPublicId, PrevalidationOutcome.NotApplicable, ct);
    }

    /// <summary>
    /// <c>DevolucionOrdenada</c> (mensajes.md §4.1) y, si sale por Tesorería, <c>OrdenDeGiroEmitida</c> v2 (§5.3) con origen la orden: la devolución de
    /// aportes sin retiro (FR-042, FR-043). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> DevolucionOrdenadaAsync(DevolucionAEmitir d, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(d);
        var h = d.Hecho;
        var contenido = new DevolucionOrdenadaV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = d.Operacion.ValueDate,
            RegisteredAt = Instante(d.Operacion.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = d.Valores,
            OperationNumber = d.Operacion.Number,
            RulesVersion = d.Operacion.RulesVersion,
            OrderKind = d.TipoDeOrden,
            OrderPublicId = d.Orden?.PublicId,
            PayeePersonPublicId = d.BeneficiarioPublicId,
            Source = d.Fuente,
        };
        var unidad = await EmitirDeCuentaAsync(contenido, DevolucionOrdenadaV1.Type, h, d.Operacion, d.Clase, d.BeneficiarioPublicId,
            PrevalidationOutcome.NotApplicable, ct);
        if (unidad.IsFailure || d.Orden is null) return unidad;

        var orden = new OrdenDeGiroEmitidaV2
        {
            OrderPublicId = d.Orden.PublicId,
            OrderNumber = d.Orden.Number,
            OrderKind = d.TipoDeOrden,
            Account = h.Referencia,
            SourceDocumentPublicId = d.Operacion.PublicId,
            Form = "Transfer",
            Payee = d.Beneficiario,
            GrossAmount = d.Orden.Amount,
            NetAmount = d.Orden.Amount,
            PayDate = h.FechaContable,
            Concept = d.Concepto,
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, d.Orden.PublicId, ClaseOrdenDeGiro, Prefijo(d.Orden.Number), d.Orden.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: d.BeneficiarioPublicId),
            ClavesDeEvento.Confirmacion, [orden], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return Result.Success(new UnidadEmitida([.. unidad.Value.Mensajes, .. mensajes]));
    }

    /// <summary>
    /// <c>OperacionDeCuentaReversada</c> (mensajes.md §4.1): el espejo exacto del hecho original con las dimensiones vigentes a la fecha contable; origen el
    /// reverso (<c>AccountReversal</c>), <c>related</c> = el original y <b>hereda el modo</b> del hecho original. Sin guardar.
    /// </summary>
    public async Task<UnidadEmitida> OperacionDeCuentaReversadaAsync(ReversoDeCuentaAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var original = await HechoOriginalAsync(r.Original.PublicId, r.TipoDelOriginal, r.Original.Number, ct)
                       ?? await HechoDeLaUnidadAsync(r.Original, r.TipoDelOriginal, ct);
        var contenido = new OperacionDeCuentaReversadaV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            ValueDate = r.Original.ValueDate,
            RegisteredAt = Instante(r.Reverso.RegisteredAt),
            Dimensions = h.Dimensiones(),
            Amounts = r.Valores,
            Original = original,
            OperationNumber = r.Reverso.Number,
            RulesVersion = r.Reverso.RulesVersion,
            Reason = r.Motivo,
            InitiatedBy = r.IniciadoPor,
            Mirrors =
            [
                new DepositFactMirrorV1 { Type = r.TipoDelOriginal, Amounts = r.Valores },
                .. r.OtrasCuentas.Select(o => new DepositFactMirrorV1
                {
                    Type = r.TipoDelOriginal, Amounts = o.Valores, Account = o.Hecho.Referencia, HolderPersonPublicId = o.Hecho.TitularPublicId,
                    Dimensions = o.Hecho.Dimensiones(),
                }),
            ],
            CollectorClass = r.ClaseDeRecaudador,
            OrderKind = r.TipoDeOrden,
            ReasonCode = r.MotivoDeLaContrapartida,
        };
        var relacionado = new DocumentoRelacionado(r.Original.PublicId, r.ClaseDelOriginal, r.Original.Number);
        ModoDeEntrega modo = original is null
            ? (await modoDePaso.ResolverAsync(OperacionDeCuentaReversadaV1.Type, h.FechaContable, ct)).Value ?? new ModoDeEntrega.Sellado(DeliveryMode.NotPosted)
            : new ModoDeEntrega.Heredado(original.OriginPublicId);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, r.Reverso.PublicId, ClaseReversoDeCuenta, Prefijo(r.Reverso.Number), r.Reverso.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo, Relacionado: relacionado, UsuarioDeOrigen: r.ReversadoPor), ct);
        return new UnidadEmitida(mensajes);
    }

    /// <summary>
    /// Feature 014 (decisión del dueño del 2026-10-18, D45): el hecho original de una operación de cuenta que viajó en la unidad de <b>otro</b> origen —el
    /// cruce al retiro va en la unidad del pago del crédito—, por el mensaje que sellan sus filas del libro. Nulo si no lo hay.
    /// </summary>
    private async Task<OriginalFactRefV1?> HechoDeLaUnidadAsync(MemberAccountOperation operacion, string tipo, CancellationToken ct)
    {
        var mensaje = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.OperationId == operacion.Id && t.MessagePublicId != null).Select(t => t.MessagePublicId).FirstOrDefaultAsync(ct);
        if (mensaje is not { } id) return null;
        var leido = db.IntegrationMessages.Local.Where(m => m.PublicId == id).Select(m => new { m.OriginPublicId, m.OriginNumber, m.Type }).FirstOrDefault()
                    ?? await db.IntegrationMessages.AsNoTracking().Where(m => m.PublicId == id)
                        .Select(m => new { m.OriginPublicId, m.OriginNumber, m.Type }).FirstOrDefaultAsync(ct);
        return leido is null || leido.Type != tipo
            ? null
            : new OriginalFactRefV1 { MessageId = id, Type = tipo, OriginPublicId = leido.OriginPublicId, Number = leido.OriginNumber ?? operacion.Number };
    }

    private async Task<Result<UnidadEmitida>> EmitirDeCuentaAsync(object contenido, string tipo, HechoDeCuenta h, MemberAccountOperation op, string clase,
        Guid tercero, PrevalidationOutcome validacion, CancellationToken ct)
    {
        var modo = await modoDePaso.ResolverAsync(tipo, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, clase, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: tercero),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: validacion), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    private static DateTimeOffset Instante(DateTime registrado) => new(DateTime.SpecifyKind(registrado, DateTimeKind.Utc));
}
