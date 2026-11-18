using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.SignatureDocuments;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.SignatureDocuments;

/// <summary>
/// 2026-11-18 (pedido del dueño): los documentos para firma de la solicitud sobre el escenario del desembolso (<see cref="DesembolsoDePrueba"/>):
/// las plantillas base se cargan sin pisar nada, generar exige la solicitud aprobada y plantillas que apliquen, crea el pagaré físico con el número
/// de la solicitud, incluye la libranza sólo cuando el crédito se paga por nómina, pide los campos digitados obligatorios y volver a generar
/// reemplaza el juego anterior sin otro pagaré.
/// </summary>
public sealed class DocumentosParaFirmaTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDocumentosParaFirmaRenderer _renderizador = Substitute.For<IDocumentosParaFirmaRenderer>();
    private readonly List<PaqueteParaFirma> _pintados = [];
    private readonly List<UploadAttachmentCommand> _subidas = [];

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _renderizador.Render(Arg.Any<PaqueteParaFirma>()).Returns(ci =>
        {
            _pintados.Add(ci.Arg<PaqueteParaFirma>());
            return "%PDF-1.7 prueba"u8.ToArray();
        });
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _subidas.Add(ci.Arg<UploadAttachmentCommand>());
            return Result.Success(Guid.NewGuid());
        });
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private ArmadoDeDatosParaFirma Armado() => new(_d.Db, _d.O.Simulador(), _d.G.Reloj);

    private GenerateSignatureDocumentsCommandHandler Generar() => new(_d.Db, Armado(), _renderizador, _sender, _d.G.Alcance, _d.G.Auditoria, _d.G.Actor,
        Substitute.For<IBlobStore>(), _d.G.Reloj);

    private async Task CargarBaseAsync()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetSignatureConfigurationQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new GetSignatureConfigurationQueryHandler(_d.Db).Handle(new GetSignatureConfigurationQuery(), default));
        (await new LoadBaseSignatureTemplatesCommandHandler(_d.Db, sender).Handle(new LoadBaseSignatureTemplatesCommand(), default)).IsSuccess.Should().BeTrue();
        _d.Db.ChangeTracker.Clear();
    }

    private async Task<Result<GeneratedSignatureDocumentsDto>> GenerarAsync(Guid solicitud, Dictionary<string, string?>? valores = null)
    {
        var r = await Generar().Handle(new GenerateSignatureDocumentsCommand(solicitud, valores) { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Cargar_la_base_agrega_las_tres_plantillas_y_sus_campos_una_sola_vez()
    {
        await CargarBaseAsync();
        await CargarBaseAsync();

        (await _d.Db.SignatureDocumentTemplates.Select(t => t.Code).ToListAsync())
            .Should().BeEquivalentTo([PlantillasBase.CodigoPagare, PlantillasBase.CodigoCarta, PlantillasBase.CodigoLibranza]);
        (await _d.Db.SignatureDocumentFields.CountAsync()).Should().Be(PlantillasBase.Campos.Count);
        (await _d.Db.SignatureDocumentTemplates.AnyAsync(t => t.IsVerified)).Should().BeFalse("la base no está validada por un abogado");
    }

    [Fact]
    public void Las_plantillas_base_son_validas()
    {
        var catalogo = new CatalogoDeCampos(PlantillasBase.Campos.Select(c => c.Codigo));
        foreach (var p in PlantillasBase.Plantillas)
            PlantillaDeFirma.Validar(p.Texto, catalogo).Should().BeEmpty(p.Codigo);
    }

    [Fact]
    public async Task Sin_plantillas_no_genera()
    {
        var s = await _d.AprobadaAsync();

        var r = await GenerarAsync(s.PublicId);

        r.Error.Code.Should().Be(ErroresDeDocumentosParaFirma.CodigoSinPlantillas);
    }

    [Fact]
    public async Task Genera_el_pagare_y_la_carta_con_el_pagare_fisico_numerado_como_la_solicitud()
    {
        await CargarBaseAsync();
        var s = await _d.AprobadaAsync();

        var r = await GenerarAsync(s.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Set.Documents.Select(d => d.Code).Should().Equal(PlantillasBase.CodigoPagare, PlantillasBase.CodigoCarta);
        r.Value.Warnings.Should().Contain(w => w.Contains("sin validar"));

        var pagare = await _d.Db.PromissoryNotes.SingleAsync();
        pagare.Kind.Should().Be(PromissoryNoteKind.Physical);
        pagare.Status.Should().Be(PromissoryNoteStatus.Created);
        pagare.Number.Should().Be(s.Number);

        var juego = await _d.Db.SignatureDocumentSets.SingleAsync();
        juego.Status.Should().Be(SignatureDocumentSetStatus.Active);
        juego.PromissoryNotePublicId.Should().Be(pagare.PublicId);
        _subidas.Should().ContainSingle().Which.OwnerEntityType.Should().Be(AdjuntosDeModulo.DocumentosParaFirmaDeCartera);

        var paquete = _pintados.Single();
        paquete.Documentos.Should().HaveCount(2);
        var textos = paquete.Documentos[0].Bloques.Select(b => b.TextoPlano).ToList();
        textos[0].Should().Be($"PAGARÉ No. {s.Number}");
        var finDelDocumento = _d.O.Ana.TaxId[^3..];
        textos.Should().Contain(t => t.Contains("en calidad de deudor(a)") && t.Contains(finDelDocumento));
        paquete.Documentos[0].Firmantes.Should().Contain(f => f.Calidad == "Deudor");
        paquete.Documentos[0].Huella.Should().BeTrue();
        paquete.MarcaDeAgua.Should().Be(GenerateSignatureDocumentsCommandHandler.MarcaDePruebas);
    }

    [Fact]
    public async Task La_libranza_sale_solo_si_el_credito_se_paga_por_nomina_y_la_firma_solo_el_deudor()
    {
        await CargarBaseAsync();
        var s = await _d.AprobadaAsync();
        var fila = await _d.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        fila.RepaymentChannel = RepaymentChannel.OwnPayroll;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var r = await GenerarAsync(s.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Set.Documents.Select(d => d.Code).Should().Equal(PlantillasBase.CodigoPagare, PlantillasBase.CodigoCarta, PlantillasBase.CodigoLibranza);
        var libranza = _pintados.Single().Documentos[2];
        libranza.Firmantes.Should().ContainSingle().Which.Calidad.Should().Be("Deudor");
        // Con nómina propia, la pagaduría es la cooperativa.
        var datos = await _d.Db.SignatureDocumentSets.Select(j => j.FieldsJson).SingleAsync();
        datos.Should().Contain(CamposDelSistema.PagaduriaNombre);
    }

    [Fact]
    public async Task Volver_a_generar_reemplaza_el_juego_y_no_crea_otro_pagare()
    {
        await CargarBaseAsync();
        var s = await _d.AprobadaAsync();

        (await GenerarAsync(s.PublicId)).IsSuccess.Should().BeTrue();
        (await GenerarAsync(s.PublicId)).IsSuccess.Should().BeTrue();

        (await _d.Db.PromissoryNotes.CountAsync()).Should().Be(1);
        var juegos = await _d.Db.SignatureDocumentSets.OrderBy(j => j.Id).ToListAsync();
        juegos.Select(j => j.Status).Should().Equal(SignatureDocumentSetStatus.Superseded, SignatureDocumentSetStatus.Active);
    }

    [Fact]
    public async Task Un_campo_digitado_obligatorio_que_falta_no_deja_generar()
    {
        await CargarBaseAsync();
        (await new SaveSignatureFieldCommandHandler(_d.Db).Handle(new SaveSignatureFieldCommand(null,
            new SignatureFieldInput("PLACA", "Placa del vehículo", "Text", "Typed", null, null, true, null, 9, true)), default)).IsSuccess.Should().BeTrue();
        var pagare = await _d.Db.SignatureDocumentTemplates.SingleAsync(t => t.Code == PlantillasBase.CodigoPagare);
        pagare.CambiarTexto(pagare.Body + "\nPlaca: {{PLACA}}");
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        var s = await _d.AprobadaAsync();

        (await GenerarAsync(s.PublicId)).Error.Code.Should().Be(ErroresDeDocumentosParaFirma.CodigoFaltanCampos);
        var r = await GenerarAsync(s.PublicId, new() { ["PLACA"] = "ABC123" });
        r.IsSuccess.Should().BeTrue();
        _pintados[^1].Documentos[0].Bloques.Should().Contain(b => b.TextoPlano == "Placa: ABC123");
    }

    [Fact]
    public async Task Guardar_una_plantilla_valida_los_campos_y_el_codigo()
    {
        var guardar = new SaveSignatureTemplateCommandHandler(_d.Db);
        var entrada = new SignatureTemplateInput("Other", "AUTORIZA", "Autorización", "Hola {{NO_EXISTE}}", null, null, true, 1, false, true);

        (await guardar.Handle(new SaveSignatureTemplateCommand(null, entrada), default)).Error.Code
            .Should().Be(ErroresDeDocumentosParaFirma.CodigoPlantillaInvalida);

        var buena = await guardar.Handle(new SaveSignatureTemplateCommand(null, entrada with { Body = "Hola {{DEUDOR_NOMBRE}}" }), default);
        buena.IsSuccess.Should().BeTrue();
        (await guardar.Handle(new SaveSignatureTemplateCommand(null, entrada with { Body = "Otra" }), default)).Error.Code
            .Should().Be(ErroresDeDocumentosParaFirma.CodigoCodigoDuplicado);

        var cambiada = await guardar.Handle(new SaveSignatureTemplateCommand(buena.Value.PublicId, entrada with { Body = "Hola {{DEUDOR_DOCUMENTO}}" }), default);
        cambiada.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task Un_campo_propio_no_usa_un_codigo_del_sistema_y_no_se_borra_si_una_plantilla_lo_usa()
    {
        await CargarBaseAsync();
        var guardar = new SaveSignatureFieldCommandHandler(_d.Db);

        (await guardar.Handle(new SaveSignatureFieldCommand(null, new SignatureFieldInput("MONTO", "Monto", "Money", "Constant", null, null, false, null, 1, true)),
            default)).Error.Code.Should().Be(ErroresDeDocumentosParaFirma.CodigoCampoReservado);
        (await guardar.Handle(new SaveSignatureFieldCommand(null, new SignatureFieldInput("VALOR_X", "Valor", "Money", "Constant", "abc", null, false, null, 1, true)),
            default)).Error.Code.Should().Be(ErroresDeDocumentosParaFirma.CodigoValorInvalido);

        var ciudad = await _d.Db.SignatureDocumentFields.SingleAsync(f => f.Code == PlantillasBase.CiudadDePago);
        (await new DeleteSignatureFieldCommandHandler(_d.Db, _d.G.Reloj).Handle(new DeleteSignatureFieldCommand(ciudad.PublicId), default)).Error.Code
            .Should().Be(ErroresDeDocumentosParaFirma.CodigoCampoEnUso);
    }
}
