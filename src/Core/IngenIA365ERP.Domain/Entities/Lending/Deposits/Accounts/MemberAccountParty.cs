using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_MemberAccountParties</c> (feature 014, K7; data-model §3.2; FR-005, FR-033, FR-034, I-1): quién es quién en una cuenta —titular,
/// cotitulares, firmantes, representante legal y beneficiarios—. Estado con fechas, no versión: una fila por persona, papel y vigencia;
/// cerrar es escribir <see cref="ValidTo"/> con un comando auditado (<see cref="Terminar"/>). Toda parte es persona del maestro (Principio V).
/// Un titular vigente por cuenta, que es <c>MemberAccount.HolderPersonId</c>. No es hecho.
/// </summary>
public class MemberAccountParty : AuditableEntity
{
    /// <summary>Largo del motivo de fin (<c>nvarchar(300)</c>).</summary>
    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; set; }

    /// <summary>Navegación para nacer en el mismo guardado que la cuenta.</summary>
    public MemberAccount? MemberAccount { get; set; }

    public int PersonId { get; set; }

    public MemberAccountPartyRole Role { get; set; }

    /// <summary>
    /// Titular y cotitulares: proporción (Σ = 1 entre los vigentes; la usa el certificado tributario, FR-104). Beneficiarios: porcentaje (Σ = 1 por
    /// conjunto). Nula en firmantes y representantes.
    /// </summary>
    public decimal? Share { get; set; }

    /// <summary>Firmantes: límite por operación; nulo = sin límite.</summary>
    public decimal? SignatureLimit { get; set; }

    /// <summary><c>LegalRepresentative</c>: <c>Parent</c>, <c>Guardian</c>, <c>CourtOrder</c>, <c>LegalEntityRepresentative</c>.</summary>
    public string? RepresentationBasis { get; set; }

    /// <summary>Registro civil, decisión judicial o certificado de existencia; obligatorio con <c>LegalRepresentative</c>.</summary>
    public Guid? RepresentationSupportAttachmentPublicId { get; set; }

    public Guid? SignatureSpecimenAttachmentPublicId { get; set; }

    public Guid? PhotoAttachmentPublicId { get; set; }

    /// <summary>Conjunto de beneficiarios al que pertenece; cambiarlos cierra el vigente y abre el siguiente (FR-034).</summary>
    public int? BeneficiarySetNumber { get; set; }

    /// <summary>Constancia firmada por el titular (FR-034).</summary>
    public Guid? BeneficiaryRecordAttachmentPublicId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>Mayoría de edad, revocación, fallecimiento, cambio de beneficiarios.</summary>
    public string? EndReason { get; set; }

    /// <summary>Nulo = el proceso.</summary>
    public int? EndedByUserId { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>¿Rige el <paramref name="dia"/>? (desde incluido, hasta incluido).</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);

    /// <summary>Titular o cotitular: las partes que tienen proporción del saldo.</summary>
    public bool EsTitularOCotitular => Role is MemberAccountPartyRole.Holder or MemberAccountPartyRole.CoHolder;

    /// <summary>
    /// Una parte nueva (§3.2): el representante legal exige su fundamento y su soporte; el beneficiario, su conjunto; la proporción va sólo en
    /// titulares, cotitulares y beneficiarios, entre 0 y 1; el límite de firma sólo en firmantes.
    /// </summary>
    public static MemberAccountParty Nueva(MemberAccount cuenta, int personaId, MemberAccountPartyRole papel, DateOnly desde, decimal? proporcion = null,
        decimal? limiteDeFirma = null, string? fundamento = null, Guid? soporte = null, int? conjunto = null, Guid? constancia = null,
        bool soporteDiferido = false)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentOutOfRangeException.ThrowIfLessThan(personaId, 1);
        if (proporcion is { } p && (p <= 0m || p > 1m))
            throw new ArgumentOutOfRangeException(nameof(proporcion), p, "La proporción va entre 0 (excluido) y 1.");
        if (proporcion is not null && papel is not (MemberAccountPartyRole.Holder or MemberAccountPartyRole.CoHolder or MemberAccountPartyRole.Beneficiary))
            throw new ArgumentException("Sólo titulares, cotitulares y beneficiarios tienen proporción.", nameof(proporcion));
        if (limiteDeFirma is not null && papel != MemberAccountPartyRole.Signatory)
            throw new ArgumentException("Sólo los firmantes tienen límite de firma.", nameof(limiteDeFirma));
        // US9 (T155; data-model §7.1): en la migración el soporte se sube después (la cuenta queda con RequiresDataUpdate), así que sólo se difiere.
        if (papel == MemberAccountPartyRole.LegalRepresentative && (string.IsNullOrWhiteSpace(fundamento) || (soporte is null && !soporteDiferido)))
            throw new ArgumentException("El representante legal lleva su fundamento y su soporte (FR-033).", nameof(soporte));
        if (papel == MemberAccountPartyRole.Beneficiary && conjunto is null)
            throw new ArgumentException("Un beneficiario pertenece a un conjunto (FR-034).", nameof(conjunto));
        if (cuenta.Nature == LineNature.Contribution && papel is not (MemberAccountPartyRole.Holder or MemberAccountPartyRole.Beneficiary))
            throw new ArgumentException("En una cuenta de aportes sólo hay titular y beneficiarios (§3.2).", nameof(papel));
        return new MemberAccountParty
        {
            MemberAccount = cuenta.Id == 0 ? cuenta : null,
            MemberAccountId = cuenta.Id,
            PersonId = personaId,
            Role = papel,
            Share = proporcion,
            SignatureLimit = limiteDeFirma,
            RepresentationBasis = string.IsNullOrWhiteSpace(fundamento) ? null : fundamento.Trim(),
            RepresentationSupportAttachmentPublicId = soporte,
            BeneficiarySetNumber = papel == MemberAccountPartyRole.Beneficiary ? conjunto : null,
            BeneficiaryRecordAttachmentPublicId = constancia,
            ValidFrom = desde,
        };
    }

    /// <summary>
    /// Cierra la vigencia de la parte el <paramref name="hasta"/> con su motivo (mayoría de edad, revocación, fallecimiento, cambio). Una parte que se
    /// reemplaza el mismo día en que empezó cierra la víspera de ese día: no rigió nunca, pero queda (nunca se borra).
    /// </summary>
    public void Terminar(DateOnly hasta, string motivo, int? usuario)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (ValidTo is not null)
            throw new InvalidOperationException($"La parte ya terminó el {ValidTo:yyyy-MM-dd}.");
        if (hasta < ValidFrom.AddDays(-1))
            throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "La parte no termina antes de empezar.");
        ValidTo = hasta;
        var texto = motivo.Trim();
        EndReason = texto.Length <= LargoDelMotivo ? texto : texto[..LargoDelMotivo];
        EndedByUserId = usuario;
    }

    /// <summary>¿Suman 1 las proporciones de titulares y cotitulares vigentes? (<c>Lending.Savings.SharesDoNotAddUp</c>, §3.2).</summary>
    public static bool ProporcionesCuadran(IEnumerable<MemberAccountParty> partes, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(partes);
        var vigentes = partes.Where(p => p.EsTitularOCotitular && p.VigenteEn(dia)).ToList();
        if (vigentes.Count <= 1 && vigentes.All(p => p.Share is null)) return true;
        return vigentes.Sum(p => p.Share ?? 0m) == 1m;
    }
}
