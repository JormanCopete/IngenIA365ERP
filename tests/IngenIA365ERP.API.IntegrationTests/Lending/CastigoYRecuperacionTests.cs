using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.API.IntegrationTests.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.PagosE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0902 (feature 013, K3, US14; quickstart §5, pasos 8 y 9; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): el castigo por sus rutas
/// (<c>/api/lending/write-off-batches</c>) sobre una cooperativa aislada activada, con la contabilidad iniciada y la matriz contable completa por la
/// plantilla. Dos créditos desembolsados hoy y el reloj llevado a 30 días después de la fecha <b>exigible</b> de la primera cuota (art. 829,
/// <see cref="PagosE2E.ExigibleAsync"/>), con el ciclo diario corrido:
/// <list type="bullet">
/// <item>el lote se arma con los soportes de cada crédito (informe motivado, concepto de irrecuperabilidad y, mientras la consulta de aportes no está
/// disponible —D19—, el cruce de aportes): el deudor que ya no es asociado queda incluido y el que sigue siendo asociado, rechazado con el requisito
/// <c>NotAnAssociate</c>; agregarlo después responde <c>Lending.WriteOff.RequirementMissing</c> con <c>data.missing</c> (US14-1, C98);</item>
/// <item>enviado, el consejo lo aprueba en sesión con sus votos y el acta cerrada (punto <c>Decision</c>, sujeto <c>WriteOffBatch</c>); aplicarlo con
/// otro punto responde <c>Lending.Session.ItemMismatch</c> y con el suyo deja el crédito <c>WrittenOff</c>; su <c>CreditoCastigado</c> se contabiliza en
/// un <c>PV</c> (US14-2);</item>
/// <item>el tipo de entidad del perfil (cooperativa de ahorro y crédito) debe reportar los castigos: el ensayo publica una versión del documento
/// <c>TiposDeOrganizacion</c> con <c>writeOffReport</c> —la semilla no lo trae, lo aporta el dueño—, el lote sella el plazo y se levanta la alerta
/// <c>Cartera.PlazoRegulatorioPorVencer</c>; el reporte se registra una sola vez;</item>
/// <item>un pago del deudor por el recaudador de prueba sobre el castigado es una recuperación: emite <c>RecuperacionDeCastigada</c> y la vista
/// <c>charge-offs</c> lo muestra con el punto del acta (US14-3).</item>
/// </list>
/// La política del ensayo apaga <c>Castigo.ExigeDeterioroTotal</c>: el deterioro total pediría meses de mora y un cierre por cada uno (lo fijan
/// <c>RequisitosDeCastigoTests</c> y <c>LoteDeCastigoTests</c>). El retiro del asociado se escribe en la base: <b>ninguna ruta de Core apaga hoy
/// <c>IsAssociate</c></b> (el retiro llega con Ahorros y aportes, feature 014). (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CastigoYRecuperacionTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "castigo";
    private const string Lotes = "/api/lending/write-off-batches";

    [Fact]
    public async Task El_lote_de_castigo_se_arma_lo_aprueba_el_consejo_se_aplica_y_recupera()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var tenant = esc.Coop.TenantPublicId;
        var u = esc.Usuarios;
        var hoy = esc.Hoy;
        var despachador = new ConductorDelDespachador(fx);
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);
        await ActasE2E.ReporteDeCastigosObligatorioAsync(fx, http, tenant, hoy, dias: 30);

        // La política del ensayo: sin deterioro total (pediría meses de mora y sus cierres).
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/parameters/Castigo.ExigeDeterioroTotal/versions", new
        {
            scopeKind = 0, value = "false", validFrom = hoy, reason = "El ensayo castiga sin deterioro total (e2e)",
        });

        // Quien arma el lote sube los soportes con su plantilla cartera.jefe (Attachments.Upload desde el bloque D, decisión del dueño del 2026-10-15).

        var (retirado, _) = await DeudorAsync(http, admin, "Retirado");
        var aCastigar = await CierreE2E.DesembolsadoAsync(http, esc, retirado, 1_000_000m);
        var (vigente, _) = await DeudorAsync(http, admin, "Vigente");
        var deAsociado = await CierreE2E.DesembolsadoAsync(http, esc, vigente, 1_000_000m);
        var (socio, _) = await DeudorAsync(http, admin, "Socio");
        var deSocio = await CierreE2E.DesembolsadoAsync(http, esc, socio, 1_000_000m);
        // Bloque D: uno que entra al lote y, antes de aplicarlo, vuelve a ser asociado: se excluye al aplicar y el resto se castiga.
        var (reafiliado, _) = await DeudorAsync(http, admin, "Reafiliado");
        var deReafiliado = await CierreE2E.DesembolsadoAsync(http, esc, reafiliado, 1_000_000m);
        // El retiro del asociado: sin ruta en Core todavía (feature 014), se escribe en el maestro.
        await AsociadoAsync(fx, esc, retirado, false);
        await AsociadoAsync(fx, esc, reafiliado, false);

        var castigo = (await ExigibleAsync(http, admin, aCastigar)).AddDays(30);
        using (RelojEn(fx, castigo))
        {
            await CicloDeHoyAsync(fx, http, u.Jefe.Token, tenant);
            await despachador.PasadasAsync(tenant, 3);

            // ============================================================ el lote: incluido y rechazado (US14-1, C98) ==
            var soportes = new List<object>();
            foreach (var credito in new[] { aCastigar, deAsociado, deReafiliado })
                soportes.Add(new
                {
                    loanPublicId = credito,
                    irrecoverabilityAttachmentPublicId = await ActasE2E.SoporteAsync(http, u.Jefe.Token, credito, "LendingWriteOffSupport", "concepto.pdf"),
                    reportAttachmentPublicId = await ActasE2E.SoporteAsync(http, u.Jefe.Token, credito, "LendingWriteOffSupport", "informe.pdf"),
                    contributionsOffsetAttachmentPublicId = await ActasE2E.SoporteAsync(http, u.Jefe.Token, credito, "LendingWriteOffSupport", "aportes.pdf"),
                });
            var armado = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, Lotes, new
            {
                loanPublicIds = new[] { aCastigar, deAsociado, deReafiliado }, reason = "Deudores irrecuperables según el informe de cobranza (e2e)",
                reports = soportes,
            });
            armado.StatusCode.Should().Be(HttpStatusCode.Created, await armado.Content.ReadAsStringAsync());
            var lote = await CarteraE2E.LeerAsync(armado);
            var idLote = lote.GetProperty("publicId").GetGuid();
            lote.GetProperty("status").GetInt32().Should().Be((int)WriteOffBatchStatus.Draft);
            lote.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("loanPublicId").GetGuid())
                .Should().BeEquivalentTo([aCastigar, deReafiliado], "los que ya no son asociados cumplen");
            // Bloque D: la altura mínima de mora del castigo sigue pendiente de validar y armar el lote lo avisa, sin bloquear; el plazo del reporte sí
            // lo trae la norma del ensayo.
            var avisos = lote.GetProperty("warnings").EnumerateArray().Select(a => a.GetProperty("code").GetString()).ToList();
            avisos.Should().Contain("WriteOffMinimumArrearsPending").And.NotContain("WriteOffReportTermPending");
            var rechazado = lote.GetProperty("rejected").EnumerateArray().Should().ContainSingle().Subject;
            rechazado.GetProperty("loanPublicId").GetGuid().Should().Be(deAsociado);
            rechazado.GetProperty("missing").EnumerateArray().Select(m => m.GetString()).Should().Equal("NotAnAssociate");

            var repetido = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Lotes}/{idLote}/items", new { loanPublicId = deAsociado });
            (await CarteraE2E.CodigoDeErrorAsync(repetido)).Should().Be("Lending.WriteOffBatch.LoanAlreadyIncluded", "el rechazado ya está en el lote");
            var agregar = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Lotes}/{idLote}/items", new { loanPublicId = deSocio });
            agregar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await agregar.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(agregar)).Should().Be("Lending.WriteOff.RequirementMissing");
            (await CarteraE2E.LeerAsync(agregar)).GetProperty("data").GetProperty("missing").EnumerateArray().Select(m => m.GetString())
                .Should().Contain("NotAnAssociate");

            // ============================================================== el consejo lo aprueba con acta (US14-2) ==
            var enviado = await CarteraE2E.ConClaveAsync(http, u.Jefe.Token, HttpMethod.Post, $"{Lotes}/{idLote}/submit", null);
            enviado.StatusCode.Should().Be(HttpStatusCode.OK, await enviado.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(enviado)).GetProperty("status").GetInt32().Should().Be((int)WriteOffBatchStatus.Submitted);

            var punto = await ActasE2E.DecididoAsync(http, esc, esc.Originacion.Consejo, LendingDecisionSubjects.WriteOffBatch, idLote, castigo, "Decision");
            await AsociadoAsync(fx, esc, reafiliado, true);
            var aprobado = await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Lotes}/{idLote}");
            aprobado.GetProperty("status").GetInt32().Should().Be((int)WriteOffBatchStatus.Approved, aprobado.ToString());
            aprobado.GetProperty("sessionItem").GetProperty("itemPublicId").GetGuid().Should().Be(punto);
            aprobado.GetProperty("sessionItem").GetProperty("minutesNumber").GetString().Should().NotBeNullOrEmpty("el acta está cerrada y numerada");

            // Con otro punto no se aplica; con el suyo, sí (la secretaría del consejo aplica: quien armó no).
            var ajeno = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, $"{Lotes}/{idLote}/apply",
                new { sessionItemPublicId = (await CarteraE2E.ActivarCarteraAsync(fx, esc.Coop)).SessionItemPublicId });
            ajeno.IsSuccessStatusCode.Should().BeFalse(await ajeno.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(ajeno)).Should().Be("Lending.Session.ItemMismatch");
            var aplicar = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, $"{Lotes}/{idLote}/apply", new { sessionItemPublicId = punto });
            aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
            var aplicado = await CarteraE2E.LeerAsync(aplicar);
            aplicado.GetProperty("operations").EnumerateArray().Should().ContainSingle()
                .Which.GetProperty("loanPublicId").GetGuid().Should().Be(aCastigar);
            var excluido = aplicado.GetProperty("excluded").EnumerateArray().Should().ContainSingle().Subject;
            excluido.GetProperty("loanPublicId").GetGuid().Should().Be(deReafiliado, "volvió a ser asociado antes de aplicar el lote");
            excluido.GetProperty("missing").EnumerateArray().Select(m => m.GetString()).Should().Equal("NotAnAssociate");
            (await EstadoAsync(fx, esc, deReafiliado)).Should().Be(LoanStatus.Active, "el excluido no se castiga");
            aplicado.GetProperty("supervisorReportRequired").GetBoolean().Should().BeTrue("el tipo de entidad debe reportar los castigos");
            aplicado.GetProperty("supervisorReportDueDate").GetString().Should().Be(castigo.AddDays(30).ToString("yyyy-MM-dd"));
            (await EstadoAsync(fx, esc, aCastigar)).Should().Be(LoanStatus.WrittenOff);
            (await EstadoAsync(fx, esc, deAsociado)).Should().Be(LoanStatus.Active, "el rechazado no se toca");
            (await CarteraE2E.GetAsync(http, u.Jefe.Token, $"{Lotes}/{idLote}")).GetProperty("status").GetInt32().Should().Be((int)WriteOffBatchStatus.Applied);

            // CreditoCastigado llega a Contabilidad en un PV.
            var hecho = await ActasE2E.HechoProcesadoAsync(http, admin, despachador, tenant, aCastigar, "CreditoCastigado");
            (await ActasE2E.TipoDeComprobanteAsync(fx, esc, http, admin, hecho)).Should().Be("PV");

            // =================================================================== el plazo del reporte al supervisor ==
            var alertas = (await CarteraE2E.GetAsync(http, admin, "/api/lending/alerts?typeCode=Cartera.PlazoRegulatorioPorVencer&pageSize=50"))
                .GetProperty("items").EnumerateArray().ToList();
            alertas.Should().Contain(a => a.ToString().Contains(lote.GetProperty("number").GetString()!, StringComparison.Ordinal),
                "aplicar el lote levanta la alerta del plazo del reporte");
            var reporte = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, $"{Lotes}/{idLote}/supervisor-report",
                new { reference = "SES-2026-000123 (e2e)" });
            reporte.StatusCode.Should().Be(HttpStatusCode.OK, await reporte.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(reporte)).GetProperty("supervisorReportReference").GetString().Should().Be("SES-2026-000123 (e2e)");
            var otraVez = await CarteraE2E.ConClaveAsync(http, u.Secretaria.Token, HttpMethod.Post, $"{Lotes}/{idLote}/supervisor-report",
                new { reference = "SES-2026-000124 (e2e)" });
            otraVez.IsSuccessStatusCode.Should().BeFalse("el reporte se registra una sola vez");

            // ==================================================== la recuperación por el recaudador de prueba (US14-3) ==
            var recuperacion = castigo.AddDays(1);
            using (RelojEn(fx, recuperacion))
            {
                var pago = await AplicadoAsync(http, u, Pago(aCastigar, retirado, "FreeAmount", 20_000m, recuperacion));
                pago.GetProperty("operationPublicId").GetGuid().Should().NotBeEmpty();
                (await ActasE2E.HechosAsync(http, admin, aCastigar, "RecuperacionDeCastigada")).Should().NotBeEmpty("el pago de un castigado es recuperación");

                var vista = new ContabilidadE2E.Tabla(await CarteraE2E.GetAsync(http, u.Jefe.Token,
                    $"/api/reports/lending/charge-offs?from={castigo:yyyy-MM-dd}&to={recuperacion:yyyy-MM-dd}"));
                var fila = vista.Filas.Should().ContainSingle(f => ContabilidadE2E.Tabla.Celda(f, vista.IndicePorClave("_credito")).GetString() == aCastigar.ToString())
                    .Subject;
                ContabilidadE2E.Tabla.Celda(fila, vista.Indice("Recuperado")).GetDecimal().Should().Be(20_000m, "menos de lo vencido: todo es recuperación");
                ContabilidadE2E.Tabla.Celda(fila, vista.Indice("Punto de acta")).GetString().Should().StartWith("Acta ");
                ContabilidadE2E.Tabla.Celda(fila, vista.Indice("Saldo castigado")).GetDecimal().Should().BePositive();
            }
        }
    }

    /// <summary>La condición de asociado en el maestro de personas: sin ruta en Core hasta la feature 014, se escribe en la base.</summary>
    private static Task<int> AsociadoAsync(CentralIdentityApiFixture fx, Escenario esc, Guid persona, bool asociado) =>
        EnLaBaseAsync(fx, esc, async db =>
        {
            var p = await db.People.SingleAsync(x => x.PublicId == persona);
            p.IsAssociate = asociado;
            return await db.SaveChangesAsync(CancellationToken.None);
        });
}

/// <summary>
/// Los ayudantes de las e2e de US14 (feature 013, K3, T0902, T0903): la sesión de un órgano con todos sus miembros votando a favor y el acta cerrada,
/// los soportes por la subida directa, el documento <c>TiposDeOrganizacion</c> con el reporte de castigos, y los hechos contables de un crédito.
/// (nuevo)
/// </summary>
public static class ActasE2E
{
    /// <summary>Los miembros con voto vigentes del órgano: (persona, miembro).</summary>
    public static async Task<List<(Guid Persona, Guid Miembro)>> MiembrosAsync(HttpClient http, string token, Guid organo)
    {
        var detalle = await CarteraE2E.GetAsync(http, token, $"/api/lending/governing-bodies/{organo}");
        return detalle.GetProperty("memberList").EnumerateArray()
            .Where(m => m.GetProperty("votes").GetBoolean() && m.GetProperty("validTo").ValueKind == JsonValueKind.Null)
            .Select(m => (m.GetProperty("person").GetProperty("personPublicId").GetGuid(), m.GetProperty("memberPublicId").GetGuid()))
            .ToList();
    }

    /// <summary>
    /// Una sesión del órgano el día <paramref name="fecha"/> con todos sus miembros presentes, un punto <paramref name="clase"/> sobre el sujeto, el voto
    /// a favor de cada uno (lo registra la secretaría) y el acta cerrada con la huella revisada; devuelve el punto.
    /// </summary>
    public static async Task<Guid> DecididoAsync(HttpClient http, DesembolsoE2E.Escenario esc, Guid organo, string sujeto, Guid sujetoId, DateOnly fecha,
        string clase, Guid? recomiendaA = null)
    {
        var secretaria = esc.Usuarios.Secretaria.Token;
        var miembros = await MiembrosAsync(http, esc.Coop.TokenAdmin, organo);
        miembros.Should().NotBeEmpty($"el órgano {organo} tiene miembros con voto");
        var sesion = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, OriginacionE2E.Sesiones, new
        {
            governingBodyPublicId = organo, kind = "Extraordinary", mode = "InPerson", heldOn = fecha, location = "Sala de juntas",
        })).GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{OriginacionE2E.Sesiones}/{sesion}/attendance", new
        {
            attendees = miembros.Select(m => new { memberPublicId = m.Miembro, present = true }).ToList(),
        });
        var punto = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{OriginacionE2E.Sesiones}/{sesion}/items", new
        {
            subject = sujeto, subjectPublicId = sujetoId, kind = clase, recommendsToBodyPublicId = recomiendaA,
        })).GetProperty("itemPublicId").GetGuid();
        foreach (var (persona, _) in miembros)
        {
            var voto = await OriginacionE2E.VotarAsync(http, secretaria, sesion, punto, persona, "For", "RecordedBySecretary");
            voto.IsSuccessStatusCode.Should().BeTrue($"el voto de {persona}: «{await voto.Content.ReadAsStringAsync()}»");
        }
        var cerrada = await OriginacionE2E.CerrarAsync(http, secretaria, sesion);
        cerrada.IsSuccessStatusCode.Should().BeTrue($"cerrar el acta: «{await cerrada.Content.ReadAsStringAsync()}»");
        (await CarteraE2E.LeerAsync(cerrada)).GetProperty("status").GetInt32().Should().Be((int)SessionStatus.Closed);
        return punto;
    }

    /// <summary>Sube un soporte como el navegador (pedir, enviar al almacén, confirmar) y devuelve el adjunto.</summary>
    public static async Task<Guid> SoporteAsync(HttpClient http, string token, Guid dueno, string claseDeDueno, string nombre)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, dueno, contenido, nombre, ownerEntityType: claseDeDueno);
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");
        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, nombre);
        var url = subida.GetProperty("url").GetString()!;
        using var almacen = await http.PostAsync(Uri.TryCreate(url, UriKind.Absolute, out var destino) ? destino.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe el soporte: {(int)almacen.StatusCode}");
        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }

    /// <summary>
    /// La cooperativa de ahorro y crédito debe reportar los castigos al supervisor en <paramref name="dias"/> días: publica (con el maestro) una versión
    /// del documento <c>TiposDeOrganizacion</c> igual a la vigente más <c>writeOffReport</c> en ese tipo, vigente desde <paramref name="desde"/>, y la
    /// replica en la cooperativa. La semilla no trae el dato (lo aporta el dueño, T0910). Si ya lo trae, sólo replica.
    /// </summary>
    public static async Task ReporteDeCastigosObligatorioAsync(CentralIdentityApiFixture fx, HttpClient http, Guid tenant, DateOnly desde, int dias)
    {
        var maestro = await CarteraE2E.TokenMaestroAsync(fx, http);
        var versiones = (await CarteraE2E.GetAsync(http, maestro, "/api/saas/regulatory-documents?kind=TiposDeOrganizacion")).EnumerateArray()
            .Where(d => DateOnly.Parse(d.GetProperty("validFrom").GetString()!, System.Globalization.CultureInfo.InvariantCulture) <= desde)
            .ToList();
        versiones.Should().NotBeEmpty("la central trae el documento de tipos de organización");
        var vigente = versiones.OrderBy(d => d.GetProperty("validFrom").GetString(), StringComparer.Ordinal)
            .ThenBy(d => d.TryGetProperty("sequence", out var s) ? s.GetInt64() : 0L).Last();
        using var contenido = await CarteraE2E.MandarGetAsync(http, maestro, $"/api/saas/regulatory-documents/{vigente.GetProperty("publicId").GetGuid()}/content");
        contenido.StatusCode.Should().Be(HttpStatusCode.OK, await contenido.Content.ReadAsStringAsync());
        var documento = JsonNode.Parse(await contenido.Content.ReadAsStringAsync())!.AsObject();
        var tipo = documento["organizationTypes"]!.AsArray().Single(t => (string?)t!["code"] == "COOP_AHORRO_CREDITO")!.AsObject();
        if (tipo["writeOffReport"] is null)
        {
            tipo["writeOffReport"] = new JsonObject { ["required"] = true, ["dueDays"] = dias };
            var alta = await CarteraE2E.ConClaveAsync(http, maestro, HttpMethod.Post, "/api/saas/regulatory-documents", new
            {
                kind = "TiposDeOrganizacion", code = $"TIPOS-E2E-CASTIGO-{Guid.NewGuid():N}"[..30], validFrom = desde, schemaVersion = 1,
                content = documento.ToJsonString(), verified = false, source = "Plazo del reporte de castigos aportado por el dueño (e2e)",
            });
            alta.StatusCode.Should().Be(HttpStatusCode.Created, $"publicar el documento: «{await alta.Content.ReadAsStringAsync()}»");
        }
        await ValoresNacionalesE2E.ReplicarAsync(fx, tenant);
    }

    /// <summary>Los hechos contables de ese tipo del crédito (<c>GET /api/lending/accounting/facts</c>).</summary>
    public static async Task<List<JsonElement>> HechosAsync(HttpClient http, string token, Guid credito, string tipo) =>
        (await CarteraE2E.GetAsync(http, token, $"/api/lending/accounting/facts?type={tipo}&loanPublicId={credito}&pageSize=50"))
            .GetProperty("items").EnumerateArray().ToList();

    /// <summary>Pasadas del despachador hasta que el último hecho de ese tipo del crédito quede procesado; devuelve el hecho.</summary>
    public static async Task<JsonElement> HechoProcesadoAsync(HttpClient http, string token, ConductorDelDespachador despachador, Guid tenant, Guid credito,
        string tipo)
    {
        JsonElement hecho = default;
        for (var i = 0; i < 10; i++)
        {
            var lista = await HechosAsync(http, token, credito, tipo);
            lista.Should().NotBeEmpty($"el crédito emitió un «{tipo}»");
            hecho = lista[0];
            if (hecho.GetProperty("status").GetString() is not ("Pending" or "InBatch")) break;
            await despachador.PasadasAsync(tenant, 3);
        }
        if (hecho.GetProperty("status").GetString() != "Processed")
        {
            var detalle = await CarteraE2E.GetAsync(http, token, $"/api/lending/messages/{hecho.GetProperty("messagePublicId").GetGuid()}");
            throw new Xunit.Sdk.XunitException($"El hecho {tipo} no se contabilizó: {hecho} · {detalle}");
        }
        return hecho;
    }

    /// <summary>El tipo de comprobante del recibo contable del hecho.</summary>
    public static async Task<string> TipoDeComprobanteAsync(CentralIdentityApiFixture fx, DesembolsoE2E.Escenario esc, HttpClient http, string token,
        JsonElement hecho)
    {
        var mensaje = hecho.GetProperty("messagePublicId").GetGuid();
        var recibos = (await CarteraE2E.GetAsync(http, token, $"/api/accounting/lending/postings?message={mensaje}"))
            .GetProperty("items").EnumerateArray().Where(r => r.GetProperty("messagePublicId").GetGuid() == mensaje).ToList();
        recibos.Should().ContainSingle($"el hecho {mensaje} tiene un recibo");
        recibos[0].GetProperty("accountingDocumentPublicId").ValueKind.Should().Be(JsonValueKind.String, $"el recibo tiene comprobante: {recibos[0]} · {hecho}");
        var documento = recibos[0].GetProperty("accountingDocumentPublicId").GetGuid();
        return await DesembolsoE2E.EnLaBaseAsync(fx, esc, db => db.AccountingDocuments.AsNoTracking().Where(x => x.PublicId == documento)
            .Select(x => x.VoucherType!.Code).SingleAsync());
    }
}
