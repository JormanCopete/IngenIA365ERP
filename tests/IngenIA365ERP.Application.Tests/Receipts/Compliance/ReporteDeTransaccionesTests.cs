using System.Runtime.CompilerServices;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, R1, T0385 (R-24; FR-096; archivos-y-plantillas.md §6), reescrita en la segunda ronda del 2026-10-27 al Anexo 2 TRSO de la Supersolidaria (T0507):
/// <list type="bullet">
/// <item>la estructura es un layout JSON con vigencia sobre <c>Domain/Common/Layouts</c>: ancho fijo de 645 posiciones en ANSI CP1252, encabezado, una línea por
/// transacción y cola, cotejada con el PDF del anexo y vigente en diciembre de 2026;</item>
/// <item>las múltiples se reportan operación por operación y ninguna transacción aparece dos veces; por defecto entran efectivo, cheque y plataforma virtual; el
/// retiro y el depósito conservan su sentido;</item>
/// <item>el archivo dorado (<c>Casos/uiaf-trso-202612.esperado.txt</c>) se compara byte a byte; <c>UIAF_ESCRIBIR_ESPERADO=1</c> lo regenera a propósito;</item>
/// <item>sin código de organización o sin usuario SIREL no hay archivo; la ausencia no va en el archivo: es un recordatorio para SIREL que recibe su radicado;</item>
/// <item>bloqueantes → <c>Validated</c> sin archivo; regenerar → <c>Superseded</c>; cargado con radicado y desde ahí no se regenera.</item>
/// </list>
/// </summary>
public sealed class ReporteDeTransaccionesTests
{
    private const string Codigo = "0600100042";
    private readonly EscenarioDeRecaudo _e = new();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IEfectivoDelPuntoDeVenta _pos = Substitute.For<IEfectivoDelPuntoDeVenta>();
    private readonly Person _ana;
    private readonly Person _beto;
    private readonly Person _empresa;

    public ReporteDeTransaccionesTests()
    {
        _e.Reloj.AhoraLocal.Returns(new DateTimeOffset(2027, 1, 3, 9, 0, 0, TimeSpan.FromHours(-5)));
        _e.Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        _ana = new Person { FirstName = "Ana", OtherNames = "María", LastName = "Núñez", SecondLastName = "Peña", TaxId = "1.001.234", IdType = "C" };
        _beto = new Person { FirstName = "Beto", LastName = "O'Neil-Pagador", TaxId = "1002", IdType = "E" };
        _empresa = new Person
        {
            FirstName = string.Empty, LastName = string.Empty, BusinessName = "Comercializadora Ñandú S.A.S.", TaxId = "900111222", TaxIdCheckDigit = "3", IdType = "N",
            PersonType = "02",
        };
        _e.Db.People.AddRange(_ana, _beto, _empresa);
        _e.OficinaA.LegacyCode = "001";
        _e.OficinaA.MunicipalityDaneCode = "05001";
        _e.Db.SaveChanges();
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));
        _pos.PorPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([]);
        _e.Como(_e.Supervisor);
    }

    private void Identificacion()
    {
        _e.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftReporteCodigoDeOrganizacion, Codigo);
        _e.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftReporteUsuarioSirel, "oficial.cumplimiento");
    }

    /// <summary>Un recibo vigente con su aplicación principal, sus medios y, si lleva efectivo, su fila del registro único.</summary>
    private Receipt Recibo(string numero, DateOnly fecha, Person titular, Person? entrega, ApplicationTarget destino, string producto, ReceiptChannel canal = ReceiptChannel.CashDesk,
        decimal efectivo = 0m, decimal cheque = 0m, decimal transferencia = 0m, ReceiptState estado = ReceiptState.Valid)
    {
        var total = efectivo + cheque + transferencia;
        var r = new Receipt
        {
            Number = numero, Channel = canal, BranchId = _e.OficinaA.Id, PayerPersonId = (entrega ?? titular).Id, DeliveredByPersonId = entrega?.Id, ValueDate = fecha,
            ProposedAccountingDate = fecha, TotalApplied = total, TotalMeans = total, Status = estado, OperationKey = Guid.NewGuid(), RegisteredAt = DateTime.UtcNow,
        };
        _e.Db.IssuedReceipts.Add(r);
        _e.Db.SaveChanges();
        var ordinal = 0;
        foreach (var (clase, valor) in new[] { (PaymentMeansClass.Cash, efectivo), (PaymentMeansClass.Check, cheque), (PaymentMeansClass.Transfer, transferencia) })
            if (valor > 0m)
                _e.Db.ReceiptMeans.Add(new ReceiptMeans { ReceiptId = r.Id, Ordinal = ++ordinal, PaymentMeansId = 1, MeansClass = clase, Amount = valor });
        _e.Db.ReceiptApplications.Add(new ReceiptApplication
        {
            ReceiptId = r.Id, Ordinal = 1, Target = destino, ProductNumber = producto, BeneficiaryPersonId = titular.Id, Amount = total,
        });
        if (efectivo > 0m && canal != ReceiptChannel.Online)
            _e.Db.ReceiptsCashTransactions.Add(new CashTransaction
            {
                DocumentPublicId = r.PublicId, DocumentNumber = numero, DocumentKind = CashTransactionDocumentKind.Receipt, Direction = CashTransactionDirection.In,
                PersonId = titular.Id, PayerPersonId = (entrega ?? titular).Id, DeliveredByPersonId = entrega?.Id, CashAmount = efectivo,
                OtherMeansAmount = total - efectivo, Channel = canal, BranchId = _e.OficinaA.Id, OccurredOn = fecha, Month = fecha.Year * 100 + fecha.Month,
            });
        _e.Db.SaveChanges();
        return r;
    }

    private void Egreso(string numero, DateOnly fecha, Person titular, decimal valor, string naturaleza, string producto)
    {
        var egreso = new CashDisbursement
        {
            Number = numero, Kind = CashDisbursementKind.MiscExpense, Amount = valor, BeneficiaryPersonId = titular.Id, BranchId = _e.OficinaA.Id, ProductNature = naturaleza,
            ProductNumber = producto, OperationDate = fecha, IdempotencyKey = numero, VerificationCode = "v",
        };
        _e.Db.CashDisbursements.Add(egreso);
        _e.Db.ReceiptsCashTransactions.Add(new CashTransaction
        {
            DocumentPublicId = egreso.PublicId, DocumentNumber = numero, DocumentKind = CashTransactionDocumentKind.CashDisbursement, Direction = CashTransactionDirection.Out,
            PersonId = titular.Id, PayerPersonId = titular.Id, CashAmount = valor, Channel = ReceiptChannel.CashDesk, BranchId = _e.OficinaA.Id, OccurredOn = fecha,
            Month = fecha.Year * 100 + fecha.Month,
        });
        _e.Db.SaveChanges();
    }

    /// <summary>
    /// El mes de diciembre de 2026: Ana deposita 6.000.000 en efectivo que trae Beto (individual); la empresa suma 33.000.000 entre efectivo, un cheque y un pago en
    /// línea (sus tres operaciones, como múltiples); Beto retira 5.500.000 de su ahorro (individual, retiro); lo que no llega al umbral o es transferencia en ventanilla
    /// no entra.
    /// </summary>
    private void Diciembre()
    {
        _e.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftReporteTiposDeProducto, "Loan=08;Account=01;Account:AP=70;MiscConcept=17;PointOfSale=71");
        Recibo("RC-101", new DateOnly(2026, 12, 3), _ana, _beto, ApplicationTarget.Loan, "CR-000001", efectivo: 6_000_000.4m);
        Recibo("RC-102", new DateOnly(2026, 12, 5), _empresa, null, ApplicationTarget.Account, "AP-0001", efectivo: 20_000_000m);
        Recibo("RC-103", new DateOnly(2026, 12, 10), _empresa, null, ApplicationTarget.Account, "AP-0001", cheque: 12_000_000m);
        Recibo("RC-104", new DateOnly(2026, 12, 15), _empresa, null, ApplicationTarget.Loan, "CR-000009", canal: ReceiptChannel.Online, transferencia: 1_000_000m);
        Recibo("RC-105", new DateOnly(2026, 12, 16), _ana, null, ApplicationTarget.Loan, "CR-000001", transferencia: 9_000_000m);
        Recibo("RC-106", new DateOnly(2026, 12, 17), _ana, null, ApplicationTarget.Loan, "CR-000001", efectivo: 8_000_000m, estado: ReceiptState.Voided);
        Egreso("EG-201", new DateOnly(2026, 12, 18), _ana, 1_000_000m, "Deposit", "AH-000555");
        Egreso("EG-202", new DateOnly(2026, 12, 20), _beto, 5_500_000m, "Deposit", "AH-000777");
    }

    private PreparacionDelReporteDeTransacciones Preparacion() => new(_e.Db, _e.Parametros, _pos);

    private GeneracionDelReporteDeTransacciones Generacion() => new(_e.Db, _e.Actor, Preparacion(), _sender,
        new AuditoriaDeRecaudo(_e.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance), _e.Reloj);

    [Fact]
    public void El_layout_embebido_es_el_Anexo_2_TRSO_cotejado_y_rige_en_diciembre_de_2026()
    {
        var layout = CatalogoDeLayoutsDeRecaudo.Embebido.Vigente(new DateOnly(2026, 12, 31));
        layout.Should().NotBeNull("rige desde el 1 de enero de 2026: el primer período de la salida en vivo es diciembre de 2026");
        layout!.Code.Should().Be("UIAF-TRSO-v1");
        layout.HasUnverifiedFields.Should().BeFalse("las posiciones están cotejadas con los cuadros 1, 2 y 3 del anexo");
        layout.FileKind.Should().Be(TipoDeArchivoDeLayout.FixedWidth);
        layout.Codificacion().CodePage.Should().Be(1252, "ANSI CP1252");
        layout.Records.Select(r => r.Code).Should().Equal("Header", "Detail", "Trailer");
        layout.Records.Should().OnlyContain(r => r.Length == 645 && r.Fields.Sum(f => f.Length) == 645);
        layout.Registro("Detail")!.Fields.Should().HaveCount(22);
    }

    [Fact]
    public void Las_multiples_van_operacion_por_operacion_sin_repetir_y_con_su_sentido()
    {
        var d = new DateOnly(2026, 12, 1);
        var t = new[]
        {
            new TransaccionReportable(1, d, 6_000_000m, MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.In, "RC-1", "Receipts"),
            new TransaccionReportable(2, d, 25_000_000m, MedioDeTransaccionReportable.Efectivo, CashTransactionDirection.In, "RC-2", "Receipts"),
            new TransaccionReportable(2, d.AddDays(1), 6_000_000m, MedioDeTransaccionReportable.Cheque, CashTransactionDirection.Out, "EG-3", "Receipts"),
            new TransaccionReportable(2, d.AddDays(2), 100_000m, MedioDeTransaccionReportable.PlataformaVirtual, CashTransactionDirection.In, "RC-4", "Receipts"),
            new TransaccionReportable(3, d, 40_000_000m, MedioDeTransaccionReportable.PlataformaVirtual, CashTransactionDirection.In, "RC-5", "Receipts"),
        };

        var lineas = ReporteDeTransacciones.Armar(t, CashTransactionReportScope.All, 5_000_000m, 30_000_000m, CashTransactionReportApplication.Active);

        lineas.Select(l => l.Transaccion.Documento).Should().OnlyHaveUniqueItems("ninguna transacción aparece dos veces");
        lineas.Where(l => l.Clase == CashTransactionReportLineKind.Multiple).Select(l => l.Transaccion.Documento).Should()
            .BeEquivalentTo(["RC-2", "EG-3", "RC-4", "RC-5"], "las del titular que llega a las múltiples, cada una por separado, aun la de 100.000");
        lineas.Single(l => l.Transaccion.Documento == "RC-1").Clase.Should().Be(CashTransactionReportLineKind.Individual);
        lineas.Single(l => l.Transaccion.Documento == "EG-3").Transaccion.Sentido.Should().Be(CashTransactionDirection.Out, "el retiro sigue siendo retiro");

        var soloEfectivo = ReporteDeTransacciones.Armar(t, CashTransactionReportScope.CashOnly, 5_000_000m, 30_000_000m, CashTransactionReportApplication.Active);
        soloEfectivo.Select(l => l.Transaccion.Documento).Should().BeEquivalentTo(["RC-1", "RC-2"], "sin cheque ni plataforma, la empresa no llega a las múltiples");
        ReporteDeTransacciones.Armar(t, CashTransactionReportScope.CashAndCheck, 5_000_000m, 30_000_000m, CashTransactionReportApplication.Active)
            .Select(l => l.Transaccion.Documento).Should().BeEquivalentTo(["RC-1", "RC-2", "EG-3"], "con cheque la empresa suma 31.000.000");
        ReporteDeTransacciones.Armar(t, CashTransactionReportScope.All, 5_000_000m, 30_000_000m, CashTransactionReportApplication.Suspended).Should().BeEmpty();
    }

    [Fact]
    public void El_alcance_por_defecto_son_los_tres_medios_del_anexo()
    {
        var definicion = ParametrosDeRecaudoYTesoreria.Definiciones.Single(d => d.Clave == ParametrosDeRecaudoYTesoreria.SarlaftReporteAlcance);
        definicion.DefectoSeguro.Should().Be("EfectivoChequeYPlataforma");
        ReportesDeTransacciones.Alcance("EfectivoChequeYPlataforma").Should().Be(CashTransactionReportScope.All);
        ReportesDeTransacciones.Alcance("Todas").Should().Be(CashTransactionReportScope.All, "el nombre anterior sigue valiendo");
        ReportesDeTransacciones.Alcance("EfectivoYCheque").Should().Be(CashTransactionReportScope.CashAndCheck);
        ReportesDeTransacciones.Alcance("SoloEfectivo").Should().Be(CashTransactionReportScope.CashOnly);
        ParametrosDeRecaudoYTesoreria.Definiciones.Where(d => d.Clave.StartsWith("Sarlaft.Umbral", StringComparison.Ordinal) && d.Clave != ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos)
            .Should().OnlyContain(d => d.Descripcion.Contains("CE 72 de 2024 prorrogada por CE 95 de 2026"));
    }

    [Theory]
    [InlineData("C", 13)]
    [InlineData("E", 22)]
    [InlineData("N", 31)]
    [InlineData("P", 41)]
    [InlineData("T", 12)]
    [InlineData("R", 11)]
    [InlineData("PPT", 0)]
    public void El_tipo_de_identificacion_es_el_codigo_numerico_de_la_tabla_7_4(string idType, int codigo) =>
        DatosParaLaUiaf.TipoDeIdentificacion(idType).Should().Be(codigo);

    [Fact]
    public void Personas_naturales_y_juridicas_y_codigos_de_producto_como_los_pide_el_anexo()
    {
        var natural = DatosParaLaUiaf.Persona("C", "1.001.234", null, "01", "ana", "maría", "núñez", "peña", null);
        natural.Should().Be(new PersonaParaLaUiaf(13, "1001234", -1, "ANA MARÍA", "NÚÑEZ PEÑA", "-1"));
        var juridica = DatosParaLaUiaf.Persona("N", "900.111.222", "3", "02", "", null, "", null, "Comercializadora Ñandú S.A.S.");
        juridica.Should().Be(new PersonaParaLaUiaf(31, "900111222", 3, "-1", "-1", "COMERCIALIZADORA ÑANDÚ S A S"));

        const string mapa = "Loan=08;Account=01;Account:AP=70;Account:APX=68";
        DatosParaLaUiaf.TipoDeProducto(mapa, "Account", "AP-0001").Should().Be(70);
        DatosParaLaUiaf.TipoDeProducto(mapa, "Account", "APX-1").Should().Be(68, "gana el prefijo más largo");
        DatosParaLaUiaf.TipoDeProducto(mapa, "Account", "AH-1").Should().Be(1);
        DatosParaLaUiaf.TipoDeProducto(mapa, "PendingApplication", "X").Should().BeNull("sin código, el archivo lo bloquea");
        DatosParaLaUiaf.NombreDelArchivo(Codigo, 2026, 12).Should().Be("TRSO0600100042" + "1226.txt");
    }

    [Fact]
    public async Task El_archivo_de_diciembre_es_el_dorado_byte_a_byte()
    {
        Identificacion();
        Diciembre();

        var p = (await Preparacion().PrepararAsync(2026, 12, default)).Value;

        p.Issues.Where(i => i.Severity == "Blocking").Should().BeEmpty(string.Join(" | ", p.Issues.Select(i => i.Message)));
        p.Lineas.Select(l => (l.Transaccion.Documento, l.Clase)).Should().Equal(
            ("RC-101", CashTransactionReportLineKind.Individual), ("RC-102", CashTransactionReportLineKind.Multiple), ("RC-103", CashTransactionReportLineKind.Multiple),
            ("RC-104", CashTransactionReportLineKind.Multiple), ("EG-202", CashTransactionReportLineKind.Individual));
        p.NombreDelArchivo.Should().Be($"TRSO{Codigo}1226.txt");
        var texto = LayoutDeArchivo.Obtener("windows-1252").GetString(p.Escritura!.Contenido!);
        texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(7).And.OnlyContain(l => l.Length == 645);
        texto.Should().StartWith($"0000000000{Codigo}2026-12-310000000005XXXX");
        p.Escritura.Contenido.Should().Contain((byte)0xD1, "la Ñ va en CP1252 (209), no en UTF-8");
        Comparar("uiaf-trso-202612", p.Escritura.Contenido!);
    }

    [Fact]
    public async Task Sin_codigo_de_organizacion_ni_usuario_SIREL_no_hay_archivo()
    {
        Diciembre();

        var r = await Generacion().GenerarAsync(2026, 12, default);

        r.Value.Status.Should().Be(CashTransactionReportStatus.Validated);
        r.Value.FileName.Should().BeNull();
        r.Value.Issues.Where(i => i.Severity == "Blocking").Select(i => i.Code).Should().Contain(
            [ReportesDeTransacciones.CodigoSinCodigoDeOrganizacion, ReportesDeTransacciones.CodigoSinUsuarioSirel]);
        await _sender.DidNotReceive().Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Genera_con_el_nombre_del_anexo_regenera_y_marca_cargado()
    {
        Identificacion();
        Diciembre();
        string? nombre = null;
        _sender.Send(Arg.Do<UploadAttachmentCommand>(c => nombre = c.FileName), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));

        var r = await Generacion().GenerarAsync(2026, 12, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(CashTransactionReportStatus.Generated);
        r.Value.LayoutVerified.Should().BeTrue();
        r.Value.Lines.Should().Be(5);
        r.Value.FileName.Should().Be($"TRSO{Codigo}1226.txt");
        nombre.Should().Be(r.Value.FileName);
        var lineas = await _e.Db.CashTransactionReportLines.AsNoTracking().OrderBy(l => l.LineNumber).ToListAsync();
        lineas.Should().OnlyContain(l => l.TransactionCount == 1);
        lineas.Last().DocumentsJson.Should().Contain("\"direction\":1", "el retiro queda registrado como retiro");

        var otra = await new RegenerateCashTransactionReportCommandHandler(_e.Db, Generacion()).Handle(new RegenerateCashTransactionReportCommand(r.Value.PublicId), default);
        otra.Value.Version.Should().Be(2);
        (await _e.Db.CashTransactionReports.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.PublicId)).Status.Should().Be(CashTransactionReportStatus.Superseded);

        var cargado = await Marcar(otra.Value.PublicId, "UIAF-123");
        cargado.Value.Status.Should().Be(CashTransactionReportStatus.Uploaded);
        (await Generacion().GenerarAsync(2026, 12, default)).Error.Code.Should().Be(ErroresDelReporteDeTransacciones.CodigoYaCargado);
    }

    [Fact]
    public async Task Con_un_bloqueante_queda_validado_sin_archivo()
    {
        Identificacion();
        var sinDocumento = new Person { FirstName = "Sin", LastName = "Documento", TaxId = string.Empty, IdType = "C" };
        _e.Db.People.Add(sinDocumento);
        await _e.Db.SaveChangesAsync();
        Recibo("RC-1", new DateOnly(2026, 12, 12), sinDocumento, null, ApplicationTarget.Loan, "CR-1", efectivo: 7_000_000m);

        var r = await Generacion().GenerarAsync(2026, 12, default);

        r.Value.Status.Should().Be(CashTransactionReportStatus.Validated);
        r.Value.BlockingIssues.Should().BeGreaterThan(0);
        await _sender.DidNotReceive().Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suspendido_y_la_ausencia_es_un_recordatorio_para_SIREL_que_recibe_su_radicado()
    {
        Vigencia(ParametrosDeRecaudoYTesoreria.SarlaftReporteEstado, "Suspendido", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));
        Diciembre();
        var suspendido = await Generacion().GenerarAsync(2026, 12, default);
        suspendido.Value.ApplicationState.Should().Be(CashTransactionReportApplication.Suspended);
        suspendido.Value.Lines.Should().Be(0);

        var enero = await Generacion().GenerarAsync(2027, 1, default);
        enero.Value.IsAbsenceReport.Should().BeTrue("por defecto se recuerda la ausencia");
        enero.Value.FileName.Should().BeNull("la ausencia se presenta en SIREL, no en el archivo");
        enero.Value.Issues.Should().ContainSingle(i => i.Code == ReportesDeTransacciones.CodigoAusenciaEnSirel && i.Severity == "Warning");
        (await Marcar(enero.Value.PublicId, "SIREL-AUS-1")).Value.Status.Should().Be(CashTransactionReportStatus.Uploaded);

        Vigencia(ParametrosDeRecaudoYTesoreria.SarlaftReporteExigeAusencia, "false", new DateOnly(2027, 2, 1));
        var febrero = await Generacion().GenerarAsync(2027, 2, default);
        febrero.Value.IsAbsenceReport.Should().BeFalse();
        (await Marcar(febrero.Value.PublicId, "X")).Error.Code.Should().Be(ErroresDelReporteDeTransacciones.CodigoNoGenerado);
    }

    private void Vigencia(string clave, string valor, DateOnly desde, DateOnly? hasta = null)
    {
        _e.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = ParametrosDeRecaudoYTesoreria.ModuloRecaudo, Key = clave, ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None, Value = valor, ValidFrom = desde,
            ValidTo = hasta, Reason = "prueba",
        });
        _e.Db.SaveChanges();
    }

    private Task<Result<CashTransactionReportDto>> Marcar(Guid reporte, string radicado) =>
        new MarkCashTransactionReportUploadedCommandHandler(_e.Db, _e.Actor, new AuditoriaDeRecaudo(_e.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance), _e.Reloj)
            .Handle(new MarkCashTransactionReportUploadedCommand(reporte, radicado), default);

    private static void Comparar(string caso, byte[] contenido, [CallerFilePath] string fuente = "")
    {
        var ruta = Path.Combine(Path.GetDirectoryName(fuente)!, "Casos", caso + ".esperado.txt");
        // Regenerar a propósito (tras cotejarlo a mano con el anexo): UIAF_ESCRIBIR_ESPERADO=1 escribe el archivo junto al código fuente.
        if (Environment.GetEnvironmentVariable("UIAF_ESCRIBIR_ESPERADO") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllBytes(ruta, contenido);
        }
        File.Exists(ruta).Should().BeTrue($"falta el archivo dorado {ruta}: regénerelo con UIAF_ESCRIBIR_ESPERADO=1");
        contenido.Should().Equal(File.ReadAllBytes(ruta), $"el archivo debe ser byte a byte el de {Path.GetFileName(ruta)}");
    }
}
