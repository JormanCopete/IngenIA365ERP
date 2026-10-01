using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Services;
using IngenIA365ERP.Storage.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T022 (feature 012; decisiones-transversales T10, T14, T47): arrendamientos de trabajos de fondo en
/// <c>COR_BackgroundLeases</c>, en la base de cada cooperativa, contra el motor real
/// (<c>DB_PROVIDER=PostgreSql</c> y <c>SqlServer</c>): InMemory no ejecuta el <c>UPDATE … WHERE</c> que
/// decide quién toma la fila.
///
/// <para>
/// Escrita en la fase 2; se ejecuta tras la migración <c>PlataformaParaInventario</c> (T186), que crea la
/// tabla y siembra sus cinco filas. Los dos casos de <c>TransaccionExplicita</c> (T052: la anidada se une a
/// la de afuera; un interbloqueo 40P01/1205 se repite entero tras <c>DescartarCambios()</c>) usan también
/// <c>COR_OperationKeys</c>, que llega con la misma migración, y corren en su propia cooperativa aislada.
/// </para>
///
/// <para>
/// Cada caso usa un nombre de arrendamiento distinto, en una cooperativa aislada, para no depender del
/// orden: la fixture tiene apagados todos los trabajos de fondo, así que nadie más toca estas filas.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class ArrendamientosYTransaccionTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "arrendamientos";

    private async Task<TenantDirectoryEntry> EntradaAsync(Guid publicId)
    {
        using var ambito = fx.Factory.Services.CreateScope();
        var activas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None);
        return activas.Single(t => t.PublicId == publicId);
    }

    private async Task<TenantDirectoryEntry> CooperativaAsync() =>
        await EntradaAsync((await InventarioE2E.CooperativaAisladaAsync(fx, Cooperativa)).TenantPublicId);

    /// <summary>Corre <paramref name="trabajo"/> en la base de la cooperativa, por el ejecutor real.</summary>
    private async Task<T> EnLaCooperativaAsync<T>(TenantDirectoryEntry entrada, Func<IServiceProvider, Task<T>> trabajo)
    {
        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        T resultado = default!;
        var r = await ejecutor.EjecutarAsync(entrada, Actor.ProcesoDeIntegracion("Tarea:prueba-arrendamientos"), "Tarea:prueba-arrendamientos",
            async (servicios, _) => resultado = await trabajo(servicios));
        r.Should().Be(ResultadoEnCooperativa.Ejecutada, "la base de la cooperativa aislada está migrada");
        return resultado;
    }

    /// <summary>Un arrendador con dueño propio, sobre la base de la cooperativa del ámbito.</summary>
    private static ArrendamientosEnBase Dueno(IServiceProvider servicios, string dueno) =>
        new(servicios.GetRequiredService<ApplicationDbContext>(), servicios.GetRequiredService<IDateTimeService>(),
            NullLogger<ArrendamientosEnBase>.Instance, dueno);

    /// <summary>El ejecutor del host, pero con <see cref="IArrendamientos"/> a nombre de otra réplica.</summary>
    private sealed class EjecutorConDueno(IEjecutorEnCooperativa interno, string dueno) : IEjecutorEnCooperativa
    {
        public Task<ResultadoEnCooperativa> EjecutarAsync(TenantDirectoryEntry cooperativa, Actor actor, string origen,
            Func<IServiceProvider, CancellationToken, Task> trabajo, CancellationToken ct = default) =>
            interno.EjecutarAsync(cooperativa, actor, origen, (s, c) => trabajo(new ServiciosConDueno(s, dueno), c), ct);
    }

    private sealed class ServiciosConDueno(IServiceProvider interno, string dueno) : IServiceProvider
    {
        public object? GetService(Type tipo) =>
            tipo == typeof(IArrendamientos) ? Dueno(interno, dueno) : interno.GetService(tipo);
    }

    private Task<bool> ArrendarAsync(TenantDirectoryEntry entrada, string dueno, string nombre, TimeSpan duracion) =>
        EnLaCooperativaAsync(entrada, s => Dueno(s, dueno).ArrendarAsync(nombre, duracion));

    private Task<BackgroundLease> FilaAsync(TenantDirectoryEntry entrada, string nombre) =>
        EnLaCooperativaAsync(entrada, s => s.GetRequiredService<ApplicationDbContext>().Set<BackgroundLease>()
            .AsNoTracking().SingleAsync(l => l.Name == nombre));

    [Fact]
    public async Task La_migracion_deja_las_cinco_filas_libres()
    {
        var entrada = await CooperativaAsync();

        var filas = await EnLaCooperativaAsync(entrada, s => s.GetRequiredService<ApplicationDbContext>().Set<BackgroundLease>()
            .AsNoTracking().OrderBy(l => l.Id).ToListAsync());

        filas.Select(l => l.Name).Should().BeEquivalentTo(NombresDeArrendamiento.Todos);
        filas.Should().OnlyContain(l => !l.IsDeleted);
    }

    [Fact]
    public async Task Dos_duenos_que_piden_a_la_vez_uno_solo_lo_toma()
    {
        var entrada = await CooperativaAsync();
        var nombre = NombresDeArrendamiento.Despacho;

        var intentos = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            ArrendarAsync(entrada, $"dueño-{i}", nombre, TimeSpan.FromMinutes(2))));

        intentos.Count(t => t).Should().Be(1, "el UPDATE … WHERE LeaseUntil < ahora OR Owner = yo deja entrar a uno solo");
        var fila = await FilaAsync(entrada, nombre);
        fila.Owner.Should().StartWith("dueño-");
        fila.LeaseUntil.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Vencido_lo_toma_el_otro()
    {
        var entrada = await CooperativaAsync();
        var nombre = NombresDeArrendamiento.ReenvioDeAuditoria;

        (await ArrendarAsync(entrada, "réplica-a", nombre, TimeSpan.FromSeconds(1))).Should().BeTrue();
        (await ArrendarAsync(entrada, "réplica-b", nombre, TimeSpan.FromMinutes(2))).Should().BeFalse("todavía es de A");
        (await ArrendarAsync(entrada, "réplica-a", nombre, TimeSpan.FromSeconds(1))).Should().BeTrue("el mismo dueño lo vuelve a tomar");

        await Task.Delay(TimeSpan.FromSeconds(2));

        (await ArrendarAsync(entrada, "réplica-b", nombre, TimeSpan.FromMinutes(2))).Should().BeTrue("vencido, cualquiera lo toma");
        (await FilaAsync(entrada, nombre)).Owner.Should().Be("réplica-b");
    }

    [Fact]
    public async Task Renovar_extiende_el_vencimiento_y_solo_lo_hace_el_dueno()
    {
        var entrada = await CooperativaAsync();
        var nombre = NombresDeArrendamiento.FacturacionElectronica;

        (await ArrendarAsync(entrada, "réplica-a", nombre, TimeSpan.FromSeconds(30))).Should().BeTrue();
        var antes = (await FilaAsync(entrada, nombre)).LeaseUntil;

        (await EnLaCooperativaAsync(entrada, s => Dueno(s, "réplica-a").RenovarAsync(nombre, TimeSpan.FromMinutes(5))))
            .Should().BeTrue();
        (await FilaAsync(entrada, nombre)).LeaseUntil.Should().BeAfter(antes.AddMinutes(4));

        (await EnLaCooperativaAsync(entrada, s => Dueno(s, "réplica-b").RenovarAsync(nombre, TimeSpan.FromMinutes(5))))
            .Should().BeFalse("no es suyo");
    }

    [Fact]
    public async Task Soltado_queda_libre_para_el_siguiente()
    {
        var entrada = await CooperativaAsync();
        var nombre = NombresDeArrendamiento.TareasProgramadas;

        (await ArrendarAsync(entrada, "réplica-a", nombre, TimeSpan.FromMinutes(2))).Should().BeTrue();
        await EnLaCooperativaAsync(entrada, async s =>
        {
            await Dueno(s, "réplica-b").SoltarAsync(nombre);
            return true;
        });
        (await FilaAsync(entrada, nombre)).Owner.Should().Be("réplica-a", "soltar sólo libera lo propio");

        await EnLaCooperativaAsync(entrada, async s =>
        {
            await Dueno(s, "réplica-a").SoltarAsync(nombre);
            return true;
        });

        (await FilaAsync(entrada, nombre)).Owner.Should().BeNull();
        (await ArrendarAsync(entrada, "réplica-b", nombre, TimeSpan.FromMinutes(2))).Should().BeTrue();
        await EnLaCooperativaAsync(entrada, async s =>
        {
            await Dueno(s, "réplica-b").SoltarAsync(nombre);
            return true;
        });
    }

    [Fact]
    public async Task Dos_despachadores_de_correo_sobre_la_misma_cooperativa_mandan_un_solo_correo()
    {
        var aislada = await InventarioE2E.CooperativaAisladaAsync(fx, Cooperativa);
        var entrada = await EntradaAsync(aislada.TenantPublicId);
        var correo = $"admin.{Cooperativa}@coop.inventario.test";
        var asunto = $"Correo único {Guid.NewGuid():N}";

        // Una notificación con correo pendiente para el administrador de la cooperativa, por el comando.
        await EnLaCooperativaAsync(entrada, async s =>
        {
            var destinatario = await s.GetRequiredService<IApplicationDbContext>().Users.AsNoTracking()
                .Where(u => u.Email == correo).Select(u => u.PublicId).SingleAsync();
            var enviado = await s.GetRequiredService<ISender>().Send(new SendNotificationCommand(new NotificationPayload(
                destinatario, NotificationType.RoleAssigned, asunto, "Una sola vez.", NotificationChannels.Email)));
            enviado.IsSuccess.Should().BeTrue();
            return true;
        });

        var ambitos = fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var ejecutor = fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>();
        // Dos réplicas, no dos despachadores del mismo proceso: el dueño del arrendamiento es el del
        // proceso (ArrendamientosEnBase.DuenoDelProceso) y quien ya es dueño lo vuelve a tomar, así que
        // dos instancias en este host se lo pasaban entre sí y a veces mandaban el correo dos veces.
        var uno = new NotificationEmailDispatcher(ambitos, new EjecutorConDueno(ejecutor, "réplica-a"), NullLogger<NotificationEmailDispatcher>.Instance);
        var otro = new NotificationEmailDispatcher(ambitos, new EjecutorConDueno(ejecutor, "réplica-b"), NullLogger<NotificationEmailDispatcher>.Instance);

        await Task.WhenAll(uno.DespacharUnaPasadaAsync(CancellationToken.None), otro.DespacharUnaPasadaAsync(CancellationToken.None));
        await Task.WhenAll(uno.DespacharUnaPasadaAsync(CancellationToken.None), otro.DespacharUnaPasadaAsync(CancellationToken.None));

        fx.Emails.Sent.Count(m => m.Subject == asunto).Should().Be(1,
            "el arrendamiento email.dispatch de la cooperativa deja leer el lote pendiente a una sola réplica (pregunta B5)");
        (await FilaAsync(entrada, NombresDeArrendamiento.Correo)).Owner.Should().BeNull("cada pasada suelta el arrendamiento al terminar");
    }

    // ------------------------------------------------ TransaccionExplicita (T052) --

    private static OperationKey FilaDePrueba(string marca) => new()
    {
        Key = Guid.NewGuid(), Operation = marca, RequestSha256 = new string('0', 64), CentralUserId = Guid.Empty, ActorName = "Prueba T052",
    };

    [Fact]
    public async Task Una_TransaccionExplicita_anidada_se_une_a_la_de_afuera()
    {
        var entrada = await EntradaAsync((await InventarioE2E.CooperativaAisladaAsync(fx, "transaccion")).TenantPublicId);
        var marca = $"Anidada-{Guid.NewGuid():N}"[..40];

        var resultado = await EnLaCooperativaAsync(entrada, async s =>
        {
            var db = s.GetRequiredService<IApplicationDbContext>();
            var ctx = s.GetRequiredService<ApplicationDbContext>();
            return await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                var afuera = ctx.Database.CurrentTransaction;
                afuera.Should().NotBeNull();
                db.OperationKeys.Add(FilaDePrueba(marca));
                await db.SaveChangesAsync();

                var adentro = await TransaccionExplicita.EjecutarAsync(db, async () =>
                {
                    ctx.Database.CurrentTransaction.Should().BeSameAs(afuera, "la anidada no abre otra transacción");
                    db.OperationKeys.Add(FilaDePrueba(marca));
                    await db.SaveChangesAsync();
                    return Result.Success();
                }, CancellationToken.None);
                adentro.IsSuccess.Should().BeTrue();

                // La de afuera decide: un fallo revierte también lo que guardó la anidada.
                return Result.Failure("Prueba.Revertir", "se revierte a propósito");
            }, CancellationToken.None);
        });

        resultado.IsFailure.Should().BeTrue();
        var quedaron = await EnLaCooperativaAsync(entrada, s => s.GetRequiredService<ApplicationDbContext>().OperationKeys
            .AsNoTracking().CountAsync(k => k.Operation == marca));
        quedaron.Should().Be(0, "la anidada se unió a la de afuera y se revirtió con ella");
    }

    [Fact]
    public async Task Ante_un_interbloqueo_la_estrategia_repite_el_trabajo_entero_tras_descartar()
    {
        var entrada = await EntradaAsync((await InventarioE2E.CooperativaAisladaAsync(fx, "transaccion")).TenantPublicId);
        var barrera = new Barrier(2);
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        // Dos trabajos toman dos filas de COR_BackgroundLeases en orden cruzado: el motor elige una víctima
        // (PostgreSQL 40P01, SQL Server 1205), la estrategia de ejecución la repite entera y el segundo
        // intento ya no espera en la barrera.
        Task<int> TrabajoAsync(string marca, string primero, string segundo) => EnLaCooperativaAsync(entrada, async s =>
        {
            var db = s.GetRequiredService<IApplicationDbContext>();
            var ctx = s.GetRequiredService<ApplicationDbContext>();
            var intentos = 0;
            var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
            {
                intentos++;
                // Una fila pendiente en el ChangeTracker: si el reintento no descartara, quedarían dos.
                db.OperationKeys.Add(FilaDePrueba(marca));
                await ctx.Set<BackgroundLease>().Where(l => l.Name == primero)
                    .ExecuteUpdateAsync(u => u.SetProperty(l => l.LeaseUntil, l => l.LeaseUntil));
                if (intentos == 1) await Task.Run(() => barrera.SignalAndWait(TimeSpan.FromSeconds(30)));
                await ctx.Set<BackgroundLease>().Where(l => l.Name == segundo)
                    .ExecuteUpdateAsync(u => u.SetProperty(l => l.LeaseUntil, l => l.LeaseUntil));
                await db.SaveChangesAsync();
                return Result.Success(intentos);
            }, CancellationToken.None);
            r.IsSuccess.Should().BeTrue();
            return r.Value;
        });

        var uno = $"Interbloqueo-A-{sufijo}";
        var dos = $"Interbloqueo-B-{sufijo}";
        var intentos = await Task.WhenAll(
            TrabajoAsync(uno, NombresDeArrendamiento.Despacho, NombresDeArrendamiento.ReenvioDeAuditoria),
            TrabajoAsync(dos, NombresDeArrendamiento.ReenvioDeAuditoria, NombresDeArrendamiento.Despacho));

        intentos.Max().Should().BeGreaterThan(1, "la víctima del interbloqueo se repitió");
        intentos.Min().Should().Be(1, "la otra confirmó al primer intento");
        foreach (var marca in new[] { uno, dos })
        {
            var filas = await EnLaCooperativaAsync(entrada, s => s.GetRequiredService<ApplicationDbContext>().OperationKeys
                .AsNoTracking().CountAsync(k => k.Operation == marca));
            filas.Should().Be(1, "cada intento empieza con DescartarCambios(): lo agregado en el intento fallido no se guarda dos veces");
        }
    }
}
