using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payoffs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Lo que admite <c>Recogida.ProductosAdmitidos</c> (feature 028, R15): se lee con la línea del <b>crédito nuevo</b> —la suya si la tiene, si no la
/// general— a la fecha proyectada de desembolso; ilegible o vacío, el defecto (sólo créditos, total o parcial: lo de siempre). Lo usan la consulta de
/// productos y, desde T024, la validación de la solicitud. (nuevo)
/// </summary>
public static class AdmisionDeRecogidaVigente
{
    public static async Task<AdmisionDeRecogida> LeerAsync(ReglasDeParametrosDeCartera parametros, DateOnly fecha, int? lineaDelCreditoNuevo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        var leido = lineaDelCreditoNuevo is { } l
            ? await parametros.LeerAsync(ParametrosDeCartera.RecogidaProductosAdmitidos, fecha, null, ParameterScopeKind.CreditLine, l, ct)
            : await parametros.LeerAsync(ParametrosDeCartera.RecogidaProductosAdmitidos, fecha, ct: ct);
        return ProductosAdmitidos.Leer(leido.IsSuccess ? leido.Value.Texto : null);
    }

    /// <summary>Los modos admitidos de un producto, en el orden en que se escriben (<see cref="ProductosAdmitidos.ModosPosibles"/>).</summary>
    public static IReadOnlyList<PayoffMode> Modos(AdmisionDeRecogida admision, ProductoDeRecogida producto)
    {
        ArgumentNullException.ThrowIfNull(admision);
        return ProductosAdmitidos.ModosPosibles[producto].Where(m => admision.Admite(producto, m)).ToList();
    }
}

/// <summary>
/// <c>POST /api/lending/payoffs/candidates</c> (2026-11-27, pedido del dueño; feature 028, HU1, T014; api.md §1; <c>Lending.Payoffs.Read</c>): los productos
/// del deudor que un crédito nuevo podría recoger, liquidados a la fecha proyectada de desembolso D (nula = hoy) con la información hasta R (nula = ahora),
/// y el resumen de lo marcado en <see cref="Selected"/>: total, neto a girar contra <see cref="NewLoan"/> y cuotas que se liberan. En la entrega E1 sólo
/// créditos. <see cref="CreditLinePublicId"/> es la línea del crédito nuevo, que decide los productos admitidos (<c>Recogida.ProductosAdmitidos</c>; nula,
/// el parámetro general; fuera del alcance de líneas de quien consulta, el 404 de la línea). <see cref="ExcludeApplicationPublicId"/> es la solicitud que
/// se corrige: no cuenta como «otra solicitud que lo recoge». Los créditos salen de <c>ListLoansQuery</c>, que aplica el alcance de oficina y línea. No
/// escribe: audita <c>Lending.Payoff.Viewed</c> por crédito liquidado. (nuevo)
/// </summary>
public sealed record GetPayoffCandidatesQuery(
    Guid PersonPublicId,
    Guid? CreditLinePublicId = null,
    DateOnly? ProjectedDisbursementDate = null,
    DateTime? RecordedAsOf = null,
    IReadOnlyList<PayoffInput>? Selected = null,
    PayoffNewLoanInput? NewLoan = null,
    Guid? ExcludeApplicationPublicId = null) : IRequest<Result<PayoffCandidatesDto>>
{
    /// <summary>Para imprimir: trae además el membrete de la cooperativa (lo pide la ruta del PDF, no el cuerpo).</summary>
    public bool ForPrint { get; init; }
}

public sealed class GetPayoffCandidatesQueryValidator : AbstractValidator<GetPayoffCandidatesQuery>
{
    public GetPayoffCandidatesQueryValidator(IDateTimeService reloj)
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Indique el deudor.");
        RuleFor(x => x.ProjectedDisbursementDate).Must(d => d is null || d >= reloj.HoyLocal)
            .WithMessage("La fecha proyectada de desembolso es hoy o una fecha futura.");
        RuleFor(x => x.Selected).Must(s => s is null || s.Count <= GetPayoffCandidatesQueryHandler.MaximoDeMarcados)
            .WithMessage($"Se marcan a lo sumo {GetPayoffCandidatesQueryHandler.MaximoDeMarcados} productos.");
        RuleFor(x => x.Selected).Must(s => s is null || s.Where(p => p?.ProductPublicId is not null).GroupBy(p => (p.Kind, p.ProductPublicId)).All(g => g.Count() == 1))
            .WithMessage("Un producto está marcado dos veces.");
        RuleForEach(x => x.Selected).ChildRules(p =>
        {
            // 2026-11-27 (feature 028, E3, T078): también cuentas de aportes y ahorros, por cuotas o como abono; las facturas llegan con E4.
            p.RuleFor(x => x.Kind).Must(k => k is PayoffProductKind.Loan or PayoffProductKind.Account)
                .WithMessage("Por ahora se recogen créditos y cuentas de aportes y ahorros: las facturas llegan en otra entrega.");
            p.RuleFor(x => x.ProductPublicId).NotEmpty().WithMessage("Indique el producto marcado.");
            p.RuleFor(x => x.Mode).Must(m => m is PayoffMode.Total or PayoffMode.Partial).When(x => x.Kind == PayoffProductKind.Loan)
                .WithMessage("Un crédito se recoge total o parcial.");
            p.RuleFor(x => x.Mode).Must(m => m is PayoffMode.Installments or PayoffMode.Deposit).When(x => x.Kind == PayoffProductKind.Account)
                .WithMessage("Una cuenta se recoge por sus cuotas pendientes o con un abono.");
            p.RuleFor(x => x.Amount).NotNull().GreaterThan(0m).When(x => x.Mode is PayoffMode.Partial or PayoffMode.Deposit)
                .WithMessage("Una recogida parcial o un abono lleva un valor mayor que cero.");
            p.RuleFor(x => x.InstallmentTreatment).IsInEnum().When(x => x.InstallmentTreatment is not null);
        });
        RuleFor(x => x.NewLoan!.Amount).GreaterThan(0m).When(x => x.NewLoan is not null).WithMessage("El monto del crédito nuevo es mayor que cero.");
        RuleFor(x => x.NewLoan!.MonthlyInstallment).GreaterThanOrEqualTo(0m).When(x => x.NewLoan?.MonthlyInstallment is not null)
            .WithMessage("La cuota del crédito nuevo no es negativa.");
    }
}

public sealed class GetPayoffCandidatesQueryHandler(
    IApplicationDbContext db,
    ISender sender,
    LiquidacionDeRecogida liquidacion,
    CapacidadDePago capacidad,
    ReglasDeParametrosDeCartera parametros,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj,
    IBlobStore store,
    IAttachmentCipher? cifrado = null,
    RecogidaDeCuentas? cuentas = null) : IRequestHandler<GetPayoffCandidatesQuery, Result<PayoffCandidatesDto>>
{
    public const int MaximoDeMarcados = 50;

    /// <summary>Los estados que se muestran: lo vivo del deudor. Sólo el vigente (<see cref="LoanStatus.Active"/>) se puede recoger.</summary>
    private static readonly LoanStatus[] Vivos = [LoanStatus.Active, LoanStatus.PendingDisbursement, LoanStatus.WrittenOff];

    /// <summary>Las solicitudes en curso: una recogida proyectada en ellas cuenta como «otra solicitud que lo recoge».</summary>
    public static readonly CreditApplicationStatus[] SolicitudesEnCurso =
    [
        CreditApplicationStatus.Draft, CreditApplicationStatus.Submitted, CreditApplicationStatus.PendingDecision, CreditApplicationStatus.Returned,
        CreditApplicationStatus.Approved,
    ];

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public async Task<Result<PayoffCandidatesDto>> Handle(GetPayoffCandidatesQuery request, CancellationToken ct)
    {
        // Las mismas reglas del validador, por si la consulta llega sin la tubería (una prueba, otro handler).
        var hoy = reloj.HoyLocal;
        var d = request.ProjectedDisbursementDate ?? hoy;
        if (d < hoy) return Result.Failure<PayoffCandidatesDto>(ErroresDeRecogida.FechaPasada());
        var ahora = reloj.UtcNow;

        var persona = await LecturasDelEstadoDeCuenta.PersonaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<PayoffCandidatesDto>(LecturasDelEstadoDeCuenta.PersonaInexistente());

        int? lineaNueva = null;
        string? textoDeLaLinea = null;
        if (request.CreditLinePublicId is { } lineaPedida)
        {
            var linea = await db.CreditLines.AsNoTracking().Where(l => l.PublicId == lineaPedida && !l.IsDeleted)
                .Select(l => new { l.Id, l.Code, l.Name }).FirstOrDefaultAsync(ct);
            if (linea is null) return Result.Failure<PayoffCandidatesDto>(ErroresDeGobierno.LineaInexistente());
            var dentro = await alcance.AsegurarAsync(null, linea.Id, ErroresDeGobierno.LineaInexistente(), auditoria, "CreditLine", lineaPedida, ct);
            if (dentro.IsFailure) return Result.Failure<PayoffCandidatesDto>(dentro.Error);
            lineaNueva = linea.Id;
            textoDeLaLinea = $"{linea.Code} · {linea.Name}";
        }
        var admision = await AdmisionDeRecogidaVigente.LeerAsync(parametros, d, lineaNueva, ct);
        var modosDeCredito = AdmisionDeRecogidaVigente.Modos(admision, ProductoDeRecogida.Creditos);

        // Sus créditos como deudora, con el alcance de quien consulta (como la simulación de cancelación): uno de otra persona, como codeudora o fuera del
        // alcance no aparece.
        var lista = await sender.Send(new ListLoansQuery(PersonPublicId: request.PersonPublicId, Role: "Debtor", PageSize: 200), ct);
        if (lista.IsFailure) return Result.Failure<PayoffCandidatesDto>(lista.Error);
        var listados = lista.Value.Items
            .Where(c => Enum.TryParse<LoanStatus>(c.Status, out var estado) && Vivos.Contains(estado))
            .ToList();
        var publicos = listados.Select(c => c.PublicId).ToList();
        var creditos = (await db.Loans.AsNoTracking().Where(l => publicos.Contains(l.PublicId) && !l.IsDeleted).ToListAsync(ct))
            .ToDictionary(l => l.PublicId);
        var ids = creditos.Values.Select(c => c.Id).ToList();

        var canales = await CanalesAsync(ids, d, ct);
        var cuotas = await capacidad.ProximaCuotaPorCreditoAsync(ids, d, ct);
        var enOtras = await EnOtrasSolicitudesAsync(ids, request.ExcludeApplicationPublicId, ct);

        var productos = new List<PayoffCandidateDto>(listados.Count);
        foreach (var listado in listados)
        {
            if (!creditos.TryGetValue(listado.PublicId, out var credito)) continue;
            var producto = await ProductoAsync(credito, listado, d, request.RecordedAsOf, modosDeCredito, canales.GetValueOrDefault(credito.Id),
                cuotas.TryGetValue(credito.Id, out var cuota) ? cuota : null, enOtras.GetValueOrDefault(credito.Id), ct);
            productos.Add(producto);
        }
        // 2026-11-27 (feature 028, E3, T078; HU6): las cuentas vivas del titular (aportes y ahorros, nunca el CDAT), con sus cuotas pendientes a D; de fábrica
        // se ven sin poder marcarse («la cooperativa no los recoge»).
        productos.AddRange(await CuentasAsync(request.PersonPublicId, d, admision, ct));

        var resumen = Resumen(request, productos, persona.Nombre);
        if (resumen.IsFailure) return Result.Failure<PayoffCandidatesDto>(resumen.Error);
        var (marcados, sumario) = resumen.Value;

        return Result.Success(new PayoffCandidatesDto(d, request.RecordedAsOf ?? ahora, marcados, sumario)
        {
            Person = persona,
            NewLoanLine = textoDeLaLinea,
            Letterhead = request.ForPrint ? await LecturasDelEstadoDeCuenta.MembreteAsync(db, store, cifrado, ct) : null,
        });
    }

    /// <summary>Un crédito liquidado a (D, R) con lo que se le muestra al asesor; audita la liquidación vista.</summary>
    private async Task<PayoffCandidateDto> ProductoAsync(Loan credito, LoanListItemDto listado, DateOnly d, DateTime? r, IReadOnlyList<PayoffMode> modos,
        RepaymentChannel? canal, decimal? cuota, string? otraSolicitud, CancellationToken ct)
    {
        var linea = $"{listado.Line.Code} · {listado.Line.Name}";
        var avisos = new List<string>();
        if (otraSolicitud is not null)
            avisos.Add($"El crédito {credito.Number} ya está en {TextoDeLaSolicitud(otraSolicitud)}, que también lo recoge.");

        var liquidada = await liquidacion.LiquidarCreditoAsync(credito, d, r, ct);
        if (liquidada.IsFailure)
        {
            // Sin liquidación no se puede recoger, pero el producto se muestra con el motivo: lo demás del deudor sigue.
            var bloqueo = ErroresDeRecogida.SinLiquidar(credito.Number, liquidada.Error);
            return new PayoffCandidateDto(PayoffProductKind.Loan, credito.PublicId, credito.Number, linea, credito.Origin, canal, listado.DaysPastDue, cuota,
                false, bloqueo.Motivo, modos, 0m, PayoffBreakdownDto.Vacio, null, null, null, null, otraSolicitud, string.Empty, avisos)
            {
                Status = credito.Status, BlockMessage = bloqueo.Mensaje,
            };
        }

        var l = liquidada.Value;
        await auditoria.EmitirLiquidacionVistaAsync(credito.PublicId, d, r, l.Huella, ct);
        avisos.InsertRange(0, l.Avisos.Where(a => !string.Equals(a, l.ExplicacionDeIntereses, StringComparison.Ordinal)));

        var bloqueoDelCredito = l.Bloqueo ?? (modos.Count == 0 ? ErroresDeRecogida.NoAdmitido(credito.Number) : null);
        // 2026-11-27 (feature 028, E2, T066; HU4): la cuota causada con el tratamiento que rige (Recogida.CuotaCausada de la línea de este crédito), si el
        // asesor lo elige («Preguntar»), la cuota enviada y lo que se recoge dejándola.
        var segun = l.Segun(l.TratamientoPorDefecto);
        var cuotaCausada = CuotaCausada(l, segun);
        return new PayoffCandidateDto(PayoffProductKind.Loan, credito.PublicId, credito.Number, linea, credito.Origin, canal, l.DiasDeMora, cuota,
            bloqueoDelCredito is null, bloqueoDelCredito?.Motivo, modos, segun.Valor, PayoffBreakdownDto.De(segun.Desglose), l.Intereses.Efectivo,
            l.Intereses.Motivo, cuotaCausada, l.SalvoCuota?.ValorARecoger, otraSolicitud, l.Huella, avisos)
        {
            Status = credito.Status, BlockMessage = bloqueoDelCredito?.Mensaje, InterestModeExplanation = l.ExplicacionDeIntereses,
            InterestExplanation = PayoffInterestLineDto.De(segun.ExplicacionDelInteres), RecognizedReleased = l.ReconocidoQueSeLibera,
            BreakdownExcludingCurrentInstallment = l.SalvoCuota is { } salvo ? PayoffBreakdownDto.De(salvo.Desglose) : null,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E3, T078; HU6, FR-001, FR-002): las cuentas vivas (activas o inactivas) de la persona como <b>titular</b> dentro del alcance de
    /// oficina y línea de quien consulta —aportes, ahorro a la vista, permanente y contractual; un CDAT no aparece (no es una deuda, HU6-4)—, con sus cuotas
    /// pendientes a D, lo que cuesta recogerlas por cuotas, los modos que admiten la línea del crédito nuevo (<c>Recogida.ProductosAdmitidos</c>) y la de la
    /// cuenta (el abono en aportes, sólo con extraordinarios) y, si no se puede marcar, por qué: de fábrica, «la cooperativa no los recoge». Ver saldos de la
    /// persona queda en la auditoría.
    /// </summary>
    private async Task<IReadOnlyList<PayoffCandidateDto>> CuentasAsync(Guid persona, DateOnly d, AdmisionDeRecogida admision, CancellationToken ct)
    {
        if (cuentas is null) return [];
        var titular = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (titular is null) return [];
        var ambito = await alcance.ObtenerAsync(ct);
        var vivas = RecogidaDeCuentas.Vivas;
        var leidas = (await db.MemberAccounts.AsNoTracking()
                .Where(a => a.HolderPersonId == titular && !a.IsDeleted && vivas.Contains(a.Status))
                .PorOficinaYLinea(ambito, a => a.BranchId, a => a.CreditLineId)
                .ToListAsync(ct))
            .Where(a => ProductosAdmitidos.DeCuenta(a.Nature, a.DepositClass) is not null)
            .OrderBy(a => a.Nature).ThenBy(a => a.Number, StringComparer.Ordinal)
            .ToList();
        if (leidas.Count == 0) return [];

        var resultado = new List<PayoffCandidateDto>(leidas.Count);
        foreach (var c in await cuentas.LeerAsync(leidas, d, ct))
        {
            var producto = c.Producto!.Value;
            var modos = c.Admitidos(admision);
            var bloqueo = c.Bloqueo
                          ?? (!admision.Admite(producto) ? ErroresDeRecogida.CuentaNoAdmitida(c.Cuenta.Number, producto)
                              : modos.Count == 0 ? ErroresDeRecogida.SinAbonoEnLaLinea(c.Cuenta.Number, c.CodigoDeLinea)
                              : modos.All(m => m == PayoffMode.Installments) && c.Pendiente <= 0m ? ErroresDeRecogida.SinCuotasPendientes(c.Cuenta.Number, d)
                              : null);
            resultado.Add(new PayoffCandidateDto(PayoffProductKind.Account, c.Cuenta.PublicId, c.Cuenta.Number, c.Linea, null, c.Cuenta.ContributionPaymentChannel,
                c.Cuotas.Where(q => q.EnMoraEl(d)).Select(q => d.DayNumber - q.Cuota.DueDate.DayNumber).DefaultIfEmpty(0).Max(), c.Cuenta.ContributionInstallmentAmount, bloqueo is null, bloqueo?.Motivo, modos, c.Pendiente,
                PayoffBreakdownDto.De(new DesgloseDeRecogida(Capital: c.Pendiente)), null, null, null, null, null, c.Huella(d), [])
            {
                BlockMessage = bloqueo?.Mensaje,
                AccountProduct = producto.ToString(),
                PendingInstallments = c.Cuotas.Select(q => new PayoffAccountInstallmentDto(q.Cuota.PublicId, q.Cuota.Periodo, q.Cuota.DueDate, q.Pendiente)).ToList(),
                DepositNotAllowedReason = c.MotivoSinAbono,
            });
        }
        await auditoria.EmitirSaldosDePersonaVistosAsync(persona, "payoff-candidates", new { accounts = resultado.Count, date = d }, ct);
        return resultado;
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; api.md §1 <c>currentInstallment</c>): la cuota causada como la ve el asesor: con el tratamiento que rige, si la elige,
    /// la enviada, lo que deja debiendo si se deja y qué pasa con ella en cada caso. Sin cuota causada ni dejada, nula. Lo usa también la vista previa del
    /// desembolso. (nuevo)
    /// </summary>
    public static PayoffCurrentInstallmentDto? CuotaCausada(LiquidacionDeCreditoRecogido l, RecogidaSegunCuota segun)
    {
        ArgumentNullException.ThrowIfNull(l);
        ArgumentNullException.ThrowIfNull(segun);
        if (segun.Cuota is not { } c) return null;
        var incluida = l.Segun(InstallmentTreatment.Include);
        var dejada = l.SalvoCuota is not null ? l.Segun(InstallmentTreatment.Exclude) : null;
        return new PayoffCurrentInstallmentDto(c.Numero, c.Vencimiento, c.Valor, segun.Tratamiento, l.CuotaElegible, PayoffSentInstallmentDto.De(l.CuotaEnviada))
        {
            Left = l.SalvoCuota?.QuedaEnLaCuota,
            Parameter = l.CuotaCausadaConfigurada,
            Explanation = segun.ExplicacionDeLaCuota,
            IncludedExplanation = incluida.ExplicacionDeLaCuota,
            LeftExplanation = dejada?.ExplicacionDeLaCuota,
            NotLeftReason = l.CuotaCausadaConfigurada != ParametrosDeCartera.CuotaIncluir && !l.AdmiteDejarLaCuota ? l.RecogidoCompletoPorque : null,
            // 2026-11-27 (feature 028, D13 ajustada): la regla con que se deja (proporcional dentro del tope legal del prepago) y lo que sería tal como se envió.
            LeftRule = l.SalvoCuota?.Regla.Regla,
            LeftRuleReason = l.SalvoCuota?.Regla.Motivo,
            LeftAsSent = l.SalvoCuota?.QuedaTalComoSeEnvio,
        };
    }

    /// <summary>
    /// Lo marcado: el total (todo en un total; en un parcial, el valor pedido topado en lo que cuesta recogerlo), el neto contra el crédito nuevo y la suma
    /// de las cuotas que se liberan (la próxima cuota de cada crédito recogido total). Lo que no se puede recoger o no se admite de ese modo no se cuenta y
    /// se avisa; un producto que no está entre los del deudor es un error.
    /// </summary>
    private static Result<(IReadOnlyList<PayoffCandidateDto> Productos, PayoffSummaryDto Resumen)> Resumen(GetPayoffCandidatesQuery request,
        List<PayoffCandidateDto> productos, string persona)
    {
        var avisos = new List<string>();
        var total = 0m;
        var liberadas = 0m;
        var contados = 0;
        foreach (var marcado in request.Selected ?? [])
        {
            var i = productos.FindIndex(p => p.Kind == marcado.Kind && p.ProductPublicId == marcado.ProductPublicId);
            if (i < 0) return Result.Failure<(IReadOnlyList<PayoffCandidateDto>, PayoffSummaryDto)>(ErroresDeRecogida.NoEsDelSolicitante(persona));
            var p = productos[i];
            var nombre = p.Kind == PayoffProductKind.Account ? $"La cuenta {p.Number}" : $"El crédito {p.Number}";
            if (!p.Eligible)
            {
                avisos.Add($"{nombre} está {(p.Kind == PayoffProductKind.Account ? "marcada" : "marcado")} pero no se puede recoger: {p.BlockMessage}");
                continue;
            }
            if (!p.AdmittedModes.Contains(marcado.Mode))
            {
                avisos.Add(p.Kind == PayoffProductKind.Account
                    ? $"{nombre} está marcada {TextoDelModo(marcado.Mode)}, pero no admite recogerla así."
                    : $"{nombre} está marcado {TextoDelModo(marcado.Mode)}, pero la línea del crédito nuevo no admite recogerlo así.");
                continue;
            }
            if (p.Kind == PayoffProductKind.Account)
            {
                // 2026-11-27 (feature 028, E3, T078): una cuenta, por sus cuotas pendientes a D o con el abono pedido; no libera cuotas de crédito.
                if (marcado.Mode == PayoffMode.Installments && p.ToPayOff <= 0m)
                {
                    avisos.Add($"{nombre} no tiene cuotas pendientes a la fecha: no se recoge nada por cuotas.");
                    continue;
                }
                var deLaCuenta = marcado.Mode == PayoffMode.Installments ? p.ToPayOff : marcado.Amount ?? 0m;
                total += deLaCuenta;
                contados++;
                productos[i] = p with { Selected = true, SelectedMode = marcado.Mode, SelectedAmount = deLaCuenta };
                continue;
            }
            // 2026-11-27 (feature 028, E2, T066): con «Preguntar», dejar la cuota causada recoge todo menos ella.
            var dejaLaCuota = marcado.Mode == PayoffMode.Total && marcado.InstallmentTreatment == InstallmentTreatment.Exclude
                              && p.CurrentInstallment is { Choosable: true } && p.ToPayOffExcludingCurrentInstallment is not null;
            if (marcado.Mode == PayoffMode.Total && marcado.InstallmentTreatment == InstallmentTreatment.Exclude && !dejaLaCuota
                && p.CurrentInstallment is not { Treatment: InstallmentTreatment.Exclude })
                avisos.Add($"El crédito {p.Number} no admite que el asesor deje la cuota causada: se recoge con ella.");
            var tratamiento = dejaLaCuota ? InstallmentTreatment.Exclude : p.CurrentInstallment?.Treatment ?? InstallmentTreatment.Include;
            var paraRecoger = dejaLaCuota ? p.ToPayOffExcludingCurrentInstallment!.Value : p.ToPayOff;
            var valor = marcado.Mode == PayoffMode.Total ? paraRecoger : Math.Min(marcado.Amount ?? 0m, p.ToPayOff);
            if (marcado.Mode == PayoffMode.Partial && marcado.Amount > p.ToPayOff)
                avisos.Add($"El valor parcial del crédito {p.Number} pasa de lo que cuesta recogerlo: se toma {Pesos(p.ToPayOff)}.");
            total += valor;
            contados++;
            if (marcado.Mode == PayoffMode.Total) liberadas += p.Installment ?? 0m;
            productos[i] = p with
            {
                Selected = true, SelectedMode = marcado.Mode, SelectedAmount = valor,
                SelectedInstallmentTreatment = marcado.Mode == PayoffMode.Total ? tratamiento : null,
            };
            if (dejaLaCuota && p.CurrentInstallment is { } c)
            {
                // Lo marcado se muestra (y se imprime) con lo que se recoge dejándola: el valor, el desglose, la cuota con lo que deberá y su explicación.
                productos[i] = productos[i] with
                {
                    ToPayOff = paraRecoger, Breakdown = p.BreakdownExcludingCurrentInstallment ?? p.Breakdown,
                    CurrentInstallment = c with { Treatment = InstallmentTreatment.Exclude, Amount = c.Left ?? c.Amount, Explanation = c.LeftExplanation },
                    InterestExplanation = p.InterestExplanation.Where(r => r.Installment != c.Number).ToList(),
                };
            }
        }

        decimal? neto = request.NewLoan is { } nuevo ? nuevo.Amount - total : null;
        if (neto is <= 0m)
            avisos.Add($"Lo que se recoge ({Pesos(total)}) deja el neto a girar en {Pesos(neto.Value)}: con estas recogidas la solicitud no se puede radicar.");
        return Result.Success<(IReadOnlyList<PayoffCandidateDto>, PayoffSummaryDto)>((productos, new PayoffSummaryDto(total, neto, liberadas, null, avisos)
        {
            SelectedCount = contados, NewLoanAmount = request.NewLoan?.Amount, NewInstallment = request.NewLoan?.MonthlyInstallment,
        }));
    }

    /// <summary>La forma de pago de cada crédito: la del vínculo vigente a D o, si ninguno rige ese día, la del último.</summary>
    private async Task<Dictionary<int, RepaymentChannel?>> CanalesAsync(IReadOnlyCollection<int> ids, DateOnly d, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var creditos = ids.ToArray();
        var vinculos = await db.LoanPayrollLinks.AsNoTracking().Where(v => creditos.Contains(v.LoanId) && !v.IsDeleted).ToListAsync(ct);
        return vinculos.GroupBy(v => v.LoanId).ToDictionary(g => g.Key, g =>
            (RepaymentChannel?)(g.Where(v => v.VigenteEn(d)).MaxBy(v => v.ValidFrom) ?? g.MaxBy(v => v.ValidFrom))!.RepaymentChannel);
    }

    /// <summary>
    /// Los créditos que ya están en la recogida proyectada viva de otra solicitud en curso (borrador, radicada, en decisión, devuelta o aprobada sin
    /// desembolsar), con el número de esa solicitud («» si es un borrador sin número); la que se corrige no cuenta. 2026-11-27 (T027): la misma lectura
    /// que usa el detalle de la solicitud (<see cref="RecogidasDeLaSolicitud.EnOtrasSolicitudesAsync"/>).
    /// </summary>
    private Task<Dictionary<int, string>> EnOtrasSolicitudesAsync(IReadOnlyCollection<int> ids, Guid? excluida, CancellationToken ct) =>
        RecogidasDeLaSolicitud.EnOtrasSolicitudesAsync(db, ids, null, excluida, ct);

    /// <summary>«la solicitud S-000123» o, si es un borrador sin número, «otra solicitud en borrador».</summary>
    public static string TextoDeLaSolicitud(string numero) =>
        string.IsNullOrWhiteSpace(numero) ? "otra solicitud en borrador" : $"la solicitud {numero}";

    private static string TextoDelModo(PayoffMode modo) => modo switch
    {
        PayoffMode.Total => "para recoger total",
        PayoffMode.Partial => "para recoger parcial",
        PayoffMode.Installments => "para recoger por cuotas",
        PayoffMode.Deposit => "para recibir un abono",
        _ => $"como {modo}",
    };

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", Colombia);
}
