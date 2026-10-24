using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>Los errores de la base de la Tesorería nueva (feature 015, T0019; data-model §21). Cada historia agrega los suyos. (nuevo)</summary>
public static partial class ErroresDeTesoreria
{
    public const string CodigoNoActivado = "Treasury.NotActivated";

    public static Error NoActivado() => new(CodigoNoActivado,
        "Tesorería todavía no está activada en la cooperativa: complete la puesta en marcha y actívela antes de registrar operaciones.");
}
