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

/// <summary>
/// La cuota causada enviada a su canal. 2026-11-27 (feature 028, E2, T066): en qué va (<c>SentInstallmentState</c> como número), la pagaduría, el archivo, el
/// período, el valor de la línea, la cuota que nombra, el convenio, lo corriente y lo vencido, la corrida de la nómina propia y la frase que lo dice.
/// </summary>
public sealed record CuotaEnviadaDto(int State, string? Paymaster, string? File, string? Period, decimal? LineAmount)
{
    public int? InstallmentNumber { get; init; }
    public string? Agreement { get; init; }
    public decimal? CurrentAmount { get; init; }
    public decimal? PastDueAmount { get; init; }
    public Guid? PayrollRunPublicId { get; init; }

    /// <summary>«La cuota 7 ya se envió a la pagaduría X en el archivo ADP-000123 de noviembre de 2026 y no ha respondido.»</summary>
    public string? Text { get; init; }

    /// <summary>El estado en palabras (cuando el servidor no mandó la frase).</summary>
    public string Texto => Text ?? TextosDeRecogida.EstadoDeEnvio(State);

    /// <summary>¿Va en la nómina propia? (si no, en un archivo de libranza).</summary>
    public bool EnNominaPropia => State is TextosDeRecogida.EnvioNominaGenerada or TextosDeRecogida.EnvioNominaCalculada or TextosDeRecogida.EnvioNominaAprobada;
}

/// <summary>
/// La cuota causada del crédito a la fecha proyectada, qué se hace con ella y si se puede elegir. 2026-11-27 (feature 028, E2, T066): con «Preguntar» el
/// asesor elige (<see cref="Choosable"/>); <see cref="Left"/> es lo que la cuota debería a su canal si se deja (nulo si no se puede dejar) y las explicaciones
/// dicen qué pasa con ella en cada caso.
/// </summary>
public sealed record CuotaCausadaDeRecogidaDto(int Number, DateOnly? DueDate, decimal Amount, int Treatment, bool Choosable, CuotaEnviadaDto? Sent)
{
    public decimal? Left { get; init; }

    /// <summary>Lo que dice <c>Recogida.CuotaCausada</c> para la línea del crédito: <c>Incluir</c>, <c>Excluir</c> o <c>Preguntar</c>.</summary>
    public string? Parameter { get; init; }

    public string? Explanation { get; init; }
    public string? IncludedExplanation { get; init; }
    public string? LeftExplanation { get; init; }

    /// <summary>Por qué no se puede dejar aunque la línea lo pida (vivienda, sin cuota causada a la fecha).</summary>
    public string? NotLeftReason { get; init; }

    /// <summary>Qué pasa con la cuota con el tratamiento <paramref name="tratamiento"/> (nulo, el que rige).</summary>
    public string? Explicacion(int? tratamiento) => tratamiento switch
    {
        TextosDeRecogida.CuotaExcluida when Choosable => LeftExplanation ?? Explanation,
        TextosDeRecogida.CuotaIncluida when Choosable => IncludedExplanation ?? Explanation,
        _ => Explanation,
    };

    /// <summary>El tratamiento que rige con lo que eligió el asesor: sólo con «Preguntar» cuenta su elección.</summary>
    public int Tratamiento(int? elegido) => Choosable && elegido is TextosDeRecogida.CuotaIncluida or TextosDeRecogida.CuotaExcluida ? elegido.Value : Treatment;
}

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

    /// <summary>2026-11-27 (feature 028, T051): el interés explicado renglón a renglón, con los días que dice el servidor.</summary>
    public IReadOnlyList<RenglonDeInteresesDto> InterestExplanation { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T051): lo reconocido al inicio del ciclo y no causado que se libera (informativo).</summary>
    public decimal RecognizedReleased { get; init; }

    public bool AdmiteParcial => AdmittedModes.Contains(TextosDeRecogida.ModoParcial);

    /// <summary>2026-11-27 (feature 028, E2, T066): el desglose de todo menos la cuota causada; nulo si no se puede dejar.</summary>
    public DesgloseDeRecogidaDto? BreakdownExcludingCurrentInstallment { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): con qué tratamiento de la cuota causada se marcó (en la respuesta con lo marcado).</summary>
    public int? SelectedInstallmentTreatment { get; init; }

    /// <summary>¿El asesor elige qué hacer con la cuota causada? («Preguntar» y un crédito que admite dejarla).</summary>
    public bool CuotaElegible => CurrentInstallment is { Choosable: true } && ToPayOffExcludingCurrentInstallment is not null;

    /// <summary>El tratamiento de la cuota que rige con lo que eligió el asesor (nulo, el de la línea).</summary>
    public int TratamientoDeLaCuota(int? elegido) => CurrentInstallment?.Tratamiento(elegido) ?? TextosDeRecogida.CuotaIncluida;

    /// <summary>
    /// Lo que cuesta recogerlo con el tratamiento elegido: con «Preguntar» y la cuota dejada, todo menos ella; si no, <see cref="ToPayOff"/> (que ya es el del
    /// tratamiento de la línea).
    /// </summary>
    public decimal ParaRecoger(int? elegido) =>
        CuotaElegible && elegido == TextosDeRecogida.CuotaExcluida ? ToPayOffExcludingCurrentInstallment!.Value : ToPayOff;

    /// <summary>El desglose con el tratamiento elegido (como <see cref="ParaRecoger"/>).</summary>
    public DesgloseDeRecogidaDto Desglose(int? elegido) =>
        CuotaElegible && elegido == TextosDeRecogida.CuotaExcluida && BreakdownExcludingCurrentInstallment is { } d ? d : Breakdown;
}

/// <summary>
/// 2026-11-27 (feature 028, T051; HU5): un renglón de la explicación del interés de una recogida: su clase (<c>Accrued</c>, <c>FullInstallment</c>,
/// <c>Proportional</c>, <c>Returned</c>, <c>RecognizedReleased</c>), la cuota, los días, el valor y el texto. Los días los cuenta el servidor con la
/// reconstrucción: la pantalla no los calcula. (nuevo)
/// </summary>
public sealed record RenglonDeInteresesDto(string Kind, int? Installment, int? Days, decimal Amount, string Text)
{
    public const string ClaseReconocidoQueSeLibera = "RecognizedReleased";

    /// <summary>Lo reconocido que se libera no cambia lo que se recoge: se muestra aparte.</summary>
    public bool EsInformativo => Kind == ClaseReconocidoQueSeLibera;
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
public sealed record RecogidaMarcada(Guid ProductPublicId, string Number, int Mode, decimal? Amount, decimal ToPayOff, int? InstallmentTreatment = null)
{
    /// <summary>
    /// Lo que pide el servidor (en E1, siempre un crédito). 2026-11-27 (feature 028, E2, T066): con el tratamiento de la cuota causada elegido, sólo en una
    /// recogida total.
    /// </summary>
    public RecogidaElegidaRequest ComoPedido() => new(TextosDeRecogida.ClaseCredito, ProductPublicId, Mode, Mode == TextosDeRecogida.ModoParcial ? Amount : null,
        InstallmentTreatment: Mode == TextosDeRecogida.ModoTotal ? InstallmentTreatment : null);
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
            // 2026-11-27 (feature 028, E2, T066): una recogida total con la cuota causada dejada a su canal recoge todo menos ella.
            total += m.Mode == TextosDeRecogida.ModoTotal ? p.ParaRecoger(m.InstallmentTreatment) : Math.Min(m.Amount ?? 0m, p.ToPayOff);
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
/// 2026-11-27 (feature 028, HU2, T028): a qué fecha se proyectan las recogidas de la solicitud (D) y con la información hasta cuándo (R): lo que simuló el
/// asesor en «Productos que recoge» (<c>payoffProjection</c> de la solicitud). (nuevo)
/// </summary>
public sealed record ProyeccionDeRecogidaRequest(DateOnly? ProjectedDisbursementDate, DateTime? RecordedAsOf);

/// <summary>
/// 2026-11-27 (feature 028, HU2, T028–T029): un producto que la solicitud recoge, con lo proyectado al guardarla o radicarla (<c>payoffs[]</c> del
/// detalle): número, modo, valor pedido, lo proyectado y su desglose, la cuota causada, D, R y, si otra solicitud viva lo recoge también, su número.
/// </summary>
public sealed record RecogidaDeSolicitudDto(
    int Kind,
    Guid? ProductPublicId,
    string? Reference,
    string Number,
    int Mode,
    decimal? RequestedAmount,
    decimal ProjectedAmount,
    DesgloseDeRecogidaDto Breakdown,
    int? InstallmentTreatment,
    string? InterestMode,
    CuotaCausadaDeRecogidaDto? CurrentInstallment,
    DateOnly ProjectedDate,
    DateTime RecordedAsOf,
    bool Projected,
    string? InOtherApplication,
    int Order)
{
    /// <summary>Lo mismo, como lo pide el servidor al guardar (para mandarlo tal cual si no se cambia).</summary>
    public RecogidaElegidaRequest ComoPedido() => new(Kind, ProductPublicId, Mode,
        Mode is TextosDeRecogida.ModoParcial or TextosDeRecogida.ModoAbono ? RequestedAmount : null, Reference, InstallmentTreatment);

    /// <summary>Como lo marca la tabla de productos al abrir la corrección (sólo créditos; con el tratamiento de la cuota causada que guardó).</summary>
    public RecogidaMarcada? ComoMarcada() => Kind == TextosDeRecogida.ClaseCredito && ProductPublicId is { } producto
        ? new RecogidaMarcada(producto, Number, Mode, Mode == TextosDeRecogida.ModoParcial ? RequestedAmount : null, ProjectedAmount,
            Mode == TextosDeRecogida.ModoTotal ? InstallmentTreatment : null)
        : null;
}

/// <summary>2026-11-27 (feature 028, HU2, T029): lo que recoge la solicitud en total, el neto a girar, D y R (<c>payoffSummary</c>).</summary>
public sealed record ResumenDeRecogidasDeSolicitudDto(decimal Total, decimal NetToDisburse, DateOnly ProjectedDate, DateTime RecordedAsOf);

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

    // 2026-11-27 (feature 028, E2, T066): SentInstallmentState (en qué va la cuota del período frente a su canal).
    public const int EnvioNinguno = 0;
    public const int EnvioSinRespuesta = 1;
    public const int EnvioDescontadaPendienteDeGiro = 2;
    public const int EnvioNoDescontada = 3;
    public const int EnvioNominaGenerada = 4;
    public const int EnvioNominaCalculada = 5;
    public const int EnvioNominaAprobada = 6;

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
        CuotaExcluida => "Se deja a su canal, como se envió",
        _ => "—",
    };

    /// <summary>2026-11-27 (feature 028, E2, T072): las dos opciones del selector de la cuota causada con «Preguntar».</summary>
    public static string OpcionDeCuota(int tratamiento) => tratamiento switch
    {
        CuotaIncluida => "Incluir la cuota",
        CuotaExcluida => "Dejarla a su canal",
        _ => "—",
    };

    /// <summary>2026-11-27 (feature 028, E2, T066): en qué va la cuota del período frente a su canal, en palabras.</summary>
    public static string EstadoDeEnvio(int estado) => estado switch
    {
        EnvioSinRespuesta => "Enviada a la pagaduría, sin respuesta",
        EnvioDescontadaPendienteDeGiro => "Descontada por la pagaduría, pendiente de giro",
        EnvioNoDescontada => "No descontada por la pagaduría",
        EnvioNominaGenerada => "En la nómina propia (novedad generada)",
        EnvioNominaCalculada => "En la nómina propia (corrida en borrador)",
        EnvioNominaAprobada => "Aplicada por la nómina propia",
        _ => "Sin enviar",
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

    /// <summary>
    /// 2026-11-27 (feature 028, T029): los conceptos con valor de un desglose, en el orden en que se muestran; lo que se descuenta (interés devuelto,
    /// anticipo, saldo a favor) va en negativo. Lo usan el detalle de un producto para recoger y «Lo que recoge» de la solicitud.
    /// </summary>
    public static IReadOnlyList<(string Concepto, decimal Valor)> Conceptos(DesgloseDeRecogidaDto d)
    {
        ArgumentNullException.ThrowIfNull(d);
        (string, decimal)[] todos =
        [
            ("Capital", d.Principal), ("Interés corriente", d.CurrentInterest), ("Interés devuelto por proporcionales", -d.InterestReturned),
            ("Interés cobrado por proporcionales", d.InterestCharged), ("Interés de mora", d.DefaultInterest), ("Seguro", d.Insurance),
            ("Administración", d.Administration), ("Otros cargos", d.OtherCharges), ("Anticipo a favor", -d.Advance), ("Saldo a favor", -d.CreditBalance),
        ];
        return todos.Where(c => c.Item2 != 0m).ToList();
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T051): el interés de lo que proyectó la solicitud, en palabras, desde su desglose (la solicitud no guarda los días: los da
    /// la vista previa del desembolso, que vuelve a liquidar): lo cobrado por la cuota completa y lo devuelto por período calendario.
    /// </summary>
    public static IReadOnlyList<string> InteresesDelDesglose(DesgloseDeRecogidaDto d, string? modo)
    {
        ArgumentNullException.ThrowIfNull(d);
        var textos = new List<string>();
        if (d.CurrentInterest != 0m)
            textos.Add($"Se cobra el interés causado hasta la fecha proyectada de desembolso: {Pesos(d.CurrentInterest)}.");
        if (d.InterestCharged != 0m)
            textos.Add($"Se cobra el interés completo de la cuota en curso: también el de los días que faltan hasta su vencimiento, {Pesos(d.InterestCharged)}.");
        if (d.InterestReturned != 0m)
            textos.Add($"Se devuelve el interés ya causado de cuotas cuyo período no ha empezado (período calendario): {Pesos(d.InterestReturned)}.");
        if (textos.Count == 0 && modo is "Corrido") textos.Add("Se cobra el interés corrido hasta la fecha proyectada de desembolso.");
        return textos;
    }

    /// <summary>Un valor en pesos sin centavos: <c>$ 1.234.567</c>.</summary>
    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public static string Pesos(decimal? valor) => valor is { } v ? Pesos(v) : "—";
}
