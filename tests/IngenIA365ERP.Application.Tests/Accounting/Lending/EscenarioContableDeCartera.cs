using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// El escenario contable de los casos dorados del constructor de Cartera (feature 013, T0612; molde <c>EscenarioContable</c> de la 012): el de
/// <see cref="ContabilidadTestData"/> más lo que dice <c>Casos/escenario.json</c> —cuentas habilitadas para CAR, la matriz vigente desde el
/// 1 de enero con los roles de T52, las sucursales y las personas de los sobres—, los tipos de comprobante y de cruce de las semillas, el
/// mapeo de tipos de comprobante de <see cref="LendingVoucherMappingsSeeder"/> (contracts/contabilidad.md §5) y los meses abiertos de los
/// casos (noviembre y diciembre de 2026, marzo de 2027). Ayudantes para leer un caso como lo entrega la bandeja
/// (<see cref="Unidad"/>: <see cref="MensajeEntrante"/> con su contenido tipado <c>…V1</c> y la clase de origen como texto, T10) y para
/// armar el comprobante esperado (<see cref="ComprobanteEsperado"/>), que debe pasar las reglas 1 a 11 de la 009.
///
/// <para>
/// Formato de un caso: <c>sobre</c> (el común de la unidad), <c>mensajes[]</c> (cada uno con <c>messageId</c>, <c>type</c>, <c>kind</c>,
/// <c>originDocumentClass</c>, <c>relatedDocumentClass</c> opcional y <c>payload</c>, que pisan al sobre), <c>matrizExtra</c> opcional y
/// <c>esperado</c>: <c>{ tipo, fecha, origen, lineas[] }</c> o <c>{ sinComprobante: Informational | ZeroValue | SameAccounts }</c>. Cada
/// línea esperada trae <c>rol</c>, <c>cuenta</c>, <c>debito</c>, <c>credito</c>, <c>tercero</c> (alias de persona o nulo), <c>cruce</c>
/// (<c>"PG 000123"</c>, <c>"FV PV01-1532"</c> o nulo), <c>sucursal</c> (alias), <c>centro</c> (si lleva el centro de costo de la regla) y
/// <c>mapa { hechos[], concepto, creditos[] }</c>.
/// </para>
/// </summary>
public sealed class EscenarioContableDeCartera
{
    public static string Carpeta => Path.Combine(AppContext.BaseDirectory, "Accounting", "Lending", "Casos");

    private const AccountingModules Car = AccountingModules.Accounting | AccountingModules.Lending;

    public ContabilidadTestData D { get; } = new();
    public IDimensionesDeCartera Dimensiones { get; } = Substitute.For<IDimensionesDeCartera>();
    public Dictionary<string, ChartOfAccount> Cuentas { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Person> Personas { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Branch> Sucursales { get; } = new(StringComparer.Ordinal);
    public IReadOnlyList<LendingPostingRule> Matriz => D.Db.LendingPostingRules.ToList();

    /// <summary>El actor del consumo: el proceso de integración (T0647; como en la 012).</summary>
    public IActorActual ActorActual { get; } = Substitute.For<IActorActual>();
    public IAuditAppendOnlyWriter Auditoria { get; } = Substitute.For<IAuditAppendOnlyWriter>();
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public AccountingAuditEmitter Emisor { get; }

    public EscenarioContableDeCartera()
    {
        ActorActual.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion("Mensaje:1"));
        Emisor = new AccountingAuditEmitter(Auditoria, D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);

        var escenario = JsonNode.Parse(File.ReadAllText(Path.Combine(Carpeta, "escenario.json")))!.AsObject();

        D.Principal.PublicId = Guid.Parse((string)escenario["sucursales"]!["principal"]!);
        D.Norte.PublicId = Guid.Parse((string)escenario["sucursales"]!["norte"]!);
        Sucursales["principal"] = D.Principal;
        Sucursales["norte"] = D.Norte;
        foreach (var p in escenario["personas"]!.AsArray())
        {
            var persona = new Person
            {
                PublicId = Guid.Parse((string)p!["publicId"]!), FirstName = (string)p["nombre"]!, LastName = "Prueba",
                TaxId = Guid.NewGuid().ToString("N")[..10], Status = "A", CreatedBy = "test",
            };
            D.Db.People.Add(persona);
            D.Db.SaveChanges();
            Personas[(string)p["alias"]!] = persona;
        }

        foreach (var c in escenario["cuentas"]!.AsArray())
        {
            var codigo = (string)c!["codigo"]!;
            var cuenta = D.Cuenta(codigo, Enum.Parse<AccountNature>((string)c["naturaleza"]!), Car,
                tercero: (bool?)c["tercero"] ?? false, cruce: (bool?)c["cruce"] ?? false, centro: (bool?)c["centro"] ?? false);
            cuenta.Name = (string)c["nombre"]!;
            Cuentas[codigo] = cuenta;
        }
        D.Db.SaveChanges();

        // Los tipos de comprobante de CAR y los documentos cruce de las semillas (PG con el número del crédito, FV con la factura).
        VoucherTypesSeeder.AplicarAsync(D.Db, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();
        var cruces = D.Db.CrossDocumentTypes.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cruce in CrossDocumentTypesSeeder.Catalogo().Where(x => !cruces.Contains(x.Code))) D.Db.CrossDocumentTypes.Add(cruce);
        D.Db.SaveChanges();
        LendingVoucherMappingsSeeder.AplicarAsync(D.Db, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();

        AbrirMeses(escenario["periodosAbiertos"]!.AsArray().Select(m => (string)m!).ToList());
        AgregarReglas(escenario["matriz"]!.AsArray());

        Dimensiones.CatalogoAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CatalogoDeDimensionesDeCarteraDto(
            [new("LIB01", "Libranza", "CONSUMO", "ConLibranza", true), new("COM01", "Comercio", "CONSUMO", "SinLibranza", true)],
            [new("CONSUMO", "Consumo", ["ConLibranza", "SinLibranza"])],
            RolesDeCuentaDeCartera.Conceptos, ["Receipts", "Payroll", "Test"], OperacionesDeCartera.Todas.Select(o => o.Codigo).ToList())));
        Dimensiones.CombinacionesEnUsoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CombinacionDeCarteraEnUsoDto>>([]));
    }

    public ChartOfAccount Cuenta(string codigo) => Cuentas[codigo];

    // ----------------------------------------------------------------------------------------------- consumidor --

    public ConsumoDeCartera Consumo() => new(D.Db, ActorActual, D.Clock, Emisor, Alertas);

    public PostLendingFactsCommandHandler PorUnidad() => new(D.Db, new MensajesEntrantes(D.Db), Consumo(), D.Poster,
        new ResolutorDeReglasDeCartera(D.Db), new TiposDeComprobanteDeCartera(D.Db), D.Clock);

    public PostLendingSummaryGroupCommandHandler Resumido() => new(D.Db, new MensajesEntrantes(D.Db), Consumo(), D.Poster,
        new ResolutorDeReglasDeCartera(D.Db), new TiposDeComprobanteDeCartera(D.Db), D.Clock);

    /// <summary>Los catálogos del constructor para estas unidades (matriz del rango de sus fechas, mapeos y personas).</summary>
    public Task<CatalogosDeCartera> CatalogosAsync(params IReadOnlyList<MensajeEntrante>[] unidades) =>
        CatalogosDeCartera.CargarAsync(D.Db, new ResolutorDeReglasDeCartera(D.Db), new TiposDeComprobanteDeCartera(D.Db),
            unidades.Select(u => (IReadOnlyList<HechoDeCartera>)u.Select(HechoDeCartera.De).ToList()), D.Clock.TodayUtc, default);

    /// <summary>
    /// Escribe el hecho en la bandeja con su entrega a Contabilidad, como lo habría dejado el emisor de Cartera: la clase de origen como texto
    /// (T10), el usuario de origen nulo si lo originó el proceso (T11), la clave de horario y el alcance del lote si se dan.
    /// </summary>
    public IntegrationMessage Emitir(MensajeEntrante m, DeliveryStatus estado = DeliveryStatus.Pending, IntegrationBatch? lote = null, string? horario = null,
        short? version = null, string? moneda = null, string? alcance = null)
    {
        var s = m.Envelope;
        var payload = Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(m.Payload!));
        var mensaje = new IntegrationMessage
        {
            PublicId = s.MessageId,
            Type = s.Type,
            Version = version ?? (short)s.Version,
            Kind = s.Kind,
            OriginModule = s.OriginModule,
            OriginKind = s.Origin.Kind,
            OriginPublicId = s.Origin.PublicId,
            OriginDocumentClass = m.OriginDocumentClassText,
            OriginNumber = s.Origin.Number,
            OriginEventKey = s.OriginEventKey,
            RelatedPublicId = s.Related?.PublicId,
            RelatedDocumentClass = m.RelatedDocumentClassText,
            RelatedNumber = s.Related?.Number,
            ChainRootPublicId = s.ChainRootPublicId,
            OperationDate = s.Origin.OperationDate,
            BranchPublicId = s.BranchPublicId,
            PersonPublicId = s.PersonPublicId,
            Currency = moneda ?? s.Currency,
            ExchangeRate = s.ExchangeRate,
            PayloadJson = payload,
            PayloadSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
            PrevalidationOutcome = PrevalidationOutcome.Postable,
            OriginActorKind = s.OriginUser.CentralUserId is null ? ActorKind.Process : ActorKind.Person,
            OriginUserCentralId = s.OriginUser.CentralUserId,
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
            BatchScopeKey = alcance,
            BatchId = lote?.Id,
            CreatedBy = "test",
        });
        D.Db.SaveChanges();
        return mensaje;
    }

    public IReadOnlyList<IntegrationMessage> Emitir(IReadOnlyList<MensajeEntrante> unidad, DeliveryStatus estado = DeliveryStatus.Pending,
        IntegrationBatch? lote = null, string? horario = null) => unidad.Select(m => Emitir(m, estado, lote, horario)).ToList();

    public IntegrationBatch Lote(long numero = 1, BatchTrigger disparador = BatchTrigger.LendingAccrualRun)
    {
        var lote = new IntegrationBatch
        {
            Number = numero, Destination = IntegrationDestinations.Accounting, Trigger = disparador, RequestedByKind = ActorKind.Process,
            RequestedByName = Actor.NombreDelProceso, RequestedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        lote.Iniciar(ContabilidadTestData.Ahora);
        D.Db.IntegrationBatches.Add(lote);
        D.Db.SaveChanges();
        return lote;
    }

    /// <summary>La <c>ScheduleKey</c> de Cartera de un hecho (C06): <c>CAR.{corto}|{disparador}||{granularidad}</c>.</summary>
    public static string Horario(string tipo, string granularidad = ClavesDeLote.GranularidadPorDia, string disparador = ClavesDeLote.FinDeCausacion) =>
        ClavesDeLote.Horario(ClavesDeLote.PrefijoDeCartera + OperacionesDeCartera.Buscar(tipo)!.Corto, disparador, null, granularidad);

    /// <summary>Una unidad con otros <c>messageId</c>, otro origen y, si se da, otra fecha contable (para armar varias partes de un día).</summary>
    public static IReadOnlyList<MensajeEntrante> Copia(IReadOnlyList<MensajeEntrante> unidad, Guid? origen = null, DateOnly? fecha = null)
    {
        var o = origen ?? Guid.NewGuid();
        return unidad.Select(m => m with
        {
            Envelope = m.Envelope with
            {
                MessageId = Guid.NewGuid(),
                Origin = m.Envelope.Origin with { PublicId = o, OperationDate = fecha ?? m.Envelope.Origin.OperationDate },
            },
        }).ToList();
    }

    /// <summary>La unidad de un caso por su nombre (<c>Casos/{nombre}.json</c>).</summary>
    public static IReadOnlyList<MensajeEntrante> UnidadDe(string nombre) => Unidad(Caso(nombre));

    /// <summary>Abre los meses <c>aaaa-mm</c> (creando el ejercicio si falta).</summary>
    private void AbrirMeses(IReadOnlyList<string> meses)
    {
        foreach (var texto in meses)
        {
            var mes = DateOnly.ParseExact(texto + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var ejercicio = D.Db.FiscalYears.SingleOrDefault(f => f.Year == mes.Year);
            if (ejercicio is null)
            {
                ejercicio = new FiscalYear { Year = mes.Year, CreatedBy = "test" };
                D.Db.FiscalYears.Add(ejercicio);
                D.Db.SaveChanges();
            }
            var periodo = D.Db.AccountingPeriods.SingleOrDefault(p => p.FiscalYearId == ejercicio.Id && p.Month == mes.Month);
            if (periodo is null)
                D.Db.AccountingPeriods.Add(new AccountingPeriod
                {
                    FiscalYearId = ejercicio.Id, Month = (byte)mes.Month, StartDate = mes, EndDate = mes.AddMonths(1).AddDays(-1),
                    Status = PeriodStatus.Open, CreatedBy = "test",
                });
            else periodo.Status = PeriodStatus.Open;
            D.Db.SaveChanges();
        }
    }

    /// <summary>Reglas en el formato de <c>escenario.json</c> (<c>rol</c>, <c>cuenta</c>, <c>op</c> y las dimensiones que fijen; <c>centro: true</c> = el centro de prueba).</summary>
    public void AgregarReglas(JsonArray reglas)
    {
        foreach (var r in reglas)
        {
            var desde = (string?)r!["desde"] is { } d ? DateOnly.Parse(d, CultureInfo.InvariantCulture) : MatrizDePruebaDeCartera.Enero1;
            var hasta = (string?)r["hasta"] is { } h ? DateOnly.Parse(h, CultureInfo.InvariantCulture) : (DateOnly?)null;
            D.Db.LendingPostingRules.Add(new LendingPostingRule((string?)r["op"] ?? LendingPostingRule.Cualquiera, (string)r["rol"]!, Cuentas[(string)r["cuenta"]!].Id,
                desde, (string?)r["linea"], (string?)r["modalidad"], (string?)r["subsegmento"], (string?)r["categoria"], (string?)r["concepto"],
                (bool?)r["empleado"], (bool?)r["gracia"], (bool?)r["porCuenta"], (bool?)r["reestructurado"], (string?)r["clase"], (string?)r["motivo"],
                (string?)r["sucursal"] is { } s ? Sucursales[s].Id : null, (bool?)r["centro"] == true ? D.Centro.Id : null, hasta, "escenario")
            { CreatedBy = "test" });
        }
        D.Db.SaveChanges();
    }

    // ----------------------------------------------------------------------------------------------------- casos --

    public static IEnumerable<object[]> NombresDeCasos() => Directory.GetFiles(Carpeta, "*.json")
        .Select(Path.GetFileNameWithoutExtension)
        .Where(n => n != "escenario")
        .Order(StringComparer.Ordinal)
        .Select(n => new object[] { n! });

    public static JsonObject Caso(string nombre) => JsonNode.Parse(File.ReadAllText(Path.Combine(Carpeta, nombre + ".json")))!.AsObject();

    /// <summary>Los campos de un mensaje del caso que no son del sobre <see cref="IntegrationEnvelopeV1"/>: la clase como texto (T10).</summary>
    private static readonly string[] SoloDelCaso = ["originDocumentClass", "relatedDocumentClass"];

    /// <summary>
    /// La unidad de un caso como la entrega <c>IMensajesEntrantes</c>: cada mensaje es el sobre común más lo suyo, con el contenido tipado a su
    /// <c>…V1</c> de <see cref="CatalogoDeMensajesDeCarteraV1"/>, los bytes y su hash, la entrega a Contabilidad y la clase de origen como texto.
    /// </summary>
    public static IReadOnlyList<MensajeEntrante> Unidad(JsonObject caso)
    {
        var unidad = new List<MensajeEntrante>();
        foreach (var m in caso["mensajes"]!.AsArray())
        {
            var sobre = caso["sobre"]!.DeepClone().AsObject();
            foreach (var (clave, valor) in m!.AsObject())
                if (!SoloDelCaso.Contains(clave)) sobre[clave] = valor?.DeepClone();
            var envoltorio = JsonSerializer.Deserialize<IntegrationEnvelopeV1>(sobre.ToJsonString(), OpcionesDeMensajes.Opciones)!;
            var registro = TipoDe(envoltorio.Type);
            var payloadJson = m["payload"]!.ToJsonString();
            var contenido = JsonSerializer.Deserialize(payloadJson, registro.Record, OpcionesDeMensajes.Opciones)!;
            var bytes = OpcionesDeMensajes.Serializar(contenido);
            unidad.Add(new MensajeEntrante(envoltorio with { Payload = contenido }, contenido, Encoding.UTF8.GetString(bytes),
                Convert.ToHexStringLower(SHA256.HashData(bytes)), PrevalidationOutcome.Postable,
                new EntregaEntrante(IntegrationDestinations.Accounting, DeliveryMode.Online, DeliveryStatus.Pending, null, null, null, 0, null))
            {
                OriginDocumentClassText = (string?)m["originDocumentClass"],
                RelatedDocumentClassText = (string?)m["relatedDocumentClass"],
            });
        }
        return unidad;
    }

    public static TipoDeMensaje TipoDe(string tipo) =>
        CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.SingleOrDefault(t => t.Type == tipo)
        ?? throw new InvalidOperationException($"«{tipo}» no es un hecho de Cartera a Contabilidad.");

    /// <summary>
    /// Las propiedades del JSON que el record no tiene (rutas con puntos): la deserialización las ignora en silencio, así que un error de
    /// escritura en un caso dejaría un dato sin llegar al constructor.
    /// </summary>
    public static IEnumerable<string> PropiedadesDesconocidas(JsonNode? nodo, Type tipo, string ruta = "payload")
    {
        if (nodo is null) yield break;
        var subyacente = Nullable.GetUnderlyingType(tipo) ?? tipo;
        if (nodo is JsonArray arreglo)
        {
            var elemento = subyacente.IsGenericType ? subyacente.GetGenericArguments()[0] : typeof(object);
            for (var i = 0; i < arreglo.Count; i++)
                foreach (var x in PropiedadesDesconocidas(arreglo[i], elemento, $"{ruta}[{i}]")) yield return x;
            yield break;
        }
        if (nodo is not JsonObject objeto || subyacente == typeof(object) || subyacente.Namespace?.StartsWith("System", StringComparison.Ordinal) == true) yield break;
        var propiedades = subyacente.GetProperties().ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), StringComparer.Ordinal);
        foreach (var (clave, valor) in objeto)
        {
            if (!propiedades.TryGetValue(clave, out var propiedad)) yield return $"{ruta}.{clave}";
            else foreach (var x in PropiedadesDesconocidas(valor, propiedad.PropertyType, $"{ruta}.{clave}")) yield return x;
        }
    }

    // --------------------------------------------------------------------------------------------- comprobante --

    /// <summary>
    /// El comprobante que el caso espera, como <see cref="PostingRequest"/> de la 009: tipo, fecha, origen <c>("CAR", esperado.origen,
    /// origin.publicId del principal)</c>, <c>Kind = Regular</c>, y cada línea con cuenta, lado, importe, tercero, cruce, sucursal y centro.
    /// </summary>
    public PostingRequest ComprobanteEsperado(JsonObject caso)
    {
        var esperado = caso["esperado"]!.AsObject();
        var unidad = Unidad(caso);
        var lineas = esperado["lineas"]!.AsArray().Select(l => Linea(l!.AsObject())).ToList();
        return new PostingRequest((string)esperado["tipo"]!, DateOnly.Parse((string)esperado["fecha"]!, CultureInfo.InvariantCulture),
            $"Caso dorado {unidad[0].Envelope.Origin.Number}",
            new AccountingOrigin(ModuloContable.Cartera, (string)esperado["origen"]!, unidad[0].Envelope.Origin.PublicId),
            lineas, DocumentKind.Regular);
    }

    public PostingLine Linea(JsonObject x)
    {
        var cruce = ((string?)x["cruce"])?.Split(' ', 2);
        var debito = (decimal)x["debito"]!;
        var credito = (decimal)x["credito"]!;
        return new PostingLine
        {
            AccountCode = (string)x["cuenta"]!,
            Debit = debito,
            Credit = credito,
            Detail = (string?)x["rol"],
            PersonId = (string?)x["tercero"] is { } tercero ? Personas[tercero].Id : null,
            BranchId = Sucursales[(string?)x["sucursal"] ?? "principal"].Id,
            CostCenterId = (bool?)x["centro"] == true ? D.Centro.Id : null,
            CrossDocumentType = cruce?[0],
            CrossDocumentNumber = cruce?[1],
        };
    }
}
