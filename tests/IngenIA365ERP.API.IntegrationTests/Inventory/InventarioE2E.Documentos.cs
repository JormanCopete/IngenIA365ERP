using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// Ayudantes comunes de las e2e del cierre de I1 (T443): peticiones con <c>Idempotency-Key</c>, libros de plantilla, usuarios
/// invitados con un rol y el ciclo de los documentos (borrador → confirmar). Todo por HTTP, como la pantalla.
/// </summary>
public static partial class InventarioE2E
{
    /// <summary>La fecha de hoy en Colombia, la que usa el servidor para «hoy» (<c>HoyLocal</c>).</summary>
    public static DateOnly HoyEnColombia => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));

    /// <summary>Una petición con el token; las que no son GET llevan su propia <c>Idempotency-Key</c> (o la dada).</summary>
    public static Task<HttpResponseMessage> MandarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null, Guid? clave = null)
    {
        var peticion = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null) peticion.Content = JsonContent.Create(cuerpo);
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (metodo != HttpMethod.Get)
        {
            peticion.Headers.Remove(CabeceraDeClave);
            peticion.Headers.Add(CabeceraDeClave, (clave ?? Guid.NewGuid()).ToString());
        }
        return http.SendAsync(peticion);
    }

    /// <summary>Manda y exige un éxito; devuelve el cuerpo (o un objeto vacío si no trae).</summary>
    public static async Task<JsonElement> ExitoAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null)
    {
        var resp = await MandarAsync(http, token, metodo, url, cuerpo);
        var texto = await resp.Content.ReadAsStringAsync();
        resp.IsSuccessStatusCode.Should().BeTrue($"{metodo} {url} respondió {(int)resp.StatusCode}: «{texto}»");
        return string.IsNullOrWhiteSpace(texto) ? JsonDocument.Parse("{}").RootElement.Clone() : JsonDocument.Parse(texto).RootElement.Clone();
    }

    /// <summary>El 422 (u otro estado) esperado con su código de error; devuelve el sobre para mirar <c>data</c>.</summary>
    public static async Task<JsonElement> FallaAsync(HttpResponseMessage resp, string codigo, HttpStatusCode estado = HttpStatusCode.UnprocessableEntity)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        resp.StatusCode.Should().Be(estado, texto);
        var cuerpo = JsonDocument.Parse(texto).RootElement.Clone();
        cuerpo.GetProperty("code").GetString().Should().Be(codigo, texto);
        return cuerpo;
    }

    // ------------------------------------------------------------------------------------------ plantillas --

    /// <summary>Un libro con una hoja por sección: encabezados en la fila 1 y los datos debajo.</summary>
    public static byte[] Libro(params (string Hoja, string[] Encabezados, IReadOnlyList<string?[]> Filas)[] hojas)
    {
        using var libro = new XLWorkbook();
        foreach (var (nombre, encabezados, filas) in hojas)
        {
            var hoja = libro.Worksheets.Add(nombre);
            for (var c = 0; c < encabezados.Length; c++) hoja.Cell(1, c + 1).Value = encabezados[c];
            for (var f = 0; f < filas.Count; f++)
                for (var c = 0; c < filas[f].Length; c++)
                    if (filas[f][c] is { } v) hoja.Cell(f + 2, c + 1).SetValue(v);
        }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary><c>POST {ruta}/import?mode=</c> con el libro, cada vez con su clave (§0.5 de plantillas.md).</summary>
    public static Task<HttpResponseMessage> ImportarAsync(HttpClient http, string token, string ruta, string modo, byte[] contenido, string? motivo = null)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        formulario.Add(archivo, "file", "plantilla.xlsx");
        if (motivo is not null) formulario.Add(new StringContent(motivo), "reason");
        var peticion = new HttpRequestMessage(HttpMethod.Post, $"{ruta}/import?mode={modo}") { Content = formulario };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(ConClave(peticion));
    }

    /// <summary>Revisa (sin errores) y aplica el mismo libro.</summary>
    public static async Task<JsonElement> RevisarYAplicarAsync(HttpClient http, string token, string ruta, byte[] libro, string? motivo = null)
    {
        var revision = await ImportarAsync(http, token, ruta, "review", libro, motivo);
        revision.StatusCode.Should().Be(HttpStatusCode.OK, await revision.Content.ReadAsStringAsync());
        var cuerpo = await LeerAsync(revision);
        cuerpo.GetProperty("valid").GetBoolean().Should().BeTrue($"{ruta}: {cuerpo}");
        var aplicado = await ImportarAsync(http, token, ruta, "apply", libro, motivo);
        aplicado.StatusCode.Should().Be(HttpStatusCode.OK, await aplicado.Content.ReadAsStringAsync());
        return await LeerAsync(aplicado);
    }

    // -------------------------------------------------------------------------------------------- usuarios --

    /// <summary>
    /// Un usuario de la cooperativa aislada con los roles dados (por código): lo invita el administrador, acepta la invitación
    /// (la sesión sale de ahí) y recibe los roles. Los permisos se resuelven en cada petición, así que el token sirve después
    /// de asignar. Devuelve el token y el <c>PublicId</c> del usuario.
    /// </summary>
    public static async Task<(string Token, Guid UsuarioPublicId)> UsuarioAsync(
        CentralIdentityApiFixture fx, HttpClient http, CooperativaAislada coop, string correo, params string[] roles)
    {
        var invitacion = await EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/tenants/{coop.TenantPublicId}/invitations", new { email = correo });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, await invitacion.Content.ReadAsStringAsync());
        var acceso = await AceptarInvitacionAsync(fx, http, correo, "Ensayo-Inventario-2026!");

        var usuarios = await GetAsync(http, coop.TokenAdmin, $"/api/admin/users?search={Uri.EscapeDataString(correo)}&pageSize=50");
        var usuario = usuarios.GetProperty("items").EnumerateArray()
            .Single(u => string.Equals(u.GetProperty("email").GetString(), correo, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
        if (roles.Length > 0)
        {
            var lista = (await GetAsync(http, coop.TokenAdmin, "/api/admin/roles?includeBuiltIn=true&pageSize=100")).GetProperty("items").EnumerateArray().ToList();
            foreach (var codigo in roles)
            {
                var rol = lista.Single(r => r.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
                var asignacion = await EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, $"/api/admin/users/{usuario}/roles", new { rolePublicId = rol });
                if (asignacion.IsSuccessStatusCode || await CodigoDeErrorAsync(asignacion) == "Security.Users.AlreadyAssignedRole") continue;
                asignacion.IsSuccessStatusCode.Should().BeTrue($"rol {codigo} a {correo}: {await asignacion.Content.ReadAsStringAsync()}");
            }
        }
        return (acceso, usuario);
    }

    /// <summary>
    /// Un rol propio: la plantilla <paramref name="plantilla"/> (si hay) más <paramref name="extra"/> menos <paramref name="sin"/>.
    /// Devuelve su <c>PublicId</c>.
    /// </summary>
    public static async Task<Guid> RolAsync(HttpClient http, CooperativaAislada coop, string codigo, string? plantilla, string[]? extra = null, string[]? sin = null)
    {
        var admin = coop.TokenAdmin;
        var catalogo = (await GetAsync(http, admin, "/api/admin/permissions")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("code").GetString()!, p => p.GetProperty("publicId").GetGuid(), StringComparer.OrdinalIgnoreCase);
        var codigos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Guid? creado = null;
        if (plantilla is not null)
        {
            var desde = await ExitoAsync(http, admin, HttpMethod.Post, "/api/admin/roles/from-template",
                new { templateKey = plantilla, code = codigo, name = $"Ensayo {codigo}" });
            creado = desde.GetProperty("rolePublicId").GetGuid();
            var rol = await GetAsync(http, admin, $"/api/admin/roles/{creado}");
            foreach (var p in rol.GetProperty("permissions").EnumerateArray()) codigos.Add(p.GetProperty("code").GetString()!);
        }
        foreach (var e in extra ?? []) codigos.Add(e);
        foreach (var s in sin ?? []) codigos.Remove(s);
        var ids = codigos.Select(c => catalogo.TryGetValue(c, out var id) ? id : throw new InvalidOperationException($"El permiso {c} no está en el catálogo.")).ToList();

        if (creado is { } existente)
        {
            var put = await EnviarAsync(http, admin, HttpMethod.Put, $"/api/admin/roles/{existente}",
                new { name = $"Ensayo {codigo}", description = (string?)null, isAssignable = true, permissionPublicIds = ids });
            put.IsSuccessStatusCode.Should().BeTrue($"rol {codigo}: {await put.Content.ReadAsStringAsync()}");
            return existente;
        }
        var alta = await EnviarAsync(http, admin, HttpMethod.Post, "/api/admin/roles",
            new { code = codigo, name = $"Ensayo {codigo}", description = (string?)null, permissionPublicIds = ids });
        alta.IsSuccessStatusCode.Should().BeTrue($"rol {codigo}: {await alta.Content.ReadAsStringAsync()}");
        var lista = (await GetAsync(http, admin, $"/api/admin/roles?search={Uri.EscapeDataString(codigo)}&includeBuiltIn=false&pageSize=50"))
            .GetProperty("items").EnumerateArray();
        return lista.Single(r => r.GetProperty("code").GetString() == codigo).GetProperty("publicId").GetGuid();
    }

    /// <summary>Reemplaza el alcance comercial de un usuario por esas bodegas (la primera, por defecto).</summary>
    public static async Task AlcanceAsync(HttpClient http, string tokenAdmin, Guid usuario, params Guid[] bodegas)
    {
        await ExitoAsync(http, tokenAdmin, HttpMethod.Put, $"/api/inventory/scopes/users/{usuario}", new
        {
            warehouses = bodegas.Select((b, i) => new { warehousePublicId = b, isDefault = i == 0 }).ToList(),
        });
    }

    // ------------------------------------------------------------------------------------------- documentos --

    /// <summary><c>POST {ruta}</c> con el borrador; exige 201 y devuelve el documento.</summary>
    public static async Task<JsonElement> BorradorAsync(HttpClient http, string token, string ruta, object cuerpo)
    {
        var resp = await MandarAsync(http, token, HttpMethod.Post, ruta, cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"borrador en {ruta}: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    /// <summary><c>POST {ruta}/{id}/confirm</c>; devuelve la respuesta sin juzgarla.</summary>
    public static Task<HttpResponseMessage> PedirConfirmarAsync(HttpClient http, string token, string ruta, Guid id, Guid? clave = null) =>
        MandarAsync(http, token, HttpMethod.Post, $"{ruta}/{id}/confirm", new { }, clave);

    /// <summary>Confirma y exige 200; devuelve el <c>ConfirmationResultDto</c>.</summary>
    public static async Task<JsonElement> ConfirmarAsync(HttpClient http, string token, string ruta, Guid id)
    {
        var resp = await PedirConfirmarAsync(http, token, ruta, id);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"confirmar {ruta}/{id}: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    // ------------------------------------------------------------------------------------------ aprobaciones --

    /// <summary>La huella del contenido de una solicitud, como la vio quien puede verla (<c>GET /api/inventory/approvals/{id}</c>).</summary>
    public static async Task<string> HuellaAsync(HttpClient http, string token, Guid solicitud) =>
        (await GetAsync(http, token, $"/api/inventory/approvals/{solicitud}")).GetProperty("contentSha256").GetString()!;

    /// <summary><c>POST /api/inventory/approvals/{id}/decide</c> desde la sesión propia; la huella la lee el administrador.</summary>
    public static async Task<HttpResponseMessage> DecidirAsync(HttpClient http, string tokenAdmin, string token, Guid solicitud, bool aprobar,
        string? motivo = null, string? huella = null)
    {
        huella ??= await HuellaAsync(http, tokenAdmin, solicitud);
        return await MandarAsync(http, token, HttpMethod.Post, $"/api/inventory/approvals/{solicitud}/decide", new
        {
            decision = aprobar ? "Approve" : "Reject", reason = motivo, method = "OwnSession", expectedContentSha256 = huella,
        });
    }

    /// <summary>Una política de confirmación para un tipo de documento, vigente desde esa fecha.</summary>
    public static Task<JsonElement> PoliticaAsync(HttpClient http, string tokenAdmin, Guid tipo, DateOnly desde, params (decimal Umbral, string Permiso)[] niveles) =>
        ExitoAsync(http, tokenAdmin, HttpMethod.Post, "/api/inventory/approval-policies", new
        {
            subject = "DocumentConfirmation", documentTypePublicId = tipo, validFrom = desde.ToString("yyyy-MM-dd"), reason = "Política del ensayo",
            levels = niveles.Select((n, i) => new { order = i + 1, threshold = n.Umbral, permissionCode = n.Permiso }).ToList(),
        });

    // -------------------------------------------------------------------------------------------- informes --

    /// <summary>Una vista de <c>/api/reports/inventory/{vista}?format=json&amp;…</c> como tabla.</summary>
    public static async Task<Accounting.ContabilidadE2E.Tabla> InformeAsync(HttpClient http, string token, string vista, string query) =>
        new(await GetAsync(http, token, $"/api/reports/inventory/{vista}?format=json&{query}"));

    // --------------------------------------------------------------------------------- SQL en la cooperativa --

    /// <summary>
    /// Ejecuta una sentencia en la base de la cooperativa, por debajo de la API: sólo para lo que el ensayo hace «a mano» (alterar
    /// una proyección para ver que la integridad lo detecta). Devuelve las filas afectadas.
    /// </summary>
    public static async Task<int> SqlEnLaCooperativaAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, string sqlPostgres, string sqlSqlServer)
    {
        var filas = 0;
        await ConLaBaseAsync(fx, coop, async db =>
        {
            var sql = CentralIdentityApiFixture.ProviderKey == "SqlServer" ? sqlSqlServer : sqlPostgres;
            filas = await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database, sql);
        });
        return filas;
    }

    /// <summary>Una consulta escalar en la base de la cooperativa (para mirar lo que la API no expone, como columnas de auditoría).</summary>
    public static async Task<object?> EscalarEnLaCooperativaAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, string sqlPostgres, string sqlSqlServer)
    {
        object? valor = null;
        await ConLaBaseAsync(fx, coop, async db =>
        {
            var conexion = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(db.Database);
            await conexion.OpenAsync();
            try
            {
                await using var comando = conexion.CreateCommand();
                comando.CommandText = CentralIdentityApiFixture.ProviderKey == "SqlServer" ? sqlSqlServer : sqlPostgres;
                valor = await comando.ExecuteScalarAsync();
            }
            finally
            {
                await conexion.CloseAsync();
            }
        });
        return valor is DBNull ? null : valor;
    }

    private static async Task ConLaBaseAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, Func<Persistence.DbContext.ApplicationDbContext, Task> trabajo)
    {
        using var alcance = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(fx.Factory.Services);
        var servicios = alcance.ServiceProvider;
        var directorio = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Application.Common.Interfaces.ITenantDirectory>(servicios);
        var entrada = (await directorio.ListActiveAsync(CancellationToken.None)).Single(t => t.PublicId == coop.TenantPublicId);
        var fabrica = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Application.Common.Interfaces.ITenantDbContextFactory>(servicios);
        await using var ambito = fabrica.Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        await trabajo((Persistence.DbContext.ApplicationDbContext)ambito.Db);
    }
}
