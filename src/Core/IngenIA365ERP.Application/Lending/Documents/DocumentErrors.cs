using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>Una obligación con saldo que niega el paz y salvo (<c>data.obligations[]</c>). (nuevo)</summary>
public sealed record ObligacionConSaldoDto(Guid LoanPublicId, string Number, decimal Balance);

/// <summary>Una venta de la persona con su mensaje en reproceso que niega el paz y salvo general (<c>data.pendingSales[]</c>, US13-4). (nuevo)</summary>
public sealed record VentaEnReprocesoDto(string? Invoice, Guid MessagePublicId);

/// <summary>
/// Los errores de los documentos emitidos de Cartera (feature 013, US13, T0788; data-model §10.1, api.md §15; C111). <c>Lending.Document.NotFound</c> es
/// 404 —también fuera del alcance y para un código de otra cooperativa—; los demás, 422 con <c>data</c> para corregir. Los mensajes van en español
/// claro y dicen qué hacer (FR-013); ninguno muestra un código. (nuevo)
/// </summary>
public static class DocumentErrors
{
    public const string CodigoPazYSalvoNegado = "Lending.Document.ClearanceDenied";
    public const string CodigoAnuladoNoSeReemite = "Lending.Document.VoidedCannotReissue";
    public const string CodigoClaseNoDisponible = "Lending.Document.KindNotAvailable";
    public const string CodigoSinFirmante = "Lending.Document.SignerMissing";
    public const string CodigoInexistente = "Lending.Document.NotFound";
    public const string CodigoYaAnulado = "Lending.Document.AlreadyVoided";
    public const string CodigoTitularNoObligado = "Lending.Document.HolderNotObligated";
    public const string CodigoNadaQueCertificar = "Lending.Document.NothingToCertify";
    public const string CodigoPeriodoInvalido = "Lending.Document.PeriodInvalid";
    public const string CodigoAñoNoTerminado = "Lending.Document.TaxYearNotClosed";

    /// <summary>El período más largo de un extracto (T0971): cinco años.</summary>
    public const int AñosMaximosDelExtracto = 5;

    /// <summary>La entrega de Cartera en la que llegan el extracto y el certificado tributario (C88).</summary>
    public const string EntregaDeK5 = "K5";

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// El paz y salvo no se emite: con saldo, las obligaciones y su saldo; con una venta en reproceso, la factura y el mensaje (FR-131, US13-1, US13-4).
    /// </summary>
    public static Error PazYSalvoNegado(IReadOnlyList<ObligacionConSaldoDto> obligaciones, IReadOnlyList<VentaEnReprocesoDto> ventas)
    {
        var motivos = new List<string>();
        if (obligaciones.Count > 0)
            motivos.Add(obligaciones.Count == 1
                ? $"el crédito {obligaciones[0].Number} tiene saldo de {Pesos(obligaciones[0].Balance)}"
                : $"los créditos {string.Join(", ", obligaciones.Select(o => o.Number))} tienen saldo");
        if (ventas.Count > 0)
            motivos.Add(ventas.Count == 1
                ? $"la venta a crédito de la factura {ventas[0].Invoice ?? "sin número"} está pendiente de reproceso en Cartera"
                : $"las ventas a crédito de las facturas {string.Join(", ", ventas.Select(v => v.Invoice ?? "sin número"))} están pendientes de reproceso en Cartera");
        return new ErrorConDatos(CodigoPazYSalvoNegado, $"No se puede emitir el paz y salvo: {string.Join("; ", motivos)}.",
            new
            {
                obligations = obligaciones.Select(o => new { loanPublicId = o.LoanPublicId, number = o.Number, balance = o.Balance }).ToArray(),
                pendingSales = ventas.Select(v => new { invoice = v.Invoice, messagePublicId = v.MessagePublicId }).ToArray(),
            });
    }

    public static Error AnuladoNoSeReemite(string numero) => new ErrorConDatos(CodigoAnuladoNoSeReemite,
        $"El documento {numero} fue anulado: emita uno nuevo.", new { number = numero });

    public static Error ClaseNoDisponible(IssuedDocumentKind clase) => new ErrorConDatos(CodigoClaseNoDisponible,
        "Este documento estará disponible en una entrega posterior de Cartera.", new { kind = clase.ToString(), availableFrom = EntregaDeK5 });

    public static Error SinFirmante() => new(CodigoSinFirmante,
        "Falta configurar el firmante de los documentos (nombre y cargo) en los parámetros de Cartera.");

    public static Error Inexistente() => new(CodigoInexistente, "El documento no existe.");

    /// <summary>La verificación de un código inexistente o de otra cooperativa (D21): el mismo 404, con su propio mensaje.</summary>
    public static Error CodigoDesconocido() => new(CodigoInexistente, "El código no corresponde a un documento de esta cooperativa.");

    public static Error YaAnulado(string numero, DateTime anuladoEn) => new ErrorConDatos(CodigoYaAnulado,
        $"El documento {numero} ya está anulado: una anulación no se repite.", new { number = numero, voidedAt = anuladoEn });

    public static Error TitularNoObligado(string persona, string credito) => new ErrorConDatos(CodigoTitularNoObligado,
        $"{persona} no es deudora ni codeudora del crédito {credito}: el documento se emite a nombre de quien está obligado.",
        new { loanNumber = credito });

    public static Error NadaQueCertificar(string motivo) => new ErrorConDatos(CodigoNadaQueCertificar,
        $"No hay nada que certificar: {motivo}.", new { reason = motivo });

    /// <summary>
    /// K5 (T0971): el extracto sin período, con la fecha inicial después de la final, que termina después de hoy o más largo que
    /// <see cref="AñosMaximosDelExtracto"/> años. (nuevo)
    /// </summary>
    public static Error PeriodoInvalido(string motivo, DateOnly? desde, DateOnly? hasta) => new ErrorConDatos(CodigoPeriodoInvalido,
        $"El período del extracto no es válido: {motivo}.", new { periodFrom = desde, periodTo = hasta, maxYears = AñosMaximosDelExtracto });

    /// <summary>K5 (T0971): el certificado tributario de un año que no ha terminado (el en curso o uno futuro). (nuevo)</summary>
    public static Error AñoNoTerminado(int año) => new ErrorConDatos(CodigoAñoNoTerminado,
        $"El certificado tributario de {año} se emite cuando el año termine: desde el 1 de enero de {año + 1}.",
        new { year = año, availableFrom = new DateOnly(año + 1, 1, 1) });

    /// <summary>El nombre de la clase como lo lee la persona.</summary>
    public static string NombreDeLaClase(IssuedDocumentKind clase) => clase switch
    {
        IssuedDocumentKind.PaymentPlan => "plan de pagos",
        IssuedDocumentKind.LoanClearance => "paz y salvo",
        IssuedDocumentKind.GeneralClearance => "paz y salvo general",
        IssuedDocumentKind.BalanceCertificate => "certificado de saldo",
        IssuedDocumentKind.Statement => "extracto",
        IssuedDocumentKind.TaxCertificate => "certificado tributario",
        _ => clase.ToString(),
    };

    public static string Pesos(decimal valor) => valor.ToString("C0", Co);
}
