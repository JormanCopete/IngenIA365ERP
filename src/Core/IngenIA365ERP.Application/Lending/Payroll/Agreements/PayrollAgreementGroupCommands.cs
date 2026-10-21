using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll.Agreements;

// Feature 014, K7 (US3, T090; api.md §18.3; data-model §1.4.1, §1.4.2; FR-125, E-05): los grupos de archivo y las equivalencias de concepto de un
// convenio de pagaduría. Los dos cuelgan de la versión del convenio, que es inmutable una vez respalda un archivo enviado: fijarlos registra una
// versión nueva que copia la vigente (sus fechas, reglas y códigos de respuesta) y reemplaza sólo lo que se fija. (nuevo)

/// <summary>Un grupo de archivo: código, nombre, formatos de ida y vuelta (ámbitos de libranza, genéricos) y calendario propio opcional. (nuevo)</summary>
public sealed record PayrollFileGroupInput(
    string Code,
    string Name,
    Guid OutboundFormatPublicId,
    Guid ResultFormatPublicId,
    int? CutoffDay = null,
    int? PayDay = null,
    int? SendLeadDays = null);

/// <summary>Un ítem de una equivalencia: naturaleza, línea (o «*»), concepto (o «Installment») y prioridad. (nuevo)</summary>
public sealed record PayrollEquivalenceItemInput(LineNature Nature, string LineCode, string Concept, int Priority = 1);

/// <summary>Un código de la pagaduría con los ítems de Cartera que agrupa, su grupo de archivo, su regla de reparto y su prelación. (nuevo)</summary>
public sealed record PayrollConceptEquivalenceInput(
    string PaymasterConceptCode,
    string Description,
    ConceptAllocationRule Split,
    IReadOnlyList<PayrollEquivalenceItemInput> Members,
    string? FileGroupCode = null,
    int Order = 1);

/// <summary>Un formato por referencia: su id público y su código. (nuevo)</summary>
public sealed record BankFileFormatRefDto(Guid PublicId, string Code);

/// <summary>El calendario de un grupo; nulo en un campo = el de la versión. (nuevo)</summary>
public sealed record PayrollFileGroupCalendarDto(int? CutoffDay, int? SendLeadDays, int? PayDay);

/// <summary>Un grupo de archivo de la versión que rige (api.md §18.3). (nuevo)</summary>
public sealed record PayrollAgreementFileGroupDto(
    Guid PublicId,
    string Code,
    string Name,
    BankFileFormatRefDto OutboundFormat,
    BankFileFormatRefDto ResultFormat,
    PayrollFileGroupCalendarDto Calendar,
    int Order,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>Un ítem de una equivalencia en la respuesta. (nuevo)</summary>
public sealed record PayrollEquivalenceMemberDto(string LineCode, string Nature, string Concept, int Priority);

/// <summary>Una equivalencia de la versión que rige (api.md §18.3). (nuevo)</summary>
public sealed record PayrollAgreementConceptEquivalenceDto(
    Guid PublicId,
    string PaymasterConceptCode,
    string Description,
    string? FileGroupCode,
    IReadOnlyList<PayrollEquivalenceMemberDto> Members,
    string Split,
    int Order,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>
/// La versión nueva de un convenio que copia la vigente para cambiar sus grupos o sus equivalencias (T090): misma periodicidad, fechas, tope,
/// reglas, ventana, regla del no aplicado y códigos de respuesta; la anterior rige hasta la víspera. No guarda la versión: la deja agregada. (nuevo)
/// </summary>
public sealed class CopiaDeVersionDeConvenio(IApplicationDbContext db, AltaDeVersionDeConvenio alta)
{
    public async Task<Result<(PayrollAgreementVersion Anterior, PayrollAgreementVersion Nueva)>> CopiarAsync(Guid convenioPublicId, DateOnly desde,
        string motivo, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == convenioPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Falla(ErroresDeLibranza.ConvenioInexistente());
        if (convenio.Status == PayrollAgreementStatus.Closed) return Falla(ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, "está cerrado"));
        if (convenio.IsOwnPayroll) return Falla(ErroresDeLibranza.NominaPropiaSinArchivo(convenio.Code));
        var anterior = await db.PayrollAgreementVersions.Where(v => v.PayrollAgreementId == convenio.Id && !v.IsDeleted)
            .OrderByDescending(v => v.ValidFrom).FirstOrDefaultAsync(ct);
        if (anterior is null) return Falla(ErroresDeLibranza.SinVersionVigente(convenio.Code, desde));
        var vigencia = await alta.VerificarVigenciaAsync(convenio.Id, desde, ct);
        if (vigencia.IsFailure) return Falla(vigencia.Error);

        if (anterior.ValidTo is null || anterior.ValidTo >= desde) anterior.CerrarLaVispera(desde);
        var nueva = new PayrollAgreementVersion
        {
            PayrollAgreementId = convenio.Id,
            ValidFrom = desde,
            Periodicity = anterior.Periodicity,
            CutoffDay = anterior.CutoffDay,
            PaymentDay = anterior.PaymentDay,
            SendLeadDays = anterior.SendLeadDays,
            OutboundFormatId = anterior.OutboundFormatId,
            ResultFormatId = anterior.ResultFormatId,
            UsesFileGroups = anterior.UsesFileGroups,
            EmployeeIdentification = anterior.EmployeeIdentification,
            IncludesAccountInstallments = anterior.IncludesAccountInstallments,
            DeductionCapFraction = anterior.DeductionCapFraction,
            IncludesPastDue = anterior.IncludesPastDue,
            NoveltyRulesJson = anterior.NoveltyRulesJson,
            TransferWindowDays = anterior.TransferWindowDays,
            NonDeductionAction = anterior.NonDeductionAction,
            AlternateRepaymentChannel = anterior.AlternateRepaymentChannel,
            Notes = motivo.Trim(),
        };
        db.PayrollAgreementVersions.Add(nueva);
        await db.SaveChangesAsync(ct);
        foreach (var c in await db.PayrollAgreementResultCodes.AsNoTracking().Where(r => r.PayrollAgreementVersionId == anterior.Id && !r.IsDeleted).ToListAsync(ct))
            db.PayrollAgreementResultCodes.Add(new PayrollAgreementResultCode
            {
                PayrollAgreementVersionId = nueva.Id,
                PaymasterCode = c.PaymasterCode,
                Description = c.Description,
                Outcome = c.Outcome,
                NonDeductionReason = c.NonDeductionReason,
            });
        return Result.Success((anterior, nueva));
    }

    /// <summary>Los grupos y las equivalencias de una versión, para copiarlos o validarlos.</summary>
    public async Task<(List<PayrollAgreementFileGroup> Grupos, List<PayrollAgreementConceptEquivalence> Equivalencias)> DeLaVersionAsync(int versionId,
        CancellationToken ct) =>
        (await db.PayrollAgreementFileGroups.AsNoTracking().Where(g => g.PayrollAgreementVersionId == versionId && !g.IsDeleted).OrderBy(g => g.Order).ToListAsync(ct),
         await db.PayrollAgreementConceptEquivalences.AsNoTracking().Where(e => e.PayrollAgreementVersionId == versionId && !e.IsDeleted)
             .OrderBy(e => e.Priority).ThenBy(e => e.PaymasterConceptCode).ToListAsync(ct));

    /// <summary>La última versión del convenio con sus grupos y equivalencias, o nula si el convenio no existe o no tiene versiones.</summary>
    public async Task<(PayrollAgreementVersion Version, List<PayrollAgreementFileGroup> Grupos, List<PayrollAgreementConceptEquivalence> Equivalencias)?> ActualAsync(
        Guid convenioPublicId, CancellationToken ct)
    {
        var version = await db.PayrollAgreementVersions.AsNoTracking()
            .Where(v => !v.IsDeleted && db.PayrollAgreements.Any(a => a.Id == v.PayrollAgreementId && a.PublicId == convenioPublicId && !a.IsDeleted))
            .OrderByDescending(v => v.ValidFrom).FirstOrDefaultAsync(ct);
        if (version is null) return null;
        var (grupos, equivalencias) = await DeLaVersionAsync(version.Id, ct);
        return (version, grupos, equivalencias);
    }

    /// <summary>
    /// Valida las equivalencias antes de escribir nada: códigos únicos, las reglas puras (<see cref="EquivalenciasDePagaduria.Validar"/>) y, con grupos,
    /// que cada una vaya en uno de <paramref name="codigosDeGrupo"/> (<c>.LineWithoutGroup</c>); sin grupos, ninguna lleva grupo.
    /// </summary>
    public static Result ValidarEquivalencias(bool usaGrupos, IReadOnlyCollection<string> codigosDeGrupo, IReadOnlyList<EquivalenciaAFijar> equivalencias)
    {
        var repetida = equivalencias.GroupBy(e => e.Codigo, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null) return Result.Failure(ErroresDeLibranza.EquivalenciaRepetida(repetida.Key));
        var problema = EquivalenciasDePagaduria.Validar(equivalencias.Select(e => new EquivalenciaDePagaduria(e.Codigo, e.Regla, e.Prioridad, e.Items)).ToList());
        if (problema is not null) return Result.Failure(ErroresDeLibranza.DeEquivalencias(problema));
        foreach (var e in equivalencias)
        {
            if (usaGrupos && (e.Grupo is null || !codigosDeGrupo.Contains(e.Grupo, StringComparer.OrdinalIgnoreCase)))
                return Result.Failure(ErroresDeLibranza.LineaSinGrupo(e.Codigo, e.Grupo));
            if (!usaGrupos && e.Grupo is not null) return Result.Failure(ErroresDeLibranza.LineaSinGrupo(e.Codigo, e.Grupo));
        }
        return Result.Success();
    }

    /// <summary>Agrega a la versión las equivalencias ya validadas, con su grupo resuelto por código entre los grupos ya guardados.</summary>
    public void AgregarEquivalencias(PayrollAgreementVersion version, IReadOnlyList<PayrollAgreementFileGroup> grupos, IReadOnlyList<EquivalenciaAFijar> equivalencias)
    {
        var porCodigo = grupos.ToDictionary(g => g.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var e in equivalencias)
        {
            db.PayrollAgreementConceptEquivalences.Add(new PayrollAgreementConceptEquivalence
            {
                PayrollAgreementVersionId = version.Id,
                FileGroupId = e.Grupo is not null && porCodigo.TryGetValue(e.Grupo, out var grupo) ? grupo.Id : null,
                PaymasterConceptCode = e.Codigo,
                Description = e.Descripcion,
                AllocationRule = e.Regla,
                Priority = e.Prioridad,
                ItemsJson = EquivalenciasDePagaduria.Serializar(e.Items),
            });
        }
    }

    /// <summary>Las equivalencias de una versión como las fija el comando, con el código de su grupo.</summary>
    public static IReadOnlyList<EquivalenciaAFijar> Fijables(IReadOnlyList<PayrollAgreementFileGroup> grupos, IEnumerable<PayrollAgreementConceptEquivalence> equivalencias)
    {
        var codigoDeGrupo = grupos.ToDictionary(g => g.Id, g => g.Code);
        return equivalencias.Select(e => new EquivalenciaAFijar(e.PaymasterConceptCode, e.Description, e.AllocationRule, e.Priority,
            e.FileGroupId is { } gid ? codigoDeGrupo.GetValueOrDefault(gid) : null, EquivalenciasDePagaduria.Leer(e.ItemsJson))).ToList();
    }

    private static Result<(PayrollAgreementVersion, PayrollAgreementVersion)> Falla(Error e) => Result.Failure<(PayrollAgreementVersion, PayrollAgreementVersion)>(e);
}

/// <summary>Una equivalencia lista para validar y agregar: código, descripción, regla, prelación, código del grupo e ítems. (nuevo)</summary>
public sealed record EquivalenciaAFijar(string Codigo, string Descripcion, ConceptAllocationRule Regla, int Prioridad, string? Grupo,
    IReadOnlyList<ItemDeEquivalencia> Items);

// ----------------------------------------------------------------------------------------------------------------- grupos de archivo --

/// <summary>
/// Fija los grupos de archivo del convenio desde <see cref="ValidFrom"/> (api.md §18.3, <c>POST /{id}/file-groups</c>, <c>PayrollAgreements.Manage</c>,
/// con clave): registra una versión nueva que usa grupos (los formatos de la versión dejan de usarse) con los grupos dados, y copia las
/// equivalencias de la vigente con su grupo por código (<c>.LineWithoutGroup</c> si alguna queda sin el suyo). Formatos de los ámbitos de
/// libranza (<c>Core.BankFileFormat.ScopeMismatch</c>). (nuevo)
/// </summary>
public sealed record SetPayrollAgreementFileGroupsCommand(Guid AgreementPublicId, DateOnly ValidFrom, IReadOnlyList<PayrollFileGroupInput> Groups, string Reason)
    : IRequest<Result<IReadOnlyList<PayrollAgreementFileGroupDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetPayrollAgreementFileGroupsCommandValidator : AbstractValidator<SetPayrollAgreementFileGroupsCommand>
{
    public SetPayrollAgreementFileGroupsCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rigen los grupos.");
        RuleFor(x => x.Groups).NotEmpty().WithMessage("Registre al menos un grupo de archivo.");
        RuleForEach(x => x.Groups).ChildRules(g =>
        {
            g.RuleFor(y => y.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
            g.RuleFor(y => y.Name).NotEmpty().MaximumLength(120).WithMessage("El nombre del grupo tiene hasta 120 caracteres.");
            g.RuleFor(y => y.OutboundFormatPublicId).NotEmpty().WithMessage("Elija el formato de ida del grupo.");
            g.RuleFor(y => y.ResultFormatPublicId).NotEmpty().WithMessage("Elija el formato de vuelta del grupo.");
            g.RuleFor(y => y.CutoffDay).InclusiveBetween(1, 31).When(y => y.CutoffDay is not null).WithMessage("El día de corte va de 1 a 31.");
            g.RuleFor(y => y.PayDay).InclusiveBetween(1, 31).When(y => y.PayDay is not null).WithMessage("El día de pago va de 1 a 31.");
            g.RuleFor(y => y.SendLeadDays).InclusiveBetween(0, 60).When(y => y.SendLeadDays is not null).WithMessage("La antelación va de 0 a 60 días.");
        });
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo del cambio.").MaximumLength(500);
    }
}

public sealed class SetPayrollAgreementFileGroupsCommandHandler(IApplicationDbContext db, CopiaDeVersionDeConvenio copia)
    : IRequestHandler<SetPayrollAgreementFileGroupsCommand, Result<IReadOnlyList<PayrollAgreementFileGroupDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementFileGroupDto>>> Handle(SetPayrollAgreementFileGroupsCommand request, CancellationToken ct)
    {
        var codigos = request.Groups.Select(g => CodigoDeCatalogo.Normalizar(g.Code)!).ToList();
        if (codigos.GroupBy(c => c, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            return Falla(ErroresDeLibranza.GrupoRepetido(repetido.Key));
        var ids = request.Groups.SelectMany(g => new[] { g.OutboundFormatPublicId, g.ResultFormatPublicId }).Distinct().ToList();
        var formatos = await db.BankFileFormats.AsNoTracking().Where(f => ids.Contains(f.PublicId) && !f.IsDeleted)
            .ToDictionaryAsync(f => f.PublicId, ct);
        foreach (var g in request.Groups)
        {
            if (!formatos.TryGetValue(g.OutboundFormatPublicId, out var ida) || !formatos.TryGetValue(g.ResultFormatPublicId, out var vuelta))
                return Falla(BankFileFormatErrors.NotFound);
            if (ida.Scope != BankFileScope.PayrollDeductionRequest)
                return Falla(BankFileFormatErrors.ScopeMismatch(ida.Code, ida.Scope, BankFileScope.PayrollDeductionRequest));
            if (vuelta.Scope != BankFileScope.PayrollDeductionResult)
                return Falla(BankFileFormatErrors.ScopeMismatch(vuelta.Code, vuelta.Scope, BankFileScope.PayrollDeductionResult));
        }

        // Las equivalencias de la vigente pasan a la nueva con su grupo por código: se valida antes de escribir nada.
        var actual = await copia.ActualAsync(request.AgreementPublicId, ct);
        var equivalencias = actual is { } a ? CopiaDeVersionDeConvenio.Fijables(a.Grupos, a.Equivalencias) : [];
        var validas = CopiaDeVersionDeConvenio.ValidarEquivalencias(usaGrupos: true, codigos, equivalencias);
        if (validas.IsFailure) return Falla(validas.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var versiones = await copia.CopiarAsync(request.AgreementPublicId, request.ValidFrom, request.Reason, ct);
            if (versiones.IsFailure) return Falla(versiones.Error);
            var (_, nueva) = versiones.Value;
            nueva.UsesFileGroups = true;
            nueva.OutboundFormatId = null;
            nueva.ResultFormatId = null;
            var grupos = request.Groups.Select((g, i) => new PayrollAgreementFileGroup
            {
                PayrollAgreementVersionId = nueva.Id,
                Code = codigos[i],
                Name = g.Name.Trim(),
                OutboundFormatId = formatos[g.OutboundFormatPublicId].Id,
                ResultFormatId = formatos[g.ResultFormatPublicId].Id,
                CutoffDay = (byte?)g.CutoffDay,
                PaymentDay = (byte?)g.PayDay,
                SendLeadDays = g.SendLeadDays,
                Order = i + 1,
            }).ToList();
            db.PayrollAgreementFileGroups.AddRange(grupos);
            await db.SaveChangesAsync(ct);

            copia.AgregarEquivalencias(nueva, grupos, equivalencias);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeGruposDeConvenio.GruposAsync(db, nueva, ct));
        }, ct);
    }

    private static Result<IReadOnlyList<PayrollAgreementFileGroupDto>> Falla(Error e) => Result.Failure<IReadOnlyList<PayrollAgreementFileGroupDto>>(e);
}

// --------------------------------------------------------------------------------------------------------------- equivalencias --

/// <summary>
/// Fija las equivalencias de concepto del convenio desde <see cref="ValidFrom"/> (api.md §18.3, <c>POST /{id}/concept-equivalences</c>,
/// <c>PayrollAgreements.Manage</c>, con clave): registra una versión nueva que copia la vigente (y sus grupos) y reemplaza sus equivalencias.
/// Reglas (data-model §1.4.2): cada código con su regla de reparto (<c>.EquivalenceWithoutSplit</c>) y al menos un ítem; con <c>Priority</c>, la
/// prioridad de cada ítem es única; un ítem va en un solo código (<c>.ConceptMappedTwice</c>); con grupos, cada código va en un grupo del convenio
/// (<c>.LineWithoutGroup</c>). (nuevo)
/// </summary>
public sealed record SetPayrollAgreementConceptEquivalencesCommand(Guid AgreementPublicId, DateOnly ValidFrom,
    IReadOnlyList<PayrollConceptEquivalenceInput> Equivalences, string Reason)
    : IRequest<Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetPayrollAgreementConceptEquivalencesCommandValidator : AbstractValidator<SetPayrollAgreementConceptEquivalencesCommand>
{
    public SetPayrollAgreementConceptEquivalencesCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rigen las equivalencias.");
        RuleFor(x => x.Equivalences).NotNull();
        RuleForEach(x => x.Equivalences).ChildRules(e =>
        {
            e.RuleFor(y => y.PaymasterConceptCode).NotEmpty().MaximumLength(20).WithMessage("El código de la pagaduría tiene hasta 20 caracteres.");
            e.RuleFor(y => y.Description).NotEmpty().MaximumLength(200);
            e.RuleFor(y => y.FileGroupCode).MaximumLength(CodigoDeCatalogo.LargoCorto);
            e.RuleForEach(y => y.Members).ChildRules(m =>
            {
                m.RuleFor(z => z.Nature).IsInEnum();
                m.RuleFor(z => z.LineCode).NotEmpty().MaximumLength(20).WithMessage("Indique la línea o «*» para todas.");
                m.RuleFor(z => z.Concept).NotEmpty().MaximumLength(40).WithMessage("Indique el concepto o «Installment» para la cuota completa.");
            });
        });
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga el motivo del cambio.").MaximumLength(500);
    }
}

public sealed class SetPayrollAgreementConceptEquivalencesCommandHandler(IApplicationDbContext db, CopiaDeVersionDeConvenio copia)
    : IRequestHandler<SetPayrollAgreementConceptEquivalencesCommand, Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>> Handle(SetPayrollAgreementConceptEquivalencesCommand request,
        CancellationToken ct)
    {
        var equivalencias = request.Equivalences.Select(e => new EquivalenciaAFijar(e.PaymasterConceptCode.Trim(), e.Description.Trim(), e.Split, e.Order,
            string.IsNullOrWhiteSpace(e.FileGroupCode) ? null : CodigoDeCatalogo.Normalizar(e.FileGroupCode),
            (e.Members ?? []).Select(m => new ItemDeEquivalencia(m.Nature, m.LineCode.Trim(), m.Concept.Trim(), m.Priority)).ToList())).ToList();
        // Se valida contra los grupos de la vigente antes de escribir nada (la versión nueva los copia).
        var actual = await copia.ActualAsync(request.AgreementPublicId, ct);
        if (actual is { } a)
        {
            var validas = CopiaDeVersionDeConvenio.ValidarEquivalencias(a.Version.UsesFileGroups, a.Grupos.Select(g => g.Code).ToList(), equivalencias);
            if (validas.IsFailure) return Falla(validas.Error);
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var versiones = await copia.CopiarAsync(request.AgreementPublicId, request.ValidFrom, request.Reason, ct);
            if (versiones.IsFailure) return Falla(versiones.Error);
            var (anterior, nueva) = versiones.Value;

            // Los grupos de la vigente pasan a la nueva tal cual.
            var (gruposAnteriores, _) = await copia.DeLaVersionAsync(anterior.Id, ct);
            var grupos = gruposAnteriores.Select(g => new PayrollAgreementFileGroup
            {
                PayrollAgreementVersionId = nueva.Id, Code = g.Code, Name = g.Name, OutboundFormatId = g.OutboundFormatId, ResultFormatId = g.ResultFormatId,
                CutoffDay = g.CutoffDay, PaymentDay = g.PaymentDay, SendLeadDays = g.SendLeadDays, Order = g.Order,
            }).ToList();
            db.PayrollAgreementFileGroups.AddRange(grupos);
            await db.SaveChangesAsync(ct);

            copia.AgregarEquivalencias(nueva, grupos, equivalencias);
            await db.SaveChangesAsync(ct);
            return Result.Success(await ConsultasDeGruposDeConvenio.EquivalenciasAsync(db, nueva, ct));
        }, ct);
    }

    private static Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>> Falla(Error e) =>
        Result.Failure<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>(e);
}

// -------------------------------------------------------------------------------------------------------------------- consultas --

/// <summary>Lo armado de grupos y equivalencias para las respuestas. (nuevo)</summary>
public static class ConsultasDeGruposDeConvenio
{
    public static async Task<IReadOnlyList<PayrollAgreementFileGroupDto>> GruposAsync(IApplicationDbContext db, PayrollAgreementVersion version,
        CancellationToken ct)
    {
        var grupos = await db.PayrollAgreementFileGroups.AsNoTracking().Where(g => g.PayrollAgreementVersionId == version.Id && !g.IsDeleted)
            .OrderBy(g => g.Order).ToListAsync(ct);
        var ids = grupos.SelectMany(g => new[] { g.OutboundFormatId, g.ResultFormatId }).Distinct().ToList();
        var formatos = await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => new BankFileFormatRefDto(f.PublicId, f.Code), ct);
        return grupos.Select(g => new PayrollAgreementFileGroupDto(g.PublicId, g.Code, g.Name, formatos[g.OutboundFormatId], formatos[g.ResultFormatId],
            new PayrollFileGroupCalendarDto(g.CutoffDay, g.SendLeadDays, g.PaymentDay), g.Order, version.ValidFrom, version.ValidTo)).ToList();
    }

    public static async Task<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>> EquivalenciasAsync(IApplicationDbContext db, PayrollAgreementVersion version,
        CancellationToken ct)
    {
        var grupos = await db.PayrollAgreementFileGroups.AsNoTracking().Where(g => g.PayrollAgreementVersionId == version.Id && !g.IsDeleted)
            .ToDictionaryAsync(g => g.Id, g => g.Code, ct);
        var equivalencias = await db.PayrollAgreementConceptEquivalences.AsNoTracking().Where(e => e.PayrollAgreementVersionId == version.Id && !e.IsDeleted)
            .OrderBy(e => e.Priority).ThenBy(e => e.PaymasterConceptCode).ToListAsync(ct);
        return equivalencias.Select(e => new PayrollAgreementConceptEquivalenceDto(e.PublicId, e.PaymasterConceptCode, e.Description,
            e.FileGroupId is { } gid ? grupos.GetValueOrDefault(gid) : null,
            EquivalenciasDePagaduria.Leer(e.ItemsJson).Select(i => new PayrollEquivalenceMemberDto(i.CreditLineCode, i.Nature.ToString(), i.Concept, i.Priority)).ToList(),
            e.AllocationRule.ToString(), e.Priority, version.ValidFrom, version.ValidTo)).ToList();
    }

    /// <summary>La versión del convenio que rige el día, o el error del convenio.</summary>
    public static async Task<Result<PayrollAgreementVersion>> VersionAsync(IApplicationDbContext db, Guid convenioPublicId, DateOnly dia, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == convenioPublicId && !a.IsDeleted, ct);
        if (convenio is null) return Result.Failure<PayrollAgreementVersion>(ErroresDeLibranza.ConvenioInexistente());
        var versiones = await db.PayrollAgreementVersions.AsNoTracking().Where(v => v.PayrollAgreementId == convenio.Id && !v.IsDeleted).ToListAsync(ct);
        return PayrollAgreementVersion.Vigente(versiones, dia) is { } vigente
            ? Result.Success(vigente)
            : Result.Failure<PayrollAgreementVersion>(ErroresDeLibranza.SinVersionVigente(convenio.Code, dia));
    }
}

/// <summary>Los grupos de archivo que rigen el día (api.md §18.3, <c>GET /{id}/file-groups?date=</c>, <c>PayrollAgreements.View</c>). (nuevo)</summary>
public sealed record ListPayrollAgreementFileGroupsQuery(Guid AgreementPublicId, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<PayrollAgreementFileGroupDto>>>;

public sealed class ListPayrollAgreementFileGroupsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListPayrollAgreementFileGroupsQuery, Result<IReadOnlyList<PayrollAgreementFileGroupDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementFileGroupDto>>> Handle(ListPayrollAgreementFileGroupsQuery request, CancellationToken ct)
    {
        var dia = request.Date ?? reloj.HoyLocal;
        var version = await ConsultasDeGruposDeConvenio.VersionAsync(db, request.AgreementPublicId, dia, ct);
        return version.IsFailure
            ? Result.Failure<IReadOnlyList<PayrollAgreementFileGroupDto>>(version.Error)
            : Result.Success(await ConsultasDeGruposDeConvenio.GruposAsync(db, version.Value, ct));
    }
}

/// <summary>Las equivalencias que rigen el día (api.md §18.3, <c>GET /{id}/concept-equivalences?date=</c>, <c>PayrollAgreements.View</c>). (nuevo)</summary>
public sealed record ListPayrollAgreementConceptEquivalencesQuery(Guid AgreementPublicId, DateOnly? Date = null)
    : IRequest<Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>>;

public sealed class ListPayrollAgreementConceptEquivalencesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListPayrollAgreementConceptEquivalencesQuery, Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>>
{
    public async Task<Result<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>> Handle(ListPayrollAgreementConceptEquivalencesQuery request, CancellationToken ct)
    {
        var version = await ConsultasDeGruposDeConvenio.VersionAsync(db, request.AgreementPublicId, request.Date ?? reloj.HoyLocal, ct);
        return version.IsFailure
            ? Result.Failure<IReadOnlyList<PayrollAgreementConceptEquivalenceDto>>(version.Error)
            : Result.Success(await ConsultasDeGruposDeConvenio.EquivalenciasAsync(db, version.Value, ct));
    }
}
