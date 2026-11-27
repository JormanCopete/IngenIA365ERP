using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Un crédito que la solicitud recoge, ya validado y proyectado a la fecha proyectada de desembolso (feature 028, HU2, T023): el crédito, cómo se recoge,
/// el valor pedido de un parcial, su liquidación y lo que guarda la solicitud (<see cref="ProyeccionDeRecogida"/>). (nuevo)
/// </summary>
public sealed record RecogidaPreparada(Loan Credito, PayoffMode Modo, decimal? ValorPedido, LiquidacionDeCreditoRecogido Liquidacion,
    ProyeccionDeRecogida Proyeccion)
{
    public ProductoRecogido Producto => ProductoRecogido.Credito(Credito.Id, Credito.Number);

    /// <summary>Lo que se proyecta recoger: todo en un total; en un parcial, el valor pedido (topado en lo que cuesta recogerlo).</summary>
    public decimal ValorProyectado => Proyeccion.Desglose.Total;

    /// <summary>Cómo la ve la foto de la solicitud (producto, modo, valor pedido y cuota causada).</summary>
    public RecogidaEnLaFoto EnLaFoto => new(Credito.Number, Modo, ValorPedido, Proyeccion.TratamientoDeCuota);
}

/// <summary>
/// Las recogidas de una solicitud listas para guardarse (feature 028, T023): cada una con su proyección a D (<see cref="Fecha"/>) con la información
/// hasta R (<see cref="InformacionHasta"/>). (nuevo)
/// </summary>
public sealed record RecogidasPreparadas(IReadOnlyList<RecogidaPreparada> Recogidas, DateOnly Fecha, DateTime InformacionHasta)
{
    public static RecogidasPreparadas Ninguna(DateOnly fecha, DateTime ahora) => new([], fecha, ahora);

    public bool Hay => Recogidas.Count > 0;

    /// <summary>Lo que se proyecta recoger en total.</summary>
    public decimal Total => Recogidas.Sum(r => r.ValorProyectado);

    /// <summary>El texto estable del grupo «Recogidas» de la foto de la solicitud (nulo sin recogidas).</summary>
    public string? TextoDeLaFoto => FotoDeSolicitud.TextoDeRecogidas(Recogidas.Select(r => r.EnLaFoto));
}

/// <summary>
/// <b>La recogida que pide una solicitud</b> (2026-11-27, pedido del dueño; feature 028, HU2, T023–T024; FR-006, FR-007, FR-009; api.md §2): traduce lo
/// que trae el cuerpo (<c>payoffs</c> o la lista vieja <c>ownLoanPayoffs</c>, <see cref="RecogidasPedidas.Unificar"/>), lo valida y lo proyecta con
/// <see cref="LiquidacionDeRecogida"/> —el único sitio que liquida una recogida— a la fecha proyectada de desembolso D (la de <c>payoffProjection</c>; si
/// no viene, la fecha esperada de desembolso; si no, hoy; nunca antes de hoy) con la información hasta R (la que viene, o ahora), y reescribe las filas
/// de <c>LND_CreditApplicationPayoffs</c> de la solicitud. Lo usan crear, corregir y editar la radicada (<see cref="PrepararAsync"/>) y radicar, que vuelve
/// a proyectar con lo de ahora para que quien decide vea lo vigente (<see cref="ReproyectarAsync"/>).
/// <para>Las reglas, en este orden, con su código (<c>Lending.Payoff.*</c>):</para>
/// <list type="number">
/// <item>en la entrega E1 sólo se recogen créditos (las cuentas llegan con E3 y las facturas con E4): <c>ProductNotAdmitted</c>;</item>
/// <item>el crédito existe y es del <b>solicitante como deudor</b>: si no, <c>NotOfApplicant</c> (también si es codeudor);</item>
/// <item>la línea de la solicitud lo admite (<c>Recogida.ProductosAdmitidos</c>, <see cref="AdmisionDeRecogidaVigente"/>): <c>ProductNotAdmitted</c>; y en
/// ese modo: <c>ModeNotAdmitted</c>;</item>
/// <item>se puede liquidar (una regla del motor o la usura del día: su propio código) y está vivo y elegible: en mora, <c>RequiresRestructuring</c>;
/// castigado, en insolvencia que negocia, sin saldo o no vigente, <c>NotPayable</c> con el motivo;</item>
/// <item>un parcial lleva un valor mayor que cero (la forma la cuida el validador); topado en lo que cuesta recogerlo: si lo cubre, se proyecta como
/// total;</item>
/// <item>el neto (monto − lo recogido proyectado) es positivo: <c>NetNotPositive</c>.</item>
/// </list>
/// Un crédito que ya está en otra solicitud viva no bloquea: el aviso sale al leer la solicitud (<c>payoffs[].inOtherApplication</c>, FR-009). No
/// audita: la liquidación vista se audita en la consulta de productos. (nuevo)
/// </summary>
public sealed class RecogidasDeLaSolicitud(
    IApplicationDbContext db,
    LiquidacionDeRecogida liquidacion,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj)
{
    /// <summary>
    /// Las recogidas que pide el cuerpo de la solicitud, validadas y proyectadas. Sin recogidas, <see cref="RecogidasPreparadas.Ninguna"/>: así se quitan
    /// las que tenía.
    /// </summary>
    public async Task<Result<RecogidasPreparadas>> PrepararAsync(CreditApplicationInput e, int solicitanteId, int lineaId, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var unificadas = RecogidasPedidas.Unificar(e.Payoffs, e.OwnLoanPayoffs);
        if (unificadas.IsFailure) return Result.Failure<RecogidasPreparadas>(unificadas.Error);
        var ahora = reloj.UtcNow;
        var d = e.PayoffProjection?.ProjectedDisbursementDate ?? e.ExpectedDisbursementDate ?? hoy;
        // Una fecha pasada no se proyecta: lo que valdría ayer con lo de hoy no le sirve a nadie (la pantalla tampoco la deja elegir).
        if (d < hoy) d = hoy;
        if (unificadas.Value.Count == 0) return Result.Success(RecogidasPreparadas.Ninguna(d, ahora));
        // R no pasa de ahora (lo que todavía no se sabe no se puede haber tomado).
        DateTime? r = null;
        if (e.PayoffProjection?.RecordedAsOf is { } pedida) r = Utc(pedida) > ahora ? ahora : Utc(pedida);
        return await ProyectarAsync(unificadas.Value, solicitanteId, lineaId, e.Amount, d, r, ct);
    }

    /// <summary>
    /// Vuelve a proyectar las recogidas vivas de la solicitud con lo de ahora (al radicar, FR-007): la misma D que guardó (nunca antes de hoy; las copiadas
    /// de <c>PayoffRequest</c> sin proyección toman la fecha esperada de desembolso), R = ahora, y las mismas reglas que al guardar.
    /// </summary>
    public async Task<Result<RecogidasPreparadas>> ReproyectarAsync(CreditApplication s, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var filas = await (from f in db.CreditApplicationPayoffs.AsNoTracking()
                           join l in db.Loans.AsNoTracking() on f.LoanId equals l.Id into creditos
                           from l in creditos.DefaultIfEmpty()
                           where f.CreditApplicationId == s.Id && !f.IsDeleted
                           orderby f.Order, f.Id
                           select new { Fila = f, LoanPublicId = l == null ? (Guid?)null : l.PublicId })
            .ToListAsync(ct);
        var conProyeccion = filas.Where(f => f.Fila.TieneProyeccion).Select(f => f.Fila.ProjectedDate).ToList();
        var d = conProyeccion.Count > 0 ? conProyeccion.Max() : s.ExpectedDisbursementDate ?? hoy;
        if (d < hoy) d = hoy;
        if (filas.Count == 0) return Result.Success(RecogidasPreparadas.Ninguna(d, reloj.UtcNow));
        var pedidas = filas.Select(f => new PayoffInput(f.Fila.ProductKind, f.LoanPublicId, f.Fila.Mode, f.Fila.RequestedAmount,
            f.Fila.CommercialReceivableReference, f.Fila.InstallmentTreatment)).ToList();
        return await ProyectarAsync(pedidas, s.ApplicantPersonId, s.CreditLineId, s.RequestedAmount, d, null, ct);
    }

    /// <summary>
    /// Reescribe las recogidas proyectadas de la solicitud (data-model: mientras es editable se reescribe entera): las vivas quedan de baja lógica —y se
    /// guardan antes, porque el único filtrado por crédito no ve la baja de la fila vieja si la nueva entra primero en el mismo guardado— y entran las
    /// nuevas, en el orden pedido. Las nuevas las guarda quien llama, en su transacción.
    /// </summary>
    public async Task ReescribirAsync(int solicitudId, RecogidasPreparadas preparadas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preparadas);
        var vivas = await db.CreditApplicationPayoffs.Where(f => f.CreditApplicationId == solicitudId && !f.IsDeleted).ToListAsync(ct);
        if (vivas.Count > 0)
        {
            foreach (var fila in vivas) fila.IsDeleted = true;
            await db.SaveChangesAsync(ct);
        }
        var orden = 1;
        foreach (var r in preparadas.Recogidas)
            db.CreditApplicationPayoffs.Add(CreditApplicationPayoff.Proyectar(solicitudId, r.Producto, r.Modo, r.ValorPedido, orden++, r.Proyeccion));
    }

    /// <summary>Las reglas de arriba sobre una lista ya unificada, con D y R resueltas.</summary>
    private async Task<Result<RecogidasPreparadas>> ProyectarAsync(IReadOnlyList<PayoffInput> pedidas, int solicitanteId, int lineaId, decimal monto,
        DateOnly d, DateTime? r, CancellationToken ct)
    {
        if (pedidas.FirstOrDefault(p => p.Kind != PayoffProductKind.Loan) is { } otro)
            return Falla(ErroresDeRecogida.ClaseNoAdmitida(otro.Kind));
        var repetido = pedidas.GroupBy(p => p.ProductPublicId).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            return Falla(ErroresDeLineasDeCredito.Invalido("payoffs", "Un crédito está dos veces entre los que recoge la solicitud: déjelo una sola vez."));

        var publicos = pedidas.Select(p => p.ProductPublicId).OfType<Guid>().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => publicos.Contains(l.PublicId) && !l.IsDeleted).ToDictionaryAsync(l => l.PublicId, ct);
        var nombre = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == solicitanteId).FirstOrDefaultAsync(ct) is { } persona
            ? ReferenciasDeGobierno.Nombre(persona)
            : "el solicitante";
        var admision = await AdmisionDeRecogidaVigente.LeerAsync(parametros, d, lineaId, ct);
        var modos = AdmisionDeRecogidaVigente.Modos(admision, ProductoDeRecogida.Creditos);

        var preparadas = new List<RecogidaPreparada>(pedidas.Count);
        DateTime? informacion = r;
        foreach (var p in pedidas)
        {
            if (p.ProductPublicId is not { } publico || !creditos.TryGetValue(publico, out var credito) || credito.DebtorPersonId != solicitanteId)
                return Falla(ErroresDeRecogida.NoEsDelSolicitante(nombre));
            if (modos.Count == 0) return Falla(ErroresDeRecogida.NoAdmitido(credito.Number).ComoError());
            if (!modos.Contains(p.Mode)) return Falla(ErroresDeRecogida.ModoNoAdmitido(credito.Number, p.Mode, modos));
            if (p.Mode == PayoffMode.Partial && p.Amount is not > 0m)
                return Falla(ErroresDeLineasDeCredito.Invalido("payoffs", $"La recogida parcial del crédito {credito.Number} lleva un valor mayor que cero."));

            var liquidada = await liquidacion.LiquidarCreditoAsync(credito, d, r, ct);
            if (liquidada.IsFailure) return Falla(liquidada.Error);
            var l = liquidada.Value;
            if (l.Bloqueo is { } bloqueo) return Falla(bloqueo.ComoError());
            informacion ??= l.InformacionHasta;

            // E1: la cuota causada siempre se incluye; «todo menos la cuota causada» (Recogida.CuotaCausada) llega con E2 (T066).
            var proyeccion = l.Proyeccion(InstallmentTreatment.Include);
            if (p.Mode == PayoffMode.Partial && p.Amount < l.ValorARecoger)
            {
                // Un parcial cubre lo exigible a D y abona el resto a capital (D7). Un crédito que se puede recoger está al día, así que a D no tiene nada
                // vencido: lo proyectado va a capital. El desembolso lo vuelve a liquidar y muestra la diferencia, si la hay. Si el parcial cubre todo, se
                // proyecta como total (casos límite de la spec).
                proyeccion = proyeccion with { Desglose = new DesgloseDeRecogida(Capital: p.Amount.Value) };
            }
            preparadas.Add(new RecogidaPreparada(credito, p.Mode, p.Mode == PayoffMode.Partial ? p.Amount : null, l, proyeccion));
        }

        var total = preparadas.Sum(x => x.ValorProyectado);
        if (monto - total <= 0m) return Falla(ErroresDeRecogida.NetoNoPositivo(monto, total));
        return Result.Success(new RecogidasPreparadas(preparadas, d, informacion ?? reloj.UtcNow));
    }

    /// <summary>
    /// Los créditos de <paramref name="creditos"/> que ya están en la recogida proyectada viva de otra solicitud en curso (borrador, radicada, en decisión,
    /// devuelta o aprobada sin desembolsar), con el número de esa solicitud («» si es un borrador sin número). La solicitud excluida —por Id o por
    /// PublicId— no cuenta. Lo usan la consulta de productos y la lectura de la solicitud (FR-009: es un aviso, no bloquea).
    /// </summary>
    public static async Task<Dictionary<int, string>> EnOtrasSolicitudesAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos, int? excluidaId,
        Guid? excluidaPublicId, CancellationToken ct)
    {
        if (creditos.Count == 0) return [];
        var ids = creditos.ToArray();
        var enCurso = GetPayoffCandidatesQueryHandler.SolicitudesEnCurso;
        var filas = await (
                from f in db.CreditApplicationPayoffs.AsNoTracking()
                join s in db.CreditApplications.AsNoTracking() on f.CreditApplicationId equals s.Id
                where !f.IsDeleted && f.LoanId != null && ids.Contains(f.LoanId.Value) && !s.IsDeleted && enCurso.Contains(s.Status)
                      && (excluidaId == null || s.Id != excluidaId) && (excluidaPublicId == null || s.PublicId != excluidaPublicId)
                select new { LoanId = f.LoanId!.Value, s.Id, s.Number })
            .ToListAsync(ct);
        return filas.GroupBy(f => f.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.Id).First().Number ?? string.Empty);
    }

    private static DateTime Utc(DateTime instante) =>
        DateTime.SpecifyKind(instante.Kind == DateTimeKind.Local ? instante.ToUniversalTime() : instante, DateTimeKind.Utc);

    private static Result<RecogidasPreparadas> Falla(Error e) => Result.Failure<RecogidasPreparadas>(e);
}
