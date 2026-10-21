using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static IngenIA365ERP.Application.Tests.Lending.Regulatory.RegulatoriosDePrueba;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T0992 (feature 013, K5, US16; adaptadores-y-archivos.md §7.5; api.md §18; C46): validar no guarda nada; generar con bloqueantes deja una generación
/// <c>Validated</c> sin archivo con sus inconsistencias (y la ruta responde <c>Lending.Regulatory.BlockingIssues</c> con
/// <c>{ regulatoryFilePublicId, version, issues[] }</c>); sin bloqueantes y con advertencias exige reconocerlas (si no, el aviso
/// <c>Lending.Regulatory.LayoutSinCotejar</c>); regenerar crea <c>Version + 1</c> y deja la anterior <c>Superseded</c> con su archivo; el archivo se sube
/// como adjunto <c>LendingRegulatoryFile</c> con el <c>PublicId</c> de la fila y su SHA-256; marcar cargado exige <c>Generated</c> y después el período
/// no se regenera; descargar una <c>Validated</c> es <c>NoFile</c>; una central desconocida es <c>BureauUnknown</c>; cada generación deja un evento
/// resumen y las líneas de las centrales van sin diff. Sobre el crédito de Ana (con condiciones y plan reales) cerrado en diciembre de 2026.
/// </summary>
public sealed class ArchivosRegulatoriosCommandsTests : IAsyncLifetime
{
    private RegulatoriosDePrueba _r = null!;

    public async Task InitializeAsync()
    {
        _r = await CrearAsync();
        await _r.CierreAsync([Linea(_r.Credito, capital: 3_000_000m)]);
    }

    public Task DisposeAsync()
    {
        _r.Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------------------------------------- piezas --

    private async Task<Result<RegulatoryFileDetailDto>> SicsesAsync(bool reconocer = true)
    {
        var r = await new GenerateSicsesFileCommandHandler(_r.Generacion())
            .Handle(new GenerateSicsesFileCommand(Anio, Mes, reconocer) { OperationKey = Guid.NewGuid() }, default);
        _r.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<RegulatoryFileDetailDto>> CentralAsync(string central = "DATACREDITO", bool reconocer = true)
    {
        var r = await new GenerateCreditBureauFileCommandHandler(_r.Generacion())
            .Handle(new GenerateCreditBureauFileCommand(central, Anio, Mes, reconocer) { OperationKey = Guid.NewGuid() }, default);
        _r.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<RegulatoryFileDto>> MarcarAsync(Guid id)
    {
        var r = await new MarkRegulatoryFileUploadedCommandHandler(_r.Db, _r.G.Auditoria, _r.G.Actor, _r.G.Reloj)
            .Handle(new MarkRegulatoryFileUploadedCommand(id, "RAD-2027-0001", _r.G.UtcNow) { OperationKey = Guid.NewGuid() }, default);
        _r.Db.ChangeTracker.Clear();
        return r;
    }

    private static RegulatoryFileDetailDto Exito(Result<RegulatoryFileDetailDto> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Un tipo de documento que el SICSES no admite: la fila de Ana queda con una bloqueante.</summary>
    private async Task DocumentoInvalidoAsync()
    {
        var ana = _r.Db.People.Single(p => p.Id == _r.Ana.Id);
        ana.IdType = "XX";
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task Validar_no_guarda_nada_y_nombra_el_credito_y_el_campo()
    {
        await DocumentoInvalidoAsync();

        var r = await new ValidateSicsesFileQueryHandler(_r.Preparacion()).Handle(new ValidateSicsesFileQuery(Anio, Mes), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Layout.Should().Be("SICSES-CARTERA-v1");
        r.Value.Blocking.Should().Be(1);
        r.Value.Issues.Should().Contain(i => i.Severity == "Blocking" && i.Loan == _r.Credito.Number && i.Path == "Deudor.TipoDeDocumento"
                                             && i.Code == CodigosDeLayout.ValorNoPermitido);
        r.Value.Warnings.Should().Contain(w => w.Code == AdvertenciasRegulatorias.LayoutSinCotejar);
        (await _r.Db.RegulatoryFiles.CountAsync()).Should().Be(0, "validar no guarda nada");
        _r.Subidas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_cierre_del_mes_validar_responde_MonthlyClose_NotFound()
    {
        var r = await new ValidateSicsesFileQueryHandler(_r.Preparacion()).Handle(new ValidateSicsesFileQuery(2027, 1), default);

        r.Error.Code.Should().Be("Lending.MonthlyClose.NotFound");
    }

    [Fact]
    public async Task Con_bloqueantes_queda_una_generacion_Validated_sin_archivo_y_la_ruta_responde_BlockingIssues()
    {
        await DocumentoInvalidoAsync();

        var g = Exito(await SicsesAsync(reconocer: false));

        g.File.Status.Should().Be(RegulatoryFileStatus.Validated);
        (g.File.Version, g.File.DownloadAvailable, g.File.Sha256, g.File.FileName, g.File.Counts.Blocking).Should().Be((1, false, null, null, 1));
        _r.Subidas.Should().BeEmpty("sin archivo no hay adjunto");
        var fila = await _r.Db.RegulatoryFiles.AsNoTracking().SingleAsync();
        (fila.Status, fila.FileAttachmentPublicId, fila.BlockingIssueCount).Should().Be((RegulatoryFileStatus.Validated, (Guid?)null, 1));
        fila.IssuesJson.Should().Contain("Deudor.TipoDeDocumento");

        var error = ErroresDeArchivosRegulatorios.Bloqueantes(g);
        error.Code.Should().Be("Lending.Regulatory.BlockingIssues");
        var datos = JsonSerializer.SerializeToElement(((ErrorConDatos)error).Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        datos.GetProperty("regulatoryFilePublicId").GetGuid().Should().Be(g.File.PublicId);
        datos.GetProperty("version").GetInt32().Should().Be(1);
        datos.GetProperty("issues").EnumerateArray().Should().ContainSingle(i => i.GetProperty("path").GetString() == "Deudor.TipoDeDocumento");

        var descarga = await new IssueRegulatoryFileDownloadLinkCommandHandler(_r.Db, null!, null!, _r.G.Auditoria, _r.G.Reloj)
            .Handle(new IssueRegulatoryFileDownloadLinkCommand(g.File.PublicId), default);
        descarga.Error.Code.Should().Be("Lending.RegulatoryFile.NoFile");
        (await MarcarAsync(g.File.PublicId)).Error.Code.Should().Be("Lending.RegulatoryFile.NotGenerated");
    }

    [Fact]
    public async Task Sin_reconocer_las_advertencias_responde_el_aviso_LayoutSinCotejar()
    {
        var r = await SicsesAsync(reconocer: false);

        r.Error.Code.Should().Be(AdvertenciasRegulatorias.LayoutSinCotejar);
        r.Error.Message.Should().Contain("sin cotejar");
        (await _r.Db.RegulatoryFiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Generar_sube_el_adjunto_con_su_huella_y_regenerar_deja_la_anterior_Superseded_con_su_archivo()
    {
        var v1 = Exito(await SicsesAsync()).File;

        v1.Status.Should().Be(RegulatoryFileStatus.Generated);
        v1.Warnings.Select(w => w.Code).Should().Contain(AdvertenciasRegulatorias.LayoutSinCotejar);
        v1.FileName.Should().StartWith(ArchivosRegulatorios.Prefijo).And.EndWith("_SICSES_2026-12_v1.txt");
        var subida = _r.Subidas.Should().ContainSingle().Subject;
        (subida.OwnerEntityType, subida.OwnerEntityPublicId).Should().Be((AdjuntosDeModulo.ArchivoRegulatorio, v1.PublicId),
            "el adjunto se sube antes de guardar la fila, con su PublicId");
        Convert.ToHexStringLower(SHA256.HashData(subida.Content)).Should().Be(v1.Sha256);
        AdjuntosDeModulo.De(AdjuntosDeModulo.ArchivoRegulatorio).Should().BeEquivalentTo(new { PermisoDeLectura = "Lending.RegulatoryFiles.Read", Borrable = false });

        var v2 = Exito(await SicsesAsync()).File;

        (v2.Version, v2.Status).Should().Be((2, RegulatoryFileStatus.Generated));
        var anterior = await _r.Db.RegulatoryFiles.AsNoTracking().SingleAsync(f => f.PublicId == v1.PublicId);
        (anterior.Status, anterior.FileAttachmentPublicId is not null, anterior.SupersededAt is not null).Should().Be((RegulatoryFileStatus.Superseded, true, true));
        (await _r.G.EventosAsync()).Count(e => e == "Lending.RegulatoryFile.Generated").Should().Be(2, "un evento resumen por archivo (C46)");

        var lista = await new ListRegulatoryFilesQueryHandler(_r.Db).Handle(new ListRegulatoryFilesQuery(RegulatoryFileKind.Sicses), default);
        lista.Value.Items.Select(f => (f.Version, f.Status)).Should().Equal((2, RegulatoryFileStatus.Generated), (1, RegulatoryFileStatus.Superseded));
    }

    [Fact]
    public async Task Marcar_cargado_exige_Generated_y_despues_el_periodo_no_se_regenera()
    {
        var g = Exito(await SicsesAsync()).File;

        var cargado = await MarcarAsync(g.PublicId);

        cargado.IsSuccess.Should().BeTrue();
        cargado.Value.Status.Should().Be(RegulatoryFileStatus.Uploaded);
        cargado.Value.Uploaded!.FilingNumber.Should().Be("RAD-2027-0001");
        (await _r.G.EventosAsync()).Should().Contain("Lending.RegulatoryFile.Uploaded");
        (await MarcarAsync(g.PublicId)).Error.Code.Should().Be("Lending.RegulatoryFile.NotGenerated");
        var otra = await SicsesAsync();
        otra.Error.Code.Should().Be("Lending.Regulatory.AlreadyUploaded");
        _r.Subidas.Should().ContainSingle("lo cargado no se regenera");
    }

    [Fact]
    public async Task El_comprobante_del_cargue_se_sube_antes_de_marcar_cargado_y_despues_no_se_borra()
    {
        // Decisión del dueño del 2026-10-16: LendingRegulatoryFileReceipt, subido con MarkUploaded mientras la generación está Generated, leído con Read.
        var g = Exito(await SicsesAsync()).File;
        var permisos = NSubstitute.Substitute.For<IngenIA365ERP.Application.Payroll.Services.IPermissionChecker>();
        permisos.HasPermissionAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<CancellationToken>()).Returns(true);
        const string Tipo = AdjuntosDeModulo.ComprobanteDeCargueRegulatorio;

        (await AdjuntosDeModulo.PuedeSubirAsync(_r.Db, permisos, Tipo, g.PublicId, default)).Should().BeNull();
        (await AdjuntosDeModulo.PuedeSubirAsync(_r.Db, permisos, Tipo, Guid.NewGuid(), default))!.Code.Should().Be("Generic.NotFound");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_r.Db, Tipo, g.PublicId, default, permisos)).Should().BeNull("antes de marcar cargado se puede cambiar");
        await permisos.Received().HasPermissionAsync("Lending.RegulatoryFiles.MarkUploaded", NSubstitute.Arg.Any<CancellationToken>());
        (await AdjuntosDeModulo.PuedeLeerAsync(permisos, Tipo, default)).Should().BeTrue();
        await permisos.Received().HasPermissionAsync("Lending.RegulatoryFiles.Read", NSubstitute.Arg.Any<CancellationToken>());

        var ajeno = await ComprobanteAsync(Guid.NewGuid());
        var mal = await new MarkRegulatoryFileUploadedCommandHandler(_r.Db, _r.G.Auditoria, _r.G.Actor, _r.G.Reloj)
            .Handle(new MarkRegulatoryFileUploadedCommand(g.PublicId, "RAD-2027-0002", _r.G.UtcNow, null, ajeno) { OperationKey = Guid.NewGuid() }, default);
        mal.Error.Code.Should().Be(ErroresDeArchivosRegulatorios.CodigoComprobanteInvalido, "el comprobante es de otra generación");

        var comprobante = await ComprobanteAsync(g.PublicId);
        var cargado = await new MarkRegulatoryFileUploadedCommandHandler(_r.Db, _r.G.Auditoria, _r.G.Actor, _r.G.Reloj)
            .Handle(new MarkRegulatoryFileUploadedCommand(g.PublicId, "RAD-2027-0002", _r.G.UtcNow, null, comprobante) { OperationKey = Guid.NewGuid() }, default);
        _r.Db.ChangeTracker.Clear();

        cargado.Value.Uploaded!.ReceiptAttachmentPublicId.Should().Be(comprobante);
        (await _r.Db.RegulatoryFiles.AsNoTracking().SingleAsync(f => f.PublicId == g.PublicId)).ReceiptAttachmentPublicId.Should().Be(comprobante);
        (await AdjuntosDeModulo.PuedeBorrarAsync(_r.Db, Tipo, g.PublicId, default, permisos))!.Code.Should().Be(AttachmentErrorCodes.OwnerLocked);
        (await AdjuntosDeModulo.PuedeSubirAsync(_r.Db, permisos, Tipo, g.PublicId, default))!.Code.Should().Be(AttachmentErrorCodes.OwnerLocked);
    }

    private async Task<Guid> ComprobanteAsync(Guid dueno)
    {
        var adjunto = new IngenIA365ERP.Domain.Entities.Core.Attachment
        {
            OwnerEntityType = AdjuntosDeModulo.ComprobanteDeCargueRegulatorio, OwnerEntityPublicId = dueno, FileName = "radicado.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x",
            Status = IngenIA365ERP.Domain.Enums.Core.EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        _r.Db.Attachments.Add(adjunto);
        await _r.Db.SaveChangesAsync();
        _r.Db.ChangeTracker.Clear();
        return adjunto.PublicId;
    }

    [Fact]
    public async Task El_archivo_de_una_central_guarda_sus_lineas_incluidas_y_excluidas_y_levanta_las_alertas()
    {
        var moroso = _r.Persona("Moroso", "Prueba", "71000001");
        var credito = await _r.CreditoAsync("CR-ARC-0001", moroso, dias: 60);
        var detalle = Exito(await CentralAsync());

        detalle.File.Status.Should().Be(RegulatoryFileStatus.Generated, string.Join(" | ", detalle.Issues.Select(i => i.Message)));
        detalle.File.Counts.Excluded.Should().Be(1);
        detalle.Excluded.Should().ContainSingle(x => x.Loan == "CR-ARC-0001" && x.Reason == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence);
        var archivo = await _r.Db.RegulatoryFiles.AsNoTracking().SingleAsync(f => f.PublicId == detalle.File.PublicId);
        var lineas = await _r.Db.RegulatoryFileLines.AsNoTracking().Where(l => l.RegulatoryFileId == archivo.Id).ToListAsync();
        lineas.Should().HaveCount(2);
        lineas.Should().ContainSingle(l => l.LoanId == credito.Id && !l.Included && l.ExclusionReason == MotivosDeExclusionDeCentral.NoPriorNoticeEvidence);
        lineas.Should().ContainSingle(l => l.LoanId == _r.Credito.Id && l.Included && l.ReportedStatusCode == "01");
        typeof(RegulatoryFileLine).IsDefined(typeof(SinDiffDeAuditoriaAttribute), false).Should().BeTrue("las líneas van con el evento resumen (C46)");
        _r.Alertas.Should().Contain(a => a.TypeCode == TiposDeAlerta.AvisoPrevioSinConstancia);
        var texto = System.Text.Encoding.Latin1.GetString(_r.Subidas.Single().Content);
        texto.Should().Contain(_r.Credito.Number).And.NotContain("CR-ARC-0001", "el excluido no entra al archivo");

        var leido = await new GetRegulatoryFileQueryHandler(_r.Db).Handle(new GetRegulatoryFileQuery(detalle.File.PublicId), default);
        leido.Value.Excluded.Should().ContainSingle(x => x.Loan == "CR-ARC-0001");
        leido.Value.Totals.GetProperty("Totales.Registros").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Una_central_desconocida_responde_BureauUnknown()
    {
        var r = await CentralAsync("CIFIN");

        r.Error.Code.Should().Be("Lending.Regulatory.BureauUnknown");
        var v = await new ValidateCreditBureauFileQueryHandler(_r.Preparacion()).Handle(new ValidateCreditBureauFileQuery("CIFIN", Anio, Mes), default);
        v.Error.Code.Should().Be("Lending.Regulatory.BureauUnknown");
    }

    [Fact]
    public async Task Leer_una_generacion_inexistente_responde_NotFound()
    {
        var r = await new GetRegulatoryFileQueryHandler(_r.Db).Handle(new GetRegulatoryFileQuery(Guid.NewGuid()), default);

        r.Error.Code.Should().Be("Lending.RegulatoryFile.NotFound");
    }
}
