using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;
using O = IngenIA365ERP.Application.Accounting.Inventory.Reglas.OperacionesDeInventario;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T450 (T27; contracts/contabilidad.md §2.2–§2.4): la resolución de la matriz. Gana la de mayor peso
/// (bodega o punto 16, centro 8, sucursal 4, grupo 2); las exigidas por igualdad exacta y las opcionales con <c>*</c>;
/// vigencia por la fecha de las reglas (<c>effectiveDate</c> en el ajuste de costo, la del original en la anulación); un
/// rol con importe cero no pide regla; sin candidata, <c>Missing</c> con sus datos; tarifa distinta en la regla o en la
/// cuenta, <c>TaxRateMismatch</c>; los impuestos por unidad no se comparan. Y los catálogos fijos de §2.7.
/// </summary>
public class ResolutorDeReglasTests
{
    private static readonly DateOnly Marzo = new(2026, 3, 15);

    private static PeticionDeRegla Inventario(string grupo = "ABARROTES", string? bodega = "B01", Guid? sucursal = null, Guid? centro = null,
        DateOnly? fecha = null, decimal importe = 100m) =>
        new(O.Compra, R.Inventario, new ValoresBuscados(grupo, bodega, BranchPublicId: sucursal, CostCenterPublicId: centro), fecha ?? Marzo, importe, [1, 2]);

    private static async Task<MatrizVigente> CargarAsync(MatrizDePrueba m, DateOnly? desde = null, DateOnly? hasta = null) =>
        await new ResolutorDeReglas(m.D.Db).CargarAsync(desde ?? MatrizDePrueba.Enero1, hasta ?? new DateOnly(2026, 12, 31), default);

    // ------------------------------------------------------------------------------------------ catálogos fijos --

    [Fact]
    public void Los_catalogos_fijos_son_los_de_decisiones_transversales()
    {
        O.Todas.Select(o => o.Codigo).Should().Equal(
            "Venta", "CostoDeVenta", "Compra", "FacturaProveedor", "DevolucionAProveedor", "DevolucionDeCliente", "NotaCredito", "NotaDebito",
            "AjustePositivo", "AjusteNegativo", "ConsumoInterno", "RetiroGravado", "Baja", "Ensamble", "DespachoTraslado", "RecepcionTraslado",
            "AjusteDeCosto", "Reclasificacion", "MovimientoDeCaja", "DiferenciaDeArqueo");
        R.Todos.Select(r => r.Codigo).Should().Equal(
            "Inventario", "Transito", "Costo", "Ingreso", "Descuento", "Devolucion", "Impuesto", "Retencion", "MedioDePago",
            "MercanciaPorFacturar", "CuentaPorPagar", "Contrapartida", "CajaDestino", "Sobrante", "Faltante", "GastoDeArqueo", "Redondeo");
        O.Todas.SelectMany(o => o.Roles).Should().OnlyContain(r => R.Buscar(r) != null, "toda operación nombra roles del catálogo");
        O.Todas.Should().OnlyContain(o => o.RolesExigidos.All(o.TieneRol));
        O.Todas.Select(o => o.Mensaje).Should().OnlyContain(m => CatalogoDeMensajesV1.Todos.Any(t => t.Type == m));
        O.DelMensaje(AjusteInventarioAprobadoV1.Type).Select(o => o.Codigo).Should()
            .BeEquivalentTo(["AjustePositivo", "AjusteNegativo", "ConsumoInterno", "RetiroGravado", "Baja", "Ensamble"]);
    }

    [Fact]
    public void Las_dimensiones_por_rol_son_las_de_la_tabla_de_2_2()
    {
        var impuesto = R.Buscar(R.Impuesto)!;
        impuesto.Exigidas.Should().Equal(DimensionDeRegla.TaxRateCode, DimensionDeRegla.TaxRate);
        impuesto.Admite(DimensionDeRegla.WarehouseCode).Should().BeFalse();
        R.Buscar(R.MedioDePago)!.Exigidas.Should().Equal(DimensionDeRegla.PaymentMeansCode);
        R.Buscar(R.MedioDePago)!.Opcionales.Should().Contain(DimensionDeRegla.PointOfSaleCode);
        R.Buscar(R.Faltante)!.MotivosFijos.Should().Equal("ShortageToCashier");
        R.Buscar(R.Faltante)!.Tercero.Should().Be(TerceroNatural.Cajero);
        R.Buscar(R.CuentaPorPagar)!.Opcionales.Should().Equal(DimensionDeRegla.Branch);

        var contrapartida = R.Buscar(R.Contrapartida)!;
        R.DimensionesDe(O.Baja, contrapartida).Exigidas.Should().Equal(DimensionDeRegla.ReasonCode);
        R.DimensionesDe(O.AjustePositivo, contrapartida).Exigidas.Should().BeEmpty();
        R.MotivosAdmitidos(O.AjusteDeCosto, contrapartida).Should().Contain("PriceDifference").And.NotContain("RoundingResidue");
        R.MotivosAdmitidos(O.Baja, contrapartida).Should().BeNull("la causa la valida Inventario");

        R.RolDeLaBodega(R.Inventario, WarehouseBehavior.Transit).Should().Be(R.Transito);
        R.RolDeLaBodega(R.Costo, WarehouseBehavior.Transit).Should().Be(R.Costo);
    }

    [Fact]
    public void La_compatibilidad_de_la_clase_de_impuesto_es_la_de_2_5()
    {
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Vat, Domain.Enums.Core.TaxKind.Iva).Should().BeTrue();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Vat, Domain.Enums.Core.TaxKind.ReteIva).Should().BeTrue();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Withholding, Domain.Enums.Core.TaxKind.Iva).Should().BeFalse();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Withholding, Domain.Enums.Core.TaxKind.ReteFuente).Should().BeTrue();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Ica, Domain.Enums.Core.TaxKind.ReteIca).Should().BeTrue();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.Gmf, Domain.Enums.Core.TaxKind.Inc).Should().BeTrue();
        R.CuentaCompatibleConTarifa(Domain.Enums.Accounting.TaxKind.None, Domain.Enums.Core.TaxKind.Other).Should().BeFalse();
    }

    // --------------------------------------------------------------------------------------------- resolución --

    [Fact]
    public async Task Gana_la_de_mayor_peso_y_las_opcionales_coinciden_con_comodin()
    {
        var m = new MatrizDePrueba();
        var general = m.Regla(O.Compra, R.Inventario, m.Inventario, grupo: "ABARROTES");
        var porBodega = m.Regla(O.Compra, R.Inventario, m.InventarioBodega, grupo: "ABARROTES", bodega: "B01");
        m.Regla(O.Compra, R.Inventario, m.Costo, grupo: "ABARROTES", sucursal: m.D.Norte.Id, centro: m.D.Centro.Id);
        var matriz = await CargarAsync(m);

        var r = matriz.Resolver(Inventario());
        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value!.Regla.Id.Should().Be(porBodega.Id, "bodega 16 + grupo 2 le gana a grupo 2");
        r.Value.Cuenta.Code.Should().Be("14350502");
        r.Value.BranchId.Should().BeNull();

        matriz.Resolver(Inventario(bodega: "B09")).Value!.Regla.Id.Should().Be(general.Id, "otra bodega cae en el comodín");

        var conSucursalYCentro = matriz.Resolver(Inventario(bodega: "B09", sucursal: m.D.Norte.PublicId, centro: m.D.Centro.PublicId)).Value!;
        conSucursalYCentro.Cuenta.Code.Should().Be("61350501", "centro 8 + sucursal 4 + grupo 2 = 14 le gana a grupo 2");
        conSucursalYCentro.BranchId.Should().Be(m.D.Norte.Id, "la sucursal llega por PublicId y se traduce a Id");
        conSucursalYCentro.CostCenterId.Should().Be(m.D.Centro.Id);

        matriz.Resolver(Inventario(bodega: "B01", sucursal: m.D.Norte.PublicId, centro: m.D.Centro.PublicId)).Value!.Regla.Id
            .Should().Be(porBodega.Id, "bodega 16 + grupo 2 = 18 le gana a 14");
    }

    [Fact]
    public async Task Las_exigidas_se_comparan_por_igualdad_exacta()
    {
        var m = new MatrizDePrueba();
        m.Regla(O.Compra, R.Inventario, m.Inventario, grupo: "ASEO");

        var r = (await CargarAsync(m)).Resolver(Inventario(grupo: "ABARROTES"));

        r.IsFailure.Should().BeTrue("una regla de otro grupo nunca sirve");
        r.Error.Code.Should().Be("Accounting.InventoryRule.Missing");
    }

    [Fact]
    public async Task Sin_candidata_responde_Missing_con_operacion_rol_valores_fecha_y_lineas()
    {
        var m = new MatrizDePrueba();

        var r = (await CargarAsync(m)).Resolver(Inventario(sucursal: m.D.Principal.PublicId));

        r.Error.Code.Should().Be("Accounting.InventoryRule.Missing");
        var datos = ((ErrorConDatos)r.Error).Data;
        Propiedad(datos, "operation").Should().Be("Compra");
        Propiedad(datos, "role").Should().Be("Inventario");
        Propiedad(datos, "date").Should().Be(Marzo);
        ((IReadOnlyList<int>)Propiedad(datos, "lineNumbers")!).Should().Equal(1, 2);
        var valores = Propiedad(datos, "values")!;
        Propiedad(valores, "accountingGroupCode").Should().Be("ABARROTES");
        Propiedad(valores, "warehouseCode").Should().Be("B01");
        Propiedad(valores, "branch").Should().Be(m.D.Principal.PublicId);
    }

    [Fact]
    public async Task Un_rol_con_importe_cero_no_pide_regla()
    {
        var m = new MatrizDePrueba();
        var matriz = await CargarAsync(m);

        var r = matriz.Resolver(new PeticionDeRegla(O.Venta, R.Descuento, new ValoresBuscados("ABARROTES"), Marzo, 0m, [1]));

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull("una venta sin descuento no pide regla de Descuento");
    }

    [Fact]
    public async Task La_vigencia_se_mira_en_la_fecha_de_las_reglas()
    {
        var m = new MatrizDePrueba();
        var vieja = m.Regla(O.Compra, R.Inventario, m.Inventario, grupo: "ABARROTES", hasta: new DateOnly(2026, 2, 28));
        var nueva = m.Regla(O.Compra, R.Inventario, m.InventarioBodega, desde: new DateOnly(2026, 3, 1), grupo: "ABARROTES");
        var matriz = await CargarAsync(m);

        matriz.Resolver(Inventario(fecha: new DateOnly(2026, 2, 10))).Value!.Regla.Id.Should().Be(vieja.Id);
        matriz.Resolver(Inventario(fecha: new DateOnly(2026, 3, 1))).Value!.Regla.Id.Should().Be(nueva.Id);
        (await CargarAsync(m, MatrizDePrueba.Enero1.AddYears(-1), MatrizDePrueba.Enero1.AddDays(-1)))
            .Resolver(Inventario(fecha: new DateOnly(2025, 12, 31))).Error.Code.Should().Be("Accounting.InventoryRule.Missing", "antes de toda vigencia no hay regla");
    }

    [Fact]
    public void La_fecha_de_las_reglas_es_la_efectiva_en_el_ajuste_de_costo_y_la_del_original_en_la_anulacion()
    {
        var sobre = new IntegrationEnvelopeV1 { Origin = new MessageOriginV1 { OperationDate = new DateOnly(2026, 4, 2) } };

        ResolutorDeReglas.FechaDeLasReglas(sobre, new CompraRecibidaV1()).Should().Be(new DateOnly(2026, 4, 2));
        ResolutorDeReglas.FechaDeLasReglas(sobre, new AjusteDeCostoReconocidoV1 { EffectiveDate = new DateOnly(2026, 3, 31) })
            .Should().Be(new DateOnly(2026, 3, 31));
        ResolutorDeReglas.FechaDeLasReglasDelAnulado(new VoidedContentV1 { OperationDate = new DateOnly(2026, 2, 14) })
            .Should().Be(new DateOnly(2026, 2, 14), "el espejo usa las reglas vigentes en la fecha del original (T29, G2)");
    }

    // ------------------------------------------------------------------------------------------------- tarifas --

    private static PeticionDeRegla Iva(decimal? tarifa) =>
        new(O.Venta, R.Impuesto, new ValoresBuscados(TaxRateCode: "IVA19"), Marzo, 19m, [3], tarifa);

    [Fact]
    public async Task Una_tarifa_distinta_en_la_regla_responde_TaxRateMismatch()
    {
        var m = new MatrizDePrueba();
        m.Regla(O.Venta, R.Impuesto, m.IvaGenerado, tarifa: "IVA19", valorTarifa: 0.16m);

        var r = (await CargarAsync(m)).Resolver(Iva(0.19m));

        r.Error.Code.Should().Be("Accounting.InventoryRule.TaxRateMismatch");
        var datos = ((ErrorConDatos)r.Error).Data;
        Propiedad(datos, "tax").Should().Be("IVA19");
        Propiedad(datos, "account").Should().Be("24080501");
        Propiedad(datos, "ruleOrAccountRate").Should().Be(0.16m);
        Propiedad(datos, "messageRate").Should().Be(0.19m);
    }

    [Fact]
    public async Task Una_tarifa_distinta_en_la_cuenta_responde_TaxRateMismatch()
    {
        var m = new MatrizDePrueba();
        m.Regla(O.Venta, R.Retencion, m.Retencion, tarifa: "IVA19", valorTarifa: 0.19m);

        var r = (await CargarAsync(m)).Resolver(Iva(0.19m) with { Role = R.Retencion });

        r.Error.Code.Should().Be("Accounting.InventoryRule.TaxRateMismatch", "la cuenta 23654001 tiene 2,5 % vigente (CuentaParaReglas.TarifaVigenteDe)");
        Propiedad(((ErrorConDatos)r.Error).Data, "ruleOrAccountRate").Should().Be(0.025m);
    }

    [Fact]
    public async Task Con_la_misma_tarifa_resuelve_y_los_impuestos_por_unidad_no_se_comparan()
    {
        var m = new MatrizDePrueba();
        m.Regla(O.Venta, R.Impuesto, m.IvaGenerado, tarifa: "IVA19", valorTarifa: 0.19m);
        var matriz = await CargarAsync(m);

        matriz.Resolver(Iva(0.19m)).IsSuccess.Should().BeTrue();
        var porUnidad = matriz.Resolver(Iva(null));
        porUnidad.IsSuccess.Should().BeTrue("un impuesto de valor por unidad no trae tarifa y no se compara");
        porUnidad.Value!.CuentaParaReglas.TarifaVigente.Should().Be(0.19m);
    }

    // ----------------------------------------------------------------------------------- retroactividad (§2.4) --

    [Fact]
    public async Task El_ultimo_contabilizado_de_la_operacion_es_el_de_fecha_mas_reciente()
    {
        var m = new MatrizDePrueba();
        m.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 2), MatrizDePrueba.Compra(), "CO-1");
        m.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra(), "CO-2");
        m.Contabilizado(AjusteInventarioAprobadoV1.Type, new DateOnly(2026, 3, 20), """{"operation":"Baja","lines":[]}""", "AJ-1");
        var resolutor = new ResolutorDeReglas(m.D.Db);

        var ultimo = await resolutor.UltimoContabilizadoAsync(O.Compra, default);
        ultimo!.OperationDate.Should().Be(new DateOnly(2026, 3, 9));
        ultimo.Documento.Should().Be("CO CO-2");

        (await resolutor.UltimoContabilizadoAsync(O.AjustePositivo, default)).Should().BeNull("el ajuste contabilizado es una baja");
        (await resolutor.UltimoContabilizadoAsync(O.Baja, default))!.OperationDate.Should().Be(new DateOnly(2026, 3, 20));
    }

    [Fact]
    public async Task Una_clave_nueva_coincide_solo_con_mensajes_que_traen_sus_valores()
    {
        var m = new MatrizDePrueba();
        m.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra("ABARROTES", "B01"), "CO-2");
        var resolutor = new ResolutorDeReglas(m.D.Db);

        var otraBodega = new Domain.Entities.Accounting.Inventory.InventoryPostingRule(O.Compra, R.Inventario, m.Inventario.Id, new DateOnly(2026, 3, 1), "ABARROTES", "B02");
        (await resolutor.ContabilizadoQueCoincideAsync(otraBodega, default)).Should().BeNull("ninguna compra contabilizada movió la bodega B02");

        var mismaBodega = new Domain.Entities.Accounting.Inventory.InventoryPostingRule(O.Compra, R.Inventario, m.Inventario.Id, new DateOnly(2026, 3, 1), "ABARROTES", "B01");
        (await resolutor.ContabilizadoQueCoincideAsync(mismaBodega, default))!.Documento.Should().Be("CO CO-2");

        var despues = new Domain.Entities.Accounting.Inventory.InventoryPostingRule(O.Compra, R.Inventario, m.Inventario.Id, new DateOnly(2026, 3, 10), "ABARROTES", "B01");
        (await resolutor.ContabilizadoQueCoincideAsync(despues, default)).Should().BeNull("sólo cuenta lo contabilizado desde su vigencia");

        var otraSucursal = new Domain.Entities.Accounting.Inventory.InventoryPostingRule(O.Compra, R.Inventario, m.Inventario.Id, new DateOnly(2026, 3, 1), "ABARROTES", branchId: m.D.Norte.Id);
        (await resolutor.ContabilizadoQueCoincideAsync(otraSucursal, default)).Should().BeNull("la compra fue de la sucursal principal");
    }

    private static object? Propiedad(object objeto, string nombre) => objeto.GetType().GetProperty(nombre)!.GetValue(objeto);
}
