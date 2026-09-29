using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Application.Inventory.Sales.Reservas;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// La cooperativa de prueba del ciclo comercial de I6 (feature 012, T863–T868): la de ventas (<see cref="VentasDePrueba"/>: P1 arroz a 2.000
/// con 100 unidades a 1.000 en PRIN, P3 aceite gravado a 10.000 con 50 a 6.000, Ana, la caja abierta) con el crédito provisional
/// (<see cref="CreditoDePrueba"/>: CREDASOC, el asociado X, el motor de aprobaciones real) y obligada a facturar con el canal simulado
/// (<see cref="VentaElectronicaDePrueba"/>: FV con la resolución SETP). Suma los tipos de I6 —COT cotización, PED pedido, REM remisión, FVR
/// factura desde remisiones (prefijo fiscal SETP) y NDV nota débito, con su consecutivo— y arma el ciclo común con todas las estrategias de
/// venta y las de I6, con la entrega I6 (el despliegue sigue en I5). (nuevo)
/// </summary>
public sealed class CicloComercialDePrueba
{
    public VentasDePrueba V { get; }
    public CreditoDePrueba C { get; }
    public VentaElectronicaDePrueba E { get; }
    public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    public Person Ana => V.Ana;
    public Guid P1 => V.P1;
    public Guid P3 => V.P3;

    private CicloComercialDePrueba(VentasDePrueba v, CreditoDePrueba c, VentaElectronicaDePrueba e) => (V, C, E) = (v, c, e);

    public static async Task<CicloComercialDePrueba> CrearAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        var c = await CreditoDePrueba.SobreAsync(v);
        c.AsociadoX.Email = "xiomara@correo.co";
        var e = await VentaElectronicaDePrueba.SobreAsync(v);
        var ciclo = new CicloComercialDePrueba(v, c, e);
        foreach (var (codigo, clase, prefijoFiscal) in new[]
                 {
                     ("COT", DocumentClass.SalesQuote, (string?)null), ("PED", DocumentClass.SalesOrder, null), ("REM", DocumentClass.Shipment, null),
                     ("FVR", DocumentClass.SalesInvoiceFromShipments, "SETP"), ("NDV", DocumentClass.DebitNote, null),
                 })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, AllWarehouses = true, FiscalPrefix = prefijoFiscal };
            if (prefijoFiscal is null) tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            v.Db.InventoryDocumentTypes.Add(tipo);
        }
        await v.Db.SaveChangesAsync();
        c.ConfirmacionDelMotor = ciclo.Confirmacion;
        c.UsarMotorReal();
        return ciclo;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public ReservasDeInventario Reservas() => new(Db, V.K.Registro(), V.Compras.C.Reloj);

    public VinculosDelCiclo Vinculos() => new(Db, V.Compras.C.Reloj);

    public ReglasDeConfirmacionDeVenta Reglas() => new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Lector(), V.K.Permisos, E.Guardia(), V.Calculo(),
        V.Aprobaciones(), V.Toque, C.Credito(), new AprobacionDeCredito(Db, C.Motor, V.K.Actor, C.EnCurso));

    /// <summary>Las estrategias de venta de I3 y las de I6, con la entrega I6.</summary>
    public EfectosDeClase Efectos()
    {
        var registro = V.K.Registro();
        var reversion = new ReversionDeKardex(Db, registro);
        var emision = new EmisionDeInventario(Db, C.EnCurso);
        var maestros = V.K.Maestros();
        var reglas = Reglas();
        var anulacion = new AnulacionDeVenta(registro, reversion, emision, Db, V.Compras.C.Reloj);
        var reservas = new ReservasDeInventario(Db, registro, V.Compras.C.Reloj);
        var vinculos = Vinculos();
        return new EfectosDeClase(
        [
            new EfectoFacturaDeVenta(registro, emision, maestros, reglas, anulacion, Db, reservas, vinculos),
            new EfectoComprobanteDeVenta(registro, emision, maestros, reglas, anulacion, Db, reservas, vinculos),
            new EfectoNotaCreditoDeVenta(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoNotaDeVentaNoElectronica(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoDeCotizacion(maestros),
            new EfectoDePedido(maestros, reservas, V.K.Lector()),
            new EfectoDeRemision(registro, emision, maestros, anulacion, reservas, vinculos, Db),
            new EfectoDeFacturaDesdeRemisiones(registro, emision, maestros, reglas, anulacion, Db, vinculos),
            new EfectoDeNotaDebito(emision, maestros, reglas, Db),
        ], EntregaDelComercio.I6);
    }

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, V.K.Maestros(), V.K.Actor, V.Compras.C.Reloj, Efectos(), C.Motor, V.K.Cerrojo, new Numerador(Db, V.K.Cerrojo),
        new EmisorDeMensajes(Db, V.K.Actor, V.Compras.C.Reloj), V.K.Lector(), V.K.Vista(), [E.NuevoPasoFiscal()], []);

    public SaveInventoryDraftCommandHandler Guardar() =>
        new(Db, V.K.Maestros(), V.K.Alcance, V.K.Actor, V.Compras.C.Reloj, Efectos(), V.K.Vista(), [V.Borrador()]);

    public VoidInventoryDocumentCommandHandler Anular() => new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Vista(), Confirmacion());

    public SaveCreditNoteDraftCommandHandler Nota() =>
        new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Alcance, V.K.Maestros(), V.K.Permisos, V.Calculo(), V.K.Vista());

    // ------------------------------------------------------------------------------------------ escenarios --

    public SalesLineInput Linea(Guid producto, decimal cantidad, decimal? precio = null) => V.Linea(producto, cantidad, precio);

    public SalesDraftInput Documento(string tipo, Person? cliente = null, IReadOnlyList<Guid>? origenes = null, params SalesLineInput[] lineas) =>
        V.Venta(tipo, cliente ?? Ana, lineas: lineas) with { OriginPublicIds = origenes };

    public async Task<Result<InventoryDocumentDto>> GuardarAsync(SalesDraftInput entrada, IReadOnlyList<DocumentClass> ruta, Guid? id = null) =>
        await Guardar().Handle(new SaveInventoryDraftCommand(id, DocumentClassGroup.Sales, entrada.ComoBorrador(ruta)), default);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        await new ConfirmInventoryDocumentCommandHandler(Db, Confirmacion()).Handle(new ConfirmInventoryDocumentCommand(documento, DocumentClassGroup.Sales), default);

    public async Task<Result<VoidResultDto>> AnularAsync(Guid documento, string motivo = "Error de digitación") =>
        await Anular().Handle(new VoidInventoryDocumentCommand(documento, DocumentClassGroup.Sales, motivo), default);

    /// <summary>Guarda y confirma; falla la prueba si algo no pasa.</summary>
    public async Task<InventoryDocument> ConfirmadoAsync(SalesDraftInput entrada, IReadOnlyList<DocumentClass> ruta)
    {
        var borrador = Exito(await GuardarAsync(entrada, ruta));
        Exito(await ConfirmarAsync(borrador.PublicId));
        return Doc(borrador.PublicId);
    }

    public async Task<InventoryDocument> PedidoAsync(decimal cantidad, Guid? producto = null, IReadOnlyList<Guid>? cotizaciones = null) =>
        await ConfirmadoAsync(Documento("PED", origenes: cotizaciones, lineas: cotizaciones is null ? [Linea(producto ?? P1, cantidad)] : []), RutasDeVenta.Pedidos);

    public async Task<InventoryDocument> RemisionAsync(decimal cantidad, InventoryDocument? pedido = null, Guid? producto = null)
    {
        SalesLineInput[] lineas = pedido is null
            ? [Linea(producto ?? P1, cantidad)]
            : [Linea(producto ?? P1, cantidad) with { OriginLinePublicId = pedido.Lines.Single(l => !l.IsDeleted).PublicId }];
        return await ConfirmadoAsync(Documento("REM", origenes: pedido is null ? null : [pedido.PublicId], lineas: lineas), RutasDeVenta.Remisiones);
    }

    /// <summary>El borrador de una factura (FV o FVR) con sus orígenes, cobrado en efectivo por su total (dos guardados).</summary>
    public async Task<Guid> FacturaAsync(string tipo, IReadOnlyList<Guid> origenes, params SalesLineInput[] lineas)
    {
        var borrador = Exito(await GuardarAsync(Documento(tipo, origenes: origenes, lineas: lineas), RutasDeVenta.Facturas));
        var total = Doc(borrador.PublicId).AmountDue;
        Exito(await GuardarAsync(Documento(tipo, origenes: origenes, lineas: lineas) with { Payments = [V.Pago(V.Efectivo, total)] }, RutasDeVenta.Facturas,
            borrador.PublicId));
        return borrador.PublicId;
    }

    public InventoryDocument Doc(Guid publicId)
    {
        Db.ChangeTracker.Clear();
        return Db.InventoryDocuments.Include(d => d.Lines).Single(d => d.PublicId == publicId);
    }

    public decimal Reservado(Guid producto) => Db.StockBalances.AsNoTracking().Single(s => s.ProductId == V.K.ProductoId(producto) && s.WarehouseId == V.K.Principal.Id).Reserved;

    public decimal Fisico(Guid producto) => Db.StockBalances.AsNoTracking().Single(s => s.ProductId == V.K.ProductoId(producto) && s.WarehouseId == V.K.Principal.Id).Physical;

    public List<Domain.Entities.Inventory.Warehousing.Reservation> ReservasDe(InventoryDocument pedido) =>
        Db.Reservations.AsNoTracking().Where(r => r.DocumentId == pedido.Id).OrderBy(r => r.Id).ToList();

    public List<Domain.Entities.Inventory.Transactions.KardexEntry> KardexDe(InventoryDocument documento) =>
        Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == documento.Id).OrderBy(k => k.Id).ToList();

    public List<string> MensajesDe(Guid documento) => V.MensajesDe(documento);

    public static T Exito<T>(Result<T> r) =>
        r.IsSuccess ? r.Value : throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
}
