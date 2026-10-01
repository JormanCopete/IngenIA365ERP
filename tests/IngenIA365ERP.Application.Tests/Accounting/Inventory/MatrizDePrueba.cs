using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;
using CoreTaxKind = IngenIA365ERP.Domain.Enums.Core.TaxKind;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Escenario de la matriz contable de Inventario (feature 012, I2): el de <see cref="ContabilidadTestData"/> más cuentas
/// habilitadas para INV, el catálogo tributario mínimo (IVA 19 %), las dimensiones de Inventario de un doble de
/// <see cref="IDimensionesDeInventario"/>, y ayudantes para sembrar reglas y mensajes ya contabilizados.
/// </summary>
public sealed class MatrizDePrueba
{
    public static readonly DateOnly Enero1 = new(2026, 1, 1);

    public ContabilidadTestData D { get; } = new();
    public IDimensionesDeInventario Dimensiones { get; } = Substitute.For<IDimensionesDeInventario>();
    public ChartOfAccount Inventario { get; }
    public ChartOfAccount InventarioBodega { get; }
    public ChartOfAccount Costo { get; }
    public ChartOfAccount IvaGenerado { get; }
    public ChartOfAccount Retencion { get; }
    public ChartOfAccount Caja { get; }
    public ChartOfAccount SinInventario { get; }
    public TaxRate Iva19 { get; }

    public MatrizDePrueba()
    {
        const AccountingModules inv = AccountingModules.Accounting | AccountingModules.Inventory;
        Inventario = D.Cuenta("14350501", modulos: inv);
        InventarioBodega = D.Cuenta("14350502", modulos: inv);
        Costo = D.Cuenta("61350501", modulos: inv);
        IvaGenerado = D.Cuenta("24080501", AccountNature.Credit, inv, tercero: true, baseGravable: true, tarifa: 0.19m);
        IvaGenerado.TaxKind = TaxKind.Vat;
        Retencion = D.Cuenta("23654001", AccountNature.Credit, inv, tercero: true, baseGravable: true, tarifa: 0.025m);
        Caja = D.Cuenta("11050501", modulos: inv);
        SinInventario = D.Cuenta("51050501");
        D.Db.SaveChanges();

        var iva = new TaxDefinition { Code = "IVA", Name = "IVA", Kind = CoreTaxKind.Iva, CreatedBy = "test" };
        var rtf = new TaxDefinition { Code = "RTF", Name = "ReteFuente", Kind = CoreTaxKind.ReteFuente, IsWithholding = true, CreatedBy = "test" };
        D.Db.TaxDefinitions.AddRange(iva, rtf);
        Iva19 = new TaxRate { TaxDefinition = iva, Code = "IVA19", Name = "IVA 19", Rate = 0.19m, ValidFrom = new DateOnly(2017, 1, 1), CreatedBy = "test" };
        D.Db.TaxRates.AddRange(Iva19,
            new TaxRate { TaxDefinition = rtf, Code = "RF25", Name = "Compras 2,5", Rate = 0.025m, ValidFrom = Enero1, CreatedBy = "test" });
        D.Db.SaveChanges();

        Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDto(
            [new("ABARROTES", "Abarrotes"), new("ASEO", "Aseo")],
            [new("B01", "Bodega principal", D.Principal.PublicId, null, WarehouseBehavior.Operational),
             new("TR01", "Tránsito", D.Principal.PublicId, null, WarehouseBehavior.Transit)],
            [new("PV01", "Punto 1")],
            [new("VENC", "Vencimiento"), new("ROTURA", "Rotura")],
            [new("CO", "Compra")],
            [new("EFE", "Efectivo", Domain.Enums.Core.PaymentMeansClass.Cash)])));
        Dimensiones.CombinacionesEnUsoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CombinacionEnUsoDto>>([]));
    }

    public InventoryPostingRule Regla(string operacion, string rol, ChartOfAccount cuenta, DateOnly? desde = null,
        string? grupo = null, string? bodega = null, string? punto = null, string? medio = null, string? tarifa = null, decimal? valorTarifa = null,
        string? motivo = null, int? sucursal = null, int? centro = null, DateOnly? hasta = null)
    {
        var regla = new InventoryPostingRule(operacion, rol, cuenta.Id, desde ?? Enero1, grupo, bodega, punto, medio, tarifa, valorTarifa, motivo,
            sucursal, centro, hasta, "prueba") { CreatedBy = "test" };
        D.Db.InventoryPostingRules.Add(regla);
        D.Db.SaveChanges();
        return regla;
    }

    /// <summary>Un mensaje a Contabilidad ya contabilizado (con comprobante), con su contenido JSON.</summary>
    public IntegrationMessage Contabilizado(string tipo, DateOnly fecha, string payloadJson, string numero = "CO-1", Guid? sucursal = null)
    {
        var mensaje = new IntegrationMessage
        {
            Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "INV", OriginKind = MessageOriginKind.Document,
            OriginPublicId = Guid.NewGuid(), OriginDocumentClass = "PurchaseReceipt", OriginDocumentTypeCode = "CO", OriginNumber = numero,
            OriginEventKey = "Confirmation", ChainRootPublicId = Guid.NewGuid(), OperationDate = fecha,
            BranchPublicId = sucursal ?? D.Principal.PublicId, Currency = "COP", ExchangeRate = 1m, PayloadJson = payloadJson,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))),
            OriginUserCentralId = Guid.NewGuid(), OriginUserName = "Ana", EmittedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        D.Db.IntegrationMessages.Add(mensaje);
        D.Db.InventoryPostings.Add(new InventoryPosting
        {
            MessagePublicId = mensaje.PublicId, MessageType = tipo, MessageKind = IntegrationMessageKind.Business, SourceModule = "INV",
            SourcePublicId = mensaje.OriginPublicId, SourceDocumentClass = "PurchaseReceipt", SourceDocumentTypeCode = "CO", SourceDocumentNumber = numero,
            OperationDate = fecha, AccountingDocumentId = 1, OriginUserName = "Ana", ActorName = "Proceso de integración",
            ProcessedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        });
        D.Db.SaveChanges();
        return mensaje;
    }

    /// <summary>El contenido de una compra con un renglón por grupo y bodega.</summary>
    public static string Compra(string grupo = "ABARROTES", string bodega = "B01") =>
        $$"""{"operation":"Compra","lines":[{"accountingGroupCode":"{{grupo}}","warehouseCode":"{{bodega}}","warehouseBehavior":"Operational","movement":"Entry","quantityBase":1,"cost":100,"documentLines":[1]}]}""";
}
