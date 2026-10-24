using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
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
/// Feature 015, R1, T0385 (R-24; FR-096; archivos-y-plantillas.md §6): el reporte de transacciones.
/// <list type="bullet">
/// <item>la estructura es un layout JSON con vigencia y <c>verified = false</c> sobre <c>Domain/Common/Layouts</c>: cada generación lleva
/// <c>Recaudo.Sarlaft.LayoutSinCotejar</c> como advertencia y nunca dentro del archivo;</item>
/// <item>el alcance <c>SoloEfectivo</c>/<c>Todas</c>; individuales sobre el umbral y múltiples del mes por persona;</item>
/// <item>el estado <c>Vigente</c>/<c>Suspendido</c> se sella; la ausencia sólo con <c>Sarlaft.Reporte.ExigeAusencia</c>;</item>
/// <item>bloqueantes → <c>Validated</c> sin archivo; regenerar → <c>Superseded</c>; cargado con radicado y desde ahí no se regenera;</item>
/// <item>leer el archivo deja la lectura sensible <c>Receipts.Sarlaft.ReportRead</c>.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class ReporteDeTransaccionesTests
{
    private static readonly DateOnly Enero = new(2027, 1, 15);
    private readonly EscenarioDeRecaudo _e = new();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Person _ana;
    private readonly Person _beto;

    public ReporteDeTransaccionesTests()
    {
        _e.Reloj.AhoraLocal.Returns(new DateTimeOffset(2027, 2, 3, 9, 0, 0, TimeSpan.FromHours(-5)));
        _e.Db.Companies.Add(new Company { Name = "Cooperativa de prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        _ana = new Person { FirstName = "Ana", LastName = "Asociada", TaxId = "1001", IdType = "C" };
        _beto = new Person { FirstName = "Beto", LastName = "Pagador", TaxId = "1002", IdType = "C" };
        _e.Db.People.AddRange(_ana, _beto);
        _e.Db.SaveChanges();
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));
        _e.Como(_e.Supervisor);
    }

    private void Efectivo(Person p, decimal efectivo, int dia, decimal otros = 0m, string documento = "RC-1") =>
        _e.Db.ReceiptsCashTransactions.Add(new CashTransaction
        {
            DocumentPublicId = Guid.NewGuid(), DocumentNumber = documento, DocumentKind = CashTransactionDocumentKind.Receipt, Direction = CashTransactionDirection.In,
            PersonId = p.Id, PayerPersonId = p.Id, CashAmount = efectivo, OtherMeansAmount = otros, Channel = ReceiptChannel.CashDesk, BranchId = _e.OficinaA.Id,
            OccurredOn = new DateOnly(2027, 1, dia), Month = 202701,
        });

    private GeneracionDelReporteDeTransacciones Generacion() => new(_e.Db, _e.Actor, new PreparacionDelReporteDeTransacciones(_e.Db, _e.Parametros), _sender,
        new AuditoriaDeRecaudo(_e.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance), _e.Reloj);

    [Fact]
    public void El_layout_embebido_tiene_vigencia_y_esta_sin_cotejar()
    {
        var layout = CatalogoDeLayoutsDeRecaudo.Embebido.Vigente(Enero);
        layout.Should().NotBeNull();
        layout!.HasUnverifiedFields.Should().BeTrue("hasta que el oficial lo coteje (P7)");
        CatalogoDeLayoutsDeRecaudo.Embebido.Vigente(new DateOnly(2026, 12, 31)).Should().BeNull("rige desde enero de 2027");
    }

    [Fact]
    public void Individuales_sobre_el_umbral_y_multiples_del_mes_por_persona_con_su_alcance()
    {
        var t = new[]
        {
            new TransaccionReportable(1, Enero, 6_000_000m, 0m, "RC-1", "Receipts"),
            new TransaccionReportable(2, Enero, 4_000_000m, 2_000_000m, "RC-2", "Receipts"),
            new TransaccionReportable(2, Enero.AddDays(3), 4_000_000m, 0m, "RC-3", "PointOfSale"),
        };
        var soloEfectivo = ReporteDeTransacciones.Armar(t, CashTransactionReportScope.CashOnly, 5_000_000m, 8_000_000m, CashTransactionReportApplication.Active);
        soloEfectivo.Where(l => l.Clase == CashTransactionReportLineKind.Individual).Select(l => l.Documento).Should().Equal("RC-1");
        soloEfectivo.Single(l => l.Clase == CashTransactionReportLineKind.Multiple).Should().Match<LineaDelReporte>(l => l.PersonaId == 2 && l.Valor == 8_000_000m
            && l.Origen == "PointOfSale+Receipts");

        var todas = ReporteDeTransacciones.Armar(t, CashTransactionReportScope.All, 5_000_000m, 8_000_000m, CashTransactionReportApplication.Active);
        todas.Count(l => l.Clase == CashTransactionReportLineKind.Individual).Should().Be(2, "con «Todas» RC-2 suma 6.000.000");
        ReporteDeTransacciones.Armar(t, CashTransactionReportScope.All, 5_000_000m, 8_000_000m, CashTransactionReportApplication.Suspended).Should().BeEmpty();
    }

    [Fact]
    public async Task Genera_con_la_advertencia_fuera_del_archivo_regenera_y_marca_cargado()
    {
        Efectivo(_ana, 6_000_000m, 10);
        await _e.Db.SaveChangesAsync();
        byte[]? contenido = null;
        _sender.Send(Arg.Do<UploadAttachmentCommand>(c => contenido = c.Content), Arg.Any<CancellationToken>()).Returns(Result.Success(Guid.NewGuid()));

        var r = await Generacion().GenerarAsync(2027, 1, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(CashTransactionReportStatus.Generated);
        r.Value.LayoutVerified.Should().BeFalse();
        r.Value.Issues.Should().Contain(i => i.Code == ErroresDelReporteDeTransacciones.CodigoLayoutSinCotejar && i.Severity == "Warning");
        System.Text.Encoding.UTF8.GetString(contenido!).Should().NotContain("LayoutSinCotejar").And.Contain("1001");

        var otra = await new RegenerateCashTransactionReportCommandHandler(_e.Db, Generacion()).Handle(new RegenerateCashTransactionReportCommand(r.Value.PublicId), default);
        otra.Value.Version.Should().Be(2);
        (await _e.Db.CashTransactionReports.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.PublicId)).Status.Should().Be(CashTransactionReportStatus.Superseded);

        var cargado = await new MarkCashTransactionReportUploadedCommandHandler(_e.Db, _e.Actor, new AuditoriaDeRecaudo(_e.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance),
            _e.Reloj).Handle(new MarkCashTransactionReportUploadedCommand(otra.Value.PublicId, "UIAF-123"), default);
        cargado.Value.Status.Should().Be(CashTransactionReportStatus.Uploaded);
        (await Generacion().GenerarAsync(2027, 1, default)).Error.Code.Should().Be(ErroresDelReporteDeTransacciones.CodigoYaCargado);
    }

    [Fact]
    public async Task Con_un_bloqueante_queda_validado_sin_archivo()
    {
        var sinDocumento = new Person { FirstName = "Sin", LastName = "Documento", TaxId = string.Empty, IdType = "C" };
        _e.Db.People.Add(sinDocumento);
        await _e.Db.SaveChangesAsync();
        Efectivo(sinDocumento, 7_000_000m, 12);
        await _e.Db.SaveChangesAsync();

        var r = await Generacion().GenerarAsync(2027, 1, default);

        r.Value.Status.Should().Be(CashTransactionReportStatus.Validated);
        r.Value.BlockingIssues.Should().BeGreaterThan(0);
        await _sender.DidNotReceive().Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Suspendido_y_ausencia_siguen_sus_parametros()
    {
        _e.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftReporteEstado, "Suspendido");
        Efectivo(_ana, 6_000_000m, 10);
        await _e.Db.SaveChangesAsync();
        var suspendido = await Generacion().GenerarAsync(2027, 1, default);
        suspendido.Value.ApplicationState.Should().Be(CashTransactionReportApplication.Suspended);
        suspendido.Value.Lines.Should().Be(0);

        var otroMes = await Generacion().GenerarAsync(2027, 2, default);
        otroMes.Value.IsAbsenceReport.Should().BeFalse("sin ExigeAusencia no se reporta la ausencia");
        otroMes.Value.Status.Should().Be(CashTransactionReportStatus.Validated);
    }
}
