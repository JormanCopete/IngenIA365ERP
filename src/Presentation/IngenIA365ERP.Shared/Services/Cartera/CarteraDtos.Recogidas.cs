using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

// 2026-11-27 (pedido del dueño; feature 028, HU1, T018): los DTOs de los productos para recoger con el desembolso de un crédito nuevo
// (/api/lending/payoffs/candidates; specs/028-recogida-de-creditos/contracts/api.md §1). Las respuestas traen los enums como número (EnumPorNombreONumero)
// y aquí se leen como int; el pedido también los manda por número. (nuevo)

/// <summary>Un producto marcado para recoger (<c>PayoffInput</c> del servidor): clase, producto, modo, valor del parcial y tratamiento de la cuota causada.</summary>
public sealed record RecogidaElegidaRequest(
    int Kind,
    Guid? ProductPublicId,
    int Mode,
    decimal? Amount = null,
    string? Reference = null,
    int? InstallmentTreatment = null);

/// <summary>El crédito nuevo que se simula: su monto (para el neto) y su cuota (para compararla con las que se liberan).</summary>
public sealed record CreditoNuevoDeRecogidaRequest(decimal Amount, decimal? MonthlyInstallment = null);

/// <summary>El pedido de la consulta y de su PDF.</summary>
public sealed record ProductosParaRecogerRequest(
    Guid PersonPublicId,
    Guid? CreditLinePublicId = null,
    DateOnly? ProjectedDisbursementDate = null,
    DateTime? RecordedAsOf = null,
    IReadOnlyList<RecogidaElegidaRequest>? Selected = null,
    CreditoNuevoDeRecogidaRequest? NewLoan = null,
    Guid? ExcludeApplicationPublicId = null,
    bool? Decimals = null);

/// <summary>Lo que cuesta recoger un producto, por concepto: el interés devuelto, el anticipo y el saldo a favor se descuentan.</summary>
public sealed record DesgloseDeRecogidaDto(
    decimal Principal,
    decimal CurrentInterest,
    decimal InterestReturned,
    decimal InterestCharged,
    decimal DefaultInterest,
    decimal Insurance,
    decimal Administration,
    decimal OtherCharges,
    decimal Advance,
    decimal CreditBalance)
{
    public decimal Total => Principal + CurrentInterest - InterestReturned + InterestCharged + DefaultInterest + Insurance + Administration + OtherCharges
        - Advance - CreditBalance;
}

/// <summary>La cuota causada enviada a su canal (llega con la entrega E2).</summary>
public sealed record CuotaEnviadaDto(int State, string? Paymaster, string? File, string? Period, decimal? LineAmount);

/// <summary>La cuota causada del crédito a la fecha proyectada, qué se hace con ella y si se puede elegir.</summary>
public sealed record CuotaCausadaDeRecogidaDto(int Number, DateOnly? DueDate, decimal Amount, int Treatment, bool Choosable, CuotaEnviadaDto? Sent);

/// <summary>Un producto del deudor que el crédito nuevo podría recoger.</summary>
public sealed record ProductoParaRecogerDto(
    int Kind,
    Guid ProductPublicId,
    string Number,
    string Line,
    int? Origin,
    int? Channel,
    int DaysPastDue,
    decimal? Installment,
    bool Eligible,
    string? BlockReason,
    IReadOnlyList<int> AdmittedModes,
    decimal ToPayOff,
    DesgloseDeRecogidaDto Breakdown,
    string? InterestMode,
    string? InterestModeReason,
    CuotaCausadaDeRecogidaDto? CurrentInstallment,
    decimal? ToPayOffExcludingCurrentInstallment,
    string? InOtherApplication,
    string Fingerprint,
    IReadOnlyList<string> Warnings)
{
    public int? Status { get; init; }
    public string? BlockMessage { get; init; }
    public string? InterestModeExplanation { get; init; }
    public bool Selected { get; init; }
    public int? SelectedMode { get; init; }
    public decimal? SelectedAmount { get; init; }

    public bool AdmiteParcial => AdmittedModes.Contains(TextosDeRecogida.ModoParcial);
}

public sealed record CapacidadDespuesDeRecogerDto(decimal Available, bool Declared);

/// <summary>El resumen de lo marcado: total, neto a girar, cuotas que se liberan y avisos.</summary>
public sealed record ResumenDeRecogidaDto(
    decimal SelectedTotal,
    decimal? NetToDisburse,
    decimal ReleasedInstallments,
    CapacidadDespuesDeRecogerDto? CapacityAfter,
    IReadOnlyList<string> Warnings)
{
    public int SelectedCount { get; init; }
    public decimal? NewLoanAmount { get; init; }
    public decimal? NewInstallment { get; init; }
    public bool NetNotPositive { get; init; }
}

/// <summary>La respuesta: D, R, los productos y el resumen; la persona, para mostrarla.</summary>
public sealed record ProductosParaRecogerDto(
    DateOnly Date,
    DateTime RecordedAsOf,
    IReadOnlyList<ProductoParaRecogerDto> Products,
    ResumenDeRecogidaDto Summary)
{
    public PersonaDelEstadoDeCuentaDto? Person { get; init; }
    public string? NewLoanLine { get; init; }
}

/// <summary>
/// Lo que la pantalla marca en la tabla de productos para recoger y lo que devuelve al que la contiene: el producto, el modo y el valor del parcial, y
/// lo que la tabla ya sabe de él (número y valor a recoger) para mostrarlo sin volver a consultar. (nuevo)
/// </summary>
public sealed record RecogidaMarcada(Guid ProductPublicId, string Number, int Mode, decimal? Amount, decimal ToPayOff)
{
    /// <summary>Lo que pide el servidor (en E1, siempre un crédito).</summary>
    public RecogidaElegidaRequest ComoPedido() => new(TextosDeRecogida.ClaseCredito, ProductPublicId, Mode, Mode == TextosDeRecogida.ModoParcial ? Amount : null);
}

/// <summary>
/// Lo que la tabla de productos para recoger le avisa a quien la contiene cada vez que cambia lo marcado o se vuelve a simular: lo marcado (con su
/// pedido para el servidor), el resumen, la fecha proyectada de desembolso (D) y hasta cuándo se tomó la información (R). (nuevo)
/// </summary>
public sealed record SeleccionDeRecogida(IReadOnlyList<RecogidaMarcada> Marcadas, ResumenDeRecogidaDto Resumen, DateOnly FechaProyectada,
    DateTime? InformacionHasta)
{
    public static SeleccionDeRecogida Vacia(DateOnly fecha) => new([], new ResumenDeRecogidaDto(0m, null, 0m, null, []), fecha, null);

    /// <summary>Lo marcado como lo pide el servidor (<c>payoffs</c> de la solicitud).</summary>
    public IReadOnlyList<RecogidaElegidaRequest> Pedido => Marcadas.Select(m => m.ComoPedido()).ToList();

    /// <summary>
    /// El resumen de lo marcado sobre los productos que ya trajo la consulta, con la misma regla del servidor: total en un total; en un parcial, el valor
    /// pedido topado en lo que cuesta recoger; las cuotas que se liberan, las de los recogidos total; el neto, el monto menos lo recogido.
    /// </summary>
    public static ResumenDeRecogidaDto Resumir(IEnumerable<ProductoParaRecogerDto> productos, IReadOnlyList<RecogidaMarcada> marcadas, decimal? monto,
        decimal? cuotaNueva)
    {
        var porProducto = productos.Where(p => p.Eligible).ToDictionary(p => p.ProductPublicId);
        var total = 0m;
        var liberadas = 0m;
        var contadas = 0;
        foreach (var m in marcadas)
        {
            if (!porProducto.TryGetValue(m.ProductPublicId, out var p) || !p.AdmittedModes.Contains(m.Mode)) continue;
            total += m.Mode == TextosDeRecogida.ModoTotal ? p.ToPayOff : Math.Min(m.Amount ?? 0m, p.ToPayOff);
            if (m.Mode == TextosDeRecogida.ModoTotal) liberadas += p.Installment ?? 0m;
            contadas++;
        }
        decimal? neto = monto is { } v ? v - total : null;
        var avisos = neto is <= 0m
            ? new[] { $"Lo que se recoge ({TextosDeRecogida.Pesos(total)}) deja el neto a girar en {TextosDeRecogida.Pesos(neto)}: con estas recogidas la solicitud no se puede radicar." }
            : [];
        return new ResumenDeRecogidaDto(total, neto, liberadas, null, avisos)
        {
            SelectedCount = contadas, NewLoanAmount = monto, NewInstallment = cuotaNueva, NetNotPositive = neto is <= 0m,
        };
    }
}

/// <summary>
/// Los textos de la recogida (feature 028, HU1): los números de los enums del servidor (<c>PayoffProductKind</c>, <c>PayoffMode</c>,
/// <c>InstallmentTreatment</c>, <c>LoanOrigin</c>, <c>RepaymentChannel</c>, <c>LoanStatus</c>) en palabras, los motivos por los que no se recoge un producto
/// y los valores en pesos. (nuevo)
/// </summary>
public static class TextosDeRecogida
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public const int ClaseCredito = 1;
    public const int ClaseCuenta = 2;
    public const int ClaseFactura = 3;

    public const int ModoTotal = 1;
    public const int ModoParcial = 2;
    public const int ModoCuotas = 3;
    public const int ModoAbono = 4;

    public const int CuotaIncluida = 1;
    public const int CuotaExcluida = 2;

    public static string Clase(int clase) => clase switch
    {
        ClaseCredito => "Crédito",
        ClaseCuenta => "Cuenta",
        ClaseFactura => "Factura a crédito",
        _ => "—",
    };

    public static string Modo(int? modo) => modo switch
    {
        ModoTotal => "Total",
        ModoParcial => "Parcial",
        ModoCuotas => "Cuotas",
        ModoAbono => "Abono",
        _ => "—",
    };

    public static string Origen(int? origen) => origen switch
    {
        1 or 3 => "Crédito",
        2 => "Venta de Comercial",
        4 => "Cargo masivo",
        _ => "—",
    };

    public static string Canal(int? canal) => canal switch
    {
        1 => "Caja",
        2 => "Nómina propia",
        3 => "Libranza",
        4 => "Débito automático",
        _ => "—",
    };

    public static string Estado(int? estado) => estado switch
    {
        1 => "Pendiente de giro",
        2 => "Vigente",
        3 => "Cancelado",
        4 => "Castigado",
        5 => "Anulado",
        6 => "Novado",
        _ => "—",
    };

    public static string TratamientoDeCuota(int tratamiento) => tratamiento switch
    {
        CuotaIncluida => "Se incluye en la recogida",
        CuotaExcluida => "Queda como se envió",
        _ => "—",
    };

    /// <summary>El motivo corto por el que no se recoge (el <c>blockReason</c> del servidor), para la marca deshabilitada.</summary>
    public static string Motivo(string? motivo) => motivo switch
    {
        null or "" => string.Empty,
        "RequiresRestructuring" => "En mora: requiere reestructuración",
        "WrittenOff" => "Castigado",
        "Insolvency" => "En insolvencia",
        "NoBalance" => "Sin saldo",
        "NotActive" => "No está vigente",
        "NotAdmitted" => "La línea no lo admite",
        "NotSettled" => "No se pudo liquidar",
        _ => motivo,
    };

    /// <summary>El modo de intereses (<c>Corrido</c> o <c>CuotaCompleta</c>) en palabras.</summary>
    public static string ModoDeIntereses(string? modo) => modo switch
    {
        "Corrido" => "Interés corrido",
        "CuotaCompleta" => "Cuota completa",
        _ => "—",
    };

    /// <summary>«Está en la solicitud S-000123» o, sin número, «Está en otra solicitud en borrador».</summary>
    public static string EnOtraSolicitud(string? numero) =>
        numero is null ? string.Empty : string.IsNullOrWhiteSpace(numero) ? "Está en otra solicitud en borrador" : $"Está en la solicitud {numero}";

    /// <summary>Un valor en pesos sin centavos: <c>$ 1.234.567</c>.</summary>
    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public static string Pesos(decimal? valor) => valor is { } v ? Pesos(v) : "—";
}
