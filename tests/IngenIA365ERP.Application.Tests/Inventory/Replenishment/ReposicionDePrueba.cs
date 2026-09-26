using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Projections;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Replenishment;

/// <summary>
/// La cooperativa de prueba de la reposición (feature 012, US17): la de <see cref="KardexDePrueba"/> (PRIN, B2 y el tránsito, P1 y
/// P2, el ciclo común real) con los tipos de alerta sembrados y el <see cref="Alertas"/> <b>real</b> —así se ve la ocurrencia que
/// suma una alerta pendiente—, destinatarios falsos y un <see cref="ISender"/> que lleva <see cref="RaiseAlertCommand"/> a su
/// handler real. La confirmación trae el <see cref="AvisoDeReposicionAlConfirmar"/>.
/// </summary>
public sealed class ReposicionDePrueba
{
    public KardexDePrueba K { get; }
    public IDestinatariosPorPermiso Destinatarios { get; } = Substitute.For<IDestinatariosPorPermiso>();
    public ISender Sender { get; } = Substitute.For<ISender>();
    public List<RaiseAlertCommand> Enviadas { get; } = [];

    private ReposicionDePrueba(KardexDePrueba k)
    {
        K = k;
        Destinatarios.ResolverAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new DestinatariosDeAlerta([new DestinatarioDeAlerta(7, Guid.NewGuid(), "Compras", "compras@coop.test")], SinDestinatario: false));
        Sender.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        Sender.Send(Arg.Any<RaiseAlertCommand>(), Arg.Any<CancellationToken>()).Returns(async c =>
        {
            var comando = c.Arg<RaiseAlertCommand>();
            Enviadas.Add(comando);
            return await new RaiseAlertCommandHandler(Alertas()).Handle(comando, c.Arg<CancellationToken>());
        });
        k.AvisoDeReposicion = Aviso();
    }

    public static async Task<ReposicionDePrueba> CrearAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        await Persistence.Seeding.Parametric.AlertTypesSeeder.AplicarAsync(k.C.Db, CancellationToken.None);
        return new ReposicionDePrueba(k);
    }

    public Alertas Alertas() => new(K.C.Db, Destinatarios, K.Actor, K.C.Reloj, Sender, NullLogger<Alertas>.Instance);

    public EvaluacionDeReposicion Evaluacion() => new(K.C.Db, new PosicionDeReposicion(K.C.Db));

    public AvisoDeReposicionAlConfirmar Aviso() => new(K.C.Db, Evaluacion(), Alertas());

    public RevisionDeReorden Revision() => new(K.C.Db, Evaluacion(), Sender);

    /// <summary>La política de <paramref name="producto"/> en <paramref name="bodega"/> (por defecto la de US17-2: 10 / 50 / 15).</summary>
    public ReorderPolicy Politica(Guid producto, Warehouse bodega, decimal minimo = 10m, decimal maximo = 50m, decimal punto = 15m)
    {
        var politica = new ReorderPolicy { ProductId = K.ProductoId(producto), WarehouseId = bodega.Id, MinimumQuantity = minimo, MaximumQuantity = maximo, ReorderPoint = punto };
        K.C.Db.ReorderPolicies.Add(politica);
        K.C.Db.SaveChanges();
        return politica;
    }

    /// <summary>Existencia puesta a mano en la proyección (sin kardex): basta para la posición de la revisión y la vista.</summary>
    public void Existencia(Guid producto, Warehouse bodega, decimal fisico)
    {
        var id = K.ProductoId(producto);
        var fila = K.C.Db.StockBalances.FirstOrDefault(s => s.ProductId == id && s.WarehouseId == bodega.Id);
        if (fila is null) K.C.Db.StockBalances.Add(new StockBalance { ProductId = id, WarehouseId = bodega.Id, Physical = fisico });
        else fila.Physical = fisico;
        K.C.Db.SaveChanges();
    }

    /// <summary>Un despacho confirmado hacia <paramref name="destino"/> por <paramref name="cantidad"/>, todavía sin recibir.</summary>
    public void EnTransito(Guid producto, Warehouse origen, Warehouse destino, decimal cantidad)
    {
        var db = K.C.Db;
        var tipo = db.InventoryDocumentTypes.FirstOrDefault(t => t.Code == "TRD");
        if (tipo is null)
        {
            tipo = new InventoryDocumentType { Code = "TRD", Name = "Despacho", Class = DocumentClass.TransferDispatch, IsActive = true };
            db.InventoryDocumentTypes.Add(tipo);
            db.SaveChanges();
        }
        var despacho = new InventoryDocument
        {
            Class = DocumentClass.TransferDispatch, DocumentTypeId = tipo.Id, OperationDate = Catalog.CatalogoDePrueba.Hoy, BranchId = K.Sucursal.Id,
            WarehouseId = origen.Id, DestinationWarehouseId = destino.Id, TransitWarehouseId = K.Transito.Id, Prefix = "TRD",
            Number = db.InventoryDocuments.Count(d => d.Class == DocumentClass.TransferDispatch) + 1,
        };
        despacho.Lines.Add(new InventoryDocumentLine { Document = despacho, LineNumber = 1, ProductId = K.ProductoId(producto), UnitId = 1, Quantity = cantidad, QuantityBase = cantidad });
        despacho.Confirmar(1, DateTime.UtcNow);
        db.InventoryDocuments.Add(despacho);
        db.SaveChanges();
    }

    public Task<List<Alert>> AlertasAsync() => K.C.Db.Alerts.OrderBy(a => a.Id).ToListAsync();
}
