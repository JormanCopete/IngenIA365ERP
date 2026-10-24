using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US19 (T0227; DT §2.7): los errores de la remesa de la bóveda. (nuevo)
public static partial class ErroresDeRecaudo
{
    public static Error RemesaNoCoincide(string detalle) => new ErrorConDatos("Receipts.Vault.RemittanceMismatch",
        $"La remesa que recibe la bóveda no coincide: {detalle}", new { detail = detalle });
}
