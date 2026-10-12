using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T9): todo Cartera usa la fecha local <c>America/Bogota</c> (<c>HoyLocal</c>); el motor
/// recibe las fechas por parámetro. Recorre el motor nuevo desde el Setup; la fase Fundacional B suma
/// <c>Application/Lending</c> cuando la cartera heredada se retira.
/// </summary>
public class LaCarteraUsaLaFechaLocal
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [
        "Core/IngenIA365ERP.Domain/Lending",
        // Fase Fundacional B (T0143): la Cartera nueva, ya sin la heredada.
        "Core/IngenIA365ERP.Application/Lending",
    ];

    /// <summary>El procesador de Cartera de la API (T0143).</summary>
    private static readonly string[] Archivos = [
        "Presentation/IngenIA365ERP.API/Integration/ProcesadorDeCartera.cs",
        "Presentation/IngenIA365ERP.API/Integration/OpcionesDelProcesoDeCartera.cs",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bTodayUtc\b", RegexOptions.Compiled), "usa la fecha UTC en vez de HoyLocal"),
        (new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", RegexOptions.Compiled), "lee el reloj directo"),
        (new Regex(@"\bTimeProvider\.System\b", RegexOptions.Compiled), "usa el reloj del sistema"),
    ];

    [Fact]
    public void La_regla_mira_la_ventana_legal_y_la_cola_de_mensajes()
    {
        // US17 (T0273): Domain/Lending/Collections y Application/Lending/Collections entran por sus carpetas (recursivas).
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Collections", "VentanaLegalDeContacto.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Collections", "ColaDeMensajesAlDeudor.cs"), StringComparison.Ordinal));
    }

    /// <summary>US1 (T0340): el motor de días, tasas, usura y plan trabaja en <c>DateOnly</c>; de <c>DateTime</c> sólo usa los días del mes.</summary>
    private static readonly string[] CarpetasDelMotor = [
        "Core/IngenIA365ERP.Domain/Lending/DayCount",
        "Core/IngenIA365ERP.Domain/Lending/Rates",
        "Core/IngenIA365ERP.Domain/Lending/Schedules",
    ];

    [Fact]
    public void El_motor_de_US1_recibe_las_fechas_y_trabaja_en_DateOnly()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(CarpetasDelMotor)
            .Concat(ReglaSobreFuente.ArchivosNombrados(["Core/IngenIA365ERP.Domain/Lending/MotorDeCartera.cs"])).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("DayCount", "ContadorDeDias.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Schedules", "GeneradorDePlan.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Rates", "ControlDeUsura.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith("MotorDeCartera.cs", StringComparison.Ordinal));
        var infracciones = ReglaSobreFuente.Infracciones(archivos, [
            (new Regex(@"\bDateTime(Offset)?\b(?!\.DaysInMonth\b)", RegexOptions.Compiled), "usa DateTime en el motor (las fechas de negocio son DateOnly)"),
            (new Regex(@"\b(IDateTimeService|TimeProvider|HoyLocal)\b", RegexOptions.Compiled), "consulta el reloj desde el motor"),
        ]);
        ReglaSobreFuente.SinInfracciones(infracciones, "El motor de Cartera no recibe sus fechas por parámetro (T9, T27)");
    }

    /// <summary>
    /// US4 (T0480): la causación y la mora también trabajan en <c>DateOnly</c> y reciben sus fechas. <c>Replay</c> no entra en esta lista: la
    /// reconstrucción recibe R (<c>RegisteredAt ≤ R</c>, T24) y R es un instante <c>DateTime</c> en UTC; el reloj tampoco lo consulta (lo
    /// vigila <see cref="La_reconstruccion_recibe_R_y_no_consulta_el_reloj"/>).
    /// </summary>
    private static readonly string[] CarpetasDelMotorDeUS4 = [
        "Core/IngenIA365ERP.Domain/Lending/Accrual",
        "Core/IngenIA365ERP.Domain/Lending/Delinquency",
        // US5 (T0533–T0535): la prelación, el aplicador de pagos y el tope del prepago reciben la fecha valor.
        "Core/IngenIA365ERP.Domain/Lending/Payments",
    ];

    [Fact]
    public void La_causacion_y_la_mora_trabajan_en_DateOnly()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(CarpetasDelMotorDeUS4).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Accrual", "Causacion.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Delinquency", "AlturaDeMora.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Payments", "AplicadorDePagos.cs"), StringComparison.Ordinal));
        var infracciones = ReglaSobreFuente.Infracciones(archivos, [
            (new Regex(@"\bDateTime(Offset)?\b(?!\.DaysInMonth\b)", RegexOptions.Compiled), "usa DateTime en la causación o la mora (las fechas de negocio son DateOnly)"),
            (new Regex(@"\b(IDateTimeService|TimeProvider|HoyLocal)\b", RegexOptions.Compiled), "consulta el reloj desde el motor"),
        ]);
        ReglaSobreFuente.SinInfracciones(infracciones, "La causación o la mora de Cartera no reciben sus fechas por parámetro (T9, T27)");
    }

    [Fact]
    public void La_reconstruccion_recibe_R_y_no_consulta_el_reloj()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(["Core/IngenIA365ERP.Domain/Lending/Replay"]).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Replay", "ReconstruccionDeCredito.cs"), StringComparison.Ordinal));
        var infracciones = ReglaSobreFuente.Infracciones(archivos, [
            (new Regex(@"\b(IDateTimeService|TimeProvider|HoyLocal|TodayUtc)\b", RegexOptions.Compiled), "consulta el reloj desde la reconstrucción"),
            (new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", RegexOptions.Compiled), "lee el reloj directo"),
        ]);
        ReglaSobreFuente.SinInfracciones(infracciones, "La reconstrucción de Cartera no recibe R por parámetro (T24)");
    }

    [Fact]
    public void Cartera_no_lee_el_reloj_por_su_cuenta()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas).Concat(ReglaSobreFuente.ArchivosNombrados(Archivos)), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera lee el reloj fuera de la fecha local (T9)");
    }
}
