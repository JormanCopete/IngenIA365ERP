using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US1 (T0072, T0073; data-model §1.5, §21; DT §2.7): los errores de la configuración de Recaudo. Los de medios por punto son los de
// data-model §21; los demás nacen aquí (nuevo) y quedan registrados en DT §2.7. El duplicado de un código es el común Catalogo.CodigoDuplicado.
public static partial class ErroresDeRecaudo
{
    public const string CodigoMedioDeCredito = "Receipts.PaymentMeans.CreditClassNotAllowed";
    public const string CodigoTarjetaIncompleta = "Receipts.PaymentMeans.CardSetupIncomplete";
    public const string CodigoChequeApagado = "Receipts.PaymentMeans.CheckDisabled";
    public const string CodigoMedioFueraDeLaEntrega = "Receipts.PaymentMeans.NotInDelivery";
    public const string CodigoMedioInactivo = "Receipts.PaymentMeans.Inactive";
    public const string CodigoMedioYaOfrecido = "Receipts.PaymentMeans.AlreadyOffered";
    public const string CodigoCajeroSinPersona = "Receipts.Cashier.PersonRequired";
    public const string CodigoCajeroSeCruza = "Receipts.Cashier.Overlaps";
    public const string CodigoCajaPrincipalRepetida = "Receipts.CashBox.MainAlreadyExists";
    public const string CodigoTopesInvalidos = "Receipts.CashBox.CapsInvalid";
    public const string CodigoTipoDeClaseOcupada = "Receipts.DocumentType.KindTaken";
    public const string CodigoConceptoDelSistema = "Receipts.MiscConcept.SystemConcept";
    public const string CodigoTalonarioSeCruza = "Receipts.ContingencyBook.Overlaps";
    public const string CodigoSuplenteMismaPersona = "Receipts.Delegation.SamePerson";
    public const string CodigoBovedaRepetida = "Receipts.Vault.AlreadyExists";
    public const string CodigoBovedaSinCajaPrincipal = "Receipts.Vault.MainCashBoxRequired";
    public const string CodigoCustodiosInsuficientes = "Receipts.Vault.CustodiansRequired";

    public static Error MedioDeCredito() => new(CodigoMedioDeCredito,
        "Un medio de crédito (a asociado o comercial) no se ofrece en Recaudo: pagar una obligación con crédito no es un recaudo.");

    public static Error TarjetaIncompleta(string falta) => new ErrorConDatos(CodigoTarjetaIncompleta,
        $"El medio de tarjeta está incompleto: falta {falta}. Complételo en Medios de pago antes de ofrecerlo.", new { missing = falta });

    public static Error ChequeApagado() => new(CodigoChequeApagado,
        "La cooperativa no recibe cheques en caja (parámetro Caja.AdmiteCheque): actívelo antes de ofrecer un medio de cheque.");

    public static Error MedioFueraDeLaEntrega(string clase) => new ErrorConDatos(CodigoMedioFueraDeLaEntrega,
        "Este medio todavía no se ofrece en Recaudo: por ahora la caja recibe efectivo, transferencia, consignación y cheque.", new { @class = clase });

    public static Error MedioInactivo() => new(CodigoMedioInactivo, "El medio de pago está inactivo en el catálogo: actívelo antes de ofrecerlo.");

    public static Error MedioYaOfrecido() => new(CodigoMedioYaOfrecido, "El medio ya se ofrece en este punto.");

    public static Error CajeroSinPersona() => new(CodigoCajeroSinPersona,
        "El usuario no tiene una persona del maestro: vincúlelo a su persona en Seguridad antes de asignarlo como cajero.");

    public static Error CajeroSeCruza() => new(CodigoCajeroSeCruza, "El usuario ya es cajero de esta caja en esas fechas.");

    public static Error CajaPrincipalRepetida(string existente) => new ErrorConDatos(CodigoCajaPrincipalRepetida,
        $"La oficina ya tiene su caja principal ({existente}): quítele la marca antes de dársela a otra.", new { existing = existente });

    public static Error TopesInvalidos() => new(CodigoTopesInvalidos,
        "El tope de bloqueo del efectivo tiene que ser mayor o igual que el tope de aviso.");

    public static Error TipoDeClaseOcupada(string existente) => new ErrorConDatos(CodigoTipoDeClaseOcupada,
        $"Ya hay un tipo de documento activo de esa clase ({existente}): inactívelo antes de crear otro.", new { existing = existente });

    public static Error ConceptoDelSistema() => new(CodigoConceptoDelSistema,
        "Este concepto lo usa el sistema (giro devuelto, sobrantes, reintegros, rendimientos): no se inactiva.");

    public static Error TalonarioSeCruza(int desde, int hasta) => new ErrorConDatos(CodigoTalonarioSeCruza,
        $"El rango se cruza con el talonario {desde}–{hasta} del punto: use otro rango.", new { fromNumber = desde, toNumber = hasta });

    public static Error SuplenteMismaPersona() => new(CodigoSuplenteMismaPersona, "El suplente tiene que ser una persona distinta del supervisor.");

    public static Error BovedaRepetida() => new(CodigoBovedaRepetida, "La oficina ya tiene su bóveda.");

    public static Error BovedaSinCajaPrincipal() => new(CodigoBovedaSinCajaPrincipal,
        "Para que la caja principal haga de bóveda, indique una caja de la oficina marcada como principal.");

    public static Error CustodiosInsuficientes() => new(CodigoCustodiosInsuficientes,
        "La bóveda exige doble custodia (Caja.DobleCustodia): asigne al menos dos custodios distintos.");
}
