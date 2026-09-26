using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Purchasing;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// La cooperativa de prueba de compras (feature 012, US9): la del kardex (<see cref="KardexDePrueba"/>: PRIN y B2 activas, los
/// ajustes) más los tipos de compras con su consecutivo (REC recepción, FCP factura, NCP nota, DVP devolución), la UVT de 2026, el
/// municipio de la sucursal (Cali, 76001), dos proveedores —A: persona jurídica declarante y responsable de IVA; B: otro— y los
/// productos P1 (arroz, se compra por docena: 12 unidades) y P3 (gravado al 19 %). El ciclo común es el real con las cuatro
/// estrategias de compras, <see cref="BorradorDeCompra"/> y <see cref="CalculoTributarioDeCompra"/>.
/// </summary>
public sealed class ComprasDePrueba
{
    public const decimal Uvt = 52_374m;

    public KardexDePrueba K { get; }
    public CatalogoDePrueba C => K.C;
    public Person ProveedorA { get; private set; } = null!;
    public Person ProveedorB { get; private set; } = null!;
    public Guid P1 { get; private set; }
    public Guid P3 { get; private set; }
    public ContextoDeCompraDirecta CompraDirecta { get; } = new();
    public AlertasDePrueba Alertas { get; } = new();

    private ComprasDePrueba(KardexDePrueba k) => K = k;

    public static async Task<ComprasDePrueba> CrearAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        var c = new ComprasDePrueba(k);
        var db = k.C.Db;

        foreach (var (codigo, clase) in new[]
                 {
                     ("REC", DocumentClass.PurchaseReceipt), ("FCP", DocumentClass.SupplierInvoice),
                     ("NCP", DocumentClass.SupplierNote), ("DVP", DocumentClass.SupplierReturn),
                 })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
        }

        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Uvt, Name = "UVT", Kind = LegalParameterKind.Amount, Value = Uvt, ValidFrom = new DateTime(2026, 1, 1),
        });

        var pais = new Country { Name = "Colombia" };
        var departamento = new Department { Country = pais, Code = "76", Name = "Valle del Cauca" };
        db.Cities.Add(new City { Department = departamento, Name = "Cali", DaneCode = "76001" });
        db.Cities.Add(new City { Department = departamento, Name = "Palmira", DaneCode = "76520" });
        k.Sucursal.MunicipalityDaneCode = "76001";

        c.ProveedorA = new Person
        {
            PersonType = "02", TaxId = "900123456", BusinessName = "Distribuidora del Valle S.A.S.", FirstName = "", LastName = "",
            IsVatResponsible = true, IsIncomeTaxFiler = true,
        };
        c.ProveedorB = new Person
        {
            PersonType = "02", TaxId = "800999111", BusinessName = "Abarrotes La 14", FirstName = "", LastName = "",
            IsVatResponsible = true, IsIncomeTaxFiler = true,
        };
        db.People.AddRange(c.ProveedorA, c.ProveedorB);
        await db.SaveChangesAsync();

        c.P1 = k.P1;
        await k.C.Db.ProductUnits.AddAsync(new Domain.Entities.Inventory.Catalog.ProductUnit
        {
            ProductId = k.ProductoId(k.P1), UnitId = k.C.Unidad("DOC").Id, Factor = 12m, UsedForPurchase = true, UsedForSale = true,
        });
        await db.SaveChangesAsync();
        c.P3 = (await k.C.ProductoAsync(k.C.Alta("P3", "Aceite 1 L", tratamiento: Domain.Enums.Core.VatSaleTreatment.Taxed, impuestos: k.C.Iva19()))).PublicId;
        return c;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public CalculoTributarioDeCompra Calculo() =>
        new(C.Db, new LectorDeCatalogoTributario(C.Db, new LectorDeUvt(C.Db), K.Lector()));

    public VinculosDeCompra Vinculos() => new(C.Db, C.Reloj);

    public EfectosDeClase Efectos()
    {
        var registro = K.Registro();
        var reversion = new ReversionDeKardex(C.Db, registro);
        var emision = new EmisionDeInventario(C.Db);
        var maestros = K.Maestros();
        var vinculos = Vinculos();
        var diferencias = new DiferenciasDePrecioDeCompra(C.Db, vinculos);
        return new EfectosDeClase(
        [
            new EfectoDeAjustePositivo(registro, reversion, emision, maestros, K.Permisos, C.Db),
            new EfectoDeAjusteNegativo(registro, reversion, emision, maestros, K.Permisos, C.Db),
            new EfectoRecepcionDeCompra(registro, reversion, emision, maestros, Calculo(), C.Db),
            new EfectoFacturaDeProveedor(registro, emision, maestros, Calculo(), vinculos, diferencias, C.Db),
            new EfectoNotaDeProveedor(registro, emision, maestros, Calculo(), vinculos, diferencias, C.Db),
            new EfectoDevolucionAProveedor(registro, reversion, emision, maestros, vinculos, C.Db),
        ]);
    }

    public BorradorDeCompra Borrador() => new(C.Db, C.Reloj, Calculo(), Vinculos(), CompraDirecta);

    public SaveInventoryDraftCommandHandler Guardar(EfectosDeClase? efectos = null) =>
        new(C.Db, K.Maestros(), K.Alcance, K.Actor, C.Reloj, efectos ?? Efectos(), K.Vista(), [Borrador()]);

    public ConfirmacionDeDocumento Confirmacion(EfectosDeClase? efectos = null) => new(
        C.Db, K.Maestros(), K.Actor, C.Reloj, efectos ?? Efectos(), K.Motor, K.Cerrojo, new Numerador(C.Db, K.Cerrojo),
        new EmisorDeMensajes(C.Db, K.Actor, C.Reloj), K.Lector(), K.Vista(), [], []);

    public VoidInventoryDocumentCommandHandler Anular() => new(C.Db, K.Actor, C.Reloj, K.Vista(), Confirmacion());

    public ConfirmDirectPurchaseCommandHandler CompraDirectaHandler() =>
        new(C.Db, Guardar(), Confirmacion(), CompraDirecta, K.Vista());

    // ------------------------------------------------------------------------------------------ escenarios --

    public Guid Tipo(string codigo) => K.Tipo(codigo).PublicId;

    public Guid Docena => C.Unidad("DOC").PublicId;

    public Guid Base(Guid producto) => K.Unidad(producto);

    /// <summary>Una línea de compra: cantidad en la unidad, precio por unidad y descuento opcional.</summary>
    public SaveInventoryDraftLine Linea(Guid producto, decimal cantidad, decimal precio, Guid? unidad = null, decimal? descuento = null) =>
        new(null, producto, unidad ?? Base(producto), cantidad, UnitPrice: precio, DiscountAmount: descuento);

    public SaveInventoryDraftRequest Recepcion(Person? proveedor = null, string? remision = "REM-77", string? municipio = null, DateOnly? fecha = null,
        params SaveInventoryDraftLine[] lineas) =>
        new(Tipo("REC"), fecha, K.Principal.PublicId, null, null, null, remision, null, null, null, null, null, null, lineas,
            OperationMunicipalityDaneCode: municipio, SupplierPersonPublicId: (proveedor ?? ProveedorA).PublicId);

    public static SupplierDocumentRequest Documento(string numero = "4521", string? prefijo = "FV", string? cufe = null, bool electronica = false,
        bool credito = false, DateOnly? emision = null, DateOnly? vence = null) =>
        new(prefijo, numero, cufe, emision ?? CatalogoDePrueba.Hoy, vence ?? (credito ? CatalogoDePrueba.Hoy.AddDays(30) : null),
            credito ? "Credit" : "Cash", electronica);

    public SaveInventoryDraftRequest Factura(SupplierDocumentRequest documento, Person? proveedor = null, params SaveInventoryDraftLine[] lineas) =>
        new(Tipo("FCP"), null, null, null, null, null, null, null, null, null, null, null, null, lineas,
            SupplierPersonPublicId: (proveedor ?? ProveedorA).PublicId, Supplier: documento);

    public async Task<Result<InventoryDocumentDto>> GuardarAsync(SaveInventoryDraftRequest borrador, Guid? id = null) =>
        await Guardar().Handle(new SaveInventoryDraftCommand(id, DocumentClassGroup.Purchases, borrador), default);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(documento, DocumentClassGroup.Purchases), default);

    /// <summary>Guarda y confirma; falla la prueba si algo no pasa.</summary>
    public async Task<InventoryDocumentDto> ConfirmadoAsync(SaveInventoryDraftRequest borrador)
    {
        var guardado = await GuardarAsync(borrador);
        if (guardado.IsFailure) throw new InvalidOperationException($"{guardado.Error.Code}: {guardado.Error.Message}");
        var r = await ConfirmarAsync(guardado.Value.PublicId);
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return guardado.Value;
    }

    /// <summary>La recepción de 3 docenas de P1 a 15.600 la docena (36 unidades a 1.300), confirmada.</summary>
    public Task<InventoryDocumentDto> RecepcionDeTresDocenasAsync(Person? proveedor = null) =>
        ConfirmadoAsync(Recepcion(proveedor, lineas: [Linea(P1, 3m, 15_600m, Docena)]));

    /// <summary>La factura contra todas las líneas de <paramref name="recepcion"/>, en su unidad y cantidad, al precio dado o al de la recepción.</summary>
    public async Task<SaveInventoryDraftRequest> FacturaContraAsync(InventoryDocumentDto recepcion, SupplierDocumentRequest documento,
        decimal? precio = null, decimal? cantidad = null, Person? proveedor = null)
    {
        var lineas = await LineasAsync(recepcion.PublicId);
        return Factura(documento, proveedor, lineas.Select(l =>
            new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, cantidad ?? l.Quantity, UnitPrice: precio ?? l.UnitPrice, ReceiptLinePublicId: l.PublicId)).ToArray());
    }

    /// <summary>Un servicio (flete) con el concepto de retención de servicios.</summary>
    public async Task<Guid> ServicioAsync(string codigo = "FLETE") =>
        (await C.ProductoAsync(C.Alta(codigo, "Flete", clase: ProductKind.Service))).PublicId;

    /// <summary>ReteICA de Cali: la fila general del municipio (actividad «*»), sin base mínima.</summary>
    public async Task ReteIcaDeCaliAsync(decimal tarifa)
    {
        var reteIca = new Domain.Entities.Core.Taxes.TaxDefinition
        {
            Code = "RETEICA", Name = "ReteICA", Kind = Domain.Enums.Core.TaxKind.ReteIca,
            CalculationForm = Domain.Enums.Core.TaxCalculationForm.PercentOfBase, IsWithholding = true, IsActive = true,
        };
        C.Db.TaxDefinitions.Add(reteIca);
        C.Db.TaxRates.Add(new Domain.Entities.Core.Taxes.TaxRate
        {
            TaxDefinition = reteIca, Code = "RICACALI", Name = "ReteICA Cali general", Rate = tarifa, MunicipalityDaneCode = "76001",
            ActivityCode = "*", AppliesTo = Domain.Enums.Core.TaxAppliesTo.Both, ValidFrom = new DateOnly(2026, 1, 1), LegalSource = "Acuerdo municipal",
        });
        await C.Db.SaveChangesAsync();
    }

    /// <summary>Las líneas del documento (seguidas por el contexto).</summary>
    public async Task<List<InventoryDocumentLine>> LineasAsync(Guid documento) =>
        await C.Db.InventoryDocumentLines.Where(l => l.Document!.PublicId == documento && !l.IsDeleted).OrderBy(l => l.LineNumber).ToListAsync();

    public InventoryDocument Documento(Guid publicId) => C.Db.InventoryDocuments.Single(d => d.PublicId == publicId);

    public void Parametro(string modulo, string clave, string valor)
    {
        C.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = modulo, Key = clave, ScopeKind = ParameterScopeKind.None, ScopeId = 0, Value = valor,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        C.Db.SaveChanges();
    }

    /// <summary>Los contenidos de los mensajes emitidos por el documento, por tipo.</summary>
    public List<string> MensajesDe(Guid documento) =>
        C.Db.IntegrationMessages.Where(m => m.OriginPublicId == documento).OrderBy(m => m.Id).Select(m => m.Type).ToList();

    public string ContenidoDe(Guid documento, string tipo) =>
        C.Db.IntegrationMessages.Where(m => m.OriginPublicId == documento && m.Type == tipo).OrderBy(m => m.Id).Select(m => m.PayloadJson).First();
}

/// <summary>Las alertas de prueba: anota lo que se levanta y lo que un proceso atiende. (nuevo)</summary>
public sealed class AlertasDePrueba : IAlertas
{
    public List<AlertaALevantar> Levantadas { get; } = [];
    public List<string> Atendidas { get; } = [];

    public Task<Result<AlertaLevantada>> LevantarAsync(AlertaALevantar alerta, CancellationToken ct)
    {
        var repetida = Levantadas.Any(a => a.DedupKey == alerta.DedupKey && !Atendidas.Contains(a.DedupKey!));
        Levantadas.Add(alerta);
        return Task.FromResult(Result.Success(new AlertaLevantada(Guid.NewGuid(), repetida ? DesenlaceDeAlerta.Repetida : DesenlaceDeAlerta.Levantada, 1, false)));
    }

    public Task<bool> AtenderPorProcesoAsync(string dedupKey, string nota, CancellationToken ct)
    {
        Atendidas.Add(dedupKey);
        return Task.FromResult(Levantadas.Any(a => a.DedupKey == dedupKey));
    }
}
