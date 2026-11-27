using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Lo que se desembolsa (api.md §9.1 <c>DisbursementInput</c>; FR-045, FR-046, D12): la solicitud aprobada; para un tramo siguiente, el crédito
/// y el valor del tramo (sin crédito, nace con el primero; sin valor, el primer tramo es lo aprobado entero o lo que la línea admite); la fecha
/// valor (hoy si no viene); la forma; el beneficiario con su cuenta (propuesta desde la persona y corregible, I10); los créditos propios que se
/// recogen (sólo los que la solicitud aprobada autoriza) y la huella de la vista previa que se vio. (nuevo)
/// </summary>
public sealed record DisbursementInput
{
    public Guid ApplicationPublicId { get; init; }

    /// <summary>El crédito de un tramo siguiente; nulo en el primero.</summary>
    public Guid? LoanPublicId { get; init; }

    /// <summary>El valor del tramo; nulo = lo que queda por desembolsar de lo aprobado.</summary>
    public decimal? TrancheAmount { get; init; }

    /// <summary>Fecha valor; nula = hoy (fecha local de la cooperativa).</summary>
    public DateOnly? ValueDate { get; init; }

    public DisbursementForm Form { get; init; } = DisbursementForm.Transfer;

    /// <summary>Nulo = el deudor, con la cuenta registrada en su persona.</summary>
    public BeneficiaryInput? Beneficiary { get; init; }

    /// <summary>
    /// Créditos propios a recoger (FR-046), la lista de antes. 2026-11-27 (feature 028, T032; D9): <b>opcional</b>: las recogidas salen de la solicitud
    /// (<c>LND_CreditApplicationPayoffs</c>), todas las autorizadas; si viene, debe ser la misma —mismos productos y modos, y el valor de un parcial—: si no,
    /// <c>Lending.Payoff.MustMatchApplication</c>. Con <see cref="Payoffs"/> a la vez, <c>Request.BodyInvalid</c>.
    /// </summary>
    public IReadOnlyList<OwnLoanPayoffInput> OwnLoanPayoffs { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T032; api.md §3): la lista nueva (<see cref="PayoffInput"/>); opcional, con la misma regla que <see cref="OwnLoanPayoffs"/>. (nuevo)</summary>
    public IReadOnlyList<PayoffInput>? Payoffs { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, T032, T035; FR-015): quien desembolsa revisó las diferencias entre lo proyectado y lo de ahora y los productos que se omiten.
    /// Si la vista previa los tuvo y viene en falso, <c>Lending.Payoff.DifferencesNotAcknowledged</c>. (nuevo)
    /// </summary>
    public bool AcknowledgedPayoffDifferences { get; init; }

    /// <summary>Si la transferencia sale en un archivo de desembolsos de Cartera (C10); sólo en <c>Transfer</c>.</summary>
    public bool PaidByFile { get; init; }

    /// <summary>La huella de la vista previa que vio quien desembolsa; si viene y no coincide, <c>Lending.Disbursement.PreviewChanged</c>.</summary>
    public string? ExpectedPreviewSha256 { get; init; }
}

/// <summary>
/// El beneficiario del giro (api.md §9.1): una persona del maestro (el deudor o un tercero) y, en transferencia, su cuenta. <paramref name="AccountType"/>
/// es <c>Savings</c> o <c>Checking</c> (1 y 2 en la orden, como en Nómina). (nuevo)
/// </summary>
public sealed record BeneficiaryInput(Guid PersonPublicId, Guid? BankPublicId = null, string? AccountType = null, string? AccountNumber = null)
{
    public const string Ahorros = "Savings";
    public const string Corriente = "Checking";

    /// <summary>El tipo de cuenta como lo guarda la orden: 1 ahorros, 2 corriente; nulo si no viene o no se reconoce.</summary>
    public static byte? Tipo(string? texto) => texto?.Trim() switch
    {
        null or "" => null,
        Ahorros or "1" or "A" or "AH" => 1,
        Corriente or "2" or "C" or "CC" => 2,
        _ => null,
    };

    /// <summary>El texto del tipo de cuenta de una orden.</summary>
    public static string? Texto(byte? tipo) => tipo switch { 1 => Ahorros, 2 => Corriente, _ => null };
}
