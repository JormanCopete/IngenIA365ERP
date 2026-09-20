using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Reports;

// ---------------------------------------------------------------------------------------------
// Estados financieros (feature 009 E2, FR-047): situación financiera, resultado integral, cambios
// en el patrimonio y flujo de efectivo por el método indirecto. Los cuatro se arman sobre los
// rubros NIIF del grupo de la empresa (SaldosPorRubro): ningún código de cuenta está aquí, sólo
// códigos de rubro, que son letras y viven en la semilla rubros-niif.json.
// ---------------------------------------------------------------------------------------------

public sealed record FinancialPositionQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;
public sealed record IncomeStatementQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;
public sealed record EquityChangesQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;
public sealed record CashFlowQuery(FiltrosDeInforme Filtros) : IRequest<Result<TablaExportable>>;

/// <summary>Lo que un estado financiero admite de los filtros comunes: nivel 1..6 y formato conocido, si vienen.</summary>
public sealed class FiltrosDeEstadoFinancieroValidator : AbstractValidator<FiltrosDeInforme>
{
    private static readonly string[] Formatos = ["json", "xlsx", "pdf", "docx"];

    public FiltrosDeEstadoFinancieroValidator()
    {
        RuleFor(f => f.Level).InclusiveBetween(1, 6).When(f => f.Level is not null);
        RuleFor(f => f.Format).Must(f => Formatos.Contains(f!, StringComparer.OrdinalIgnoreCase))
            .When(f => !string.IsNullOrWhiteSpace(f.Format)).WithMessage("Formato desconocido: json, xlsx, pdf o docx.");
    }
}

public sealed class FinancialPositionQueryValidator : AbstractValidator<FinancialPositionQuery>
{
    public FinancialPositionQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeEstadoFinancieroValidator());
}

public sealed class IncomeStatementQueryValidator : AbstractValidator<IncomeStatementQuery>
{
    public IncomeStatementQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeEstadoFinancieroValidator());
}

public sealed class EquityChangesQueryValidator : AbstractValidator<EquityChangesQuery>
{
    public EquityChangesQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeEstadoFinancieroValidator());
}

public sealed class CashFlowQueryValidator : AbstractValidator<CashFlowQuery>
{
    public CashFlowQueryValidator() => RuleFor(x => x.Filtros).NotNull().SetValidator(new FiltrosDeEstadoFinancieroValidator());
}

/// <summary>
/// Los códigos de rubro que los estados necesitan nombrar (para inyectar el resultado, cuadrar,
/// subtotalizar y mapear ECP y EFE desde el ESF). Son los de <c>rubros-niif.json</c>; un grupo
/// que no traiga alguno simplemente lo suma como cero.
/// </summary>
internal static class RubrosNiif
{
    public const string Activo = "ESF-A";
    public const string Pasivo = "ESF-P";
    public const string Patrimonio = "ESF-PT";
    public const string ResultadoDelEjercicio = "ESF-PT-REJ";
    public const string CuentasDeOrden = "ORD";
    public const string Efectivo = "ESF-A-EFE";

    public const string Ingresos = "ERI-ING";
    public const string Costos = "ERI-COS";
    public const string GastosAdministracion = "ERI-GAD";
    public const string GastosVentas = "ERI-GVT";
    public const string Deterioro = "ERI-DET";
    public const string OtrosIngresos = "ERI-OING";
    public const string OtrosGastos = "ERI-OGAS";
    public const string Financieros = "ERI-FIN";
    public const string Impuestos = "ERI-IMP";
    public const string OtroResultadoIntegral = "ERI-ORI";
    public const string Cierre = "ERI-CIE";

    public const string SinRubro = "Sin rubro NIIF";

    /// <summary>
    /// Decisión 13 del diseño E2: cada rubro del ECP se alimenta de un rubro del patrimonio del ESF.
    /// El del resultado lleva además el resultado del período inyectado.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EcpDesdeEsf = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ECP-CAP"] = "ESF-PT-CAP",
        ["ECP-RES"] = "ESF-PT-RES",
        ["ECP-FDE"] = "ESF-PT-FDE",
        ["ECP-SUP"] = "ESF-PT-SUP",
        ["ECP-RAC"] = "ESF-PT-RAC",
        ["ECP-REJ"] = ResultadoDelEjercicio,
        ["ECP-ORI"] = "ESF-PT-ORI",
    };

    /// <summary>
    /// Decisión 14 del diseño E2: cada rubro del EFE es la variación (saldo al final − saldo al día
    /// antes del inicio) de las cuentas <b>propias</b> de estos rubros del ESF, multiplicada por el
    /// signo del rubro del EFE (−1 en activos: crecer consume efectivo). Los dos rubros que no son
    /// variación del ESF —el resultado y los ajustes— se calculan aparte. Las cuentas del
    /// resultado (35) entran en financiación por su movimiento propio (la distribución del
    /// excedente anterior); el resultado del período va en EFE-OPE-RES.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> EfeDesdeEsf = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["EFE-OPE-CAR"] = ["ESF-A-CAR", "ESF-A-CXC", "ESF-A-DIF", "ESF-A-OTR"],
        ["EFE-OPE-INV"] = ["ESF-A-INVT"],
        ["EFE-OPE-DEP"] = ["ESF-P-DEP"],
        ["EFE-OPE-CXP"] = ["ESF-P-CXP", "ESF-P-IMP", "ESF-P-LAB", "ESF-P-FSO", "ESF-P-PRV", "ESF-P-DIF", "ESF-P-OTR"],
        ["EFE-INV-PPE"] = ["ESF-A-PPE", "ESF-A-PIN", "ESF-A-INTG"],
        ["EFE-INV-FIN"] = ["ESF-A-INV"],
        ["EFE-FIN-OBF"] = ["ESF-P-OBF"],
        ["EFE-FIN-CAP"] = ["ESF-PT-CAP", "ESF-PT-RES", "ESF-PT-FDE", "ESF-PT-SUP", "ESF-PT-RAC", "ESF-PT-ORI", ResultadoDelEjercicio],
        ["EFE-EFE"] = [Efectivo],
    };

    /// <summary>
    /// Las contra-cuentas del activo no entran como variación: su movimiento del período es el
    /// gasto por deterioro, depreciación y amortización, que ya está en EFE-OPE-AJU (ERI-DET). Por
    /// eso PPE, intangibles, cartera y cuentas por cobrar van brutos. Cuando hay bajas o castigos
    /// las dos cifras difieren y la fila «Diferencia» lo muestra: para eso existe.
    /// </summary>
    public static readonly string[] ContrasYaEnAjustes = ["ESF-A-CAR-DET", "ESF-A-CXC-DET", "ESF-A-PPE-DEP", "ESF-A-INTG-AM"];

    public const string EfeResultado = "EFE-OPE-RES";
    public const string EfeAjustes = "EFE-OPE-AJU";
    public const string EfeCuentasPorPagar = "EFE-OPE-CXP";
    public const string EfeEfectivo = "EFE-EFE";
}

/// <summary>Lo común a los cuatro estados: preparar el contexto, cargar rubros, armar filas y notas, auditar la exportación.</summary>
internal static class ArmadoDeEstados
{
    public static readonly ColumnaExportable[] ColumnasComparativas =
    [
        new("Rubro", TipoDeColumna.Texto, "rubro"),
        new("Nombre", TipoDeColumna.Texto, "nombre"),
        new("Saldo", TipoDeColumna.Moneda, "saldo"),
        new("Comparativo", TipoDeColumna.Moneda, "comparativo"),
        new("Variación", TipoDeColumna.Moneda, "variacion"),
        new("_rubro", TipoDeColumna.Texto, "_rubro"),
    ];

    public static FilaExportable FilaComparativa(string? codigo, string nombre, decimal actual, decimal comparativo, string seccion, bool resaltada) =>
        new([codigo ?? string.Empty, nombre, actual, comparativo, actual - comparativo, codigo], seccion, resaltada);

    /// <summary>El ESF a una fecha: los saldos y el resultado del ejercicio que se inyecta en el rubro del resultado.</summary>
    public sealed class SituacionFinanciera
    {
        private readonly SaldosPorRubro _rubros;
        private readonly HashSet<string> _llevanElResultado;

        public SituacionFinanciera(SaldosPorRubro rubros, SaldosPorRubro.Saldos saldos, decimal resultado)
        {
            _rubros = rubros;
            Saldos = saldos;
            Resultado = resultado;
            // El resultado se suma al rubro del resultado y a sus ancestros (Patrimonio). Si el grupo
            // no trae ESF-PT-REJ, cae en la raíz del patrimonio para que el estado siga cuadrando.
            var destino = rubros.PorCodigo(RubrosNiif.ResultadoDelEjercicio) ?? rubros.PorCodigo(RubrosNiif.Patrimonio);
            _llevanElResultado = new HashSet<string>(StringComparer.Ordinal);
            for (var r = destino; r is not null; r = r.ParentCode is null ? null : rubros.PorCodigo(r.ParentCode)) _llevanElResultado.Add(r.Code);
        }

        public SaldosPorRubro.Saldos Saldos { get; }
        public decimal Resultado { get; }

        /// <summary>Valor del rubro como lo muestra el ESF: con hijos y, en el del resultado y sus padres, con el resultado inyectado.</summary>
        public decimal Valor(string code) => Saldos.Total(code) + (_llevanElResultado.Contains(code) ? Resultado : 0m);

        public decimal Activo => Valor(RubrosNiif.Activo);
        public decimal Pasivo => Valor(RubrosNiif.Pasivo);
        public decimal Patrimonio => Valor(RubrosNiif.Patrimonio);
        public decimal Descuadre => Activo - Pasivo - Patrimonio;
    }

    public static async Task<SituacionFinanciera> SituacionAsync(SaldosPorRubro rubros, DateOnly fecha, CancellationToken ct) =>
        new(rubros, await rubros.ALaFechaAsync(fecha, ct), await rubros.ResultadoALaFechaAsync(fecha, ct));

    /// <summary>La raíz (rubro sin padre) de la cadena de un rubro.</summary>
    public static SaldosPorRubro.Rubro Raiz(SaldosPorRubro rubros, SaldosPorRubro.Rubro r)
    {
        var actual = r;
        while (actual.ParentCode is not null && rubros.PorCodigo(actual.ParentCode) is { } padre) actual = padre;
        return actual;
    }

    /// <summary>
    /// Las filas de una sección: los rubros con padre en su orden y, al final, los sin padre como
    /// total de la sección. Un rubro con hijos o sin padre va resaltado.
    /// </summary>
    public static IEnumerable<SaldosPorRubro.Rubro> EnOrdenDePresentacion(IEnumerable<SaldosPorRubro.Rubro> deLaSeccion)
    {
        var lista = deLaSeccion.OrderBy(r => r.Order).ToList();
        foreach (var r in lista.Where(r => r.ParentCode is not null)) yield return r;
        foreach (var r in lista.Where(r => r.ParentCode is null)) yield return r;
    }

    public static bool Resaltado(SaldosPorRubro rubros, SaldosPorRubro.Rubro r) => r.ParentCode is null || rubros.TieneHijos(r.Code);

    public static string NotaDeSinRubro(SaldosPorRubro rubros, IEnumerable<JerarquiaDelPlan.Cuenta> cuentas)
    {
        var lista = cuentas.Select(c => c.Code).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
        return $"{lista.Count} cuenta(s) con movimiento cuyo rubro NIIF no existe para el grupo {rubros.Grupo}; van en «{RubrosNiif.SinRubro}» y no entran en ningún total: {string.Join(", ", lista)}. Corrija el rubro en el plan de cuentas.";
    }

    public static async Task<Result<TablaExportable>> EntregarAsync(
        AccountingAuditEmitter audit, string informe, FiltrosDeInforme filtros, TablaExportable tabla, CancellationToken ct)
    {
        if (filtros.EsExportacion) await audit.EmitirExportacionAsync(informe, filtros, filtros.Format!, tabla.Filas.Count, ct);
        return Result.Success(tabla);
    }
}

// ------------------------------------------------------------------- situación financiera --

/// <summary>
/// ESF a la fecha <c>To</c> con comparativo al mismo día del año anterior. Saldo acumulado desde
/// el inicio de la contabilidad (apertura incluida). El resultado del ejercicio (1 de enero → To)
/// se inyecta en «Resultado del ejercicio» mientras no hay cierre; las cuentas de orden se
/// muestran al final como memorando y no entran en el cuadre Activo = Pasivo + Patrimonio.
/// </summary>
public sealed class FinancialPositionQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock,
    ICurrentUserService user, AccountingAuditEmitter audit) : IRequestHandler<FinancialPositionQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(FinancialPositionQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var carga = await SaldosPorRubro.CargarAsync(db, c, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var rubros = carga.Value;

        var fecha = c.Hasta;
        var fechaComparativa = fecha.AddYears(-1);
        var actual = await ArmadoDeEstados.SituacionAsync(rubros, fecha, ct);
        var anterior = await ArmadoDeEstados.SituacionAsync(rubros, fechaComparativa, ct);

        var filas = new List<FilaExportable>();
        var esf = rubros.DelEstado(FinancialStatementKind.FinancialPosition);
        bool EsDeOrden(SaldosPorRubro.Rubro r) => ArmadoDeEstados.Raiz(rubros, r).Code == RubrosNiif.CuentasDeOrden;
        var patrimoniales = esf.Where(r => !EsDeOrden(r)).ToList();
        var deOrden = esf.Where(EsDeOrden).ToList();

        void Emitir(IEnumerable<SaldosPorRubro.Rubro> conjunto)
        {
            foreach (var seccion in conjunto.Select(r => r.Section).Distinct(StringComparer.Ordinal))
                foreach (var r in ArmadoDeEstados.EnOrdenDePresentacion(conjunto.Where(x => x.Section == seccion)))
                    filas.Add(ArmadoDeEstados.FilaComparativa(r.Code, r.Name, actual.Valor(r.Code), anterior.Valor(r.Code), seccion, ArmadoDeEstados.Resaltado(rubros, r)));
        }

        Emitir(patrimoniales);
        var seccionPatrimonio = rubros.PorCodigo(RubrosNiif.Patrimonio)?.Section ?? patrimoniales.LastOrDefault()?.Section ?? "Patrimonio";
        filas.Add(ArmadoDeEstados.FilaComparativa(null, "Total pasivo y patrimonio",
            actual.Pasivo + actual.Patrimonio, anterior.Pasivo + anterior.Patrimonio, seccionPatrimonio, resaltada: true));
        Emitir(deOrden);

        var (sinRubro, sumaSinRubro) = actual.Saldos.SinRubroDe(FinancialStatementKind.FinancialPosition);
        var (sinRubroAnterior, sumaSinRubroAnterior) = anterior.Saldos.SinRubroDe(FinancialStatementKind.FinancialPosition);
        if (sinRubro.Count > 0 || sinRubroAnterior.Count > 0)
            filas.Add(ArmadoDeEstados.FilaComparativa(null, RubrosNiif.SinRubro, sumaSinRubro, sumaSinRubroAnterior, RubrosNiif.SinRubro, resaltada: true));

        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, $"al {fecha:dd/MM/yyyy}", c, ct))
        {
            $"Comparativo al {fechaComparativa:dd/MM/yyyy}; Variación = saldo − comparativo.",
            $"«Resultado del ejercicio» incluye el resultado acumulado del ejercicio ({SaldosPorRubro.InicioDelEjercicio(fecha):dd/MM/yyyy} – {fecha:dd/MM/yyyy}): {actual.Resultado:N2}" +
            (request.Filtros.IncludeClosing ? " (con el cierre incluido, el resultado del año ya está en las cuentas del patrimonio)." : "."),
        };
        if (deOrden.Count > 0) notas.Add("Las cuentas de orden se muestran como memorando y no entran en el cuadre Activo = Pasivo + Patrimonio.");
        if (sinRubro.Count > 0) notas.Add(ArmadoDeEstados.NotaDeSinRubro(rubros, sinRubro.Select(x => x.Cuenta)));
        if (actual.Descuadre != 0m) notas.Add($"El estado no cuadra: Activo − (Pasivo + Patrimonio) = {actual.Descuadre:N2}. Revise las cuentas sin rubro y las naturalezas del plan.");

        var tabla = new TablaExportable("Estado de situación financiera", $"Al {fecha:dd/MM/yyyy} · comparativo al {fechaComparativa:dd/MM/yyyy}",
            ArmadoDeEstados.ColumnasComparativas, filas, null, notas);
        return await ArmadoDeEstados.EntregarAsync(audit, "financial-position", request.Filtros, tabla, ct);
    }
}

// ------------------------------------------------------------------- resultado integral --

/// <summary>
/// ERI del rango con comparativo del mismo rango un año antes. Los subtotales se calculan en
/// código (decisión 11 del diseño): utilidad bruta, resultado operacional, antes de impuestos y
/// del ejercicio; resultado integral total si el grupo tiene ORI. El rubro de cierre sólo aparece
/// con <c>IncludeClosing</c>.
/// </summary>
public sealed class IncomeStatementQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock,
    ICurrentUserService user, AccountingAuditEmitter audit) : IRequestHandler<IncomeStatementQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(IncomeStatementQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var carga = await SaldosPorRubro.CargarAsync(db, c, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var rubros = carga.Value;

        var desdeAnterior = c.Desde.AddYears(-1);
        var hastaAnterior = c.Hasta.AddYears(-1);
        var actual = await rubros.DelRangoAsync(c.Desde, c.Hasta, ct);
        var anterior = await rubros.DelRangoAsync(desdeAnterior, hastaAnterior, ct);

        var eri = rubros.DelEstado(FinancialStatementKind.IncomeStatement)
            .Where(r => request.Filtros.IncludeClosing || r.Code != RubrosNiif.Cierre).ToList();

        var subtotalesActual = Subtotales.De(rubros, actual);
        var subtotalesAnterior = Subtotales.De(rubros, anterior);
        var filas = new List<FilaExportable>();
        var emitidos = new HashSet<string>(StringComparer.Ordinal);

        string? SeccionDe(string code) => rubros.PorCodigo(code)?.Section;
        void Subtotal(string nombre, string seccion)
        {
            if (!emitidos.Add(nombre)) return;
            filas.Add(ArmadoDeEstados.FilaComparativa(null, nombre, subtotalesActual[nombre], subtotalesAnterior[nombre], seccion, resaltada: true));
        }

        foreach (var seccion in eri.Select(r => r.Section).Distinct(StringComparer.Ordinal))
        {
            foreach (var r in ArmadoDeEstados.EnOrdenDePresentacion(eri.Where(x => x.Section == seccion)))
                filas.Add(ArmadoDeEstados.FilaComparativa(r.Code, r.Name, actual.Total(r.Code), anterior.Total(r.Code), seccion, ArmadoDeEstados.Resaltado(rubros, r)));

            if (seccion == SeccionDe(RubrosNiif.Costos)) Subtotal(Subtotales.UtilidadBruta, seccion);
            if (seccion == SeccionDe(RubrosNiif.GastosAdministracion) || seccion == SeccionDe(RubrosNiif.Deterioro)) Subtotal(Subtotales.ResultadoOperacional, seccion);
            if (seccion == SeccionDe(RubrosNiif.Financieros) || seccion == SeccionDe(RubrosNiif.OtrosGastos)) Subtotal(Subtotales.ResultadoAntesDeImpuestos, seccion);
            if (seccion == SeccionDe(RubrosNiif.Impuestos)) Subtotal(Subtotales.ResultadoDelEjercicio, seccion);
            if (seccion == SeccionDe(RubrosNiif.OtroResultadoIntegral)) Subtotal(Subtotales.ResultadoIntegralTotal, seccion);
        }
        // Un grupo sin alguno de esos rubros igual recibe la cadena completa, al final.
        var ultimaSeccion = filas.LastOrDefault()?.Seccion ?? "Resultado";
        Subtotal(Subtotales.UtilidadBruta, ultimaSeccion);
        Subtotal(Subtotales.ResultadoOperacional, ultimaSeccion);
        Subtotal(Subtotales.ResultadoAntesDeImpuestos, ultimaSeccion);
        Subtotal(Subtotales.ResultadoDelEjercicio, ultimaSeccion);
        if (rubros.Existe(RubrosNiif.OtroResultadoIntegral)) Subtotal(Subtotales.ResultadoIntegralTotal, ultimaSeccion);

        var (sinRubro, sumaSinRubro) = actual.SinRubroDe(FinancialStatementKind.IncomeStatement);
        var (sinRubroAnterior, sumaSinRubroAnterior) = anterior.SinRubroDe(FinancialStatementKind.IncomeStatement);
        if (sinRubro.Count > 0 || sinRubroAnterior.Count > 0)
            filas.Add(ArmadoDeEstados.FilaComparativa(null, RubrosNiif.SinRubro, sumaSinRubro, sumaSinRubroAnterior, RubrosNiif.SinRubro, resaltada: true));

        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct))
        {
            $"Comparativo del {desdeAnterior:dd/MM/yyyy} al {hastaAnterior:dd/MM/yyyy}; Variación = saldo − comparativo.",
            "Los rubros se muestran con su signo (las devoluciones restan al ingreso); los subtotales resaltados se calculan sobre los rubros: ingresos − costos = utilidad bruta; − gastos = resultado operacional; + otros ingresos − otros gastos − financieros = resultado antes de impuestos; − impuestos = resultado del ejercicio.",
        };
        if (request.Filtros.IncludeClosing) notas.Add("Incluye el comprobante de cierre: con él, los ingresos y gastos del ejercicio quedan en cero y el rubro «Cierre del ejercicio» muestra lo que pasó por sus cuentas.");
        if (sinRubro.Count > 0) notas.Add(ArmadoDeEstados.NotaDeSinRubro(rubros, sinRubro.Select(x => x.Cuenta)));
        // La cadena de subtotales y el resultado contable (Σ créditos − débitos de las cuentas con
        // rubro del ERI, lo que el ESF inyecta) coinciden cuando el signo de cada rubro va con la
        // naturaleza de sus cuentas. Si no, hay una parametrización torcida y se dice.
        var resultadoPorRubros = subtotalesActual[Subtotales.ResultadoIntegralTotal];
        var resultadoContableSinCierre = actual.ResultadoContable - ResultadoQuePasaPorElCierre(rubros, actual);
        if (resultadoPorRubros != resultadoContableSinCierre)
            notas.Add($"El resultado por rubros ({resultadoPorRubros:N2}) difiere del resultado contable de las cuentas con rubro del ERI ({resultadoContableSinCierre:N2}): revise el signo de los rubros y la naturaleza de las cuentas.");

        var tabla = new TablaExportable("Estado de resultado integral", $"Del {c.Desde:dd/MM/yyyy} al {c.Hasta:dd/MM/yyyy} · comparativo del {desdeAnterior:dd/MM/yyyy} al {hastaAnterior:dd/MM/yyyy}",
            ArmadoDeEstados.ColumnasComparativas, filas, null, notas);
        return await ArmadoDeEstados.EntregarAsync(audit, "income-statement", request.Filtros, tabla, ct);
    }

    /// <summary>Lo contabilizado en las cuentas del rubro de cierre, crédito-positivo: el resultado contable lo incluye y la cadena de subtotales no.</summary>
    private static decimal ResultadoQuePasaPorElCierre(SaldosPorRubro rubros, SaldosPorRubro.Saldos saldos)
    {
        if (!rubros.Existe(RubrosNiif.Cierre)) return 0m;
        var s = saldos.Sumas(RubrosNiif.Cierre);
        return s.Creditos - s.Debitos;
    }

    /// <summary>La cadena de subtotales del ERI (decisión 11), por nombre de fila.</summary>
    internal static class Subtotales
    {
        public const string UtilidadBruta = "Utilidad bruta";
        public const string ResultadoOperacional = "Resultado operacional";
        public const string ResultadoAntesDeImpuestos = "Resultado antes de impuestos";
        public const string ResultadoDelEjercicio = "Resultado del ejercicio";
        public const string ResultadoIntegralTotal = "Resultado integral total";

        public static Dictionary<string, decimal> De(SaldosPorRubro rubros, SaldosPorRubro.Saldos s)
        {
            // Ingresos ya trae las devoluciones restadas (hijo con signo −1): no se restan otra vez.
            var utilidadBruta = s.Total(RubrosNiif.Ingresos) - s.Total(RubrosNiif.Costos);
            var operacional = utilidadBruta - s.Total(RubrosNiif.GastosAdministracion) - s.Total(RubrosNiif.GastosVentas) - s.Total(RubrosNiif.Deterioro);
            var antesDeImpuestos = operacional + s.Total(RubrosNiif.OtrosIngresos) - s.Total(RubrosNiif.OtrosGastos) - s.Total(RubrosNiif.Financieros);
            var delEjercicio = antesDeImpuestos - s.Total(RubrosNiif.Impuestos);
            var integral = delEjercicio + (rubros.Existe(RubrosNiif.OtroResultadoIntegral) ? s.Total(RubrosNiif.OtroResultadoIntegral) : 0m);
            return new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [UtilidadBruta] = utilidadBruta,
                [ResultadoOperacional] = operacional,
                [ResultadoAntesDeImpuestos] = antesDeImpuestos,
                [ResultadoDelEjercicio] = delEjercicio,
                [ResultadoIntegralTotal] = integral,
            };
        }
    }
}

// ------------------------------------------------------------- cambios en el patrimonio --

/// <summary>
/// ECP del rango: una fila por rubro <c>EquityChanges</c> del grupo, alimentado desde su rubro del
/// patrimonio en el ESF (<see cref="RubrosNiif.EcpDesdeEsf"/>). Saldo inicial al día antes de
/// <c>From</c> (con el resultado acumulado a esa fecha en el rubro del resultado), aumentos =
/// créditos del rango, disminuciones = débitos del rango; en el rubro del resultado, además, el
/// resultado del rango entra como aumento (excedente) o disminución (pérdida).
/// </summary>
public sealed class EquityChangesQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock,
    ICurrentUserService user, AccountingAuditEmitter audit) : IRequestHandler<EquityChangesQuery, Result<TablaExportable>>
{
    private static readonly ColumnaExportable[] Columnas =
    [
        new("Rubro", TipoDeColumna.Texto, "rubro"),
        new("Nombre", TipoDeColumna.Texto, "nombre"),
        new("Saldo inicial", TipoDeColumna.Moneda, "saldoInicial"),
        new("Aumentos", TipoDeColumna.Moneda, "aumentos"),
        new("Disminuciones", TipoDeColumna.Moneda, "disminuciones"),
        new("Saldo final", TipoDeColumna.Moneda, "saldoFinal"),
        new("_rubro", TipoDeColumna.Texto, "_rubro"),
    ];

    public async Task<Result<TablaExportable>> Handle(EquityChangesQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var carga = await SaldosPorRubro.CargarAsync(db, c, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var rubros = carga.Value;

        var diaAntes = c.Desde.AddDays(-1);
        var inicial = await ArmadoDeEstados.SituacionAsync(rubros, diaAntes, ct);
        var rango = await rubros.DelRangoAsync(c.Desde, c.Hasta, ct);
        var resultadoDelRango = rango.ResultadoContable;

        var filas = new List<FilaExportable>();
        decimal tInicial = 0m, tAumentos = 0m, tDisminuciones = 0m, tFinal = 0m;
        var sinOrigen = new List<string>();
        foreach (var r in rubros.DelEstado(FinancialStatementKind.EquityChanges))
        {
            if (!RubrosNiif.EcpDesdeEsf.TryGetValue(r.Code, out var origen) || !rubros.Existe(origen))
            {
                sinOrigen.Add(r.Code);
                filas.Add(new FilaExportable([r.Code, r.Name, 0m, 0m, 0m, 0m, r.Code], r.Section));
                continue;
            }
            var saldoInicial = inicial.Valor(origen);
            var sumas = rango.Sumas(origen);
            var aumentos = sumas.Creditos;
            var disminuciones = sumas.Debitos;
            if (origen == RubrosNiif.ResultadoDelEjercicio)
            {
                if (resultadoDelRango >= 0m) aumentos += resultadoDelRango; else disminuciones += -resultadoDelRango;
            }
            var saldoFinal = saldoInicial + aumentos - disminuciones;
            filas.Add(new FilaExportable([r.Code, r.Name, saldoInicial, aumentos, disminuciones, saldoFinal, r.Code], r.Section));
            tInicial += saldoInicial; tAumentos += aumentos; tDisminuciones += disminuciones; tFinal += saldoFinal;
        }
        var totales = new FilaExportable(["", "Total patrimonio", tInicial, tAumentos, tDisminuciones, tFinal, null], null, Resaltada: true);

        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct))
        {
            $"Saldo inicial al {diaAntes:dd/MM/yyyy}; aumentos = créditos del período, disminuciones = débitos del período; saldo final = inicial + aumentos − disminuciones.",
            "Cada rubro se alimenta de su rubro del patrimonio en el estado de situación financiera: " +
            string.Join(", ", RubrosNiif.EcpDesdeEsf.Where(kv => rubros.Existe(kv.Key)).Select(kv => $"{kv.Key} ← {kv.Value}")) + ".",
            $"«Resultado del ejercicio»: el saldo inicial trae el resultado acumulado al {diaAntes:dd/MM/yyyy} ({inicial.Resultado:N2}) y el resultado del período ({resultadoDelRango:N2}) entra como {(resultadoDelRango >= 0m ? "aumento" : "disminución")}.",
        };
        if (sinOrigen.Count > 0) notas.Add($"Rubros sin origen en el ESF para el grupo {rubros.Grupo}, en cero: {string.Join(", ", sinOrigen)}.");

        var tabla = new TablaExportable("Estado de cambios en el patrimonio", $"Del {c.Desde:dd/MM/yyyy} al {c.Hasta:dd/MM/yyyy}", Columnas, filas, totales, notas);
        return await ArmadoDeEstados.EntregarAsync(audit, "equity-changes", request.Filtros, tabla, ct);
    }
}

// ---------------------------------------------------------------------- flujo de efectivo --

/// <summary>
/// EFE por el método indirecto (decisión 14): resultado del período + ajustes que no mueven
/// efectivo + variaciones de activos y pasivos por actividad, que deben explicar la variación del
/// efectivo. La fila final «Diferencia» = Σ actividades − Δ efectivo tiene que ser cero; si no lo
/// es, hay un rubro del ESF fuera del mapeo o una baja/castigo que separa el gasto por deterioro
/// y depreciación de la variación de sus contra-cuentas, y la nota lo dice.
/// </summary>
public sealed class CashFlowQueryHandler(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock,
    ICurrentUserService user, AccountingAuditEmitter audit) : IRequestHandler<CashFlowQuery, Result<TablaExportable>>
{
    private static readonly ColumnaExportable[] Columnas =
    [
        new("Concepto", TipoDeColumna.Texto, "concepto"),
        new("Valor", TipoDeColumna.Moneda, "valor"),
        new("_rubro", TipoDeColumna.Texto, "_rubro"),
    ];

    public async Task<Result<TablaExportable>> Handle(CashFlowQuery request, CancellationToken ct)
    {
        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, request.Filtros, ct);
        if (ctx.IsFailure) return Result.Failure<TablaExportable>(ctx.Error);
        var c = ctx.Value;
        var carga = await SaldosPorRubro.CargarAsync(db, c, ct);
        if (carga.IsFailure) return Result.Failure<TablaExportable>(carga.Error);
        var rubros = carga.Value;

        var diaAntes = c.Desde.AddDays(-1);
        var inicio = await ArmadoDeEstados.SituacionAsync(rubros, diaAntes, ct);
        var fin = await ArmadoDeEstados.SituacionAsync(rubros, c.Hasta, ct);
        var rango = await rubros.DelRangoAsync(c.Desde, c.Hasta, ct);

        // Variación de las cuentas propias de un rubro del ESF (sin hijos: cada rubro se mapea por separado).
        decimal Delta(string esf) => fin.Saldos.Propio(esf) - inicio.Saldos.Propio(esf);

        // Valor crudo de cada rubro del EFE antes de aplicar su signo (decisión 14).
        var crudo = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (codigoEfe, origenes) in RubrosNiif.EfeDesdeEsf) crudo[codigoEfe] = origenes.Sum(Delta);
        // El resultado del período: la variación del resultado acumulado que el ESF inyecta. En un
        // rango dentro del mismo ejercicio es exactamente el resultado del rango.
        crudo[RubrosNiif.EfeResultado] = fin.Resultado - inicio.Resultado;
        // Los ajustes: el gasto del período por deterioro, depreciación y amortización (no mueve efectivo).
        crudo[RubrosNiif.EfeAjustes] = rango.Total(RubrosNiif.Deterioro);

        // Lo del ESF que no está en el mapeo va a cuentas por pagar y otros pasivos, con su signo de
        // efectivo (un activo que crece resta), y se avisa. Las contra-cuentas quedan fuera a propósito.
        var mapeados = new HashSet<string>(RubrosNiif.EfeDesdeEsf.Values.SelectMany(v => v), StringComparer.Ordinal);
        var fueraDelMapeo = new List<string>();
        foreach (var esf in rubros.DelEstado(FinancialStatementKind.FinancialPosition))
        {
            if (mapeados.Contains(esf.Code) || RubrosNiif.ContrasYaEnAjustes.Contains(esf.Code, StringComparer.Ordinal)) continue;
            var raiz = ArmadoDeEstados.Raiz(rubros, esf).Code;
            if (raiz == RubrosNiif.CuentasDeOrden) continue;
            var delta = Delta(esf.Code);
            if (delta == 0m) continue;
            fueraDelMapeo.Add(esf.Code);
            crudo[RubrosNiif.EfeCuentasPorPagar] = crudo.GetValueOrDefault(RubrosNiif.EfeCuentasPorPagar) + (raiz == RubrosNiif.Activo ? -delta : delta);
        }
        // Las cuentas sin rubro también son variación de activo (clase 1, 8) o de pasivo/patrimonio (2, 3, 9).
        var sinRubro = fin.Saldos.SinRubroDe(FinancialStatementKind.FinancialPosition).Cuentas
            .Concat(inicio.Saldos.SinRubroDe(FinancialStatementKind.FinancialPosition).Cuentas)
            .Select(x => x.Cuenta).DistinctBy(x => x.Id).ToList();
        foreach (var cuenta in sinRubro)
        {
            var delta = SaldoSinRubro(fin.Saldos, cuenta) - SaldoSinRubro(inicio.Saldos, cuenta);
            var esActivo = JerarquiaDelPlan.NaturalezaDeClase(cuenta.Code) == AccountNature.Debit;
            crudo[RubrosNiif.EfeCuentasPorPagar] = crudo.GetValueOrDefault(RubrosNiif.EfeCuentasPorPagar) + (esActivo ? -delta : delta);
        }

        var efe = rubros.DelEstado(FinancialStatementKind.CashFlow);
        var valor = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var r in efe.Where(r => r.ParentCode is not null)) valor[r.Code] = crudo.GetValueOrDefault(r.Code) * r.Sign;
        foreach (var r in efe.Where(r => r.ParentCode is null))
            valor[r.Code] = rubros.TieneHijos(r.Code) ? rubros.Hijos(r.Code).Sum(h => valor.GetValueOrDefault(h.Code)) : crudo.GetValueOrDefault(r.Code) * r.Sign;

        var variacionDelEfectivo = valor.GetValueOrDefault(RubrosNiif.EfeEfectivo);
        var actividades = efe.Where(r => r.ParentCode is null && r.Code != RubrosNiif.EfeEfectivo).Sum(r => valor[r.Code]);
        var diferencia = actividades - variacionDelEfectivo;

        var filas = new List<FilaExportable>();
        foreach (var seccion in efe.Select(r => r.Section).Distinct(StringComparer.Ordinal))
        {
            var deLaSeccion = efe.Where(x => x.Section == seccion).ToList();
            if (deLaSeccion.Any(r => r.Code == RubrosNiif.EfeEfectivo))
            {
                filas.Add(new FilaExportable(["Efectivo y equivalentes al inicio", inicio.Valor(RubrosNiif.Efectivo), null], seccion));
                filas.Add(new FilaExportable(["Efectivo y equivalentes al final", fin.Valor(RubrosNiif.Efectivo), null], seccion));
            }
            foreach (var r in ArmadoDeEstados.EnOrdenDePresentacion(deLaSeccion))
                filas.Add(new FilaExportable([r.Name, valor[r.Code], r.Code], seccion, ArmadoDeEstados.Resaltado(rubros, r)));
        }
        filas.Add(new FilaExportable(["Diferencia (Σ actividades − variación del efectivo)", diferencia, null], "Comprobación", Resaltada: true));

        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, request.Filtros, c.PeriodoTexto, c, ct))
        {
            $"Método indirecto. Variaciones entre el {diaAntes:dd/MM/yyyy} y el {c.Hasta:dd/MM/yyyy} de los rubros del estado de situación financiera; positivo = entra efectivo.",
            "Mapeo desde el ESF: " + string.Join("; ", RubrosNiif.EfeDesdeEsf.Where(kv => rubros.Existe(kv.Key)).Select(kv => $"{kv.Key} ← {string.Join(" + ", kv.Value)}")) +
            $". {RubrosNiif.EfeResultado} = resultado del período; {RubrosNiif.EfeAjustes} = gasto por deterioro, depreciación y amortización ({RubrosNiif.Deterioro}); las contra-cuentas ({string.Join(", ", RubrosNiif.ContrasYaEnAjustes)}) no se toman como variación porque su movimiento es ese gasto.",
            "La fila «Diferencia» debe ser cero. Si no lo es, hubo bajas o castigos (el gasto del período no coincide con la variación de las contra-cuentas) o el rango cruza un cierre de ejercicio.",
        };
        if (fueraDelMapeo.Count > 0) notas.Add($"Rubros del ESF fuera del mapeo, sumados a {RubrosNiif.EfeCuentasPorPagar}: {string.Join(", ", fueraDelMapeo)}.");
        if (sinRubro.Count > 0) notas.Add($"{sinRubro.Count} cuenta(s) sin rubro NIIF sumadas a {RubrosNiif.EfeCuentasPorPagar} por su clase: {string.Join(", ", sinRubro.Select(x => x.Code))}.");

        var tabla = new TablaExportable("Estado de flujos de efectivo", $"Del {c.Desde:dd/MM/yyyy} al {c.Hasta:dd/MM/yyyy} · método indirecto", Columnas, filas, null, notas);
        return await ArmadoDeEstados.EntregarAsync(audit, "cash-flow", request.Filtros, tabla, ct);
    }

    private static decimal SaldoSinRubro(SaldosPorRubro.Saldos saldos, JerarquiaDelPlan.Cuenta cuenta) =>
        saldos.SinRubro.Where(x => x.Cuenta.Id == cuenta.Id).Sum(x => x.Saldo);
}
