namespace IngenIA365ERP.Domain.Payroll.Policies;

/// <summary>
/// Catálogo cerrado de las políticas por empresa con vigencia (<c>PAY_CompanyPolicies</c>,
/// data-model §2.1, R4). Una política es una <b>decisión de la cooperativa</b> —qué semana
/// laboral tiene, si paga las vacaciones por anticipado, cómo propone los descuentos al
/// retiro—, nunca un valor legal: por eso los valores admitidos y el defecto van aquí
/// <b>como texto</b> y el lector (<c>PayrollPolicyReader</c>) los tipa. Un valor legal
/// (umbral de exoneración, porcentaje compensable) sigue siendo un parámetro con vigencia en
/// <c>PAY_LegalParameters</c>.
///
/// <para>
/// Dos claves llegan copiadas de <c>COR_SystemSettings</c> por la migración de datos
/// (<c>Exonerada114_1</c> desde <c>Payroll.ApplyEmployerExemption</c>,
/// <c>AllowSameUserApproval</c> desde su homónima); el lector cae a esa tabla sólo si la clave
/// no existe aquí, hasta que se retire.
/// </para>
/// </summary>
public static class CompanyPolicyKeys
{
    public const string Exonerada114_1 = "Exonerada114_1";
    public const string SemanaLaboral = "SemanaLaboral";
    public const string VacacionesPagoAnticipado = "VacacionesPagoAnticipado";
    public const string CotizaArlEnVacaciones = "CotizaArlEnVacaciones";
    public const string RetefteTopesAnualesModo = "RetefteTopesAnualesModo";
    public const string P2SecuenciaDepuracion = "P2SecuenciaDepuracion";
    public const string DianPlazoComputo = "DianPlazoComputo";
    public const string DianMedioPagoMapa = "DianMedioPagoMapa";
    public const string DeduccionAlRetiroModo = "DeduccionAlRetiroModo";
    public const string ArranqueNominaFecha = "ArranqueNominaFecha";
    public const string AllowSameUserApproval = "AllowSameUserApproval";

    public const string Verdadero = "true";
    public const string Falso = "false";

    // Valores admitidos por clave, con nombre propio para que los comandos y las pantallas no
    // escriban el texto a mano.
    public static class SemanaLaboralValores
    {
        public const string LunesASabado = "LunesASabado";
        public const string LunesAViernes = "LunesAViernes";
    }

    public static class RetefteTopesAnualesModoValores
    {
        public const string Acumulado = "Acumulado";
        public const string Mensualizado = "Mensualizado";
    }

    public static class P2SecuenciaDepuracionValores
    {
        public const string DepurarLuegoDividir = "DepurarLuegoDividir";
        public const string DividirLuegoDepurar = "DividirLuegoDepurar";
    }

    public static class DianPlazoComputoValores
    {
        public const string Calendario = "Calendario";
        public const string Habiles = "Habiles";
    }

    public static class DeduccionAlRetiroModoValores
    {
        public const string SaldoTotal = "SaldoTotal";
        public const string SoloCuotasCausadas = "SoloCuotasCausadas";
        public const string NoProponer = "NoProponer";
    }

    /// <summary>Formato del valor de <see cref="ArranqueNominaFecha"/>.</summary>
    public const string FormatoFecha = "yyyy-MM-dd";

    /// <summary>
    /// Descripción de cada clave: valores admitidos (vacío = texto libre con el formato que dice
    /// <see cref="Definicion.Formato"/>), defecto, quién la lee. El defecto de
    /// <see cref="ArranqueNominaFecha"/> no es fijo: lo pone el seeder con la fecha del primer
    /// período de la cooperativa, y por eso aquí es nulo. Los dos migrados tampoco traen defecto
    /// propio: primero manda lo copiado de <c>COR_SystemSettings</c>; si nunca existió, <c>false</c>.
    /// </summary>
    public sealed record Definicion(
        string Clave,
        IReadOnlyList<string> Admitidos,
        string? Defecto,
        string Descripcion,
        string? Formato = null);

    private static readonly string[] Booleanos = [Verdadero, Falso];

    public static readonly IReadOnlyList<Definicion> Todas =
    [
        new(Exonerada114_1, Booleanos, Falso,
            "El empleador goza de la exoneración de salud, SENA e ICBF (art. 114-1 E.T.) para empleados bajo el tope legal; la leen los aportes del empleador de la ordinaria y la PILA"),
        new(SemanaLaboral, [SemanaLaboralValores.LunesASabado, SemanaLaboralValores.LunesAViernes], SemanaLaboralValores.LunesASabado,
            "Qué días cuentan como hábiles al contar vacaciones y plazos"),
        new(VacacionesPagoAnticipado, Booleanos, Verdadero,
            "La liquidación de vacaciones paga los días del disfrute y la ordinaria sólo registra la ausencia; en false la ordinaria los paga"),
        new(CotizaArlEnVacaciones, Booleanos, Falso,
            "Si los días de vacaciones cotizan riesgos laborales (PILA y aportes del empleador)"),
        new(RetefteTopesAnualesModo, [RetefteTopesAnualesModoValores.Acumulado, RetefteTopesAnualesModoValores.Mensualizado], RetefteTopesAnualesModoValores.Mensualizado,
            "Cómo se controlan los topes anuales de renta exenta y deducciones al depurar la retención"),
        new(P2SecuenciaDepuracion, [P2SecuenciaDepuracionValores.DepurarLuegoDividir, P2SecuenciaDepuracionValores.DividirLuegoDepurar], P2SecuenciaDepuracionValores.DepurarLuegoDividir,
            "Orden de la depuración en el cálculo del porcentaje fijo del procedimiento 2"),
        new(DianPlazoComputo, [DianPlazoComputoValores.Calendario, DianPlazoComputoValores.Habiles], DianPlazoComputoValores.Calendario,
            "Cómo se cuentan los días del plazo de transmisión a la DIAN para el aviso"),
        new(DianMedioPagoMapa, [], """{ "Transfer": "47", "Check": "20", "Cash": "10" }""",
            "Equivalencia entre la forma de pago de la ficha y el código de medio de pago de la DIAN (tabla 5.3.3.2)",
            Formato: "JSON { \"Transfer\": código, \"Check\": código, \"Cash\": código }"),
        new(DeduccionAlRetiroModo, [DeduccionAlRetiroModoValores.SaldoTotal, DeduccionAlRetiroModoValores.SoloCuotasCausadas, DeduccionAlRetiroModoValores.NoProponer], DeduccionAlRetiroModoValores.SaldoTotal,
            "Qué propone la definitiva como descuento de los préstamos y libranzas del empleado"),
        new(ArranqueNominaFecha, [], null,
            "Fecha desde la que la nómina corre en esta plataforma; quien ingresó antes necesita saldo inicial de prestaciones",
            Formato: FormatoFecha),
        new(AllowSameUserApproval, Booleanos, Falso,
            "Permitir que quien calculó apruebe la misma liquidación, con confirmación expresa"),
    ];

    public static bool Existe(string clave) =>
        Todas.Any(d => string.Equals(d.Clave, clave, StringComparison.Ordinal));

    public static Definicion? Buscar(string clave) =>
        Todas.FirstOrDefault(d => string.Equals(d.Clave, clave, StringComparison.Ordinal));

    /// <summary>
    /// Si el texto es un valor admitido para la clave. Las claves de texto libre (JSON, fecha)
    /// sólo exigen que no venga vacío: el formato lo comprueba el comando con su validador.
    /// </summary>
    public static bool Admite(string clave, string? valor)
    {
        var def = Buscar(clave);
        if (def is null || string.IsNullOrWhiteSpace(valor)) return false;
        return def.Admitidos.Count == 0 || def.Admitidos.Contains(valor, StringComparer.Ordinal);
    }
}
