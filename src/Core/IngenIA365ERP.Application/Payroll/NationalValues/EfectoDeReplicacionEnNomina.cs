using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Payroll.NationalValues;

/// <summary>
/// La proyección de Nómina de los valores nacionales (feature 013, T93, enmienda A7; data-model parte 3 §7.5): cuando la réplica
/// trae <c>SMMLV</c>, <c>UVT</c> o <c>FESTIVO</c>, escribe en la <b>misma transacción</b> la fila de <c>PAY_LegalParameters</c>
/// (fuente «Valor nacional {SourcePublicId}») o de <c>PAY_Holidays</c> (<see cref="HolidayOrigin.National"/> con
/// <see cref="Holiday.NationalValuePublicId"/>). Así los 13 lectores de <c>PAY_LegalParameters</c>, <c>LectorDeUvt</c> y los motores
/// no cambian: leen lo que sólo escribe la réplica. Cuatro reglas:
///
/// <list type="number">
/// <item><b>Adopta lo que ya existe.</b> Mismo código y vigencia desde con el mismo valor (o la misma fecha de festivo) se marca
/// como proyección sin insertar: los únicos <c>UK_PAY_LegalParameters_Code_ValidFrom</c> y <c>UK_PAY_Holidays_Date</c> harían fallar
/// la réplica. Si el valor difiere, se conserva el de Nómina y se alerta <c>Nomina.ValorNacionalDistinto</c> para que el contador
/// decida.</item>
/// <item>Lo nuevo se inserta (las semillas de Nómina ya no siembran SMMLV, UVT ni festivos Ley 51).</item>
/// <item><b>Cierra la vigencia anterior</b> la víspera de la nueva, como <c>AddLegalParameterVersionCommand</c>, y marca
/// desactualizados los borradores de nómina.</item>
/// <item><b>Sólo la réplica escribe</b>: <c>AddLegalParameterVersionCommand</c> rechaza SMMLV y UVT
/// (<c>Payroll.LegalParameter.NationalValue</c>) y los comandos de festivos no editan ni borran un <c>National</c>.</item>
/// </list>
/// Los festivos <c>Manual</c> y <c>Decreed</c> son de Nómina y no se tocan (Cartera no los usa). Una corrección central reemplaza en
/// la proyección el valor de la fila que corrige. (nuevo)
/// </summary>
public sealed class EfectoDeReplicacionEnNomina : IEfectoDeReplicacion
{
    public const string NombreDelEfecto = "nomina";

    /// <summary>El prefijo de la fuente de una fila proyectada.</summary>
    public const string PrefijoDeFuente = "Valor nacional ";

    public static string FuenteDe(Guid sourcePublicId) => $"{PrefijoDeFuente}{sourcePublicId:D}";

    /// <summary>Los códigos de <c>PAY_LegalParameters</c> que son valores nacionales (los de la central se llaman igual).</summary>
    public static readonly IReadOnlySet<string> CodigosProyectados = new HashSet<string>(StringComparer.Ordinal)
    {
        LegalParameterCodes.Smmlv,
        LegalParameterCodes.Uvt,
    };

    public string Nombre => NombreDelEfecto;

    public async Task<EfectoAplicado> AlReplicarAsync(IApplicationDbContext db, LoteReplicado lote, CancellationToken ct)
    {
        var conteo = new Conteo();
        var notas = new List<string>();
        var alertas = new List<AlertaALevantar>();

        foreach (var copia in lote.Valores.Where(v => CodigosProyectados.Contains(v.Code) && v.Value is not null))
            await ParametroAsync(db, copia, lote, conteo, notas, alertas, ct);

        foreach (var copia in lote.Valores.Where(v => v.Code == CodigosDeValorNacional.Festivo))
            await FestivoAsync(db, copia, lote, conteo, notas, ct);

        if (conteo.ParametrosNuevos > 0)
            await new PayrollRunStaleMarker(db, NullLogger<PayrollRunStaleMarker>.Instance)
                .MarkAllDraftsStaleAsync("valor nacional replicado (SMMLV o UVT con vigencia nueva)", ct);

        return new EfectoAplicado(new Dictionary<string, int>
        {
            ["parametrosAdoptados"] = conteo.ParametrosAdoptados,
            ["parametrosInsertados"] = conteo.ParametrosNuevos,
            ["parametrosCorregidos"] = conteo.ParametrosCorregidos,
            ["parametrosDistintos"] = conteo.ParametrosDistintos,
            ["festivosAdoptados"] = conteo.FestivosAdoptados,
            ["festivosInsertados"] = conteo.FestivosNuevos,
            ["festivosLocalesConservados"] = conteo.FestivosLocales,
        }, notas, alertas);
    }

    private sealed class Conteo
    {
        public int ParametrosAdoptados, ParametrosNuevos, ParametrosCorregidos, ParametrosDistintos, FestivosAdoptados, FestivosNuevos, FestivosLocales;
    }

    // ------------------------------------------------------------------------------------------------- SMMLV y UVT --

    private static async Task ParametroAsync(IApplicationDbContext db, NationalValue copia, LoteReplicado lote, Conteo conteo,
        List<string> notas, List<AlertaALevantar> alertas, CancellationToken ct)
    {
        var desde = copia.ValidFrom.ToDateTime(TimeOnly.MinValue);
        var fuente = FuenteDe(copia.SourcePublicId);
        var versiones = await db.PayrollLegalParameters.Where(p => p.Code == copia.Code && !p.IsDeleted).ToListAsync(ct);
        versiones.AddRange(db.PayrollLegalParameters.Local.Where(p => p.Code == copia.Code && !p.IsDeleted && !versiones.Contains(p)));

        // Una corrección central reemplaza el valor de la proyección de la fila que corrige.
        if (copia.SupersedesSourcePublicId is { } corregida
            && versiones.FirstOrDefault(p => p.Source == FuenteDe(corregida)) is { } proyectada
            && proyectada.ValidFrom == desde)
        {
            proyectada.Value = copia.Value;
            proyectada.Source = fuente;
            proyectada.UpdatedAt = lote.Ahora;
            conteo.ParametrosCorregidos++;
            notas.Add($"{copia.Code} {copia.ValidFrom:yyyy-MM-dd} corregido a {Texto(copia.Value)}.");
            return;
        }

        var misma = versiones.FirstOrDefault(p => p.ValidFrom == desde);
        if (misma is not null)
        {
            if (misma.Value == copia.Value)
            {
                if (misma.Source != fuente)
                {
                    misma.Source = fuente;
                    misma.UpdatedAt = lote.Ahora;
                }
                conteo.ParametrosAdoptados++;
                return;
            }

            conteo.ParametrosDistintos++;
            notas.Add($"{copia.Code} {copia.ValidFrom:yyyy-MM-dd}: Nómina tiene {Texto(misma.Value)} y la norma {Texto(copia.Value)}; se conserva el de Nómina.");
            alertas.Add(new AlertaALevantar(
                AlertasDeValoresNacionales.ValorNacionalDistinto,
                $"{copia.Code} distinto del nacional",
                $"Desde el {copia.ValidFrom:yyyy-MM-dd} Nómina tiene {copia.Code} = {Texto(misma.Value)} y el valor nacional replicado ({copia.Source}) es "
                + $"{Texto(copia.Value)}. Se conserva el de Nómina hasta que el contador decida: si el nacional es el correcto, retire la vigencia de Nómina "
                + "y la réplica siguiente lo proyecta.",
                EntityType: nameof(NationalValue), EntityPublicId: copia.PublicId,
                DedupKey: AlertasDeValoresNacionales.ClaveDeDistinto(copia.Code, copia.ValidFrom)));
            return;
        }

        var anterior = versiones.Where(p => p.ValidFrom < desde).OrderByDescending(p => p.ValidFrom).FirstOrDefault();
        var siguiente = versiones.Where(p => p.ValidFrom > desde).OrderBy(p => p.ValidFrom).FirstOrDefault();
        if (anterior is not null && (anterior.ValidTo is null || anterior.ValidTo >= desde))
        {
            anterior.ValidTo = desde.AddDays(-1);
            anterior.UpdatedAt = lote.Ahora;
        }

        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = copia.Code,
            Name = anterior?.Name ?? siguiente?.Name ?? CodigosDeValorNacional.Buscar(copia.Code)?.Descripcion ?? copia.Code,
            Kind = LegalParameterKind.Amount,
            Value = copia.Value,
            ValidFrom = desde,
            ValidTo = siguiente?.ValidFrom.AddDays(-1),
            Source = fuente,
            CreatedAt = lote.Ahora,
            CreatedBy = "Réplica de valores nacionales",
        });
        conteo.ParametrosNuevos++;
    }

    // ----------------------------------------------------------------------------------------------------- festivos --

    private static async Task FestivoAsync(IApplicationDbContext db, NationalValue copia, LoteReplicado lote, Conteo conteo, List<string> notas, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(copia.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) return;

        var existente = db.Holidays.Local.FirstOrDefault(h => h.Date == fecha && !h.IsDeleted)
                        ?? await db.Holidays.FirstOrDefaultAsync(h => h.Date == fecha && !h.IsDeleted, ct);
        if (existente is null)
        {
            db.Holidays.Add(new Holiday
            {
                Date = fecha,
                Name = Recortar(copia.Label ?? "Festivo nacional", 80),
                Origin = HolidayOrigin.National,
                Year = (short)fecha.Year,
                NationalValuePublicId = copia.SourcePublicId,
                CreatedAt = lote.Ahora,
                CreatedBy = "Réplica de valores nacionales",
            });
            conteo.FestivosNuevos++;
            return;
        }

        if (existente.Origin is HolidayOrigin.Manual or HolidayOrigin.Decreed)
        {
            conteo.FestivosLocales++;
            notas.Add($"{fecha:yyyy-MM-dd} ya es festivo {existente.Origin} de Nómina («{existente.Name}»); no se toca.");
            return;
        }

        // Ley 51 sembrado antes de la réplica, o un nacional que trae una corrección: se marca como proyección.
        existente.Origin = HolidayOrigin.National;
        existente.NationalValuePublicId = copia.SourcePublicId;
        existente.UpdatedAt = lote.Ahora;
        conteo.FestivosAdoptados++;
    }

    private static string Texto(decimal? valor) => valor?.ToString("#,##0.##########", CultureInfo.GetCultureInfo("es-CO")) ?? "sin valor";

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}
