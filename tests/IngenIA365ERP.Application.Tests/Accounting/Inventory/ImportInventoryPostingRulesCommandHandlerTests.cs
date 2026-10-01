using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using P = IngenIA365ERP.Application.Accounting.Inventory.Reglas.PlantillaDeMatrizDeInventario;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T452 (T27; contracts/plantillas.md §16 y §0.5; contracts/contabilidad.md §2.7): la plantilla 16 con la
/// mecánica común. Las columnas son las de §16; la tarifa en puntos se guarda como fracción; <c>review</c> no guarda;
/// <c>apply</c> es todo o nada con 422 <c>Import.Invalid</c> y <c>data.errors[] { row, column }</c>; la misma clave con una
/// fecha posterior es una versión nueva; y las reglas son las del alta una a una (mismos códigos).
/// </summary>
public class ImportInventoryPostingRulesCommandHandlerTests
{
    private sealed class Escenario
    {
        public MatrizDePrueba M { get; } = new();
        public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
        public ICurrentUserPermissions Permisos { get; } = Substitute.For<ICurrentUserPermissions>();
        public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();

        public Escenario()
        {
            Permisos.EsMaestroGlobal.Returns(true);
        }

        public void Archivo(params string?[][] filas)
        {
            var tabla = new TablaLeida(P.Columnas.ToArray(), filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
            Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success(tabla)));
        }

        public async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo)
        {
            var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
            var ejecutor = new EjecutorDeImportacion(M.D.Db, Lector, Permisos, servicios);
            var emisor = new AccountingAuditEmitter(Auditoria, M.D.User, M.D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            var reglas = new ReglasDeLaMatriz(M.D.Db, M.Dimensiones, new ResolutorDeReglas(M.D.Db));
            var r = await new ImportInventoryPostingRulesCommandHandler(M.D.Db, ejecutor, reglas, emisor)
                .Handle(new ImportInventoryPostingRulesCommand(modo, new ArchivoDeImportacion("matriz.xlsx", [1, 2, 3])), default);
            M.D.Db.ChangeTracker.Clear();
            return r;
        }
    }

    /// <summary>Una fila en el orden de §16.</summary>
    private static string?[] Fila(string operacion, string rol, string cuenta, string desde = "2026-01-01", string? grupo = null, string? bodega = null,
        string? punto = null, string? medio = null, string? tarifa = null, string? porcentaje = null, string? causa = null, string? sucursal = null,
        string? centro = null, string? hasta = null, string? notas = null) =>
        [operacion, rol, grupo, bodega, punto, medio, tarifa, porcentaje, causa, sucursal, centro, cuenta, desde, hasta, notas];

    [Fact]
    public void La_plantilla_tiene_las_columnas_de_16_y_sus_instrucciones_salen_de_los_catalogos()
    {
        P.Definicion.Clave.Should().Be("accounting.inventory-rules");
        P.Definicion.EsDeUnaHoja.Should().BeTrue();
        P.Definicion.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(
            "operacion", "rol", "grupoContable", "bodega", "puntoDeVenta", "medioDePago", "tarifa", "tarifaPorcentaje", "causa",
            "sucursal", "centroDeCosto", "cuenta", "vigenteDesde", "vigenteHasta", "notas");
        P.Definicion.Hojas[0].Columnas.Where(c => c.Obligatoria).Select(c => c.Nombre).Should().Equal("operacion", "rol", "cuenta", "vigenteDesde");
        P.Definicion.Hoja("Datos")!.Columna("rol")!.Reglas.Should().Contain("DiferenciaDeArqueo → MedioDePago, Faltante, GastoDeArqueo, Sobrante");
        P.Definicion.Hoja("Datos")!.Columna("grupoContable")!.Reglas.Should().Contain("obligatoria en Inventario, Transito, Costo, Ingreso");
    }

    [Fact]
    public async Task Review_no_guarda_y_apply_crea_con_la_tarifa_como_fraccion()
    {
        var e = new Escenario();
        e.Archivo(
            Fila("Compra", "Inventario", "14350501", grupo: "ABARROTES", bodega: "*", notas: "Contadora"),
            Fila("Venta", "Impuesto", "24080501", tarifa: "IVA19", porcentaje: "19"),
            Fila("Baja", "Contrapartida", "61350501", causa: "VENC", sucursal: "Principal"));

        var revision = await e.ImportarAsync(ModoDeImportacion.Review);
        revision.IsSuccess.Should().BeTrue(revision.Error?.Message);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(x => $"{x.Row}/{x.Column}: {x.Message}")));
        revision.Value.Sheets.Single().Created.Should().Be(3);
        (await e.M.D.Db.InventoryPostingRules.CountAsync()).Should().Be(0, "review corre las mismas reglas y no guarda nada");

        var aplicacion = await e.ImportarAsync(ModoDeImportacion.Apply);
        aplicacion.IsSuccess.Should().BeTrue(aplicacion.Error?.Message);
        var reglas = await e.M.D.Db.InventoryPostingRules.ToListAsync();
        reglas.Should().HaveCount(3);
        reglas.Single(r => r.Role == "Impuesto").TaxRate.Should().Be(0.19m, "19 puntos se guardan como 0,19");
        reglas.Single(r => r.Role == "Inventario").WarehouseCode.Should().BeNull("* es comodín");
        reglas.Single(r => r.Role == "Inventario").Notes.Should().Be("Contadora");
        reglas.Single(r => r.Role == "Contrapartida").BranchId.Should().Be(e.M.D.Principal.Id, "la sucursal se busca por código o nombre");
        await e.Auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == "Accounting.InventoryRules.Imported"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Apply_es_todo_o_nada_con_los_errores_por_fila_y_columna()
    {
        var e = new Escenario();
        e.Archivo(
            Fila("Compra", "Inventario", "14350501", grupo: "ABARROTES"),
            Fila("Compra", "Inventario", "14350501", grupo: "LACTEOS"),
            Fila("Venta", "Impuesto", "24080501", tarifa: "IVA19", porcentaje: "16"),
            Fila("Compra", "Inventario", "51050501", grupo: "ASEO"),
            Fila("Compra", "Inventario", "14350501", grupo: "ASEO", medio: "EFE"));

        var r = await e.ImportarAsync(ModoDeImportacion.Apply);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Import.Invalid");
        var cuerpo = (ImportResultDto)((ErrorConDatos)r.Error).Data;
        cuerpo.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(new[]
        {
            (3, "grupoContable", "Accounting.InventoryRule.DimensionCodeUnknown"),
            (4, "tarifaPorcentaje", "Accounting.InventoryRule.TaxRateMismatch"),
            (5, "cuenta", "Accounting.Account.NotEligible"),
            (6, "medioDePago", "Accounting.InventoryRule.DimensionNotAllowed"),
        }, "las mismas reglas y los mismos códigos que el alta una a una");
        (await e.M.D.Db.InventoryPostingRules.CountAsync()).Should().Be(0, "con un error no se guarda nada");
    }

    [Fact]
    public async Task La_misma_clave_con_fecha_posterior_es_version_nueva_y_repetir_el_archivo_no_cambia_nada()
    {
        var e = new Escenario();
        e.Archivo(Fila("Compra", "Inventario", "14350501", grupo: "ABARROTES"));
        (await e.ImportarAsync(ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();

        e.Archivo(
            Fila("Compra", "Inventario", "14350501", grupo: "ABARROTES"),
            Fila("Compra", "Inventario", "14350502", "2026-03-01", grupo: "ABARROTES"));
        var r = await e.ImportarAsync(ModoDeImportacion.Apply);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Sheets.Single().Unchanged.Should().Be(1);
        r.Value.Sheets.Single().Created.Should().Be(1);
        var reglas = await e.M.D.Db.InventoryPostingRules.OrderBy(x => x.ValidFrom).ToListAsync();
        reglas.Should().HaveCount(2);
        reglas[0].ValidTo.Should().Be(new DateOnly(2026, 2, 28), "la versión nueva cierra la anterior la víspera");
        reglas[1].AccountId.Should().Be(e.M.InventarioBodega.Id);
    }

    [Fact]
    public async Task La_retroactividad_y_los_cruces_responden_como_en_el_alta()
    {
        var e = new Escenario();
        e.Archivo(Fila("Compra", "Inventario", "14350501", grupo: "ABARROTES"));
        (await e.ImportarAsync(ModoDeImportacion.Apply)).IsSuccess.Should().BeTrue();
        e.M.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra(), "CO-7");

        e.Archivo(
            Fila("Compra", "Inventario", "14350502", "2026-03-05", grupo: "ABARROTES"),
            Fila("Compra", "Inventario", "14350502", grupo: "ABARROTES"));
        var r = await e.ImportarAsync(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Select(x => (x.Row, x.Column, x.Code)).Should().BeEquivalentTo(new[]
        {
            (2, "vigenteDesde", "Accounting.InventoryRule.RetroactiveOverPosted"),
            (3, "cuenta", "Accounting.InventoryRule.Overlaps"),
        });
    }

    [Fact]
    public async Task La_descarga_con_datos_trae_las_vigentes_con_la_tarifa_en_puntos()
    {
        var e = new Escenario();
        e.M.Regla("Venta", "Impuesto", e.M.IvaGenerado, tarifa: "IVA19", valorTarifa: 0.19m);
        e.M.Regla("Compra", "Inventario", e.M.Inventario, grupo: "ABARROTES", hasta: new DateOnly(2026, 1, 31));

        e.M.D.Clock.HoyLocal.Returns(new DateOnly(2026, 3, 20));
        var r = await new GetInventoryRulesTemplateQueryHandler(e.M.D.Db, e.M.D.Clock).Handle(new GetInventoryRulesTemplateQuery(), default);

        var filas = r.Value.De("Datos");
        filas.Should().ContainSingle("la regla cerrada en enero ya no rige el 20 de marzo");
        filas[0][P.Columnas.ToList().IndexOf(P.TarifaPorcentaje)].Should().Be(19m);
        filas[0][P.Columnas.ToList().IndexOf(P.Cuenta)].Should().Be("24080501");
    }
}
