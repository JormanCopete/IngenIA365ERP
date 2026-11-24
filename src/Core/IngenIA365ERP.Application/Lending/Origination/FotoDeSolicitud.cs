using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Origination;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Un codeudor o avalista en la <see cref="FotoDeSolicitud"/>.</summary>
public sealed record ParteEnLaFoto(int PersonId, CreditPartyRole Role, decimal? Ingreso, decimal? Gasto, decimal? CuotasExternas);

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

    /// <summary>La foto de la solicitud y sus partes vivas (el solicitante da la capacidad; los demás, los codeudores en su orden).</summary>
    public static FotoDeSolicitud De(CreditApplication s, IEnumerable<CreditApplicationParty> partes)
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
            solicitante?.MonthlyExpenses, solicitante?.ExternalInstallments, codeudores, s.PayoffRequest, s.PolicyExceptionRequested,
            s.PolicyExceptionReason, s.PrepaymentChoice, s.AssetValue, s.SupplierPersonId, s.DiscountOptionCode);
    }

    /// <summary>La foto de la solicitud con sus partes leídas de la base.</summary>
    public static async Task<FotoDeSolicitud> TomarAsync(IApplicationDbContext db, CreditApplication s, CancellationToken ct)
    {
        var partes = await db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == s.Id && !p.IsDeleted).ToListAsync(ct);
        return De(s, partes);
    }

    /// <summary>La foto como JSON para <see cref="CreditApplication.ReturnSnapshot"/>.</summary>
    public static async Task<string> SerializarAsync(IApplicationDbContext db, CreditApplication s, CancellationToken ct) =>
        JsonSerializer.Serialize(await TomarAsync(db, s, ct), Json);

    /// <summary>Lee una foto guardada; ilegible, nula (no se muestran cambios).</summary>
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
        Si(C, "Primer pago", PrimerPago, despues.PrimerPago);
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
