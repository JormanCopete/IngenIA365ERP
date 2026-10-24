using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Identificacion;

/// <summary>Una regla vigente, como la ve el motor. (nuevo)</summary>
public sealed record ReglaDeIdentificacion(int Id, IdentificationCriterion Criterio, int Prioridad, bool AplicarAlCargar);

/// <summary>Un producto cuyo número coincide con la referencia del abono. <see cref="Exacta"/>: carácter por carácter con el número completo. (nuevo)</summary>
public sealed record CandidatoPorReferencia(Guid PersonaPublicId, Guid ProductoPublicId, ApplicationTarget Destino, bool Exacta);

/// <summary>Una consignación reportada por verificar que puede ser el abono. (nuevo)</summary>
public sealed record CandidatoReportado(int ReportadaId, Guid PersonaPublicId, Guid ProductoPublicId, ApplicationTarget Destino, decimal Valor, DateOnly Fecha,
    string? Referencia);

/// <summary>Un exigible de la persona (cuota del ciclo o cuota de cuenta) con su valor de hoy. (nuevo)</summary>
public sealed record CandidatoExigible(Guid PersonaPublicId, Guid ProductoPublicId, ApplicationTarget Destino, decimal Valor);

/// <summary>Lo que el motor recibe: el abono y lo encontrado por cada criterio. (nuevo)</summary>
public sealed record AbonoAIdentificar(
    decimal Valor,
    DateOnly Fecha,
    string? Referencia,
    IReadOnlyList<CandidatoPorReferencia> PorReferencia,
    IReadOnlyList<Guid> PersonasDelConsignante,
    IReadOnlyList<CandidatoReportado> Reportadas,
    IReadOnlyList<CandidatoExigible> Exigibles,
    int DiasDeVentana);

/// <summary>
/// La propuesta: la regla que la dio, la persona y el producto (o sólo la persona, con el documento del consignante), la consignación reportada que confirma y si se
/// aplica al cargar. (nuevo)
/// </summary>
public sealed record PropuestaDeIdentificacion(int ReglaId, IdentificationCriterion Criterio, Guid PersonaPublicId, Guid? ProductoPublicId, ApplicationTarget? Destino,
    decimal Valor, bool AplicarAlCargar, int? ReportadaId, string Explicacion);

/// <summary>
/// El motor puro de las propuestas de identificación (feature 015, R1, T0379; FR-067; D15; SC-008). Las reglas activas se recorren por prioridad; cada una propone sólo si
/// encuentra <b>un único</b> candidato (con dos o más, pasa a la siguiente: la ambigüedad la resuelve una persona):
/// <list type="bullet">
/// <item><c>PaymentReference</c>: un único producto cuyo número coincide con la referencia;</item>
/// <item><c>DepositorTaxId</c>: una única persona con el documento del consignante (propone la persona, sin producto);</item>
/// <item><c>ReportedDeposit</c>: una única consignación reportada por verificar con el mismo valor, en la ventana de días anteriores al movimiento y, si las dos tienen
/// referencia, la misma;</item>
/// <item><c>ExactDueAmount</c>: un único exigible de las personas del consignante igual al valor del abono.</item>
/// </list>
/// Sólo se aplica al cargar lo de <c>PaymentReference</c> con la referencia <b>exacta</b>, la regla marcada y el parámetro encendido (lo decide quien llama con
/// <paramref name="aplicarAlCargarPermitido"/>). Nunca se propone por encima del valor del abono. (nuevo)
/// </summary>
public static class MotorDeIdentificacion
{
    public static PropuestaDeIdentificacion? Proponer(AbonoAIdentificar abono, IReadOnlyList<ReglaDeIdentificacion> reglas, bool aplicarAlCargarPermitido)
    {
        ArgumentNullException.ThrowIfNull(abono);
        ArgumentNullException.ThrowIfNull(reglas);
        if (abono.Valor <= 0m) return null;
        foreach (var regla in reglas.OrderBy(r => r.Prioridad).ThenBy(r => r.Id))
        {
            var p = regla.Criterio switch
            {
                IdentificationCriterion.PaymentReference => PorReferencia(abono, regla, aplicarAlCargarPermitido),
                IdentificationCriterion.DepositorTaxId => PorConsignante(abono, regla),
                IdentificationCriterion.ReportedDeposit => PorReportada(abono, regla),
                IdentificationCriterion.ExactDueAmount => PorExigible(abono, regla),
                _ => null,
            };
            if (p is not null) return p;
        }
        return null;
    }

    private static PropuestaDeIdentificacion? PorReferencia(AbonoAIdentificar a, ReglaDeIdentificacion r, bool permitido)
    {
        if (string.IsNullOrWhiteSpace(a.Referencia) || a.PorReferencia.Count != 1) return null;
        var c = a.PorReferencia[0];
        return new PropuestaDeIdentificacion(r.Id, r.Criterio, c.PersonaPublicId, c.ProductoPublicId, c.Destino, a.Valor, permitido && r.AplicarAlCargar && c.Exacta, null,
            $"La referencia {a.Referencia} es el número de un producto{(c.Exacta ? " (exacta)" : string.Empty)}.");
    }

    private static PropuestaDeIdentificacion? PorConsignante(AbonoAIdentificar a, ReglaDeIdentificacion r) =>
        a.PersonasDelConsignante.Count == 1
            ? new PropuestaDeIdentificacion(r.Id, r.Criterio, a.PersonasDelConsignante[0], null, null, a.Valor, false, null,
                "El documento del consignante es el de una persona: elija su producto.")
            : null;

    private static PropuestaDeIdentificacion? PorReportada(AbonoAIdentificar a, ReglaDeIdentificacion r)
    {
        var desde = a.Fecha.AddDays(-Math.Max(0, a.DiasDeVentana));
        var posibles = a.Reportadas.Where(x => x.Valor == a.Valor && x.Fecha >= desde && x.Fecha <= a.Fecha
                                               && (string.IsNullOrWhiteSpace(x.Referencia) || string.IsNullOrWhiteSpace(a.Referencia)
                                                   || string.Equals(x.Referencia.Trim(), a.Referencia.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (posibles.Count != 1) return null;
        var c = posibles[0];
        return new PropuestaDeIdentificacion(r.Id, r.Criterio, c.PersonaPublicId, c.ProductoPublicId, c.Destino, a.Valor, false, c.ReportadaId,
            $"Coincide con la consignación reportada del {c.Fecha:yyyy-MM-dd}.");
    }

    private static PropuestaDeIdentificacion? PorExigible(AbonoAIdentificar a, ReglaDeIdentificacion r)
    {
        var posibles = a.Exigibles.Where(e => e.Valor == a.Valor).ToList();
        if (posibles.Count != 1) return null;
        var c = posibles[0];
        return new PropuestaDeIdentificacion(r.Id, r.Criterio, c.PersonaPublicId, c.ProductoPublicId, c.Destino, a.Valor, false, null,
            "El valor es exactamente lo exigible de un producto del consignante.");
    }
}
