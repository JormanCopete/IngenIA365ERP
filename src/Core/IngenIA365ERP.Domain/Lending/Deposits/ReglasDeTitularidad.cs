using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Una parte que se pide para una cuenta (cotitular, firmante o representante); el titular no va aquí: es el de la cuenta. (nuevo)</summary>
public sealed record ParteSolicitada(
    int PersonaId,
    MemberAccountPartyRole Papel,
    decimal? Proporcion = null,
    decimal? LimiteDeFirma = null,
    string? Fundamento = null,
    Guid? Soporte = null,
    Guid? Firma = null,
    Guid? Foto = null);

/// <summary>Un beneficiario con su porcentaje (0–100, como lo digita el titular; se guarda como fracción). (nuevo)</summary>
public sealed record BeneficiarioSolicitado(int PersonaId, decimal Porcentaje);

/// <summary>
/// Lo que la titularidad necesita saber de la línea y del titular: la naturaleza, lo que la versión vigente admite (cotitulares, menores, personas
/// jurídicas) y si el titular es menor o persona jurídica. (nuevo)
/// </summary>
public sealed record ContextoDeTitularidad(
    LineNature Naturaleza,
    int TitularId,
    bool AdmiteCotitulares,
    bool AdmiteMenores,
    bool AdmitePersonasJuridicas,
    bool TitularMenor,
    bool TitularJuridico,
    bool SoporteDiferido = false);

/// <summary>Una parte ya resuelta, lista para nacer como fila de <c>LND_MemberAccountParties</c> (la proporción como fracción). (nuevo)</summary>
public sealed record ParteResuelta(
    int PersonaId,
    MemberAccountPartyRole Papel,
    decimal? Proporcion,
    decimal? LimiteDeFirma = null,
    string? Fundamento = null,
    Guid? Soporte = null,
    Guid? Firma = null,
    Guid? Foto = null);

/// <summary>
/// Las reglas de titularidad de una cuenta de captación o de aportes (feature 014, US2, T073–T074; data-model §3.2; FR-033, FR-034): puras, sin
/// consultar nada. Reciben las partes pedidas y devuelven las filas resueltas —el titular con la proporción que le queda— o el primer problema:
/// <list type="bullet">
/// <item><b>Aportes</b>: sólo titular y beneficiarios (<see cref="CodigoSoloTitularYBeneficiarios"/>).</item>
/// <item><b>Cotitulares</b> sólo si la línea los admite (<see cref="CodigoCotitularesNoAdmitidos"/>); con proporción las de todos o las de ninguno —sin
/// proporciones se reparte por igual y el titular absorbe el redondeo—, y lo de los cotitulares deja algo al titular
/// (<see cref="CodigoProporcionesNoCuadran"/>).</item>
/// <item><b>Menores</b> sólo si la línea los admite (<see cref="CodigoMenoresNoAdmitidos"/>) y con exactamente un representante legal con su
/// fundamento y su soporte (<see cref="CodigoRepresentanteRequerido"/>, <c>data.supportRequired</c>); una persona jurídica, igual
/// (<see cref="CodigoPersonasJuridicasNoAdmitidas"/>).</item>
/// <item><b>Manejo conjunto</b> exige al menos dos que firmen (<see cref="CodigoConjuntoSinFirmantes"/>).</item>
/// <item><b>Beneficiarios</b> que suman exactamente 100 % (<see cref="CodigoBeneficiariosNoSuman100"/>, <c>data.sum</c>), cada uno con algo y ninguno el
/// titular.</item>
/// <item>Una persona tiene un solo papel y el titular no repite como otra parte (<see cref="CodigoParteRepetida"/>).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class ReglasDeTitularidad
{
    public const string CodigoCotitularesNoAdmitidos = "Lending.Savings.CoHoldersNotAllowed";
    public const string CodigoMenoresNoAdmitidos = "Lending.Savings.MinorsNotAllowed";
    public const string CodigoPersonasJuridicasNoAdmitidas = "Lending.Savings.LegalEntitiesNotAllowed";
    public const string CodigoRepresentanteRequerido = "Lending.Savings.RepresentativeRequired";
    public const string CodigoBeneficiariosNoSuman100 = "Lending.Savings.BeneficiariesMustSum100";
    public const string CodigoProporcionesNoCuadran = "Lending.Savings.SharesDoNotAddUp";
    public const string CodigoSoloTitularYBeneficiarios = "Lending.Contributions.OnlyHolderAndBeneficiaries";
    public const string CodigoConjuntoSinFirmantes = "Lending.Savings.JointRequiresTwoSigners";
    public const string CodigoParteRepetida = "Lending.Savings.PartyRepeated";
    public const string CodigoParteInvalida = "Lending.Savings.PartyInvalid";

    /// <summary>Los fundamentos de la representación (data-model §3.2).</summary>
    public const string FundamentoPadres = "Parent";
    public const string FundamentoTutor = "Guardian";
    public const string FundamentoJudicial = "CourtOrder";
    public const string FundamentoPersonaJuridica = "LegalEntityRepresentative";

    public static IReadOnlyList<string> Fundamentos { get; } = [FundamentoPadres, FundamentoTutor, FundamentoJudicial, FundamentoPersonaJuridica];

    /// <summary>Decimales de una proporción (<c>Fraccion</c>, decimal(9,6)).</summary>
    public const int DecimalesDeProporcion = 6;

    /// <summary>¿Es menor a <paramref name="fecha"/> quien nació el <paramref name="nacimiento"/>, con la mayoría de edad en <paramref name="mayoria"/> años?</summary>
    public static bool EsMenor(DateOnly? nacimiento, DateOnly fecha, int mayoria) =>
        nacimiento is { } n && CumpleMayoriaEl(n, mayoria) > fecha;

    /// <summary>El día en que cumple la mayoría de edad (el 29 de febrero se cumple el 28 en un año no bisiesto, como <c>AddYears</c>).</summary>
    public static DateOnly CumpleMayoriaEl(DateOnly nacimiento, int mayoria)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mayoria, 1);
        return nacimiento.AddYears(mayoria);
    }

    /// <summary>
    /// Resuelve las partes de una cuenta: el titular (con la proporción que le queda si hay cotitulares) y las partes pedidas, o el primer problema.
    /// </summary>
    public static (IReadOnlyList<ParteResuelta> Partes, ProblemaDeCartera? Problema) Resolver(ContextoDeTitularidad c, HandlingMode manejo,
        IReadOnlyList<ParteSolicitada> partes)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(partes);

        if (partes.Any(p => p.Papel is MemberAccountPartyRole.Holder or MemberAccountPartyRole.Beneficiary || !Enum.IsDefined(p.Papel)))
            return Falla(CodigoParteInvalida, "El titular es el de la cuenta y los beneficiarios van aparte: las partes son cotitulares, firmantes o representantes.",
                "parties", null);
        if (partes.Any(p => p.PersonaId == c.TitularId))
            return Falla(CodigoParteRepetida, "El titular no puede ser además cotitular, firmante o representante de su propia cuenta.", "parties", c.TitularId);
        if (partes.GroupBy(p => p.PersonaId).Any(g => g.Count() > 1))
            return Falla(CodigoParteRepetida, "Una persona aparece dos veces entre las partes: cada una tiene un solo papel en la cuenta.", "parties", null);
        var cotitulares = partes.Where(p => p.Papel == MemberAccountPartyRole.CoHolder).ToList();

        if (c.Naturaleza == LineNature.Contribution)
        {
            if (partes.Count > 0)
                return Falla(CodigoSoloTitularYBeneficiarios,
                    "Una cuenta de aportes sólo tiene titular y beneficiarios: los aportes son de cada asociado y no admiten cotitulares, firmantes ni representantes.",
                    "parties", null);
            if (manejo != HandlingMode.Indistinct)
                return Falla(CodigoSoloTitularYBeneficiarios, "Una cuenta de aportes la maneja sólo su titular: el manejo es indistinto.", "handlingMode",
                    manejo.ToString());
            return ([new ParteResuelta(c.TitularId, MemberAccountPartyRole.Holder, null)], null);
        }

        if (cotitulares.Count > 0 && !c.AdmiteCotitulares)
            return Falla(CodigoCotitularesNoAdmitidos, "La línea no admite cotitulares: la cuenta tiene un solo titular.", "parties", cotitulares.Count);

        foreach (var p in partes)
        {
            if (p.Proporcion is not null && p.Papel != MemberAccountPartyRole.CoHolder)
                return Falla(CodigoParteInvalida, "Sólo los cotitulares tienen proporción del saldo.", "share", p.PersonaId);
            if (p.LimiteDeFirma is not null && p.Papel != MemberAccountPartyRole.Signatory)
                return Falla(CodigoParteInvalida, "Sólo los firmantes tienen límite de firma.", "signatureLimit", p.PersonaId);
            if (p.LimiteDeFirma is { } limite && (limite < 0m || decimal.Round(limite, 2) != limite))
                return Falla(CodigoParteInvalida, "El límite de firma es un valor en pesos, positivo y con a lo sumo dos decimales.", "signatureLimit", limite);
            if (p.Papel == MemberAccountPartyRole.LegalRepresentative && p.Fundamento is { } f && !Fundamentos.Contains(f, StringComparer.Ordinal))
                return Falla(CodigoParteInvalida, $"El fundamento de la representación es uno de: {string.Join(", ", Fundamentos)}.", "representationBasis", f);
        }

        // Menores y personas jurídicas: exactamente un representante legal con fundamento y soporte.
        var representantes = partes.Where(p => p.Papel == MemberAccountPartyRole.LegalRepresentative).ToList();
        if (c.TitularMenor && !c.AdmiteMenores)
            return Falla(CodigoMenoresNoAdmitidos, "La línea no admite cuentas de menores de edad.", "holderPersonPublicId", null);
        if (c.TitularJuridico && !c.AdmitePersonasJuridicas)
            return Falla(CodigoPersonasJuridicasNoAdmitidas, "La línea no admite cuentas de personas jurídicas.", "holderPersonPublicId", null);
        if (c.TitularMenor || c.TitularJuridico)
        {
            var quien = c.TitularMenor ? "El titular es menor de edad" : "El titular es una persona jurídica";
            // US9 (T153): en la migración el soporte del representante se sube después (SoporteDiferido); el fundamento se exige igual.
            if (representantes.Count != 1 || (representantes[0].Soporte is null && !c.SoporteDiferido) || string.IsNullOrWhiteSpace(representantes[0].Fundamento))
                return (Array.Empty<ParteResuelta>(), new ProblemaDeCartera(CodigoRepresentanteRequerido,
                    $"{quien}: la cuenta la maneja exactamente un representante legal, con el fundamento de la representación y su soporte "
                    + (c.TitularMenor ? "(registro civil o decisión judicial) " : "(certificado de existencia y representación) ") + "como adjunto.",
                    new Dictionary<string, object?>
                    {
                        ["supportRequired"] = true,
                        ["representatives"] = representantes.Count,
                        ["field"] = "parties",
                    }));
        }

        // Manejo conjunto: al menos dos firman (titular o su representante, cotitulares y firmantes).
        var firman = 1 + partes.Count(p => p.Papel is MemberAccountPartyRole.CoHolder or MemberAccountPartyRole.Signatory);
        if (manejo == HandlingMode.Joint && firman < 2)
            return Falla(CodigoConjuntoSinFirmantes, "Con manejo conjunto firman al menos dos: agregue un cotitular o un firmante, o elija manejo indistinto.",
                "handlingMode", manejo.ToString());

        // Proporciones: todas o ninguna; sin ninguna, por igual y el titular absorbe el redondeo.
        decimal? delTitular = null;
        var proporciones = new Dictionary<int, decimal>();
        if (cotitulares.Count > 0)
        {
            var conProporcion = cotitulares.Count(p => p.Proporcion is not null);
            if (conProporcion != 0 && conProporcion != cotitulares.Count)
                return Falla(CodigoProporcionesNoCuadran, "Indique la proporción de todos los cotitulares o la de ninguno (entonces se reparte por igual).", "share",
                    null);
            if (conProporcion == 0)
            {
                var parte = decimal.Round(1m / (cotitulares.Count + 1), DecimalesDeProporcion, MidpointRounding.ToZero);
                foreach (var p in cotitulares) proporciones[p.PersonaId] = parte;
            }
            else
            {
                foreach (var p in cotitulares)
                {
                    var v = p.Proporcion!.Value;
                    if (v <= 0m || v >= 1m || decimal.Round(v, DecimalesDeProporcion) != v)
                        return Falla(CodigoProporcionesNoCuadran, "La proporción de un cotitular es una fracción mayor que 0 y menor que 1, con a lo sumo seis decimales.",
                            "share", v);
                    proporciones[p.PersonaId] = v;
                }
            }
            var resto = 1m - proporciones.Values.Sum();
            if (resto <= 0m)
                return (Array.Empty<ParteResuelta>(), new ProblemaDeCartera(CodigoProporcionesNoCuadran,
                    "Las proporciones de los cotitulares no dejan nada al titular: titular y cotitulares suman 100 %.",
                    new Dictionary<string, object?> { ["coHoldersSum"] = proporciones.Values.Sum(), ["field"] = "share" }));
            delTitular = resto;
        }

        var resueltas = new List<ParteResuelta> { new(c.TitularId, MemberAccountPartyRole.Holder, delTitular) };
        resueltas.AddRange(partes.Select(p => new ParteResuelta(p.PersonaId, p.Papel,
            p.Papel == MemberAccountPartyRole.CoHolder ? proporciones[p.PersonaId] : null, p.LimiteDeFirma,
            string.IsNullOrWhiteSpace(p.Fundamento) ? null : p.Fundamento.Trim(), p.Soporte, p.Firma, p.Foto)));
        return (resueltas, null);
    }

    /// <summary>
    /// Los beneficiarios como fracciones (porcentaje ÷ 100), o el problema: cada uno con más de 0, ninguno el titular ni repetido, y la suma
    /// exactamente 100 % (FR-034). Sin beneficiarios no hay problema.
    /// </summary>
    public static (IReadOnlyList<(int PersonaId, decimal Fraccion)> Beneficiarios, ProblemaDeCartera? Problema) ResolverBeneficiarios(int titularId,
        IReadOnlyList<BeneficiarioSolicitado> beneficiarios)
    {
        ArgumentNullException.ThrowIfNull(beneficiarios);
        if (beneficiarios.Count == 0) return ([], null);
        if (beneficiarios.Any(b => b.PersonaId == titularId))
            return ([], ProblemaDeCartera.Con(CodigoParteRepetida, "El titular no puede ser beneficiario de su propia cuenta.", "beneficiaries", titularId));
        if (beneficiarios.GroupBy(b => b.PersonaId).Any(g => g.Count() > 1))
            return ([], ProblemaDeCartera.Con(CodigoParteRepetida, "Un beneficiario aparece dos veces: sume sus porcentajes en una sola fila.", "beneficiaries", null));
        if (beneficiarios.Any(b => b.Porcentaje <= 0m || decimal.Round(b.Porcentaje, 4) != b.Porcentaje))
            return ([], ProblemaDeCartera.Con(CodigoBeneficiariosNoSuman100,
                "Cada beneficiario tiene un porcentaje mayor que 0, con a lo sumo cuatro decimales.", "beneficiaries", null));
        var suma = beneficiarios.Sum(b => b.Porcentaje);
        if (suma != 100m)
            return ([], new ProblemaDeCartera(CodigoBeneficiariosNoSuman100,
                $"Los porcentajes de los beneficiarios suman {suma:0.####} %: deben sumar exactamente 100 %.",
                new Dictionary<string, object?> { ["sum"] = suma, ["field"] = "beneficiaries" }));
        return (beneficiarios.Select(b => (b.PersonaId, b.Porcentaje / 100m)).ToList(), null);
    }

    private static (IReadOnlyList<ParteResuelta>, ProblemaDeCartera?) Falla(string codigo, string mensaje, string campo, object? valor) =>
        (Array.Empty<ParteResuelta>(), new ProblemaDeCartera(codigo, mensaje, new Dictionary<string, object?> { ["field"] = campo, ["value"] = valor }));
}
