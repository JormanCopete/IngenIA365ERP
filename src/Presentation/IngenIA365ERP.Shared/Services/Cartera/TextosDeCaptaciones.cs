namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de las captaciones y los aportes (feature 014, US1, T070): naturalezas, clases, métodos de saldo, modos de tramos y de
/// cuota, y lo del CDAT y el contractual. Los valores son los números de los enums del servidor (<c>DepositEnums</c>), que la API devuelve como
/// número; los pedidos los mandan igual. Ningún texto muestra un código técnico. (nuevo)
/// </summary>
public static class TextosDeCaptaciones
{
    public const int Colocacion = 1;
    public const int Captacion = 2;
    public const int Aportes = 3;

    public const int ALaVista = 1;
    public const int Contractual = 2;
    public const int Permanente = 3;
    public const int Cdat = 4;

    public static IReadOnlyList<(int Valor, string Texto)> Naturalezas { get; } = [(1, "Crédito"), (2, "Captación"), (3, "Aportes")];

    public static IReadOnlyList<(int Valor, string Texto)> Clases { get; } =
        [(ALaVista, "Ahorro a la vista"), (Contractual, "Ahorro contractual"), (Permanente, "Ahorro permanente"), (Cdat, "CDAT")];

    public static IReadOnlyList<(int Valor, string Texto)> MetodosDeSaldo { get; } = [(1, "Saldo diario"), (2, "Saldo promedio"), (3, "Saldo mínimo")];

    public static IReadOnlyList<(int Valor, string Texto)> ModosDeTramos { get; } =
        [(1, "Todo el saldo a la tasa de su tramo"), (2, "Cada porción a la tasa de su tramo")];

    public static IReadOnlyList<(int Valor, string Texto)> ModalidadesDeInteres { get; } = [(1, "Vencidos"), (2, "Anticipados")];

    public static IReadOnlyList<(int Valor, string Texto)> ReglasDeProrroga { get; } =
        [(1, "La misma tasa"), (2, "La tasa vigente para el plazo"), (3, "Una tasa pactada")];

    public static IReadOnlyList<(int Valor, string Texto)> TratamientosAlVencer { get; } = [(1, "Deja de causar (vencido por pagar)"), (2, "Pasa a la vista")];

    public static IReadOnlyList<(int Valor, string Texto)> PenalidadesDeRedencion { get; } =
        [(1, "Reliquidar a una tasa de penalidad"), (2, "Descontar una parte del interés causado")];

    public static IReadOnlyList<(int Valor, string Texto)> EfectosDelIncumplimiento { get; } =
        [(1, "Tasa castigo"), (2, "Pasa a la vista"), (3, "Se cancela")];

    public static IReadOnlyList<(int Valor, string Texto)> ReconocimientosDeBonificacion { get; } = [(1, "Al cumplir"), (2, "Provisionada")];

    public static IReadOnlyList<(int Valor, string Texto)> ModosDeValorDeAportes { get; } =
        [(1, "Valor fijo"), (2, "Porcentaje del ingreso"), (3, "Salarios mínimos")];

    public static IReadOnlyList<(int Valor, string Texto)> AccionesDelExceso { get; } = [(1, "Rechazarlo"), (2, "Devolverlo por decisión")];

    /// <summary>Las tarifas de retención que una línea puede nombrar (vacía = la de su clase).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> TarifasDeRetencion { get; } =
    [
        ("", "La de su clase"), ("Retencion.TarifaAhorro", "Tarifa de ahorro"), ("Retencion.TarifaCdat", "Tarifa de CDAT"),
        ("Retencion.TarifaContractual", "Tarifa del contractual"), ("Retencion.TarifaPermanente", "Tarifa del permanente"),
    ];

    public static string Naturaleza(int v) => De(Naturalezas, v);

    public static string Clase(int? v) => v is { } x ? De(Clases, x) : "—";

    public static string MetodoDeSaldo(int v) => De(MetodosDeSaldo, v);

    public static string ModoDeValorDeAportes(int v) => De(ModosDeValorDeAportes, v);

    /// <summary>El parámetro <c>?naturaleza=</c> de la pantalla del catálogo: <c>captacion</c>, <c>aportes</c> o <c>credito</c>.</summary>
    public static int NaturalezaDeLaRuta(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "captacion" or "captación" => Captacion,
        "aportes" => Aportes,
        _ => Colocacion,
    };

    public static string RutaDeLaNaturaleza(int naturaleza) => naturaleza switch
    {
        Captacion => "captacion",
        Aportes => "aportes",
        _ => "credito",
    };

    // ------------------------------------------------------------------------------------------- cuentas (US2, T079) --

    public const int Titular = 1;
    public const int Cotitular = 2;
    public const int Firmante = 3;
    public const int Representante = 4;
    public const int Beneficiario = 5;

    public const int Indistinto = 1;
    public const int Conjunto = 2;

    public const int EnApertura = 1;
    public const int Activa = 2;
    public const int Cancelada = 6;

    public static IReadOnlyList<(int Valor, string Texto)> Papeles { get; } =
        [(Titular, "Titular"), (Cotitular, "Cotitular"), (Firmante, "Firmante"), (Representante, "Representante legal"), (Beneficiario, "Beneficiario")];

    /// <summary>Los papeles que se agregan a mano (el titular es el de la cuenta y los beneficiarios van aparte).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> PapelesQueSeAgregan { get; } =
        [(Cotitular, "Cotitular"), (Firmante, "Firmante"), (Representante, "Representante legal")];

    public static IReadOnlyList<(int Valor, string Texto)> Manejos { get; } = [(Indistinto, "Indistinto (firma cualquiera)"), (Conjunto, "Conjunto (firman juntos)")];

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeCuenta { get; } =
        [(EnApertura, "En apertura"), (Activa, "Activa"), (3, "Inactiva"), (4, "Vencida"), (5, "En cancelación"), (Cancelada, "Cancelada")];

    public static IReadOnlyList<(string Valor, string Texto)> Fundamentos { get; } =
    [
        ("Parent", "Padre o madre"), ("Guardian", "Tutor o curador"), ("CourtOrder", "Decisión judicial"),
        ("LegalEntityRepresentative", "Representante de la persona jurídica"),
    ];

    public static string Papel(int v) => De(Papeles, v);

    public static string Manejo(int v) => De(Manejos, v);

    public static string EstadoDeCuenta(int v) => De(EstadosDeCuenta, v);

    public static string Fundamento(string? v) => Fundamentos.FirstOrDefault(f => f.Valor == v).Texto ?? v ?? "—";

    /// <summary>Por qué una cuenta sigue en apertura (<c>openingBlockers</c>).</summary>
    public static string RetencionDeApertura(string v) => v switch
    {
        "ScreeningMatch" => "Coincidencia en listas restrictivas: la libera el oficial de cumplimiento",
        "InitialDeposit" => "Espera el depósito de apertura",
        _ => v,
    };

    /// <summary>Los conceptos del libro de la cuenta (<c>MemberAccountConcept</c>, por nombre en <c>balances.byConcept</c>).</summary>
    public static string Concepto(string v) => v switch
    {
        "Principal" => "Capital",
        "InterestPayable" => "Interés por pagar",
        "Withholding" => "Retención",
        "Gmf" => "GMF",
        "ContributionMinimum" => "Aporte mínimo",
        "ContributionOrdinary" => "Aportes ordinarios",
        "ContributionExtraordinary" => "Aportes extraordinarios",
        "ContributionRevaluation" => "Revalorización",
        "ChargeReceivable" => "Cargos por cobrar",
        "Bonus" => "Bonificación",
        _ => v,
    };

    /// <summary>Las marcas de la lista de cuentas.</summary>
    public static string MarcaDeCuenta(string v) => v switch
    {
        "RequiresDataUpdate" => "Actualizar datos",
        "Garnished" => "Embargada",
        "GmfExempt" => "Exenta de GMF",
        "OverdrawnTechnical" => "Sobregiro técnico",
        _ => v,
    };

    // ------------------------------------------------------------------------------------ movimientos (US4, T124) --

    /// <summary>Las clases de operación de una cuenta (<c>MemberAccountOperationKind</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeOperacion { get; } =
    [
        (1, "Saldo inicial"), (2, "Consignación"), (3, "Retiro"), (4, "Traslado"), (5, "Reverso"), (6, "Liquidación de intereses"), (7, "Constitución de CDAT"),
        (8, "Pago de intereses de CDAT"), (9, "Prórroga de CDAT"), (10, "Renovación de CDAT"), (11, "Redención de CDAT"), (12, "Cancelación"), (13, "Cargo"),
        (14, "Condonación de cargo"), (15, "Cruce interno"), (16, "Débito automático"), (17, "Devolución de aportes"), (18, "Abono por asamblea"),
        (19, "Reclasificación"), (20, "Vencimiento contractual"), (21, "A no reclamados"), (22, "Entrega por fallecimiento"), (23, "Consignación de embargo"),
        (24, "Contrato de ahorro"), (25, "Incumplimiento del contrato"), (26, "Bonificación del contrato"),
    ];

    /// <summary>Las clases de fila del libro (<c>MemberAccountTransactionKind</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeFila { get; } =
    [
        (1, "Saldo inicial"), (2, "Consignación"), (3, "Retiro"), (4, "Traslado recibido"), (5, "Traslado enviado"), (6, "Interés causado"),
        (7, "Ajuste de causación"), (8, "Interés abonado"), (9, "Interés pagado"), (10, "Retención"), (11, "GMF"), (12, "Cargo"), (13, "Penalidad"),
        (14, "Capitalización"), (15, "Reclasificación"), (16, "Cruce interno"), (17, "Devolución"), (18, "A no reclamados"), (19, "Embargo consignado"),
        (20, "Cargo condonado"), (21, "Bonificación"), (22, "Abono por asamblea"),
    ];

    /// <summary>Los conceptos del libro por número (<c>MemberAccountConcept</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Conceptos { get; } =
    [
        (1, "Capital"), (2, "Interés por pagar"), (3, "Retención"), (4, "GMF"), (5, "Aporte mínimo"), (6, "Aportes ordinarios"), (7, "Aportes extraordinarios"),
        (8, "Revalorización"), (9, "Cargos por cobrar"), (10, "Bonificación"),
    ];

    /// <summary>Los estados de un cargo (<c>AccountChargeStatus</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeCargo { get; } = [(1, "Cobrado"), (2, "Pendiente"), (3, "Condonado"), (4, "Reversado")];

    public const int CargoCobrado = 1;
    public const int CargoPendiente = 2;

    /// <summary>Los alcances de una autorización de débito automático (<c>DirectDebitScope</c>, por nombre al registrarla).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> AlcancesDeDebito { get; } =
        [("AllLoansOfHolder", "Todos los créditos del titular"), ("SingleLoan", "Un crédito")];

    public static string ClaseDeOperacion(int v) => De(ClasesDeOperacion, v);

    public static string ClaseDeFila(int v) => De(ClasesDeFila, v);

    public static string Concepto(int v) => De(Conceptos, v);

    public static string EstadoDeCargo(int v) => De(EstadosDeCargo, v);

    public static string AlcanceDeDebito(int v) => v == 1 ? "Un crédito" : "Todos los créditos del titular";

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int v)
    {
        foreach (var (valor, texto) in lista)
            if (valor == v) return texto;
        return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
