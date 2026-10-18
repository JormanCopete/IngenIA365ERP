using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// Abre una cuenta de captación o de aportes (feature 014, US2, T073; api.md §4, <c>POST /api/lending/accounts</c>, <c>Lending.Accounts.Open</c>, con
/// clave; data-model §3.1, §3.2; FR-006, FR-018, FR-019, FR-030 a FR-036):
/// <list type="number">
/// <item>la línea en alcance, de captación o de aportes, activa y con condiciones vigentes hoy; un CDAT se constituye por su ruta y el contractual llega
/// con K9; la naturaleza activada en la Cartera (<c>Lending.Savings.NotActivated</c>, <c>Lending.Contributions.NotActivated</c>) y la entrega
/// desplegada; la oficina en el alcance y entre las que ofrecen la línea;</item>
/// <item>el titular del maestro, no fallecido, con los datos mínimos de conocimiento (<c>Lending.Savings.MemberDataIncomplete</c>), asociado vigente si
/// la línea, el perfil o la naturaleza (aportes) lo exigen (<c>Lending.Savings.MembersOnly</c>, «vincular primero»); la clase autorizada por el perfil
/// regulatorio; una sola cuenta de aportes viva por persona y un ahorro permanente vivo por persona y línea;</item>
/// <item>la titularidad (<see cref="TitularidadDeCuentas"/>: cotitulares, manejo, firmantes, representante de menores y personas jurídicas,
/// beneficiarios que suman 100 % y la autorización de datos del titular y los cotitulares); la declaración de origen de fondos si el depósito de
/// apertura de la línea alcanza el umbral de efectivo; la marca de exención del GMF con la política vigente (<c>Lending.Gmf.AlreadyMarked</c>);</item>
/// <item>en una transacción: el cerrojo de <c>LND_Setup</c> y de las personas, las listas restrictivas del titular y los cotitulares (una coincidencia
/// deja la cuenta <b>en apertura</b>, retenida para el oficial de cumplimiento, y responde el aviso <c>Lending.Screening.Blocked</c>), la cuenta con
/// sus partes, sus beneficiarios, su control y su marca, la intervención <c>Radicar</c> (quien abre no aprueba la apertura) y el número por
/// <see cref="NumeradorDeCartera"/> <b>al final</b> del orden de cerrojos.</item>
/// </list>
/// Nace <c>Active</c>, o <c>Opening</c> si la retienen las listas o si la línea exige un depósito de apertura (que llega por el contrato de cuentas). (nuevo)
/// </summary>
public sealed record OpenMemberAccountCommand(OpenAccountInput Input) : IRequest<Result<MemberAccountDto>>, IOperacionIdempotente, IConOficina
{
    public Guid OperationKey { get; init; }

    public Guid? BranchPublicId => Input?.OfficePublicId;
}

public sealed class OpenMemberAccountCommandValidator : AbstractValidator<OpenMemberAccountCommand>
{
    public OpenMemberAccountCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.LinePublicId).NotEmpty().WithMessage("Elija la línea.").When(x => x.Input is not null);
        RuleFor(x => x.Input.HolderPersonPublicId).NotEmpty().WithMessage("Elija el titular.").When(x => x.Input is not null);
        RuleFor(x => x.Input.OfficePublicId).NotEmpty().WithMessage("Elija la oficina.").When(x => x.Input is not null);
        RuleFor(x => x.Input).SetValidator(new ValidadorDeTitularidad()).When(x => x.Input is not null);
    }

    /// <summary>La forma de las partes y los beneficiarios (los valores los decide <c>ReglasDeTitularidad</c>).</summary>
    private sealed class ValidadorDeTitularidad : AbstractValidator<OpenAccountInput>
    {
        public ValidadorDeTitularidad()
        {
            RuleFor(x => x.HandlingMode).IsInEnum();
            RuleFor(x => x.Parties).NotNull();
            RuleForEach(x => x.Parties).SetValidator(new ValidadorDeParte());
            RuleFor(x => x.Beneficiaries).NotNull();
            RuleForEach(x => x.Beneficiaries).ChildRules(b => b.RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elija el beneficiario."));
        }
    }
}

/// <summary>La forma de una parte pedida. (nuevo)</summary>
public sealed class ValidadorDeParte : AbstractValidator<MemberAccountPartyInput>
{
    public ValidadorDeParte()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elija la persona.");
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.RepresentationBasis).MaximumLength(30);
    }
}

public sealed class OpenMemberAccountCommandHandler(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    TitularidadDeCuentas titularidad,
    ReglasDeParametrosDeCartera parametros,
    IConsultaDeListasDeCartera listas,
    RegistroDeIntervenciones intervenciones,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<OpenMemberAccountCommand, Result<MemberAccountDto>>
{
    /// <summary>El tipo del expediente de segregación de una cuenta.</summary>
    public const string Expediente = nameof(MemberAccount);

    public async Task<Result<MemberAccountDto>> Handle(OpenMemberAccountCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var hoy = reloj.HoyLocal;
        if (e.OpeningDate is { } fecha && fecha != hoy) return Falla(ErroresDeCuentas.FechaDeApertura(hoy));

        // ------------------------------------------------------------------------------------------------- la línea --
        var encontrada = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, e.LinePublicId, seguir: false, ct);
        if (encontrada.IsFailure) return Falla(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Nature == LineNature.Placement) return Falla(ErroresDeCuentas.De(MemberAccount.ValidarNaturaleza(LineNature.Placement, null)!));
        var naturaleza = linea.Nature;
        if (!(await ActivacionesDeCaptacion.LeerAsync(db, ct)).RigeEn(naturaleza, hoy)) return Falla(ErroresDeCuentas.SinActivar(naturaleza));
        if (EntregaDeLineas.NoDisponible(naturaleza, contexto.Entrega) is { } sinEntrega) return Falla(sinEntrega);
        if (linea.Status != CreditLineStatus.Active) return Falla(ErroresDeCuentas.LineaNoAbre(linea.Code, linea.Status.ToString()));

        DepositLineVersion? version = null;
        DepositClass? clase = null;
        if (naturaleza == LineNature.Deposit)
        {
            version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, linea.Id, hoy, null, ct);
            if (version is null) return Falla(ErroresDeCuentas.LineaSinVersion(linea.Code, hoy));
            clase = version.DepositClass;
            if (clase == DepositClass.TermDeposit) return Falla(ErroresDeCuentas.UsarRutaDeCdat());
            // El contractual sella sus condiciones al constituirse (FR-007): llega con K9 (US7).
            if (clase == DepositClass.Contractual && contexto.Entrega < EntregaDeCartera.K9)
                return Falla(new ErrorConDatos(RequisitosDeActivacionDeCaptaciones.CodigoEntregaNoDisponible,
                    "El ahorro contractual llega con la entrega K9 de la Cartera, que todavía no está desplegada.",
                    new { nature = naturaleza.ToString(), availableFrom = EntregaDeCartera.K9.ToString() }));
        }
        else if (await LectorDeLineasDeCaptacion.VigenteDeAportesAsync(db, linea.Id, hoy, null, ct) is null)
        {
            return Falla(ErroresDeCuentas.LineaSinVersion(linea.Code, hoy));
        }

        // ------------------------------------------------------------------------------------------------ la oficina --
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == e.OfficePublicId && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        var enAlcance = await alcance.AsegurarAsync(oficina, null, Error.NotFound, auditoria, "Branch", e.OfficePublicId, ct);
        if (oficina is null || enAlcance.IsFailure) return Falla(Error.NotFound);
        if (version?.LeerOficinas() is { Count: > 0 } ofrecida && !ofrecida.Contains(e.OfficePublicId)) return Falla(ErroresDeCuentas.OficinaNoAdmitida(linea.Code));

        // ------------------------------------------------------------------------------------------------- el titular --
        var titular = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == e.HolderPersonPublicId && !p.IsDeleted, ct);
        if (titular is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (titular.IsDeceased) return Falla(ErroresDeCuentas.TitularFallecido(titular.PublicId));
        if (LectorDeCuentas.DatosFaltantes(titular) is { Count: > 0 } faltan) return Falla(ErroresDeCuentas.DatosIncompletos(titular.PublicId, faltan));

        var perfil = await contexto.PerfilAsync(hoy, ct);
        if (naturaleza == LineNature.Deposit
            && ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(clase!.Value, perfil.AutorizaCaptar, perfil.TipoDeOrganizacion, perfil.PerfilPublicId) is { } noAutorizada)
            return Falla(ErroresDeCuentas.De(noAutorizada));
        var exigeAsociado = naturaleza == LineNature.Contribution || version!.RequiresAssociate || perfil.SoloAsociados;
        if (exigeAsociado && !(await PersonaDeOriginacion.AfiliacionAsync(db, titular, hoy, ct)).Activo)
            return Falla(ErroresDeCuentas.SoloAsociados(titular.PublicId, ReferenciasDeGobierno.Nombre(titular), titular.IsAssociate ? "Withdrawn" : "NotAssociate"));
        if (await UnicaVivaAsync(naturaleza, clase, titular.Id, linea.Id, ct) is { } yaAbierta) return Falla(yaAbierta);

        // ------------------------------------------------------------------------------------------------ titularidad --
        var resuelta = await titularidad.ResolverAsync(titular, naturaleza, version, e.HandlingMode, e.Parties, e.Beneficiaries, hoy, ct);
        if (resuelta.IsFailure) return Falla(resuelta.Error);

        // FR-032, FR-059: el depósito de apertura que alcanza el umbral de efectivo pide la declaración de origen de fondos.
        if (version is { MinOpeningAmount: > 0m } && await LectorDeCuentas.UmbralDeOrigenDeFondosAsync(parametros, hoy, ct) is { } umbral
            && version.MinOpeningAmount >= umbral && e.SourceOfFunds?.DeclarationAttachmentPublicId is null)
            return Falla(ErroresDeCuentas.OrigenDeFondos(umbral, version.MinOpeningAmount));

        if (e.MarkGmfExempt)
        {
            if (naturaleza != LineNature.Deposit)
                return Falla(ErroresDeCuentas.De(ProblemaDeCartera.Con(GmfExemptAccount.CodigoNoEsAhorro,
                    "Sólo una cuenta de ahorro se marca exenta del GMF: una cuenta de aportes no.", "markGmfExempt", true)));
            if (await LectorDeCuentas.PoliticaDeGmfAsync(parametros, hoy, ct) is var politica && politica != GmfExemptionPolicy.SingleMarkedAccount)
                return Falla(ErroresDeCuentas.PoliticaSinMarca(politica.ToString()));
        }

        var publicId = Guid.NewGuid();
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(Expediente, publicId), AccionesDeSegregacion.Radicar, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            var personas = resuelta.Value.Partes.Where(p => p.Papel is MemberAccountPartyRole.Holder or MemberAccountPartyRole.CoHolder)
                .Select(p => p.PersonaId).Distinct().Order().ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(personas, ModoDeEspera.Esperar, ct);
            // Bajo el cerrojo de la persona: la cuenta de aportes, el permanente y la marca del GMF, que otra apertura pudo tomar mientras tanto.
            if (await UnicaVivaAsync(naturaleza, clase, titular.Id, linea.Id, ct) is { } tomada) return Falla(tomada);
            if (e.MarkGmfExempt && await MarcaVigenteAsync(titular.Id, ct) is { } marcada) return Falla(marcada);

            // FR-032: listas restrictivas del titular y los cotitulares; una coincidencia retiene la apertura para el oficial.
            var avisos = new List<MemberAccountWarningDto>();
            var coincidencias = new List<Guid>();
            foreach (var persona in personas)
            {
                var consultada = await listas.ConsultarAsync(new PedidoDeConsultaDeListas(persona, ScreeningContext.AccountOpening), ct);
                if (!consultada.CuentaComoConsulta)
                    return Falla(new ErrorConDatos(ErroresDeOriginacion.CodigoCanalNoDisponible,
                        "La consulta de listas restrictivas no está disponible: no se puede abrir la cuenta sin ella. Intente más tarde.",
                        new { channelCode = consultada.Resultado.ChannelCode }));
                if (consultada.Bloquea) coincidencias.Add(consultada.Resultado.PublicId);
            }
            if (coincidencias.Count > 0)
            {
                var aviso = ErroresDeCuentas.ListasBloquean(coincidencias);
                avisos.AddRange(coincidencias.Select(c => new MemberAccountWarningDto(aviso.Code, aviso.Message, c)));
            }

            var cuenta = MemberAccount.Abrir(linea.Id, naturaleza, clase, titular.Id, oficina.Value, e.HandlingMode, hoy, verificada.Value.UsuarioId, ahora);
            cuenta.PublicId = publicId;
            if (coincidencias.Count == 0 && version is not { MinOpeningAmount: > 0m }) cuenta.Activar(hoy, ahora);
            db.MemberAccounts.Add(cuenta);
            foreach (var fila in TitularidadDeCuentas.Filas(cuenta, resuelta.Value.Partes, hoy)) db.MemberAccountParties.Add(fila);
            foreach (var fila in TitularidadDeCuentas.FilasDeBeneficiarios(cuenta, resuelta.Value.Beneficiarios, 1, e.BeneficiaryRecordAttachmentPublicId, hoy))
                db.MemberAccountParties.Add(fila);
            registro.IniciarControl(cuenta, hoy, ahora);
            intervenciones.Registrar(verificada.Value, Expediente, publicId);
            // Al final del orden de cerrojos: el consecutivo de la línea (FR-019, FR-035).
            await numerador.NumerarCuentaAsync(cuenta, linea.Code, PrefijoDe(linea), ct);
            await db.SaveChangesAsync(ct);

            if (e.MarkGmfExempt)
            {
                db.GmfExemptAccounts.Add(GmfExemptAccount.Marcar(cuenta, hoy, "Marcada al abrir la cuenta, a solicitud del titular.", verificada.Value.UsuarioId));
                await db.SaveChangesAsync(ct);
            }
            return Result.Success(await LectorDeCuentas.DtoAsync(db, cuenta, hoy, ct, avisos));
        }, ct);
    }

    /// <summary>El prefijo del número de las cuentas de una línea: su código y un guion (el consecutivo es por línea, data-model §0.4).</summary>
    public static string PrefijoDe(CreditLine linea) => linea.Code + "-";

    /// <summary>
    /// Una cuenta de aportes viva por persona, un ahorro permanente vivo por persona y línea. US10 (T102, FR-030, FR-085): con un retiro aprobado, liquidado
    /// o por rehacer la persona sólo recibe operaciones del retiro (no abre cuentas); terminado el retiro, el reingreso abre cuentas nuevas y las canceladas
    /// conservan su historia.
    /// </summary>
    private async Task<Error?> UnicaVivaAsync(LineNature naturaleza, DepositClass? clase, int titularId, int lineaId, CancellationToken ct)
    {
        var retiro = await db.MembershipWithdrawals.AsNoTracking()
            .Where(w => w.PersonId == titularId && !w.IsDeleted && (w.Status == MembershipWithdrawalStatus.Approved
                        || w.Status == MembershipWithdrawalStatus.Settled || w.Status == MembershipWithdrawalStatus.ToRedo))
            .Select(w => new { w.Number, w.Status }).FirstOrDefaultAsync(ct);
        if (retiro is not null) return Withdrawals.ErroresDeRetiro.EnCurso(retiro.Number, retiro.Status);
        if (naturaleza == LineNature.Contribution)
        {
            var numero = await db.MemberAccounts.AsNoTracking()
                .Where(a => a.HolderPersonId == titularId && a.Nature == LineNature.Contribution && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .Select(a => a.Number).FirstOrDefaultAsync(ct);
            return numero is null ? null : ErroresDeCuentas.AportesYaAbierta(numero);
        }
        if (clase != DepositClass.Permanent) return null;
        var permanente = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == titularId && a.CreditLineId == lineaId && a.DepositClass == DepositClass.Permanent
                        && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
            .Select(a => a.Number).FirstOrDefaultAsync(ct);
        return permanente is null ? null : ErroresDeCuentas.PermanenteYaAbierto(permanente);
    }

    private async Task<Error?> MarcaVigenteAsync(int personaId, CancellationToken ct) => await MarcasDeGmf.VigenteAsync(db, personaId, ct) is { } vigente
        ? ErroresDeCuentas.YaMarcada(vigente.AccountPublicId, vigente.AccountNumber, vigente.ValidFrom)
        : null;

    private static Result<MemberAccountDto> Falla(Error e) => Result.Failure<MemberAccountDto>(e);
}
