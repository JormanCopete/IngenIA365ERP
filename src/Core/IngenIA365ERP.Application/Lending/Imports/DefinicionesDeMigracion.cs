using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>Una parte de la carga de migración (plantillas.md §5.2): su clave en kebab, si es obligatoria y su definición. (nuevo)</summary>
public sealed record ParteDeMigracion(string Parte, int Orden, bool Obligatoria, DefinicionDePlantilla Definicion)
{
    public string Clave => Definicion.Clave;
}

/// <summary>
/// Las 17 plantillas de las partes de la migración (<c>lending.migration.{parte}</c>) y la de cobros provisionales
/// (<c>lending.provisional-collections</c>) (feature 013, US8, T0737, T82; plantillas.md §5.2, §5.3, §6, §0.3). Una sola hoja <c>Datos</c> cada una
/// (admiten <c>.csv</c>), encabezados en <c>camelCase</c> español que se <b>congelan</b> antes de publicarse; ninguna crea personas
/// (<see cref="TipoDeValor.Persona"/> exige la persona viva en el maestro). Importar una parte exige <c>Lending.Migration.Load</c>; revisar y
/// activar con cobros provisionales, <c>Lending.Activation.Execute</c>. Las reglas de cada columna las aplica
/// <c>ImportMigrationPartCommand</c> (y <c>ReviewProvisionalCollectionsCommand</c>, la de cobros). (nuevo)
/// </summary>
public static class DefinicionesDeMigracion
{
    public const string PrefijoDeClave = "lending.migration.";
    public const string CobrosProvisionalesClave = "lending.provisional-collections";
    public const string PermisoDeImportacion = "Lending.Migration.Load";
    public const string PermisoDeCobrosProvisionales = "Lending.Activation.Execute";

    /// <summary>Las partes en kebab (plantillas.md §5.2).</summary>
    public static class Partes
    {
        public const string Loans = "loans";
        public const string Installments = "installments";
        public const string Balances = "balances";
        public const string RiskData = "risk-data";
        public const string Restructurings = "restructurings";
        public const string CoDebtors = "co-debtors";
        public const string Collaterals = "collaterals";
        public const string ChargedOff = "charged-off";
        public const string Sales = "sales";
        public const string CreditLimits = "credit-limits";
        public const string Blocks = "blocks";
        public const string Authorizations = "authorizations";
        public const string PriorNotices = "prior-notices";
        public const string BureauReports = "bureau-reports";
        public const string Claims = "claims";
        public const string RelatedParties = "related-parties";
        public const string ArrearsHistory = "arrears-history";
    }

    /// <summary>Los encabezados de las 17 partes y de los cobros provisionales (los repetidos llevan el mismo nombre).</summary>
    public static class Columnas
    {
        public const string Numero = "numero";
        public const string NumeroDeCredito = "numeroDeCredito";
        public const string Deudor = "deudor";
        public const string Oficina = "oficina";
        public const string Linea = "linea";
        public const string Asesor = "asesor";
        public const string Factura = "factura";
        public const string FechaDeDesembolso = "fechaDeDesembolso";
        public const string MontoDesembolsado = "montoDesembolsado";
        public const string TipoDeTasa = "tipoDeTasa";
        public const string TasaCorrienteEA = "tasaCorrienteEA";
        public const string Indice = "indice";
        public const string ClaveDelIndice = "claveDelIndice";
        public const string Margen = "margen";
        public const string TasaPiso = "tasaPiso";
        public const string TasaTecho = "tasaTecho";
        public const string MesesDeReajuste = "mesesDeReajuste";
        public const string TipoDeTasaDeMora = "tipoDeTasaDeMora";
        public const string TasaDeMoraEA = "tasaDeMoraEA";
        public const string FactorDeMora = "factorDeMora";
        public const string ConvencionDeDias = "convencionDeDias";
        public const string Equivalencia = "equivalencia";
        public const string RedondeoUnidad = "redondeoUnidad";
        public const string RedondeoModo = "redondeoModo";
        public const string RedondeoMomento = "redondeoMomento";
        public const string SistemaDeAmortizacion = "sistemaDeAmortizacion";
        public const string Prelacion = "prelacion";
        public const string Periodicidad = "periodicidad";
        public const string Plazo = "plazo";
        public const string PreferenciaDeAbono = "preferenciaDeAbono";
        public const string FormaDePago = "formaDePago";
        public const string Pagaduria = "pagaduria";
        public const string IdEmpleadoEnPagaduria = "idEmpleadoEnPagaduria";
        public const string FechaDeLibranza = "fechaDeLibranza";
        public const string EtapaDeCobranza = "etapaDeCobranza";
        public const string EnJuridico = "enJuridico";
        public const string Abogado = "abogado";
        public const string Notas = "notas";

        public const string Cuota = "cuota";
        public const string FechaDeVencimiento = "fechaDeVencimiento";
        public const string Capital = "capital";
        public const string InteresCorriente = "interesCorriente";
        public const string InteresDeMora = "interesDeMora";
        public const string Seguro = "seguro";
        public const string Otros = "otros";
        public const string InteresProyectado = "interesProyectado";

        public const string Concepto = "concepto";
        public const string Valor = "valor";

        public const string DiasDeMora = "diasDeMora";
        public const string Calificacion = "calificacion";
        public const string Subcategoria = "subcategoria";
        public const string Causa = "causa";
        public const string DeterioroCapital = "deterioroCapital";
        public const string DeterioroIntereses = "deterioroIntereses";
        public const string DeterioroOtros = "deterioroOtros";
        public const string DeterioroGeneral = "deterioroGeneral";
        public const string FechaDeIngresoAE = "fechaDeIngresoAE";
        public const string EnSuspenso = "enSuspenso";

        public const string Clase = "clase";
        public const string VecesReestructurado = "vecesReestructurado";
        public const string FechaUltima = "fechaUltima";
        public const string CalificacionAlReestructurar = "calificacionAlReestructurar";
        public const string CuotasConsecutivasPagadas = "cuotasConsecutivasPagadas";
        public const string CreditoAnterior = "creditoAnterior";

        public const string Codeudor = "codeudor";
        public const string Rol = "rol";
        public const string Desde = "desde";

        public const string Tipo = "tipo";
        public const string Referencia = "referencia";
        public const string FechaDeConstitucion = "fechaDeConstitucion";
        public const string VigenteHasta = "vigenteHasta";
        public const string Estado = "estado";
        public const string Descripcion = "descripcion";
        public const string ValorAvaluo = "valorAvaluo";
        public const string FechaAvaluo = "fechaAvaluo";
        public const string Poliza = "poliza";
        public const string PolizaVence = "polizaVence";
        public const string Aseguradora = "aseguradora";
        public const string ClasePagare = "clasePagare";
        public const string CanalDeCustodia = "canalDeCustodia";
        public const string IdExternoPagare = "idExternoPagare";
        public const string FondoDeGarantias = "fondoDeGarantias";
        public const string Cobertura = "cobertura";
        public const string Comision = "comision";

        public const string FechaDeCastigo = "fechaDeCastigo";
        public const string CapitalCastigado = "capitalCastigado";
        public const string InteresesCastigados = "interesesCastigados";
        public const string OtrosCastigados = "otrosCastigados";
        public const string SoporteDelCastigo = "soporteDelCastigo";

        public const string FechaDeVenta = "fechaDeVenta";
        public const string ValorOriginal = "valorOriginal";
        public const string CobrosExternos = "cobrosExternos";
        public const string NotasYAjustes = "notasYAjustes";
        public const string SaldoAlCorte = "saldoAlCorte";
        public const string Resultado = "resultado";
        public const string ReferenciaDeLosCobros = "referenciaDeLosCobros";

        public const string Persona = "persona";
        public const string VigenteDesde = "vigenteDesde";
        public const string Motivo = "motivo";

        public const string Hasta = "hasta";
        public const string Soporte = "soporte";
        public const string EstadoDeLaReclamacion = "estadoDeLaReclamacion";
        public const string EstadoDeLaInsolvencia = "estadoDeLaInsolvencia";

        public const string Finalidad = "finalidad";
        public const string Canal = "canal";
        public const string Fecha = "fecha";
        public const string VersionDelTexto = "versionDelTexto";
        public const string Evidencia = "evidencia";
        public const string CanalDeObtencion = "canalDeObtencion";

        public const string Episodio = "episodio";
        public const string ComunicacionesExigidas = "comunicacionesExigidas";
        public const string FechaDeEnvio1 = "fechaDeEnvio1";
        public const string Canal1 = "canal1";
        public const string Referencia1 = "referencia1";
        public const string FechaDeEnvio2 = "fechaDeEnvio2";
        public const string Canal2 = "canal2";
        public const string Referencia2 = "referencia2";

        public const string Central = "central";
        public const string UltimoPeriodoReportado = "ultimoPeriodoReportado";
        public const string NovedadReportada = "novedadReportada";
        public const string DatoNegativo = "datoNegativo";
        public const string ConAvisoPrevio = "conAvisoPrevio";

        public const string FechaDeRadicacion = "fechaDeRadicacion";
        public const string Prorrogado = "prorrogado";

        public const string Mes = "mes";
        public const string MoraMaximaDias = "moraMaximaDias";
        public const string MoraAlCierreDias = "moraAlCierreDias";

        public const string FechaValor = "fechaValor";
        public const string ReferenciaContable = "referenciaContable";
        public const string Observaciones = "observaciones";
    }

    private const string Numero20 = "número de crédito: letras, dígitos y guion (^[A-Za-z0-9-]+$), hasta 20; se guarda en mayúsculas";
    private const string PersonaViva = "documento sin puntos ni dígito de verificación; debe existir y estar viva en Maestros › Personas (esta plantilla no crea personas)";
    private const string Mes = "mes AAAA-MM";

    private static ColumnaDePlantilla Credito(string nombre, string reglas, bool obligatoria = true) =>
        new(nombre, TipoDeValor.Texto, Obligatoria: obligatoria, Largo: 20, Reglas: $"{reglas}; {Numero20}", Ejemplo: "CR-000123");

    private static ColumnaDePlantilla Persona(string nombre, bool obligatoria, string reglas = PersonaViva) =>
        new(nombre, TipoDeValor.Persona, Obligatoria: obligatoria, Largo: 20, Reglas: reglas);

    private static DefinicionDePlantilla Parte(string parte, string nombre, IReadOnlyList<ColumnaDePlantilla> columnas) =>
        new(PrefijoDeClave + parte, nombre, ModuloDeAuditoria.Lending, [new HojaDePlantilla("Datos", columnas)]);

    // ------------------------------------------------------------------------------------------------------- 5.3.1 loans --

    public static DefinicionDePlantilla Creditos { get; } = Parte(Partes.Loans, "Migración — créditos",
    [
        Credito(Columnas.Numero, "llave; único en la carga (también frente a la cartera castigada) y frente a los créditos de Cartera; es el número que conserva el crédito"),
        Persona(Columnas.Deudor, obligatoria: true),
        new(Columnas.Oficina, TipoDeValor.Sucursal, Obligatoria: true, Largo: 20, Reglas: "código o nombre de la sucursal; debe estar vinculada a una oficina", Ejemplo: "NORTE"),
        new(Columnas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "con versión vigente al corte; la modalidad vivienda llega con una entrega posterior", Ejemplo: "LIBRE"),
        Persona(Columnas.Asesor, obligatoria: false, "para la colocación por asesor; " + PersonaViva),
        new(Columnas.Factura, TipoDeValor.Texto, Largo: 40, Reglas: "si nació de una venta de Comercial: la factura tal como la muestra la venta; debe estar en la parte sales como Migrated", Ejemplo: ""),
        new(Columnas.FechaDeDesembolso, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2025-03-15"),
        new(Columnas.MontoDesembolsado, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero", Ejemplo: "5000000,00"),
        new(Columnas.TipoDeTasa, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Fixed (fija) o Variable", Ejemplo: "Fixed"),
        new(Columnas.TasaCorrienteEA, TipoDeValor.Porcentaje, Reglas: "con Fixed: la pactada, efectiva anual en puntos; sobre la usura del corte es aviso, no error", Ejemplo: "22,5"),
        new(Columnas.Indice, TipoDeValor.Codigo, Largo: 20, Reglas: "con Variable", Ejemplo: ""),
        new(Columnas.ClaveDelIndice, TipoDeValor.Texto, Largo: 20, Reglas: "con Variable", Ejemplo: ""),
        new(Columnas.Margen, TipoDeValor.Porcentaje, Reglas: "con Variable; en puntos", Ejemplo: ""),
        new(Columnas.TasaPiso, TipoDeValor.Porcentaje, Ejemplo: ""),
        new(Columnas.TasaTecho, TipoDeValor.Porcentaje, Ejemplo: ""),
        new(Columnas.MesesDeReajuste, TipoDeValor.Entero, Reglas: "con Variable: 1, 2, 3, 6 o 12", Ejemplo: ""),
        new(Columnas.TipoDeTasaDeMora, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Fixed (fija) o RelativeToCurrent (relativa a la corriente)", Ejemplo: "RelativeToCurrent"),
        new(Columnas.TasaDeMoraEA, TipoDeValor.Porcentaje, Reglas: "con Fixed", Ejemplo: ""),
        new(Columnas.FactorDeMora, TipoDeValor.Costo, Reglas: "con RelativeToCurrent: múltiplo de la corriente", Ejemplo: "1,5"),
        new(Columnas.ConvencionDeDias, TipoDeValor.Enumeracion, Reglas: "Thirty360, Actual365, Actual360; vacío = la de la versión de la línea", Ejemplo: ""),
        new(Columnas.Equivalencia, TipoDeValor.Enumeracion, Reglas: "Compound o Linear; vacío = la de la versión de la línea", Ejemplo: ""),
        new(Columnas.RedondeoUnidad, TipoDeValor.Monto, Reglas: "vacío = la de la versión de la línea", Ejemplo: ""),
        new(Columnas.RedondeoModo, TipoDeValor.Enumeracion, Reglas: "AwayFromZero, ToEven, Up, Down; vacío = el de la versión de la línea", Ejemplo: ""),
        new(Columnas.RedondeoMomento, TipoDeValor.Enumeracion, Reglas: "PerConcept, PerInstallment, PerTotal; vacío = el de la versión de la línea", Ejemplo: ""),
        new(Columnas.SistemaDeAmortizacion, TipoDeValor.Enumeracion, Reglas: "LevelPayment, ConstantPrincipal, Bullet; vacío = el de la versión de la línea", Ejemplo: ""),
        new(Columnas.Prelacion, TipoDeValor.Enumeracion, Reglas: "OldestInstallmentFirst o ConceptFirst; vacío = la de la versión de la línea", Ejemplo: ""),
        new(Columnas.Periodicidad, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "SinglePayment, Weekly, TenDay, Biweekly, Monthly, Bimonthly, Quarterly, SemiAnnual, Annual", Ejemplo: "Monthly"),
        new(Columnas.Plazo, TipoDeValor.Entero, Obligatoria: true, Reglas: "cuotas totales pactadas", Ejemplo: "36"),
        new(Columnas.PreferenciaDeAbono, TipoDeValor.Enumeracion, Reglas: "ReduceTerm 1 o ReduceInstallment 2; vacío: un abono extraordinario sin elección queda como saldo a favor", Ejemplo: ""),
        new(Columnas.FormaDePago, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Cashier 1 (caja), OwnPayroll 2 (nómina propia), PaymasterDeduction 3 (libranza), DirectDebit 4 (débito automático)", Ejemplo: "Cashier"),
        Persona(Columnas.Pagaduria, obligatoria: false, "con PaymasterDeduction: la persona jurídica pagadora; " + PersonaViva),
        new(Columnas.IdEmpleadoEnPagaduria, TipoDeValor.Texto, Largo: 30, Reglas: "con PaymasterDeduction", Ejemplo: ""),
        new(Columnas.FechaDeLibranza, TipoDeValor.Fecha, Reglas: "con PaymasterDeduction: para la prelación", Ejemplo: ""),
        new(Columnas.EtapaDeCobranza, TipoDeValor.Texto, Largo: 30, Reglas: "se conserva para la cobranza de una entrega posterior", Ejemplo: ""),
        new(Columnas.EnJuridico, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
        Persona(Columnas.Abogado, obligatoria: false, "con cobro jurídico; " + PersonaViva),
        new(Columnas.Notas, TipoDeValor.Texto, Largo: 400, Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------------ 5.3.2 installments --

    public static DefinicionDePlantilla Cuotas { get; } = Parte(Partes.Installments, "Migración — cuotas pendientes",
    [
        Credito(Columnas.NumeroDeCredito, "llave con la cuota; un crédito de la parte loans"),
        new(Columnas.Cuota, TipoDeValor.Entero, Obligatoria: true, Reglas: "llave con el crédito; desde 1", Ejemplo: "13"),
        new(Columnas.FechaDeVencimiento, TipoDeValor.Fecha, Obligatoria: true, Reglas: "creciente con la cuota", Ejemplo: "2026-12-15"),
        new(Columnas.Capital, TipoDeValor.Monto, Obligatoria: true, Reglas: "capital pendiente al corte; el plan pendiente suma el saldo de capital", Ejemplo: "138888,89"),
        new(Columnas.InteresCorriente, TipoDeValor.Monto, Reglas: "causado y no pagado al corte, incluido el que está en suspenso; sólo en cuotas vencidas o en la cuota en curso; vacío = 0", Ejemplo: ""),
        new(Columnas.InteresDeMora, TipoDeValor.Monto, Reglas: "causado y no pagado; vacío = 0", Ejemplo: ""),
        new(Columnas.Seguro, TipoDeValor.Monto, Reglas: "causado y no pagado; vacío = 0", Ejemplo: ""),
        new(Columnas.Otros, TipoDeValor.Monto, Reglas: "otros cargos causados y no pagados; vacío = 0", Ejemplo: ""),
        new(Columnas.InteresProyectado, TipoDeValor.Monto, Reglas: "informativo: el que el sistema de origen proyectaba; el motor causa el suyo desde el corte", Ejemplo: ""),
    ]);

    // ---------------------------------------------------------------------------------------------------- 5.3.3 balances --

    public static DefinicionDePlantilla Saldos { get; } = Parte(Partes.Balances, "Migración — saldos por concepto",
    [
        Credito(Columnas.NumeroDeCredito, "llave con el concepto; un crédito de la parte loans"),
        new(Columnas.Concepto, TipoDeValor.Enumeracion, Obligatoria: true,
            Reglas: "llave; Principal, CurrentInterest, DefaultInterest, CurrentInterestInSuspense, DefaultInterestInSuspense, Insurance, CollectionFee, OtherCharge, Advance, CreditBalance; Principal en cada crédito",
            Ejemplo: "Principal"),
        new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor o igual a cero", Ejemplo: "5000000,00"),
    ]);

    // --------------------------------------------------------------------------------------------------- 5.3.4 risk-data --

    public static DefinicionDePlantilla DatosDeRiesgo { get; } = Parte(Partes.RiskData, "Migración — mora, calificación y deterioro",
    [
        Credito(Columnas.NumeroDeCredito, "llave; una fila por crédito de la parte loans"),
        new(Columnas.DiasDeMora, TipoDeValor.Entero, Obligatoria: true, Reglas: "al corte; si difiere de lo que dicen las cuotas es aviso", Ejemplo: "0"),
        new(Columnas.Calificacion, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "A, B, C, D o E", Ejemplo: "A"),
        new(Columnas.Subcategoria, TipoDeValor.Enumeracion, Reglas: "None, E1, E2; vacío = None", Ejemplo: ""),
        new(Columnas.Causa, TipoDeValor.Enumeracion, Reglas: "Arrears, Model, Drag, Restructured, Evaluation, Analysis, SingleInstallment, NewLoan, Insolvency, Homologation", Ejemplo: ""),
        new(Columnas.DeterioroCapital, TipoDeValor.Monto, Obligatoria: true, Reglas: "deterioro individual del capital", Ejemplo: "0"),
        new(Columnas.DeterioroIntereses, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.DeterioroOtros, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.DeterioroGeneral, TipoDeValor.Monto, Reglas: "la parte del crédito en el general; el cierre base la suma por dimensión; vacío = 0", Ejemplo: ""),
        new(Columnas.FechaDeIngresoAE, TipoDeValor.Fecha, Reglas: "con calificación E", Ejemplo: ""),
        new(Columnas.EnSuspenso, TipoDeValor.SiNo, Reglas: "el crédito ya había suspendido la causación; vacío = no", Ejemplo: "no"),
    ]);

    // ------------------------------------------------------------------------------------------------ 5.3.5 restructurings --

    public static DefinicionDePlantilla Reestructuraciones { get; } = Parte(Partes.Restructurings, "Migración — reestructuraciones",
    [
        Credito(Columnas.NumeroDeCredito, "llave; un crédito de la parte loans"),
        new(Columnas.Clase, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Restructuring 5 o Modification 6: la da la causa, no el mecanismo", Ejemplo: "Restructuring"),
        new(Columnas.VecesReestructurado, TipoDeValor.Entero, Obligatoria: true, Reglas: "desde 1", Ejemplo: "1"),
        new(Columnas.FechaUltima, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2025-08-01"),
        new(Columnas.CalificacionAlReestructurar, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "A, B, C, D o E", Ejemplo: "C"),
        new(Columnas.CuotasConsecutivasPagadas, TipoDeValor.Entero, Obligatoria: true, Reglas: "mayor o igual a cero", Ejemplo: "3"),
        Credito(Columnas.CreditoAnterior, "el crédito del que nació; puede no estar en la carga", obligatoria: false),
    ]);

    // ---------------------------------------------------------------------------------------------------- 5.3.6 co-debtors --

    public static DefinicionDePlantilla Codeudores { get; } = Parte(Partes.CoDebtors, "Migración — codeudores y avalistas",
    [
        Credito(Columnas.NumeroDeCredito, "llave con el codeudor; un crédito de loans o de charged-off"),
        Persona(Columnas.Codeudor, obligatoria: true, "llave; distinta del deudor; " + PersonaViva),
        new(Columnas.Rol, TipoDeValor.Enumeracion, Reglas: "CoDebtor 2 o Guarantor 3; vacío = CoDebtor", Ejemplo: ""),
        new(Columnas.Desde, TipoDeValor.Fecha, Ejemplo: ""),
    ]);

    // ---------------------------------------------------------------------------------------------------- 5.3.7 collaterals --

    public static DefinicionDePlantilla Garantias { get; } = Parte(Partes.Collaterals, "Migración — garantías y pagarés",
    [
        Credito(Columnas.NumeroDeCredito, "llave con el tipo y la referencia; un crédito de loans o de charged-off"),
        new(Columnas.Tipo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "llave; PromissoryNote 1, Pledge 3, Mortgage 4, GuaranteeFund 5, Contributions 6 (el codeudor va en co-debtors)", Ejemplo: "PromissoryNote"),
        new(Columnas.Referencia, TipoDeValor.Texto, Obligatoria: true, Largo: 40, Reglas: "llave; número del pagaré, matrícula, placa o certificado del fondo", Ejemplo: "PG-55821"),
        new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Ejemplo: "5000000,00"),
        new(Columnas.FechaDeConstitucion, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2025-03-15"),
        new(Columnas.VigenteHasta, TipoDeValor.Fecha, Ejemplo: ""),
        new(Columnas.Estado, TipoDeValor.Enumeracion, Reglas: "Constituted 1, Valid 2, Released 3, Extinguished 4, Executed 5; vacío = Valid", Ejemplo: ""),
        new(Columnas.Descripcion, TipoDeValor.Texto, Largo: 400, Ejemplo: ""),
        new(Columnas.ValorAvaluo, TipoDeValor.Monto, Reglas: "en prenda e hipoteca", Ejemplo: ""),
        new(Columnas.FechaAvaluo, TipoDeValor.Fecha, Reglas: "en prenda e hipoteca", Ejemplo: ""),
        new(Columnas.Poliza, TipoDeValor.Texto, Largo: 40, Reglas: "en prenda e hipoteca", Ejemplo: ""),
        new(Columnas.PolizaVence, TipoDeValor.Fecha, Reglas: "en prenda e hipoteca", Ejemplo: ""),
        Persona(Columnas.Aseguradora, obligatoria: false, "en prenda e hipoteca; " + PersonaViva),
        new(Columnas.ClasePagare, TipoDeValor.Enumeracion, Reglas: "con PromissoryNote: Physical 1 o Dematerialized 2", Ejemplo: "Physical"),
        new(Columnas.CanalDeCustodia, TipoDeValor.Texto, Largo: 30, Reglas: "con Dematerialized", Ejemplo: ""),
        new(Columnas.IdExternoPagare, TipoDeValor.Texto, Largo: 60, Reglas: "con Dematerialized", Ejemplo: ""),
        Persona(Columnas.FondoDeGarantias, obligatoria: false, "con GuaranteeFund; " + PersonaViva),
        new(Columnas.Cobertura, TipoDeValor.Porcentaje, Reglas: "con GuaranteeFund; en puntos", Ejemplo: ""),
        new(Columnas.Comision, TipoDeValor.Porcentaje, Reglas: "con GuaranteeFund; en puntos", Ejemplo: ""),
    ]);

    // --------------------------------------------------------------------------------------------------- 5.3.8 charged-off --

    public static DefinicionDePlantilla Castigados { get; } = Parte(Partes.ChargedOff, "Migración — cartera castigada",
    [
        Credito(Columnas.Numero, "llave; no puede estar también en loans"),
        Persona(Columnas.Deudor, obligatoria: true),
        new(Columnas.Oficina, TipoDeValor.Sucursal, Obligatoria: true, Largo: 20, Reglas: "código o nombre de la sucursal; debe estar vinculada a una oficina", Ejemplo: "NORTE"),
        new(Columnas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "LIBRE"),
        new(Columnas.FechaDeDesembolso, TipoDeValor.Fecha, Ejemplo: ""),
        new(Columnas.FechaDeCastigo, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2024-11-30"),
        new(Columnas.CapitalCastigado, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero", Ejemplo: "1200000,00"),
        new(Columnas.InteresesCastigados, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.OtrosCastigados, TipoDeValor.Monto, Reglas: "vacío = 0", Ejemplo: ""),
        new(Columnas.SoporteDelCastigo, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Reglas: "referencia del acta con que se castigó, como dato", Ejemplo: "Acta 112 del consejo"),
    ]);

    // --------------------------------------------------------------------------------------------------------- 5.3.9 sales --

    public static DefinicionDePlantilla Ventas { get; } = Parte(Partes.Sales, "Migración — ventas a crédito por factura",
    [
        new(Columnas.Factura, TipoDeValor.Texto, Obligatoria: true, Largo: 40, Reglas: "llave; tal como la muestra la venta", Ejemplo: "FE-1001"),
        new(Columnas.FechaDeVenta, TipoDeValor.Fecha, Obligatoria: true, Reglas: "anterior o igual al corte", Ejemplo: "2026-10-05"),
        Persona(Columnas.Deudor, obligatoria: true),
        new(Columnas.ValorOriginal, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero", Ejemplo: "900000,00"),
        new(Columnas.CobrosExternos, TipoDeValor.Monto, Reglas: "cobros recibidos por fuera, sumados por factura; vacío = 0", Ejemplo: ""),
        new(Columnas.NotasYAjustes, TipoDeValor.Monto, Reglas: "neto de notas crédito (−) y débito (+) hasta el corte; vacío = 0", Ejemplo: ""),
        new(Columnas.SaldoAlCorte, TipoDeValor.Monto, Obligatoria: true, Reglas: "= valorOriginal − cobrosExternos + notasYAjustes; mayor o igual a cero", Ejemplo: "900000,00"),
        new(Columnas.Resultado, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Migrated (con saldo y un crédito de loans con esta factura) o CancelledBeforeCutoff (saldo cero)", Ejemplo: "Migrated"),
        Credito(Columnas.NumeroDeCredito, "con Migrated: el crédito de loans", obligatoria: false),
        new(Columnas.Linea, TipoDeValor.Codigo, Largo: 10, Reglas: "con CancelledBeforeCutoff: la línea de la obligación en cero; vacío = la de origen comercial por defecto", Ejemplo: ""),
        new(Columnas.ReferenciaDeLosCobros, TipoDeValor.Texto, Largo: 400, Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------------- 5.3.10 credit-limits --

    public static DefinicionDePlantilla Cupos { get; } = Parte(Partes.CreditLimits, "Migración — cupos",
    [
        Persona(Columnas.Persona, obligatoria: true, "llave; " + PersonaViva),
        new(Columnas.Tipo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "llave; Consolidated 1, ByLine 2, Revolving 3", Ejemplo: "Consolidated"),
        new(Columnas.Linea, TipoDeValor.Codigo, Largo: 10, Reglas: "llave; con ByLine y Revolving (en Revolving, una línea de origen comercial)", Ejemplo: ""),
        new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero", Ejemplo: "10000000,00"),
        new(Columnas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave", Ejemplo: "2026-01-01"),
        new(Columnas.VigenteHasta, TipoDeValor.Fecha, Ejemplo: ""),
        new(Columnas.Motivo, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Ejemplo: "Cupo aprobado por el comité"),
        new(Columnas.PreferenciaDeAbono, TipoDeValor.Enumeracion, Reglas: "ReduceTerm o ReduceInstallment; la heredan los créditos de las compras", Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------------------- 5.3.11 blocks --

    public static DefinicionDePlantilla Bloqueos { get; } = Parte(Partes.Blocks, "Migración — bloqueos, embargos, fallecimientos e insolvencias",
    [
        Persona(Columnas.Persona, obligatoria: true, "llave; " + PersonaViva),
        new(Columnas.Motivo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "llave; Manual 1, AssociateWithdrawal 6, Deceased 7, Insolvency 8, Garnishment 9 (los automáticos no se migran)", Ejemplo: "Garnishment"),
        Credito(Columnas.NumeroDeCredito, "si el bloqueo es de un crédito (embargo)", obligatoria: false),
        new(Columnas.Desde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave", Ejemplo: "2026-05-02"),
        new(Columnas.Hasta, TipoDeValor.Fecha, Ejemplo: ""),
        new(Columnas.Soporte, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Ejemplo: "Oficio 2026-0451 del juzgado"),
        Persona(Columnas.Aseguradora, obligatoria: false, "con Deceased; " + PersonaViva),
        new(Columnas.EstadoDeLaReclamacion, TipoDeValor.Enumeracion, Reglas: "con Deceased: InProcess 1, Paid 2, Denied 3, Withdrawn 4; vacío = InProcess", Ejemplo: ""),
        new(Columnas.EstadoDeLaInsolvencia, TipoDeValor.Enumeracion, Reglas: "con Insolvency: Negotiating 1, Agreement 2, Failed 3; vacío = Negotiating", Ejemplo: ""),
    ]);

    // ----------------------------------------------------------------------------------------------- 5.3.12 authorizations --

    public static DefinicionDePlantilla Autorizaciones { get; } = Parte(Partes.Authorizations, "Migración — autorizaciones del titular",
    [
        Persona(Columnas.Persona, obligatoria: true, "llave; deudor o codeudor de la carga; " + PersonaViva),
        new(Columnas.Finalidad, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "llave; GeneralTreatment 1, CreditProcessing 2, CreditBureauQuery 3, CreditBureauReport 4, ContactChannel 5", Ejemplo: "CreditBureauQuery"),
        new(Columnas.Canal, TipoDeValor.Enumeracion, Reglas: "llave; con ContactChannel: Sms 1, WhatsApp 2, Email 3, Call 4, Visit 5", Ejemplo: ""),
        new(Columnas.Estado, TipoDeValor.Enumeracion, Reglas: "Accepted o Revoked; vacío = Accepted", Ejemplo: ""),
        new(Columnas.Fecha, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2024-02-10"),
        new(Columnas.VersionDelTexto, TipoDeValor.Texto, Largo: 30, Reglas: "versión de la política con esa finalidad; vacío = sin versión", Ejemplo: ""),
        new(Columnas.Evidencia, TipoDeValor.Texto, Largo: 200, Reglas: "dónde está la evidencia; vacío = sin evidencia: el crédito se cobra pero no se consulta ni se reporta a centrales ni recibe mensajes de cobranza", Ejemplo: "Formato físico 2024-118"),
        new(Columnas.CanalDeObtencion, TipoDeValor.Texto, Largo: 60, Ejemplo: ""),
    ]);

    // ----------------------------------------------------------------------------------------------- 5.3.13 prior-notices --

    public static DefinicionDePlantilla AvisosPrevios { get; } = Parte(Partes.PriorNotices, "Migración — avisos previos enviados",
    [
        Credito(Columnas.NumeroDeCredito, "llave con el episodio"),
        new(Columnas.Episodio, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; vencimiento de la primera cuota impaga del episodio; si no es el vigente según las cuotas, aviso y la fila no se migra", Ejemplo: "2026-09-15"),
        new(Columnas.ComunicacionesExigidas, TipoDeValor.Entero, Obligatoria: true, Reglas: "1 o 2", Ejemplo: "1"),
        new(Columnas.FechaDeEnvio1, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-10-01"),
        new(Columnas.Canal1, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Email 1, Sms 2, WhatsApp 3, Letter 4", Ejemplo: "Letter"),
        new(Columnas.Referencia1, TipoDeValor.Texto, Obligatoria: true, Largo: 100, Reglas: "la constancia (guía, id del envío); sin ella el aviso nace esperando la constancia", Ejemplo: "GUIA-778812"),
        new(Columnas.FechaDeEnvio2, TipoDeValor.Fecha, Reglas: "con 2 comunicaciones; en un día distinto del primero", Ejemplo: ""),
        new(Columnas.Canal2, TipoDeValor.Enumeracion, Reglas: "con 2 comunicaciones", Ejemplo: ""),
        new(Columnas.Referencia2, TipoDeValor.Texto, Largo: 100, Reglas: "con 2 comunicaciones", Ejemplo: ""),
    ]);

    // ---------------------------------------------------------------------------------------------- 5.3.14 bureau-reports --

    public static DefinicionDePlantilla ReportesACentrales { get; } = Parte(Partes.BureauReports, "Migración — reportes vigentes en centrales",
    [
        Credito(Columnas.NumeroDeCredito, "llave con la central; un crédito de loans o de charged-off"),
        new(Columnas.Central, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "llave; código de la central", Ejemplo: "DATACRED"),
        new(Columnas.UltimoPeriodoReportado, TipoDeValor.Texto, Obligatoria: true, Largo: 7, Reglas: Mes, Ejemplo: "2026-10"),
        new(Columnas.NovedadReportada, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "tal como se reportó", Ejemplo: "AL DIA"),
        new(Columnas.DatoNegativo, TipoDeValor.SiNo, Obligatoria: true, Ejemplo: "no"),
        new(Columnas.Episodio, TipoDeValor.Fecha, Reglas: "con dato negativo", Ejemplo: ""),
        new(Columnas.ConAvisoPrevio, TipoDeValor.SiNo, Reglas: "con dato negativo; «no»: el primer archivo de esa central lo retira", Ejemplo: ""),
    ]);

    // ------------------------------------------------------------------------------------------------------- 5.3.15 claims --

    public static DefinicionDePlantilla Reclamos { get; } = Parte(Partes.Claims, "Migración — reclamos en trámite sobre el reporte",
    [
        Credito(Columnas.NumeroDeCredito, "llave con la central y la radicación"),
        new(Columnas.Central, TipoDeValor.Texto, Obligatoria: true, Largo: 20, Reglas: "llave", Ejemplo: "DATACRED"),
        new(Columnas.Tipo, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Claim 1 o Impersonation 2", Ejemplo: "Claim"),
        new(Columnas.FechaDeRadicacion, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; el plazo legal corre desde aquí", Ejemplo: "2026-11-20"),
        new(Columnas.Prorrogado, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: ""),
        new(Columnas.Descripcion, TipoDeValor.Texto, Largo: 400, Ejemplo: ""),
    ]);

    // ----------------------------------------------------------------------------------------------- 5.3.16 related-parties --

    // Perezosa: el registro (CatalogoDePlantillasDeCartera) cita los cobros provisionales de esta clase y esta parte cita sus columnas; leerla al
    // inicializar la clase dejaba nula la definición de los cobros en el registro según cuál de las dos se tocara primero.
    private static readonly Lazy<DefinicionDePlantilla> _vinculados = new(() => Parte(Partes.RelatedParties, "Migración — personas vinculadas",
        CatalogoDePlantillasDeCartera.Vinculados.Hojas[0].Columnas));

    public static DefinicionDePlantilla Vinculados => _vinculados.Value;

    // ---------------------------------------------------------------------------------------------- 5.3.17 arrears-history --

    public static DefinicionDePlantilla HistorialDeMora { get; } = Parte(Partes.ArrearsHistory, "Migración — historial de mora mensual",
    [
        Credito(Columnas.NumeroDeCredito, "llave con el mes; un crédito de loans o de charged-off"),
        new(Columnas.Mes, TipoDeValor.Texto, Obligatoria: true, Largo: 7, Reglas: "llave; " + Mes + ", anterior al mes del corte", Ejemplo: "2026-06"),
        new(Columnas.MoraMaximaDias, TipoDeValor.Entero, Obligatoria: true, Reglas: "mora máxima del mes", Ejemplo: "0"),
        new(Columnas.MoraAlCierreDias, TipoDeValor.Entero, Ejemplo: ""),
    ]);

    // -------------------------------------------------------------------------------------------- 6 cobros provisionales --

    public static DefinicionDePlantilla CobrosProvisionales { get; } = new(CobrosProvisionalesClave, "Cobros provisionales", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(Columnas.Factura, TipoDeValor.Texto, Obligatoria: true, Largo: 40, Reglas: "llave con la referencia contable; una venta confirmada después del corte, no una de la migración", Ejemplo: "FE-2045"),
            new(Columnas.Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor que cero; los cobros de una factura no superan su valor a crédito", Ejemplo: "150000,00"),
            new(Columnas.FechaValor, TipoDeValor.Fecha, Obligatoria: true, Reglas: "entre la fecha de la venta y el registro de la activación", Ejemplo: "2026-12-01"),
            new(Columnas.ReferenciaContable, TipoDeValor.Texto, Obligatoria: true, Largo: 60, Reglas: "llave; el comprobante que redujo la cuenta por cobrar provisional", Ejemplo: "RC-000812"),
            new(Columnas.Observaciones, TipoDeValor.Texto, Largo: 400, Ejemplo: ""),
        ]),
    ]);

    /// <summary>Las 17 partes en el orden de carga (plantillas.md §5.2), con su obligatoriedad (<c>sales</c> e historial se exigen aparte).</summary>
    public static IReadOnlyList<ParteDeMigracion> Todas => _todas.Value;

    private static readonly Lazy<IReadOnlyList<ParteDeMigracion>> _todas = new(() =>
    [
        new(Partes.Loans, 1, true, Creditos),
        new(Partes.Installments, 2, true, Cuotas),
        new(Partes.Balances, 3, true, Saldos),
        new(Partes.RiskData, 4, true, DatosDeRiesgo),
        new(Partes.Restructurings, 5, false, Reestructuraciones),
        new(Partes.CoDebtors, 6, false, Codeudores),
        new(Partes.Collaterals, 7, false, Garantias),
        new(Partes.ChargedOff, 8, false, Castigados),
        new(Partes.Sales, 9, false, Ventas),
        new(Partes.CreditLimits, 10, false, Cupos),
        new(Partes.Blocks, 11, false, Bloqueos),
        new(Partes.Authorizations, 12, false, Autorizaciones),
        new(Partes.PriorNotices, 13, false, AvisosPrevios),
        new(Partes.BureauReports, 14, false, ReportesACentrales),
        new(Partes.Claims, 15, false, Reclamos),
        new(Partes.RelatedParties, 16, false, Vinculados),
        new(Partes.ArrearsHistory, 17, false, HistorialDeMora),
    ]);

    /// <summary>La parte por su clave en kebab (<c>loans</c>) o completa (<c>lending.migration.loans</c>), sin distinguir mayúsculas; nula si no existe.</summary>
    public static ParteDeMigracion? Buscar(string? parte)
    {
        if (string.IsNullOrWhiteSpace(parte)) return null;
        var clave = parte.Trim();
        if (clave.StartsWith(PrefijoDeClave, StringComparison.OrdinalIgnoreCase)) clave = clave[PrefijoDeClave.Length..];
        return Todas.FirstOrDefault(p => string.Equals(p.Parte, clave, StringComparison.OrdinalIgnoreCase));
    }
}
