using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Transport;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Connectors;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.Connectors;

/// <summary>
/// Feature 020 (T170; R16 a R18): el consumidor de los avisos con un sistema externo falso. Cada resultado del mapa de respuestas, el nuevo
/// inicio de sesión una sola vez, el tope de intentos, la espera con el conector deshabilitado y el mismo identificador externo en cada
/// reintento.
/// </summary>
public class DestinoConectoresTests
{
    private const string Definicion = """
        {
          "auth": { "kind": "None" },
          "operation": { "method": "POST", "path": "/v1/add",
            "body": "{\"placa\":\"{{vehiculo.placa}}\",\"valor\":{{pago.valor:int}},\"externalID\":\"{{envio.id}}\"}" },
          "responses": [ { "status": 200, "outcome": "Done" }, { "status": 202, "outcome": "DoneWithWarning" },
            { "status": 401, "outcome": "ReAuth" }, { "status": 500, "outcome": "Retry" } ],
          "maxAttempts": 3
        }
        """;

    private static readonly DateTime Ahora = new(2026, 12, 5, 15, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMensajesEntrantes _entrantes = Substitute.For<IMensajesEntrantes>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ICurrentTenantService _cooperativa = Substitute.For<ICurrentTenantService>();
    private readonly EjecutorFalso _ejecutor = new();
    private readonly Guid _externo = Guid.NewGuid();
    private readonly Guid _mensaje = Guid.NewGuid();
    private Connector _conector = null!;
    private ConnectorRequest _envio = null!;

    public DestinoConectoresTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _cooperativa.TenantId.Returns(Guid.NewGuid().ToString());
        _sender.Send(Arg.Any<RegisterConnectorCallCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RegisterConnectorCallCommandHandler(_db, _reloj).Handle(ci.Arg<RegisterConnectorCallCommand>(), CancellationToken.None));
        Sembrar(habilitado: true);
    }

    private void Sembrar(bool habilitado)
    {
        _conector = new Connector { Code = "RADIO", Name = "Radio", IsEnabled = habilitado, CurrentVersionNumber = 1 };
        _conector.Versions.Add(new ConnectorVersion { Number = 1, BaseUrl = "https://proveedor.example.com", DefinitionJson = Definicion, DefinitionSha256 = "x" });
        _db.Connectors.Add(_conector);
        _db.SaveChanges();
        _envio = new ConnectorRequest { MessagePublicId = _mensaje, ConnectorId = _conector.Id, VersionNumber = 1, OriginRef = "TRN:x", ExternalId = _externo };
        _db.ConnectorRequests.Add(_envio);
        _db.SaveChanges();

        var solicitud = new SolicitudDeConectorV1
        {
            ConnectorPublicId = _conector.PublicId, ConnectorVersion = 1, ExternalId = _externo, OriginRef = "TRN:x",
            Variables = new Dictionary<string, string> { ["vehiculo.placa"] = "ABC123", ["pago.valor"] = "50000", ["envio.id"] = _externo.ToString("D") },
        };
        var json = System.Text.Json.JsonSerializer.Serialize(solicitud);
        var sobre = new IntegrationEnvelopeV1 { MessageId = _mensaje, Type = SolicitudDeConectorV1.Type, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "TRN" };
        var entrante = new MensajeEntrante(sobre, solicitud, json, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), null,
            new EntregaEntrante(IntegrationDestinations.Connectors, DeliveryMode.Always, DeliveryStatus.Pending, null, null, null, 0, null));
        _entrantes.LeerAsync(Arg.Any<IReadOnlyCollection<Guid>>(), IntegrationDestinations.Connectors, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<MensajeEntrante>>([entrante]));
    }

    private DestinoConectores Destino()
    {
        var almacen = new AlmacenDeCredencialesDeConector(_db, new EphemeralDataProtectionProvider(), _cooperativa);
        var sesion = new SesionDeConector(_ejecutor, almacen, new MemoryCache(new MemoryCacheOptions()), _cooperativa);
        return new DestinoConectores(_db, _entrantes, sesion, almacen, _ejecutor, _sender, _reloj, NullLogger<DestinoConectores>.Instance);
    }

    private async Task<ResultadoDeConsumo> ConsumirAsync()
    {
        var unidad = new UnidadDeConsumo(IntegrationDestinations.Connectors, Guid.NewGuid(), "Confirmation", [_mensaje], null, 0);
        var r = await Destino().ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(unidad), CancellationToken.None);
        return r.Single().Resultado;
    }

    [Fact]
    public async Task Hecho_procesa_con_el_identificador_externo_y_deja_el_envio_enviado()
    {
        _ejecutor.Responder(200);

        var r = await ConsumirAsync();

        r.Should().BeOfType<ResultadoDeConsumo.Processed>().Which.AccountingDocumentPublicId.Should().Be(_externo);
        _envio.Status.Should().Be(ConnectorRequestStatus.Sent);
        _db.ConnectorCalls.Should().ContainSingle().Which.RequestBodySanitized.Should().Contain("ABC123");
    }

    [Fact]
    public async Task Hecho_con_aviso_procesa_y_guarda_el_aviso()
    {
        _ejecutor.Responder(202, "ya tiene vigencia");

        var r = await ConsumirAsync();

        r.Should().BeOfType<ResultadoDeConsumo.Processed>();
        _envio.Status.Should().Be(ConnectorRequestStatus.SentWithWarning);
        _envio.Warning.Should().Contain("vigencia");
    }

    [Fact]
    public async Task Reintentar_deja_el_envio_reintentando()
    {
        _ejecutor.Responder(500);

        (await ConsumirAsync()).Should().BeOfType<ResultadoDeConsumo.Retry>();
        _envio.Status.Should().Be(ConnectorRequestStatus.Retrying);
    }

    [Fact]
    public async Task Lo_no_previsto_se_rechaza_para_revisar()
    {
        _ejecutor.Responder(400, "vehículo no existe");

        (await ConsumirAsync()).Should().BeOfType<ResultadoDeConsumo.Rejected>();
        _envio.Status.Should().Be(ConnectorRequestStatus.Review);
    }

    [Fact]
    public async Task Ante_un_401_inicia_sesion_otra_vez_una_sola_vez()
    {
        _ejecutor.Responder(401);

        (await ConsumirAsync()).Should().BeOfType<ResultadoDeConsumo.Retry>();
        _ejecutor.Llamadas.Should().HaveCount(2);
    }

    [Fact]
    public async Task Con_los_intentos_agotados_no_llama_y_pide_revision()
    {
        _envio.Attempts = 3;
        await _db.SaveChangesAsync();

        (await ConsumirAsync()).Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(DestinoConectores.CodigoAgotado);
        _ejecutor.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_el_conector_deshabilitado_el_envio_espera()
    {
        _conector.IsEnabled = false;
        await _db.SaveChangesAsync();

        (await ConsumirAsync()).Should().BeOfType<ResultadoDeConsumo.Retry>();
        _envio.Status.Should().Be(ConnectorRequestStatus.OnHold);
        _ejecutor.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Cada_reintento_lleva_el_mismo_identificador_externo()
    {
        _ejecutor.Responder(500);
        await ConsumirAsync();
        await ConsumirAsync();

        _ejecutor.Llamadas.Should().HaveCount(2);
        _ejecutor.Llamadas.Should().OnlyContain(s => s.Cuerpo!.Contains(_externo.ToString("D")));
    }

    private sealed class EjecutorFalso : IEjecutorDeConector
    {
        private int _codigo = 200;
        private string? _cuerpo = "{}";

        public List<SolicitudHttp> Llamadas { get; } = [];

        public void Responder(int codigo, string? cuerpo = "{}")
        {
            _codigo = codigo;
            _cuerpo = cuerpo;
        }

        public Task<RespuestaHttp> EjecutarAsync(SolicitudHttp solicitud, CancellationToken ct)
        {
            Llamadas.Add(solicitud);
            return Task.FromResult(new RespuestaHttp(_codigo, _cuerpo, 12));
        }
    }
}
