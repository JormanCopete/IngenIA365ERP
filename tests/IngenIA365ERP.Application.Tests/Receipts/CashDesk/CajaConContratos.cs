using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Tests.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Receipts;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// La caja de las pruebas de la fase 6 (feature 015, US3, T0102–T0106) sobre <see cref="CajaDePrueba"/>: los contratos de obligaciones y de cuentas simulados
/// (por defecto aplican todo lo que reciben), una persona asociada con dos créditos y una cuenta de aportes, y un concepto vario. La sesión de la cajera se
/// abre con 500.000 de base. Las pruebas se escriben ahora y se corren al final del plan.
/// </summary>
internal sealed class CajaConContratos
{
    public CajaDePrueba C { get; } = new();
    public IContratoDeObligaciones Obligaciones { get; } = Substitute.For<IContratoDeObligaciones>();
    public IContratoDeCuentas Cuentas { get; } = Substitute.For<IContratoDeCuentas>();
    public IListasDeRecaudo Listas { get; } = Substitute.For<IListasDeRecaudo>();
    public IBusquedaDeObligaciones Busqueda { get; } = Substitute.For<IBusquedaDeObligaciones>();

    public Guid CreditoA { get; } = Guid.NewGuid();
    public Guid CreditoB { get; } = Guid.NewGuid();
    public Guid Aportes { get; } = Guid.NewGuid();
    public Person Asociado { get; }
    public MiscConcept Concepto { get; }
    public CashSessionDto Sesion { get; private set; } = null!;

    public CajaConContratos()
    {
        Asociado = new Person { FirstName = "Ana", LastName = "Asociada", TaxId = "2001", IsAssociate = true };
        C.E.Db.People.Add(Asociado);
        Concepto = new MiscConcept { Code = "CERT", Name = "Certificado", Direction = MiscConceptDirection.Income };
        C.E.Db.MiscConcepts.Add(Concepto);
        C.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RC", Name = "Recibo de caja", Kind = ReceiptDocumentKind.CashReceipt });
        C.E.Db.SaveChanges();
        Listas.EvaluarAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ScreeningOutcomeR.Clear);

        Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            var alternativas = s.Modality == ModalidadDePago.ExtraordinaryPrincipal && s.Choice is null
                ? new[] { Simulacion(s, EleccionDeAbono.ReduceTerm, []), Simulacion(s, EleccionDeAbono.ReduceInstallment, []) }
                : [];
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "", Simulacion(s, s.Choice, alternativas));
        });
        Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDeAplicacionDto(ci.Arg<SolicitudDeAplicacion>().Payments.Select(Aplicado).ToList(), []));
        Cuentas.SimularAsync(Arg.Any<SolicitudDeSimulacionDeCuenta>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<SimulacionDeCuentaDto>(ResultadoDelContrato.Answered, "OK", "", new SimulacionDeCuentaDto("sim-cuenta", true, Disponible(),
                Disponible(), null, [], [], null, false, [], [])));
        Cuentas.ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDeConsignacionDto(ci.Arg<SolicitudDeConsignacion>().Deposits.Select(Consignado).ToList(), []));
        Cuentas.ConsultarAsync(Arg.Any<SolicitudDeConsultaDeCuentas>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<CuentasDePersonaDto>(ResultadoDelContrato.Answered, "OK", "", new CuentasDePersonaDto(EstadoDeCartera.Active,
                EstadoDeCartera.Active, ci.Arg<SolicitudDeConsultaDeCuentas>().PersonPublicId, EscenarioDeRecaudo.Hoy, [])));
        // Lo que puede pagar (corrida del 2026-10-26): el listado de obligaciones y los exigibles de cuentas, vacíos salvo que la prueba los fije.
        Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "", new ListadoDeObligacionesDto(EstadoDeCartera.Active,
                ci.Arg<SolicitudDeListado>().PersonPublicId, EscenarioDeRecaudo.Hoy, [])));
        Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "OK", "", new ExigiblesDeCuentasDto(EstadoDeCartera.Active,
                EstadoDeCartera.Active, [], [])));
    }

    public static SimulacionDePagoDto Simulacion(SolicitudDeSimulacion s, EleccionDeAbono? eleccion, IReadOnlyList<SimulacionDePagoDto> alternativas) =>
        new("sim-" + s.ObligationPublicId.ToString("N")[..6], s.ObligationPublicId, s.Modality, s.Amount, s.ValueDate, eleccion, s.Amount, 0m, 0m, 0m,
            [new LineaDeDistribucionDto(1, ConceptoDeObligacion.Principal, s.Amount)], Saldos(), null, alternativas, []);

    public static SaldosDeObligacionDto Saldos() => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static DisponibleDto Disponible() => new(0, 0, 0, 0, 0, 0);

    public static RespuestaDelContratoDto<ComprobanteDeAplicacionDto> Aplicado(PagoAAplicar p) =>
        new(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeAplicacionDto(Guid.NewGuid(), "PAG-" + p.IdempotencyKey[^4..], p.ObligationPublicId, "CR-1",
            p.IdempotencyKey, p.Modality, p.ValueDate, p.ValueDate, DateTime.UtcNow, p.Amount, p.Amount, 0m, 0m, 0m, [], Saldos(), null, false,
            p.ValueDate.AddDays(3), "h", "LND-2026.1", []));

    public static RespuestaDelContratoDto<ComprobanteDeCuentaDto> Consignado(ConsignacionAAplicar c) =>
        new(ResultadoDelContrato.Applied, "OK", "", new ComprobanteDeCuentaDto(Guid.NewGuid(), "CON-1", c.AccountPublicId, "AP-1", c.IdempotencyKey, "Deposit",
            c.ValueDate, c.ValueDate, DateTime.UtcNow, c.Amount, [], null, [], [], Disponible(), null, null, false, "h", "DEP-2026.1", []));

    public GetPayablesQueryHandler Pagables() => new(C.E.Db, Obligaciones, Cuentas, Busqueda, C.E.Reloj, new AuditoriaDeRecaudo(C.E.Servicios,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditoriaDeRecaudo>.Instance));

    public CreateCashOperationCommandHandler Crear() => new(C.E.Db, C.E.Alcance, C.E.Reloj);
    public UpdateCashOperationItemsCommandHandler Items() => new(C.E.Db, C.E.Alcance, Obligaciones, Cuentas);
    public UpdateCashOperationMeansCommandHandler Medios() => new(C.E.Db, C.E.Alcance, C.E.Parametros, C.E.Reloj);
    public DeclareSourceOfFundsCommandHandler Declarar() => new(C.E.Db, C.E.Alcance, C.E.Reloj);
    public AbandonCashOperationCommandHandler Abandonar() => new(C.E.Db, C.E.Alcance);

    public ConfirmacionDeRecibo Confirmacion() => new(C.E.Db, C.E.Alcance, C.E.Actor, C.E.Reloj, C.Cerrojo, C.Numerador, C.E.Parametros, C.Presente,
        new Application.Accounting.Receipts.ContabilidadParaRecaudo(Substitute.For<MediatR.ISender>()), Obligaciones, Cuentas, Listas, C.Emision, C.E.Alertas);

    /// <summary>Abre la sesión de la cajera.</summary>
    public async Task AbrirAsync() => Sesion = await C.AbrirAsync();

    /// <summary>Una operación del asociado con sus ítems y sus medios ya imputados.</summary>
    public async Task<CashOperationDto> PreparadaAsync(IReadOnlyList<ItemPedidoDto> items, IReadOnlyList<MedioDeCajaDto> medios, DateOnly? fecha = null,
        string? motivo = null, Guid? soporte = null)
    {
        var op = (await Crear().Handle(new CreateCashOperationCommand(Sesion.PublicId, Asociado.PublicId, ValueDate: fecha, BackdateReason: motivo,
            BackdateSupportAttachmentPublicId: soporte), default)).Value;
        var conItems = await Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, items), default);
        if (conItems.IsFailure) throw new InvalidOperationException(conItems.Error.Code);
        var conMedios = await Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, medios), default);
        if (conMedios.IsFailure) throw new InvalidOperationException(conMedios.Error.Code);
        return conMedios.Value;
    }

    public ItemPedidoDto Cuota(Guid credito, decimal valor) => new(ApplicationTarget.Loan, valor, credito, LoanModality: ModalidadDePago.CycleInstallment);

    public ItemPedidoDto Abono(Guid credito, decimal valor, EleccionDeAbono? eleccion) =>
        new(ApplicationTarget.Loan, valor, credito, LoanModality: ModalidadDePago.ExtraordinaryPrincipal, Choice: eleccion);

    public ItemPedidoDto CuotaDeAportes(decimal valor) => new(ApplicationTarget.Account, valor, Aportes, AccountModality: ModalidadDeConsignacion.Installments);

    public MedioDeCajaDto Efectivo(decimal valor) => new(C.Efectivo.PublicId, valor);

    public MedioDeCajaDto Transferencia(decimal valor, string referencia = "TR-1") => new(C.Transferencia.PublicId, valor, referencia);
}
