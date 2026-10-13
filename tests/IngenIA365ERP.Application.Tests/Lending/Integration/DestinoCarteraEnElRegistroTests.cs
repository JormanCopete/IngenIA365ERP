using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US3 (bloque 7C, T0449; T15, T16) y US6 (bloque 10A, T0586; C16): <see cref="DestinoCartera"/> queda registrado en <c>AddApplication</c>. El
/// destino <c>Lending</c> cuenta sólo cuando su consumidor acepta <b>todo</b> lo que adopta la fila <c>CAR</c>
/// (<c>RegistroDeIntegracion.ImplementacionDisponible</c>). Desde US6 acepta la familia de Comercial en toda versión (<c>VentaACreditoRegistrada</c>,
/// <c>AjusteDeVentaACredito</c>) además del <c>ResultadoDelGiro</c> v1: con la fila de la semilla el destino cuenta y el despachador le entrega las
/// ventas a crédito y el resultado del giro; lo que todavía no acepta (los descuentos de pagaduría de K2) lo deja esperando. (nuevo)
/// </summary>
public class DestinoCarteraEnElRegistroTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly DestinoCartera _destino = new(Substitute.For<ISender>(), NullLogger<DestinoCartera>.Instance);

    private RegistroDeIntegracion Registro() => new(_db, [_destino], _ambiente);

    private void SembrarCar(params (string Contrato, short Version)[] adopciones)
    {
        var p = new IntegrationParticipant
        {
            Code = "CAR", Class = IntegrationParticipantClass.Lending, Role = IntegrationParticipantRole.MessageDestination,
            Destination = IntegrationDestinations.Lending, IsEnabled = true, ValidFrom = Hoy.AddDays(-1), Reason = "Activación de Cartera",
            RegisteredByKind = ActorKind.Process, RegisteredByName = "Migración PlataformaParaCartera",
        };
        foreach (var (contrato, version) in adopciones)
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = p.ValidFrom, Reason = "Semilla" });
        _db.IntegrationParticipants.Add(p);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public void Acepta_el_resultado_del_giro_y_la_familia_de_Comercial_en_toda_version()
    {
        _destino.Destino.Should().Be(IntegrationDestinations.Lending);
        _destino.Acepta("ResultadoDelGiro", 1).Should().BeTrue();
        _destino.Acepta("ResultadoDelGiro", 2).Should().BeFalse();
        _destino.Acepta("VentaACreditoRegistrada", 1).Should().BeTrue();
        _destino.Acepta("VentaACreditoRegistrada", 2).Should().BeTrue();
        _destino.Acepta("VentaACreditoRegistrada", 7).Should().BeTrue("una versión desconocida la responde el consumidor, no el despachador (C16)");
        _destino.Acepta("AjusteDeVentaACredito", 1).Should().BeTrue();
        _destino.Acepta("EnvioDeDescuentosConfirmado", 1).Should().BeFalse("llega con K2");
    }

    [Fact]
    public async Task Con_la_fila_CAR_de_la_semilla_el_destino_cuenta_y_le_llega_lo_de_Comercial()
    {
        SembrarCar(("VentaACreditoRegistrada", 1), ("VentaACreditoRegistrada", 2), ("AjusteDeVentaACredito", 1), ("ResultadoDelGiro", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeTrue(
            "el consumidor acepta todo lo que la fila adopta: el despachador le entrega las ventas a crédito y el resultado del giro");
        var fila = await _db.IntegrationParticipants.Include(p => p.Adoptions).SingleAsync();
        Registro().ImplementacionDisponible(fila, Hoy).Should().BeTrue();
    }

    [Fact]
    public async Task Si_la_fila_adopta_algo_que_Cartera_todavia_no_acepta_el_destino_no_cuenta()
    {
        SembrarCar(("VentaACreditoRegistrada", 2), ("EnvioDeDescuentosConfirmado", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeFalse();
    }

    [Fact]
    public async Task Si_la_fila_adopta_solo_lo_que_Cartera_acepta_el_destino_cuenta()
    {
        SembrarCar(("ResultadoDelGiro", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeTrue();
    }
}
