using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// El escenario de los aportes (feature 014, US3, T092): el de <see cref="CuentasDePrueba"/> (hoy 2026-12-01, aportes y captaciones activados desde el
/// 2026-11-01) con la línea de aportes de cuota mensual fija de 50.000 que vence el 5, mínimo no reducible de 500.000 que se completa primero y que admite
/// extraordinarios; el contrato de cuentas <b>real</b> con sus piezas —el registro de recaudadores sustituido (<c>TST</c> y <c>NOM</c> con
/// <c>ProveedorDeCuentas</c> v1), la transacción «en curso» (InMemory no tiene transacciones), Contabilidad iniciada con el mes abierto y la evaluación
/// contable sin hallazgos—, el generador de cuotas y la consulta de aportes. (nuevo)
/// </summary>
internal sealed class AportesDePrueba : IDisposable
{
    public static readonly Recaudador Prueba = new(ClavesDeRecaudador.Prueba, ClaseDeRecaudador.Test);

    public AportesDePrueba()
    {
        // Sin valores nacionales salvo los que la prueba siembre (un límite legal sin valor no limita).
        G.Valores.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IngenIA365ERP.Application.Common.NationalValues.ValorNacionalVigente>(new Error("NationalValues.Missing", "Sin valor.")));
        Transaccion.HayEnCurso().Returns(_ => EnTransaccion);
        Registro.RecaudadorAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var codigo = ci.ArgAt<string>(0);
            return Task.FromResult(Recaudadores.TryGetValue(codigo, out var clase)
                ? new ParticipanteVigente(Guid.NewGuid(), codigo, clase, IntegrationParticipantRole.ObligationsCollector, string.Empty,
                    clase == IntegrationParticipantClass.Test, new DateOnly(2026, 1, 1), null,
                    Adopciones.TryGetValue(codigo, out var a) ? a : [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 1)])
                : null);
        });
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, ci.Arg<DateOnly>()))));
        Calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil(Festivos, ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(true, [], [])));
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        Servicios = new ServiceCollection()
            .AddSingleton(G.Mongo)
            .AddSingleton<IApplicationDbContext>(G.Db)
            .AddSingleton(tenant)
            .AddSingleton(NominaTestData.UsuarioDePrueba("cajera@coop", 9))
            .AddSingleton(G.Actor)
            .BuildServiceProvider();
    }

    public CuentasDePrueba C { get; } = new();
    public Sessions.GobiernoDePrueba G => C.G;
    public TestApplicationDbContext Db => C.G.Db;
    public IServiceProvider Servicios { get; }
    public ITransaccionDelRecaudador Transaccion { get; } = Substitute.For<ITransaccionDelRecaudador>();
    public bool EnTransaccion { get; set; } = true;
    public IRegistroDeIntegracion Registro { get; } = Substitute.For<IRegistroDeIntegracion>();
    public IContabilidadParaCartera Contabilidad { get; } = Substitute.For<IContabilidadParaCartera>();
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public ICalendarioHabil Calendario { get; } = Substitute.For<ICalendarioHabil>();
    public List<DateOnly> Festivos { get; } = [];

    /// <summary>US5 (T133): con <c>true</c>, las operaciones ajustan la causación por fecha valor (nulo por defecto: las pruebas de K7 y US4 no causan).</summary>
    public bool ConAjusteDeCausacion { get; set; }

    /// <summary>El motor de aprobaciones (US4): nulo, la atribución excedida responde «pendiente» sin solicitud.</summary>
    public IMotorDeAprobaciones? Aprobaciones { get; set; }

    public Dictionary<string, IntegrationParticipantClass> Recaudadores { get; } = new()
    {
        [ClavesDeRecaudador.Prueba] = IntegrationParticipantClass.Test,
        [ClavesDeRecaudador.Nomina] = IntegrationParticipantClass.Payroll,
    };

    public Dictionary<string, IReadOnlyList<VersionAdoptada>> Adopciones { get; } = [];

    public void Dispose() => C.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    public FechaContable FechaContable() => new(Contabilidad, Alertas, Db);
    public EmisionDeCartera Emision() => new(Db, new EmisorDeMensajes(Db, G.Actor, G.Reloj), FechaContable(), new ModoDePasoDeCartera(Db, Contabilidad), G.Actor);
    public NumeradorDeCartera Numerador() => new(Db, C.Cerrojo);
    public RegistroDeCuentas RegistroDeCuentas() => new(Db);
    public ReglasDeAportes Reglas() => new(Db, C.Parametros(), G.Valores);
    public ReglasDeMovimientos Movimientos() => new(Db, C.Parametros(), G.Valores, Calendario, G.Actor, G.Reloj, Aprobaciones);
    public CargosDeCuentas Cargos() => new(Db, RegistroDeCuentas(), Numerador(), Emision(), G.Actor, G.Reloj);
    public ConsignacionEnCuentas Consignacion() =>
        new(Db, RegistroDeCuentas(), Numerador(), Emision(), Reglas(), C.Parametros(), G.Actor, G.Reloj, Movimientos(), Cargos(), Ajuste(), Cdat?.Invoke(),
            Reactivacion());

    /// <summary>US14 (T208): la reactivación de una cuenta inactiva por un movimiento del titular, con su reclasificación.</summary>
    public Application.Lending.Deposits.Accrual.ReactivacionDeCuenta Reactivacion() =>
        new(new Application.Lending.Deposits.Holds.ReclasificacionDeCuenta(Db, RegistroDeCuentas(), Numerador(), Emision(), G.Actor, G.Reloj), G.Reloj);

    /// <summary>US6 (T170): el fondeo del CDAT por el recaudador; nulo, el contrato no fondea CDAT (las pruebas de K7 y K8).</summary>
    public Func<Application.Lending.Deposits.TermDeposits.ConstitucionDeCdat>? Cdat { get; set; }
    public ReversionDeCuentas Reversion() => new(Db, RegistroDeCuentas(), Numerador(), Emision(), G.Actor, G.Reloj, alertas: Alertas, ajusteDeCausacion: Ajuste());
    public RetiroDeCuenta Retiro() => new(Db, RegistroDeCuentas(), Numerador(), Emision(), Movimientos(), Cargos(), G.Actor, G.Reloj, Ajuste(), Reactivacion());
    public TrasladoDeCuenta Traslado() => new(Db, RegistroDeCuentas(), Numerador(), Emision(), Movimientos(), Retiro(), Consignacion(), G.Actor, G.Reloj, Ajuste(),
        Reactivacion());

    // US5 (T131–T136): el interés de las cuentas de captación.
    public Application.Lending.Deposits.Accrual.LectorDeInteres LectorDeInteres() => new(Db, G.Valores);
    public Application.Lending.Deposits.Accrual.AjusteDeCausacionDeCuenta? Ajuste() =>
        ConAjusteDeCausacion ? new(Db, LectorDeInteres(), Emision(), RegistroDeCuentas()) : null;
    public Application.Lending.Deposits.Accrual.CausacionDeCaptaciones Causacion() => new(Db, RegistroDeCuentas(), LectorDeInteres(),
        new Application.Lending.Deposits.Accrual.PartesDeHechosDeCaptaciones(Db, Emision()), Emision(), FechaContable(), C.Cerrojo, G.Reloj, Alertas);
    public Application.Lending.Deposits.Accrual.LiquidacionDeIntereses Liquidacion() => new(Db, RegistroDeCuentas(), LectorDeInteres(),
        new Application.Lending.Deposits.Accrual.ReglasDeRetencion(C.Parametros(), G.Valores), Numerador(), Emision(), G.Actor, G.Reloj);
    public Application.Lending.Deposits.Accrual.LiquidacionALaFecha LiquidacionALaFecha() =>
        new(Db, Liquidacion(), Causacion(), RegistroDeCuentas(), Numerador(), Emision(), G.Actor, G.Reloj);
    public AuditoriaDeEscriturasDeCartera Auditoria() => new(Servicios, Db);
    public ValidacionPreviaContableDeCartera ValidacionPrevia() => new(Contabilidad, G.Parametros(), G.Reloj);

    public ContratoDeCuentasDeCartera Contrato() => new(Db, Transaccion, Registro, ValidacionPrevia(), C.Cerrojo, Consignacion(), Reversion(),
        RegistroDeCuentas(), C.Parametros(), Auditoria(), new SolicitudDeConsignacionValidator(), new SolicitudDeReversoDeCuentaValidator(),
        new SolicitudDeExigiblesValidator(), new SolicitudDeSimulacionDeCuentaValidator(), new SolicitudDeConsultaDeCuentasValidator(), G.Reloj,
        Movimientos(), Retiro(), Traslado(), Cargos(), new SolicitudDeRetiroValidator(), new SolicitudDeTrasladoValidator());

    public GeneradorDeCuotas Generador() => new(Db, RegistroDeCuentas(), G.Valores);

    public ConsultaDeAportesDeCartera Consulta() => new(Db, Reglas());

    // ------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>La línea de aportes del escenario (cuota fija mensual que vence el 5, mínimo de 500.000 primero, extraordinarios).</summary>
    public static ContributionLineVersionInput Version(decimal cuota = 50_000m, bool extraordinarios = true, PermanentSavingsShareInput? permanente = null,
        ContributionAmountInput? minimo = null, ContributionInstallmentInput? forma = null) => new()
    {
        ValidFrom = CuentasDePrueba.Hoy,
        Reason = "Estatutos, art. 20",
        MinimumNonReducible = minimo ?? new ContributionAmountInput(ContributionAmountMode.FixedAmount, 500_000m),
        Installment = forma ?? new ContributionInstallmentInput { Kind = ContributionAmountMode.FixedAmount, Amount = cuota, DueDay = 5 },
        PermanentSavingsShare = permanente,
        AllowsExtraordinary = extraordinarios,
        RefundTermDays = 60,
    };

    /// <summary>Abre la cuenta de aportes de <paramref name="persona"/> en la línea (crea la línea si no se da).</summary>
    public async Task<MemberAccountDto> CuentaDeAportesAsync(Person persona, LendingLineDto? linea = null)
    {
        linea ??= await C.LineaDeAportesAsync(propia: Version());
        return await C.AbiertaAsync(C.Apertura(linea, persona));
    }

    /// <summary>Corre el generador de cuotas para la cuenta el día <paramref name="d"/>.</summary>
    public async Task<IReadOnlyList<Application.Lending.Deposits.Accrual.FalloDeCuenta>> GenerarAsync(MemberAccountDto cuenta, DateOnly d)
    {
        var id = await Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var r = await Generador().ProcesarAsync(new LendingProcessRun { Id = 1 }, d, [id], default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public static string Clave() => $"{ClavesDeRecaudador.Prueba}:{Guid.NewGuid():N}";

    public ConsignacionAAplicar Consignacion(MemberAccountDto cuenta, decimal valor, ModalidadDeConsignacion modalidad = ModalidadDeConsignacion.Installments,
        DateOnly? fechaValor = null, string? clave = null, IReadOnlyList<Guid>? cuotas = null,
        ClaseDeMedioDeRecaudo medio = ClaseDeMedioDeRecaudo.Cash) =>
        new(clave ?? Clave(), cuenta.PublicId, modalidad, valor, fechaValor ?? G.HoyLocal, medio, cuenta.Holder.PersonPublicId,
            new ReferenciaDeRecibo("RC-1", fechaValor ?? G.HoyLocal, null, null, "Recibo"), cuotas);

    public async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> ConsignarAsync(ConsignacionAAplicar c, Recaudador? recaudador = null)
    {
        var r = await Contrato().ConsignarAsync(new SolicitudDeConsignacion(recaudador ?? Prueba, FormaDeAplicacion.NonInteractive, [c], []), default);
        Db.ChangeTracker.Clear();
        return r.Deposits.Should().ContainSingle().Subject;
    }

    public async Task<ComprobanteDeCuentaDto> ConsignadaAsync(ConsignacionAAplicar c, Recaudador? recaudador = null)
    {
        var r = await ConsignarAsync(c, recaudador);
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        return r.Value!;
    }

    public async Task<IReadOnlyList<ExigibleDeCuentaDto>> ExigiblesAsync(Person persona, DateOnly? fecha = null)
    {
        var r = await Contrato().ListarExigiblesAsync(new SolicitudDeExigibles(Prueba, [persona.PublicId], fecha ?? G.HoyLocal), default);
        r.Result.Should().Be(ResultadoDelContrato.Answered, $"{r.Code}: {r.Message}");
        return r.Value!.Installments;
    }

    public async Task<IReadOnlyList<string>> HechosAsync() =>
        await Db.IntegrationMessages.AsNoTracking().OrderBy(m => m.Id).Select(m => m.Type).ToListAsync();

    public async Task<IReadOnlyList<string>> EventosAsync() => await G.EventosAsync();
}
