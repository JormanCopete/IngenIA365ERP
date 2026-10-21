using System.Security.Cryptography;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// El escenario de los documentos al deudor (feature 013, US13, bloque 14A): el de <see cref="PagosDePrueba"/> —el crédito de Ana desembolsado hoy,
/// martes 1 de diciembre de 2026, por 3.000.000, Cartera activada— con el reloj en hora de Colombia, los techos <c>COBRANZA_*</c>, la plantilla
/// <c>ENVIODOC</c>, el firmante configurado, el PDF pintado por un renderizador de prueba (los bytes dependen del número y del código, para que la
/// huella cambie entre documentos) y subido por un <c>ISender</c> que guarda el adjunto <c>Direct</c> como lo haría la subida real. La cooperativa es
/// «Cooperativa de Prueba» con NIT 900123456-7. (nuevo)
/// </summary>
internal sealed class DocumentosDePrueba : IDisposable
{
    private static readonly TimeSpan Colombia = TimeSpan.FromHours(-5);

    private DocumentosDePrueba(PagosDePrueba p)
    {
        P = p;
        G.Reloj.AhoraLocal.Returns(_ => new DateTimeOffset(DateTime.SpecifyKind(G.UtcNow, DateTimeKind.Utc)).ToOffset(Colombia));
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraInicioSabado, 8m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaHoraFinSabado, 15m, "Ley 2300 de 2023");
        G.Limite(CatalogoDeLimitesLegales.CobranzaContactosDia, 1m, "Ley 2300 de 2023");
        Calendario.ObtenerAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<IReadOnlyCollection<DayOfWeek>>(), Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new CalendarioHabil([], ci.ArgAt<IReadOnlyCollection<DayOfWeek>>(2))));
        Canales.Resolver(Arg.Any<string>()).Returns(new Collections.CanalDePrueba(Application.Common.ExternalChannels.CodigosDeCanalExterno.Correo,
            [MessageChannel.Email], consultaEntrega: false, admiteAdjuntos: true));

        Renderizador.Render(Arg.Any<DocumentoDeCarteraParaImprimir>()).Returns(ci =>
        {
            var d = ci.Arg<DocumentoDeCarteraParaImprimir>();
            Pintados.Add(d);
            return System.Text.Encoding.UTF8.GetBytes($"%PDF-1.7 {d.Encabezado.Numero} {d.Encabezado.CodigoDeVerificacion}");
        });
        Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>()).Returns(async ci =>
        {
            var s = ci.Arg<UploadAttachmentCommand>();
            Subidas.Add(s);
            if (FallaLaSubida) return Result.Failure<Guid>("Attachments.StoreUnavailable", "El almacén no respondió.");
            var adjunto = new Attachment
            {
                TenantId = 1, OwnerEntityType = s.OwnerEntityType, OwnerEntityPublicId = s.OwnerEntityPublicId, FileName = s.FileName,
                ContentType = s.ContentType, SizeBytes = s.Content.LongLength, Sha256Hex = Convert.ToHexString(SHA256.HashData(s.Content)).ToLowerInvariant(),
                StoragePath = $"1/2026/12/{Guid.NewGuid():N}.bin", Format = FormatoDeAdjunto.Direct, Status = EstadoDeAdjunto.Available, CreatedBy = "test",
            };
            Db.Attachments.Add(adjunto);
            await Db.SaveChangesAsync();
            Contenidos[adjunto.StoragePath] = s.Content;
            return Result.Success(adjunto.PublicId);
        });
        Almacen.FirmarDescargaAsync(Arg.Any<BlobReference>(), Arg.Any<DescargaFirmada>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EnlaceDeDescarga($"https://almacen.test/{ci.Arg<BlobReference>().Uri}", ci.Arg<DescargaFirmada>().VenceEn));
        Almacen.GetAsync(Arg.Any<BlobReference>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<Stream>(new MemoryStream(Contenidos[ci.Arg<BlobReference>().Uri])));
        Codigos.Generar().Returns(_ => CodigosAEntregar.Count > 0 ? CodigosAEntregar.Dequeue() : new GeneradorDeCodigosDeVerificacion().Generar());
        G.ActuarComo(D.Cajera);
    }

    public static async Task<DocumentosDePrueba> CrearAsync()
    {
        var d = new DocumentosDePrueba(await PagosDePrueba.CrearAsync());
        d.Db.Companies.Add(new Company { Name = "Cooperativa de Prueba", TaxId = "900123456", TaxIdCheckDigit = "7", CreatedBy = "test" });
        d.Db.NotificationTemplates.Add(new NotificationTemplate
        {
            TemplateName = "ENVIODOC v1", Channel = nameof(MessageChannel.Email), Subject = "Su {{ClaseDeDocumento}} {{NumeroDelDocumento}}",
            BodyTemplate = "Señor(a) {{NombreDelTitular}}: adjuntamos su {{ClaseDeDocumento}}. Código de verificación {{CodigoDeVerificacion}}.",
            IsActive = true, Code = EntregaDeDocumentosDeCartera.Plantilla, Module = "Lending", Purpose = nameof(DebtorMessagePurpose.DocumentDelivery),
            Version = 1, ValidFrom = new DateOnly(2020, 1, 1), Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(DebtorMessagePurpose.DocumentDelivery),
            IsSeeded = true, CreatedBy = "semilla",
        });
        await d.Db.SaveChangesAsync();
        await d.ParametroAsync(ParametrosDeCartera.DocumentosFirmanteNombre, "María Gerente");
        await d.ParametroAsync(ParametrosDeCartera.DocumentosFirmanteCargo, "Gerente general");
        d.Db.ChangeTracker.Clear();
        return d;
    }

    public PagosDePrueba P { get; }
    public Disbursements.DesembolsoDePrueba D => P.D;
    public Sessions.GobiernoDePrueba G => P.G;
    public TestApplicationDbContext Db => P.Db;
    public Domain.Entities.Lending.Loans.Loan Credito => P.Credito;
    public Person Ana => D.O.Ana;
    public Person Beto => D.O.Beto;

    public ICalendarioHabil Calendario { get; } = Substitute.For<ICalendarioHabil>();
    public ICanalesDeMensajeria Canales { get; } = Substitute.For<ICanalesDeMensajeria>();
    public IDocumentoDeCarteraRenderer Renderizador { get; } = Substitute.For<IDocumentoDeCarteraRenderer>();
    public ISender Sender { get; } = Substitute.For<ISender>();
    public IBlobStore Almacen { get; } = Substitute.For<IBlobStore>();
    public IGeneradorDeCodigosDeVerificacion Codigos { get; } = Substitute.For<IGeneradorDeCodigosDeVerificacion>();
    public Queue<string> CodigosAEntregar { get; } = new();
    public List<DocumentoDeCarteraParaImprimir> Pintados { get; } = [];
    public List<UploadAttachmentCommand> Subidas { get; } = [];
    public Dictionary<string, byte[]> Contenidos { get; } = [];
    public bool FallaLaSubida { get; set; }
    public bool EnProduccion { get; set; }

    public void Dispose() => P.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    public ColaDeMensajesAlDeudor Cola() =>
        new(Db, G.Reloj, D.Parametros(), Calendario, D.O.Autorizaciones, Canales, Options.Create(D.O.Opciones));

    public EntregaDeDocumentosDeCartera Entrega() => new(Cola(), G.Reloj);

    public ConstructorDeDocumentosDeCartera Constructor() =>
        new(Db, P.C.Cargador(), G.Alcance, G.Auditoria, D.Parametros(), P.Firmeza(), G.Reloj);

    /// <summary>La entrega de Cartera con que emite el manejador (nula: la vigente, K5 desde T0969).</summary>
    public Domain.Lending.EntregaDeCartera? EntregaDeCartera { get; set; }

    public IssueLendingDocumentCommandHandler Emisor()
    {
        var ambiente = Substitute.For<IAmbienteDeDespliegue>();
        ambiente.EsProduccion.Returns(_ => EnProduccion);
        return new(Db, Constructor(), Entrega(), Codigos, D.Numerador(), D.O.Cerrojo, Renderizador, Sender, Almacen,
            Options.Create(new LimitesDeAdjuntos()), G.Alcance, G.Auditoria, G.Actor, G.Reloj,
            Options.Create(new IdentityEmailOptions { BaseUrl = "https://erp.coop.test/" }), ambiente, entregaVigente: EntregaDeCartera);
    }

    public ListIssuedLendingDocumentsQueryHandler Listar() => new(Db, G.Alcance, G.Reloj);
    public GetIssuedLendingDocumentQueryHandler Detalle() => new(Db, G.Alcance, G.Auditoria, G.Reloj);
    public VerifyLendingDocumentQueryHandler Verificar() => new(Db, G.Auditoria, G.Reloj);
    public IssueLendingDocumentDownloadLinkCommandHandler Enlace() =>
        new(Db, Almacen, Options.Create(new LimitesDeAdjuntos()), G.Alcance, G.Auditoria, G.Reloj);
    public VoidLendingDocumentCommandHandler Anular() => new(Db, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public SendLendingDocumentCommandHandler Enviar() => new(Db, Entrega(), G.Alcance, G.Auditoria, G.Actor);

    // ------------------------------------------------------------------------------------------------------- escenario --

    public static IssueLendingDocumentCommand Pedido(IssuedDocumentKind clase, Guid persona, Guid? credito = null, DateOnly? corte = null,
        DocumentDeliveryMode entrega = DocumentDeliveryMode.Download) =>
        new(clase, persona, credito, corte, null, null, entrega) { OperationKey = Guid.NewGuid() };

    /// <summary>K5: el pedido de un extracto del período o de un certificado tributario del año.</summary>
    public static IssueLendingDocumentCommand Extracto(Guid persona, Guid credito, DateOnly? desde, DateOnly? hasta,
        DocumentDeliveryMode entrega = DocumentDeliveryMode.Download) =>
        new(IssuedDocumentKind.Statement, persona, credito, null, desde is null || hasta is null ? null : new PeriodoDeDocumento(desde.Value, hasta.Value),
            null, entrega) { OperationKey = Guid.NewGuid() };

    public static IssueLendingDocumentCommand CertificadoTributario(Guid persona, short? año, DocumentDeliveryMode entrega = DocumentDeliveryMode.Download) =>
        new(IssuedDocumentKind.TaxCertificate, persona, null, null, null, año, entrega) { OperationKey = Guid.NewGuid() };

    public async Task<Result<IssuedLendingDocumentDto>> EmitirAsync(IssueLendingDocumentCommand pedido)
    {
        var r = await Emisor().Handle(pedido, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public async Task<IssuedLendingDocumentDto> EmitidoAsync(IssueLendingDocumentCommand pedido)
    {
        var r = await EmitirAsync(pedido);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Paga todo el crédito de Ana hoy en efectivo (lo deja cancelado) y devuelve la operación del pago.</summary>
    public async Task<Guid> CancelarAsync()
    {
        var c = await P.AplicadoAsync(P.Pago(Application.Common.Integration.Lending.ModalidadDePago.FullPayoff, 3_000_000m));
        Db.ChangeTracker.Clear();
        return c.OperationPublicId;
    }

    public async Task<IssuedLendingDocument> DocumentoAsync(Guid publicId) =>
        await Db.IssuedLendingDocuments.AsNoTracking().SingleAsync(d => d.PublicId == publicId);

    public async Task<IReadOnlyList<IssuedDocumentEvent>> EventosAsync(Guid publicId)
    {
        var d = await DocumentoAsync(publicId);
        return await Db.IssuedDocumentEvents.AsNoTracking().Where(e => e.IssuedDocumentId == d.Id).OrderBy(e => e.Id).ToListAsync();
    }

    public async Task ParametroAsync(string clave, string valor)
    {
        Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = G.UtcNow.AddDays(-10),
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>La emisión por la tubería de la clave (<c>IdempotencyBehavior</c>), como la llama la ruta.</summary>
    public async Task<Result<IssuedLendingDocumentDto>> ConClaveAsync(IssueLendingDocumentCommand pedido)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IApplicationDbContext>(Db);
        servicios.AddSingleton(G.Actor);
        servicios.AddSingleton(Substitute.For<IAuditService>());
        servicios.AddSingleton(new EstadoDeLaOperacion());
        var behavior = new IdempotencyBehavior<IssueLendingDocumentCommand, Result<IssuedLendingDocumentDto>>(servicios.BuildServiceProvider(),
            NullLogger<IdempotencyBehavior<IssueLendingDocumentCommand, Result<IssuedLendingDocumentDto>>>.Instance);
        var r = await behavior.Handle(pedido, _ => Emisor().Handle(pedido, default), default);
        Db.ChangeTracker.Clear();
        return r;
    }
}
