using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Un crédito de una novedad tal como se emite (mensajes.md §10.20; K3, T0881): su hecho (sobre y dimensiones de hoy), su categoría contable, lo que
/// le saca la capitalización por concepto y cuota, lo que traslada al enlazado (en una novación o refinanciación) y sus saldos antes de la novedad
/// (para el traslado de dimensión). (nuevo)
/// </summary>
public sealed record CreditoDeNovedadAEmitir(
    HechoDeCredito Hecho,
    CategoriaContableVigente Categoria,
    IReadOnlyList<PartidaDeNovedad> Capitalizado,
    IReadOnlyList<PartidaDeNovedad> Trasladado,
    IReadOnlyList<SaldoDeCuota> SaldosAntes,
    LoanPlanSummaryV1? PlanAnterior,
    LoanOperation? Operacion);

/// <summary>Una novedad tal como se emite: su clase de hecho, sus créditos, el plan nuevo, el conteo y el enlazado. (nuevo)</summary>
public sealed record NovedadAEmitir(
    Guid NoveltyPublicId,
    NoveltyMechanism Mecanismo,
    NoveltyCause Causa,
    IReadOnlyList<CreditoDeNovedadAEmitir> Creditos,
    LoanPlanSummaryV1? PlanNuevo,
    int ReestructuracionesAntes,
    int ReestructuracionesDespues,
    LoanRefV1? Enlazado,
    Guid? PuntoDeActa,
    DateOnly FechaValor,
    DateTime Registrado)
{
    /// <summary>T0883: la sucursal contable (oficina) a la que pasa el crédito en un cambio de oficina.</summary>
    public Guid? SucursalNueva { get; init; }

    public bool CreaEnlazado => Mecanismo is NoveltyMechanism.Novation or NoveltyMechanism.Refinancing;

    /// <summary>El tipo de hecho principal (mensajes.md §2.1): reestructurado, modificado o novado.</summary>
    public string Tipo => CreaEnlazado ? CreditoNovadoV1.Type : Causa == NoveltyCause.Restructuring ? CreditoReestructuradoV1.Type : CreditoModificadoV1.Type;
}

/// <summary>Un hecho de una novedad ya armado (para la validación previa y para emitir). (nuevo)</summary>
public sealed record HechoDeNovedad(string Tipo, object Contenido, CreditoDeNovedadAEmitir Credito, bool Aparte);

/// <summary>Una condonación tal como se emite (mensajes.md §10.21; K3, T0884). (nuevo)</summary>
public sealed record CondonacionAEmitir(
    HechoDeCredito Hecho,
    Waiver Condonacion,
    LoanOperation Operacion,
    IReadOnlyList<ConceptAmountV1> Condonado,
    IReadOnlyList<ConceptAmountV1> ContraElDeterioro,
    Guid? PuntoDeActa,
    PrevalidationOutcome ValidacionPrevia);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de los hechos de una novedad (mensajes.md §3).</summary>
    public const string ClaseNovedad = "TermsChange";

    /// <summary>La clase del origen de una condonación (mensajes.md §3).</summary>
    public const string ClaseCondonacion = "Waiver";

    /// <summary>El <c>reasonCode</c> de lo capitalizado (mensajes.md §10.20).</summary>
    public const string MotivoCapitalizacion = "Capitalizacion";

    /// <summary>El <c>reasonCode</c> de lo condonado (mensajes.md §10.21).</summary>
    public const string MotivoCondonacion = "Condonacion";

    /// <summary>
    /// Los hechos de una novedad (mensajes.md §10.20, §10.15; contabilidad.md §2.1), sin emitir —los usa la validación previa y la emisión—:
    /// <list type="bullet">
    /// <item>prórroga, cambio de fecha o de tasa: <c>CreditoReestructurado</c> o <c>CreditoModificado</c> con lo capitalizado (<c>reasonCode =
    /// Capitalizacion</c>, el concepto dice de dónde sale) y, <b>aparte</b>, <c>DimensionContableCambiada</c> si el crédito pasa a reestructurado:
    /// traslada los saldos que quedan en la combinación anterior (el capital de antes y el interés que no se capitaliza);</item>
    /// <item>novación o refinanciación: un <c>CreditoNovado</c> por cada crédito anterior con todo lo que pasa al enlazado (por concepto) y, en el
    /// principal, lo capitalizado; <c>previousDimensions</c> son las del anterior y <c>dimensions</c> las del enlazado, que hereda la categoría.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<HechoDeNovedad> HechosDeNovedad(NovedadAEmitir n)
    {
        ArgumentNullException.ThrowIfNull(n);
        var hechos = new List<HechoDeNovedad>();
        var principal = n.Creditos[0];
        if (n.Mecanismo == NoveltyMechanism.BranchChange)
        {
            // T0883 (C51): la oficina sólo cambia por novedad; los saldos vivos pasan a la sucursal contable nueva.
            var h0 = principal.Hecho;
            var previas = Dimensiones(h0, principal.Categoria);
            var dePaso = previas with { BranchPublicId = n.SucursalNueva ?? previas.BranchPublicId };
            var vivos = principal.SaldosAntes.Where(s => s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)).GroupBy(s => s.Concepto)
                .Select(g => new ConceptAmountV1 { Concept = g.Key.ToString(), Amount = g.Sum(s => s.Valor) }).Where(a => a.Amount > 0m).ToList();
            if (vivos.Count > 0)
                hechos.Add(new HechoDeNovedad(DimensionContableCambiadaV1.Type, new DimensionContableCambiadaV1
                {
                    Loan = h0.Referencia, DebtorPersonPublicId = h0.DeudorPublicId, ValueDate = n.FechaValor,
                    RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(n.Registrado, DateTimeKind.Utc)), Dimensions = dePaso, Amounts = vivos,
                    RulesVersion = h0.Condiciones.RulesVersion, PreviousDimensions = previas, ChangedDimensions = ["branchPublicId"], EffectiveDate = n.FechaValor,
                }, principal, true));
            return hechos;
        }
        var nuevasDelPrincipal = Dimensiones(principal.Hecho, principal.Categoria) with { Restructured = n.ReestructuracionesDespues > 0 };
        foreach (var c in n.Creditos)
        {
            var h = c.Hecho;
            var antes = Dimensiones(h, c.Categoria) with { Restructured = h.Condiciones.RestructuringCount > 0 };
            var capitalizado = c.Capitalizado.Select(p => new ConceptAmountV1
            {
                Concept = p.Concepto.ToString(), InstallmentNumber = p.Cuota, Amount = p.Monto, ReasonCode = MotivoCapitalizacion,
            }).ToList();
            var registrado = new DateTimeOffset(DateTime.SpecifyKind(n.Registrado, DateTimeKind.Utc));
            if (n.CreaEnlazado)
            {
                var trasladado = c.Trasladado.GroupBy(p => p.Concepto).OrderBy(g => (int)g.Key)
                    .Select(g => new ConceptAmountV1 { Concept = g.Key.ToString(), Amount = g.Sum(p => p.Monto) }).Where(a => a.Amount != 0m);
                hechos.Add(new HechoDeNovedad(CreditoNovadoV1.Type, new CreditoNovadoV1
                {
                    Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = n.FechaValor, RegisteredAt = registrado,
                    Dimensions = nuevasDelPrincipal, Amounts = [.. trasladado, .. capitalizado], RulesVersion = h.Condiciones.RulesVersion,
                    PreviousDimensions = antes, NoveltyPublicId = n.NoveltyPublicId, SessionItemPublicId = n.PuntoDeActa,
                    PreviousPlan = c.PlanAnterior ?? new LoanPlanSummaryV1(), NewPlan = n.PlanNuevo ?? new LoanPlanSummaryV1(),
                    RestructuringCount = n.ReestructuracionesDespues, NewLoan = n.Enlazado,
                }, c, false));
                continue;
            }

            var nuevas = antes with { Restructured = n.ReestructuracionesDespues > 0 };
            object contenido = n.Causa == NoveltyCause.Restructuring
                ? new CreditoReestructuradoV1
                {
                    Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = n.FechaValor, RegisteredAt = registrado, Dimensions = nuevas,
                    Amounts = capitalizado, RulesVersion = h.Condiciones.RulesVersion, PreviousDimensions = antes, NoveltyPublicId = n.NoveltyPublicId,
                    SessionItemPublicId = n.PuntoDeActa, PreviousPlan = c.PlanAnterior ?? new LoanPlanSummaryV1(), NewPlan = n.PlanNuevo ?? new LoanPlanSummaryV1(),
                    RestructuringCount = n.ReestructuracionesDespues,
                }
                : new CreditoModificadoV1
                {
                    Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = n.FechaValor, RegisteredAt = registrado, Dimensions = nuevas,
                    Amounts = capitalizado, RulesVersion = h.Condiciones.RulesVersion, PreviousDimensions = antes, NoveltyPublicId = n.NoveltyPublicId,
                    SessionItemPublicId = n.PuntoDeActa, PreviousPlan = c.PlanAnterior ?? new LoanPlanSummaryV1(), NewPlan = n.PlanNuevo ?? new LoanPlanSummaryV1(),
                    RestructuringCount = n.ReestructuracionesDespues,
                };
            hechos.Add(new HechoDeNovedad(n.Tipo, contenido, c, false));

            // El crédito pasa a reestructurado: los saldos que quedan en la combinación anterior se trasladan (lo capitalizado ya entró con la nueva).
            if (antes.Restructured != nuevas.Restructured)
            {
                var porConcepto = c.SaldosAntes.Where(s => s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance))
                    .GroupBy(s => s.Concepto).ToDictionary(g => g.Key, g => g.Sum(s => s.Valor));
                foreach (var p in c.Capitalizado)
                    porConcepto[p.Concepto] = porConcepto.GetValueOrDefault(p.Concepto) - p.Monto;
                var saldos = porConcepto.Where(kv => kv.Value > 0m).OrderBy(kv => (int)kv.Key)
                    .Select(kv => new ConceptAmountV1 { Concept = kv.Key.ToString(), Amount = kv.Value }).ToList();
                if (saldos.Count > 0)
                    hechos.Add(new HechoDeNovedad(DimensionContableCambiadaV1.Type, new DimensionContableCambiadaV1
                    {
                        Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = n.FechaValor, RegisteredAt = registrado, Dimensions = nuevas,
                        Amounts = saldos, RulesVersion = h.Condiciones.RulesVersion, PreviousDimensions = antes, ChangedDimensions = ["restructured"],
                        EffectiveDate = n.FechaValor,
                    }, c, true));
            }
        }
        return hechos;
    }

    /// <summary>
    /// Emite los hechos de una novedad (mensajes.md §10.20; T0881): cada uno con origen la operación <c>TermsChange</c> de su crédito (clase
    /// <c>TermsChange</c>), clave <c>Confirmation</c>, el modo de su tipo (<c>EnLinea</c> por defecto), tercero el deudor y el sello de la validación
    /// previa; la <c>DimensionContableCambiada</c> va aparte con clave <c>Confirmation:{tipoDeHecho:N}</c> y <c>causeFact</c> = el hecho principal. Lo
    /// origina la persona que aplica. En el mismo guardado; sin guardar. Devuelve el mensaje principal de cada crédito.
    /// </summary>
    public async Task<Result<IReadOnlyDictionary<int, Guid>>> NovedadAsync(IReadOnlyList<HechoDeNovedad> hechos, PrevalidationOutcome validacionPrevia,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hechos);
        var principales = new Dictionary<int, Guid>();
        foreach (var hecho in hechos.Where(h => !h.Aparte))
        {
            var h = hecho.Credito.Hecho;
            var op = hecho.Credito.Operacion ?? throw new InvalidOperationException("Un hecho de una novedad sale de su operación TermsChange.");
            var modo = await modoDePaso.ResolverAsync(hecho.Tipo, h.FechaContable, ct);
            if (modo.IsFailure) return Result.Failure<IReadOnlyDictionary<int, Guid>>(modo.Error);
            var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
                new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseNovedad, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                    PersonPublicId: h.DeudorPublicId),
                ClavesDeEvento.Confirmacion, [hecho.Contenido], modo.Value, ValidacionPrevia: validacionPrevia), ct);
            principales[h.Credito.Id] = mensajes[0].PublicId;
        }

        foreach (var hecho in hechos.Where(h => h.Aparte))
        {
            var h = hecho.Credito.Hecho;
            var op = hecho.Credito.Operacion ?? throw new InvalidOperationException("Un hecho de una novedad sale de su operación TermsChange.");
            var tipo = await db.LendingFactTypes.AsNoTracking().Where(f => f.Code == hecho.Tipo && !f.IsDeleted).Select(f => (Guid?)f.PublicId).FirstOrDefaultAsync(ct);
            var clave = tipo is { } tipoDeHecho ? ClavesDeEvento.ConfirmacionPor(tipoDeHecho) : ClavesDeEvento.ConfirmacionPor(h.Credito.PublicId);
            var contenido = hecho.Contenido is DimensionContableCambiadaV1 d && principales.TryGetValue(h.Credito.Id, out var causa)
                ? d with { CauseFact = new OriginalFactRefV1 { MessageId = causa, Type = hechos.First(x => !x.Aparte).Tipo, OriginPublicId = op.PublicId, Number = op.Number } }
                : hecho.Contenido;
            var modo = await modoDePaso.ResolverAsync(hecho.Tipo, h.FechaContable, emitidoPorElProceso: false, ct);
            if (modo.IsFailure) return Result.Failure<IReadOnlyDictionary<int, Guid>>(modo.Error);
            await emisor.EmitirAsync(new SolicitudDeEmision(
                new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClaseNovedad, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                    PersonPublicId: h.DeudorPublicId),
                clave, [contenido], modo.Value, ValidacionPrevia: validacionPrevia), ct);
        }
        return Result.Success<IReadOnlyDictionary<int, Guid>>(principales);
    }

    /// <summary>El contenido de <c>CondonacionAprobada</c> (mensajes.md §10.21), sin emitir (lo usa también la validación previa).</summary>
    public static CondonacionAprobadaV1 ContenidoDeCondonacion(HechoDeCredito h, CategoriaContableVigente categoria, Guid waiverPublicId, DateOnly fechaValor,
        DateTime registrado, IReadOnlyList<ConceptAmountV1> condonado, IReadOnlyList<ConceptAmountV1> contraElDeterioro, Guid? puntoDeActa) => new()
    {
        Loan = h.Referencia,
        DebtorPersonPublicId = h.DeudorPublicId,
        ValueDate = fechaValor,
        RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(registrado, DateTimeKind.Utc)),
        Dimensions = Dimensiones(h, categoria),
        Amounts = condonado,
        RulesVersion = h.Condiciones.RulesVersion,
        WaiverPublicId = waiverPublicId,
        SessionItemPublicId = puntoDeActa,
        ImpairmentApplied = contraElDeterioro,
        ImpairmentShortfall = condonado.Where(a => !EsSuspenso(a.Concept)).Sum(a => a.Amount) - contraElDeterioro.Sum(a => a.Amount),
    };

    /// <summary>
    /// Emite <c>CondonacionAprobada</c> (mensajes.md §10.21; T0884): origen la operación <c>Waiver</c>, clave <c>Confirmation</c>, el modo del tipo, tercero
    /// el deudor, el sello de la validación previa. Lo origina la persona que aplica. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CondonacionAsync(CondonacionAEmitir c, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        var h = c.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = ContenidoDeCondonacion(h, categoria, c.Condonacion.PublicId, c.Operacion.ValueDate, c.Operacion.RegisteredAt, c.Condonado,
            c.ContraElDeterioro, c.PuntoDeActa);
        var modo = await modoDePaso.ResolverAsync(CondonacionAprobadaV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, c.Operacion.PublicId, ClaseCondonacion, Prefijo(c.Operacion.Number), c.Operacion.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value, ValidacionPrevia: c.ValidacionPrevia), ct);
        return Result.Success(mensajes[0]);
    }

    private static bool EsSuspenso(string concepto) =>
        concepto is nameof(LoanConcept.CurrentInterestInSuspense) or nameof(LoanConcept.DefaultInterestInSuspense);
}
