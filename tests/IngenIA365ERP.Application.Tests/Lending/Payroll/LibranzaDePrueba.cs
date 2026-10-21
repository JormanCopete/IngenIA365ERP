using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Application.Lending.Payroll.NotApplied;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Application.Lending.Payroll.Tasks;
using IngenIA365ERP.Application.Lending.Payroll.Transfers;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// El escenario de la libranza (feature 013, K2, US10, bloque B): el de <see cref="PagosDePrueba"/> —InMemory, hoy local 2026-12-01, el crédito de Ana
/// por 3.000.000 a 24 cuotas mensuales desembolsado hoy, Cartera activada, el contrato de obligaciones real— con
/// <list type="bullet">
/// <item>la cooperativa como persona jurídica (con el NIT de la empresa) y dos pagadurías (personas jurídicas): la Secretaría de Educación y una
/// Gobernación;</item>
/// <item>los límites legales <c>LIBRANZA_CAPACIDAD_DESCUENTO</c> 0,5, <c>LIBRANZA_VENTANA_GIRO_DIAS</c> 10 y <c>LIBRANZA_UMBRAL_MORA_PAGADURIA</c>
/// 30, los cinco techos de cobranza y el perfil regulatorio con la cooperativa registrada como operadora de libranza;</item>
/// <item>un formato de ida (ámbito 5) y uno de vuelta (ámbito 6), genéricos y delimitados;</item>
/// <item>el convenio <c>PAGMUN01</c> de la Secretaría (corte 20, envío 25, pago 30, tope 40 %, regla «pasar a caja», códigos 00 descontado y 07
/// licencia) y, si se pide, el crédito de Ana vinculado a él;</item>
/// <item>la cola de mensajes al deudor real con las plantillas <c>NOAPLICPAG</c> y <c>PASOACAJA</c> y el almacén de adjuntos como doble.</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class LibranzaDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    private LibranzaDePrueba(PagosDePrueba p)
    {
        P = p;
        G.Limite(CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, 0.5m, "Ley 1527 de 2012");
        G.Limite(CatalogoDeLimitesLegales.LibranzaVentanaGiroDias, 10m, "Circular Básica Contable y Financiera");
        G.Limite(CatalogoDeLimitesLegales.LibranzaUmbralMoraPagaduria, 30m, "Circular Básica Contable y Financiera");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, 8m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinSabado, 15m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaContactosDia, 1m, "Ley 2300 de 2023");
        ConRegistroDeOperador(true);
        G.Reloj.AhoraLocal.Returns(_ => new DateTimeOffset(DateTime.SpecifyKind(G.UtcNow, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(-5)));
        Calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil([], ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        Canales.Resolver(Arg.Any<string>()).Returns(ci => new CanalesDeMensajeria.CanalNoInstalado(ci.Arg<string>()));
        Canales.Codigos.Returns(_ => []);
        Sender.Send(Arg.Do<UploadAttachmentCommand>(Subidas.Add), Arg.Any<CancellationToken>()).Returns(_ => Result.Success(Guid.NewGuid()));

        Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        Cooperativa = Juridica("Cooperativa de prueba", "900123456");
        Pagaduria = Juridica("Secretaría de Educación", "800111222");
        OtraPagaduria = Juridica("Gobernación del Valle", "800333444");
        Ida = Formato("PAGMUN-IDA", BankFileScope.PayrollDeductionRequest,
            ("Documento", "PayeeDocument"), ("Empleado", "PayerEmployeeId"), ("Credito", "LoanNumber"), ("Cuota", "InstallmentNumber"),
            ("Valor", "Amount"), ("Novedad", "NoveltyKind"));
        Vuelta = Formato("PAGMUN-VUELTA", BankFileScope.PayrollDeductionResult,
            ("Credito", "LoanNumber"), ("Codigo", "ResultCode"), ("Descontado", "DeductedAmount"), ("Fecha", "DeductionDate"));
        Plantilla("NOAPLICPAG", "La cuota de su crédito {{NumeroDelCredito}} no fue descontada",
            "Señor(a) {{NombreDelTitular}}: la cuota del crédito {{NumeroDelCredito}} no fue descontada ({{MotivoDeNoDescuento}}). Puede pagarla por {{FormaDePagoNueva}}.");
        Plantilla("PASOACAJA", "Cambio en la forma de pago de su crédito {{NumeroDelCredito}}",
            "Señor(a) {{NombreDelTitular}}: desde ahora las cuotas del crédito {{NumeroDelCredito}} se pagan por {{FormaDePagoNueva}}.");
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Arma el escenario; con <paramref name="conConvenio"/> crea <c>PAGMUN01</c> y con <paramref name="vincular"/> le vincula el crédito de Ana.</summary>
    public static async Task<LibranzaDePrueba> CrearAsync(bool conConvenio = true, bool vincular = true)
    {
        var l = new LibranzaDePrueba(await PagosDePrueba.CrearAsync());
        l.G.Conceder();
        if (conConvenio) l.Convenio = await l.ConvenioAsync("PAGMUN01", l.Pagaduria);
        if (conConvenio && vincular) l.Vincular(l.Credito, l.Convenio, "EMP-0001");
        return l;
    }

    public PagosDePrueba P { get; }
    public Disbursements.DesembolsoDePrueba D => P.D;
    public Sessions.GobiernoDePrueba G => P.G;
    public TestApplicationDbContext Db => P.Db;
    public Loan Credito => P.Credito;
    public Person Ana => D.O.Ana;

    public Person Cooperativa { get; }
    public Person Pagaduria { get; }
    public Person OtraPagaduria { get; }
    public BankFileFormat Ida { get; }
    public BankFileFormat Vuelta { get; }
    public PayrollAgreement Convenio { get; set; } = null!;

    public ISender Sender { get; } = Substitute.For<ISender>();
    public List<UploadAttachmentCommand> Subidas { get; } = [];
    public ICalendarioHabil Calendario { get; } = Substitute.For<ICalendarioHabil>();
    public ICanalesDeMensajeria Canales { get; } = Substitute.For<ICanalesDeMensajeria>();
    public List<AlertaALevantar> Alertas => D.O.L.Levantadas;

    public void Dispose() => P.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public ConveniosDeLibranza Convenios() => new(Db, D.Parametros(), G.Valores, G.Perfil);
    public AltaDeVersionDeConvenio Alta() => new(Db, G.Valores);
    public CreatePayrollAgreementCommandHandler Crear() => new(Db, Convenios(), Alta(), G.Reloj);
    public AddPayrollAgreementVersionCommandHandler Versionar() => new(Db, Alta(), G.Reloj);
    public SetPayrollAgreementContactsCommandHandler Contactos() => new(Db, G.Reloj);
    public GetPayrollAgreementQueryHandler Leer() => new(Db, G.Reloj);
    public ListPayrollAgreementsQueryHandler Listar() => new(Db, G.Reloj);
    public ListPendingPayrollTransfersQueryHandler Pendientes() => new(Db, G.Alcance, G.Reloj);

    public PayrollDeductionLines Lineas() => new(Db, P.C.Cargador(), Convenios(), G.Reloj);
    public PreparacionDeArchivoDeDescuentos Preparacion() => new(Db, Convenios(), Lineas());
    public GeneratePayrollDeductionFileCommandHandler Generar() =>
        new(Db, Preparacion(), D.Numerador(), D.O.Cerrojo, D.Emision(), Sender, G.Auditoria, G.Actor, G.Reloj);
    public PreviewPayrollDeductionFileQueryHandler VistaPrevia() => new(Db, Preparacion());
    public VoidPayrollDeductionFileCommandHandler Anular() => new(Db, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public ListPayrollDeductionFilesQueryHandler ListarArchivos() => new(Db, G.Alcance);
    public GetPayrollDeductionFileQueryHandler LeerArchivo() => new(Db, G.Alcance, G.Auditoria);

    public ColaDeMensajesAlDeudor Cola() =>
        new(Db, G.Reloj, D.Parametros(), Calendario, D.O.Autorizaciones, Canales, Options.Create(new CanalesExternosOptions { AmbienteDePruebas = true }));
    public SeguimientoDeNoAplicados Seguimiento() => new(Db, Cola(), G.Reloj);
    public ConsumePayrollFileSentCommandHandler ConsumirEnvio() => new(Db, new MensajesEntrantes(Db), G.Reloj);
    public ConsumePayrollDeductionResultCommandHandler ConsumirResultado() =>
        new(Db, new MensajesEntrantes(Db), D.O.Cerrojo, Convenios(), Seguimiento(), D.Alertas, G.Reloj);

    public ConfirmTestPayrollFileSentCommandHandler ConfirmarEnvio(bool dobleRegistrado = true) =>
        new(Db, D.Emisor(), D.Emision(), G.Actor, G.Reloj, dobleRegistrado ? [new RecaudadorDePrueba()] : []);
    public EmitTestPayrollDeductionResultCommandHandler EmitirResultado(bool dobleRegistrado = true) =>
        new(Db, D.Emisor(), D.Emision(), G.Actor, G.Reloj, dobleRegistrado ? [new RecaudadorDePrueba()] : []);

    public TransferLoanPayrollCommandHandler Trasladar() =>
        new(Db, D.O.Cerrojo, P.C.Cargador(), D.Partes(), D.FechaContable(), D.Emision(), D.ValidacionPrevia(), Convenios(), Cola(), G.Alcance,
            G.Auditoria, G.Actor, G.Reloj);
    public ListInstallmentNonDeductionsQueryHandler NoAplicados() => new(Db, G.Alcance);
    public ResolveInstallmentNonDeductionCommandHandler Resolver() => new(Db, Seguimiento(), G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    public AlertPendingPayrollDeductionFilesCommandHandler TareaDeArchivos() => new(Db, D.Alertas, G.Reloj);
    public AlertOverduePayrollTransfersCommandHandler TareaDeGiros() => new(Db, D.Alertas, Cola(), G.Reloj);

    public Application.Lending.Integration.Contrato.NoDescontadosDelLote NoDescontados() => new(Db, P.C.Cargador(), D.Alertas, G.Reloj, Seguimiento());

    // ------------------------------------------------------------------------------------------------------- escenario --

    public void ConRegistroDeOperador(bool registrado) =>
        G.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "CAC", true, 1, null, 2, registrado, registrado ? "OPL-001" : null,
                registrado ? new DateOnly(2026, 1, 1) : null, "Resolución de prueba", null, "Perfil de prueba", null, "Prueba", G.UtcNow.AddYears(-1)));

    public Person Juridica(string razonSocial, string nit)
    {
        var p = new Person { BusinessName = razonSocial, TaxId = nit, IdType = "N", PersonType = "02", Email = "contacto@" + nit + ".test", CreatedBy = "test" };
        Db.People.Add(p);
        Db.SaveChanges();
        return p;
    }

    public BankFileFormat Formato(string codigo, BankFileScope ambito, params (string Nombre, string Origen)[] campos)
    {
        var def = new BankFileFormatDefinition
        {
            Code = codigo, Name = $"Formato {codigo}", Scope = ambito.ToString(), ValidFrom = new DateOnly(2026, 1, 1), Kind = nameof(BankFileKind.Delimited),
            Delimiter = ";", Encoding = "utf-8", AmountFormat = nameof(BankFileAmountFormat.Point2), FileName = codigo + "-{BatchReference}.csv",
        };
        def.Records.Header.Enabled = false;
        def.Records.Trailer.Enabled = false;
        def.Records.Detail.Fields = campos.Select((c, i) => new BankFileFormatDefinition.BankFileFieldDefinition { Order = i + 1, Name = c.Nombre, Source = c.Origen })
            .ToList();
        BankFileFormatValidator.Validar(def).Should().BeEmpty("el formato de la prueba es válido");
        var formato = new BankFileFormat { CreatedBy = "test" };
        def.AplicarA(formato, bankId: null);
        Db.BankFileFormats.Add(formato);
        Db.SaveChanges();
        return formato;
    }

    private void Plantilla(string codigo, string asunto, string cuerpo) =>
        Db.NotificationTemplates.Add(new NotificationTemplate
        {
            TemplateName = $"{codigo} v1", Channel = nameof(MessageChannel.Email), Subject = asunto, BodyTemplate = cuerpo, IsActive = true, Code = codigo,
            Module = "Lending", Purpose = nameof(DebtorMessagePurpose.Informative), Version = 1, ValidFrom = new DateOnly(2020, 1, 1),
            Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(DebtorMessagePurpose.Informative), IsSeeded = true, ReviewPending = true,
            CreatedBy = "semilla",
        });

    /// <summary>La versión por defecto de un convenio: desde el 2026-11-01, corte 20, envío 25, pago 30, tope 40 %, regla «pasar a caja».</summary>
    public PayrollAgreementVersionInput Version(DateOnly? desde = null) => new(desde ?? new DateOnly(2026, 11, 1), 20, 30, Ida.PublicId, Vuelta.PublicId,
        "Convenio firmado", SendDay: 25, SendLeadDays: 5, DeductionCap: 0.4m,
        ResultCodes: [new PayrollResultCodeInput("00", "Descontado", PayrollResultOutcome.Deducted), new PayrollResultCodeInput("07", "Licencia no remunerada",
            PayrollResultOutcome.NotDeducted, NonDeductionReason.Leave)]);

    /// <summary>Crea el convenio por el comando y falla la prueba si no pudo.</summary>
    public async Task<PayrollAgreement> ConvenioAsync(string codigo, Person pagaduria, bool nominaPropia = false, PayrollAgreementVersionInput? version = null)
    {
        var r = await Crear().Handle(new CreatePayrollAgreementCommand(codigo, "Convenio " + codigo, pagaduria.PublicId, nominaPropia, version ?? Version())
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return await Db.PayrollAgreements.AsNoTracking().SingleAsync(a => a.PublicId == r.Value.PublicId);
    }

    /// <summary>Deja el vínculo vigente del crédito como libranza del convenio (desde el desembolso, con su fecha de libranza).</summary>
    public void Vincular(Loan credito, PayrollAgreement convenio, string? empleado, RepaymentChannel forma = RepaymentChannel.PaymasterDeduction)
    {
        var v = Db.LoanPayrollLinks.Single(x => x.LoanId == credito.Id && x.ValidTo == null);
        v.RepaymentChannel = forma;
        v.PaymasterPersonId = forma == RepaymentChannel.OwnPayroll ? credito.DebtorPersonId : convenio.PaymasterPersonId;
        v.IsOwnPayroll = forma == RepaymentChannel.OwnPayroll;
        v.PayrollAgreementId = convenio.Id;
        v.EmployeeIdAtPaymaster = empleado;
        v.LibranzaDate = v.ValidFrom;
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Otro crédito desembolsado hoy (Beto o Darío), vinculado al convenio.</summary>
    public async Task<Loan> OtroCreditoAsync(Person deudor, decimal monto, string empleado, PayrollAgreement? convenio = null)
    {
        G.ActuarComo(D.O.Asesora);
        var credito = await P.C.DesembolsarAsync(D.O.Solicitud(monto, 12, deudor));
        Vincular(credito, convenio ?? Convenio, empleado);
        G.Conceder();
        return credito;
    }

    /// <summary>Genera el archivo del período y falla la prueba si no pudo.</summary>
    public async Task<PayrollDeductionFileDto> ArchivoAsync(int anio = 2027, int mes = 1, PayrollAgreement? convenio = null)
    {
        G.ActuarComo(D.Cajera);
        var r = await Generar().Handle(new GeneratePayrollDeductionFileCommand((convenio ?? Convenio).PublicId, anio, mes) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return r.Value;
    }

    /// <summary>El recaudador de prueba confirma el envío del archivo y Cartera lo consume.</summary>
    public async Task<ResultadoDeConsumo> ConfirmarYConsumirAsync(Guid archivo, string? radicado = "RAD-001")
    {
        G.ActuarComo(D.Cajera);
        var r = await ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(archivo, "Portal", radicado) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        var c = await ConsumirEnvio().Handle(new ConsumePayrollFileSentCommand([r.Value.MessagePublicId]), default);
        Db.ChangeTracker.Clear();
        return c.Value;
    }

    /// <summary>La respuesta de la pagaduría (formato de vuelta: crédito;código;descontado;fecha) que carga quien prueba.</summary>
    public static byte[] Respuesta(params (string Credito, string Codigo, decimal Descontado, DateOnly? Fecha)[] filas) =>
        System.Text.Encoding.UTF8.GetBytes(string.Join("\n", filas.Select(f =>
            $"{f.Credito};{f.Codigo};{f.Descontado.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)};{f.Fecha?.ToString("yyyyMMdd") ?? string.Empty}"))
            + "\n");

    /// <summary>El recaudador de prueba devuelve el resultado y Cartera lo consume.</summary>
    public async Task<ResultadoDeConsumo> ResultadoYConsumoAsync(Guid archivo, byte[] respuesta)
    {
        G.ActuarComo(D.Cajera);
        var r = await EmitirResultado().Handle(new EmitTestPayrollDeductionResultCommand(archivo, "respuesta.csv", respuesta) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        G.ActuarComoProceso();
        var c = await ConsumirResultado().Handle(new ConsumePayrollDeductionResultCommand([r.Value.MessagePublicId]), default);
        Db.ChangeTracker.Clear();
        c.IsSuccess.Should().BeTrue(c.IsFailure ? $"{c.Error.Code}: {c.Error.Message}" : string.Empty);
        return c.Value;
    }

    /// <summary>La línea del archivo de un crédito.</summary>
    public async Task<PayrollDeductionFileLine> LineaAsync(Guid archivo, Loan credito) =>
        await (from l in Db.PayrollDeductionFileLines.AsNoTracking()
               join f in Db.PayrollDeductionFiles.AsNoTracking() on l.PayrollDeductionFileId equals f.Id
               where f.PublicId == archivo && l.LoanId == credito.Id
               select l).SingleAsync();
}
