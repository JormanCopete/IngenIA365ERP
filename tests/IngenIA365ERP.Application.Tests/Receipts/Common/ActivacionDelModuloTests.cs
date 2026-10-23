using FluentAssertions;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Common;

/// <summary>
/// Feature 015, T0019 (FR-150, FR-159): con <c>RCD_Setup.Status = NotActivated</c> toda escritura de negocio de Recaudo responde
/// <c>Receipts.NotActivated</c> sin llegar al manejador, y con <c>TES_Setup</c> igual, <c>Treasury.NotActivated</c>; la configuración y la puesta
/// en marcha (comandos sin la marca) sí se admiten antes de activar. Lo hace <see cref="ModuloActivoBehavior{TRequest,TResponse}"/>.
/// </summary>
public class ActivacionDelModuloTests
{
    private sealed record EscrituraDeCaja : IRequest<Result<int>>, IEscrituraDeRecaudo;

    private sealed record EscrituraDeGiro : IRequest<Result>, IEscrituraDeTesoreria;

    private sealed record ConfiguracionDeCaja : IRequest<Result>;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private void Estados(ReceiptsModuleStatus recaudo, TreasuryModuleStatus tesoreria)
    {
        _db.ReceiptsSetups.Add(new ReceiptsSetup { Status = recaudo });
        _db.TreasurySetups.Add(new TreasurySetup { Status = tesoreria });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Sin_activar_una_escritura_de_Recaudo_responde_NotActivated_sin_llegar_al_manejador()
    {
        Estados(ReceiptsModuleStatus.NotActivated, TreasuryModuleStatus.NotActivated);
        var llego = false;

        var r = await new ModuloActivoBehavior<EscrituraDeCaja, Result<int>>(_db)
            .Handle(new EscrituraDeCaja(), _ => { llego = true; return Task.FromResult(Result.Success(1)); }, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoActivado);
        llego.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_activar_una_escritura_de_Tesoreria_responde_NotActivated()
    {
        Estados(ReceiptsModuleStatus.Active, TreasuryModuleStatus.NotActivated);

        var r = await new ModuloActivoBehavior<EscrituraDeGiro, Result>(_db)
            .Handle(new EscrituraDeGiro(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoNoActivado);
    }

    [Fact]
    public async Task Activado_el_modulo_la_escritura_pasa()
    {
        Estados(ReceiptsModuleStatus.Active, TreasuryModuleStatus.Active);

        (await new ModuloActivoBehavior<EscrituraDeCaja, Result<int>>(_db)
            .Handle(new EscrituraDeCaja(), _ => Task.FromResult(Result.Success(7)), CancellationToken.None)).Value.Should().Be(7);
        (await new ModuloActivoBehavior<EscrituraDeGiro, Result>(_db)
            .Handle(new EscrituraDeGiro(), _ => Task.FromResult(Result.Success()), CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_configuracion_y_la_puesta_en_marcha_se_admiten_antes_de_activar()
    {
        Estados(ReceiptsModuleStatus.NotActivated, TreasuryModuleStatus.NotActivated);

        var r = await new ModuloActivoBehavior<ConfiguracionDeCaja, Result>(_db)
            .Handle(new ConfiguracionDeCaja(), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_la_fila_de_configuracion_tampoco_hay_activacion()
    {
        var r = await new ModuloActivoBehavior<EscrituraDeCaja, Result<int>>(_db)
            .Handle(new EscrituraDeCaja(), _ => Task.FromResult(Result.Success(1)), CancellationToken.None);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoActivado);
    }
}
