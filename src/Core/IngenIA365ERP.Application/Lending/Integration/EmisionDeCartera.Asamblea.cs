using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Lo que una línea de la asamblea deja en una cuenta del asociado: su hecho de cuenta y el cambio de saldo por concepto. (nuevo)</summary>
public sealed record LineaDeAsambleaAEmitir(HechoDeCuenta Hecho, IReadOnlyList<DepositConceptAmountV1> Valores);

/// <summary>Una orden de giro de la asamblea (el pago de los excedentes o la amortización): la cuenta de aportes, el valor, la orden y el beneficiario. (nuevo)</summary>
public sealed record OrdenDeAsambleaAEmitir(HechoDeCuenta Hecho, decimal Monto, AccountPayoutOrder Orden, PayeeV1 Beneficiario, Guid BeneficiarioPublicId);

/// <summary>
/// La aplicación de una decisión de asamblea tal como se emite (feature 014, US12, T203; mensajes.md §4.1, forma masiva): la decisión, el punto del acta,
/// la fecha, las líneas por cuenta, los pagos de excedentes en dinero y las órdenes de giro. (nuevo)
/// </summary>
public sealed record DistribucionAEmitir(
    AssemblyDistribution Distribucion,
    Guid SessionItemPublicId,
    DateOnly Fecha,
    IReadOnlyList<LineaDeAsambleaAEmitir> Lineas,
    IReadOnlyList<OrdenDeAsambleaAEmitir> Pagos,
    IReadOnlyList<OrdenDeAsambleaAEmitir> Ordenes,
    PrevalidationOutcome ValidacionPrevia);

/// <summary>El reverso entero de una decisión de asamblea: por cuenta, su hecho vigente y el espejo de lo que la decisión le dejó. (nuevo)</summary>
public sealed record ReversoDeDistribucionAEmitir(
    AssemblyDistribution Distribucion,
    string TipoDelOriginal,
    DateOnly Fecha,
    IReadOnlyList<LineaDeAsambleaAEmitir> Espejos,
    string Motivo,
    UserRefV1? ReversadoPor,
    DateTime Instante);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de una decisión de asamblea (y de sus operaciones de cuenta).</summary>
    public const string ClaseDecisionDeAsamblea = "AssemblyDistribution";

    /// <summary>La clase del origen del reverso entero de una decisión de asamblea.</summary>
    public const string ClaseReversoDeAsamblea = "AssemblyDistributionReversal";

    /// <summary>El tipo del hecho masivo de la clase de decisión (contabilidad.md §3, filas 22 a 24).</summary>
    public static string TipoDeAsamblea(AssemblyDistributionKind clase) => clase switch
    {
        AssemblyDistributionKind.Revaluation => RevalorizacionDeAportesAplicadaV1.Type,
        AssemblyDistributionKind.SurplusDistribution => ExcedentesDistribuidosV1.Type,
        _ => AportesAmortizadosV1.Type,
    };

    /// <summary>El destino como lo nombra el hecho (mensajes.md §4.1): <c>ContributionCapital</c>, <c>DepositAccount</c> u <c>Order</c>.</summary>
    public static string? DestinoDelHecho(AssemblyDistribution d) => d.Kind switch
    {
        AssemblyDistributionKind.Revaluation => "ContributionCapital",
        AssemblyDistributionKind.Amortization => "Order",
        _ => d.SurplusDestination switch
        {
            SurplusDestination.Savings => "DepositAccount",
            SurplusDestination.Payment => "Order",
            _ => "ContributionCapital",
        },
    };

    /// <summary>
    /// <c>RevalorizacionDeAportesAplicada</c>, <c>ExcedentesDistribuidos</c> o <c>AportesAmortizados</c> (US12, T203; mensajes.md §4.1): <b>un</b> hecho masivo
    /// con una línea por cuenta (y, en el pago de excedentes, un pago por asociado), origen la decisión (<c>AssemblyDistribution</c>), tercero ninguno —cada
    /// línea lleva su titular—, comprobante <c>AO</c> por el mapeo; y una <c>OrdenDeGiroEmitida</c> v2 por orden, con origen la orden. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> DistribucionAplicadaAsync(DistribucionAEmitir a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var d = a.Distribucion;
        var primera = a.Lineas.Select(l => l.Hecho).Concat(a.Pagos.Select(p => p.Hecho)).Concat(a.Ordenes.Select(o => o.Hecho)).First();
        var referencia = new AssemblyDistributionRefV1
        {
            PublicId = d.PublicId, Kind = d.Kind.ToString(), Year = d.FiscalYear, SessionItemPublicId = a.SessionItemPublicId, Fund = d.FundAmount.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            Percent = d.DecidedFraction, Value = d.DecidedValue, Base = d.AppliedTotal,
        };
        var lineas = a.Lineas.Select(l => new DepositFactLineV1
        {
            Account = l.Hecho.Referencia, HolderPersonPublicId = l.Hecho.TitularPublicId, Dimensions = l.Hecho.Dimensiones(), Amounts = l.Valores,
            ValueDate = a.Fecha,
        }).ToList();
        var totales = lineas.GroupBy(l => System.Text.Json.JsonSerializer.Serialize(l.Dimensions))
            .Select(g => new DepositFactTotalV1
            {
                Dimensions = g.First().Dimensions, AccountCount = g.Count(),
                Amounts = g.SelectMany(l => l.Amounts).GroupBy(v => (v.Concept, v.ContributionClass, v.ReasonCode))
                    .Select(v => new DepositConceptAmountV1
                    {
                        Concept = v.Key.Concept, ContributionClass = v.Key.ContributionClass, ReasonCode = v.Key.ReasonCode, Amount = v.Sum(x => x.Amount),
                    }).ToList(),
            }).ToList();
        var control = lineas.Sum(l => l.Amounts.Sum(v => Math.Abs(v.Amount))) + a.Pagos.Sum(p => p.Monto);
        object contenido = d.Kind switch
        {
            AssemblyDistributionKind.Revaluation => new RevalorizacionDeAportesAplicadaV1
            {
                Distribution = referencia, Totals = totales, Lines = lineas, Destination = DestinoDelHecho(d), LineCount = lineas.Count, ControlTotal = control,
            },
            AssemblyDistributionKind.SurplusDistribution => new ExcedentesDistribuidosV1
            {
                Distribution = referencia, Totals = totales, Lines = lineas, Destination = DestinoDelHecho(d), LineCount = lineas.Count + a.Pagos.Count,
                ControlTotal = control,
                Payments = a.Pagos.Select(p => new SurplusPaymentV1
                {
                    Account = p.Hecho.Referencia, HolderPersonPublicId = p.Hecho.TitularPublicId, Dimensions = p.Hecho.Dimensiones(), Amount = p.Monto,
                    OrderPublicId = p.Orden.PublicId,
                }).ToList(),
            },
            _ => new AportesAmortizadosV1
            {
                Distribution = referencia, Totals = totales, Lines = lineas, Destination = DestinoDelHecho(d), LineCount = lineas.Count, ControlTotal = control,
            },
        };
        var tipo = TipoDeAsamblea(d.Kind);
        var modo = await modoDePaso.ResolverAsync(tipo, primera.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = new List<Domain.Entities.Integration.Transactions.IntegrationMessage>(await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, d.PublicId, ClaseDecisionDeAsamblea, Prefijo(d.Number), d.Number, primera.FechaContable,
                primera.SucursalPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: a.ValidacionPrevia), ct));

        // Las órdenes de giro (el pago de los excedentes y la amortización), cada una con su origen.
        foreach (var o in a.Pagos.Concat(a.Ordenes))
        {
            var orden = new OrdenDeGiroEmitidaV2
            {
                OrderPublicId = o.Orden.PublicId,
                OrderNumber = o.Orden.Number,
                OrderKind = TipoDeOrden(o.Orden),
                Account = o.Hecho.Referencia,
                SourceDocumentPublicId = d.PublicId,
                Form = "Transfer",
                Payee = o.Beneficiario,
                GrossAmount = o.Orden.Amount,
                NetAmount = o.Orden.Amount,
                PayDate = o.Hecho.FechaContable,
                Concept = d.Kind == AssemblyDistributionKind.Amortization ? $"Amortización de aportes {d.Number}" : $"Excedentes {d.FiscalYear} {d.Number}",
            };
            mensajes.AddRange(await emisor.EmitirAsync(new SolicitudDeEmision(
                new OrigenDeEmision(MessageOriginKind.Document, o.Orden.PublicId, ClaseOrdenDeGiro, Prefijo(o.Orden.Number), o.Orden.Number, o.Hecho.FechaContable,
                    o.Hecho.SucursalPublicId, PersonPublicId: o.BeneficiarioPublicId),
                ClavesDeEvento.Confirmacion, [orden], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct));
        }
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>
    /// El reverso entero de una decisión de asamblea (US12, T203): <b>un</b> <c>OperacionDeCuentaReversada</c> con un espejo por cuenta —las dimensiones
    /// vigentes de cada una— que el constructor devuelve a su fondo (o a la devolución por pagar); origen el reverso de la decisión, <c>related</c> = la
    /// decisión y <b>hereda el modo</b> del hecho original. Sin guardar.
    /// </summary>
    public async Task<UnidadEmitida> DistribucionReversadaAsync(ReversoDeDistribucionAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var d = r.Distribucion;
        var primera = r.Espejos[0].Hecho;
        var numero = $"{d.Number}-R";
        var contenido = new OperacionDeCuentaReversadaV1
        {
            Account = primera.Referencia,
            HolderPersonPublicId = primera.TitularPublicId,
            ValueDate = d.AppliedOn ?? r.Fecha,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(r.Instante, DateTimeKind.Utc)),
            Dimensions = primera.Dimensiones(),
            Amounts = r.Espejos[0].Valores,
            Original = d.AppliedMessagePublicId is { } mensaje
                ? new OriginalFactRefV1 { MessageId = mensaje, Type = r.TipoDelOriginal, OriginPublicId = d.PublicId, Number = d.Number }
                : null,
            OperationNumber = numero,
            RulesVersion = Domain.Lending.VersionesDeReglas.Vigente,
            Reason = r.Motivo,
            InitiatedBy = "AssemblyDistributionReversal",
            Mirrors = r.Espejos.Select(e => new DepositFactMirrorV1
            {
                Type = r.TipoDelOriginal, Amounts = e.Valores, Account = e.Hecho.Referencia, HolderPersonPublicId = e.Hecho.TitularPublicId,
                Dimensions = e.Hecho.Dimensiones(),
            }).ToList(),
            OrderKind = d.Kind == AssemblyDistributionKind.Amortization ? nameof(PayoutPurpose.ContributionRefund) : null,
        };
        ModoDeEntrega modo = d.AppliedMessagePublicId is null
            ? (await modoDePaso.ResolverAsync(OperacionDeCuentaReversadaV1.Type, primera.FechaContable, ct)).Value ?? new ModoDeEntrega.Sellado(DeliveryMode.NotPosted)
            : new ModoDeEntrega.Heredado(d.PublicId);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, Guid.NewGuid(), ClaseReversoDeAsamblea, Prefijo(numero), numero, primera.FechaContable,
                primera.SucursalPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo, Relacionado: new DocumentoRelacionado(d.PublicId, ClaseDecisionDeAsamblea, d.Number),
            UsuarioDeOrigen: r.ReversadoPor), ct);
        return new UnidadEmitida(mensajes);
    }
}
