using FluentAssertions;
using IngenIA365ERP.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Common.Behaviors;

/// <summary>
/// <see cref="PuntoDeGuardado"/> sobre SQLite en memoria, que tiene transacciones y puntos de guardado. La e2e
/// <c>Database/PuntoDeGuardadoTests</c> lo prueba contra los dos motores reales; aquí, lo que no se provoca a voluntad con ellos: una falla que
/// se lleva la transacción entera —como hace SQL Server con la víctima de un interbloqueo (1205)— no tiene punto al que volver, y lo que sube es
/// la falla original, no el «ROLLBACK sin BEGIN» (3903) del punto. Hasta el 2026-10-16 subía éste, tapaba el interbloqueo y la estrategia de
/// ejecución no repetía nada (<c>ConcurrenciaPagoYCausacionTests</c> en SQL Server). (nuevo)
/// </summary>
public class PuntoDeGuardadoTests
{
    private static async Task<TestApplicationDbContext> ContextoConTransaccionAsync()
    {
        var opciones = new DbContextOptionsBuilder<TestApplicationDbContext>().UseSqlite("Data Source=:memory:").Options;
        var db = new TestApplicationDbContext(opciones);
        await db.Database.OpenConnectionAsync();
        await db.Database.BeginTransactionAsync();
        return db;
    }

    [Fact]
    public async Task Una_falla_que_deja_la_transaccion_viva_vuelve_al_punto_y_devuelve_alFallar()
    {
        await using var db = await ContextoConTransaccionAsync();

        var r = await PuntoDeGuardado.EjecutarAsync<string>(db, "prueba", () => throw new InvalidOperationException("choque"),
            e => $"falló: {e.Message}", CancellationToken.None);

        r.Should().Be("falló: choque");
        db.Database.CurrentTransaction.Should().NotBeNull("la transacción de afuera sigue y decide ella");
    }

    [Fact]
    public async Task Una_falla_que_se_llevo_la_transaccion_relanza_la_original_para_que_se_repita_la_unidad_entera()
    {
        await using var db = await ContextoConTransaccionAsync();
        var llamadasAlFallar = 0;

        var acto = () => PuntoDeGuardado.EjecutarAsync<string>(db, "prueba", async () =>
        {
            // Lo que hace SQL Server con la víctima de un interbloqueo: deshace la transacción entera, con sus puntos.
            await db.Database.ExecuteSqlRawAsync("ROLLBACK");
            throw new TimeoutException("víctima del interbloqueo");
        }, e => { llamadasAlFallar++; return "falló"; }, CancellationToken.None);

        (await acto.Should().ThrowAsync<TimeoutException>()).WithMessage("víctima del interbloqueo");
        llamadasAlFallar.Should().Be(0, "sin transacción no hay de qué seguir: decide la estrategia de ejecución, no quien llamó al punto");
    }
}
