using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// Las pruebas e2e de la contabilidad (feature 009 E2: informes y presupuesto) comparten UN host y
/// UNA cooperativa, como las de nómina. La colección es otra para que xUnit pueda correr las dos
/// suites en paralelo sobre contenedores distintos y ninguna vea los comprobantes de la otra.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class ContabilidadCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Contabilidad e2e";
}

/// <summary>
/// Bootstrap de la cooperativa de pruebas contables y ayudantes HTTP. Reutiliza
/// <see cref="NominaE2E.PrepararAsync"/> (decisión 24 del diseño E2): esa cooperativa ya tiene la
/// contabilidad iniciada —PUC solidario, movimiento en el nivel 5, ejercicio 2026 abierto, sucursal
/// «Principal»— y un usuario de sólo lectura. Aquí se agregan las auxiliares que estas pruebas
/// mueven, una segunda sucursal vinculada a una oficina administrativa y un usuario asignado sólo a
/// ella. Todo pasa por la API con tokens reales; nada se escribe a la base por debajo.
/// </summary>
public static class ContabilidadE2E
{
    public const string CorreoNorte = "contabilidad.norte@coop.nomina.test";
    public const string ClaveNorte = "Contabilidad-Norte-2026!";

    // Auxiliares de movimiento (nivel 5, ocho dígitos) bajo subcuentas del CUIF. Cada prueba usa las
    // suyas para que los saldos que afirma no dependan de lo que otra prueba contabilizó.
    public const string GastoSeguros = "51100501";      // 511005 SEGUROS
    public const string HonorariosPorPagar = "24050501"; // 240505 HONORARIOS
    public const string CajaNorte = "11050501";          // 110505 CAJA GENERAL
    public const string IngresoNorte = "41352001";       // 413520 VENTA DE PRODUCTOS EN ALMACENES NO ESPECIALIZADOS
    public const string CajaPrincipal = "11051001";      // 110510 CAJA MENOR
    public const string IngresoPrincipal = "41352201";   // 413522 VENTA DE PRODUCTOS AGROPECUARIOS
    public const string GastoAseo = "51101001";          // 511010 ASEO Y ELEMENTOS
    public const string GastoCafeteria = "51101101";     // 511011 CAFETERÍA
    public const string SubcuentaDeAgrupacion = "511010";

    /// <summary>Lo que sólo la sucursal Norte movió y lo que sólo la Principal movió: importes únicos para buscarlos en cualquier respuesta.</summary>
    public const decimal ImporteNorte = 1_234_567m;
    public const decimal ImportePrincipal = 8_765_432m;

    public sealed class Contexto
    {
        public required string TokenAdmin { get; init; }
        public required string TokenSoloLectura { get; init; }
        public required string TokenNorte { get; init; }
        public required Guid TenantPublicId { get; init; }
        public required Guid SucursalPrincipal { get; init; }
        public required Guid SucursalNorte { get; init; }
        /// <summary>Tercero y documento cruce del comprobante que sólo la Principal ve.</summary>
        public required Guid TerceroPrincipal { get; init; }
        public required Guid CuentaCajaPrincipal { get; init; }
        public required Guid ComprobanteNorte { get; init; }
        public required Guid ComprobantePrincipal { get; init; }
    }

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Contexto>> Contextos = [];
    private static readonly object Cerrojo = new();
    private static int _documento = 800_000_000;
    private static int _numeroDeCruce = 1000;

    public static Task<Contexto> PrepararAsync(CentralIdentityApiFixture fx)
    {
        lock (Cerrojo)
        {
            if (!Contextos.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx);
                Contextos[fx] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Contexto> PrepararDeVerdadAsync(CentralIdentityApiFixture fx)
    {
        var nomina = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = nomina.TokenAdmin;

        // --- auxiliares ---
        foreach (var (codigo, nombre) in new[]
                 {
                     (GastoSeguros, "Seguros generales"), (HonorariosPorPagar, "Honorarios por pagar"),
                     (CajaNorte, "Caja general Norte"), (IngresoNorte, "Ventas almacén"),
                     (CajaPrincipal, "Caja menor Principal"), (IngresoPrincipal, "Ventas agropecuarias"),
                     (GastoAseo, "Aseo y elementos"), (GastoCafeteria, "Cafetería"),
                 })
            await CrearAuxiliarAsync(http, admin, codigo, nombre);

        // --- segunda sucursal, vinculada a una oficina administrativa, y un usuario asignado sólo a ella ---
        var sucursales = await GetAsync(http, admin, "/api/core/branches?PageNumber=1&PageSize=20");
        var principal = sucursales.GetProperty("items").EnumerateArray().First(s => s.GetProperty("name").GetString() == "Principal").GetProperty("publicId").GetGuid();

        var oficina = await EnviarAsync(http, admin, HttpMethod.Post, "/api/admin/branches", new
        {
            tenantPublicId = nomina.TenantPublicId, code = "NORTE", name = "Oficina Norte", isHeadquarters = false,
        });
        oficina.StatusCode.Should().Be(HttpStatusCode.OK, $"oficina administrativa: «{await oficina.Content.ReadAsStringAsync()}»");
        var oficinaPublicId = (await LeerAsync(oficina)).GetGuid();

        var norte = await EnviarAsync(http, admin, HttpMethod.Post, "/api/core/branches", new { name = "Norte", shortName = "NTE", tenantBranchPublicId = oficinaPublicId });
        norte.StatusCode.Should().Be(HttpStatusCode.Created, $"sucursal Norte: «{await norte.Content.ReadAsStringAsync()}»");
        var sucursalNorte = (await LeerAsync(norte)).GetGuid();

        var invitacion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/tenants/{nomina.TenantPublicId}/invitations", new { email = CorreoNorte });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, $"invitación de Norte: «{await invitacion.Content.ReadAsStringAsync()}»");
        var tokenNorte = await AceptarInvitacionAsync(fx, http, CorreoNorte, ClaveNorte);

        var usuarios = await GetAsync(http, admin, $"/api/admin/users?search={Uri.EscapeDataString(CorreoNorte)}&pageSize=10");
        var usuarioNorte = usuarios.GetProperty("items").EnumerateArray()
            .First(u => string.Equals(u.GetProperty("email").GetString(), CorreoNorte, StringComparison.OrdinalIgnoreCase))
            .GetProperty("publicId").GetGuid();
        var asignacion = await EnviarAsync(http, admin, HttpMethod.Post, $"/api/admin/users/{usuarioNorte}/branches", new { branchPublicId = oficinaPublicId, isDefault = true });
        asignacion.IsSuccessStatusCode.Should().BeTrue($"asignar la oficina Norte: «{await asignacion.Content.ReadAsStringAsync()}»");

        // --- un comprobante en cada sucursal, cada uno con su tercero y su documento cruce ---
        var terceroNorte = await CrearPersonaAsync(http, admin, "Norte");
        var terceroPrincipal = await CrearPersonaAsync(http, admin, "Principal");
        var comprobanteNorte = await ContabilizarAsync(http, admin, new DateOnly(2026, 9, 5), "Venta de contado Norte", new object[]
        {
            Linea(CajaNorte, sucursalNorte, ImporteNorte, 0m, terceroNorte, "FV", SiguienteCruce()),
            Linea(IngresoNorte, sucursalNorte, 0m, ImporteNorte, terceroNorte),
        });
        var comprobantePrincipal = await ContabilizarAsync(http, admin, new DateOnly(2026, 9, 6), "Venta de contado Principal", new object[]
        {
            Linea(CajaPrincipal, principal, ImportePrincipal, 0m, terceroPrincipal, "FV", SiguienteCruce()),
            Linea(IngresoPrincipal, principal, 0m, ImportePrincipal, terceroPrincipal),
        });

        return new Contexto
        {
            TokenAdmin = admin,
            TokenSoloLectura = nomina.TokenSoloLectura,
            TokenNorte = tokenNorte,
            TenantPublicId = nomina.TenantPublicId,
            SucursalPrincipal = principal,
            SucursalNorte = sucursalNorte,
            TerceroPrincipal = terceroPrincipal,
            CuentaCajaPrincipal = await CuentaAsync(http, admin, CajaPrincipal),
            ComprobanteNorte = comprobanteNorte,
            ComprobantePrincipal = comprobantePrincipal,
        };
    }

    private static async Task<string> AceptarInvitacionAsync(CentralIdentityApiFixture fx, HttpClient http, string correo, string clave)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull($"la invitación de {correo} tiene que haber salido por correo");
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        token.Success.Should().BeTrue("el correo de invitación trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = clave } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar la invitación de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
        var acceso = (await LeerAsync(resp)).GetProperty("accessToken").GetString();
        acceso.Should().NotBeNullOrWhiteSpace();
        return acceso!;
    }

    // ------------------------------------------------------ cooperativa aislada --

    /// <summary>Una cooperativa propia dentro del mismo host: para lo que cambia el estado de todo el libro (cerrar el ejercicio, cargar la apertura) sin pisar a las demás pruebas.</summary>
    public sealed class CooperativaAislada
    {
        public required string TokenAdmin { get; init; }
        public required Guid TenantPublicId { get; init; }
        public required Guid SucursalPrincipal { get; init; }
        public required int PrimerEjercicio { get; init; }
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<CooperativaAislada>> Aisladas = [];

    /// <summary>
    /// Alta de una cooperativa con su administrador y la contabilidad iniciada (PUC solidario, nivel 5,
    /// sucursal «Principal», ejercicio <paramref name="primerEjercicio"/> abierto) —el mismo camino HTTP
    /// que <see cref="NominaE2E.PrepararAsync"/>, sin nómina—. Una por sufijo y fixture.
    /// </summary>
    public static Task<CooperativaAislada> CooperativaAisladaAsync(CentralIdentityApiFixture fx, string sufijo, int primerEjercicio)
    {
        lock (Cerrojo)
        {
            if (!Aisladas.TryGetValue((fx, sufijo), out var tarea))
            {
                tarea = CooperativaAisladaDeVerdadAsync(fx, sufijo, primerEjercicio);
                Aisladas[(fx, sufijo)] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<CooperativaAislada> CooperativaAisladaDeVerdadAsync(CentralIdentityApiFixture fx, string sufijo, int primerEjercicio)
    {
        using var http = fx.CreateClient();
        var tokenMaestro = await fx.IniciarSesionMaestroAsync(http);
        var correoAdmin = $"admin.{sufijo}@coop.contabilidad.test";
        var alta = await EnviarAsync(http, tokenMaestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = $"Coop. Contabilidad {sufijo}",
            schemaName = $"tenant_conta_{sufijo}",
            subdomain = $"tenant_conta_{sufijo}",
            nit = (900_800_000 + Math.Abs(sufijo.Sum(c => c) % 100_000)).ToString(),
            legalName = $"Cooperativa Contabilidad {sufijo} E2E",
            contactEmail = $"contacto.{sufijo}@coop.contabilidad.test",
            planType = "Basic",
            maxUsers = 20,
            storageLimitMb = 1024,
            firstAdminEmail = correoAdmin,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, $"alta de la cooperativa {sufijo}: «{await alta.Content.ReadAsStringAsync()}»");
        var tenantPublicId = (await LeerAsync(alta)).GetProperty("tenantPublicId").GetGuid();
        var admin = await AceptarInvitacionAsync(fx, http, correoAdmin, $"Conta-{sufijo}-2026!");

        var sucursal = await EnviarAsync(http, admin, HttpMethod.Post, "/api/core/branches", new { name = "Principal", shortName = "PPAL" });
        sucursal.IsSuccessStatusCode.Should().BeTrue($"sucursal: «{await sucursal.Content.ReadAsStringAsync()}»");
        var sucursales = await GetAsync(http, admin, "/api/core/branches?PageNumber=1&PageSize=10");
        var principal = sucursales.GetProperty("items").EnumerateArray().First().GetProperty("publicId").GetGuid();
        var inicio = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/setup/initialize", new
        {
            catalogCode = "PUC-SOLIDARIO", movementLevel = 5, niifGroup = 2, firstFiscalYear = primerEjercicio, mainBranchPublicId = principal, fourEyes = false,
        });
        inicio.IsSuccessStatusCode.Should().BeTrue($"iniciar la contabilidad: «{await inicio.Content.ReadAsStringAsync()}»");
        return new CooperativaAislada { TokenAdmin = admin, TenantPublicId = tenantPublicId, SucursalPrincipal = principal, PrimerEjercicio = primerEjercicio };
    }

    // ------------------------------------------------------------------ plan --

    /// <summary>Una auxiliar de movimiento bajo la subcuenta del catálogo (los seis primeros dígitos), habilitada para Contabilidad. Idempotente.</summary>
    public static Task CrearAuxiliarAsync(HttpClient http, string token, string codigo, string nombre) =>
        CrearCuentaAsync(http, token, codigo, nombre, codigo[..6], ["CNT"]);

    /// <summary>Una cuenta propia bajo el padre indicado (donde el CUIF no trae hijos —3505 EXCEDENTES— la empresa crea la de 6 dígitos y debajo la auxiliar). Idempotente.</summary>
    public static async Task CrearCuentaAsync(HttpClient http, string token, string codigo, string nombre, string codigoPadre, string[] modulos,
        bool tercero = false, bool cruce = false, bool centro = false, bool sucursal = false)
    {
        var padre = await CuentaAsync(http, token, codigoPadre);
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = codigo, name = nombre, parentPublicId = padre,
            enabledModules = modulos, requiresThirdParty = tercero, requiresCrossDocument = cruce, requiresCostCenter = centro, requiresBranch = sucursal,
        });
        if (resp.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return;
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"cuenta {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>El PublicId de una cuenta del plan por su código exacto.</summary>
    public static async Task<Guid> CuentaAsync(HttpClient http, string token, string codigo)
    {
        var busqueda = await GetAsync(http, token, $"/api/accounting/accounts/search?q={codigo}&onlyMovement=false");
        var cuenta = busqueda.EnumerateArray().FirstOrDefault(c => c.GetProperty("code").GetString() == codigo);
        cuenta.ValueKind.Should().Be(JsonValueKind.Object, $"la cuenta {codigo} existe en el plan");
        return cuenta.GetProperty("publicId").GetGuid();
    }

    // ------------------------------------------------------------------ datos --

    public static async Task<Guid> CrearPersonaAsync(HttpClient http, string token, string nombre)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Tercero", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetGuid();
    }

    /// <summary>El documento de identidad de una persona (lo que el libro auxiliar pinta como código del tercero).</summary>
    public static async Task<string> DocumentoDeAsync(HttpClient http, string token, Guid persona) =>
        (await GetAsync(http, token, $"/api/core/people/{persona}")).GetProperty("taxId").GetString()!;

    public static string SiguienteCruce() => Interlocked.Increment(ref _numeroDeCruce).ToString();

    public static object Linea(string cuenta, Guid? sucursal, decimal debito, decimal credito, Guid? persona = null, string? tipoCruce = null, string? numeroCruce = null, string? detalle = null) => new
    {
        accountCode = cuenta, branchPublicId = sucursal, costCenterPublicId = (Guid?)null, personPublicId = persona,
        crossDocumentType = tipoCruce, crossDocumentNumber = numeroCruce, debit = debito, credit = credito, detail = detalle, taxBase = (decimal?)null,
    };

    /// <summary>Guarda un borrador CG con esas líneas: 201 con <c>publicId</c> y sin errores bloqueantes (salvo que se admitan a propósito).</summary>
    public static async Task<Guid> BorradorAsync(HttpClient http, string token, DateOnly fecha, string descripcion, object[] lineas, bool admiteErrores = false)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/documents/drafts", new
        {
            voucherTypeCode = "CG", date = fecha, description = descripcion, lines = lineas,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"borrador: «{await resp.Content.ReadAsStringAsync()}»");
        var cuerpo = await LeerAsync(resp);
        if (!admiteErrores)
            cuerpo.GetProperty("errors").EnumerateArray().Where(e => e.GetProperty("severity").GetString() == "Error")
                .Should().BeEmpty("el borrador de prueba no debe tener errores bloqueantes");
        return cuerpo.GetProperty("publicId").GetGuid();
    }

    public static async Task CerrarMesAsync(HttpClient http, string token, int year, int month)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/accounting/periods/{year}/{month}/close", null);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent, $"cerrar {year}-{month}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    public static async Task ReabrirMesAsync(HttpClient http, string token, int year, int month, string motivo)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/accounting/periods/{year}/{month}/reopen", new { reason = motivo });
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent, $"reabrir {year}-{month}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Abre el ejercicio si no existe (422 <c>AlreadyExists</c> no es un fallo: otra prueba pudo abrirlo).</summary>
    public static async Task AbrirEjercicioAsync(HttpClient http, string token, int year)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/periods/years", new { year });
        if (resp.StatusCode == HttpStatusCode.UnprocessableEntity) { (await CodigoDeErrorAsync(resp)).Should().Be("Accounting.FiscalYear.AlreadyExists"); return; }
        resp.IsSuccessStatusCode.Should().BeTrue($"abrir {year}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>Los eventos de la auditoría de la cooperativa con esa acción, reintentando hasta 30 s (la escritura es asíncrona).</summary>
    public static async Task<List<JsonElement>> EventosDeAuditoriaAsync(HttpClient http, string token, string modulo, string accion, Func<JsonElement, bool>? filtro = null, int minimo = 1)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        List<JsonElement> eventos = [];
        while (DateTime.UtcNow < limite)
        {
            var pagina = await GetAsync(http, token, $"/api/audit/logs?module={modulo}&action={Uri.EscapeDataString(accion)}&pageSize=100");
            eventos = pagina.GetProperty("items").EnumerateArray().Where(e => filtro is null || filtro(e)).ToList();
            if (eventos.Count >= minimo) break;
            await Task.Delay(500);
        }
        return eventos;
    }

    public static async Task<long> ContabilizarBorradorAsync(HttpClient http, string token, Guid borrador)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/accounting/documents/{borrador}/post", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"contabilizar: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetProperty("number").GetInt64();
    }

    /// <summary>Borrador y contabilización en un paso; devuelve el PublicId del comprobante.</summary>
    public static async Task<Guid> ContabilizarAsync(HttpClient http, string token, DateOnly fecha, string descripcion, object[] lineas)
    {
        var borrador = await BorradorAsync(http, token, fecha, descripcion, lineas);
        await ContabilizarBorradorAsync(http, token, borrador);
        return borrador;
    }

    public static async Task<Guid> ReversarAsync(HttpClient http, string token, Guid comprobante, string motivo)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/accounting/documents/{comprobante}/reverse", new { reason = motivo, date = (DateOnly?)null });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetProperty("reversalPublicId").GetGuid();
    }

    // ------------------------------------------------------------------ tablas --

    /// <summary>Una tabla de informe tal como la API la entrega en JSON, con acceso por nombre de columna.</summary>
    public sealed class Tabla(JsonElement raiz)
    {
        public JsonElement Raiz => raiz;
        public string Titulo => raiz.GetProperty("titulo").GetString() ?? string.Empty;
        public IReadOnlyList<string> Columnas { get; } = raiz.GetProperty("columnas").EnumerateArray().Select(c => c.GetProperty("nombre").GetString() ?? string.Empty).ToList();
        public IReadOnlyList<string?> Claves { get; } = raiz.GetProperty("columnas").EnumerateArray().Select(c => c.TryGetProperty("clave", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null).ToList();
        public IReadOnlyList<JsonElement> Filas { get; } = raiz.GetProperty("filas").EnumerateArray().ToList();
        public JsonElement? Totales => raiz.TryGetProperty("totales", out var t) && t.ValueKind == JsonValueKind.Object ? t : null;
        public IReadOnlyList<string> Notas => raiz.GetProperty("notas").EnumerateArray().Select(n => n.GetString() ?? string.Empty).ToList();

        public int Indice(string columna)
        {
            var i = Columnas.ToList().IndexOf(columna);
            i.Should().BeGreaterThanOrEqualTo(0, $"la tabla «{Titulo}» tiene la columna {columna} (tiene: {string.Join(", ", Columnas)})");
            return i;
        }

        public int IndicePorClave(string clave)
        {
            var i = Claves.ToList().IndexOf(clave);
            i.Should().BeGreaterThanOrEqualTo(0, $"la tabla «{Titulo}» tiene la columna oculta {clave}");
            return i;
        }

        public static JsonElement Celda(JsonElement fila, int indice) => fila.GetProperty("valores")[indice];

        public static decimal Numero(JsonElement fila, int indice)
        {
            var v = Celda(fila, indice);
            return v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
        }

        public static string Texto(JsonElement fila, int indice)
        {
            var v = Celda(fila, indice);
            return v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : v.ToString();
        }

        public decimal Numero(JsonElement fila, string columna) => Numero(fila, Indice(columna));
        public string Texto(JsonElement fila, string columna) => Texto(fila, Indice(columna));
        public string Oculta(JsonElement fila, string clave) => Texto(fila, IndicePorClave(clave));

        /// <summary>La fila cuya primera columna («Código») es exactamente ese texto, o nula.</summary>
        public JsonElement? PorCodigo(string codigo) => Filas.Select(f => (JsonElement?)f).FirstOrDefault(f => Texto(f!.Value, 0) == codigo);
    }

    public static async Task<Tabla> InformeAsync(HttpClient http, string token, string vistaYFiltros)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Get, $"/api/reports/accounting/{vistaYFiltros}", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"GET /api/reports/accounting/{vistaYFiltros}: «{await resp.Content.ReadAsStringAsync()}»");
        return new Tabla(await LeerAsync(resp));
    }

    // ------------------------------------------------------------------- http --

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo) =>
        NominaE2E.EnviarAsync(http, token, metodo, url, cuerpo);

    public static Task<JsonElement> LeerAsync(HttpResponseMessage resp) => NominaE2E.LeerAsync(resp);

    public static Task<JsonElement> GetAsync(HttpClient http, string token, string url) => NominaE2E.GetAsync(http, token, url);

    public static Task<string> CodigoDeErrorAsync(HttpResponseMessage resp) => NominaE2E.CodigoDeErrorAsync(resp);
}
