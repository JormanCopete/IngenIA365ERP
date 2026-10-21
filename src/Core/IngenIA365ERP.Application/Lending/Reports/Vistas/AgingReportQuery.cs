using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>aging</c> de <c>/api/reports/lending</c> (feature 013, US15, K1, T0797; FR-133; contracts/api.md §17): la cartera viva al corte
/// (<c>asOf</c>, hoy por defecto) por rangos de edad de mora —los límites de <c>Informes.RangosDeEdad</c> vigentes al corte o los del filtro
/// propio <c>ranges</c> (días en orden creciente, separados por coma)—, una fila por crédito con su saldo de capital y sus días de mora de la
/// posición diaria vigente al corte (<c>LND_LoanDailyPositions</c>) y la calificación <b>informativa</b> del mismo motor del cierre
/// (<see cref="CalificacionInformativa"/>, T46), agrupadas por rango. Trae datos del deudor (PD). El alcance va en la consulta de créditos
/// (<see cref="AmbitoDeInformeDeCartera"/>, <see cref="IAlcanceDeCartera"/>). Columnas ocultas <c>_credito</c> y <c>_persona</c>. (nuevo)
/// </summary>
public sealed record AgingReportQuery(FiltrosDeInformeDeCartera Filtros, string? Ranges = null) : IRequest<Result<TablaExportable>>;

public sealed class AgingReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ReglasDeParametrosDeCartera parametros,
    CalificacionInformativa informativa,
    IDateTimeService reloj) : IRequestHandler<AgingReportQuery, Result<TablaExportable>>
{
    public const string PatronDeRangos = "días en orden creciente separados por coma, como 30,60,90";

    /// <summary>Lo que la vista declara al publicarse (T0798).</summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "aging", "Cartera por edades", "Saldo de capital por rango de días de mora al corte, crédito por crédito, con la calificación informativa.",
        "cartera-por-edades", ["asOf", "branch", "line", "modality", "subsegment", "advisor"], ["ranges"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"),
        new("Deudor"),
        new("Línea"),
        new("Modalidad"),
        new("Oficina"),
        new("Saldo de capital", TipoDeColumna.Moneda),
        new("Días de mora", TipoDeColumna.Entero),
        new("Rango"),
        new("Calificación"),
        new("_credito", Clave: "_credito"),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(AgingReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);

        IReadOnlyList<int> limites;
        if (!string.IsNullOrWhiteSpace(request.Ranges))
        {
            var propios = Limites(request.Ranges);
            if (propios is null)
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("ranges", request.Ranges, [PatronDeRangos]));
            limites = propios;
        }
        else
        {
            var leido = await parametros.LeerAsync(ParametrosDeCartera.InformesRangosDeEdad, corte, ct: ct);
            if (leido.IsFailure) return Result.Failure<TablaExportable>(leido.Error);
            limites = Limites(leido.Value.Texto) ?? Limites(ParametrosDeCartera.Definiciones
                .First(d => d.Clave == ParametrosDeCartera.InformesRangosDeEdad).DefectoSeguro)!;
        }

        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = await CargadorDeRiesgo.VivosAl(ambito.Creditos(db), corte).OrderBy(l => l.Number).ThenBy(l => l.Id).ToListAsync(ct);
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, creditos.Select(c => c.Id).ToList(), corte, ct);
        var vivos = creditos.Where(c => posiciones.TryGetValue(c.Id, out var p) && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode)).ToList();

        var notas = new List<string>
        {
            $"Rangos de días de mora: {string.Join(" · ", Etiquetas(limites))}" + (string.IsNullOrWhiteSpace(request.Ranges) ? " (Informes.RangosDeEdad)." : "."),
            "Saldo de capital y días de mora de la posición diaria vigente al corte; la calificación es informativa (el mismo motor del cierre, no se guarda).",
        };
        var calificaciones = new Dictionary<int, string>();
        if (vivos.Count > 0)
        {
            var calculo = await informativa.CalcularAsync(vivos.Select(c => c.Id).ToList(), corte, null, sinAnalisis: false, ct);
            if (calculo.IsSuccess)
            {
                foreach (var r in calculo.Value.Calculados)
                    calificaciones[r.Credito.Credito.Id] = TextosDeInformesDeCartera.Calificacion(r.Calificacion.Categoria, r.Calificacion.Subcategoria);
            }
            else
            {
                notas.Add($"Sin calificación informativa: {calculo.Error.Message}");
            }
        }

        var (personas, lineas, oficinas) = await ListLoansQueryHandler.ReferenciasAsync(db, vivos, ct);
        var filas = vivos
            .Select(c => (Credito: c, Posicion: posiciones[c.Id], Rango: RangoDe(posiciones[c.Id].DaysPastDue, limites)))
            .OrderBy(x => x.Rango.Orden).ThenByDescending(x => x.Posicion.DaysPastDue).ThenBy(x => x.Credito.Number, StringComparer.Ordinal)
            .Select(x => new FilaExportable(
            [
                x.Credito.Number, personas[x.Credito.DebtorPersonId].Name, $"{lineas[x.Credito.CreditLineId].Code} · {lineas[x.Credito.CreditLineId].Name}",
                x.Posicion.ModalityCode, oficinas[x.Credito.BranchId].Name, x.Posicion.Principal, x.Posicion.DaysPastDue, x.Rango.Etiqueta,
                calificaciones.GetValueOrDefault(x.Credito.Id) ?? string.Empty, x.Credito.PublicId.ToString(), personas[x.Credito.DebtorPersonId].PublicId.ToString(),
            ], x.Rango.Etiqueta))
            .ToList();
        var totales = new FilaExportable(["Total", null, null, null, null, filas.Sum(r => (decimal)r.Valores[5]!), null, null, null, null, null]);
        return Result.Success(new TablaExportable("Cartera por edades", $"Al {corte:yyyy-MM-dd} · {vivos.Count} créditos · {ambito.Descripcion()}",
            Columnas, filas, totales, notas));
    }

    /// <summary>Los límites de los rangos (días, estrictamente crecientes y positivos), o nulo si el texto no los trae así.</summary>
    public static IReadOnlyList<int>? Limites(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limites = new List<int>();
        foreach (var parte in texto.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(parte, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0) return null;
            if (limites.Count > 0 && n <= limites[^1]) return null;
            limites.Add(n);
        }
        return limites.Count == 0 ? null : limites;
    }

    /// <summary>Los rótulos de los rangos: «Al día», «1 a 30», «31 a 60»… y «Más de N».</summary>
    public static IReadOnlyList<string> Etiquetas(IReadOnlyList<int> limites)
    {
        var etiquetas = new List<string> { "Al día" };
        var desde = 1;
        foreach (var l in limites)
        {
            etiquetas.Add($"{desde} a {l}");
            desde = l + 1;
        }
        etiquetas.Add($"Más de {limites[^1]}");
        return etiquetas;
    }

    /// <summary>El rango de unos días de mora: su orden (0 = al día) y su rótulo.</summary>
    public static (int Orden, string Etiqueta) RangoDe(int dias, IReadOnlyList<int> limites)
    {
        var etiquetas = Etiquetas(limites);
        if (dias <= 0) return (0, etiquetas[0]);
        for (var i = 0; i < limites.Count; i++)
            if (dias <= limites[i]) return (i + 1, etiquetas[i + 1]);
        return (limites.Count + 1, etiquetas[^1]);
    }
}
