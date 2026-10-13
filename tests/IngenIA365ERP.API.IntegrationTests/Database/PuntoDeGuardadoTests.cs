using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Database;

/// <summary>
/// Feature 013, US6, T0583 (C25, D26; DT §2.6): <see cref="PuntoDeGuardado"/> contra el motor real (<c>DB_PROVIDER=PostgreSql</c> y <c>SqlServer</c>):
/// una falla técnica dentro del punto vuelve a él —lo que el trabajo guardó no queda— y la transacción de afuera sigue utilizable (en PostgreSQL, sin
/// el punto, la falla dejaría la transacción abortada) y confirma lo suyo; sin falla, lo del punto se confirma con la de afuera. InMemory no tiene
/// transacciones: por eso esta prueba vive aquí. Es la que respalda la reserva del número dentro de la confirmación de Comercial.
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class PuntoDeGuardadoTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "punto_guardado";

    private static OperationKey Fila(string marca) => new()
    {
        Key = Guid.NewGuid(), Operation = marca, RequestSha256 = new string('0', 64), CentralUserId = Guid.Empty, ActorName = "Prueba T0583",
    };

    private async Task<TenantDirectoryEntry> EntradaAsync()
    {
        var aislada = await InventarioE2E.CooperativaAisladaAsync(fx, Cooperativa);
        using var ambito = fx.Factory.Services.CreateScope();
        var activas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None);
        return activas.Single(t => t.PublicId == aislada.TenantPublicId);
    }

    private async Task<T> EnLaCooperativaAsync<T>(TenantDirectoryEntry entrada, Func<IServiceProvider, Task<T>> trabajo)
    {
        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        T resultado = default!;
        var r = await ejecutor.EjecutarAsync(entrada, Actor.ProcesoDeIntegracion("Tarea:prueba-punto-de-guardado"), "Tarea:prueba-punto-de-guardado",
            async (servicios, _) => resultado = await trabajo(servicios));
        r.Should().Be(ResultadoEnCooperativa.Ejecutada);
        return resultado;
    }

    private Task<int> CuantasAsync(TenantDirectoryEntry entrada, string marca) =>
        EnLaCooperativaAsync(entrada, s => s.GetRequiredService<ApplicationDbContext>().OperationKeys.AsNoTracking().CountAsync(k => k.Operation == marca));

    [Fact]
    public async Task Una_falla_dentro_vuelve_al_punto_y_la_transaccion_de_afuera_sigue_utilizable()
    {
        var entrada = await EntradaAsync();
        var afuera = $"Afuera-{Guid.NewGuid():N}";
        var adentro = $"Adentro-{Guid.NewGuid():N}";

        var resultado = await EnLaCooperativaAsync(entrada, async s =>
        {
            var db = s.GetRequiredService<IApplicationDbContext>();
            var ctx = s.GetRequiredService<ApplicationDbContext>();
            return await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                db.OperationKeys.Add(Fila(afuera));
                await db.SaveChangesAsync();

                var enElPunto = await PuntoDeGuardado.EjecutarAsync(db, "prueba_punto", async () =>
                {
                    db.OperationKeys.Add(Fila(adentro));
                    await db.SaveChangesAsync();
                    db.OperationKeys.Add(Fila(adentro));
                    // Una falla técnica del motor: en PostgreSQL deja la transacción abortada si nadie vuelve al punto.
                    await ctx.Database.ExecuteSqlRawAsync("SELECT 1/0");
                    return "terminó";
                }, e => "falló", CancellationToken.None);
                enElPunto.Should().Be("falló");

                // La de afuera sigue: guarda otra fila y confirma.
                db.OperationKeys.Add(Fila(afuera));
                await db.SaveChangesAsync();
                return Result.Success();
            }, CancellationToken.None);
        });

        resultado.IsSuccess.Should().BeTrue();
        (await CuantasAsync(entrada, afuera)).Should().Be(2, "la transacción de afuera confirmó lo suyo");
        (await CuantasAsync(entrada, adentro)).Should().Be(0, "lo que el trabajo guardó dentro del punto se deshizo y lo que dejó agregado no se escribe");
    }

    [Fact]
    public async Task Sin_falla_lo_del_punto_se_confirma_con_la_de_afuera()
    {
        var entrada = await EntradaAsync();
        var marca = $"Punto-{Guid.NewGuid():N}";

        await EnLaCooperativaAsync(entrada, async s =>
        {
            var db = s.GetRequiredService<IApplicationDbContext>();
            return await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                var r = await PuntoDeGuardado.EjecutarAsync(db, "prueba_punto", async () =>
                {
                    db.OperationKeys.Add(Fila(marca));
                    await db.SaveChangesAsync();
                    return "terminó";
                }, e => "falló", CancellationToken.None);
                r.Should().Be("terminó");
                return Result.Success();
            }, CancellationToken.None);
        });

        (await CuantasAsync(entrada, marca)).Should().Be(1);
    }
}
