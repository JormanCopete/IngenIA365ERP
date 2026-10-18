using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;

// Feature 014, US2 (T074; api.md §4, PUT /{id}/parties y POST /{id}/data-update; data-model §3.2; FR-033, FR-034): cambiar las partes de una cuenta
// —cotitulares con su proporción, manejo, firmantes con su límite, representante legal con su soporte y beneficiarios que suman 100 %— y levantar la
// marca «actualizar datos» tras la mayoría de edad. Las partes son estado con fechas: lo que cambia se cierra la víspera y lo nuevo rige desde hoy;
// nada se borra. Cambiar beneficiarios cierra el conjunto vigente y abre el siguiente con la constancia del titular. (nuevo)

/// <summary>
/// Cambia las partes de la cuenta (<c>PUT /api/lending/accounts/{id}/parties</c>, <c>Lending.Accounts.Parties.Manage</c>, con clave). El cuerpo es el
/// conjunto completo: lo vigente que no viene igual se cierra y lo que viene distinto nace desde hoy. Las mismas reglas que la apertura
/// (<see cref="TitularidadDeCuentas"/>); un cambio de beneficiarios exige la constancia firmada por el titular (<c>Lending.Savings.BeneficiaryRecordRequired</c>).
/// (nuevo)
/// </summary>
public sealed record SetMemberAccountPartiesCommand(
    Guid AccountPublicId,
    HandlingMode HandlingMode,
    IReadOnlyList<MemberAccountPartyInput> Parties,
    IReadOnlyList<MemberAccountBeneficiaryInput> Beneficiaries,
    Guid? EvidenceAttachmentPublicId,
    string Reason) : IRequest<Result<MemberAccountPartiesDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetMemberAccountPartiesCommandValidator : ValidadorConMotivo<SetMemberAccountPartiesCommand>
{
    public SetMemberAccountPartiesCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.HandlingMode).IsInEnum();
        RuleFor(x => x.Parties).NotNull();
        RuleForEach(x => x.Parties).SetValidator(new ValidadorDeParte());
        RuleFor(x => x.Beneficiaries).NotNull();
        RuleForEach(x => x.Beneficiaries).ChildRules(b => b.RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elija el beneficiario."));
    }
}

public sealed class SetMemberAccountPartiesCommandHandler(
    IApplicationDbContext db,
    TitularidadDeCuentas titularidad,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SetMemberAccountPartiesCommand, Result<MemberAccountPartiesDto>>
{
    public const string CodigoConstanciaRequerida = "Lending.Savings.BeneficiaryRecordRequired";

    public async Task<Result<MemberAccountPartiesDto>> Handle(SetMemberAccountPartiesCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.AdmiteOperaciones() is { } cerrada) return Falla(ErroresDeCuentas.De(cerrada));
        if (!(await ActivacionesDeCaptacion.LeerAsync(db, ct)).RigeEn(cuenta.Nature, hoy)) return Falla(ErroresDeCuentas.SinActivar(cuenta.Nature));

        var titular = await db.People.AsNoTracking().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        var version = cuenta.Nature == LineNature.Deposit
            ? await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, hoy, null, ct)
            : null;
        var resuelta = await titularidad.ResolverAsync(titular, cuenta.Nature, version, request.HandlingMode, request.Parties, request.Beneficiaries, hoy, ct);
        if (resuelta.IsFailure) return Falla(resuelta.Error);
        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;
        var motivo = request.Reason.Trim();

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(resuelta.Value.Partes.Where(p => p.Papel is MemberAccountPartyRole.Holder or MemberAccountPartyRole.CoHolder)
                .Select(p => p.PersonaId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);

            var vigentes = await db.MemberAccountParties.Where(p => p.MemberAccountId == cuenta.Id && p.ValidTo == null && !p.IsDeleted).ToListAsync(ct);
            var cambio = Cambios(vigentes, resuelta.Value.Partes, resuelta.Value.Beneficiarios);
            if (cambio.BeneficiariosCambian && resuelta.Value.Beneficiarios.Count > 0 && request.EvidenceAttachmentPublicId is null)
                return Falla(new ErrorConDatos(CodigoConstanciaRequerida,
                    "Un cambio de beneficiarios lleva la constancia firmada por el titular: adjúntela y vuelva a guardar.",
                    new { field = "evidenceAttachmentPublicId" }));

            // Primero se cierra lo que cambia (los únicos filtrados a la vigencia abierta lo exigen) y después nace lo nuevo.
            var cierre = hoy.AddDays(-1);
            foreach (var fila in cambio.Cerrar) fila.Terminar(cierre, FinDe(fila, motivo), usuario);
            cuenta.HandlingMode = request.HandlingMode;
            await db.SaveChangesAsync(ct);

            foreach (var fila in TitularidadDeCuentas.Filas(cuenta, cambio.Nuevas, hoy)) db.MemberAccountParties.Add(fila);
            if (cambio.BeneficiariosCambian && resuelta.Value.Beneficiarios.Count > 0)
            {
                var conjunto = (await db.MemberAccountParties.Where(p => p.MemberAccountId == cuenta.Id && p.Role == MemberAccountPartyRole.Beneficiary)
                    .MaxAsync(p => p.BeneficiarySetNumber, ct) ?? 0) + 1;
                foreach (var fila in TitularidadDeCuentas.FilasDeBeneficiarios(cuenta, resuelta.Value.Beneficiarios, conjunto, request.EvidenceAttachmentPublicId, hoy))
                    db.MemberAccountParties.Add(fila);
            }
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDePartes.DtoAsync(db, cuenta, hoy, conHistoria: false, ct));
        }, ct);
    }

    /// <summary>Lo que cambia: las filas vigentes que se cierran, las partes que nacen y si cambia el conjunto de beneficiarios. (nuevo)</summary>
    public sealed record CambioDePartes(IReadOnlyList<MemberAccountParty> Cerrar, IReadOnlyList<ParteResuelta> Nuevas, bool BeneficiariosCambian);

    /// <summary>
    /// Compara lo vigente con lo pedido: una parte igual (persona, papel, proporción, límite, fundamento y adjuntos) sigue; las demás se cierran o nacen.
    /// Los beneficiarios cambian juntos: si el conjunto pedido no es el vigente (personas y porcentajes), se cierra entero.
    /// </summary>
    public static CambioDePartes Cambios(IReadOnlyList<MemberAccountParty> vigentes, IReadOnlyList<ParteResuelta> pedidas,
        IReadOnlyList<(int PersonaId, decimal Fraccion)> beneficiarios)
    {
        var cerrar = new List<MemberAccountParty>();
        var nuevas = new List<ParteResuelta>();
        var actuales = vigentes.Where(p => p.Role != MemberAccountPartyRole.Beneficiary).ToList();
        foreach (var pedida in pedidas)
        {
            var igual = actuales.FirstOrDefault(a => Igual(a, pedida));
            if (igual is null) nuevas.Add(pedida);
        }
        cerrar.AddRange(actuales.Where(a => !pedidas.Any(p => Igual(a, p))));

        var deBeneficiarios = vigentes.Where(p => p.Role == MemberAccountPartyRole.Beneficiary).ToList();
        var mismos = deBeneficiarios.Count == beneficiarios.Count
                     && deBeneficiarios.All(a => beneficiarios.Any(b => b.PersonaId == a.PersonId && b.Fraccion == a.Share));
        if (!mismos) cerrar.AddRange(deBeneficiarios);
        return new CambioDePartes(cerrar, nuevas, !mismos);
    }

    private static bool Igual(MemberAccountParty a, ParteResuelta p) =>
        a.PersonId == p.PersonaId && a.Role == p.Papel && a.Share == p.Proporcion && a.SignatureLimit == p.LimiteDeFirma
        && string.Equals(a.RepresentationBasis, p.Fundamento, StringComparison.Ordinal) && a.RepresentationSupportAttachmentPublicId == p.Soporte
        && a.SignatureSpecimenAttachmentPublicId == p.Firma && a.PhotoAttachmentPublicId == p.Foto;

    private static string FinDe(MemberAccountParty fila, string motivo) => fila.Role == MemberAccountPartyRole.Beneficiary
        ? $"Cambio de beneficiarios: {motivo}"
        : $"Cambio de partes: {motivo}";

    private static Result<MemberAccountPartiesDto> Falla(Error e) => Result.Failure<MemberAccountPartiesDto>(e);
}

/// <summary>
/// Levanta la marca «actualizar datos» que dejó la mayoría de edad del titular (<c>POST /api/lending/accounts/{id}/data-update</c>,
/// <c>Lending.Accounts.Parties.Manage</c>, con clave; FR-033, R22): exige los datos mínimos de conocimiento del titular y su autorización de tratamiento
/// de datos vigentes. Sin marca, <c>Lending.Savings.DataUpdateNotRequired</c>. (nuevo)
/// </summary>
public sealed record ConfirmMemberAccountDataUpdateCommand(Guid AccountPublicId, string Reason)
    : IRequest<Result<MemberAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmMemberAccountDataUpdateCommandValidator : ValidadorConMotivo<ConfirmMemberAccountDataUpdateCommand>
{
    public ConfirmMemberAccountDataUpdateCommandValidator() => RuleFor(x => x.AccountPublicId).NotEmpty();
}

public sealed class ConfirmMemberAccountDataUpdateCommandHandler(
    IApplicationDbContext db,
    IAutorizacionDeDatos autorizaciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmMemberAccountDataUpdateCommand, Result<MemberAccountDto>>
{
    public async Task<Result<MemberAccountDto>> Handle(ConfirmMemberAccountDataUpdateCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountDto>(leida.Error);
        var cuenta = leida.Value;
        if (!cuenta.RequiresDataUpdate) return Result.Failure<MemberAccountDto>(ErroresDeCuentas.SinActualizacionPendiente());
        var titular = await db.People.AsNoTracking().FirstAsync(p => p.Id == cuenta.HolderPersonId, ct);
        if (LectorDeCuentas.DatosFaltantes(titular) is { Count: > 0 } faltan)
            return Result.Failure<MemberAccountDto>(ErroresDeCuentas.DatosIncompletos(titular.PublicId, faltan));
        var autorizacion = await autorizaciones.VigenteAsync(titular.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, ct);
        if (autorizacion is not { Autoriza: true })
            return Result.Failure<MemberAccountDto>(ErroresDeOriginacion.SinAutorizacion(titular.PublicId, ReferenciasDeGobierno.Nombre(titular),
                DataAuthorizationPurpose.GeneralTreatment));

        cuenta.RequiresDataUpdate = false;
        cuenta.DataUpdateRequiredSince = null;
        await db.SaveChangesAsync(ct);
        return Result.Success(await LectorDeCuentas.DtoAsync(db, cuenta, reloj.HoyLocal, ct));
    }
}

/// <summary>Las partes de la cuenta a una fecha, con su historia si se pide. (nuevo)</summary>
public static class ConsultasDePartes
{
    public static async Task<MemberAccountPartiesDto> DtoAsync(IApplicationDbContext db, MemberAccount cuenta, DateOnly fecha, bool conHistoria,
        CancellationToken ct)
    {
        var filas = await db.MemberAccountParties.AsNoTracking().Where(p => p.MemberAccountId == cuenta.Id && !p.IsDeleted).OrderBy(p => p.Id).ToListAsync(ct);
        var personas = await LectorDeCuentas.PersonasAsync(db, filas.Select(p => p.PersonId).Append(cuenta.HolderPersonId), ct);
        var vigentes = filas.Where(p => p.VigenteEn(fecha)).ToList();
        return new MemberAccountPartiesDto(fecha, cuenta.HandlingMode,
            vigentes.Where(p => p.Role != MemberAccountPartyRole.Beneficiary).OrderBy(p => p.Role).ThenBy(p => p.Id)
                .Select(p => LectorDeCuentas.Parte(p, personas)).ToList(),
            LectorDeCuentas.Beneficiarios(vigentes, personas),
            cuenta.RequiresDataUpdate,
            conHistoria ? filas.Select(p => LectorDeCuentas.Parte(p, personas)).ToList() : []);
    }
}
