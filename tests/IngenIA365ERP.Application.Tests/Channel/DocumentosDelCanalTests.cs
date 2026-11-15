using FluentAssertions;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Channel;

/// <summary>
/// Feature 021, US9 (T127; FR-055): el canal ofrece sólo los documentos que la cooperativa publica en los conceptos del asociado, y los emite con el motor de
/// Cartera en modo descarga, marcados «por el canal». Un documento no publicado o un producto que no es de la persona no existe para ella.
/// </summary>
public sealed class DocumentosDelCanalTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Guid _persona = Guid.NewGuid();
    private readonly Guid _credito = Guid.NewGuid();
    private readonly Guid _ahorro = Guid.NewGuid();

    public DocumentosDelCanalTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 12, 1));
        _db.ChannelPublishedConcepts.Add(new ChannelPublishedConcept { Kind = ConceptKind.Loan, PublishStatement = true, PublishTaxCertificate = true });
        _db.ChannelPublishedConcepts.Add(new ChannelPublishedConcept { Kind = ConceptKind.ContractualSaving, PublishStatement = false });
        _db.ChannelPublishedConcepts.Add(new ChannelPublishedConcept { Kind = ConceptKind.Contribution, PublishCertificate = true });
        _db.SaveChanges();
        _sender.Send(Arg.Is<GetChannelPayablesQuery>(q => q.PersonPublicId == _persona), Arg.Any<CancellationToken>()).Returns(Result.Success(
            new ChannelPayablesDto(new DateOnly(2026, 12, 1),
                [Producto("Loan", _credito, "CR-1"), Producto("ContractualSaving", _ahorro, "AC-9"), Producto("Contribution", Guid.NewGuid(), "AP-1")], [], [])));
        _sender.Send(Arg.Any<ListChannelDocumentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => Listar().Handle(ci.ArgAt<ListChannelDocumentsQuery>(0), default));
    }

    private static ChannelPayableDto Producto(string clase, Guid producto, string numero) =>
        new($"X:{producto}", clase, "L1", numero, producto, numero, 0m, 0m, 0m, 0m, 0m, null, null, 0, null, false, new ModosDelConceptoDto(false, null, null, false), true,
            null, []);

    private ListChannelDocumentsQueryHandler Listar() => new(_db, _sender);

    private IssueChannelDocumentCommandHandler Emitir() =>
        new(_db, _sender, Substitute.For<IBlobStore>(), _reloj, new AuditoriaDelCanal(new ServiceCollection().BuildServiceProvider(), NullLogger<AuditoriaDelCanal>.Instance));

    [Fact]
    public async Task Lista_solo_lo_publicado_de_los_productos_de_la_persona()
    {
        var r = await Listar().Handle(new ListChannelDocumentsQuery(_persona), default);

        r.Value.Select(d => (d.Kind, d.ProductPublicId)).Should().BeEquivalentTo(new (string, Guid?)[]
        {
            (nameof(IssuedDocumentKind.Statement), _credito),
            (nameof(IssuedDocumentKind.TaxCertificate), null),
            (nameof(IssuedDocumentKind.ContributionCertificate), null),
        });
        r.Value.Single(d => d.Kind == nameof(IssuedDocumentKind.Statement)).Parameter.Should().Be("period");
    }

    [Fact]
    public async Task Un_documento_no_publicado_no_esta_disponible()
    {
        var r = await Emitir().Handle(new IssueChannelDocumentCommand(_persona, nameof(IssuedDocumentKind.AccountStatement), _ahorro, null, null, null), default);

        r.Error.Code.Should().Be(ErroresDeDocumentosDelCanal.NoDisponible);
        await _sender.DidNotReceive().Send(Arg.Any<IssueLendingDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_producto_de_otra_persona_no_esta_disponible()
    {
        var r = await Emitir().Handle(new IssueChannelDocumentCommand(_persona, nameof(IssuedDocumentKind.Statement), Guid.NewGuid(), null, null, null), default);

        r.Error.Code.Should().Be(ErroresDeDocumentosDelCanal.NoDisponible);
    }

    [Fact]
    public async Task El_extracto_lo_emite_Cartera_en_descarga_marcado_por_el_canal_con_tres_meses_por_defecto()
    {
        _sender.Send(Arg.Any<IssueLendingDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<IssuedLendingDocumentDto>("Lending.Document.SignerMissing", "Falta el firmante."));

        var r = await Emitir().Handle(new IssueChannelDocumentCommand(_persona, nameof(IssuedDocumentKind.Statement), _credito, null, null, null), default);

        r.Error.Code.Should().Be("Lending.Document.SignerMissing");
        await _sender.Received(1).Send(Arg.Is<IssueLendingDocumentCommand>(c => c.PorElCanal && c.Kind == IssuedDocumentKind.Statement
            && c.LoanPublicId == _credito && c.PersonPublicId == _persona && c.Delivery == DocumentDeliveryMode.Download
            && c.Period == new PeriodoDeDocumento(new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1))), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Por_el_canal_no_viaja_en_el_json()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new IssueLendingDocumentCommand(IssuedDocumentKind.Statement, _persona, _credito, null, null, null,
            DocumentDeliveryMode.Download) { PorElCanal = true });
        json.Should().NotContain(nameof(IssueLendingDocumentCommand.PorElCanal));
    }
}
