using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;

namespace IngenIA365ERP.API.IntegrationTests.NationalValues;

/// <summary>
/// T0039 (feature 013, fase 2; T37–T41, FR-015, C69, C83; contracts/api.md §21.3, §21.5), parte de plataforma, en el motor de
/// <c>DB_PROVIDER</c>: el maestro con segundo factor registra una usura, un IBR y un límite en el registro central; sin maestro la puerta no
/// abre; la réplica los copia en orden a dos cooperativas y repetirla no duplica; una cooperativa nueva nace con su copia; una corrección llega
/// con <c>SupersedesPublicId</c> y la consulta con <c>date</c> y <c>recordedAsOf</c> devuelve el valor anterior para un R viejo; una revisión
/// objetada deja el valor «objetado» y sin revisión queda «pendiente de validar» sin bloquear la lectura; <c>/missing</c> lista la usura del
/// mes siguiente sin registrar. La parte de la corrida <c>NationalValueCorrection</c> la agrega la fase de US4. (nuevo)
///
/// <para>Escrita el 2026-10-06 sin poder correrla (Docker no arranca en la máquina de desarrollo): compila; la corrida es T0098.</para>
/// </summary>
[Collection(ValoresNacionalesCollection.Nombre)]
public sealed class ValoresNacionalesYReplicaTests(CentralIdentityApiFixture fx)
{
    private const string Central = "/api/saas/national-values";
    private const string Copia = "/api/core/national-values";

    /// <summary>La clave de usura de la propuesta de E4 (catálogo de modalidades sembrado en la central).</summary>
    private const string Consumo = "CONSUMO_Y_ORDINARIO";

    [Fact]
    public async Task El_maestro_registra_la_replica_copia_en_orden_y_la_correccion_respeta_lo_que_se_sabia()
    {
        using var http = fx.CreateClient();
        var maestro = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var a = await ValoresNacionalesE2E.CooperativaAisladaAsync(fx, "replica_a");
        var b = await ValoresNacionalesE2E.CooperativaAisladaAsync(fx, "replica_b");
        var hoy = InventarioE2E.HoyEnColombia;
        var inicioDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finDelMes = inicioDelMes.AddMonths(1).AddDays(-1);

        // (1) Sin maestro, la puerta del registro central no abre (el administrador de una cooperativa no es el maestro).
        var intruso = await InventarioE2E.MandarAsync(http, a.TokenAdmin, HttpMethod.Post, Central, new
        {
            code = "IBR", key = "OVERNIGHT", validFrom = hoy, value = 0.091m, unit = "NominalMV", source = "Banco de la República",
        });
        ((int)intruso.StatusCode).Should().BeOneOf([401, 403, 404], $"sólo el maestro registra: «{await intruso.Content.ReadAsStringAsync()}»");

        // (2) El maestro registra la usura del mes, un IBR y un límite.
        var usura = await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = Consumo, validFrom = inicioDelMes, validTo = finDelMes, value = 0.2562m, unit = "EA",
            source = "Superintendencia Financiera, resolución del mes",
        });
        var ibr = await RegistrarAsync(http, maestro, new
        {
            code = "IBR", key = "OVERNIGHT", validFrom = hoy, referenceDate = hoy, value = 0.091m, unit = "NominalMV", source = "Banco de la República",
        });
        var limite = await RegistrarAsync(http, maestro, new
        {
            code = "LIMITE", key = "AVISO_PREVIO_DIAS", validFrom = new DateOnly(hoy.Year + 1, 1, 1), value = 20m, unit = "Dias",
            source = "Ley 1266 de 2008; Ley 2157 de 2021",
        });
        var centrales = (await InventarioE2E.ExitoAsync(http, maestro, HttpMethod.Get, $"{Central}?pageSize=500")).GetProperty("items").EnumerateArray().ToList();
        var secuencias = new[] { usura, ibr, limite }
            .Select(id => centrales.Single(v => v.GetProperty("publicId").GetGuid() == id).GetProperty("sequence").GetInt64()).ToList();
        secuencias.Should().BeInAscendingOrder().And.OnlyHaveUniqueItems("el contador común numera en el orden de registro");

        // Repetir el mismo (code, key, validFrom) vigente se rechaza.
        var repetido = await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, Central, new
        {
            code = "IBR", key = "OVERNIGHT", validFrom = hoy, referenceDate = hoy, value = 0.091m, unit = "NominalMV", source = "Banco de la República",
        });
        await InventarioE2E.FallaAsync(repetido, "NationalValues.Value.AlreadyRegistered");

        // (3) La réplica copia a dos cooperativas, en orden de Sequence, y repetirla no duplica.
        foreach (var coop in new[] { a, b })
        {
            var primera = await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
            primera.CentralDisponible.Should().BeTrue();
            primera.Valores.Should().BeGreaterThanOrEqualTo(3, "copia lo registrado después del alta");
            var segunda = await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
            segunda.Valores.Should().Be(0, "repetir la réplica no copia nada otra vez");
            var tercera = await ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
            tercera.Valores.Should().Be(0);

            var copia = await CopiaAsync(http, coop.TokenAdmin, "IBR", "OVERNIGHT");
            copia.Should().ContainSingle(v => v.GetProperty("sequence").GetInt64() == secuencias[1], "una sola copia del IBR aunque corrió tres veces");
            var orden = (await InventarioE2E.ExitoAsync(http, coop.TokenAdmin, HttpMethod.Get, $"{Copia}?pageSize=500")).GetProperty("items")
                .EnumerateArray().Select(v => v.GetProperty("sequence").GetInt64()).ToList();
            orden.Should().OnlyHaveUniqueItems("SourceSequence es único en la copia");
        }

        // (4) Una cooperativa nueva nace con su copia (la semilla del alta, Order 88), sin esperar a la tarea.
        var c = await ValoresNacionalesE2E.CooperativaAisladaAsync(fx, "replica_nueva");
        (await CopiaAsync(http, c.TokenAdmin, "USURA", Consumo)).Should().NotBeEmpty("la cooperativa nace con la usura vigente");
        (await CopiaAsync(http, c.TokenAdmin, "SMMLV", null)).Should().NotBeEmpty("y con el salario mínimo de la semilla central");
        (await ValoresNacionalesE2E.ReplicarAsync(fx, c.TenantPublicId)).Valores.Should().Be(0, "el alta ya copió todo");

        // (5) Una corrección llega con SupersedesPublicId; con un R anterior se ve el valor de antes (C83).
        var antesDeCorregir = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1));
        var correccion = await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, $"{Central}/{ibr}/corrections", new
        {
            value = 0.0915m, referenceDate = hoy, source = "Banco de la República, publicación corregida", reason = "Error de digitación en la tasa",
        });
        correccion.StatusCode.Should().Be(HttpStatusCode.Created, await correccion.Content.ReadAsStringAsync());
        var corregido = (await CarteraE2E.LeerAsync(correccion)).GetProperty("nationalValuePublicId").GetGuid();
        (await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, $"{Central}/{ibr}/corrections", new
        {
            value = 0.092m, source = "x", reason = "",
        })).IsSuccessStatusCode.Should().BeFalse("una corrección sin motivo no se acepta");

        (await ValoresNacionalesE2E.ReplicarAsync(fx, a.TenantPublicId)).Valores.Should().Be(1, "sólo la corrección");
        var copiaDeLaCorreccion = (await CopiaAsync(http, a.TokenAdmin, "IBR", "OVERNIGHT")).Single(v => v.GetProperty("value").GetDecimal() == 0.0915m);
        copiaDeLaCorreccion.GetProperty("supersedes").GetGuid().Should().Be(ibr, "la copia sabe a quién reemplaza");
        corregido.Should().NotBeEmpty();

        var hoyTexto = hoy.ToString("yyyy-MM-dd");
        var vigenteHoy = await CopiaAsync(http, a.TokenAdmin, "IBR", "OVERNIGHT", $"&date={hoyTexto}");
        vigenteHoy.Should().ContainSingle().Which.GetProperty("value").GetDecimal().Should().Be(0.0915m);
        var loQueSeSabia = await CopiaAsync(http, a.TokenAdmin, "IBR", "OVERNIGHT", $"&date={hoyTexto}&recordedAsOf={Uri.EscapeDataString(antesDeCorregir.ToString("o"))}");
        loQueSeSabia.Should().ContainSingle().Which.GetProperty("value").GetDecimal().Should().Be(0.091m,
            "con lo que se sabía antes de la corrección rige el valor anterior");

        // (6) Revisión: sin revisar queda «pendiente de validar» (0) y se lee igual; objetada (2) con nota.
        var usuraEnA = (await CopiaAsync(http, a.TokenAdmin, "USURA", Consumo)).Single();
        usuraEnA.GetProperty("status").GetInt32().Should().Be(0, "pendiente de validar, que no bloquea la lectura");
        var objecion = await InventarioE2E.MandarAsync(http, a.TokenAdmin, HttpMethod.Post, $"{Copia}/{usuraEnA.GetProperty("publicId").GetGuid()}/review",
            new { decision = "Objected", notes = "La tasa no coincide con la certificación publicada" });
        objecion.StatusCode.Should().Be(HttpStatusCode.Created, await objecion.Content.ReadAsStringAsync());
        var sinNota = await InventarioE2E.MandarAsync(http, a.TokenAdmin, HttpMethod.Post, $"{Copia}/{usuraEnA.GetProperty("publicId").GetGuid()}/review",
            new { decision = "Objected" });
        sinNota.IsSuccessStatusCode.Should().BeFalse("objetar exige la nota");
        (await CopiaAsync(http, a.TokenAdmin, "USURA", Consumo)).Single().GetProperty("status").GetInt32().Should().Be(2, "objetada");
        (await CopiaAsync(http, b.TokenAdmin, "USURA", Consumo)).Single().GetProperty("status").GetInt32().Should().Be(0,
            "la revisión es de cada cooperativa");

        // (7) La bitácora de réplicas registra sólo las pasadas que copiaron algo.
        var replicas = (await InventarioE2E.ExitoAsync(http, a.TokenAdmin, HttpMethod.Get, $"{Copia}/replications?pageSize=50")).GetProperty("items")
            .EnumerateArray().ToList();
        replicas.Should().OnlyContain(r => r.GetProperty("values").GetInt32() + r.GetProperty("documents").GetInt32() > 0,
            "una pasada vacía no deja fila");
    }

    [Fact]
    public async Task Los_faltantes_listan_la_usura_del_mes_siguiente_sin_registrar()
    {
        using var http = fx.CreateClient();
        var maestro = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var hoy = InventarioE2E.HoyEnColombia;
        var siguiente = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1);

        var faltantes = (await InventarioE2E.ExitoAsync(http, maestro, HttpMethod.Get, $"{Central}/missing?days=40")).EnumerateArray().ToList();
        // Sin la usura del mes en curso (si la otra prueba no corrió antes) la fecha es hoy; con ella, la del mes siguiente.
        faltantes.Should().Contain(f => f.GetProperty("code").GetString() == "USURA" && f.GetProperty("key").GetString() == Consumo
            && DateOnly.Parse(f.GetProperty("date").GetString()!) >= hoy && DateOnly.Parse(f.GetProperty("date").GetString()!) <= siguiente.AddDays(40),
            "la usura del mes siguiente no está registrada (C69)");
        faltantes.Should().NotContain(f => f.GetProperty("code").GetString() == "SMMLV", "la semilla central deja el salario mínimo del año");
    }

    // ------------------------------------------------------------------------------------------ ayudantes --

    private static async Task<Guid> RegistrarAsync(HttpClient http, string maestro, object cuerpo)
    {
        var resp = await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, Central, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar {JsonSerializer.Serialize(cuerpo)}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("nationalValuePublicId").GetGuid();
    }

    private static async Task<List<JsonElement>> CopiaAsync(HttpClient http, string token, string codigo, string? clave, string extra = "")
    {
        var url = $"{Copia}?code={codigo}{(clave is null ? "" : $"&key={clave}")}&pageSize=200{extra}";
        return (await InventarioE2E.ExitoAsync(http, token, HttpMethod.Get, url)).GetProperty("items").EnumerateArray().ToList();
    }
}
