using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 028 (T064, T064a; R6, FR-021): <see cref="CuotaEnviadaDelCredito"/> dice, en lote, en qué va la cuota del período en curso de cada crédito: en un
/// archivo de libranza enviado sin respuesta, descontada y pendiente de giro, no descontada, o —si la libranza no dice nada— en la nómina propia (por el
/// puerto de Nómina), con la pagaduría, el convenio, el archivo, el período y los valores de la línea. Lo descontado y girado ya está pagado; lo de otro
/// período no cuenta, salvo lo descontado y sin girar de una línea anterior. El escenario es <see cref="OriginacionDePrueba"/> con créditos de libro de Ana
/// y una pagaduría. (nuevo)
/// </summary>
public sealed class CuotaEnviadaDelCreditoTests : IAsyncLifetime
{
    private static readonly DateOnly Vence = new(2026, 12, 30);
    private static readonly DateOnly Fecha = OriginacionDePrueba.Hoy.AddDays(5);

    private OriginacionDePrueba _o = null!;
    private PayrollAgreement _convenio = null!;
    private readonly IDescuentosDeNominaParaCartera _nomina = Substitute.For<IDescuentosDeNominaParaCartera>();

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        var pagaduria = _o.Persona("Pagaduría", "Departamental", "800100200", null, null);
        _convenio = new PayrollAgreement { Code = "ADP", PaymasterPersonId = pagaduria.Id, CreatedBy = "test" };
        _o.Db.PayrollAgreements.Add(_convenio);
        await _o.Db.SaveChangesAsync();
        _nomina.VivosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DescuentoDeNominaVivoDto>>([]));
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private CuotaEnviadaDelCredito Consulta() => new(_o.Db, _nomina);

    private static CuotaPorConsultar Cuota(Loan credito, int numero = 7) => new(credito.Id, credito.PublicId, numero, Vence);

    /// <summary>Un archivo enviado del período con la línea del crédito (cuota 7, 312.400 corrientes y lo vencido).</summary>
    private PayrollDeductionFileLine Enviada(Loan credito, int anio = 2026, int mes = 12, int sub = 1, string numero = "ADP-000123", decimal vencido = 0m)
    {
        var archivo = new PayrollDeductionFile
        {
            PayrollAgreementId = _convenio.Id, PayrollAgreementVersionId = 1, PeriodYear = (short)anio, PeriodMonth = (byte)mes, SubPeriodNumber = (byte)sub,
            Number = numero, OutboundFormatId = 1, FileName = $"{numero}.txt", FileSha256 = "x", GeneratedAt = _o.G.UtcNow, CreatedBy = "test",
        };
        archivo.MarcarEnviado(_o.G.UtcNow, Guid.NewGuid(), null);
        _o.Db.PayrollDeductionFiles.Add(archivo);
        _o.Db.SaveChanges();
        var linea = new PayrollDeductionFileLine
        {
            PayrollDeductionFileId = archivo.Id, LineNumber = 1, LoanId = credito.Id, LoanPayrollLinkId = 1, PersonId = credito.DebtorPersonId, InstallmentNumber = 7,
            DueDate = Vence, CurrentAmount = 312_400m, PastDueAmount = vencido, TotalAmount = 312_400m + vencido, LibranzaDate = new DateOnly(2026, 1, 15),
            CreatedBy = "test",
        };
        _o.Db.PayrollDeductionFileLines.Add(linea);
        _o.Db.SaveChanges();
        return linea;
    }

    private PayrollDeductionResult Resultado(PayrollDeductionFileLine linea, PayrollResultOutcome resultado, decimal descontado, decimal noDescontado = 0m)
    {
        var r = new PayrollDeductionResult
        {
            PayrollDeductionFileLineId = linea.Id, PayrollDeductionFileId = linea.PayrollDeductionFileId, LoanId = linea.LoanId, InboundMessageId = 1,
            Outcome = resultado, DeductedAmount = descontado, NotDeductedAmount = noDescontado, DeductionDate = Vence.AddDays(-5), CreatedBy = "test",
        };
        _o.Db.PayrollDeductionResults.Add(r);
        _o.Db.SaveChanges();
        return r;
    }

    // ------------------------------------------------------------------------------------------------------------ libranza --

    [Fact]
    public async Task Sin_nada_enviado_no_aparece()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");

        (await Consulta().ConsultarAsync([Cuota(credito)], Fecha, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_linea_del_archivo_enviado_sin_respuesta_es_enviada_sin_respuesta_con_sus_datos()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        Enviada(credito, vencido: 10_000m);

        var e = await Consulta().DeAsync(Cuota(credito), Fecha, default);

        e.Should().NotBeNull();
        e!.Should().BeEquivalentTo(new
        {
            LoanId = credito.Id, Estado = SentInstallmentState.SentWithoutResponse, Cuota = (int?)7, Vencimiento = (DateOnly?)Vence, Pagaduria = "Pagaduría Departamental",
            Convenio = "ADP", Archivo = "ADP-000123", Corriente = (decimal?)312_400m, Vencido = (decimal?)10_000m, Total = (decimal?)322_400m,
        });
        e.Periodo.Should().Be("diciembre de 2026");
        e.EnPagaduria.Should().BeTrue();
        TextosDeCuotaEnviada.Frase(e).Should().Be(
            "La cuota 7 ya se envió a la pagaduría Pagaduría Departamental en el archivo ADP-000123 de diciembre de 2026 y no ha respondido.");
    }

    [Fact]
    public async Task Descontada_y_sin_girar_es_pendiente_de_giro_y_girada_ya_no_aparece()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        var linea = Enviada(credito);
        var resultado = Resultado(linea, PayrollResultOutcome.Deducted, 312_400m);

        (await Consulta().DeAsync(Cuota(credito), Fecha, default))!.Estado.Should().Be(SentInstallmentState.DeductedPendingTransfer);

        _o.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-1", Sequence = 1, ValueDate = Vence, AccountingDate = Vence,
            RegisteredAt = _o.G.UtcNow, Amount = 312_400m, CollectorCode = "REC", IdempotencyKey = "REC:giro", Payload = "{}", InputsSha256 = "x",
            RulesVersion = "LND-2026.1", PayrollDeductionResultId = resultado.Id,
        });
        _o.Db.SaveChanges();

        (await Consulta().DeAsync(Cuota(credito), Fecha, default)).Should().BeNull("el giro ya pagó la cuota");
    }

    [Fact]
    public async Task No_descontada_es_no_descontada()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        Resultado(Enviada(credito), PayrollResultOutcome.NotDeducted, 0m, 312_400m);

        (await Consulta().DeAsync(Cuota(credito), Fecha, default))!.Estado.Should().Be(SentInstallmentState.NotDeducted);
    }

    [Fact]
    public async Task El_ultimo_resultado_de_la_linea_manda()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        var linea = Enviada(credito);
        var primero = Resultado(linea, PayrollResultOutcome.NotDeducted, 0m, 312_400m);
        _o.Db.PayrollDeductionResults.Add(new PayrollDeductionResult
        {
            PayrollDeductionFileLineId = linea.Id, PayrollDeductionFileId = linea.PayrollDeductionFileId, LoanId = credito.Id, InboundMessageId = 2,
            Outcome = PayrollResultOutcome.Deducted, DeductedAmount = 312_400m, SupersedesResultId = primero.Id, CreatedBy = "test",
        });
        _o.Db.SaveChanges();

        (await Consulta().DeAsync(Cuota(credito), Fecha, default))!.Estado.Should().Be(SentInstallmentState.DeductedPendingTransfer);
    }

    [Fact]
    public async Task Solo_cuenta_el_periodo_de_la_cuota_salvo_lo_pendiente_de_giro_de_antes()
    {
        var conOtroPeriodo = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        var conPendienteAnterior = _o.Credito(_o.Ana, 1_000_000m, "CR-2");
        Enviada(conOtroPeriodo, mes: 11, numero: "ADP-000100");
        Resultado(Enviada(conPendienteAnterior, mes: 11, numero: "ADP-000101"), PayrollResultOutcome.Deducted, 300_000m);

        var r = await Consulta().ConsultarAsync([Cuota(conOtroPeriodo), Cuota(conPendienteAnterior)], Fecha, default);

        r.Should().ContainSingle().Which.Key.Should().Be(conPendienteAnterior.Id, "el archivo de noviembre sin respuesta no es el de la cuota de diciembre");
        r[conPendienteAnterior.Id].Should().Match<CuotaEnviada>(e => e.Estado == SentInstallmentState.DeductedPendingTransfer && e.Archivo == "ADP-000101"
                                                                     && e.Periodo == "noviembre de 2026");
    }

    [Fact]
    public async Task En_un_convenio_quincenal_toma_la_quincena_del_vencimiento()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        Enviada(credito, sub: 1, numero: "ADP-Q1");
        Enviada(credito, sub: 2, numero: "ADP-Q2");

        var e = await Consulta().DeAsync(Cuota(credito), Fecha, default);

        (e!.Archivo, e.Periodo).Should().Be(("ADP-Q2", "diciembre de 2026 (segunda quincena)"));
    }

    // ------------------------------------------------------------------------------------------------------- nómina propia --

    [Fact]
    public async Task Sin_libranza_pregunta_a_la_nomina_propia_y_toma_el_periodo_del_vencimiento()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        var otro = _o.Credito(_o.Ana, 1_000_000m, "CR-2");
        var corrida = Guid.NewGuid();
        _nomina.VivosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Fecha, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<DescuentoDeNominaVivoDto>>(
        [
            new(credito.PublicId, EstadoDelDescuentoDeNomina.Generated, Guid.NewGuid(), "Noviembre 2026", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), null, null,
                6, 300_000m),
            new(credito.PublicId, EstadoDelDescuentoDeNomina.Calculated, Guid.NewGuid(), "Diciembre 2026", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), corrida, 1,
                7, 312_400m),
            new(otro.PublicId, EstadoDelDescuentoDeNomina.Approved, Guid.NewGuid(), "Diciembre 2026", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), corrida, 1,
                null, 280_000m),
        ]));

        var r = await Consulta().ConsultarAsync([Cuota(credito), Cuota(otro, 3)], Fecha, default);

        r[credito.Id].Should().BeEquivalentTo(new
        {
            Estado = SentInstallmentState.OwnPayrollCalculated, Cuota = (int?)7, Periodo = "Diciembre 2026", Total = (decimal?)312_400m, Corrida = (Guid?)corrida,
            Pagaduria = (string?)null, Archivo = (string?)null,
        });
        r[credito.Id].EnNominaPropia.Should().BeTrue();
        (r[otro.Id].Estado, r[otro.Id].Cuota).Should().Be((SentInstallmentState.OwnPayrollApproved, (int?)3), "sin cuota en la novedad, la que se preguntó");
        TextosDeCuotaEnviada.Frase(r[credito.Id]).Should().Be("La cuota 7 ya la calculó la nómina propia del período Diciembre 2026 (corrida en borrador).");
    }

    [Fact]
    public async Task Con_libranza_no_pregunta_a_la_nomina()
    {
        var credito = _o.Credito(_o.Ana, 1_000_000m, "CR-1");
        Enviada(credito);

        await Consulta().DeAsync(Cuota(credito), Fecha, default);

        await _nomina.DidNotReceive().VivosAsync(Arg.Is<IReadOnlyCollection<Guid>>(l => l.Contains(credito.PublicId)), Arg.Any<DateOnly>(),
            Arg.Any<CancellationToken>());
    }

    // --------------------------------------------------------------------------------------------------------------- textos --

    [Fact]
    public void La_recogida_explica_que_pasa_con_la_cuota_segun_se_incluya_o_se_deje()
    {
        var enviada = new CuotaEnviada(1, SentInstallmentState.SentWithoutResponse, 7, Vence, "COLPENSIONES", "ADP", 1, "ADP-000123", "noviembre de 2026", 312_400m,
            0m, 312_400m);

        TextosDeCuotaEnviada.EnLaRecogida(7, 312_400m, InstallmentTreatment.Exclude, enviada).Should().StartWith(
            "La cuota 7 por $312.400 ya se envió a la pagaduría COLPENSIONES en el archivo ADP-000123 de noviembre de 2026 y no ha respondido: se deja en el "
            + "crédito y la pagará la pagaduría; el crédito queda cancelado cuando la gire.");
        TextosDeCuotaEnviada.EnLaRecogida(7, 312_400m, InstallmentTreatment.Include, enviada).Should().EndWith(
            "se recoge completa; lo que la pagaduría gire después se tratará según " + TextosDeCuotaEnviada.GiroPosterior + ".");
        TextosDeCuotaEnviada.EnLaRecogida(7, 312_400m, InstallmentTreatment.Include, null).Should().BeNull("incluida y sin enviar no hay nada que explicar");
        TextosDeCuotaEnviada.EnLaRecogida(7, 312_400m, InstallmentTreatment.Exclude, null).Should().Contain("la paga su canal de siempre");
        TextosDeCuotaEnviada.EnLaRecogida(7, 312_400m, InstallmentTreatment.Include,
                enviada with { Estado = SentInstallmentState.OwnPayrollCalculated, Pagaduria = null, Archivo = null, Periodo = "Noviembre 2026" })
            .Should().Contain("al aprobar la nómina, el descuento de este crédito baja a cero");
    }
}
