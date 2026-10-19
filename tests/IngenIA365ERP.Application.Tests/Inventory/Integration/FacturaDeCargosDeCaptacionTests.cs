using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 014 (decisión del dueño del 2026-10-18, research §13 D51; cierra T117): Comercial consume el cargo gravado de una cuenta de captación y deja
/// lista su factura de venta por el flujo de ventas (un borrador con el ítem del cargo, el titular y la referencia <c>CGO:{cargo:N}</c>); sin el ítem en el
/// catálogo, el mensaje se rechaza para reprocesarlo.
/// </summary>
public class FacturaDeCargosDeCaptacionTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILectorDeParametros _parametros = Substitute.For<ILectorDeParametros>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public FacturaDeCargosDeCaptacionTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 12, 1));
        _parametros.LeerAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<IngenIA365ERP.Domain.Enums.Parameters.ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ValorDeParametro>(Error.NotFound));
    }

    private ConsumeDepositChargeMessageCommandHandler Handler() => new(_db, _entrantes, _sender, _parametros, _reloj);

    private Guid Mensaje(object contenido, string tipo)
    {
        var id = Guid.NewGuid();
        var sobre = new IntegrationEnvelopeV1 { MessageId = id, Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "Lending" };
        _entrantes.LeerAsync(Arg.Is<IReadOnlyList<Guid>>(l => l.Contains(id)), IntegrationDestinations.Inventory, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>(
            [
                new MensajeEntrante(sobre, contenido, "{}", "sha", null,
                    new EntregaEntrante(IntegrationDestinations.Inventory, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null)),
            ]));
        return id;
    }

    private static CargoDeCaptacionCausadoV1 Causado(string? item) => new()
    {
        Charge = new DepositChargeInvoiceRefV1 { ChargePublicId = Guid.NewGuid(), Kind = "Periodic", Code = "CUOTAMANEJO", Name = "Cuota de manejo", CommercialItemCode = item },
        Account = new MemberAccountRefV1 { AccountPublicId = Guid.NewGuid(), AccountNumber = "AHVISTA-000001" },
        HolderPersonPublicId = Guid.NewGuid(),
        AmountBeforeTax = 10_000m,
        TaxConceptCode = "IVA19",
        Status = "Collected",
        CausedOn = new DateOnly(2026, 11, 30),
    };

    [Fact]
    public async Task El_cargo_causado_deja_lista_la_factura_del_servicio()
    {
        var unidad = new UnitOfMeasure { Code = "UND", Name = "Unidad", CreatedBy = "test" };
        _db.UnitsOfMeasure.Add(unidad);
        _db.SaveChanges();
        _db.Products.Add(new Product { Code = "SRVMANEJO", Name = "Cuota de manejo", Kind = ProductKind.Service, BaseUnitId = unidad.Id, CategoryId = 1, CreatedBy = "test" });
        _db.InventoryDocumentTypes.Add(new InventoryDocumentType { Code = "FV", Name = "Factura de venta", Class = DocumentClass.SalesInvoice, CreatedBy = "test" });
        _db.Warehouses.Add(new Warehouse { Code = "PPAL", Name = "Principal", CreatedBy = "test" });
        _db.SaveChanges();
        // El guardado del borrador lo hace el flujo de ventas (aquí, un sustituto que responde lo que respondería sin alcance de bodega).
        _sender.Send(Arg.Any<SaveInventoryDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InventoryDocumentDto>(new Error("Inventory.Scope.Denied", "Sin alcance en la prueba.")));
        var cargo = Causado("SRVMANEJO");

        var r = await Handler().Handle(new ConsumeDepositChargeMessageCommand([Mensaje(cargo, CargoDeCaptacionCausadoV1.Type)]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Inventory.Scope.Denied", "lo que responde el flujo de ventas vuelve a la bandeja");
        await _sender.Received(1).Send(Arg.Is<SaveInventoryDraftCommand>(c => c.ExpectedGroup == DocumentClassGroup.Sales
            && c.Draft.ExternalReference == ConsumeDepositChargeMessageCommandHandler.Referencia(cargo.Charge.ChargePublicId)
            && c.Draft.CounterpartyPersonPublicId == cargo.HolderPersonPublicId && c.Draft.Lines.Single().UnitPrice == 10_000m
            && c.Draft.Lines.Single().Quantity == 1m), Arg.Any<CancellationToken>());
        new DestinoInventario(_sender, NullLogger<DestinoInventario>.Instance).Acepta(CargoDeCaptacionReversadoV1.Type, 1).Should().BeTrue();
    }

    [Fact]
    public async Task Sin_el_item_en_el_catalogo_se_rechaza_para_reprocesar()
    {
        var r = await Handler().Handle(new ConsumeDepositChargeMessageCommand([Mensaje(Causado("NOEXISTE"), CargoDeCaptacionCausadoV1.Type)]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ConsumeDepositChargeMessageCommandHandler.CodigoItemInexistente);
        await _sender.DidNotReceive().Send(Arg.Any<SaveInventoryDraftCommand>(), Arg.Any<CancellationToken>());
    }
}
