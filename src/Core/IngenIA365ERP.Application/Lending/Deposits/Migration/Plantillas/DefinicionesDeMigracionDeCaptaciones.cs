using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;

/// <summary>
/// Una parte de la carga de migración de aportes o de captaciones (contracts/plantillas-y-archivos.md §1.1 de la 014): su clave en kebab, el orden de
/// carga, las naturalezas que la admiten, la entrega desde la que se importa, si es obligatoria por naturaleza y su definición. <see cref="Definicion"/>
/// es nula en una parte que todavía no se construye (<see cref="LlegaCon"/> dice con qué historia): importarla responde
/// <c>Lending.Migration.PartNotAvailable</c>. (nuevo)
/// </summary>
public sealed record ParteDeMigracionDeCaptaciones(
    string Parte,
    int Orden,
    IReadOnlySet<LineNature> Naturalezas,
    EntregaDeCartera Entrega,
    bool ObligatoriaEnAportes,
    bool ObligatoriaEnCaptaciones,
    DefinicionDePlantilla? Definicion,
    string Nombre,
    string? LlegaCon = null)
{
    /// <summary>La clave de la plantilla: <c>lending.{parte}</c>.</summary>
    public string Clave => DefinicionesDeMigracionDeCaptaciones.PrefijoDeClave + Parte;

    public bool Admite(LineNature naturaleza) => Naturalezas.Contains(naturaleza);

    public bool ObligatoriaEn(LineNature naturaleza) => naturaleza == LineNature.Contribution ? ObligatoriaEnAportes : ObligatoriaEnCaptaciones;

    /// <summary>¿Se puede importar con la entrega <paramref name="desplegada"/>? (construida y con su entrega desplegada).</summary>
    public bool Disponible(EntregaDeCartera desplegada) => Definicion is not null && desplegada >= Entrega;
}

/// <summary>
/// Las plantillas de las partes de la migración de aportes y captaciones (feature 014, US9, T152; contracts/plantillas-y-archivos.md §1.1 y §4; research
/// E115–E118). Una sola hoja <c>Datos</c> cada una (admiten <c>.csv</c>), encabezados en <c>camelCase</c> español que se <b>congelan</b> antes de publicarse;
/// <b>ninguna crea personas</b> (<see cref="TipoDeValor.Persona"/> exige la persona viva en el maestro) ni nombra al sistema anterior: se habla de «código
/// heredado» y «cifras de referencia». Importar, descargar y revisar exigen <c>Lending.Deposits.Migration</c>; con datos, además
/// <c>Lending.Reports.ExportPersonalData</c>. Las reglas de cada columna las aplica <c>ImportDepositMigrationPartCommand</c> y las que cruzan partes
/// <c>ApplyDepositMigrationLoadCommand</c>.
/// <list type="bullet">
/// <item><c>line-equivalences</c> (E115): la hoja <c>Equivalencias</c> de las plantillas de líneas es una parte de la carga —es sólo de la migración y se
/// reemplaza con ella—: el código heredado de cada producto con la línea nueva.</item>
/// <item><c>term-deposits</c> nace con el CDAT (K9, US6; se importa con la entrega K9 desplegada) y <c>holds-and-garnishments</c> llega con los bloqueos
/// y embargos (US11), cuya tabla de destino todavía no existe (E116).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class DefinicionesDeMigracionDeCaptaciones
{
    public const string PrefijoDeClave = "lending.";
    public const string Permiso = "Lending.Deposits.Migration";
    public const string PermisoDeDatosPersonales = "Lending.Reports.ExportPersonalData";

    /// <summary>Las partes en kebab (§1.1).</summary>
    public static class Partes
    {
        public const string LineEquivalences = "line-equivalences";
        public const string MemberAccounts = "member-accounts";
        public const string MemberAccountParties = "member-account-parties";
        public const string TermDeposits = "term-deposits";
        public const string ContributionAccounts = "contribution-accounts";
        public const string AccountInstallments = "account-installments";
        public const string HoldsAndGarnishments = "holds-and-garnishments";
        public const string GmfExemptions = "gmf-exemptions";
        public const string TaxYearToDate = "tax-year-to-date";
    }

    /// <summary>Los encabezados de las partes (§4; los repetidos llevan el mismo nombre).</summary>
    public static class Columnas
    {
        public const string CodigoHeredado = "codigoHeredado";
        public const string DescripcionHeredada = "descripcionHeredada";
        public const string Linea = "linea";
        public const string Clase = "clase";

        public const string Numero = "numero";
        public const string Titular = "titular";
        public const string Oficina = "oficina";
        public const string FechaDeApertura = "fechaDeApertura";
        public const string Estado = "estado";
        public const string FechaUltimoMovimientoDelTitular = "fechaUltimoMovimientoDelTitular";
        public const string Manejo = "manejo";
        public const string SaldoCapital = "saldoCapital";
        public const string InteresCausadoNoLiquidado = "interesCausadoNoLiquidado";
        public const string UltimaLiquidacionHasta = "ultimaLiquidacionHasta";
        public const string CargosPendientes = "cargosPendientes";
        public const string CuotaContractual = "cuotaContractual";
        public const string CuotasPagadas = "cuotasPagadas";
        public const string CuotasAtrasadas = "cuotasAtrasadas";
        public const string FechaDeVencimientoContractual = "fechaDeVencimientoContractual";
        public const string TasaPactadaEA = "tasaPactadaEA";
        public const string TasaBonificadaEA = "tasaBonificadaEA";
        public const string EstadoContractual = "estadoContractual";
        public const string TasaNegociadaMargenEA = "tasaNegociadaMargenEA";
        public const string CanjePendiente = "canjePendiente";
        public const string SoporteDeOrigenDeFondos = "soporteDeOrigenDeFondos";

        public const string Cuenta = "cuenta";
        public const string Persona = "persona";
        public const string Rol = "rol";
        public const string Proporcion = "proporcion";
        public const string LimiteDeFirma = "limiteDeFirma";
        public const string PorcentajeBeneficiario = "porcentajeBeneficiario";
        public const string Fundamento = "fundamento";
        public const string VigenteDesde = "vigenteDesde";
        public const string VigenteHasta = "vigenteHasta";
        public const string Soporte = "soporte";

        public const string Asociado = "asociado";
        public const string FechaDeVinculacion = "fechaDeVinculacion";
        public const string AportesMinimos = "aportesMinimos";
        public const string AportesOrdinarios = "aportesOrdinarios";
        public const string AportesExtraordinarios = "aportesExtraordinarios";
        public const string AportesRevalorizados = "aportesRevalorizados";
        public const string MinimoNoReducible = "minimoNoReducible";
        public const string IngresoRegistrado = "ingresoRegistrado";
        public const string CuotaPactada = "cuotaPactada";

        public const string Periodo = "periodo";
        public const string FechaDeVencimiento = "fechaDeVencimiento";
        public const string Valor = "valor";
        public const string Pagado = "pagado";
        public const string MotivoNoDescontado = "motivoNoDescontado";

        public const string CuentaExenta = "cuentaExenta";
        public const string ExentoUsadoDelMes = "exentoUsadoDelMes";

        public const string Anio = "anio";
        public const string Concepto = "concepto";
        public const string BaseDeRetencion = "baseDeRetencion";

        // US6 (T169; §4.3): el CDAT.
        public const string NumeroDeTitulo = "numeroDeTitulo";
        public const string Capital = "capital";
        public const string FechaDeConstitucion = "fechaDeConstitucion";
        public const string FechaDeInicioVigente = "fechaDeInicioVigente";
        public const string PlazoDias = "plazoDias";
        public const string TasaDeTablaEA = "tasaDeTablaEA";
        public const string MargenNegociadoEA = "margenNegociadoEA";
        public const string PeriodicidadDeIntereses = "periodicidadDeIntereses";
        public const string Modalidad = "modalidad";
        public const string PeriodosPagados = "periodosPagados";
        public const string InteresCausadoNoPagado = "interesCausadoNoPagado";
        public const string DestinoDeIntereses = "destinoDeIntereses";
        public const string CuentaDestino = "cuentaDestino";
        public const string InstruccionAlVencimiento = "instruccionAlVencimiento";
        public const string ProrrogasHechas = "prorrogasHechas";
        public const string PlazoDeEmision = "plazoDeEmision";
        public const string Embargado = "embargado";
        public const string TitularFallecido = "titularFallecido";
    }

    private const string NumeroDeCuenta = "número de cuenta: letras, dígitos y guion (^[A-Za-z0-9-]+$), hasta 20; se guarda en mayúsculas";
    private const string PersonaViva = "documento sin puntos ni dígito de verificación; debe existir y estar viva en Maestros › Personas (esta plantilla no crea personas)";

    private static ColumnaDePlantilla Cuenta(string nombre, bool obligatoria, string reglas) =>
        new(nombre, TipoDeValor.Texto, Obligatoria: obligatoria, Largo: 20, Reglas: $"{reglas}; {NumeroDeCuenta}", Ejemplo: "AHV-000123");

    private static ColumnaDePlantilla Persona(string nombre, bool obligatoria, string reglas = PersonaViva) =>
        new(nombre, TipoDeValor.Persona, Obligatoria: obligatoria, Largo: 20, Reglas: reglas);

    private static ColumnaDePlantilla Oficina() =>
        new(Columnas.Oficina, TipoDeValor.Sucursal, Obligatoria: true, Largo: 20, Reglas: "código o nombre de la sucursal; debe estar vinculada a una oficina en su alcance",
            Ejemplo: "NORTE");

    private static ColumnaDePlantilla LineaDeLaCuenta() =>
        new(Columnas.Linea, TipoDeValor.Codigo, Largo: 10, Reglas: "la línea nueva con versión vigente al corte; va ésta o codigoHeredado", Ejemplo: "AHVISTA");

    private static ColumnaDePlantilla CodigoHeredadoDeLaCuenta() =>
        new(Columnas.CodigoHeredado, TipoDeValor.Texto, Largo: 20,
            Reglas: "el código del producto heredado; resuelve la línea por la parte line-equivalences, que se carga antes", Ejemplo: "");

    private static DefinicionDePlantilla Parte(string parte, string nombre, IReadOnlyList<ColumnaDePlantilla> columnas) =>
        new(PrefijoDeClave + parte, nombre, ModuloDeAuditoria.Lending, [new HojaDePlantilla("Datos", columnas)]);

    // ------------------------------------------------------------------------------------------- line-equivalences (E115) --

    public static DefinicionDePlantilla Equivalencias { get; } = Parte(Partes.LineEquivalences, "Migración de captaciones — equivalencias de productos",
    [
        new(Columnas.CodigoHeredado, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "llave; el código del producto heredado, se guarda en mayúsculas", Ejemplo: "AH01"),
        new(Columnas.DescripcionHeredada, TipoDeValor.Texto, Largo: 120, Reglas: "informativa", Ejemplo: "Ahorro a la vista"),
        new(Columnas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "una línea de la naturaleza de la carga, con versión vigente al corte", Ejemplo: "AHVISTA"),
        new(Columnas.Clase, TipoDeValor.Enumeracion, Largo: 20,
            Reglas: "en captaciones, la clase de la línea: Demand, Contractual, Permanent o TermDeposit (debe coincidir); vacía en aportes", Ejemplo: "Demand"),
    ]);

    // ---------------------------------------------------------------------------------------------- 4.1 member-accounts --

    public static DefinicionDePlantilla CuentasDeCaptacion { get; } = Parte(Partes.MemberAccounts, "Migración de captaciones — cuentas de ahorro",
    [
        Cuenta(Columnas.Numero, true, "llave; único en la carga y frente a las cuentas de la Cartera; es el número que conserva la cuenta"),
        Persona(Columnas.Titular, true, "el titular; si la línea exige asociado, asociado vigente al corte; " + PersonaViva),
        Oficina(),
        LineaDeLaCuenta(),
        CodigoHeredadoDeLaCuenta(),
        new(Columnas.Clase, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 20,
            Reglas: "Demand (a la vista), Permanent o Contractual; debe coincidir con la de la línea. El CDAT va en term-deposits",
            Ejemplo: "Demand"),
        new(Columnas.FechaDeApertura, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2020-05-14"),
        new(Columnas.Estado, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 20, Reglas: "Active o Inactive; Inactive con fechaUltimoMovimientoDelTitular",
            Ejemplo: "Active"),
        new(Columnas.FechaUltimoMovimientoDelTitular, TipoDeValor.Fecha, Reglas: "para medir la inactividad; anterior o igual al corte", Ejemplo: ""),
        new(Columnas.Manejo, TipoDeValor.Enumeracion, Largo: 20, Reglas: "Indistinct o Joint; vacío = Indistinct", Ejemplo: ""),
        new(Columnas.SaldoCapital, TipoDeValor.Monto, Obligatoria: true, Reglas: "saldo de capital al corte, mayor o igual a cero (incluye lo que está en canje)",
            Ejemplo: "1500000,00"),
        new(Columnas.InteresCausadoNoLiquidado, TipoDeValor.Costo,
            Reglas: "causado y no liquidado al corte, hasta 6 decimales; se liquida en la primera liquidación sin volver a causarse; vacío = 0", Ejemplo: ""),
        new(Columnas.UltimaLiquidacionHasta, TipoDeValor.Fecha, Reglas: "obligatoria si hay interés causado; anterior o igual al corte", Ejemplo: "2026-09-30"),
        new(Columnas.CargosPendientes, TipoDeValor.Monto, Reglas: "cargos por cobrar al titular al corte; vacío = 0", Ejemplo: ""),
        new(Columnas.CuotaContractual, TipoDeValor.Monto, Reglas: "obligatoria en el ahorro contractual: la cuota pactada; vacía en las demás clases",
            Ejemplo: ""),
        new(Columnas.CuotasPagadas, TipoDeValor.Entero,
            Reglas: "obligatoria en el ahorro contractual: las cuotas pagadas al corte (cuentan para la bonificación fechadas en su vencimiento del plan)",
            Ejemplo: ""),
        new(Columnas.CuotasAtrasadas, TipoDeValor.Entero,
            Reglas: "ahorro contractual: las vencidas y sin pagar al corte (informativo; las que siguen exigibles van en account-installments)", Ejemplo: ""),
        new(Columnas.FechaDeVencimientoContractual, TipoDeValor.Fecha,
            Reglas: "obligatoria en el ahorro contractual, posterior al corte; el plazo son los meses enteros desde la apertura", Ejemplo: ""),
        new(Columnas.TasaPactadaEA, TipoDeValor.Porcentaje,
            Reglas: "ahorro contractual: la tasa base pactada en puntos EA (vacía = la de la línea al corte); en las demás clases llega con una entrega posterior",
            Ejemplo: ""),
        new(Columnas.TasaBonificadaEA, TipoDeValor.Porcentaje,
            Reglas: "ahorro contractual: la tasa bonificada pactada en puntos EA (vacía = la de la línea)", Ejemplo: ""),
        new(Columnas.EstadoContractual, TipoDeValor.Enumeracion, Largo: 20,
            Reglas: "ahorro contractual: Current (vigente, el defecto) o Breached (ya incumplido: causa a la tasa de castigo desde el corte)", Ejemplo: ""),
        new(Columnas.TasaNegociadaMargenEA, TipoDeValor.Porcentaje, Reglas: "tasa negociada: llega con una entrega posterior", Ejemplo: ""),
        new(Columnas.CanjePendiente, TipoDeValor.Monto, Reglas: "de saldoCapital, lo que sigue en canje al corte; se libera con los días hábiles del cheque; vacío = 0",
            Ejemplo: ""),
        new(Columnas.SoporteDeOrigenDeFondos, TipoDeValor.Texto, Largo: 200, Reglas: "referencia", Ejemplo: ""),
    ]);

    // ----------------------------------------------------------------------------------------- 4.2 member-account-parties --

    public static DefinicionDePlantilla PartesDeCuenta { get; } = Parte(Partes.MemberAccountParties, "Migración de captaciones — cotitulares, firmantes, representantes y beneficiarios",
    [
        Cuenta(Columnas.Cuenta, true, "llave con la persona y el rol; una cuenta de member-accounts o de contribution-accounts de la carga"),
        Persona(Columnas.Persona, true),
        new(Columnas.Rol, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 30,
            Reglas: "llave; CoHolder (cotitular), Signatory (firmante), LegalRepresentative (representante legal) o Beneficiary; el titular va en la cuenta. En aportes sólo Beneficiary",
            Ejemplo: "Beneficiary"),
        new(Columnas.Proporcion, TipoDeValor.Porcentaje, Reglas: "con CoHolder, en puntos; la del titular es el resto; sin proporción, partes iguales", Ejemplo: ""),
        new(Columnas.LimiteDeFirma, TipoDeValor.Monto, Reglas: "con Signatory", Ejemplo: ""),
        new(Columnas.PorcentajeBeneficiario, TipoDeValor.Porcentaje, Reglas: "con Beneficiary, en puntos; por cuenta suman 100", Ejemplo: "50"),
        new(Columnas.Fundamento, TipoDeValor.Enumeracion, Largo: 40,
            Reglas: "con LegalRepresentative: Parent (padres), Guardian (tutor), CourtOrder (decisión judicial) o LegalEntityRepresentative", Ejemplo: ""),
        new(Columnas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2020-05-14"),
        new(Columnas.VigenteHasta, TipoDeValor.Fecha, Reglas: "vacía = vigente; posterior al corte", Ejemplo: ""),
        new(Columnas.Soporte, TipoDeValor.Texto, Largo: 200,
            Reglas: "con LegalRepresentative: registro civil o decisión judicial; el adjunto se sube después y la cuenta queda con la actualización de datos pendiente",
            Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------------ 4.3 term-deposits --

    /// <summary>
    /// US6 (pendiente de la fase 10; §4.3): los CDAT vigentes al corte con su versión de condiciones vigente (tras sus prórrogas). Al activar nacen con el plan
    /// que regenera <c>PlanDeCdat</c> desde su inicio vigente; el interés causado se compara con el del plan (<c>Lending.TermDeposit.ScheduleMismatch</c>).
    /// </summary>
    public static DefinicionDePlantilla CdatVigentes { get; } = Parte(Partes.TermDeposits, "Migración de captaciones — CDAT",
    [
        Cuenta(Columnas.Numero, true, "llave; único en la carga y frente a las cuentas de la Cartera; es el número que conserva el CDAT"),
        Persona(Columnas.Titular, true, "el titular; si la línea exige asociado, asociado vigente al corte; " + PersonaViva),
        Oficina(),
        LineaDeLaCuenta(),
        CodigoHeredadoDeLaCuenta(),
        new(Columnas.NumeroDeTitulo, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "el título vigente; las prórrogas siguientes llevan sufijo de versión",
            Ejemplo: "CDT-000123"),
        new(Columnas.Capital, TipoDeValor.Monto, Obligatoria: true, Reglas: "el capital de la versión vigente, mayor que cero", Ejemplo: "10000000,00"),
        new(Columnas.FechaDeConstitucion, TipoDeValor.Fecha, Obligatoria: true, Reglas: "la del título original; anterior o igual al corte", Ejemplo: "2025-07-15"),
        new(Columnas.FechaDeInicioVigente, TipoDeValor.Fecha, Obligatoria: true,
            Reglas: "el inicio de la versión vigente (tras las prórrogas); entre la constitución y el corte", Ejemplo: "2026-07-15"),
        new(Columnas.PlazoDias, TipoDeValor.Entero, Obligatoria: true, Reglas: "el plazo pactado en días (aviso si sale del rango de la línea: lo pactado no se reescribe)",
            Ejemplo: "180"),
        new(Columnas.FechaDeVencimiento, TipoDeValor.Fecha, Obligatoria: true,
            Reglas: "posterior al corte; inicio + plazo o el hábil siguiente si cae en no hábil", Ejemplo: "2027-01-11"),
        new(Columnas.TasaPactadaEA, TipoDeValor.Porcentaje, Obligatoria: true, Reglas: "la tasa efectiva anual sellada, en puntos; no se revisa contra la tabla", Ejemplo: "10"),
        new(Columnas.TasaDeTablaEA, TipoDeValor.Porcentaje, Reglas: "si se negoció: la de la tabla; tabla + margen = pactada", Ejemplo: ""),
        new(Columnas.MargenNegociadoEA, TipoDeValor.Porcentaje, Reglas: "si se negoció: el margen sobre la tabla", Ejemplo: ""),
        new(Columnas.PeriodicidadDeIntereses, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 20,
            Reglas: "Monthly, Bimonthly, Quarterly, SemiAnnual, Annual o SinglePayment (al vencimiento)", Ejemplo: "Monthly"),
        new(Columnas.Modalidad, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 20, Reglas: "Arrears (vencido) o Advance (anticipado)", Ejemplo: "Arrears"),
        new(Columnas.PeriodosPagados, TipoDeValor.Entero, Obligatoria: true, Reglas: "los períodos del plan vigente ya pagados al corte", Ejemplo: "3"),
        new(Columnas.InteresCausadoNoPagado, TipoDeValor.Costo, Obligatoria: true,
            Reglas: "lo causado y no pagado (anticipado: lo causado del período en curso) al corte, hasta 6 decimales; se compara con el plan (tolerancia de un peso)",
            Ejemplo: "26475,80"),
        new(Columnas.DestinoDeIntereses, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 30,
            Reglas: "Account (abono en una cuenta a la vista del titular) o DisbursementOrder (giro a la cuenta bancaria registrada del titular)", Ejemplo: "Account"),
        Cuenta(Columnas.CuentaDestino, false, "con Account: una cuenta a la vista del titular, de la carga o viva en la Cartera"),
        new(Columnas.InstruccionAlVencimiento, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 30,
            Reglas: "Rollover (prorrogar), RenewWithNewTerms (renovar con condiciones) o CancelAndPay (cancelar y pagar)", Ejemplo: "Rollover"),
        new(Columnas.ProrrogasHechas, TipoDeValor.Entero, Reglas: "las prórrogas ya hechas; vacío = 0", Ejemplo: ""),
        new(Columnas.PlazoDeEmision, TipoDeValor.Enumeracion, Largo: 30,
            Reglas: "UnderSixMonths, SixToTwelve, TwelveToEighteen o EighteenOrMore; se calcula del plazo y, si se informa distinto, es error", Ejemplo: ""),
        new(Columnas.Embargado, TipoDeValor.SiNo, Reglas: "informativo hasta la entrega de bloqueos y embargos", Ejemplo: ""),
        new(Columnas.TitularFallecido, TipoDeValor.SiNo, Reglas: "se cruza con el maestro de personas (aviso)", Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------- 4.4 contribution-accounts --

    public static DefinicionDePlantilla CuentasDeAportes { get; } = Parte(Partes.ContributionAccounts, "Migración de aportes — cuentas de aportes",
    [
        Persona(Columnas.Asociado, true, "llave; asociado vigente al corte; una sola cuenta de aportes por asociado; " + PersonaViva),
        Cuenta(Columnas.Numero, false, "si falta, se numera al activar con el consecutivo de la línea"),
        Oficina(),
        LineaDeLaCuenta(),
        CodigoHeredadoDeLaCuenta(),
        new(Columnas.FechaDeVinculacion, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte; la antigüedad", Ejemplo: "2015-03-01"),
        new(Columnas.AportesMinimos, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor o igual a cero", Ejemplo: "0,00"),
        new(Columnas.AportesOrdinarios, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor o igual a cero", Ejemplo: "2500000,00"),
        new(Columnas.AportesExtraordinarios, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.AportesRevalorizados, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.MinimoNoReducible, TipoDeValor.Monto, Reglas: "si el estatuto fija uno individual; vacío = el de la línea", Ejemplo: ""),
        new(Columnas.IngresoRegistrado, TipoDeValor.Monto, Reglas: "con cuota por porcentaje del ingreso: el ingreso registrado del asociado (aviso si no coincide)", Ejemplo: ""),
        new(Columnas.CuotaPactada, TipoDeValor.Monto, Reglas: "si difiere de la cuota de la línea, aviso", Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------ 4.5 account-installments --

    public static DefinicionDePlantilla CuotasAtrasadas { get; } = Parte(Partes.AccountInstallments, "Migración de captaciones — cuotas atrasadas",
    [
        Cuenta(Columnas.Cuenta, true, "llave con el período; una cuenta de aportes o de ahorro permanente de la carga"),
        new(Columnas.Periodo, TipoDeValor.Texto, Obligatoria: true, Largo: 10, Reglas: "llave; AAAA-MM o AAAA-MM-n (n = subperíodo: quincena, decena o semana, 1 a 5)",
            Ejemplo: "2026-09"),
        new(Columnas.FechaDeVencimiento, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2026-09-30"),
        new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "el valor de la cuota, mayor que cero", Ejemplo: "50000,00"),
        new(Columnas.Pagado, TipoDeValor.Monto, Reglas: "lo pagado de la cuota (menor que el valor); vacío = 0", Ejemplo: ""),
        new(Columnas.MotivoNoDescontado, TipoDeValor.Enumeracion, Largo: 30,
            Reglas: "si Nómina o la pagaduría no la descontó: DeductionCap, InsufficientNet, Termination, Leave, Disability, DeductionPriority u Other", Ejemplo: ""),
    ]);

    // -------------------------------------------------------------------------------------------- 4.7 gmf-exemptions --

    public static DefinicionDePlantilla ExencionesDeGmf { get; } = Parte(Partes.GmfExemptions, "Migración de captaciones — cuentas exentas del GMF",
    [
        Persona(Columnas.Persona, true, "llave; el titular de la cuenta exenta; " + PersonaViva),
        Cuenta(Columnas.CuentaExenta, true, "una cuenta de ahorro de member-accounts de la carga, de la que la persona es titular"),
        new(Columnas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2024-01-15"),
        new(Columnas.ExentoUsadoDelMes, TipoDeValor.Monto, Obligatoria: true, Reglas: "lo exento usado en el mes del corte, mayor o igual a cero", Ejemplo: "350000,00"),
    ]);

    // ------------------------------------------------------------------------------------------- 4.8 tax-year-to-date --

    public static DefinicionDePlantilla AcumuladoTributario { get; } = Parte(Partes.TaxYearToDate, "Migración de captaciones — acumulado tributario del año",
    [
        Persona(Columnas.Titular, true, "llave con la cuenta y el concepto; el titular o cotitular; " + PersonaViva),
        Cuenta(Columnas.Cuenta, true, "llave; una cuenta de ahorro de member-accounts de la carga"),
        new(Columnas.Anio, TipoDeValor.Entero, Obligatoria: true, Reglas: "el año del corte (cuatro dígitos)", Ejemplo: ""),
        new(Columnas.Concepto, TipoDeValor.Enumeracion, Obligatoria: true, Largo: 30,
            Reglas: "llave; Interest (rendimientos), Withholding (retención), Gmf o InflationaryComponent (componente inflacionario)", Ejemplo: "Interest"),
        new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "del año, anterior al corte; mayor o igual a cero", Ejemplo: "125000,00"),
        new(Columnas.BaseDeRetencion, TipoDeValor.Monto, Reglas: "con Withholding: la base sobre la que se retuvo", Ejemplo: ""),
    ]);

    private static readonly IReadOnlySet<LineNature> Captaciones = new HashSet<LineNature> { LineNature.Deposit };
    private static readonly IReadOnlySet<LineNature> Aportes = new HashSet<LineNature> { LineNature.Contribution };
    private static readonly IReadOnlySet<LineNature> Ambas = new HashSet<LineNature> { LineNature.Deposit, LineNature.Contribution };

    /// <summary>Las partes en el orden de carga (§1.1, con las equivalencias primero, E115).</summary>
    public static IReadOnlyList<ParteDeMigracionDeCaptaciones> Todas => _todas.Value;

    private static readonly Lazy<IReadOnlyList<ParteDeMigracionDeCaptaciones>> _todas = new(() =>
    [
        new(Partes.LineEquivalences, 0, Ambas, EntregaDeCartera.K7, false, false, Equivalencias, "Equivalencias de productos"),
        new(Partes.MemberAccounts, 5, Captaciones, EntregaDeCartera.K8, false, true, CuentasDeCaptacion, "Cuentas de ahorro"),
        new(Partes.MemberAccountParties, 6, Ambas, EntregaDeCartera.K7, false, false, PartesDeCuenta, "Cotitulares, firmantes, representantes y beneficiarios"),
        new(Partes.TermDeposits, 7, Captaciones, EntregaDeCartera.K9, false, false, CdatVigentes, "CDAT"),
        new(Partes.ContributionAccounts, 8, Aportes, EntregaDeCartera.K7, true, false, CuentasDeAportes, "Cuentas de aportes"),
        new(Partes.AccountInstallments, 9, Ambas, EntregaDeCartera.K7, false, false, CuotasAtrasadas, "Cuotas atrasadas"),
        new(Partes.HoldsAndGarnishments, 10, Ambas, EntregaDeCartera.K8, false, false, null, "Bloqueos y embargos", LlegaCon: "los bloqueos y embargos (K8)"),
        new(Partes.GmfExemptions, 11, Captaciones, EntregaDeCartera.K8, false, false, ExencionesDeGmf, "Cuentas exentas del GMF"),
        new(Partes.TaxYearToDate, 12, Captaciones, EntregaDeCartera.K8, false, false, AcumuladoTributario, "Acumulado tributario del año"),
    ]);

    /// <summary>La parte por su clave en kebab (<c>member-accounts</c>) o completa (<c>lending.member-accounts</c>), sin distinguir mayúsculas; nula si no existe.</summary>
    public static ParteDeMigracionDeCaptaciones? Buscar(string? parte)
    {
        if (string.IsNullOrWhiteSpace(parte)) return null;
        var clave = parte.Trim();
        if (clave.StartsWith(PrefijoDeClave, StringComparison.OrdinalIgnoreCase)) clave = clave[PrefijoDeClave.Length..];
        return Todas.FirstOrDefault(p => string.Equals(p.Parte, clave, StringComparison.OrdinalIgnoreCase));
    }
}
