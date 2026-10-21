using System.Text;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Los textos del lado contable de la integración con Cartera (feature 013, US7, T0664–T0668): las familias de rol con que se agrupan
/// las pestañas de la matriz (las trae el catálogo en <c>Family</c>, <c>FamiliaDeRol</c> en Application), los roles y las dimensiones con
/// tilde, los tipos de hecho legibles, los indicadores regulatorios y los códigos de error con que la pantalla ramifica. Las pantallas no
/// ramifican por el texto de un error ni muestran el nombre de otro sistema. (nuevo)
/// </summary>
public static class TextosDeContabilidadDeCartera
{
    // ------------------------------------------------------------------------------------------- familias --

    /// <summary>Las familias en el orden de las pestañas, con su nombre en pantalla.</summary>
    public static IReadOnlyList<(string Clave, string Nombre)> Familias { get; } =
    [
        ("Saldo", "Cartera, intereses y deterioro"),
        ("Orden", "Orden y castigada"),
        ("Puente", "Recaudo, desembolso y Comercial"),
        ("PasivoConElDeudor", "Pasivos con el deudor"),
        ("Pasivo", "Seguros y terceros por pagar"),
        ("Resultado", "Resultados"),
        ("Diferido", "Ingresos diferidos"),
        ("Contingente", "Contingentes"),
        // Feature 014 (T149): las familias de captaciones y aportes.
        ("Captacion", "Captaciones"),
        ("Patrimonio", "Aportes y fondos"),
    ];

    public static string Familia(string? clave) => Familias.FirstOrDefault(f => f.Clave == clave).Nombre ?? clave ?? "—";

    // ---------------------------------------------------------------------------------------------- roles --

    /// <summary>El nombre de un rol de la matriz (los códigos no llevan tilde ni espacios).</summary>
    public static string Rol(string? rol) => rol switch
    {
        null => "—",
        "Capital" => "Capital",
        "InteresPorCobrar" => "Interés por cobrar",
        "PagoPorCuentaDelAsociado" => "Pago por cuenta del asociado",
        "CargoPorCobrar" => "Cargo por cobrar",
        "DeterioroIndividual" => "Deterioro individual",
        "DeterioroGeneral" => "Deterioro general",
        "SuspensoEnOrden" => "Intereses en suspenso (orden)",
        "ContraOrdenContingente" => "Contrapartida de orden contingente",
        "CastigadaEnOrden" => "Cartera castigada (orden)",
        "ContraOrdenControl" => "Contrapartida de orden de control",
        "Anticipo" => "Anticipo",
        "SaldoAFavor" => "Saldo a favor",
        "SeguroPorPagar" => "Seguro por pagar",
        "CuentaPorPagarATercero" => "Cuenta por pagar a tercero",
        "RecaudoPorAplicar" => "Recaudo por aplicar",
        "DesembolsoPorGirar" => "Desembolso por girar",
        "PuenteComercial" => "Puente con Comercial",
        "CuentaPorCobrarComercial" => "Cuenta por cobrar de Comercial",
        "Ingreso" => "Ingreso",
        "GastoDeterioro" => "Gasto por deterioro",
        "RecuperacionDeterioro" => "Recuperación de deterioro",
        "GastoDeCastigoOCondonacion" => "Gasto por castigo o condonación",
        "IngresoPorRecuperacion" => "Ingreso por recuperación",
        "IngresoDiferido" => "Ingreso diferido",
        "ContingenteAcreedora" => "Contingente acreedora",
        "ContraContingenteAcreedora" => "Contrapartida contingente acreedora",
        // Feature 014 (T149): los 17 roles de captaciones y aportes.
        "CapitalDeCaptacion" => "Capital de captación",
        "CapitalDeCaptacionInactiva" => "Capital de captación inactiva",
        "InteresPorPagar" => "Interés por pagar",
        "GastoPorIntereses" => "Gasto por intereses",
        "RetencionPorPagar" => "Retención por pagar",
        "GmfPorPagar" => "GMF por pagar",
        "GastoPorBonificacion" => "Gasto por bonificación",
        "IngresoPorCargos" => "Ingreso por cargos",
        "CargoDeCuentaPorCobrar" => "Cargo de cuenta por cobrar",
        "AportesSociales" => "Aportes sociales",
        "FondoDeRevalorizacion" => "Fondo de revalorización",
        "FondoDeExcedentes" => "Fondo de excedentes",
        "DevolucionPorPagar" => "Devolución por pagar",
        "NoReclamadosPorPagar" => "No reclamados por pagar",
        "EmbargosPorConsignar" => "Embargos por consignar",
        "IngresoPorPenalidad" => "Ingreso por penalidad",
        "PerdidaAbsorbida" => "Pérdida absorbida",
        "RetencionDeAsambleaPorPagar" => "Retención de la asamblea por pagar",
        _ => rol,
    };

    // -------------------------------------------------------------------------------- naturaleza (feature 014) --

    /// <summary>Los roles de captaciones y aportes (feature 014): los que filtra la naturaleza «Captaciones y aportes» de la matriz y la completitud.</summary>
    public static IReadOnlySet<string> RolesDeCaptaciones { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "CapitalDeCaptacion", "CapitalDeCaptacionInactiva", "InteresPorPagar", "GastoPorIntereses", "RetencionPorPagar", "GmfPorPagar",
        "GastoPorBonificacion", "IngresoPorCargos", "CargoDeCuentaPorCobrar", "AportesSociales", "FondoDeRevalorizacion", "FondoDeExcedentes",
        "DevolucionPorPagar", "NoReclamadosPorPagar", "EmbargosPorConsignar", "IngresoPorPenalidad", "PerdidaAbsorbida", "RetencionDeAsambleaPorPagar",
    };

    /// <summary>El filtro de naturaleza de la matriz y la completitud: créditos, captaciones y aportes, o todas.</summary>
    public static IReadOnlyList<(string Clave, string Nombre)> FiltrosDeNaturaleza { get; } =
    [
        ("", "Todas"),
        ("Placement", "Créditos"),
        ("Deposit", "Captaciones y aportes"),
    ];

    /// <summary>
    /// ¿El rol es de la naturaleza del filtro? El puente del recaudo es de las dos (recauda créditos y cuentas); sin filtro, todos.
    /// </summary>
    public static bool RolDeLaNaturaleza(string? rol, string? filtro) => string.IsNullOrEmpty(filtro) || rol == "RecaudoPorAplicar"
        || (filtro == "Deposit") == (rol is not null && RolesDeCaptaciones.Contains(rol));

    /// <summary>El nombre de una naturaleza de línea.</summary>
    public static string NaturalezaDeLinea(string? naturaleza) => naturaleza switch
    {
        "Placement" => "Créditos",
        "Deposit" => "Captaciones",
        "Contribution" => "Aportes",
        null or "" => "—",
        _ => naturaleza,
    };

    /// <summary>Un valor de una dimensión de captaciones (clase, plazo de emisión, clase de aporte o marca) en pantalla.</summary>
    public static string ValorDeCaptacion(string? valor) => valor switch
    {
        "Demand" => "A la vista",
        "Contractual" => "Contractual",
        "Permanent" => "Permanente",
        "TermDeposit" => "CDAT",
        "UnderSixMonths" => "Menos de 6 meses",
        "SixToTwelve" => "De 6 a 12 meses",
        "TwelveToEighteen" => "De 12 a 18 meses",
        "EighteenOrMore" => "18 meses o más",
        "Minimum" => "Mínimos no reducibles",
        "Ordinary" => "Ordinarios",
        "Extraordinary" => "Extraordinarios",
        "Revalued" => "Revalorizados",
        "Standard" => "Estándar",
        "GmfExempt" => "Exenta del GMF",
        "Garnished" => "Embargada",
        "Deposit" => "Captaciones",
        "Contribution" => "Aportes",
        null or "" => "—",
        _ => valor,
    };

    public static string Naturaleza(string? naturaleza) => naturaleza switch
    {
        "Debit" => "Débito",
        "Credit" => "Crédito",
        _ => naturaleza ?? "—",
    };

    // ---------------------------------------------------------------------------------------- dimensiones --

    /// <summary>El nombre de una dimensión de la API (<c>creditLineCode</c>…) para la pantalla.</summary>
    public static string Dimension(string dimension) => dimension switch
    {
        "operation" => "Hecho",
        "creditLineCode" => "Línea de crédito",
        "modalityCode" => "Modalidad",
        "subsegmentCode" => "Subsegmento",
        "riskCategory" => "Categoría",
        "conceptCode" => "Concepto",
        "isEmployee" => "Empleado",
        "inGracePeriod" => "En período de gracia",
        "onBehalfOfAssociate" => "Por cuenta del asociado",
        "restructured" => "Reestructurado",
        "collectorClass" => "Clase de recaudador",
        "reasonCode" => "Motivo",
        "branch" => "Sucursal",
        "costCenter" => "Centro de costo",
        "lineNature" => "Naturaleza",
        "depositClass" => "Clase de captación",
        "issueTermBand" => "Plazo de emisión",
        "contributionClass" => "Clase de aporte",
        "accountFlag" => "Marca de la cuenta",
        _ => dimension,
    };

    /// <summary>Las dimensiones que son sí/no.</summary>
    public static bool EsBooleana(string dimension) => dimension is "isEmployee" or "inGracePeriod" or "onBehalfOfAssociate" or "restructured";

    /// <summary>Un motivo de la matriz (nivel del deterioro, forma del giro, castigo o condonación, contingente).</summary>
    public static string Motivo(string? motivo) => motivo switch
    {
        "Individual" => "Individual",
        "General" => "General",
        "Additional" => "Adicional",
        "Transfer" => "Transferencia",
        "Check" => "Cheque",
        "ThirdPartyPayment" => "Pago a tercero",
        "WriteOff" => "Castigo",
        "Waiver" => "Condonación",
        "ApprovedNotDisbursed" => "Aprobado sin desembolsar",
        "RevolvingLimit" => "Cupo rotativo",
        "CollateralReceived" => "Garantía recibida",
        _ => motivo ?? "—",
    };

    // ------------------------------------------------------------------------------------------ hechos --

    private static readonly IReadOnlyDictionary<string, string> ConTilde = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Interes"] = "interés",
        ["Credito"] = "crédito",
        ["Anulacion"] = "anulación",
        ["Causacion"] = "causación",
        ["Condonacion"] = "condonación",
        ["Obligacion"] = "obligación",
        ["Recuperacion"] = "recuperación",
        ["Calificacion"] = "calificación",
        ["Dimension"] = "dimensión",
        ["Garantia"] = "garantía",
        ["Pagaduria"] = "pagaduría",
        ["Rotativo"] = "rotativo",
    };

    /// <summary>
    /// Un tipo de hecho legible: los tipos se guardan sin tildes y en una palabra (<c>InteresCausado</c>) y la pantalla los muestra
    /// separados y con tilde («Interés causado»).
    /// </summary>
    public static string TipoDeHecho(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo)) return "—";
        var palabras = new List<string>();
        var actual = new StringBuilder();
        foreach (var c in tipo)
        {
            if (char.IsUpper(c) && actual.Length > 0)
            {
                palabras.Add(actual.ToString());
                actual.Clear();
            }
            actual.Append(c);
        }
        if (actual.Length > 0) palabras.Add(actual.ToString());
        var texto = string.Join(' ', palabras.Select(p => ConTilde.TryGetValue(p, out var t) ? t : p.ToLowerInvariant()));
        return char.ToUpperInvariant(texto[0]) + texto[1..];
    }

    /// <summary>El modo de paso de un hecho tal como lo trae la entrega (<c>DeliveryMode</c> por su nombre).</summary>
    public static string Modo(string? modo) => modo switch
    {
        "Online" => "En línea",
        "Batch" => "Por lotes",
        "NotPosted" => "No pasa",
        "Always" => "Siempre",
        _ => modo ?? "—",
    };

    /// <summary>El estado de una entrega por su nombre (<c>DeliveryStatus</c>).</summary>
    public static string EstadoDeEntrega(string? estado) => estado switch
    {
        "Pending" => "Pendiente",
        "InBatch" => "En lote",
        "Processed" => "Procesado",
        "Rejected" => "Rechazado",
        "NotApplicable" => "No aplica",
        "ValidationFailed" => "Validación fallida",
        _ => estado ?? "—",
    };

    /// <summary>Lo que dijo la validación previa por su nombre (<c>PrevalidationOutcome</c>).</summary>
    public static string ValidacionPrevia(string? resultado) => resultado switch
    {
        "Postable" => "Contabilizable",
        "NoResponse" => "Sin respuesta",
        "NotApplicable" => "No aplica",
        "NotPostable" => "No contabilizable (con advertencia)",
        _ => resultado ?? "—",
    };

    /// <summary>Un hecho que llegó sin comprobante (<c>MotivoSinComprobante</c>).</summary>
    public static string SinComprobante(int? motivo) => motivo switch
    {
        1 => "Registrado sin comprobante (informativo)",
        2 => "Registrado sin comprobante (valor cero)",
        3 => "No incorporado",
        _ => "",
    };

    /// <summary>
    /// El módulo de quien corrige un hallazgo (<c>QuienCorrigeDto.Module</c>): <c>Lending</c> sin pantalla es un defecto del emisor y va a
    /// soporte (§13.3).
    /// </summary>
    public static string ModuloQueCorrige(string? modulo) => modulo switch
    {
        "Accounting" => "Contabilidad",
        "Lending" => "Cartera (soporte)",
        "Core" => "Maestros",
        _ => modulo ?? "—",
    };

    // ------------------------------------------------------------------------------------- indicadores --

    /// <summary><c>RegulatoryIndicatorKind</c>: número, nombre con que viaja y texto.</summary>
    public static IReadOnlyList<(int Valor, string Nombre, string Texto)> TiposDeIndicador { get; } =
    [
        (1, "TechnicalCapital", "Patrimonio técnico"),
        (2, "SolvencyRatio", "Relación de solvencia"),
    ];

    public static string TipoDeIndicador(int tipo) => TiposDeIndicador.FirstOrDefault(t => t.Valor == tipo).Texto ?? tipo.ToString();

    /// <summary><c>SolvencyRatio</c>: se registra como fracción (0,12 = 12 %).</summary>
    public const int RelacionDeSolvencia = 2;

    // ------------------------------------------------------------------------------------------- códigos --

    /// <summary>El cierre del mes con Cartera activa que todavía no cerró ese mes: no se reconoce.</summary>
    public const string CarteraSinCerrar = "Accounting.Period.LendingNotClosed";

    /// <summary>El cierre del mes con hechos de Cartera sin procesar: se reconoce con <c>acknowledgeLendingPending</c>.</summary>
    public const string CarteraPendiente = "Accounting.Period.LendingPending";

    /// <summary>El cierre del mes que dejaría sin activar una carga de Cartera: se reconoce con <c>acknowledgeLendingLoad</c>.</summary>
    public const string CargaQueVence = "Accounting.Period.LendingLoadWillExpire";

    /// <summary>Ya hay un lote de Cartera en curso con el mismo alcance; <c>data.batchPublicId</c> lo nombra.</summary>
    public const string LoteEnCurso = "Accounting.LendingBatch.AlreadyRunning";

    /// <summary>El envío posterior de tipos que siguen en «no pasa».</summary>
    public const string ModoTodaviaNoPasa = "Integration.SendNotApplicable.ModeStillNotPosted";
}
