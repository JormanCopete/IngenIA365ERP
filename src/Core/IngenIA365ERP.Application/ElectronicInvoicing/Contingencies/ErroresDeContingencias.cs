using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;

/// <summary>Los errores de las contingencias 03 y 04 (feature 012, I4, T726; api.md §24.6). (nuevo)</summary>
public static class ErroresDeContingencias
{
    public const string Dian04OnlyByChannelCode = "ElectronicInvoicing.Contingency.Dian04OnlyByChannel";
    public const string AlreadyOpenCode = "ElectronicInvoicing.Contingency.AlreadyOpen";

    /// <summary>El evento no existe. (nuevo)</summary>
    public const string NotFoundCode = "ElectronicInvoicing.Contingency.NotFound";

    /// <summary>El evento ya se cerró. (nuevo)</summary>
    public const string AlreadyClosedCode = "ElectronicInvoicing.Contingency.AlreadyClosed";

    /// <summary>El inicio o el fin declarados no caben: en el futuro, o el fin antes del inicio. (nuevo)</summary>
    public const string InvalidDatesCode = "ElectronicInvoicing.Contingency.InvalidDates";

    public static Error Dian04OnlyByChannel() =>
        new(Dian04OnlyByChannelCode,
            "La contingencia de la DIAN la declara sólo el canal, cuando entrega el documento firmado sin poder validarlo. " +
            "Una persona declara la contingencia de facturación (tipo 03).");

    public static Error AlreadyOpen(Guid abierta) =>
        new ErrorConDatos(AlreadyOpenCode, "Ya hay una contingencia de facturación abierta en este canal: ciérrela antes de abrir otra.",
            new { contingencyPublicId = abierta });

    public static Error NotFound() => new(NotFoundCode, "La contingencia no existe.");

    public static Error AlreadyClosed() => new(AlreadyClosedCode, "La contingencia ya está cerrada.");

    public static Error InvalidDates(string motivo) => new(InvalidDatesCode, motivo);
}
