using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Domain.Tests.Lending.Collections;

/// <summary>
/// Un caso dorado de la ventana legal de contacto (feature 013, T0269; T74, Ley 2300; <c>adaptadores-y-archivos.md</c> §4.5;
/// quickstart §1.4): un archivo JSON en <c>Casos/</c>, calculado a mano, que el abogado puede leer. Molde de los de Nómina y del
/// ejemplo de §4.5: las reglas (el techo nacional, que ya trae los días no hábiles nacionales), la restricción de la cooperativa si
/// la hay, el titular, el historial de la semana y la solicitud, con la decisión esperada. Un caso puede traer varios
/// <c>escenarios</c> que cambian el titular, el historial, la restricción de la cooperativa o la solicitud; si no trae, el caso
/// mismo es el único escenario.
///
/// <para>Los instantes son hora local de Colombia sin zona (<c>"2026-11-11T09:00"</c>). <c>festivosManualesDeNomina</c> es sólo
/// documentación: el cargador <b>no</b> se los pasa al motor, porque los festivos «Manual» de Nómina no cuentan (E5); el motor
/// recibe sólo los nacionales.</para>
/// </summary>
public sealed class CasoDoradoDeVentanaLegal
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Caso { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public ReglasJson Reglas { get; set; } = new();
    public ReglasJson? Cooperativa { get; set; }
    public List<DateOnly> FestivosManualesDeNomina { get; set; } = [];
    public TitularJson Titular { get; set; } = new();
    public List<ContactoJson> Historial { get; set; } = [];
    public SolicitudJson? Solicitud { get; set; }
    public EsperadoJson? Esperado { get; set; }
    public List<EscenarioJson> Escenarios { get; set; } = [];

    public static string DirectorioDeCasos => Path.Combine(AppContext.BaseDirectory, "Lending", "Collections", "Casos");

    /// <summary>Los casos de la ventana: los que empiezan con su número (los <c>reparto-*.json</c> de K4 son de otro motor).</summary>
    public static IEnumerable<string> Archivos() =>
        Directory.EnumerateFiles(DirectorioDeCasos, "*.json")
            .Where(f => char.IsAsciiDigit(Path.GetFileName(f)[0]))
            .OrderBy(f => f, StringComparer.Ordinal);

    public static CasoDoradoDeVentanaLegal Cargar(string ruta) =>
        JsonSerializer.Deserialize<CasoDoradoDeVentanaLegal>(File.ReadAllText(ruta), Opciones)
        ?? throw new InvalidOperationException($"El caso {ruta} está vacío.");

    /// <summary>Los escenarios a correr: los del caso, o el caso mismo si no trae.</summary>
    public IEnumerable<EscenarioJson> EscenariosACorrer()
    {
        if (Escenarios.Count > 0) return Escenarios;
        return Solicitud is { } s && Esperado is { } e
            ? [new EscenarioJson { Nombre = Caso, Solicitud = s, Esperado = e }]
            : throw new InvalidOperationException($"El caso {Caso} no trae solicitud ni escenarios.");
    }

    /// <summary>El techo nacional ∩ la restricción de la cooperativa (la del escenario, si la trae).</summary>
    public ReglasDeContacto ReglasDelMotor(EscenarioJson escenario)
    {
        var techo = Reglas.DelMotor();
        var cooperativa = escenario.Cooperativa ?? Cooperativa;
        return cooperativa is null ? techo : ReglasDeContacto.Combinar(techo, cooperativa.DelMotor());
    }

    public EstadoDelTitular TitularDelMotor(EscenarioJson escenario) => (escenario.Titular ?? Titular).DelMotor();

    /// <summary>
    /// K4 (T0938): los mensajes del historial y las gestiones pasan por <see cref="HistorialDeContactos.Componer"/>, que deja sólo las
    /// gestiones con contacto directo que nadie anuló.
    /// </summary>
    public HistorialDeContactos HistorialDelMotor(EscenarioJson escenario)
    {
        var historial = escenario.Historial ?? Historial;
        return HistorialDeContactos.Componer(
            historial.Where(c => c.Proposito is not null).Select(c => c.DelMotor()),
            historial.Where(c => c.Proposito is null).Select(c => c.Gestion()));
    }

    internal static DateTime Instante(string texto) =>
        DateTime.ParseExact(texto, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static TimeOnly Hora(string texto) => TimeOnly.ParseExact(texto, "HH:mm", CultureInfo.InvariantCulture);

    public sealed class ReglasJson
    {
        public HorarioJson Horario { get; set; } = new();
        public int ContactosPorDia { get; set; } = 1;
        public List<DateOnly> NoHabiles { get; set; } = [];

        public ReglasDeContacto DelMotor() => new(Franja(Horario.LunesAViernes), Franja(Horario.Sabado), ContactosPorDia, NoHabiles);

        private static FranjaHoraria? Franja(List<string>? par) =>
            par is { Count: 2 } ? new FranjaHoraria(Hora(par[0]), Hora(par[1])) : null;
    }

    public sealed class HorarioJson
    {
        public List<string>? LunesAViernes { get; set; }
        public List<string>? Sabado { get; set; }
    }

    public sealed class TitularJson
    {
        public ContactRole Rol { get; set; } = ContactRole.Debtor;
        public bool Obligado { get; set; } = true;
        public List<MessageChannel> CanalesAutorizados { get; set; } = [MessageChannel.Email, MessageChannel.Sms, MessageChannel.WhatsApp];
        public bool ConEvidencia { get; set; } = true;
        public bool AutorizacionGeneral { get; set; } = true;
        public bool AutorizacionGeneralConEvidencia { get; set; } = true;
        public List<MessageChannel> Destinos { get; set; } =
            [MessageChannel.Email, MessageChannel.Sms, MessageChannel.WhatsApp, MessageChannel.Letter];
        public bool DeudorFallecidoConReclamacion { get; set; }
        public bool Insolvencia { get; set; }
        public bool CobroSuspendido { get; set; }

        public EstadoDelTitular DelMotor() => new(Obligado, Rol)
        {
            Autorizaciones = CanalesAutorizados.Select(c => new AutorizacionDeCanal(c, ConEvidencia)).ToList(),
            AutorizacionGeneral = AutorizacionGeneral,
            AutorizacionGeneralConEvidencia = AutorizacionGeneralConEvidencia,
            Destinos = Destinos,
            DeudorFallecidoConReclamacion = DeudorFallecidoConReclamacion,
            EnInsolvencia = Insolvencia,
            CobroSuspendido = CobroSuspendido,
        };
    }

    /// <summary>
    /// Un contacto de la semana: con <c>proposito</c> es un mensaje enviado (<c>canal</c> es un <see cref="MessageChannel"/>); sin él,
    /// una gestión (K4; <c>canal</c> es un <see cref="CollectionActionChannel"/>, <c>directo</c> dice si hubo contacto directo, <c>id</c> la
    /// identifica y <c>anula</c> es el <c>id</c> de la gestión que corrige).
    /// </summary>
    public sealed class ContactoJson
    {
        public string En { get; set; } = string.Empty;
        public string Canal { get; set; } = string.Empty;
        public DebtorMessagePurpose? Proposito { get; set; }
        public bool Directo { get; set; }
        public int Id { get; set; }
        public int? Anula { get; set; }

        public GestionParaHistorial Gestion() =>
            new(Id, Instante(En), Enum.Parse<CollectionActionChannel>(Canal), Directo, Anula);

        public ContactoRegistrado DelMotor() => Proposito is { } p
            ? ContactoRegistrado.DeMensaje(Instante(En), Enum.Parse<MessageChannel>(Canal), p)
            : ContactoRegistrado.DeGestion(Instante(En), Enum.Parse<CollectionActionChannel>(Canal), Directo);
    }

    public sealed class SolicitudJson
    {
        public string Pedido { get; set; } = string.Empty;
        public string? Ahora { get; set; }
        public MessageChannel Canal { get; set; }
        public DebtorMessagePurpose Proposito { get; set; }
        public bool Inmediato { get; set; }

        public SolicitudDeContacto DelMotor() =>
            new(Instante(Pedido), Canal, Proposito, Inmediato) { Ahora = Ahora is null ? null : Instante(Ahora) };
    }

    public sealed class EscenarioJson
    {
        public string Nombre { get; set; } = string.Empty;
        public TitularJson? Titular { get; set; }
        public List<ContactoJson>? Historial { get; set; }
        public ReglasJson? Cooperativa { get; set; }
        public SolicitudJson Solicitud { get; set; } = new();
        public EsperadoJson Esperado { get; set; } = new();
    }

    public sealed class EsperadoJson
    {
        public string Decision { get; set; } = string.Empty;
        public string? En { get; set; }
        public string? Siguiente { get; set; }
        public List<ContactWindowReason> Motivos { get; set; } = [];
        public ContactWindowReason? Motivo { get; set; }
    }
}
