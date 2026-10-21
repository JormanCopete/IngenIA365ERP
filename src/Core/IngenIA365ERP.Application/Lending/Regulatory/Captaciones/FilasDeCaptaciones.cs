using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Regulatory.Captaciones;

// Feature 014, US17 (T220; contracts/plantillas-y-archivos.md §8; FR-111, FR-112; research §25): los formatos regulatorios de captaciones y aportes —los del
// SICSES (9026 captaciones, 9068 cotitulares, 9013 aportes, 169 saldos diarios, 9067 fondo de liquidez) y los de la exógena de la DIAN (1019, 1020)—, las
// traducciones de los códigos del ERP a los del formato y las filas de cada uno, puras: la proyección lee la base y entrega estos datos; así los archivos
// dorados no necesitan base. Los layouts son dato (Layouts/*.json), con verified = false hasta cotejarlos. (nuevo)

/// <summary>Un formato regulatorio de captaciones: su código (el de la ruta), su clase, su nombre, si es anual y si trae datos del titular. (nuevo)</summary>
public sealed record FormatoDeCaptaciones(string Codigo, RegulatoryFileKind Clase, string Nombre, bool Anual, bool ConDatosDelTitular, string Descripcion)
{
    /// <summary>El último día del período: el del mes en el SICSES, el 31 de diciembre en la exógena.</summary>
    public DateOnly FinDelPeriodo(int anio, int? mes) =>
        Anual ? new DateOnly(anio, 12, 31) : new DateOnly(anio, mes!.Value, 1).AddMonths(1).AddDays(-1);

    /// <summary>El mes con que se guarda la generación: el del período, o 12 en la exógena (es del año).</summary>
    public int MesDelPeriodo(int? mes) => Anual ? 12 : mes!.Value;
}

/// <summary>Los formatos regulatorios de captaciones (api.md §18.2, <c>{formatCode}</c>). (nuevo)</summary>
public static class FormatosDeCaptaciones
{
    public const string Captaciones = "sicses-captaciones";
    public const string Cotitulares = "sicses-cotitulares";
    public const string Aportes = "sicses-aportes";
    public const string SaldosDiarios = "sicses-saldos-diarios";
    public const string FondoDeLiquidez = "sicses-fondo-liquidez";
    public const string Exogena1019 = "exogena-1019";
    public const string Exogena1020 = "exogena-1020";

    public static IReadOnlyList<FormatoDeCaptaciones> Todos { get; } =
    [
        new(Captaciones, RegulatoryFileKind.DepositsSicses, "SICSES · Captaciones (9026)", false, true,
            "Una fila por cuenta de captación viva al cierre del mes: titular, línea, tipo de ahorro, fechas, plazo, tasa, saldos y marcas."),
        new(Cotitulares, RegulatoryFileKind.DepositsSicses, "SICSES · Cotitulares de depósitos (9068)", false, true,
            "Una fila por cotitular vigente al cierre del mes de cada cuenta de captación viva."),
        new(Aportes, RegulatoryFileKind.DepositsSicses, "SICSES · Aportes por asociado (9013)", false, true,
            "Una fila por asociado: saldo de aportes por clase, cuota mensual, revalorización, promedio del ejercicio y último pago."),
        new(SaldosDiarios, RegulatoryFileKind.DepositsSicses, "SICSES · Saldos diarios del ahorro ordinario (169)", false, false,
            "Una fila por día del mes con el saldo de capital del ahorro a la vista al cierre del día."),
        new(FondoDeLiquidez, RegulatoryFileKind.DepositsSicses, "SICSES · Fondo de liquidez (9067)", false, false,
            "Los saldos de depósitos por clase al cierre del mes y las inversiones del fondo de liquidez vigentes ese día."),
        new(Exogena1019, RegulatoryFileKind.Exogenous, "Exógena · Formato 1019 (cuentas de ahorro)", true, true,
            "Por persona y cuenta de ahorro (titular y cotitulares): saldo final, promedio, mediana, máximo y mínimo, y movimientos crédito y débito del año."),
        new(Exogena1020, RegulatoryFileKind.Exogenous, "Exógena · Formato 1020 (CDAT)", true, true,
            "Por persona, CDAT y mes del año: tipo de movimiento, saldos, inversión, intereses causados y pagados y retención."),
    ];

    public static FormatoDeCaptaciones? Buscar(string? codigo) =>
        Todos.FirstOrDefault(f => string.Equals(f.Codigo, codigo?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Las traducciones de los códigos del ERP a los de los formatos de captaciones (todas por cotejar con el instructivo vigente; T235). Tipos de identificación
/// del SICSES: los del instructivo D-GEIN-010 v04 (C, N, P, E, T, R, U, O; en el 9013 la tarjeta de identidad es I). De la DIAN: la tabla de tipos de
/// documento de la exógena (11 registro civil, 12 tarjeta de identidad, 13 cédula, 21 tarjeta de extranjería, 22 cédula de extranjería, 31 NIT,
/// 41 pasaporte, 42 documento extranjero, 47 PEP, 48 PPT, 50 NIT de otro país). (nuevo)
/// </summary>
public static class TraduccionesDeCaptaciones
{
    /// <summary>El tipo de identificación del SICSES (9026, 9068) desde el <c>IdType</c> de la persona.</summary>
    public static string IdentificacionSicses(string? idType) => Normalizado(idType) switch
    {
        "" or "C" or "CC" => "C",
        "N" or "NI" or "NIT" => "N",
        "P" or "PA" or "PP" => "P",
        "E" or "CE" => "E",
        "T" or "TI" => "T",
        "R" or "RC" => "R",
        "U" or "NUIP" => "U",
        _ => "O",
    };

    /// <summary>El tipo de identificación del formato de aportes (9013): como el SICSES, salvo la tarjeta de identidad (I) y sin «otro».</summary>
    public static string IdentificacionAportes(string? idType) => IdentificacionSicses(idType) switch
    {
        "T" => "I",
        var otro => otro,
    };

    /// <summary>El tipo de documento de la DIAN; nulo si no se sabe (el validador lo bloquea como obligatorio vacío).</summary>
    public static int? DocumentoDian(string? idType) => Normalizado(idType) switch
    {
        "" or "C" or "CC" => 13,
        "N" or "NI" or "NIT" => 31,
        "E" or "CE" => 22,
        "P" or "PA" or "PP" => 41,
        "T" or "TI" => 12,
        "R" or "RC" => 11,
        "TE" => 21,
        "DE" or "X" => 42,
        "PEP" => 47,
        "PPT" => 48,
        _ => null,
    };

    /// <summary>El tipo de ahorro del SICSES (1 cuenta de ahorro, 2 CDAT, 3 contractual, 4 permanente; el 5, programado de vivienda, no se distingue).</summary>
    public static int TipoDeAhorro(DepositClass? clase) => clase switch
    {
        DepositClass.TermDeposit => 2,
        DepositClass.Contractual => 3,
        DepositClass.Permanent => 4,
        _ => 1,
    };

    /// <summary>El estado de la cuenta en el 9026: 1 activa, 2 inactiva, 3 vencida, 4 en cancelación.</summary>
    public static int Estado(MemberAccountStatus estado) => estado switch
    {
        MemberAccountStatus.Inactive => 2,
        MemberAccountStatus.Matured => 3,
        MemberAccountStatus.Closing => 4,
        _ => 1,
    };

    /// <summary>El tipo de cuenta del 9026: 1 individual (sin cotitulares), 2 conjunta, 3 alterna (indistinta).</summary>
    public static int TipoDeCuenta(bool conCotitulares, HandlingMode manejo) =>
        !conCotitulares ? 1 : manejo == HandlingMode.Joint ? 2 : 3;

    /// <summary>El tipo de título del 9067: 1 CDT, 3 bonos, 4 cuentas de ahorro, 5 patrimonio autónomo; nulo si la inversión es «otra».</summary>
    public static int? TituloDelFondo(LiquidityHoldingKind clase) => clase switch
    {
        LiquidityHoldingKind.TermInvestment => 1,
        LiquidityHoldingKind.GovernmentBond => 3,
        LiquidityHoldingKind.BankDeposit => 4,
        LiquidityHoldingKind.TrustFund => 5,
        _ => null,
    };

    /// <summary>El tipo de cuenta de la exógena 1019: 2 ahorro, 3 contractual, 4 permanente.</summary>
    public static int TipoDeCuentaExogena(DepositClass? clase) => clase switch
    {
        DepositClass.Contractual => 3,
        DepositClass.Permanent => 4,
        _ => 2,
    };

    /// <summary>El país de la DIAN para Colombia (tabla de países de la exógena).</summary>
    public const string PaisColombia = "169";

    public static string SiNo(bool valor) => valor ? "S" : "N";

    private static string Normalizado(string? idType) => idType?.Trim().ToUpperInvariant() ?? string.Empty;
}

// ------------------------------------------------------------------------------------------------------------------------- entradas --

/// <summary>Una identificación como la lee la proyección: el <c>IdType</c> del maestro y el número.</summary>
public sealed record IdentificacionDePersona(string TipoDeDocumento, string NumeroDeDocumento);

/// <summary>Una cuenta del 9026 tal como la entrega la proyección. (nuevo)</summary>
public sealed record CuentaDelSicses
{
    public required string Numero { get; init; }
    public required IdentificacionDePersona Titular { get; init; }
    public DepositClass? Clase { get; init; }
    public required string Linea { get; init; }
    public required string Oficina { get; init; }
    public MemberAccountStatus Estado { get; init; }
    public DateOnly FechaDeApertura { get; init; }
    public int? PlazoEnDias { get; init; }
    public DateOnly? FechaDeVencimiento { get; init; }
    public int? PeriodicidadDeInteresesEnDias { get; init; }
    public decimal? TasaEfectivaAnual { get; init; }
    public decimal Capital { get; init; }
    public decimal InteresPorPagar { get; init; }
    public decimal? ValorDeLaCuota { get; init; }
    public DateOnly? FechaDelUltimoMovimiento { get; init; }
    public bool ExentaDeGmf { get; init; }
    public bool TieneCotitulares { get; init; }
    public HandlingMode Manejo { get; init; }
    public bool Embargada { get; init; }
}

/// <summary>Un cotitular del 9068: el titular, el cotitular y el depósito. (nuevo)</summary>
public sealed record CotitularDelSicses(IdentificacionDePersona Titular, IdentificacionDePersona Cotitular, DepositClass? Clase, string Numero);

/// <summary>Un asociado del 9013 con sus aportes al corte. (nuevo)</summary>
public sealed record AsociadoDelSicses
{
    public required IdentificacionDePersona Asociado { get; init; }
    public decimal Minimos { get; init; }
    public decimal Ordinarios { get; init; }
    public decimal Extraordinarios { get; init; }
    public decimal Revalorizados { get; init; }
    public decimal CuotaMensual { get; init; }
    public decimal PromedioDelEjercicio { get; init; }
    public DateOnly? UltimaFechaDePago { get; init; }
    public decimal Saldo => Minimos + Ordinarios + Extraordinarios + Revalorizados;
}

/// <summary>El encabezado del 9067: los saldos de depósitos por clase y si el permanente admite retiro parcial. (nuevo)</summary>
public sealed record EncabezadoDelFondoDeLiquidez(decimal ALaVista, decimal ATermino, decimal Contractual, decimal Permanente, bool PermanenteConRetiroParcial)
{
    public decimal SaldoDeDepositos => ALaVista + ATermino + Contractual + Permanente;
}

/// <summary>Una inversión del fondo de liquidez vigente al corte (detalle del 9067). (nuevo)</summary>
public sealed record TituloDelFondoDeLiquidez(string Nit, string Entidad, LiquidityHoldingKind Clase, string Numero, decimal Capital, decimal Intereses,
    int? PlazoEnDias, DateOnly? Vencimiento, bool CapitalizaIntereses);

/// <summary>Los datos de una persona que pide la exógena: identificación, nombres o razón social y ubicación. (nuevo)</summary>
public sealed record PersonaDeLaExogena
{
    public required IdentificacionDePersona Identificacion { get; init; }
    public bool EsJuridica { get; init; }
    public string? PrimerApellido { get; init; }
    public string? SegundoApellido { get; init; }
    public string? PrimerNombre { get; init; }
    public string? OtrosNombres { get; init; }
    public string? RazonSocial { get; init; }
    public string? Direccion { get; init; }

    /// <summary>El código DIVIPOLA del municipio (cinco dígitos: departamento y municipio), si se conoce.</summary>
    public string? Divipola { get; init; }
}

/// <summary>Una fila de la 1019: la persona, la cuenta, su calidad y las cifras de la cuenta en el año. (nuevo)</summary>
public sealed record CuentaDeLaExogena1019
{
    public required PersonaDeLaExogena Persona { get; init; }
    public required string Numero { get; init; }
    public DepositClass? Clase { get; init; }
    public bool EsTitularPrincipal { get; init; }
    public bool ExentaDeGmf { get; init; }
    public required ResumenDeSerie Saldos { get; init; }
    public decimal SaldoFinal { get; init; }
    public required ResumenDeSerie Creditos { get; init; }
    public required ResumenDeSerie Debitos { get; init; }
}

/// <summary>El tipo de movimiento de un CDAT en un mes de la 1020. (nuevo)</summary>
public enum MovimientoDeCdatEnElMes
{
    Emision = 1,
    Renovacion = 2,
    Cancelacion = 3,
    Vigente = 4,
}

/// <summary>Una fila de la 1020: la persona, el CDAT, el mes y lo que le tocó por su proporción. (nuevo)</summary>
public sealed record TituloDeLaExogena1020
{
    public required PersonaDeLaExogena Persona { get; init; }
    public required string Numero { get; init; }
    public int Mes { get; init; }
    public MovimientoDeCdatEnElMes Movimiento { get; init; }
    public decimal SaldoInicial { get; init; }
    public decimal ValorDeLaInversion { get; init; }
    public decimal InteresesCausados { get; init; }
    public decimal InteresesPagados { get; init; }
    public decimal Retencion { get; init; }
    public decimal SaldoFinal { get; init; }
}

// ---------------------------------------------------------------------------------------------------------------------------- filas --

/// <summary>
/// Las filas de cada formato de captaciones con las rutas (<c>path</c>) que usan sus layouts. Puras y deterministas: ordenan por número de cuenta, documento
/// o fecha. La exógena va en pesos enteros (<see cref="EstadisticasDeSaldos.APesos"/>); el SICSES, al centavo. (nuevo)
/// </summary>
public static class FilasDeCaptaciones
{
    public const string Detalle = "Detail";
    public const string Encabezado = "Header";
    public const int DecimalesDeLaTasa = 6;

    public static IReadOnlyList<FilaDeLayout> Captaciones(IEnumerable<CuentaDelSicses> cuentas) =>
        cuentas.OrderBy(c => c.Numero, StringComparer.Ordinal).Select(c => new FilaDeLayout(Detalle, c.Numero, Valores(
            ("Titular.TipoDeIdentificacion", TraduccionesDeCaptaciones.IdentificacionSicses(c.Titular.TipoDeDocumento)),
            ("Titular.NumeroDeIdentificacion", c.Titular.NumeroDeDocumento.Trim()),
            ("Cuenta.CodigoContable", null),
            ("Cuenta.Linea", c.Linea),
            ("Cuenta.TipoDeAhorro", TraduccionesDeCaptaciones.TipoDeAhorro(c.Clase)),
            ("Cuenta.PeriodicidadDeInteresesEnDias", c.PeriodicidadDeInteresesEnDias),
            ("Cuenta.FechaDeApertura", c.FechaDeApertura),
            ("Cuenta.PlazoEnDias", c.PlazoEnDias),
            ("Cuenta.FechaDeVencimiento", c.FechaDeVencimiento),
            ("Cuenta.TasaEfectivaAnual", c.TasaEfectivaAnual is { } t ? decimal.Round(t, DecimalesDeLaTasa, MidpointRounding.AwayFromZero) : null),
            ("Saldos.Capital", c.Capital),
            ("Saldos.InteresPorPagar", decimal.Round(c.InteresPorPagar, 2, MidpointRounding.AwayFromZero)),
            ("Cuenta.ValorDeLaCuota", c.ValorDeLaCuota),
            ("Cuenta.FechaDelUltimoMovimiento", c.FechaDelUltimoMovimiento),
            ("Cuenta.Estado", TraduccionesDeCaptaciones.Estado(c.Estado)),
            ("Cuenta.Numero", c.Numero),
            ("Cuenta.ExentaDeGmf", TraduccionesDeCaptaciones.SiNo(c.ExentaDeGmf)),
            ("Cuenta.TieneCotitulares", TraduccionesDeCaptaciones.SiNo(c.TieneCotitulares)),
            ("Cuenta.TipoDeCuenta", TraduccionesDeCaptaciones.TipoDeCuenta(c.TieneCotitulares, c.Manejo)),
            ("Cuenta.Embargada", TraduccionesDeCaptaciones.SiNo(c.Embargada)),
            ("Cuenta.Oficina", c.Oficina)))).ToList();

    public static IReadOnlyList<FilaDeLayout> Cotitulares(IEnumerable<CotitularDelSicses> cotitulares) =>
        cotitulares.OrderBy(c => c.Numero, StringComparer.Ordinal).ThenBy(c => c.Cotitular.NumeroDeDocumento, StringComparer.Ordinal)
            .Select(c => new FilaDeLayout(Detalle, c.Numero, Valores(
                ("Titular.TipoDeIdentificacion", TraduccionesDeCaptaciones.IdentificacionSicses(c.Titular.TipoDeDocumento)),
                ("Titular.NumeroDeIdentificacion", c.Titular.NumeroDeDocumento.Trim()),
                ("Cotitular.TipoDeIdentificacion", TraduccionesDeCaptaciones.IdentificacionSicses(c.Cotitular.TipoDeDocumento)),
                ("Cotitular.NumeroDeIdentificacion", c.Cotitular.NumeroDeDocumento.Trim()),
                ("Cuenta.TipoDeAhorro", TraduccionesDeCaptaciones.TipoDeAhorro(c.Clase)),
                ("Cuenta.Numero", c.Numero)))).ToList();

    public static IReadOnlyList<FilaDeLayout> Aportes(IEnumerable<AsociadoDelSicses> asociados) =>
        asociados.OrderBy(a => a.Asociado.NumeroDeDocumento, StringComparer.Ordinal).Select(a => new FilaDeLayout(Detalle, a.Asociado.NumeroDeDocumento, Valores(
            ("Asociado.TipoDeIdentificacion", TraduccionesDeCaptaciones.IdentificacionAportes(a.Asociado.TipoDeDocumento)),
            ("Asociado.NumeroDeIdentificacion", a.Asociado.NumeroDeDocumento.Trim()),
            ("Aportes.Saldo", a.Saldo),
            ("Aportes.CuotaMensual", a.CuotaMensual),
            // Los mínimos son aportes ordinarios (Ley 79 de 1988): el formato no tiene renglón propio para ellos (pendiente de validar, T235).
            ("Aportes.Ordinarios", a.Minimos + a.Ordinarios),
            ("Aportes.Extraordinarios", a.Extraordinarios),
            ("Aportes.Revalorizacion", a.Revalorizados),
            ("Aportes.PromedioDelEjercicio", decimal.Round(a.PromedioDelEjercicio, 2, MidpointRounding.AwayFromZero)),
            ("Aportes.UltimaFechaDePago", a.UltimaFechaDePago)))).ToList();

    public static IReadOnlyList<FilaDeLayout> SaldosDiarios(IEnumerable<(DateOnly Dia, decimal Saldo)> serie) =>
        serie.OrderBy(d => d.Dia).Select(d => new FilaDeLayout(Detalle, d.Dia.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            Valores(("Dia.Fecha", d.Dia), ("Dia.Saldo", d.Saldo)))).ToList();

    public static IReadOnlyList<FilaDeLayout> FondoDeLiquidez(EncabezadoDelFondoDeLiquidez encabezado, IEnumerable<TituloDelFondoDeLiquidez> titulos)
    {
        var filas = new List<FilaDeLayout>
        {
            new(Encabezado, null, Valores(
                ("Fondo.SaldoDeDepositos", Centavos(encabezado.SaldoDeDepositos)),
                ("Fondo.DevolucionDelPermanente", encabezado.PermanenteConRetiroParcial ? 2 : 1),
                ("Fondo.ALaVista", Centavos(encabezado.ALaVista)),
                ("Fondo.ATermino", Centavos(encabezado.ATermino)),
                ("Fondo.Contractual", Centavos(encabezado.Contractual)),
                ("Fondo.Permanente", Centavos(encabezado.Permanente)))),
        };
        filas.AddRange(titulos.OrderBy(t => t.Nit, StringComparer.Ordinal).ThenBy(t => t.Numero, StringComparer.Ordinal).Select(t => new FilaDeLayout(Detalle, t.Numero,
            Valores(
                ("Titulo.NitDeLaEntidad", t.Nit),
                ("Titulo.NombreDeLaEntidad", t.Entidad),
                ("Titulo.Tipo", TraduccionesDeCaptaciones.TituloDelFondo(t.Clase)),
                ("Titulo.Numero", t.Numero),
                ("Titulo.ValorCapital", Centavos(t.Capital)),
                ("Titulo.ValorIntereses", Centavos(t.Intereses)),
                ("Titulo.PlazoEnDias", t.PlazoEnDias),
                ("Titulo.FechaDeVencimiento", t.Vencimiento),
                ("Titulo.CapitalizaIntereses", t.CapitalizaIntereses ? "SI" : "NO")))));
        return filas;
    }

    public static IReadOnlyList<FilaDeLayout> Exogena1019(IEnumerable<CuentaDeLaExogena1019> filas) =>
        filas.OrderBy(f => f.Numero, StringComparer.Ordinal).ThenBy(f => f.EsTitularPrincipal ? 0 : 1)
            .ThenBy(f => f.Persona.Identificacion.NumeroDeDocumento, StringComparer.Ordinal)
            .Select(f => new FilaDeLayout(Detalle, f.Numero, Valores(Persona(f.Persona).Concat(
            [
                ("Cuenta.TipoDeCuenta", TraduccionesDeCaptaciones.TipoDeCuentaExogena(f.Clase)),
                ("Cuenta.Numero", f.Numero),
                ("Cuenta.CalidadDelTitular", f.EsTitularPrincipal ? 1 : 2),
                ("Cuenta.ExencionGmf", f.ExentaDeGmf ? 1 : 2),
                ("Saldo.Final", Pesos(f.SaldoFinal)),
                ("Saldo.Promedio", Pesos(f.Saldos.Promedio)),
                ("Saldo.Mediana", Pesos(f.Saldos.Mediana)),
                ("Saldo.Maximo", Pesos(f.Saldos.Maximo)),
                ("Saldo.Minimo", Pesos(f.Saldos.Minimo)),
                ("Creditos.Total", Pesos(f.Creditos.Total)),
                ("Creditos.Numero", f.Creditos.Cantidad),
                ("Creditos.Promedio", Pesos(f.Creditos.Promedio)),
                ("Creditos.Mediana", Pesos(f.Creditos.Mediana)),
                ("Debitos.Total", Pesos(f.Debitos.Total)),
                ("Debitos.Numero", f.Debitos.Cantidad),
                ("Debitos.Promedio", Pesos(f.Debitos.Promedio)),
            ]).ToArray()))).ToList();

    public static IReadOnlyList<FilaDeLayout> Exogena1020(IEnumerable<TituloDeLaExogena1020> filas) =>
        filas.OrderBy(f => f.Numero, StringComparer.Ordinal).ThenBy(f => f.Mes).ThenBy(f => f.Persona.Identificacion.NumeroDeDocumento, StringComparer.Ordinal)
            .Select(f => new FilaDeLayout(Detalle, f.Numero, Valores(Persona(f.Persona).Concat(
            [
                ("Titulo.Numero", f.Numero),
                ("Titulo.Tipo", 2),
                ("Titulo.TipoDeMovimiento", (int)f.Movimiento),
                ("Titulo.Mes", f.Mes),
                ("Titulo.SaldoInicial", Pesos(f.SaldoInicial)),
                ("Titulo.ValorDeLaInversion", Pesos(f.ValorDeLaInversion)),
                ("Titulo.InteresesCausados", Pesos(f.InteresesCausados)),
                ("Titulo.InteresesPagados", Pesos(f.InteresesPagados)),
                ("Titulo.Retencion", Pesos(f.Retencion)),
                ("Titulo.SaldoFinal", Pesos(f.SaldoFinal)),
            ]).ToArray()))).ToList();

    private static IEnumerable<(string, object?)> Persona(PersonaDeLaExogena p)
    {
        var divipola = p.Divipola?.Trim();
        var conCodigo = divipola is { Length: 5 } && divipola.All(char.IsAsciiDigit);
        return
        [
            ("Persona.TipoDeDocumento", TraduccionesDeCaptaciones.DocumentoDian(p.Identificacion.TipoDeDocumento)),
            ("Persona.NumeroDeIdentificacion", p.Identificacion.NumeroDeDocumento.Trim()),
            ("Persona.PrimerApellido", p.EsJuridica ? null : p.PrimerApellido),
            ("Persona.SegundoApellido", p.EsJuridica ? null : p.SegundoApellido),
            ("Persona.PrimerNombre", p.EsJuridica ? null : p.PrimerNombre),
            ("Persona.OtrosNombres", p.EsJuridica ? null : p.OtrosNombres),
            ("Persona.RazonSocial", p.EsJuridica ? p.RazonSocial : null),
            ("Persona.Direccion", p.Direccion),
            ("Persona.CodigoDeDepartamento", conCodigo ? divipola![..2] : null),
            ("Persona.CodigoDeMunicipio", conCodigo ? divipola![2..] : null),
            ("Persona.Pais", TraduccionesDeCaptaciones.PaisColombia),
        ];
    }

    private static decimal Pesos(decimal valor) => EstadisticasDeSaldos.APesos(valor);

    private static decimal Centavos(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static Dictionary<string, object?> Valores(params (string Ruta, object? Valor)[] pares)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (ruta, valor) in pares) d[ruta] = valor;
        return d;
    }
}
