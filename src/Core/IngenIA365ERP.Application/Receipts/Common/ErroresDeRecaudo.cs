using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// Los errores de la base de Recaudo (feature 015, T0019, T0042; data-model §21): el mensaje dice qué hacer; las pantallas ramifican por el código,
/// nunca por el texto. Cada historia agrega los suyos en su propio archivo. (nuevo)
/// </summary>
public static class ErroresDeRecaudo
{
    public const string CodigoNoActivado = "Receipts.NotActivated";
    public const string CodigoCorteDespuesDelCicloDeCartera = "Receipts.Setup.CutoffAfterLendingCycle";

    public static Error NoActivado() => new(CodigoNoActivado,
        "Recaudo todavía no está activado en la cooperativa: complete la puesta en marcha y actívelo antes de registrar operaciones.");

    public static Error CorteDespuesDelCicloDeCartera(TimeOnly corte, TimeOnly inicioDelCiclo) => new ErrorConDatos(CodigoCorteDespuesDelCicloDeCartera,
        $"La hora de corte {corte:HH\\:mm} no cae antes del ciclo diario de Cartera, que empieza a las {inicioDelCiclo:HH\\:mm} del mismo día: elija una hora anterior.",
        new { cutoff = corte.ToString("HH:mm"), lendingCycleStart = inicioDelCiclo.ToString("HH:mm") });
}
