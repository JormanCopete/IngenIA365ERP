using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>
/// La vista <c>concentration</c> de <c>/api/reports/lending</c> (feature 013, US15, K5, T0981; FR-134, FR-041; US15-2; contracts/api.md §17): la
/// cartera viva al corte (<c>asOf</c>) concentrada por deudor, grupo conectado, línea, sector o pagaduría (<c>by</c>), con la participación de
/// cada uno en el total y, por deudor y por grupo, su porcentaje del <b>límite individual</b> y la señal si está cerca (<c>warnAt</c>, por
/// defecto <see cref="AvisoPorDefecto"/> % del límite) o lo supera. <c>top</c> recorta los mayores deudores o grupos. Por deudor y por grupo
/// trae datos del deudor (C84: la ruta exige <c>Lending.Loans.Read</c> para esos dos y el permiso de datos personales para exportarlos); por
/// línea, sector y pagaduría es agregada. (nuevo)
///
/// <para>Definiciones fijadas aquí, <b>a validar por la contadora</b> (T0977):</para>
/// <list type="bullet">
/// <item>El <b>saldo</b> es el capital de la posición diaria vigente al corte de los créditos vivos no castigados del alcance, como deudor.</item>
/// <item>El <b>límite</b> es <c>Concentracion.LimiteIndividual</c> (acotado por el límite legal) por el patrimonio técnico vigente de
/// <see cref="LectorDeIndicadoresDeLaEntidad"/> (C33), el mismo que usan las validaciones de US2, que hoy miden sin garantía admisible. Sin
/// patrimonio técnico vigente la vista sale sin porcentaje y con la advertencia, no falla.</item>
/// <item>El <b>grupo conectado</b> es el de <see cref="GrupoConectadoDelDeudor"/> (el mismo cálculo de la validación de concentración de US2) y su
/// saldo suma el de sus miembros dentro del alcance; los grupos de una sola persona no se listan.</item>
/// <item>El <b>sector</b> es la actividad económica (CIIU) de la persona en el maestro, «Sin sector» si falta; la <b>pagaduría</b>, la del vínculo
/// de libranza vigente al corte, «Sin pagaduría» si no tiene.</item>
/// </list>
/// </summary>
public sealed record ConcentrationReportQuery(FiltrosDeInformeDeCartera Filtros, string? By = null, string? Top = null, string? WarnAt = null)
    : IRequest<Result<TablaExportable>>;

public sealed class ConcentrationReportQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ReglasDeParametrosDeCartera parametros,
    LectorDeIndicadoresDeLaEntidad indicadores,
    GrupoConectadoDelDeudor grupos,
    IDateTimeService reloj) : IRequestHandler<ConcentrationReportQuery, Result<TablaExportable>>
{
    public const string PorDeudor = "debtor";
    public const string PorGrupo = "group";
    public const string PorLinea = "line";
    public const string PorSector = "sector";
    public const string PorPagaduria = "payer";

    public static readonly IReadOnlyList<string> Dimensiones = [PorDeudor, PorGrupo, PorLinea, PorSector, PorPagaduria];

    /// <summary>Las dimensiones que nombran personas (C84).</summary>
    public static readonly IReadOnlyList<string> ConDatosDelDeudor = [PorDeudor, PorGrupo];

    public const int TopPorDefecto = 20;
    public const int TopMaximo = 500;
    public const int AvisoPorDefecto = 80;

    public const string SenalExcede = "Supera el límite";
    public const string SenalCerca = "Cerca del límite";
    public const string SinSector = "Sin sector";
    public const string SinPagaduria = "Sin pagaduría";

    /// <summary>
    /// Por deudor y por grupo trae datos del deudor: la ruta exige <c>Lending.Loans.Read</c> y, para exportar, el permiso de datos personales; por
    /// línea, sector y pagaduría (<see cref="VistaDeInformeDeCartera.PersonalDataExceptWhen"/>) es agregada y basta <c>Lending.Reports.View</c>.
    /// </summary>
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "concentration", "Concentración",
        "Los mayores deudores y grupos conectados, y la cartera por línea, sector y pagaduría, con su participación y su porcentaje del límite individual.",
        "concentracion", ["asOf", "branch", "line", "modality", "subsegment"], ["by", "top", "warnAt"], EntregaDeCartera.K5) with
    {
        PersonalDataExceptWhen = new ExcepcionDeDatosDelDeudor("by", [PorLinea, PorSector, PorPagaduria]),
    };

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Dimensión"),
        new("Saldo", TipoDeColumna.Moneda),
        new("Participación", TipoDeColumna.Porcentaje),
        new("% del límite individual", TipoDeColumna.Porcentaje),
        new("Señal"),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(ConcentrationReportQuery request, CancellationToken ct)
    {
        var by = FiltrosDeInformeDeCartera.Elegir("by", request.By, Dimensiones, PorDeudor);
        if (by.IsFailure) return Result.Failure<TablaExportable>(by.Error);
        var top = TopPorDefecto;
        if (!string.IsNullOrWhiteSpace(request.Top)
            && (!int.TryParse(request.Top, NumberStyles.None, CultureInfo.InvariantCulture, out top) || top < 1 || top > TopMaximo))
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("top", request.Top, [$"un entero de 1 a {TopMaximo}"]));
        var aviso = (decimal)AvisoPorDefecto;
        if (!string.IsNullOrWhiteSpace(request.WarnAt)
            && (!decimal.TryParse(request.WarnAt, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out aviso) || aviso <= 0m || aviso > 100m))
            return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("warnAt", request.WarnAt, ["un porcentaje del límite mayor que 0 y hasta 100"]));

        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var creditos = await CargadorDeRiesgo.VivosAl(ambito.Creditos(db), corte).Where(l => l.Status != LoanStatus.WrittenOff)
            .Select(l => new { l.Id, l.DebtorPersonId, l.CreditLineId }).ToListAsync(ct);
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, creditos.Select(c => c.Id).ToList(), corte, ct);
        var saldos = creditos
            .Where(c => posiciones.TryGetValue(c.Id, out var p) && p.Principal > 0m && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode))
            .Select(c => (c.Id, Persona: c.DebtorPersonId, Linea: c.CreditLineId, Saldo: posiciones[c.Id].Principal))
            .ToList();
        var total = saldos.Sum(s => s.Saldo);

        var notas = new List<string>
        {
            "Saldo: capital de la posición diaria al corte de los créditos vivos no castigados, como deudor. Participación: sobre el total del alcance.",
        };
        var limite = await LimiteAsync(corte, notas, ct);
        var conLimite = ConDatosDelDeudor.Contains(by.Value);
        if (conLimite && limite is { } l)
            notas.Add($"Límite individual: {l:N0} (el porcentaje vigente del patrimonio técnico). Señal desde el {aviso.ToString("0.##", CultureInfo.InvariantCulture)} % del límite.");
        if (!conLimite) notas.Add("El porcentaje del límite individual se mide por deudor y por grupo conectado.");
        notas.Add("Definiciones a validar por la contadora.");

        IReadOnlyList<(string Dimension, decimal Saldo, Guid? Persona)> filas = by.Value switch
        {
            PorDeudor => await PorPersonaAsync(saldos.GroupBy(s => s.Persona).ToDictionary(g => g.Key, g => g.Sum(s => s.Saldo)), ct),
            PorGrupo => await PorGrupoAsync(saldos.GroupBy(s => s.Persona).ToDictionary(g => g.Key, g => g.Sum(s => s.Saldo)), corte, ct),
            PorLinea => await PorLineaAsync(saldos.Select(s => (s.Linea, s.Saldo)).ToList(), ct),
            PorSector => await PorSectorAsync(saldos.Select(s => (s.Persona, s.Saldo)).ToList(), ct),
            _ => await PorPagaduriaAsync(saldos.Select(s => (s.Id, s.Saldo)).ToList(), corte, ct),
        };

        var ordenadas = filas.OrderByDescending(x => x.Saldo).ThenBy(x => x.Dimension, StringComparer.CurrentCulture).ToList();
        if (conLimite) ordenadas = ordenadas.Take(top).ToList();
        var resultado = ordenadas.Select(x =>
        {
            decimal? delLimite = conLimite && limite is { } lim && lim > 0m ? QualityReportQueryHandler.Porcentaje(x.Saldo, lim) : null;
            var senal = delLimite is not { } p ? string.Empty : p > 100m ? SenalExcede : p >= aviso ? SenalCerca : string.Empty;
            return new FilaExportable([x.Dimension, x.Saldo, QualityReportQueryHandler.Porcentaje(x.Saldo, total), delLimite, senal, x.Persona?.ToString()]);
        }).ToList();
        var totales = new FilaExportable(["Total del alcance", total, total == 0m ? null : 100m, null, null, null]);
        return Result.Success(new TablaExportable("Concentración",
            $"Al {corte:yyyy-MM-dd} · por {Nombre(by.Value)}" + (conLimite ? $" · los {top} mayores" : string.Empty) + $" · {ambito.Descripcion()}",
            Columnas, resultado, totales, notas));
    }

    /// <summary>El límite individual en pesos al corte, o nulo (con la advertencia en las notas) si no hay patrimonio técnico vigente o porcentaje.</summary>
    private async Task<decimal?> LimiteAsync(DateOnly corte, List<string> notas, CancellationToken ct)
    {
        var leidos = await indicadores.LeerAsync(corte, ct);
        if (!leidos.PatrimonioTecnicoVigente || leidos.PatrimonioTecnico is not { } tecnico)
        {
            notas.Add("Advertencia: no hay un patrimonio técnico vigente registrado en Contabilidad al corte; la vista sale sin el porcentaje del límite individual.");
            return null;
        }
        var fraccion = await parametros.LeerAsync(ParametrosDeCartera.ConcentracionLimiteIndividual, corte, ct: ct);
        if (fraccion.IsFailure || fraccion.Value.Valor is not decimal f)
        {
            notas.Add("Advertencia: no se pudo leer el porcentaje del límite individual vigente; la vista sale sin el porcentaje del límite.");
            return null;
        }
        return decimal.Round(tecnico.Value * f, 2, MidpointRounding.AwayFromZero);
    }

    private static string Nombre(string by) => by switch
    {
        PorGrupo => "grupo conectado",
        PorLinea => "línea",
        PorSector => "sector",
        PorPagaduria => "pagaduría",
        _ => "deudor",
    };

    private async Task<IReadOnlyList<(string, decimal, Guid?)>> PorPersonaAsync(IReadOnlyDictionary<int, decimal> porPersona, CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, porPersona.Keys, ct);
        return porPersona.Select(p => (personas.GetValueOrDefault(p.Key)?.FullName ?? string.Empty, p.Value, personas.GetValueOrDefault(p.Key)?.PersonPublicId)).ToList();
    }

    private async Task<IReadOnlyList<(string, decimal, Guid?)>> PorGrupoAsync(IReadOnlyDictionary<int, decimal> porPersona, DateOnly corte, CancellationToken ct)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var conjuntos = new List<(IReadOnlyList<int> Miembros, Guid? Registrado)>();
        foreach (var persona in porPersona.Keys.OrderBy(p => p))
        {
            var grupo = await grupos.DeAsync(persona, corte, ct);
            if (grupo.EsDeUno) continue;
            var miembros = grupo.Miembros.Distinct().OrderBy(m => m).ToList();
            if (vistos.Add(string.Join(',', miembros))) conjuntos.Add((miembros, grupo.GrupoPublicId));
        }
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, conjuntos.SelectMany(c => c.Miembros), ct);
        var registrados = conjuntos.Where(c => c.Registrado is not null).Select(c => c.Registrado!.Value).ToList();
        var nombres = await db.ConnectedGroups.AsNoTracking().IgnoreQueryFilters().Where(g => registrados.Contains(g.PublicId))
            .ToDictionaryAsync(g => g.PublicId, g => g.Name, ct);
        return conjuntos.Select(c =>
        {
            var saldo = c.Miembros.Sum(m => porPersona.GetValueOrDefault(m));
            var mayor = c.Miembros.OrderByDescending(m => porPersona.GetValueOrDefault(m)).ThenBy(m => m).First();
            var nombreMayor = personas.GetValueOrDefault(mayor)?.FullName ?? string.Empty;
            var nombre = c.Registrado is { } r && nombres.TryGetValue(r, out var registrado)
                ? $"{registrado} ({c.Miembros.Count} personas)"
                : $"Grupo de {nombreMayor} y {c.Miembros.Count - 1} más";
            return (nombre, saldo, personas.GetValueOrDefault(mayor)?.PersonPublicId);
        }).Where(x => x.saldo > 0m).ToList();
    }

    private async Task<IReadOnlyList<(string, decimal, Guid?)>> PorLineaAsync(IReadOnlyList<(int Linea, decimal Saldo)> saldos, CancellationToken ct)
    {
        var ids = saldos.Select(s => s.Linea).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => $"{l.Code} · {l.Name}", ct);
        return saldos.GroupBy(s => lineas.GetValueOrDefault(s.Linea) ?? string.Empty).Select(g => (g.Key, g.Sum(s => s.Saldo), (Guid?)null)).ToList();
    }

    private async Task<IReadOnlyList<(string, decimal, Guid?)>> PorSectorAsync(IReadOnlyList<(int Persona, decimal Saldo)> saldos, CancellationToken ct)
    {
        var ids = saldos.Select(s => s.Persona).Distinct().ToList();
        var sectores = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.CiiuCode, ct);
        return saldos.GroupBy(s => sectores.GetValueOrDefault(s.Persona) is { Length: > 0 } ciiu ? $"CIIU {ciiu.Trim()}" : SinSector)
            .Select(g => (g.Key, g.Sum(s => s.Saldo), (Guid?)null)).ToList();
    }

    private async Task<IReadOnlyList<(string, decimal, Guid?)>> PorPagaduriaAsync(IReadOnlyList<(int Credito, decimal Saldo)> saldos, DateOnly corte,
        CancellationToken ct)
    {
        var ids = saldos.Select(s => s.Credito).ToList();
        var vinculos = (await db.LoanPayrollLinks.AsNoTracking()
                .Where(v => ids.Contains(v.LoanId) && !v.IsDeleted && v.ValidFrom <= corte && (v.ValidTo == null || v.ValidTo >= corte))
                .Select(v => new { v.LoanId, v.Id, v.PaymasterPersonId, v.PayrollAgreementId }).ToListAsync(ct))
            .GroupBy(v => v.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Id).First());
        var convenios = vinculos.Values.Where(v => v.PayrollAgreementId is not null).Select(v => v.PayrollAgreementId!.Value).Distinct().ToList();
        var deConvenio = await db.PayrollAgreements.AsNoTracking().IgnoreQueryFilters().Where(a => convenios.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.Code, a.PaymasterPersonId }, ct);
        int? Pagadora(int credito) => vinculos.TryGetValue(credito, out var v)
            ? v.PaymasterPersonId ?? (v.PayrollAgreementId is { } c && deConvenio.TryGetValue(c, out var a) ? a.PaymasterPersonId : null)
            : null;
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, saldos.Select(s => Pagadora(s.Credito)).OfType<int>(), ct);
        string Nombre(int credito)
        {
            if (Pagadora(credito) is not { } p) return SinPagaduria;
            var codigo = vinculos[credito].PayrollAgreementId is { } c && deConvenio.TryGetValue(c, out var a) ? $"{a.Code} · " : string.Empty;
            return codigo + (personas.GetValueOrDefault(p)?.FullName ?? string.Empty);
        }
        return saldos.GroupBy(s => Nombre(s.Credito)).Select(g => (g.Key, g.Sum(s => s.Saldo), (Guid?)null)).ToList();
    }
}
