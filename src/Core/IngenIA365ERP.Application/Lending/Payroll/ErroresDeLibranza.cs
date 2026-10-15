using System.Globalization;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Payroll;

/// <summary>
/// Los errores de la libranza (feature 013, K2, T0848; data-model §10 y §24; api.md §13): convenios, capacidad de descuento, archivo de
/// descuentos, respuestas de la pagaduría, pago de la pagaduría por el contrato, traslado y no aplicados. El código es el contrato; el mensaje
/// lo lee la persona y nunca lleva el código (<c>TextosDeCarteraTests</c>). (nuevo)
/// </summary>
public static class ErroresDeLibranza
{
    public const string CodigoConvenioInexistente = "Lending.PayrollAgreement.NotFound";
    public const string CodigoPagadorNoJuridico = "Lending.PayrollAgreement.PayerNotLegalEntity";
    public const string CodigoSinRegistroDeOperador = "Lending.PayrollAgreement.OperatorRegistrationMissing";
    public const string CodigoNominaPropiaRepetida = "Lending.PayrollAgreement.OwnPayrollAlreadyExists";
    public const string CodigoNominaPropiaNoEsLaCooperativa = "Lending.PayrollAgreement.OwnPayrollNotCooperative";
    public const string CodigoVersionCruzada = "Lending.PayrollAgreement.VersionOverlaps";
    public const string CodigoVersionEnUso = "Lending.PayrollAgreement.VersionInUse";
    public const string CodigoSinVersionVigente = "Lending.PayrollAgreement.NoCurrentVersion";
    public const string CodigoConvenioNoDisponible = "Lending.PayrollAgreement.NotAvailable";
    public const string CodigoCodigoDeRespuestaRepetido = "Lending.PayrollAgreement.ResultCodeDuplicate";
    public const string CodigoCapacidadExcedida = "Lending.Payroll.DeductionCapacityExceeded";
    public const string CodigoCertificacionRequerida = "Lending.Payroll.CertificationRequired";
    public const string CodigoNoEsLibranza = "Lending.Loan.NotPayroll";
    public const string CodigoArchivoYaEnviado = "Lending.PayrollFile.AlreadySent";
    public const string CodigoArchivoReemplazado = "Lending.PayrollFile.Superseded";
    public const string CodigoHuellaDistinta = "Lending.PayrollFile.HashMismatch";
    public const string CodigoLineasQueNoCasan = "Lending.PayrollFile.LineMismatch";
    public const string CodigoNominaPropiaSinArchivo = "Lending.PayrollFile.OwnPayrollHasNoFile";
    public const string CodigoArchivoVacio = "Lending.PayrollFile.Empty";
    public const string CodigoNoCabe = "Lending.PayrollFile.LineTooLong";
    public const string CodigoPeriodoInvalido = "Lending.PayrollFile.PeriodInvalid";
    public const string CodigoArchivoInexistente = "Lending.PayrollDeductionFile.NotFound";
    public const string CodigoReferenciaRequerida = "Lending.Payment.PaymasterReferenceRequired";
    public const string CodigoReferenciaInvalida = "Lending.Payment.PaymasterReferenceInvalid";
    public const string CodigoNoAplicadoYaResuelto = "Lending.InstallmentNonDeduction.AlreadyResolved";
    public const string CodigoNoAplicadoInexistente = "Lending.InstallmentNonDeduction.NotFound";

    public static Error ConvenioInexistente() => new(CodigoConvenioInexistente, "El convenio de libranza no existe.");

    public static Error PagadorNoJuridico(string nombre) => new ErrorConDatos(CodigoPagadorNoJuridico,
        $"«{nombre}» no es una persona jurídica: la pagaduría de un convenio de libranza es una empresa o entidad registrada en Maestros › Personas.",
        new { payer = nombre });

    public static Error SinRegistroDeOperador(DateOnly fecha) => new ErrorConDatos(CodigoSinRegistroDeOperador,
        $"El perfil regulatorio vigente al {fecha:yyyy-MM-dd} no registra a la cooperativa como operadora de libranza: regístrelo en el perfil "
        + "regulatorio antes de crear convenios o radicar por libranza.", new { date = fecha });

    public static Error NominaPropiaRepetida(string codigo) => new ErrorConDatos(CodigoNominaPropiaRepetida,
        $"Ya existe el convenio de la nómina propia ({codigo}): la cooperativa tiene uno solo.", new { existingCode = codigo });

    public static Error NominaPropiaNoEsLaCooperativa() => new(CodigoNominaPropiaNoEsLaCooperativa,
        "El convenio de la nómina propia lleva como pagaduría a la cooperativa misma: la persona jurídica con el NIT de la empresa.");

    public static Error VersionCruzada(DateOnly desde, DateOnly ultima) => new ErrorConDatos(CodigoVersionCruzada,
        $"La versión nueva empieza el {desde:yyyy-MM-dd} y la última registrada empieza el {ultima:yyyy-MM-dd}: una versión nueva empieza después "
        + "de la anterior, que rige hasta la víspera.", new { validFrom = desde, lastValidFrom = ultima });

    public static Error VersionEnUso(string archivo, DateOnly hasta) => new ErrorConDatos(CodigoVersionEnUso,
        $"La versión vigente ya respaldó el archivo enviado {archivo}, que cubre hasta el {hasta:yyyy-MM-dd}: la versión nueva empieza después de ese día.",
        new { file = archivo, coveredUntil = hasta });

    public static Error SinVersionVigente(string codigo, DateOnly fecha) => new ErrorConDatos(CodigoSinVersionVigente,
        $"El convenio {codigo} no tiene una versión vigente al {fecha:yyyy-MM-dd}: registre la versión antes de generar el archivo.",
        new { agreement = codigo, date = fecha });

    public static Error ConvenioNoDisponible(string? codigo, string motivo) => new ErrorConDatos(CodigoConvenioNoDisponible,
        codigo is null ? $"El convenio de libranza no está disponible: {motivo}." : $"El convenio {codigo} no está disponible: {motivo}.",
        new { agreement = codigo, reason = motivo });

    public static Error CodigoDeRespuestaRepetido(string codigo) => new ErrorConDatos(CodigoCodigoDeRespuestaRepetido,
        $"El código de respuesta «{codigo}» viene dos veces en la misma versión.", new { code = codigo });

    public static Error CapacidadExcedida(decimal disponible, decimal pedido) => new ErrorConDatos(CodigoCapacidadExcedida,
        $"La cuota de {Pesos(pedido)} no cabe en la capacidad de descuento: quedan {Pesos(disponible)} disponibles sobre lo certificado.",
        new { available = disponible, requested = pedido });

    public static Error CertificacionRequerida() => new(CodigoCertificacionRequerida,
        "La libranza con una pagaduría exige la certificación laboral y de descuentos con su fecha y el ingreso neto certificado.");

    public static Error NoEsLibranza(string numero) => new ErrorConDatos(CodigoNoEsLibranza,
        $"El crédito {numero} no se paga por libranza: no tiene pagaduría que trasladar.", new { loan = numero });

    public static Error ArchivoYaEnviado(string numero) => new ErrorConDatos(CodigoArchivoYaEnviado,
        $"El archivo {numero} del período ya se envió a la pagaduría: no se regenera ni se anula. La corrección va como novedad del período siguiente.",
        new { file = numero });

    public static Error ArchivoReemplazado(string numero, string estado) => new ErrorConDatos(CodigoArchivoReemplazado,
        $"El archivo {numero} ya no es la versión vigente del período (está {Estado(estado)}).", new { file = numero, status = estado });

    public static Error HuellaDistinta(string numero) => new ErrorConDatos(CodigoHuellaDistinta,
        $"La huella informada no es la del archivo {numero}: Recaudo envió otro contenido.", new { file = numero });

    public static Error LineasQueNoCasan(IReadOnlyList<object> lineas) => new ErrorConDatos(CodigoLineasQueNoCasan,
        $"{lineas.Count} línea(s) del resultado no son del archivo citado o esperan otro valor: Recaudo debe corregir y enviar un resultado nuevo.",
        new { lines = lineas });

    public static Error NominaPropiaSinArchivo(string codigo) => new ErrorConDatos(CodigoNominaPropiaSinArchivo,
        $"El convenio {codigo} es la nómina propia: no genera archivo, Nómina aplica sus descuentos por el contrato de obligaciones.",
        new { agreement = codigo });

    public static Error ArchivoVacio(string codigo, int anio, int mes) => new ErrorConDatos(CodigoArchivoVacio,
        string.Create(CultureInfo.InvariantCulture, $"El convenio {codigo} no tiene obligaciones ni novedades para el período {anio}-{mes:00}: no hay archivo que generar."),
        new { agreement = codigo, year = anio, month = mes });

    public static Error PeriodoInvalido(int anio, int mes, int? subPeriodo) => new ErrorConDatos(CodigoPeriodoInvalido,
        string.Create(CultureInfo.InvariantCulture, $"El período {anio}-{mes:00}{(subPeriodo is { } s ? $"/{s}" : string.Empty)} no existe para la periodicidad del convenio: ")
        + "una pagaduría mensual tiene un solo archivo por mes y una quincenal, dos (1 y 2).", new { year = anio, month = mes, subPeriod = subPeriodo });

    public static Error NoCabe(int linea, string campo, string credito) => new ErrorConDatos(CodigoNoCabe,
        $"El crédito {credito} no cabe en el formato de ida: el campo «{campo}» es más largo de lo que admite.",
        new { line = linea, field = campo, loan = credito });

    public static Error ArchivoInexistente() => new(CodigoArchivoInexistente, "El archivo de descuentos no existe.");

    public static Error ReferenciaRequerida(string credito) => new ErrorConDatos(CodigoReferenciaRequerida,
        $"El crédito {credito} tiene lo descontado por esta pagaduría pendiente de giro: el pago debe citar la línea del archivo que cubre.",
        new { loan = credito });

    public static Error ReferenciaInvalida(string credito) => new ErrorConDatos(CodigoReferenciaInvalida,
        $"La línea citada no está pendiente de giro para el crédito {credito} y esta pagaduría: ya se giró, no se descontó o es de otro crédito.",
        new { loan = credito });

    public static Error NoAplicadoYaResuelto() => new(CodigoNoAplicadoYaResuelto, "El no aplicado ya se resolvió.");

    public static Error NoAplicadoInexistente() => new(CodigoNoAplicadoInexistente, "El no aplicado no existe.");

    /// <summary>Un valor en pesos con separadores de Colombia.</summary>
    public static string Pesos(decimal valor) => "$" + valor.ToString("N2", new CultureInfo("es-CO"));

    private static string Estado(string estado) => estado switch
    {
        "Superseded" => "reemplazado por una versión posterior",
        "Voided" => "anulado",
        "Sent" => "enviado",
        _ => "generado",
    };
}
