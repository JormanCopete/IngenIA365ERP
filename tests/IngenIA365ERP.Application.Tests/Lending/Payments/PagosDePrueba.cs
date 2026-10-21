using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// El escenario de los pagos (feature 013, US5, bloque 9B): el de <see cref="CausacionDePrueba"/> —InMemory, hoy local 2026-12-01, línea <c>CONLIB</c> al
/// 20 % EA, usura de consumo 25 %, el crédito de Ana desembolsado hoy por 3.000.000 a 24 cuotas mensuales, Cartera activada desde el 2026-11-01— con
/// el contrato de obligaciones <b>real</b> y sus piezas: el registro de recaudadores sustituido (<c>TST</c> de prueba y <c>NOM</c> de Nómina cuentan), la
/// transacción del recaudador «en curso» (InMemory no tiene transacciones; lo transaccional va en e2e), el SMMLV y <c>PREPAGO_TOPE_SMMLV</c>
/// replicados, Contabilidad iniciada con el mes abierto y la evaluación contable «no disponible», salvo que la prueba lo cambie. (nuevo)
/// </summary>
internal sealed class PagosDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    private PagosDePrueba(CausacionDePrueba c)
    {
        C = c;
        Transaccion.HayEnCurso().Returns(_ => EnTransaccion);
        Registro.RecaudadorAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var codigo = ci.ArgAt<string>(0);
            return Task.FromResult(Recaudadores.TryGetValue(codigo, out var clase)
                ? new ParticipanteVigente(Guid.NewGuid(), codigo, clase, IntegrationParticipantRole.ObligationsCollector, string.Empty, clase == IntegrationParticipantClass.Test,
                    new DateOnly(2026, 1, 1), null, [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)])
                : null);
        });
        Cierres.CerradoHastaAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(CerradoHasta));
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        Servicios = new ServiceCollection()
            .AddSingleton(G.Mongo)
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton(tenant)
            .AddSingleton(NominaTestData.UsuarioDePrueba("cajera@coop", 9))
            .AddSingleton(G.Actor)
            .BuildServiceProvider();
    }

    /// <summary>
    /// Arma el escenario con el crédito de Ana desembolsado hoy y los valores nacionales del tope de prepago. Con <paramref name="sinTopeDePrepago"/>
    /// no siembra ni el SMMLV ni el límite <c>PREPAGO_TOPE_SMMLV</c> (la recogida no los necesita, decisión del dueño del 2026-10-14).
    /// </summary>
    public static async Task<PagosDePrueba> CrearAsync(bool sinTopeDePrepago = false, CausacionDePrueba? causacion = null)
    {
        var p = new PagosDePrueba(causacion ?? await CausacionDePrueba.CrearAsync());
        if (!sinTopeDePrepago)
            await CopiasDePrueba.SembrarAsync(p.Db, p.G.UtcNow.AddDays(-40),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Smmlv, string.Empty, new DateOnly(2026, 1, 1), 1_750_905m, p.G.UtcNow.AddDays(-40), unidad: "Pesos"),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PrepagoTopeSmmlv, new DateOnly(2026, 1, 1), 880m, p.G.UtcNow.AddDays(-40),
                    unidad: "Veces"));
        p.Db.ChangeTracker.Clear();
        p.G.Conceder();
        return p;
    }

    public CausacionDePrueba C { get; }
    public Disbursements.DesembolsoDePrueba D => C.D;
    public Sessions.GobiernoDePrueba G => C.D.G;
    public TestApplicationDbContext Db => C.Db;
    public Loan Credito => C.Credito;
    public IServiceProvider Servicios { get; }

    public ITransaccionDelRecaudador Transaccion { get; } = Substitute.For<ITransaccionDelRecaudador>();
    public bool EnTransaccion { get; set; } = true;
    public IRegistroDeIntegracion Registro { get; } = Substitute.For<IRegistroDeIntegracion>();
    public Dictionary<string, IntegrationParticipantClass> Recaudadores { get; } = new()
    {
        [ClavesDeRecaudador.Prueba] = IntegrationParticipantClass.Test,
        [ClavesDeRecaudador.Nomina] = IntegrationParticipantClass.Payroll,
    };
    public ICierresDeCartera Cierres { get; } = Substitute.For<ICierresDeCartera>();
    public DateOnly? CerradoHasta { get; set; }
    public IAvisoDeAbonoConPreferencia Aviso { get; } = Substitute.For<IAvisoDeAbonoConPreferencia>();
    public IVigenciaDeDocumentosDeCartera Documentos => new Application.Lending.Documents.AnulacionDeDocumentosPorReverso(Db);

    public void Dispose() => C.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public ReglasDeFechaValor Fechas() => new(Db, D.Parametros(), G.Permisos, G.Actor, Cierres, G.Reloj);
    public FirmezaDelPago Firmeza() => new(D.Parametros(), new CalendarioDeDiasHabiles(Db));
    public PreferenciaDePrepagoVigente Preferencias() => new(Db);
    public AplicacionDePagos Aplicacion() => new(Db, C.Cargador(), D.PuestaAlDia(), D.Registro(), D.Numerador(), D.Emision(), D.Partes(), D.FechaContable(),
        Firmeza(), Fechas(), Preferencias(), D.O.L.Valores(), G.Actor, Aviso, G.Reloj);
    public ReversionDePago Reversion() => new(Db, C.Cargador(), D.Registro(), D.Numerador(), D.Emision(), D.Partes(), D.FechaContable(), Aplicacion(), Documentos,
        G.Actor, G.Reloj);
    public ResolucionDeSaldoAFavor Resolucion() => new(Db, C.Cargador(), D.PuestaAlDia(), D.Registro(), D.Numerador(), D.Emision(), D.Partes(), D.FechaContable(),
        Aplicacion(), G.Actor, G.Reloj);
    public AuditoriaDeEscriturasDeCartera Auditoria() => new(Servicios, Db);
    public NoDescontadosDelLote NoDescontados() => new(Db, C.Cargador(), D.Alertas, G.Reloj);

    public ContratoDeObligacionesDeCartera Contrato() => new(Db, Transaccion, Registro, D.ValidacionPrevia(), D.O.Cerrojo, C.Cargador(), Aplicacion(), Reversion(),
        Resolucion(), NoDescontados(), Fechas(), Auditoria(), new SolicitudDeAplicacionValidator(), new SolicitudDeReversoValidator(),
        new SolicitudDeResolucionValidator(), new SolicitudDeSimulacionValidator(), G.Reloj);

    public ObligacionesDeCartera Obligaciones() => new(Db, C.Cargador());

    public SimulateLoanPaymentQueryHandler Simular() => new(Db, Aplicacion(), Fechas(), G.Alcance, G.Auditoria, G.Reloj);
    public PreviewLoanPaymentReversalQueryHandler VistaPrevia() => new(Db, Reversion(), G.Alcance, G.Auditoria);
    public ReverseLoanPaymentCommandHandler Reversar() => new(Db, Reversion(), D.O.Cerrojo, G.Alcance, G.Auditoria);
    public ChangePrepaymentPreferenceCommandHandler CambiarPreferencia() =>
        new(Db, D.Registro(), D.Numerador(), D.O.Cerrojo, Preferencias(), G.Permisos, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public GetPrepaymentPreferenceQueryHandler Preferencia() => new(Db, Preferencias(), G.Alcance, G.Auditoria, G.Reloj);

    // ------------------------------------------------------------------------------------------------------- escenario --

    public static Recaudador Prueba { get; } = new(ClavesDeRecaudador.Prueba, ClaseDeRecaudador.Test);

    public static string Clave() => $"{ClavesDeRecaudador.Prueba}:{Guid.NewGuid():N}";

    /// <summary>Un pago del recaudador de prueba sobre el crédito de Ana (o el que diga), pagado por Ana en efectivo.</summary>
    public PagoAAplicar Pago(ModalidadDePago modalidad, decimal valor, DateOnly? fechaValor = null, string? clave = null, EleccionDeAbono? eleccion = null,
        int? cuota = null, string? simulacion = null, Guid? obligacion = null, ClaseDeMedioDeRecaudo medio = ClaseDeMedioDeRecaudo.Cash, Guid? pagador = null) =>
        new(clave ?? Clave(), obligacion ?? Credito.PublicId, modalidad, valor, fechaValor ?? G.HoyLocal, medio, pagador ?? D.O.Ana.PublicId,
            new ReferenciaDeRecibo("RC-TST-0001", fechaValor ?? G.HoyLocal, null, null, "Recibo"), eleccion, cuota, simulacion);

    public async Task<RespuestaDelContratoDto<ComprobanteDeAplicacionDto>> AplicarAsync(PagoAAplicar pago, FormaDeAplicacion forma = FormaDeAplicacion.NonInteractive,
        Recaudador? recaudador = null)
    {
        var r = await Contrato().AplicarAsync(new SolicitudDeAplicacion(recaudador ?? Prueba, forma, [pago], []), default);
        Db.ChangeTracker.Clear();
        return r.Payments.Should().ContainSingle().Subject;
    }

    /// <summary>Aplica y falla la prueba si no quedó aplicado.</summary>
    public async Task<ComprobanteDeAplicacionDto> AplicadoAsync(PagoAAplicar pago, FormaDeAplicacion forma = FormaDeAplicacion.NonInteractive)
    {
        var r = await AplicarAsync(pago, forma);
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        return r.Value!;
    }

    /// <summary>Mueve el reloj y causa con el ciclo diario hasta esa fecha.</summary>
    public async Task CausarHastaAsync(DateOnly fecha)
    {
        await C.CausarHastaAsync(fecha);
        Db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<string>> EventosAsync() => await G.EventosAsync();

    public async Task<LoanOperation> OperacionAsync(Guid publicId) => await Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == publicId);

    /// <summary>El libro de Cartera del crédito cuadra con su reconstrucción hasta la marca (T33, data-model §5.3).</summary>
    public async Task ElLibroCuadraAsync(int? loanId = null)
    {
        var id = loanId ?? Credito.Id;
        var marca = (await C.ControlAsync(id)).LastAccruedDate;
        var cargado = await C.Cargador().CargarAsync(id, null, marca, default);
        cargado!.Listo.Should().BeTrue(cargado.Error?.Message);
        var libro = (await C.Cargador().LibroAsync([id], marca, default))[id];
        Domain.Lending.MotorDeCartera.Verificar(cargado.Entrada!, marca, libro).Should().BeEmpty("el libro cuadra con la reconstrucción");
    }
}
