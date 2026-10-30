using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Accounting;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0129 — la guarda de la migración destructiva <c>RetiroDeLaCarteraHeredada</c> (T0197; data-model parte 3 §13.3–§13.4;
/// FR-144; Principio XII), en el motor de <c>DB_PROVIDER</c>, y la mudanza de lo conservado (api.md §25). En una cooperativa
/// aislada: se baja la base hasta la migración anterior al retiro (el <c>Down()</c> recrea vacías las 55 tablas), se deja una
/// fila en <c>LND_LoanPortfolios</c> —con su línea en <c>LND_CreditLineParameters</c>— y otra en <c>LND_SavingsAccounts</c>; el
/// retiro se niega nombrando la retirada y su cantidad; con la fila <c>COR_SystemSettings.SettingKey = 'LND.RetiroHeredado.Aprobado'</c>
/// pasa, las 55 desaparecen y la de ahorros sigue. Por HTTP, el crédito heredado responde 404 sin alias.
///
/// <para>
/// Feature 014 (T007, T010): lo conservado de ahorros, aportes y CDT se retiró con la migración <c>RetiroDeCaptacionesHeredadas</c>
/// (R28), así que la fila de ahorros se escribe por debajo después de bajar la base, y <c>/api/savings/*</c> y
/// <c>/api/reports/savings/*</c> responden 404 como el crédito heredado. Antes de volver a la última migración la prueba borra esa
/// fila: con ella, la guarda de la 014 se negaría (la prueba de esa guarda es <c>RetiroDeCaptacionesHeredadasTests</c>).
/// </para>
///
/// <para>
/// Escribe a la base por debajo a propósito: las tablas retiradas ya no tienen entidad, y lo que se prueba es la migración. Las
/// filas mínimas se arman leyendo las columnas obligatorias del catálogo de la base (así la prueba no repite el esquema heredado).
/// Al terminar la base queda otra vez en la última migración.
/// </para>
/// </summary>
[Collection(CarteraCollection.Nombre)]
public class RetiroDeLaCarteraHeredadaTests(CentralIdentityApiFixture fx)
{
    private const string Retiro = "RetiroDeLaCarteraHeredada";
    private const string Aprobacion = "LND.RetiroHeredado.Aprobado";

    private static bool EsSqlServer => CentralIdentityApiFixture.ProviderKey == "SqlServer";

    /// <summary>Muestra de las 55 retiradas que se comprueba ausente después del retiro.</summary>
    private static readonly string[] Retiradas =
    [
        "LND_LoanPortfolios", "LND_CreditLineParameters", "LND_Transactions", "LND_PendingInstallments", "LND_Guarantees",
        "LND_PayrollDeductions", "LND_InterestRates", "LND_OnlineQueries", "LND_RecreationApplications", "LND_CollectionCases",
    ];

    [Fact]
    public async Task La_guarda_detiene_el_retiro_con_filas_y_sin_aprobacion_y_los_ahorros_sobreviven()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "retiro");
        using var http = fx.CreateClient();
        var persona = await ContabilidadE2E.CrearPersonaAsync(http, coop.TokenAdmin, "Deudora heredada");

        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == coop.TenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>()
            .Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (ApplicationDbContext)ambito.Db;
        var migrador = db.GetService<IMigrator>();

        // (1) Bajar hasta la migración anterior al retiro: el Down() recrea vacías las 55 tablas y la columna de Nómina.
        var migraciones = db.Database.GetMigrations().ToList();
        var indice = migraciones.FindIndex(m => m.EndsWith("_" + Retiro, StringComparison.Ordinal));
        indice.Should().BePositive($"la migración {Retiro} existe en el ensamblado de {CentralIdentityApiFixture.ProviderKey}");
        // Feature 015 (fase 46): RetiroDeLaTesoreriaHeredada es irreversible (su Down() lanza); antes de recorrer el historial hacia atrás se saca de la base.
        await IngenIA365ERP.API.IntegrationTests.Treasury.RetiroDeLaTesoreriaHeredadaTests.SacarElRetiroAsync(db);
        await migrador.MigrateAsync(migraciones[indice - 1]);
        (await ExisteTablaAsync(db, "LND_LoanPortfolios")).Should().BeTrue("el Down() del retiro recrea las tablas heredadas");

        // Una cuenta de ahorro: tabla conservada por la 013, el retiro del crédito no la toca. Desde la 014 no tiene entidad: se
        // escribe por debajo, como las de crédito (la recreó el Down() de RetiroDeCaptacionesHeredadas).
        await InsertarFilaMinimaAsync(db, "LND_SavingsAccounts", new Dictionary<string, object> { ["AccountNumber"] = 900001 });

        // (2) Un crédito heredado y sin aprobación: la guarda se niega y nombra la tabla y la cantidad.
        var personaId = await db.People.Where(p => p.PublicId == persona).Select(p => p.Id).SingleAsync();
        var linea = await InsertarFilaMinimaAsync(db, "LND_CreditLineParameters", new Dictionary<string, object>());
        await InsertarFilaMinimaAsync(db, "LND_LoanPortfolios", new Dictionary<string, object>
        {
            ["CreditLineId"] = linea,
            ["PersonId"] = personaId,
        });

        var mensaje = await MensajeDelFalloAsync(() => migrador.MigrateAsync(migraciones[indice]));
        mensaje.Should().NotBeNull("con filas y sin aprobación la migración tiene que detenerse");
        mensaje.Should().Contain(Retiro).And.Contain("LND_LoanPortfolios (1 filas)").And.Contain("LND_CreditLineParameters (1 filas)");
        if (EsSqlServer) mensaje.Should().Contain("50013");
        (await ExisteTablaAsync(db, "LND_LoanPortfolios")).Should().BeTrue("la guarda corre antes de borrar nada");
        (await db.Database.GetAppliedMigrationsAsync()).Should().NotContain(migraciones[indice]);

        // (3) Con la aprobación del dueño para esta cooperativa, pasa.
        db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = Aprobacion, SettingValue = "true", ValueType = "Boolean", ModulePrefix = "LND",
            Description = "Aprobación del retiro de la cartera heredada (prueba T0129).",
        });
        await db.SaveChangesAsync();

        await migrador.MigrateAsync(migraciones[indice]);
        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(migraciones[indice]);
        foreach (var tabla in Retiradas)
            (await ExisteTablaAsync(db, tabla)).Should().BeFalse($"{tabla} se retira");
        (await ExisteColumnaAsync(db, "PAY_SettlementDeductions", "LoanPortfolioId")).Should().BeFalse();
        (await ExisteColumnaAsync(db, "PAY_Novelties", "LoanPortfolioId")).Should().BeFalse();

        // (4) La cuenta de ahorro sigue ahí (tabla conservada, sin cambio de nombre).
        (await ExisteTablaAsync(db, "LND_SavingsAccounts")).Should().BeTrue();
        (await FilasAsync(db, "LND_SavingsAccounts")).Should().Be(1);

        // Deja la base en la última migración para el resto de la colección. La fila de ahorros se borra antes: con ella, la guarda
        // de RetiroDeCaptacionesHeredadas (feature 014) se negaría, y aquí no se prueba esa guarda.
        await db.Database.ExecuteSqlRawAsync(EsSqlServer ? "DELETE FROM [dbo].[LND_SavingsAccounts]" : "DELETE FROM dbo.\"LND_SavingsAccounts\"");
        await migrador.MigrateAsync();
    }

    [Fact]
    public void Las_rutas_de_credito_heredado_no_existen_y_lo_conservado_tampoco_desde_la_014()
    {
        var rutas = fx.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(r => r.RoutePattern.RawText ?? string.Empty)
            .ToList();

        // Las 15 familias del crédito heredado (63 rutas) y los informes heredados de /api/reports/lending: sin alias. El prefijo
        // /api/reports/lending volvió con el centro de informes de la Cartera nueva (DT §2.15, R-08): lo que se comprueba es que no queda
        // ninguna ruta heredada —aging/pdf, loan-statement, y person-portfolio y savings-statement, que se mudaron a /api/reports/savings—.
        string[] retiradas =
        [
            "/api/lending/accruals", "/api/lending/classifications", "/api/lending/defaults", "/api/lending/housing-parameters",
            "/api/lending/interest-rates", "/api/lending/loan-applications", "/api/lending/portfolios", "/api/lending/payments",
            "/api/lending/deduction-concepts", "/api/lending/payroll-deductions", "/api/lending/periodicity-parameters",
            "/api/lending/portfolio-accounts", "/api/lending/provision-parameters", "/api/lending/scoring-parameters",
            "/api/lending/term-rates", "/api/reports/lending/aging/", "/api/reports/lending/loan-statement",
            "/api/reports/lending/person-portfolio", "/api/reports/lending/savings-statement",
        ];
        rutas.Where(r => retiradas.Any(p => r.StartsWith(p, StringComparison.OrdinalIgnoreCase))).Should().BeEmpty("el crédito heredado se retira sin alias (api.md §25)");
        // Bajo /api/reports/lending sólo queda el centro de informes nuevo: el registro de vistas y una ruta de un segmento por vista.
        rutas.Where(r => r.StartsWith("/api/reports/lending", StringComparison.OrdinalIgnoreCase))
            .Should().OnlyContain(r => System.Text.RegularExpressions.Regex.IsMatch(r, "^/api/reports/lending/?([a-z0-9-]+)?$"),
                "el centro de informes publica /api/reports/lending/{vista} (también exogena-1019 y exogena-1020 de la 014), sin rutas con identificador ni /pdf como las heredadas");

        // Las 12 familias que la 013 mudó a /api/savings se retiraron con la 014 (T007, R28), también sin alias; ninguna vuelve a
        // /api/lending con su nombre heredado.
        string[] conservadas =
        [
            "savings-accounts", "savings-interest-liquidation", "savings-parameters", "sipla-parameters", "statements",
            "sub-zones", "transaction-codes", "withdrawal-statuses", "withdrawals", "zone-types", "zones",
        ];
        // «contributions» ya no es una familia heredada: desde la 014 /api/lending/contributions son los aportes nuevos (los de la clase Contributions de captaciones).
        foreach (var familia in conservadas)
            rutas.Should().NotContain(r => r.StartsWith($"/api/lending/{familia}", StringComparison.OrdinalIgnoreCase), $"{familia} ya no está en /api/lending");
        rutas.Should().NotContain(r => r.StartsWith("/api/savings", StringComparison.OrdinalIgnoreCase), "lo conservado se retiró con la 014");
        rutas.Should().NotContain(r => r.StartsWith("/api/cdt", StringComparison.OrdinalIgnoreCase), "el CDT heredado se retiró con la 014");
        rutas.Should().NotContain(r => r.StartsWith("/api/reports/savings", StringComparison.OrdinalIgnoreCase), "los informes heredados de ahorros se retiraron con la 014");
    }

    [Fact]
    public async Task Por_HTTP_el_credito_heredado_y_los_ahorros_heredados_responden_404()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "retiro_http");
        using var http = fx.CreateClient();

        // /api/reports/lending/aging es hoy la vista «aging» del centro de informes nuevo (DT §2.15): lo heredado es su /pdf y las rutas con id.
        foreach (var ruta in new[] { "/api/lending/portfolios", "/api/lending/loan-applications", "/api/lending/payments",
                     "/api/reports/lending/aging/pdf", $"/api/reports/lending/loan-statement/{Guid.NewGuid()}/pdf",
                     $"/api/reports/lending/person-portfolio/{Guid.NewGuid()}", $"/api/reports/lending/savings-statement/{Guid.NewGuid()}/pdf" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
            var resp = await http.SendAsync(pedido);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} se retiró sin alias");
        }

        foreach (var ruta in new[] { "/api/savings/zones", "/api/savings/zone-types", "/api/savings/sub-zones", "/api/savings/transaction-codes",
                     "/api/savings/withdrawal-statuses", "/api/savings/savings-parameters", "/api/savings/sipla-parameters",
                     "/api/savings/savings-accounts", "/api/savings/withdrawals" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
            var resp = await http.SendAsync(pedido);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} se retiró sin alias con la 014");
        }
    }

    // ------------------------------------------------------------ ayudantes --

    /// <summary>
    /// Inserta una fila con lo mínimo que exige la tabla: cada columna obligatoria sin valor por defecto ni identidad recibe un
    /// valor neutro según su tipo, salvo las de <paramref name="fijos"/>. Devuelve el <c>Id</c>.
    /// </summary>
    internal static async Task<long> InsertarFilaMinimaAsync(ApplicationDbContext db, string tabla, IDictionary<string, object> fijos)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            var columnas = new List<(string Nombre, string Tipo)>();
            await using (var consulta = conexion.CreateCommand())
            {
                consulta.CommandText = EsSqlServer
                    ? """
                      SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @t AND IS_NULLABLE = 'NO' AND COLUMN_DEFAULT IS NULL
                        AND COLUMNPROPERTY(OBJECT_ID('dbo.' + TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 0
                        AND DATA_TYPE NOT IN ('timestamp', 'rowversion')
                      """
                    : """
                      SELECT column_name, data_type FROM information_schema.columns
                      WHERE table_schema = 'dbo' AND table_name = @t AND is_nullable = 'NO' AND column_default IS NULL
                        AND is_identity = 'NO'
                      """;
                var parametro = consulta.CreateParameter();
                parametro.ParameterName = "@t";
                parametro.Value = tabla;
                consulta.Parameters.Add(parametro);
                await using var lector = await consulta.ExecuteReaderAsync();
                while (await lector.ReadAsync())
                    columnas.Add((lector.GetString(0), lector.GetString(1).ToLowerInvariant()));
            }
            columnas.Should().NotBeEmpty($"{tabla} tiene que existir con columnas obligatorias");

            string Valor(string nombre, string tipo)
            {
                if (fijos.TryGetValue(nombre, out var fijo)) return Convert.ToString(fijo, System.Globalization.CultureInfo.InvariantCulture)!;
                return tipo switch
                {
                    "boolean" => "false",
                    "bit" => "0",
                    "uuid" => "gen_random_uuid()",
                    "uniqueidentifier" => "NEWID()",
                    "date" => "'2026-01-02'",
                    _ when tipo.Contains("time", StringComparison.Ordinal) => "'2026-01-02'",
                    _ when tipo.Contains("char", StringComparison.Ordinal) || tipo == "text" => EsSqlServer ? "N'X'" : "'X'",
                    _ => "0",
                };
            }

            var nombres = columnas.Select(c => EsSqlServer ? $"[{c.Nombre}]" : $"\"{c.Nombre}\"");
            var valores = columnas.Select(c => Valor(c.Nombre, c.Tipo));
            await using var insercion = conexion.CreateCommand();
            insercion.CommandText = EsSqlServer
                ? $"INSERT INTO [dbo].[{tabla}] ({string.Join(", ", nombres)}) OUTPUT INSERTED.[Id] VALUES ({string.Join(", ", valores)})"
                : $"INSERT INTO dbo.\"{tabla}\" ({string.Join(", ", nombres)}) VALUES ({string.Join(", ", valores)}) RETURNING \"Id\"";
            return Convert.ToInt64(await insercion.ExecuteScalarAsync());
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }

    internal static async Task<long> FilasAsync(ApplicationDbContext db, string tabla)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = EsSqlServer ? $"SELECT COUNT(*) FROM [dbo].[{tabla}]" : $"SELECT COUNT(*) FROM dbo.\"{tabla}\"";
            return Convert.ToInt64(await comando.ExecuteScalarAsync());
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }

    internal static Task<bool> ExisteTablaAsync(ApplicationDbContext db, string tabla) =>
        ContarAsync(db, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'dbo' AND table_name = @t", tabla, null);

    private static Task<bool> ExisteColumnaAsync(ApplicationDbContext db, string tabla, string columna) =>
        ContarAsync(db, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = @t AND column_name = @c", tabla, columna);

    private static async Task<bool> ContarAsync(ApplicationDbContext db, string sql, string tabla, string? columna)
    {
        var conexion = db.Database.GetDbConnection();
        var abierta = conexion.State == System.Data.ConnectionState.Open;
        if (!abierta) await conexion.OpenAsync();
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = sql;
            var t = comando.CreateParameter();
            t.ParameterName = "@t";
            t.Value = tabla;
            comando.Parameters.Add(t);
            if (columna is not null)
            {
                var c = comando.CreateParameter();
                c.ParameterName = "@c";
                c.Value = columna;
                comando.Parameters.Add(c);
            }
            return Convert.ToInt64(await comando.ExecuteScalarAsync()) > 0;
        }
        finally
        {
            if (!abierta) await conexion.CloseAsync();
        }
    }

    /// <summary>El mensaje del fallo con sus excepciones internas (y el número de error de SQL Server), o nulo si no falló.</summary>
    internal static async Task<string?> MensajeDelFalloAsync(Func<Task> accion)
    {
        try
        {
            await accion();
            return null;
        }
        catch (Exception ex)
        {
            var partes = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                partes.Add(e.Message);
                if (e.GetType().GetProperty("Number")?.GetValue(e) is int numero) partes.Add(numero.ToString());
            }
            return string.Join(" | ", partes);
        }
    }
}
