using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Treasury.Integration;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Orders;

/// <summary>
/// Feature 015, US10, T0253 (E-01 c; contratos-v2.md §2.3; FR-108, FR-164): Cartera informa a Tesorería el archivo de desembolsos que generó
/// (<c>ArchivoDeDesembolsosGenerado</c> v1, <c>CAR → Treasury</c>, clave <c>Confirmation:{archivo:N}</c>); Tesorería lo guarda con sus órdenes, se las anota y no las
/// paga; el anulado (<c>ArchivoDeDesembolsosAnulado</c> v1) suelta las que siguen sin resultado, a la espera de otro archivo de Cartera; repetir el mensaje no hace
/// nada. Prueba escrita, sin correr.
/// </summary>
public class ConsumoDelArchivoDeDesembolsosTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Guid _archivo = Guid.NewGuid();
    private readonly Guid _ordenDeCartera = Guid.NewGuid();

    public ConsumoDelArchivoDeDesembolsosTests() => _reloj.UtcNow.Returns(new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc));

    private OrdenDelArchivoV1[] Ordenes => [new() { OrderPublicId = _ordenDeCartera, OrderNumber = "OG-1", NetAmount = 1_000_000m, PayeePersonPublicId = Guid.NewGuid() }];

    private MensajeEntrante Mensaje(object contenido, string tipo)
    {
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = Guid.NewGuid(), Type = tipo, Version = 1, OriginModule = ModulosDeOrigen.Cartera, OriginEventKey = ClavesDeEvento.ConfirmacionPor(_archivo),
            Origin = new MessageOriginV1 { PublicId = _archivo, Number = "ARD-1", OperationDate = new DateOnly(2026, 11, 3) }, Payload = contenido,
        };
        var m = new MensajeEntrante(sobre, contenido, "{}", "sha", null,
            new EntregaEntrante(IntegrationDestinations.Treasury, Domain.Enums.Integration.DeliveryMode.Always, Domain.Enums.Integration.DeliveryStatus.Pending, null, null, null, 0, null));
        _entrantes.LeerAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(sobre.MessageId)), IntegrationDestinations.Treasury, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>([m]));
        return m;
    }

    private Task<Result<ResultadoDeConsumo>> ConsumirAsync(MensajeEntrante m) =>
        new ConsumeLendingDisbursementFileCommandHandler(_db, _entrantes, _reloj).Handle(new ConsumeLendingDisbursementFileCommand([m.MessageId]), default);

    private async Task<PaymentOrder> OrdenAsync()
    {
        var orden = new PaymentOrder
        {
            Number = "TES-1", Origin = PaymentOrderOrigin.LendingDisbursement, OriginOrderPublicId = _ordenDeCartera, OriginOrderNumber = "OG-1", OrderKind = "LoanDisbursement",
            BeneficiaryPersonId = 1, NetAmount = 1_000_000m, GrossAmount = 1_000_000m,
        };
        _db.PaymentOrders.Add(orden);
        await _db.SaveChangesAsync();
        return orden;
    }

    [Fact]
    public async Task El_generado_guarda_el_archivo_con_sus_ordenes_y_se_las_anota_sin_pagarlas()
    {
        var orden = await OrdenAsync();
        var m = Mensaje(new ArchivoDeDesembolsosGeneradoV1
        {
            FilePublicId = _archivo, FileNumber = "ARD-1", Orders = Ordenes, Total = 1_000_000m, LineCount = 1,
            File = new ArchivoDeDesembolsosAdjuntoV1 { AttachmentPublicId = Guid.NewGuid(), FileName = "ard-1.txt", Sha256 = new string('a', 64), SizeBytes = 120 },
            GeneratedBy = new UserRefV1 { Name = "tesorera" },
        }, ArchivoDeDesembolsosGeneradoV1.Type);

        (await ConsumirAsync(m)).Value.Should().BeOfType<ResultadoDeConsumo.Processed>();

        var archivo = await _db.LendingDisbursementFiles.Include(f => f.Orders).SingleAsync();
        archivo.Status.Should().Be(LendingDisbursementFileState.Generated);
        archivo.Orders.Single().PaymentOrderId.Should().Be(orden.Id);
        var leida = await _db.PaymentOrders.SingleAsync();
        leida.LendingDisbursementFileId.Should().Be(archivo.Id);
        leida.PaidByLendingFile.Should().BeTrue("Tesorería no la paga: espera su resultado");
        (await ConsumirAsync(m)).Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>("repetir el mensaje no hace nada");
    }

    [Fact]
    public async Task El_anulado_suelta_las_ordenes_sin_resultado_y_un_anulado_desconocido_se_rechaza()
    {
        await OrdenAsync();
        var generado = Mensaje(new ArchivoDeDesembolsosGeneradoV1 { FilePublicId = _archivo, FileNumber = "ARD-1", Orders = Ordenes }, ArchivoDeDesembolsosGeneradoV1.Type);
        await ConsumirAsync(generado);
        var anulado = Mensaje(new ArchivoDeDesembolsosAnuladoV1 { FilePublicId = _archivo, FileNumber = "ARD-1", Orders = Ordenes, Reason = "Cuenta errada" },
            ArchivoDeDesembolsosAnuladoV1.Type);

        (await ConsumirAsync(anulado)).Value.Should().BeOfType<ResultadoDeConsumo.Processed>();

        (await _db.LendingDisbursementFiles.SingleAsync()).Status.Should().Be(LendingDisbursementFileState.Voided);
        (await _db.PaymentOrders.SingleAsync()).LendingDisbursementFileId.Should().BeNull("espera otro archivo de Cartera");

        var desconocido = Mensaje(new ArchivoDeDesembolsosAnuladoV1 { FilePublicId = Guid.NewGuid(), FileNumber = "ARD-9", Reason = "x" }, ArchivoDeDesembolsosAnuladoV1.Type);
        (await ConsumirAsync(desconocido)).Value.Should().BeOfType<ResultadoDeConsumo.Rejected>()
            .Which.Code.Should().Be(ConsumeLendingDisbursementFileCommandHandler.CodigoArchivoDesconocido);
    }

    [Fact]
    public void Cartera_los_declara_hacia_Tesoreria_con_la_clave_por_archivo_y_Tesoreria_los_acepta()
    {
        foreach (var tipo in new[] { ArchivoDeDesembolsosGeneradoV1.Type, ArchivoDeDesembolsosAnuladoV1.Type })
        {
            var t = CatalogoDeMensajes.Buscar(tipo, 1)!;
            t.OriginModule.Should().Be(ModulosDeOrigen.Cartera);
            t.AdmiteDestino(IntegrationDestinations.Treasury).Should().BeTrue();
            ClavesDeEvento.EsValida(t.FormaDeClave, ClavesDeEvento.ConfirmacionPor(Guid.NewGuid())).Should().BeTrue();
            DestinoTesoreria.Aceptados.Should().Contain((tipo, 1));
        }
    }
}
