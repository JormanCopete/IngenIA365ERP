using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// 2026-11-16 (pedido del dueño): las reglas de la caja sobre lo que se paga. La pantalla las aplica antes de mandar los ítems; el servidor las vuelve a validar al
// guardarlos (UpdateCashOperationItemsCommandHandler) y al confirmar (ConfirmacionDeRecibo), con el rechazo por ítem de siempre (Receipts.Receipt.ItemRejected,
// data.items[] { itemId, code, message }). Lo que sabe de Cartera y Captaciones lo pregunta por sus contratos; de Contabilidad, por IContabilidadParaRecaudo. (nuevo)

/// <summary>
/// Las reglas de la caja vigentes a una fecha (2026-11-16): si con algo exigible sólo se paga lo exigible (<c>Caja.ConMoraSoloExigible</c>), si sin nada exigible se
/// paga al menos una cuota completa (<c>Caja.AnticipoMinimoUnaCuota</c>) y la prelación por clase de producto (<c>Caja.PrelacionPorClase</c>; vacía = libre). (nuevo)
/// </summary>
/// <remarks>
/// 2026-11-26 (pedido del dueño): <see cref="ConMoraPermiteAnticipar"/> (<c>Caja.ConMoraPermiteAnticipar</c>): con mora, el mismo recibo paga lo vencido y además
/// anticipa o abona, siempre que lo vencido se cubra primero.
/// </remarks>
public sealed record PoliticaDeCaja(bool ConMoraSoloExigible, bool AnticipoMinimoUnaCuota, IReadOnlyList<string> PrelacionPorClase,
    bool CargarExigibleAlElegir = false, bool ConMoraPermiteAnticipar = false)
{
    public const string Aportes = "Aportes";
    public const string Creditos = "Creditos";
    public const string Ahorros = "Ahorros";
    public const string Facturas = "Facturas";
    public const string Conceptos = "Conceptos";

    /// <summary>Los defectos de las tres claves (sin lector de parámetros, como en las pruebas que arman el handler a mano).</summary>
    public static PoliticaDeCaja PorDefecto { get; } = new(
        DefectoSiNo(ParametrosDeRecaudoYTesoreria.CajaConMoraSoloExigible),
        DefectoSiNo(ParametrosDeRecaudoYTesoreria.CajaAnticipoMinimoUnaCuota),
        Clases(Defecto(ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase)),
        DefectoSiNo(ParametrosDeRecaudoYTesoreria.CajaCargarExigibleAlElegir),
        DefectoSiNo(ParametrosDeRecaudoYTesoreria.CajaConMoraPermiteAnticipar));

    /// <summary>Las reglas vigentes a <paramref name="fecha"/> por el único lector de parámetros; sin él, los defectos.</summary>
    public static async Task<PoliticaDeCaja> LeerAsync(ParametrosDeCaja? parametros, DateOnly fecha, CancellationToken ct)
    {
        if (parametros is null) return PorDefecto;
        return new PoliticaDeCaja(
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaConMoraSoloExigible, fecha, ct),
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaAnticipoMinimoUnaCuota, fecha, ct),
            Clases(await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.CajaPrelacionPorClase, fecha, ct)),
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaCargarExigibleAlElegir, fecha, ct),
            await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaConMoraPermiteAnticipar, fecha, ct));
    }

    /// <summary>Las clases de la prelación escrita («Aportes, Creditos»), con su nombre canónico, sin repetir y sin las que no existen.</summary>
    public static IReadOnlyList<string> Clases(string? texto) =>
        (texto ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(c => ParametrosDeRecaudoYTesoreria.ClasesDePrelacion.FirstOrDefault(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>La posición de una clase en la prelación; las que no se nombran van después de todas.</summary>
    public int Rango(string clase)
    {
        for (var k = 0; k < PrelacionPorClase.Count; k++)
            if (PrelacionPorClase[k] == clase) return k;
        return int.MaxValue;
    }

    /// <summary>El nombre de la clase como lo lee el cajero.</summary>
    public static string Nombre(string clase) => clase switch
    {
        Aportes => "aportes",
        Creditos => "créditos",
        Ahorros => "ahorros",
        Facturas => "facturas",
        Conceptos => "conceptos varios",
        _ => clase,
    };

    private static string Defecto(string clave) =>
        ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Modulo == ParametrosDeRecaudoYTesoreria.ModuloRecaudo && d.Clave == clave).DefectoSeguro;

    private static bool DefectoSiNo(string clave) => string.Equals(Defecto(clave), "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Lo que las reglas miran de un ítem, sea el pedido o el guardado en la operación. (nuevo)</summary>
public sealed record ItemDeLaRegla(
    Guid ItemId,
    ApplicationTarget Target,
    decimal Amount,
    Guid? ProductPublicId,
    Guid BeneficiaryPersonPublicId,
    ModalidadDePago? LoanModality,
    int? InstallmentNumber,
    string? CrossDocument,
    ApplicationTarget? PendingTarget,
    bool ApplyAdvanceDiscount = false)
{
    public static ItemDeLaRegla De(ItemDeCajaDto i)
    {
        ArgumentNullException.ThrowIfNull(i);
        return new ItemDeLaRegla(i.ItemId, i.Target, i.Amount, i.ProductPublicId, i.BeneficiaryPersonPublicId, i.LoanModality, i.InstallmentNumber, i.CrossDocument,
            i.PendingTarget, i.AdvanceDiscount > 0m);
    }
}

/// <summary>Un ítem que una regla de la caja rechaza, con su código y el mensaje para el cajero. (nuevo)</summary>
public sealed record RechazoDeLaCaja(Guid ItemId, string Code, string Message)
{
    /// <summary>Como va en <c>data.items[]</c> de <c>Receipts.Receipt.ItemRejected</c>.</summary>
    public object Dato => new { itemId = ItemId, code = Code, message = Message };
}

/// <summary>
/// Las reglas de la caja sobre los ítems de una operación (2026-11-16, pedido del dueño). Para cada crédito, con lo que dice su listado (<c>ListarAsync</c>, como
/// deudor y como codeudor de quien lo paga):
/// <list type="number">
/// <item>con bloqueos (castigo, insolvencia que negocia) no se recibe: <c>Receipts.Receipt.ObligationBlocked</c>;</item>
/// <item>la modalidad tiene que estar entre las que admite: <c>Receipts.Receipt.ModalityNotAdmitted</c>;</item>
/// <item>con <c>Caja.ConMoraSoloExigible</c> y algo exigible, sólo la cancelación total, lo exigible o la cuota específica de una cuota exigible:
/// <c>Receipts.Receipt.OverdueOnlyDue</c>;</item>
/// <item>con <c>Caja.AnticipoMinimoUnaCuota</c> y nada exigible, la cuota del ciclo va por la cuota completa (menos lo ya anticipado) y la cuota específica de una
/// cuota futura por lo que cuesta anticiparla (<c>DetalleAsync</c>): <c>Receipts.Receipt.BelowOneInstallment</c>. Abono a capital, anticipo, valor libre y
/// cancelación no tienen mínimo.</item>
/// </list>
/// Y con <c>Caja.PrelacionPorClase</c>, mientras la persona tenga algo exigible de una clase anterior que los ítems de esa clase no cubran, ningún ítem de una clase
/// posterior lleva valor (<c>Receipts.Receipt.PriorityNotRespected</c>, con la clase pendiente y su valor). Lo exigible de cada clase: créditos, lo exigible de sus
/// obligaciones como deudora sin bloqueo; aportes y ahorros, las cuotas exigibles pendientes de sus cuentas de esa naturaleza; facturas, el saldo de sus cuentas por
/// cobrar comerciales; conceptos, nada. Un crédito que el listado no trae (otro deudor, un doble de pruebas) no se juzga aquí: lo juzga Cartera al simular. (nuevo)
/// <para>
/// 2026-11-26 (pedido del dueño): la mora es lo <b>vencido</b> (<see cref="Vencido"/>: las cuotas vencidas o que vencen hoy), no lo exigible del período —por
/// período calendario, el interés ya causado del mes en curso no es mora y no impide pagar la cuota completa ni anticipar—. Con
/// <c>Caja.ConMoraPermiteAnticipar</c>, con mora se admite toda modalidad que cubre primero lo exigible (cuota del ciclo, anticipo, valor libre, abono a
/// capital) y la cuota específica de una cuota por vencer sólo detrás de los ítems del mismo recibo que cubren lo vencido. Esa cuota específica se admite aunque
/// la línea no deje pagar fuera de orden: Cartera la simula sobre lo que dejan los ítems anteriores.
/// </para>
/// </summary>
public sealed class ReglasDeLaCaja(
    IApplicationDbContext db,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IContabilidadParaRecaudo? contabilidad = null)
{
    /// <summary>El detalle de cada obligación consultada (una lectura por obligación): lo usan las reglas y el descuento por pago anticipado.</summary>
    private readonly Dictionary<Guid, DetalleDeObligacionDto?> _detalles = [];

    /// <summary>Los rechazos de <paramref name="items"/> de la operación de <paramref name="titular"/> a la fecha valor, en el orden de los ítems.</summary>
    public async Task<IReadOnlyList<RechazoDeLaCaja>> EvaluarAsync(PoliticaDeCaja politica, Guid titular, DateOnly fecha, IReadOnlyList<ItemDeLaRegla> items,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(items);
        var rechazos = new Dictionary<Guid, RechazoDeLaCaja>();
        var deCreditos = items.Where(i => i.Target == ApplicationTarget.Loan && i.ProductPublicId is not null && i.LoanModality is not null).ToList();
        var conPrelacion = politica.PrelacionPorClase.Count > 0;
        if (deCreditos.Count == 0 && !conPrelacion) return [];

        // El listado de cada persona que paga créditos (y del titular, para la prelación), como deudora y como codeudora.
        var listados = new Dictionary<Guid, IReadOnlyList<ObligacionListadaDto>>();
        var personas = deCreditos.Select(i => i.BeneficiaryPersonPublicId).ToList();
        if (conPrelacion) personas.Add(titular);
        foreach (var persona in personas.Distinct())
        {
            var r = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, persona, fecha, IncludeAsCoDebtor: true), ct);
            listados[persona] = r?.Value?.Obligations ?? [];
        }

        var detalles = _detalles;
        // Los ítems admitidos de cada crédito, en el orden del recibo: lo que va antes de un ítem le paga primero a lo vencido.
        var anteriores = new Dictionary<Guid, List<ItemDeLaRegla>>();
        foreach (var item in deCreditos)
        {
            var o = listados.GetValueOrDefault(item.BeneficiaryPersonPublicId)?.FirstOrDefault(x => x.ObligationPublicId == item.ProductPublicId);
            if (o is null) continue;
            if (!anteriores.TryGetValue(o.ObligationPublicId, out var antes)) anteriores[o.ObligationPublicId] = antes = [];
            if (await DelCreditoAsync(politica, o, item, antes, fecha, detalles, ct) is { } rechazo) rechazos[item.ItemId] = rechazo;
            else antes.Add(item);
        }

        if (conPrelacion)
            foreach (var rechazo in await PrelacionAsync(politica, titular, fecha, items, listados.GetValueOrDefault(titular) ?? [], ct))
                rechazos.TryAdd(rechazo.ItemId, rechazo);

        return items.Where(i => rechazos.ContainsKey(i.ItemId)).Select(i => rechazos[i.ItemId]).ToList();
    }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): la mora de un crédito para las reglas de la caja: lo exigible sin el interés ya causado de la cuota del período calendario
    /// en curso (las cuotas vencidas o que vencen hoy y los cargos sin cuota). Con el ciclo de la cuota es lo exigible; un listado sin el dato, también.
    /// </summary>
    public static decimal Vencido(ObligacionListadaDto o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return o.DueWithoutPeriodInProgress ?? o.DueToday;
    }

    private async Task<RechazoDeLaCaja?> DelCreditoAsync(PoliticaDeCaja politica, ObligacionListadaDto o, ItemDeLaRegla item, IReadOnlyList<ItemDeLaRegla> antes,
        DateOnly fecha, Dictionary<Guid, DetalleDeObligacionDto?> detalles, CancellationToken ct) =>
        await ModalidadDelCreditoAsync(politica, o, item, antes, fecha, detalles, ct)
        ?? (item.ApplyAdvanceDiscount ? await DescuentoNoAdmitidoAsync(o, item, antes, fecha, detalles, ct) : null);

    /// <summary>
    /// 2026-11-26 (pedido del dueño): por qué el ítem no lleva el descuento de intereses por pago anticipado que pidió el cajero, o nulo si lo lleva. Sólo la
    /// cuota específica de una cuota completa y por vencer, sin anticipo previo, de un período que no ha empezado, de una línea con descuento
    /// (<c>Pagos.DescuentoPorPagoAnticipado</c>) y con el crédito al día después de lo que va antes en el recibo.
    /// </summary>
    private async Task<RechazoDeLaCaja?> DescuentoNoAdmitidoAsync(ObligacionListadaDto o, ItemDeLaRegla item, IReadOnlyList<ItemDeLaRegla> antes, DateOnly fecha,
        Dictionary<Guid, DetalleDeObligacionDto?> detalles, CancellationToken ct)
    {
        string? motivo;
        if (item.LoanModality != ModalidadDePago.SpecificInstallment || item.InstallmentNumber is not { } n)
            motivo = "sólo se da al anticipar una cuota completa (cuota específica)";
        else
        {
            var detalle = await DetalleAsync(o.ObligationPublicId, fecha, detalles, ct);
            var cuota = detalle?.Installments.FirstOrDefault(q => q.Number == n);
            var vencido = Vencido(o);
            motivo = detalle is not { AdvanceDiscountFraction: > 0m } ? "su línea no tiene descuento por pago anticipado"
                : cuota is not { Status: "Upcoming" } ? $"la cuota {n} no está por vencer"
                : cuota.InCurrentPeriod ? $"la cuota {n} es la del período en curso; sólo llevan descuento las de períodos que no han empezado"
                : cuota.AdvanceApplied > 0m ? $"la cuota {n} ya tiene un anticipo; el descuento es sólo para la cuota completa"
                : item.Amount != cuota.ValueToAnticipate ? $"la cuota {n} se anticipa completa, por {cuota.ValueToAnticipate:N2}"
                : vencido > 0m && await CubiertoAntesAsync(o, antes, fecha, detalles, ct) < vencido
                    ? $"tiene {vencido:N2} vencidos; el recibo tiene que dejarlo al día antes (agregue «Lo exigible»)"
                    : null;
        }
        return motivo is null
            ? null
            : new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoDescuentoNoAdmitido, $"El crédito {o.Number} no lleva descuento por pago anticipado: {motivo}.");
    }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): el descuento de intereses por pago anticipado de la cuota <paramref name="cuota"/> a <paramref name="fecha"/>: el interés
    /// del plan de la cuota por la fracción de la línea, al peso (<see cref="DescuentoPorPagoAnticipado.Calcular"/>), con la fracción usada. Cero si la línea no
    /// tiene descuento o la cuota no existe. Lo admite o no <see cref="EvaluarAsync"/>; esto sólo lo calcula.
    /// </summary>
    public async Task<(decimal Descuento, decimal Fraccion)> DescuentoAsync(Guid obligacion, int cuota, DateOnly fecha, CancellationToken ct)
    {
        var detalle = await DetalleAsync(obligacion, fecha, _detalles, ct);
        var q = detalle?.Installments.FirstOrDefault(x => x.Number == cuota);
        if (detalle is null || q is null || detalle.AdvanceDiscountFraction <= 0m) return (0m, 0m);
        return (DescuentoPorPagoAnticipado.Calcular(q.Interest, detalle.AdvanceDiscountFraction), detalle.AdvanceDiscountFraction);
    }

    private async Task<RechazoDeLaCaja?> ModalidadDelCreditoAsync(PoliticaDeCaja politica, ObligacionListadaDto o, ItemDeLaRegla item,
        IReadOnlyList<ItemDeLaRegla> antes, DateOnly fecha, Dictionary<Guid, DetalleDeObligacionDto?> detalles, CancellationToken ct)
    {
        var modalidad = item.LoanModality!.Value;
        var vencido = Vencido(o);
        if (o.BlockReasons.Count > 0)
            return new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoObligacionBloqueada,
                $"El crédito {o.Number} {string.Join(" y ", o.BlockReasons.Select(Bloqueo))}: sus pagos no se reciben en caja.");
        // 2026-11-26: la cuota específica que la línea sólo admite sin nada vencido entra detrás de los ítems del recibo que cubren lo vencido; Cartera la
        // simula sobre lo que ellos dejan.
        if (!o.AdmittedModalities.Contains(modalidad)
            && !(modalidad == ModalidadDePago.SpecificInstallment && o.AdmittedModalities.Count > 0 && vencido > 0m
                 && await CubiertoAntesAsync(o, antes, fecha, detalles, ct) >= vencido))
            return new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoModalidadNoAdmitida,
                modalidad != ModalidadDePago.SpecificInstallment
                    ? $"El crédito {o.Number} no admite la modalidad «{NombreDeModalidad(modalidad)}»."
                    : vencido > 0m
                        ? $"El crédito {o.Number} tiene {vencido:N2} vencidos y su línea no deja pagar una cuota con anteriores impagas: agregue antes «Lo exigible» por lo vencido, en este mismo recibo."
                        : $"El crédito {o.Number} no admite pagar una cuota específica: su línea no lo permite.");

        if (politica.ConMoraSoloExigible && vencido > 0m)
        {
            if (modalidad is ModalidadDePago.FullPayoff or ModalidadDePago.PastDue) return null;
            if (modalidad == ModalidadDePago.SpecificInstallment)
            {
                var cuota = await CuotaAsync(o.ObligationPublicId, item.InstallmentNumber, fecha, detalles, ct);
                if (cuota is null || cuota.DueDate <= fecha) return null;
                // 2026-11-26 (pedido del dueño): si la cooperativa lo permite, la cuota por vencer se anticipa detrás de lo que cubre lo vencido.
                if (politica.ConMoraPermiteAnticipar)
                    return await CubiertoAntesAsync(o, antes, fecha, detalles, ct) >= vencido
                        ? null
                        : new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoSoloLoExigible,
                            $"El crédito {o.Number} tiene {vencido:N2} vencidos: para anticipar la cuota {cuota.Number} agregue antes «Lo exigible» por lo vencido, en este mismo recibo.");
            }
            // 2026-11-26: la cuota del ciclo, el anticipo, el valor libre y el abono a capital pagan primero lo exigible.
            else if (politica.ConMoraPermiteAnticipar) return null;
            return new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoSoloLoExigible,
                $"El crédito {o.Number} tiene {vencido:N2} vencidos: con cuotas vencidas sólo se paga lo exigible, una cuota exigible o la cancelación total; no se anticipa ni se abona a capital.");
        }

        if (politica.AnticipoMinimoUnaCuota && vencido <= 0m)
        {
            if (modalidad == ModalidadDePago.CycleInstallment)
            {
                // Lo ya anticipado cuenta: la cuota del ciclo sólo toma lo que le falta a la cuota (el listado no separa el anticipo de cada cuota).
                var minimo = Math.Max(0m, o.CycleInstallment - Math.Max(0m, o.Advance));
                if (item.Amount < minimo)
                    return new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoMenosDeUnaCuota,
                        $"El crédito {o.Number} no tiene nada exigible: la cuota del ciclo se paga completa, por lo menos {minimo:N2}.");
            }
            else if (modalidad == ModalidadDePago.SpecificInstallment
                     && await CuotaAsync(o.ObligationPublicId, item.InstallmentNumber, fecha, detalles, ct) is { Status: "Upcoming" } cuota
                     && item.Amount < cuota.ValueToAnticipate)
                return new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoMenosDeUnaCuota,
                    $"Anticipar la cuota {cuota.Number} del crédito {o.Number} cuesta {cuota.ValueToAnticipate:N2} (capital, interés, seguro y otros cargos): se anticipa completa.");
        }
        return null;
    }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo que los ítems anteriores del mismo recibo a este crédito le pagan primero a lo vencido. Toda modalidad cubre primero lo
    /// exigible salvo la cuota específica de una cuota por vencer; lo exigible o la cancelación sin valor cubren lo exigible entero.
    /// </summary>
    private async Task<decimal> CubiertoAntesAsync(ObligacionListadaDto o, IReadOnlyList<ItemDeLaRegla> antes, DateOnly fecha,
        Dictionary<Guid, DetalleDeObligacionDto?> detalles, CancellationToken ct)
    {
        var cubierto = 0m;
        foreach (var a in antes)
        {
            if (a.LoanModality == ModalidadDePago.SpecificInstallment)
            {
                var cuota = await CuotaAsync(o.ObligationPublicId, a.InstallmentNumber, fecha, detalles, ct);
                if (cuota is null || cuota.DueDate > fecha) continue;
            }
            cubierto += a.Amount <= 0m && a.LoanModality is ModalidadDePago.PastDue or ModalidadDePago.FullPayoff ? o.DueToday : a.Amount;
        }
        return cubierto;
    }

    /// <summary>La cuota del detalle de la obligación (una lectura por obligación); nula si el contrato no da el detalle o la cuota no existe.</summary>
    private async Task<CuotaDeObligacionDto?> CuotaAsync(Guid obligacion, int? numero, DateOnly fecha, Dictionary<Guid, DetalleDeObligacionDto?> detalles,
        CancellationToken ct)
    {
        if (numero is not { } n) return null;
        return (await DetalleAsync(obligacion, fecha, detalles, ct))?.Installments.FirstOrDefault(q => q.Number == n);
    }

    /// <summary>El detalle de la obligación (una lectura por obligación); nulo si el contrato no lo da.</summary>
    private async Task<DetalleDeObligacionDto?> DetalleAsync(Guid obligacion, DateOnly fecha, Dictionary<Guid, DetalleDeObligacionDto?> detalles,
        CancellationToken ct)
    {
        if (!detalles.TryGetValue(obligacion, out var detalle))
        {
            var r = await obligaciones.DetalleAsync(new SolicitudDeDetalle(ClavesDeRecaudo.Recaudador, obligacion, fecha), ct);
            detalles[obligacion] = detalle = r?.Result == ResultadoDelContrato.Answered ? r.Value : null;
        }
        return detalle;
    }

    private async Task<IReadOnlyList<RechazoDeLaCaja>> PrelacionAsync(PoliticaDeCaja politica, Guid titular, DateOnly fecha, IReadOnlyList<ItemDeLaRegla> items,
        IReadOnlyList<ObligacionListadaDto> delTitular, CancellationToken ct)
    {
        var conValor = items.Where(i => i.Amount > 0m).ToList();
        if (conValor.Count == 0) return [];

        // La naturaleza de cada cuenta: la de sus cuotas exigibles y, para las que no tienen, la de la consulta de cuentas de su titular.
        var exigiblesDeCuentas = (await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [titular], fecha), ct))?.Value?.Installments
            ?? [];
        var naturalezas = new Dictionary<Guid, NaturalezaDeCuenta>();
        foreach (var x in exigiblesDeCuentas) naturalezas.TryAdd(x.AccountPublicId, x.Nature);
        foreach (var persona in conValor.Where(i => i.Target == ApplicationTarget.Account && i.ProductPublicId is { } p && !naturalezas.ContainsKey(p))
                     .Select(i => i.BeneficiaryPersonPublicId).Distinct())
        {
            var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, persona, fecha), ct);
            foreach (var c in r?.Value?.Accounts ?? []) naturalezas.TryAdd(c.AccountPublicId, c.Nature);
        }

        string Clase(ItemDeLaRegla i) => i.Target switch
        {
            ApplicationTarget.Loan => PoliticaDeCaja.Creditos,
            ApplicationTarget.Account => i.ProductPublicId is { } p && naturalezas.GetValueOrDefault(p) == NaturalezaDeCuenta.Contribution
                ? PoliticaDeCaja.Aportes
                : PoliticaDeCaja.Ahorros,
            ApplicationTarget.CommercialReceivable => PoliticaDeCaja.Facturas,
            ApplicationTarget.PendingApplication => i.PendingTarget == ApplicationTarget.Account ? PoliticaDeCaja.Ahorros : PoliticaDeCaja.Creditos,
            _ => PoliticaDeCaja.Conceptos,
        };

        // Lo que cada ítem cubre de su producto: el crédito, la cuenta o la factura (por su número).
        string? Producto(ItemDeLaRegla i) => i.Target switch
        {
            ApplicationTarget.Loan or ApplicationTarget.Account => i.ProductPublicId?.ToString(),
            ApplicationTarget.CommercialReceivable => i.CrossDocument?.Trim(),
            _ => null,
        };

        var ultimo = conValor.Max(i => politica.Rango(Clase(i)));
        var pendientes = new List<(string Clase, int Rango, decimal Pendiente)>();
        for (var k = 0; k < politica.PrelacionPorClase.Count && k < ultimo; k++)
        {
            var clase = politica.PrelacionPorClase[k];
            IEnumerable<(string Producto, decimal Exigible)> exigibles = clase switch
            {
                PoliticaDeCaja.Creditos => delTitular.Where(o => o.Role == RolEnLaObligacion.Debtor && o.BlockReasons.Count == 0 && o.DueToday > 0m)
                    .Select(o => (o.ObligationPublicId.ToString(), o.DueToday)),
                PoliticaDeCaja.Aportes or PoliticaDeCaja.Ahorros => exigiblesDeCuentas
                    .Where(x => x.Pending > 0m && (x.Nature == NaturalezaDeCuenta.Contribution) == (clase == PoliticaDeCaja.Aportes))
                    .GroupBy(x => x.AccountPublicId).Select(g => (g.Key.ToString(), g.Sum(x => x.Pending))),
                PoliticaDeCaja.Facturas when contabilidad is not null => (await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, titular, ct))
                    .Select(f => (f.InvoiceNumber, f.Pending)),
                _ => [],
            };
            var pagado = conValor.Where(i => Clase(i) == clase && Producto(i) is not null)
                .GroupBy(i => Producto(i)!, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(i => i.Amount), StringComparer.Ordinal);
            var pendiente = exigibles.Sum(e => Math.Max(0m, e.Exigible - pagado.GetValueOrDefault(e.Producto)));
            if (pendiente > 0m) pendientes.Add((clase, k, pendiente));
        }
        if (pendientes.Count == 0) return [];

        var orden = string.Join(" → ", politica.PrelacionPorClase.Select(PoliticaDeCaja.Nombre));
        var salida = new List<RechazoDeLaCaja>();
        foreach (var item in conValor)
        {
            var rango = politica.Rango(Clase(item));
            var antes = pendientes.FirstOrDefault(p => p.Rango < rango);
            if (antes.Clase is null) continue;
            salida.Add(new RechazoDeLaCaja(item.ItemId, ErroresDeRecaudo.CodigoPrelacionNoRespetada,
                $"Primero se paga lo exigible de {PoliticaDeCaja.Nombre(antes.Clase)} ({antes.Pendiente:N2}): la cooperativa cobra en este orden: {orden}."));
        }
        return salida;
    }

    private static string Bloqueo(string codigo) => codigo switch
    {
        "WrittenOff" => "está castigado",
        "Insolvency" => "es de un deudor en proceso de insolvencia",
        _ => $"está bloqueado ({codigo})",
    };

    private static string NombreDeModalidad(ModalidadDePago m) => m switch
    {
        ModalidadDePago.FullPayoff => "cancelación total",
        ModalidadDePago.PastDue => "lo exigible",
        ModalidadDePago.CycleInstallment => "cuota del ciclo",
        ModalidadDePago.Advance => "anticipo",
        ModalidadDePago.ExtraordinaryPrincipal => "abono extraordinario a capital",
        ModalidadDePago.SpecificInstallment => "cuota específica",
        ModalidadDePago.FreeAmount => "valor libre",
        _ => m.ToString(),
    };
}
