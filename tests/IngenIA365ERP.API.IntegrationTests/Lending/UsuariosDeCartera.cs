using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Security.Roles.Plantillas;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0267 (feature 013, quickstart §2.7): los usuarios del ensayo de Cartera sobre una cooperativa aislada, todo por HTTP. Los roles salen
/// de las trece plantillas <c>cartera.*</c> por <c>POST /api/lending/activation/suggested-roles</c> (código <c>CARTERA-…</c>); el ajuste de
/// cada usuario va en un rol propio aparte, para no tocar el rol de la plantilla que comparten varios. Cada usuario acepta su invitación
/// (la cooperativa aislada no exige segundo factor: la sesión es directa), recibe sus roles, su persona del maestro
/// (<c>PUT /api/admin/users/{id}</c> con <c>personPublicId</c>; todos salvo <c>sinpersona</c>), sus oficinas
/// (<c>SEC_UserBranchAssignments</c>, <c>POST /api/admin/users/{id}/branches</c>, con las oficinas O1 y O2 vinculadas a su agencia contable) y
/// su alcance de líneas (<c>PUT /api/lending/scopes/users/{userPublicId}</c>); el rol de <c>jefe</c> recibe su monto operativo
/// (<c>POST /api/lending/amount-limits</c>).
/// <list type="table">
/// <item><c>administrador</c>: <c>cartera.administrador</c> (trae todas las oficinas y líneas).</item>
/// <item><c>asesor.o1</c>, <c>asesor.o2</c>: <c>cartera.asesor</c> (desde el 2026-10-14 la plantilla trae <c>Lending.CreditLines.View</c>, respuesta
/// del dueño: ya no hace falta el rol de ensayo aparte que tenía para leer líneas); O1 y O2.</item>
/// <item><c>analista</c>: <c>cartera.analista</c>; O1 y O2.</item>
/// <item><c>jefe</c>: <c>cartera.jefe</c> + <c>Lending.Payments.BackdateBeforeClose</c>; <c>Lending.Disbursements.Execute</c> hasta 50.000.000.</item>
/// <item><c>gerente</c>: <c>cartera.gerente</c>.</item>
/// <item><c>comite.1..3</c>: miembros del Comité de crédito con <c>Lending.Sessions.Read</c> y <c>Lending.Sessions.Vote</c>; <c>comite.1</c>
/// además <c>cartera.asesor</c> (US2-6). La plantilla <c>cartera.comite-credito</c> es la de la <b>secretaría</b> del comité
/// (<c>RecordVotes</c>, <c>Close</c>): con ella un miembro registraría el voto de otro, que es justo lo que el ensayo prueba que no puede.</item>
/// <item><c>secretaria</c>: <c>cartera.consejo</c> (registra asistencia y votos, cierra).</item>
/// <item><c>gestor</c>: <c>cartera.gestor-cobranza</c>; O1.</item>
/// <item><c>oficial</c>: <c>cartera.oficial-cumplimiento</c>.</item>
/// <item><c>contadora</c>: <c>cartera.contador</c> + <c>Core.NationalValues.Review</c> (los permisos contables de Cartera llegan con US7).</item>
/// <item><c>auditor</c>, <c>revisor</c>: <c>cartera.auditor</c>, <c>cartera.revisor-fiscal</c>.</item>
/// <item><c>pruebas</c>: <c>Lending.TestDoubles.Operate</c>.</item>
/// <item><c>sinpersona</c>: <c>cartera.analista</c> sin persona (para <c>Lending.Security.UserWithoutPerson</c>); O1 y O2.</item>
/// <item><c>lectura</c>: el rol integrado <c>ReadOnly</c>.</item>
/// </list>
/// El alcance de líneas nace vacío; desde US1 (T0343) <see cref="FijarLineasAsync"/> asigna las líneas que ya existen —
/// <c>SeguridadYHabeasDataDeCarteraTests</c> da <c>CONLIB</c>, <c>CONCAJA</c> y <c>ROTATIVO</c> a <c>asesor.o1</c>, no <c>VARIBR</c>—. Uno
/// por cooperativa: las pruebas que piden los usuarios de la misma cooperativa los comparten. (nuevo)
/// </summary>
public sealed class UsuariosDeCartera
{
    /// <summary>Un usuario del ensayo: su token, su <c>PublicId</c>, su persona (nula en <c>sinpersona</c>) y sus roles.</summary>
    public sealed record Usuario(string Clave, string Correo, string Token, Guid PublicId, Guid? Persona, IReadOnlyList<Guid> Roles);

    /// <summary>La contraseña con que los usuarios del ensayo aceptan su invitación.</summary>
    public const string ClaveDelEnsayo = "Ensayo-Cartera-2026!";

    private static int _ingresos;

    /// <summary>
    /// Un access nuevo del usuario por <c>/api/auth/login</c> (una sola membresía: selección automática). Lo usan las pruebas largas: el access
    /// dura 15 minutos (US4, T0483). Cada ingreso va con su propia IP de origen (<c>CF-Connecting-IP</c>, la que cuenta el limitador): el host de la
    /// colección es uno y <c>POST /api/auth/login</c> admite 10 por minuto por IP, así que en la regresión completa, con las demás clases
    /// ingresando en el mismo minuto, <c>DocumentosDeCarteraTests</c> recibía 429 (2026-10-14) aunque en el filtro de módulos pasaba.
    /// </summary>
    public static async Task<string> IngresarAsync(HttpClient http, Usuario usuario)
    {
        var n = Interlocked.Increment(ref _ingresos);
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = usuario.Correo, password = ClaveDelEnsayo }),
        };
        pedido.Headers.Add("CF-Connecting-IP", $"10.252.{n / 250 % 250}.{n % 250 + 1}");
        var resp = await http.SendAsync(pedido);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"ingresar como {usuario.Clave}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.LeerAsync(resp)).GetProperty("accessToken").GetString()!;
    }

    /// <summary>Una oficina (<c>ADM_Branches</c>) y la agencia contable (<c>COR_Branches</c>) vinculada a ella.</summary>
    public sealed record Oficina(string Codigo, Guid OficinaPublicId, Guid AgenciaPublicId);

    /// <summary>El monto operativo del rol de <c>jefe</c> en <c>Lending.Disbursements.Execute</c> (quickstart §2.7).</summary>
    public const decimal LimiteDeDesembolsoDelJefe = 50_000_000m;

    public required IReadOnlyDictionary<string, Usuario> Todos { get; init; }
    public required Oficina O1 { get; init; }
    public required Oficina O2 { get; init; }

    /// <summary>Los roles creados desde las plantillas: clave de la plantilla → <c>PublicId</c> del rol.</summary>
    public required IReadOnlyDictionary<string, Guid> RolesSugeridos { get; init; }

    public Usuario Administrador => Todos["administrador"];
    public Usuario AsesorO1 => Todos["asesor.o1"];
    public Usuario AsesorO2 => Todos["asesor.o2"];
    public Usuario Analista => Todos["analista"];
    public Usuario Jefe => Todos["jefe"];
    public Usuario Gerente => Todos["gerente"];
    public Usuario Comite1 => Todos["comite.1"];
    public Usuario Comite2 => Todos["comite.2"];
    public Usuario Comite3 => Todos["comite.3"];
    public Usuario Secretaria => Todos["secretaria"];
    public Usuario Gestor => Todos["gestor"];
    public Usuario Oficial => Todos["oficial"];
    public Usuario Contadora => Todos["contadora"];
    public Usuario Auditor => Todos["auditor"];
    public Usuario Revisor => Todos["revisor"];
    public Usuario Pruebas => Todos["pruebas"];
    public Usuario SinPersona => Todos["sinpersona"];
    public Usuario Lectura => Todos["lectura"];

    private static readonly Dictionary<(CentralIdentityApiFixture, Guid), Task<UsuariosDeCartera>> Creados = [];
    private static readonly object Cerrojo = new();

    public static Task<UsuariosDeCartera> CrearAsync(CentralIdentityApiFixture fx, CarteraE2E.CooperativaAislada coop)
    {
        lock (Cerrojo)
        {
            if (!Creados.TryGetValue((fx, coop.TenantPublicId), out var tarea))
            {
                tarea = CrearDeVerdadAsync(fx, coop);
                Creados[(fx, coop.TenantPublicId)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<UsuariosDeCartera> CrearDeVerdadAsync(CentralIdentityApiFixture fx, CarteraE2E.CooperativaAislada coop)
    {
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = CarteraE2E.Hoy(fx);
        var sufijo = coop.TenantPublicId.ToString("N")[..8];

        // Las trece plantillas en bloque (paso del asistente de activación); correrlo dos veces no crea nada.
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/activation/suggested-roles", new { });
        var roles = (await CarteraE2E.GetAsync(http, admin, "/api/admin/roles?search=CARTERA-&includeBuiltIn=false&pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .ToDictionary(r => r.GetProperty("code").GetString()!, r => r.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
        Guid Plantilla(string clave) => roles[CreateSuggestedLendingRolesCommand.CodigoDeRol(clave)];
        var sugeridos = PlantillasDeCartera.ToDictionary(c => c, Plantilla);

        // Los ajustes del ensayo, cada uno en su rol propio.
        var ajusteJefe = await RolPropioAsync(http, admin, "ENS-CAR-JEFE", "Lending.Payments.BackdateBeforeClose");
        var miembroDeComite = await RolPropioAsync(http, admin, "ENS-CAR-COMITE", "Lending.Sessions.Read", "Lending.Sessions.Vote");
        // Accounting.LendingRules.Manage, Accounting.LendingBatches.Run y Accounting.RegulatoryIndicators.Manage (quickstart §2.7) todavía no
        // están en el catálogo: los siembra la historia de la matriz contable (US7), que los suma aquí.
        var contable = await RolPropioAsync(http, admin, "ENS-CAR-CONTA", "Core.NationalValues.Review");
        var pruebas = await RolPropioAsync(http, admin, "ENS-CAR-PRUEBAS", "Lending.TestDoubles.Operate");
        var lectura = (await CarteraE2E.GetAsync(http, admin, "/api/admin/roles?includeBuiltIn=true&pageSize=100")).GetProperty("items").EnumerateArray()
            .Single(r => r.GetProperty("code").GetString() == "ReadOnly").GetProperty("publicId").GetGuid();

        var o1 = await OficinaAsync(http, coop, "O1");
        var o2 = await OficinaAsync(http, coop, "O2");

        var todos = new Dictionary<string, Usuario>(StringComparer.Ordinal);
        async Task UnoAsync(string clave, Guid[] rolesDelUsuario, Oficina[] oficinas, bool conPersona = true)
        {
            var correo = $"{clave}.{sufijo}@coop.cartera.test";
            todos[clave] = await UsuarioAsync(fx, http, coop, clave, correo, rolesDelUsuario, oficinas, conPersona);
        }

        await UnoAsync("administrador", [Plantilla("cartera.administrador")], []);
        await UnoAsync("asesor.o1", [Plantilla("cartera.asesor")], [o1]);
        await UnoAsync("asesor.o2", [Plantilla("cartera.asesor")], [o2]);
        await UnoAsync("analista", [Plantilla("cartera.analista")], [o1, o2]);
        await UnoAsync("jefe", [Plantilla("cartera.jefe"), ajusteJefe], []);
        await UnoAsync("gerente", [Plantilla("cartera.gerente")], []);
        await UnoAsync("comite.1", [miembroDeComite, Plantilla("cartera.asesor")], [o1]);
        await UnoAsync("comite.2", [miembroDeComite], []);
        await UnoAsync("comite.3", [miembroDeComite], []);
        await UnoAsync("secretaria", [Plantilla("cartera.consejo")], []);
        await UnoAsync("gestor", [Plantilla("cartera.gestor-cobranza")], [o1]);
        await UnoAsync("oficial", [Plantilla("cartera.oficial-cumplimiento")], []);
        await UnoAsync("contadora", [Plantilla("cartera.contador"), contable], []);
        await UnoAsync("auditor", [Plantilla("cartera.auditor")], []);
        await UnoAsync("revisor", [Plantilla("cartera.revisor-fiscal")], []);
        await UnoAsync("pruebas", [pruebas], []);
        await UnoAsync("sinpersona", [Plantilla("cartera.analista")], [o1, o2], conPersona: false);
        await UnoAsync("lectura", [lectura], []);

        // El monto operativo del jefe, sobre el rol de su plantilla.
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/amount-limits", new
        {
            rolePublicId = Plantilla("cartera.jefe"), permissionCode = "Lending.Disbursements.Execute", maxAmount = LimiteDeDesembolsoDelJefe,
            validFrom = hoy, reason = "Límite de desembolso del jefe de cartera del ensayo",
        });

        return new UsuariosDeCartera { Todos = todos, O1 = o1, O2 = o2, RolesSugeridos = sugeridos };
    }

    /// <summary>Las plantillas de Cartera (quickstart §2.3): las trece del crédito y, desde la feature 014 (T018), las cuatro de captaciones.</summary>
    public static readonly string[] PlantillasDeCartera =
    [
        "cartera.administrador", "cartera.asesor", "cartera.analista", "cartera.jefe", "cartera.comite-credito", "cartera.comite-riesgos",
        "cartera.gerente", "cartera.consejo", "cartera.gestor-cobranza", "cartera.oficial-cumplimiento", "cartera.contador", "cartera.auditor",
        "cartera.revisor-fiscal",
        "cartera.jefe-captaciones", "cartera.asesor-captaciones", "cartera.analista-aportes", "cartera.tesorero-liquidez",
    ];

    private static async Task<Usuario> UsuarioAsync(CentralIdentityApiFixture fx, HttpClient http, CarteraE2E.CooperativaAislada coop, string clave,
        string correo, Guid[] roles, Oficina[] oficinas, bool conPersona)
    {
        var admin = coop.TokenAdmin;
        var invitacion = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", DocumentoDePrueba.Invitacion(correo));
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, $"invitar a {clave}: «{await invitacion.Content.ReadAsStringAsync()}»");
        var token = await CarteraE2E.AceptarInvitacionAsync(fx, http, correo, ClaveDelEnsayo);
        var usuario = await CarteraE2E.UsuarioPorCorreoAsync(http, admin, correo);

        foreach (var rol in roles)
        {
            var asignacion = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
            if (asignacion.IsSuccessStatusCode || await CarteraE2E.CodigoDeErrorAsync(asignacion) == "Security.Users.AlreadyAssignedRole") continue;
            asignacion.IsSuccessStatusCode.Should().BeTrue($"rol {rol} a {clave}: «{await asignacion.Content.ReadAsStringAsync()}»");
        }

        Guid? persona = null;
        if (conPersona)
        {
            persona = await CarteraE2E.PersonaAsync(http, admin, NombreDe(clave), "Ensayo");
            await CarteraE2E.AsociarPersonaAsync(http, admin, usuario, correo, persona.Value);
        }

        for (var i = 0; i < oficinas.Length; i++)
        {
            var oficina = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuario}/branches", new
            {
                branchPublicId = oficinas[i].OficinaPublicId, isDefault = i == 0,
            });
            oficina.IsSuccessStatusCode.Should().BeTrue($"oficina {oficinas[i].Codigo} a {clave}: «{await oficina.Content.ReadAsStringAsync()}»");
        }

        // El alcance de líneas nace vacío (FijarLineasAsync lo cambia cuando existen las líneas).
        await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Put, $"/api/lending/scopes/users/{usuario}", new { lines = Array.Empty<object>() });

        return new Usuario(clave, correo, token, usuario, persona, roles);
    }

    /// <summary>
    /// Reemplaza en bloque las líneas que opera <paramref name="usuario"/> (<c>PUT /api/lending/scopes/users/{id}</c>, api.md §3.8); la primera
    /// queda por defecto. Las oficinas no cambian: las asigna Seguridad.
    /// </summary>
    public static Task<System.Text.Json.JsonElement> FijarLineasAsync(HttpClient http, string tokenAdmin, Usuario usuario, params Guid[] lineas) =>
        CarteraE2E.ExitoAsync(http, tokenAdmin, HttpMethod.Put, $"/api/lending/scopes/users/{usuario.PublicId}", new
        {
            lines = lineas.Select((l, i) => new { creditLinePublicId = l, isDefault = i == 0 }).ToList(),
        });

    /// <summary>Un nombre de persona legible desde la clave del usuario (<c>comite.2</c> → <c>Comite2</c>).</summary>
    private static string NombreDe(string clave) =>
        string.Concat(clave.Split('.').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    /// <summary>Un rol propio con esos permisos (por código); devuelve su <c>PublicId</c>.</summary>
    private static async Task<Guid> RolPropioAsync(HttpClient http, string admin, string codigo, params string[] permisos)
    {
        var catalogo = (await CarteraE2E.GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
        var ids = permisos.Select(c => catalogo.TryGetValue(c, out var id) ? id : throw new InvalidOperationException($"El permiso {c} no está en el catálogo.")).ToList();
        var alta = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/admin/roles",
            new { code = codigo, name = $"Ensayo {codigo}", description = "Ajuste del ensayo de Cartera (quickstart §2.7)", permissionPublicIds = ids });
        alta.IsSuccessStatusCode.Should().BeTrue($"rol {codigo}: «{await alta.Content.ReadAsStringAsync()}»");
        return (await CarteraE2E.GetAsync(http, admin, $"/api/admin/roles?search={Uri.EscapeDataString(codigo)}&includeBuiltIn=false&pageSize=50"))
            .GetProperty("items").EnumerateArray().Single(r => r.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
    }

    /// <summary>La oficina (<c>/api/admin/branches</c>) y su agencia contable vinculada (<c>/api/core/branches</c> con <c>tenantBranchPublicId</c>).</summary>
    private static async Task<Oficina> OficinaAsync(HttpClient http, CarteraE2E.CooperativaAislada coop, string codigo)
    {
        var alta = await CarteraE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, "/api/admin/branches", new
        {
            tenantPublicId = coop.TenantPublicId, code = codigo, name = $"Oficina {codigo}", isHeadquarters = false,
        });
        alta.IsSuccessStatusCode.Should().BeTrue($"oficina {codigo}: «{await alta.Content.ReadAsStringAsync()}»");
        var cuerpo = await CarteraE2E.LeerAsync(alta);
        var oficina = cuerpo.ValueKind == JsonValueKind.String ? cuerpo.GetGuid() : cuerpo.GetProperty("publicId").GetGuid();

        var agencia = await CarteraE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, "/api/core/branches", new
        {
            name = $"Agencia {codigo}", shortName = codigo, tenantBranchPublicId = oficina,
        });
        agencia.StatusCode.Should().Be(HttpStatusCode.Created, $"agencia {codigo}: «{await agencia.Content.ReadAsStringAsync()}»");
        return new Oficina(codigo, oficina, (await CarteraE2E.LeerAsync(agencia)).GetGuid());
    }
}
