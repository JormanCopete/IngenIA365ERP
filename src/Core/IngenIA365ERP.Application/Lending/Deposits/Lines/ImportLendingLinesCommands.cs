using System.Globalization;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Lines.PlantillasDeLineasDeCaptacion.C;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>
/// Plantilla de líneas de captación (feature 014, E251; <c>POST /api/lending/lines/deposit/import?mode=review|apply</c>, <c>Lending.DepositLines.Manage</c>).
/// Cada fila es una versión con <b>las mismas reglas que el alta unitaria</b>: nada se guarda si una fila falla. Una línea nueva se crea, una existente
/// recibe una versión nueva, una versión igual a la registrada queda «sin cambio». (nuevo)
/// </summary>
public sealed record ImportDepositLinesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportDepositLinesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    AltaDeVersionDeCaptacion alta,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    IDateTimeService reloj) : IRequestHandler<ImportDepositLinesCommand, Result<ImportResultDto>>
{
    private const string MotivoPorDefecto = "Importación de líneas de captación";

    private sealed class Pendiente(FilaDeImportacion fila, string codigo, DateOnly desde)
    {
        public FilaDeImportacion Fila { get; } = fila;
        public string Codigo { get; } = codigo;
        public DateOnly Desde { get; } = desde;
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; } = true;
        public DepositClass Clase { get; set; }
        public DepositLineVersionInput? Input { get; set; }
        public BorradorDeVersionDeCaptacion? Borrador { get; set; }
        public List<(FilaDeImportacion Fila, DepositRateTierInput Tramo)> Tramos { get; } = [];
        public string Llave => $"{Codigo}|{Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    public Task<Result<ImportResultDto>> Handle(ImportDepositLinesCommand request, CancellationToken ct)
    {
        var aplicar = new List<Pendiente>();
        return ejecutor.EjecutarAsync(PlantillasDeLineasDeCaptacion.Captacion, request, (ctx, c) => ProcesarAsync(ctx, aplicar, c), ct,
            (ctx, c) => AplicarAsync(ctx, aplicar, c));
    }

    private async Task ProcesarAsync(ContextoDeImportacion ctx, List<Pendiente> aplicar, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var minima = await ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Deposit, hoy, ct);
        var vigente = await alcance.ObtenerAsync(ct);
        var perfil = await contexto.PerfilAsync(hoy, ct);
        var hoja = ctx.Hoja(PlantillasDeLineasDeCaptacion.HojaLineas);
        var leidas = new List<Pendiente>();
        foreach (var f in hoja.Filas)
        {
            var codigo = f.Codigo(C.Codigo, 10);
            var desde = f.Fecha(C.VigenteDesde);
            if (codigo is null || desde is null) continue;
            var p = new Pendiente(f, codigo, desde.Value);
            if (!hoja.LlaveUnica(f, p.Llave, C.Codigo)) continue;
            LeerFila(f, p, ctx);
            leidas.Add(p);
        }
        var porLlave = leidas.ToDictionary(p => p.Llave, StringComparer.Ordinal);
        foreach (var f in ctx.Hoja(PlantillasDeLineasDeCaptacion.HojaTramos).Filas)
        {
            var codigo = f.Codigo(C.Codigo, 10);
            var desde = f.Fecha(C.VigenteDesde);
            if (codigo is null || desde is null) continue;
            var llave = $"{codigo}|{desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            var tasa = f.Porcentaje(C.TasaEA);
            var tramo = new DepositRateTierInput(f.Monto(C.SaldoDesde), f.Monto(C.SaldoHasta), f.Entero(C.PlazoDesdeDias), f.Entero(C.PlazoHastaDias),
                f.Monto(C.MontoDesde), f.Monto(C.MontoHasta), tasa);
            if (!porLlave.TryGetValue(llave, out var padre))
                f.Error(C.Codigo, ImportErrors.CellNotFound, $"La hoja Lineas no tiene la versión {codigo} desde {desde:yyyy-MM-dd}: el tramo cuelga de una versión de esa hoja.");
            else padre.Tramos.Add((f, tramo));
        }

        var codigos = leidas.Select(p => p.Codigo).Distinct().ToArray();
        var lineas = await db.CreditLines.Where(l => codigos.Contains(l.Code) && !l.IsDeleted).ToListAsync(ct);
        var ids = lineas.Select(l => l.Id).ToArray();
        var registradas = await db.DepositLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var clases = new Dictionary<int, DepositClass>();
        foreach (var l in lineas.Where(l => l.Nature == LineNature.Deposit))
            clases[l.Id] = await LectorDeLineasDeCaptacion.ClaseDeAsync(db, l.Id, ct) ?? DepositClass.Demand;

        foreach (var p in leidas.OrderBy(p => p.Codigo, StringComparer.Ordinal).ThenBy(p => p.Desde))
        {
            if (p.Fila.TieneErrores || p.Input is null || p.Tramos.Any(t => t.Fila.TieneErrores)) continue;
            var linea = lineas.FirstOrDefault(l => l.Code == p.Codigo);
            if (linea is not null && linea.Id != 0 && !vigente.IncluyeLinea(linea.Id))
            {
                // Fuera del alcance de quien importa la línea no existe (FR-004): el mismo mensaje que un código desconocido.
                p.Fila.Error(C.Codigo, ErroresDeLineasDeCaptacion.CodigoLineaInexistente, ErroresDeLineasDeCaptacion.LineaInexistente().Message);
                continue;
            }
            if (linea is not null && linea.Nature != LineNature.Deposit)
            {
                p.Fila.Error(C.Codigo, ErroresDeLineasDeCaptacion.CodigoUsarRutaDeCredito, $"El código {p.Codigo} ya es de una línea de {ErroresDeLineasDeCaptacion.NombreDe(linea.Nature)}.");
                continue;
            }
            if (linea is not null && clases.TryGetValue(linea.Id, out var claseActual) && claseActual != p.Clase)
            {
                p.Fila.Error(C.Clase, "Lending.DepositLine.ClassImmutable", $"La línea {p.Codigo} es de clase {claseActual}: la clase no cambia.");
                continue;
            }
            int? destino = null;
            if (p.Input.TermDeposit?.DemandLinePublicId is { } publica)
            {
                var alVista = await db.CreditLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == publica && !l.IsDeleted, ct);
                destino = alVista?.Id;
            }
            var borrador = p.Input.Borrador(p.Clase, destino, perfil.SoloAsociados);
            p.Borrador = borrador;

            if (linea is not null)
            {
                var suyas = registradas.Where(r => r.CreditLineId == linea.Id).ToList();
                var igual = suyas.FirstOrDefault(r => r.ValidFrom == p.Desde
                    && r.ContentSha256 == DepositLineVersion.Registrar(linea.Id, borrador, 1, DateOnly.MinValue, reloj.UtcNow).ContentSha256);
                if (igual is not null)
                {
                    ctx.Registrar(p.Fila, p.Llave, AccionDeImportacion.Unchanged);
                    continue;
                }
                if (linea.Status == CreditLineStatus.Closed)
                {
                    p.Fila.Error(C.Codigo, "Lending.CreditLine.Closed", $"La línea {p.Codigo} está cerrada: reábrala antes de registrarle una versión.");
                    continue;
                }
                var contra = await alta.ValidarContraLoRegistradoAsync(linea, p.Desde, ct);
                if (contra.IsFailure) { Marcar(p, ctx, contra.Error); continue; }
            }
            else if (destino is null && p.Input.TermDeposit?.DemandLinePublicId is not null)
            {
                p.Fila.Error(C.LineaALaVista, ErroresDeLineasDeCaptacion.CodigoLineaReferidaInvalida, "La línea a la vista indicada no existe.");
                continue;
            }
            if (ReglasDeLineaDeCaptacion.ValidarVersion(borrador, minima) is { } problema) { Marcar(p, ctx, ErroresDeLineasDeCaptacion.De(problema)); continue; }
            if (p.Activa)
            {
                if (EntregaDeLineas.NoDisponible(LineNature.Deposit, contexto.Entrega) is { } sinEntrega) { Marcar(p, ctx, sinEntrega); continue; }
                if (ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(p.Clase, perfil.AutorizaCaptar, perfil.TipoDeOrganizacion, perfil.PerfilPublicId) is { } noAutorizada)
                { Marcar(p, ctx, ErroresDeLineasDeCaptacion.De(noAutorizada)); continue; }
            }

            aplicar.Add(p);
            ctx.PedirMotivo();
            ctx.Registrar(p.Fila, p.Llave, linea is null ? AccionDeImportacion.Create : AccionDeImportacion.Update);
            foreach (var (fila, _) in p.Tramos) ctx.Registrar(fila, p.Llave, AccionDeImportacion.Create);
            if (linea is null && lineas.All(l => l.Code != p.Codigo))
            {
                var nueva = CreditLine.Nueva(p.Codigo, p.Nombre, null, LineNature.Deposit);
                db.CreditLines.Add(nueva);
                lineas.Add(nueva);
            }
        }
    }

    private async Task AplicarAsync(ContextoDeImportacion ctx, List<Pendiente> aplicar, CancellationToken ct)
    {
        foreach (var p in aplicar)
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Code == p.Codigo && !l.IsDeleted, ct);
            linea.Name = p.Nombre;
            await alta.RegistrarAsync(linea, p.Borrador!, ct);
            if (p.Activa && linea.Status != CreditLineStatus.Active) linea.Activar();
        }
        _ = ctx;
    }

    private static void Marcar(Pendiente p, ContextoDeImportacion ctx, Error error)
    {
        var campo = error is ErrorConDatos { Data: { } d } ? CampoDe(d) : null;
        var columna = campo switch
        {
            "validFrom" => C.VigenteDesde,
            "reason" => C.Motivo,
            "rate" or "annualRate" => C.TasaEA,
            _ => C.Codigo,
        };
        p.Fila.Error(columna, error.Code, error.Message);
    }

    private static string? CampoDe(object data)
    {
        var t = data.GetType().GetProperty("errors")?.GetValue(data) as System.Collections.IEnumerable;
        if (t is null) return null;
        foreach (var e in t) return e?.GetType().GetProperty("Field")?.GetValue(e)?.ToString();
        return null;
    }

    private void LeerFila(FilaDeImportacion f, Pendiente p, ContextoDeImportacion ctx)
    {
        p.Nombre = f.Texto(C.Nombre, 120) ?? string.Empty;
        p.Activa = f.SiNo(C.Activa, porDefecto: true);
        var clase = f.Enumeracion<DepositClass>(C.Clase);
        var tasa = f.Porcentaje(C.TasaEA);
        var modoTramos = f.Enumeracion<RateTierMode>(C.ModoDeTramos);
        var metodo = f.Enumeracion<BalanceMethod>(C.MetodoDeSaldo);
        var convencion = f.Enumeracion<DayCountConvention>(C.ConvencionDeDias);
        var liquidacion = f.Enumeracion<RepaymentPeriodicity>(C.PeriodicidadDeLiquidacion);
        var interes = f.Lista(C.PeriodicidadesDeInteres);
        var periodicidades = new List<RepaymentPeriodicity>();
        foreach (var texto in interes ?? [])
        {
            if (Enum.TryParse<RepaymentPeriodicity>(texto, ignoreCase: true, out var per)) periodicidades.Add(per);
            else f.Error(C.PeriodicidadesDeInteres, ImportErrors.CellFormat, $"«{texto}» no es una periodicidad.");
        }
        var regla = f.Enumeracion<RolloverRateRule>(C.ReglaDeTasaEnProrroga);
        var penalidad = f.Enumeracion<EarlyRedemptionPenaltyMode>(C.ModoDePenalidad);
        var periodicidadDeCuota = f.Enumeracion<RepaymentPeriodicity>(C.PeriodicidadDeCuota);
        var efecto = f.Enumeracion<ContractualBreachEffect>(C.EfectoDelIncumplimiento);
        var alVista = f.Codigo(C.LineaALaVista, 10);
        Guid? alVistaPublica = null;
        if (alVista is not null)
        {
            var existente = db.CreditLines.AsNoTracking().FirstOrDefault(l => l.Code == alVista && !l.IsDeleted && l.Nature == LineNature.Deposit);
            if (existente is null) f.Error(C.LineaALaVista, ErroresDeLineasDeCaptacion.CodigoLineaReferidaInvalida, $"No hay una línea de captación «{alVista}»: créela antes.");
            else alVistaPublica = existente.PublicId;
        }
        var minApertura = f.Monto(C.MinimoApertura);
        var minMantener = f.Monto(C.MinimoMantener);
        var minInteres = f.Monto(C.MinimoParaInteres);
        var maxOperacion = f.Monto(C.RetiroMaximoPorOperacion);
        var maxDia = f.Monto(C.RetiroMaximoPorDia);
        var gmf = f.SiNo(C.AplicaGmf, porDefecto: true);
        var diaLiquidacion = f.Entero(C.DiaDeLiquidacion);
        var inactividad = f.Entero(C.DiasDeInactividad);
        if (clase is null || f.TieneErrores) return;
        p.Clase = clase.Value;

        TermDepositLineInput? cdat = null;
        ContractualLineInput? contractual = null;
        PermanentLineInput? permanente = null;
        switch (clase.Value)
        {
            case DepositClass.TermDeposit:
                cdat = new TermDepositLineInput
                {
                    MinTermDays = f.Entero(C.PlazoMinimoDias) ?? 0,
                    MaxTermDays = f.Entero(C.PlazoMaximoDias) ?? 0,
                    InterestPeriodicities = periodicidades.Count == 0 ? [RepaymentPeriodicity.SinglePayment] : periodicidades,
                    RolloverAutomatic = f.SiNo(C.ProrrogaAutomatica, porDefecto: true),
                    MaxRollovers = f.Entero(C.MaximoDeProrrogas),
                    RolloverRateRule = regla ?? RolloverRateRule.CurrentTableRate,
                    GraceBusinessDays = f.Entero(C.DiasHabilesDeGracia) ?? 3,
                    DemandLinePublicId = alVistaPublica,
                    EarlyRedemption = new EarlyRedemptionInput(f.SiNo(C.RedencionAnticipada), penalidad, f.Porcentaje(C.PenalidadEA), f.Porcentaje(C.DescuentoDePenalidad),
                        f.Entero(C.DiasMinimosParaRedimir)),
                };
                break;
            case DepositClass.Contractual:
                contractual = new ContractualLineInput
                {
                    InstallmentPeriodicity = periodicidadDeCuota ?? RepaymentPeriodicity.Monthly,
                    MinInstallment = f.Monto(C.CuotaMinima) ?? 0m,
                    TermMonthsFrom = f.Entero(C.PlazoMesesDesde) ?? 0,
                    TermMonthsTo = f.Entero(C.PlazoMesesHasta) ?? 0,
                    BonusRateEa = f.Porcentaje(C.TasaDeBonificacionEA),
                    BreachRateEa = f.Porcentaje(C.TasaDeIncumplimientoEA),
                    ToleranceInstallments = f.Entero(C.CuotasDeTolerancia),
                    BreachEffect = efecto ?? ContractualBreachEffect.MoveToDemand,
                };
                break;
            case DepositClass.Permanent:
                permanente = new PermanentLineInput(f.SiNo(C.RetirableSoloAlRetiro, porDefecto: true));
                break;
        }
        if (f.TieneErrores) return;

        p.Input = new DepositLineVersionInput
        {
            ValidFrom = p.Desde,
            Reason = f.Texto(C.Motivo, 300) ?? ctx.Motivo ?? MotivoPorDefecto,
            DepositClass = clase,
            RequiresMembership = f.SiNo(C.ExigeAsociado, porDefecto: true),
            AllowsCoHolders = f.SiNo(C.AdmiteCotitulares),
            AllowsMinors = f.SiNo(C.AdmiteMenores),
            AllowsLegalEntities = f.SiNo(C.AdmitePersonasJuridicas),
            AllowsPartialWithdrawals = f.SiNo(C.AdmiteRetirosParciales),
            Rate = new DepositRateInput
            {
                Kind = RateKind.Fixed,
                EffectiveAnnual = tasa,
                TierMode = p.Tramos.Count == 0 ? null : modoTramos ?? RateTierMode.WholeBalance,
                Tiers = p.Tramos.Select(t => t.Tramo).ToList(),
            },
            DeclaresAgreedRate = f.SiNo(C.TasaPactada),
            NegotiatedRateAllowed = f.SiNo(C.AdmiteTasaNegociada),
            RateChangeNoticeDays = f.Entero(C.AvisoDeCambioDeTasaDias) ?? 0,
            BalanceMethod = metodo ?? BalanceMethod.DailyBalance,
            Minimums = new DepositMinimumsInput(minApertura ?? 0m, minMantener ?? 0m, minInteres ?? 0m),
            DayCount = convencion ?? DayCountConvention.Actual365,
            SettlementPeriodicity = liquidacion ?? RepaymentPeriodicity.Monthly,
            SettlementDay = diaLiquidacion is { } d ? (byte)Math.Clamp(d, 0, 255) : null,
            Tax = new DepositTaxInput(f.Texto(C.ClaveDeRetencion, 60), gmf),
            Limits = new DepositLimitsInput(maxOperacion, maxDia),
            Inactivity = new DepositInactivityInput(inactividad ?? 360),
            TermDeposit = cdat,
            Contractual = contractual,
            Permanent = permanente,
        };
    }
}

/// <summary>
/// Plantilla de la línea de aportes (feature 014, E251; <c>POST /api/lending/lines/contribution/import?mode=review|apply</c>,
/// <c>Lending.ContributionLines.Manage</c>): una versión por fila, con las reglas del alta unitaria (topes del fondo de empleados y límite de participación
/// incluidos). Una sola línea de aportes activa por cooperativa. (nuevo)
/// </summary>
public sealed record ImportContributionLinesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportContributionLinesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    AltaDeVersionDeCaptacion alta,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    IDateTimeService reloj) : IRequestHandler<ImportContributionLinesCommand, Result<ImportResultDto>>
{
    private const string MotivoPorDefecto = "Importación de la línea de aportes";

    private sealed class Pendiente(FilaDeImportacion fila, string codigo, DateOnly desde)
    {
        public FilaDeImportacion Fila { get; } = fila;
        public string Codigo { get; } = codigo;
        public DateOnly Desde { get; } = desde;
        public string Nombre { get; set; } = string.Empty;
        public bool Activa { get; set; } = true;
        public ContributionLineVersionInput? Input { get; set; }
        public Guid? LineaPermanente { get; set; }
        public BorradorDeVersionDeAportes? Borrador { get; set; }
        public string Llave => $"{Codigo}|{Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    public Task<Result<ImportResultDto>> Handle(ImportContributionLinesCommand request, CancellationToken ct)
    {
        var aplicar = new List<Pendiente>();
        return ejecutor.EjecutarAsync(PlantillasDeLineasDeCaptacion.Aportes, request, (ctx, c) => ProcesarAsync(ctx, aplicar, c), ct,
            (_, c) => AplicarAsync(aplicar, c));
    }

    private async Task ProcesarAsync(ContextoDeImportacion ctx, List<Pendiente> aplicar, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var minima = await ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Contribution, hoy, ct);
        var vigente = await alcance.ObtenerAsync(ct);
        var hoja = ctx.Hoja(PlantillasDeLineasDeCaptacion.HojaLineas);
        var leidas = new List<Pendiente>();
        foreach (var f in hoja.Filas)
        {
            var codigo = f.Codigo(C.Codigo, 10);
            var desde = f.Fecha(C.VigenteDesde);
            if (codigo is null || desde is null) continue;
            var p = new Pendiente(f, codigo, desde.Value);
            if (!hoja.LlaveUnica(f, p.Llave, C.Codigo)) continue;
            Leer(f, p, ctx);
            leidas.Add(p);
        }

        var codigos = leidas.Select(p => p.Codigo).Distinct().ToArray();
        var lineas = await db.CreditLines.Where(l => codigos.Contains(l.Code) && !l.IsDeleted).ToListAsync(ct);
        var ids = lineas.Select(l => l.Id).ToArray();
        var registradas = await db.ContributionLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var activas = leidas.Where(p => p.Activa).Select(p => p.Codigo).Distinct().ToList();
        if (activas.Count > 1)
            foreach (var p in leidas.Where(p => p.Activa && p.Codigo != activas[0]))
                p.Fila.Error(C.Activa, ErroresDeLineasDeCaptacion.CodigoAportesYaActiva, "La cooperativa tiene una sola línea de aportes activa: el archivo trae más de una.");

        foreach (var p in leidas.OrderBy(p => p.Codigo, StringComparer.Ordinal).ThenBy(p => p.Desde))
        {
            if (p.Fila.TieneErrores || p.Input is null) continue;
            var linea = lineas.FirstOrDefault(l => l.Code == p.Codigo);
            if (linea is not null && linea.Id != 0 && !vigente.IncluyeLinea(linea.Id))
            {
                p.Fila.Error(C.Codigo, ErroresDeLineasDeCaptacion.CodigoLineaInexistente, ErroresDeLineasDeCaptacion.LineaInexistente().Message);
                continue;
            }
            if (linea is not null && linea.Nature != LineNature.Contribution)
            {
                p.Fila.Error(C.Codigo, ErroresDeLineasDeCaptacion.CodigoUsarRutaDeCredito, $"El código {p.Codigo} ya es de una línea de {ErroresDeLineasDeCaptacion.NombreDe(linea.Nature)}.");
                continue;
            }
            int? permanenteId = null;
            if (p.Input.PermanentSavingsShare is not null)
            {
                var r = await ReferenciasDeLineasDeAportes.LineaPermanenteAsync(db, p.Input, ct);
                if (r.IsFailure) { p.Fila.Error(C.LineaDeAhorroPermanente, r.Error.Code, r.Error.Message); continue; }
                permanenteId = r.Value;
            }
            var borrador = p.Input.Borrador(permanenteId);
            p.Borrador = borrador;

            if (linea is not null)
            {
                var suyas = registradas.Where(r => r.CreditLineId == linea.Id).ToList();
                var igual = suyas.FirstOrDefault(r => r.ValidFrom == p.Desde
                    && r.ContentSha256 == ContributionLineVersion.Registrar(linea.Id, borrador, 1, DateOnly.MinValue, reloj.UtcNow).ContentSha256);
                if (igual is not null) { ctx.Registrar(p.Fila, p.Llave, AccionDeImportacion.Unchanged); continue; }
                if (linea.Status == CreditLineStatus.Closed)
                {
                    p.Fila.Error(C.Codigo, "Lending.CreditLine.Closed", $"La línea {p.Codigo} está cerrada: reábrala antes de registrarle una versión.");
                    continue;
                }
                var contra = await alta.ValidarContraLoRegistradoAsync(linea, p.Desde, ct);
                if (contra.IsFailure) { p.Fila.Error(C.VigenteDesde, contra.Error.Code, contra.Error.Message); continue; }
            }
            if (ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(borrador, minima) is { } problema)
            {
                var e = ErroresDeLineasDeCaptacion.De(problema);
                p.Fila.Error(problema.Datos is { } d && d.TryGetValue("field", out var campo) && campo?.ToString() == "validFrom" ? C.VigenteDesde : C.CuotaValor, e.Code, e.Message);
                continue;
            }
            var topes = await ReferenciasDeLineasDeAportes.TopesAsync(contexto, borrador, ct);
            if (topes.IsFailure) { p.Fila.Error(C.CuotaValor, topes.Error.Code, topes.Error.Message); continue; }
            if (p.Activa)
            {
                if (EntregaDeLineas.NoDisponible(LineNature.Contribution, contexto.Entrega) is { } sinEntrega) { p.Fila.Error(C.Activa, sinEntrega.Code, sinEntrega.Message); continue; }
                if (await ReferenciasDeLineasDeAportes.OtraActivaAsync(db, linea?.Id, ct) is { } otra && !aplicar.Any(a => a.Codigo == otra.Code))
                {
                    var e = ErroresDeLineasDeCaptacion.AportesYaActiva(otra.Code, otra.Name);
                    p.Fila.Error(C.Activa, e.Code, e.Message);
                    continue;
                }
            }

            aplicar.Add(p);
            ctx.PedirMotivo();
            ctx.Registrar(p.Fila, p.Llave, linea is null ? AccionDeImportacion.Create : AccionDeImportacion.Update);
            if (linea is null && lineas.All(l => l.Code != p.Codigo))
            {
                var nueva = CreditLine.Nueva(p.Codigo, p.Nombre, null, LineNature.Contribution);
                db.CreditLines.Add(nueva);
                lineas.Add(nueva);
            }
        }
    }

    private async Task AplicarAsync(List<Pendiente> aplicar, CancellationToken ct)
    {
        foreach (var p in aplicar)
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Code == p.Codigo && !l.IsDeleted, ct);
            linea.Name = p.Nombre;
            await alta.RegistrarAportesAsync(linea, p.Borrador!, ct);
            if (p.Activa && linea.Status != CreditLineStatus.Active) linea.Activar();
        }
    }

    private void Leer(FilaDeImportacion f, Pendiente p, ContextoDeImportacion ctx)
    {
        p.Nombre = f.Texto(C.Nombre, 120) ?? string.Empty;
        p.Activa = f.SiNo(C.Activa, porDefecto: true);
        var minimoModo = f.Enumeracion<ContributionAmountMode>(C.MinimoModo) ?? ContributionAmountMode.FixedAmount;
        var cuotaModo = f.Enumeracion<ContributionAmountMode>(C.CuotaModo) ?? ContributionAmountMode.FixedAmount;
        var minimo = f.Costo(C.MinimoValor) ?? 0m;
        var cuota = f.Costo(C.CuotaValor);
        var periodicidad = f.Enumeracion<RepaymentPeriodicity>(C.CuotaPeriodicidad) ?? RepaymentPeriodicity.Monthly;
        var excedente = f.Enumeracion<ExcessContributionAction>(C.AccionSobreExcedente) ?? ExcessContributionAction.Reject;
        var dia = f.Entero(C.CuotaDiaDeVencimiento);
        var redondeo = f.Monto(C.CuotaRedondeo) ?? 1m;
        var permanente = f.Codigo(C.LineaDeAhorroPermanente, 10);
        var fraccion = f.Porcentaje(C.FraccionAAhorroPermanente);
        PermanentSavingsShareInput? reparto = null;
        if (permanente is not null)
        {
            var existente = db.CreditLines.AsNoTracking().FirstOrDefault(l => l.Code == permanente && !l.IsDeleted && l.Nature == LineNature.Deposit);
            if (existente is null) f.Error(C.LineaDeAhorroPermanente, ErroresDeLineasDeCaptacion.CodigoLineaReferidaInvalida, $"No hay una línea de captación «{permanente}»: créela antes.");
            else if (fraccion is null) f.Error(C.FraccionAAhorroPermanente, ImportErrors.CellRequired, "Indique qué parte de la cuota va al ahorro permanente.");
            else reparto = new PermanentSavingsShareInput(existente.PublicId, fraccion.Value);
        }
        if (cuota is null) f.Error(C.CuotaValor, ImportErrors.CellRequired, "Indique el valor de la cuota periódica.");
        if (f.TieneErrores) return;

        p.Input = new ContributionLineVersionInput
        {
            ValidFrom = p.Desde,
            Reason = f.Texto(C.Motivo, 300) ?? ctx.Motivo ?? MotivoPorDefecto,
            MinimumNonReducible = minimoModo == ContributionAmountMode.FixedAmount
                ? new ContributionAmountInput(minimoModo, minimo, null)
                : new ContributionAmountInput(minimoModo, null, minimo),
            Installment = new ContributionInstallmentInput
            {
                Kind = cuotaModo,
                Amount = cuotaModo == ContributionAmountMode.FixedAmount ? cuota : null,
                Fraction = cuotaModo == ContributionAmountMode.FixedAmount ? null : cuota,
                RoundingUnit = redondeo,
                Periodicity = periodicidad,
                DueDay = dia is { } d ? (byte)Math.Clamp(d, 0, 255) : null,
                MinimumFirst = f.SiNo(C.MinimoPrimero, porDefecto: true),
            },
            PermanentSavingsShare = reparto,
            AllowsExtraordinary = f.SiNo(C.AdmiteExtraordinarios),
            MaxParticipationFraction = f.Porcentaje(C.ParticipacionMaxima),
            ExcessAction = excedente,
            AllowsTransferBetweenMembers = f.SiNo(C.AdmiteTraspasoEntreAsociados),
            RefundTermDays = f.Entero(C.DiasDeDevolucion) ?? 0,
            LossDeductionEnabled = f.SiNo(C.AbsorbePerdidas),
        };
    }
}
