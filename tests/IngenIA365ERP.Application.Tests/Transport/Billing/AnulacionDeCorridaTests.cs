using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Billing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Transport;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Billing;

/// <summary>
/// 2026-11-25 (pedido del dueño: anular la facturación masiva y volver a facturar): «Anular la corrida» anula por Comercial cada factura
/// abierta y sin pagos —un comprobante no electrónico con su documento contrario, una factura electrónica con su nota de anulación total— y
/// deja sus cargos anulados en la misma petición; lo pagado, abonado o avisado se omite con su motivo; lo que Comercial rechaza también, y la
/// pantalla no lo vuelve a intentar; una corrida en curso no se anula.
/// </summary>
public class AnulacionDeCorridaTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 5);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ISender _sender = Substitute.For<ISender>();
    private BillingRun _corrida = null!;
    private Person _dueño = null!;
    private TransportService _servicio = null!;
    private int _numero;

    public AnulacionDeCorridaTests()
    {
        _sender.Send(Arg.Any<VoidInventoryDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new VoidResultDto(Guid.NewGuid(), "AN1", DocumentStatus.Confirmed, Hoy, null, null)));
    }

    private async Task SembrarAsync(BillingRunStatus estado = BillingRunStatus.Finished)
    {
        _dueño = new Person { TaxId = "1234567", FirstName = "Ana", LastName = "Prueba" };
        _db.People.Add(_dueño);
        _servicio = new TransportService { ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        _db.TransportServices.Add(_servicio);
        _corrida = new BillingRun { Period = "202612", Mode = BillingMode.Batch, Status = estado };
        _db.TransportBillingRuns.Add(_corrida);
        await _db.SaveChangesAsync();
    }

    /// <summary>Una factura confirmada de la clase dada con su cargo de la corrida.</summary>
    private async Task<ServiceCharge> FacturaAsync(DocumentClass clase, ChargeStatus estado = ChargeStatus.Open, bool avisado = false)
    {
        _numero++;
        var factura = new InventoryDocument { Class = clase, DocumentTypeId = 1, Prefix = "RV", Number = _numero, OperationDate = Hoy, Currency = "COP", BranchId = 1 };
        factura.Lines.Add(new InventoryDocumentLine
        {
            Document = factura, LineNumber = 1, ProductId = 1, UnitId = 1, Quantity = 1m, UnitPrice = 73_425m, GrossAmount = 73_425m, NetAmount = 73_425m,
        });
        factura.Confirmar(1, DateTime.UtcNow);
        _db.InventoryDocuments.Add(factura);
        var vehiculo = Vehicle.Registrar(_dueño, Hoy.AddYears(-1));
        (vehiculo.Plate, vehiculo.InternalNumber) = ($"AAA{_numero:000}", $"{_numero:0000}");
        var cargo = new ServiceCharge
        {
            Vehicle = vehiculo, Service = _servicio, Period = "202612", OwnerPersonId = _dueño.Id, InvoicePublicId = factura.PublicId,
            InvoiceNumber = $"RV{_numero}", InvoiceLinePublicId = factura.Lines.Single().PublicId, Amount = 73_425m, Route = ChargeRoute.Lending,
            BillingRunId = _corrida.Id, Status = estado, NotifiedRequestPublicId = avisado ? Guid.NewGuid() : null,
        };
        _db.ServiceCharges.Add(cargo);
        await _db.SaveChangesAsync();
        return cargo;
    }

    private Task<Result<BillingRunVoidDto>> AnularAsync(int tanda = 10, IReadOnlyList<Guid>? saltar = null) =>
        new VoidServiceBillingRunCommandHandler(_db, _sender).Handle(new VoidServiceBillingRunCommand(_corrida.PublicId, "Se facturó noviembre por error", tanda, saltar),
            CancellationToken.None);

    [Fact]
    public async Task Anula_las_abiertas_y_sin_pagos_y_omite_las_pagadas_y_avisadas_con_su_motivo()
    {
        await SembrarAsync();
        var abierta = await FacturaAsync(DocumentClass.NonElectronicSalesReceipt);
        var pagada = await FacturaAsync(DocumentClass.NonElectronicSalesReceipt, ChargeStatus.Paid);
        var avisada = await FacturaAsync(DocumentClass.NonElectronicSalesReceipt, avisado: true);

        var r = (await AnularAsync()).Value;

        r.VoidedInvoices.Should().Be(1);
        r.VoidedCharges.Should().Be(1);
        r.Pending.Should().Be(0);
        abierta.Status.Should().Be(ChargeStatus.Voided);
        pagada.Status.Should().Be(ChargeStatus.Paid);
        avisada.Status.Should().Be(ChargeStatus.Open);
        r.Omitted.Select(o => (o.InvoiceNumber, o.Reason)).Should().BeEquivalentTo(
            [(pagada.InvoiceNumber, "Ya está pagada."), (avisada.InvoiceNumber, "Ya se avisó al sistema externo.")]);
        await _sender.Received(1).Send(Arg.Is<VoidInventoryDocumentCommand>(c => c.DocumentPublicId == abierta.InvoicePublicId
            && c.ExpectedGroup == DocumentClassGroup.Sales && c.Reason == "Se facturó noviembre por error"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_factura_electronica_se_corrige_con_su_nota_de_anulacion_total_y_lo_que_Comercial_rechaza_se_omite()
    {
        await SembrarAsync();
        var fe = await FacturaAsync(DocumentClass.SalesInvoice);
        _sender.Send(Arg.Any<SaveCreditNoteDraftCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<InventoryDocumentDto>("Inventory.CreditNote.TypeMissing", "No hay un tipo de nota crédito activo."));

        var r = (await AnularAsync()).Value;

        await _sender.Received(1).Send(Arg.Is<SaveCreditNoteDraftCommand>(c => c.DocumentPublicId == null && c.Draft.OriginDocumentPublicId == fe.InvoicePublicId
            && c.Draft.TotalVoid && !c.Draft.WithReturn && c.Draft.Lines.Count == 0 && c.Draft.Refunds.Count == 0), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<VoidInventoryDocumentCommand>(), Arg.Any<CancellationToken>());
        r.VoidedInvoices.Should().Be(0);
        r.Omitted.Should().ContainSingle().Which.Reason.Should().Be("No hay un tipo de nota crédito activo.");
        fe.Status.Should().Be(ChargeStatus.Open, "si Comercial no la anuló, el cargo sigue vivo");

        var otra = (await AnularAsync(saltar: [fe.InvoicePublicId])).Value;
        otra.Omitted.Should().BeEmpty("la pantalla no vuelve a intentar las que fallaron");
        otra.Pending.Should().Be(0);
    }

    [Fact]
    public async Task Va_por_tandas_y_dice_cuantas_faltan()
    {
        await SembrarAsync();
        for (var i = 0; i < 3; i++) await FacturaAsync(DocumentClass.NonElectronicSalesReceipt);

        var primera = (await AnularAsync(tanda: 2)).Value;
        var segunda = (await AnularAsync(tanda: 2)).Value;

        (primera.VoidedInvoices, primera.Pending).Should().Be((2, 1));
        (segunda.VoidedInvoices, segunda.Pending).Should().Be((1, 0));
        _db.ServiceCharges.Should().OnlyContain(c => c.Status == ChargeStatus.Voided);
    }

    [Fact]
    public async Task Una_corrida_en_curso_no_se_anula()
    {
        await SembrarAsync(BillingRunStatus.Running);

        (await AnularAsync()).Error.Code.Should().Be("Billing.RunNotFinished");
    }

    [Fact]
    public void El_motivo_es_obligatorio()
    {
        new VoidServiceBillingRunCommandValidator().Validate(new VoidServiceBillingRunCommand(Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
    }
}
