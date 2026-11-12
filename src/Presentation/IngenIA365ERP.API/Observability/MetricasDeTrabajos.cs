using IngenIA365ERP.Application.Common.Mensajeria;
using Prometheus;

namespace IngenIA365ERP.API.Observability;

/// <summary>
/// Las métricas de los trabajos de fondo (feature 023, US8; research R8), en formato Prometheus en un puerto aparte (<c>Metricas:Puerto</c>,
/// 9464) que el Ingress no publica. Lo que la base dice por cooperativa —pendientes, edad del más viejo, muertos— lo alimenta el barrido; la
/// profundidad del broker la da el propio RabbitMQ (plugin <c>rabbitmq_prometheus</c>). Ninguna etiqueta lleva datos de negocio: la cola y el
/// nombre de la cooperativa. (nuevo)
/// </summary>
public sealed class MetricasDeTrabajos : IMedidorDeTrabajos
{
    private static readonly Gauge Pendientes = Metrics.CreateGauge("erp_trabajos_pendientes",
        "Trabajos pendientes según la base, por cola y cooperativa.", new GaugeConfiguration { LabelNames = ["cola", "cooperativa"] });

    private static readonly Gauge Edad = Metrics.CreateGauge("erp_trabajos_edad_mas_viejo_segundos",
        "Edad del trabajo pendiente más viejo, por cola y cooperativa.", new GaugeConfiguration { LabelNames = ["cola", "cooperativa"] });

    private static readonly Gauge Muertos = Metrics.CreateGauge("erp_trabajos_muertos",
        "Trabajos que agotaron su tope y esperan reproceso, por cola y cooperativa.", new GaugeConfiguration { LabelNames = ["cola", "cooperativa"] });

    private static readonly Counter Publicaciones = Metrics.CreateCounter("erp_mensajeria_publicaciones_total",
        "Avisos publicados por resultado (confirmada, rechazada, sin-broker).", new CounterConfiguration { LabelNames = ["cola", "resultado"] });

    private static readonly Counter Consumos = Metrics.CreateCounter("erp_mensajeria_consumos_total",
        "Avisos consumidos por resultado.", new CounterConfiguration { LabelNames = ["cola", "resultado"] });

    private static readonly Histogram EsperaDelCobroSegundos = Metrics.CreateHistogram("erp_cobro_espera_segundos",
        "Lo que el cobro del POS esperó la validación, y si validó a tiempo.",
        new HistogramConfiguration { LabelNames = ["validado"], Buckets = [0.1, 0.25, 0.5, 1, 1.5, 2, 3, 5, 10, 15] });

    public void Estado(string cola, Guid cooperativa, string nombreCooperativa, int pendientes, double edadDelMasViejoSegundos, int muertos)
    {
        var etiqueta = string.IsNullOrWhiteSpace(nombreCooperativa) ? cooperativa.ToString("N") : nombreCooperativa;
        Pendientes.WithLabels(cola, etiqueta).Set(pendientes);
        Edad.WithLabels(cola, etiqueta).Set(edadDelMasViejoSegundos);
        Muertos.WithLabels(cola, etiqueta).Set(muertos);
    }

    public void Publicacion(string cola, string resultado) => Publicaciones.WithLabels(cola, resultado).Inc();

    public void Consumo(string cola, string resultado) => Consumos.WithLabels(cola, resultado).Inc();

    public void EsperaDelCobro(TimeSpan espera, bool validado) =>
        EsperaDelCobroSegundos.WithLabels(validado ? "si" : "no").Observe(espera.TotalSeconds);
}
