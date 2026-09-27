using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// La cooperativa de prueba de ventas de oficina y notas (feature 012, I3, T556, T557): la de compras (<see cref="ComprasDePrueba"/>: PRIN
/// y B2, P1 arroz, P3 aceite gravado al 19 %, Cali), <b>no obligada</b> a facturar electrónicamente (<c>Dian.ObligadaAFacturar = false</c>),
/// con los tipos RV (comprobante no electrónico), NV (su nota), FV (factura electrónica) y NC (nota crédito), la lista general (P1 a
/// 2.000, P3 a 10.000), el consumidor final, la clienta Ana, la vendedora Luz, un punto PV1 con su caja CJ1 y una sesión abierta del
/// usuario, y cuatro medios: efectivo, tarjeta (con adquirente y su persona), bono de número único y transferencia. El ciclo común
/// es el real con las estrategias de venta, <see cref="BorradorDeVenta"/> y <see cref="ReglasDeConfirmacionDeVenta"/>, y la
/// entrega I3 (el despliegue sigue en I1). 100 unidades de P1 a 1.000 en PRIN. (nuevo)
/// </summary>
public sealed class VentasDePrueba
{
    public ComprasDePrueba Compras { get; }
    public KardexDePrueba K => Compras.K;
    public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => Compras.C.Db;
    public IToqueDeSesionDeCaja Toque { get; } = Substitute.For<IToqueDeSesionDeCaja>();
    public AlertasDePrueba Alertas { get; } = new();

    public Person ConsumidorFinal { get; private set; } = null!;
    public Person Ana { get; private set; } = null!;
    public Person Adquirente { get; private set; } = null!;
    public Salesperson Luz { get; private set; } = null!;
    public PointOfSale Punto { get; private set; } = null!;
    public CashRegister Caja { get; private set; } = null!;
    public CashSession Sesion { get; private set; } = null!;
    public PaymentMeans Efectivo { get; private set; } = null!;
    public PaymentMeans Tarjeta { get; private set; } = null!;
    public PaymentMeans Bono { get; private set; } = null!;
    public PaymentMeans Transferencia { get; private set; } = null!;
    public Guid P1 => Compras.P1;
    public Guid P3 => Compras.P3;

    private VentasDePrueba(ComprasDePrueba compras) => Compras = compras;

    public static async Task<VentasDePrueba> CrearAsync()
    {
        var v = new VentasDePrueba(await ComprasDePrueba.CrearAsync());
        var db = v.Db;
        v.Toque.TocarAsync(Arg.Any<int>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);

        foreach (var (codigo, clase, consecutivo) in new[]
                 {
                     ("RV", DocumentClass.NonElectronicSalesReceipt, true), ("NV", DocumentClass.NonElectronicSalesNote, true),
                     ("FV", DocumentClass.SalesInvoice, false), ("NC", DocumentClass.CreditNote, true),
                 })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, AllWarehouses = true };
            if (consecutivo) tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
        }
        db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.ObligadaAFacturar,
            ScopeKind = ParameterScopeKind.None, Value = "false", ValidFrom = new DateOnly(2026, 1, 1), Reason = "régimen simple",
        });

        var final = CatalogoDian.Embebido.ConsumidorFinal(Catalog.CatalogoDePrueba.Hoy)!;
        v.ConsumidorFinal = new Person { PersonType = "01", TaxId = final.Numero, FirstName = "Consumidor", LastName = "Final", IsCustomer = true };
        v.Ana = new Person { PersonType = "01", TaxId = "31999888", FirstName = "Ana", LastName = "Pérez", IsCustomer = true, Email = "ana@correo.co" };
        v.Adquirente = new Person { PersonType = "02", TaxId = "860034313", BusinessName = "Redeban S.A.", FirstName = "", LastName = "" };
        var luz = new Person { PersonType = "01", TaxId = "29555444", FirstName = "Luz", LastName = "Gómez" };
        db.People.AddRange(v.ConsumidorFinal, v.Ana, v.Adquirente, luz);
        await db.SaveChangesAsync();
        v.Luz = new Salesperson { PersonId = luz.Id };
        db.Salespeople.Add(v.Luz);

        var canal = new SalesChannel { Code = "MOSTRADOR", Name = "Mostrador" };
        db.SalesChannels.Add(canal);
        await db.SaveChangesAsync();
        v.Punto = new PointOfSale
        {
            Code = "PV1", Name = "Almacén Florida", BranchId = v.K.Sucursal.Id, SalesChannelId = canal.Id, PosEnabled = true, DefaultWarehouseId = v.K.Principal.Id,
        };
        db.PointsOfSale.Add(v.Punto);
        await db.SaveChangesAsync();
        v.Caja = new CashRegister { PointOfSaleId = v.Punto.Id, Code = "CJ1", Name = "Caja 1", WarehouseId = v.K.Principal.Id, ReceiptWidthMm = 80 };
        db.CashRegisters.Add(v.Caja);
        await db.SaveChangesAsync();
        v.Sesion = new CashSession
        {
            CashRegisterId = v.Caja.Id, PointOfSaleId = v.Punto.Id, CashierUserId = KardexDePrueba.Usuario, CashierName = "Cajera Uno",
            OperatingDate = Catalog.CatalogoDePrueba.Hoy, OpenedAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc),
            LastActivityAt = new DateTime(2026, 9, 25, 13, 0, 0, DateTimeKind.Utc),
        };
        db.CashSessions.Add(v.Sesion);

        var red = new CardNetwork { Code = "VISA", Name = "Visa" };
        var adquirente = new CardAcquirer { Code = "REDEBAN", Name = "Redeban", PersonId = v.Adquirente.Id };
        db.CardNetworks.Add(red);
        db.CardAcquirers.Add(adquirente);
        await db.SaveChangesAsync();
        v.Efectivo = Medio("EFECTIVO", PaymentMeansClass.Cash, "10", m => { m.AllowsChange = true; m.CountMethod = CashCountMethod.PhysicalCount; });
        v.Tarjeta = Medio("VISA", PaymentMeansClass.CreditCard, "48", m =>
        {
            m.CardNetworkId = red.Id;
            m.CardAcquirerId = adquirente.Id;
            m.CountMethod = CashCountMethod.VoucherTotal;
        });
        v.Bono = Medio("BONO", PaymentMeansClass.Voucher, "71", m =>
        {
            m.RequiresReference = true;
            m.ReferenceKind = PaymentReferenceKind.VoucherNumber;
            m.UniqueReference = true;
            m.CountMethod = CashCountMethod.ByReference;
        });
        v.Transferencia = Medio("TRANSF", PaymentMeansClass.Transfer, "47", m => m.CountMethod = CashCountMethod.None);
        db.PaymentMeans.AddRange(v.Efectivo, v.Tarjeta, v.Bono, v.Transferencia);
        await db.SaveChangesAsync();

        var cerrojo = Substitute.For<ICerrojoPorClave>();
        var general = (await new CreatePriceListCommandHandler(db, cerrojo).Handle(
            new CreatePriceListCommand("GENERAL", "General", false, null, new DateOnly(2026, 1, 1), null, "Lista"), default)).Value;
        var und = v.Compras.C.Unidad("UND").PublicId;
        await new SetPriceListItemsCommandHandler(db).Handle(new SetPriceListItemsCommand(general,
            [new PriceListItemInput(v.P1, und, 2000m), new PriceListItemInput(v.P3, und, 10000m)], "Precios"), default);

        await v.K.EntradaAsync(v.P1, 100m, 1000m);
        await v.K.EntradaAsync(v.P3, 50m, 6000m);
        return v;
    }

    private static PaymentMeans Medio(string codigo, PaymentMeansClass clase, string dian, Action<PaymentMeans> ajuste)
    {
        var m = new PaymentMeans
        {
            Code = codigo, Name = codigo, Class = clase, DianPaymentMeansCode = dian, OfferedAtAllPointsOfSale = true, OfferedInAllChannels = true,
            OfferedForAllDocumentTypes = true, ValidFrom = new DateOnly(2026, 1, 1),
        };
        ajuste(m);
        return m;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public LectorDeCatalogoTributario Catalogo() => new(Db, new LectorDeUvt(Db), K.Lector());

    public CalculoTributarioDeVenta Calculo() => new(Db, Catalogo(), K.Lector());

    public PrecificacionDeVenta Precificacion() => new(Db, Catalogo(), K.Lector());

    public AprobacionDeDescuentos Aprobaciones() => new(Db, K.Motor, K.Actor);

    public ReglasDeConfirmacionDeVenta Reglas() => new(Db, K.Actor, Compras.C.Reloj, K.Lector(), K.Permisos, new GuardiaDeEmisionFiscal(K.Lector()),
        Calculo(), Aprobaciones(), Toque);

    /// <summary>Las estrategias de ajustes y de ventas, con la entrega I3.</summary>
    public EfectosDeClase Efectos(ReglasDeConfirmacionDeVenta? reglas = null)
    {
        var registro = K.Registro();
        var reversion = new ReversionDeKardex(Db, registro);
        var emision = new EmisionDeInventario(Db);
        var maestros = K.Maestros();
        reglas ??= Reglas();
        var anulacion = new AnulacionDeVenta(registro, reversion, emision, Db, Compras.C.Reloj);
        return new EfectosDeClase(
        [
            new EfectoDeAjustePositivo(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoDeAjusteNegativo(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoDeConsumoInterno(registro, reversion, emision, maestros, K.Permisos, Db, new RetiroGravado(Db, Catalogo())),
            new EfectoComprobanteDeVenta(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoFacturaDeVenta(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoDocumentoEquivalentePos(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoNotaDeVentaNoElectronica(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoNotaCreditoDeVenta(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoNotaDeAjustePos(registro, emision, maestros, reglas, anulacion, Db),
        ], EntregaDelComercio.I3);
    }

    public BorradorDeVenta Borrador() => new(Db, K.Actor, Compras.C.Reloj, Precificacion(), Aprobaciones(), K.Permisos, K.Lector());

    public SaveInventoryDraftCommandHandler Guardar() =>
        new(Db, K.Maestros(), K.Alcance, K.Actor, Compras.C.Reloj, Efectos(), K.Vista(), [Borrador()]);

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, K.Maestros(), K.Actor, Compras.C.Reloj, Efectos(), K.Motor, K.Cerrojo, new Numerador(Db, K.Cerrojo),
        new EmisorDeMensajes(Db, K.Actor, Compras.C.Reloj), K.Lector(), K.Vista(), [], [],
        avisosAlConfirmar: [new AlertaDeVentaBajoCosto(Db, K.Lector(), Alertas)]);

    public ConfirmInventoryDocumentCommandHandler Confirmar() => new(Db, Confirmacion());

    public VoidInventoryDocumentCommandHandler Anular() => new(Db, K.Actor, Compras.C.Reloj, K.Vista(), Confirmacion());

    public SaveCreditNoteDraftCommandHandler Nota() =>
        new(Db, K.Actor, Compras.C.Reloj, K.Alcance, K.Maestros(), K.Permisos, Calculo(), K.Vista());

    // ------------------------------------------------------------------------------------------ escenarios --

    public Guid Tipo(string codigo) => K.Tipo(codigo).PublicId;

    public Guid Unidad(Guid producto) => K.Unidad(producto);

    public SalesLineInput Linea(Guid producto, decimal cantidad, decimal? precio = null) => new(producto, Unidad(producto), cantidad, precio);

    public DocumentPaymentInput Pago(PaymentMeans medio, decimal valor, string? referencia = null, decimal? entregado = null) =>
        new(medio.PublicId, valor, entregado, referencia, CashSessionPublicId: medio.CountMethod == CashCountMethod.None ? null : Sesion.PublicId);

    public SalesDraftInput Venta(string tipo = "RV", Person? cliente = null, Guid? vendedor = null, IReadOnlyList<DocumentPaymentInput>? pagos = null,
        params SalesLineInput[] lineas) =>
        new(Tipo(tipo), K.Principal.PublicId, lineas, pagos ?? [], CounterpartyPersonPublicId: cliente?.PublicId, SalespersonPublicId: vendedor);

    public async Task<Result<InventoryDocumentDto>> GuardarAsync(SalesDraftInput venta, Guid? id = null) =>
        await Guardar().Handle(new SaveInventoryDraftCommand(id, DocumentClassGroup.Sales, venta.ComoBorrador()), default);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento, decimal? esperado = null) =>
        await Confirmar().Handle(new ConfirmInventoryDocumentCommand(documento, DocumentClassGroup.Sales) { ExpectedAmountDue = esperado }, default);

    /// <summary>Guarda la venta con pagos que cuadran (efectivo por el total, salvo que se den) y la confirma; falla la prueba si no pasa.</summary>
    public async Task<InventoryDocument> VentaConfirmadaAsync(Func<decimal, IReadOnlyList<DocumentPaymentInput>>? pagos = null, Person? cliente = null,
        params SalesLineInput[] lineas)
    {
        var borrador = await GuardarAsync(Venta(cliente: cliente, lineas: lineas));
        if (borrador.IsFailure) throw new InvalidOperationException($"{borrador.Error.Code}: {borrador.Error.Message}");
        var total = Documento(borrador.Value.PublicId).AmountDue;
        var conPagos = await GuardarAsync(Venta(cliente: cliente, pagos: pagos?.Invoke(total) ?? [Pago(Efectivo, total)], lineas: lineas), borrador.Value.PublicId);
        if (conPagos.IsFailure) throw new InvalidOperationException($"{conPagos.Error.Code}: {conPagos.Error.Message}");
        var r = await ConfirmarAsync(borrador.Value.PublicId);
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return Documento(borrador.Value.PublicId);
    }

    public async Task<Result<InventoryDocumentDto>> NotaAsync(CreditNoteDraftInput nota, Guid? id = null) =>
        await Nota().Handle(new SaveCreditNoteDraftCommand(id, nota), default);

    public InventoryDocument Documento(Guid publicId) => Db.InventoryDocuments.Include(d => d.Lines).Single(d => d.PublicId == publicId);

    public void Parametro(string clave, string valor) => K.Parametro(clave, valor);

    public List<string> MensajesDe(Guid documento) => Compras.MensajesDe(documento);

    public string ContenidoDe(Guid documento, string tipo) => Compras.ContenidoDe(documento, tipo);
}
