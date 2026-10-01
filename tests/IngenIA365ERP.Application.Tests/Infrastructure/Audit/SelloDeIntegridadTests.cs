using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Integrity;
using IngenIA365ERP.Audit.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace IngenIA365ERP.Application.Tests.Infrastructure.Audit;

/// <summary>
/// T011 (feature 012; decisiones-transversales T38, FR-008; quickstart §1.1 «Sello de integridad»): los casos
/// dorados de <see cref="SelloDeIntegridad"/>. El JSON canónico esperado se escribió a mano y los hashes y el
/// HMAC se calcularon fuera de .NET (Python, <c>hashlib</c>/<c>hmac</c>) sobre ese texto: si la
/// canonicalización cambia un solo byte, estos casos lo dicen, y ninguna cadena ya sellada se podría volver a
/// verificar.
/// </summary>
public class SelloDeIntegridadTests
{
    private static string Carpeta => Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Audit", "Casos");

    public static TheoryData<string> CasosDeEventos() =>
    [
        "01-claves-ordenadas.json",
        "02-instantes-utc.json",
        "03-decimales-invariantes.json",
        "04-cadena-de-tres.json",
    ];

    [Theory]
    [MemberData(nameof(CasosDeEventos))]
    public void El_canonico_y_el_hash_son_los_calculados_a_mano(string archivo)
    {
        var caso = Leer(archivo);
        var anterior = SelloDeIntegridad.HashInicial;

        EnCultura("es-CO", () =>
        {
            foreach (var evento in caso.GetProperty("eventos").EnumerateArray())
            {
                var campos = (Dictionary<string, object?>)Valor(evento.GetProperty("campos"))!;
                var canonico = SelloDeIntegridad.Canonico(campos);

                canonico.Should().Be(evento.GetProperty("canonico").GetString(), $"{archivo}: el JSON canónico es único");
                var hash = SelloDeIntegridad.Hash(anterior, canonico);
                hash.Should().Be(evento.GetProperty("hash").GetString(), $"{archivo}: SHA-256(PrevHash ‖ canónico)");
                anterior = hash;
            }
        });
    }

    [Fact]
    public void El_canonico_de_un_documento_de_Mongo_deja_fuera_solo_chain_hash()
    {
        var caso = Leer("04-cadena-de-tres.json").GetProperty("eventos")[2];
        var campos = (Dictionary<string, object?>)Valor(caso.GetProperty("campos"))!;
        var documento = ABson(campos);
        documento[SelloDeIntegridad.Cadena.Campo].AsBsonDocument[SelloDeIntegridad.Cadena.Hash] = caso.GetProperty("hash").GetString();

        SelloDeIntegridad.Canonico(documento).Should().Be(caso.GetProperty("canonico").GetString(),
            "leído de vuelta de Mongo (fechas a milisegundos, enteros de 32 o 64 bits) da el mismo texto que al sellar");
    }

    [Fact]
    public void Un_byte_cambiado_en_cualquier_campo_cambia_el_hash()
    {
        var caso = Leer("04-cadena-de-tres.json").GetProperty("eventos")[2];
        var original = (Dictionary<string, object?>)Valor(caso.GetProperty("campos"))!;
        var hashOriginal = SelloDeIntegridad.Hash(SelloDeIntegridad.HashInicial, SelloDeIntegridad.Canonico(original));

        foreach (var ruta in Rutas(original, ""))
        {
            var alterado = Copiar(original);
            Alterar(alterado, ruta.Split('/'));
            SelloDeIntegridad.Hash(SelloDeIntegridad.HashInicial, SelloDeIntegridad.Canonico(alterado))
                .Should().NotBe(hashOriginal, $"cambiar «{ruta}» tiene que notarse");
        }

        SelloDeIntegridad.Hash("1" + SelloDeIntegridad.HashInicial[1..], SelloDeIntegridad.Canonico(original))
            .Should().NotBe(hashOriginal, "el hash anterior también entra");
    }

    [Fact]
    public void El_HMAC_del_ancla_es_el_calculado_a_mano_con_su_version()
    {
        var ancla = Leer("05-ancla.json").GetProperty("ancla");
        var version = ancla.GetProperty("keyVersion").GetString()!;
        var sello = new SelloDeIntegridad(Firmas(version, ancla.GetProperty("claveBase64").GetString()!));
        var stream = ancla.GetProperty("stream").GetString()!;
        var seq = ancla.GetProperty("seq").GetInt64();
        var hash = ancla.GetProperty("hash").GetString()!;
        var anclada = DateTimeOffset.Parse(ancla.GetProperty("anchoredAt").GetString()!, CultureInfo.InvariantCulture).UtcDateTime;

        SelloDeIntegridad.CargaDeAncla(stream, seq, hash, anclada).Should().Be(ancla.GetProperty("carga").GetString());
        var (hmac, keyVersion) = sello.FirmarAncla(stream, seq, hash, anclada);
        hmac.Should().Be(ancla.GetProperty("hmac").GetString());
        keyVersion.Should().Be(version);

        sello.AnclaValida(stream, seq, hash, anclada, hmac, keyVersion).Should().BeTrue();
        sello.AnclaValida(stream, seq + 1, hash, anclada, hmac, keyVersion).Should().BeFalse("otra posición no es la firmada");
        sello.AnclaValida(stream, seq, hash, anclada, hmac, "otra-version").Should().BeFalse("una versión desconocida no valida nada");
    }

    [Fact]
    public void Sin_clave_de_anclaje_o_con_la_de_los_PDF_no_se_ancla()
    {
        var soloPdf = new AuditSignatureService(Options.Create(new AuditSignatureSettings
        {
            CurrentKeyVersion = "dev-v1",
            AnchorKeyVersion = "dev-v1",
            Keys = [new AuditSignatureKey { Version = "dev-v1", SecretBase64 = Convert.ToBase64String(new byte[32]) }],
        }));

        soloPdf.AnchorKeyVersion.Should().BeNull("pregunta A2: las anclas nunca se firman con dev-v1");
        var firmar = () => new SelloDeIntegridad(soloPdf).FirmarAncla("x:10y", 1, SelloDeIntegridad.HashInicial, DateTime.UtcNow);
        firmar.Should().Throw<InvalidOperationException>().WithMessage("*AnclaSinClave*");
    }

    // ------------------------------------------------------------ ayudantes --

    private static AuditSignatureService Firmas(string version, string claveBase64) => new(Options.Create(new AuditSignatureSettings
    {
        CurrentKeyVersion = "pdf-prueba",
        AnchorKeyVersion = version,
        Keys =
        [
            new AuditSignatureKey { Version = "pdf-prueba", SecretBase64 = Convert.ToBase64String(new byte[32]) },
            new AuditSignatureKey { Version = version, SecretBase64 = claveBase64 },
        ],
    }));

    private static JsonElement Leer(string archivo) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Carpeta, archivo))).RootElement.Clone();

    /// <summary><c>{"$fecha": …}</c> es un instante con zona, <c>{"$decimal": …}</c> un decimal; los números, enteros.</summary>
    private static object? Valor(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetInt64(),
        JsonValueKind.Array => e.EnumerateArray().Select(Valor).ToList(),
        JsonValueKind.Object when e.TryGetProperty("$fecha", out var f) =>
            DateTimeOffset.Parse(f.GetString()!, CultureInfo.InvariantCulture),
        JsonValueKind.Object when e.TryGetProperty("$decimal", out var d) =>
            decimal.Parse(d.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Valor(p.Value)),
        _ => throw new InvalidOperationException(e.ValueKind.ToString()),
    };

    private static BsonDocument ABson(Dictionary<string, object?> campos)
    {
        var doc = new BsonDocument();
        foreach (var (clave, valor) in campos)
        {
            doc[clave] = valor switch
            {
                null => BsonNull.Value,
                DateTimeOffset f => new BsonDateTime(f.UtcDateTime),
                long n when n is >= int.MinValue and <= int.MaxValue => new BsonInt32((int)n),
                Dictionary<string, object?> sub => ABson(sub),
                string s => new BsonString(s),
                _ => BsonValue.Create(valor),
            };
        }
        return doc;
    }

    private static IEnumerable<string> Rutas(Dictionary<string, object?> campos, string prefijo)
    {
        foreach (var (clave, valor) in campos)
        {
            if (valor is Dictionary<string, object?> sub)
            {
                foreach (var r in Rutas(sub, $"{prefijo}{clave}/")) yield return r;
            }
            else
            {
                yield return prefijo + clave;
            }
        }
    }

    private static Dictionary<string, object?> Copiar(Dictionary<string, object?> campos) =>
        campos.ToDictionary(p => p.Key, p => p.Value is Dictionary<string, object?> sub ? Copiar(sub) : p.Value);

    private static void Alterar(Dictionary<string, object?> campos, string[] ruta)
    {
        if (ruta.Length > 1)
        {
            Alterar((Dictionary<string, object?>)campos[ruta[0]]!, ruta[1..]);
            return;
        }
        campos[ruta[0]] = campos[ruta[0]] switch
        {
            string s when s.Length > 0 => (char)(s[0] ^ 1) + s[1..],
            long n => n + 1,
            DateTimeOffset f => f.AddMilliseconds(1),
            var otro => otro + "x",
        };
    }

    private static void EnCultura(string nombre, Action accion)
    {
        var antes = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(nombre);
        try { accion(); }
        finally { CultureInfo.CurrentCulture = antes; }
    }
}
