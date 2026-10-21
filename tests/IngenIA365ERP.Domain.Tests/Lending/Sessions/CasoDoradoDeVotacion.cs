using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Tests.Lending.Sessions;

/// <summary>
/// Un caso dorado de la votación de un acta (feature 013, T0240; T64, R133, F7, C75; quickstart §1.4): un archivo JSON en
/// <c>Casos/</c>, calculado a mano, que el secretario del consejo puede leer. Trae la regla sellada (fracciones exactas con su
/// comparación y la base de la mayoría), los miembros del órgano vigentes a la fecha de la sesión y uno o más escenarios con la
/// asistencia, el punto (regla, excluidos con motivo y votos) y lo que DEBE salir: quórum, elegibles, base, votos requeridos,
/// conteos, resultado, votos rechazados con su motivo y frases de la explicación. Molde de <c>CasoDoradoDeCosteo</c>.
///
/// <para>Las personas son enteros (en la base, <c>COR_People.Id</c>); los principales van del 101 en adelante y los suplentes del
/// 201. Una persona que no aparece en la asistencia no está presente; <c>presentes</c> es el atajo de una asistencia toda
/// <c>Present</c>.</para>
/// </summary>
public sealed class CasoDoradoDeVotacion
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ReglaJson Regla { get; set; } = new();
    public List<MiembroJson> Miembros { get; set; } = [];
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Sessions", "Casos");

    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json").OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeVotacion Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeVotacion>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    public ReglaDeVotacionSellada ReglaDelMotor() => new(
        new Fraccion(Regla.Quorum.Numerador, Regla.Quorum.Denominador), Regla.Quorum.Comparacion,
        new Fraccion(Regla.Mayoria.Numerador, Regla.Mayoria.Denominador), Regla.Mayoria.Comparacion, Regla.Mayoria.Base,
        Regla.Vinculados is { } v ? new Fraccion(v.Numerador, v.Denominador) : null);

    public IReadOnlyList<MiembroDelOrgano> MiembrosDelMotor() =>
        Miembros.Select(m => new MiembroDelOrgano(m.Persona, m.Principal, m.ConVoto, m.SuplenteDe)).ToList();

    public sealed class ReglaJson
    {
        public FraccionJson Quorum { get; set; } = new();
        public MayoriaJson Mayoria { get; set; } = new();
        public FraccionJson? Vinculados { get; set; }
    }

    public class FraccionJson
    {
        public int Numerador { get; set; }
        public int Denominador { get; set; }
        public FractionComparison Comparacion { get; set; } = FractionComparison.AtLeast;
    }

    public sealed class MayoriaJson : FraccionJson
    {
        public MajorityBase Base { get; set; } = MajorityBase.PresentEligible;
    }

    public sealed class MiembroJson
    {
        public int Persona { get; set; }
        public bool Principal { get; set; } = true;
        public bool ConVoto { get; set; } = true;
        public int? SuplenteDe { get; set; }
    }

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;
        public List<int>? Presentes { get; set; }
        public List<AsistenciaJson>? Asistencia { get; set; }
        public PuntoJson Punto { get; set; } = new();
        public EsperadoJson Esperado { get; set; } = new();

        public IReadOnlyList<AsistenciaASesion> AsistenciaDelMotor() =>
            (Asistencia ?? []).Select(a => new AsistenciaASesion(a.Persona, a.Estado))
                .Concat((Presentes ?? []).Select(p => new AsistenciaASesion(p, AttendanceStatus.Present)))
                .ToList();

        public PuntoAVotar PuntoDelMotor() => new(
            Punto.Regla,
            Punto.Excluidos.Select(e => new PersonaExcluida(e.Persona, e.Motivo)).ToList(),
            Punto.Votos.Select(v => new VotoRegistrado(v.Voto, v.Persona, v.Valor, v.Reemplaza)).ToList());
    }

    public sealed class AsistenciaJson
    {
        public int Persona { get; set; }
        public AttendanceStatus Estado { get; set; } = AttendanceStatus.Present;
    }

    public sealed class PuntoJson
    {
        public VotingRule Regla { get; set; } = VotingRule.Majority;
        public List<ExcluidoJson> Excluidos { get; set; } = [];
        public List<VotoJson> Votos { get; set; } = [];
    }

    public sealed class ExcluidoJson
    {
        public int Persona { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }

    public sealed class VotoJson
    {
        public int Voto { get; set; }
        public int Persona { get; set; }
        public VoteValue Valor { get; set; }
        public int? Reemplaza { get; set; }
    }

    public sealed class EsperadoJson
    {
        public QuorumJson Quorum { get; set; } = new();
        public int Elegibles { get; set; }
        public int Base { get; set; }
        public int Requeridos { get; set; }
        public int AFavor { get; set; }
        public int EnContra { get; set; }
        public int Abstenciones { get; set; }
        public ResultadoDeVotacion Resultado { get; set; }
        public List<RechazadoJson> Rechazados { get; set; } = [];
        public List<string> ExplicacionContiene { get; set; } = [];
    }

    public sealed class QuorumJson
    {
        public bool Cumple { get; set; }
        public int Composicion { get; set; }
        public int Requerido { get; set; }
        public int PresentesElegibles { get; set; }
    }

    public sealed class RechazadoJson
    {
        public int Persona { get; set; }
        public MotivoDeVotoRechazado Motivo { get; set; }
    }
}
