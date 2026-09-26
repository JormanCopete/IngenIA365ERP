using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// El escenario contable de los casos dorados del constructor y de las pruebas del consumidor (feature 012, T449, T453–T457):
/// el de <see cref="ContabilidadTestData"/> más lo que dice <c>Casos/escenario.json</c> —cuentas habilitadas para INV, la
/// matriz vigente desde el 1 de enero, los tipos de comprobante de Inventario con sus mapeos, los tipos de cruce y las personas
/// de los sobres—, noviembre abierto (la venta de mensajes.md §14 es del 14 de noviembre) y ayudantes para armar unidades desde
/// JSON y escribirlas en la bandeja como si las hubiera emitido Inventario.
/// </summary>
public sealed class EscenarioContable
{
    public static readonly Guid Principal = Guid.Parse("b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f");
    public static readonly Guid Norte = Guid.Parse("c8e2f3a4-1b5c-4d6e-9f70-2b3c4d5e6f71");
    public static readonly Guid CentroDeCosto = Guid.Parse("d9f3a4b5-2c6d-4e7f-8a81-3c4d5e6f7a82");
    public static readonly Guid Asociado = Guid.Parse("c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
    public static readonly Guid Redeban = Guid.Parse("f0e1d2c3-b4a5-4968-8776-655443322110");
    public static readonly Guid Proveedor = Guid.Parse("11111111-2222-4333-8444-555555555555");
    public static readonly Guid Cajero = Guid.Parse("2a3b4c5d-6e7f-4a8b-9c0d-1e2f3a4b5c6d");

    public static string Carpeta => Path.Combine(AppContext.BaseDirectory, "Accounting", "Inventory", "Casos");

    public ContabilidadTestData D { get; } = new();
    public Dictionary<string, ChartOfAccount> Cuentas { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, Guid> PersonaPorId { get; } = [];
    public IActorActual ActorActual { get; } = Substitute.For<IActorActual>();
    public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();
    public IDimensionesDeInventario Dimensiones { get; } = Substitute.For<IDimensionesDeInventario>();
    public AccountingAuditEmitter Emisor { get; }

    public EscenarioContable()
    {
        ActorActual.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion("Mensaje:1"));
        Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
        var escenario = JsonNode.Parse(File.ReadAllText(Path.Combine(Carpeta, "escenario.json")))!.AsObject();

        D.Principal.PublicId = Principal;
        D.Norte.PublicId = Norte;
        D.Centro.PublicId = CentroDeCosto;
        foreach (var p in escenario["personas"]!.AsArray())
        {
            var persona = new Person { PublicId = Guid.Parse((string)p!["publicId"]!), FirstName = (string)p["nombre"]!, LastName = "Prueba", TaxId = Guid.NewGuid().ToString("N")[..10], Status = "A", CreatedBy = "test" };
            D.Db.People.Add(persona);
            D.Db.SaveChanges();
            PersonaPorId[persona.Id] = persona.PublicId;
        }
        PersonaPorId[D.Tercero.Id] = D.Tercero.PublicId;

        const AccountingModules modulos = AccountingModules.Accounting | AccountingModules.Inventory;
        foreach (var c in escenario["cuentas"]!.AsArray())
        {
            var codigo = (string)c!["codigo"]!;
            var cuenta = D.Cuenta(codigo,
                Enum.Parse<AccountNature>((string?)c["naturaleza"] ?? "Debit"), modulos,
                tercero: (bool?)c["tercero"] ?? false, cruce: (bool?)c["cruce"] ?? false, centro: (bool?)c["centro"] ?? false,
                baseGravable: (bool?)c["base"] ?? false, tarifa: (decimal?)c["tarifa"]);
            cuenta.Name = (string)c["nombre"]!;
            cuenta.TaxKind = Enum.Parse<TaxKind>((string?)c["impuesto"] ?? "None");
            Cuentas[codigo] = cuenta;
        }
        D.Db.SaveChanges();

        foreach (var t in escenario["tiposDeComprobante"]!.AsArray())
            D.Db.VoucherTypes.Add(new VoucherType { Code = (string)t!, Name = (string)t!, Usage = VoucherUsage.Module, ModuleCode = "INV", IsSeeded = true, CreatedBy = "test" });
        foreach (var t in escenario["tiposDeCruce"]!.AsArray())
            D.Db.CrossDocumentTypes.Add(new CrossDocumentType { Code = (string)t!, Name = (string)t!, IsSeeded = true, CreatedBy = "test" });
        D.Db.AccountingPeriods.Add(new AccountingPeriod
        {
            FiscalYearId = D.Db.FiscalYears.Single().Id, Month = 11, StartDate = new DateOnly(2026, 11, 1), EndDate = new DateOnly(2026, 11, 30),
            Status = PeriodStatus.Open, CreatedBy = "test",
        });
        D.Db.SaveChanges();

        var tipos = D.Db.VoucherTypes.ToDictionary(v => v.Code, v => v.Id);
        var cruces = D.Db.CrossDocumentTypes.ToDictionary(v => v.Code, v => v.Id);
        foreach (var m in escenario["mapeos"]!.AsArray())
        {
            var cruce = (string?)m!["cruce"];
            D.Db.InventoryVoucherMappings.Add(new InventoryVoucherMapping((string)m["operacion"]!, null, tipos[(string)m["tipo"]!],
                cruce is null ? null : cruces[cruce]) { CreatedBy = "system:seed" });
        }
        D.Db.SaveChanges();
        AgregarReglas(escenario["matriz"]!.AsArray());

        Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(CatalogoDeDimensionesDto.Vacio));
        Dimensiones.CombinacionesEnUsoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<CombinacionEnUsoDto>>([]));
    }

    public ChartOfAccount Cuenta(string codigo) => Cuentas[codigo];

    /// <summary>Reglas en el formato de <c>escenario.json</c> (<c>op</c>, <c>rol</c>, <c>cuenta</c> y las dimensiones que fijen).</summary>
    public void AgregarReglas(JsonArray reglas)
    {
        foreach (var r in reglas)
        {
            var desde = (string?)r!["desde"] is { } d ? DateOnly.Parse(d, CultureInfo.InvariantCulture) : MatrizDePrueba.Enero1;
            var hasta = (string?)r["hasta"] is { } h ? DateOnly.Parse(h, CultureInfo.InvariantCulture) : (DateOnly?)null;
            D.Db.InventoryPostingRules.Add(new InventoryPostingRule((string)r["op"]!, (string)r["rol"]!, Cuentas[(string)r["cuenta"]!].Id, desde,
                (string?)r["grupo"], (string?)r["bodega"], (string?)r["punto"], (string?)r["medio"], (string?)r["tarifa"], (decimal?)r["valorTarifa"],
                (string?)r["motivo"], null, null, hasta, "escenario") { CreatedBy = "test" });
        }
        D.Db.SaveChanges();
    }

    public InventoryPostingRule Regla(string op, string rol, string cuenta, string? grupo = null, string? bodega = null, string? medio = null, DateOnly? desde = null)
    {
        var regla = new InventoryPostingRule(op, rol, Cuentas[cuenta].Id, desde ?? MatrizDePrueba.Enero1, grupo, bodega, null, medio, null, null, null, null, null, null, "prueba") { CreatedBy = "test" };
        D.Db.InventoryPostingRules.Add(regla);
        D.Db.SaveChanges();
        return regla;
    }

    // ----------------------------------------------------------------------------------------------------- casos --

    public static IEnumerable<object[]> NombresDeCasos() => Directory.GetFiles(Carpeta, "*.json")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(n => n != "escenario")
        .Order(StringComparer.Ordinal)
        .Select(n => new object[] { n! });

    public static JsonObject Caso(string nombre) => JsonNode.Parse(File.ReadAllText(Path.Combine(Carpeta, nombre + ".json")))!.AsObject();

    /// <summary>La unidad de un caso: cada mensaje es el sobre común más su <c>messageId</c>, su <c>type</c> y su contenido.</summary>
    public static IReadOnlyList<MensajeDeUnidad> Unidad(JsonObject caso)
    {
        var unidad = new List<MensajeDeUnidad>();
        foreach (var m in caso["mensajes"]!.AsArray())
        {
            var sobre = caso["sobre"]!.DeepClone().AsObject();
            foreach (var (clave, valor) in m!.AsObject()) sobre[clave] = valor?.DeepClone();
            unidad.Add(DesdeJson(sobre.ToJsonString()));
        }
        return unidad;
    }

    /// <summary>Un sobre del contrato con su contenido tipado (como lo entrega <c>IMensajesEntrantes</c>).</summary>
    public static MensajeDeUnidad DesdeJson(string json)
    {
        var sobre = JsonSerializer.Deserialize<IntegrationEnvelopeV1>(json, OpcionesDeMensajes.Opciones)!;
        var registro = CatalogoDeMensajesV1.Todos.First(t => t.Type == sobre.Type && t.Version == sobre.Version);
        var contenido = ((JsonElement)sobre.Payload!).Deserialize(registro.Record, OpcionesDeMensajes.Opciones)!;
        return new MensajeDeUnidad(sobre with { Payload = contenido }, contenido);
    }

    public Task<CatalogosDelConstructor> CatalogosAsync(params IReadOnlyList<MensajeDeUnidad>[] unidades) =>
        CatalogosDelConstructor.CargarAsync(D.Db, new ResolutorDeReglas(D.Db), new TiposDeComprobanteDeInventario(D.Db), unidades, D.Clock.TodayUtc, default);

    // ---------------------------------------------------------------------------------------------------- bandeja --

    /// <summary>Escribe el mensaje en la bandeja con su entrega a Contabilidad, como lo habría dejado el emisor.</summary>
    public IntegrationMessage Emitir(MensajeDeUnidad m, DeliveryStatus estado = DeliveryStatus.Pending, IntegrationBatch? lote = null, string? horario = null,
        short? version = null, string? moneda = null)
    {
        var s = m.Sobre;
        var payload = Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(m.Contenido!));
        var mensaje = new IntegrationMessage
        {
            PublicId = s.MessageId,
            Type = s.Type,
            Version = version ?? (short)s.Version,
            Kind = s.Kind,
            OriginModule = s.OriginModule,
            OriginKind = s.Origin.Kind,
            OriginPublicId = s.Origin.PublicId,
            OriginDocumentClass = s.Origin.DocumentClass?.ToString(),
            OriginDocumentTypeCode = s.Origin.DocumentTypeCode,
            OriginNumber = s.Origin.Number,
            OriginEventKey = s.OriginEventKey,
            RelatedPublicId = s.Related?.PublicId,
            RelatedDocumentClass = s.Related?.DocumentClass.ToString(),
            RelatedNumber = s.Related?.Number,
            ChainRootPublicId = s.ChainRootPublicId,
            OperationDate = s.Origin.OperationDate,
            BranchPublicId = s.BranchPublicId,
            CostCenterPublicId = s.CostCenterPublicId,
            WarehouseCode = s.WarehouseCode,
            PersonPublicId = s.PersonPublicId,
            Currency = moneda ?? s.Currency,
            ExchangeRate = s.ExchangeRate,
            PayloadJson = payload,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
            PrevalidationOutcome = PrevalidationOutcome.Postable,
            OriginUserCentralId = s.OriginUser.CentralUserId ?? Guid.Empty,
            OriginUserName = s.OriginUser.Name,
            EmittedAt = ContabilidadTestData.Ahora,
            CreatedBy = "test",
        };
        D.Db.IntegrationMessages.Add(mensaje);
        D.Db.SaveChanges();
        D.Db.IntegrationMessageDeliveries.Add(new IntegrationMessageDelivery
        {
            MessageId = mensaje.Id,
            Destination = IntegrationDestinations.Accounting,
            Mode = estado == DeliveryStatus.InBatch ? DeliveryMode.Batch : DeliveryMode.Online,
            Status = estado,
            ScheduleKey = horario,
            BatchId = lote?.Id,
            CreatedBy = "test",
        });
        D.Db.SaveChanges();
        return mensaje;
    }

    public IReadOnlyList<IntegrationMessage> Emitir(IReadOnlyList<MensajeDeUnidad> unidad, DeliveryStatus estado = DeliveryStatus.Pending, IntegrationBatch? lote = null, string? horario = null) =>
        unidad.Select(m => Emitir(m, estado, lote, horario)).ToList();

    public IntegrationBatch Lote(long numero = 1)
    {
        var lote = new IntegrationBatch
        {
            Number = numero, Destination = IntegrationDestinations.Accounting, Trigger = BatchTrigger.Manual, RequestedByKind = ActorKind.Process,
            RequestedByName = Actor.NombreDelProceso, RequestedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        lote.Iniciar(ContabilidadTestData.Ahora);
        D.Db.IntegrationBatches.Add(lote);
        D.Db.SaveChanges();
        return lote;
    }

    /// <summary>Una <c>ScheduleKey</c> resumida o por documento del tipo de documento dado.</summary>
    public static string Horario(string tipo, bool resumido) => ClavesDeLote.Horario(tipo, ClavesDeLote.HoraDiaria, new TimeOnly(22, 0), resumido ? "Resumido" : "PorDocumento");

    // ------------------------------------------------------------------------------------ unidades de ejemplo --

    /// <summary>Un sobre mínimo de un documento de Inventario (sucursal principal).</summary>
    public static IntegrationEnvelopeV1 Sobre(string tipo, string clase = "PurchaseReceipt", string tipoDoc = "CO", string numero = "REC-1",
        DateOnly? fecha = null, Guid? persona = null, Guid? origen = null, DocumentRefV1? relacionado = null, IntegrationMessageKind kind = IntegrationMessageKind.Business) => new()
    {
        MessageId = Guid.NewGuid(),
        Type = tipo,
        Version = 1,
        Kind = kind,
        OriginEventKey = "Confirmation",
        Origin = new MessageOriginV1
        {
            Kind = MessageOriginKind.Document, DocumentClass = Enum.Parse<Domain.Enums.Inventory.DocumentClass>(clase), DocumentTypeCode = tipoDoc,
            Number = numero, PublicId = origen ?? Guid.NewGuid(), OperationDate = fecha ?? ContabilidadTestData.Marzo15,
        },
        Related = relacionado,
        ChainRootPublicId = Guid.NewGuid(),
        BranchPublicId = Principal,
        PersonPublicId = persona,
        OriginUser = new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Ana Compradora" },
        EmittedAt = ContabilidadTestData.Ahora,
    };

    /// <summary>Una compra de un renglón: inventario de <paramref name="grupo"/> contra mercancía por facturar del proveedor.</summary>
    public static IReadOnlyList<MensajeDeUnidad> Compra(decimal costo = 1000m, string grupo = "ABARROTES", DateOnly? fecha = null, Guid? persona = null,
        string numero = "REC-1", Guid? origen = null, DocumentRefV1? relacionado = null)
    {
        var contenido = new CompraRecibidaV1
        {
            Lines = [new CostLineV1 { AccountingGroupCode = grupo, WarehouseCode = "B01", Movement = Domain.Enums.Inventory.KardexEntryKind.Entry, QuantityBase = 1m, Cost = costo, DocumentLines = [1] }],
        };
        var sobre = Sobre(CompraRecibidaV1.Type, numero: numero, fecha: fecha, persona: persona ?? Proveedor, origen: origen, relacionado: relacionado);
        return [new MensajeDeUnidad(sobre with { Payload = contenido }, contenido)];
    }
}
