using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Waivers;

/// <summary>Una línea de condonación pedida (api.md §11.2, <c>concepts[]</c>): el concepto, el valor y, si se quiere, la cuota (nula = todas). (nuevo)</summary>
public sealed record WaiverConceptInput(LoanConcept Concept, decimal Amount, int? InstallmentNumber = null);

/// <summary>Una línea de una condonación. (nuevo)</summary>
public sealed record WaiverLineDto(LoanConcept Concept, int? InstallmentNumber, decimal Amount, decimal AvailableAtRequest);

/// <summary>Una condonación (api.md §11.2): lista y detalle. (nuevo)</summary>
public sealed record WaiverDto(
    Guid PublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    WaiverStatus Status,
    DateOnly AsOfDate,
    string Reason,
    int DaysPastDueAtRequest,
    bool IsRelatedParty,
    decimal TotalAmount,
    IReadOnlyList<WaiverLineDto> Concepts,
    SubjectRoutingDto? Routing,
    IReadOnlyList<SubjectDecisionDto> Decisions,
    string RequestedBy,
    DateTime RequestedAt,
    Guid? AppliedOperationPublicId,
    DateTime? AppliedAt);

/// <summary>Lo que dejó aplicar una condonación (api.md §11.2, <c>POST /{id}/apply</c>). (nuevo)</summary>
public sealed record ApplyWaiverResultDto(Guid OperationPublicId, string OperationNumber, IReadOnlyList<string> Messages, decimal ImpairmentApplied,
    decimal ImpairmentShortfall, IReadOnlyList<string> Warnings);

/// <summary>Una condonación enrutada (T63). (nuevo)</summary>
public sealed record CondonacionEnrutada(MatrizVigente Matriz, EnrutamientoDeAtribucion Enrutamiento, AttributionLevel Nivel, string Huella, decimal Monto,
    bool Vinculado)
{
    public SubjectRoutingDto Dto(IReadOnlyDictionary<int, string> organos) => new(Nivel.Code, Nivel.Name, Nivel.Kind,
        Nivel.GoverningBodyId is { } g ? organos.GetValueOrDefault(g) : null, Monto, Vinculado,
        Enrutamiento.Razones.Select(r => new RoutingReasonDto(r.Clase, r.Nivel.Codigo, r.Detalle)).ToList(), Huella);
}

/// <summary>
/// Lo común de las condonaciones (feature 013, K3, T0884): el expediente, el prefijo, la lectura en el alcance (la oficina y la línea del crédito; fuera,
/// el mismo 404), la respuesta y el <b>enrutamiento</b> por la matriz de atribuciones (sujeto <c>Condonation</c>; FR-094, T63): por cada concepto que se
/// condona —con el total condonado como monto, los días de mora y el vinculado sellados al radicar, la línea y la modalidad del crédito— el nivel que
/// pide la matriz; el efectivo es el mayor. Condonar capital exige además el nivel de excepción (<c>Lending.Waiver.PrincipalRequiresException</c> si la
/// matriz no lo tiene). Los datos sellados en la condonación hacen que el enrutamiento se pueda recalcular igual al decidir. (nuevo)
/// </summary>
public sealed class CondonacionesDeCartera(IApplicationDbContext db, MatrizVigenteDeAtribuciones matrices, IDateTimeService reloj)
{
    /// <summary>El tipo de expediente de una condonación en <c>LND_CaseParticipations</c>.</summary>
    public const string Expediente = "Waiver";

    /// <summary>El prefijo del número de la condonación.</summary>
    public const string Prefijo = "CON-";

    /// <summary>El prefijo del comprobante de la operación <c>Waiver</c>.</summary>
    public const string PrefijoDeOperacion = "CND-";

    public const string CodigoInexistente = "Lending.Waiver.NotFound";
    public const string CodigoCapitalSinExcepcion = "Lending.Waiver.PrincipalRequiresException";

    public static Error Inexistente() => new(CodigoInexistente, "La condonación no existe.");

    public static Error CapitalSinExcepcion() => new(CodigoCapitalSinExcepcion,
        "Condonar capital sólo lo decide el nivel de excepción de la matriz de atribuciones, y la matriz vigente no lo tiene. Quite el capital o pida que "
        + "se defina ese nivel.");

    /// <summary>La condonación y su crédito si existen y están en el alcance; si no, <c>Lending.Waiver.NotFound</c>.</summary>
    public static async Task<Result<(Waiver Condonacion, Loan Credito)>> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.Waivers.Include(w => w.Lines) : db.Waivers.AsNoTracking().Include(w => w.Lines);
        var w = await consulta.FirstOrDefaultAsync(x => x.PublicId == publicId && !x.IsDeleted, ct);
        if (w is null) return Result.Failure<(Waiver, Loan)>(Inexistente());
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == w.LoanId, ct);
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, Inexistente(), auditoria, Expediente, w.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<(Waiver, Loan)>(dentro.Error) : Result.Success((w, credito));
    }

    /// <summary>El enrutamiento de la condonación con lo sellado al radicar y la matriz vigente a <paramref name="fecha"/>.</summary>
    public async Task<Result<CondonacionEnrutada>> EnrutarAsync(Waiver w, Loan credito, DateOnly fecha, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<CondonacionEnrutada>(ErroresDeGobierno.SinMatrizVigente(fecha));
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == credito.CreditLineId).Select(l => l.Code).FirstAsync(ct);
        var modalidad = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber)
            .Select(t => t.ModalityCode).FirstAsync(ct);
        var conceptos = w.Lines.Select(l => l.Concept).Distinct().OrderBy(c => (int)c).ToList();
        if (conceptos.Count == 0) conceptos.Add(LoanConcept.DefaultInterest);

        var razones = new List<RazonDeAtribucion>();
        var noExceptuables = new List<MotivoNoExceptuable>();
        NivelDeAtribucion? efectivo = null;
        try
        {
            foreach (var concepto in conceptos)
            {
                var e = EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar(LendingDecisionSubjects.Condonation, w.TotalAmount)
                {
                    Linea = linea,
                    Modalidad = modalidad,
                    DiasDeMora = w.DaysPastDueAtRequest,
                    Concepto = concepto,
                    HayVinculado = w.IsRelatedParty,
                }, matriz.ParaElMotor);
                foreach (var r in e.Razones.Where(r => !razones.Any(x => x.Clase == r.Clase && x.Detalle == r.Detalle))) razones.Add(r);
                noExceptuables.AddRange(e.NoExceptuables);
                if (efectivo is null || e.NivelEfectivo.Rango > efectivo.Rango) efectivo = e.NivelEfectivo;
            }
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<CondonacionEnrutada>(new Error(AttributionMatrix.CodigoSinNivelFinal, e.Message));
        }
        if (w.CondonaCapital)
        {
            var excepcion = matriz.ParaElMotor.Niveles.SingleOrDefault(n => n.EsDeExcepcion);
            if (excepcion is null) return Result.Failure<CondonacionEnrutada>(CapitalSinExcepcion());
            razones.Add(new RazonDeAtribucion(RazonDeEnrutamiento.Exception, excepcion, $"Condona capital: mínimo {excepcion.Nombre} (FR-094)."));
            if (excepcion.Rango > efectivo!.Rango) efectivo = excepcion;
        }
        var enrutamiento = new EnrutamientoDeAtribucion(efectivo!, razones, noExceptuables.Distinct().ToList());
        var nivel = matriz.Niveles.Single(n => n.Code == efectivo!.Codigo);
        var huella = new HuellaDeCartera()
            .Texto("subject", LendingDecisionSubjects.Condonation)
            .Identificador("waiver", w.PublicId)
            .Fecha("asOf", w.AsOfDate)
            .Lista("lines", w.Lines.OrderBy(l => (int)l.Concept).ThenBy(l => l.InstallmentNumber ?? 0),
                (h, l) => h.Enumeracion<LoanConcept>("concept", l.Concept).Entero("installment", l.InstallmentNumber).Decimal("amount", l.Amount))
            .Entero("daysPastDue", w.DaysPastDueAtRequest)
            .Identificador("matrix", matriz.Matriz.PublicId)
            .Entero("matrixVersion", matriz.Matriz.VersionNumber)
            .Texto("level", efectivo!.Codigo)
            .Lista("reasons", razones, (h, r) => h.Enumeracion("kind", (RazonDeEnrutamiento?)r.Clase).Texto("level", r.Nivel.Codigo))
            .Decimal("amount", w.TotalAmount)
            .Logico("relatedParty", w.IsRelatedParty)
            .Calcular();
        return Result.Success(new CondonacionEnrutada(matriz, enrutamiento, nivel, huella, w.TotalAmount, w.IsRelatedParty));
    }

    /// <summary>La respuesta de una condonación, con su enrutamiento de hoy si está radicada.</summary>
    public async Task<WaiverDto> DtoAsync(Waiver w, Loan credito, CancellationToken ct)
    {
        SubjectRoutingDto? ruta = null;
        if (w.Status == WaiverStatus.Submitted && await EnrutarAsync(w, credito, reloj.HoyLocal, ct) is { IsSuccess: true } e)
            ruta = e.Value.Dto(await EnrutamientoDeNovedad.OrganosAsync(db, ct));
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, [w.RequestedByUserId], ct);
        var operacion = w.AppliedOperationId is { } op
            ? await db.LoanOperations.AsNoTracking().Where(o => o.Id == op).Select(o => (Guid?)o.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return new WaiverDto(w.PublicId, w.Number, credito.PublicId, credito.Number, w.Status, w.AsOfDate, w.Reason, w.DaysPastDueAtRequest, w.IsRelatedParty,
            w.TotalAmount, w.Lines.OrderBy(l => (int)l.Concept).ThenBy(l => l.InstallmentNumber ?? 0)
                .Select(l => new WaiverLineDto(l.Concept, l.InstallmentNumber, l.Amount, l.AvailableAtRequest)).ToList(),
            ruta, await NovedadesDeCartera.DecisionesAsync(db, LendingDecisionSubjects.Condonation, w.PublicId, ct),
            usuarios.GetValueOrDefault(w.RequestedByUserId)?.Name ?? string.Empty, w.RequestedAt, operacion, w.AppliedAt);
    }
}
