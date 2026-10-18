using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreementVersions</c> (feature 013, K2; data-model §24.2; FR-108, FR-113–FR-116): lo que rige del convenio en un intervalo
/// —periodicidad y fechas de la nómina de la pagaduría, antelación del archivo, formatos de ida y vuelta elegidos por referencia (ámbitos
/// <c>PayrollDeductionRequest</c> y <c>PayrollDeductionResult</c>, siempre genéricos), tope propio de descuento, si lleva lo vencido, reglas de
/// novedades, ventana de giro, regla del no aplicado y forma de pago alterna—. Sin cruces: una versión nueva cierra la anterior la víspera
/// (<see cref="CerrarLaVispera"/>). R = <c>CreatedAt</c> (bitemporal, T38). Una versión usada por un archivo <c>Sent</c> no se edita: se crea
/// otra (lo decide el comando, que sabe de archivos). No es hecho. (nuevo)
/// </summary>
public class PayrollAgreementVersion : AuditableEntity
{
    public int PayrollAgreementId { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>Nulo = sin fin. Una versión nueva la cierra la víspera de su inicio.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Periodicidad de la nómina de la pagaduría (la enumeración de la parte 1; C44).</summary>
    public RepaymentPeriodicity Periodicity { get; set; } = RepaymentPeriodicity.Monthly;

    /// <summary>Día de corte de novedades.</summary>
    public byte CutoffDay { get; set; }

    /// <summary>Día de pago de la nómina de la pagaduría (fecha del descuento).</summary>
    public byte PaymentDay { get; set; }

    /// <summary>Antelación con que se genera el archivo frente al corte (FR-113); la vigila <c>cartera.archivos-de-descuento</c>.</summary>
    public int SendLeadDays { get; set; }

    /// <summary>Formato de ida: ámbito <c>PayrollDeductionRequest</c>, genérico.</summary>
    public int OutboundFormatId { get; set; }

    /// <summary>Formato de vuelta: ámbito <c>PayrollDeductionResult</c>, genérico (lo usa Recaudo y la vista previa).</summary>
    public int ResultFormatId { get; set; }

    /// <summary>Tope propio del convenio como fracción; nunca por encima de <c>LIBRANZA_CAPACIDAD_DESCUENTO</c> (T42). Nulo = sin tope propio.</summary>
    public decimal? DeductionCapFraction { get; set; }

    /// <summary>El archivo lleva lo vencido además de lo exigible del período (FR-113).</summary>
    public bool IncludesPastDue { get; set; }

    /// <summary>Qué novedades reporta la pagaduría (altas, cambios de valor, cancelaciones), en JSON.</summary>
    public string NoveltyRulesJson { get; set; } = "{}";

    /// <summary>Ventana de giro en días; nulo = <c>Libranza.VentanaDeGiroDias</c> vigente (FR-114).</summary>
    public int? TransferWindowDays { get; set; }

    /// <summary>
    /// Regla del convenio para el no aplicado (FR-115): <c>IncludedInNextFile</c>, <c>MovedToCashPayment</c> o <c>SentToCollection</c>. Las
    /// otras dos acciones del enum (<c>DebtorNotified</c>, <c>Closed</c>) son seguimientos, no reglas (<see cref="EsReglaDeNoAplicado"/>).
    /// </summary>
    public NonDeductionFollowUpAction NonDeductionAction { get; set; } = NonDeductionFollowUpAction.MovedToCashPayment;

    /// <summary>Forma de pago alterna al retiro sin nuevo empleador; por defecto caja (FR-116, C44).</summary>
    public RepaymentChannel AlternateRepaymentChannel { get; set; } = RepaymentChannel.Cashier;

    public string? Notes { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0823) --

    /// <summary>¿Rige el <paramref name="dia"/>?</summary>
    public bool VigenteEn(DateOnly dia) => ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>¿Se cruza con el intervalo [<paramref name="desde"/>, <paramref name="hasta"/>] (nulo = sin fin)?</summary>
    public bool SeCruzaCon(DateOnly desde, DateOnly? hasta) =>
        ValidFrom <= (hasta ?? DateOnly.MaxValue) && (ValidTo ?? DateOnly.MaxValue) >= desde;

    /// <summary>
    /// Cierra esta versión la víspera de <paramref name="inicioDeLaNueva"/> (una versión nueva cierra la anterior; data-model §24.2). Sólo una
    /// versión que empezó antes y sigue abierta o termina después; si la nueva empieza el mismo día o antes que ésta, es un cruce
    /// (<c>Lending.PayrollAgreement.VersionOverlaps</c>) y no se cierra nada.
    /// </summary>
    public void CerrarLaVispera(DateOnly inicioDeLaNueva)
    {
        if (inicioDeLaNueva <= ValidFrom)
            throw new InvalidOperationException($"La versión que empieza el {ValidFrom:yyyy-MM-dd} no se cierra con otra que empieza el {inicioDeLaNueva:yyyy-MM-dd}.");
        var vispera = inicioDeLaNueva.AddDays(-1);
        if (ValidTo is { } hasta && hasta <= vispera)
            throw new InvalidOperationException($"La versión ya termina el {hasta:yyyy-MM-dd}.");
        ValidTo = vispera;
    }

    /// <summary>¿La acción sirve como regla del convenio para el no aplicado (FR-115)?</summary>
    public static bool EsReglaDeNoAplicado(NonDeductionFollowUpAction accion) => accion is NonDeductionFollowUpAction.IncludedInNextFile
        or NonDeductionFollowUpAction.MovedToCashPayment or NonDeductionFollowUpAction.SentToCollection;

    /// <summary>
    /// La versión del convenio que rige el <paramref name="dia"/> entre <paramref name="versiones"/>, o nula. Si hubiera dos (no debería: no
    /// se cruzan), la que empezó después.
    /// </summary>
    public static PayrollAgreementVersion? Vigente(IEnumerable<PayrollAgreementVersion> versiones, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(versiones);
        return versiones.Where(v => !v.IsDeleted && v.VigenteEn(dia)).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
    }
}
