using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Kardex;

/// <summary>
/// La cooperativa de prueba del kardex (feature 012, US2): el catálogo de <see cref="CatalogoDePrueba"/> con una sucursal, dos
/// bodegas activas (PRIN y B2, cada una con su ubicación <c>GENERAL</c> por defecto) y la de tránsito, los tipos de ajuste y de
/// anulación con su consecutivo, un centro de costo, y el ciclo común real —guardar, confirmar y anular— con las estrategias
/// de ajuste reales (<see cref="EfectoDeAjustePositivo"/>, <see cref="EfectoDeSalidaPorAjuste"/>), <see cref="RegistroDeKardex"/>,
/// <see cref="ReversionDeKardex"/>, <see cref="EmisionDeInventario"/>, el numerador y el emisor. Sustituidos: el actor, los
/// permisos, el alcance, el motor de aprobaciones (sin niveles salvo que se pida) y el cerrojo (lo transaccional es de la e2e
/// <c>ConcurrenciaDeExistenciasTests</c>).
/// </summary>
public sealed class KardexDePrueba
{
    public const int Usuario = 7;

    public CatalogoDePrueba C { get; }
    public IActorActual Actor { get; } = Substitute.For<IActorActual>();
    public IPermissionChecker Permisos { get; } = Substitute.For<IPermissionChecker>();
    public IAlcanceDeInventario Alcance { get; } = Substitute.For<IAlcanceDeInventario>();
    public IMotorDeAprobaciones Motor { get; } = Substitute.For<IMotorDeAprobaciones>();
    public ICerrojoDeInventario Cerrojo { get; } = Substitute.For<ICerrojoDeInventario>();
    public List<PedidoDeCerrojo> Bloqueos { get; } = [];

    public Branch Sucursal { get; private set; } = null!;
    public Warehouse Principal { get; private set; } = null!;
    public Warehouse Segunda { get; private set; } = null!;
    public Warehouse Transito { get; private set; } = null!;
    public CostCenter Centro { get; private set; } = null!;
    public Guid P1 { get; private set; }
    public Guid P2 { get; private set; }

    private KardexDePrueba(CatalogoDePrueba c)
    {
        C = c;
        Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, Usuario, Guid.NewGuid(), Guid.NewGuid(),
            "bodeguero@coop.co", "bodeguero@coop.co", ExecutionChannel.Web, "POST /api/inventory/adjustments", "10.0.0.1", null));
        Permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        Cerrojo.When(x => x.BloquearAsync(Arg.Any<PedidoDeCerrojo>(), Arg.Any<CancellationToken>())).Do(x => Bloqueos.Add(x.Arg<PedidoDeCerrojo>()));
        SinNiveles();
    }

    public static async Task<KardexDePrueba> CrearAsync()
    {
        var k = new KardexDePrueba(await CatalogoDePrueba.CrearAsync());
        var db = k.C.Db;

        k.Sucursal = new Branch { Name = "Principal" };
        k.Centro = new CostCenter { Name = "Administración" };
        db.AddRange(k.Sucursal, k.Centro);
        await db.SaveChangesAsync();

        var operativa = db.WarehouseTypes.First(t => t.Behavior == WarehouseBehavior.Operational).Id;
        var deTransito = db.WarehouseTypes.First(t => t.Behavior == WarehouseBehavior.Transit).Id;
        k.Principal = Bodega("PRIN", "Principal", operativa, k.Sucursal.Id, WarehouseBehavior.Operational);
        k.Segunda = Bodega("B2", "Segunda", operativa, k.Sucursal.Id, WarehouseBehavior.Operational);
        k.Transito = Bodega("TR01", "Tránsito", deTransito, k.Sucursal.Id, WarehouseBehavior.Transit);
        db.Warehouses.AddRange(k.Principal, k.Segunda, k.Transito);

        foreach (var (codigo, clase) in new[]
                 {
                     ("AJP", DocumentClass.PositiveAdjustment), ("AJN", DocumentClass.NegativeAdjustment),
                     ("CI", DocumentClass.InternalConsumption), ("BAJ", DocumentClass.WriteOff), ("ENS", DocumentClass.Assembly),
                     ("ANU", DocumentClass.Voiding),
                 })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
        }
        await db.SaveChangesAsync();

        k.P1 = (await k.C.ProductoAsync(k.C.Alta("P1", "Arroz Diana 500 g"))).PublicId;
        k.P2 = (await k.C.ProductoAsync(k.C.Alta("P2", "Frijol"))).PublicId;
        return k;
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

    public LectorDeParametros Lector() => new(C.Db);

    public RegistroDeKardex Registro() => new(C.Db, Lector(), C.Reloj);

    public IMaestrosDelDocumento Maestros() => new MaestrosDelDocumentoEnBase(C.Db);

    public VistaDeDocumentos Vista() => new(C.Db, Maestros(), Permisos, Alcance);

    public EfectosDeClase Efectos()
    {
        var registro = Registro();
        var reversion = new ReversionDeKardex(C.Db, registro);
        var emision = new EmisionDeInventario(C.Db);
        var maestros = Maestros();
        return new EfectosDeClase(
        [
            new EfectoDeAjustePositivo(registro, reversion, emision, maestros, Permisos, C.Db),
            new EfectoDeAjusteNegativo(registro, reversion, emision, maestros, Permisos, C.Db),
            new EfectoDeConsumoInterno(registro, reversion, emision, maestros, Permisos, C.Db),
            new EfectoDeBaja(registro, reversion, emision, maestros, Permisos, C.Db),
        ]);
    }

    /// <summary>El aviso de reposición que la confirmación llama después del kardex (US17, T953); nulo = sin aviso.</summary>
    public AvisoDeReposicionAlConfirmar? AvisoDeReposicion { get; set; }

    public ConfirmacionDeDocumento Confirmacion(EfectosDeClase? efectos = null) => new(
        C.Db, Maestros(), Actor, C.Reloj, efectos ?? Efectos(), Motor, Cerrojo, new Numerador(C.Db, Cerrojo),
        new EmisorDeMensajes(C.Db, Actor, C.Reloj), Lector(), Vista(), [], [], avisoDeReposicion: AvisoDeReposicion);

    public SaveInventoryDraftCommandHandler Guardar(EfectosDeClase? efectos = null) =>
        new(C.Db, Maestros(), Alcance, Actor, C.Reloj, efectos ?? Efectos(), Vista());

    public VoidInventoryDocumentCommandHandler Anular() => new(C.Db, Actor, C.Reloj, Vista(), Confirmacion());

    // ------------------------------------------------------------------------------------------ escenarios --

    public void SinNiveles() =>
        Motor.EvaluarAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(_ =>
            Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], null, false, false), null)));

    /// <summary>Una vigencia de <c>Existencias.StockNegativoPermitido</c> (general o por bodega) desde el 1 de enero.</summary>
    public void Parametro(string clave, string valor, ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0)
    {
        C.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = clave, ScopeKind = ambito, ScopeId = ambitoId, Value = valor,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        C.Db.SaveChanges();
    }

    public InventoryDocumentType Tipo(string codigo) => C.Db.InventoryDocumentTypes.Single(t => t.Code == codigo);

    public Guid Causa(string codigo = "MERMA") => C.Db.AdjustmentCauses.Single(c => c.Code == codigo).PublicId;

    public int ProductoId(Guid publicId) => C.Producto(publicId).Id;

    public Guid Unidad(Guid producto) => C.Db.UnitsOfMeasure.Single(u => u.Id == C.Producto(producto).BaseUnitId).PublicId;

    /// <summary>Una línea del borrador en la unidad base del producto.</summary>
    public SaveInventoryDraftLine Linea(Guid producto, decimal cantidad, decimal? costo = null) =>
        new(null, producto, Unidad(producto), cantidad, UnitCost: costo);

    public SaveInventoryDraftRequest Borrador(string tipo, Warehouse? bodega = null, Guid? causa = null, Guid? centro = null,
        DateOnly? fecha = null, params SaveInventoryDraftLine[] lineas) =>
        new(Tipo(tipo).PublicId, fecha, (bodega ?? Principal).PublicId, null, centro, null, null, null, causa, null, null, null, null, lineas);

    public async Task<Result<InventoryDocumentDto>> GuardarAsync(SaveInventoryDraftRequest borrador) =>
        await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, borrador), default);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento, EfectosDeClase? efectos = null) =>
        await Confirmacion(efectos).ConfirmarAsync(new PedidoDeConfirmacion(documento, DocumentClassGroup.Adjustments), default);

    /// <summary>Guarda y confirma; falla la prueba si el borrador no se guarda.</summary>
    public async Task<(Guid Documento, Result<ConfirmationResultDto> Confirmacion)> AjusteAsync(SaveInventoryDraftRequest borrador)
    {
        var guardado = await GuardarAsync(borrador);
        if (guardado.IsFailure) throw new InvalidOperationException($"{guardado.Error.Code}: {guardado.Error.Message}");
        return (guardado.Value.PublicId, await ConfirmarAsync(guardado.Value.PublicId));
    }

    /// <summary>Un ajuste positivo confirmado de <paramref name="cantidad"/> a <paramref name="costo"/>.</summary>
    public async Task<Guid> EntradaAsync(Guid producto, decimal cantidad, decimal costo, Warehouse? bodega = null)
    {
        var (documento, r) = await AjusteAsync(Borrador("AJP", bodega, lineas: [Linea(producto, cantidad, costo)]));
        if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        return documento;
    }
}
