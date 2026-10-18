using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>De qué es una línea del extracto. (nuevo)</summary>
public enum ClaseDeLineaDeExtracto
{
    Desembolso = 1,
    SaldoMigrado = 2,
    Pago = 3,
    Reverso = 4,
    AplicacionDeAnticipo = 5,
    SaldoAFavor = 6,
    Cargo = 7,
    Condonacion = 8,
    AjusteComercial = 9,
    Capitalizacion = 10,
    Traslado = 11,
    Castigo = 12,
    Recuperacion = 13,
    Causacion = 14,
}

/// <summary>Lo que el extracto sabe de una operación del crédito para nombrarla: su número y su clase. (nuevo)</summary>
public sealed record OperacionDelExtracto(string Id, string Numero, LoanOperationKind Clase);

/// <summary>
/// Un reverso conocido a R de una operación cuya fecha valor o la del reverso cae en el período: la reconstrucción no lo ve (lo reversado «nunca
/// existió», D4), así que el extracto lo informa en su propia línea sin efecto en los saldos. (nuevo)
/// </summary>
public sealed record ReversoDelExtracto(DateOnly FechaValor, string NumeroDelReverso, string NumeroDeLoReversado, LoanOperationKind ClaseDeLoReversado,
    decimal Valor);

/// <summary>
/// Una línea del extracto: la fecha valor, la clase, la descripción y el movimiento por concepto con signo (+ aumenta lo que se debe, − lo
/// disminuye). Un reverso lleva el valor de lo reversado como <see cref="ValorInformativo"/> y ningún movimiento. (nuevo)
/// </summary>
public sealed record LineaDeExtracto(DateOnly FechaValor, ClaseDeLineaDeExtracto Clase, string Descripcion, string? OperacionId,
    IReadOnlyDictionary<LoanConcept, decimal> PorConcepto, decimal? ValorInformativo = null)
{
    /// <summary>La suma de los movimientos de la línea.</summary>
    public decimal Neto => PorConcepto.Values.Sum();

    public decimal De(LoanConcept concepto) => PorConcepto.GetValueOrDefault(concepto);
}

/// <summary>El extracto armado: período, saldo inicial, líneas y saldo final por concepto, con su explicación. (nuevo)</summary>
public sealed record ExtractoArmado(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyDictionary<LoanConcept, decimal> SaldoInicial,
    IReadOnlyList<LineaDeExtracto> Lineas,
    IReadOnlyDictionary<LoanConcept, decimal> SaldoFinal,
    ExplicacionDeCartera Explicacion)
{
    /// <summary>El movimiento del período de un concepto (la suma de sus líneas).</summary>
    public decimal Movimiento(LoanConcept concepto) => Lineas.Sum(l => l.De(concepto));

    public decimal TotalInicial => SaldoInicial.Values.Sum();

    public decimal TotalFinal => SaldoFinal.Values.Sum();
}

/// <summary>
/// El extracto de un crédito por un período (feature 013, US13, K5, T0970; adaptadores-y-archivos.md §8.6; FR-131): recibe dos estados de la
/// reconstrucción con la misma R —el instante de emisión—, uno a la víspera del período (nulo si el crédito todavía no existía) y otro a su último
/// día armado con el libro teórico desde el primer día del período (<c>MotorDeCartera.Reconstruir(entrada, hasta, desde)</c>), y arma:
/// <list type="bullet">
/// <item>el <b>saldo inicial</b> por concepto (el estado de la víspera);</item>
/// <item>las <b>operaciones del período</b> por fecha valor: cada hecho del libro teórico agrupado por su operación (desembolso, pago con su
/// distribución por concepto, cargos, condonaciones, ajustes, traslados), la <b>causación agregada por mes y concepto</b> (un renglón por mes, con
/// fecha el último día del mes dentro del período) y los reversos conocidos con lo que reversaron, informativos;</item>
/// <item>el <b>saldo final</b> por concepto (el estado del último día).</item>
/// </list>
/// Saldo inicial + movimientos = saldo final al centavo en cada concepto de <see cref="LoanConcept"/>: si no cuadra es un error de programación y
/// lanza. Un período sin movimientos y sin saldo (por ejemplo, posterior a la cancelación) no se extracta: lanza <see cref="ReglaDeCarteraException"/>
/// con <see cref="CodigoNadaQueExtractar"/> (§8.1: «período con movimientos o saldo»). Motor puro: sólo <c>DateOnly</c> y <c>decimal</c>. (nuevo)
/// </summary>
public static class ExtractoDeCredito
{
    /// <summary>El rechazo de un período sin movimientos ni saldo (el mismo código que el resto de documentos sin nada que certificar).</summary>
    public const string CodigoNadaQueExtractar = "Lending.Document.NothingToCertify";

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Todos los conceptos, en el orden del enum.</summary>
    public static IReadOnlyList<LoanConcept> Conceptos { get; } = Enum.GetValues<LoanConcept>().OrderBy(c => (int)c).ToList();

    /// <summary>Arma el extracto de <paramref name="desde"/> a <paramref name="hasta"/> (ver la clase).</summary>
    public static ExtractoArmado Armar(EstadoDelCredito? alInicio, EstadoDelCredito alFinal, DateOnly desde, DateOnly hasta,
        IReadOnlyDictionary<string, OperacionDelExtracto>? operaciones = null, IReadOnlyList<ReversoDelExtracto>? reversos = null)
    {
        ArgumentNullException.ThrowIfNull(alFinal);
        if (desde > hasta) throw new ArgumentException("El período empieza después de terminar.", nameof(desde));
        if (alFinal.Fecha != hasta) throw new ArgumentException("El estado final debe ser al último día del período.", nameof(alFinal));
        if (alInicio is not null && alInicio.Fecha != desde.AddDays(-1))
            throw new ArgumentException("El estado inicial debe ser a la víspera del período.", nameof(alInicio));
        if (alInicio is not null && alInicio.InformacionHasta != alFinal.InformacionHasta)
            throw new ArgumentException("Los dos estados deben tener la misma información (R).", nameof(alInicio));
        operaciones ??= new Dictionary<string, OperacionDelExtracto>();

        var inicial = PorConcepto(alInicio);
        var final = PorConcepto(alFinal);
        var movimientos = alFinal.Libro.Movimientos.Where(m => m.Dia >= desde && m.Dia <= hasta && m.Monto != 0m).ToList();

        var lineas = new List<LineaDeExtracto>();
        // Los hechos de las operaciones: un renglón por operación y día (o por clase y día si la fila no nombra operación).
        foreach (var grupo in movimientos.Where(m => !LibroTeorico.EsDeCausacion(m.Clase))
                     .GroupBy(m => (m.Dia, Operacion: m.OperacionId ?? string.Empty, Clase: m.OperacionId is null ? m.Clase : default)))
        {
            var filas = grupo.ToList();
            var conocida = grupo.Key.Operacion.Length > 0 ? operaciones.GetValueOrDefault(grupo.Key.Operacion) : null;
            var clase = conocida is not null ? ClaseDe(conocida.Clase, filas) : ClaseDe(filas);
            var porConcepto = Sumar(filas);
            if (porConcepto.Count == 0) continue;
            lineas.Add(new LineaDeExtracto(grupo.Key.Dia, clase, Descripcion(clase, conocida), grupo.Key.Operacion.Length > 0 ? grupo.Key.Operacion : null,
                porConcepto));
        }

        // La causación, agregada por mes y concepto, fechada el último día del mes dentro del período.
        foreach (var mes in movimientos.Where(m => LibroTeorico.EsDeCausacion(m.Clase)).GroupBy(m => (m.Dia.Year, m.Dia.Month)))
        {
            var porConcepto = Sumar(mes.ToList());
            if (porConcepto.Count == 0) continue;
            var finDeMes = new DateOnly(mes.Key.Year, mes.Key.Month, DateTime.DaysInMonth(mes.Key.Year, mes.Key.Month));
            var fecha = finDeMes > hasta ? hasta : finDeMes;
            lineas.Add(new LineaDeExtracto(fecha, ClaseDeLineaDeExtracto.Causacion,
                $"Causación de {new DateOnly(mes.Key.Year, mes.Key.Month, 1).ToString("MMMM 'de' yyyy", Co)}", null, porConcepto));
        }

        // Los reversos conocidos: informativos, sin movimiento (lo reversado ya no está en el libro).
        foreach (var r in reversos ?? [])
        {
            if (r.FechaValor < desde || r.FechaValor > hasta) continue;
            lineas.Add(new LineaDeExtracto(r.FechaValor, ClaseDeLineaDeExtracto.Reverso,
                $"{NombreDeLaClase(r.ClaseDeLoReversado)} {r.NumeroDeLoReversado} reversado por {r.NumeroDelReverso}", null,
                new Dictionary<LoanConcept, decimal>(), r.Valor));
        }

        var ordenadas = lineas
            .OrderBy(l => l.FechaValor)
            .ThenBy(l => l.Clase == ClaseDeLineaDeExtracto.Causacion ? 1 : 0)
            .ThenBy(l => l.OperacionId is { } id && long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : long.MaxValue)
            .ThenBy(l => (int)l.Clase)
            .ThenBy(l => l.Descripcion, StringComparer.Ordinal)
            .ToList();

        foreach (var concepto in Conceptos)
        {
            var suma = inicial[concepto] + ordenadas.Sum(l => l.De(concepto));
            if (suma != final[concepto])
                throw new InvalidOperationException(
                    $"El extracto no cuadra en {concepto}: saldo inicial {inicial[concepto]} + movimientos = {suma}, saldo final {final[concepto]}.");
        }

        if (ordenadas.Count == 0 && inicial.Values.All(v => v == 0m) && final.Values.All(v => v == 0m))
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoNadaQueExtractar,
                "No hay nada que certificar: el crédito no tuvo movimientos ni saldo en el período.",
                new Dictionary<string, object?> { ["periodFrom"] = desde, ["periodTo"] = hasta }));

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Extracto del {ExplicacionDeCartera.Fecha(desde)} al {ExplicacionDeCartera.Fecha(hasta)} con la información a " +
                   $"{alFinal.InformacionHasta.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)} (R).")
            .Linea($"Saldo inicial {ExplicacionDeCartera.Numero(inicial.Values.Sum())} + movimientos " +
                   $"{ExplicacionDeCartera.Numero(ordenadas.Sum(l => l.Neto))} = saldo final {ExplicacionDeCartera.Numero(final.Values.Sum())}.")
            .Linea("La causación va agregada por mes y concepto; los reversos se informan sin movimiento porque lo reversado ya no hace parte del crédito.");
        return new ExtractoArmado(desde, hasta, inicial, ordenadas, final, explicacion);
    }

    /// <summary>Los saldos por concepto del estado, con todos los conceptos (cero los que no tiene); todo en cero si el estado es nulo.</summary>
    private static Dictionary<LoanConcept, decimal> PorConcepto(EstadoDelCredito? estado) =>
        Conceptos.ToDictionary(c => c, c => estado?.Saldo(c) ?? 0m);

    private static Dictionary<LoanConcept, decimal> Sumar(IReadOnlyList<MovimientoDeLibro> filas) =>
        filas.GroupBy(f => f.Concepto).Select(g => (g.Key, Suma: g.Sum(f => f.Monto))).Where(x => x.Suma != 0m)
            .OrderBy(x => (int)x.Key).ToDictionary(x => x.Key, x => x.Suma);

    private static ClaseDeLineaDeExtracto ClaseDe(LoanOperationKind clase, IReadOnlyList<MovimientoDeLibro> filas) => clase switch
    {
        LoanOperationKind.Disbursement => ClaseDeLineaDeExtracto.Desembolso,
        LoanOperationKind.OpeningBalance => ClaseDeLineaDeExtracto.SaldoMigrado,
        LoanOperationKind.Payment or LoanOperationKind.ProvisionalCollection => ClaseDeLineaDeExtracto.Pago,
        LoanOperationKind.CreditBalanceResolution => ClaseDeLineaDeExtracto.SaldoAFavor,
        LoanOperationKind.CommercialAdjustment => ClaseDeLineaDeExtracto.AjusteComercial,
        _ => ClaseDe(filas),
    };

    /// <summary>La clase de un grupo de filas por la clase de fila que manda (la primera en el orden de <see cref="LoanTransactionKind"/>).</summary>
    private static ClaseDeLineaDeExtracto ClaseDe(IReadOnlyList<MovimientoDeLibro> filas) =>
        filas.Select(f => f.Clase).Order().First() switch
        {
            LoanTransactionKind.Disbursement => ClaseDeLineaDeExtracto.Desembolso,
            LoanTransactionKind.OpeningBalance => ClaseDeLineaDeExtracto.SaldoMigrado,
            LoanTransactionKind.PaymentAllocation or LoanTransactionKind.PaymentReallocation or LoanTransactionKind.AdvanceReceived =>
                ClaseDeLineaDeExtracto.Pago,
            LoanTransactionKind.AdvanceApplied => ClaseDeLineaDeExtracto.AplicacionDeAnticipo,
            LoanTransactionKind.CreditBalanceRecognized or LoanTransactionKind.CreditBalanceSettled => ClaseDeLineaDeExtracto.SaldoAFavor,
            LoanTransactionKind.Charge => ClaseDeLineaDeExtracto.Cargo,
            LoanTransactionKind.Waiver => ClaseDeLineaDeExtracto.Condonacion,
            LoanTransactionKind.CommercialAdjustment => ClaseDeLineaDeExtracto.AjusteComercial,
            LoanTransactionKind.Capitalization => ClaseDeLineaDeExtracto.Capitalizacion,
            LoanTransactionKind.TransferToLinkedLoan or LoanTransactionKind.TransferFromLinkedLoan => ClaseDeLineaDeExtracto.Traslado,
            LoanTransactionKind.WriteOff => ClaseDeLineaDeExtracto.Castigo,
            LoanTransactionKind.Recovery => ClaseDeLineaDeExtracto.Recuperacion,
            _ => ClaseDeLineaDeExtracto.Causacion,
        };

    private static string Descripcion(ClaseDeLineaDeExtracto clase, OperacionDelExtracto? operacion)
    {
        var nombre = clase switch
        {
            ClaseDeLineaDeExtracto.Desembolso => "Desembolso",
            ClaseDeLineaDeExtracto.SaldoMigrado => "Saldo inicial migrado",
            ClaseDeLineaDeExtracto.Pago => "Pago",
            ClaseDeLineaDeExtracto.Reverso => "Reverso",
            ClaseDeLineaDeExtracto.AplicacionDeAnticipo => "Aplicación del anticipo",
            ClaseDeLineaDeExtracto.SaldoAFavor => "Saldo a favor",
            ClaseDeLineaDeExtracto.Cargo => "Cargo",
            ClaseDeLineaDeExtracto.Condonacion => "Condonación",
            ClaseDeLineaDeExtracto.AjusteComercial => "Ajuste comercial",
            ClaseDeLineaDeExtracto.Capitalizacion => "Capitalización",
            ClaseDeLineaDeExtracto.Traslado => "Traslado entre créditos",
            ClaseDeLineaDeExtracto.Castigo => "Castigo",
            ClaseDeLineaDeExtracto.Recuperacion => "Recuperación",
            _ => "Causación",
        };
        return operacion is null || string.IsNullOrWhiteSpace(operacion.Numero) ? nombre : $"{nombre} {operacion.Numero}";
    }

    /// <summary>El nombre de una clase de operación como lo lee el deudor.</summary>
    public static string NombreDeLaClase(LoanOperationKind clase) => clase switch
    {
        LoanOperationKind.Disbursement => "Desembolso",
        LoanOperationKind.Payment => "Pago",
        LoanOperationKind.ProvisionalCollection => "Recaudo provisional",
        _ => "Operación",
    };
}
