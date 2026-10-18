using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Agreements;

// Feature 013, K2 (US10, bloque B; T0833; api.md §13.1; data-model §24.1–§24.4; FR-108, FR-112–FR-116; I13, C84): pagadurías y convenios de
// libranza. Una pagaduría es una persona jurídica del maestro; el convenio guarda sólo su código, la pagaduría, si es la nómina propia y su estado;
// lo que cambia con el tiempo vive en versiones sin cruces. Todo comando de escritura lleva clave de pantalla. (nuevo)

/// <summary>Un código de respuesta de la pagaduría y su traducción (data-model §24.4). (nuevo)</summary>
public sealed record PayrollResultCodeInput(string Code, string Description, PayrollResultOutcome Outcome, NonDeductionReason? Reason = null);

/// <summary>
/// Una versión del convenio (api.md §13.1, data-model §24.2): fechas de la nómina de la pagaduría (corte, envío —en las reglas del convenio— y
/// pago), antelación del archivo, formatos de ida y vuelta por referencia (ámbitos 5 y 6), tope propio como fracción (0,4 = 40 %), si lleva lo
/// vencido, las novedades que reporta, la ventana de giro, la regla del no aplicado, la forma de pago alterna, la periodicidad, los códigos de
/// respuesta y el motivo. (nuevo)
/// </summary>
public sealed record PayrollAgreementVersionInput(
    DateOnly ValidFrom,
    int CutoffDay,
    int PayDay,
    Guid OutboundFormatPublicId,
    Guid ResultFormatPublicId,
    string Reason,
    int? SendDay = null,
    int SendLeadDays = 0,
    decimal? DeductionCap = null,
    bool IncludesPastDue = false,
    IReadOnlyList<string>? NoveltyRules = null,
    int? TransferWindowDays = null,
    NonDeductionFollowUpAction? NotAppliedAction = null,
    RepaymentChannel? AlternateRepaymentChannel = null,
    RepaymentPeriodicity? Periodicity = null,
    IReadOnlyList<PayrollResultCodeInput>? ResultCodes = null,
    string? Notes = null);

/// <summary>Un contacto del convenio: una persona del maestro con su papel (data-model §24.3). (nuevo)</summary>
public sealed record PayrollAgreementContactInput(Guid PersonPublicId, string? Role = null, bool IsPrimary = false);

/// <summary>La forma de una versión (Principio VIII). (nuevo)</summary>
public sealed class PayrollAgreementVersionInputValidator : AbstractValidator<PayrollAgreementVersionInput>
{
    public PayrollAgreementVersionInputValidator()
    {
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige la versión.");
        RuleFor(x => x.CutoffDay).InclusiveBetween(1, 31).WithMessage("El día de corte va de 1 a 31.");
        RuleFor(x => x.PayDay).InclusiveBetween(1, 31).WithMessage("El día de pago va de 1 a 31.");
        RuleFor(x => x.SendDay).InclusiveBetween(1, 31).When(x => x.SendDay is not null).WithMessage("El día de envío va de 1 a 31.");
        RuleFor(x => x.SendLeadDays).InclusiveBetween(0, 60).WithMessage("La antelación del archivo va de 0 a 60 días.");
        RuleFor(x => x.OutboundFormatPublicId).NotEmpty().WithMessage("Elija el formato de ida del archivo de descuentos.");
        RuleFor(x => x.ResultFormatPublicId).NotEmpty().WithMessage("Elija el formato de vuelta de la respuesta de la pagaduría.");
        RuleFor(x => x.DeductionCap).GreaterThan(0m).LessThanOrEqualTo(1m).When(x => x.DeductionCap is not null)
            .WithMessage("El tope propio es una fracción mayor que cero y hasta uno (0,4 = 40 %).");
        RuleFor(x => x.TransferWindowDays).GreaterThanOrEqualTo(1).When(x => x.TransferWindowDays is not null)
            .WithMessage("La ventana de giro es de al menos un día.");
        RuleFor(x => x.NotAppliedAction).Must(a => a is null || PayrollAgreementVersion.EsReglaDeNoAplicado(a.Value))
            .WithMessage("La regla del no aplicado es IncludedInNextFile, MovedToCashPayment o SentToCollection.");
        RuleFor(x => x.AlternateRepaymentChannel).Must(c => c is null or RepaymentChannel.Cashier or RepaymentChannel.DirectDebit)
            .WithMessage("La forma de pago alterna es la caja o el débito automático.");
        RuleFor(x => x.Periodicity).IsInEnum().When(x => x.Periodicity is not null);
        RuleForEach(x => x.NoveltyRules).Must(r => Enum.TryParse<PayrollNoveltyKind>(r, ignoreCase: false, out _))
            .WithMessage("Las novedades que reporta la pagaduría son New, Change, Cancellation o Unchanged.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo de la versión.").MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleForEach(x => x.ResultCodes).ChildRules(c =>
        {
            c.RuleFor(y => y.Code).NotEmpty().MaximumLength(20).WithMessage("El código de respuesta tiene hasta 20 caracteres.");
            c.RuleFor(y => y.Description).NotEmpty().MaximumLength(200);
            c.RuleFor(y => y.Outcome).IsInEnum();
            c.RuleFor(y => y.Reason).IsInEnum().When(y => y.Reason is not null);
            c.RuleFor(y => y).Must(y => y.Outcome == PayrollResultOutcome.Deducted ? y.Reason is null : y.Reason is not null)
                .WithName("reason").WithMessage("Un código de no descuento (total o parcial) lleva su motivo; uno de descuento completo, ninguno.");
        });
    }
}

/// <summary>
/// El alta de una versión del convenio (T0833), común a crear el convenio, versionarlo e importar la plantilla: formatos de los ámbitos de
/// libranza (<c>Core.BankFileFormat.ScopeMismatch</c>), tope y ventana nunca menos estrictos que la ley (<c>Lending.Parameter.BelowLegalFloor</c>),
/// sin cruce con la anterior (<c>Lending.PayrollAgreement.VersionOverlaps</c>), y la anterior que ya respaldó un archivo enviado no se recorta
/// (<c>.VersionInUse</c>). Registrar la nueva cierra la anterior la víspera. No guarda. (nuevo)
/// </summary>
public sealed class AltaDeVersionDeConvenio(IApplicationDbContext db, IValoresNacionales valores)
{
    /// <summary>Verifica la versión contra lo registrado. Devuelve los Id de los dos formatos.</summary>
    public async Task<Result<(int Ida, int Vuelta)>> VerificarAsync(int? convenioId, PayrollAgreementVersionInput v, CancellationToken ct)
    {
        var formatos = await db.BankFileFormats.AsNoTracking()
            .Where(f => (f.PublicId == v.OutboundFormatPublicId || f.PublicId == v.ResultFormatPublicId) && !f.IsDeleted)
            .Select(f => new { f.Id, f.PublicId, f.Code, f.Scope }).ToListAsync(ct);
        var ida = formatos.FirstOrDefault(f => f.PublicId == v.OutboundFormatPublicId);
        var vuelta = formatos.FirstOrDefault(f => f.PublicId == v.ResultFormatPublicId);
        if (ida is null || vuelta is null) return Result.Failure<(int, int)>(BankFileFormatErrors.NotFound);
        if (ida.Scope != BankFileScope.PayrollDeductionRequest)
            return Result.Failure<(int, int)>(BankFileFormatErrors.ScopeMismatch(ida.Code, ida.Scope, BankFileScope.PayrollDeductionRequest));
        if (vuelta.Scope != BankFileScope.PayrollDeductionResult)
            return Result.Failure<(int, int)>(BankFileFormatErrors.ScopeMismatch(vuelta.Code, vuelta.Scope, BankFileScope.PayrollDeductionResult));

        if (v.DeductionCap is { } tope)
        {
            var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, v.ValidFrom, null, ct);
            if (limite.IsFailure) return Result.Failure<(int, int)>(limite.Error);
            if (limite.Value.Valor is { } legal && !CapacidadDeDescuento.RespetaElLimite(tope, legal))
                return Result.Failure<(int, int)>(ErroresDeParametrosDeCartera.MenosEstrictoQueLaLey(ParametrosDeCartera.LibranzaCapacidadDeDescuento,
                    tope.ToString(CultureInfo.InvariantCulture), CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, legal, limite.Value.Fuente));
        }
        if (v.TransferWindowDays is { } ventana)
        {
            var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.LibranzaVentanaGiroDias, v.ValidFrom, null, ct);
            if (limite.IsFailure) return Result.Failure<(int, int)>(limite.Error);
            if (limite.Value.Valor is { } legal && ventana > legal)
                return Result.Failure<(int, int)>(ErroresDeParametrosDeCartera.MenosEstrictoQueLaLey(ParametrosDeCartera.LibranzaVentanaDeGiroDias,
                    ventana.ToString(CultureInfo.InvariantCulture), CatalogoDeLimitesLegales.LibranzaVentanaGiroDias, legal, limite.Value.Fuente));
        }
        var repetido = (v.ResultCodes ?? []).GroupBy(c => c.Code.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null) return Result.Failure<(int, int)>(ErroresDeLibranza.CodigoDeRespuestaRepetido(repetido.Key));

        if (convenioId is { } id)
        {
            var vigencia = await VerificarVigenciaAsync(id, v.ValidFrom, ct);
            if (vigencia.IsFailure) return Result.Failure<(int, int)>(vigencia.Error);
        }
        return Result.Success((ida.Id, vuelta.Id));
    }

    /// <summary>
    /// Que una versión que empieza el <paramref name="desde"/> no se cruce con las registradas (<c>.VersionOverlaps</c>) ni recorte la que ya respaldó
    /// un archivo enviado (<c>.VersionInUse</c>). Feature 014 (T090): lo comparten las versiones de grupos de archivo y de equivalencias.
    /// </summary>
    public async Task<Result> VerificarVigenciaAsync(int convenioId, DateOnly desde, CancellationToken ct)
    {
        var versiones = await db.PayrollAgreementVersions.AsNoTracking().Where(x => x.PayrollAgreementId == convenioId && !x.IsDeleted).ToListAsync(ct);
        var ultima = versiones.OrderByDescending(x => x.ValidFrom).FirstOrDefault();
        if (ultima is not null && desde <= ultima.ValidFrom) return Result.Failure(ErroresDeLibranza.VersionCruzada(desde, ultima.ValidFrom));
        if (versiones.Any(x => x.ValidTo is { } hasta && hasta >= desde && x.ValidFrom < desde && x.Id != ultima?.Id))
            return Result.Failure(ErroresDeLibranza.VersionCruzada(desde, ultima!.ValidFrom));
        if (ultima is not null)
        {
            // Una versión que respaldó un archivo enviado no se recorta: el período de ese archivo sigue siendo suyo (data-model §24.2).
            var enviados = await db.PayrollDeductionFiles.AsNoTracking()
                .Where(f => f.PayrollAgreementVersionId == ultima.Id && f.Status == PayrollFileStatus.Sent && !f.IsDeleted)
                .Select(f => new { f.Number, f.PeriodYear, f.PeriodMonth }).ToListAsync(ct);
            var cubierto = enviados.Select(f => (f.Number, Hasta: new DateOnly(f.PeriodYear, f.PeriodMonth, 1).AddMonths(1).AddDays(-1)))
                .OrderByDescending(f => f.Hasta).FirstOrDefault();
            if (cubierto.Number is not null && cubierto.Hasta >= desde)
                return Result.Failure(ErroresDeLibranza.VersionEnUso(cubierto.Number, cubierto.Hasta));
        }
        return Result.Success();
    }

    /// <summary>Registra la versión (y sus códigos de respuesta) cerrando la anterior la víspera. Guarda la versión para darles su Id a los códigos.</summary>
    public async Task<PayrollAgreementVersion> RegistrarAsync(int convenioId, PayrollAgreementVersionInput v, (int Ida, int Vuelta) formatos, CancellationToken ct)
    {
        var anterior = await db.PayrollAgreementVersions.Where(x => x.PayrollAgreementId == convenioId && !x.IsDeleted)
            .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(ct);
        if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo >= v.ValidFrom)) anterior.CerrarLaVispera(v.ValidFrom);

        var version = new PayrollAgreementVersion
        {
            PayrollAgreementId = convenioId,
            ValidFrom = v.ValidFrom,
            Periodicity = v.Periodicity ?? RepaymentPeriodicity.Monthly,
            CutoffDay = (byte)v.CutoffDay,
            PaymentDay = (byte)v.PayDay,
            SendLeadDays = v.SendLeadDays,
            OutboundFormatId = formatos.Ida,
            ResultFormatId = formatos.Vuelta,
            DeductionCapFraction = v.DeductionCap,
            IncludesPastDue = v.IncludesPastDue,
            NoveltyRulesJson = new ReglasDelConvenio(v.SendDay, v.NoveltyRules is { Count: > 0 } r ? r : null, null).Serializar(),
            TransferWindowDays = v.TransferWindowDays,
            NonDeductionAction = v.NotAppliedAction ?? ConveniosDeLibranza.AccionPorDefecto,
            AlternateRepaymentChannel = v.AlternateRepaymentChannel ?? RepaymentChannel.Cashier,
            Notes = string.IsNullOrWhiteSpace(v.Notes) ? v.Reason.Trim() : v.Notes.Trim(),
        };
        db.PayrollAgreementVersions.Add(version);
        await db.SaveChangesAsync(ct);
        foreach (var c in v.ResultCodes ?? [])
            db.PayrollAgreementResultCodes.Add(new PayrollAgreementResultCode
            {
                PayrollAgreementVersionId = version.Id,
                PaymasterCode = c.Code.Trim(),
                Description = c.Description.Trim(),
                Outcome = c.Outcome,
                NonDeductionReason = c.Outcome == PayrollResultOutcome.Deducted ? null : c.Reason,
            });
        await db.SaveChangesAsync(ct);
        return version;
    }
}

// --------------------------------------------------------------------------------------------------------------------------- crear --

/// <summary>
/// Crea una pagaduría con su convenio y su primera versión (api.md §13.1, <c>POST /api/lending/payroll-agreements</c>,
/// <c>Lending.PayrollAgreements.Manage</c>, con clave; FR-108, I13). El nombre va en la descripción del convenio. (nuevo)
/// </summary>
public sealed record CreatePayrollAgreementCommand(string Code, string Name, Guid PayerPersonPublicId, bool IsOwnPayroll,
    PayrollAgreementVersionInput FirstVersion) : IRequest<Result<PayrollAgreementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => FirstVersion?.Reason ?? string.Empty;
}

public sealed class CreatePayrollAgreementCommandValidator : AbstractValidator<CreatePayrollAgreementCommand>
{
    public CreatePayrollAgreementCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código del convenio.")
            .MaximumLength(CodigoDeCatalogo.LargoCorto).WithMessage("El código admite hasta 10 caracteres.")
            .Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre del convenio.").MaximumLength(120);
        RuleFor(x => x.PayerPersonPublicId).NotEmpty().WithMessage("Elija la pagaduría en Maestros › Personas.");
        RuleFor(x => x.FirstVersion).NotNull().WithMessage("El convenio nace con su primera versión.")
            .SetValidator(new PayrollAgreementVersionInputValidator()!);
    }
}

/// <summary>
/// Código vivo repetido → <c>Catalogo.CodigoDuplicado</c>; pagaduría inexistente → <c>Core.Person.NotFound</c>, no jurídica →
/// <c>Lending.PayrollAgreement.PayerNotLegalEntity</c>; sin registro de operador de libranza en el perfil vigente →
/// <c>.OperatorRegistrationMissing</c>; la nómina propia es una sola (<c>.OwnPayrollAlreadyExists</c>) y su pagaduría es la cooperativa
/// (<c>.OwnPayrollNotCooperative</c>); la versión, las reglas de <see cref="AltaDeVersionDeConvenio"/>. Todo o nada. (nuevo)
/// </summary>
public sealed class CreatePayrollAgreementCommandHandler(
    IApplicationDbContext db,
    ConveniosDeLibranza convenios,
    AltaDeVersionDeConvenio alta,
    IDateTimeService reloj) : IRequestHandler<CreatePayrollAgreementCommand, Result<PayrollAgreementDto>>
{
    public async Task<Result<PayrollAgreementDto>> Handle(CreatePayrollAgreementCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        var repetido = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.Code == codigo && !a.IsDeleted, ct);
        if (repetido is not null)
            return Falla(CodigoDeCatalogo.Duplicado("un convenio de libranza", codigo, repetido.Description ?? repetido.Code, repetido.PublicId));

        var verificado = await VerificarPagaduriaAsync(db, convenios, request.PayerPersonPublicId, request.IsOwnPayroll, reloj.HoyLocal, ct);
        if (verificado.IsFailure) return Falla(verificado.Error);
        var formatos = await alta.VerificarAsync(null, request.FirstVersion, ct);
        if (formatos.IsFailure) return Falla(formatos.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var convenio = new PayrollAgreement
            {
                Code = codigo,
                PaymasterPersonId = verificado.Value,
                IsOwnPayroll = request.IsOwnPayroll,
                Description = request.Name.Trim(),
            };
            db.PayrollAgreements.Add(convenio);
            await db.SaveChangesAsync(ct);
            await alta.RegistrarAsync(convenio.Id, request.FirstVersion, formatos.Value, ct);
            return Result.Success(await ConsultasDeConvenios.DtoAsync(db, convenio.Id, reloj.HoyLocal, ct));
        }, ct);
    }

    /// <summary>La pagaduría: viva, jurídica, la cooperativa en la nómina propia (única); y la cooperativa registrada como operadora.</summary>
    public static async Task<Result<int>> VerificarPagaduriaAsync(IApplicationDbContext db, ConveniosDeLibranza convenios, Guid pagaduria, bool nominaPropia,
        DateOnly hoy, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == pagaduria && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<int>(ErroresDeGobierno.PersonaInexistente());
        if (!ConveniosDeLibranza.EsJuridica(persona)) return Result.Failure<int>(ErroresDeLibranza.PagadorNoJuridico(ReferenciasDeGobierno.Nombre(persona)));
        if (await convenios.RegistroDeOperadorAsync(hoy, ct) is { } sinRegistro) return Result.Failure<int>(sinRegistro);
        if (nominaPropia)
        {
            if (await convenios.NominaPropiaAsync(ct) is { } existente) return Result.Failure<int>(ErroresDeLibranza.NominaPropiaRepetida(existente.Code));
            var cooperativa = await convenios.PersonaDeLaCooperativaAsync(ct);
            if (cooperativa is null || cooperativa.Id != persona.Id) return Result.Failure<int>(ErroresDeLibranza.NominaPropiaNoEsLaCooperativa());
        }
        return Result.Success(persona.Id);
    }

    private static Result<PayrollAgreementDto> Falla(Error e) => Result.Failure<PayrollAgreementDto>(e);
}

// ----------------------------------------------------------------------------------------------------------------------- versionar --

/// <summary>
/// Registra una versión nueva del convenio (api.md §13.1, <c>POST /{id}/versions</c>, <c>Lending.PayrollAgreements.Manage</c>, con clave): la
/// anterior rige hasta la víspera. Un convenio cerrado no se versiona. (nuevo)
/// </summary>
public sealed record AddPayrollAgreementVersionCommand(Guid AgreementPublicId, PayrollAgreementVersionInput Version)
    : IRequest<Result<PayrollAgreementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public string Reason => Version?.Reason ?? string.Empty;
}

public sealed class AddPayrollAgreementVersionCommandValidator : AbstractValidator<AddPayrollAgreementVersionCommand>
{
    public AddPayrollAgreementVersionCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.Version).NotNull().SetValidator(new PayrollAgreementVersionInputValidator()!);
    }
}

public sealed class AddPayrollAgreementVersionCommandHandler(IApplicationDbContext db, AltaDeVersionDeConvenio alta, IDateTimeService reloj)
    : IRequestHandler<AddPayrollAgreementVersionCommand, Result<PayrollAgreementDto>>
{
    public async Task<Result<PayrollAgreementDto>> Handle(AddPayrollAgreementVersionCommand request, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.AgreementPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<PayrollAgreementDto>(ErroresDeLibranza.ConvenioInexistente());
        if (convenio.Status == PayrollAgreementStatus.Closed)
            return Result.Failure<PayrollAgreementDto>(ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, "está cerrado"));
        var formatos = await alta.VerificarAsync(convenio.Id, request.Version, ct);
        if (formatos.IsFailure) return Result.Failure<PayrollAgreementDto>(formatos.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await alta.RegistrarAsync(convenio.Id, request.Version, formatos.Value, ct);
            return Result.Success(await ConsultasDeConvenios.DtoAsync(db, convenio.Id, reloj.HoyLocal, ct));
        }, ct);
    }
}

// ------------------------------------------------------------------------------------------------------------------------ contactos --

/// <summary>
/// Fija los contactos vigentes del convenio (api.md §13.1, <c>PUT /{id}/contacts</c>, <c>Lending.PayrollAgreements.Manage</c>, con clave): cada
/// contacto es una persona del maestro (Principio V: su correo y teléfono viven allí) con su papel. Los vigentes que no vienen se cierran ayer y
/// los nuevos rigen desde hoy; los que siguen no se tocan. (nuevo)
/// </summary>
public sealed record SetPayrollAgreementContactsCommand(Guid AgreementPublicId, IReadOnlyList<PayrollAgreementContactInput> Contacts)
    : IRequest<Result<IReadOnlyList<PayrollAgreementContactDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetPayrollAgreementContactsCommandValidator : AbstractValidator<SetPayrollAgreementContactsCommand>
{
    public SetPayrollAgreementContactsCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.Contacts).NotNull();
        RuleForEach(x => x.Contacts).ChildRules(c =>
        {
            c.RuleFor(y => y.PersonPublicId).NotEmpty().WithMessage("Elija la persona del contacto en Maestros › Personas.");
            c.RuleFor(y => y.Role).Must(r => r is null || PayrollAgreementContact.Papeles.Contains(r))
                .WithMessage("El papel del contacto es Payroll, Treasury, Legal u Other.");
        });
        RuleFor(x => x.Contacts).Must(c => c.GroupBy(y => (y.PersonPublicId, y.Role ?? "Payroll")).All(g => g.Count() == 1))
            .When(x => x.Contacts is not null).WithMessage("Una persona va una sola vez con el mismo papel.");
    }
}

public sealed class SetPayrollAgreementContactsCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<SetPayrollAgreementContactsCommand, Result<IReadOnlyList<PayrollAgreementContactDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementContactDto>>> Handle(SetPayrollAgreementContactsCommand request, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == request.AgreementPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<IReadOnlyList<PayrollAgreementContactDto>>(ErroresDeLibranza.ConvenioInexistente());
        var pedidas = request.Contacts.Select(c => c.PersonPublicId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => pedidas.Contains(p.PublicId) && !p.IsDeleted).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        if (personas.Count != pedidas.Count) return Result.Failure<IReadOnlyList<PayrollAgreementContactDto>>(ErroresDeGobierno.PersonaInexistente());

        var hoy = reloj.HoyLocal;
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var todos = await db.PayrollAgreementContacts.Where(c => c.PayrollAgreementId == convenio.Id).ToListAsync(ct);
            var vigentes = todos.Where(c => !c.IsDeleted && c.VigenteEn(hoy)).ToList();
            var queQuedan = request.Contacts.Select(c => (Persona: personas[c.PersonPublicId], Papel: c.Role ?? "Payroll", c.IsPrimary)).ToList();
            foreach (var c in vigentes.Where(v => !queQuedan.Any(q => q.Persona == v.PersonId && q.Papel == v.Role)))
            {
                if (c.ValidFrom >= hoy) c.IsDeleted = true; // nació hoy: no hay vigencia que cerrar
                else c.ValidTo = hoy.AddDays(-1);
            }
            foreach (var q in queQuedan)
            {
                var sigue = vigentes.FirstOrDefault(v => v.PersonId == q.Persona && v.Role == q.Papel);
                if (sigue is not null)
                {
                    sigue.IsPrimary = q.IsPrimary;
                    continue;
                }
                // Retirado hoy mismo y vuelto a poner: la misma fila (el único es por persona, papel y desde).
                var deHoy = todos.FirstOrDefault(v => v.PersonId == q.Persona && v.Role == q.Papel && v.ValidFrom == hoy);
                if (deHoy is not null)
                {
                    deHoy.IsDeleted = false;
                    deHoy.ValidTo = null;
                    deHoy.IsPrimary = q.IsPrimary;
                    continue;
                }
                db.PayrollAgreementContacts.Add(new PayrollAgreementContact
                {
                    PayrollAgreementId = convenio.Id, PersonId = q.Persona, Role = q.Papel, IsPrimary = q.IsPrimary, ValidFrom = hoy,
                });
            }
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeConvenios.ContactosAsync(db, convenio.Id, hoy, ct));
        }, ct);
    }
}
