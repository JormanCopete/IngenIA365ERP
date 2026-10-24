using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T15; T0037, T0076; contracts/mensajes.md §13.1): cada destino de la bandeja tiene un solo consumidor; el
/// destino <c>Accounting</c> reparte por módulo de origen dentro de su único consumidor, no con dos. Dos <c>IDestinoDeMensajes</c> con el
/// mismo destino hacen fallar el arranque con <c>Integration.Destination.DuplicateConsumer</c> (<c>ConsumidoresPorDestino</c>, que
/// <c>Program.cs</c> llama al construir la aplicación). Hoy sólo <c>Accounting</c> tiene consumidor; <c>Lending</c>, <c>Inventory</c>,
/// <c>Treasury</c> y <c>Receipts</c> lo reciben en sus fases (y los dobles ocupan el destino del real: a lo sumo uno).
/// </summary>
public class UnSoloConsumidorPorDestino
{
    /// <summary>Destinos (valor de <c>IntegrationDestinations</c>) que deben tener exactamente un consumidor.</summary>
    private static readonly string[] Destinos =
    [
        IntegrationDestinations.Accounting,
        // US3 (bloque 7C, T0428): DestinoCartera; el de Treasury es el doble (TesoreriaDePrueba) hasta que exista Tesorería.
        IntegrationDestinations.Lending,
        // US5 (T0526): los dobles ocupan el destino del real hasta que exista: TesoreriaDePrueba el de Treasury y RecaudadorDePrueba el de
        // Receipts (sólo fuera de producción; en producción esos destinos no tienen consumidor hasta que lleguen Tesorería y Recaudo).
        IntegrationDestinations.Treasury,
        IntegrationDestinations.Receipts,
        // US6 (T0577, T0598; CH9): DestinoInventario, el de Comercial, que guarda ObligacionCreada y ObligacionRechazada.
        IntegrationDestinations.Inventory,
    ];

    private static readonly string[] DoblesPendientesDeRetiro = ["TesoreriaDePrueba", "RecaudadorDePrueba"];

    private static IReadOnlyList<string> TodosLosDestinos() => typeof(IntegrationDestinations)
        .GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();

    private static int Consumidores(IReadOnlyList<string> fuentes, string destino)
    {
        var nombre = typeof(IntegrationDestinations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .First(f => (string)f.GetRawConstantValue()! == destino).Name;
        var patron = new Regex($@"\bDestino\s*=>\s*(IntegrationDestinations\.{Regex.Escape(nombre)}\b|""{Regex.Escape(destino)}"")");
        return fuentes.Count(f => patron.IsMatch(f));
    }

    [Fact]
    public void Cada_destino_tiene_a_lo_sumo_un_consumidor_y_los_de_hoy_exactamente_uno()
    {
        // Fase 7 (T0132, T0141): mientras existan los dobles de la 013 (hasta la fase 19) su fuente no cuenta: la bandera transitoria registra el doble o el
        // real, nunca los dos (lo vigila ConsumidoresPorDestino.Validar() al arrancar).
        var fuentes = RepoPath.ProductionCSharpFiles().Where(f => !DoblesPendientesDeRetiro.Any(d => f.EndsWith(d + ".cs", StringComparison.Ordinal)))
            .Select(File.ReadAllText).ToList();
        var infracciones = new List<string>();
        foreach (var destino in TodosLosDestinos())
        {
            var consumidores = Consumidores(fuentes, destino);
            var esperado = Destinos.Contains(destino);
            if (consumidores > 1 || (esperado && consumidores != 1)) infracciones.Add($"{destino}: {consumidores} consumidores");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Un destino no tiene exactamente un consumidor (T15)");
    }

    [Fact]
    public void Dos_consumidores_del_mismo_destino_hacen_fallar_el_arranque()
    {
        var falla = Assert.Throws<InvalidOperationException>(() => ConsumidoresPorDestino.Validar(
            [new Consumidor(IntegrationDestinations.Treasury), new Consumidor(IntegrationDestinations.Treasury), new Consumidor(IntegrationDestinations.Accounting)]));

        Assert.Contains("Integration.Destination.DuplicateConsumer", falla.Message, StringComparison.Ordinal);
        Assert.Contains(IntegrationDestinations.Treasury, falla.Message, StringComparison.Ordinal);
        ConsumidoresPorDestino.Validar([new Consumidor(IntegrationDestinations.Treasury), new Consumidor(IntegrationDestinations.Accounting)]);
    }

    [Fact]
    public void El_arranque_valida_los_consumidores()
    {
        var programa = ReglaSobreFuente.ArchivosNombrados(["Presentation/IngenIA365ERP.API/Program.cs"]).Single();
        Assert.Matches(@"ConsumidoresPorDestino\.Validar\(", File.ReadAllText(programa));
    }

    /// <summary>
    /// US7 (T0609; contracts/contabilidad.md §6, C82): el único consumidor del destino <c>Accounting</c> es <c>DestinoContabilidad</c>, que vive
    /// en lo común del consumidor contable (<c>Application/Accounting/Integration</c>) y reparte por módulo de origen: Inventario y Cartera no
    /// tienen consumidor propio.
    /// </summary>
    [Fact]
    public void El_destino_Accounting_lo_consume_solo_DestinoContabilidad_en_la_carpeta_comun()
    {
        var aplicacion = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        var consumidores = aplicacion.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDestinoDeMensajes).IsAssignableFrom(t))
            .ToList();
        var destino = Assert.Single(consumidores, t => t.Name == "DestinoContabilidad");
        Assert.Equal("IngenIA365ERP.Application.Accounting.Integration", destino.Namespace);

        var fuentes = RepoPath.ProductionCSharpFiles()
            .Where(f => Consumidores([File.ReadAllText(f)], IntegrationDestinations.Accounting) > 0)
            .Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
            .ToList();
        var unico = Assert.Single(fuentes);
        Assert.EndsWith("Application/Accounting/Integration/DestinoContabilidad.cs", unico, StringComparison.Ordinal);
    }

    private sealed class Consumidor(string destino) : IDestinoDeMensajes
    {
        public string Destino => destino;

        public bool Acepta(string type, int version) => true;

        public Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ResultadoDeUnidad>>([]);

        public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas) => [];
    }
}
