using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// Los códigos de las ventas a crédito de Comercial en Cartera (feature 013, US6; data-model §10.1, C111; mensajes.md §11.2, §12; interfaces.md
/// §5.2). Los mensajes dicen qué hacer y nunca muestran el código (FR-013). (nuevo)
/// </summary>
public static class ErroresDeVentasACredito
{
    public const string CodigoPersonaInexistente = "Lending.Sale.PersonNotFound";
    public const string CodigoLineaInvalida = "Lending.Sale.CreditLineInvalid";

    /// <summary>La periodicidad o la unidad de plazo sin equivalencia (notas de coordinación PE, contradicción 15: rige data-model).</summary>
    public const string CodigoSinEquivalencia = "Lending.Sale.PeriodicityWithoutEquivalence";

    public const string CodigoNoIncorporada = "Lending.Sale.NotIncorporated";
    public const string CodigoVersionNoAceptada = "Integration.VersionNotAccepted";

    public const string CodigoReservaClaveReusada = "Lending.Reservation.KeyReused";
    public const string CodigoReservaInexistente = "Lending.Reservation.NotFound";
    public const string CodigoReservaUsada = "Lending.Reservation.AlreadyUsed";
    public const string CodigoReservaSinCupo = "Lending.Reservation.QuotaExceeded";
    public const string CodigoReservaBloqueada = "Lending.Reservation.Blocked";

    public const string CodigoNoMarcable = "Lending.InboundMessage.NotMarkable";
    public const string CodigoSinValidacionFallida = "Lending.Loan.NotValidationFailed";
    public const string CodigoReciboInexistente = "Lending.InboundMessage.NotFound";

    /// <summary>La venta está fuera de los rangos de la línea (monto, plazo o cuotas; verificación de FR-051).</summary>
    public const string CodigoFueraDeLaLinea = ErroresDeOriginacion.CodigoFueraDeRango;

    // ------------------------------------------------------------------------------------------------- conversión (C95) --

    public static Error PersonaInexistente() => new(CodigoPersonaInexistente,
        "El comprador de la venta no existe en el maestro de personas (o está eliminado). Regístrelo o corrija la venta y reprocese el mensaje.");

    public static Error LineaInvalida(string? codigo) => new ErrorConDatos(CodigoLineaInvalida,
        string.IsNullOrWhiteSpace(codigo)
            ? "La venta no trae línea de crédito y no hay una línea por defecto de origen comercial para el tipo de comprador. Configure la línea por defecto y reprocese el mensaje."
            : $"La línea {codigo} de la venta no existe, no está activa o no admite el origen comercial, y no hay una alterna. Corrija la línea y reprocese el mensaje.",
        new { creditLineCode = codigo });

    public static Error SinEquivalencia(string clase, string texto) => new ErrorConDatos(CodigoSinEquivalencia,
        $"La {clase} «{texto}» de la venta no tiene equivalencia en Cartera a su fecha. Regístrela en las equivalencias de Comercial y reprocese el mensaje.",
        new { kind = clase, commercialCode = texto });

    public static Error VersionNoAceptada(string tipo, int version) => new ErrorConDatos(CodigoVersionNoAceptada,
        $"Cartera no conoce la versión {version} de {tipo}. Actualice Cartera o pida a Comercial reenviar el mensaje en una versión admitida.",
        new { type = tipo, version });

    // ------------------------------------------------------------------------------------------- ventas hasta el corte --

    public static Error NoIncorporada(string motivo) => new(CodigoNoIncorporada, motivo);

    public const string MotivoSinFila =
        "Esta venta tiene fecha anterior o igual al corte de la migración y llegó después de activar Cartera sin estar en la conciliación: no se incorporó a Cartera. Regístrela como venta nueva o gestiónela por fuera de Cartera.";

    public const string MotivoAceptadaSinMigrar =
        "Esta venta no se incorporó a Cartera: se aceptó sin migrar en la activación (venta anterior al corte con saldo, aceptada con acta).";

    public const string MotivoAceptadaFueraDeCartera =
        "Esta venta no se incorporó a Cartera: se aceptó fuera de Cartera en la activación.";

    public const string MotivoSinObligacion =
        "Este ajuste es de una venta que no se incorporó a Cartera (aceptada sin migrar o fuera de Cartera): no hay obligación que ajustar.";

    // ---------------------------------------------------------------------------------------------------- reservas --

    public static Error ReservaClaveReusada() => new(CodigoReservaClaveReusada,
        "Esta venta ya reservó un número con otro valor. Libere la reserva y pida otra.");

    public static Error ReservaInexistente() => new(CodigoReservaInexistente, "No se encontró la reserva indicada.");

    public const string MensajeReservaUsada = "La reserva ya se usó: el número es de la obligación de la venta y no se libera.";

    public static string MensajeSinCupo(decimal disponible) =>
        $"El valor a crédito supera el cupo disponible del comprador ({ErroresDeOriginacion.Pesos(disponible)}).";

    // -------------------------------------------------------------------------------------------- recibos y excepción --

    public static Error NoMarcable() => new(CodigoNoMarcable,
        "Este mensaje no está rechazado para reproceso o ya se marcó no reprocesable: no hay nada que marcar.");

    public static Error SinValidacionFallida(string numero) => new ErrorConDatos(CodigoSinValidacionFallida,
        $"El crédito {numero} no tiene una validación fallida por confirmar.", new { loanNumber = numero });

    public static Error ReciboInexistente() => new(CodigoReciboInexistente, "El mensaje recibido no existe.");
}
