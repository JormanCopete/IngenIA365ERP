using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T0029, parte de registro (feature 013; T16, T20, C35, C36; contracts/mensajes.md §13; data-model parte 3 §4.6): un participante
/// <b>cuenta</b> si está habilitado y vigente a hoy, su implementación está en el despliegue con la versión adoptada y no es un doble
/// en producción. El escritor rechaza el doble en producción (<c>TestDoubleInProduction</c>), la versión que Cartera no acepta
/// (<c>VersionNotSupported</c> con <c>data: { contract, supported[] }</c>) y los cruces (<c>Overlaps</c>); habilitar cierra la fila
/// vigente la víspera y abre otra; y agrega sin guardar. InMemory con ambiente, actor y reloj falsos.
/// </summary>
public class RegistroDeIntegracionTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Siempre = new(1900, 1, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAmbienteDeDespliegue _ambiente = Substitute.For<IAmbienteDeDespliegue>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly List<IDestinoDeMensajes> _consumidores =
    [
        new DestinoFalso(IntegrationDestinations.Accounting),
        new DestinoFalso(IntegrationDestinations.Lending),
        new DestinoFalso(IntegrationDestinations.Treasury, (tipo, version) => version == 1),
    ];

    public RegistroDeIntegracionTests()
    {
        _ambiente.EsProduccion.Returns(false);
        _reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        _reloj.HoyLocal.Returns(Hoy);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, 3, Guid.NewGuid(), Guid.NewGuid(),
            "Administradora de ensayo", "admin@coop.test", ExecutionChannel.Web, "POST /api/core/integration-participants", "10.0.0.2", null));
    }

    private RegistroDeIntegracion Registro() => new(_db, _consumidores, _ambiente);

    private EscritorDelRegistroDeIntegracion Escritor() => new(_db, _ambiente, _actor, _reloj);

    private IntegrationParticipant Sembrar(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string destino,
        bool habilitado = true, bool doble = false, DateOnly? desde = null, DateOnly? hasta = null, params (string Contrato, short Version)[] adopciones)
    {
        var p = new IntegrationParticipant
        {
            Code = codigo, Class = clase, Role = rol, Destination = destino, IsEnabled = habilitado, IsTestDouble = doble,
            ValidFrom = desde ?? Siempre, ValidTo = hasta, Reason = "Semilla", RegisteredByKind = ActorKind.Process,
            RegisteredByName = "Migración PlataformaParaCartera",
        };
        foreach (var (contrato, version) in adopciones)
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = p.ValidFrom, Reason = "Semilla" });
        _db.IntegrationParticipants.Add(p);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return p;
    }

    private static NuevoParticipante Nuevo(string codigo, IntegrationParticipantClass clase, IntegrationParticipantRole rol, string? destino,
        DateOnly desde, bool habilitado = true, bool doble = false, params VersionAdoptada[] adopciones) =>
        new(codigo, clase, rol, destino, habilitado, doble, adopciones, desde, "Alta de prueba");

    // ------------------------------------------------------------------------------------------ cuándo cuenta --

    [Fact]
    public async Task Cuenta_el_destino_habilitado_vigente_y_con_su_consumidor()
    {
        Sembrar("CNT", IntegrationParticipantClass.Accounting, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Accounting);

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Accounting, Hoy, default)).Should().BeTrue();
        var vigente = await Registro().DestinoAsync(IntegrationDestinations.Accounting, Hoy, default);
        vigente!.Code.Should().Be("CNT");
    }

    [Fact]
    public async Task No_cuenta_el_destino_inhabilitado_fuera_de_vigencia_o_sin_consumidor()
    {
        Sembrar("CAR", IntegrationParticipantClass.Lending, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Lending,
            habilitado: false, adopciones: ("VentaACreditoRegistrada", 1));
        Sembrar("INV", IntegrationParticipantClass.Inventory, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Inventory,
            adopciones: ("ObligacionCreada", 1));
        Sembrar("CNT", IntegrationParticipantClass.Accounting, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Accounting,
            desde: Hoy.AddDays(1));
        Sembrar("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            hasta: Hoy.AddDays(-1), adopciones: ("OrdenDeGiroEmitida", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeFalse("CAR se siembra inhabilitado");
        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Inventory, Hoy, default)).Should().BeFalse("no hay consumidor de Inventory en este despliegue");
        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Accounting, Hoy, default)).Should().BeFalse("su vigencia empieza mañana");
        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Treasury, Hoy, default)).Should().BeFalse("su vigencia terminó ayer");
    }

    [Fact]
    public async Task No_cuenta_si_su_implementacion_no_acepta_la_version_adoptada()
    {
        Sembrar("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            adopciones: ("OrdenDeGiroEmitida", 2));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Treasury, Hoy, default)).Should().BeFalse();
        var fila = await _db.IntegrationParticipants.Include(p => p.Adoptions).SingleAsync();
        Registro().ImplementacionDisponible(fila, Hoy).Should().BeFalse();
    }

    [Fact]
    public async Task Un_doble_cuenta_fuera_de_produccion_y_nunca_en_produccion()
    {
        Sembrar("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            doble: true, adopciones: ("OrdenDeGiroEmitida", 1));

        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Treasury, Hoy, default)).Should().BeTrue();

        _ambiente.EsProduccion.Returns(true);
        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Treasury, Hoy, default)).Should().BeFalse();
    }

    [Fact]
    public async Task Un_recaudador_cuenta_con_un_contrato_que_Cartera_implementa()
    {
        Sembrar("NOM", IntegrationParticipantClass.Payroll, IntegrationParticipantRole.ObligationsCollector, string.Empty,
            adopciones: (ContratosDeIntegracion.ProveedorDeObligaciones, 1));
        Sembrar("APO", IntegrationParticipantClass.Contributions, IntegrationParticipantRole.ObligationsCollector, string.Empty,
            adopciones: (ContratosDeIntegracion.ProveedorDeObligaciones, 9));

        (await Registro().RecaudadorAsync("NOM", Hoy, default))!.Adopciones.Should().Equal(new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1));
        (await Registro().RecaudadorAsync("APO", Hoy, default)).Should().BeNull("Cartera no implementa la versión 9 del contrato");
        (await Registro().QueCuentanAsync(Hoy, default)).Select(p => p.Code).Should().Equal("NOM");
    }

    // ------------------------------------------------------------------------------------------- el escritor --

    [Fact]
    public async Task El_escritor_rechaza_un_doble_en_produccion()
    {
        _ambiente.EsProduccion.Returns(true);

        var porMarca = await Escritor().RegistrarAsync(Nuevo("TST", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, Hoy, doble: true), default);
        var porClase = await Escritor().RegistrarAsync(Nuevo("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.ObligationsCollector,
            null, Hoy), default);

        porMarca.Error.Code.Should().Be("Integration.Participant.TestDoubleInProduction");
        porClase.Error.Code.Should().Be("Integration.Participant.TestDoubleInProduction");
        _db.IntegrationParticipants.Local.Should().BeEmpty();
    }

    [Fact]
    public async Task El_escritor_rechaza_una_version_que_Cartera_no_acepta_con_las_soportadas()
    {
        var r = await Escritor().RegistrarAsync(Nuevo("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, Hoy, adopciones: new VersionAdoptada("OrdenDeGiroEmitida", 3)), default);

        r.Error.Code.Should().Be("Integration.Participant.VersionNotSupported");
        var datos = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        datos.GetType().GetProperty("contract")!.GetValue(datos).Should().Be("OrdenDeGiroEmitida");
        ((IEnumerable<int>)datos.GetType().GetProperty("supported")!.GetValue(datos)!).Should().Equal([1, 2], "feature 014 (T055): la v2 de captaciones");

        var desconocido = await Escritor().RegistrarAsync(Nuevo("NOM", IntegrationParticipantClass.Payroll, IntegrationParticipantRole.ObligationsCollector,
            null, Hoy, adopciones: new VersionAdoptada("ContratoQueNoExiste", 1)), default);
        desconocido.Error.Code.Should().Be("Integration.Participant.VersionNotSupported");
    }

    [Fact]
    public async Task El_escritor_rechaza_cruces_de_la_misma_clave_y_de_dos_destinos_reales()
    {
        Sembrar("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            desde: Hoy, adopciones: ("OrdenDeGiroEmitida", 1));
        Sembrar("NOM", IntegrationParticipantClass.Payroll, IntegrationParticipantRole.ObligationsCollector, string.Empty,
            desde: Siempre, hasta: Hoy.AddDays(10));

        var mismoDia = await Escritor().RegistrarAsync(Nuevo("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, Hoy), default);
        var otroReal = await Escritor().RegistrarAsync(Nuevo("TS2", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, Hoy.AddDays(5)), default);
        var historia = await Escritor().RegistrarAsync(Nuevo("NOM", IntegrationParticipantClass.Payroll, IntegrationParticipantRole.ObligationsCollector,
            null, Hoy), default);
        var doble = await Escritor().RegistrarAsync(Nuevo("TST", IntegrationParticipantClass.Test, IntegrationParticipantRole.MessageDestination,
            IntegrationDestinations.Treasury, Hoy, doble: true), default);

        mismoDia.Error.Code.Should().Be("Integration.Participant.Overlaps");
        otroReal.Error.Code.Should().Be("Integration.Participant.Overlaps", "dos destinos reales habilitados no se cruzan");
        historia.Error.Code.Should().Be("Integration.Participant.Overlaps", "la historia cerrada no se reescribe");
        doble.IsSuccess.Should().BeTrue("un doble ocupa el mismo destino que el real (el arranque no deja que convivan dos consumidores)");
    }

    [Fact]
    public async Task Habilitar_cierra_la_fila_vigente_la_vispera_y_abre_otra_con_sus_adopciones()
    {
        var car = Sembrar("CAR", IntegrationParticipantClass.Lending, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Lending,
            habilitado: false, adopciones: [("VentaACreditoRegistrada", 1), ("ResultadoDelGiro", 1)]);

        var r = await Escritor().CambiarHabilitacionAsync("CAR", IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Lending,
            habilitado: true, Hoy, "Activación de Cartera", default);
        r.IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var filas = await _db.IntegrationParticipants.Include(p => p.Adoptions).OrderBy(p => p.ValidFrom).ToListAsync();
        filas.Should().HaveCount(2);
        filas[0].PublicId.Should().Be(car.PublicId);
        filas[0].IsEnabled.Should().BeFalse();
        filas[0].ValidTo.Should().Be(Hoy.AddDays(-1), "se cierra la víspera: la historia queda");
        filas[0].Adoptions.Should().OnlyContain(a => a.ValidTo == Hoy.AddDays(-1));
        filas[1].IsEnabled.Should().BeTrue();
        filas[1].ValidFrom.Should().Be(Hoy);
        filas[1].ValidTo.Should().BeNull();
        filas[1].Adoptions.Select(a => (a.ContractName, (int)a.Version)).Should().BeEquivalentTo([("VentaACreditoRegistrada", 1), ("ResultadoDelGiro", 1)]);
        filas[1].RegisteredByName.Should().Be("Administradora de ensayo");
        (await Registro().DestinoHabilitadoAsync(IntegrationDestinations.Lending, Hoy, default)).Should().BeTrue();
    }

    [Fact]
    public async Task El_escritor_agrega_sin_guardar()
    {
        var r = await Escritor().RegistrarAsync(Nuevo("tst", IntegrationParticipantClass.Test, IntegrationParticipantRole.ObligationsCollector,
            IntegrationDestinations.Treasury, Hoy, doble: true, adopciones: new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 1)), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Code.Should().Be("TST");
        r.Value.Destination.Should().BeEmpty("un recaudador no tiene destino: el único compara igual en los dos motores (C36)");
        _db.Entry(r.Value).State.Should().Be(EntityState.Added);
        (await _db.IntegrationParticipants.AsNoTracking().CountAsync()).Should().Be(0, "el escritor nunca guarda");
    }

    [Fact]
    public async Task Retirar_cierra_la_fila_sin_guardar()
    {
        var tes = Sembrar("TES", IntegrationParticipantClass.Treasury, IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury,
            adopciones: ("OrdenDeGiroEmitida", 1));

        var r = await Escritor().RetirarAsync(tes.PublicId, Hoy, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.ValidTo.Should().Be(Hoy);
        (await _db.IntegrationParticipants.AsNoTracking().SingleAsync()).ValidTo.Should().BeNull("todavía no se guardó");
        (await Escritor().RetirarAsync(Guid.NewGuid(), Hoy, default)).Error.Code.Should().Be("Integration.Participant.NotFound");
    }

    /// <summary>
    /// Feature 015, R1 (T0352): adoptar la v2 suma las adopciones a la fila vigente —cierra la de antes la víspera y abre otra con las de antes más las
    /// nuevas—, conserva la habilitación y no guarda. Prueba escrita, sin correr.
    /// </summary>
    [Fact]
    public async Task Adoptar_la_v2_cierra_la_fila_vigente_y_abre_otra_con_las_dos_versiones()
    {
        Sembrar("REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, string.Empty, desde: Hoy.AddDays(-10),
            adopciones: [(ContratosDeIntegracion.ProveedorDeObligaciones, 1), (ContratosDeIntegracion.ProveedorDeCuentas, 1)]);

        var r = await Escritor().AdoptarAsync("REC", IntegrationParticipantRole.ObligationsCollector, string.Empty,
            [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeObligaciones, 2), new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 2)],
            Hoy.AddDays(1), "Recaudo adopta la v2", default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.ValidFrom.Should().Be(Hoy.AddDays(1));
        r.Value.Adoptions.Select(a => (a.ContractName, (int)a.Version)).Should().BeEquivalentTo(new[]
        {
            (ContratosDeIntegracion.ProveedorDeObligaciones, 1), (ContratosDeIntegracion.ProveedorDeCuentas, 1),
            (ContratosDeIntegracion.ProveedorDeObligaciones, 2), (ContratosDeIntegracion.ProveedorDeCuentas, 2),
        });
        _db.IntegrationParticipants.Local.Single(p => p.ValidTo is not null).ValidTo.Should().Be(Hoy, "la fila de antes se cierra la víspera");
        (await Escritor().AdoptarAsync("NOEXISTE", IntegrationParticipantRole.ObligationsCollector, string.Empty, [], Hoy, "x", default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Las_claves_del_recaudador_derivan_del_original()
    {
        var clave = "NOM:6f1c2a9e4b7d4c0e9a1f3b5d7c9e1a2b:0d4e6f8a1b3c4d5e6f7a8b9c0d1e2f3a";
        ClavesDeRecaudador.Reverso(clave).Should().Be(clave + ":reverso");
        ClavesDeRecaudador.Destino(clave).Should().Be(clave + ":destino");
        var guid = Guid.Parse("4b5c6d7e-8f90-4a1b-2c3d-4e5f6a7b8c9d");
        ClavesDeRecaudador.DePrueba(guid).Should().Be("TST:4b5c6d7e8f904a1b2c3d4e5f6a7b8c9d", "C35: se retira el prefijo TEST:");
        ClavesDeRecaudador.EsDelRecaudador(clave, ClavesDeRecaudador.Nomina).Should().BeTrue();
        ClavesDeRecaudador.EsDelRecaudador("TEST:abc", ClavesDeRecaudador.Prueba).Should().BeFalse();
    }
}
