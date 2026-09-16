using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// Feature 009, R3: un request marcado se reintenta entero ante una carrera de concurrencia,
/// descartando los cambios entre intentos; uno sin marcar no se toca.
/// </summary>
public class ReintentoPorConcurrenciaBehaviorTests
{
    private sealed record PedidoReintentable : IRequest<Result>, IReintentableAnteConcurrencia;

    private sealed record PedidoNormal : IRequest<Result>;

    private static ConcurrencyConflictException Carrera() => new("VoucherType", "abc", "otro@demo");

    [Fact]
    public async Task Reintenta_hasta_que_el_handler_sale_bien_y_descarta_cambios_entre_intentos()
    {
        var db = Substitute.For<IApplicationDbContext>();
        var behavior = new ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>(Servicios(db), NullLogger<ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>>.Instance);
        var llamadas = 0;

        var r = await behavior.Handle(new PedidoReintentable(), _ =>
        {
            llamadas++;
            if (llamadas < 3) throw Carrera();
            return Task.FromResult(Result.Success());
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        llamadas.Should().Be(3);
        db.Received(2).DescartarCambios();
    }

    [Fact]
    public async Task Tras_el_ultimo_intento_deja_pasar_la_excepcion()
    {
        var db = Substitute.For<IApplicationDbContext>();
        var behavior = new ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>(Servicios(db), NullLogger<ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>>.Instance);
        var llamadas = 0;

        var acto = () => behavior.Handle(new PedidoReintentable(), _ => { llamadas++; throw Carrera(); }, CancellationToken.None);

        await acto.Should().ThrowAsync<ConcurrencyConflictException>();
        llamadas.Should().Be(ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>.MaxIntentos);
        db.Received(ReintentoPorConcurrenciaBehavior<PedidoReintentable, Result>.MaxIntentos - 1).DescartarCambios();
    }

    [Fact]
    public async Task Un_request_sin_marcar_no_se_reintenta()
    {
        var db = Substitute.For<IApplicationDbContext>();
        var behavior = new ReintentoPorConcurrenciaBehavior<PedidoNormal, Result>(Servicios(db), NullLogger<ReintentoPorConcurrenciaBehavior<PedidoNormal, Result>>.Instance);
        var llamadas = 0;

        var acto = () => behavior.Handle(new PedidoNormal(), _ => { llamadas++; throw Carrera(); }, CancellationToken.None);

        await acto.Should().ThrowAsync<ConcurrencyConflictException>();
        llamadas.Should().Be(1);
        db.DidNotReceive().DescartarCambios();
    }

    /// <summary>El behavior pide el contexto por el proveedor de servicios, y sólo al reintentar.</summary>
    private static IServiceProvider Servicios(IApplicationDbContext db)
    {
        var sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IApplicationDbContext)).Returns(db);
        return sp;
    }
}
