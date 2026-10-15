using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>
/// Una plantilla de Cartera en el registro (plantillas.md §0.1, §0.7; api.md §3.9): clave, nombre, hojas, ruta base, el permiso para
/// descargarla vacía y para importarla, la entrega desde la que se importa y si trae datos personales. <see cref="Definicion"/> es la de
/// <see cref="EjecutorDeImportacion"/> cuando la plantilla ya está construida; nula, todavía no se descarga. (nuevo)
/// </summary>
public sealed record PlantillaDeCartera(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Sheets,
    string Route,
    string DownloadPermission,
    string ImportPermission,
    EntregaDeCartera AvailableFrom,
    bool PersonalData,
    DefinicionDePlantilla? Definicion = null);

/// <summary>
/// El registro de plantillas de importación de Cartera (feature 013, T0302, T82; plantillas.md §0.1, §0.7, §0.8): lo que
/// <c>GET /api/lending/templates</c> devuelve y las <see cref="DefinicionDePlantilla"/> de las claves <c>lending.*</c> (módulo de auditoría
/// <c>Lending</c>). US17 construye <c>lending.related-parties</c> (§2); US1 y US2 suman <c>lending.credit-lines</c> y
/// <c>lending.contribution-balances</c>, US8 las 17 partes de la migración (<see cref="DefinicionesDeMigracion"/>, una fila del registro) y los
/// cobros provisionales. Las plantillas de plataforma
/// (listas restrictivas, valores nacionales) no están: viven en sus pantallas. <b>Ninguna plantilla crea personas.</b> (nuevo)
/// </summary>
public static class CatalogoDePlantillasDeCartera
{
    public const string VinculadosClave = "lending.related-parties";

    public static class ColumnasDeVinculados
    {
        public const string Persona = "persona";
        public const string Fundamento = "fundamento";
        public const string PersonaRelacionada = "personaRelacionada";
        public const string ClaseDeParentesco = "claseDeParentesco";
        public const string Grado = "grado";
        public const string PorcentajeDeAportes = "porcentajeDeAportes";
        public const string VigenteDesde = "vigenteDesde";
        public const string VigenteHasta = "vigenteHasta";
        public const string Soporte = "soporte";
    }

    /// <summary>Plantilla 2 — personas vinculadas (plantillas.md §2). Llave: persona + fundamento + persona relacionada + vigente desde.</summary>
    public static DefinicionDePlantilla Vinculados { get; } = new(VinculadosClave, "Personas vinculadas", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(ColumnasDeVinculados.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; documento de la persona vinculada sin puntos ni dígito de verificación; debe existir y estar viva en Maestros › Personas (esta plantilla no crea personas)"),
            new(ColumnasDeVinculados.Fundamento, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "llave; BoardMember (consejero), OversightBoardMember (junta de vigilancia), LegalRepresentative (representante legal), SignificantContributor (aportante significativo), Spouse (cónyuge), Relative (pariente)",
                Ejemplo: "BoardMember"),
            new(ColumnasDeVinculados.PersonaRelacionada, TipoDeValor.Persona, Largo: 20,
                Reglas: "llave; obligatoria con Spouse y Relative: el miembro, representante o asociado del que se deriva el vínculo", Ejemplo: ""),
            new(ColumnasDeVinculados.ClaseDeParentesco, TipoDeValor.Enumeracion,
                Reglas: "obligatoria con Relative: Consanguinity (consanguinidad), Affinity (afinidad) o Civil", Ejemplo: ""),
            new(ColumnasDeVinculados.Grado, TipoDeValor.Entero, Reglas: "obligatorio con Relative; dentro de los grados legales (límite nacional)", Ejemplo: ""),
            new(ColumnasDeVinculados.PorcentajeDeAportes, TipoDeValor.Porcentaje, Reglas: "con SignificantContributor; en puntos (5 = 5 %)", Ejemplo: ""),
            new(ColumnasDeVinculados.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; yyyy-MM-dd", Ejemplo: "2026-03-15"),
            new(ColumnasDeVinculados.VigenteHasta, TipoDeValor.Fecha, Reglas: "vacía = vigente", Ejemplo: ""),
            new(ColumnasDeVinculados.Soporte, TipoDeValor.Texto, Obligatoria: true, Largo: 200,
                Reglas: "acta de elección, certificado o registro civil", Ejemplo: "Acta de asamblea 45"),
        ]),
    ]);

    public const string SaldosDeAportesClave = "lending.contribution-balances";

    /// <summary>Las columnas de la plantilla 3 (plantillas.md §3). (nuevo, US2)</summary>
    public static class ColumnasDeSaldosDeAportes
    {
        public const string Persona = "persona";
        public const string FechaDeCorte = "fechaDeCorte";
        public const string SaldoDeAportes = "saldoDeAportes";
        public const string SaldoDeAhorroPermanente = "saldoDeAhorroPermanente";
        public const string Fuente = "fuente";
    }

    /// <summary>
    /// Plantilla 3 — saldos de aportes y ahorro permanente (feature 013, US2, T0407; plantillas.md §3; D19). Llave: persona + fecha de corte.
    /// Provisional mientras Aportes y Ahorros no ofrezca su consulta. (nuevo)
    /// </summary>
    public static DefinicionDePlantilla SaldosDeAportes { get; } = new(SaldosDeAportesClave, "Saldos de aportes y ahorro permanente", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(ColumnasDeSaldosDeAportes.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; documento sin puntos ni dígito de verificación; debe existir y estar viva en Maestros › Personas (esta plantilla no crea personas)"),
            new(ColumnasDeSaldosDeAportes.FechaDeCorte, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; yyyy-MM-dd; no futura", Ejemplo: "2026-11-30"),
            new(ColumnasDeSaldosDeAportes.SaldoDeAportes, TipoDeValor.Monto, Obligatoria: true, Reglas: "mayor o igual a cero", Ejemplo: "2500000,00"),
            new(ColumnasDeSaldosDeAportes.SaldoDeAhorroPermanente, TipoDeValor.Monto, Reglas: "mayor o igual a cero; vacío = 0", Ejemplo: ""),
            new(ColumnasDeSaldosDeAportes.Fuente, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Reglas: "sistema o reporte de donde salió el saldo",
                Ejemplo: "Reporte de aportes de noviembre"),
        ]),
    ]);

    public const string LineasClave = "lending.credit-lines";

    /// <summary>Las hojas de la plantilla 1 (plantillas.md §1).</summary>
    public static class HojasDeLineas
    {
        public const string Lineas = "Lineas";
        public const string Tasas = "Tasas";
        public const string Documentos = "Documentos";
        public const string Seguros = "Seguros";
        public const string Garantias = "Garantias";
        public const string Equivalencias = "Equivalencias";
    }

    /// <summary>Las columnas de la plantilla 1 (plantillas.md §1); las que se repiten entre hojas llevan el mismo nombre.</summary>
    public static class ColumnasDeLineas
    {
        // Hoja Lineas.
        public const string Codigo = "codigo";
        public const string VigenteDesde = "vigenteDesde";
        public const string Nombre = "nombre";
        public const string Destino = "destino";
        public const string Modalidad = "modalidad";
        public const string Subsegmento = "subsegmento";
        public const string ParaEmpleados = "paraEmpleados";
        public const string Activa = "activa";
        public const string AdmiteNoAsociados = "admiteNoAsociados";
        public const string AdmiteOrigenComercial = "admiteOrigenComercial";
        public const string ConceptoTributario = "conceptoTributarioDelInteres";
        public const string SoporteDelConcepto = "soporteDelConceptoTributario";
        public const string TipoDeTasa = "tipoDeTasa";
        public const string TasaCorriente = "tasaCorrienteEA";
        public const string Indice = "indice";
        public const string ClaveDelIndice = "claveDelIndice";
        public const string Margen = "margen";
        public const string MesesDeReajuste = "mesesDeReajuste";
        public const string TasaPiso = "tasaPiso";
        public const string TasaTecho = "tasaTecho";
        public const string TipoDeMora = "tipoDeTasaDeMora";
        public const string TasaDeMora = "tasaDeMoraEA";
        public const string FactorDeMora = "factorDeMora";
        public const string ConvencionDeDias = "convencionDeDias";
        public const string Equivalencia = "equivalencia";
        public const string RedondeoUnidad = "redondeoUnidad";
        public const string RedondeoModo = "redondeoModo";
        public const string RedondeoMomento = "redondeoMomento";
        public const string Sistema = "sistemaDeAmortizacion";
        public const string AdmiteExtraordinarias = "admiteExtraordinarias";
        public const string Gracia = "gracia";
        public const string GraciaMaxima = "graciaMaximaCuotas";
        public const string InteresEnGracia = "interesEnGracia";
        public const string MontoMinimo = "montoMinimo";
        public const string MontoMaximo = "montoMaximo";
        public const string PlazoMinimo = "plazoMinimo";
        public const string PlazoMaximo = "plazoMaximo";
        public const string Periodicidades = "periodicidades";
        public const string PeriodicidadPorDefecto = "periodicidadPorDefecto";
        public const string DiaDePago = "diaDePago";
        public const string Prelacion = "prelacion";
        public const string RecalculoPorNotas = "recalculoPorNotas";
        public const string FormasDePago = "formasDePago";
        public const string FormaDePagoPorDefecto = "formaDePagoPorDefecto";
        public const string AdmiteTramos = "admiteTramos";
        public const string RelacionCupoAportes = "relacionCupoAportes";
        public const string EndeudamientoMaximo = "endeudamientoMaximo";
        public const string SaldoMinimoDeAportes = "saldoMinimoDeAportes";
        public const string ExigeCentrales = "exigeConsultaACentrales";
        public const string Notas = "notas";

        // Hojas hijas (Tasas, Documentos, Seguros, Garantias): la versión que las tiene.
        public const string Linea = "linea";
        public const string MontoDesde = "montoDesde";
        public const string MontoHasta = "montoHasta";
        public const string PlazoDesde = "plazoDesde";
        public const string PlazoHasta = "plazoHasta";
        public const string TipoDeSolicitante = "tipoDeSolicitante";
        public const string AntiguedadMinima = "antiguedadMinimaMeses";
        public const string Obligatorio = "obligatorio";
        public const string Aseguradora = "aseguradora";
        public const string Base = "base";
        public const string Tarifa = "tarifa";
        public const string Periodicidad = "periodicidad";
        public const string RetieneParteDeLaPrima = "retieneParteDeLaPrima";
        public const string PorcentajeRetenido = "porcentajeRetenido";
        public const string CodeudoresMinimos = "codeudoresMinimos";
        public const string TiposAdmitidos = "tiposAdmitidos";
        public const string CoberturaMinima = "coberturaMinima";
        public const string AportesCuentan = "aportesCuentanComoGarantia";

        // Hoja Equivalencias (data-model §1.9: una fila es una periodicidad o una unidad de plazo de Comercial).
        public const string PeriodicidadComercial = "periodicidadComercial";
        public const string UnidadDePlazo = "unidadDePlazo";
        public const string DiasPorUnidad = "diasPorUnidad";
    }

    private const string ListaDePeriodicidades =
        "SinglePayment 0 (pago único), Weekly 7 (semanal), TenDay 10 (decadal), Biweekly 15 (quincenal), Monthly 30 (mensual), Bimonthly 60 (bimestral), Quarterly 90 (trimestral), SemiAnnual 180 (semestral), Annual 360 (anual)";

    private const string ListaDeFormasDePago = "Cashier 1 (caja), OwnPayroll 2 (nómina propia), PaymasterDeduction 3 (libranza), DirectDebit 4 (débito automático)";

    /// <summary>
    /// Plantilla 1 — líneas de crédito (plantillas.md §1; FR-019–FR-033): cada fila de <c>Lineas</c> es una versión (llave <c>codigo</c> +
    /// <c>vigenteDesde</c>) y las hojas <c>Tasas</c>, <c>Documentos</c>, <c>Seguros</c> y <c>Garantias</c> cuelgan de ella y la reemplazan
    /// entera; <c>Equivalencias</c> es de la cooperativa. Las reglas son las del alta unitaria (<c>ReglasDeLineaDeCredito</c>). Los plazos
    /// van en meses (columnas <c>MinTermMonths</c>/<c>MaxTermMonths</c>).
    /// </summary>
    public static DefinicionDePlantilla LineasDeCredito { get; } = new(LineasClave, "Líneas de crédito", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla(HojasDeLineas.Lineas,
        [
            new(ColumnasDeLineas.Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; la nomenclatura de la cooperativa, no cambia", Ejemplo: "LIBRE"),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; desde cuándo rige la versión (yyyy-MM-dd); antes de hoy sólo en una línea sin solicitudes ni créditos", Ejemplo: "2026-12-01"),
            new(ColumnasDeLineas.Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Libre inversión"),
            new(ColumnasDeLineas.Destino, TipoDeValor.Texto, Obligatoria: true, Largo: 200, Ejemplo: "Libre destino"),
            new(ColumnasDeLineas.Modalidad, TipoDeValor.Codigo, Obligatoria: true, Largo: 40, Reglas: "código del catálogo nacional de modalidades vigente; VIVIENDA llega con una entrega posterior", Ejemplo: "CONSUMO"),
            new(ColumnasDeLineas.Subsegmento, TipoDeValor.Texto, Largo: 30, Reglas: "ConLibranza, SinLibranza, PersonaNatural, PersonaJuridica, Inmobiliario, Empresarial; la combinación debe existir en el catálogo", Ejemplo: "SinLibranza"),
            new(ColumnasDeLineas.ParaEmpleados, TipoDeValor.SiNo, Reglas: "línea para empleados de la cooperativa; vacío = no", Ejemplo: "no"),
            new(ColumnasDeLineas.Activa, TipoDeValor.SiNo, Reglas: "«no» cierra la línea a créditos nuevos; vacío = sí", Ejemplo: "sí"),
            new(ColumnasDeLineas.AdmiteNoAsociados, TipoDeValor.SiNo, Reglas: "sólo si el perfil regulatorio de la cooperativa lo permite", Ejemplo: "no"),
            new(ColumnasDeLineas.AdmiteOrigenComercial, TipoDeValor.SiNo, Reglas: "admite ventas a crédito de Comercial", Ejemplo: "no"),
            new(ColumnasDeLineas.ConceptoTributario, TipoDeValor.Enumeracion, Reglas: "NotTaxed (no gravado) o Taxed (gravado); obligatorio con origen comercial y tasa mayor que cero", Ejemplo: ""),
            new(ColumnasDeLineas.SoporteDelConcepto, TipoDeValor.Texto, Largo: 200, Reglas: "el identificador del adjunto con el concepto de la contadora (súbalo primero en la línea)", Ejemplo: ""),
            new(ColumnasDeLineas.TipoDeTasa, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Fixed (fija) o Variable", Ejemplo: "Fixed"),
            new(ColumnasDeLineas.TasaCorriente, TipoDeValor.Porcentaje, Reglas: "con Fixed; efectiva anual en puntos, hasta 4 decimales (22,5 = 22,5 % EA)", Ejemplo: "22,5"),
            new(ColumnasDeLineas.Indice, TipoDeValor.Codigo, Largo: 20, Reglas: "con Variable: IBR, IPC u otro índice nacional", Ejemplo: ""),
            new(ColumnasDeLineas.ClaveDelIndice, TipoDeValor.Texto, Largo: 20, Reglas: "con Variable: plazo o serie del índice, tal como la registra la plataforma", Ejemplo: ""),
            new(ColumnasDeLineas.Margen, TipoDeValor.Porcentaje, Reglas: "con Variable; en puntos, se suma en la expresión del índice", Ejemplo: ""),
            new(ColumnasDeLineas.MesesDeReajuste, TipoDeValor.Entero, Reglas: "con Variable: 1, 2, 3, 6 o 12", Ejemplo: ""),
            new(ColumnasDeLineas.TasaPiso, TipoDeValor.Porcentaje, Ejemplo: ""),
            new(ColumnasDeLineas.TasaTecho, TipoDeValor.Porcentaje, Ejemplo: ""),
            new(ColumnasDeLineas.TipoDeMora, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Fixed (fija) o RelativeToCurrent (relativa a la corriente)", Ejemplo: "RelativeToCurrent"),
            new(ColumnasDeLineas.TasaDeMora, TipoDeValor.Porcentaje, Reglas: "con Fixed; nunca por encima de la usura del día", Ejemplo: ""),
            new(ColumnasDeLineas.FactorDeMora, TipoDeValor.Costo, Reglas: "con RelativeToCurrent: múltiplo de la corriente (1,5); pendiente de validar por la contadora", Ejemplo: "1,5"),
            new(ColumnasDeLineas.ConvencionDeDias, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Thirty360 (30/360), Actual365 (real/365), Actual360 (real/360)", Ejemplo: "Actual365"),
            new(ColumnasDeLineas.Equivalencia, TipoDeValor.Enumeracion, Reglas: "Compound (compuesta) o Linear (lineal); vacío = Compound", Ejemplo: ""),
            new(ColumnasDeLineas.RedondeoUnidad, TipoDeValor.Monto, Reglas: "0,01; 1; 10; 100 o 1000; vacío = 0,01", Ejemplo: ""),
            new(ColumnasDeLineas.RedondeoModo, TipoDeValor.Enumeracion, Reglas: "AwayFromZero, ToEven, Up, Down; vacío = AwayFromZero", Ejemplo: ""),
            new(ColumnasDeLineas.RedondeoMomento, TipoDeValor.Enumeracion, Reglas: "PerConcept, PerInstallment, PerTotal; vacío = PerInstallment", Ejemplo: ""),
            new(ColumnasDeLineas.Sistema, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "LevelPayment (cuota fija), ConstantPrincipal (abono constante), Bullet (pago único)", Ejemplo: "LevelPayment"),
            new(ColumnasDeLineas.AdmiteExtraordinarias, TipoDeValor.SiNo, Ejemplo: "no"),
            new(ColumnasDeLineas.Gracia, TipoDeValor.Enumeracion, Reglas: "None, Principal (a capital), Total; vacío = None", Ejemplo: ""),
            new(ColumnasDeLineas.GraciaMaxima, TipoDeValor.Entero, Reglas: "con gracia", Ejemplo: ""),
            new(ColumnasDeLineas.InteresEnGracia, TipoDeValor.Enumeracion, Reglas: "con Total: FirstInstallment o Distributed; nunca se capitaliza", Ejemplo: ""),
            new(ColumnasDeLineas.MontoMinimo, TipoDeValor.Monto, Obligatoria: true, Ejemplo: "500000,00"),
            new(ColumnasDeLineas.MontoMaximo, TipoDeValor.Monto, Obligatoria: true, Ejemplo: "30000000,00"),
            new(ColumnasDeLineas.PlazoMinimo, TipoDeValor.Entero, Obligatoria: true, Reglas: "en meses", Ejemplo: "1"),
            new(ColumnasDeLineas.PlazoMaximo, TipoDeValor.Entero, Obligatoria: true, Reglas: "en meses", Ejemplo: "60"),
            new(ColumnasDeLineas.Periodicidades, TipoDeValor.Lista, Obligatoria: true, Reglas: ListaDePeriodicidades, Ejemplo: "Monthly"),
            new(ColumnasDeLineas.PeriodicidadPorDefecto, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "una de la lista", Ejemplo: "Monthly"),
            new(ColumnasDeLineas.DiaDePago, TipoDeValor.Entero, Reglas: "1 a 31; vacío = el día del desembolso", Ejemplo: ""),
            new(ColumnasDeLineas.Prelacion, TipoDeValor.Enumeracion, Reglas: "OldestInstallmentFirst o ConceptFirst; vacío = OldestInstallmentFirst", Ejemplo: ""),
            new(ColumnasDeLineas.RecalculoPorNotas, TipoDeValor.Enumeracion, Reglas: "con origen comercial: ReduceTerm o ReduceInstallment", Ejemplo: ""),
            new(ColumnasDeLineas.FormasDePago, TipoDeValor.Lista, Obligatoria: true, Reglas: ListaDeFormasDePago, Ejemplo: "Cashier"),
            new(ColumnasDeLineas.FormaDePagoPorDefecto, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "una de la lista", Ejemplo: "Cashier"),
            new(ColumnasDeLineas.AdmiteTramos, TipoDeValor.SiNo, Ejemplo: "no"),
            new(ColumnasDeLineas.RelacionCupoAportes, TipoDeValor.Costo, Reglas: "veces los aportes", Ejemplo: ""),
            new(ColumnasDeLineas.EndeudamientoMaximo, TipoDeValor.Monto, Ejemplo: ""),
            new(ColumnasDeLineas.SaldoMinimoDeAportes, TipoDeValor.Monto, Ejemplo: ""),
            new(ColumnasDeLineas.ExigeCentrales, TipoDeValor.SiNo, Ejemplo: "no"),
            new(ColumnasDeLineas.Notas, TipoDeValor.Texto, Largo: 300, Reglas: "el motivo de la versión; vacío = el motivo de la importación", Ejemplo: ""),
        ]),
        new HojaDePlantilla(HojasDeLineas.Tasas,
        [
            new(ColumnasDeLineas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "la línea de la hoja Lineas", Ejemplo: "LIBRE"),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "la versión de la hoja Lineas", Ejemplo: "2026-12-01"),
            new(ColumnasDeLineas.MontoDesde, TipoDeValor.Monto, Reglas: "vacío = sin límite", Ejemplo: "0"),
            new(ColumnasDeLineas.MontoHasta, TipoDeValor.Monto, Reglas: "vacío = sin límite", Ejemplo: "5000000,00"),
            new(ColumnasDeLineas.PlazoDesde, TipoDeValor.Entero, Reglas: "en meses", Ejemplo: ""),
            new(ColumnasDeLineas.PlazoHasta, TipoDeValor.Entero, Reglas: "en meses", Ejemplo: ""),
            new(ColumnasDeLineas.TipoDeSolicitante, TipoDeValor.Enumeracion, Reglas: "Associate (asociado) o Customer (cliente); vacío = cualquiera", Ejemplo: ""),
            new(ColumnasDeLineas.ParaEmpleados, TipoDeValor.SiNoIndiferente, Reglas: "vacío = indiferente", Ejemplo: ""),
            new(ColumnasDeLineas.AntiguedadMinima, TipoDeValor.Entero, Ejemplo: ""),
            new(ColumnasDeLineas.TasaCorriente, TipoDeValor.Porcentaje, Reglas: "en línea Fixed", Ejemplo: "24"),
            new(ColumnasDeLineas.Margen, TipoDeValor.Porcentaje, Reglas: "en línea Variable", Ejemplo: ""),
        ], Obligatoria: false),
        new HojaDePlantilla(HojasDeLineas.Documentos,
        [
            new(ColumnasDeLineas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "LIBRE"),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-12-01"),
            new(ColumnasDeLineas.Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave dentro de la versión", Ejemplo: "CEDULA"),
            new(ColumnasDeLineas.Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Copia de cédula"),
            new(ColumnasDeLineas.Obligatorio, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
            new(ColumnasDeLineas.MontoDesde, TipoDeValor.Monto, Ejemplo: ""),
            new(ColumnasDeLineas.MontoHasta, TipoDeValor.Monto, Ejemplo: ""),
            new(ColumnasDeLineas.TipoDeSolicitante, TipoDeValor.Enumeracion, Reglas: "Associate o Customer; vacío = cualquiera", Ejemplo: ""),
            new(ColumnasDeLineas.FormasDePago, TipoDeValor.Lista, Reglas: "una sola forma de pago o vacío (todas)", Ejemplo: ""),
        ], Obligatoria: false),
        new HojaDePlantilla(HojasDeLineas.Seguros,
        [
            new(ColumnasDeLineas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "LIBRE"),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-12-01"),
            new(ColumnasDeLineas.Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "VIDA"),
            new(ColumnasDeLineas.Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Vida deudores"),
            new(ColumnasDeLineas.Aseguradora, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento de la aseguradora en Maestros › Personas, sin dígito de verificación (esta plantilla no crea personas)", Ejemplo: ""),
            new(ColumnasDeLineas.Base, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "OutstandingBalance (saldo) o InitialAmount (monto inicial)", Ejemplo: "OutstandingBalance"),
            new(ColumnasDeLineas.Tarifa, TipoDeValor.Porcentaje, Obligatoria: true, Reglas: "por período de cobro, en puntos", Ejemplo: "0,045"),
            new(ColumnasDeLineas.Periodicidad, TipoDeValor.Enumeracion, Reglas: "vacío = la periodicidad por defecto de la línea", Ejemplo: ""),
            new(ColumnasDeLineas.Obligatorio, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
            new(ColumnasDeLineas.RetieneParteDeLaPrima, TipoDeValor.SiNo, Reglas: "con «sí», el seguro computa en la usura", Ejemplo: "no"),
            new(ColumnasDeLineas.PorcentajeRetenido, TipoDeValor.Porcentaje, Reglas: "con el anterior, en puntos", Ejemplo: ""),
        ], Obligatoria: false),
        new HojaDePlantilla(HojasDeLineas.Garantias,
        [
            new(ColumnasDeLineas.Linea, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Ejemplo: "LIBRE"),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-12-01"),
            new(ColumnasDeLineas.MontoDesde, TipoDeValor.Monto, Ejemplo: "10000001,00"),
            new(ColumnasDeLineas.MontoHasta, TipoDeValor.Monto, Ejemplo: ""),
            new(ColumnasDeLineas.PlazoDesde, TipoDeValor.Entero, Ejemplo: ""),
            new(ColumnasDeLineas.PlazoHasta, TipoDeValor.Entero, Ejemplo: ""),
            new(ColumnasDeLineas.CodeudoresMinimos, TipoDeValor.Entero, Reglas: "vacío = 0", Ejemplo: "1"),
            new(ColumnasDeLineas.TiposAdmitidos, TipoDeValor.Lista, Obligatoria: true, Reglas: "PromissoryNote 1, CoDebtor 2, Pledge 3, Mortgage 4, GuaranteeFund 5, Contributions 6", Ejemplo: "CoDebtor"),
            new(ColumnasDeLineas.CoberturaMinima, TipoDeValor.Porcentaje, Reglas: "sobre el monto, en puntos (120 = 120 %)", Ejemplo: ""),
            new(ColumnasDeLineas.AportesCuentan, TipoDeValor.SiNo, Ejemplo: "no"),
        ], Obligatoria: false),
        new HojaDePlantilla(HojasDeLineas.Equivalencias,
        [
            new(ColumnasDeLineas.PeriodicidadComercial, TipoDeValor.Texto, Largo: 20, Reglas: "el texto que manda Comercial (Weekly, Biweekly, Monthly, SinglePayment); va con periodicidad", Ejemplo: "Monthly"),
            new(ColumnasDeLineas.Periodicidad, TipoDeValor.Enumeracion, Reglas: "la periodicidad de Cartera que le corresponde", Ejemplo: "Monthly"),
            new(ColumnasDeLineas.UnidadDePlazo, TipoDeValor.Texto, Largo: 20, Reglas: "el texto de la unidad de plazo de Comercial (Days, Months); va con diasPorUnidad", Ejemplo: ""),
            new(ColumnasDeLineas.DiasPorUnidad, TipoDeValor.Entero, Reglas: "días = 1, meses = 30, años = 360", Ejemplo: ""),
            new(ColumnasDeLineas.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Ejemplo: "2026-12-01"),
        ], Obligatoria: false),
    ]);

    /// <summary>El registro de plantillas.md §0.1 que aparece en <c>/cartera/plantillas</c> (sin las de plataforma), en su orden de carga.</summary>
    public static IReadOnlyList<PlantillaDeCartera> Todas { get; } =
    [
        new(LineasClave, "Líneas de crédito", "Líneas con sus tasas, documentos, seguros, garantías y equivalencias de plazos de Comercial.",
            ["Lineas", "Tasas", "Documentos", "Seguros", "Garantias", "Equivalencias"], "/api/lending/credit-lines",
            "Lending.CreditLines.View", "Lending.CreditLines.Manage", EntregaDeCartera.K1, PersonalData: false, LineasDeCredito),
        new(VinculadosClave, "Personas vinculadas", "Consejeros, junta de vigilancia, representantes, aportantes significativos, cónyuges y parientes (Ley 454, art. 61).",
            ["Datos"], "/api/lending/related-parties", "Lending.Templates.View", "Lending.RelatedParties.Manage", EntregaDeCartera.K1, PersonalData: true,
            Vinculados),
        new(SaldosDeAportesClave, "Saldos de aportes y ahorro permanente", "Los saldos que alimentan el cupo por múltiplo de aportes.",
            ["Datos"], "/api/lending/contribution-balances", "Lending.Templates.View", "Lending.CreditLimits.Assign", EntregaDeCartera.K1, PersonalData: true,
            SaldosDeAportes),
        new(MigracionClave, "Migración por partes", "La cartera vigente al corte, parte por parte, para revisar, aplicar y activar.",
            ["Datos"], "/api/lending/migration/loads", "Lending.Templates.View", DefinicionesDeMigracion.PermisoDeImportacion, EntregaDeCartera.K1,
            PersonalData: true),
        new(DefinicionesDeMigracion.CobrosProvisionalesClave, "Cobros provisionales", "Lo cobrado por fuera de Cartera entre el corte y la activación, por factura.",
            ["Datos"], "/api/lending/activation/provisional-collections", "Lending.Templates.View", DefinicionesDeMigracion.PermisoDeCobrosProvisionales,
            EntregaDeCartera.K1, PersonalData: false, DefinicionesDeMigracion.CobrosProvisionales),
        new("lending.arrears-history", "Historial de mora complementario", "Los meses de mora que faltan para la pérdida esperada.",
            ["Datos"], "/api/lending/risk/arrears-history", "Lending.Templates.View", "Lending.ArrearsHistory.Import", EntregaDeCartera.K6a, PersonalData: true),
        new("lending.payroll-agreements", "Convenios de pagaduría", "Pagadurías, convenios, contactos y códigos de respuesta.",
            ["Convenios", "Contactos", "CodigosDeRespuesta"], "/api/lending/payroll-agreements", "Lending.PayrollAgreements.View",
            "Lending.PayrollAgreements.Manage", EntregaDeCartera.K2, PersonalData: false, PayrollAgreementsTemplate.Definicion),
    ];

    /// <summary>La fila del registro de las 17 partes de la migración: cada parte es <c>lending.migration.{parte}</c> (US8, T0737).</summary>
    public const string MigracionClave = "lending.migration";

    /// <summary>
    /// La definición de cualquier clave de Cartera: las del registro y las 17 partes <c>lending.migration.{parte}</c>
    /// (<see cref="DefinicionesDeMigracion"/>); nula si no existe o todavía no está construida.
    /// </summary>
    public static DefinicionDePlantilla? DefinicionPorClave(string? clave) =>
        clave?.Trim().StartsWith(DefinicionesDeMigracion.PrefijoDeClave, StringComparison.OrdinalIgnoreCase) == true
            ? DefinicionesDeMigracion.Buscar(clave)?.Definicion
            : Buscar(clave)?.Definicion;

    /// <summary>Si la plantilla ya se descarga: tiene definición, o es la de la migración (que se descarga parte por parte).</summary>
    public static bool Construida(PlantillaDeCartera p) => p.Definicion is not null || p.Key == MigracionClave;

    /// <summary>La plantilla por clave (sin distinguir mayúsculas), o nula.</summary>
    public static PlantillaDeCartera? Buscar(string? clave) =>
        string.IsNullOrWhiteSpace(clave) ? null : Todas.FirstOrDefault(p => string.Equals(p.Key, clave.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Si la entrega de la plantilla ya está desplegada (<see cref="CatalogoDeCartera.EntregaVigente"/>).</summary>
    public static bool Disponible(PlantillaDeCartera p) => p.AvailableFrom <= CatalogoDeCartera.EntregaVigente;
}

/// <summary>Una plantilla en el registro, con lo que puede hacer quien pregunta (plantillas.md §0.1, api.md §3.9). (nuevo)</summary>
public sealed record LendingTemplateDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<string> Sheets,
    string Route,
    string ImportPermission,
    EntregaDeCartera AvailableFrom,
    bool PersonalData,
    bool CanDownload,
    bool CanImport);

/// <summary>
/// El registro de plantillas de Cartera (api.md §3.9, <c>GET /api/lending/templates</c>, <c>Lending.Templates.View</c>): cada una con
/// <c>canDownload</c> (tiene el permiso de descargarla, su entrega está desplegada y ya está construida) y <c>canImport</c> (además, el
/// permiso de importar). Catálogo sin oficina. (nuevo)
/// </summary>
public sealed record ListLendingTemplatesQuery : IRequest<Result<IReadOnlyList<LendingTemplateDto>>>;

public sealed class ListLendingTemplatesQueryValidator : AbstractValidator<ListLendingTemplatesQuery>;

public sealed class ListLendingTemplatesQueryHandler(ICurrentUserPermissions permisos)
    : IRequestHandler<ListLendingTemplatesQuery, Result<IReadOnlyList<LendingTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<LendingTemplateDto>>> Handle(ListLendingTemplatesQuery request, CancellationToken ct)
    {
        var todos = permisos.EsMaestroGlobal;
        var concedidos = new HashSet<string>(todos ? Array.Empty<string>() : await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        bool Tiene(string p) => todos || concedidos.Contains(p);

        IReadOnlyList<LendingTemplateDto> lista = CatalogoDePlantillasDeCartera.Todas.Select(p =>
        {
            var descarga = CatalogoDePlantillasDeCartera.Disponible(p) && CatalogoDePlantillasDeCartera.Construida(p) && Tiene(p.DownloadPermission);
            return new LendingTemplateDto(p.Key, p.Name, p.Description, p.Sheets, p.Route, p.ImportPermission, p.AvailableFrom, p.PersonalData,
                descarga, descarga && Tiene(p.ImportPermission));
        }).ToList();
        return Result.Success(lista);
    }
}

/// <summary>
/// La definición de una plantilla para armar el libro vacío (api.md §3.9, <c>GET /api/lending/templates/{templateKey}/template.xlsx</c>;
/// el libro lo arma la API con <c>PlantillaDeImportacion</c>). Clave inexistente, de una entrega no desplegada o todavía sin construir →
/// <c>Lending.Template.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetLendingTemplateQuery(string TemplateKey) : IRequest<Result<DefinicionDePlantilla>>;

public sealed class GetLendingTemplateQueryValidator : AbstractValidator<GetLendingTemplateQuery>
{
    public GetLendingTemplateQueryValidator() => RuleFor(x => x.TemplateKey).NotEmpty().MaximumLength(60);
}

public sealed class GetLendingTemplateQueryHandler : IRequestHandler<GetLendingTemplateQuery, Result<DefinicionDePlantilla>>
{
    public Task<Result<DefinicionDePlantilla>> Handle(GetLendingTemplateQuery request, CancellationToken ct)
    {
        // Las 17 partes de la migración (lending.migration.{parte}) son de la fila «lending.migration» del registro (K1).
        var esParte = request.TemplateKey.Trim().StartsWith(DefinicionesDeMigracion.PrefijoDeClave, StringComparison.OrdinalIgnoreCase);
        var plantilla = CatalogoDePlantillasDeCartera.Buscar(esParte ? CatalogoDePlantillasDeCartera.MigracionClave : request.TemplateKey);
        var definicion = CatalogoDePlantillasDeCartera.DefinicionPorClave(request.TemplateKey);
        return Task.FromResult(plantilla is not null && definicion is not null && CatalogoDePlantillasDeCartera.Disponible(plantilla)
            ? Result.Success(definicion)
            : Result.Failure<DefinicionDePlantilla>(ErroresDeGobierno.PlantillaInexistente()));
    }
}
