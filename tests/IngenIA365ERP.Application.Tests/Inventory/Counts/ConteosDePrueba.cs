using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Catalog.Products;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// La cooperativa de prueba de los conteos (feature 012, US11): la de <see cref="KardexDePrueba"/> —PRIN y B2 activas con su ubicación
/// <c>GENERAL</c>, P1 y P2, los tipos de ajuste y de anulación— más una ubicación A-01 en PRIN, P3 con código de barras de la unidad y de
/// la docena (factor 12), el tipo de conteo <c>CON</c> y los dos tipos de ajuste de conteo de la semilla (<c>CONP</c>/<c>CONN</c>) con
/// su política de un nivel <c>Inventory.Counts.Approve</c>, tres usuarios (jefe, dos contadores y el aprobador) y <c>INV_Setup</c> desde
/// julio. Arma el ciclo real —definir, abrir, capturar, cerrar, generar el ajuste— con la guarda del bloqueo en la confirmación y el
/// borrador, y el motor de aprobaciones real con la fuente del documento y la fecha del ajuste de conteo. Hoy es el 25 de septiembre de
/// 2026 (se mueve con <see cref="Hoy"/>).
/// </summary>
public sealed class ConteosDePrueba
{
    public const int Jefe = KardexDePrueba.Usuario;
    public const int ContadorA = 31;
    public const int ContadorB = 32;
    public const int Aprobador = 33;

    public KardexDePrueba K { get; }
    public TestApplicationDbContext Db => K.C.Db;
    public Warehouse PRIN => K.Principal;
    public Warehouse B2 => K.Segunda;
    public WarehouseLocation A01 { get; private set; } = null!;
    public WarehouseLocation General { get; private set; } = null!;
    public Guid P3 { get; private set; }
    public User UsuarioContadorA { get; private set; } = null!;
    public User UsuarioContadorB { get; private set; } = null!;
    public MotorDeAprobaciones Motor { get; private set; } = null!;

    private ConteosDePrueba(KardexDePrueba k) => K = k;

    public static async Task<ConteosDePrueba> CrearAsync()
    {
        var c = new ConteosDePrueba(await KardexDePrueba.CrearAsync());
        var db = c.Db;
        c.General = db.WarehouseLocations.Single(l => l.WarehouseId == c.PRIN.Id && l.IsDefault);
        c.A01 = new WarehouseLocation { WarehouseId = c.PRIN.Id, Code = "A-01", Name = "Estante A", IsActive = true };
        db.WarehouseLocations.Add(c.A01);
        db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), StartedAt = DateTime.UtcNow, StartedByUserId = Jefe });
        c.UsuarioContadorA = new User { Id = ContadorA, Username = "bodega.a", IsActive = true };
        c.UsuarioContadorB = new User { Id = ContadorB, Username = "bodega.b", IsActive = true };
        db.Users.AddRange(c.UsuarioContadorA, c.UsuarioContadorB, new User { Id = Aprobador, Username = "aprobador", IsActive = true });
        await db.SaveChangesAsync();

        // El conteo y los dos tipos de ajuste de conteo con su política, como los deja la semilla.
        var conteo = new InventoryDocumentType { Code = "CON", Name = "Conteo físico", Class = DocumentClass.PhysicalCount, IsActive = true, AllWarehouses = true };
        conteo.Sequences.Add(new DocumentSequence { DocumentType = conteo, Prefix = "CF", NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        db.InventoryDocumentTypes.Add(conteo);
        foreach (var (clase, (codigo, nombre)) in InventoryDocumentTypesSeeder.AjustesDeConteo)
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = nombre, Class = clase, IsActive = true, AllWarehouses = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
            await db.SaveChangesAsync();
            db.ApprovalPolicies.Add(new ApprovalPolicy
            {
                Module = ApprovalPolicy.ModuloInventario, Subject = ApprovalSubjects.DocumentConfirmation, DocumentTypePublicId = tipo.PublicId,
                PolicyKey = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, tipo.PublicId),
                Version = 1, ValidFrom = new DateOnly(2026, 1, 1), Reason = "semilla",
                Levels = [new ApprovalPolicyLevel { Order = 1, Threshold = 0m, PermissionCode = InventoryDocumentTypesSeeder.PermisoDeAprobacionDeConteo }],
            });
        }
        await db.SaveChangesAsync();

        c.P3 = (await c.K.C.ProductoAsync(c.K.C.Alta("P3", "Aceite 1 L",
            unidades: [new UnidadPedida(c.K.C.Unidad("DOC").PublicId, 12m, Domain.Enums.Inventory.ProductUnitUsage.Purchase)],
            codigos: [new CodigoPedido("7700000000031", null), new CodigoPedido("17700000000038", c.K.C.Unidad("DOC").PublicId)]))).PublicId;
        c.UsarMotorReal();
        return c;
    }

    // ------------------------------------------------------------------------------------------- servicios --

    public VistaDeConteos Vista() => new(Db, K.Vista(), K.Lector());

    public BloqueoPorConteo Bloqueo() => new(Db, Vista());

    public DetalleDeConteo Detalle() => new(Db, Vista(), K.Vista());

    public ReglaDelAjusteDeConteo Regla() => new(Db, K.Maestros(), K.Lector(), K.C.Reloj);

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
            new EfectoConteoFisico(),
        ]);
    }

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, K.Maestros(), K.Actor, K.C.Reloj, Efectos(), Motor, K.Cerrojo, new Numerador(Db, K.Cerrojo),
        new EmisorDeMensajes(Db, K.Actor, K.C.Reloj), K.Lector(), K.Vista(), [], [], Bloqueo());

    public SaveInventoryDraftCommandHandler Guardar() =>
        new(Db, K.Maestros(), K.Alcance, K.Actor, K.C.Reloj, Efectos(), K.Vista(), null, Bloqueo());

    public VoidInventoryDocumentCommandHandler Anular() => new(Db, K.Actor, K.C.Reloj, K.Vista(), Confirmacion());

    public DiscardInventoryDraftCommandHandler Descartar() => new(Db, K.Actor, K.C.Reloj, K.Vista());

    private ServiceProvider Servicios() => new ServiceCollection()
        .AddTransient(_ => Confirmacion())
        .AddTransient<IAntesDeConfirmarPorAprobacion>(_ => new FechaDelAjusteDeConteo(Db, Regla()))
        .BuildServiceProvider();

    private void UsarMotorReal()
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var fuentes = new IFuenteDeAprobacion[] { new FuenteDeAprobacionDeDocumento(Db, K.Maestros(), Servicios()) };
        Motor = new MotorDeAprobaciones(Db, K.Actor, K.Permisos, K.Alcance, limites, Substitute.For<IAutoridadDeOtroAprobador>(),
            Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(Db, fuentes), K.C.Reloj);
    }

    /// <summary>Cambia la persona que actúa (el jefe es <see cref="Jefe"/>).</summary>
    public void ComoUsuario(int userId) =>
        K.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            $"usuario{userId}@coop.co", $"usuario{userId}@coop.co", ExecutionChannel.Web, "POST /api/inventory/counts", "10.0.0.1", null));

    /// <summary>Mueve «hoy» (fecha local y UTC al mediodía de Colombia).</summary>
    public void Hoy(DateOnly fecha)
    {
        K.C.Reloj.HoyLocal.Returns(fecha);
        K.C.Reloj.UtcNow.Returns(fecha.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc));
    }

    /// <summary>Quita permisos a la persona que actúa (los demás siguen concedidos).</summary>
    public void SinPermisos(params string[] permisos)
    {
        K.Permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(x => !permisos.Contains(x.Arg<string>()));
    }

    /// <summary>Una vigencia de un parámetro del conteo (general o por bodega) desde el 1 de enero.</summary>
    public void Parametro(string clave, string valor, Warehouse? bodega = null) =>
        K.Parametro(clave, valor, bodega is null ? Domain.Enums.Parameters.ParameterScopeKind.None : Domain.Enums.Parameters.ParameterScopeKind.Warehouse, bodega?.Id ?? 0);

    // ------------------------------------------------------------------------------------------ escenarios --

    public Guid TipoDeConteo => K.Tipo("CON").PublicId;

    public PhysicalCountRequest Definicion(CountScope alcance = CountScope.Location, Warehouse? bodega = null, bool ciego = false,
        IReadOnlyList<Guid>? ubicaciones = null, IReadOnlyList<Guid>? productos = null, IReadOnlyList<Guid>? categorias = null,
        IReadOnlyList<Guid>? contadores = null) =>
        new(TipoDeConteo, (bodega ?? PRIN).PublicId, alcance == CountScope.All ? CountKind.Total : CountKind.Cyclic, alcance,
            categorias ?? (alcance == CountScope.Category ? [K.C.Abarrotes.PublicId] : null),
            ubicaciones ?? (alcance == CountScope.Location ? [General.PublicId] : null),
            productos ?? (alcance == CountScope.Selection ? [K.P1] : null),
            alcance == CountScope.AbcClass ? "A" : null, ciego, contadores, null);

    public Task<Result<PhysicalCountDto>> DefinirAsync(PhysicalCountRequest definicion) =>
        new CreatePhysicalCountCommandHandler(Db, K.Actor, K.C.Reloj, new DefinicionDeConteo(Db, K.Maestros(), K.Alcance), Detalle())
            .Handle(new CreatePhysicalCountCommand(definicion), default);

    public Task<Result<OpenPhysicalCountResultDto>> AbrirAsync(Guid conteo) =>
        new OpenPhysicalCountCommandHandler(Db, K.Actor, K.C.Reloj, K.Maestros(), K.Cerrojo, K.Lector(), Vista())
            .Handle(new OpenPhysicalCountCommand(conteo), default);

    /// <summary>Define y abre; falla la prueba si algo no pasa.</summary>
    public async Task<Guid> AbiertoAsync(PhysicalCountRequest? definicion = null)
    {
        var definido = await DefinirAsync(definicion ?? Definicion());
        if (definido.IsFailure) throw new InvalidOperationException($"{definido.Error.Code}: {definido.Error.Message}");
        var abierto = await AbrirAsync(definido.Value.PublicId);
        if (abierto.IsFailure) throw new InvalidOperationException($"{abierto.Error.Code}: {abierto.Error.Message}");
        return definido.Value.PublicId;
    }

    public Task<Result<CaptureResultDto>> CapturarAsync(Guid conteo, byte ronda, params CountReadRequest[] lecturas) =>
        new CapturePhysicalCountCommandHandler(Db, K.Actor, K.C.Reloj, K.Maestros(), K.Cerrojo, Vista())
            .Handle(new CapturePhysicalCountCommand(conteo, ronda, lecturas), default);

    public CountReadRequest Lectura(Guid producto, decimal? cantidad = null, WarehouseLocation? ubicacion = null) =>
        new(null, producto, null, cantidad, (ubicacion ?? General).PublicId);

    public Task<Result<ClosePhysicalCountResultDto>> CerrarAsync(Guid conteo) =>
        new ClosePhysicalCountCommandHandler(Db, K.Cerrojo, Vista(), K.Vista(), Confirmacion())
            .Handle(new ClosePhysicalCountCommand(conteo), default);

    public Task<Result<CountAdjustmentResultDto>> GenerarAjusteAsync(Guid conteo) =>
        new GenerateCountAdjustmentCommandHandler(Db, K.Actor, Motor, Vista(), Regla(), Confirmacion())
            .Handle(new GenerateCountAdjustmentCommand(conteo), default);

    public Task<Result<CountAdjustmentPreviewDto>> VistaPreviaAsync(Guid conteo) =>
        new GetCountAdjustmentPreviewQueryHandler(Db, Vista(), K.Vista(), Regla()).Handle(new GetCountAdjustmentPreviewQuery(conteo), default);

    public Task<Result<PhysicalCountDto>> DetalleAsync(Guid conteo) =>
        new GetPhysicalCountQueryHandler(Vista(), Detalle()).Handle(new GetPhysicalCountQuery(conteo), default);

    /// <summary>Decide la solicitud pendiente del documento como <paramref name="usuario"/>.</summary>
    public async Task<Result<DecisionResultDto>> DecidirAsync(Guid documento, int usuario, ApprovalDecisionKind decision = ApprovalDecisionKind.Approve)
    {
        var solicitud = await Db.ApprovalRequests.AsNoTracking().SingleAsync(r => r.SourcePublicId == documento && r.Status == ApprovalRequestStatus.Pending);
        ComoUsuario(usuario);
        return await Motor.DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, decision, decision == ApprovalDecisionKind.Reject ? "no" : null,
            solicitud.ContentSha256), default);
    }

    public InventoryDocument Documento(Guid publicId) => Db.InventoryDocuments.Include(d => d.Lines).AsNoTracking().Single(d => d.PublicId == publicId);

    public List<CountSnapshotLine> Lineas(Guid conteo)
    {
        var id = Documento(conteo).Id;
        return Db.CountSnapshotLines.AsNoTracking().Where(l => l.DocumentId == id).OrderBy(l => l.Id).ToList();
    }

    /// <summary>La existencia física de un producto en una bodega.</summary>
    public decimal Fisico(Guid producto, Warehouse? bodega = null)
    {
        var id = K.ProductoId(producto);
        var b = (bodega ?? PRIN).Id;
        return Db.StockBalances.AsNoTracking().Where(s => s.ProductId == id && s.WarehouseId == b).Select(s => s.Physical).FirstOrDefault();
    }

    /// <summary>Una salida confirmada (ajuste negativo con causa) en la fecha dada.</summary>
    public Task<(Guid Documento, Result<ConfirmationResultDto> Confirmacion)> SalidaAsync(Guid producto, decimal cantidad, Warehouse? bodega = null,
        DateOnly? fecha = null, WarehouseLocation? ubicacion = null) =>
        AjusteAsync(K.Borrador("AJN", bodega, K.Causa(), fecha: fecha,
            lineas: [K.Linea(producto, cantidad) with { LocationPublicId = ubicacion?.PublicId }]));

    /// <summary>Una entrada confirmada con costo, en la fecha dada.</summary>
    public async Task<Guid> EntradaAsync(Guid producto, decimal cantidad, decimal costo, Warehouse? bodega = null, DateOnly? fecha = null,
        WarehouseLocation? ubicacion = null)
    {
        var (documento, r) = await AjusteAsync(K.Borrador("AJP", bodega, fecha: fecha,
            lineas: [K.Linea(producto, cantidad, costo) with { LocationPublicId = ubicacion?.PublicId }]));
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return documento;
    }

    private async Task<(Guid Documento, Result<ConfirmationResultDto> Confirmacion)> AjusteAsync(SaveInventoryDraftRequest borrador)
    {
        var guardado = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, borrador), default);
        if (guardado.IsFailure) throw new InvalidOperationException($"{guardado.Error.Code}: {guardado.Error.Message}");
        return (guardado.Value.PublicId,
            await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(guardado.Value.PublicId, DocumentClassGroup.Adjustments), default));
    }

    /// <summary>El conteo con sus lecturas: abre, captura lo pedido como el contador A y cierra.</summary>
    public async Task<Guid> CerradoAsync(params (Guid Producto, decimal Contado)[] contados)
    {
        var conteo = await AbiertoAsync();
        ComoUsuario(ContadorA);
        var r = await CapturarAsync(conteo, 1, contados.Select(c => Lectura(c.Producto, c.Contado)).ToArray());
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        ComoUsuario(Jefe);
        var cerrado = await CerrarAsync(conteo);
        if (cerrado.IsFailure) throw new InvalidOperationException($"{cerrado.Error.Code}: {cerrado.Error.Message}");
        return conteo;
    }

    public static string? Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : null;
}
