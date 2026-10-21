using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.NationalValues;

/// <summary>
/// Colección «Valores nacionales e2e» (feature 013, T0015; decisiones-transversales §7.6): otro host, porque el registro
/// central de valores nacionales es uno por host y todas sus cooperativas lo replican. Registrar o corregir un valor aquí
/// tocaría a las cooperativas de «Cartera e2e» si compartieran host.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ValoresNacionalesCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Valores nacionales e2e";
}

/// <summary>
/// Ayudantes de la colección (T0015). <see cref="ReplicarAsync"/> (T0059) corre una pasada de la réplica en una cooperativa
/// sin esperar a la tarea programada (la fixture la apaga).
/// </summary>
public static class ValoresNacionalesE2E
{
    public static Task<string> TokenMaestroAsync(CentralIdentityApiFixture fx, HttpClient http) => CarteraE2E.TokenMaestroAsync(fx, http);

    public static Task<CarteraE2E.CooperativaAislada> CooperativaAisladaAsync(CentralIdentityApiFixture fx, string nombre, int? primerEjercicioContable = null) =>
        CarteraE2E.CooperativaAisladaAsync(fx, "vn_" + nombre, primerEjercicioContable);

    /// <summary>
    /// Una pasada de la réplica de valores nacionales en la cooperativa (feature 013, T0059): resuelve el núcleo único
    /// (<see cref="ReplicateNationalValuesCommand"/>) por <see cref="IEjecutorEnCooperativa"/> con el actor «Proceso de
    /// integración» y el origen de la tarea, como lo haría <c>valores-nacionales.replicar</c>. Devuelve lo que copió.
    /// </summary>
    public static async Task<ResultadoDeReplicacion> ReplicarAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, CancellationToken ct = default)
    {
        TenantDirectoryEntry cooperativa;
        using (var alcance = fx.Factory.Services.CreateScope())
        {
            cooperativa = (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct))
                .Single(c => c.PublicId == tenantPublicId);
        }

        ResultadoDeReplicacion? resultado = null;
        var origen = Actor.OrigenDeTarea(TareaDeReplicaDeValoresNacionales.NombreDeLaTarea);
        var ejecutada = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(
            cooperativa, Actor.ProcesoDeIntegracion(origen), origen,
            async (servicios, c) =>
            {
                var r = await servicios.GetRequiredService<ISender>().Send(new ReplicateNationalValuesCommand(NationalValueReplicationOrigin.ScheduledTask), c);
                r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
                resultado = r.Value;
            }, ct);
        ejecutada.Should().Be(ResultadoEnCooperativa.Ejecutada, "la cooperativa no debe tener migraciones pendientes");
        return resultado!;
    }
}

/// <summary>Prueba humo de la colección (T0015): el maestro opera en este host.</summary>
[Collection(ValoresNacionalesCollection.Nombre)]
public sealed class ValoresNacionalesE2EHumoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_maestro_lista_las_cooperativas_de_este_host()
    {
        using var http = fx.CreateClient();
        var token = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var cooperativas = await CarteraE2E.GetAsync(http, token, "/api/saas/tenants");
        cooperativas.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }
}
