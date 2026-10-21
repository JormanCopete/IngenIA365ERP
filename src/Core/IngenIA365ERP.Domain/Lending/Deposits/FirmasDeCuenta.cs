using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Una parte vigente de la cuenta que puede firmar: la persona, su papel y su límite de firma (nulo = sin límite). (nuevo)</summary>
public sealed record ParteFirmante(int PersonId, MemberAccountPartyRole Role, decimal? SignatureLimit);

/// <summary>
/// La revisión de las firmas de un retiro o un traslado (FR-051, US4-1): quiénes deben firmar, quiénes firmaron de los que cuentan, quiénes faltan y si
/// está completo; con el manejo de la cuenta para el mensaje. (nuevo)
/// </summary>
public sealed record FirmasRevisadas(HandlingMode Manejo, IReadOnlyList<int> Requeridas, IReadOnlyList<int> Presentadas, IReadOnlyList<int> Faltantes,
    bool Completas, string Explicacion);

/// <summary>
/// Quién firma un retiro o un traslado de una cuenta (feature 014, US4, T111; contrato-de-cuentas §3.9 paso 2; FR-033, FR-051). Puro: recibe el manejo,
/// el titular, si es menor o persona jurídica, las partes vigentes y quiénes firmaron (identidad verificada por el recaudador o por la pantalla).
/// <list type="bullet">
/// <item><b>Titular menor</b>: sólo firma su representante legal vigente (FR-033); el menor no firma.</item>
/// <item><b>Titular persona jurídica</b>: firma su representante legal o un firmante autorizado.</item>
/// <item><b>Firmante con límite</b>: no firma por encima de su límite (no cuenta).</item>
/// <item><b>Manejo indistinto</b>: basta uno de los que pueden firmar. <b>Conjunto</b>: firman todos los titulares —el titular (o, si es menor o jurídico,
/// uno de quienes firman por él) y cada cotitular—.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class FirmasDeCuenta
{
    public static FirmasRevisadas Revisar(HandlingMode manejo, int titularId, bool titularMenor, bool titularJuridico,
        IReadOnlyList<ParteFirmante> partes, IReadOnlyCollection<int> firmaron, decimal monto)
    {
        ArgumentNullException.ThrowIfNull(partes);
        ArgumentNullException.ThrowIfNull(firmaron);
        bool AlcanzaElLimite(ParteFirmante p) => p.SignatureLimit is not { } limite || monto <= limite;

        // Quienes firman por el titular: él mismo (adulto, persona natural) o su representante (y en una jurídica, sus firmantes autorizados).
        var porElTitular = titularMenor
            ? partes.Where(p => p.Role == MemberAccountPartyRole.LegalRepresentative).Select(p => p.PersonId).ToList()
            : titularJuridico
                ? partes.Where(p => p.Role == MemberAccountPartyRole.LegalRepresentative
                                    || (p.Role == MemberAccountPartyRole.Signatory && AlcanzaElLimite(p))).Select(p => p.PersonId).ToList()
                : [titularId];
        var cotitulares = partes.Where(p => p.Role == MemberAccountPartyRole.CoHolder && p.PersonId != titularId).Select(p => p.PersonId).Distinct().ToList();
        var firmantes = titularMenor
            ? []
            : partes.Where(p => (p.Role is MemberAccountPartyRole.Signatory or MemberAccountPartyRole.LegalRepresentative) && AlcanzaElLimite(p))
                .Select(p => p.PersonId).ToList();
        var habilitados = porElTitular.Concat(titularMenor ? [] : cotitulares).Concat(firmantes).Distinct().ToList();
        var presentadas = firmaron.Where(habilitados.Contains).Distinct().ToList();

        if (manejo == HandlingMode.Joint)
        {
            var faltan = new List<int>();
            if (!porElTitular.Any(presentadas.Contains)) faltan.AddRange(porElTitular);
            faltan.AddRange(cotitulares.Where(c => !presentadas.Contains(c)));
            var requeridas = porElTitular.Concat(cotitulares).Distinct().ToList();
            return new FirmasRevisadas(manejo, requeridas, presentadas, faltan.Distinct().ToList(), faltan.Count == 0 && requeridas.Count > 0,
                faltan.Count == 0
                    ? "Manejo conjunto: firmaron todos los titulares."
                    : "Manejo conjunto: deben firmar todos los titulares (o quien firma por un titular menor o jurídico).");
        }

        var completas = presentadas.Count > 0;
        return new FirmasRevisadas(manejo, habilitados, presentadas, completas ? [] : habilitados, completas,
            completas
                ? "Manejo indistinto: firmó una persona autorizada."
                : titularMenor
                    ? "El titular es menor de edad: firma su representante legal."
                    : "Debe firmar el titular, un cotitular o un firmante autorizado (dentro de su límite).");
    }
}
