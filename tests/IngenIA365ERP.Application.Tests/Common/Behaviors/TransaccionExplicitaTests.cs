using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Behaviors;

/// <summary>
/// T052 (decisiones-transversales T14): lo que se puede ver sin un motor real. La transacción anidada que se
/// une a la de afuera y la repetición entera tras un interbloqueo (40P01/1205) las prueba la e2e
/// <c>ArrendamientosYTransaccionTests</c> en los dos motores.
/// </summary>
public class TransaccionExplicitaTests
{
    [Fact]
    public async Task Sin_DbContext_detras_corre_el_trabajo_tal_cual()
    {
        var db = Substitute.For<IApplicationDbContext>();

        var r = await TransaccionExplicita.EjecutarAsync(db, () => Task.FromResult(Result.Success(5)), CancellationToken.None);

        r.Value.Should().Be(5);
        db.DidNotReceive().DescartarCambios();
    }

    [Fact]
    public async Task Devuelve_lo_que_devuelve_el_trabajo_y_guarda_lo_confirmado()
    {
        using var db = TestDbContextFactory.Create();
        var clave = Guid.NewGuid();

        var r = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            db.OperationKeys.Add(new OperationKey { Key = clave, Operation = "Prueba", RequestSha256 = new string('0', 64), ActorName = "Ana" });
            await db.SaveChangesAsync();
            return Result.Success();
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        (await db.OperationKeys.AnyAsync(k => k.Key == clave)).Should().BeTrue();
    }

    [Fact]
    public async Task Una_excepcion_del_trabajo_sube()
    {
        using var db = TestDbContextFactory.Create();

        var acto = () => TransaccionExplicita.EjecutarAsync<Result>(db, () => throw new InvalidOperationException("falla"), CancellationToken.None);

        await acto.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------------------------------------------------------------- feature 013 (T0102, DE-02, T91) --

    [Fact]
    public void HayEnCurso_es_falso_sin_DbContext_detras()
    {
        TransaccionExplicita.HayEnCurso(Substitute.For<IApplicationDbContext>()).Should().BeFalse();
    }

    [Fact]
    public async Task HayEnCurso_es_falso_en_InMemory_dentro_y_fuera_de_EjecutarAsync()
    {
        using var db = TestDbContextFactory.Create();
        TransaccionExplicita.HayEnCurso(db).Should().BeFalse("fuera no hay transacción");

        var adentro = await TransaccionExplicita.EjecutarAsync(db, () => Task.FromResult(Result.Success(TransaccionExplicita.HayEnCurso(db))), CancellationToken.None);

        adentro.Value.Should().BeFalse("InMemory no tiene transacciones: las pruebas que necesitan una sustituyen el puerto de Cartera");
    }

    [Fact]
    public async Task HayEnCurso_es_verdadero_dentro_de_EjecutarAsync_con_un_motor_relacional_y_falso_al_salir()
    {
        var opciones = new DbContextOptionsBuilder<TestApplicationDbContext>().UseSqlite("Data Source=:memory:").Options;
        await using var db = new TestApplicationDbContext(opciones);

        TransaccionExplicita.HayEnCurso(db).Should().BeFalse();
        var adentro = await TransaccionExplicita.EjecutarAsync(db, () => Task.FromResult(Result.Success(TransaccionExplicita.HayEnCurso(db))), CancellationToken.None);

        adentro.Value.Should().BeTrue("dentro de EjecutarAsync la transacción está en curso: el contrato de obligaciones puede escribir");
        TransaccionExplicita.HayEnCurso(db).Should().BeFalse("al confirmar se cierra");
    }
}
