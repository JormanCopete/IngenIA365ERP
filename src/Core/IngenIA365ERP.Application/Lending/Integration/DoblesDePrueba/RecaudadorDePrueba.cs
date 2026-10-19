using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

/// <summary>
/// El recaudador de prueba (feature 013, US5, T0556; FR-089, T20, C35; api.md §20, mensajes.md §18): el consumidor del destino <c>Receipts</c> fuera de
/// producción y quien ejercita el contrato de obligaciones como lo haría Recaudo, con el código <c>TST</c> y la clase <c>Test</c>. Consume
/// <c>PagoReversadoPorCartera</c> v1 y responde <c>Processed</c> sin estado propio (lo que recibió lo muestra la bandeja,
/// <see cref="ListTestCollectorReversalsQuery"/>); desde K2, <c>DescuentosPorPagaduria</c>. Sólo se registra por <see cref="DoblesDePruebaDeCarteraEnDi"/>
/// (nunca en producción). No es un canal simulado (T73) ni un doble de pruebas unitarias. (nuevo)
/// </summary>
public sealed class RecaudadorDePrueba : IDestinoDeMensajes
{
    /// <summary>El recaudador con que el doble llama al contrato.</summary>
    public static Recaudador Recaudador { get; } = new(ClavesDeRecaudador.Prueba, ClaseDeRecaudador.Test);

    /// <summary>La clave que propone la pantalla: <c>TST:{guid:N}</c> (C35).</summary>
    public static string Clave(Guid guid) => $"{ClavesDeRecaudador.Prueba}:{guid:N}";

    /// <summary>
    /// Feature 014 (decisión del dueño del 2026-10-18, D52; cierra T113): el doble de Tesorería como recaudador de cuentas —código <c>TES</c>, clase
    /// <c>Treasury</c>—, con el que las rutas <c>/collector/treasury/accounts/*</c> consignan transferencias recibidas y las reversan como lo haría
    /// Tesorería. Para que cuente, quien prueba registra el participante <c>TES</c> con rol <c>ObligationsCollector</c>, clase <c>Treasury</c>,
    /// <c>IsTestDouble</c> y <c>ProveedorDeCuentas</c> adoptado (sólo fuera de producción).
    /// </summary>
    public static Recaudador Tesoreria { get; } = new(CodigoDeTesoreria, ClaseDeRecaudador.Treasury);

    /// <summary>El código del doble de Tesorería como recaudador (el mismo de la tesorería de prueba como destino, otro rol).</summary>
    public const string CodigoDeTesoreria = "TES";

    /// <summary>La clave de Tesorería: <c>TES:{documento:N}:{cuenta:N}</c> (contrato-de-cuentas §3.12).</summary>
    public static string ClaveDeTesoreria(Guid documento, Guid cuenta) => $"{CodigoDeTesoreria}:{documento:N}:{cuenta:N}";

    private static readonly HashSet<(string Type, int Version)> Aceptados =
    [
        (PagoReversadoPorCarteraV1.Type, 1),
        // K2 (T0847): el archivo de descuentos de una pagaduría; lo que se hace con él lo ordena quien prueba (confirmar el envío, cargar la respuesta).
        (DescuentosPorPagaduriaV1.Type, 1),
        // Feature 014 (T091, D36): el archivo de un grupo, con créditos y cuotas de cuentas.
        (DescuentosPorPagaduriaV2.Type, 2),
        // Feature 014, US4 (T113; FR-092): la consignación del recaudador que la Cartera reversó; el recaudador anularía su recibo.
        (IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.ConsignacionReversadaPorCarteraV1.Type, 1),
    ];

    public string Destino => IntegrationDestinations.Receipts;

    public bool Acepta(string type, int version) => Aceptados.Contains((type, version));

    public Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        return Task.FromResult<IReadOnlyList<ResultadoDeUnidad>>(trabajo.Unidades
            .Select(u => new ResultadoDeUnidad(u, new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational)))
            .ToList());
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): el recaudador no resume.</summary>
    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        return entregas
            .GroupBy(m => (m.Envelope.OriginModule, m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey))
            .Select(g => TrabajoDeConsumo.DeUnaUnidad(new UnidadDeConsumo(Destino, g.Key.PublicId, g.Key.OriginEventKey,
                g.Select(m => m.MessageId).ToList(),
                g.Select(m => m.Entrega.BatchPublicId).FirstOrDefault(b => b is not null),
                g.Max(m => m.Entrega.Attempts))
            {
                OriginModule = g.Key.OriginModule,
            }))
            .ToList();
    }
}
