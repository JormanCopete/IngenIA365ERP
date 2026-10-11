using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Scoring;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Una validación evaluada que todavía no es fila (feature 013, T0394): la parte (nula = de la solicitud), la clase, el resultado, si se puede
/// exceptuar, el código que la explica, el detalle con las cifras y la evidencia enlazada (el resultado de listas, la consulta a centrales),
/// que se guardan antes que la validación para que ésta lleve sus Ids. <see cref="Hallazgo"/> es el motivo no exceptuable que aporta (R132).
/// (nuevo)
/// </summary>
public sealed record ValidacionPendiente(int? PartyId, ApplicationCheckKind Clase, CheckOutcome Resultado, bool Exceptuable, string? Codigo, object? Detalle)
{
    public ScreeningResult? Listas { get; init; }
    public BureauQuery? Central { get; init; }
    public MotivoNoExceptuable? Hallazgo { get; init; }

    public bool Bloquea => Resultado is CheckOutcome.Blocking or CheckOutcome.Unavailable;
}

/// <summary>
/// Lo que se valida (T0394): la solicitud con sus partes vivas y sus personas, la línea, la simulación (la versión, el plan, la cuota y la
/// usura del día), la etapa y la fecha. <see cref="Solo"/> limita las clases (volver a correr algunas, api.md §4.2 <c>checks/rerun</c>). (nuevo)
/// </summary>
public sealed record ContextoDeValidacion(
    CreditApplication Solicitud,
    IReadOnlyList<CreditApplicationParty> Partes,
    IReadOnlyDictionary<int, Person> Personas,
    CreditLine Linea,
    SimulacionCalculada Simulacion,
    CheckStage Etapa,
    DateOnly Fecha)
{
    public IReadOnlySet<ApplicationCheckKind>? Solo { get; init; }

    public bool Corre(ApplicationCheckKind clase) => Solo is null || Solo.Contains(clase);

    public CreditApplicationParty Solicitante => Partes.Single(p => p.Role == CreditPartyRole.Applicant);
}

/// <summary>
/// Lo que salió de las validaciones (T0394): las pendientes y lo que el enrutamiento y el puntaje necesitan —la exposición consolidada del
/// solicitante, el monto consolidado (lo pedido más la exposición), si hay un vinculado, la capacidad de cada parte, la consulta a centrales
/// del solicitante y la concentración—. (nuevo)
/// </summary>
public sealed record ValidacionesEvaluadas(
    IReadOnlyList<ValidacionPendiente> Pendientes,
    ExposicionDelDeudor Exposicion,
    decimal MontoConsolidado,
    bool HayVinculado,
    IReadOnlyDictionary<int, CapacidadCalculada> Capacidades,
    BureauQuery? CentralDelSolicitante,
    ResultadoDeConcentracion? Concentracion)
{
    /// <summary>Los motivos no exceptuables (R132), sin repetir y en el orden del enum.</summary>
    public IReadOnlyList<MotivoNoExceptuable> Hallazgos =>
        NoExceptuables.Evaluar(Pendientes.Where(p => p.Hallazgo is not null).Select(p => p.Hallazgo!.Value));

    /// <summary>Las advertencias llegan al aprobador como alertas (FR-036) y suben al nivel de excepción (FR-037).</summary>
    public bool ConAlertas => Pendientes.Any(p => p.Resultado == CheckOutcome.Warning);

    public bool Bloquea => Pendientes.Any(p => p.Bloquea);

    /// <summary>La primera pendiente bloqueante de una clase, si la hay.</summary>
    public ValidacionPendiente? Bloqueante(ApplicationCheckKind clase) => Pendientes.FirstOrDefault(p => p.Clase == clase && p.Bloquea);
}

/// <summary>
/// Las validaciones automáticas de la originación (feature 013, T0394; FR-036, FR-026, FR-041, FR-110; data-model §2.4): corre, <b>fuera del
/// cerrojo</b>, las trece clases de <see cref="ApplicationCheckKind"/> para el solicitante y sus codeudores en la etapa pedida
/// (<c>AtSubmission</c>, <c>BeforeDecision</c>; <c>BeforeDisbursement</c> la llama US3) y deja cada una con su evidencia:
/// <list type="bullet">
/// <item><b>listas</b> (<see cref="IConsultaDeListasDeCartera"/>): una coincidencia sin liberar bloquea y no se exceptúa; <c>Unavailable</c> o el
/// simulado en producción no cuentan como consulta: <c>Unavailable</c> hasta repetirla;</item>
/// <item><b>centrales</b>, en las líneas que la exigen: sin consulta vigente que haya respondido, <c>Lending.Bureau.QueryRequired</c>; con una
/// simulada en producción, <c>Lending.Adapter.SimulatedInProduction</c> (<see cref="GuardiaDeCanalesExternos"/>, efecto
/// <c>BureauEvidenceForApproval</c>); con el canal caído, <c>Unavailable</c>;</item>
/// <item><b>capacidad de pago</b> de cada parte (<see cref="CapacidadDePago"/>): insuficiente, no exceptuable; sin ingreso declarado,
/// <c>Unavailable</c> hasta el análisis; <b>capacidad de descuento</b> en nómina propia contra <c>LIBRANZA_CAPACIDAD_DESCUENTO</c>;</item>
/// <item><b>endeudamiento consolidado</b> (<see cref="ExposicionConsolidadaDelDeudor"/>) contra el máximo de la línea; <b>cupo</b>
/// (<see cref="IDisponibleDeCupo"/>); <b>concentración</b> por deudor y por grupo (<see cref="ValidacionDeConcentracion"/>; sin patrimonio
/// técnico, bloquea y alerta);</item>
/// <item><b>usura</b> del día (no exceptuable); <b>requisitos de la línea</b> (antigüedad, edad, vínculo laboral, saldo mínimo de aportes con la
/// fecha de la carga —D19—, endeudamiento máximo, codeudores exigidos por la regla de garantías que casa por monto y plazo —FR-026,
/// <c>Lending.Collateral.CoDebtorsInsufficient</c>— y los bloqueos vigentes —<c>Lending.Debtor.Blocked</c>—);</item>
/// <item><b>autorización</b> de tratamiento para el estudio de crédito de cada parte (no exceptuable); <b>vinculado</b> (advertencia: la
/// solicitud va al consejo, FR-040); <b>documentos</b>.</item>
/// </list>
/// Exceptuable es todo lo que no está en <see cref="NoExceptuables"/>, salvo tres límites que no son de política: la concentración (legal), la
/// capacidad de descuento (legal) y los bloqueos vigentes (orden de autoridad o de la cooperativa). No escribe validaciones: las devuelve
/// pendientes y <see cref="Registrar"/> las agrega cuando su evidencia ya tiene Id. Las consultas de listas sí quedan en la unidad de trabajo
/// (las agrega su puerto, sin guardar). (nuevo)
/// </summary>
public sealed class ValidacionesDeOriginacion(
    IApplicationDbContext db,
    IConsultaDeListasDeCartera listas,
    GuardiaDeCanalesExternos guardia,
    CapacidadDePago capacidad,
    ExposicionConsolidadaDelDeudor exposicion,
    IDisponibleDeCupo cupos,
    ValidacionDeConcentracion concentracion,
    VinculadosVigentes vinculados,
    IBloqueosDelDeudor bloqueos,
    IAutorizacionDeDatos autorizaciones,
    CreditLimits.SaldoDeAportesVigente saldosDeAportes)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ValidacionesEvaluadas> EvaluarAsync(ContextoDeValidacion c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var s = c.Solicitud;
        var version = c.Simulacion.Version.Version;
        var solicitante = c.Solicitante;
        var pendientes = new List<ValidacionPendiente>();
        var personas = c.Partes.Select(p => p.PersonId).Distinct().ToList();

        // ---------------------------------------------------------------------------------------------------------- listas --
        if (c.Corre(ApplicationCheckKind.RestrictiveLists))
            foreach (var parte in c.Partes)
            {
                var r = await listas.ConsultarAsync(new PedidoDeConsultaDeListas(parte.PersonId, ScreeningContext.Application, s.Id), ct);
                var detalle = new { outcome = r.Resultado.Outcome.ToString(), channelCode = r.Resultado.ChannelCode, simulated = r.Resultado.IsSimulated };
                pendientes.Add(r.Bloquea
                    ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.RestrictiveLists, CheckOutcome.Blocking, false,
                        ErroresDeOriginacion.CodigoCoincidenciaEnListas, detalle) { Listas = r.Resultado, Hallazgo = MotivoNoExceptuable.RestrictiveListMatch }
                    : !r.CuentaComoConsulta
                        ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.RestrictiveLists, CheckOutcome.Unavailable, false,
                            ErroresDeOriginacion.CodigoCanalNoDisponible, detalle) { Listas = r.Resultado }
                        : new ValidacionPendiente(parte.Id, ApplicationCheckKind.RestrictiveLists, CheckOutcome.Passed, false, null, detalle)
                        {
                            Listas = r.Resultado,
                        });
            }

        // -------------------------------------------------------------------------------------------------------- centrales --
        BureauQuery? centralDelSolicitante = null;
        var consultas = await db.BureauQueries.AsNoTracking().Where(q => personas.Contains(q.PersonId) && !q.IsDeleted).ToListAsync(ct);
        foreach (var parte in c.Partes)
        {
            var ultima = BureauQuery.UltimaVigente(consultas, parte.PersonId, c.Fecha);
            if (parte.Role == CreditPartyRole.Applicant) centralDelSolicitante = ultima;
            if (!c.Corre(ApplicationCheckKind.CreditBureau)) continue;
            pendientes.Add(Central(parte, ultima, version.RequiresBureauQuery));
        }

        // -------------------------------------------------------------------------------------------- capacidad de pago --
        var capacidades = new Dictionary<int, CapacidadCalculada>();
        foreach (var parte in c.Partes)
        {
            var cap = await capacidad.DePagoAsync(parte, c.Simulacion.CuotaMensual, c.Fecha, ct);
            capacidades[parte.Id] = cap;
            if (!c.Corre(ApplicationCheckKind.PaymentCapacity)) continue;
            var detalle = new
            {
                income = cap.Ingresos, expenses = cap.Egresos, ownInstallments = cap.CuotasPropias, externalInstallments = cap.CuotasExternas,
                simulatedInstallment = cap.CuotaSimulada, available = cap.Disponible, declared = cap.Declarada,
            };
            pendientes.Add(!cap.Declarada
                ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.PaymentCapacity, CheckOutcome.Unavailable, false,
                    ErroresDeOriginacion.CodigoCapacidadDePago, detalle)
                : cap.Alcanza
                    ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.PaymentCapacity, CheckOutcome.Passed, false, null, detalle)
                    : new ValidacionPendiente(parte.Id, ApplicationCheckKind.PaymentCapacity, CheckOutcome.Blocking, false,
                        ErroresDeOriginacion.CodigoCapacidadDePago, detalle) { Hallazgo = MotivoNoExceptuable.PaymentCapacity });
        }

        // ---------------------------------------------------------------------------------------- capacidad de descuento --
        if (c.Corre(ApplicationCheckKind.DeductionCapacity))
        {
            if (s.RepaymentChannel != RepaymentChannel.OwnPayroll)
                pendientes.Add(new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.DeductionCapacity, CheckOutcome.NotApplicable, true, null, null));
            else
            {
                var desc = await capacidad.DeDescuentoAsync(solicitante, c.Simulacion.CuotaMensual, c.Fecha, ct);
                pendientes.Add(desc.IsFailure
                    ? new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.DeductionCapacity, CheckOutcome.Unavailable, false, desc.Error.Code,
                        new { reason = desc.Error.Message })
                    : new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.DeductionCapacity,
                        desc.Value.Alcanza ? CheckOutcome.Passed : CheckOutcome.Blocking, false,
                        desc.Value.Alcanza ? null : ErroresDeOriginacion.CodigoCapacidadDeDescuento,
                        new
                        {
                            income = desc.Value.Ingreso, fraction = desc.Value.Fraccion, allowed = desc.Value.Permitido,
                            currentDeductions = desc.Value.DescuentosVigentes, simulatedInstallment = desc.Value.CuotaSimulada,
                            available = desc.Value.Disponible, source = desc.Value.Fuente,
                        }));
            }
        }

        // ------------------------------------------------------------------------------------- endeudamiento consolidado --
        var expo = await exposicion.DeAsync(solicitante.PersonId, c.Fecha, s.Id, ct);
        var consolidado = s.RequestedAmount + expo.Total;
        if (c.Corre(ApplicationCheckKind.ConsolidatedExposure))
        {
            var excede = version.MaxIndebtedness is { } maximo && consolidado > maximo;
            pendientes.Add(new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.ConsolidatedExposure,
                excede ? CheckOutcome.Blocking : CheckOutcome.Passed, true, excede ? ErroresDeOriginacion.CodigoRequisitosDeLinea : null,
                new
                {
                    asDebtor = expo.ComoDeudor, asCoDebtor = expo.ComoCodeudor, reservations = expo.Reservas, approved = expo.Aprobadas,
                    requested = s.RequestedAmount, consolidated = consolidado, maxIndebtedness = version.MaxIndebtedness,
                    loans = expo.Creditos.Select(x => new { number = x.Number, principal = x.Capital, asCoDebtor = x.ComoCodeudor }),
                }));
        }

        // ------------------------------------------------------------------------------------------------------------ cupo --
        if (c.Corre(ApplicationCheckKind.CreditLimit))
        {
            var cupo = await cupos.CalcularAsync(solicitante.PersonId, c.Linea.Id, c.Fecha, ct);
            pendientes.Add(!cupo.TieneCupo
                ? new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.CreditLimit, CheckOutcome.NotApplicable, true, null, null)
                : new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.CreditLimit,
                    cupo.Disponible >= s.RequestedAmount ? CheckOutcome.Passed : CheckOutcome.Blocking, true,
                    cupo.Disponible >= s.RequestedAmount ? null : ErroresDeOriginacion.CodigoCupoInsuficiente,
                    new { limit = cupo.Cupo, used = cupo.Consumido, available = cupo.Disponible, requested = s.RequestedAmount, creditLimitPublicId = cupo.CreditLimitPublicId }));
        }

        // --------------------------------------------------------------------------------------------------- concentración --
        ResultadoDeConcentracion? conc = null;
        if (c.Corre(ApplicationCheckKind.Concentration))
        {
            var medida = await concentracion.MedirAsync(solicitante.PersonId, s.RequestedAmount, conGarantiaAdmisible: false, c.Fecha, s.Id, ct);
            if (medida.IsFailure)
                pendientes.Add(new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.Concentration, CheckOutcome.Blocking, false,
                    medida.Error.Code, new { reason = medida.Error.Message }));
            else
            {
                conc = medida.Value;
                var excedida = conc.Excedida;
                pendientes.Add(new ValidacionPendiente(solicitante.Id, ApplicationCheckKind.Concentration,
                    excedida is null ? CheckOutcome.Passed : CheckOutcome.Blocking, false,
                    excedida is null ? null : ErroresDeOriginacion.CodigoConcentracionExcedida,
                    new
                    {
                        debtor = Medida(conc.PorDeudor),
                        group = conc.PorGrupo is null ? null : Medida(conc.PorGrupo),
                    }));
            }
        }

        // ------------------------------------------------------------------------------------------------------------ usura --
        if (c.Corre(ApplicationCheckKind.Usury))
        {
            var u = c.Simulacion.Usura;
            pendientes.Add(new ValidacionPendiente(null, ApplicationCheckKind.Usury, u.Pasa ? CheckOutcome.Passed : CheckOutcome.Blocking, false,
                u.Pasa ? null : ErroresDeOriginacion.CodigoUsuraExcedida,
                new
                {
                    effectiveRate = u.EfectivaConCargos, usuryRate = u.Usura, modalityKey = u.ClaveDeUsura, headroom = u.Holgura,
                    nationalValuePublicId = u.UsuraPublicId, date = ErroresDeOriginacion.Fecha(c.Simulacion.Fecha),
                }) { Hallazgo = u.Pasa ? null : MotivoNoExceptuable.Usury });
        }

        // ------------------------------------------------------------------------------------------ requisitos de la línea --
        if (c.Corre(ApplicationCheckKind.LineRequirements))
        {
            pendientes.AddRange(await RequisitosAsync(c, ct));
            var vigentes = await bloqueos.VigentesAsync(personas, ct);
            foreach (var parte in c.Partes)
            {
                var suyos = vigentes.Where(b => b.PersonId == parte.PersonId).ToList();
                if (suyos.Count == 0) continue;
                pendientes.Add(new ValidacionPendiente(parte.Id, ApplicationCheckKind.LineRequirements, CheckOutcome.Blocking, false,
                    ErroresDeOriginacion.CodigoDeudorBloqueado,
                    new { requirement = "DebtorBlock", blocks = suyos.Select(b => new { reason = b.Reason.ToString(), startedOn = b.StartedOn, b.PublicId }) }));
            }
        }

        // ----------------------------------------------------------------------------------------------- autorización --
        if (c.Corre(ApplicationCheckKind.DataAuthorization))
            foreach (var parte in c.Partes)
            {
                var persona = c.Personas[parte.PersonId];
                var a = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
                pendientes.Add(a is { Autoriza: true }
                    ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.DataAuthorization, CheckOutcome.Passed, false, null,
                        new { consentPublicId = a.ConsentPublicId, hasEvidence = a.HasEvidence })
                    : new ValidacionPendiente(parte.Id, ApplicationCheckKind.DataAuthorization, CheckOutcome.Blocking, false,
                        ErroresDeOriginacion.CodigoSinAutorizacion, new { purpose = DataAuthorizationPurpose.CreditProcessing.ToString() })
                    {
                        Hallazgo = MotivoNoExceptuable.HolderAuthorization,
                    });
            }

        // -------------------------------------------------------------------------------------------------------- vinculado --
        var vinculos = await vinculados.DeAsync(personas, c.Fecha, ct);
        if (c.Corre(ApplicationCheckKind.RelatedParty))
            pendientes.Add(new ValidacionPendiente(null, ApplicationCheckKind.RelatedParty,
                vinculos.Count > 0 ? CheckOutcome.Warning : CheckOutcome.Passed, true, null,
                new { relatedParties = vinculos.Select(v => new { v.PublicId, basis = v.Basis.ToString() }) }));

        // ------------------------------------------------------------------------------------------------------- documentos --
        if (c.Corre(ApplicationCheckKind.Documents))
        {
            var faltan = await DocumentosFaltantesAsync(db, s, c.Partes, c.Personas, c.Simulacion.Version, c.Fecha, ct);
            pendientes.Add(new ValidacionPendiente(null, ApplicationCheckKind.Documents, faltan.Count == 0 ? CheckOutcome.Passed : CheckOutcome.Blocking,
                true, faltan.Count == 0 ? null : ErroresDeOriginacion.CodigoDocumentosFaltantes,
                new { missing = faltan.Select(f => new { code = f.Code, name = f.Name, personPublicId = f.PersonPublicId }) }));
        }

        return new ValidacionesEvaluadas(pendientes, expo, consolidado, vinculos.Count > 0, capacidades, centralDelSolicitante, conc);
    }

    /// <summary>
    /// Agrega las validaciones evaluadas como filas de <c>LND_CreditApplicationChecks</c> (sin guardar), con el Id de su evidencia —que ya
    /// tiene que estar guardada— y la del puntaje, si lo hubo.
    /// </summary>
    public IReadOnlyList<CreditApplicationCheck> Registrar(ValidacionesEvaluadas v, int applicationId, CheckStage etapa, DateTime instante,
        CreditScore? puntaje)
    {
        ArgumentNullException.ThrowIfNull(v);
        var filas = v.Pendientes.Select(p => CreditApplicationCheck.Registrar(applicationId, p.PartyId, p.Clase, etapa, p.Resultado, p.Exceptuable,
            p.Codigo, p.Detalle is null ? null : JsonSerializer.Serialize(p.Detalle, Json), instante, p.Listas?.Id is > 0 ? p.Listas.Id : null,
            p.Central?.Id is > 0 ? p.Central.Id : null)).ToList();
        if (puntaje is not null)
            filas.Add(CreditApplicationCheck.Registrar(applicationId, null, ApplicationCheckKind.Scoring, etapa,
                puntaje.BelowMinimum ? CheckOutcome.Warning : CheckOutcome.Passed, true, null,
                JsonSerializer.Serialize(new { score = puntaje.Score, minimum = puntaje.MinimumScore, belowMinimum = puntaje.BelowMinimum }, Json),
                instante, creditScoreId: puntaje.Id > 0 ? puntaje.Id : null));
        db.CreditApplicationChecks.AddRange(filas);
        return filas;
    }

    /// <summary>
    /// Los documentos obligatorios que faltan (FR-035, data-model §1.5): cada requisito obligatorio de la versión que casa por monto, tipo de
    /// solicitante, forma de pago y parte, sin un documento vivo, no rechazado y dentro de su antigüedad.
    /// </summary>
    public static async Task<IReadOnlyList<DocumentoFaltante>> DocumentosFaltantesAsync(IApplicationDbContext db, CreditApplication s,
        IReadOnlyList<CreditApplicationParty> partes, IReadOnlyDictionary<int, Person> personas, Configuration.VersionDeLineaLeida version, DateOnly hoy,
        CancellationToken ct)
    {
        var documentos = await db.CreditApplicationDocuments.AsNoTracking().Where(d => d.ApplicationId == s.Id && !d.IsDeleted).ToListAsync(ct);
        var faltan = new List<DocumentoFaltante>();
        foreach (var r in version.Documentos.Where(d => d.IsMandatory).OrderBy(d => d.Order))
        {
            if (r.MinAmount is { } min && s.RequestedAmount < min) continue;
            if (r.MaxAmount is { } max && s.RequestedAmount > max) continue;
            if (r.DebtorKind is { } tipo && tipo != s.DebtorKind) continue;
            if (r.RepaymentChannel is { } forma && forma != s.RepaymentChannel) continue;
            var aQuienes = partes.Where(p => r.AppliesTo is null ? p.Role == CreditPartyRole.Applicant : p.Role == r.AppliesTo).ToList();
            foreach (var parte in aQuienes)
            {
                var cumple = documentos.Any(d => d.RequirementId == r.Id && (d.PartyId is null ? parte.Role == CreditPartyRole.Applicant : d.PartyId == parte.Id)
                                                 && d.Cuenta(hoy, r.MaxAgeDays));
                if (!cumple)
                    faltan.Add(new DocumentoFaltante(r.Code, r.Name, r.AppliesTo is null ? null : personas.GetValueOrDefault(parte.PersonId)?.PublicId));
            }
        }
        return faltan;
    }

    private ValidacionPendiente Central(CreditApplicationParty parte, BureauQuery? ultima, bool exigida)
    {
        if (ultima is null)
            return exigida
                ? new ValidacionPendiente(parte.Id, ApplicationCheckKind.CreditBureau, CheckOutcome.Blocking, true,
                    ErroresDeOriginacion.CodigoConsultaACentralesRequerida, new { required = true })
                : new ValidacionPendiente(parte.Id, ApplicationCheckKind.CreditBureau, CheckOutcome.NotApplicable, true, null, null);

        var detalle = new
        {
            ultima.PublicId, outcome = ultima.Outcome.ToString(), channelCode = ultima.ChannelCode, simulated = ultima.IsSimulated, score = ultima.Score,
            negativeMarks = ultima.NegativeMarks, validUntil = ultima.ValidUntil,
        };
        if (!ultima.Respondio)
            return new ValidacionPendiente(parte.Id, ApplicationCheckKind.CreditBureau, exigida ? CheckOutcome.Unavailable : CheckOutcome.NotApplicable,
                true, exigida ? ErroresDeOriginacion.CodigoCanalNoDisponible : null, detalle) { Central = ultima };
        if (exigida && guardia.Evaluar(EfectoLegalDeCanal.BureauEvidenceForApproval, ultima.ChannelCode, ultima.IsSimulated) is { TieneEfecto: false })
            return new ValidacionPendiente(parte.Id, ApplicationCheckKind.CreditBureau, CheckOutcome.Blocking, true,
                ErroresDeOriginacion.CodigoSimuladoEnProduccion, detalle) { Central = ultima };
        return new ValidacionPendiente(parte.Id, ApplicationCheckKind.CreditBureau, CheckOutcome.Passed, true, null, detalle) { Central = ultima };
    }

    private async Task<IReadOnlyList<ValidacionPendiente>> RequisitosAsync(ContextoDeValidacion c, CancellationToken ct)
    {
        var v = c.Simulacion.Version.Version;
        var s = c.Solicitud;
        var solicitante = c.Solicitante;
        var persona = c.Personas[solicitante.PersonId];
        var faltas = new List<object>();

        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, persona, c.Fecha, ct);
        var antiguedad = afiliacion.Activo ? PersonaDeOriginacion.MesesCumplidos(afiliacion.Desde, c.Fecha) : 0;
        if (v.MinAssociateSeniorityMonths is { } meses && antiguedad < meses)
            faltas.Add(new { requirement = "MinAssociateSeniorityMonths", required = meses, actual = antiguedad });
        var edad = PersonaDeOriginacion.Edad(persona, c.Fecha);
        if (v.MinAge is { } minima && (edad is null || edad < minima)) faltas.Add(new { requirement = "MinAge", required = minima, actual = edad });
        if (v.MaxAge is { } maxima && (edad is null || edad > maxima)) faltas.Add(new { requirement = "MaxAge", required = maxima, actual = edad });
        if (v.RequiresEmployment && !persona.IsEmployee) faltas.Add(new { requirement = "RequiresEmployment", required = true, actual = false });
        if (v.MinContributionsBalance is { } aportesMinimos)
        {
            // D19 (bloque 6B): el saldo lo da el lector único, que pregunta a Aportes y, mientras responda «no disponible», toma la última carga.
            var saldo = await saldosDeAportes.DeAsync(persona.Id, c.Fecha, ct: ct);
            if (saldo is null || saldo.Aportes < aportesMinimos)
                faltas.Add(new
                {
                    requirement = "MinContributionsBalance", required = aportesMinimos, actual = saldo?.Aportes,
                    asOfDate = saldo is null ? null : ErroresDeOriginacion.Fecha(saldo.Corte), source = saldo?.Fuente,
                });
        }

        var resultado = new List<ValidacionPendiente>
        {
            new(solicitante.Id, ApplicationCheckKind.LineRequirements, faltas.Count == 0 ? CheckOutcome.Passed : CheckOutcome.Blocking, true,
                faltas.Count == 0 ? null : ErroresDeOriginacion.CodigoRequisitosDeLinea, new { unmet = faltas }),
        };

        // FR-026: los codeudores que exige la regla de garantías que casa por monto y plazo (la de menor orden).
        var regla = c.Simulacion.Version.Garantias.OrderBy(g => g.Order).FirstOrDefault(g =>
            (g.MinAmount is not { } mn || s.RequestedAmount >= mn) && (g.MaxAmount is not { } mx || s.RequestedAmount <= mx)
            && (g.MinTermMonths is not { } pmn || (s.TermMonths ?? 0) >= pmn) && (g.MaxTermMonths is not { } pmx || (s.TermMonths ?? 0) <= pmx));
        if (regla is { RequiredCoDebtors: > 0 })
        {
            var presentes = c.Partes.Count(p => p.Role == CreditPartyRole.CoDebtor);
            if (presentes < regla.RequiredCoDebtors)
                resultado.Add(new ValidacionPendiente(null, ApplicationCheckKind.LineRequirements, CheckOutcome.Blocking, true,
                    ErroresDeOriginacion.CodigoCodeudoresInsuficientes, new { required = regla.RequiredCoDebtors, present = presentes, ruleOrder = regla.Order }));
        }
        return resultado;
    }

    /// <summary>
    /// Los valores de cada fuente del puntaje para el solicitante (FR-037; <see cref="FuentesDePuntaje"/>). Lo que el programa todavía no sabe
    /// medir (antigüedad laboral, cobertura de garantías, mora en centrales) va sin dato y toma el rango «sin dato» de la línea.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, decimal?>> ValoresDePuntajeAsync(ContextoDeValidacion c, ValidacionesEvaluadas v, CancellationToken ct)
    {
        var solicitante = c.Solicitante;
        var persona = c.Personas[solicitante.PersonId];
        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, persona, c.Fecha, ct);
        var saldo = await saldosDeAportes.DeAsync(persona.Id, c.Fecha, ct: ct);
        var desde = c.Fecha.AddMonths(-12);
        var creditos = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == persona.Id && !l.IsDeleted)
            .Select(l => new { l.Id, l.Status }).ToListAsync(ct);
        var ids = creditos.Select(x => x.Id).ToArray();
        var moraInterna = ids.Length == 0
            ? (int?)null
            : await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate > desde && p.PositionDate <= c.Fecha)
                .MaxAsync(p => (int?)p.DaysPastDue, ct);

        return new Dictionary<string, decimal?>(StringComparer.Ordinal)
        {
            [FuentesDePuntaje.EdadDelSolicitante] = PersonaDeOriginacion.Edad(persona, c.Fecha),
            [FuentesDePuntaje.AntiguedadComoAsociado] = afiliacion.Activo ? PersonaDeOriginacion.MesesCumplidos(afiliacion.Desde, c.Fecha) : null,
            [FuentesDePuntaje.AntiguedadLaboral] = null,
            [FuentesDePuntaje.IngresoMensual] = solicitante.MonthlyIncome,
            [FuentesDePuntaje.SaldoDeAportes] = saldo?.Aportes,
            [FuentesDePuntaje.CapacidadDePago] = v.Capacidades.GetValueOrDefault(solicitante.Id)?.Relacion,
            [FuentesDePuntaje.MontoSolicitado] = c.Solicitud.RequestedAmount,
            [FuentesDePuntaje.PlazoEnMeses] = c.Solicitud.TermMonths,
            [FuentesDePuntaje.CoberturaDeGarantias] = null,
            [FuentesDePuntaje.PuntajeDeCentrales] = v.CentralDelSolicitante is { Respondio: true } q ? q.Score : null,
            [FuentesDePuntaje.MoraMaximaEnCentrales] = null,
            [FuentesDePuntaje.MoraMaximaInterna] = moraInterna ?? (ids.Length == 0 ? null : 0),
            [FuentesDePuntaje.CreditosPagados] = creditos.Count(x => x.Status == LoanStatus.PaidOff),
        };
    }

    private static object Medida(MedicionDeConcentracion m) => new
    {
        scope = m.Alcance.ToString(), groupPublicId = m.GrupoPublicId, limit = m.Limite, current = m.Actual, requested = m.Pedido, excess = m.Exceso,
        @base = m.Base, fraction = m.Fraccion, cutoffDate = m.Corte, members = m.Miembros.Select(x => new { personPublicId = x.PersonPublicId, exposure = x.Exposicion }),
    };
}
