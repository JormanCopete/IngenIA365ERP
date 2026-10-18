using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Integration.Reservas;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// El escenario de las ventas a crédito de Comercial en Cartera (feature 013, US6, bloque 10A): el del desembolso (<see cref="DesembolsoDePrueba"/>,
/// InMemory, hoy 2026-12-01, Cartera activada, Ana asociada, la oficina Norte) con
/// <list type="bullet">
/// <item>la línea <c>CONLIB</c> de <b>origen comercial</b> (por defecto para asociados, sin cobrar IVA sobre el interés);</item>
/// <item>las equivalencias de Comercial <c>MONTHLY</c> → mensual y <c>MONTHS</c> → 30 días;</item>
/// <item>la cajera de Comercial que confirma las ventas (con identidad central: es el usuario de origen de las respuestas, T11);</item>
/// <item>las piezas reales del consumidor (conversión, evaluación con el disponible y los bloqueos reales, nacimiento, recibos, ajustes) y de la
/// consulta y la reserva, sobre un único emisor de Cartera.</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class VentasACreditoDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    private VentasACreditoDePrueba(DesembolsoDePrueba d)
    {
        D = d;
        Db.CommercialTermEquivalences.Add(CommercialTermEquivalence.Nueva(CommercialTermKind.Periodicity, "MONTHLY", RepaymentPeriodicity.Monthly, null,
            new DateOnly(2026, 1, 1), 1, G.UtcNow.AddDays(-60)));
        Db.CommercialTermEquivalences.Add(CommercialTermEquivalence.Nueva(CommercialTermKind.TermUnit, "MONTHS", null, 30, new DateOnly(2026, 1, 1), 1,
            G.UtcNow.AddDays(-60)));
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        Transaccion.HayEnCurso().Returns(true);
    }

    public static async Task<VentasACreditoDePrueba> CrearAsync() => new(await DesembolsoDePrueba.CrearAsync(VersionDeVenta()));

    /// <summary>La versión de la línea que admite el origen comercial.</summary>
    public static CreditLineVersionInput VersionDeVenta() => OriginacionDePrueba.VersionDeLinea() with
    {
        AllowsCommercialOrigin = true,
        CommercialDefaultFor = DebtorKind.Associate,
        InterestTaxConcept = new InterestTaxConceptInput(false, Guid.NewGuid()),
        Amounts = new AmountRangeInput(100_000m, 30_000_000m),
    };

    /// <summary>
    /// T1098: las ventas sobre un escenario ya armado (el de los pagos, creado con <see cref="VersionDeVenta"/>), para aplicar pagos reales al
    /// crédito que nace de una venta.
    /// </summary>
    public static VentasACreditoDePrueba Sobre(DesembolsoDePrueba d) => new(d);

    public DesembolsoDePrueba D { get; }
    public OriginacionDePrueba O => D.O;
    public Sessions.GobiernoDePrueba G => D.G;
    public Application.Tests.Common.TestApplicationDbContext Db => D.Db;
    /// <summary>
    /// El emisor de Cartera de un consumo: uno por unidad, como en la API (el <c>EmisorDeMensajes</c> es Scoped y recuerda lo que emitió en su ámbito).
    /// </summary>
    public EmisionDeCartera Emision => _emision ??= D.Emision();

    private EmisionDeCartera? _emision;
    public ITransaccionDelRecaudador Transaccion { get; } = Substitute.For<ITransaccionDelRecaudador>();
    public User Cajera => D.Cajera;
    public Person Ana => O.Ana;

    public void Dispose() => D.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    public RecibosDeCartera Recibos() => new(Db, G.Reloj);
    public ConversionDeVentaACredito Conversion() => new(Db, new VersionVigenteDeLinea(Db), new EquivalenciasDeComercio(Db));
    public EvaluacionDeVentaACredito Evaluacion() => new(O.BloqueosReales(), O.Disponible(), O.Concentracion(), O.Listas, O.Autorizaciones);
    public NacimientoDeObligacionDeVenta Nacimiento() =>
        new(Db, O.Simulador(), D.Tramos(), D.Registro(), D.Numerador(), D.Cargador(), Emision, D.FechaContable(), O.Disponible(), D.Parametros(),
            D.Alertas, G.Actor, G.Reloj);
    public ConsumoDeVentaACredito Consumo() => new(Db, Recibos(), Conversion(), Evaluacion(), Nacimiento(), D.ValidacionPrevia(), Emision, O.Cerrojo);
    public VentasAnterioresAlCorte Anteriores() => new(Db, Recibos(), Emision, D.Alertas);
    public AjusteDesdeComercial Ajustes() =>
        new(Db, Recibos(), Consumo(), Anteriores(), Evaluacion(), new VersionVigenteDeLinea(Db), D.Cargador(), D.Registro(), D.Numerador(), Emision,
            D.FechaContable(), O.Cerrojo, G.Actor, G.Reloj);
    public ConsumeCommercialCreditMessageCommandHandler Consumir()
    {
        _emision = D.Emision();
        return new(Db, new MensajesEntrantes(Db), Recibos(), Consumo(), Ajustes(), Anteriores(), new Application.Lending.Deposits.Charges.FacturasDeCargos(Db));
    }

    public ConsultasDeCartera Consultas() =>
        new(Db, O.BloqueosReales(), Verificacion, O.Autorizaciones, O.Disponible(), new VersionVigenteDeLinea(Db), new EquivalenciasDeComercio(Db),
            D.Parametros(), G.Auditoria);
    public Application.Lending.Origination.IVerificacionDeListas Verificacion { get; } = Substitute.For<Application.Lending.Origination.IVerificacionDeListas>();

    public ReservasDeCartera Reservas() =>
        new(Db, Transaccion, O.Cerrojo, D.Numerador(), O.Disponible(), O.BloqueosReales(), D.Parametros(),
            new AuditoriaDeEscriturasDeCartera(Servicios(), Db), new SolicitudDeReservaValidator(), new SolicitudDeLiberacionValidator(), G.Actor, G.Reloj);

    /// <summary>Los servicios de la auditoría encadenada de prueba (como los de <see cref="Sessions.GobiernoDePrueba"/>).</summary>
    public IServiceProvider Servicios()
    {
        var tenant = Substitute.For<Application.Common.Interfaces.ICurrentTenantService>();
        tenant.TenantId.Returns(Application.Tests.Common.CooperativaDePrueba.PublicIdN);
        return new ServiceCollection()
            .AddSingleton(G.Mongo)
            .AddSingleton<Application.Common.Interfaces.IApplicationDbContext>(Db)
            .AddSingleton(tenant)
            .AddSingleton(Payroll.Common.NominaTestData.UsuarioDePrueba("cajera@coop", 7))
            .BuildServiceProvider();
    }

    // ---------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Un cupo rotativo general (línea nula, C54) para la persona, con su preferencia de abono.</summary>
    public CreditLimit Rotativo(Person p, decimal monto, PrepaymentChoice? preferencia = PrepaymentChoice.ReduceTerm)
    {
        var cupo = CreditLimit.NuevaVersion(p.Id, CreditLimitKind.Revolving, null, monto, CreditLimitOrigin.Assigned, new DateOnly(2026, 1, 1), 1,
            G.UtcNow.AddDays(-30), "Cupo rotativo de prueba", null);
        Db.CreditLimits.Add(cupo);
        Db.SaveChanges();
        if (preferencia is not null)
        {
            Db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(p.Id, null, cupo.Id, preferencia, 1, G.UtcNow.AddDays(-30), PreferenceOrigin.CreditLimitGrant,
                null));
            Db.SaveChanges();
        }
        Db.ChangeTracker.Clear();
        return cupo;
    }

    /// <summary>Una venta a crédito: el documento, el pago y lo que dice Comercial.</summary>
    public sealed record Venta(Guid Documento, Guid Pago, string Numero, decimal Monto, DateOnly Fecha);

    public Venta NuevaVenta(decimal monto = 600_000m, DateOnly? fecha = null, string? numero = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), numero ?? $"PV01-{Random.Shared.Next(1000, 9999)}", monto, fecha ?? Hoy);

    public static VentaACreditoRegistradaV2 ContenidoV2(Venta v, bool pendiente = false, string? linea = "CONLIB", string? numeroReservado = null,
        Guid? reserva = null, string periodicidad = "Monthly", int cuotas = 3) => new()
    {
        ThirdPartyKind = ConversionDeVentaACredito.Asociado,
        Person = new PartySnapshotV1 { TaxIdType = "CC", TaxId = "52000001", Name = "Ana Solicitante" },
        CreditPayment = new CreditPaymentV1
        {
            PaymentPublicId = v.Pago, LineNumber = 1, PaymentMeansCode = "CREDASOC", PaymentMeansClass = PaymentMeansClass.AssociateCredit, Amount = v.Monto,
        },
        DocumentTotal = v.Monto,
        AmountDue = v.Monto,
        Terms = new CreditTermsV1
        {
            TermUnit = "Months", Term = cuotas, Installments = cuotas, Periodicity = periodicidad, FirstDueDate = v.Fecha.AddMonths(1),
            FinalDueDate = v.Fecha.AddMonths(cuotas),
        },
        CreditLineCode = linea,
        SuggestedCreditLineCode = linea,
        PendingValidation = pendiente,
        Origin = pendiente ? CreditOrigin.ProvisionalCredit : CreditOrigin.Validated,
        AccountsReceivableRecordedBy = "Cartera",
        ReservedLoanNumber = numeroReservado,
        ReservationPublicId = reserva,
    };

    /// <summary>
    /// Emite el mensaje de Comercial como la cajera (la persona que confirma, T11), lo guarda y devuelve su <c>messageId</c>. <paramref name="contenido"/>
    /// es una <see cref="VentaACreditoRegistradaV2"/>, una v1 o un <see cref="AjusteDeVentaACreditoV1"/>.
    /// </summary>
    public async Task<Guid> EmitirAsync(Venta v, object contenido, Person? comprador = null, Guid? relacionado = null, Guid? documento = null,
        string? numero = null, DateOnly? fecha = null)
    {
        G.ActuarComo(Cajera);
        var emisor = new EmisorDeMensajes(Db, G.Actor, G.Reloj);
        var esAjuste = contenido is AjusteDeVentaACreditoV1;
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, documento ?? v.Documento, esAjuste ? nameof(DocumentClass.CreditNote) : nameof(DocumentClass.PosEquivalentDocument),
                "PV", numero ?? v.Numero, fecha ?? v.Fecha, O.Norte.PublicId, PersonPublicId: (comprador ?? Ana).PublicId),
            ClavesDeEvento.ConfirmacionPor(esAjuste ? ((AjusteDeVentaACreditoV1)contenido).OriginalCreditPaymentPublicId : v.Pago),
            [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: relacionado is { } r ? new DocumentoRelacionado(r, nameof(DocumentClass.PosEquivalentDocument), v.Numero) : null));
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return mensajes[0].PublicId;
    }

    /// <summary>Consume un mensaje como el proceso de integración (I-2) y devuelve lo que respondió.</summary>
    public async Task<ResultadoDeConsumo> ConsumirAsync(Guid mensaje)
    {
        G.ActuarComoProceso();
        var r = await Consumir().Handle(new ConsumeCommercialCreditMessageCommand([mensaje]), default);
        Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Emite una v2 de la venta y la consume.</summary>
    public async Task<(Guid Mensaje, ResultadoDeConsumo Resultado)> VenderAsync(Venta v, VentaACreditoRegistradaV2? contenido = null, Person? comprador = null)
    {
        var id = await EmitirAsync(v, contenido ?? ContenidoV2(v), comprador);
        return (id, await ConsumirAsync(id));
    }

    /// <summary>El crédito que nació de la venta.</summary>
    public Task<Domain.Entities.Lending.Loans.Loan> CreditoDeAsync(Venta v) =>
        Db.Loans.AsNoTracking().SingleAsync(l => l.SourcePaymentPublicId == v.Pago);

    /// <summary>Los mensajes emitidos de un tipo.</summary>
    public Task<List<Domain.Entities.Integration.Transactions.IntegrationMessage>> MensajesAsync(string tipo) =>
        Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == tipo).OrderBy(m => m.Id).ToListAsync();
}
