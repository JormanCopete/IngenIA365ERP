using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

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
        // La puerta del maestro de /api/saas responde 422 Saas.NotMasterAdmin (RequireMasterAdminAttribute), como el resto de la consola SaaS.
        ((int)intruso.StatusCode).Should().Be(422, $"sólo el maestro registra: «{await intruso.Content.ReadAsStringAsync()}»");
        (await InventarioE2E.CodigoDeErrorAsync(intruso)).Should().Be("Saas.NotMasterAdmin");

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

    /// <summary>
    /// T0482 (US4, T28, C03, C05; SC-002): el maestro corrige la usura con <c>SupersedesPublicId</c> después de causar; la réplica encola en la misma
    /// transacción la corrida <c>NationalValueCorrection</c>; la corrida deja un ajuste por día con su parte <c>AjusteDeCausacionPorFechaValor</c> y
    /// el libro vuelve a cuadrar; una liquidación con R anterior a la corrección no cambia. Usa la modalidad <c>PRODUCTIVO_RURAL</c>, que ningún otro
    /// ensayo de este host registra: la usura de consumo es de <see cref="El_maestro_registra_la_replica_copia_en_orden_y_la_correccion_respeta_lo_que_se_sabia"/>
    /// y la de microcrédito de <c>UsuraNuevaCierraLineasTests</c>. El saldo a favor (<c>CreditBalanceRecognized</c>) necesita un interés ya pagado
    /// que la corrección baje: llega con los pagos (US5); sin pagos la corrección sólo ajusta.
    /// </summary>
    [Fact]
    public async Task La_correccion_de_la_usura_despues_de_causar_ajusta_el_libro_y_no_cambia_lo_que_se_sabia()
    {
        const string modalidad = "PRODUCTIVO_RURAL";
        using var http = fx.CreateClient();
        var maestro = await ValoresNacionalesE2E.TokenMaestroAsync(fx, http);
        var hoy = CarteraE2E.Hoy(fx);
        var usura = await RegistrarAsync(http, maestro, new
        {
            code = "USURA", key = modalidad, validFrom = new DateOnly(hoy.Year, hoy.Month, 1), value = 0.60m, unit = "EA",
            source = "Superintendencia Financiera, certificación de prueba de la corrección (e2e)",
        });

        var esc = await DesembolsoE2E.EscenarioAsync(fx, "vn_correccion", modalidad);
        var tenant = esc.Coop.TenantPublicId;
        var jefe = esc.Usuarios.Jefe.Token;
        var (_, _, hecho) = await DesembolsoE2E.CreditoDesembolsadoAsync(http, esc, "Corregido", 2_000_000m);
        var credito = hecho.GetProperty("loanPublicId").GetGuid();

        // Causar cinco días a la tasa pactada (22 % EA, bajo la usura de 60 %).
        using (CausacionE2E.RelojEn(fx, hoy.AddDays(5)))
            await CausacionE2E.CicloDeHoyAsync(fx, http, jefe, tenant);
        (await CausacionE2E.MarcaAsync(fx, tenant, credito)).Should().Be(hoy.AddDays(5));
        var antes = await CausacionE2E.LiquidacionAsync(http, jefe, credito, hoy.AddDays(5));
        var loQueSeSabia = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromSeconds(1));

        // El maestro corrige la usura a 20 % (por debajo de la tasa pactada): la réplica encola la corrección.
        var correccion = await InventarioE2E.MandarAsync(http, maestro, HttpMethod.Post, $"{Central}/{usura}/corrections", new
        {
            value = 0.20m, source = "Superintendencia Financiera, certificación corregida (e2e)", reason = "La certificación publicada traía otra tasa",
        });
        correccion.StatusCode.Should().Be(HttpStatusCode.Created, await correccion.Content.ReadAsStringAsync());
        await ValoresNacionalesE2E.ReplicarAsync(fx, tenant);
        var pendientes = (await CarteraE2E.GetAsync(http, jefe, $"{CausacionE2E.Corridas}?kind=NationalValueCorrection")).GetProperty("items")
            .EnumerateArray().ToList();
        pendientes.Should().ContainSingle("la réplica encola una corrida por valor corregido (C05)");
        pendientes[0].GetProperty("status").GetString().Should().Be("Requested");
        pendientes[0].GetProperty("requestedBy").GetProperty("kind").GetString().Should().Be("Process");
        var corrida = pendientes[0].GetProperty("publicId").GetGuid();

        using (CausacionE2E.RelojEn(fx, hoy.AddDays(5)))
            await CausacionE2E.CorrerCorreccionAsync(fx, tenant, corrida, tamano: 1);

        (await CarteraE2E.GetAsync(http, jefe, $"{CausacionE2E.Corridas}/{corrida}")).GetProperty("run").GetProperty("status").GetString()
            .Should().Be("Completed");
        var filas = await CausacionE2E.CausacionAsync(fx, tenant, credito);
        var ajustes = filas.Where(f => f.Clase == LoanTransactionKind.AccrualAdjustment).ToList();
        ajustes.Should().NotBeEmpty("la causación al 22 % pasaba la usura corregida: cada día se ajusta (T28)");
        ajustes.Should().OnlyContain(f => f.Valor < 0m && f.Concepto == LoanConcept.CurrentInterest);
        ajustes.Select(f => f.Dia).Distinct().Should().BeEquivalentTo(Enumerable.Range(1, 5).Select(i => hoy.AddDays(i)), "un ajuste por día causado");
        var partes = await CausacionE2E.EnLaBaseAsync(fx, tenant, async db =>
        {
            var id = await db.LendingProcessRuns.Where(r => r.PublicId == corrida).Select(r => r.Id).SingleAsync();
            return await db.AccountingFactParts.AsNoTracking().Where(p => p.ProcessRunId == id).Select(p => new { p.FactType, p.SourceKind }).ToListAsync();
        });
        partes.Should().NotBeEmpty().And.OnlyContain(p => p.FactType == "AjusteDeCausacionPorFechaValor"
            && p.SourceKind == AccountingFactPartSource.NationalValueCorrectionBatch);

        // SC-002: con R anterior a la corrección la liquidación es la misma; con todo lo sabido, baja.
        using (CausacionE2E.RelojEn(fx, hoy.AddDays(5)))
        {
            var conR = await CausacionE2E.LiquidacionAsync(http, jefe, credito, hoy.AddDays(5), loQueSeSabia);
            conR.GetProperty("fingerprint").GetString().Should().Be(antes.GetProperty("fingerprint").GetString());
            conR.GetProperty("toPayOff").GetDecimal().Should().Be(antes.GetProperty("toPayOff").GetDecimal());
            var ahora = await CausacionE2E.LiquidacionAsync(http, jefe, credito, hoy.AddDays(5));
            ahora.GetProperty("toPayOff").GetDecimal().Should().BeLessThan(antes.GetProperty("toPayOff").GetDecimal(), "la usura corregida topa el interés");
            ahora.GetProperty("usuryCapped").GetBoolean().Should().BeTrue();
            var verificacion = await CarteraE2E.ExitoAsync(http, esc.Coop.TokenAdmin, HttpMethod.Post, CausacionE2E.Integridad, new { loanPublicIds = new[] { credito } });
            verificacion.GetProperty("mismatches").GetArrayLength().Should().Be(0, "después de la corrección el libro vuelve a cuadrar con la reconstrucción");
        }
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
