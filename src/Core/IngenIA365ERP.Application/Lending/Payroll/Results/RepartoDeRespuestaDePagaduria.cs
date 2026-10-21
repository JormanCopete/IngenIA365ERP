using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;

namespace IngenIA365ERP.Application.Lending.Payroll.Results;

/// <summary>Una línea del archivo de un grupo, de crédito o de cuenta, como la ve el reparto. (nuevo)</summary>
public sealed record LineaPorRepartir(int Id, bool EsDeCuenta, string? EmployeeIdAtPaymaster, string? Codigo, int Prioridad, decimal Total);

/// <summary>Lo que tocó a una línea: lo asignado de lo descontado por su código y la fila de la respuesta de donde salió. (nuevo)</summary>
public sealed record AsignacionDeLinea(LineaPorRepartir Linea, decimal Asignado, DeductionConceptLineV2 Fila);

/// <summary>Una fila de la respuesta que no se aplicó, con su motivo (uno de los tres códigos de <see cref="ErroresDeLibranza"/>). (nuevo)</summary>
public sealed record FilaNoAplicada(DeductionConceptLineV2 Fila, string Motivo, decimal Descontado);

/// <summary>El reparto de una respuesta por código: lo asignado a cada línea y las filas que van a «no aplicados». (nuevo)</summary>
public sealed record RepartoDeRespuesta(IReadOnlyList<AsignacionDeLinea> Asignaciones, IReadOnlyList<FilaNoAplicada> NoAplicadas);

/// <summary>
/// El reparto de lo que la pagaduría informa por empleado y por código suyo (feature 014, T091; decisión del dueño del 2026-10-18, D36; mensajes.md §5.7
/// de la 014): las filas del mismo empleado y código se suman; cada una se reparte entre las líneas del archivo de ese empleado con ese código, con la
/// regla del código (<see cref="RepartoDePagaduria"/>: <c>Priority</c> por la prioridad sellada en la línea, <c>Proportional</c> a prorrata de lo enviado).
/// Un empleado que el archivo no tiene va a «no aplicados» con <c>.EmployeeWithoutEquivalence</c>; un código que el archivo no le pidió a ese empleado,
/// con <c>.ConceptWithoutEquivalence</c>; lo descontado que sobra sobre lo enviado, con <c>.DeductedExceedsSent</c>. Ninguna de ellas bloquea lo demás.
/// Las líneas que ninguna fila nombra no reciben resultado. Puro. (nuevo)
/// </summary>
public static class RepartoDeRespuestaDePagaduria
{
    public static RepartoDeRespuesta Repartir(IReadOnlyList<DeductionConceptLineV2> filas, IReadOnlyList<LineaPorRepartir> lineas,
        IReadOnlyDictionary<string, ConceptAllocationRule> reglas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(reglas);
        var asignaciones = new List<AsignacionDeLinea>();
        var noAplicadas = new List<FilaNoAplicada>();
        var empleados = lineas.Where(l => !string.IsNullOrWhiteSpace(l.EmployeeIdAtPaymaster)).Select(l => Clave(l.EmployeeIdAtPaymaster))
            .ToHashSet(StringComparer.Ordinal);

        var agrupadas = filas
            .GroupBy(f => (Empleado: Clave(f.PayerEmployeeId), Codigo: Clave(f.PaymasterConceptCode)))
            .Select(g => g.First() with { Expected = g.Sum(f => f.Expected), Deducted = g.Sum(f => f.Deducted) })
            .ToList();
        foreach (var fila in agrupadas)
        {
            var empleado = Clave(fila.PayerEmployeeId);
            var codigo = Clave(fila.PaymasterConceptCode);
            if (!empleados.Contains(empleado))
            {
                noAplicadas.Add(new FilaNoAplicada(fila, ErroresDeLibranza.CodigoEmpleadoSinEquivalencia, fila.Deducted));
                continue;
            }
            var suyas = lineas.Where(l => Clave(l.EmployeeIdAtPaymaster) == empleado && Clave(l.Codigo) == codigo).ToList();
            if (suyas.Count == 0)
            {
                noAplicadas.Add(new FilaNoAplicada(fila, ErroresDeLibranza.CodigoConceptoSinEquivalencia, fila.Deducted));
                continue;
            }
            var regla = reglas.TryGetValue(codigo, out var r) ? r : ConceptAllocationRule.Priority;
            var claves = suyas.ToDictionary(l => $"{(l.EsDeCuenta ? 'C' : 'L')}{l.Id:D10}");
            var reparto = RepartoDePagaduria.Repartir(Math.Max(0m, fila.Deducted), regla,
                claves.Select(kv => new PorRepartir(kv.Key, kv.Value.Total, kv.Value.Prioridad)).ToList());
            var asignado = reparto.Asignaciones.ToDictionary(a => a.Clave, a => a.Asignado);
            foreach (var (clave, linea) in claves)
                asignaciones.Add(new AsignacionDeLinea(linea, asignado.GetValueOrDefault(clave), fila));
            if (reparto.Sobrante > 0m) noAplicadas.Add(new FilaNoAplicada(fila, ErroresDeLibranza.CodigoDescuentoSobrante, reparto.Sobrante));
        }
        return new RepartoDeRespuesta(asignaciones, noAplicadas);
    }

    private static string Clave(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}
