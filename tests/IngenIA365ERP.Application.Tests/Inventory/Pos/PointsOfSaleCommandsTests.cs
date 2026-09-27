using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Security.Scopes;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Inventory.Imports.PlantillaDePuntosDeVenta;

namespace IngenIA365ERP.Application.Tests.Inventory.Pos;

/// <summary>
/// Feature 012, I3, T552 (contracts/api.md §20.1, §22.3; FR-058, FR-067; T35): puntos de venta, cajas y dónde se ofrece cada
/// medio. Rol ↔ clase para los seis <see cref="CashRegisterDocumentRole"/> (<c>Inventory.CashRegister.RoleClassMismatch</c>),
/// estrechado por la obligación de facturar; la bodega de la caja es de la sucursal del punto
/// (<c>Inventory.CashRegister.WarehouseBranchMismatch</c>); un rol por caja; un punto con sesión abierta no se desactiva
/// (<c>Inventory.PointOfSale.HasOpenSessions</c>); un conjunto explícito junto con «todos» es
/// <c>Inventory.PaymentMeans.AvailabilityConflict</c>; el código del punto no cambia. Y el alcance por punto sobre
/// <c>INV_UserPointOfSaleScopes</c> (T596), la plantilla 10 (T595) y la visibilidad de la venta por su punto.
/// </summary>
public class PointsOfSaleCommandsTests
{
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();

    public PointsOfSaleCommandsTests() => _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);

    private sealed record Escenario(
        CatalogoDePrueba C, Branch Florida, Branch Palmira, SalesChannel Mostrador, Warehouse Bodega, Warehouse OtraSucursal,
        Dictionary<DocumentClass, InventoryDocumentType> Tipos);

    private static async Task<Escenario> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var florida = new Branch { Name = "Florida", LegacyCode = "01" };
        var palmira = new Branch { Name = "Palmira", LegacyCode = "02" };
        var mostrador = new SalesChannel { Code = "MOSTRADOR", Name = "Mostrador" };
        c.Db.AddRange(florida, palmira, mostrador);
        await c.Db.SaveChangesAsync();
        var tipo = c.Db.WarehouseTypes.First(t => t.Behavior == WarehouseBehavior.Operational);
        var bodega = new Warehouse { Code = "PV01", Name = "Bodega PV01", BranchId = florida.Id, WarehouseTypeId = tipo.Id, Behavior = WarehouseBehavior.Operational };
        var otra = new Warehouse { Code = "PAL01", Name = "Bodega Palmira", BranchId = palmira.Id, WarehouseTypeId = tipo.Id, Behavior = WarehouseBehavior.Operational };
        c.Db.AddRange(bodega, otra);
        var tipos = new Dictionary<DocumentClass, InventoryDocumentType>();
        foreach (var (clase, codigo) in new[]
                 {
                     (DocumentClass.PosEquivalentDocument, "POS"), (DocumentClass.SalesInvoice, "FV"), (DocumentClass.PosAdjustmentNote, "NAPOS"),
                     (DocumentClass.CreditNote, "NC"), (DocumentClass.NonElectronicSalesReceipt, "RV"), (DocumentClass.NonElectronicSalesNote, "NRV"),
                 })
        {
            tipos[clase] = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, AllWarehouses = true, IsActive = true };
            c.Db.InventoryDocumentTypes.Add(tipos[clase]);
        }
        await c.Db.SaveChangesAsync();
        return new Escenario(c, florida, palmira, mostrador, bodega, otra, tipos);
    }

    private Task<Result<PointOfSaleDto>> CrearPuntoAsync(Escenario e, string codigo = "PV01", Warehouse? bodega = null) =>
        new CreatePointOfSaleCommandHandler(e.C.Db).Handle(
            new CreatePointOfSaleCommand(codigo, "Almacén Florida", e.Florida.PublicId, e.Mostrador.PublicId, true, (bodega ?? e.Bodega).PublicId), default);

    private Task<Result<CashRegisterDto>> CrearCajaAsync(Escenario e, Guid punto, IReadOnlyList<CashRegisterDocumentTypeInput> tipos, Warehouse? bodega = null, string codigo = "CJ01") =>
        new CreateCashRegisterCommandHandler(e.C.Db, _alcance, new LectorDeParametros(e.C.Db), e.C.Reloj).Handle(
            new CreateCashRegisterCommand(punto, codigo, "Caja 1", (bodega ?? e.Bodega).PublicId, null, CashRegisterPrintFormat.Ticket80, tipos), default);

    private static CashRegisterDocumentTypeInput Rol(Escenario e, CashRegisterDocumentRole rol, DocumentClass clase) => new(rol, e.Tipos[clase].PublicId);

    private static ErrorConDatos ConDatos(Error error) => error.Should().BeOfType<ErrorConDatos>().Subject;

    // ------------------------------------------------------------------------------------------ roles y clases --

    [Theory]
    [InlineData(CashRegisterDocumentRole.PosSale, DocumentClass.SalesInvoice)]
    [InlineData(CashRegisterDocumentRole.InvoiceOnRequest, DocumentClass.PosEquivalentDocument)]
    [InlineData(CashRegisterDocumentRole.PosAdjustmentNote, DocumentClass.CreditNote)]
    [InlineData(CashRegisterDocumentRole.InvoiceCreditNote, DocumentClass.PosAdjustmentNote)]
    [InlineData(CashRegisterDocumentRole.PosSaleContingency, DocumentClass.SalesInvoice)]
    [InlineData(CashRegisterDocumentRole.InvoiceContingency, DocumentClass.PosEquivalentDocument)]
    public async Task Cada_rol_admite_solo_sus_clases(CashRegisterDocumentRole rol, DocumentClass claseAjena)
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;

        var r = await CrearCajaAsync(e, punto.PointOfSalePublicId, [Rol(e, rol, claseAjena)]);

        r.Error.Code.Should().Be(ErroresDePuntoDeVenta.RoleClassMismatchCode);
        ConDatos(r.Error).Data.GetType().GetProperty("role")!.GetValue(ConDatos(r.Error).Data).Should().Be(rol);
    }

    [Fact]
    public async Task Una_caja_que_vende_POS_y_factura_a_peticion_lleva_los_seis_roles()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;

        var r = await CrearCajaAsync(e, punto.PointOfSalePublicId,
        [
            Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument),
            Rol(e, CashRegisterDocumentRole.InvoiceOnRequest, DocumentClass.SalesInvoice),
            Rol(e, CashRegisterDocumentRole.PosAdjustmentNote, DocumentClass.PosAdjustmentNote),
            Rol(e, CashRegisterDocumentRole.InvoiceCreditNote, DocumentClass.CreditNote),
            Rol(e, CashRegisterDocumentRole.PosSaleContingency, DocumentClass.PosEquivalentDocument),
            Rol(e, CashRegisterDocumentRole.InvoiceContingency, DocumentClass.SalesInvoice),
        ]);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.DocumentTypes.Select(t => t.Role).Should().BeEquivalentTo(Enum.GetValues<CashRegisterDocumentRole>());
        r.Value.PrintFormat.Should().Be(CashRegisterPrintFormat.Ticket80);
    }

    [Fact]
    public async Task La_venta_POS_de_una_cooperativa_obligada_es_electronica_y_la_de_una_no_obligada_no()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;

        var obligada = await CrearCajaAsync(e, punto.PointOfSalePublicId, [Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.NonElectronicSalesReceipt)]);
        obligada.Error.Code.Should().Be(ErroresDePuntoDeVenta.RoleClassMismatchCode);

        e.C.Db.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.ObligadaAFacturar,
            ScopeKind = ParameterScopeKind.None, Value = "false", ValidFrom = new DateOnly(2026, 1, 1), Reason = "régimen simple",
        });
        await e.C.Db.SaveChangesAsync();
        var noObligada = await CrearCajaAsync(e, punto.PointOfSalePublicId,
        [
            Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.NonElectronicSalesReceipt),
            Rol(e, CashRegisterDocumentRole.PosAdjustmentNote, DocumentClass.NonElectronicSalesNote),
        ]);
        noObligada.IsSuccess.Should().BeTrue(noObligada.IsFailure ? noObligada.Error.Message : string.Empty);
    }

    [Fact]
    public async Task Un_rol_por_caja_y_la_factura_a_peticion_lleva_su_nota_credito()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;

        var repetido = await CrearCajaAsync(e, punto.PointOfSalePublicId,
            [Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument), Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument)]);
        var sinNota = await CrearCajaAsync(e, punto.PointOfSalePublicId, [Rol(e, CashRegisterDocumentRole.InvoiceOnRequest, DocumentClass.SalesInvoice)]);

        repetido.Error.Code.Should().Be(ErroresDePuntoDeVenta.RoleDuplicateCode);
        sinNota.Error.Code.Should().Be(ErroresDePuntoDeVenta.RoleRequiredCode);
    }

    [Fact]
    public async Task Cambiar_el_tipo_de_un_rol_y_quitar_otro_en_la_edicion()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;
        var otroPos = new InventoryDocumentType { Code = "POS2", Name = "POS 2", Class = DocumentClass.PosEquivalentDocument, AllWarehouses = true, IsActive = true };
        e.C.Db.Add(otroPos);
        await e.C.Db.SaveChangesAsync();
        var caja = (await CrearCajaAsync(e, punto.PointOfSalePublicId,
            [Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument), Rol(e, CashRegisterDocumentRole.PosAdjustmentNote, DocumentClass.PosAdjustmentNote)])).Value;

        var r = await new UpdateCashRegisterCommandHandler(e.C.Db, _alcance, new LectorDeParametros(e.C.Db), e.C.Reloj).Handle(
            new UpdateCashRegisterCommand(punto.PointOfSalePublicId, caja.CashRegisterPublicId, "Caja 1", e.Bodega.PublicId, null, CashRegisterPrintFormat.Letter,
                [new(CashRegisterDocumentRole.PosSale, otroPos.PublicId)]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.DocumentTypes.Should().ContainSingle(t => t.Role == CashRegisterDocumentRole.PosSale && t.DocumentTypeCode == "POS2");
        r.Value.PrintFormat.Should().Be(CashRegisterPrintFormat.Letter);
        e.C.Db.CashRegisterDocumentTypes.Count(t => t.Role == CashRegisterDocumentRole.PosAdjustmentNote && t.IsDeleted).Should().Be(1, "el rol que no vino se da de baja");
    }

    // ------------------------------------------------------------------------------------------ bodegas --

    [Fact]
    public async Task La_bodega_de_la_caja_es_de_la_sucursal_del_punto()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;

        var r = await CrearCajaAsync(e, punto.PointOfSalePublicId, [Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument)], e.OtraSucursal);

        r.Error.Code.Should().Be(ErroresDePuntoDeVenta.WarehouseBranchMismatchCode);
    }

    [Fact]
    public async Task La_bodega_por_defecto_del_punto_es_de_su_sucursal_y_el_codigo_no_se_repite()
    {
        var e = await EscenarioAsync();

        (await CrearPuntoAsync(e, "PV02", e.OtraSucursal)).Error.Code.Should().Be(ErroresDePuntoDeVenta.PointWarehouseBranchMismatchCode);
        (await CrearPuntoAsync(e)).IsSuccess.Should().BeTrue();
        (await CrearPuntoAsync(e, "pv01")).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    // ------------------------------------------------------------------------------------------ puntos --

    [Fact]
    public async Task Un_punto_con_sesion_abierta_no_se_desactiva_y_su_codigo_no_cambia()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;
        var caja = (await CrearCajaAsync(e, punto.PointOfSalePublicId, [Rol(e, CashRegisterDocumentRole.PosSale, DocumentClass.PosEquivalentDocument)])).Value;
        var entidad = e.C.Db.PointsOfSale.Single();
        e.C.Db.CashSessions.Add(new CashSession
        {
            CashRegisterId = e.C.Db.CashRegisters.Single().Id, PointOfSaleId = entidad.Id, CashierUserId = 1, CashierName = "Ana",
            OperatingDate = CatalogoDePrueba.Hoy, OpenedAt = DateTime.UtcNow,
        });
        await e.C.Db.SaveChangesAsync();
        var editar = new UpdatePointOfSaleCommandHandler(e.C.Db, _alcance);

        var desactivar = await editar.Handle(new UpdatePointOfSaleCommand(punto.PointOfSalePublicId, "Almacén", e.Mostrador.PublicId, true, e.Bodega.PublicId, false), default);
        var renombrar = await editar.Handle(new UpdatePointOfSaleCommand(punto.PointOfSalePublicId, "Almacén central", e.Mostrador.PublicId, false, e.Bodega.PublicId, true), default);

        desactivar.Error.Code.Should().Be(ErroresDePuntoDeVenta.HasOpenSessionsCode);
        renombrar.Value.Name.Should().Be("Almacén central");
        renombrar.Value.Code.Should().Be("PV01");
        renombrar.Value.PosEnabled.Should().BeFalse();
        renombrar.Value.CashRegisters!.Single().OpenSession!.CashierName.Should().Be("Ana");
        caja.Code.Should().Be("CJ01");
    }

    [Fact]
    public async Task Los_puntos_se_ven_por_el_alcance()
    {
        var e = await EscenarioAsync();
        var uno = (await CrearPuntoAsync(e)).Value;
        var dos = (await CrearPuntoAsync(e, "PV02")).Value;
        var idUno = e.C.Db.PointsOfSale.Single(p => p.Code == "PV01").Id;
        var alcance = Substitute.For<IAlcanceDeInventario>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(
            new AlcanceDeInventario(true, new HashSet<int>(), null, false, new HashSet<int> { idUno }, idUno));

        var lista = await new ListPointsOfSaleQueryHandler(e.C.Db, alcance).Handle(new ListPointsOfSaleQuery(), default);
        var ajeno = await new GetPointOfSaleQueryHandler(e.C.Db, alcance).Handle(new GetPointOfSaleQuery(dos.PointOfSalePublicId), default);

        lista.Value.Should().ContainSingle(p => p.PointOfSalePublicId == uno.PointOfSalePublicId);
        ajeno.Error.Code.Should().Be(ErroresDeAlcance.CodigoPuntoInexistente);
    }

    // ------------------------------------------------------------------------------------------ disponibilidad --

    [Fact]
    public async Task Un_conjunto_explicito_junto_con_todos_es_conflicto()
    {
        var e = await EscenarioAsync();
        var punto = (await CrearPuntoAsync(e)).Value;
        var todos = new PaymentMeans { Code = "EFECTIVO", Name = "Efectivo", Class = PaymentMeansClass.Cash, DianPaymentMeansCode = "10", OfferedAtAllPointsOfSale = true, OfferedInAllChannels = true, OfferedForAllDocumentTypes = true };
        var explicito = new PaymentMeans { Code = "BONO", Name = "Bono", Class = PaymentMeansClass.Voucher, DianPaymentMeansCode = "71", OfferedInAllChannels = true, OfferedForAllDocumentTypes = true };
        e.C.Db.AddRange(todos, explicito);
        await e.C.Db.SaveChangesAsync();
        var fijar = new SetPaymentMeansAvailabilityCommandHandler(e.C.Db, _alcance, e.C.Reloj);

        var conflicto = await fijar.Handle(new SetPaymentMeansAvailabilityCommand(todos.PublicId, [punto.PointOfSalePublicId], [], []), default);
        var ok = await fijar.Handle(new SetPaymentMeansAvailabilityCommand(explicito.PublicId, [punto.PointOfSalePublicId], [], []), default);
        var vaciar = await fijar.Handle(new SetPaymentMeansAvailabilityCommand(explicito.PublicId, [], [], []), default);

        conflicto.Error.Code.Should().Be(ErroresDePuntoDeVenta.AvailabilityConflictCode);
        ConDatos(conflicto.Error).Data.GetType().GetProperty("dimension")!.GetValue(ConDatos(conflicto.Error).Data).Should().Be("pointsOfSale");
        ok.Value.PointOfSalePublicIds.Should().Equal(punto.PointOfSalePublicId);
        vaciar.Value.PointOfSalePublicIds.Should().BeEmpty("un conjunto vacío no es «todos»: el medio no se ofrece en ningún punto");
        vaciar.Value.OfferedAtAllPoints.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ alcance por punto (T596) --

    [Fact]
    public async Task Las_asignaciones_de_punto_leen_solo_filas_vivas_y_una_por_defecto()
    {
        var e = await EscenarioAsync();
        await CrearPuntoAsync(e);
        await CrearPuntoAsync(e, "PV02");
        var uno = e.C.Db.PointsOfSale.Single(p => p.Code == "PV01").Id;
        var dos = e.C.Db.PointsOfSale.Single(p => p.Code == "PV02").Id;
        var puerto = new AsignacionesDePuntoDeVentaEnBase(e.C.Db, e.C.Reloj);

        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(uno, true), new AsignacionPedida(dos, true)], default)).Error.Code
            .Should().Be(ErroresDeAlcance.CodigoPorDefectoRepetido);
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(uno, true), new AsignacionPedida(dos, false)], default)).IsSuccess.Should().BeTrue();
        await e.C.Db.SaveChangesAsync();
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(dos, true)], default)).IsSuccess.Should().BeTrue();
        await e.C.Db.SaveChangesAsync();
        (await puerto.ReemplazarAsync(7, [new AsignacionPedida(999, false)], default)).Error.Code.Should().Be(ErroresDeAlcance.CodigoPuntoInexistente);

        var asignados = await puerto.PuntosDelUsuarioAsync(7, default);
        asignados.Ids.Should().BeEquivalentTo([dos]);
        asignados.PorDefecto.Should().Be(dos);
        e.C.Db.UserPointOfSaleScopes.Count(s => s.UserId == 7 && s.IsDeleted).Should().Be(1, "el retirado queda de baja lógica");
    }

    [Fact]
    public void La_venta_se_ve_por_su_punto_aunque_su_bodega_no_este_en_el_alcance()
    {
        var alcance = new AlcanceDeInventario(false, new HashSet<int> { 1 }, 1, false, new HashSet<int> { 5 }, 5);
        var venta = new InventoryDocument { WarehouseId = 9, PointOfSaleId = 5 };
        var ajena = new InventoryDocument { WarehouseId = 9, PointOfSaleId = 6 };

        FiltroDeAlcance.DocumentoVisible(alcance, venta, []).Should().BeTrue();
        FiltroDeAlcance.DocumentoVisible(alcance, ajena, []).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ plantilla 10 (T595) --

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private async Task<Result<ImportResultDto>> ImportarAsync(Escenario e, ModoDeImportacion modo)
    {
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? "?", out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.EsMaestroGlobal.Returns(true);
        var ejecutor = new EjecutorDeImportacion(e.C.Db, _lector, permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
        var r = await new ImportPointsOfSaleCommandHandler(e.C.Db, ejecutor, new LectorDeParametros(e.C.Db), e.C.Reloj)
            .Handle(new ImportPointsOfSaleCommand(modo, new ArchivoDeImportacion("puntos.xlsx", [1, 2, 3])), default);
        e.C.Olvidar();
        return r;
    }

    private static readonly string[] EncabezadosPuntos = [P.Codigo, P.Nombre, P.Sucursal, P.Canal, P.BodegaPorDefecto];
    private static readonly string[] EncabezadosCajas = [P.Codigo, P.Punto, P.Nombre, P.Bodega, P.TipoVentaPos, P.TipoFactura, P.TipoNotaCreditoFactura, P.Impresion];

    [Fact]
    public async Task La_plantilla_10_crea_puntos_y_cajas_con_sus_roles()
    {
        var e = await EscenarioAsync();
        Hoja(P.HojaPuntos, EncabezadosPuntos, ["PV01", "Almacén Florida", "01", "MOSTRADOR", "PV01"]);
        Hoja(P.HojaCajas, EncabezadosCajas,
            ["CJ01", "PV01", "Caja 1", "PV01", "POS", "FV", "NC", "Tirilla80"],
            ["CJ02", "PV01", "Caja 2", "PV01", "POS", null, null, "Carta"]);

        var r = await ImportarAsync(e, ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var punto = e.C.Db.PointsOfSale.Single();
        punto.PosEnabled.Should().BeTrue("vacío = sí");
        e.C.Db.CashRegisters.Single(c => c.Code == "CJ02").ReceiptWidthMm.Should().Be((short)CashRegisterPrintFormat.Letter);
        var cj01 = e.C.Db.CashRegisters.Single(c => c.Code == "CJ01").Id;
        e.C.Db.CashRegisterDocumentTypes.Where(t => t.CashRegisterId == cj01).Select(t => t.Role)
            .Should().BeEquivalentTo([CashRegisterDocumentRole.PosSale, CashRegisterDocumentRole.InvoiceOnRequest, CashRegisterDocumentRole.InvoiceCreditNote]);

        // Volver a subir el mismo archivo no cambia nada; cambiar la sucursal de un punto no se admite.
        var otraVez = await ImportarAsync(e, ModoDeImportacion.Review);
        otraVez.Value.Sheets.Should().OnlyContain(h => h.Unchanged == h.Rows);
        Hoja(P.HojaPuntos, EncabezadosPuntos, ["PV01", "Almacén Florida", "02", "MOSTRADOR", "PV01"]);
        (await ImportarAsync(e, ModoDeImportacion.Review)).Value.Errors.Should().Contain(x => x.Column == P.Sucursal);
    }

    [Fact]
    public async Task La_plantilla_10_aplica_las_reglas_del_alta_todo_o_nada()
    {
        var e = await EscenarioAsync();
        Hoja(P.HojaPuntos, EncabezadosPuntos, ["PV01", "Almacén Florida", "01", "MOSTRADOR", "PV01"]);
        Hoja(P.HojaCajas, EncabezadosCajas,
            ["CJ01", "PV01", "Caja 1", "PV01", "POS", null, null, null],
            ["CJ02", "PV01", "Caja 2", "PAL01", "FV", null, null, null]);

        var r = await ImportarAsync(e, ModoDeImportacion.Apply);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var cuerpo = (ImportResultDto)ConDatos(r.Error).Data;
        cuerpo.Errors.Should().Contain(x => x.Row == 3 && (x.Code == ErroresDePuntoDeVenta.WarehouseBranchMismatchCode || x.Code == ErroresDePuntoDeVenta.RoleClassMismatchCode));
        e.C.Db.PointsOfSale.Should().BeEmpty("todo o nada");
    }
}
