using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US7 (T0206 a T0208; data-model §21; DT §2.7): los errores de las pagadurías en Recaudo. Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeRecaudo
{
    public const string CodigoReemplazaArchivoEnviado = "Receipts.Paymaster.SupersedesSentFile";
    public const string CodigoVersionNoAdoptada = "Receipts.Paymaster.VersionNotAdopted";
    public const string CodigoGiroExcedePendiente = "Receipts.Paymaster.RemittanceExceedsPending";

    public static Error ReemplazaArchivoEnviado(string archivo) => new ErrorConDatos(CodigoReemplazaArchivoEnviado,
        $"Llegó una versión que reemplaza al archivo {archivo}, pero ese archivo ya se envió a la pagaduría: no se reemplaza.", new { file = archivo });

    public static Error PagaduriaDesconocida() => new("Receipts.Paymaster.PaymasterUnknown",
        "La pagaduría del archivo no existe en el maestro de personas.");

    public static Error PersonaDelArchivoDesconocida() => new("Receipts.Paymaster.PersonUnknown",
        "Una persona de las líneas del archivo no existe en el maestro de personas.");

    public static Error ArchivoNoEnviable(string estado) => new ErrorConDatos("Receipts.Paymaster.FileNotSendable",
        $"El archivo está «{estado}»: sólo se confirma el envío de un archivo recibido.", new { state = estado });

    public static Error ArchivoSinEnviar() => new("Receipts.Paymaster.FileNotSent",
        "Confirme primero el envío del archivo a la pagaduría: la respuesta se carga sobre un archivo enviado.");

    public static Error FormatoDeRespuestaInexistente() => new("Receipts.Paymaster.ResponseFormatNotFound",
        "No hay un formato de respuesta de pagaduría vigente (ámbito 6) ni el genérico: cárguelo en Maestros › Formatos bancarios o capture la respuesta a mano.");

    public static Error RespuestaIlegible(IReadOnlyList<object> errores) => new ErrorConDatos("Receipts.Paymaster.ResponseUnreadable",
        $"La respuesta de la pagaduría tiene {errores.Count} error(es): revise las líneas y campos señalados.", new { errors = errores });

    public static Error RespuestaVacia() => new("Receipts.Paymaster.ResponseEmpty", "La respuesta de la pagaduría no trae líneas.");

    public static Error RespuestaNoEmitible(string estado) => new ErrorConDatos("Receipts.Paymaster.ResponseNotEmittable",
        $"La respuesta está «{estado}»: sólo se emite una respuesta revisada que no se haya emitido.", new { state = estado });

    public static Error VersionNoAdoptada(int version) => new ErrorConDatos(CodigoVersionNoAdoptada,
        $"Recaudo no tiene adoptada la versión {version} de DescuentosPorPagaduria en el registro de participantes: adóptela en la activación antes de responder este archivo.",
        new { version });

    public static Error GiroExcedePendiente(decimal pendiente) => new ErrorConDatos(CodigoGiroExcedePendiente,
        $"El giro supera lo pendiente de girar de los archivos elegidos ({pendiente:N2}) y no se pidió dejar el excedente a favor de la pagaduría.", new { pending = pendiente });

    public static Error GiroSinLineas() => new("Receipts.Paymaster.RemittanceNoLines",
        "Los archivos elegidos no tienen líneas descontadas pendientes de giro.");

    public static Error GiroYaAplicado() => new("Receipts.Paymaster.RemittanceApplied", "El giro ya está aplicado: no le quedan líneas pendientes.");

    public static Error SaldoNoEsDePagaduria() => new("Receipts.Paymaster.NotPaymasterBalance",
        "El saldo no es de una pagaduría: se resuelve desde «Saldos a favor».");

    public static Error SoporteDePagaduriaRequerido() => new("Receipts.Paymaster.SupportRequired",
        "Adjunte el soporte con que se resuelve el saldo de la pagaduría.");
}
