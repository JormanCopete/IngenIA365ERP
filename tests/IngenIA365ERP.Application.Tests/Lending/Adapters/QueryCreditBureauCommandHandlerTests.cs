using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.ExternalChannels;
using IngenIA365ERP.ExternalChannels.CreditBureaus;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Adapters;

/// <summary>
/// Feature 013, T0375 (api.md §4.4, adaptadores §3.8, T69, T73): consultar a una central. Sin autorización <c>CreditBureauQuery</c> →
/// <c>Lending.Authorization.Missing</c>; con una migrada sin evidencia → <c>Lending.Authorization.WithoutEvidence</c>; canal caído →
/// <c>Lending.Adapter.Unavailable</c> con <c>channelCode</c>; sin canal configurado → <c>Core.ExternalChannel.NotConfigured</c>. Con éxito guarda
/// <c>LND_BureauQueries</c> con el canal sellado, la autorización usada, el resumen y el informe crudo como adjunto <c>LendingBureauReport</c>, y
/// <c>ValidUntil</c> = hoy + <c>Centrales.DiasDeVigenciaDeConsulta</c>; dentro de la vigencia se reutiliza la última; la respuesta trae sólo el
/// estado; leer el informe y pedir su enlace emiten <c>Lending.CreditBureauReport.Viewed</c>.
/// </summary>
public sealed class QueryCreditBureauCommandHandlerTests : IAsyncLifetime
{
    private const string Central = "DATACREDITO";

    private OriginacionDePrueba _o = null!;
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICredencialesDeCanalExterno _credenciales = Substitute.For<ICredencialesDeCanalExterno>();
    private readonly ICurrentTenantService _tenant = Substitute.For<ICurrentTenantService>();
    private readonly IBlobStore _almacen = Substitute.For<IBlobStore>();
    private readonly Guid _informe = Guid.NewGuid();
    private readonly Guid _consentimiento = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: false);
        _tenant.TenantId.Returns(Guid.NewGuid().ToString());
        _sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success(_informe));
        _almacen.FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EnlaceDeDescarga("https://almacen.test/informe", ci.Arg<DescargaFirmada>().VenceEn));
        Autorizar(_o.Ana, conEvidencia: true);
        _o.G.ActuarComo(_o.Analista);
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private void Autorizar(Person p, bool conEvidencia) =>
        _o.Autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.CreditBureauQuery, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, _o.G.UtcNow.AddYears(-1), null)
            {
                Purpose = DataAuthorizationPurpose.CreditBureauQuery, HasEvidence = conEvidencia, ConsentPublicId = _consentimiento,
                Origin = conEvidencia ? DataAuthorizationOrigin.CreditApplication : DataAuthorizationOrigin.Migration,
            });

    private async Task ConfigurarAsync(string canal = CodigosDeCanalExterno.Simulado)
    {
        _o.Db.ExternalChannelSettings.Add(new ExternalChannelSetting
        {
            Kind = ExternalChannelKind.CreditBureau, BureauCode = Central, SettingKey = ExternalChannelSetting.ClaveDe(ExternalChannelKind.CreditBureau, Central),
            ChannelCode = canal, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Canal de prueba",
        });
        await _o.Db.SaveChangesAsync();
    }

    private QueryCreditBureauCommandHandler Handler() =>
        new(_o.Db,
            new CentralesDeRiesgo([new CentralDeRiesgoSimulada(Options.Create(new ExternalChannelsOptions()), new MemoriaDeCentralSimulada())],
                new CircuitoSiempreCerrado()),
            _credenciales, _tenant, Options.Create(_o.Opciones), _o.Autorizaciones, _o.Parametros(), _sender, _o.G.Alcance, _o.G.Auditoria, _o.G.Actor, _o.G.Reloj);

    private async Task<Result<BureauQueryCreatedDto>> ConsultarAsync(Person p)
    {
        var r = await Handler().Handle(new QueryCreditBureauCommand(p.PublicId, Central, QueryCreditBureauCommand.ParaCupo) { OperationKey = Guid.NewGuid() },
            default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Sin_autorizacion_para_consultar_centrales_es_Authorization_Missing()
    {
        await ConfigurarAsync();
        _o.QuitarAutorizacion(_o.Beto, DataAuthorizationPurpose.CreditBureauQuery);

        var r = await ConsultarAsync(_o.Beto);

        r.Error.Code.Should().Be("Lending.Authorization.Missing");
        OriginacionDePrueba.Datos(r.Error).GetProperty("purpose").GetString().Should().Be("CreditBureauQuery");
        OriginacionDePrueba.Datos(r.Error).GetProperty("howToFix").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Una_autorizacion_migrada_sin_evidencia_no_sirve()
    {
        await ConfigurarAsync();
        Autorizar(_o.Ana, conEvidencia: false);

        (await ConsultarAsync(_o.Ana)).Error.Code.Should().Be("Lending.Authorization.WithoutEvidence");
    }

    [Fact]
    public async Task Sin_canal_configurado_para_la_central_es_NotConfigured()
    {
        var r = await ConsultarAsync(_o.Ana);

        r.Error.Code.Should().Be("Core.ExternalChannel.NotConfigured");
        OriginacionDePrueba.Datos(r.Error).GetProperty("bureauCode").GetString().Should().Be(Central);
    }

    [Fact]
    public async Task Con_el_canal_caido_es_Adapter_Unavailable_y_no_guarda_nada()
    {
        await ConfigurarAsync();
        var caido = _o.Persona("Eva", "Caída", "52000005", new DateOnly(2022, 1, 1), new DateOnly(1990, 1, 1));
        Autorizar(caido, conEvidencia: true);

        var r = await ConsultarAsync(caido);

        r.Error.Code.Should().Be("Lending.Adapter.Unavailable");
        OriginacionDePrueba.Datos(r.Error).GetProperty("channelCode").GetString().Should().Be(CodigosDeCanalExterno.Simulado);
        (await _o.Db.BureauQueries.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Con_exito_guarda_la_consulta_con_su_evidencia_y_responde_solo_el_estado()
    {
        await ConfigurarAsync();

        var r = await ConsultarAsync(_o.Ana);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be("Ok");
        r.Value.Simulated.Should().BeTrue();
        r.Value.Reused.Should().BeFalse();
        typeof(BureauQueryCreatedDto).GetProperties().Select(p => p.Name).Should().NotContain(["Score", "NegativeMarks", "Summary"],
            "la respuesta trae sólo el estado, nunca el contenido");

        var q = await _o.Db.BureauQueries.SingleAsync();
        q.PublicId.Should().Be(r.Value.BureauQueryPublicId);
        q.ChannelCode.Should().Be(CodigosDeCanalExterno.Simulado);
        q.BureauCode.Should().Be(Central);
        q.AuthorizationConsentPublicId.Should().Be(_consentimiento);
        q.Score.Should().Be(CentralDeRiesgoSimulada.PuntajeConReporteNegativo, "el documento de Ana termina en 1: reporte negativo");
        q.NegativeMarks.Should().Be(1);
        q.Summary.Should().Contain("SIM-");
        q.IsSimulated.Should().BeTrue();
        q.ReportAttachmentPublicId.Should().Be(_informe);
        q.ValidUntil.Should().Be(OriginacionDePrueba.Hoy.AddDays(30), "Centrales.DiasDeVigenciaDeConsulta por defecto");
        await _sender.Received(1).Send(Arg.Is<UploadAttachmentCommand>(c => c.OwnerEntityType == AdjuntosDeModulo.InformeDeCentrales
            && c.OwnerEntityPublicId == q.PublicId && c.ContentType == "application/json"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_persona_sin_historia_queda_NoHistory()
    {
        await ConfigurarAsync();
        var nueva = _o.Persona("Nora", "Nueva", "52000002", new DateOnly(2025, 1, 1), new DateOnly(1999, 1, 1));
        Autorizar(nueva, conEvidencia: true);

        (await ConsultarAsync(nueva)).Value.Status.Should().Be("NoHistory");
        (await _o.Db.BureauQueries.SingleAsync()).Outcome.Should().Be(ExternalOutcome.NotFound);
    }

    [Fact]
    public async Task Dentro_de_la_vigencia_se_reutiliza_la_ultima_consulta()
    {
        await ConfigurarAsync();
        var primera = await ConsultarAsync(_o.Ana);
        _o.G.HoyLocal = OriginacionDePrueba.Hoy.AddDays(10);

        var segunda = await ConsultarAsync(_o.Ana);

        segunda.Value.Reused.Should().BeTrue();
        segunda.Value.BureauQueryPublicId.Should().Be(primera.Value.BureauQueryPublicId);
        (await _o.Db.BureauQueries.CountAsync()).Should().Be(1);

        _o.G.HoyLocal = OriginacionDePrueba.Hoy.AddDays(31);
        (await ConsultarAsync(_o.Ana)).Value.Reused.Should().BeFalse("vencida la vigencia se consulta otra vez");
    }

    [Fact]
    public async Task Leer_el_informe_y_pedir_su_enlace_quedan_auditados()
    {
        await ConfigurarAsync();
        var r = await ConsultarAsync(_o.Ana);
        _o.Db.Attachments.Add(new Attachment
        {
            PublicId = _informe, OwnerEntityType = AdjuntosDeModulo.InformeDeCentrales, OwnerEntityPublicId = r.Value.BureauQueryPublicId,
            FileName = "informe.json", ContentType = "application/json", SizeBytes = 10, Sha256Hex = new string('b', 64), StoragePath = "cooperativa/informe.bin",
            Format = FormatoDeAdjunto.Direct, Status = EstadoDeAdjunto.Available, CreatedBy = "test",
        });
        await _o.Db.SaveChangesAsync();

        var detalle = await new GetBureauQueryQueryHandler(_o.Db, _o.G.Alcance, _o.G.Auditoria).Handle(new GetBureauQueryQuery(r.Value.BureauQueryPublicId), default);
        var enlace = await new IssueBureauReportDownloadLinkCommandHandler(_o.Db, _almacen, Options.Create(new LimitesDeAdjuntos()), _o.G.Alcance,
            _o.G.Auditoria, _o.G.Reloj).Handle(new IssueBureauReportDownloadLinkCommand(r.Value.BureauQueryPublicId), default);
        var lista = await new ListBureauQueriesQueryHandler(_o.Db, _o.G.Alcance).Handle(new ListBureauQueriesQuery(_o.Ana.PublicId), default);

        detalle.Value.Artifact!.AttachmentPublicId.Should().Be(_informe);
        detalle.Value.Summary!.NegativeMarks.Should().Be(1);
        enlace.Value.Direct.Should().BeTrue();
        enlace.Value.ExpiresAt.Should().Be(new DateTimeOffset(_o.G.UtcNow.AddSeconds(60), TimeSpan.Zero));
        lista.Value.Items.Should().ContainSingle().Which.Artifact.Should().BeNull("la lista no trae el informe");
        (await _o.G.EventosAsync()).Count(e => e == AuditEventTypes.LendingCreditBureauReportViewed).Should().Be(2);
    }
}
