using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// El escenario del desembolso (feature 013, US3, bloque 7A): el de <see cref="OriginacionDePrueba"/> (InMemory, hoy local 2026-12-01, línea
/// <c>CONLIB</c> al 20 % EA, usura de consumo 25 %, Ana, Beto, Carla, la oficina Norte, Cartera activada, la matriz) con
/// <list type="bullet">
/// <item>la solicitud de Ana radicada por la asesora y aprobada por el analista (<see cref="AprobadaAsync"/>);</item>
/// <item>la cajera, con persona, que desembolsa (no intervino: radicar y desembolsar no chocan);</item>
/// <item>un banco con código ACH, la mora sobre intereses no permitida por la ley (límite 0) y Contabilidad iniciada con el mes abierto y la
/// evaluación contable «no disponible» (<c>NotApplicable</c>), salvo que la prueba lo cambie;</item>
/// <item>el contrato de obligaciones como doble (lo construye la fase 9) y el motor provisional del crédito (fase 8).</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class DesembolsoDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = OriginacionDePrueba.Hoy;

    private DesembolsoDePrueba(OriginacionDePrueba o)
    {
        O = o;
        Cajera = o.UsuarioConPersona("Cajera", "1001003");
        Banco = new Bank { Name = "Banco de prueba", TransferCode = "0007", CreatedBy = "test" };
        Db.Banks.Add(Banco);
        Db.SaveChanges();
        G.Limite(CatalogoDeLimitesLegales.MoraSobreInteresesPermitida, 0m, "Código de Comercio");
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, ci.Arg<DateOnly>()))));
        Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeEvaluacionContableDto>(new Error(ContabilidadParaCarteraProvisional.CodigoEvaluacionNoDisponible, "no disponible")));
        Contrato.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeAplicacion>();
            Aplicadas.Add(s);
            return Task.FromResult(new RespuestaDeAplicacionDto(s.Payments.Select(p => new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(
                ResultadoDelContrato.Applied, "PagoAplicado", "Aplicado", Comprobante(p))).ToList(), []));
        });
    }

    public static async Task<DesembolsoDePrueba> CrearAsync(CreditLineVersionInput? version = null, DateOnly? usuraHasta = null) =>
        new(await OriginacionDePrueba.CrearAsync(version: version, usuraHasta: usuraHasta));

    public OriginacionDePrueba O { get; }
    public Application.Tests.Lending.Sessions.GobiernoDePrueba G => O.G;
    public Application.Tests.Common.TestApplicationDbContext Db => O.Db;
    public IContabilidadParaCartera Contabilidad => O.Contabilidad;
    public IContratoDeObligaciones Contrato { get; } = Substitute.For<IContratoDeObligaciones>();
    public List<SolicitudDeAplicacion> Aplicadas { get; } = [];
    public IAlertas Alertas => O.L.Alertas;
    public User Cajera { get; }
    public Bank Banco { get; }
    public IMotorDeCreditoEnDesembolso? Motor { get; set; }

    public void Dispose() => O.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public ReglasDeParametrosDeCartera Parametros() => O.Parametros();
    public FechaContable FechaContable() => new(Contabilidad, Alertas);
    public EmisorDeMensajes Emisor() => new(Db, G.Actor, G.Reloj);
    public EmisionDeCartera Emision() => new(Db, Emisor(), FechaContable(), new ModoDePasoDeCartera(Db, Contabilidad), G.Actor);
    public IMotorDeCreditoEnDesembolso MotorDelCredito() => Motor ?? new MotorDeCreditoEnDesembolsoProvisional(Db);
    public ValidacionPreviaContableDeCartera ValidacionPrevia() => new(Contabilidad, G.Parametros(), G.Reloj);
    public RequisitosDelDesembolso Requisitos() =>
        new(Db, O.Simulador(), O.Concentracion(), O.Bloqueos, G.Intervenciones(), O.Guardia(), Parametros(), G.Parametros(), ValidacionPrevia(),
            MotorDelCredito(), Emision(), G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public RegistroDeCartera Registro() => new(Db);
    public NumeradorDeCartera Numerador() => new(Db, O.Cerrojo);
    public TramoDeDesembolso Tramos() => new(Db, Registro(), Numerador(), Parametros(), new CalendarioDeDiasHabiles(Db), G.Actor, G.Reloj);
    public ReversoDeDesembolso Reverso() => new(Db, Registro(), Numerador(), MotorDelCredito(), Emision(), FechaContable(), G.Reloj);

    public ExecuteLoanDisbursementCommandHandler Desembolsar() =>
        new(Db, Requisitos(), Tramos(), O.Cerrojo, Registro(), Numerador(), new RecogidaEnElDesembolso(Contrato), MotorDelCredito(), Emision(),
            FechaContable(), G.Intervenciones(), G.Auditoria, G.Actor, G.Reloj);
    public PreviewDisbursementQueryHandler VistaPrevia() => new(Requisitos());
    public VoidDisbursementOrderCommandHandler AnularOrden() =>
        new(Db, O.Cerrojo, Emision(), FechaContable(), G.Intervenciones(), G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public ReissueDisbursementOrderCommandHandler GirarDeNuevo() =>
        new(Db, Tramos(), O.Cerrojo, Emision(), FechaContable(), G.Intervenciones(), G.Alcance, G.Auditoria, G.Reloj);
    public VoidPendingLoanCommandHandler AnularCredito() => new(Db, Contrato, O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public ConsumeTreasuryResultCommandHandler Consumir() => new(Db, new MensajesEntrantes(Db), O.Cerrojo, Reverso(), G.Reloj);
    public EmitTestDisbursementResultCommandHandler EmitirResultado(bool dobleRegistrado = true) =>
        new(Db, Emisor(), Emision(), dobleRegistrado ? [new TesoreriaDePrueba()] : []);
    public ListTestTreasuryOrdersQueryHandler OrdenesDePrueba(bool dobleRegistrado = true) => new(Db, dobleRegistrado ? [new TesoreriaDePrueba()] : []);

    // ------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Radica (asesora) y aprueba (analista o gerencia) la solicitud; falla la prueba si no pudo.</summary>
    public async Task<CreditApplicationDto> AprobadaAsync(CreditApplicationInput? entrada = null)
    {
        G.ActuarComo(O.Asesora);
        var s = await O.RadicadaAsync(entrada);
        G.ActuarComo(O.Analista);
        G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst", "Lending.Decisions.Management");
        var huella = await O.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);
        huella.IsSuccess.Should().BeTrue(huella.IsFailure ? $"{huella.Error.Code}: {huella.Error.Message}" : string.Empty);
        var r = await O.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId, CreditDecisionOutcome.Approve,
            null, null, huella.Value.ContentSha256) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        G.ActuarComo(Cajera);
        G.Conceder();
        return s;
    }

    /// <summary>La entrada por defecto: transferencia a la cuenta de ahorros de Ana en el banco de prueba.</summary>
    public DisbursementInput Entrada(CreditApplicationDto s) => new()
    {
        ApplicationPublicId = s.PublicId,
        Form = DisbursementForm.Transfer,
        Beneficiary = new BeneficiaryInput(O.Ana.PublicId, Banco.PublicId, BeneficiaryInput.Ahorros, "0123456789"),
    };

    public async Task<Result<DisbursementResultDto>> DesembolsarAsync(DisbursementInput entrada, bool limpiar = true)
    {
        var r = await Desembolsar().Handle(new ExecuteLoanDisbursementCommand(entrada) { OperationKey = Guid.NewGuid() }, default);
        if (limpiar) Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Desembolsa y falla la prueba si no pudo.</summary>
    public async Task<DisbursementResultDto> DesembolsadoAsync(DisbursementInput entrada)
    {
        var r = await DesembolsarAsync(entrada);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Publica desde mañana una versión de <c>CONLIB</c> con <paramref name="reglas"/> y pasa el reloj a mañana.</summary>
    public async Task VersionDesdeMananaAsync(params CollateralRuleInput[] reglas)
    {
        var r = await O.L.Versionar().Handle(new AddCreditLineVersionCommand(O.Linea.PublicId,
            OriginacionDePrueba.VersionDeLinea() with { ValidFrom = Hoy.AddDays(1), CollateralRules = reglas }) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        Manana();
    }

    public void Manana()
    {
        G.HoyLocal = Hoy.AddDays(1);
        G.UtcNow = G.UtcNow.AddDays(1);
    }

    /// <summary>Un rol de la cajera con <c>Lending.Disbursements.Execute</c> y el monto operativo dado.</summary>
    public void MontoOperativo(decimal maximo)
    {
        var permiso = new Permission { Resource = "Lending.Disbursements", Action = "Execute" };
        Db.Permissions.Add(permiso);
        var rol = new Role { Code = "CAJA", Name = "Caja", IsActive = true };
        Db.Roles.Add(rol);
        Db.SaveChanges();
        Db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = permiso.Id });
        Db.UserRoles.Add(new UserRole { UserId = Cajera.Id, RoleId = rol.Id });
        Db.PermissionAmountLimits.Add(new PermissionAmountLimit
        {
            RoleId = rol.Id, PermissionCode = RequisitosDelDesembolso.PermisoDeDesembolsar, MaxAmount = maximo, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    private static ComprobanteDeAplicacionDto Comprobante(PagoAAplicar p) => new(Guid.NewGuid(), "PAG-" + p.ObligationPublicId.ToString("N")[..6], p.ObligationPublicId,
        "CR", p.IdempotencyKey, p.Modality, p.ValueDate, p.ValueDate, DateTime.UtcNow, p.Amount, p.Amount, 0m, 0m, 0m, [],
        new SaldosDeObligacionDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0), null, p.Modality == ModalidadDePago.FullPayoff, p.ValueDate, "h", "LND-2026.1", []);

    public static System.Text.Json.JsonElement Datos(Error error) => OriginacionDePrueba.Datos(error);

    public static NullLogger<T> Log<T>() => NullLogger<T>.Instance;
}
