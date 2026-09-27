using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// La bandeja de salida armada a mano para las pruebas de la plataforma de mensajería de I2 (feature 012, T461–T467): mensajes,
/// entregas y dependencias escritos directo en el contexto InMemory, sin pasar por el emisor, para poner cada entrega en el
/// estado que la prueba necesita. El reloj y el actor son falsos.
/// </summary>
public sealed class BandejaDePrueba
{
    public static readonly DateTime Ahora = new(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly Fecha = new(2026, 11, 14);
    public static readonly Guid Sucursal = Guid.NewGuid();

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public IActorActual ActorActual { get; } = Substitute.For<IActorActual>();

    public BandejaDePrueba()
    {
        FijarReloj(Ahora);
        ActorActual.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Persona());
    }

    /// <summary>El reloj en UTC; la hora local es −05:00 (el sustituto no ejecuta la implementación por defecto).</summary>
    public void FijarReloj(DateTime utc)
    {
        Reloj.UtcNow.Returns(utc);
        var local = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(-5));
        Reloj.AhoraLocal.Returns(local);
        Reloj.HoyLocal.Returns(DateOnly.FromDateTime(local.DateTime));
    }

    public static Actor Persona() => new(
        ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Laura Contadora", "laura@coop.test", ExecutionChannel.Web,
        "POST /api/inventory/messages/reprocess", "10.0.0.9", null);

    public static Actor Proceso(string origen = "Lote:1") => IngenIA365ERP.Application.Common.Execution.Actor.ProcesoDeIntegracion(origen);

    public IntegrationMessage Mensaje(
        Guid? origen = null,
        string tipo = "CompraRecibida",
        string clave = "Confirmation",
        DateOnly? fecha = null,
        string? tipoDeDocumento = "COMPRA",
        string numero = "CO-1",
        Guid? relacionado = null,
        string payload = "{}",
        IntegrationMessageKind kind = IntegrationMessageKind.Business,
        PrevalidationOutcome? validacion = PrevalidationOutcome.Postable,
        string? bodega = "B01")
    {
        var mensaje = new IntegrationMessage
        {
            Type = tipo,
            Version = 1,
            Kind = kind,
            OriginModule = "INV",
            OriginKind = MessageOriginKind.Document,
            OriginPublicId = origen ?? Guid.NewGuid(),
            OriginDocumentClass = "PurchaseReceipt",
            OriginDocumentTypeCode = tipoDeDocumento,
            OriginNumber = numero,
            OriginEventKey = clave,
            RelatedPublicId = relacionado,
            RelatedDocumentClass = relacionado is null ? null : "PurchaseReceipt",
            RelatedNumber = relacionado is null ? null : "CO-0",
            ChainRootPublicId = relacionado ?? origen ?? Guid.NewGuid(),
            OperationDate = fecha ?? Fecha,
            BranchPublicId = Sucursal,
            WarehouseCode = bodega,
            Currency = "COP",
            ExchangeRate = 1m,
            PayloadJson = payload,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
            PrevalidationOutcome = validacion,
            OriginUserCentralId = Guid.NewGuid(),
            OriginUserName = "Ana Compradora",
            EmittedAt = Ahora.AddMinutes(-30),
        };
        Db.IntegrationMessages.Add(mensaje);
        Db.SaveChanges();
        return mensaje;
    }

    public IntegrationMessageDelivery Entrega(
        IntegrationMessage mensaje,
        DeliveryStatus estado = DeliveryStatus.Pending,
        string destino = IntegrationDestinations.Accounting,
        DeliveryMode? modo = null,
        string? horario = null,
        IntegrationBatch? lote = null,
        DateTime? proximoIntento = null,
        int intentos = 0)
    {
        var entrega = new IntegrationMessageDelivery
        {
            MessageId = mensaje.Id,
            Destination = destino,
            Mode = modo ?? (estado switch
            {
                DeliveryStatus.InBatch => DeliveryMode.Batch,
                DeliveryStatus.NotApplicable => DeliveryMode.NotPosted,
                _ => DeliveryMode.Online,
            }),
            Status = estado,
            ScheduleKey = horario,
            BatchId = lote?.Id,
            Attempts = intentos,
            NextAttemptAt = proximoIntento ?? (estado == DeliveryStatus.Pending ? Ahora.AddMinutes(-1) : null),
        };
        Db.IntegrationMessageDeliveries.Add(entrega);
        Db.SaveChanges();
        return entrega;
    }

    public void Depende(IntegrationMessage mensaje, IntegrationMessage de)
    {
        Db.IntegrationMessageDependencies.Add(new IntegrationMessageDependency { MessageId = mensaje.Id, DependsOnMessageId = de.Id });
        Db.SaveChanges();
    }

    public IntegrationBatch Lote(BatchTrigger disparador = BatchTrigger.Manual, BatchStatus estado = BatchStatus.Requested, long numero = 1,
        string? horario = null, DateTime? franja = null, string destino = IntegrationDestinations.Accounting)
    {
        var lote = new IntegrationBatch
        {
            Number = numero,
            Destination = destino,
            Trigger = disparador,
            ScheduleKey = horario,
            ScheduledFor = franja,
            RequestedByKind = ActorKind.Process,
            RequestedByName = IngenIA365ERP.Application.Common.Execution.Actor.NombreDelProceso,
            RequestedAt = Ahora.AddHours(-1),
        };
        if (estado != BatchStatus.Requested) lote.Iniciar(Ahora.AddMinutes(-10));
        Db.IntegrationBatches.Add(lote);
        Db.SaveChanges();
        return lote;
    }

    /// <summary>Relee todo desde el almacén, como lo vería otra petición.</summary>
    public void Olvidar() => Db.ChangeTracker.Clear();
}

/// <summary>Un destino falso: acepta lo que se le diga y no consume nada.</summary>
public sealed class DestinoFalso(string destino, Func<string, int, bool>? acepta = null) : IDestinoDeMensajes
{
    public string Destino => destino;

    public bool Acepta(string type, int version) => acepta?.Invoke(type, version) ?? true;

    public Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ResultadoDeUnidad>>([]);

    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas) => [];
}
