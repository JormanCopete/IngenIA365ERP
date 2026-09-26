using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Catalog;

/// <summary>
/// La cooperativa de prueba del catálogo (feature 012, US1): InMemory con las semillas reales —unidades
/// (<see cref="InventoryUnitsSeeder"/>), tipos de bodega, causas de ajuste y catálogo tributario— más una categoría, un grupo
/// contable y una marca, y un reloj fijo. Arma productos con <see cref="CreateProductCommandHandler"/> para que las pruebas
/// pasen por las mismas reglas que la pantalla.
/// </summary>
public sealed class CatalogoDePrueba
{
    public static readonly DateOnly Hoy = new(2026, 9, 25);

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();

    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();

    public ProductCategory Abarrotes { get; private set; } = null!;

    public AccountingGroup GrupoAbarrotes { get; private set; } = null!;

    public Brand Diana { get; private set; } = null!;

    private CatalogoDePrueba()
    {
        Reloj.HoyLocal.Returns(Hoy);
        Reloj.UtcNow.Returns(new DateTime(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc));
    }

    public static async Task<CatalogoDePrueba> CrearAsync()
    {
        var c = new CatalogoDePrueba();
        await InventoryUnitsSeeder.AplicarAsync(c.Db, DateTime.UtcNow, null, default);
        await WarehouseTypesSeeder.AplicarAsync(c.Db, default);
        await AdjustmentCausesSeeder.AplicarAsync(c.Db, default);
        await TaxCatalogSeeder.AplicarAsync(c.Db, default);

        c.Abarrotes = new ProductCategory { Code = "ABARROTES", Name = "Abarrotes", Level = 1, IsActive = true };
        c.GrupoAbarrotes = new AccountingGroup { Code = "ABARR", Name = "Abarrotes", IsActive = true };
        c.Diana = new Brand { Code = "DIANA", Name = "Diana", IsActive = true };
        c.Db.AddRange(c.Abarrotes, c.GrupoAbarrotes, c.Diana);
        await c.Db.SaveChangesAsync();
        c.Abarrotes.Path = ProductCategory.RutaDe(null, c.Abarrotes.Id);
        await c.Db.SaveChangesAsync();
        return c;
    }

    public UnitOfMeasure Unidad(string codigo) => Db.UnitsOfMeasure.Single(u => u.Code == codigo);

    public TaxDefinition Impuesto(string codigo) => Db.TaxDefinitions.Single(t => t.Code == codigo);

    public TaxRate Tarifa(string codigo) => Db.TaxRates.Single(t => t.Code == codigo);

    public Guid Concepto(string codigo = "COMPRAS") => Db.WithholdingConcepts.Single(c => c.Code == codigo).PublicId;

    /// <summary>Un gravado al 19 % con la tarifa IVA19.</summary>
    public IReadOnlyList<ImpuestoPedido> Iva19() => [new ImpuestoPedido(Impuesto("IVA").PublicId, Tarifa("IVA19").PublicId, null)];

    public CreateProductCommand Alta(
        string codigo = "P1",
        string nombre = "Arroz Diana 500 g",
        ProductKind clase = ProductKind.Inventoriable,
        string unidadBase = "UND",
        bool conGrupo = true,
        bool conConcepto = true,
        VatSaleTreatment tratamiento = VatSaleTreatment.Excluded,
        IReadOnlyList<ImpuestoPedido>? impuestos = null,
        IReadOnlyList<UnidadPedida>? unidades = null,
        IReadOnlyList<CodigoPedido>? codigos = null,
        bool lote = false,
        string? referencia = null,
        Guid? marca = null) =>
        new(codigo, nombre, null, null, clase, Abarrotes.PublicId, marca, Unidad(unidadBase).PublicId,
            conGrupo ? GrupoAbarrotes.PublicId : null, tratamiento, conConcepto ? Concepto() : null, referencia, null, null,
            lote, false, false, unidades, codigos, impuestos);

    public Task<Result<ProductDto>> CrearProductoAsync(CreateProductCommand comando) =>
        new CreateProductCommandHandler(Db, Reloj).Handle(comando, default);

    /// <summary>Crea el producto o falla la prueba con el error.</summary>
    public async Task<ProductDto> ProductoAsync(CreateProductCommand comando)
    {
        var r = await CrearProductoAsync(comando);
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return r.Value;
    }

    public Product Producto(Guid publicId) => Db.Products.Single(p => p.PublicId == publicId);

    /// <summary>Deja una línea de documento del producto (un «movimiento»), en borrador o confirmada.</summary>
    public async Task MovimientoAsync(Guid productoPublicId, decimal cantidad = 1m, decimal? enBase = null, string? unidad = null, bool confirmado = true)
    {
        var producto = Producto(productoPublicId);
        var documento = new InventoryDocument { Class = DocumentClass.PositiveAdjustment, DocumentTypeId = 1, Prefix = string.Empty, OperationDate = Hoy, BranchId = 1 };
        if (confirmado) documento.Confirmar(1, DateTime.UtcNow);
        documento.Lines.Add(new InventoryDocumentLine
        {
            Document = documento,
            LineNumber = 1,
            ProductId = producto.Id,
            UnitId = unidad is null ? producto.BaseUnitId : Unidad(unidad).Id,
            Quantity = cantidad,
            QuantityBase = enBase ?? cantidad,
        });
        Db.InventoryDocuments.Add(documento);
        await Db.SaveChangesAsync();
    }

    public static string Codigo(Error error) => error.Code;

    public static object? Datos(Error error) => (error as ErrorConDatos)?.Data;

    public void Olvidar() => Db.ChangeTracker.Clear();

    public Task<int> ContarAsync<T>() where T : class => Db.Set<T>().CountAsync();
}
