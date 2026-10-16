using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// Los prefijos del número de cada clase de documento emitido (feature 013, US13, T0779; data-model §27.1, adaptadores-y-archivos.md §8.7): <c>PP-</c>
/// plan de pagos, <c>PS-</c> paz y salvo de una obligación, <c>PSG-</c> paz y salvo general, <c>CS-</c> certificado de saldo y, para K5, <c>EX-</c>
/// extracto y <c>CT-</c> certificado tributario. <see cref="Common.NumeradorDeCartera"/> los usa al crear la fila que falta de
/// <c>LND_NumberSequences</c> (<c>Kind = IssuedDocument</c>, <c>ScopeKey</c> = la clase): cada clase lleva su propio consecutivo. Después rige el
/// prefijo de la fila. (nuevo)
/// </summary>
public static class PrefijosDeDocumentosDeCartera
{
    public const string PlanDePagos = "PP-";
    public const string PazYSalvo = "PS-";
    public const string PazYSalvoGeneral = "PSG-";
    public const string CertificadoDeSaldo = "CS-";
    public const string Extracto = "EX-";
    public const string CertificadoTributario = "CT-";

    /// <summary>Las clases que se emiten en K1 (C88); el extracto y el certificado tributario llegan con K5.</summary>
    public static IReadOnlySet<IssuedDocumentKind> DeK1 { get; } = new HashSet<IssuedDocumentKind>
    {
        IssuedDocumentKind.PaymentPlan, IssuedDocumentKind.LoanClearance, IssuedDocumentKind.GeneralClearance, IssuedDocumentKind.BalanceCertificate,
    };

    /// <summary>Las clases que llegan con K5 (T0969, C88): el extracto y el certificado tributario. (nuevo)</summary>
    public static IReadOnlySet<IssuedDocumentKind> DeK5 { get; } = new HashSet<IssuedDocumentKind>
    {
        IssuedDocumentKind.Statement, IssuedDocumentKind.TaxCertificate,
    };

    /// <summary>¿La clase se emite en la entrega <paramref name="entrega"/>? Las de K1 siempre; el extracto y el certificado tributario desde K5.</summary>
    public static bool Disponible(IssuedDocumentKind clase, EntregaDeCartera entrega) =>
        DeK1.Contains(clase) || (DeK5.Contains(clase) && entrega >= EntregaDeCartera.K5);

    /// <summary>El prefijo de la clase.</summary>
    public static string De(IssuedDocumentKind clase) => clase switch
    {
        IssuedDocumentKind.PaymentPlan => PlanDePagos,
        IssuedDocumentKind.LoanClearance => PazYSalvo,
        IssuedDocumentKind.GeneralClearance => PazYSalvoGeneral,
        IssuedDocumentKind.BalanceCertificate => CertificadoDeSaldo,
        IssuedDocumentKind.Statement => Extracto,
        IssuedDocumentKind.TaxCertificate => CertificadoTributario,
        _ => throw new ArgumentOutOfRangeException(nameof(clase), clase, "Clase de documento desconocida."),
    };

    /// <summary>El ámbito del consecutivo de la clase en <c>LND_NumberSequences</c>.</summary>
    public static string Ambito(IssuedDocumentKind clase) => clase.ToString();
}
