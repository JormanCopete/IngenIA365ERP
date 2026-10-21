using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>La titularidad resuelta de una cuenta: las partes (con el titular), los beneficiarios como fracción y si el titular es menor. (nuevo)</summary>
public sealed record TitularidadResuelta(
    IReadOnlyList<ParteResuelta> Partes,
    IReadOnlyList<(int PersonaId, decimal Fraccion)> Beneficiarios,
    bool TitularMenor,
    int MayoriaDeEdad);

/// <summary>
/// La titularidad de una cuenta, común a la apertura y al cambio de partes (feature 014, US2, T073–T074; data-model §3.2; FR-006, FR-033, FR-034):
/// resuelve las personas por su <c>PublicId</c> (todas del maestro, Principio V; ninguna fallecida salvo un beneficiario), decide si el titular es
/// menor con <c>Captaciones.MayoriaDeEdadAnios</c> y su fecha de nacimiento, aplica <see cref="ReglasDeTitularidad"/> con lo que la versión vigente de la
/// línea admite (cotitulares, menores, personas jurídicas) y exige la autorización de tratamiento de datos del titular y de los cotitulares
/// (<c>Lending.Authorization.Missing</c>). El representante legal es mayor de edad. Sólo lee. (nuevo)
/// </summary>
public sealed class TitularidadDeCuentas(IApplicationDbContext db, IAutorizacionDeDatos autorizaciones, ReglasDeParametrosDeCartera parametros)
{
    public async Task<Result<TitularidadResuelta>> ResolverAsync(Person titular, LineNature naturaleza, DepositLineVersion? version, HandlingMode manejo,
        IReadOnlyList<MemberAccountPartyInput> partes, IReadOnlyList<MemberAccountBeneficiaryInput> beneficiarios, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(titular);
        var publicos = partes.Select(p => p.PersonPublicId).Concat(beneficiarios.Select(b => b.PersonPublicId)).Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => publicos.Contains(p.PublicId) && !p.IsDeleted).ToListAsync(ct);
        var porPublico = personas.ToDictionary(p => p.PublicId);
        var faltantes = publicos.Where(p => !porPublico.ContainsKey(p)).ToList();
        if (faltantes.Count > 0)
            return Falla(new ErrorConDatos(ErroresDeGobierno.PersonaInexistente().Code, ErroresDeGobierno.PersonaInexistente().Message,
                new { personPublicId = faltantes[0] }));
        if (partes.Select(p => porPublico[p.PersonPublicId]).FirstOrDefault(p => p.IsDeceased) is { } fallecida)
            return Falla(ErroresDeCuentas.TitularFallecido(fallecida.PublicId));

        var mayoria = await LectorDeCuentas.MayoriaDeEdadAsync(parametros, fecha, ct);
        var juridica = LectorDeCuentas.EsJuridica(titular);
        var menor = !juridica && ReglasDeTitularidad.EsMenor(titular.DateOfBirth, fecha, mayoria);
        foreach (var representante in partes.Where(p => p.Role == MemberAccountPartyRole.LegalRepresentative).Select(p => porPublico[p.PersonPublicId]))
            if (!LectorDeCuentas.EsJuridica(representante) && ReglasDeTitularidad.EsMenor(representante.DateOfBirth, fecha, mayoria))
                return Falla(new ErrorConDatos(ReglasDeTitularidad.CodigoParteInvalida,
                    $"{ReferenciasDeGobierno.Nombre(representante)} es menor de edad: no puede ser representante legal.",
                    new { personPublicId = representante.PublicId, field = "parties" }));

        var contexto = new ContextoDeTitularidad(naturaleza, titular.Id,
            AdmiteCotitulares: version?.AllowsCoHolders ?? false,
            AdmiteMenores: version?.AllowsMinors ?? true,
            AdmitePersonasJuridicas: version?.AllowsLegalEntities ?? true,
            TitularMenor: menor,
            TitularJuridico: juridica);
        var (resueltas, problema) = ReglasDeTitularidad.Resolver(contexto, manejo, partes.Select(p => new ParteSolicitada(porPublico[p.PersonPublicId].Id, p.Role,
            p.Share, p.SignatureLimit, p.RepresentationBasis, p.SupportAttachmentPublicId, p.SignatureSpecimenAttachmentPublicId, p.PhotoAttachmentPublicId)).ToList());
        if (problema is not null) return Falla(ErroresDeCuentas.De(problema));
        var (deBeneficiarios, problemaDeBeneficiarios) = ReglasDeTitularidad.ResolverBeneficiarios(titular.Id,
            beneficiarios.Select(b => new BeneficiarioSolicitado(porPublico[b.PersonPublicId].Id, b.Percent)).ToList());
        if (problemaDeBeneficiarios is not null) return Falla(ErroresDeCuentas.De(problemaDeBeneficiarios));

        // FR-006: la autorización de tratamiento del titular y de los cotitulares.
        var porId = personas.ToDictionary(p => p.Id);
        porId[titular.Id] = titular;
        foreach (var parte in resueltas.Where(p => p.Papel is MemberAccountPartyRole.Holder or MemberAccountPartyRole.CoHolder))
        {
            var persona = porId[parte.PersonaId];
            var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, ct);
            if (autorizacion is not { Autoriza: true })
                return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, ReferenciasDeGobierno.Nombre(persona), DataAuthorizationPurpose.GeneralTreatment));
        }
        return Result.Success(new TitularidadResuelta(resueltas, deBeneficiarios, menor, mayoria));
    }

    /// <summary>Las filas de las partes (sin los beneficiarios) desde <paramref name="desde"/>.</summary>
    public static IEnumerable<MemberAccountParty> Filas(MemberAccount cuenta, IEnumerable<ParteResuelta> partes, DateOnly desde) =>
        partes.Select(p => Con(MemberAccountParty.Nueva(cuenta, p.PersonaId, p.Papel, desde, p.Proporcion, p.LimiteDeFirma, p.Fundamento, p.Soporte), p));

    /// <summary>Las filas de los beneficiarios del conjunto <paramref name="conjunto"/> desde <paramref name="desde"/>.</summary>
    public static IEnumerable<MemberAccountParty> FilasDeBeneficiarios(MemberAccount cuenta, IEnumerable<(int PersonaId, decimal Fraccion)> beneficiarios,
        int conjunto, Guid? constancia, DateOnly desde) =>
        beneficiarios.Select(b => MemberAccountParty.Nueva(cuenta, b.PersonaId, MemberAccountPartyRole.Beneficiary, desde, b.Fraccion, conjunto: conjunto,
            constancia: constancia));

    private static MemberAccountParty Con(MemberAccountParty fila, ParteResuelta p)
    {
        fila.SignatureSpecimenAttachmentPublicId = p.Firma;
        fila.PhotoAttachmentPublicId = p.Foto;
        return fila;
    }

    private static Result<TitularidadResuelta> Falla(Error e) => Result.Failure<TitularidadResuelta>(e);
}
