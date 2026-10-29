using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Treasury.Resultados;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US11 (T0158; FR-118 a FR-123, FR-212, FR-213, FR-215; data-model §21): los errores del resultado del giro y de la respuesta del banco. Los que
// data-model §21 no nombraba son (nuevo) y quedan en DT §2.7.
public static partial class ErroresDeTesoreria
{
    public const string CodigoResultadoNoEnviada = TransicionesDelResultado.CodigoNoEnviada;
    public const string CodigoTransicionInvalida = TransicionesDelResultado.CodigoTransicionInvalida;
    public const string CodigoSoporteRequerido = "Treasury.Result.SupportRequired";
    public const string CodigoDiscrepancia = "Treasury.Result.Discrepancy";

    public static Error ResultadoNoEnviada(string orden) => new ErrorConDatos(CodigoResultadoNoEnviada,
        $"La orden {orden} no se ha enviado al banco: todavía no tiene resultado que registrar.", new { order = orden });

    public static Error TransicionInvalida(string orden, string estado, string resultado) => new ErrorConDatos(CodigoTransicionInvalida,
        $"La orden {orden} está «{estado}» y no admite el resultado «{resultado}» (FR-215).", new { order = orden, state = estado, outcome = resultado });

    public static Error SoporteRequerido() => new(CodigoSoporteRequerido,
        "El registro manual del resultado exige su soporte (la constancia del banco o del portal): adjúntelo antes de registrar.");

    public static Error ResultadoInvalido(string resultado) => new ErrorConDatos("Treasury.Result.InvalidOutcome",
        $"«{resultado}» no es un resultado que se registre a mano: girada (Disbursed), rechazada (Rejected) o cheque no cobrado (CheckNotCashed).",
        new { outcome = resultado });

    public static Error CausalRequerida() => new("Treasury.Result.ReasonRequired",
        "Un rechazo o un cheque no cobrado lleva su causal: escríbala antes de registrar.");

    public static Error FormatoDeRespuestaInexistente() => new("Treasury.Response.FormatNotFound",
        "No hay un formato de respuesta vigente para el banco de la cuenta ni el genérico (RESP-PAGOS-GENERICO): créelo en Formatos bancarios.");

    public static Error RespuestaIlegible(IReadOnlyList<object> errores) => new ErrorConDatos("Treasury.Response.Unreadable",
        "La respuesta del banco no se pudo leer con su formato: revise las líneas señaladas.", new { errors = errores });

    public static Error RespuestaVacia() => new("Treasury.Response.Empty", "La respuesta del banco no trae líneas.");

    public static Error ResultadoDeRespuestaDesconocido(int linea, string texto) => new ErrorConDatos("Treasury.Response.UnknownOutcome",
        $"La línea {linea} trae el resultado «{texto}», que no es pagado ni rechazado.", new { line = linea, outcome = texto });

    public static Error LineaSinDiscrepancia() => new("Treasury.Response.NoDiscrepancy",
        "La línea no tiene discrepancia: ya se informó al cargar la respuesta.");

    public static Error LineaYaRevisada() => new("Treasury.Response.AlreadyReviewed", "La línea ya se revisó.");

    public static Error Discrepancia(int linea, string detalle) => new ErrorConDatos(CodigoDiscrepancia,
        $"La línea {linea} de la respuesta no coincide con el archivo generado ({detalle}): no se informa hasta que una persona la revise (FR-212).",
        new { line = linea, detail = detalle });
}
