using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Pos;

/// <summary>
/// Feature 012, I3, T554 (contracts/api.md §20.2; FR-057, FR-058, FR-059; T50, F13): la venta del POS mientras es borrador y su cobro.
/// Una lectura repetida suma cantidad y «3*» multiplica; el código de empaque trae su unidad y su factor; un código sin coincidencia es
/// <c>Inventory.Product.NotFound</c>; bajo costo avisa o bloquea según <c>Ventas.BajoCosto</c>; la factura a petición exige cliente;
/// cambiar el cliente vuelve a precificar; recuperar exige una venta suspendida y avisa <c>Inventory.Pos.Repriced</c> en otra fecha
/// operativa; un descuento sobre el tope pide su aprobación y cambiar la línea la deja sin efecto; cobrar con ella pendiente es
/// <c>Inventory.Discount.ApprovalPending</c>; sin sesión abierta, <c>Inventory.CashSession.NotOpen</c>; un punto sin POS responde
/// <c>Inventory.Pos.NotEnabled</c> al abrir, recuperar y leer; y cada acción de riesgo deja su evento con canal <c>pos</c>, las lecturas no.
/// </summary>
public class PosCommandsTests
{
    private const string BarraUnidad = "7701234000011";
    private const string BarraDocena = "7701234000128";
    private const int Supervisor = 20;

    private ComprasDePrueba _c = null!;
    private KardexDePrueba K => _c.K;
    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => _c.C.Db;
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly ILimitesPorPermiso _limites = Substitute.For<ILimitesPorPermiso>();
    private readonly IToqueDeSesionDeCaja _toque = Substitute.For<IToqueDeSesionDeCaja>();
    private PointOfSale _punto = null!;
    private CashRegister _caja = null!;
    private CashSession _sesion = null!;
    private Person _cliente = null!;
    private Salesperson _vendedor = null!;
    private int _consumidorFinal;

    private static async Task<PosCommandsTests> CrearAsync()
    {
        var t = new PosCommandsTests { _c = await ComprasDePrueba.CrearAsync() };
        t._alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        t._limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        t._toque.TocarAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        var db = t.Db;

        var canal = new SalesChannel { Code = "MOSTRADOR", Name = "Mostrador" };
        db.SalesChannels.Add(canal);
        var rv = new InventoryDocumentType { Code = "RV", Name = "Comprobante de venta", Class = DocumentClass.NonElectronicSalesReceipt, AllWarehouses = true, IsActive = true };
        rv.Sequences.Add(new DocumentSequence { DocumentType = rv, Prefix = "RV", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        var fv = new InventoryDocumentType { Code = "FV", Name = "Factura", Class = DocumentClass.SalesInvoice, AllWarehouses = true, IsActive = true };
        db.InventoryDocumentTypes.AddRange(rv, fv);
        await db.SaveChangesAsync();

        t._punto = new PointOfSale
        {
            Code = "PV1", Name = "Almacén Florida", BranchId = t.K.Sucursal.Id, SalesChannelId = canal.Id, PosEnabled = true, DefaultWarehouseId = t.K.Principal.Id,
        };
        db.PointsOfSale.Add(t._punto);
        await db.SaveChangesAsync();
        t._caja = new CashRegister { PointOfSaleId = t._punto.Id, Code = "CJ1", Name = "Caja 1", WarehouseId = t.K.Principal.Id, ReceiptWidthMm = 80 };
        t._caja.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = t._caja, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = rv.Id });
        t._caja.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = t._caja, Role = CashRegisterDocumentRole.InvoiceOnRequest, DocumentTypeId = fv.Id });
        db.CashRegisters.Add(t._caja);
        await db.SaveChangesAsync();
        t._sesion = t.Sesion(t._caja, CatalogoDePruebaHoy);
        db.CashSessions.Add(t._sesion);

        var final = CatalogoDian.Embebido.ConsumidorFinal(CatalogoDePruebaHoy)!;
        var consumidor = new Person { PersonType = "01", TaxId = final.Numero, FirstName = "Consumidor", LastName = "Final", IsCustomer = true };
        t._cliente = new Person { PersonType = "01", TaxId = "31999888", FirstName = "Ana", LastName = "Pérez", IsCustomer = true };
        var vendedora = new Person { PersonType = "01", TaxId = "29555444", FirstName = "Luz", LastName = "Gómez" };
        db.People.AddRange(consumidor, t._cliente, vendedora);
        await db.SaveChangesAsync();
        t._consumidorFinal = consumidor.Id;
        t._vendedor = new Salesperson { PersonId = vendedora.Id };
        db.Salespeople.Add(t._vendedor);

        var p1 = t.K.ProductoId(t._c.P1);
        var docena = db.ProductUnits.Single(u => u.ProductId == p1 && u.UnitId == t._c.C.Unidad("DOC").Id);
        db.ProductBarcodes.Add(new ProductBarcode { ProductId = p1, Barcode = BarraUnidad, IsPrimary = true });
        db.ProductBarcodes.Add(new ProductBarcode { ProductId = p1, Barcode = BarraDocena, ProductUnitId = docena.Id });
        await db.SaveChangesAsync();

        var cerrojo = Substitute.For<ICerrojoPorClave>();
        var general = (await new CreatePriceListCommandHandler(db, cerrojo).Handle(
            new CreatePriceListCommand("GENERAL", "General", false, null, new DateOnly(2026, 1, 1), null, "Lista"), default)).Value;
        var und = t._c.C.Unidad("UND").PublicId;
        await new SetPriceListItemsCommandHandler(db).Handle(new SetPriceListItemsCommand(general,
            [new PriceListItemInput(t._c.P1, und, 2000m), new PriceListItemInput(t._c.P1, t._c.Docena, 22000m), new PriceListItemInput(t._c.P3, und, 10000m)],
            "Precios"), default);
        var deAna = (await new CreatePriceListCommandHandler(db, cerrojo).Handle(
            new CreatePriceListCommand("ANA", "Precio de Ana", false, new PriceListScopeInput(PersonPublicId: t._cliente.PublicId), new DateOnly(2026, 1, 1), null, "Lista"),
            default)).Value;
        await new SetPriceListItemsCommandHandler(db).Handle(new SetPriceListItemsCommand(deAna, [new PriceListItemInput(t._c.P1, und, 1800m)], "Precios"), default);
        return t;
    }

    private static readonly DateOnly CatalogoDePruebaHoy = Catalog.CatalogoDePrueba.Hoy;

    private CashSession Sesion(CashRegister caja, DateOnly fecha, int cajero = KardexDePrueba.Usuario) => new()
    {
        CashRegisterId = caja.Id, PointOfSaleId = caja.PointOfSaleId, CashierUserId = cajero, CashierName = "Cajera Uno", OperatingDate = fecha,
        OpenedAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc), LastActivityAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc),
    };

    // ------------------------------------------------------------------------------------------------ servicios --

    private MotorDeAprobaciones Motor() => new(Db, K.Actor, K.Permisos, _alcance, _limites, Substitute.For<IAutoridadDeOtroAprobador>(),
        Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(Db, [new FuenteDeAprobacionDeDescuento(Db)]), _c.C.Reloj);

    private PrecificacionDeVenta Precificacion() => new(Db, new LectorDeCatalogoTributario(Db, new LectorDeUvt(Db), K.Lector()), K.Lector());

    private BorradorDelPos Pos() => new(Db, K.Actor, _c.C.Reloj, _alcance, K.Maestros(), Precificacion(),
        new AprobacionDeDescuentos(Db, Motor(), K.Actor), Motor(), K.Permisos);

    private readonly Guid _cooperativa = Guid.NewGuid();

    private AuditoriaDelPuntoDeVenta Auditoria()
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(_cooperativa.ToString());
        var servicios = new ServiceCollection().AddSingleton(tenant).AddSingleton(K.Actor).BuildServiceProvider();
        return new AuditoriaDelPuntoDeVenta(Db, servicios);
    }

    private async Task<PosDraftDto> AbrirAsync(string? primera = null)
    {
        var r = await new CreatePosDraftCommandHandler(Db, Pos()).Handle(
            new CreatePosDraftCommand(_sesion.PublicId, primera is null ? null : new PosFirstLineInput(primera)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<PosDraftDto>> LeerAsync(Guid venta, string codigo, decimal? cantidad = null) =>
        new AddPosLineCommandHandler(Db, Pos()).Handle(new AddPosLineCommand(venta, codigo, Quantity: cantidad), default);

    private Task<IngenIA365ERP.Application.Common.Models.Result<PosDraftDto>> LineaAsync(Guid venta, Guid linea, decimal? cantidad = null, decimal? precio = null, PosDiscountInput? descuento = null) =>
        new UpdatePosLineCommandHandler(Db, Pos(), Auditoria()).Handle(new UpdatePosLineCommand(venta, linea, cantidad, precio, descuento), default);

    private CheckoutPosDraftCommandHandler Cobro() => new(Db, Pos(), new RegistroDePagos(Db, K.Permisos, _c.C.Reloj),
        new AprobacionDeDescuentos(Db, Motor(), K.Actor), _toque, _c.Confirmacion(), new ConstructorDeTirilla(Db), Auditoria());

    private List<string> Eventos() => Db.AuditOutbox.Select(e => e.PayloadJson!).ToList()
        .Select(AuditoriaEncadenada.LeerCarga).Select(e => $"{e.Action}|{e.Metadata!["Channel"]}").ToList();

    // ----------------------------------------------------------------------------------------------- el lector --

    [Fact]
    public async Task Una_lectura_repetida_suma_cantidad_y_3_por_multiplica()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);

        await t.LeerAsync(venta.DraftPublicId, BarraUnidad);
        var r = await t.LeerAsync(venta.DraftPublicId, "3*" + BarraUnidad);

        r.Value.Lines.Should().ContainSingle();
        var linea = r.Value.Lines[0];
        (linea.Quantity, linea.UnitPrice, linea.Total).Should().Be((5m, 2000m, 10000m));
        r.Value.LastLine!.LineNumber.Should().Be(1);
        r.Value.Totals.AmountDue.Should().Be(10000m);
        r.Value.Customer.IsFinalConsumer.Should().BeTrue("el POS abre con el consumidor final");
    }

    [Fact]
    public async Task El_codigo_de_empaque_trae_su_unidad_y_su_factor()
    {
        var t = await CrearAsync();

        var leido = await new LookupPosProductQueryHandler(t.Db, t.Pos(), t.Precificacion()).Handle(new LookupPosProductQuery(BarraDocena, t._sesion.PublicId), default);
        var venta = await t.AbrirAsync(BarraDocena);

        (leido.Value.Unit.Code, leido.Value.Factor, leido.Value.Price).Should().Be(("DOC", 12m, 22000m));
        var linea = venta.Lines.Single();
        (linea.Unit.Code, linea.Factor, linea.Quantity, linea.QuantityBase).Should().Be(("DOC", 12m, 1m, 12m));
    }

    [Fact]
    public async Task Un_codigo_sin_coincidencia_exacta_es_Product_NotFound()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync();

        var r = await t.LeerAsync(venta.DraftPublicId, "77012");
        var consulta = await new LookupPosProductQueryHandler(t.Db, t.Pos(), t.Precificacion()).Handle(new LookupPosProductQuery("P", t._sesion.PublicId), default);

        r.Error.Code.Should().Be(ErroresDelPos.ProductNotFoundCode);
        consulta.Error.Code.Should().Be(ErroresDelPos.ProductNotFoundCode);
    }

    [Fact]
    public async Task Bajo_costo_avisa_con_Alertar_y_bloquea_con_Bloquear()
    {
        var t = await CrearAsync();
        await t.K.EntradaAsync(t._c.P1, 10m, 2500m);
        var venta = await t.AbrirAsync();

        var alerta = await t.LeerAsync(venta.DraftPublicId, BarraUnidad);
        t._c.Parametro(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasBajoCosto, "Bloquear");
        var bloqueo = await t.LeerAsync(venta.DraftPublicId, t._c.C.Producto(t._c.P1).Code);

        alerta.Value.Lines.Single().BelowCost.Should().BeTrue();
        alerta.Value.Warnings.Should().Contain(w => w.Code == ErroresDePrecios.BelowCostCode);
        bloqueo.Error.Code.Should().Be(ErroresDePrecios.BelowCostCode);
    }

    // ------------------------------------------------------------------------------------ cliente y documento --

    [Fact]
    public async Task La_factura_a_peticion_exige_cliente_y_cambiar_el_cliente_vuelve_a_precificar()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var editar = new UpdatePosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria());

        var sinCliente = await editar.Handle(new UpdatePosDraftCommand(venta.DraftPublicId, Role: CashRegisterDocumentRole.InvoiceOnRequest), default);
        var conCliente = await editar.Handle(new UpdatePosDraftCommand(venta.DraftPublicId, CustomerPersonPublicId: t._cliente.PublicId), default);
        var factura = await editar.Handle(new UpdatePosDraftCommand(venta.DraftPublicId, Role: CashRegisterDocumentRole.InvoiceOnRequest), default);

        sinCliente.Error.Code.Should().Be(ErroresDelPos.InvoiceRequiresCustomerCode);
        conCliente.Value.Lines.Single().UnitPrice.Should().Be(1800m, "Ana tiene su propia lista");
        conCliente.Value.Lines.Single().PriceList!.Code.Should().Be("ANA");
        factura.Value.Class.Should().Be(DocumentClass.SalesInvoice);
        factura.Value.DocumentType.Role.Should().Be(CashRegisterDocumentRole.InvoiceOnRequest);
    }

    [Fact]
    public async Task El_vendedor_debe_tener_rol_vivo()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var editar = new UpdatePosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria());

        var vivo = await editar.Handle(new UpdatePosDraftCommand(venta.DraftPublicId, SalespersonPublicId: t._vendedor.PublicId), default);
        t._vendedor.IsDeleted = true;
        await t.Db.SaveChangesAsync();
        var retirado = await editar.Handle(new UpdatePosDraftCommand(venta.DraftPublicId, SalespersonPublicId: t._vendedor.PublicId), default);

        vivo.Value.Salesperson!.Name.Should().Be("Luz Gómez");
        retirado.Error.Code.Should().Be(ErroresDelPos.SalespersonInvalidCode);
    }

    // ----------------------------------------------------------------------------------- suspender y recuperar --

    [Fact]
    public async Task Recuperar_exige_una_venta_suspendida_y_en_otra_fecha_operativa_avisa_Repriced()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var recuperar = new ResumePosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria());

        var noSuspendida = await recuperar.Handle(new ResumePosDraftCommand(venta.DraftPublicId, t._sesion.PublicId), default);
        await new SuspendPosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new SuspendPosDraftCommand(venta.DraftPublicId, "Señora del abrigo"), default);
        var suspendidas = await new ListPosDraftsQueryHandler(t.Db, t._alcance).Handle(new ListPosDraftsQuery(Suspended: true), default);

        // Al día siguiente, otra cajera en otra caja del mismo punto.
        t._sesion.Cerrar(KardexDePrueba.Usuario, DateTime.UtcNow);
        var caja2 = new CashRegister { PointOfSaleId = t._punto.Id, Code = "CJ2", Name = "Caja 2", WarehouseId = t.K.Principal.Id };
        caja2.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = caja2, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = t.K.C.Db.InventoryDocumentTypes.Single(x => x.Code == "RV").Id });
        t.Db.CashRegisters.Add(caja2);
        await t.Db.SaveChangesAsync();
        var manana = t.Sesion(caja2, CatalogoDePruebaHoy.AddDays(1));
        t.Db.CashSessions.Add(manana);
        await t.Db.SaveChangesAsync();
        var recuperada = await recuperar.Handle(new ResumePosDraftCommand(venta.DraftPublicId, manana.PublicId), default);

        noSuspendida.Error.Code.Should().Be(ErroresDelPos.NotSuspendedCode);
        suspendidas.Value.Single().Suspended!.Label.Should().Be("Señora del abrigo");
        recuperada.IsSuccess.Should().BeTrue(recuperada.IsFailure ? recuperada.Error.Message : string.Empty);
        recuperada.Value.Warnings.Should().Contain(w => w.Code == ErroresDelPos.RepricedCode);
        (recuperada.Value.CashRegister.Code, recuperada.Value.OperationDate, recuperada.Value.Suspended).Should().Be(("CJ2", CatalogoDePruebaHoy.AddDays(1), null));
    }

    // ---------------------------------------------------------------------------------------------- descuentos --

    [Fact]
    public async Task Un_descuento_sobre_el_tope_pide_aprobacion_y_cambiar_la_linea_la_deja_sin_efecto()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var linea = venta.Lines.Single().LinePublicId;

        var r = await t.LineaAsync(venta.DraftPublicId, linea, descuento: new PosDiscountInput(Percent: 0.10m));
        var solicitud = t.Db.ApprovalRequests.Single();
        var descuento = t.Db.DocumentLineDiscounts.Single(d => !d.IsDeleted);
        var otraLectura = await t.LeerAsync(venta.DraftPublicId, t._c.C.Producto(t._c.P3).Code);
        var conservada = t.Db.ApprovalRequests.Single().Status;
        await t.LineaAsync(venta.DraftPublicId, linea, cantidad: 2m);

        (solicitud.Subject, solicitud.SourceType, solicitud.SourcePublicId).Should().Be((ApprovalSubjects.DiscountOverCap, ApprovalSourceTypes.DocumentLineDiscount, descuento.PublicId));
        solicitud.ContentSha256.Should().NotBeNullOrEmpty();
        r.Value.PendingApprovals.Should().ContainSingle(p => p.LineNumber == 1);
        r.Value.Lines.Single().Discounts.Single().Should().Match<PosLineDiscountDto>(d => d.RequiresApproval && d.Amount == 200m && d.Approval!.Status == "Pending");
        otraLectura.IsSuccess.Should().BeTrue();
        conservada.Should().Be(ApprovalRequestStatus.Pending, "leer otro producto no toca la línea con descuento");
        t.Db.ApprovalRequests.Select(x => x.Status).Should().BeEquivalentTo([ApprovalRequestStatus.Cancelled, ApprovalRequestStatus.Pending],
            "cambiar la cantidad cambia la huella: la solicitud anterior queda sin efecto y se pide otra");
    }

    [Fact]
    public async Task Cobrar_con_un_descuento_sin_aprobar_es_ApprovalPending()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var r = await t.LineaAsync(venta.DraftPublicId, venta.Lines.Single().LinePublicId, descuento: new PosDiscountInput(Amount: 500m));

        var cobro = await t.Cobro().Handle(new CheckoutPosDraftCommand(venta.DraftPublicId, [], r.Value.Totals.AmountDue), default);

        cobro.Error.Code.Should().Be(ErroresDePrecios.ApprovalPendingCode);
    }

    // ------------------------------------------------------------------------------------------ sesión y punto --

    [Fact]
    public async Task Sin_sesion_abierta_del_usuario_es_CashSession_NotOpen()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var ajena = t.Sesion(t._caja, CatalogoDePruebaHoy, cajero: Supervisor);
        t.Db.CashSessions.Add(ajena);
        await t.Db.SaveChangesAsync();

        var deOtro = await new CreatePosDraftCommandHandler(t.Db, t.Pos()).Handle(new CreatePosDraftCommand(ajena.PublicId), default);
        t._sesion.Cerrar(KardexDePrueba.Usuario, DateTime.UtcNow);
        await t.Db.SaveChangesAsync();
        var cerrada = await t.LeerAsync(venta.DraftPublicId, BarraUnidad);
        var cobro = await t.Cobro().Handle(new CheckoutPosDraftCommand(venta.DraftPublicId, [], venta.Totals.AmountDue), default);

        deOtro.Error.Code.Should().Be(ErroresDelPos.CashSessionNotOpenCode);
        cerrada.Error.Code.Should().Be(ErroresDelPos.CashSessionNotOpenCode);
        cobro.Error.Code.Should().Be(ErroresDelPos.CashSessionNotOpenCode);
    }

    [Fact]
    public async Task Un_punto_sin_POS_no_abre_ni_recupera_ni_lee()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        await new SuspendPosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new SuspendPosDraftCommand(venta.DraftPublicId, "Espera"), default);
        t._punto.PosEnabled = false;
        await t.Db.SaveChangesAsync();

        var abrir = await new CreatePosDraftCommandHandler(t.Db, t.Pos()).Handle(new CreatePosDraftCommand(t._sesion.PublicId), default);
        var recuperar = await new ResumePosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new ResumePosDraftCommand(venta.DraftPublicId, t._sesion.PublicId), default);
        var leer = await new LookupPosProductQueryHandler(t.Db, t.Pos(), t.Precificacion()).Handle(new LookupPosProductQuery(BarraUnidad, t._sesion.PublicId), default);

        abrir.Error.Code.Should().Be(ErroresDelPos.NotEnabledCode);
        recuperar.Error.Code.Should().Be(ErroresDelPos.NotEnabledCode);
        leer.Error.Code.Should().Be(ErroresDelPos.NotEnabledCode);
    }

    [Fact]
    public async Task El_cobro_valida_los_pagos_contra_lo_que_la_venta_dice()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        var efectivo = new Domain.Entities.Core.Payments.PaymentMeans
        {
            Code = "EFE", Name = "Efectivo", Class = Domain.Enums.Core.PaymentMeansClass.Cash, AllowsChange = true, CountMethod = Domain.Enums.Core.CashCountMethod.PhysicalCount,
            DianPaymentMeansCode = "10", OfferedAtAllPointsOfSale = true, OfferedInAllChannels = true, OfferedForAllDocumentTypes = true,
            ValidFrom = new DateOnly(2026, 1, 1),
        };
        t.Db.PaymentMeans.Add(efectivo);
        await t.Db.SaveChangesAsync();

        var otroTotal = await t.Cobro().Handle(new CheckoutPosDraftCommand(venta.DraftPublicId, [new DocumentPaymentInput(efectivo.PublicId, 2000m)], 1999m), default);
        var noSuma = await t.Cobro().Handle(new CheckoutPosDraftCommand(venta.DraftPublicId, [new DocumentPaymentInput(efectivo.PublicId, 1500m)], 2000m), default);
        var tarjeta = await t.Cobro().Handle(new CheckoutPosDraftCommand(venta.DraftPublicId,
            [new DocumentPaymentInput(efectivo.PublicId, 2000m, Reference: "4111 1111 1111 1111")], 2000m), default);

        otroTotal.Error.Code.Should().Be(ErroresDelPos.TotalChangedCode);
        noSuma.Error.Code.Should().Be("Payments.TotalMismatch");
        tarjeta.Error.Code.Should().Be("Payments.CardNumberNotAllowed");
        t.Db.DocumentPayments.Should().BeEmpty("un cobro rechazado no deja pagos");
    }

    // --------------------------------------------------------------------------------------------- auditoría --

    [Fact]
    public async Task Cada_accion_de_riesgo_deja_su_evento_con_canal_pos_y_las_lecturas_no()
    {
        var t = await CrearAsync();
        var venta = await t.AbrirAsync(BarraUnidad);
        await t.LeerAsync(venta.DraftPublicId, t._c.C.Producto(t._c.P3).Code);
        var leidas = t.Eventos();
        var r = await t.LeerAsync(venta.DraftPublicId, BarraUnidad);
        var linea = r.Value.Lines.First().LinePublicId;

        await t.LineaAsync(venta.DraftPublicId, linea, precio: 1900m);
        await t.LineaAsync(venta.DraftPublicId, linea, descuento: new PosDiscountInput(Percent: 0.01m));
        await new RemovePosLineCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new RemovePosLineCommand(venta.DraftPublicId, r.Value.Lines.Last().LinePublicId), default);
        await new SuspendPosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new SuspendPosDraftCommand(venta.DraftPublicId, "Espera"), default);
        await new ResumePosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new ResumePosDraftCommand(venta.DraftPublicId, t._sesion.PublicId), default);
        await new DiscardPosDraftCommandHandler(t.Db, t.Pos(), t.Auditoria()).Handle(new DiscardPosDraftCommand(venta.DraftPublicId, "El cliente se arrepintió"), default);

        leidas.Should().BeEmpty("abrir la venta y leer productos no son acciones de riesgo");
        t.Eventos().Should().Equal(
            $"{AuditEventTypes.InventoryPosPriceOverridden}|pos",
            $"{AuditEventTypes.InventoryPosDiscountApplied}|pos",
            $"{AuditEventTypes.InventoryPosLineRemoved}|pos",
            $"{AuditEventTypes.InventoryPosSuspended}|pos",
            $"{AuditEventTypes.InventoryPosResumed}|pos",
            $"{AuditEventTypes.InventoryPosDiscarded}|pos");
        t.Db.InventoryDocuments.Single(d => d.PublicId == venta.DraftPublicId).Status.Should().Be(DocumentStatus.Discarded);
    }

    // ---------------------------------------------------------------------------------- entrega y reimpresión --

    [Fact]
    public async Task La_primera_entrega_sale_una_vez_y_la_reimpresion_lleva_COPIA_desde_la_copia_fiscal()
    {
        var t = await CrearAsync();
        var borrador = await t.AbrirAsync(BarraUnidad);
        var venta = t.Db.InventoryDocuments.Single(d => d.PublicId == borrador.DraftPublicId);
        venta.CounterpartyPersonId = t._cliente.Id;
        venta.Prefix = "RV";
        venta.Number = 41;
        venta.Confirmar(KardexDePrueba.Usuario, DateTime.UtcNow);
        t.Db.DocumentPartySnapshots.Add(new DocumentPartySnapshot { DocumentId = venta.Id, PersonId = t._cliente.Id, TaxId = "31999888", LegalName = "Ana Pérez (copia)", DianIdTypeCode = "13" });
        t.Db.DocumentPayments.Add(new DocumentPayment { DocumentId = venta.Id, LineNumber = 1, Amount = 2000m, MeansCode = "EFE", MeansName = "Efectivo", ChangeGiven = 500m });
        await t.Db.SaveChangesAsync();
        t._cliente.FirstName = "Otra";
        await t.Db.SaveChangesAsync();

        var tirilla = new ConstructorDeTirilla(t.Db, Microsoft.Extensions.Options.Options.Create(new TirillaOptions { AmbienteDePruebas = true }));
        var entrega = new EntregaDeDocumentos(t.Db, t._alcance, tirilla, Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Notifications.IEmailSender>(), []);
        var entregar = new DeliverSalesDocumentCommandHandler(t.Db, entrega, t.Auditoria());
        var primera = await entregar.Handle(new DeliverSalesDocumentCommand(venta.PublicId, CashRegisterPrintFormat.Ticket80), default);
        var segunda = await entregar.Handle(new DeliverSalesDocumentCommand(venta.PublicId, CashRegisterPrintFormat.Ticket80), default);
        var copia = await new ReprintDocumentCommandHandler(t.Db, entrega, t.Auditoria()).Handle(
            new ReprintDocumentCommand(venta.PublicId, CashRegisterPrintFormat.Ticket58, "Se dañó"), default);
        var carta = await entregar.Handle(new DeliverSalesDocumentCommand(venta.PublicId, CashRegisterPrintFormat.Letter), default);

        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : string.Empty);
        var ticket = primera.Value.Ticket!;
        (ticket.Copy, ticket.Party.Name, ticket.Document.Number, ticket.Change).Should().Be((false, "Ana Pérez (copia)", 41L, 500m));
        ticket.Document.ClassLabel.Should().Be("Comprobante de venta");
        ticket.Footer.Should().Equal(ConstructorDeTirilla.LeyendaDePruebas);
        segunda.Error.Code.Should().Be(ErroresDelPos.AlreadyDeliveredCode);
        (copia.Value.Copy, copia.Value.Ticket!.Format).Should().Be((true, CashRegisterPrintFormat.Ticket58));
        copia.Value.Ticket.Footer.Should().StartWith(ConstructorDeTirilla.MarcaDeCopia);
        carta.Error.Code.Should().Be(EntregaDeDocumentos.RepresentationUnavailableCode, "el PDF lo pone la API (SalesDocumentReport)");
        t.Eventos().Should().Equal($"{AuditEventTypes.InventoryDocumentDelivered}|web", $"{AuditEventTypes.InventoryDocumentReprinted}|web");
    }
}
