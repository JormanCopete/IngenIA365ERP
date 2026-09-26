using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Transfers;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// La cooperativa de prueba de los traslados (feature 012, US10): la de <see cref="KardexDePrueba"/> —PRIN y B2 activas en la
/// sucursal Principal con su tránsito TR01, los tipos de ajuste y anulación— más una segunda sucursal (Florida) con PV2 activa y su
/// tránsito TR02, una segunda ubicación A-01 en PRIN, B-01 en PV2, y los tipos TRD (despacho), TRR (recepción) y MUB (movimiento
/// entre ubicaciones) con su consecutivo. Arma el ciclo común con las estrategias reales —ajustes, despacho, recepción y
/// ubicaciones— y, si se pide, el motor de aprobaciones real con las fuentes del documento y de la diferencia de traslado.
/// </summary>
public sealed class TrasladosDePrueba
{
    public const int Despachador = KardexDePrueba.Usuario;
    public const int Receptor = 21;
    public const int Proponente = 22;
    public const int Aprobador = 23;

    public KardexDePrueba K { get; }
    public TestApplicationDbContext Db => K.C.Db;
    public Warehouse PRIN => K.Principal;
    public Warehouse TR01 => K.Transito;
    public Branch Florida { get; private set; } = null!;
    public Warehouse PV2 { get; private set; } = null!;
    public Warehouse TR02 { get; private set; } = null!;
    public WarehouseLocation A01 { get; private set; } = null!;
    public WarehouseLocation B01 { get; private set; } = null!;
    public MotorDeAprobaciones? MotorReal { get; private set; }

    private TrasladosDePrueba(KardexDePrueba k) => K = k;

    public static async Task<TrasladosDePrueba> CrearAsync()
    {
        var t = new TrasladosDePrueba(await KardexDePrueba.CrearAsync());
        var db = t.Db;
        var operativa = t.PRIN.WarehouseTypeId;
        var deTransito = t.TR01.WarehouseTypeId;

        t.Florida = new Branch { Name = "Florida" };
        db.Add(t.Florida);
        await db.SaveChangesAsync();
        t.PV2 = Bodega("PV2", "Punto Florida", operativa, t.Florida.Id, WarehouseBehavior.Operational);
        t.TR02 = Bodega("TR02", "Tránsito Florida", deTransito, t.Florida.Id, WarehouseBehavior.Transit);
        db.Warehouses.AddRange(t.PV2, t.TR02);
        t.A01 = new WarehouseLocation { WarehouseId = t.PRIN.Id, Code = "A-01", Name = "Estante A", IsActive = true };
        db.WarehouseLocations.Add(t.A01);
        await db.SaveChangesAsync();
        t.B01 = new WarehouseLocation { WarehouseId = t.PV2.Id, Code = "B-01", Name = "Estante B", IsActive = true };
        db.WarehouseLocations.Add(t.B01);

        foreach (var (codigo, clase) in new[] { ("TRD", DocumentClass.TransferDispatch), ("TRR", DocumentClass.TransferReceipt), ("MUB", DocumentClass.LocationMove) })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, AllWarehouses = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
        }
        await db.SaveChangesAsync();
        return t;
    }

    private static Warehouse Bodega(string codigo, string nombre, int tipo, int sucursal, WarehouseBehavior comportamiento)
    {
        var bodega = new Warehouse
        {
            Code = codigo, Name = nombre, BranchId = sucursal, WarehouseTypeId = tipo, Behavior = comportamiento,
            ActivationStatus = WarehouseActivationStatus.Active, IsActive = true,
        };
        bodega.Locations.Add(new WarehouseLocation
        {
            Warehouse = bodega, Code = WarehouseLocation.CodigoPorDefecto, Name = WarehouseLocation.NombrePorDefecto, IsDefault = true, IsActive = true,
        });
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
            new EfectoDespachoDeTraslado(registro, reversion, emision, maestros, Db),
            new EfectoRecepcionDeTraslado(registro, emision, Db),
            new EfectoMovimientoEntreUbicaciones(registro, reversion, maestros, Db),
        ]);
    }

    public IMotorDeAprobaciones Motor => (IMotorDeAprobaciones?)MotorReal ?? K.Motor;

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, K.Maestros(), K.Actor, K.C.Reloj, Efectos(), Motor, K.Cerrojo, new Numerador(Db, K.Cerrojo),
        new EmisorDeMensajes(Db, K.Actor, K.C.Reloj), K.Lector(), K.Vista(), [], []);

    public SaveInventoryDraftCommandHandler Guardar() => new(Db, K.Maestros(), K.Alcance, K.Actor, K.C.Reloj, Efectos(), K.Vista());

    public VoidInventoryDocumentCommandHandler Anular() => new(Db, K.Actor, K.C.Reloj, K.Vista(), Confirmacion());

    private ServiceProvider Servicios() => new ServiceCollection().AddTransient(_ => Confirmacion()).BuildServiceProvider();

    public CierreDeDiferencias Cierre() => new(Db, K.Actor, K.C.Reloj, Servicios());

    public VistaDeTraslados Vista() => new(Db, K.Vista());

    /// <summary>El motor real con las dos fuentes: la última aprobación confirma el documento que resuelve la diferencia.</summary>
    public MotorDeAprobaciones UsarMotorReal()
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var fuentes = new IFuenteDeAprobacion[]
        {
            new FuenteDeAprobacionDeDocumento(Db, K.Maestros(), Servicios()),
            new FuenteDeAprobacionDeDiferencia(Db, Cierre()),
        };
        MotorReal = new MotorDeAprobaciones(Db, K.Actor, K.Permisos, K.Alcance, limites, Substitute.For<IAutoridadDeOtroAprobador>(),
            Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(Db, fuentes), K.C.Reloj);
        return MotorReal;
    }

    /// <summary>Cambia la persona que actúa (quien despacha es <see cref="Despachador"/>).</summary>
    public void ComoUsuario(int userId) =>
        K.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            $"usuario{userId}@coop.co", $"usuario{userId}@coop.co", ExecutionChannel.Web, "POST /api/inventory/transfers", "10.0.0.1", null));

    /// <summary>Un alcance de sólo estas bodegas.</summary>
    public void SoloEn(params Warehouse[] bodegas) =>
        K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new AlcanceDeInventario(false, bodegas.Select(b => b.Id).ToHashSet(), null, false, new HashSet<int>(), null));

    // ------------------------------------------------------------------------------------------ escenarios --

    public Guid Tipo(string codigo) => K.Tipo(codigo).PublicId;

    /// <summary>El borrador de un despacho de <paramref name="origen"/> a <paramref name="destino"/>.</summary>
    public SaveInventoryDraftRequest Despacho(Warehouse origen, Warehouse destino, DateOnly? fecha = null, params SaveInventoryDraftLine[] lineas) =>
        new(Tipo("TRD"), fecha, origen.PublicId, destino.PublicId, null, null, null, null, null, null, null, null, null, lineas);

    public async Task<Result<InventoryDocumentDto>> GuardarDespachoAsync(SaveInventoryDraftRequest borrador) =>
        await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Transfers, borrador), default);

    public async Task<Result<ConfirmationResultDto>> DespacharAsync(Guid despacho) =>
        await new DispatchTransferCommandHandler(Db, Confirmacion()).Handle(new DispatchTransferCommand(despacho), default);

    /// <summary>Guarda y despacha; falla la prueba si el borrador no se guarda.</summary>
    public async Task<(Guid Despacho, Result<ConfirmationResultDto> Resultado)> TrasladoAsync(Warehouse origen, Warehouse destino, params SaveInventoryDraftLine[] lineas)
    {
        var guardado = await GuardarDespachoAsync(Despacho(origen, destino, null, lineas));
        if (guardado.IsFailure) throw new InvalidOperationException($"{guardado.Error.Code}: {guardado.Error.Message}");
        return (guardado.Value.PublicId, await DespacharAsync(guardado.Value.PublicId));
    }

    /// <summary>Un despacho ya confirmado de <paramref name="cantidad"/> de P1 de PRIN a PV2 (con 20 × 1.000 en PRIN).</summary>
    public async Task<Guid> DespachoConfirmadoAsync(decimal cantidad = 10m, Guid? producto = null)
    {
        var p = producto ?? K.P1;
        await K.EntradaAsync(p, 20m, 1_000m);
        var (despacho, r) = await TrasladoAsync(PRIN, PV2, K.Linea(p, cantidad));
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return despacho;
    }

    public ReceiveTransferCommandHandler Recibir() =>
        new(Db, K.Actor, K.Alcance, K.C.Reloj, K.Maestros(), K.Vista(), Confirmacion());

    public Task<Result<ReceiveTransferResultDto>> RecibirAsync(Guid despacho, DateOnly? fecha = null, params LineaRecibidaRequest[] lineas) =>
        Recibir().Handle(new ReceiveTransferCommand(despacho, lineas, fecha), default);

    /// <summary>La línea de lo recibido de la primera línea del despacho.</summary>
    public LineaRecibidaRequest Recibida(Guid despacho, decimal cantidad, decimal? sobrante = null, int linea = 1, Guid? ubicacion = null)
    {
        var id = Db.InventoryDocuments.Include(d => d.Lines).Single(d => d.PublicId == despacho).Lines.Single(l => l.LineNumber == linea).PublicId;
        return new LineaRecibidaRequest(id, cantidad, null, ubicacion, sobrante);
    }

    public ResolveTransferDiscrepancyCommandHandler Resolver() =>
        new(Db, K.Actor, K.Alcance, K.C.Reloj, Motor, Cierre());

    public Task<Result<ResolveTransferDiscrepancyResultDto>> ResolverAsync(Guid diferencia, TransferDiscrepancyResolution resolucion,
        decimal? cantidad = null, Guid? causa = null, string motivo = "reclamación al transportador") =>
        Resolver().Handle(new ResolveTransferDiscrepancyCommand(diferencia, resolucion, motivo, cantidad, causa), default);

    /// <summary>Decide la solicitud pendiente de la diferencia como <paramref name="usuario"/>.</summary>
    public async Task<Result<DecisionResultDto>> DecidirAsync(Guid diferencia, ApprovalDecisionKind decision, int usuario = Aprobador, string? motivo = null)
    {
        var solicitud = await Db.ApprovalRequests.AsNoTracking()
            .SingleAsync(r => r.SourcePublicId == diferencia && r.Status == ApprovalRequestStatus.Pending);
        ComoUsuario(usuario);
        return await MotorReal!.DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, decision, motivo, solicitud.ContentSha256), default);
    }

    public TransferDiscrepancy Diferencia(Guid publicId) => Db.TransferDiscrepancies.AsNoTracking().Single(d => d.PublicId == publicId);

    public InventoryDocument Documento(Guid publicId) => Db.InventoryDocuments.Include(d => d.Lines).AsNoTracking().Single(d => d.PublicId == publicId);

    /// <summary>La existencia física de un producto en una bodega (0 si no hay fila).</summary>
    public decimal Fisico(Guid producto, Warehouse bodega)
    {
        var id = K.ProductoId(producto);
        return Db.StockBalances.AsNoTracking().Where(s => s.ProductId == id && s.WarehouseId == bodega.Id).Select(s => s.Physical).FirstOrDefault();
    }

    /// <summary>El valor total del producto en todos sus ámbitos de costo.</summary>
    public decimal ValorTotal(Guid producto)
    {
        var id = K.ProductoId(producto);
        return Db.CostStates.AsNoTracking().Where(c => c.ProductId == id).Sum(c => c.Value);
    }

    public string? Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : null;
}
