using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Configuration;

/// <summary>
/// El efecto de la réplica sobre las líneas de crédito (feature 013, US1, T0357; FR-021, US1-5; data-model §1.2; T44): corre <b>en la
/// misma transacción</b> de la réplica cuando el lote trae una <c>USURA</c> o un <c>CatalogoDeModalidades</c> nuevos, y también con
/// Cartera sin activar (las líneas existen antes de activar).
///
/// <list type="bullet">
/// <item><b>Usura nueva</b>: cada línea no cerrada cuya combinación (en el catálogo vigente) usa esa clave se controla con la versión que
/// rige el día de la usura (o hoy, si ya rige), con la versión y cada banda sobre el plan tipo (<see cref="PlanTipoDeLinea"/>). Si alguna la
/// supera, la línea queda bloqueada desde ese día (<c>UsuryBlockedSince</c>, <c>UsuryBlockedByValuePublicId</c>) y se alerta
/// <c>Cartera.LineaCerradaPorUsura</c> con <c>DedupKey</c> por línea. Una línea ya bloqueada no se vuelve a alertar.</item>
/// <item><b>Catálogo nuevo</b>: una línea cuya combinación ya no está, o cuya modalidad salió del catálogo, se cierra con
/// <c>ModalityRetired</c> y se alerta (mismo tipo, motivo en el cuerpo).</item>
/// </list>
/// Los conteos quedan en <c>EffectsSummaryJson</c> de <c>COR_NationalValueReplications</c>. El efecto no guarda: deja los cambios en el
/// contexto de la réplica y devuelve las alertas, que el núcleo levanta tras guardar. Los valores del lote todavía no están en la base (el
/// núcleo los agrega después de los efectos): las usuras se toman del lote; el índice de las líneas variables y el catálogo que el lote no
/// trae, de los lectores únicos sobre el mismo contexto. (nuevo)
/// </summary>
public sealed class EfectoDeReplicacionUsuraEnLineas : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "usura-en-lineas";

    public const string ConteoBloqueadas = "lineasBloqueadas";
    public const string ConteoCerradas = "lineasCerradasPorModalidad";

    /// <summary><c>Cartera.LineaCerradaPorUsura:{código de la línea}</c>.</summary>
    public static string ClaveDeAlerta(string codigoDeLinea) => $"{TiposDeAlerta.LineaCerradaPorUsura}:{codigoDeLinea}";

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        var usuras = lote.Valores
            .Where(v => v.Code == CodigosDeValorNacional.Usura && v.Value is not null)
            .GroupBy(v => v.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.SourceSequence).First(), StringComparer.Ordinal);
        var documentoNuevo = lote.Documentos
            .Where(d => d.Kind == nameof(RegulatoryDocumentKind.CatalogoDeModalidades))
            .OrderByDescending(d => d.ValidFrom).ThenByDescending(d => d.SourceSequence)
            .FirstOrDefault();
        if (usuras.Count == 0 && documentoNuevo is null) return EfectoAplicado.Nada;

        var catalogo = await CatalogoAsync(db, documentoNuevo, lote.Hoy, ct);
        if (catalogo is null)
            return new EfectoAplicado(new Dictionary<string, int> { [ConteoBloqueadas] = 0, [ConteoCerradas] = 0 },
                ["No hay catálogo de modalidades vigente: las líneas no se pudieron revisar contra la usura nueva."], []);

        var lineas = await db.CreditLines.Where(l => !l.IsDeleted && l.Status != CreditLineStatus.Closed).OrderBy(l => l.Code).ToListAsync(ct);
        var versiones = new VersionVigenteDeLinea(db);
        var reglas = new ReglasDeLineaDeCredito(new LectorDeDocumentoNormativo(db), new LectorDeValoresNacionales(db), new PerfilSinUso());
        var alertas = new List<AlertaALevantar>();
        var notas = new List<string>();
        int bloqueadas = 0, cerradas = 0;

        foreach (var linea in lineas)
        {
            var leida = await versiones.LeerVigenteOProximaAsync(linea.Id, lote.Hoy, ct);
            if (leida is null) continue;
            var borrador = leida.Borrador();
            var combinacion = catalogo.Buscar(borrador.ModalityCode, borrador.SubsegmentCode);
            var modalidad = catalogo.Modalidades.FirstOrDefault(m => m.Code == borrador.ModalityCode);

            if (documentoNuevo is not null && (combinacion is null || modalidad?.RetiredFrom is { } retirada && retirada <= lote.Hoy))
            {
                linea.Cerrar(CreditLineClosedReason.ModalityRetired, $"La modalidad {borrador.ModalityCode} salió del catálogo nacional de modalidades.", null, lote.Ahora);
                cerradas++;
                notas.Add($"{linea.Code}: cerrada, su modalidad {borrador.ModalityCode} salió del catálogo.");
                alertas.Add(new AlertaALevantar(TiposDeAlerta.LineaCerradaPorUsura, $"Línea {linea.Code} cerrada: modalidad retirada",
                    $"La línea {linea.Code} ({linea.Name}) se cerró a créditos nuevos: su modalidad {borrador.ModalityCode} ya no está en el catálogo nacional de "
                    + "modalidades vigente. Los créditos vivos no cambian. Registre una línea con una modalidad vigente.",
                    EntityType: nameof(CreditLine), EntityPublicId: linea.PublicId, DedupKey: ClaveDeAlerta(linea.Code),
                    ScopeCreditLinePublicId: linea.PublicId));
                continue;
            }

            if (combinacion?.UsuryKey is not { } clave || !usuras.TryGetValue(clave, out var copia)) continue;
            if (linea.EstaBloqueadaPorUsura) continue;

            var fecha = copia.ValidFrom > lote.Hoy ? copia.ValidFrom : lote.Hoy;
            var aplicable = fecha == lote.Hoy ? borrador : (await versiones.LeerVigenteOProximaAsync(linea.Id, fecha, ct))?.Borrador() ?? borrador;
            var indice = await reglas.IndiceAsync(aplicable, fecha, ct);
            if (indice.IsFailure)
            {
                notas.Add($"{linea.Code}: sin el índice {aplicable.IndexCode} {aplicable.IndexKey} del {Fecha(fecha)}, no se pudo controlar la usura nueva.");
                continue;
            }

            var usura = new UsuraCertificada(copia.Value!.Value, copia.PublicId, copia.Key, copia.ValidFrom);
            var controles = PlanTipoDeLinea.Controlar(aplicable, indice.Value, usura, fecha);
            var excede = controles.Where(c => !c.Resultado.Pasa).OrderBy(c => c.Resultado.Holgura ?? 0m).FirstOrDefault();
            var moraExcede = aplicable.DefaultRateKind == DefaultRateKind.Fixed && aplicable.DefaultRate > usura.Tasa;
            if (excede is null && !moraExcede) continue;

            if (!linea.BloquearPorUsura(fecha, copia.PublicId)) continue;
            bloqueadas++;
            var efectiva = excede?.Resultado.EfectivaConCargos ?? aplicable.DefaultRate ?? 0m;
            notas.Add($"{linea.Code}: bloqueada desde {Fecha(fecha)}, efectiva {Porcentaje(efectiva)} sobre la usura {clave} de {Porcentaje(usura.Tasa)}.");
            alertas.Add(new AlertaALevantar(TiposDeAlerta.LineaCerradaPorUsura, $"Línea {linea.Code} cerrada por usura",
                $"Desde el {Fecha(fecha)} la usura certificada de {borrador.ModalityCode} es {Porcentaje(usura.Tasa)} EA y la línea {linea.Code} ({linea.Name}) "
                + $"llega a {Porcentaje(efectiva)} EA con sus cargos. No admite créditos nuevos hasta que registre una versión bajo la usura. "
                + "Los créditos vivos se topan solos cada día.",
                EntityType: nameof(CreditLine), EntityPublicId: linea.PublicId, DedupKey: ClaveDeAlerta(linea.Code),
                ScopeCreditLinePublicId: linea.PublicId));
        }

        return new EfectoAplicado(new Dictionary<string, int> { [ConteoBloqueadas] = bloqueadas, [ConteoCerradas] = cerradas }, notas, alertas);
    }

    private static async Task<CatalogoDeModalidades?> CatalogoAsync(IApplicationDbContext db, RegulatoryDocument? nuevo, DateOnly hoy, CancellationToken ct)
    {
        if (nuevo is not null)
        {
            var leido = CatalogoDeModalidades.Leer(nuevo.Content, nuevo.SchemaVersion);
            if (leido.EsValido) return leido.Documento;
        }
        var vigente = await new LectorDeDocumentoNormativo(db).CatalogoDeModalidadesAsync(hoy, null, ct);
        return vigente.IsSuccess ? vigente.Value.Documento : null;
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.##", CultureInfo.GetCultureInfo("es-CO")) + " %";

    /// <summary>La réplica no mira el perfil regulatorio: sólo controla la usura y la modalidad.</summary>
    private sealed class PerfilSinUso : IPerfilRegulatorio
    {
        public Task<PerfilRegulatorioDto?> VigenteAsync(DateOnly fecha, DateTime? informacionHasta, CancellationToken ct) =>
            Task.FromResult<PerfilRegulatorioDto?>(null);
    }
}
