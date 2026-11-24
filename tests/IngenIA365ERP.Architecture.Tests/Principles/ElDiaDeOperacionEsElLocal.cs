using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// «Hoy» es el día de Colombia (<c>HoyLocal</c>), nunca el día UTC. Entre las 19:00 y la medianoche
/// <c>TodayUtc</c> ya dice mañana. Hasta el 2026-11-23 Contabilidad y Nómina medían «la fecha no puede
/// ser futura» con <c>TodayUtc</c>, así que un comprobante digitado a las ocho de la noche se podía fechar
/// al día siguiente. Cartera tenía su propia regla, <see cref="LaCarteraUsaLaFechaLocal"/>; ésta la
/// extiende a toda la aplicación y al dominio.
/// </summary>
public class ElDiaDeOperacionEsElLocal
{
    private static readonly string[] Carpetas = [
        "Core/IngenIA365ERP.Application",
        "Core/IngenIA365ERP.Domain",
    ];

    /// <summary>El reloj mismo declara <c>TodayUtc</c>; nadie más lo usa.</summary>
    private static readonly string[] Exentos = [
        "src/Core/IngenIA365ERP.Application/Common/Interfaces/IDateTimeService.cs",
    ];

    [Fact]
    public void Nadie_usa_la_fecha_UTC_como_hoy()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.Contains(archivos, a => a.EndsWith("AccountingPoster.cs", StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith("SettlementAccountingPoster.cs", StringComparison.Ordinal));
        var infracciones = ReglaSobreFuente.Infracciones(archivos,
            [(new Regex(@"\bTodayUtc\b", RegexOptions.Compiled), "usa la fecha UTC en vez de HoyLocal")],
            relativo => Exentos.Contains(relativo.Replace('\\', '/')));
        ReglaSobreFuente.SinInfracciones(infracciones, "«Hoy» se mide con la fecha de Colombia (HoyLocal), no con la UTC");
    }
}
