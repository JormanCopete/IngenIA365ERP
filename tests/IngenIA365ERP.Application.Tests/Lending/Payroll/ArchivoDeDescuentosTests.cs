using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0809 (FR-113, T78, C19, C46, I9; mensajes.md §11.6): el archivo de descuentos de un convenio y período. Lleva lo exigible del
/// período de cada crédito vivo vinculado al convenio y, si el convenio lo admite, lo vencido; sus novedades se calculan contra el último enviado; tiene
/// su SHA-256 y el adjunto <c>LendingPayrollDeductionFile</c> se sube antes de guardar la fila; emite <c>DescuentosPorPagaduria</c> hacia
/// <c>Receipts</c> con el adjunto, la huella, los totales y las líneas estructuradas, sin bytes. Regenerar antes del envío crea la versión siguiente y
/// deja la anterior <c>Superseded</c> (con <c>related</c>); después del envío, <c>Lending.PayrollFile.AlreadySent</c>; anular sólo desde
/// <c>Generated</c>; sin versión vigente, <c>NoCurrentVersion</c>; la nómina propia no genera archivo. La vista previa no escribe; cada archivo deja un
/// evento resumen (sus líneas van sin diff). El contenido del archivo es dorado: <c>Casos/descuentos-pagmun01-202701.esperado.txt</c>, regenerable sólo
/// con <c>CARTERA_ESCRIBIR_ESPERADO=1</c>.
/// </summary>
public sealed class ArchivoDeDescuentosTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private LibranzaDePrueba _l = null!;
    private Loan _beto = null!;
    private Loan _dario = null!;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        _beto = await _l.OtroCreditoAsync(_l.D.O.Beto, 1_200_000m, "EMP-0002");
        _dario = await _l.OtroCreditoAsync(_l.D.O.Dario, 2_000_000m, "EMP-0003");
        // Darío trae su libranza de antes (un traslado): va primero en la prelación (FR-111).
        var v = _l.Db.LoanPayrollLinks.Single(x => x.LoanId == _dario.Id && x.ValidTo == null);
        v.LibranzaDate = new DateOnly(2026, 6, 1);
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<PayrollDeductionFileDto>> GenerarAsync(int anio = 2027, int mes = 1, Guid? convenio = null)
    {
        _l.G.ActuarComo(_l.D.Cajera);
        var r = await _l.Generar().Handle(new GeneratePayrollDeductionFileCommand(convenio ?? _l.Convenio.PublicId, anio, mes) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<DescuentosPorPagaduriaV1> MensajeAsync(Guid archivo)
    {
        var m = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.OriginPublicId == archivo && x.Type == DescuentosPorPagaduriaV1.Type);
        return JsonSerializer.Deserialize<DescuentosPorPagaduriaV1>(m.PayloadJson!, Json)!;
    }

    [Fact]
    public async Task Genera_el_archivo_con_sus_novedades_su_huella_su_adjunto_y_su_mensaje()
    {
        var r = await GenerarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var f = r.Value;
        (f.Year, f.Month, f.Version, f.Status, f.Lines, f.NewCount, f.ChangeCount, f.CancellationCount, f.UnchangedCount)
            .Should().Be((2027, 1, 1, "Generated", 3, 3, 0, 0, 0), "sin archivo base todas son altas");
        f.Number.Should().StartWith(GeneratePayrollDeductionFileCommandHandler.Prefijo);

        var subida = _l.Subidas.Should().ContainSingle().Subject;
        subida.OwnerEntityType.Should().Be(AdjuntosDeModulo.ArchivoDeDescuentos);
        subida.OwnerEntityPublicId.Should().Be(f.PublicId, "el adjunto se sube antes de guardar la fila, con su PublicId");
        Convert.ToHexString(SHA256.HashData(subida.Content)).ToLowerInvariant().Should().Be(f.Sha256);

        var lineas = await _l.Db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == _l.Db.PayrollDeductionFiles
            .Single(x => x.PublicId == f.PublicId).Id).OrderBy(l => l.LineNumber).ToListAsync();
        lineas.Select(l => l.LoanId).Should().Equal([_dario.Id, _l.Credito.Id, _beto.Id], "prelación por la fecha de la libranza y, a igual fecha, por crédito");
        lineas.Should().OnlyContain(l => l.NoveltyKind == PayrollNoveltyKind.New && l.TotalAmount > 0m && l.InstallmentNumber == 1 && l.PastDueAmount == 0m
            && l.DueDate >= new DateOnly(2027, 1, 1) && l.DueDate <= new DateOnly(2027, 1, 31));
        f.Total.Should().Be(lineas.Sum(l => l.TotalAmount));
        typeof(PayrollDeductionFileLine).IsDefined(typeof(SinDiffDeAuditoriaAttribute), false).Should().BeTrue("las líneas van con el evento resumen (C46)");
        (await _l.G.EventosAsync()).Should().Contain("Lending.PayrollDeductionFile.Generated");

        var mensaje = await MensajeAsync(f.PublicId);
        mensaje.File.Sha256.Should().Be(f.Sha256);
        mensaje.File.AttachmentPublicId.Should().NotBeEmpty();
        mensaje.File.FormatCode.Should().Be("PAGMUN-IDA");
        (mensaje.Agreement.AgreementCode, mensaje.Agreement.AgreementVersion, mensaje.Agreement.PaymasterPersonPublicId)
            .Should().Be(("PAGMUN01", 1, _l.Pagaduria.PublicId));
        mensaje.SendBy.Should().Be(new DateOnly(2027, 1, 25), "el día de envío del convenio");
        mensaje.Lines.Select(l => l.DeductionFileLinePublicId).Should().Equal(lineas.Select(l => l.PublicId), "las líneas estructuradas citan cada línea (C19)");
        mensaje.Lines.Select(l => l.PayerEmployeeId).Should().Equal("EMP-0003", "EMP-0001", "EMP-0002");
        (mensaje.Totals.LineCount, mensaje.Totals.Amount).Should().Be((3, f.Total));
        mensaje.SupersedesFile.Should().BeNull();
        var crudo = (await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.OriginPublicId == f.PublicId)).PayloadJson!;
        crudo.Should().NotContain("\"content\"", "el archivo viaja por su adjunto, sin bytes");
        (await _l.Db.IntegrationMessageDeliveries.AsNoTracking().AnyAsync(d => d.Destination == IngenIA365ERP.Domain.Enums.Integration.IntegrationDestinations.Receipts)).Should().BeTrue();
    }

    [Fact]
    public async Task El_contenido_del_archivo_es_el_dorado()
    {
        (await GenerarAsync()).IsSuccess.Should().BeTrue();
        var texto = Encoding.UTF8.GetString(_l.Subidas.Single().Content).Replace("\r\n", "\n");

        var ruta = RutaDelEsperado();
        if (Environment.GetEnvironmentVariable("CARTERA_ESCRIBIR_ESPERADO") == "1") File.WriteAllText(ruta, texto);
        File.Exists(ruta).Should().BeTrue($"falta el archivo dorado {ruta}: regénerelo con CARTERA_ESCRIBIR_ESPERADO=1");
        texto.Should().Be(File.ReadAllText(ruta).Replace("\r\n", "\n"));
    }

    private static string RutaDelEsperado([CallerFilePath] string fuente = "") =>
        Path.Combine(Path.GetDirectoryName(fuente)!, "Casos", "descuentos-pagmun01-202701.esperado.txt");

    [Fact]
    public async Task Las_novedades_se_calculan_contra_el_ultimo_enviado()
    {
        var enero = (await GenerarAsync()).Value;
        await _l.ConfirmarYConsumirAsync(enero.PublicId);
        // Beto se traslada a otra pagaduría: deja de estar en el convenio y sale como cancelación.
        var v = _l.Db.LoanPayrollLinks.Single(x => x.LoanId == _beto.Id && x.ValidTo == null);
        v.PayrollAgreementId = null;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();

        var febrero = (await GenerarAsync(2027, 2)).Value;

        (febrero.NewCount, febrero.CancellationCount, febrero.ChangeCount + febrero.UnchangedCount).Should().Be((0, 1, 2), "Ana y Darío siguen en el convenio");
        var lineas = await _l.Db.PayrollDeductionFileLines.AsNoTracking()
            .Where(l => l.PayrollDeductionFileId == _l.Db.PayrollDeductionFiles.Single(x => x.PublicId == febrero.PublicId).Id).ToListAsync();
        var deEnero = await _l.Db.PayrollDeductionFileLines.AsNoTracking()
            .Where(l => l.PayrollDeductionFileId == _l.Db.PayrollDeductionFiles.Single(x => x.PublicId == enero.PublicId).Id).ToDictionaryAsync(l => l.LoanId);
        foreach (var l in lineas.Where(l => l.LoanId != _beto.Id))
        {
            // La cuota de febrero no vale lo mismo que la primera (días de interés): «cambio» con lo que pidió el base, o «sin cambio» si coincide.
            l.NoveltyKind.Should().Be(l.TotalAmount == deEnero[l.LoanId].TotalAmount ? PayrollNoveltyKind.Unchanged : PayrollNoveltyKind.Change);
            if (l.NoveltyKind == PayrollNoveltyKind.Change) l.PreviousAmount.Should().Be(deEnero[l.LoanId].TotalAmount);
        }
        lineas.Single(l => l.LoanId == _beto.Id).Should().Match<PayrollDeductionFileLine>(l => l.NoveltyKind == PayrollNoveltyKind.Cancellation
            && l.TotalAmount == 0m && l.PreviousAmount > 0m);
        (await _l.Db.PayrollDeductionFiles.AsNoTracking().SingleAsync(x => x.PublicId == febrero.PublicId)).BaseFileId
            .Should().Be((await _l.Db.PayrollDeductionFiles.AsNoTracking().SingleAsync(x => x.PublicId == enero.PublicId)).Id);
    }

    [Fact]
    public async Task Con_IncludesPastDue_lleva_lo_vencido()
    {
        var r = await _l.Versionar().Handle(new AddPayrollAgreementVersionCommand(_l.Convenio.PublicId,
            _l.Version(new DateOnly(2027, 2, 1)) with { IncludesPastDue = true, Reason = "Otrosí: lleva lo vencido" }) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _l.Db.ChangeTracker.Clear();
        await _l.P.CausarHastaAsync(new DateOnly(2027, 1, 20));

        var febrero = (await GenerarAsync(2027, 2)).Value;

        var linea = await _l.LineaAsync(febrero.PublicId, _l.Credito);
        linea.PastDueAmount.Should().BeGreaterThan(0m, "la cuota de enero quedó impaga");
        linea.TotalAmount.Should().Be(linea.CurrentAmount + linea.PastDueAmount);
        (await MensajeAsync(febrero.PublicId)).Totals.PastDueAmount.Should().BeGreaterThan(0m);
    }

    [Fact]
    public async Task Regenerar_antes_del_envio_crea_la_version_siguiente_y_despues_del_envio_no_se_puede()
    {
        var primero = (await GenerarAsync()).Value;

        var segundo = (await GenerarAsync()).Value;

        segundo.Version.Should().Be(2);
        var anterior = (await _l.ListarArchivos().Handle(new ListPayrollDeductionFilesQuery(_l.Convenio.PublicId), default)).Value.Items
            .Single(x => x.PublicId == primero.PublicId);
        (anterior.Status, anterior.SupersededByPublicId).Should().Be(("Superseded", (Guid?)segundo.PublicId));
        var mensaje = await MensajeAsync(segundo.PublicId);
        mensaje.SupersedesFile!.FilePublicId.Should().Be(primero.PublicId);
        (await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.OriginPublicId == segundo.PublicId)).RelatedPublicId.Should().Be(primero.PublicId);

        await _l.ConfirmarYConsumirAsync(segundo.PublicId);
        var tercero = await GenerarAsync();
        tercero.Error.Code.Should().Be(ErroresDeLibranza.CodigoArchivoYaEnviado, "la corrección va como novedad del período siguiente (I9)");
    }

    [Fact]
    public async Task Anular_solo_desde_Generated()
    {
        var f = (await GenerarAsync()).Value;
        _l.G.ActuarComo(_l.D.Cajera);

        var anulado = await _l.Anular().Handle(new VoidPayrollDeductionFileCommand(f.PublicId, "Se generó con el período equivocado") { OperationKey = Guid.NewGuid() }, default);
        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? anulado.Error.Message : string.Empty);
        anulado.Value.Status.Should().Be("Voided");
        _l.Db.ChangeTracker.Clear();
        (await _l.Anular().Handle(new VoidPayrollDeductionFileCommand(f.PublicId, "otra vez") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeLibranza.CodigoArchivoReemplazado);

        var otro = (await GenerarAsync()).Value;
        await _l.ConfirmarYConsumirAsync(otro.PublicId);
        (await _l.Anular().Handle(new VoidPayrollDeductionFileCommand(otro.PublicId, "ya se envió") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeLibranza.CodigoArchivoYaEnviado);
    }

    [Fact]
    public async Task Sin_version_vigente_o_de_la_nomina_propia_no_hay_archivo()
    {
        (await GenerarAsync(2026, 10)).Error.Code.Should().Be(ErroresDeLibranza.CodigoSinVersionVigente);

        var nomina = await _l.ConvenioAsync("NOMINA", _l.Cooperativa, nominaPropia: true);
        (await GenerarAsync(convenio: nomina.PublicId)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNominaPropiaSinArchivo);
        (await _l.Db.PayrollDeductionFiles.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task La_vista_previa_no_escribe()
    {
        var r = await _l.VistaPrevia().Handle(new PreviewPayrollDeductionFileQuery(_l.Convenio.PublicId, 2027, 1), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Lines.Should().HaveCount(3);
        r.Value.Text.Should().HaveCount(3);
        (r.Value.NewCount, r.Value.Version).Should().Be((3, 1));
        (await _l.Db.PayrollDeductionFiles.AnyAsync()).Should().BeFalse();
        (await _l.Db.IntegrationMessages.AnyAsync(m => m.Type == DescuentosPorPagaduriaV1.Type)).Should().BeFalse();
        _l.Subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Fuera_del_alcance_el_archivo_no_se_ve()
    {
        var f = (await GenerarAsync()).Value;
        _l.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { 9999 }, TodasLasLineas = true };

        (await _l.LeerArchivo().Handle(new GetPayrollDeductionFileQuery(f.PublicId), default)).Error.Code.Should().Be(ErroresDeLibranza.CodigoArchivoInexistente);
        (await _l.ListarArchivos().Handle(new ListPayrollDeductionFilesQuery(), default)).Value.Items.Should().BeEmpty();
    }
}
