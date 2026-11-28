using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Origination;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Un codeudor o avalista en la <see cref="FotoDeSolicitud"/>.</summary>
public sealed record ParteEnLaFoto(int PersonId, CreditPartyRole Role, decimal? Ingreso, decimal? Gasto, decimal? CuotasExternas);

/// <summary>
/// 2026-11-27 (feature 028, T026): un producto que la solicitud recoge, como lo ve la foto: su número visible, el modo, el valor pedido (parcial o abono)
/// y el tratamiento de la cuota causada. Lo proyectado no está: cambia con los días y no es algo que se corrija. (nuevo)
/// </summary>
public sealed record RecogidaEnLaFoto(string Producto, PayoffMode Modo, decimal? Valor, InstallmentTreatment? Cuota = null);

/// <summary>Un dato que cambió entre dos fotos, con su grupo y sus valores crudos (los Id se resuelven al mostrarlos).</summary>
public sealed record CampoDistinto(GrupoEditableAlDevolver Grupo, string Campo, string? Antes, string? Despues);

/// <summary>
/// Lo que el deudor y la oficina pueden corregir de una solicitud, agrupado por <see cref="GrupoEditableAlDevolver"/> (pedido del dueño del
/// 2026-11-23). Sirve dos veces: al editar una solicitud devuelta dice qué grupos cambian (y el parámetro decide si se admiten), y al
/// devolverla se guarda en <see cref="CreditApplication.ReturnSnapshot"/> para mostrar después qué cambió. Lo que la simulación decide sola (la
/// versión de la línea, la huella) no está: no lo corrige nadie. (nuevo)
/// </summary>
public sealed record FotoDeSolicitud(
    decimal Monto,
    int? Cuotas,
    int? PlazoMeses,
    RepaymentPeriodicity? Periodicidad,
    AmortizationSystem? Sistema,
    DateOnly? PrimerPago,
    DateOnly? Desembolso,
    GraceKind? Gracia,
    int? PeriodosDeGracia,
    string? Extraordinarias,
    bool? Vis,
    int LineaId,
    int OficinaId,
    string? Destino,
    RepaymentChannel FormaDePago,
    int? ConvenioId,
    string? IdentificacionEnPagaduria,
    string? Seguros,
    decimal? Ingreso,
    decimal? Gasto,
    decimal? CuotasExternas,
    IReadOnlyList<ParteEnLaFoto> Codeudores,
    string? Recogidas,
    bool Excepcion,
    string? MotivoDeExcepcion,
    PrepaymentChoice? Preferencia,
    decimal? ValorDelBien = null,
    int? ProveedorId = null,
    string? OpcionDeDescuento = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 2026-11-25 (prueba a mano del dueño): la fecha que el plan pone solo al primer pago con las condiciones de esta foto (sin fecha pedida: un
    /// período después del desembolso, o el día de pago de la línea). Sólo la lleva la foto de «después»; no se guarda en la foto de la devolución.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public DateOnly? PrimerPagoPorDefecto { get; init; }

    /// <summary>La fecha del primer pago que el plan pone solo (<see cref="FechasDeVencimiento"/> sin primer vencimiento); en pago único, ninguna.</summary>
    public static DateOnly? PrimerPagoSinFecha(DateOnly desembolso, RepaymentPeriodicity periodicidad, int? diaDePago) =>
        periodicidad == RepaymentPeriodicity.SinglePayment ? null : FechasDeVencimiento.Generar(desembolso, periodicidad, 1, diaDePago)[0];

    /// <summary>
    /// La foto de la solicitud y sus partes vivas (el solicitante da la capacidad; los demás, los codeudores en su orden). 2026-11-27 (feature 028,
    /// T026): <paramref name="recogidas"/> es el texto estable de lo que recoge (<see cref="TextoDeRecogidas"/>), que sale de sus filas de
    /// <c>LND_CreditApplicationPayoffs</c> (<see cref="TomarAsync"/>) o de lo que se va a guardar; ya no del JSON <c>PayoffRequest</c>.
    /// </summary>
    public static FotoDeSolicitud De(CreditApplication s, IEnumerable<CreditApplicationParty> partes, string? recogidas)
    {
        var vivas = partes.Where(p => !p.IsDeleted).ToList();
        var solicitante = vivas.FirstOrDefault(p => p.Role == CreditPartyRole.Applicant);
        var codeudores = vivas.Where(p => p.Role != CreditPartyRole.Applicant)
            .OrderBy(p => p.PersonId)
            .Select(p => new ParteEnLaFoto(p.PersonId, p.Role, p.MonthlyIncome, p.MonthlyExpenses, p.ExternalInstallments))
            .ToList();
        return new FotoDeSolicitud(s.RequestedAmount, s.Installments, s.TermMonths, s.Periodicity, s.AmortizationSystem, s.FirstDueDate,
            s.ExpectedDisbursementDate, s.GraceKind, s.GraceInstallments, s.ExtraordinaryInstallments, s.IsSocialHousing, s.CreditLineId, s.BranchId,
            s.Purpose, s.RepaymentChannel, s.PayrollAgreementId, s.PayrollEmployeeIdentifier, s.SelectedInsurances, solicitante?.MonthlyIncome,
            solicitante?.MonthlyExpenses, solicitante?.ExternalInstallments, codeudores, recogidas, s.PolicyExceptionRequested,
            s.PolicyExceptionReason, s.PrepaymentChoice, s.AssetValue, s.SupplierPersonId, s.DiscountOptionCode);
    }

    /// <summary>La foto de la solicitud con sus partes y sus recogidas leídas de la base.</summary>
    public static async Task<FotoDeSolicitud> TomarAsync(IApplicationDbContext db, CreditApplication s, CancellationToken ct)
    {
        var partes = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).ToListAsync(ct);
        return De(s, partes, await RecogidasAsync(db, s.Id, ct));
    }

    /// <summary>El texto estable de lo que recoge la solicitud, desde sus filas vivas en su orden (nulo si no recoge nada).</summary>
    public static async Task<string?> RecogidasAsync(IApplicationDbContext db, int solicitudId, CancellationToken ct)
    {
        var filas = await db.CreditApplicationPayoffs.AsNoTracking().Where(f => f.CreditApplicationId == solicitudId && !f.IsDeleted)
            .OrderBy(f => f.Order).ThenBy(f => f.Id)
            .Select(f => new { f.ProductNumber, f.Mode, f.RequestedAmount, f.InstallmentTreatment }).ToListAsync(ct);
        return TextoDeRecogidas(filas.Select(f => new RecogidaEnLaFoto(f.ProductNumber, f.Mode, f.RequestedAmount, f.InstallmentTreatment)));
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T026): el texto estable del grupo «Recogidas»: por producto, en su orden, el número, el modo y —en un parcial o un
    /// abono— el valor pedido; «sin la cuota causada» si la deja (E2). Es lo que se compara al corregir y lo que lee quien decide en «Qué cambió»
    /// («CR-1 total; CR-2 parcial 400000»). Sin recogidas, nulo: una solicitud que nunca recogió se ve igual que antes de la feature.
    /// </summary>
    public static string? TextoDeRecogidas(IEnumerable<RecogidaEnLaFoto> recogidas)
    {
        var partes = recogidas.Select(r =>
        {
            var modo = r.Modo switch
            {
                PayoffMode.Total => "total",
                PayoffMode.Partial => "parcial",
                PayoffMode.Installments => "cuotas",
                PayoffMode.Deposit => "abono",
                _ => r.Modo.ToString(),
            };
            var valor = r.Modo is PayoffMode.Partial or PayoffMode.Deposit && r.Valor is { } v
                ? " " + v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            var cuota = r.Cuota == InstallmentTreatment.Exclude ? " sin la cuota causada" : string.Empty;
            return $"{r.Producto} {modo}{valor}{cuota}";
        }).ToList();
        return partes.Count == 0 ? null : string.Join("; ", partes);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T026): una foto guardada antes de la feature trae en <see cref="Recogidas"/> el JSON de <c>PayoffRequest</c>
    /// (<c>[{"loanPublicId":…,"mode":"Full","amount":null}]</c>). Para compararla con la de ahora sin falsos cambios, lo pasa al texto estable con los
    /// números de los créditos, con las reglas de la migración que copió ese JSON a filas: un crédito repetido cuenta una vez, uno que no existe no cuenta,
    /// <c>Partial</c> es parcial con su valor y lo demás es total. Una foto que ya trae el texto estable (o nada) queda como está.
    /// </summary>
    public static async Task<FotoDeSolicitud> NormalizarAsync(IApplicationDbContext db, FotoDeSolicitud foto, CancellationToken ct)
    {
        if (foto.Recogidas is not { } texto || !texto.TrimStart().StartsWith('[')) return foto;
        List<OwnLoanPayoffInput>? viejas;
        try { viejas = JsonSerializer.Deserialize<List<OwnLoanPayoffInput>>(texto, Json); }
        catch (JsonException) { return foto; }
        var lista = (viejas ?? []).Where(v => v is not null).ToList();
        var ids = lista.Select(v => v.LoanPublicId).Distinct().ToList();
        var numeros = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.PublicId)).ToDictionaryAsync(l => l.PublicId, l => l.Number, ct);
        var vistas = new HashSet<Guid>();
        var recogidas = new List<RecogidaEnLaFoto>();
        foreach (var v in lista)
        {
            if (!numeros.TryGetValue(v.LoanPublicId, out var numero) || !vistas.Add(v.LoanPublicId)) continue;
            var parcial = string.Equals(v.Mode?.Trim(), "Partial", StringComparison.OrdinalIgnoreCase);
            recogidas.Add(new RecogidaEnLaFoto(numero, parcial ? PayoffMode.Partial : PayoffMode.Total, parcial ? v.Amount : null));
        }
        return foto with { Recogidas = TextoDeRecogidas(recogidas) };
    }

    /// <summary>La foto como JSON para <see cref="CreditApplication.ReturnSnapshot"/>.</summary>
    public static async Task<string> SerializarAsync(IApplicationDbContext db, CreditApplication s, CancellationToken ct) =>
        JsonSerializer.Serialize(await TomarAsync(db, s, ct), Json);

    /// <summary>Lee una foto guardada; ilegible, nula (no se muestran cambios). Las fotos viejas con el JSON de las recogidas se normalizan al compararlas
    /// (<see cref="NormalizarAsync"/>).</summary>
    public static FotoDeSolicitud? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<FotoDeSolicitud>(json, Json); }
        catch (JsonException) { return null; }
    }

    /// <summary>Cada dato distinto entre esta foto (antes) y <paramref name="despues"/>, en el orden de los grupos.</summary>
    public IReadOnlyList<CampoDistinto> Diferencias(FotoDeSolicitud despues)
    {
        var lista = new List<CampoDistinto>();
        void Si<T>(GrupoEditableAlDevolver grupo, string campo, T antes, T ahora)
        {
            if (!EqualityComparer<T>.Default.Equals(antes, ahora)) lista.Add(new CampoDistinto(grupo, campo, Texto(antes), Texto(ahora)));
        }

        const GrupoEditableAlDevolver C = GrupoEditableAlDevolver.Condiciones;
        Si(C, "Monto", Monto, despues.Monto);
        Si(C, "Cuotas", Cuotas, despues.Cuotas);
        Si(C, "Plazo en meses", PlazoMeses, despues.PlazoMeses);
        Si(C, "Periodicidad", Periodicidad, despues.Periodicidad);
        Si(C, "Sistema de amortización", Sistema, despues.Sistema);
        // 2026-11-25: sin fecha («según la periodicidad») y la fecha que el plan pone solo son el mismo primer pago: la simulación la llena al elegir
        // una línea que la pide, y eso no es un cambio. Lo que la mueve (desembolso, periodicidad, línea) se anota en su propio renglón.
        if (!MismoPrimerPago(PrimerPago, despues.PrimerPago, despues.PrimerPagoPorDefecto))
            lista.Add(new CampoDistinto(C, "Primer pago", Texto(PrimerPago), Texto(despues.PrimerPago)));
        Si(C, "Desembolso previsto", Desembolso, despues.Desembolso);
        Si(C, "Gracia", Gracia, despues.Gracia);
        Si(C, "Períodos de gracia", PeriodosDeGracia, despues.PeriodosDeGracia);
        Si(C, "Cuotas extraordinarias", Extraordinarias, despues.Extraordinarias);
        Si(C, "Vivienda de interés social", Vis, despues.Vis);
        Si(C, "Valor del bien", ValorDelBien, despues.ValorDelBien);
        Si(C, "Proveedor del bien", ProveedorId, despues.ProveedorId);
        Si(C, "Opción de descuento", OpcionDeDescuento, despues.OpcionDeDescuento);
        Si(GrupoEditableAlDevolver.Linea, "Línea", LineaId, despues.LineaId);
        Si(GrupoEditableAlDevolver.Oficina, "Oficina", OficinaId, despues.OficinaId);
        Si(GrupoEditableAlDevolver.Destino, "Destino", Destino, despues.Destino);
        Si(GrupoEditableAlDevolver.FormaDePago, "Forma de pago", FormaDePago, despues.FormaDePago);
        Si(GrupoEditableAlDevolver.FormaDePago, "Convenio de libranza", ConvenioId, despues.ConvenioId);
        Si(GrupoEditableAlDevolver.FormaDePago, "Identificación ante la pagaduría", IdentificacionEnPagaduria, despues.IdentificacionEnPagaduria);
        Si(GrupoEditableAlDevolver.Seguros, "Seguros", Seguros, despues.Seguros);
        Si(GrupoEditableAlDevolver.Capacidad, "Ingreso mensual", Ingreso, despues.Ingreso);
        Si(GrupoEditableAlDevolver.Capacidad, "Gasto mensual", Gasto, despues.Gasto);
        Si(GrupoEditableAlDevolver.Capacidad, "Cuotas en otras entidades", CuotasExternas, despues.CuotasExternas);
        if (!Codeudores.SequenceEqual(despues.Codeudores))
            lista.Add(new CampoDistinto(GrupoEditableAlDevolver.Codeudores, "Codeudores", Partes(Codeudores), Partes(despues.Codeudores)));
        Si(GrupoEditableAlDevolver.Recogidas, "Créditos que recoge", Recogidas, despues.Recogidas);
        Si(GrupoEditableAlDevolver.ExcepcionDePolitica, "Pide excepción a la política", Excepcion, despues.Excepcion);
        Si(GrupoEditableAlDevolver.ExcepcionDePolitica, "Motivo de la excepción", MotivoDeExcepcion, despues.MotivoDeExcepcion);
        Si(GrupoEditableAlDevolver.PreferenciaDeAbono, "Preferencia de abono a capital", Preferencia, despues.Preferencia);
        return lista;
    }

    private static bool MismoPrimerPago(DateOnly? antes, DateOnly? ahora, DateOnly? porDefecto) =>
        antes == ahora || (porDefecto is not null && (antes ?? porDefecto) == (ahora ?? porDefecto));

    /// <summary>Los grupos en que esta foto y <paramref name="despues"/> difieren.</summary>
    public GrupoEditableAlDevolver GruposDistintos(FotoDeSolicitud despues) =>
        Diferencias(despues).Aggregate(GrupoEditableAlDevolver.Ninguno, (g, c) => g | c.Grupo);

    private static string? Texto<T>(T valor) => valor switch
    {
        null => null,
        decimal d => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
        DateOnly f => f.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        bool b => b ? "Sí" : "No",
        RepaymentPeriodicity p => p switch
        {
            RepaymentPeriodicity.SinglePayment => "Pago único", RepaymentPeriodicity.Weekly => "Semanal", RepaymentPeriodicity.TenDay => "Decadal",
            RepaymentPeriodicity.Biweekly => "Quincenal", RepaymentPeriodicity.Monthly => "Mensual", RepaymentPeriodicity.Bimonthly => "Bimestral",
            RepaymentPeriodicity.Quarterly => "Trimestral", RepaymentPeriodicity.SemiAnnual => "Semestral", RepaymentPeriodicity.Annual => "Anual",
            _ => p.ToString(),
        },
        AmortizationSystem a => a switch
        {
            AmortizationSystem.LevelPayment => "Cuota fija", AmortizationSystem.ConstantPrincipal => "Abono constante a capital",
            AmortizationSystem.Bullet => "Pago único", _ => a.ToString(),
        },
        RepaymentChannel c => c switch
        {
            RepaymentChannel.Cashier => "Pago en caja", RepaymentChannel.OwnPayroll => "Nómina de la cooperativa",
            RepaymentChannel.PaymasterDeduction => "Libranza", RepaymentChannel.DirectDebit => "Débito automático", _ => c.ToString(),
        },
        PrepaymentChoice x => x == PrepaymentChoice.ReduceTerm ? "Reducir el plazo" : "Reducir la cuota",
        GraceKind g => g == GraceKind.None ? "Sin gracia" : g.ToString(),
        _ => valor.ToString(),
    };

    private static string Partes(IReadOnlyList<ParteEnLaFoto> partes) =>
        string.Join("; ", partes.Select(p => $"{p.PersonId}:{p.Role}:{Texto(p.Ingreso)}:{Texto(p.Gasto)}:{Texto(p.CuotasExternas)}"));
}
