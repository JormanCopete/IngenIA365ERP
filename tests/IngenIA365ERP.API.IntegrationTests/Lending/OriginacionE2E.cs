using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Attachments;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using static IngenIA365ERP.API.IntegrationTests.Lending.LineasE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Los ayudantes de la originación (feature 013, US2, T0384, T0386): el escenario de una cooperativa aislada con sus usuarios (T0267), la línea
/// <c>ORIGEN</c> (consumo por caja al 22 % EA, 0,5 a 100 millones) y, si se pide, <c>CONDOC</c> (la misma con la cédula obligatoria), el
/// alcance de líneas de quien origina y decide, el canal <c>SIMULADO</c> de centrales (<c>DATACREDITO</c>), la política de datos, el comité
/// (comite.1..3) y el consejo (cinco principales, fracción de vinculados 4/5), la matriz del ensayo (analista 5, excepción, gerencia 20, comité
/// 100 millones y consejo), el patrimonio técnico de 1.000.000.000 en el doble de Contabilidad (<c>ContabilidadParaCarteraConDoble</c>; límite
/// individual 100 millones) y una lista restrictiva <c>PROPIA</c> con la persona E. Todo por HTTP salvo el doble. (nuevo)
/// </summary>
public static class OriginacionE2E
{
    public const string Simulaciones = "/api/lending/simulations";
    public const string Solicitudes = "/api/lending/applications";
    public const string Cupos = "/api/lending/credit-limits";
    public const string Centrales = "/api/lending/bureau-queries";
    public const string Listas = "/api/lending/screening-results";
    public const string Pendientes = "/api/lending/pending-decisions";
    public const string Organos = "/api/lending/governing-bodies";
    public const string Sesiones = "/api/lending/sessions";
    public const string Autorizaciones = "/api/compliance/habeas-data/authorizations";
    public const decimal PatrimonioTecnico = 1_000_000_000m;
    public const string Central = "DATACREDITO";

    /// <summary>El escenario de una cooperativa de originación.</summary>
    public sealed record Escenario(
        CarteraE2E.CooperativaAislada Coop,
        UsuariosDeCartera Usuarios,
        DateOnly Hoy,
        Guid Origen,
        Guid? ConDocumento,
        Guid Comite,
        IReadOnlyDictionary<Guid, Guid> MiembrosDelComite,
        Guid Consejo,
        IReadOnlyList<(Guid Persona, Guid Miembro)> Consejeros,
        Guid PersonaEnLista,
        Guid RolDeRadicacion)
    {
        /// <summary>
        /// La modalidad de las líneas del ensayo cuando no es la de consumo (US4, T0482): en el host «Valores nacionales e2e» la usura de consumo
        /// la registra <c>ValoresNacionalesYReplicaTests</c> y nadie más puede registrarla; nulo = consumo por caja.
        /// </summary>
        public string? Modalidad { get; init; }
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<Escenario>> Escenarios = [];
    private static readonly object Cerrojo = new();
    private static int _documento;

    /// <summary>
    /// Una cooperativa de originación por nombre y fixture; <paramref name="activada"/> la activa por <c>ActivarCarteraAsync</c> (C107). Con
    /// <paramref name="modalidad"/> las líneas son de esa modalidad (sin subsegmento) y el escenario no registra los valores de la central: quien
    /// lo pide registra antes la usura de esa modalidad (US4, T0482).
    /// </summary>
    public static Task<Escenario> EscenarioAsync(CentralIdentityApiFixture fx, string nombre, bool activada, string? modalidad = null)
    {
        lock (Cerrojo)
        {
            if (!Escenarios.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = PrepararAsync(fx, nombre, activada, modalidad);
                Escenarios[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Escenario> PrepararAsync(CentralIdentityApiFixture fx, string nombre, bool activada, string? modalidad)
    {
        var hoy = CarteraE2E.Hoy(fx);
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, nombre, primerEjercicioContable: hoy.Year);
        if (activada) await CarteraE2E.ActivarCarteraAsync(fx, coop);
        var usuarios = await UsuariosDeCartera.CrearAsync(fx, coop);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        await CarteraE2E.PersonaDelAdministradorAsync(http, coop);
        if (modalidad is null) await LineasE2E.PrepararAsync(fx, coop);
        else await NationalValues.ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        fx.Dobles.PatrimonioTecnico(coop.TenantPublicId, PatrimonioTecnico, hoy.AddDays(-1));

        // Las líneas: ORIGEN sin documentos y, en la activada, CONDOC con la cédula obligatoria.
        var origen = await CrearExitoAsync(http, admin, "ORIGEN", "Consumo para la originación", VersionDeOriginacion(hoy, conDocumento: false, modalidad));
        Guid? conDocumento = activada
            ? await CrearExitoAsync(http, admin, "CONDOC", "Consumo con documentos", VersionDeOriginacion(hoy, conDocumento: true, modalidad))
            : null;
        var lineas = conDocumento is { } d ? new[] { origen, d } : [origen];
        foreach (var u in new[] { usuarios.AsesorO1, usuarios.AsesorO2, usuarios.Analista, usuarios.Comite1, usuarios.SinPersona })
            await UsuariosDeCartera.FijarLineasAsync(http, admin, u, lineas);

        // Perfil con actividad financiera (la fracción de vinculados 4/5 manda), canal de centrales y política de datos.
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/core/regulatory-profile/versions", new
        {
            organizationType = "COOP_AHORRO_CREDITO", financialActivity = true, supervisionLevel = 2, validFrom = hoy,
            source = "Certificado de la Supersolidaria (e2e)", reason = "Cooperativa con actividad financiera para la originación",
        });
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/core/external-channels", new
        {
            kind = "CreditBureau", bureauCode = Central, channelCode = "SIMULADO", validFrom = hoy, reason = "Central simulada para la originación (e2e)",
        });
        foreach (var finalidad in new[] { "GeneralTreatment", "CreditProcessing", "CreditBureauQuery" })
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/compliance/habeas-data/policies/", new
            {
                title = $"Política de datos: {finalidad}", contentMarkdown = "Texto de la política para las e2e de originación.", effectiveFrom = DateTime.UtcNow,
                purpose = finalidad,
            });

        // Órganos: el comité con comite.1..3 y el consejo con cinco principales (personas del maestro), fracción de vinculados 4/5.
        var comite = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Organos, new
        {
            code = "COMITE", name = "Comité de crédito", kind = "CreditCommittee", decidesCredit = true, minutesPrefix = "CC-", rules = Reglas(hoy, null),
        })).GetProperty("publicId").GetGuid();
        var miembros = new Dictionary<Guid, Guid>();
        foreach (var u in new[] { usuarios.Comite1, usuarios.Comite2, usuarios.Comite3 })
            miembros[u.Persona!.Value] = CarteraE2E.MiembroDe(await NombrarAsync(http, admin, comite, u.Persona!.Value, hoy));
        // Con la activación ya hay un consejo (el del acta de activación): se reusa; si no, el del ensayo con cinco principales.
        var organos = (await CarteraE2E.GetAsync(http, admin, Organos)).EnumerateArray().ToList();
        var existente = organos.FirstOrDefault(o => o.GetProperty("kind").GetInt32() == (int)IngenIA365ERP.Domain.Enums.Lending.GoverningBodyKind.BoardOfDirectors
            && o.GetProperty("isActive").GetBoolean());
        var consejeros = new List<(Guid, Guid)>();
        Guid consejo;
        if (existente.ValueKind == JsonValueKind.Object) consejo = existente.GetProperty("publicId").GetGuid();
        else
        {
            consejo = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, Organos, new
            {
                code = "CONSEJO", name = "Consejo de administración", kind = "BoardOfDirectors", decidesCredit = true, minutesPrefix = "CA-",
                rules = Reglas(hoy, (4, 5)),
            })).GetProperty("publicId").GetGuid();
            for (var i = 1; i <= 5; i++)
            {
                var persona = await AsociadoAsync(http, admin, $"Consejero{(char)('A' + i - 1)}");
                consejeros.Add((persona, CarteraE2E.MiembroDe(await NombrarAsync(http, admin, consejo, persona, hoy))));
            }
        }

        // La matriz del ensayo (quickstart §3.5).
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/attribution-matrices", new
        {
            validFrom = hoy, reason = "Reglamento de crédito del ensayo", source = "Acuerdo 12 del consejo (e2e)",
            levels = new object[]
            {
                new { code = "ANALISTA", name = "Analista", order = 10, kind = "Individual", permissionCode = "Lending.Decisions.Analyst", consolidatedAmountCeiling = 5_000_000m },
                new { code = "EXCEP", name = "Excepción", order = 15, kind = "Individual", permissionCode = "Lending.Decisions.Exceptions", consolidatedAmountCeiling = 5_000_000m, isExceptionLevel = true },
                new { code = "GERENCIA", name = "Gerencia", order = 20, kind = "Individual", permissionCode = "Lending.Decisions.Management", consolidatedAmountCeiling = 20_000_000m },
                new { code = "COMITE", name = "Comité de crédito", order = 30, kind = "Collegiate", governingBodyPublicId = comite, consolidatedAmountCeiling = 100_000_000m },
                new { code = "CONSEJO", name = "Consejo de administración", order = 40, kind = "Collegiate", governingBodyPublicId = consejo, isBoardLevel = true },
            },
            rules = Array.Empty<object>(),
        });

        // La lista propia con la persona E (la persona se crea antes para poner su documento).
        var (enLista, documentoEnLista) = await AsociadoConDocumentoAsync(http, admin, "Eugenia");
        var carga = await ImportarListaAsync(http, admin, LibroDeListas(hoy, (documentoEnLista, "EUGENIA ASOCIADO")), "apply");
        carga.StatusCode.Should().Be(HttpStatusCode.OK, $"cargar la lista propia: «{await carga.Content.ReadAsStringAsync()}»");

        // Un rol de radicación (crear, radicar, simular y asignar cupos con monto operativo de 1.000.000) para quien el ensayo quiera.
        var radicacion = await RolPropioAsync(http, admin, "ENS-ORIG-RADICA", "Lending.Applications.Read", "Lending.Applications.Create",
            "Lending.Applications.Submit", "Lending.Simulations.Run", "Lending.CreditLimits.Read", "Lending.CreditLimits.Assign");
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/amount-limits", new
        {
            rolePublicId = radicacion, permissionCode = "Lending.CreditLimits.Assign", maxAmount = 1_000_000m, validFrom = hoy,
            reason = "Monto operativo de cupos de quien radica en el ensayo",
        });

        return new Escenario(coop, usuarios, hoy, origen, conDocumento, comite, miembros, consejo, consejeros, enLista, radicacion) { Modalidad = modalidad };
    }

    /// <summary>La versión de las líneas del ensayo: consumo por caja al 22 % EA, de 500.000 a 100.000.000 y de 1 a 60 meses.</summary>
    /// <remarks>Con <paramref name="modalidad"/>, de esa modalidad y sin subsegmento (US4, T0482).</remarks>
    public static Dictionary<string, object?> VersionDeOriginacion(DateOnly desde, bool conDocumento, string? modalidad = null)
    {
        var v = VersionFija(desde, modalidad ?? "CONSUMO", modalidad is null ? "SinLibranza" : null, 0.22m, "Versión de la originación (e2e)");
        v["amounts"] = new { min = 500_000m, max = 100_000_000m };
        if (conDocumento) v["documents"] = new[] { new { code = "CEDULA", name = "Copia de la cédula", mandatory = true } };
        return v;
    }

    // ------------------------------------------------------------------------------------------------ personas --

    /// <summary>Un asociado nuevo con su autorización de estudio del crédito y de consulta a centrales (con evidencia); devuelve su persona.</summary>
    public static async Task<Guid> DeudorAsync(HttpClient http, string admin, string nombre, bool conCentrales = true)
    {
        var persona = await AsociadoAsync(http, admin, nombre);
        await AutorizarAsync(http, admin, persona, "CreditProcessing");
        if (conCentrales) await AutorizarAsync(http, admin, persona, "CreditBureauQuery");
        return persona;
    }

    public static async Task<Guid> AsociadoAsync(HttpClient http, string admin, string nombre) => (await AsociadoConDocumentoAsync(http, admin, nombre)).Persona;

    /// <summary>
    /// Un asociado (persona y afiliación en un paso). El documento termina en 0: la central simulada responde «sin marcas» por el último dígito.
    /// </summary>
    public static async Task<(Guid Persona, string Documento)> AsociadoConDocumentoAsync(HttpClient http, string admin, string nombre)
    {
        var documento = (7_700_000_000L + Interlocked.Increment(ref _documento) * 10L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/associates/with-person", new
        {
            person = new { idType = "C", taxId = documento, firstName = nombre, lastName = "Asociado", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.originacion.test" },
            associate = new { joinDate = new DateOnly(2024, 1, 15), contributionRate = 5m },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"asociado {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        return ((await CarteraE2E.LeerAsync(alta)).GetProperty("personPublicId").GetGuid(), documento);
    }

    /// <summary>Registra una autorización por finalidad con su evidencia (dueño: la persona).</summary>
    public static async Task AutorizarAsync(HttpClient http, string admin, Guid persona, string finalidad)
    {
        var evidencia = await SubirEvidenciaAsync(http, admin, persona);
        var resp = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Autorizaciones, new
        {
            personPublicId = persona, purpose = finalidad, evidenceAttachmentPublicId = evidencia, grantedAt = DateTime.UtcNow,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"autorización {finalidad}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Sube una evidencia <c>DataAuthorization</c> como el navegador: pedir, enviar al almacén local, confirmar.</summary>
    public static async Task<Guid> SubirEvidenciaAsync(HttpClient http, string token, Guid persona)
    {
        var contenido = AdjuntosE2E.Pdf(4_096);
        using var pedido = await AdjuntosE2E.PedirSubidaAsync(http, token, persona, contenido, "autorizacion-firmada.pdf", ownerEntityType: "DataAuthorization");
        pedido.StatusCode.Should().Be(HttpStatusCode.Created, await pedido.Content.ReadAsStringAsync());
        var autorizacion = await CarteraE2E.LeerAsync(pedido);
        var adjunto = autorizacion.GetProperty("attachmentPublicId").GetGuid();
        var subida = autorizacion.GetProperty("upload");

        using var formulario = new MultipartFormDataContent();
        foreach (var campo in subida.GetProperty("fields").EnumerateObject())
            formulario.Add(new StringContent(campo.Value.GetString()!), campo.Name);
        formulario.Add(new ByteArrayContent(contenido), subida.GetProperty("fileField").GetString()!, "autorizacion-firmada.pdf");
        var url = subida.GetProperty("url").GetString()!;
        var absoluta = Uri.TryCreate(url, UriKind.Absolute, out var destino);
        using var almacen = await http.PostAsync(absoluta ? destino!.PathAndQuery : url, formulario);
        almacen.IsSuccessStatusCode.Should().BeTrue($"el almacén recibe la evidencia: {(int)almacen.StatusCode}");

        using var confirmacion = await CarteraE2E.EnviarAsync(http, token, HttpMethod.Post, $"/api/attachments/{adjunto}/confirm", null);
        confirmacion.StatusCode.Should().Be(HttpStatusCode.OK, await confirmacion.Content.ReadAsStringAsync());
        return adjunto;
    }

    // ------------------------------------------------------------------------------------------ originación --

    /// <summary>El cuerpo de una solicitud de <paramref name="persona"/> en la oficina O1 con la capacidad declarada holgada.</summary>
    public static object Solicitud(Escenario esc, Guid persona, decimal monto, Guid? linea = null) => new
    {
        applicantPersonPublicId = persona, officePublicId = esc.Usuarios.O1.AgenciaPublicId, creditLinePublicId = linea ?? esc.Origen,
        purpose = "Libre inversión (e2e)", amount = monto, installments = 36, periodicity = "Monthly", amortizationSystem = "LevelPayment",
        paymentForm = "Cashier", prepaymentPreference = "ReduceTerm",
        applicantCapacity = new { monthlyIncome = 40_000_000m, monthlyExpenses = 2_000_000m, externalInstallments = 0m },
    };

    /// <summary>Crea el borrador y lo radica con <paramref name="token"/>; devuelve el DTO radicado.</summary>
    public static async Task<JsonElement> RadicarAsync(HttpClient http, string token, object cuerpo)
    {
        var borrador = await CarteraE2E.ExitoAsync(http, token, HttpMethod.Post, Solicitudes, cuerpo);
        var id = borrador.GetProperty("publicId").GetGuid();
        var radicada = await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Solicitudes}/{id}/submit", new { rowVersion = borrador.GetProperty("rowVersion").GetString() });
        var texto = await radicada.Content.ReadAsStringAsync();
        radicada.StatusCode.Should().Be(HttpStatusCode.OK, $"radicar: «{texto}»");
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    public static async Task<(string Nivel, string Huella)> EnrutamientoAsync(HttpClient http, string token, Guid solicitud)
    {
        var r = await CarteraE2E.GetAsync(http, token, $"{Solicitudes}/{solicitud}/routing");
        return (r.GetProperty("effectiveLevelCode").GetString()!, r.GetProperty("contentSha256").GetString()!);
    }

    public static Task<HttpResponseMessage> DecidirAsync(HttpClient http, string token, string sujeto, Guid id, string resultado, string? huella,
        string? motivo = null, object? condiciones = null) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"/api/lending/{sujeto}/{id}/decisions", new
        {
            outcome = resultado, reason = motivo, conditions = condiciones, expectedContentSha256 = huella,
        });

    /// <summary>Otorga un cupo consolidado asignado por <paramref name="monto"/>.</summary>
    public static Task<HttpResponseMessage> OtorgarCupoAsync(HttpClient http, string token, Guid persona, decimal monto, DateOnly desde,
        string clase = "Consolidated") =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, Cupos, new
        {
            personPublicId = persona, kind = clase, origin = "Assigned", amount = monto, validFrom = desde,
            prepaymentPreference = clase == "Revolving" ? "ReduceTerm" : null, reason = "Cupo del ensayo de originación",
        });

    // ---------------------------------------------------------------------------------------------- sesiones --

    /// <summary>Una sesión de hoy del órgano con todos sus miembros presentes y un punto <c>Decision</c> sobre el sujeto.</summary>
    public static async Task<(Guid Sesion, Guid Punto)> SesionConPuntoAsync(HttpClient http, Escenario esc, Guid organo, IEnumerable<Guid> miembros,
        string sujeto, Guid sujetoId)
    {
        var secretaria = esc.Usuarios.Secretaria.Token;
        var sesion = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, Sesiones, new
        {
            governingBodyPublicId = organo, kind = "Extraordinary", mode = "InPerson", heldOn = esc.Hoy, location = "Sala de juntas",
        })).GetProperty("publicId").GetGuid();
        await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{Sesiones}/{sesion}/attendance", new
        {
            attendees = miembros.Select(m => new { memberPublicId = m, present = true }).ToList(),
        });
        var punto = (await CarteraE2E.ExitoAsync(http, secretaria, HttpMethod.Post, $"{Sesiones}/{sesion}/items", new
        {
            subject = sujeto, subjectPublicId = sujetoId, kind = "Decision",
        })).GetProperty("itemPublicId").GetGuid();
        return (sesion, punto);
    }

    public static Task<HttpResponseMessage> VotarAsync(HttpClient http, string token, Guid sesion, Guid punto, Guid persona, string voto, string metodo) =>
        CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Sesiones}/{sesion}/items/{punto}/votes", new
        {
            personPublicId = persona, vote = voto, method = metodo,
        });

    public static async Task<HttpResponseMessage> CerrarAsync(HttpClient http, string token, Guid sesion)
    {
        var huella = (await CarteraE2E.GetAsync(http, token, $"{Sesiones}/{sesion}")).GetProperty("currentContentSha256").GetString();
        return await CarteraE2E.ConClaveAsync(http, token, HttpMethod.Post, $"{Sesiones}/{sesion}/close", new { expectedContentSha256 = huella });
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    public static async Task<Guid> RolPropioAsync(HttpClient http, string admin, string codigo, params string[] permisos)
    {
        var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/admin/roles", new
        {
            code = codigo, name = $"Ensayo {codigo}", description = "Ajuste del ensayo de originación", permissionPublicIds = permisos.Select(c => catalogo[c]).ToList(),
        });
        alta.IsSuccessStatusCode.Should().BeTrue($"rol {codigo}: «{await alta.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.GetAsync(http, admin, $"/api/admin/roles?search={Uri.EscapeDataString(codigo)}&includeBuiltIn=false&pageSize=50"))
            .GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
    }

    /// <summary>Le da un rol a un usuario del ensayo (si ya lo tiene, sigue).</summary>
    public static async Task DarRolAsync(HttpClient http, string admin, UsuariosDeCartera.Usuario usuario, Guid rol)
    {
        var resp = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario.PublicId}/roles", new { rolePublicId = rol });
        if (resp.IsSuccessStatusCode || await CarteraE2E.CodigoDeErrorAsync(resp) == "Security.Users.AlreadyAssignedRole") return;
        resp.IsSuccessStatusCode.Should().BeTrue(await resp.Content.ReadAsStringAsync());
    }

    private static Task<JsonElement> NombrarAsync(HttpClient http, string admin, Guid organo, Guid persona, DateOnly desde) =>
        CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{Organos}/{organo}/members", new
        {
            personPublicId = persona, position = "Member", votes = true, validFrom = desde, appointmentNotes = "Nombramiento del ensayo de originación",
        });

    private static object Reglas(DateOnly desde, (int N, int D)? vinculados) => new
    {
        quorum = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
        ordinaryMajority = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
        relatedPartyFraction = vinculados is { } v ? new { numerator = v.N, denominator = v.D, comparison = "AtLeast" } : null,
        validFrom = desde, reason = "Reglas de los estatutos para la originación", source = "Estatutos de la cooperativa de prueba, art. 40",
    };

    /// <summary>El libro de la plantilla <c>compliance.restrictive-lists</c> con una lista PROPIA.</summary>
    public static byte[] LibroDeListas(DateOnly vigente, params (string Documento, string Nombre)[] entradas)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Datos");
        string[] encabezados = ["lista", "fuente", "publicadaEl", "vigenteDesde", "tipoDeEntidad", "tipoDeDocumento", "numeroDeDocumento", "nombreCompleto",
            "alias", "programa", "notas"];
        for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
        for (var f = 0; f < entradas.Length; f++)
        {
            var fila = f + 2;
            hoja.Cell(fila, 1).Value = "PROPIA";
            hoja.Cell(fila, 2).Value = "Lista interna del oficial de cumplimiento";
            hoja.Cell(fila, 3).Value = vigente.ToString("yyyy-MM-dd");
            hoja.Cell(fila, 4).Value = vigente.ToString("yyyy-MM-dd");
            hoja.Cell(fila, 5).Value = "Person";
            hoja.Cell(fila, 6).Value = "CC";
            hoja.Cell(fila, 7).Value = entradas[f].Documento;
            hoja.Cell(fila, 8).Value = entradas[f].Nombre;
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    public static Task<HttpResponseMessage> ImportarListaAsync(HttpClient http, string token, byte[] contenido, string modo)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "archivo", "listas.xlsx");
        formulario.Add(new StringContent("Lista propia del ensayo de originación"), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"/api/compliance/restrictive-lists/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(peticion));
    }
}
