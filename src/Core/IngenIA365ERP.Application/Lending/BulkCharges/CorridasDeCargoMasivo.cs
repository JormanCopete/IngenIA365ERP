using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.BulkCharges;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>Lo que la vista previa decide de un asociado.</summary>
public sealed record LineaCalculada(
    int PersonId,
    int BranchId,
    decimal Amount,
    int Installments,
    DateOnly FirstDueDate,
    RepaymentChannel Channel,
    int? AgreementId,
    BulkChargeLineStatus Status,
    string? Reason,
    string? Detail,
    string? Warning,
    int? SourceRow);

/// <summary>El resultado de calcular una corrida: sus líneas, su huella, la versión de la línea y la decisión contable.</summary>
public sealed record CalculoDeCorrida(
    IReadOnlyList<LineaCalculada> Lineas,
    string Huella,
    int VersionId,
    DecisionDeValidacionPrevia? Contable)
{
    public IEnumerable<LineaCalculada> Incluidas => Lineas.Where(l => l.Status == BulkChargeLineStatus.Pending);
}

/// <summary>Lo que el borrador guarda de dónde salen los asociados (<c>FiltersJson</c>): los filtros o las filas del plano.</summary>
public sealed record OrigenDeCargoMasivo(BulkChargeFiltersInput? Filters, IReadOnlyList<FilaDeCargoMasivo>? Rows)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Escribir() => JsonSerializer.Serialize(this, Json);

    public static OrigenDeCargoMasivo Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new OrigenDeCargoMasivo(new BulkChargeFiltersInput(), null);
        try
        {
            return JsonSerializer.Deserialize<OrigenDeCargoMasivo>(json, Json) ?? new OrigenDeCargoMasivo(new BulkChargeFiltersInput(), null);
        }
        catch (JsonException)
        {
            return new OrigenDeCargoMasivo(new BulkChargeFiltersInput(), null);
        }
    }
}

/// <summary>
/// El corazón de los cargos masivos (2026-11-25, pedido del dueño; feature 026): lee la línea marcada y su versión vigente a la fecha del cargo, valida los
/// datos, arma la población (los asociados activos con los filtros, o las filas del plano), decide por asociado —excluido (D3), ya cargado en el período,
/// sin convenio para la forma de pago o incluido—, simula el plan de cada combinación con el simulador de la línea, sella la huella y pregunta a
/// Contabilidad si podrá contabilizarlo. No crea obligaciones: eso es de <see cref="NacimientoDeCargoMasivo"/>. (nuevo)
/// </summary>
public sealed class CorridasDeCargoMasivo(
    IApplicationDbContext db,
    VersionVigenteDeLinea versiones,
    SimuladorDeCredito simulador,
    ValidacionPreviaContableDeCartera validacionPrevia)
{
    public const string TipoDeComprobante = "CO";

    private static readonly DeceasedCaseStatus[] CasosVivos = [DeceasedCaseStatus.Registered, DeceasedCaseStatus.DeliveryPending, DeceasedCaseStatus.Approved];

    // ------------------------------------------------------------------------------------------------------- la línea --

    /// <summary>La línea de colocación con una versión vigente a la fecha que admite cargos masivos.</summary>
    public async Task<Result<(CreditLine Linea, VersionDeLineaLeida Version)>> LineaAsync(int lineaId, DateOnly fecha, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lineaId && !l.IsDeleted, ct);
        if (linea is null) return Result.Failure<(CreditLine, VersionDeLineaLeida)>(ErroresDeCargoMasivo.LineaInexistente());
        var version = await versiones.LeerAsync(linea.Id, fecha, ct: ct);
        if (linea.Nature != LineNature.Placement || version is null || !version.Version.AllowsBulkCharge)
            return Result.Failure<(CreditLine, VersionDeLineaLeida)>(ErroresDeCargoMasivo.LineaNoHabilitada(linea.Code, fecha));
        return Result.Success((linea, version));
    }

    /// <summary>Los datos de la corrida contra la versión de la línea: rangos de monto y cuotas, periodicidad y forma de pago admitidas, primer pago.</summary>
    public static Error? ValidarDatos(BulkChargeRun run, CreditLineVersion v)
    {
        if (ReglasDeCargoMasivo.ValidarDatos(run.Amount, run.Installments, run.ChargeDate, run.FirstDueDate, run.PaymentCycle, run.CounterpartMode,
                run.CounterpartPersonId is not null) is { } problema)
            return new Error(problema.Codigo, problema.Mensaje);
        if (!ReglasDeCargoMasivo.PeriodoValido(run.Period))
            return new Error(ErroresDeCargoMasivo.CodigoPeriodoInvalido, "El período es el año y el mes del cargo (aaaamm).");
        if (run.Amount < v.MinAmount || run.Amount > v.MaxAmount)
            return ErroresDeCargoMasivo.FueraDeRango($"El valor debe estar entre {v.MinAmount:N0} y {v.MaxAmount:N0}, los montos de la línea.");
        if (v.MaxInstallments is { } max && run.Installments > max)
            return ErroresDeCargoMasivo.FueraDeRango($"La línea admite hasta {max} cuotas.");
        if (!v.LeerPeriodicidades().Contains(run.Periodicity))
            return ErroresDeCargoMasivo.FueraDeRango("La línea no admite esa periodicidad.");
        if (run.ChannelMode == BulkChargeChannelMode.Fixed && run.RepaymentChannel is { } forma && !v.LeerFormasDePago().Contains(forma))
            return ErroresDeCargoMasivo.FueraDeRango("La línea no admite esa forma de pago.");
        return null;
    }

    // ------------------------------------------------------------------------------------------------------ calcular --

    /// <summary>
    /// Calcula la corrida sin escribir nada: la versión vigente a la fecha del cargo, la población, la decisión por asociado, la simulación de cada
    /// combinación de valor, cuotas y primer pago, la huella y la evaluación contable (sin alertas: es la vista previa).
    /// </summary>
    public async Task<Result<CalculoDeCorrida>> CalcularAsync(BulkChargeRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        var leida = await LineaAsync(run.CreditLineId, run.ChargeDate, ct);
        if (leida.IsFailure) return Result.Failure<CalculoDeCorrida>(leida.Error);
        var (linea, version) = leida.Value;
        var v = version.Version;
        if (ValidarDatos(run, v) is { } invalido) return Result.Failure<CalculoDeCorrida>(invalido);

        var origen = OrigenDeCargoMasivo.Leer(run.FiltersJson);
        var candidatos = await CandidatosAsync(run, origen, ct);
        var contexto = await ContextoAsync(run, candidatos.Select(c => c.PersonId).Distinct().ToList(), ct);
        var formasAdmitidas = v.LeerFormasDePago();
        var principal = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct)
                        ?? 0;

        var lineas = new List<LineaCalculada>(candidatos.Count);
        var simulaciones = new Dictionary<(decimal, int, DateOnly), Result<SimulacionCalculada>>();
        foreach (var c in candidatos)
        {
            var monto = c.Amount ?? run.Amount;
            var cuotas = c.Installments ?? run.Installments;
            var primerPago = c.FirstDueDate ?? run.FirstDueDate;
            var oficina = c.BranchId ?? principal;
            LineaCalculada Omitir(string motivo, string? detalle) =>
                new(c.PersonId, oficina, monto, cuotas, primerPago, RepaymentChannel.Cashier, null, BulkChargeLineStatus.Skipped, motivo, detalle, null, c.Row);

            if (c.Error is { } error)
            {
                lineas.Add(Omitir(error, c.ErrorDetail));
                continue;
            }
            var asociado = contexto.Asociado(c);
            if (ReglasDeCargoMasivo.Exclusion(asociado, run.ChargeDate) is { } motivo)
            {
                lineas.Add(Omitir(motivo, null));
                continue;
            }
            if (contexto.YaCargados.TryGetValue(c.PersonId, out var otraCorrida))
            {
                lineas.Add(Omitir(MotivosDeCargoMasivo.YaCargado, $"Ya tiene el cargo de este período en la corrida {otraCorrida}."));
                continue;
            }

            var convenioPagaduria = c.EmployerCompanyId is { } empresa && contexto.ConvenioPorEmpresa.TryGetValue(empresa, out var cp) ? cp : (int?)null;
            var forma = c.RepaymentChannel is { } propia
                ? new ReglasDeCargoMasivo.FormaResuelta(propia, null, null)
                : ReglasDeCargoMasivo.Forma(run.ChannelMode, run.RepaymentChannel, c.DeductionType, convenioPagaduria is not null,
                    contexto.ConvenioDeNominaPropia is not null, formasAdmitidas, v.DefaultRepaymentChannel);
            if (forma.Omision is { } sinConvenio)
            {
                lineas.Add(Omitir(sinConvenio, null));
                continue;
            }
            var canal = forma.Forma!.Value;
            if (canal == RepaymentChannel.PaymasterDeduction && convenioPagaduria is null)
            {
                lineas.Add(Omitir(MotivosDeCargoMasivo.SinPagaduria, null));
                continue;
            }
            if (canal == RepaymentChannel.OwnPayroll && contexto.ConvenioDeNominaPropia is null)
            {
                lineas.Add(Omitir(MotivosDeCargoMasivo.SinNominaPropia, null));
                continue;
            }
            var convenio = canal switch
            {
                RepaymentChannel.PaymasterDeduction => convenioPagaduria,
                RepaymentChannel.OwnPayroll => contexto.ConvenioDeNominaPropia,
                _ => null,
            };

            // La simulación de la combinación (valor, cuotas, primer pago), una vez.
            var clave = (monto, cuotas, primerPago);
            if (!simulaciones.TryGetValue(clave, out var sim))
            {
                sim = await SimularAsync(linea, run, monto, cuotas, primerPago, null, ct);
                simulaciones[clave] = sim;
            }
            if (sim.IsFailure)
            {
                // Con los mismos datos para todos, una falla de la línea es de la corrida entera.
                if (run.Source == BulkChargeSource.Filters) return Result.Failure<CalculoDeCorrida>(sim.Error);
                lineas.Add(Omitir(MotivosDeCargoMasivo.ReglaDeLinea, sim.Error.Message));
                continue;
            }
            lineas.Add(new LineaCalculada(c.PersonId, oficina, monto, cuotas, primerPago, canal, convenio, BulkChargeLineStatus.Pending, null, null,
                forma.Aviso, c.Row));
        }

        var incluidas = lineas.Where(l => l.Status == BulkChargeLineStatus.Pending).ToList();
        DecisionDeValidacionPrevia? contable = null;
        if (incluidas.Count > 0)
        {
            var muestra = simulaciones.Values.First(s => s.IsSuccess).Value;
            var oficinas = incluidas.Select(l => l.BranchId).Distinct().ToList();
            var sucursales = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.Id)).Select(b => b.PublicId).ToListAsync(ct);
            var hechos = sucursales.Select(s => HechoParaValidar(linea, muestra, s, run.ChargeDate, run.Amount)).ToList();
            contable = await validacionPrevia.DecidirAsync(hechos, OrigenDeValidacionPrevia.Pantalla, $"el cargo masivo de {linea.Code}", alertar: false,
                ct);
        }
        return Result.Success(new CalculoDeCorrida(lineas, Huella(run, v.Id, lineas), v.Id, contable));
    }

    /// <summary>La simulación del plan de una obligación con el simulador de la línea, dentro de sus rangos.</summary>
    public async Task<Result<SimulacionCalculada>> SimularAsync(CreditLine linea, BulkChargeRun run, decimal monto, int cuotas, DateOnly primerPago,
        Guid? persona, CancellationToken ct)
    {
        var pedido = new ScheduleSimulationInput(linea.PublicId, monto, cuotas, run.Periodicity, DisbursementDate: run.ChargeDate,
            FirstDueDate: primerPago, AmortizationSystem: run.AmortizationSystem, PersonPublicId: persona);
        return await simulador.SimularAsync(linea, pedido, new PerfilParaLaBanda(DebtorKind.Associate, false, null), run.ChargeDate, validarRangos: true, ct);
    }

    /// <summary>El hecho como se emitiría, para la validación previa: capital contra la contrapartida del cargo de la línea, en la sucursal.</summary>
    public static MensajeContableDto HechoParaValidar(CreditLine linea, SimulacionCalculada sim, Guid sucursal, DateOnly fecha, decimal monto)
    {
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = Guid.Empty,
            Type = CargoMasivoRegistradoV1.Type,
            Kind = IntegrationMessageKind.Business,
            OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = ClavesDeEvento.Confirmacion,
            BranchPublicId = sucursal,
        };
        var contenido = new CargoMasivoRegistradoV1
        {
            ValueDate = fecha,
            Dimensions = new LoanFactDimensionsV1
            {
                CreditLineCode = linea.Code,
                ModalityCode = sim.Combinacion.ModalityCode,
                SubsegmentCode = sim.Combinacion.SubsegmentCode,
                RiskCategory = CategoriaContableDeCartera.CategoriaDeCreditoNuevo,
                RiskSubcategory = "None",
                BranchPublicId = sucursal,
                ImpairmentModel = "ArrearsAnnex1",
                RiskSegment = sim.Combinacion.ModalityCode,
                ValueUnit = "Pesos",
            },
            Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = monto }],
            RulesVersion = sim.Plan.RulesVersion,
        };
        return new MensajeContableDto(sobre, contenido);
    }

    /// <summary>El motivo por el que un asociado ya no se carga a la fecha (retirado, fallecido o con el retiro en trámite), releído al ejecutar.</summary>
    public async Task<string?> ExclusionActualAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().Where(p => p.Id == personId).Select(p => new { p.IsAssociate, p.IsDeceased }).FirstOrDefaultAsync(ct);
        var ficha = await db.Associates.AsNoTracking().Where(a => a.PersonId == personId && !a.IsDeleted)
            .Select(a => new { a.Status, a.WithdrawalDate, a.RejoinDate }).FirstOrDefaultAsync(ct);
        var vivos = MembershipWithdrawal.Vivos.ToArray();
        var caso = await db.DeceasedHolderCases.AsNoTracking().AnyAsync(d => d.PersonId == personId && !d.IsDeleted && CasosVivos.Contains(d.Status), ct);
        var retiro = await db.MembershipWithdrawals.AsNoTracking().AnyAsync(w => w.PersonId == personId && !w.IsDeleted && vivos.Contains(w.Status), ct);
        return ReglasDeCargoMasivo.Exclusion(new ReglasDeCargoMasivo.Asociado(persona?.IsAssociate == true && ficha is not null, ficha?.Status,
            ficha?.WithdrawalDate, ficha?.RejoinDate, persona?.IsDeceased == true, caso, retiro), fecha);
    }

    // --------------------------------------------------------------------------------------------- escribir la vista --

    /// <summary>Reemplaza las líneas del borrador por las del cálculo (las anteriores quedan de baja) y sella la vista previa.</summary>
    public async Task AplicarAsync(BulkChargeRun run, CalculoDeCorrida calculo, DateTime ahora, CancellationToken ct)
    {
        var anteriores = await db.BulkChargeLines.Where(l => l.RunId == run.Id && !l.IsDeleted).ToListAsync(ct);
        foreach (var a in anteriores)
        {
            a.IsDeleted = true;
            a.DeletedAt = ahora;
        }
        foreach (var l in calculo.Lineas)
        {
            BulkChargeLine fila;
            if (l.Status == BulkChargeLineStatus.Skipped)
                fila = BulkChargeLine.Omitida(run.Id, l.PersonId, run.CreditLineId, run.Period, l.BranchId, l.Reason!, l.Detail, l.SourceRow);
            else
                fila = new BulkChargeLine
                {
                    RunId = run.Id, PersonId = l.PersonId, CreditLineId = run.CreditLineId, Period = run.Period, BranchId = l.BranchId,
                    SourceRow = l.SourceRow, Warning = l.Warning,
                };
            fila.Amount = l.Amount;
            fila.Installments = l.Installments;
            fila.FirstDueDate = l.FirstDueDate;
            fila.RepaymentChannel = l.Channel;
            fila.PayrollAgreementId = l.AgreementId;
            db.BulkChargeLines.Add(fila);
        }
        run.CreditLineVersionId = calculo.VersionId;
        var incluidas = calculo.Incluidas.ToList();
        run.SellarVistaPrevia(calculo.Huella, calculo.Lineas.Count, incluidas.Count, calculo.Lineas.Count - incluidas.Count, incluidas.Sum(l => l.Amount),
            ahora);
    }

    /// <summary>La huella de la corrida: sus datos y la decisión de cada asociado, en orden de persona.</summary>
    public static string Huella(BulkChargeRun run, int versionId, IReadOnlyList<LineaCalculada> lineas)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(inv, $"{run.CreditLineId}|{versionId}|{run.Period}|{run.ChargeDate:yyyy-MM-dd}|{run.Amount:0.00}|{run.Installments}|{run.Periodicity}|")
            .Append(inv, $"{run.ChannelMode}|{run.RepaymentChannel}|{run.PaymentCycle}|{run.FirstDueDate:yyyy-MM-dd}|{run.CounterpartMode}|{run.CounterpartPersonId}\n");
        foreach (var l in lineas.OrderBy(l => l.PersonId).ThenBy(l => l.SourceRow))
            sb.Append(inv, $"{l.PersonId}|{l.Status}|{l.Reason}|{l.Amount:0.00}|{l.Installments}|{l.FirstDueDate:yyyy-MM-dd}|{l.Channel}|{l.AgreementId}|{l.BranchId}\n");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    // ------------------------------------------------------------------------------------------------------ población --

    /// <summary>Un candidato: la persona con lo de su ficha de asociado y, del plano, sus valores propios o el error de su fila.</summary>
    private sealed record Candidato(
        int PersonId,
        int? BranchId,
        string? DeductionType,
        int? EmployerCompanyId,
        int? Row,
        decimal? Amount,
        int? Installments,
        DateOnly? FirstDueDate,
        RepaymentChannel? RepaymentChannel,
        string? Error = null,
        string? ErrorDetail = null);

    private async Task<List<Candidato>> CandidatosAsync(BulkChargeRun run, OrigenDeCargoMasivo origen, CancellationToken ct)
    {
        if (run.Source == BulkChargeSource.File)
        {
            var filas = origen.Rows ?? [];
            var personas = filas.Select(f => f.PersonId).Distinct().ToList();
            var fichas = await db.Associates.AsNoTracking().Where(a => personas.Contains(a.PersonId) && !a.IsDeleted)
                .Select(a => new { a.PersonId, a.BranchId, a.DeductionType, a.EmployerCompanyId }).ToListAsync(ct);
            var porPersona = fichas.GroupBy(f => f.PersonId).ToDictionary(g => g.Key, g => g.First());
            return filas.Select(f => porPersona.TryGetValue(f.PersonId, out var a)
                    ? new Candidato(f.PersonId, a.BranchId, a.DeductionType, a.EmployerCompanyId, f.Row, f.Amount, f.Installments, f.FirstDueDate,
                        f.RepaymentChannel)
                    : new Candidato(f.PersonId, null, null, null, f.Row, f.Amount, f.Installments, f.FirstDueDate, f.RepaymentChannel,
                        MotivosDeCargoMasivo.NoAsociado))
                .ToList();
        }

        var filtros = origen.Filters ?? new BulkChargeFiltersInput();
        var consulta = db.Associates.AsNoTracking().Where(a => !a.IsDeleted && a.Status != null && a.Status.Trim().ToUpper() == "A");
        if (filtros.BranchPublicIds is { Count: > 0 } agencias)
        {
            var ids = await db.Branches.AsNoTracking().Where(b => agencias.Contains(b.PublicId)).Select(b => b.Id).ToListAsync(ct);
            consulta = consulta.Where(a => a.BranchId != null && ids.Contains(a.BranchId.Value));
        }
        if (filtros.DeductionTypes is { Count: > 0 } tipos)
        {
            var t = tipos.Select(x => x.Trim().ToUpperInvariant()).ToList();
            consulta = consulta.Where(a => a.DeductionType != null && t.Contains(a.DeductionType.Trim().ToUpper()));
        }
        if (filtros.DeductionPeriods is { Count: > 0 } periodos)
        {
            var p = periodos.Select(x => x.Trim().ToUpperInvariant()).ToList();
            consulta = consulta.Where(a => a.DeductionPeriod != null && p.Contains(a.DeductionPeriod.Trim().ToUpper()));
        }
        if (filtros.EmployerCompanyPublicIds is { Count: > 0 } empresas)
        {
            var ids = await db.EmployerCompanies.AsNoTracking().Where(e => empresas.Contains(e.PublicId)).Select(e => e.Id).ToListAsync(ct);
            consulta = consulta.Where(a => a.EmployerCompanyId != null && ids.Contains(a.EmployerCompanyId.Value));
        }
        var lista = await consulta.Select(a => new { a.PersonId, a.BranchId, a.DeductionType, a.EmployerCompanyId }).ToListAsync(ct);
        return lista.GroupBy(a => a.PersonId).Select(g => g.First())
            .Select(a => new Candidato(a.PersonId, a.BranchId, a.DeductionType, a.EmployerCompanyId, null, null, null, null, null)).ToList();
    }

    /// <summary>Lo que se consulta de una vez para todos los candidatos.</summary>
    private sealed class Contexto
    {
        public required Dictionary<int, (bool EsAsociado, bool Fallecido)> Personas { get; init; }
        public required Dictionary<int, (string? Estado, DateOnly? Retiro, DateOnly? Reingreso)> Fichas { get; init; }
        public required HashSet<int> ConCasoDeFallecimiento { get; init; }
        public required HashSet<int> ConRetiroVivo { get; init; }
        public required Dictionary<int, string> YaCargados { get; init; }
        public required Dictionary<int, int> ConvenioPorEmpresa { get; init; }
        public int? ConvenioDeNominaPropia { get; init; }

        public ReglasDeCargoMasivo.Asociado Asociado(Candidato c)
        {
            var persona = Personas.TryGetValue(c.PersonId, out var p) ? p : (EsAsociado: false, Fallecido: false);
            var ficha = Fichas.TryGetValue(c.PersonId, out var f) ? f : (Estado: null, Retiro: null, Reingreso: null);
            return new ReglasDeCargoMasivo.Asociado(persona.EsAsociado && Fichas.ContainsKey(c.PersonId), ficha.Estado, ficha.Retiro, ficha.Reingreso,
                persona.Fallecido, ConCasoDeFallecimiento.Contains(c.PersonId), ConRetiroVivo.Contains(c.PersonId));
        }
    }

    private async Task<Contexto> ContextoAsync(BulkChargeRun run, List<int> personas, CancellationToken ct)
    {
        var p = new Dictionary<int, (bool EsAsociado, bool Fallecido)>();
        var f = new Dictionary<int, (string? Estado, DateOnly? Retiro, DateOnly? Reingreso)>();
        var casos = new HashSet<int>();
        var retiros = new HashSet<int>();
        var cargados = new Dictionary<int, string>();
        var vivos = MembershipWithdrawal.Vivos.ToArray();
        foreach (var trozo in personas.Chunk(1000))
        {
            foreach (var x in await db.People.AsNoTracking().Where(x => trozo.Contains(x.Id))
                         .Select(x => new { x.Id, x.IsAssociate, x.IsDeceased }).ToListAsync(ct))
                p[x.Id] = (x.IsAssociate, x.IsDeceased);
            foreach (var a in await db.Associates.AsNoTracking().Where(a => trozo.Contains(a.PersonId) && !a.IsDeleted)
                         .Select(a => new { a.PersonId, a.Status, a.WithdrawalDate, a.RejoinDate }).ToListAsync(ct))
                f.TryAdd(a.PersonId, (a.Status, a.WithdrawalDate, a.RejoinDate));
            casos.UnionWith(await db.DeceasedHolderCases.AsNoTracking()
                .Where(d => trozo.Contains(d.PersonId) && !d.IsDeleted && CasosVivos.Contains(d.Status)).Select(d => d.PersonId).ToListAsync(ct));
            retiros.UnionWith(await db.MembershipWithdrawals.AsNoTracking()
                .Where(w => trozo.Contains(w.PersonId) && !w.IsDeleted && vivos.Contains(w.Status)).Select(w => w.PersonId).ToListAsync(ct));
            var ocupados = await (from l in db.BulkChargeLines.AsNoTracking()
                                  join r in db.BulkChargeRuns.AsNoTracking() on l.RunId equals r.Id
                                  where trozo.Contains(l.PersonId) && l.CreditLineId == run.CreditLineId && l.Period == run.Period && l.HoldsPeriod
                                        && !l.IsDeleted && l.RunId != run.Id
                                  select new { l.PersonId, r.Number }).ToListAsync(ct);
            foreach (var o in ocupados) cargados.TryAdd(o.PersonId, o.Number);
        }

        // La pagaduría de cada empresa empleadora: la persona con su NIT y su convenio activo que no es la nómina propia.
        var empresas = await db.EmployerCompanies.AsNoTracking().Where(e => !e.IsDeleted && e.TaxId != null)
            .Select(e => new { e.Id, e.TaxId }).ToListAsync(ct);
        var convenios = await (from c in db.PayrollAgreements.AsNoTracking()
                               join per in db.People.AsNoTracking() on c.PaymasterPersonId equals per.Id
                               where !c.IsDeleted && !c.IsOwnPayroll && c.Status == PayrollAgreementStatus.Active
                               select new { c.Id, per.TaxId }).ToListAsync(ct);
        var convenioPorNit = convenios.Where(c => !string.IsNullOrWhiteSpace(c.TaxId)).GroupBy(c => c.TaxId.Trim())
            .ToDictionary(g => g.Key, g => g.Min(c => c.Id));
        var porEmpresa = new Dictionary<int, int>();
        foreach (var e in empresas)
            if (convenioPorNit.TryGetValue(e.TaxId!.Trim(), out var id)) porEmpresa[e.Id] = id;
        var propia = await db.PayrollAgreements.AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsOwnPayroll && c.Status == PayrollAgreementStatus.Active).OrderBy(c => c.Id).Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);

        return new Contexto
        {
            Personas = p, Fichas = f, ConCasoDeFallecimiento = casos, ConRetiroVivo = retiros, YaCargados = cargados, ConvenioPorEmpresa = porEmpresa,
            ConvenioDeNominaPropia = propia,
        };
    }

    // ---------------------------------------------------------------------------------------------------------- dto --

    public async Task<BulkChargeRunDto> DtoAsync(BulkChargeRun run, DecisionDeValidacionPrevia? contable, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == run.CreditLineId).Select(l => new { l.PublicId, l.Code, l.Name }).FirstAsync(ct);
        var contraparte = run.CounterpartPersonId is { } cp
            ? await db.People.AsNoTracking().Where(x => x.Id == cp)
                .Select(x => new { x.PublicId, Nombre = x.BusinessName ?? (x.FirstName + " " + x.LastName) }).FirstOrDefaultAsync(ct)
            : null;
        var usuarios = new[] { (int?)run.PreparedByUserId, run.ConfirmedByUserId }.Where(u => u is not null).Select(u => u!.Value).ToList();
        var nombres = await db.Users.AsNoTracking().Where(u => usuarios.Contains(u.Id)).Select(u => new { u.Id, FullName = u.Username }).ToListAsync(ct);
        string? Nombre(int? id) => id is { } i ? nombres.FirstOrDefault(n => n.Id == i)?.FullName : null;
        var corrida = run.ProcessRunId is { } pr
            ? await db.LendingProcessRuns.AsNoTracking().Where(r => r.Id == pr).Select(r => new { r.PublicId, Estado = r.Status.ToString() }).FirstOrDefaultAsync(ct)
            : null;
        var omitidas = await db.BulkChargeLines.AsNoTracking()
            .Where(l => l.RunId == run.Id && !l.IsDeleted && (l.Status == BulkChargeLineStatus.Skipped || l.Status == BulkChargeLineStatus.Failed))
            .GroupBy(l => l.Reason).Select(g => new { Motivo = g.Key, Cuantos = g.Count() }).ToListAsync(ct);
        var origen = OrigenDeCargoMasivo.Leer(run.FiltersJson);
        BulkChargeAccountingDto? contabilidad = contable is null
            ? null
            : new BulkChargeAccountingDto(contable.Resultado.Outcome.ToString(), contable.Bloqueo is not null,
                (contable.Bloqueo is { } b ? new[] { b.Message } : []).Concat(contable.Resultado.Errors.Select(e => e.Message)).Concat(contable.Avisos)
                .Distinct().ToList(), TipoDeComprobante);
        var puedeConfirmar = run.EsBorrador && run.PendingCount > 0 && run.PreviewSha256 is not null && contable?.Bloqueo is null;
        return new BulkChargeRunDto(run.PublicId, run.Number, run.Status, linea.PublicId, linea.Code, linea.Name, run.Period, run.ChargeDate,
            run.FirstDueDate, run.Amount, run.Installments, run.Periodicity, run.ChannelMode, run.RepaymentChannel, run.PaymentCycle, run.CounterpartMode,
            contraparte?.PublicId, contraparte?.Nombre, run.Source, origen.Filters ?? new BulkChargeFiltersInput(), origen.Rows?.Count ?? 0,
            run.PreviewSha256, run.PreviewedAt, run.TotalLines, run.PendingCount, run.CreatedCount, run.SkippedCount, run.FailedCount, run.VoidedCount,
            run.TotalAmount, omitidas.Select(o => new BulkChargeReasonCountDto(o.Motivo ?? "", o.Cuantos)).OrderByDescending(o => o.Count).ToList(),
            contabilidad, puedeConfirmar, Nombre(run.PreparedByUserId), Nombre(run.ConfirmedByUserId), run.ConfirmedAt, run.CompletedAt,
            corrida?.PublicId, corrida?.Estado, run.BatchNumber, run.VoidReason, run.VoidedAt, run.CreatedAt);
    }
}
