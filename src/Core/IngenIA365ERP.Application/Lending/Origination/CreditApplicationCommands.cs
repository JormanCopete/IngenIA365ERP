using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Origination;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

// Feature 013, US2 (T0396; FR-034–FR-036, FR-047; api.md §4.2; data-model §2.1–§2.3): el borrador de la solicitud —crear, editar, adjuntar y
// quitar documentos—. Antes de la activación no se radica nada (FR-142): toda escritura responde Lending.NotActivated. Oficina y línea fuera
// del alcance de quien pide: el 404 genérico (T62). (nuevo)

/// <summary>La forma de una solicitud (Principio VIII): lo que no necesita la base. (nuevo)</summary>
public sealed class CreditApplicationInputValidator : AbstractValidator<CreditApplicationInput>
{
    public CreditApplicationInputValidator()
    {
        RuleFor(x => x.ApplicantPersonPublicId).NotEmpty().WithMessage("Elija al solicitante.");
        RuleFor(x => x.OfficePublicId).NotEmpty().WithMessage("Elija la oficina.");
        RuleFor(x => x.CreditLinePublicId).NotEmpty().WithMessage("Elija la línea de crédito.");
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0m).PrecisionScale(18, 2, true).WithMessage("El monto es mayor que cero y admite hasta 2 decimales.");
        RuleFor(x => x.Installments).GreaterThan(0).When(x => x.Kind == ApplicationKind.Loan).WithMessage("El número de cuotas es de al menos una.");
        RuleFor(x => x.Purpose).MaximumLength(300);
        RuleFor(x => x.PaymentForm).IsInEnum();
        RuleFor(x => x.PrepaymentPreference).NotNull().IsInEnum().When(x => x.Kind == ApplicationKind.Loan)
            .WithMessage("Indique qué prefiere el deudor al abonar a capital: reducir el plazo o la cuota (FR-035).");
        RuleFor(x => x.PolicyExceptionReason).NotEmpty().When(x => x.PolicyExceptionRequested)
            .WithMessage("Indique por qué pide una excepción a la política.").MaximumLength(500);
        RuleFor(x => x.SimulationSha256).Matches("^[0-9a-f]{64}$").When(x => x.SimulationSha256 is not null);
        RuleForEach(x => x.CoDebtors).ChildRules(p =>
        {
            p.RuleFor(y => y.PersonPublicId).NotEmpty();
            p.RuleFor(y => y.Role).Must(r => r is CreditPartyRole.CoDebtor or CreditPartyRole.Guarantor).WithMessage("Una parte es codeudor o avalista.");
            p.RuleFor(y => y.MonthlyIncome).GreaterThanOrEqualTo(0m).When(y => y.MonthlyIncome is not null);
            p.RuleFor(y => y.MonthlyExpenses).GreaterThanOrEqualTo(0m).When(y => y.MonthlyExpenses is not null);
            p.RuleFor(y => y.ExternalInstallments).GreaterThanOrEqualTo(0m).When(y => y.ExternalInstallments is not null);
        });
        RuleForEach(x => x.Authorizations).ChildRules(a =>
        {
            a.RuleFor(y => y.PersonPublicId).NotEmpty();
            a.RuleFor(y => y.Purpose).IsInEnum();
        });
        RuleFor(x => x.ApplicantCapacity!.MonthlyIncome).GreaterThanOrEqualTo(0m).When(x => x.ApplicantCapacity?.MonthlyIncome is not null);

        // 2026-11-27 (feature 028, T024; api.md §2): la forma de lo que recoge. Lo que necesita la base (del solicitante, vivo, admitido, neto) lo revisa
        // RecogidasDeLaSolicitud; las dos listas a la vez, RecogidasPedidas.Unificar (Request.BodyInvalid).
        RuleFor(x => x.Payoffs).Must(p => p is null || p.Count <= MaximoDeRecogidas)
            .WithMessage($"Una solicitud recoge a lo sumo {MaximoDeRecogidas} productos.");
        RuleFor(x => x.Payoffs).Must(p => p is null || p.Where(r => r?.ProductPublicId is not null).GroupBy(r => (r.Kind, r.ProductPublicId)).All(g => g.Count() == 1))
            .WithMessage("Un producto está dos veces entre los que recoge la solicitud.");
        RuleForEach(x => x.Payoffs).ChildRules(p =>
        {
            p.RuleFor(y => y.Kind).Equal(PayoffProductKind.Loan)
                .WithMessage("Por ahora el crédito nuevo sólo recoge créditos: las cuentas de aportes y ahorros y las facturas llegan en otra entrega.");
            p.RuleFor(y => y.ProductPublicId).NotEmpty().WithMessage("Indique el crédito que recoge.");
            p.RuleFor(y => y.Mode).Must(m => m is PayoffMode.Total or PayoffMode.Partial).WithMessage("Un crédito se recoge total o parcial.");
            p.RuleFor(y => y.Amount).NotNull().GreaterThan(0m).When(y => y.Mode == PayoffMode.Partial)
                .WithMessage("Una recogida parcial lleva un valor mayor que cero.");
            p.RuleFor(y => y.Amount).PrecisionScale(18, 2, true).When(y => y.Amount is not null);
            // 2026-11-27 (feature 028, E2, T066): «todo menos la cuota causada» (Recogida.CuotaCausada) sólo en una recogida total; si la línea del crédito lo
            // admite lo decide RecogidasDeLaSolicitud.
            p.RuleFor(y => y.InstallmentTreatment).IsInEnum().When(y => y.InstallmentTreatment is not null);
            p.RuleFor(y => y.InstallmentTreatment).Must(t => t != InstallmentTreatment.Exclude).When(y => y.Mode != PayoffMode.Total)
                .WithMessage("Dejar la cuota causada a su canal sólo aplica a una recogida total.");
        });
        RuleForEach(x => x.OwnLoanPayoffs).ChildRules(p =>
        {
            p.RuleFor(y => y.LoanPublicId).NotEmpty().WithMessage("Indique el crédito que recoge.");
            p.RuleFor(y => y.Amount).NotNull().GreaterThan(0m).When(y => string.Equals(y.Mode?.Trim(), RecogidasPedidas.ModoViejoParcial, StringComparison.OrdinalIgnoreCase))
                .WithMessage("Una recogida parcial lleva un valor mayor que cero.");
        });
        RuleFor(x => x.OwnLoanPayoffs).Must(p => p is null || p.Count <= MaximoDeRecogidas)
            .WithMessage($"Una solicitud recoge a lo sumo {MaximoDeRecogidas} productos.");
    }

    /// <summary>2026-11-27 (feature 028): a lo sumo los mismos productos que marca la consulta de productos para recoger.</summary>
    public const int MaximoDeRecogidas = GetPayoffCandidatesQueryHandler.MaximoDeMarcados;
}

/// <summary>Lo que la solicitud resolvió de su entrada: personas, oficina, línea, la simulación y el tipo de deudor. (nuevo)</summary>
public sealed record SolicitudArmada(
    Person Solicitante,
    DebtorKind TipoDeDeudor,
    IReadOnlyList<(Person Persona, CreditApplicationPartyInput Entrada)> Codeudores,
    int OficinaId,
    CreditLine Linea,
    SimulacionCalculada Simulacion)
{
    /// <summary>K2 (T0842): el convenio de la pagaduría externa de una solicitud por libranza.</summary>
    public int? ConvenioId { get; init; }

    /// <summary>2026-11-23: el proveedor del bien en una compra con descuento condicionado.</summary>
    public int? ProveedorId { get; init; }
}

/// <summary>
/// Las reglas que comparten crear y editar una solicitud (feature 013, T0396; FR-035, FR-018; data-model §2.1): Cartera activada; la oficina y la
/// línea dentro del alcance (fuera, el 404); el solicitante asociado activo salvo que la línea y el perfil regulatorio admitan no asociados
/// (<c>Lending.Application.NotAssociate</c>); los codeudores existen y nadie está dos veces (<c>.DuplicateParty</c>); la forma de pago está en la
/// línea y la nómina propia exige que el solicitante sea empleado (<c>.PaymentFormNotAllowed</c>); desde K2 la libranza con una pagaduría exige un
/// convenio vivo, activo y con versión vigente (<c>Lending.PayrollAgreement.NotAvailable</c>), el registro de operador de libranza en el perfil
/// regulatorio (<c>.OperatorRegistrationMissing</c>) y la certificación con su fecha (<c>Lending.Payroll.CertificationRequired</c>); y la
/// simulación con la línea vigente: monto, plazo, periodicidad y usura
/// (<c>Lending.Schedule.OutOfLineRange</c>, <c>Lending.CreditLine.Closed</c>, <c>Lending.Usury.Exceeded</c>). (nuevo)
/// </summary>
public sealed class ArmadoDeSolicitud(
    IApplicationDbContext db,
    SimuladorDeCredito simulador,
    IPerfilRegulatorio perfil,
    IAlcanceDeCartera alcanceDeLaPeticion,
    LendingAuditEmitter auditoria)
{
    public async Task<Result<SolicitudArmada>> ArmarAsync(CreditApplicationInput e, DateOnly hoy, CancellationToken ct)
    {
        if (!await CarteraActivadaAsync(db, ct)) return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());

        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == e.OfficePublicId && !b.IsDeleted).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null) return Falla(Error.NotFound);
        var enAlcance = await alcanceDeLaPeticion.AsegurarAsync(oficina, null, Error.NotFound, auditoria, "Branch", e.OfficePublicId, ct);
        if (enAlcance.IsFailure) return Falla(enAlcance.Error);
        var linea = await ConsultasDeLineas.LineaEnAlcanceAsync(db, alcanceDeLaPeticion, auditoria, e.CreditLinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Falla(linea.Error);

        var solicitante = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == e.ApplicantPersonPublicId && !p.IsDeleted, ct);
        if (solicitante is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        var pedidos = (e.CoDebtors ?? []).Select(c => c.PersonPublicId).ToList();
        var repetida = pedidos.Append(e.ApplicantPersonPublicId).GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null) return Falla(ErroresDeOriginacion.ParteRepetida(repetida.Key));
        var codeudores = await db.People.AsNoTracking().Where(p => pedidos.Contains(p.PublicId) && !p.IsDeleted).ToListAsync(ct);
        if (codeudores.Count != pedidos.Count) return Falla(ErroresDeGobierno.PersonaInexistente());

        var fecha = e.ExpectedDisbursementDate ?? hoy;
        int? convenioId = null;
        if (e.PaymentForm == RepaymentChannel.PaymasterDeduction)
        {
            var libranza = await LibranzaAsync(e, fecha, ct);
            if (libranza.IsFailure) return Falla(libranza.Error);
            convenioId = libranza.Value;
        }
        else if (e.Payroll?.PayrollAgreementPublicId is not null)
            return Falla(Payroll.ErroresDeLibranza.ConvenioNoDisponible(null, "la forma de pago de la solicitud no es la libranza con una pagaduría"));
        var perfilDeBanda = await PersonaDeOriginacion.PerfilAsync(db, solicitante, fecha, ct);
        var simulada = await simulador.SimularAsync(linea.Value, e.Simulacion(), perfilDeBanda, hoy, ct);
        if (simulada.IsFailure) return Falla(simulada.Error);
        var version = simulada.Value.Version.Version;

        var asociado = perfilDeBanda.TipoDeDeudor == DebtorKind.Associate;
        if (!asociado && !(version.AllowsNonAssociates && ReglasDeLineaDeCredito.PermiteNoAsociados(await perfil.VigenteAsync(fecha, null, ct))))
            return Falla(ErroresDeOriginacion.NoAsociado(solicitante.PublicId, ReferenciasDeGobierno.Nombre(solicitante)));

        if (!version.LeerFormasDePago().Contains(e.PaymentForm))
            return Falla(ErroresDeOriginacion.FormaDePagoNoAdmitida(e.PaymentForm.ToString(), "NotInLine"));
        if (e.PaymentForm == RepaymentChannel.OwnPayroll && !solicitante.IsEmployee)
            return Falla(ErroresDeOriginacion.FormaDePagoNoAdmitida(e.PaymentForm.ToString(), "NotEmployee"));

        var u = simulada.Value.Usura;
        if (!u.Pasa)
            return Falla(ErroresDeLineasDeCredito.UsuraExcedida("amount", simulada.Value.Fecha, u, simulada.Value.Combinacion.ModalityCode));

        // 2026-11-23 (descuento condicionado, el caso del SOAT): con opciones en la línea, el bien, el proveedor y la opción; el monto es el
        // valor del bien menos lo que paga el fondo. Sin opciones, nada de eso.
        var compra = await CompraAsync(e, version.LeerOpcionesDeDescuento(), ct);
        if (compra.IsFailure) return Falla(compra.Error);

        return Result.Success(new SolicitudArmada(solicitante, asociado ? DebtorKind.Associate : DebtorKind.Customer,
            (e.CoDebtors ?? []).Select(c => (codeudores.Single(p => p.PublicId == c.PersonPublicId), c)).ToList(), oficina.Value, linea.Value,
            simulada.Value) { ConvenioId = convenioId, ProveedorId = compra.Value });
    }

    /// <summary>El proveedor de la compra con descuento (o nulo si la línea no tiene opciones), con sus reglas.</summary>
    private async Task<Result<int?>> CompraAsync(CreditApplicationInput e, IReadOnlyList<Domain.Lending.Configuration.OpcionDeDescuento> opciones,
        CancellationToken ct)
    {
        if (opciones.Count == 0)
            return e.AssetValue is null && e.SupplierPersonPublicId is null && string.IsNullOrWhiteSpace(e.DiscountOptionCode)
                ? Result.Success<int?>(null)
                : Result.Failure<int?>(ErroresDeOriginacion.LineaSinDescuento());
        if (e.AssetValue is not { } valor || valor <= 0m || e.SupplierPersonPublicId is not { } proveedor || string.IsNullOrWhiteSpace(e.DiscountOptionCode))
            return Result.Failure<int?>(ErroresDeOriginacion.CompraIncompleta());
        var codigo = e.DiscountOptionCode.Trim().ToUpperInvariant();
        var opcion = opciones.FirstOrDefault(o => o.Code == codigo);
        if (opcion is null) return Result.Failure<int?>(ErroresDeOriginacion.OpcionDeDescuentoDesconocida(codigo, opciones.Select(o => o.Code).ToList()));
        var monto = valor - Domain.Lending.Configuration.OpcionesDeDescuento.Subsidio(valor, opcion);
        if (e.Amount != monto) return Result.Failure<int?>(ErroresDeOriginacion.MontoDeLaCompra(valor, opcion.Name, monto));
        var id = await db.People.AsNoTracking().Where(p => p.PublicId == proveedor && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        return id is null ? Result.Failure<int?>(ErroresDeGobierno.PersonaInexistente()) : Result.Success(id);
    }

    /// <summary>
    /// K2 (T0842; FR-108, FR-109): el convenio de una solicitud por libranza —vivo, de una pagaduría externa, activo y con versión vigente a la fecha—,
    /// el registro de la cooperativa como operadora de libranza y la certificación de la pagaduría con su fecha.
    /// </summary>
    private async Task<Result<int>> LibranzaAsync(CreditApplicationInput e, DateOnly fecha, CancellationToken ct)
    {
        if (e.Payroll?.PayrollAgreementPublicId is not { } publico)
            return Result.Failure<int>(Payroll.ErroresDeLibranza.ConvenioNoDisponible(null, "elija el convenio de la pagaduría"));
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == publico && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<int>(Payroll.ErroresDeLibranza.ConvenioNoDisponible(null, "el convenio no existe"));
        if (convenio.IsOwnPayroll)
            return Result.Failure<int>(Payroll.ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, "es la nómina propia: use la forma de pago nómina propia"));
        if (!convenio.Operable) return Result.Failure<int>(Payroll.ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, "no está activo"));
        var versiones = await db.PayrollAgreementVersions.AsNoTracking().Where(v => v.PayrollAgreementId == convenio.Id && !v.IsDeleted).ToListAsync(ct);
        if (Domain.Entities.Lending.Payroll.PayrollAgreementVersion.Vigente(versiones, fecha) is null)
            return Result.Failure<int>(Payroll.ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, $"no tiene una versión vigente al {fecha:yyyy-MM-dd}"));
        if (await perfil.VigenteAsync(fecha, null, ct) is not { LibranzaOperatorRegistered: true })
            return Result.Failure<int>(Payroll.ErroresDeLibranza.SinRegistroDeOperador(fecha));
        if (e.Payroll.CertificationAttachmentPublicId is null || e.Payroll.CertificationDate is null)
            return Result.Failure<int>(Payroll.ErroresDeLibranza.CertificacionRequerida());
        return Result.Success(convenio.Id);
    }

    /// <summary>Copia en la solicitud lo pedido y lo que selló la simulación (versión de la línea, plazo, huella).</summary>
    public static void Copiar(CreditApplication s, CreditApplicationInput e, SolicitudArmada a)
    {
        var plan = a.Simulacion.Entrada;
        s.Kind = e.Kind;
        s.ApplicantPersonId = a.Solicitante.Id;
        s.DebtorKind = a.TipoDeDeudor;
        s.BranchId = a.OficinaId;
        s.CreditLineId = a.Linea.Id;
        s.CreditLineVersionId = a.Simulacion.Version.Version.Id;
        s.RequestedAmount = e.Amount;
        s.Installments = e.Kind == ApplicationKind.Loan ? plan.Periodos : null;
        s.TermMonths = e.Kind == ApplicationKind.Loan ? a.Simulacion.PlazoMeses : null;
        s.Periodicity = plan.Periodicidad;
        s.AmortizationSystem = plan.Sistema;
        s.FirstDueDate = e.FirstDueDate;
        s.ExpectedDisbursementDate = e.ExpectedDisbursementDate;
        s.GraceKind = plan.Gracia == GraceKind.None ? null : plan.Gracia;
        s.GraceInstallments = plan.Gracia == GraceKind.None ? null : plan.PeriodosDeGracia;
        s.ExtraordinaryInstallments = e.Extraordinary is { Count: > 0 } x ? ConsultasDeSolicitudes.Serializar(x) : null;
        s.SelectedInsurances = e.Insurances is { Count: > 0 } seguros
            ? ConsultasDeSolicitudes.Serializar(seguros.Select(c => c.Trim().ToUpperInvariant()).Order(StringComparer.Ordinal))
            : null;
        s.SimulationSha256 = a.Simulacion.Huella;
        s.Purpose = string.IsNullOrWhiteSpace(e.Purpose) ? null : e.Purpose.Trim();
        s.RepaymentChannel = e.PaymentForm;
        // K2 (T0842): el convenio y la identificación del empleado ante la pagaduría pasan al vínculo al desembolsar (FR-109).
        s.PayrollAgreementId = e.PaymentForm == RepaymentChannel.PaymasterDeduction ? a.ConvenioId : null;
        s.PayrollEmployeeIdentifier = e.PaymentForm == RepaymentChannel.PaymasterDeduction && !string.IsNullOrWhiteSpace(e.Payroll?.EmployeeId)
            ? e.Payroll.EmployeeId.Trim()
            : null;
        s.PrepaymentChoice = e.PrepaymentPreference;
        // 2026-11-27 (feature 028, T023): las recogidas van en filas (LND_CreditApplicationPayoffs, RecogidasDeLaSolicitud): PayoffRequest se conserva y
        // ya no se escribe.
        s.PolicyExceptionRequested = e.PolicyExceptionRequested;
        s.PolicyExceptionReason = e.PolicyExceptionRequested ? e.PolicyExceptionReason?.Trim() : null;
        // K6b (T1055): sólo una solicitud de vivienda dice si el inmueble es VIS.
        s.IsSocialHousing = Housing.ValidacionDeVivienda.EsVivienda(a.Simulacion) ? e.IsSocialHousing ?? false : null;
        // 2026-11-23 (descuento condicionado): sólo con proveedor resuelto (la línea tiene opciones y la entrada las trae).
        s.AssetValue = a.ProveedorId is null ? null : e.AssetValue;
        s.SupplierPersonId = a.ProveedorId;
        s.DiscountOptionCode = a.ProveedorId is null ? null : e.DiscountOptionCode?.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Las autorizaciones que trae el cuerpo (FR-006, T69): las que no están vigentes con decisión aceptada se agregan por
    /// <see cref="IAutorizacionDeDatos.AgregarAsync"/> sin guardar (las guarda la transacción de quien llama); las vigentes no se repiten.
    /// </summary>
    public static async Task<Result> AutorizacionesAsync(IAutorizacionDeDatos autorizaciones, CreditApplicationInput e, DateTime ahora, CancellationToken ct)
    {
        foreach (var a in e.Authorizations ?? [])
        {
            var canal = a.ContactChannel?.ToString();
            var vigente = await autorizaciones.VigenteAsync(a.PersonPublicId, a.Purpose, canal, ct);
            if (vigente is { Autoriza: true }) continue;
            var agregada = await autorizaciones.AgregarAsync(new AutorizacionAAgregar(a.PersonPublicId, a.Purpose, DecisionDeAutorizacion.Accepted,
                a.PolicyVersionPublicId, a.ContactChannel, a.EvidenceAttachmentPublicId, Domain.Enums.Compliance.DataAuthorizationOrigin.CreditApplication,
                a.GrantedAt ?? ahora), ct);
            if (agregada.IsFailure) return Result.Failure(agregada.Error);
        }
        return Result.Success();
    }

    /// <summary>¿Cartera está activada? Antes de la activación sólo se simula y se otorgan cupos (FR-142, api.md §1.13).</summary>
    public static Task<bool> CarteraActivadaAsync(IApplicationDbContext db, CancellationToken ct) =>
        db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);

    private static Result<SolicitudArmada> Falla(Error e) => Result.Failure<SolicitudArmada>(e);
}

/// <summary>La solicitud viva por su <c>PublicId</c> y dentro del alcance (oficina y línea); fuera, el mismo 404 que si no existiera. (nuevo)</summary>
public static class SolicitudEnAlcance
{
    public static async Task<Result<CreditApplication>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.CreditApplications : db.CreditApplications.AsNoTracking();
        var s = await consulta.FirstOrDefaultAsync(a => a.PublicId == publicId && !a.IsDeleted, ct);
        if (s is null) return Result.Failure<CreditApplication>(ErroresDeOriginacion.SolicitudInexistente());
        var dentro = await alcance.AsegurarAsync(s.BranchId, s.CreditLineId, ErroresDeOriginacion.SolicitudInexistente(), auditoria,
            nameof(CreditApplication), s.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<CreditApplication>(dentro.Error) : Result.Success(s);
    }

    /// <summary>Nulo si la versión leída es la vigente o no se mandó; si no, <c>Concurrency.StaleRowVersion</c> (409).</summary>
    public static Error? Concurrencia(CreditApplication s, byte[]? leida) =>
        leida is { Length: > 0 } l && s.RowVersion is { Length: > 0 } actual && !l.AsSpan().SequenceEqual(actual) ? ErroresDeOriginacion.ConcurrenciaVieja() : null;
}

/// <summary>
/// Crea una solicitud en borrador (api.md §4.2, <c>POST /api/lending/applications</c>, <c>Lending.Applications.Create</c>, con clave): las reglas de
/// <see cref="ArmadoDeSolicitud"/>, el solicitante y los codeudores como partes (con la capacidad que declaren), la certificación laboral como
/// documento adicional y las autorizaciones del cuerpo, todo en una transacción. Un borrador no consume número. Quien la crea debe tener persona
/// (<c>Lending.Security.UserWithoutPerson</c>). (nuevo)
/// </summary>
public sealed record CreateCreditApplicationCommand(CreditApplicationInput Input) : IRequest<Result<CreditApplicationDto>>, IOperacionIdempotente, IConOficina
{
    public Guid OperationKey { get; init; }

    public Guid? BranchPublicId => Input?.OfficePublicId;
}

public sealed class CreateCreditApplicationCommandValidator : AbstractValidator<CreateCreditApplicationCommand>
{
    public CreateCreditApplicationCommandValidator() => RuleFor(x => x.Input).NotNull().SetValidator(new CreditApplicationInputValidator());
}

public sealed class CreateCreditApplicationCommandHandler(
    IApplicationDbContext db,
    ArmadoDeSolicitud armado,
    RecogidasDeLaSolicitud recogidas,
    IAutorizacionDeDatos autorizaciones,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateCreditApplicationCommand, Result<CreditApplicationDto>>
{
    public async Task<Result<CreditApplicationDto>> Handle(CreateCreditApplicationCommand request, CancellationToken ct)
    {
        var usuario = await UsuarioConPersona.LeerAsync(db, actorActual, ct);
        if (usuario.IsFailure) return Result.Failure<CreditApplicationDto>(usuario.Error);
        var e = request.Input;
        var armada = await armado.ArmarAsync(e, reloj.HoyLocal, ct);
        if (armada.IsFailure) return Result.Failure<CreditApplicationDto>(armada.Error);
        var a = armada.Value;
        // 2026-11-27 (feature 028, T023–T024): lo que recoge, validado y proyectado a D con la información hasta R.
        var preparadas = await recogidas.PrepararAsync(e, a.Solicitante.Id, a.Linea.Id, reloj.HoyLocal, ct);
        if (preparadas.IsFailure) return Result.Failure<CreditApplicationDto>(preparadas.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var s = new CreditApplication { Status = CreditApplicationStatus.Draft, CreatedByUserId = usuario.Value };
            ArmadoDeSolicitud.Copiar(s, e, a);
            db.CreditApplications.Add(s);
            await db.SaveChangesAsync(ct);
            await recogidas.ReescribirAsync(s.Id, preparadas.Value, ct);

            var solicitante = CreditApplicationParty.Nueva(s.Id, a.Solicitante.Id, CreditPartyRole.Applicant, 0);
            solicitante.Declarar(e.ApplicantCapacity?.MonthlyIncome, e.ApplicantCapacity?.MonthlyExpenses, e.ApplicantCapacity?.ExternalInstallments);
            db.CreditApplicationParties.Add(solicitante);
            var orden = 1;
            foreach (var (persona, entrada) in a.Codeudores)
            {
                var parte = CreditApplicationParty.Nueva(s.Id, persona.Id, entrada.Role, orden++);
                parte.Declarar(entrada.MonthlyIncome, entrada.MonthlyExpenses, entrada.ExternalInstallments);
                db.CreditApplicationParties.Add(parte);
            }
            if (e.Payroll?.CertificationAttachmentPublicId is { } certificacion)
                db.CreditApplicationDocuments.Add(CreditApplicationDocument.Nuevo(s.Id, null, null, certificacion, e.Payroll.CertificationDate));

            var autorizadas = await ArmadoDeSolicitud.AutorizacionesAsync(autorizaciones, e, reloj.UtcNow, ct);
            if (autorizadas.IsFailure) return Result.Failure<CreditApplicationDto>(autorizadas.Error);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, reloj.HoyLocal, ct));
        }, ct);
    }
}

/// <summary>
/// Edita una solicitud en <c>Draft</c> o <c>Returned</c> (api.md §4.2, <c>PUT /{id}</c>, con clave): las mismas reglas que crearla; fuera de esos
/// estados, <c>Lending.Application.NotEditable</c> con el estado; con una versión leída vieja, <c>Concurrency.StaleRowVersion</c>. Los codeudores
/// que salen se dan de baja (baja lógica) y el solicitante no cambia: para otro solicitante, otra solicitud.
/// <para>
/// 2026-11-25 (pedido del dueño): también edita una solicitud <b>radicada</b> (<c>Submitted</c> o <c>PendingDecision</c>) sin devolverla, sólo por
/// <c>PUT /{id}/submitted</c> (<see cref="EdicionRadicada"/>, permiso <c>Lending.Applications.EditSubmitted</c>), si
/// <c>Originacion.EditarRadicadaSinDevolver</c> de su línea lo deja, con <see cref="EditReason"/>, en los grupos de
/// <c>Originacion.GruposEditablesAlDevolver</c> y sin chocar con la segregación (<c>EditarRadicada</c>). Vuelve a correr las validaciones y el
/// enrutamiento, deja la fila en <c>LND_CreditApplicationEdits</c>, la intervención y el evento <c>Lending.Application.EditedAfterSubmission</c>, y
/// cambia la huella del enrutamiento: quien decide (o el acta) con la versión anterior a la vista recibe <c>Lending.Decision.ContentChanged</c>.
/// </para>
/// (nuevo)
/// </summary>
public sealed record UpdateCreditApplicationCommand(Guid ApplicationPublicId, CreditApplicationInput Input, byte[]? RowVersion = null)
    : IRequest<Result<CreditApplicationDto>>, IOperacionIdempotente, IConOficina
{
    public Guid OperationKey { get; init; }

    public Guid? BranchPublicId => Input?.OfficePublicId;

    /// <summary>2026-11-25: por qué se edita la solicitud radicada (obligatorio en ese caso; queda en su historial).</summary>
    public string? EditReason { get; init; }

    /// <summary>
    /// 2026-11-25: lo pone sólo <c>PUT /{id}/submitted</c>, que exige <c>Lending.Applications.EditSubmitted</c>; el cuerpo no lo trae. Sin él, una
    /// radicada responde <c>Lending.Application.UseSubmittedEditRoute</c>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool EdicionRadicada { get; init; }
}

public sealed class UpdateCreditApplicationCommandValidator : AbstractValidator<UpdateCreditApplicationCommand>
{
    public UpdateCreditApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new CreditApplicationInputValidator());
        RuleFor(x => x.EditReason).MaximumLength(CreditApplicationEdit.LargoDelMotivo);
    }
}

public sealed class UpdateCreditApplicationCommandHandler(
    IApplicationDbContext db,
    ArmadoDeSolicitud armado,
    RecogidasDeLaSolicitud recogidas,
    IAutorizacionDeDatos autorizaciones,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    GruposEditablesDeSolicitud editables,
    EvaluacionDeSolicitud evaluacion,
    EnrutamientoDeSolicitud enrutamiento,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    Housing.LectorDeVivienda? vivienda = null) : IRequestHandler<UpdateCreditApplicationCommand, Result<CreditApplicationDto>>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);

    public async Task<Result<CreditApplicationDto>> Handle(UpdateCreditApplicationCommand request, CancellationToken ct)
    {
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var s = leida.Value;
        var hoy = reloj.HoyLocal;

        // 2026-11-25 (pedido del dueño): la radicada se edita sin devolverla, por su ruta, si el parámetro de su línea lo deja y con motivo.
        var radicada = s.Radicada;
        IntervencionVerificada? verificada = null;
        if (radicada)
        {
            if (!request.EdicionRadicada) return Falla(ErroresDeOriginacion.EdicionRadicadaPorSuRuta(s.Status.ToString()));
            if (!await editables.EditaRadicadaAsync(s.CreditLineId, hoy, ct)) return Falla(ErroresDeOriginacion.RadicadaNoEditable(s.Status.ToString()));
            if (string.IsNullOrWhiteSpace(request.EditReason)) return Falla(ErroresDeOriginacion.MotivoDeEdicionRequerido());
            var intervencion = await intervenciones.VerificarAsync(
                new ExpedienteDeCartera(SubmitCreditApplicationCommandHandler.Expediente, s.PublicId), AccionesDeSegregacion.EditarRadicada, hoy, ct);
            if (intervencion.IsFailure) return Falla(intervencion.Error);
            verificada = intervencion.Value;
        }
        else if (request.EdicionRadicada)
            return Falla(ErroresDeOriginacion.EstadoInvalido(s.Status.ToString(), CreditApplicationStatus.PendingDecision.ToString()));
        else if (s.ExigirEditable() is not null) return Falla(ErroresDeOriginacion.NoEditable(s.Status.ToString()));
        if (SolicitudEnAlcance.Concurrencia(s, request.RowVersion) is { } vieja) return Falla(vieja);

        var e = request.Input;
        var solicitanteActual = await db.People.AsNoTracking().Where(p => p.Id == s.ApplicantPersonId).Select(p => p.PublicId).FirstAsync(ct);
        if (solicitanteActual != e.ApplicantPersonPublicId)
            return Falla(ErroresLineas("applicantPersonPublicId", "El solicitante no cambia: para otra persona, cree otra solicitud."));

        var armada = await armado.ArmarAsync(e, hoy, ct);
        if (armada.IsFailure) return Falla(ErroresDeOriginacion.ConElPasoParaCorregir(armada.Error));
        var a = armada.Value;
        // 2026-11-27 (feature 028, T023–T024): lo que recoge, validado y proyectado; si el grupo «Recogidas» no se puede corregir y cambia, lo dice la foto.
        var preparadas = await recogidas.PrepararAsync(e, a.Solicitante.Id, a.Linea.Id, hoy, ct);
        if (preparadas.IsFailure) return Falla(preparadas.Error);
        // 2026-11-23 (pedido del dueño): devuelta para corregir —o, desde el 2026-11-25, radicada—, sólo cambia lo que el parámetro de su línea deja
        // (Originacion.GruposEditablesAlDevolver).
        var antes = s.Status is CreditApplicationStatus.Returned || radicada ? await FotoDeSolicitud.TomarAsync(db, s, ct) : null;
        var permitidos = antes is null ? GrupoEditableAlDevolver.Todos : await editables.LeerAsync(s.CreditLineId, hoy, ct);
        var estadoAntes = s.Status;
        var nivelAntes = s.RequiredLevelId;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            ArmadoDeSolicitud.Copiar(s, e, a);
            var partes = await db.CreditApplicationParties.Where(p => p.ApplicationId == s.Id && !p.IsDeleted).ToListAsync(ct);
            var solicitante = partes.Single(p => p.Role == CreditPartyRole.Applicant);
            solicitante.Declarar(e.ApplicantCapacity?.MonthlyIncome, e.ApplicantCapacity?.MonthlyExpenses, e.ApplicantCapacity?.ExternalInstallments);
            var quedan = a.Codeudores.Select(c => c.Persona.Id).ToHashSet();
            foreach (var sale in partes.Where(p => p.Role != CreditPartyRole.Applicant && !quedan.Contains(p.PersonId))) sale.IsDeleted = true;
            var orden = partes.Max(p => p.Order) + 1;
            var agregadas = new List<CreditApplicationParty>();
            foreach (var (persona, entrada) in a.Codeudores)
            {
                var parte = partes.FirstOrDefault(p => p.PersonId == persona.Id && p.Role != CreditPartyRole.Applicant);
                if (parte is null)
                {
                    parte = CreditApplicationParty.Nueva(s.Id, persona.Id, entrada.Role, orden++);
                    db.CreditApplicationParties.Add(parte);
                    agregadas.Add(parte);
                }
                parte.Role = entrada.Role;
                parte.Declarar(entrada.MonthlyIncome, entrada.MonthlyExpenses, entrada.ExternalInstallments);
            }
            // 2026-11-25: con la fecha que el plan pone solo al primer pago, para no anotar como cambio la que llenó la simulación.
            var plan = a.Simulacion.Entrada;
            var despues = antes is null
                ? null
                : FotoDeSolicitud.De(s, partes.Concat(agregadas), preparadas.Value.TextoDeLaFoto) with
                {
                    PrimerPagoPorDefecto = FotoDeSolicitud.PrimerPagoSinFecha(plan.FechaDesembolso, plan.Periodicidad, plan.DiaDePago),
                };
            if (antes is not null)
            {
                var fuera = antes.GruposDistintos(despues!) & ~permitidos;
                if (fuera != GrupoEditableAlDevolver.Ninguno)
                    return Falla(ErroresDeOriginacion.NoCorregible(GruposEditablesAlDevolver.Separar(fuera), radicada));
            }
            var autorizadas = await ArmadoDeSolicitud.AutorizacionesAsync(autorizaciones, e, reloj.UtcNow, ct);
            if (autorizadas.IsFailure) return Falla(autorizadas.Error);
            if (!radicada)
            {
                await recogidas.ReescribirAsync(s.Id, preparadas.Value, ct);
                await db.SaveChangesAsync(ct);
                return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, hoy, ct));
            }
            return await EditarRadicadaAsync(s, a, preparadas.Value, antes!, despues!, agregadas, estadoAntes, nivelAntes, verificada!, request.EditReason!, ct);
        }, ct);
    }

    /// <summary>
    /// 2026-11-25: el resto de la edición de una radicada, dentro de la transacción: la autorización de los codeudores nuevos (como al radicar), el
    /// guardado, las validaciones y el enrutamiento de nuevo (lo que niega la radicación también niega la edición), la fila del historial, la
    /// intervención y el evento de auditoría. Sin nada distinto no escribe nada.
    /// </summary>
    private async Task<Result<CreditApplicationDto>> EditarRadicadaAsync(CreditApplication s, SolicitudArmada a, RecogidasPreparadas preparadas, FotoDeSolicitud antes,
        FotoDeSolicitud despues, IReadOnlyList<CreditApplicationParty> agregadas, CreditApplicationStatus estadoAntes, int? nivelAntes,
        IntervencionVerificada verificada, string motivo, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var cambios = await ConsultasDeSolicitudes.CambiosAsync(db, antes, despues, ct);
        if (cambios.Count == 0)
        {
            db.DescartarCambios();
            return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, hoy, ct));
        }

        // Como al radicar: cada codeudor nuevo trae su autorización para el estudio del crédito antes de consultar sus datos.
        foreach (var parte in agregadas)
        {
            var persona = a.Codeudores.First(c => c.Persona.Id == parte.PersonId).Persona;
            var autorizacion = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
            if (autorizacion is not { Autoriza: true })
                return Falla(ErroresDeOriginacion.SinAutorizacion(persona.PublicId, ReferenciasDeGobierno.Nombre(persona), DataAuthorizationPurpose.CreditProcessing));
        }

        var deudores = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).Select(p => p.PersonId)
            .ToListAsync(ct);
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync(deudores.Concat(agregadas.Select(p => p.PersonId)).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

        s.EditsAfterSubmission++;
        // 2026-11-27 (feature 028): las recogidas quedan con lo de ahora antes de volver a correr las validaciones, que las leen de sus filas.
        await recogidas.ReescribirAsync(s.Id, preparadas, ct);
        await db.SaveChangesAsync(ct);

        var cargada = await evaluacion.CargarAsync(s, ct);
        if (cargada.IsFailure) return Falla(cargada.Error);
        var c = cargada.Value;
        if (!c.Simulacion.Usura.Pasa)
            return Falla(ErroresDeLineasDeCredito.UsuraExcedida("amount", c.Simulacion.Fecha, c.Simulacion.Usura, c.Simulacion.Combinacion.ModalityCode));
        if (vivienda is not null
            && await Housing.ValidacionDeVivienda.SolicitudAsync(db, vivienda, s, c.Simulacion, c.Simulacion.Entrada.Monto, hoy, ct) is { IsFailure: true } ley546)
            return Falla(ley546.Error);
        var evaluada = await evaluacion.EvaluarAsync(c, CheckStage.BeforeDecision, null, SubmitCreditApplicationCommandHandler.DetenerAlRadicar, ct);
        if (evaluada.IsFailure) return Falla(evaluada.Error);
        enrutamiento.Aplicar(s, evaluada.Value.Enrutamiento);

        db.CreditApplicationEdits.Add(CreditApplicationEdit.Registrar(s.Id, estadoAntes, verificada.UsuarioId, reloj.UtcNow, motivo,
            System.Text.Json.JsonSerializer.Serialize(cambios, Json), nivelAntes, s.RequiredLevelId));
        intervenciones.Registrar(verificada, SubmitCreditApplicationCommandHandler.Expediente, s.PublicId);
        await db.SaveChangesAsync(ct);
        await auditoria.EmitirSolicitudRadicadaEditadaAsync(s.PublicId, new
        {
            number = s.Number,
            status = estadoAntes.ToString(),
            reason = motivo.Trim(),
            changes = cambios,
            levelBefore = nivelAntes,
            levelAfter = s.RequiredLevelId,
            routingSha256 = s.RoutingSha256,
        }, ct);
        return Result.Success(await ConsultasDeSolicitudes.DtoAsync(db, s, hoy, ct));
    }

    private static Result<CreditApplicationDto> Falla(Error e) => Result.Failure<CreditApplicationDto>(e);

    private static Error ErroresLineas(string campo, string mensaje) => ErroresDeLineasDeCredito.Invalido(campo, mensaje);
}

/// <summary>
/// Adjunta un documento a la solicitud (api.md §4.2, <c>POST /{id}/documents</c>, con clave; T81): el adjunto ya subido con dueño
/// <c>LoanApplicationDocument</c> y la solicitud, y el requisito de la línea por su código (<c>Lending.Application.RequirementUnknown</c>); el
/// documento de un codeudor nombra a la persona. Se adjunta mientras la solicitud no esté decidida (<c>.NotEditable</c>). (nuevo)
/// </summary>
public sealed record AttachCreditApplicationDocumentCommand(
    Guid ApplicationPublicId,
    string? RequirementCode,
    Guid AttachmentPublicId,
    DateOnly? DocumentDate = null,
    Guid? PersonPublicId = null) : IRequest<Result<CreditApplicationDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AttachCreditApplicationDocumentCommandValidator : AbstractValidator<AttachCreditApplicationDocumentCommand>
{
    public AttachCreditApplicationDocumentCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleFor(x => x.AttachmentPublicId).NotEmpty();
        RuleFor(x => x.RequirementCode).MaximumLength(20);
    }
}

public sealed class AttachCreditApplicationDocumentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    GruposEditablesDeSolicitud editables) : IRequestHandler<AttachCreditApplicationDocumentCommand, Result<CreditApplicationDocumentDto>>
{
    public async Task<Result<CreditApplicationDocumentDto>> Handle(AttachCreditApplicationDocumentCommand request, CancellationToken ct)
    {
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var s = leida.Value;
        if (s.Decidida) return Falla(ErroresDeOriginacion.NoEditable(s.Status.ToString()));
        if (await editables.ExigirAsync(s, GrupoEditableAlDevolver.Documentos, reloj.HoyLocal, ct) is { } fijo) return Falla(fijo);

        CreditLineDocumentRequirement? requisito = null;
        if (!string.IsNullOrWhiteSpace(request.RequirementCode))
        {
            var codigo = request.RequirementCode.Trim().ToUpperInvariant();
            requisito = await db.CreditLineDocumentRequirements.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CreditLineVersionId == s.CreditLineVersionId && r.Code == codigo, ct);
            if (requisito is null) return Falla(ErroresDeOriginacion.RequisitoDesconocido(codigo));
        }

        var adjunto = await db.Attachments.AsNoTracking().AnyAsync(x => x.PublicId == request.AttachmentPublicId && !x.IsDeleted
            && x.OwnerEntityType == AdjuntosDeModulo.DocumentoDeSolicitud && x.OwnerEntityPublicId == s.PublicId
            && x.Status == Domain.Enums.Core.EstadoDeAdjunto.Available, ct);
        if (!adjunto)
            return Falla(new Error(AttachmentErrorCodes.NotAvailable,
                "El archivo no existe, no es de esta solicitud o no terminó de subirse. Súbalo otra vez como documento de la solicitud."));

        int? parte = null;
        if (request.PersonPublicId is { } persona)
        {
            parte = await (from p in db.CreditApplicationParties.AsNoTracking()
                           join q in db.People.AsNoTracking() on p.PersonId equals q.Id
                           where p.ApplicationId == s.Id && !p.IsDeleted && q.PublicId == persona
                           select (int?)p.Id).FirstOrDefaultAsync(ct);
            if (parte is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        }

        var documento = CreditApplicationDocument.Nuevo(s.Id, requisito?.Id, parte, request.AttachmentPublicId, request.DocumentDate);
        db.CreditApplicationDocuments.Add(documento);
        await db.SaveChangesAsync(ct);
        return Result.Success(new CreditApplicationDocumentDto(documento.PublicId, requisito?.Code, requisito?.Name ?? "Documento adicional",
            requisito?.IsMandatory ?? false, request.PersonPublicId, documento.AttachmentPublicId, documento.DocumentDate, null));
    }

    private static Result<CreditApplicationDocumentDto> Falla(Error e) => Result.Failure<CreditApplicationDocumentDto>(e);
}

/// <summary>
/// Quita un documento de una solicitud que todavía no se decidió (api.md §4.2, <c>DELETE /{id}/documents/{documentId}</c>, con clave): baja
/// lógica de la fila; el archivo se conserva (nadie borra adjuntos por su cuenta). Decidida, <c>Attachments.OwnerLocked</c> (T81). (nuevo)
/// </summary>
public sealed record RemoveCreditApplicationDocumentCommand(Guid ApplicationPublicId, Guid DocumentPublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveCreditApplicationDocumentCommandValidator : AbstractValidator<RemoveCreditApplicationDocumentCommand>
{
    public RemoveCreditApplicationDocumentCommandValidator()
    {
        RuleFor(x => x.ApplicationPublicId).NotEmpty();
        RuleFor(x => x.DocumentPublicId).NotEmpty();
    }
}

public sealed class RemoveCreditApplicationDocumentCommandHandler(
    IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj, GruposEditablesDeSolicitud editables)
    : IRequestHandler<RemoveCreditApplicationDocumentCommand, Result>
{
    public async Task<Result> Handle(RemoveCreditApplicationDocumentCommand request, CancellationToken ct)
    {
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return Result.Failure(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure(leida.Error);
        var s = leida.Value;
        var documento = await db.CreditApplicationDocuments.FirstOrDefaultAsync(d => d.PublicId == request.DocumentPublicId && d.ApplicationId == s.Id
                                                                                     && !d.IsDeleted, ct);
        if (documento is null) return Result.Failure(Error.NotFound);
        if (s.Decidida) return Result.Failure(AdjuntosDeModulo.SolicitudBloqueada());
        if (await editables.ExigirAsync(s, GrupoEditableAlDevolver.Documentos, reloj.HoyLocal, ct) is { } fijo) return Result.Failure(fijo);

        documento.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// El usuario de quien pide con su persona del maestro (T66): en Cartera quien arma, radica, decide o desembolsa la tiene. Sin usuario,
/// <c>Lending.Actor.Unauthorized</c>; sin persona, <c>Lending.Security.UserWithoutPerson</c>. (nuevo)
/// </summary>
public static class UsuarioConPersona
{
    public static async Task<Result<int>> LeerAsync(IApplicationDbContext db, IActorActual actorActual, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<int>(ErroresDeGobierno.SinUsuario());
        var persona = await db.Users.AsNoTracking().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        return persona is null
            ? Result.Failure<int>(new ErrorConDatos(RegistroDeIntervenciones.CodigoSinPersona,
                "Su usuario no tiene una persona asociada, y en Cartera quien radica, decide, vota o desembolsa debe tenerla.",
                new { howToFix = "Pida al administrador que asocie su usuario a su persona del maestro en Seguridad > Usuarios." }))
            : Result.Success(usuario);
    }
}
