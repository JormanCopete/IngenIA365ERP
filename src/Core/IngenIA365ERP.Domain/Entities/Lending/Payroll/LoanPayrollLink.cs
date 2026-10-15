using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_LoanPayrollLinks</c> (feature 013, C.3; data-model §24.5; FR-061, FR-069, FR-109–FR-112, FR-116; C44): forma de pago y
/// vínculo deudor–pagaduría de un crédito, con vigencia. <b>Fuente única</b> de la forma de pago y la pagaduría: ni el crédito
/// ni sus condiciones las repiten. Un vínculo vigente por crédito. No es hecho: <see cref="ValidTo"/> se escribe una vez al
/// trasladar o pasar a la forma alterna. La columna <c>PayrollAgreementId</c> nace con K2 (<c>CarteraLibranza</c>), que también trae el
/// traslado (<see cref="Trasladar"/>) y el paso a la forma alterna (<see cref="PasarALaFormaAlterna"/>): el vigente se cierra y nace otro.
/// </summary>
public class LoanPayrollLink : AuditableEntity
{
    public int LoanId { get; set; }

    public RepaymentChannel RepaymentChannel { get; set; }

    /// <summary>Obligatoria en <c>OwnPayroll</c> y <c>PaymasterDeduction</c>; nula en las demás (<c>CK_LND_LoanPayrollLinks_Paymaster</c>).</summary>
    public int? PaymasterPersonId { get; set; }

    /// <summary>Nómina propia: el empleado deudor es el pagador (G7).</summary>
    public bool IsOwnPayroll { get; set; }

    /// <summary>K2 (<c>CarteraLibranza</c>): el convenio de la pagaduría; nulo en K1, en caja y en débito.</summary>
    public int? PayrollAgreementId { get; set; }

    /// <summary>Identificación del empleado ante la pagaduría (FR-109).</summary>
    public string? EmployeeIdAtPaymaster { get; set; }

    /// <summary>Fecha de la libranza para la prelación cronológica (FR-111); obligatoria con pagaduría; el traslado la conserva.</summary>
    public DateOnly? LibranzaDate { get; set; }

    public Guid? CertificationAttachmentPublicId { get; set; }

    public DateOnly? CertificationDate { get; set; }

    public decimal? CertifiedNetIncome { get; set; }

    /// <summary>Capacidad certificada por la pagaduría; contra ella se valida (FR-110).</summary>
    public decimal? CertifiedAvailableCapacity { get; set; }

    public LoanPayrollLinkOrigin Origin { get; set; }

    /// <summary>La operación que lo creó; nulo en el traslado y en el cambio a la forma alterna.</summary>
    public long? OperationId { get; set; }

    /// <summary>El vínculo anterior en un traslado o un cambio de forma de pago.</summary>
    public int? PreviousLinkId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string? EndReason { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0434) --

    /// <summary>
    /// El vínculo con que nace un crédito al desembolsar (C44; data-model §24.5): la forma de pago aprobada, <c>Origin = Origination</c> y la
    /// operación del primer tramo. La nómina propia y la libranza llevan pagaduría (<c>CK_LND_LoanPayrollLinks_Paymaster</c>); caja y débito no.
    /// </summary>
    public static LoanPayrollLink AlOriginar(int loanId, RepaymentChannel forma, int? pagaduriaId, long operacionId, DateOnly desde)
    {
        var conPagaduria = forma is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction;
        if (conPagaduria && pagaduriaId is null)
            throw new ArgumentException("La nómina propia y la libranza llevan pagaduría.", nameof(pagaduriaId));
        return new LoanPayrollLink
        {
            LoanId = loanId,
            RepaymentChannel = forma,
            PaymasterPersonId = conPagaduria ? pagaduriaId : null,
            IsOwnPayroll = forma == RepaymentChannel.OwnPayroll,
            Origin = LoanPayrollLinkOrigin.Origination,
            OperationId = operacionId,
            ValidFrom = desde,
        };
    }

    /// <summary>¿Rige el <paramref name="dia"/>?</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>
    /// Traslado de la libranza a otra pagaduría con convenio (K2, FR-116, US10-5; C44): cierra este vínculo la víspera de
    /// <paramref name="desde"/> y abre otro con <c>Origin = Transfer</c>, la <b>misma</b> <see cref="LibranzaDate"/> (prelación, FR-111) y
    /// <see cref="PreviousLinkId"/> a éste. No es una operación del libro (sin <c>OperationId</c>) ni una <c>LoanNovelty</c>.
    /// </summary>
    public LoanPayrollLink Trasladar(int pagaduriaId, int convenioId, string? empleadoEnLaPagaduria, DateOnly desde, string motivo,
        Guid? certificacion = null, DateOnly? fechaDeCertificacion = null, decimal? ingresoCertificado = null, decimal? capacidadCertificada = null)
    {
        if (RepaymentChannel != RepaymentChannel.PaymasterDeduction && RepaymentChannel != RepaymentChannel.OwnPayroll)
            throw new InvalidOperationException("Sólo una libranza se traslada a otra pagaduría.");
        Cerrar(desde, motivo);
        return new LoanPayrollLink
        {
            LoanId = LoanId,
            RepaymentChannel = RepaymentChannel.PaymasterDeduction,
            PaymasterPersonId = pagaduriaId,
            IsOwnPayroll = false,
            PayrollAgreementId = convenioId,
            EmployeeIdAtPaymaster = string.IsNullOrWhiteSpace(empleadoEnLaPagaduria) ? null : empleadoEnLaPagaduria.Trim(),
            LibranzaDate = LibranzaDate,
            CertificationAttachmentPublicId = certificacion,
            CertificationDate = fechaDeCertificacion,
            CertifiedNetIncome = ingresoCertificado,
            CertifiedAvailableCapacity = capacidadCertificada,
            Origin = LoanPayrollLinkOrigin.Transfer,
            PreviousLinkId = Id,
            ValidFrom = desde,
        };
    }

    /// <summary>
    /// Retiro sin nuevo empleador (FR-116): cierra este vínculo y abre otro con la forma de pago alterna del convenio (caja o débito, sin
    /// pagaduría) y <c>Origin = ChannelChange</c>. El aviso al deudor y la dimensión contable los emite quien lo llama.
    /// </summary>
    public LoanPayrollLink PasarALaFormaAlterna(RepaymentChannel alterna, DateOnly desde, string motivo)
    {
        if (alterna is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction)
            throw new ArgumentException("La forma alterna no lleva pagaduría: caja o débito.", nameof(alterna));
        Cerrar(desde, motivo);
        return new LoanPayrollLink
        {
            LoanId = LoanId,
            RepaymentChannel = alterna,
            Origin = LoanPayrollLinkOrigin.ChannelChange,
            PreviousLinkId = Id,
            ValidFrom = desde,
        };
    }

    /// <summary>¿El vínculo es de libranza (con pagaduría)? Pasar entre con y sin libranza cambia el sub-segmento contable (FR-116).</summary>
    public bool ConLibranza => RepaymentChannel is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction;

    private void Cerrar(DateOnly desde, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (ValidTo is not null) throw new InvalidOperationException("El vínculo ya está cerrado.");
        if (desde <= ValidFrom) throw new ArgumentOutOfRangeException(nameof(desde), desde, "El vínculo nuevo empieza después del vigente.");
        ValidTo = desde.AddDays(-1);
        EndReason = motivo.Trim();
    }
}
