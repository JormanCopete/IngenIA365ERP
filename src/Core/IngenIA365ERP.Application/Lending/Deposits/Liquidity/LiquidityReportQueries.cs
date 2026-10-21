using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Domain.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Deposits.Liquidity;

// Feature 014, US15 (T216; adelanta de T219 las dos vistas de liquidez de api.md §18.1; FR-109, FR-110): el fondo de liquidez por clase y banda, sin titulares, y
// los depósitos asegurados por ahorrador, con datos del titular y el alcance de quien pregunta. (nuevo)

/// <summary>La vista <c>liquidity-fund</c> (api.md §18.1): la base, el porcentaje y el requerido por clase y banda al corte, con el constituido y la diferencia. (nuevo)</summary>
public sealed record LiquidityFundReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class LiquidityFundReportQueryHandler(FondoDeLiquidezDeCartera fondo, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<LiquidityFundReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = new("liquidity-fund", "Fondo de liquidez",
        "La base del fondo de liquidez por clase de depósito y banda de madurez, el porcentaje, el requerido y el constituido al corte, sin datos de titulares.",
        "fondo-de-liquidez", ["asOf"], [], AvailableFrom: EntregaDeCartera.K11);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Clase"),
        new("Banda"),
        new("Cuentas", TipoDeColumna.Entero),
        new("Base", TipoDeColumna.Moneda),
        new("Porcentaje", TipoDeColumna.Porcentaje),
        new("Requerido", TipoDeColumna.Moneda),
        new("Constituido", TipoDeColumna.Moneda),
        new("Diferencia", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(LiquidityFundReportQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<TablaExportable>(fuera);
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var posicion = await fondo.PosicionAsync(corte, ct);
        if (posicion.IsFailure) return Result.Failure<TablaExportable>(posicion.Error);
        var p = posicion.Value;
        var filas = p.Calculo.Renglones.Select(r => new FilaExportable([Clase(r.Clase), r.Banda ?? "—", r.Cuentas, r.Base, r.Porcentaje * 100m, r.Requerido, null, null]))
            .ToList();
        var totales = new FilaExportable(["Total", null, filas.Sum(f => (int)f.Valores[2]!), p.Calculo.Renglones.Sum(r => r.Base), null, p.Calculo.Requerido,
            p.Constituido, p.Diferencia]);
        var notas = new List<string>(p.Calculo.Explicacion)
        {
            $"Categoría {p.Categoria}. Constituido: las inversiones del fondo registradas vigentes al corte. Una diferencia negativa es un faltante (alerta "
            + "Cartera.FondoDeLiquidezInsuficiente al tomar la foto).",
        };
        return Result.Success(new TablaExportable("Fondo de liquidez", $"Al {corte:yyyy-MM-dd} · régimen {p.Etapa}", Columnas, filas, totales, notas));
    }

    public static string Clase(Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez clase) => clase switch
    {
        Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez.ALaVista => "Ahorro a la vista",
        Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez.Cdat => "CDAT",
        Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez.Contractual => "Ahorro contractual",
        Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez.PermanenteConRetiroParcial => "Ahorro permanente (retiro parcial)",
        Domain.Lending.Deposits.ClaseDeDepositoParaLiquidez.PermanenteSoloAlRetirarse => "Ahorro permanente (sólo al retirarse)",
        _ => clase.ToString(),
    };
}

/// <summary>
/// La vista <c>insured-deposits</c> (api.md §18.1; FR-110): por ahorrador, sus depósitos (capital e intereses causados de todas sus cuentas de captación, sin
/// aportes), lo asegurado hasta el tope vigente y lo no asegurado; sólo si la entidad está inscrita en el seguro de depósitos. Con datos del titular y el alcance
/// de oficina y línea de quien pregunta (entra el ahorrador con alguna cuenta en el alcance). (nuevo)
/// </summary>
public sealed record InsuredDepositsReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class InsuredDepositsReportQueryHandler(IApplicationDbContext db, FondoDeLiquidezDeCartera fondo, IAlcanceDeCartera alcanceDeLaPeticion,
    IDateTimeService reloj) : IRequestHandler<InsuredDepositsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistasDeCaptaciones.ConDatosDelTitular(
        "insured-deposits", "Depósitos asegurados",
        "Por ahorrador, sus depósitos, lo asegurado hasta el tope del seguro de depósitos y lo no asegurado (sólo si la entidad está inscrita; sin aportes).",
        "depositos-asegurados", ["asOf"], [], EntregaDeCartera.K11);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Ahorrador"),
        new("Documento"),
        new("Cuentas", TipoDeColumna.Entero),
        new("Depósitos", TipoDeColumna.Moneda),
        new("Asegurado hasta el tope", TipoDeColumna.Moneda),
        new("No asegurado", TipoDeColumna.Moneda),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(InsuredDepositsReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var asegurados = await fondo.AseguradosAsync(corte, ct);
        if (!asegurados.Inscrita)
            return Result.Success(new TablaExportable("Depósitos asegurados", $"Al {corte:yyyy-MM-dd}", Columnas, [], null,
                ["El perfil regulatorio vigente no dice que la entidad esté inscrita en el seguro de depósitos: no hay depósitos asegurados."]));
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var enAlcance = asegurados.Depositos.Where(d => alcance.IncluyeOficina(d.OficinaId) && alcance.IncluyeLinea(d.LineaId)).Select(d => d.PersonaId).ToHashSet();
        var visibles = asegurados.Ahorradores.Where(a => enAlcance.Contains(a.PersonaId)).ToList();
        var personas = await LectorDeCuentas.PersonasAsync(db, visibles.Select(a => a.PersonaId), ct);
        var filas = visibles.Select(a =>
        {
            var p = personas[a.PersonaId];
            return new FilaExportable([p.FullName, p.DocumentNumber, a.Cuentas, a.Depositos, a.Asegurado, a.NoAsegurado, p.PersonPublicId.ToString()]);
        }).OrderBy(f => (string)f.Valores[0]!, StringComparer.CurrentCulture).ToList();
        var totales = new FilaExportable(["Total", null, visibles.Sum(a => a.Cuentas), visibles.Sum(a => a.Depositos), visibles.Sum(a => a.Asegurado),
            visibles.Sum(a => a.NoAsegurado), null]);
        var nota = asegurados.Tope is { } tope
            ? $"Tope por ahorrador: {tope:N2} ({asegurados.FuenteDelTope}). Cuenta lo de cada titular en todas sus cuentas de captación; los aportes no se aseguran."
            : "No hay un tope del seguro de depósitos registrado para el corte (TOPE_SEGURO_DEPOSITOS): nada se muestra asegurado.";
        return Result.Success(new TablaExportable("Depósitos asegurados", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales, [nota]));
    }
}

/// <summary>
/// La vista <c>liquidity-deposit-bands</c> (feature 014, respuesta 102 B del dueño, E250): los depósitos y los aportes por banda del indicador de riesgo de liquidez
/// —las salidas contractuales (CDAT y contractual) y no contractuales (ahorro a la vista, permanente y aportes) de cada banda—, el insumo de los renglones de
/// depósitos y aportes de la evaluación del riesgo de liquidez (F29, código 159) para quien arma el formato con lo demás de su balance. No genera el archivo
/// F29 completo. Sin datos de titulares; alcance total. (nuevo)
/// </summary>
public sealed record LiquidityDepositBandsReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class LiquidityDepositBandsReportQueryHandler(FondoDeLiquidezDeCartera fondo, IAlcanceDeCartera alcance, IDateTimeService reloj)
    : IRequestHandler<LiquidityDepositBandsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = new("liquidity-deposit-bands", "Depósitos y aportes por banda de liquidez",
        "Las salidas contractuales y no contractuales de los depósitos y los aportes por banda del indicador de riesgo de liquidez al corte, sin datos de titulares.",
        "depositos-y-aportes-por-banda", ["asOf"], [], AvailableFrom: EntregaDeCartera.K11);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Banda"),
        new("Hasta (días)", TipoDeColumna.Entero),
        new("Salidas contractuales", TipoDeColumna.Moneda),
        new("Salidas no contractuales", TipoDeColumna.Moneda),
        new("Total de salidas", TipoDeColumna.Moneda),
    ];

    public async Task<Result<TablaExportable>> Handle(LiquidityDepositBandsReportQuery request, CancellationToken ct)
    {
        if (await AlcanceDelFondoDeLiquidez.TotalAsync(alcance, ct) is { } fuera) return Result.Failure<TablaExportable>(fuera);
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var medicion = await fondo.RiesgoAsync(corte, MedidaDeRiesgoDeLiquidez.Irl, ct);
        var bandas = medicion.Medicion.Bandas;
        var filas = bandas.Select(b => new FilaExportable([b.Banda, b.HastaDias, b.SalidasContractuales, b.SalidasNoContractuales,
            b.SalidasContractuales + b.SalidasNoContractuales])).ToList();
        var totales = new FilaExportable(["Total", null, bandas.Sum(b => b.SalidasContractuales), bandas.Sum(b => b.SalidasNoContractuales),
            bandas.Sum(b => b.SalidasContractuales + b.SalidasNoContractuales)]);
        var notas = new List<string>
        {
            "Insumo de los renglones de depósitos y aportes de la evaluación del riesgo de liquidez (F29, código 159): el archivo completo lo arma la entidad con lo demás de su balance.",
        };
        notas.AddRange(medicion.Supuestos);
        return Result.Success(new TablaExportable("Depósitos y aportes por banda de liquidez", $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales, notas));
    }
}
