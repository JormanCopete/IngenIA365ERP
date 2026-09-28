using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I4, T736, T737, T743 y la ejecución después del commit (T734): la espera en línea del POS (validado → tirilla con el código y
/// entrega anotada; espera cumplida → pendiente de entrega, sin tirilla y una falla al circuito; contingencia 03 → tirilla de papel sin esperar;
/// rechazo → motivos sin tirilla), el aviso temprano al abrir la caja (todos los faltantes y el tipo de contingencia que falta, sin bloquear), la
/// entrega de un documento electrónico (no entregable antes de validarse; tirilla con el bloque electrónico; carta por el enlace firmado; la
/// lista de pendientes de entrega por su estado) y <see cref="TareasTrasElCommit"/>.
/// </summary>
public class EsperaYEntregaElectronicaTests
{
    private static CheckoutResultDto Cobro(Guid venta) =>
        new(venta, DocumentStatus.Confirmed, null, DocumentClass.SalesInvoice, "SETP", 1, 4000m, 4000m, 0m, null, null, null, []);

    private static async Task<(VentaElectronicaDePrueba E, Guid Venta)> FacturadaAsync()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        var (venta, r) = await e.FacturaAsync();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        return (e, venta);
    }

    private static ElectronicTransmissionResultDto Resultado(Guid id, ElectronicDocumentStatus estado, params MensajeDelCanalDto[] mensajes) =>
        new(id, estado, null, mensajes, true);

    // ------------------------------------------------------------------------------------------------ T736 --

    [Fact]
    public async Task Validado_dentro_de_la_espera_devuelve_la_tirilla_con_el_codigo_y_anota_la_entrega()
    {
        var (e, venta) = await FacturadaAsync();
        var electronico = e.Electronico(venta);
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<EmitElectronicDocumentCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            e.Estado(venta, ElectronicDocumentStatus.Validated, new string('c', 96));
            e.Electronico(venta).QrContent = "https://qr/simulado";
            e.Db.SaveChanges();
            return Result.Success(Resultado(electronico.PublicId, ElectronicDocumentStatus.Validated));
        });
        var fallas = Substitute.For<IRegistroDeFallasDelCanal>();
        var espera = new EsperaEnLineaDelPos(e.Db, sender, e.V.K.Lector(), e.V.Compras.C.Reloj, new ConstructorDeTirilla(e.Db), fallas);

        var r = await espera.CompletarAsync(Cobro(venta), CashRegisterPrintFormat.Ticket80, default);

        var bloque = r.Electronic.Should().BeOfType<ElectronicoDeLaVentaDto>().Subject;
        bloque.Status.Should().Be(ElectronicDocumentStatus.Validated);
        bloque.Deliverable.Should().BeTrue();
        bloque.PendingDelivery.Should().BeFalse();
        r.Ticket.Should().NotBeNull();
        r.Ticket!.Electronic!.UniqueCode.Should().Be(new string('c', 96));
        r.Ticket.Electronic.QrContent.Should().Be("https://qr/simulado");
        r.Ticket.Electronic.Legend.Should().Contain(LeyendasDeRepresentacion.SinValidezFiscal, "ambiente de pruebas");
        e.Electronico(venta).DeliveredAt.Should().NotBeNull();
        await fallas.DidNotReceive().RegistrarFallaAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cumplida_la_espera_queda_pendiente_de_entrega_sin_tirilla_y_cuenta_una_falla()
    {
        var (e, venta) = await FacturadaAsync();
        e.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.EsperaMaximaPosSegundos,
            ScopeKind = ParameterScopeKind.None, Value = "1", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        await e.Db.SaveChangesAsync();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<EmitElectronicDocumentCommand>(), Arg.Any<CancellationToken>()).Returns(async c =>
        {
            await Task.Delay(Timeout.Infinite, c.Arg<CancellationToken>());
            return Result.Success(Resultado(Guid.Empty, ElectronicDocumentStatus.Pending));
        });
        var fallas = Substitute.For<IRegistroDeFallasDelCanal>();
        var espera = new EsperaEnLineaDelPos(e.Db, sender, e.V.K.Lector(), e.V.Compras.C.Reloj, new ConstructorDeTirilla(e.Db), fallas);

        var r = await espera.CompletarAsync(Cobro(venta), CashRegisterPrintFormat.Ticket80, default);

        var bloque = (ElectronicoDeLaVentaDto)r.Electronic!;
        bloque.PendingDelivery.Should().BeTrue();
        bloque.Deliverable.Should().BeFalse();
        bloque.WaitedMs.Should().BeGreaterThanOrEqualTo(900);
        r.Ticket.Should().BeNull();
        e.V.Documento(venta).Number.Should().Be(1, "la venta no se renumera");
        await fallas.Received(1).RegistrarFallaAsync(GuardiaDeEmisionFiscal.CanalSimulado, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task En_contingencia_03_entrega_la_tirilla_de_papel_en_el_acto_y_el_rechazo_no_entrega()
    {
        var (e, venta) = await FacturadaAsync();
        e.Estado(venta, ElectronicDocumentStatus.IssuerContingency);
        e.Electronico(venta).ContingencyType = ContingencyType.Issuer03;
        await e.Db.SaveChangesAsync();
        var sender = Substitute.For<ISender>();
        var espera = new EsperaEnLineaDelPos(e.Db, sender, e.V.K.Lector(), e.V.Compras.C.Reloj, new ConstructorDeTirilla(e.Db));

        var papel = await espera.CompletarAsync(Cobro(venta), CashRegisterPrintFormat.Ticket80, default);

        ((ElectronicoDeLaVentaDto)papel.Electronic!).Deliverable.Should().BeTrue();
        papel.Ticket!.Electronic!.Legend.Should().Contain("contingencia");
        await sender.DidNotReceive().Send(Arg.Any<EmitElectronicDocumentCommand>(), Arg.Any<CancellationToken>());

        var (e2, rechazada) = await FacturadaAsync();
        var id = e2.Electronico(rechazada).PublicId;
        var sender2 = Substitute.For<ISender>();
        sender2.Send(Arg.Any<EmitElectronicDocumentCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            e2.Estado(rechazada, ElectronicDocumentStatus.Rejected);
            return Result.Success(Resultado(id, ElectronicDocumentStatus.Rejected, new MensajeDelCanalDto("FAK24", "Rechazo", "NIT inválido", "La identificación no es válida")));
        });
        var r = await new EsperaEnLineaDelPos(e2.Db, sender2, e2.V.K.Lector(), e2.V.Compras.C.Reloj, new ConstructorDeTirilla(e2.Db))
            .CompletarAsync(Cobro(rechazada), CashRegisterPrintFormat.Ticket80, default);
        r.Ticket.Should().BeNull();
        var bloque = (ElectronicoDeLaVentaDto)r.Electronic!;
        bloque.Status.Should().Be(ElectronicDocumentStatus.Rejected);
        bloque.Messages!.Single().Translation.Should().Be("La identificación no es válida");
        bloque.PendingDelivery.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------ T735 --

    [Fact]
    public async Task Con_la_03_abierta_la_venta_de_la_caja_toma_el_tipo_del_rol_de_contingencia()
    {
        var (e, _) = await FacturadaAsync();
        var contingencia = new Domain.Entities.Inventory.Documents.InventoryDocumentType
        {
            Code = "FVC", Name = "Factura de contingencia", Class = DocumentClass.SalesInvoice, FiscalPrefix = "CFE", IsContingency = true, IsActive = true, AllWarehouses = true,
        };
        e.Db.InventoryDocumentTypes.Add(contingencia);
        await e.Db.SaveChangesAsync();
        var fv = e.V.K.Tipo("FV");
        var caja = e.V.Caja;
        e.Db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = caja.Id, Role = CashRegisterDocumentRole.InvoiceOnRequest, DocumentTypeId = fv.Id });
        e.Db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = caja.Id, Role = CashRegisterDocumentRole.InvoiceContingency, DocumentTypeId = contingencia.Id });
        await e.Db.SaveChangesAsync();
        var venta = new Domain.Entities.Inventory.Documents.InventoryDocument { Class = DocumentClass.SalesInvoice, DocumentTypeId = fv.Id, OperationDate = VentaElectronicaDePrueba.Hoy };
        var cajaConRoles = e.Db.CashRegisters.Single(c => c.Id == caja.Id);

        (await TipoDeVentaEnContingencia.AplicarAsync(e.Db, venta, cajaConRoles, default)).Should().BeFalse("sin 03 abierta no cambia");
        e.Contingencia03();
        (await TipoDeVentaEnContingencia.AplicarAsync(e.Db, venta, cajaConRoles, default)).Should().BeTrue();
        venta.DocumentTypeId.Should().Be(contingencia.Id);

        venta.Number = 5;
        venta.DocumentTypeId = fv.Id;
        (await TipoDeVentaEnContingencia.AplicarAsync(e.Db, venta, cajaConRoles, default)).Should().BeFalse("un documento numerado nunca cambia");
    }

    // ------------------------------------------------------------------------------------------------ T737 --

    [Fact]
    public async Task Al_abrir_la_caja_avisa_todo_lo_que_falta_sin_bloquear()
    {
        var e = await VentaElectronicaDePrueba.CrearAsync();
        e.Db.ElectronicEmissionSettings.Remove(e.Configuracion);
        e.V.Parametro(Domain.Inventory.Parameters.ParametrosDeInventario.CajaUnaSesionPorCajero, "false");
        var fv = e.V.K.Tipo("FV");
        var caja = new CashRegister { PointOfSaleId = e.V.Punto.Id, Code = "CJ9", Name = "Caja 9", WarehouseId = e.V.K.Principal.Id, ReceiptWidthMm = 80 };
        e.Db.CashRegisters.Add(caja);
        await e.Db.SaveChangesAsync();
        e.Db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = caja.Id, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = fv.Id });
        await e.Db.SaveChangesAsync();

        var abrir = new OpenCashSessionCommandHandler(e.Db, e.V.K.Actor, e.V.Compras.C.Reloj, e.V.K.Alcance, e.V.K.Lector(), Substitute.For<ICerrojoPorClave>(),
            e.Guardia(), new SesionesDeCaja(e.Db, e.V.K.Actor, e.V.K.Alcance, e.V.K.Permisos), e.V.Confirmacion(), Substitute.For<IAuditoriaDelPuntoDeVenta>());
        var r = await abrir.Handle(new OpenCashSessionCommand(caja.PublicId, 100_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var codigos = r.Value.Warnings.Select(w => w.Code).ToList();
        codigos.Should().Contain(ErroresDeVentas.NotReadyCode);
        codigos.Should().Contain(GuardiaDeEmisionFiscal.DocumentTypeMissingCode, "falta el tipo de contingencia del rol de venta");
        System.Text.Json.JsonSerializer.Serialize(r.Value.Warnings.First(w => w.Code == ErroresDeVentas.NotReadyCode).Data)
            .Should().Contain(GuardiaDeEmisionFiscal.NoSettingsCode);
    }

    // ------------------------------------------------------------------------------------------------ T743 --

    private static EntregaDeDocumentos Entrega(VentaElectronicaDePrueba e, ISender? sender = null) =>
        new(e.Db, e.V.K.Alcance, new ConstructorDeTirilla(e.Db), Substitute.For<IEmailSender>(), [], sender, null, e.V.Compras.C.Reloj);

    [Fact]
    public async Task Un_documento_electronico_se_entrega_solo_despues_de_validarse()
    {
        var (e, venta) = await FacturadaAsync();
        var auditoria = Substitute.For<IAuditoriaDelPuntoDeVenta>();
        var entregar = new DeliverSalesDocumentCommandHandler(e.Db, Entrega(e), auditoria);

        var antes = await entregar.Handle(new DeliverSalesDocumentCommand(venta, CashRegisterPrintFormat.Ticket80), default);
        antes.IsFailure.Should().BeTrue();
        antes.Error.Code.Should().Be(ErroresDeDocumentosElectronicos.NotDeliverableCode);
        System.Text.Json.JsonSerializer.Serialize(((ErrorConDatos)antes.Error).Data).Should().Contain("\"status\":0", "el estado del documento electrónico: Pending");

        e.Estado(venta, ElectronicDocumentStatus.Validated, new string('d', 96));
        var despues = await entregar.Handle(new DeliverSalesDocumentCommand(venta, CashRegisterPrintFormat.Ticket80), default);
        despues.IsSuccess.Should().BeTrue(despues.IsFailure ? $"{despues.Error.Code}: {despues.Error.Message}" : null);
        despues.Value.Ticket!.Electronic!.UniqueCode.Should().Be(new string('d', 96));
        e.Electronico(venta).DeliveredAt.Should().NotBeNull();

        var copia = await new ReprintDocumentCommandHandler(e.Db, Entrega(e), auditoria)
            .Handle(new ReprintDocumentCommand(venta, CashRegisterPrintFormat.Ticket80), default);
        copia.Value.Copy.Should().BeTrue();
        copia.Value.Ticket!.Footer.Should().Contain(ConstructorDeTirilla.MarcaDeCopia);
    }

    [Fact]
    public async Task En_carta_devuelve_el_enlace_firmado_de_la_representacion_guardada()
    {
        var (e, venta) = await FacturadaAsync();
        e.Estado(venta, ElectronicDocumentStatus.Validated, new string('d', 96));
        var sender = Substitute.For<ISender>();
        var enlace = new EnlaceDeDescargaDto(true, "https://firmado/pdf", DateTimeOffset.UtcNow.AddSeconds(60));
        sender.Send(Arg.Any<GetElectronicArtifactLinkQuery>(), Arg.Any<CancellationToken>()).Returns(Result.Success(enlace));

        var r = await new DeliverSalesDocumentCommandHandler(e.Db, Entrega(e, sender), Substitute.For<IAuditoriaDelPuntoDeVenta>())
            .Handle(new DeliverSalesDocumentCommand(venta, CashRegisterPrintFormat.Letter), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Link.Should().Be(enlace);
        await sender.Received(1).Send(Arg.Is<GetElectronicArtifactLinkQuery>(q => q.Artifact == ArtefactosElectronicos.GraphicRepresentation), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Los_pendientes_de_entrega_del_POS_salen_del_estado_electronico()
    {
        var (e, venta) = await FacturadaAsync();
        e.V.Documento(venta).PointOfSaleId = e.V.Punto.Id;
        await e.Db.SaveChangesAsync();
        var listar = new ListSalesDocumentsQueryHandler(e.Db, e.V.K.Alcance);

        var pendientes = await listar.Handle(new ListSalesDocumentsQuery(PendingDelivery: true), default);
        pendientes.Value.Items.Select(i => i.DocumentPublicId).Should().Contain(venta);
        pendientes.Value.Items.Single(i => i.DocumentPublicId == venta).ElectronicStatus.Should().Be(nameof(ElectronicDocumentStatus.Pending));

        e.Estado(venta, ElectronicDocumentStatus.Validated, new string('d', 96));
        e.Electronico(venta).DeliveredAt = DateTime.UtcNow;
        await e.Db.SaveChangesAsync();
        (await listar.Handle(new ListSalesDocumentsQuery(PendingDelivery: true), default)).Value.Items.Should().NotContain(i => i.DocumentPublicId == venta);
        (await listar.Handle(new ListSalesDocumentsQuery(ElectronicStatus: "Validated"), default)).Value.Items.Should().Contain(i => i.DocumentPublicId == venta);
    }

    // ------------------------------------------------------------------------------------------ tras el commit --

    [Fact]
    public async Task Lo_anotado_corre_despues_de_la_peticion_mas_externa_y_se_descarta_si_falla()
    {
        var tareas = new TareasTrasElCommit();
        var corridas = 0;
        tareas.Agregar(_ => { corridas++; return Task.CompletedTask; });
        tareas.Completar<Result<int>>((r, _) => Task.FromResult(r.IsSuccess ? Result.Success(r.Value + 1) : r));

        tareas.Entrar();
        tareas.Entrar();
        tareas.Salir().Should().BeFalse("la anidada no ejecuta");
        tareas.Salir().Should().BeTrue();
        var respuesta = await tareas.EjecutarAsync(Result.Success(41), default);
        ((Result<int>)respuesta!).Value.Should().Be(42);
        corridas.Should().Be(1);
        tareas.HayPendientes.Should().BeFalse();

        tareas.Agregar(_ => throw new InvalidOperationException("una tarea que falla no tumba la respuesta"));
        (await tareas.EjecutarAsync("hecho", default)).Should().Be("hecho");

        tareas.Agregar(_ => { corridas++; return Task.CompletedTask; });
        tareas.Descartar();
        await tareas.EjecutarAsync(null, default);
        corridas.Should().Be(1, "lo de una petición fallida se descarta");
    }
    [Fact]
    public async Task El_behavior_corre_lo_anotado_al_volver_la_peticion_y_no_si_fallo()
    {
        var servicios = new ServiceCollection();
        servicios.AddScoped<TareasTrasElCommit>();
        using var proveedor = servicios.BuildServiceProvider();
        using var ambito = proveedor.CreateScope();
        var tareas = ambito.ServiceProvider.GetRequiredService<TareasTrasElCommit>();
        var behavior = new TrasElCommitBehavior<string, Result<int>>(ambito.ServiceProvider);

        var bien = await behavior.Handle("pedido", _ =>
        {
            tareas.Completar<Result<int>>((r, _) => Task.FromResult(Result.Success(r.Value * 2)));
            return Task.FromResult(Result.Success(21));
        }, default);
        bien.Value.Should().Be(42, "la respuesta se completa después de la petición");

        var corrio = false;
        var mal = await behavior.Handle("pedido", _ =>
        {
            tareas.Agregar(_ => { corrio = true; return Task.CompletedTask; });
            return Task.FromResult(Result.Failure<int>("X", "falló"));
        }, default);
        mal.IsFailure.Should().BeTrue();
        corrio.Should().BeFalse("lo anotado por una petición que falló se descarta");
        tareas.HayPendientes.Should().BeFalse();
    }
}
