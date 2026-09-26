using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using S = IngenIA365ERP.Application.Inventory.GoLive.PlantillaDeSaldoInicial;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// La cooperativa de prueba de la puesta en marcha (feature 012, US4): la de <see cref="KardexDePrueba"/> —PRIN y B2 activas, el
/// tránsito TR01, los tipos de ajuste— más dos bodegas no activas de la misma sucursal (B3, con la ubicación A-01, y B4), una
/// segunda sucursal con B5 y su tránsito TR05 también no activos, y los tipos sembrados de verdad
/// (<see cref="InventoryDocumentTypesSeeder"/>: el de saldo inicial <c>SIN</c> con su política de un nivel desde el 1 de enero).
/// Arma el ciclo común con las estrategias reales, incluida <see cref="EfectoSaldoInicial"/>, la importación con un lector
/// sustituido y, si se pide, el motor de aprobaciones real (<see cref="MotorReal"/>).
/// </summary>
public sealed class PuestaEnMarchaDePrueba
{
    public static readonly DateOnly Corte = new(2026, 9, 10);

    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public KardexDePrueba K { get; }
    public TestApplicationDbContext Db => K.C.Db;
    public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
    public ICurrentUserPermissions PermisosDeImportacion { get; } = Substitute.For<ICurrentUserPermissions>();
    public Warehouse B3 { get; private set; } = null!;
    public Warehouse B4 { get; private set; } = null!;
    public Branch Sucursal2 { get; private set; } = null!;
    public Warehouse B5 { get; private set; } = null!;
    public Warehouse TR05 { get; private set; } = null!;
    public MotorDeAprobaciones? MotorReal { get; private set; }
    public PuestaEnMarchaOptions Opciones { get; } = new() { PermitirActivacionSinComparacion = true };

    private PuestaEnMarchaDePrueba(KardexDePrueba k)
    {
        K = k;
        PermisosDeImportacion.EsMaestroGlobal.Returns(true);
        Lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? "Datos", out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
    }

    public static async Task<PuestaEnMarchaDePrueba> CrearAsync()
    {
        var p = new PuestaEnMarchaDePrueba(await KardexDePrueba.CrearAsync());
        var db = p.Db;
        var operativa = p.K.Principal.WarehouseTypeId;
        var deTransito = p.K.Transito.WarehouseTypeId;

        p.B3 = Bodega("B3", "Tercera", operativa, p.K.Sucursal.Id, WarehouseBehavior.Operational, "A-01");
        p.B4 = Bodega("B4", "Cuarta", operativa, p.K.Sucursal.Id, WarehouseBehavior.Operational);
        p.Sucursal2 = new Branch { Name = "Florida" };
        db.Add(p.Sucursal2);
        await db.SaveChangesAsync();
        p.B5 = Bodega("B5", "Florida", operativa, p.Sucursal2.Id, WarehouseBehavior.Operational);
        p.TR05 = Bodega("TR05", "Tránsito Florida", deTransito, p.Sucursal2.Id, WarehouseBehavior.Transit);
        db.Warehouses.AddRange(p.B3, p.B4, p.B5, p.TR05);
        await db.SaveChangesAsync();

        await InventoryDocumentTypesSeeder.AplicarAsync(db, default);
        return p;
    }

    private static Warehouse Bodega(string codigo, string nombre, int tipo, int sucursal, WarehouseBehavior comportamiento, string? otraUbicacion = null)
    {
        var bodega = new Warehouse
        {
            Code = codigo, Name = nombre, BranchId = sucursal, WarehouseTypeId = tipo, Behavior = comportamiento,
            ActivationStatus = WarehouseActivationStatus.NotActivated, IsActive = true,
        };
        bodega.Locations.Add(new WarehouseLocation
        {
            Warehouse = bodega, Code = WarehouseLocation.CodigoPorDefecto, Name = WarehouseLocation.NombrePorDefecto, IsDefault = true, IsActive = true,
        });
        if (otraUbicacion is not null)
            bodega.Locations.Add(new WarehouseLocation { Warehouse = bodega, Code = otraUbicacion, Name = otraUbicacion, IsActive = true });
        return bodega;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public EfectosDeClase Efectos()
    {
        var registro = K.Registro();
        var reversion = new ReversionDeKardex(Db, registro);
        var emision = new EmisionDeInventario(Db);
        var maestros = K.Maestros();
        return new EfectosDeClase(
        [
            new EfectoDeAjustePositivo(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoDeAjusteNegativo(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoDeConsumoInterno(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoDeBaja(registro, reversion, emision, maestros, K.Permisos, Db),
            new EfectoSaldoInicial(registro, reversion, emision, maestros, Db),
        ]);
    }

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, K.Maestros(), K.Actor, K.C.Reloj, Efectos(), (IMotorDeAprobaciones?)MotorReal ?? K.Motor, K.Cerrojo, new Numerador(Db, K.Cerrojo),
        new EmisorDeMensajes(Db, K.Actor, K.C.Reloj), K.Lector(), K.Vista(), [], []);

    public SaveInventoryDraftCommandHandler Guardar() => new(Db, K.Maestros(), K.Alcance, K.Actor, K.C.Reloj, Efectos(), K.Vista());

    public VoidInventoryDocumentCommandHandler Anular() => new(Db, K.Actor, K.C.Reloj, K.Vista(), Confirmacion());

    /// <summary>
    /// El motor de aprobaciones real, con la fuente de documentos real: la última aprobación reentra por
    /// <see cref="ConfirmacionDeDocumento"/> (que usa este mismo motor).
    /// </summary>
    public MotorDeAprobaciones UsarMotorReal()
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var servicios = new ServiceCollection().AddTransient(_ => Confirmacion()).BuildServiceProvider();
        var fuente = new FuenteDeAprobacionDeDocumento(Db, K.Maestros(), servicios);
        MotorReal = new MotorDeAprobaciones(Db, K.Actor, K.Permisos, K.Alcance, limites, Substitute.For<IAutoridadDeOtroAprobador>(),
            Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(Db, [fuente]), K.C.Reloj);
        return MotorReal;
    }

    /// <summary>Cambia la persona que actúa (el creador es <see cref="KardexDePrueba.Usuario"/>).</summary>
    public void ComoUsuario(int userId) =>
        K.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            $"usuario{userId}@coop.co", $"usuario{userId}@coop.co", ExecutionChannel.Web, "POST /api/inventory/opening-balances", "10.0.0.1", null));

    public EjecutorDeImportacion Ejecutor() =>
        new(Db, Lector, PermisosDeImportacion, new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());

    public static ArchivoDeImportacion Archivo => new("saldo-inicial.xlsx", [1, 2, 3]);

    public static readonly string[] Encabezados = [S.Bodega, S.FechaDeCorte, S.Producto, S.Ubicacion, S.Cantidad, S.CostoUnitario];

    public void Datos(string[] encabezados, IEnumerable<string?[]> filas)
    {
        _hojas.Clear();
        _hojas["Datos"] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
    }

    /// <summary>Una fila de la plantilla 14 con la fecha de corte por defecto.</summary>
    public static string?[] Fila(string bodega, string producto, string cantidad, string costo, string? ubicacion = null, DateOnly? corte = null) =>
        [bodega, (corte ?? Corte).ToString("yyyy-MM-dd"), producto, ubicacion, cantidad, costo];

    public async Task<Result<ImportResultDto>> ImportarSaldoAsync(ModoDeImportacion modo, params string?[][] filas)
    {
        Datos(Encabezados, filas);
        return await ImportarSaldoAsync(modo);
    }

    public async Task<Result<ImportResultDto>> ImportarSaldoAsync(ModoDeImportacion modo)
    {
        var r = await new ImportOpeningBalanceCommandHandler(Db, Ejecutor(), K.Alcance, K.Actor, K.C.Reloj, K.Lector())
            .Handle(new ImportOpeningBalanceCommand(modo, Archivo), default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public async Task<Result<ImportResultDto>> ImportarCifrasAsync(ModoDeImportacion modo, string[] encabezados, params string?[][] filas)
    {
        Datos(encabezados, filas);
        var r = await new ImportLegacyFiguresCommandHandler(Db, Ejecutor(), K.Alcance, K.Actor, K.C.Reloj)
            .Handle(new ImportLegacyFiguresCommand(modo, new ArchivoDeImportacion("cifras-solido.xlsx", [4, 5, 6])), default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public ActivateWarehouseCommandHandler Activar() =>
        new(Db, new ComparacionDeActivacion(Db, K.Alcance, K.C.Reloj), K.Actor, K.Permisos, K.C.Reloj, Options.Create(Opciones));

    public GetWarehouseActivationPreviewQueryHandler VistaPrevia() => new(new ComparacionDeActivacion(Db, K.Alcance, K.C.Reloj));

    /// <summary>
    /// Productos en bloque (<c>Q00001</c>…), inventariables, en unidad base <c>UND</c> y del grupo de abarrotes, para los archivos
    /// grandes: se escriben directo, sin el comando (que es lo que prueba US1).
    /// </summary>
    public async Task<IReadOnlyList<string>> ProductosEnBloqueAsync(int cuantos)
    {
        var und = K.C.Unidad("UND").Id;
        var codigos = new List<string>(cuantos);
        for (var i = 1; i <= cuantos; i++)
        {
            var codigo = $"Q{i:00000}";
            codigos.Add(codigo);
            Db.Products.Add(new Product
            {
                Code = codigo, Name = $"Producto {i}", Kind = ProductKind.Inventoriable, Status = ProductStatus.Active,
                CategoryId = K.C.Abarrotes.Id, BaseUnitId = und, AccountingGroupId = K.C.GrupoAbarrotes.Id, SearchText = codigo,
            });
        }
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return codigos;
    }

    /// <summary>Importa y aplica un saldo de una línea y devuelve el borrador; falla la prueba si no se aplica.</summary>
    public async Task<Guid> BorradorAsync(Warehouse bodega, string producto, decimal cantidad, decimal costo, DateOnly? corte = null)
    {
        var r = await ImportarSaldoAsync(ModoDeImportacion.Apply,
            Fila(bodega.Code, producto, cantidad.ToString(System.Globalization.CultureInfo.InvariantCulture),
                costo.ToString(System.Globalization.CultureInfo.InvariantCulture), corte: corte));
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return ((IReadOnlyList<DocumentoDeSaldoInicialDto>)r.Value.Extra[ImportOpeningBalanceCommandHandler.ExtraDocumentos]!).Single().DocumentPublicId;
    }

    public Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(documento, DocumentClassGroup.OpeningBalance), default);

    /// <summary>Carga y confirma (sin niveles) un saldo de una línea.</summary>
    public async Task<Guid> SaldoConfirmadoAsync(Warehouse bodega, string producto, decimal cantidad, decimal costo, DateOnly? corte = null)
    {
        var documento = await BorradorAsync(bodega, producto, cantidad, costo, corte);
        var r = await ConfirmarAsync(documento);
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        Db.ChangeTracker.Clear();
        return documento;
    }
}
