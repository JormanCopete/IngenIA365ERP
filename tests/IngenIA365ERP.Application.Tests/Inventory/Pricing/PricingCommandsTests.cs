using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Sales.Pricing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using L = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeListasDePrecios;
using T = IngenIA365ERP.Application.Inventory.Imports.PlantillaDeTopesDeDescuento;

namespace IngenIA365ERP.Application.Tests.Inventory.Pricing;

/// <summary>
/// Feature 012, I3, T553 (contracts/api.md §19.1–§19.3; FR-053, FR-054, T51): listas de precios y topes de descuento. El ámbito se
/// normaliza en <c>ScopeKey</c>; dos listas del mismo ámbito no se cruzan (<c>Inventory.PriceList.Overlaps</c>, nombrando la otra) y
/// el comando toma el candado del ámbito antes de mirar; un segmento que no es clase de asociado es
/// <c>Inventory.PriceList.SegmentUnknown</c> con <c>data.allowed</c>; el ámbito, el código e <c>includesTaxes</c> no cambian
/// (<c>ScopeLocked</c>). Un tope nuevo cierra el anterior del rol la víspera; uno que empieza el mismo día o antes es
/// <c>Inventory.DiscountCap.Overlaps</c>; el tope de un usuario es el mayor de sus roles y un rol sin fila da 0. <c>ResolvePriceQuery</c>
/// dice qué lista ganó, en qué dimensiones y las candidatas. Y las plantillas 12 y 13 (T600) con las mismas reglas.
/// </summary>
public class PricingCommandsTests
{
    private const int Cajero = 7;
    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly ICerrojoPorClave _cerrojo = Substitute.For<ICerrojoPorClave>();
    private readonly List<string> _candados = [];

    public PricingCommandsTests() =>
        _cerrojo.When(x => x.BloquearAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())).Do(x => _candados.Add(x.Arg<string>()));

    private sealed record Escenario(CatalogoDePrueba C, Branch Florida, SalesChannel Mostrador, Person Ana, Person Luis, Guid P1, Guid Plantilla);

    private static async Task<Escenario> EscenarioAsync()
    {
        var c = await CatalogoDePrueba.CrearAsync();
        var florida = new Branch { Name = "Florida", LegacyCode = "01" };
        var mostrador = new SalesChannel { Code = "MOSTRADOR", Name = "Mostrador" };
        var ana = new Person { TaxId = "16000111", FirstName = "Ana", LastName = "Gómez" };
        var luis = new Person { TaxId = "16000222", FirstName = "Luis", LastName = "Pérez" };
        c.Db.AddRange(florida, mostrador, ana, luis);
        await c.Db.SaveChangesAsync();
        c.Db.Associates.Add(new Associate { PersonId = ana.Id, AssociateClass = "ab" });
        c.Db.Associates.Add(new Associate { PersonId = luis.Id, AssociateClass = "AB" });
        await c.Db.SaveChangesAsync();
        var p1 = (await c.ProductoAsync(c.Alta("P1", unidades: [new UnidadPedida(c.Unidad("DOC").PublicId, 12m, ProductUnitUsage.Sale)]))).PublicId;
        // Una plantilla (I6) no se crea todavía por el alta: se marca a mano para probar que no recibe precio.
        var plantilla = (await c.ProductoAsync(c.Alta("CAMISA", "Camisa"))).PublicId;
        c.Producto(plantilla).Kind = ProductKind.Template;
        await c.Db.SaveChangesAsync();
        return new Escenario(c, florida, mostrador, ana, luis, p1, plantilla);
    }

    private Task<Result<Guid>> CrearListaAsync(Escenario e, string codigo, DateOnly desde, DateOnly? hasta = null, PriceListScopeInput? ambito = null, bool incluye = false) =>
        new CreatePriceListCommandHandler(e.C.Db, _cerrojo).Handle(
            new CreatePriceListCommand(codigo, $"Lista {codigo}", incluye, ambito, desde, hasta, "Lista nueva"), default);

    private static Task<Result<PriceListItemsResultDto>> PreciosAsync(Escenario e, Guid lista, params (Guid Producto, string Unidad, decimal Precio)[] precios) =>
        new SetPriceListItemsCommandHandler(e.C.Db).Handle(new SetPriceListItemsCommand(lista,
            precios.Select(p => new PriceListItemInput(p.Producto, e.C.Unidad(p.Unidad).PublicId, p.Precio)).ToList(), "Precios"), default);

    private static object? Dato(Error error, string propiedad)
    {
        var data = error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        return data.GetType().GetProperty(propiedad)!.GetValue(data);
    }

    // ------------------------------------------------------------------------------------------ listas (T597) --

    [Fact]
    public async Task El_ambito_se_normaliza_en_ScopeKey_y_el_alta_toma_el_candado_del_ambito()
    {
        var e = await EscenarioAsync();

        var r = await CrearListaAsync(e, "ASOC", Enero, ambito: new PriceListScopeInput(null, " ab ", e.Mostrador.PublicId, e.Florida.PublicId));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var lista = e.C.Db.PriceLists.Single();
        lista.ScopeKey.Should().Be($"P:-|S:AB|C:{e.Mostrador.Id}|B:{e.Florida.Id}");
        lista.DimensionCount.Should().Be(3);
        lista.Segment.Should().Be("AB");
        _candados.Should().Equal(ClavesDeCerrojo.AmbitoDeLista(lista.ScopeKey));
    }

    [Fact]
    public async Task Dos_listas_del_mismo_ambito_no_se_cruzan_y_el_error_nombra_la_otra()
    {
        var e = await EscenarioAsync();
        var general = (await CrearListaAsync(e, "GENERAL", Enero, new DateOnly(2026, 6, 30))).Value;

        var cruzada = await CrearListaAsync(e, "GEN2", new DateOnly(2026, 6, 1));
        var siguiente = await CrearListaAsync(e, "GEN3", new DateOnly(2026, 7, 1));
        var otroAmbito = await CrearListaAsync(e, "CANAL", new DateOnly(2026, 6, 1), ambito: new PriceListScopeInput(SalesChannelPublicId: e.Mostrador.PublicId));

        cruzada.Error.Code.Should().Be(ErroresDePrecios.OverlapsCode);
        Dato(cruzada.Error, "priceListPublicId").Should().Be(general);
        Dato(cruzada.Error, "code").Should().Be("GENERAL");
        siguiente.IsSuccess.Should().BeTrue("empieza el día después del cierre");
        otroAmbito.IsSuccess.Should().BeTrue("otro ámbito");
    }

    [Fact]
    public async Task Un_segmento_que_no_es_clase_de_asociado_se_rechaza_con_los_admitidos()
    {
        var e = await EscenarioAsync();

        var r = await CrearListaAsync(e, "VIP", Enero, ambito: new PriceListScopeInput(Segment: "VIP"));

        r.Error.Code.Should().Be(ErroresDePrecios.SegmentUnknownCode);
        Dato(r.Error, "allowed").Should().BeEquivalentTo(new[] { "AB" });
    }

    [Fact]
    public async Task El_codigo_de_la_lista_es_unico()
    {
        var e = await EscenarioAsync();
        await CrearListaAsync(e, "GENERAL", Enero);

        var r = await CrearListaAsync(e, "general", new DateOnly(2027, 1, 1), ambito: new PriceListScopeInput(SalesChannelPublicId: e.Mostrador.PublicId));

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Theory]
    [InlineData("code")]
    [InlineData("includesTaxes")]
    [InlineData("scope")]
    public async Task El_ambito_el_codigo_e_includesTaxes_no_cambian(string campo)
    {
        var e = await EscenarioAsync();
        var lista = (await CrearListaAsync(e, "GENERAL", Enero)).Value;
        e.C.Olvidar();

        var r = await new UpdatePriceListCommandHandler(e.C.Db, _cerrojo).Handle(new UpdatePriceListCommand(lista, "General", null, true, "Cambio",
            Code: campo == "code" ? "OTRA" : null,
            IncludesTaxes: campo == "includesTaxes" ? true : null,
            Scope: campo == "scope" ? new PriceListScopeInput(SalesChannelPublicId: e.Mostrador.PublicId) : null), default);

        r.Error.Code.Should().Be(ErroresDePrecios.ScopeLockedCode);
        Dato(r.Error, "field").Should().Be(campo);
    }

    [Fact]
    public async Task Reactivar_o_abrir_la_vigencia_mira_los_cruces_y_cerrarla_no()
    {
        var e = await EscenarioAsync();
        var vieja = (await CrearListaAsync(e, "VIEJA", Enero, new DateOnly(2026, 3, 31))).Value;
        await CrearListaAsync(e, "NUEVA", new DateOnly(2026, 4, 1));
        e.C.Olvidar();

        var abrir = await new UpdatePriceListCommandHandler(e.C.Db, _cerrojo).Handle(new UpdatePriceListCommand(vieja, "Vieja", null, true, "Reabrir"), default);
        e.C.Olvidar();
        var cerrar = await new UpdatePriceListCommandHandler(e.C.Db, _cerrojo).Handle(
            new UpdatePriceListCommand(vieja, "Vieja", new DateOnly(2026, 2, 28), true, "Cerrar antes"), default);

        abrir.Error.Code.Should().Be(ErroresDePrecios.OverlapsCode);
        cerrar.IsSuccess.Should().BeTrue();
        cerrar.Value.ValidTo.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public async Task Los_precios_van_en_la_unidad_base_o_una_de_venta_y_nunca_en_una_plantilla()
    {
        var e = await EscenarioAsync();
        var lista = (await CrearListaAsync(e, "GENERAL", Enero)).Value;

        var bien = await PreciosAsync(e, lista, (e.P1, "UND", 2100m), (e.P1, "DOC", 24000m));
        var otraVez = await PreciosAsync(e, lista, (e.P1, "UND", 2200m), (e.P1, "DOC", 24000m));
        var plantilla = await PreciosAsync(e, lista, (e.Plantilla, "UND", 50000m));
        var unidadAjena = await PreciosAsync(e, lista, (e.P1, "KG", 1m));

        bien.Value.Should().Be(new PriceListItemsResultDto(2, 0, 0, 0));
        otraVez.Value.Should().Be(new PriceListItemsResultDto(0, 1, 1, 0));
        plantilla.Error.Code.Should().Be(ErroresDePrecios.ProductNotPriceableCode);
        unidadAjena.Error.Code.Should().Be(ErroresDePrecios.UnitNotForSaleCode);
        e.C.Db.PriceListItems.Count(i => !i.IsDeleted).Should().Be(2);
    }

    [Fact]
    public async Task La_consulta_pagina_y_filtra_por_ambito_y_fecha()
    {
        var e = await EscenarioAsync();
        var general = (await CrearListaAsync(e, "GENERAL", Enero)).Value;
        await CrearListaAsync(e, "ANA", Enero, ambito: new PriceListScopeInput(e.Ana.PublicId));
        await PreciosAsync(e, general, (e.P1, "UND", 2100m));

        var personas = await new ListPriceListsQueryHandler(e.C.Db).Handle(new ListPriceListsQuery(Scope: "Person"), default);
        var generales = await new ListPriceListsQueryHandler(e.C.Db).Handle(new ListPriceListsQuery(Scope: "General", AsOf: new DateOnly(2026, 5, 1)), default);
        var detalle = await new GetPriceListQueryHandler(e.C.Db).Handle(new GetPriceListQuery(general), default);

        personas.Value.Items.Should().ContainSingle().Which.Scope.PersonName.Should().Contain("Ana");
        generales.Value.Items.Should().ContainSingle().Which.ItemCount.Should().Be(1);
        detalle.Value.Items.Should().ContainSingle().Which.Price.Should().Be(2100m);
    }

    // --------------------------------------------------------------------------------------- resolver (T598) --

    [Fact]
    public async Task Resolver_dice_que_lista_gano_en_que_dimensiones_y_las_candidatas()
    {
        var e = await EscenarioAsync();
        var general = (await CrearListaAsync(e, "GENERAL", Enero)).Value;
        var asociados = (await CrearListaAsync(e, "ASOC", Enero, ambito: new PriceListScopeInput(Segment: "AB"))).Value;
        await PreciosAsync(e, general, (e.P1, "UND", 2100m));
        await PreciosAsync(e, asociados, (e.P1, "UND", 1990m));
        var consulta = new ResolvePriceQueryHandler(e.C.Db, e.C.Reloj);

        var deLuis = await consulta.Handle(new ResolvePriceQuery(e.P1, PersonPublicId: e.Luis.PublicId), default);
        var sinCliente = await consulta.Handle(new ResolvePriceQuery(e.P1), default);
        var enDocena = await consulta.Handle(new ResolvePriceQuery(e.P1, e.C.Unidad("DOC").PublicId), default);

        deLuis.Value.Price.Should().Be(1990m, "otro asociado del mismo segmento toma la del segmento (US5-7)");
        deLuis.Value.PriceList.Code.Should().Be("ASOC");
        deLuis.Value.PriceList.MatchedDimensions.Should().Equal("Segment");
        deLuis.Value.Candidates.Select(c => c.Code).Should().Equal("ASOC", "GENERAL");
        sinCliente.Value.Price.Should().Be(2100m);
        enDocena.Error.Code.Should().Be(ErroresDePrecios.PriceNotFoundCode);
    }

    // ------------------------------------------------------------------------------------------ topes (T599) --

    private static async Task<(Role Cajeros, Role Supervisores)> RolesAsync(Escenario e)
    {
        var cajeros = new Role { Code = "CAJERO", Name = "Cajeros", IsActive = true };
        var supervisores = new Role { Code = "SUPERVISOR", Name = "Supervisores", IsActive = true };
        e.C.Db.Roles.AddRange(cajeros, supervisores);
        await e.C.Db.SaveChangesAsync();
        return (cajeros, supervisores);
    }

    private Task<Result<Guid>> TopeAsync(Escenario e, Role rol, decimal linea, decimal documento, DateOnly desde) =>
        new CreateDiscountCapCommandHandler(e.C.Db, _cerrojo).Handle(new CreateDiscountCapCommand(rol.PublicId, linea, documento, desde, null, "Comité"), default);

    [Fact]
    public async Task Un_tope_nuevo_cierra_el_anterior_del_rol_la_vispera_y_no_se_cruza()
    {
        var e = await EscenarioAsync();
        var (cajeros, _) = await RolesAsync(e);
        await TopeAsync(e, cajeros, 0.05m, 0.03m, Enero);

        var nuevo = await TopeAsync(e, cajeros, 0.08m, 0.05m, new DateOnly(2026, 7, 1));
        var cruzado = await TopeAsync(e, cajeros, 0.10m, 0.05m, new DateOnly(2026, 7, 1));

        nuevo.IsSuccess.Should().BeTrue();
        e.C.Db.DiscountCaps.Single(t => t.ValidFrom == Enero).ValidTo.Should().Be(new DateOnly(2026, 6, 30));
        cruzado.Error.Code.Should().Be(ErroresDePrecios.DiscountCapOverlapsCode);
        _candados.Should().OnlyContain(c => c == ClavesDeCerrojo.TopeDelRol(cajeros.Id));
        var lista = await new ListDiscountCapsQueryHandler(e.C.Db).Handle(new ListDiscountCapsQuery(cajeros.PublicId, new DateOnly(2026, 8, 1)), default);
        lista.Value.Should().ContainSingle().Which.MaxLinePercent.Should().Be(0.08m);
    }

    [Fact]
    public async Task Mi_tope_es_el_mayor_de_mis_roles_y_un_rol_sin_fila_da_cero()
    {
        var e = await EscenarioAsync();
        var (cajeros, supervisores) = await RolesAsync(e);
        await TopeAsync(e, cajeros, 0.05m, 0.10m, Enero);
        await TopeAsync(e, supervisores, 0.15m, 0.03m, Enero);
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, Cajero, Guid.NewGuid(), Guid.NewGuid(), "cajero", null,
            ExecutionChannel.Web, "GET", null, null));
        var consulta = new GetMyDiscountCapQueryHandler(e.C.Db, actor, e.C.Reloj);

        var sinRoles = await consulta.Handle(new GetMyDiscountCapQuery(), default);
        e.C.Db.UserRoles.Add(new UserRole { UserId = Cajero, RoleId = cajeros.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
        e.C.Db.UserRoles.Add(new UserRole { UserId = Cajero, RoleId = supervisores.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
        await e.C.Db.SaveChangesAsync();
        var conDos = await consulta.Handle(new GetMyDiscountCapQuery(), default);

        sinRoles.Value.Should().BeEquivalentTo(new MyDiscountCapDto(0m, 0m, []));
        conDos.Value.MaxLinePercent.Should().Be(0.15m);
        conDos.Value.MaxDocumentPercent.Should().Be(0.10m, "por línea y por total se toma el mayor por separado");
        conDos.Value.FromRoles.Select(r => r.RoleCode).Should().Equal("CAJERO", "SUPERVISOR");
    }

    // --------------------------------------------------------------------------------- plantillas 12 y 13 (T600) --

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private EjecutorDeImportacion Ejecutor(Escenario e)
    {
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? _hojas.Keys.First(), out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.EsMaestroGlobal.Returns(true);
        return new EjecutorDeImportacion(e.C.Db, _lector, permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
    }

    private async Task<Result<ImportResultDto>> ImportarListasAsync(Escenario e, ModoDeImportacion modo, string motivo = "Carga inicial")
    {
        var r = await new ImportPriceListsCommandHandler(e.C.Db, Ejecutor(e), _cerrojo)
            .Handle(new ImportPriceListsCommand(modo, new ArchivoDeImportacion("listas.xlsx", [1, 2, 3]), motivo), default);
        e.C.Olvidar();
        return r;
    }

    private async Task<Result<ImportResultDto>> ImportarTopesAsync(Escenario e, ModoDeImportacion modo, string motivo = "Comité")
    {
        var r = await new ImportDiscountCapsCommandHandler(e.C.Db, Ejecutor(e), _cerrojo)
            .Handle(new ImportDiscountCapsCommand(modo, new ArchivoDeImportacion("topes.xlsx", [1, 2, 3]), motivo), default);
        e.C.Olvidar();
        return r;
    }

    private static readonly string[] EncabezadosListas = [L.Codigo, L.Nombre, L.IncluyeImpuestos, L.Segmento, L.VigenteDesde, L.VigenteHasta];
    private static readonly string[] EncabezadosPrecios = [L.Lista, L.Producto, L.Unidad, L.Precio];

    [Fact]
    public async Task La_plantilla_12_crea_listas_y_precios_y_es_idempotente()
    {
        var e = await EscenarioAsync();
        Hoja(L.HojaListas, EncabezadosListas,
            ["GENERAL", "Lista general", "sí", null, "2026-01-01", null],
            ["ASOC", "Asociados", "sí", "ab", "2026-01-01", null]);
        Hoja(L.HojaPrecios, EncabezadosPrecios,
            ["GENERAL", "P1", null, "2100"],
            ["GENERAL", "P1", "DOC", "24000"],
            ["ASOC", "P1", null, "1990"]);

        var revision = await ImportarListasAsync(e, ModoDeImportacion.Review);
        var r = await ImportarListasAsync(e, ModoDeImportacion.Apply);

        revision.Value.RequiresReason.Should().BeTrue("crea vigencias de listas");
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        e.C.Db.PriceLists.Select(l => l.ScopeKey).Should().BeEquivalentTo([AmbitoDeLista.ClaveGeneral, "P:-|S:AB|C:-|B:-"]);
        e.C.Db.PriceListItems.Should().HaveCount(3);

        var otraVez = await ImportarListasAsync(e, ModoDeImportacion.Review);
        otraVez.Value.Sheets.Should().OnlyContain(h => h.Unchanged == h.Rows);
        otraVez.Value.RequiresReason.Should().BeFalse();
    }

    [Fact]
    public async Task La_plantilla_12_aplica_las_reglas_del_alta_todo_o_nada()
    {
        var e = await EscenarioAsync();
        Hoja(L.HojaListas, EncabezadosListas,
            ["GENERAL", "Lista general", "no", null, "2026-01-01", null],
            ["GEN2", "Otra general", "no", null, "2026-06-01", null],
            ["VIP", "Vip", "no", "VIP", "2026-01-01", null]);
        Hoja(L.HojaPrecios, EncabezadosPrecios, ["GENERAL", "CAMISA", null, "50000"], ["GENERAL", "P1", "KG", "1"], ["NOEXISTE", "P1", null, "1"]);

        var r = await ImportarListasAsync(e, ModoDeImportacion.Apply);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var cuerpo = (ImportResultDto)r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        cuerpo.Errors.Should().Contain(x => x.Row == 3 && x.Code == ErroresDePrecios.OverlapsCode && x.Column == L.VigenteDesde);
        cuerpo.Errors.Should().Contain(x => x.Row == 4 && x.Code == ErroresDePrecios.SegmentUnknownCode && x.Column == L.Segmento);
        cuerpo.Errors.Should().Contain(x => x.Sheet == L.HojaPrecios && x.Row == 2 && x.Code == ErroresDePrecios.ProductNotPriceableCode);
        cuerpo.Errors.Should().Contain(x => x.Sheet == L.HojaPrecios && x.Row == 3 && x.Code == ErroresDePrecios.UnitNotForSaleCode && x.Column == L.Unidad);
        cuerpo.Errors.Should().Contain(x => x.Sheet == L.HojaPrecios && x.Row == 4 && x.Code == ImportErrors.CellNotFound);
        e.C.Db.PriceLists.Should().BeEmpty("todo o nada");
    }

    [Fact]
    public async Task La_plantilla_12_no_cambia_includesTaxes_ni_la_fecha_de_inicio()
    {
        var e = await EscenarioAsync();
        await CrearListaAsync(e, "GENERAL", Enero);
        e.C.Olvidar();
        Hoja(L.HojaListas, EncabezadosListas, ["GENERAL", "Lista general", "sí", null, "2026-01-01", null]);

        var r = await ImportarListasAsync(e, ModoDeImportacion.Review);

        r.Value.Errors.Should().ContainSingle().Which.Should().Match<ErrorDeFila>(x => x.Code == ErroresDePrecios.ScopeLockedCode && x.Column == L.IncluyeImpuestos);
    }

    [Fact]
    public async Task La_plantilla_13_carga_topes_en_puntos_cierra_la_vigencia_anterior_y_pide_motivo()
    {
        var e = await EscenarioAsync();
        await RolesAsync(e);
        string[] encabezados = [T.Rol, T.TopeLinea, T.TopeDocumento, T.VigenteDesde];
        Hoja(T.Hoja, encabezados, ["CAJERO", "8", "5", "2026-07-01"], ["CAJERO", "5", "3", "2026-01-01"], ["SUPERVISOR", "15", "10", "2026-01-01"]);

        var sinMotivo = await ImportarTopesAsync(e, ModoDeImportacion.Apply, motivo: "");
        var r = await ImportarTopesAsync(e, ModoDeImportacion.Apply);

        sinMotivo.Error.Code.Should().Be(ImportErrors.InvalidCode);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var cajero = e.C.Db.DiscountCaps.Where(t => t.RoleId == e.C.Db.Roles.Single(x => x.Code == "CAJERO").Id).OrderBy(t => t.ValidFrom).ToList();
        cajero.Select(t => (t.MaxLineRate, t.ValidTo)).Should().Equal((0.05m, new DateOnly(2026, 6, 30)), (0.08m, (DateOnly?)null));
        cajero.Should().OnlyContain(t => t.Reason == "Comité");

        Hoja(T.Hoja, encabezados, ["CAJERO", "9", "5", "2026-03-01"], ["NADIE", "1", "1", "2026-01-01"]);
        var cruzada = await ImportarTopesAsync(e, ModoDeImportacion.Review);
        cruzada.Value.Errors.Should().Contain(x => x.Row == 2 && x.Code == ErroresDePrecios.DiscountCapOverlapsCode);
        cruzada.Value.Errors.Should().Contain(x => x.Row == 3 && x.Code == ImportErrors.CellNotFound && x.Column == T.Rol);
    }
}
