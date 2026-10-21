using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Enums.Alerts;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Alerts;

/// <summary>
/// T0034 (feature 013; C68, C74; data-model parte 3 §4.14, §11; SC-015, SC-022): las alertas con alcance de Cartera y con módulo de
/// origen. <c>IAlertas.LevantarAsync</c> guarda la oficina y la línea; de la lista del tipo, el origen <c>CAR</c> descarta
/// <c>Inventory.*</c> y <c>Accounting.Inventory*</c> y el origen <c>INV</c> descarta <c>Lending.*</c> y <c>Accounting.Lending*</c>; sin
/// destinatario activo la alerta va a <c>CompanyAdmin</c> con <c>WithoutRecipient</c>; y la semilla le da a los tres tipos reutilizados
/// de la 012 una versión nueva con la unión de destinatarios, sin perder los de Inventario ni duplicarse al resembrar.
/// </summary>
public class AlertasConAlcanceYModuloTests : IDisposable
{
    private static readonly DateTime Ahora = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Despliegue = new(2026, 10, 6);
    private static readonly Guid Oficina = Guid.NewGuid();
    private static readonly Guid Linea = Guid.NewGuid();
    private static readonly Guid Mensaje = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDestinatariosPorPermiso _destinatarios = Substitute.For<IDestinatariosPorPermiso>();
    private readonly IFiltroDeAlcanceDeAlertas _alcance = Substitute.For<IFiltroDeAlcanceDeAlertas>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<SendNotificationCommand> _enviadas = [];

    private static readonly DestinatarioDeAlerta Analista = new(7, Guid.NewGuid(), "Analista de cartera", "analista@coop.test");
    private static readonly DestinatarioDeAlerta Administrador = new(1, Guid.NewGuid(), "Admin", "admin@coop.test");

    public AlertasConAlcanceYModuloTests()
    {
        AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None, Despliegue).GetAwaiter().GetResult();
        _reloj.UtcNow.Returns(Ahora);
        _reloj.HoyLocal.Returns(Despliegue);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Actor.ProcesoDeIntegracion("Tarea:prueba"));
        _destinatarios.ResolverAsync(Arg.Is<IReadOnlyCollection<string>>(p => p.Count > 0), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new DestinatariosDeAlerta([Analista], SinDestinatario: false));
        _destinatarios.ResolverAsync(Arg.Is<IReadOnlyCollection<string>>(p => p.Count == 0), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(new DestinatariosDeAlerta([Administrador], SinDestinatario: true));
        _alcance.FiltrarAsync(Arg.Any<IReadOnlyList<DestinatarioDeAlerta>>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.ArgAt<IReadOnlyList<DestinatarioDeAlerta>>(0)));
        _sender.Send(Arg.Do<SendNotificationCommand>(_enviadas.Add), Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    public void Dispose() => _db.Dispose();

    private Alertas Alertas() => new(_db, _destinatarios, _actor, _reloj, _sender, NullLogger<Alertas>.Instance, _alcance);

    private IReadOnlyCollection<string> PermisosPedidos() =>
        (IReadOnlyCollection<string>)_destinatarios.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IDestinatariosPorPermiso.ResolverAsync))
            .Select(c => c.GetArguments()[0]!)
            .First();

    [Fact]
    public async Task Guarda_la_oficina_y_la_linea_del_alcance_y_se_las_pasa_al_filtro()
    {
        var r = await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.UsuraTopada, "Usura topada", "El crédito 123 topó la tasa en la usura.",
            EntityType: "Loan", EntityPublicId: Guid.NewGuid(), ModuloDeOrigen: ModulosDeOrigen.Cartera,
            ScopeBranchPublicId: Oficina, ScopeCreditLinePublicId: Linea), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var alerta = await _db.Alerts.SingleAsync();
        alerta.ScopeBranchPublicId.Should().Be(Oficina);
        alerta.ScopeCreditLinePublicId.Should().Be(Linea);
        alerta.Module.Should().Be("Lending");
        await _alcance.Received(1).FiltrarAsync(Arg.Any<IReadOnlyList<DestinatarioDeAlerta>>(), Oficina, Linea, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_origen_Cartera_se_descartan_los_destinatarios_de_Inventario()
    {
        await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.MensajeRechazado, "Rechazado", "Contabilidad rechazó el hecho.",
            EntityPublicId: Mensaje, ModuloDeOrigen: ModulosDeOrigen.Cartera), CancellationToken.None);

        PermisosPedidos().Should().BeEquivalentTo(["Lending.Messages.Reprocess", "Accounting.LendingRules.Manage"]);
    }

    [Fact]
    public async Task Con_origen_Inventario_se_descartan_los_destinatarios_de_Cartera()
    {
        await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.MensajeSinEntregar, "Sin entregar", "Tres intentos sin entregar.",
            EntityPublicId: Mensaje, ModuloDeOrigen: ModulosDeOrigen.Inventario), CancellationToken.None);

        PermisosPedidos().Should().BeEquivalentTo(["Inventory.Messages.Reprocess", "Accounting.InventoryBatches.Run"]);
    }

    [Fact]
    public async Task Sin_modulo_de_origen_la_lista_del_tipo_va_entera_y_una_lista_explicita_no_se_filtra()
    {
        await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.MensajeRechazado, "Rechazado", "x", EntityPublicId: Mensaje), CancellationToken.None);
        PermisosPedidos().Should().HaveCount(4);

        _destinatarios.ClearReceivedCalls();
        await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.ValidacionFallida, "Validación", "x", EntityPublicId: Guid.NewGuid(),
            RecipientPermissions: ["Lending.Messages.Reprocess", "Inventory.Messages.Reprocess"], ModuloDeOrigen: ModulosDeOrigen.Inventario), CancellationToken.None);
        PermisosPedidos().Should().BeEquivalentTo(["Lending.Messages.Reprocess", "Inventory.Messages.Reprocess"]);
    }

    [Fact]
    public async Task Si_el_modulo_de_origen_no_deja_destinatarios_va_a_CompanyAdmin_sin_destinatario()
    {
        // VentaBajoCosto sólo tiene destinatarios de Inventario: con origen Cartera no queda ninguno.
        var r = await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.VentaBajoCosto, "Bajo costo", "x",
            EntityPublicId: Guid.NewGuid(), ModuloDeOrigen: ModulosDeOrigen.Cartera), CancellationToken.None);

        r.Value.WithoutRecipient.Should().BeTrue();
        (await _db.Alerts.SingleAsync()).WithoutRecipient.Should().BeTrue();
        _enviadas.Should().ContainSingle().Which.Payload.RecipientUserPublicId.Should().Be(Administrador.UserPublicId);
    }

    [Fact]
    public async Task Si_nadie_tiene_el_alcance_de_la_oficina_va_a_CompanyAdmin_sin_destinatario()
    {
        _alcance.FiltrarAsync(Arg.Any<IReadOnlyList<DestinatarioDeAlerta>>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DestinatarioDeAlerta>>([]));

        var r = await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.GarantiaPorVencer, "Garantía por vencer", "La póliza vence en 20 días.",
            EntityPublicId: Guid.NewGuid(), ModuloDeOrigen: ModulosDeOrigen.Cartera, ScopeBranchPublicId: Oficina), CancellationToken.None);

        r.Value.WithoutRecipient.Should().BeTrue();
        r.Value.RecipientCount.Should().Be(1);
        _enviadas.Should().ContainSingle().Which.Payload.RecipientUserPublicId.Should().Be(Administrador.UserPublicId);
    }

    [Fact]
    public async Task Sin_alcance_de_Cartera_el_filtro_no_se_consulta()
    {
        await Alertas().LevantarAsync(new AlertaALevantar(TiposDeAlerta.Quiebre, "Quiebre", "x", EntityPublicId: Guid.NewGuid()), CancellationToken.None);

        await _alcance.DidNotReceiveWithAnyArgs().FiltrarAsync(default!, default, default, default);
    }

    // ---------------------------------------------------------------------------------------------- catálogo --

    [Fact]
    public void Los_tipos_que_ya_levantan_los_bloques_anteriores_estan_en_el_catalogo()
    {
        string[] usados =
        [
            AlertasDeValoresNacionales.ValorNacionalFaltante, AlertasDeValoresNacionales.ReplicaAtrasada,
            AlertasDeValoresNacionales.IndiceDesactualizado, AlertasDeValoresNacionales.NormativaPendienteDeValidar,
            AlertasDeValoresNacionales.LimiteLegalMasEstricto, AlertasDeValoresNacionales.ValorNacionalDistinto,
            IngenIA365ERP.Application.Core.RegulatoryProfile.AlertasDelPerfilRegulatorio.GrupoNiifDistinto,
            IngenIA365ERP.ExternalChannels.Circuit.CircuitoDeCanalExterno.TipoDeAlerta,
            TiposDeAlerta.ValidacionFallida, TiposDeAlerta.VentaNoIncorporada, TiposDeAlerta.TareaDiariaNoCorrio,
            TiposDeAlerta.PlazoDelTitularPorVencer, TiposDeAlerta.CoincidenciaEnListas, TiposDeAlerta.LibroDescuadrado,
        ];

        usados.Should().OnlyContain(t => TiposDeAlerta.Buscar(t) != null);
    }

    [Fact]
    public void Los_destinatarios_por_defecto_no_son_de_consulta_y_las_entregas_de_Cartera_son_K()
    {
        TiposDeAlerta.Todos.SelectMany(t => t.Destinatarios).Should().NotContain(p => p.EndsWith(".View", StringComparison.Ordinal));
        TiposDeAlerta.Todos.Where(t => t.Module == "Lending").Should().OnlyContain(t => t.DisponibleDesde.StartsWith('K'));
        TiposDeAlerta.Todos.Select(t => t.TypeCode).Should().OnlyHaveUniqueItems();
        TiposDeAlerta.Todos.Should().OnlyContain(t => t.Module.Length <= 20 && t.TypeCode.Length <= 60);
    }

    [Fact]
    public async Task La_garantia_por_vencer_se_siembra_con_treinta_dias_de_anticipacion()
    {
        var garantia = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.GarantiaPorVencer);

        ProyeccionDeUmbrales(garantia.ThresholdsJson).Should().Contain(TiposDeAlerta.DiasDeAnticipacion, 30m);
    }

    [Fact]
    public async Task El_libro_descuadrado_se_siembra_para_cartera_y_la_contadora()
    {
        // US4 (bloque 8C): la verificación del libro tiene su tipo propio; antes se levantaba como Cartera.CreditoSinCausar con «…:libro».
        var libro = await _db.AlertTypes.SingleAsync(t => t.TypeCode == TiposDeAlerta.LibroDescuadrado);

        libro.Module.Should().Be("Lending");
        libro.Permisos().Should().BeEquivalentTo(["Lending.Processes.Run", "Accounting.LendingRules.Manage"]);
        libro.Severity.Should().Be(IngenIA365ERP.Domain.Enums.Alerts.AlertSeverity.Critical);
        libro.Channels.Should().HaveFlag(IngenIA365ERP.Domain.Enums.Alerts.AlertChannels.Email);
        TiposDeAlerta.Buscar(TiposDeAlerta.LibroDescuadrado)!.DisponibleDesde.Should().Be("K1");
    }

    private static Dictionary<string, decimal> ProyeccionDeUmbrales(string? json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(json!)!;

    // ---------------------------------------------------------------------------------------------- semilla --

    [Fact]
    public async Task En_una_cooperativa_nueva_los_reutilizados_nacen_con_la_union_y_sin_version_extra()
    {
        var rechazado = await _db.AlertTypes.Where(t => t.TypeCode == TiposDeAlerta.MensajeRechazado).ToListAsync();

        rechazado.Should().ContainSingle();
        rechazado[0].Permisos().Should().BeEquivalentTo(
            ["Inventory.Messages.Reprocess", "Accounting.InventoryRules.Manage", "Lending.Messages.Reprocess", "Accounting.LendingRules.Manage"]);
        (await AlertTypesSeeder.AplicarAsync(_db, CancellationToken.None, Despliegue)).Should().Be(0);
    }

    [Fact]
    public async Task En_una_cooperativa_de_la_012_los_reutilizados_reciben_una_version_nueva_con_la_union_y_no_se_duplica()
    {
        using var db = TestDbContextFactory.Create();
        // Lo que dejó la semilla de la 012, con un destinatario que la cooperativa agregó y el correo apagado.
        foreach (var (tipo, permisos) in new (string, string[])[]
                 {
                     (TiposDeAlerta.ValidacionFallida, ["Inventory.Sales.Approve"]),
                     (TiposDeAlerta.MensajeRechazado, ["Inventory.Messages.Reprocess", "Accounting.InventoryRules.Manage", "Inventory.Prices.Manage"]),
                     (TiposDeAlerta.MensajeSinEntregar, ["Inventory.Messages.Reprocess", "Accounting.InventoryBatches.Run"]),
                 })
        {
            db.AlertTypes.Add(new AlertType
            {
                TypeCode = tipo, Module = "Integration", RecipientPermissions = AlertType.PermisosComoJson(permisos),
                Channels = AlertChannels.InApp, Severity = AlertSeverity.Warning, IsEnabled = true,
                ValidFrom = AlertTypesSeeder.VigenciaAbierta, Reason = AlertTypesSeeder.MotivoDeLaSemilla,
            });
        }
        await db.SaveChangesAsync();

        var insertadas = await AlertTypesSeeder.AplicarAsync(db, CancellationToken.None, Despliegue);

        var rechazado = await db.AlertTypes.Where(t => t.TypeCode == TiposDeAlerta.MensajeRechazado).OrderBy(t => t.ValidFrom).ToListAsync();
        rechazado.Should().HaveCount(2);
        rechazado[0].ValidTo.Should().Be(Despliegue.AddDays(-1), "la anterior cierra la víspera");
        rechazado[1].ValidFrom.Should().Be(Despliegue);
        rechazado[1].Reason.Should().Be(AlertTypesSeeder.MotivoDeCartera);
        rechazado[1].Channels.Should().Be(AlertChannels.InApp, "lo que la cooperativa configuró se conserva");
        rechazado[1].Permisos().Should().BeEquivalentTo(
            ["Inventory.Messages.Reprocess", "Accounting.InventoryRules.Manage", "Inventory.Prices.Manage", "Lending.Messages.Reprocess", "Accounting.LendingRules.Manage"]);
        (await db.AlertTypes.Where(t => t.TypeCode == TiposDeAlerta.ValidacionFallida && t.ValidTo == null).SingleAsync()).Permisos()
            .Should().BeEquivalentTo(["Inventory.Sales.Approve", "Lending.ValidationFailures.Confirm"]);
        insertadas.Should().Be(TiposDeAlerta.Todos.Count - 3 + 3, "los tipos que faltaban y las tres versiones de Cartera");

        (await AlertTypesSeeder.AplicarAsync(db, CancellationToken.None, Despliegue.AddDays(1))).Should().Be(0, "resembrar no duplica");
        (await db.AlertTypes.CountAsync(t => t.TypeCode == TiposDeAlerta.MensajeRechazado)).Should().Be(2);
    }
}
