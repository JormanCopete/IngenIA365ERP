using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de K3 (feature 013, US11, T0892): mecanismos y causas de la novedad, estados de la novedad y de la condonación, papeles y
/// estados de los consentimientos, conceptos que se condonan, eventos del deudor, reclamaciones e insolvencia. Los enums llegan como número y salen
/// por nombre en las peticiones; ninguno muestra un código técnico (FR-013). (nuevo)
/// </summary>
public static class TextosDeNovedades
{
    /// <summary>Los mecanismos: nombre que se manda, número que vuelve y texto.</summary>
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Mecanismos { get; } =
    [
        ("Extension", 1, "Prórroga (más plazo)"), ("PaymentDateChange", 2, "Cambio de fecha de pago"), ("RateChange", 3, "Cambio de tasa"),
        ("Novation", 4, "Novación"), ("Refinancing", 5, "Refinanciación"), ("BranchChange", 6, "Cambio de oficina"),
    ];

    public static string Mecanismo(int v) => Mecanismos.FirstOrDefault(m => m.Valor == v).Texto ?? v.ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<(string Nombre, string Texto)> Motivos { get; } =
        [("DebtorRequest", "A petición del deudor, sin dificultades de pago"), ("Hardship", "Por dificultades de pago del deudor")];

    public static string Causa(int v) => v switch { 1 => "Reestructuración", 2 => "Modificación", _ => "—" };

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Estados { get; } =
    [
        ("Draft", 1, "Borrador"), ("Submitted", 2, "Radicada"), ("Approved", 3, "Aprobada"), ("Rejected", 4, "Negada"), ("Applied", 5, "Aplicada"),
        ("Cancelled", 6, "Anulada"),
    ];

    public static string Estado(int v) => Estados.FirstOrDefault(e => e.Valor == v).Texto ?? "—";

    public static string PapelDelCredito(int v) => v switch { 1 => "Modificado", 2 => "Recogido", 3 => "Resultante", _ => "—" };

    public static string PapelDeLaParte(int v) => v switch { 1 => "Codeudor", 2 => "Avalista", 3 => "Propietario de la garantía", _ => "—" };

    public static string EstadoDelConsentimiento(int v) => v switch { 1 => "Pendiente", 2 => "Aceptó", 3 => "No aceptó", _ => "—" };

    public static string Resolucion(int? v) => v switch
    {
        1 => "No se aplica la novedad",
        2 => "Se aplica con la garantía extinguida",
        _ => "—",
    };

    public static IReadOnlyList<(string Nombre, string Texto)> Resoluciones { get; } =
        [("DoNotApply", "No aplicar la novedad"), ("ApplyWithGuaranteeExtinguished", "Aplicarla con la garantía extinguida")];

    public static string Decision(int v) => v switch { 1 => "Aprobó", 2 => "Aprobó con condiciones", 3 => "Negó", 4 => "Devolvió", _ => "—" };

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Periodicidades { get; } =
        [("Monthly", 30, "Mensual"), ("Biweekly", 15, "Quincenal"), ("Bimonthly", 60, "Bimestral"), ("Quarterly", 90, "Trimestral"),
            ("SemiAnnual", 180, "Semestral"), ("Annual", 360, "Anual")];

    // ----------------------------------------------------------------------------------------- condonaciones --

    /// <summary>Los conceptos que se condonan: nombre que se manda, número que vuelve y texto.</summary>
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Conceptos { get; } =
    [
        ("DefaultInterest", 3, "Interés de mora"), ("CurrentInterest", 2, "Interés corriente"),
        ("CurrentInterestInSuspense", 4, "Interés corriente en suspenso"), ("DefaultInterestInSuspense", 5, "Interés de mora en suspenso"),
        ("CollectionFee", 7, "Honorarios de cobranza"), ("Insurance", 6, "Seguros"), ("Principal", 1, "Capital (sólo con nivel de excepción)"),
    ];

    public static string Concepto(int v) => Conceptos.FirstOrDefault(c => c.Valor == v).Texto ?? "—";

    // ------------------------------------------------------------------ eventos del deudor, reclamación, insolvencia --

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Eventos { get; } =
        [("Deceased", 7, "Fallecimiento"), ("AssociateWithdrawal", 6, "Retiro de la cooperativa"), ("Garnishment", 9, "Embargo")];

    public static string Evento(int v) => Eventos.FirstOrDefault(e => e.Valor == v).Texto ?? "—";

    public static string EstadoDeReclamacion(int v) => v switch
    {
        1 => "En trámite",
        2 => "Pagada por la aseguradora",
        3 => "Negada",
        4 => "Desistida",
        _ => "—",
    };

    public static IReadOnlyList<(string Nombre, string Texto)> CierresDeReclamacion { get; } =
        [("Denied", "La aseguradora la negó"), ("Withdrawn", "Se desistió")];

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> Regimenes { get; } =
    [
        ("NaturalPersonDebtNegotiation", 2, "Negociación de deudas de persona natural no comerciante"),
        ("BusinessReorganization", 1, "Reorganización empresarial"),
    ];

    public static string Regimen(int? v) => v is { } r ? Regimenes.FirstOrDefault(x => x.Valor == r).Texto ?? "—" : "Sin registrar";

    public static string EstadoDeInsolvencia(int v) => v switch { 1 => "En negociación", 2 => "Con acuerdo", 3 => "Fracasada", _ => "—" };
}
