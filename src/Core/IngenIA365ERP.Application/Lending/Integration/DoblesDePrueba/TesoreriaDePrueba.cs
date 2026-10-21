using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

/// <summary>
/// La tesorería de prueba (feature 013, T0230, C106; T20, FR-089; contracts/interfaces.md §4.1): el consumidor del destino
/// <c>Treasury</c> fuera de producción. Esqueleto: consume <c>OrdenDeGiroEmitida</c> v1 y <c>AnulacionDeOrdenDeGiro</c> v1 y responde
/// <c>Processed</c> sin comprobante ni estado propio. Con la fila <c>TES</c> de clase <c>Test</c> en el registro, es lo que hace contar el
/// requisito <c>TreasuryDestinationRegistered</c> de la activación fuera de producción.
///
/// <para>
/// Sólo se registra por <see cref="DoblesDePruebaDeCarteraEnDi"/> (que la API llama con <c>IsProduction()</c>); nunca en producción. US3
/// le suma la emisión de <c>ResultadoDelGiro</c> como <c>TES</c> y las rutas <c>/api/lending/test-doubles/treasury/…</c>; US5 agrega el
/// <c>RecaudadorDePrueba</c> sobre la misma guarda. No es un canal simulado (T73) ni un doble de pruebas unitarias. (nuevo)
/// </para>
/// </summary>
public sealed class TesoreriaDePrueba : IDestinoDeMensajes
{
    /// <summary>Los tipos y versiones que consume.</summary>
    private static readonly HashSet<(string Type, int Version)> Aceptados =
    [
        (OrdenDeGiroEmitidaV1.Type, 1),
        (AnulacionDeOrdenDeGiroV1.Type, 1),
        // Feature 014 (T055): la orden de giro v2 de captaciones, para que la tesorería de prueba la adopte (TreasuryDestinationRegistered).
        (OrdenDeGiroEmitidaV2.Type, 2),
        // Decisiones del dueño del 2026-10-18 (D19, D52): Tesorería como recaudador recibe el aviso de lo que la Cartera le reversó.
        (IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.ConsignacionReversadaPorCarteraV1.Type, 1),
        (PagoReversadoPorCarteraV1.Type, 1),
    ];

    public string Destino => IntegrationDestinations.Treasury;

    public bool Acepta(string type, int version) => Aceptados.Contains((type, version));

    public Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        return Task.FromResult<IReadOnlyList<ResultadoDeUnidad>>(trabajo.Unidades
            .Select(u => new ResultadoDeUnidad(u, new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational)))
            .ToList());
    }

    /// <summary>Una unidad por (módulo, origen, clave de evento): la tesorería no resume.</summary>
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
