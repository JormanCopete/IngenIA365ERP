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
/// Feature 013, US3 (bloque 7C, T0449; T15, T16): <see cref="DestinoCartera"/> queda registrado en <c>AddApplication</c> sin cambiar lo que viaja de
/// Comercial. El destino <c>Lending</c> cuenta sólo cuando su consumidor acepta <b>todo</b> lo que adopta la fila <c>CAR</c>
/// (<c>RegistroDeIntegracion.ImplementacionDisponible</c>); la semilla la deja adoptando también la familia de Comercial
/// (<c>VentaACreditoRegistrada</c> v1/v2, <c>AjusteDeVentaACredito</c> v1), que DestinoCartera no acepta hasta US6. Así las ventas a crédito
/// siguen esperando —sin intentos ni rechazos por versión— y el <c>ResultadoDelGiro</c> también, hasta que la cooperativa adopte sólo lo que
/// Cartera acepta o US6 traiga su familia. (nuevo)
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
    public void Acepta_el_resultado_del_giro_y_todavia_no_la_familia_de_Comercial()
    {
        _destino.Destino.Should().Be(IntegrationDestinations.Lending);
        _destino.Acepta("ResultadoDelGiro", 1).Should().BeTrue();
        _destino.Acepta("ResultadoDelGiro", 2).Should().BeFalse();
        _destino.Acepta("VentaACreditoRegistrada", 1).Should().BeFalse("la familia de Comercial llega con US6");
        _destino.Acepta("VentaACreditoRegistrada", 2).Should().BeFalse();
        _destino.Acepta("AjusteDeVentaACredito", 1).Should().BeFalse();
    }

    [Fact]
    public async Task Con_la_fila_CAR_de_la_semilla_el_destino_no_cuenta_y_lo_de_Comercial_sigue_esperando()
    {
        SembrarCar(("VentaACreditoRegistrada", 1), ("VentaACreditoRegistrada", 2), ("AjusteDeVentaACredito", 1), ("ResultadoDelGiro", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeFalse(
            "el consumidor no acepta todo lo que la fila adopta: el despachador no le entrega nada");
        var fila = await _db.IntegrationParticipants.Include(p => p.Adoptions).SingleAsync();
        Registro().ImplementacionDisponible(fila, Hoy).Should().BeFalse();
    }

    [Fact]
    public async Task Si_la_fila_adopta_solo_lo_que_Cartera_acepta_el_destino_cuenta()
    {
        SembrarCar(("ResultadoDelGiro", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeTrue();
    }
}
