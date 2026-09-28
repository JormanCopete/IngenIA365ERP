using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Un canal con guion (feature 012, I4, T679): cada llamada a <c>EmitirAsync</c> o <c>ConsultarEstadoAsync</c> toma la siguiente respuesta de
/// su cola y queda anotada con la operación y la clave de idempotencia que recibió.
/// </summary>
public sealed class CanalGuionado(string codigo = "SIMULADO") : ICanalDeEmisionElectronica
{
    public string ChannelCode { get; } = codigo;

    public CapacidadesDelCanal Capacidades { get; } = CanalDePrueba.Completas();

    public Queue<ResultadoDeCanal> Emisiones { get; } = new();

    public Queue<ResultadoDeCanal> Consultas { get; } = new();

    public List<(string Operacion, string? Clave, string Numero)> Llamadas { get; } = [];

    public Task<ResultadoDeCanal> EmitirAsync(DocumentoElectronicoCanonico documento, ContextoDeCanal contexto, CancellationToken ct)
    {
        Llamadas.Add(("Emitir", contexto.IdempotencyKey, documento.Number.Full));
        return Task.FromResult(Emisiones.Dequeue());
    }

    public Task<ResultadoDeCanal> ConsultarEstadoAsync(ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct)
    {
        Llamadas.Add(("Consultar", contexto.IdempotencyKey, referencia.Numero));
        return Task.FromResult(Consultas.Dequeue());
    }

    public Task<ResultadoDeCanal> EmitirEventoAsync(EventoRadianCanonico evento, ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

    public Task<ResultadoDeCanal> DescargarArtefactoAsync(ReferenciaDeEnvio referencia, TipoDeArtefacto tipo, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<ResultadoDeCanal> ProbarAsync(ContextoDeCanal contexto, CancellationToken ct) => Task.FromResult(ResultadoDeCanal.Sin(ChannelOutcome.Validated));

    public Task<ResultadoDeCanal> ConsultarRangosAsync(ContextoDeCanal contexto, CancellationToken ct) => throw new NotSupportedException();

    public Task<ResultadoDeCanal> ConsultarAdquirenteAsync(string tipoDeIdentificacionDian, string numero, ContextoDeCanal contexto, CancellationToken ct) =>
        throw new NotSupportedException();

    /// <summary>Una respuesta del canal con código único, QR y los artefactos que se piden.</summary>
    public static ResultadoDeCanal Respuesta(ChannelOutcome resultado, bool codigo = true, bool respuestaDeValidacion = true, string? tipoDian = null,
        params MensajeDelCanal[] mensajes)
    {
        var artefactos = new List<ArtefactoDelCanal> { new(TipoDeArtefacto.XmlFirmado, "firmado.xml", "application/xml", [1, 2, 3]) };
        if (respuestaDeValidacion) artefactos.Add(new(TipoDeArtefacto.ApplicationResponse, "respuesta.xml", "application/xml", [4, 5]));
        artefactos.Add(new(TipoDeArtefacto.AttachedDocument, "ad.zip", "application/zip", [6, 7]));
        return new ResultadoDeCanal(resultado, codigo ? new string('a', 96) : null, codigo ? "CUFE" : null, codigo ? "https://qr/simulado" : null,
            null, tipoDian, mensajes, artefactos, "track-1", "P-1", "00", 12);
    }
}

/// <summary>El arrendamiento por fila sobre la base en memoria, con la misma condición que el <c>UPDATE</c> de contracts/dian.md §6.4.</summary>
public sealed class ArrendamientoEnMemoria(TestApplicationDbContext db) : IArrendamientoDeDocumentoElectronico
{
    public async Task<bool> TomarAsync(int documentoId, DateTime ahora, DateTime hasta, string dueno, bool respetarEspera, CancellationToken ct)
    {
        var d = await db.ElectronicDocuments.FirstAsync(x => x.Id == documentoId, ct);
        if (d.LeaseUntil is { } l && l >= ahora) return false;
        if (respetarEspera && d.NextAttemptAt is { } n && n > ahora) return false;
        d.LeaseUntil = hasta;
        d.LeaseOwner = dueno;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>Devuelve siempre el canónico con que se registró (o el que se le fije para probar la discrepancia).</summary>
public sealed class ReconstruccionFija : IReconstruccionDelCanonico
{
    public Dictionary<string, CanonicoConstruido> PorNumero { get; } = [];

    /// <summary>El último registrado; si se le fija otro, lo devuelve para todos (la discrepancia).</summary>
    public CanonicoConstruido? Canonico
    {
        get => _ultimo;
        set { _ultimo = value; Forzado = value; }
    }

    private CanonicoConstruido? _ultimo;

    private CanonicoConstruido? Forzado { get; set; }

    public void Registrar(CanonicoConstruido canonico)
    {
        PorNumero[canonico.Documento.Number.Full] = canonico;
        _ultimo = canonico;
    }

    public Task<Result<CanonicoConstruido>> ReconstruirAsync(ElectronicDocument documento, Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion version,
        CancellationToken ct) => Task.FromResult(Result.Success(Forzado ?? PorNumero[documento.Number]));
}

/// <summary>
/// El escenario de la emisión (T679 y las pruebas de la sección «emisión, estado, artefactos y entrega»): la cooperativa de
/// <see cref="EmisionDePrueba"/> con una configuración del canal guionado, una resolución, un reloj que se puede adelantar, el envío
/// de adjuntos y las alertas sustituidos, y atajos para registrar documentos y pedir intentos.
/// </summary>
public sealed class EscenarioDeEmision
{
    public EmisionDePrueba E { get; }

    public CanalGuionado Canal { get; } = new();

    public DateTime Ahora { get; set; } = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    public ISender Sender { get; } = Substitute.For<ISender>();

    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();

    public IRegistroDeFallasDelCanal Fallas { get; } = Substitute.For<IRegistroDeFallasDelCanal>();

    public IActorActual Actor { get; } = Substitute.For<IActorActual>();

    public ReconstruccionFija Reconstruccion { get; } = new();

    public List<UploadAttachmentCommand> Subidas { get; } = [];

    public ElectronicEmissionSetting Configuracion { get; }

    public DianNumberingResolution Resolucion { get; }

    public EsperasDeReintento Esperas { get; } = new(
        [TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)], TimeSpan.FromHours(1), TimeSpan.FromMinutes(2));

    public EscenarioDeEmision()
    {
        E = new EmisionDePrueba();
        E.Reloj.UtcNow.Returns(_ => Ahora);
        E.Reloj.HoyLocal.Returns(_ => DateOnly.FromDateTime(Ahora.AddHours(-5)));
        Configuracion = E.Configuracion();
        Resolucion = E.Resolucion(ResolutionKind.Invoice, "SETP", desde: 990000000, hasta: 995000000);
        Sender.Send(Arg.Any<UploadAttachmentCommand>(), Arg.Any<CancellationToken>())
            .Returns(c => { Subidas.Add(c.Arg<UploadAttachmentCommand>()); return Result.Success(Guid.NewGuid()); });
        Alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(IngenIA365ERP.Application.Common.Execution.Actor.ProcesoDeIntegracion("Tarea:einvoicing.process"));
    }

    public ContingenciaDeLaDian Contingencias() => new(E.Db, E.Lector(), Alertas);

    public GuardadoDeArtefactos Artefactos() => new(Sender, Reconstruccion, Alertas, NullLogger<GuardadoDeArtefactos>.Instance);

    public IntentoAnteElCanal Intento() => new(
        E.Db, new RegistroDeCanales(Canal), E.Credenciales, E.Tenant, E.Reloj, Actor, new ArrendamientoEnMemoria(E.Db), Esperas, Artefactos(),
        Contingencias(), Alertas, NullLogger<IntentoAnteElCanal>.Instance, Fallas);

    public EmitElectronicDocumentCommandHandler Emitir() => new(Intento());

    public QueryElectronicDocumentStatusCommandHandler Consultar() => new(Intento());

    public RegistroDeDocumentoElectronico Registro() => new(E.Db, E.Reloj);

    /// <summary>Registra y guarda una factura del ejemplo con el consecutivo dado y la identificación de la contraparte.</summary>
    public async Task<ElectronicDocument> FacturaAsync(long consecutivo = 990000123, string taxId = "16000110")
    {
        var entrada = Entradas.Factura(taxId) with { DocumentPublicId = Guid.NewGuid() };
        var canonico = ConstructorDelCanonico.Construir(entrada, new ContextoDelCanonico
        {
            Configuracion = Configuracion, Resolucion = Resolucion, Prefijo = "SETP", Consecutivo = consecutivo,
        });
        if (canonico.IsFailure) throw new InvalidOperationException(canonico.Error.Message);
        Reconstruccion.Registrar(canonico.Value);

        var registro = await Registro().RegistrarAsync(new PedidoDeRegistroElectronico(
            "INV", entrada.DocumentPublicId, "FV", Configuracion, canonico.Value, Resolucion.Id, ResolutionKind.Invoice), default);
        if (registro.IsFailure) throw new InvalidOperationException(registro.Error.Message);
        await E.Db.SaveChangesAsync();
        return registro.Value;
    }

    /// <summary>Adelanta el reloj más allá de cualquier espera.</summary>
    public void Adelantar(TimeSpan? cuanto = null) => Ahora = Ahora.Add(cuanto ?? TimeSpan.FromHours(2));
}

/// <summary>El registro de canales que sólo tiene el canal guionado.</summary>
public sealed class RegistroDeCanales(params ICanalDeEmisionElectronica[] canales) : ICanalesDeEmision
{
    public ICanalDeEmisionElectronica Resolver(string channelCode) =>
        canales.First(c => string.Equals(c.ChannelCode, channelCode, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<string> Codigos => canales.Select(c => c.ChannelCode).ToList();
}

/// <summary>Entradas neutrales de ejemplo (la factura de contracts/dian.md §4.2).</summary>
public static class Entradas
{
    public static readonly DateOnly Fecha = new(2026, 12, 5);

    public static EntradaDeDocumentoElectronico Factura(string taxId = "16000110") => new()
    {
        SourceModule = "INV",
        DocumentPublicId = new Guid("7d0b1a8e-0000-4000-8000-000000000001"),
        DocumentClass = "SalesInvoice",
        DocumentTypeCode = "FV",
        DocumentNumber = "SETP990000123",
        Kind = ElectronicDocumentKind.Invoice,
        OperationDate = Fecha,
        ConfirmedAtUtc = new DateTime(2026, 12, 5, 15, 14, 22, DateTimeKind.Utc),
        Currency = "COP",
        ExchangeRate = 1m,
        Contrapartes =
        [
            new FotoFiscalDeEntrada
            {
                Version = 1, OrganizationType = "2", IdTypeCode = "13", TaxId = taxId, LegalName = "ANA PÉREZ",
                Address = "CALLE 5 # 10-20", MunicipalityDaneCode = "76001", Email = "ana@correo.co",
            },
        ],
        Lineas = [new LineaDeEntrada(1, "ARZ-001", "ARROZ 500 G", 2m, "UND", "94", 2100m, 4200m, 210m)],
        Impuestos = [new ImpuestoDeEntrada(1, "IVA19", "01", false, 0.19m, null, null, 3990m, 758.10m)],
        Pagos = [new PagoDeEntrada("EFE", "Efectivo", PaymentMeansClass.Cash, "10", 4748.10m, null)],
        Totales = new TotalesDeEntrada(4200m, 210m, 758.10m, 0m, 4748.10m, 4748.10m),
    };
}
