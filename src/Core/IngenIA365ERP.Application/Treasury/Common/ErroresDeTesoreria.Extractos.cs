using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US6 (T0193; data-model §21; DT §2.7): los errores de la carga del extracto. Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeTesoreria
{
    public const string CodigoExtractoYaCargado = "Treasury.Statement.AlreadyLoaded";
    public const string CodigoLineasIlegibles = "Treasury.Statement.IllegibleLines";

    public static Error ExtractoYaCargado(string numero) => new ErrorConDatos(CodigoExtractoYaCargado,
        $"Este archivo ya se cargó en la cuenta (extracto {numero}): no se carga dos veces.", new { statement = numero });

    public static Error LineasIlegibles(IReadOnlyList<object> errores) => new ErrorConDatos(CodigoLineasIlegibles,
        $"El extracto tiene {errores.Count} línea(s) que no se pudieron leer: corríjalas o confirme cargar las demás (las ilegibles no se cargan).",
        new { errors = errores });

    public static Error ExtractoSinFormato() => new("Treasury.Statement.FormatNotFound",
        "No hay un formato de extracto vigente para el banco de la cuenta ni el genérico: cárguelo en Maestros › Formatos bancarios o capture el extracto a mano.");

    public static Error ExtractoVacio() => new("Treasury.Statement.Empty", "El extracto no trae movimientos.");

    public static Error ExtractoSinOrigen() => new("Treasury.Statement.SourceRequired",
        "Adjunte el archivo del extracto o capture sus movimientos a mano.");

    public static Error CuentaNoAdmiteExtracto() => new("Treasury.Statement.AccountInactive",
        "La cuenta bancaria propia no está activa: no se le carga extracto.");
}
