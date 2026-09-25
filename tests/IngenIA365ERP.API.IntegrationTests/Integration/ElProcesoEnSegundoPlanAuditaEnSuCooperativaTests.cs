using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;
using IngenIA365ERP.Audit.Configuration;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T020 (feature 012; T5, T6, FR-083, D-03; quickstart §1.2): un comando corrido fuera de HTTP por
/// <see cref="IEjecutorEnCooperativa"/> tiene el mismo contexto que una petición —la base, la
/// auditoría y el actor de SU cooperativa— y nada de otra ni de la global.
///
/// <para>
/// Escrita en la fase 2; se ejecuta en los dos motores tras la migración <c>PlataformaParaInventario</c>
/// (T186). El comando es uno de Core que ya existe (<see cref="CreateBranchCommand"/>): lo que se
/// prueba es el camino de ejecución, no el comando. Las aserciones de canal y origen en la metadata
/// del evento dependen de la ampliación de <c>AuditBehavior</c> (T36) y se buscan por valor, no por
/// nombre de clave.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ElProcesoEnSegundoPlanAuditaEnSuCooperativaTests(CentralIdentityApiFixture fx)
{
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";

    private async Task<TenantDirectoryEntry> EntradaAsync(Guid publicId)
    {
        using var ambito = fx.Factory.Services.CreateScope();
        var directorio = ambito.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var activas = await directorio.ListActiveAsync(CancellationToken.None);
        return activas.Single(t => t.PublicId == publicId);
    }

    private IMongoCollection<BsonDocument> Auditoria(string? tenantPublicIdN) =>
        fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, tenantPublicIdN))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);

    private static FilterDefinition<BsonDocument> ConTexto(string texto) =>
        Builders<BsonDocument>.Filter.Regex("newValuesJson", new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(texto)));

    private async Task<BsonDocument?> EsperarEventoAsync(IMongoCollection<BsonDocument> coleccion, string texto)
    {
        var auditoria = fx.Factory.Services.GetRequiredService<IAuditService>();
        var limite = DateTime.UtcNow.AddSeconds(20);
        do
        {
            await auditoria.FlushAsync();
            var evento = await coleccion.Find(ConTexto(texto)).FirstOrDefaultAsync();
            if (evento is not null) return evento;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        return null;
    }

    [Fact]
    public async Task El_comando_del_proceso_escribe_y_audita_solo_en_su_cooperativa()
    {
        var a = await InventarioE2E.CooperativaAisladaAsync(fx, "procesoa");
        var b = await InventarioE2E.CooperativaAisladaAsync(fx, "procesob");
        var entradaA = await EntradaAsync(a.TenantPublicId);
        var nombre = $"Sucursal del proceso {Guid.NewGuid():N}"[..40];
        const string origen = "Tarea:prueba-e2e";

        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        var resultado = await ejecutor.EjecutarAsync(entradaA, Actor.ProcesoDeIntegracion(origen), origen, async (servicios, ct) =>
        {
            var enviado = await servicios.GetRequiredService<ISender>().Send(new CreateBranchCommand { Name = nombre, ShortName = "PROC" }, ct);
            enviado.IsSuccess.Should().BeTrue(enviado.IsFailure ? enviado.Error.Code : "");
        });

        resultado.Should().Be(ResultadoEnCooperativa.Ejecutada);
        ContextoAmbiental.Activo.Should().BeFalse("el ejecutor restaura el contexto al terminar");

        // La fila SQL quedó en la base de A y no en la de B.
        using var http = fx.CreateClient();
        var sucursalesA = await InventarioE2E.GetAsync(http, a.TokenAdmin, "/api/core/branches?PageNumber=1&PageSize=100");
        sucursalesA.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should().Contain(nombre);
        var sucursalesB = await InventarioE2E.GetAsync(http, b.TokenAdmin, "/api/core/branches?PageNumber=1&PageSize=100");
        sucursalesB.GetProperty("items").EnumerateArray().Select(s => s.GetProperty("name").GetString()).Should().NotContain(nombre);

        // La auditoría, en IngenIA365ERP_Audit_{A}, firmada por el proceso, sin IP y con canal y origen.
        var evento = await EsperarEventoAsync(Auditoria(a.TenantPublicId.ToString("N")), nombre);
        evento.Should().NotBeNull("el evento del comando va a la base de auditoría de la cooperativa A");
        evento!["userName"].AsString.Should().Be(Actor.NombreDelProceso);
        (evento.Contains("ipAddress") && !evento["ipAddress"].IsBsonNull).Should().BeFalse("el proceso no tiene IP");
        var metadata = evento.Contains("metadata") && evento["metadata"].IsBsonDocument ? evento["metadata"].AsBsonDocument : new BsonDocument();
        metadata.Values.Select(v => v.ToString()).Should().Contain(v => string.Equals(v, "proceso", StringComparison.OrdinalIgnoreCase)
            || string.Equals(v, "Process", StringComparison.OrdinalIgnoreCase), "el canal del proceso es «proceso» (T36)");
        metadata.Values.Select(v => v.ToString()).Should().Contain(origen, "el origen del proceso es Tarea:{nombre}");

        // Nada en B ni en la global.
        (await Auditoria(b.TenantPublicId.ToString("N")).CountDocumentsAsync(ConTexto(nombre))).Should().Be(0);
        (await Auditoria(null).CountDocumentsAsync(ConTexto(nombre))).Should().Be(0, "un trabajo de fondo nunca audita en la base global");
    }

    [Fact]
    public async Task Dentro_de_una_peticion_el_ejecutor_lanza_y_no_escribe()
    {
        var a = await InventarioE2E.CooperativaAisladaAsync(fx, "procesoa");
        var entradaA = await EntradaAsync(a.TenantPublicId);
        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        var accesor = fx.Factory.Services.GetRequiredService<IHttpContextAccessor>();
        var corrio = false;

        accesor.HttpContext = new DefaultHttpContext();
        try
        {
            await FluentActions.Invoking(() => ejecutor.EjecutarAsync(entradaA, Actor.ProcesoDeIntegracion("Tarea:x"), "Tarea:x", (_, _) =>
            {
                corrio = true;
                return Task.CompletedTask;
            })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*petición*");
        }
        finally
        {
            accesor.HttpContext = null;
        }

        corrio.Should().BeFalse();
    }

    [Fact]
    public async Task Una_entrada_sin_base_lanza_y_no_cae_a_la_plantilla()
    {
        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        var sinBase = new TenantDirectoryEntry(999_999, Guid.NewGuid(), "sin_base", "Cooperativa sin base");
        var corrio = false;

        await FluentActions.Invoking(() => ejecutor.EjecutarAsync(sinBase, Actor.ProcesoDeIntegracion("Tarea:x"), "Tarea:x", (_, _) =>
        {
            corrio = true;
            return Task.CompletedTask;
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*no trae base*");

        corrio.Should().BeFalse();
    }

    [Fact]
    public async Task La_auditoria_con_contexto_ambiental_sin_cooperativa_lanza()
    {
        // El accesor real siempre resuelve la cooperativa del ambiental; el caso «contexto sin
        // cooperativa» se arma con un ICurrentTenantService que no la resuelve, sobre el MISMO
        // MongoAuditService del host (la guarda es suya, no del accesor).
        var entrada = new TenantDirectoryEntry(1, Guid.NewGuid(), "x", "X", DatabaseName: "x");
        var tenantNulo = NSubstitute.Substitute.For<ICurrentTenantService>();
        using var servicio = new IngenIA365ERP.Audit.Services.MongoAuditService(
            fx.Factory.Services.GetRequiredService<IMongoClient>(),
            Microsoft.Extensions.Options.Options.Create(new IngenIA365ERP.Audit.Configuration.MongoDbSettings { DatabaseName = BaseDeAuditoria, FlushIntervalSeconds = 3600 }),
            fx.Factory.Services.GetRequiredService<ICurrentUserService>(),
            tenantNulo,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<IngenIA365ERP.Audit.Services.MongoAuditService>.Instance);
        var globalAntes = await Auditoria(null).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

        using (ContextoAmbiental.Fijar(entrada, Actor.ProcesoDeIntegracion("Tarea:sin-cooperativa"), "Tarea:sin-cooperativa"))
        {
            await FluentActions.Invoking(() => servicio.LogAsync(new AuditLogCommand { Action = "Create", EntityType = "Prueba" }))
                .Should().ThrowAsync<InvalidOperationException>();
        }

        await servicio.FlushAsync();
        (await Auditoria(null).CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("entityType", "Prueba")))
            .Should().Be(0, $"no se escribió nada en la global (tenía {globalAntes})");
    }
}
