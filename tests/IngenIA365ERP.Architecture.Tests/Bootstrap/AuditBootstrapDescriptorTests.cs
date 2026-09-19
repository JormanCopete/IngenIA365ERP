using IngenIA365ERP.Audit.Bootstrap;
using IngenIA365ERP.Audit.Bootstrap.Models;
using Xunit;

namespace IngenIA365ERP.Architecture.Tests.Bootstrap;

/// <summary>
/// Smoke tests del parser del descriptor de bootstrap de auditoría
/// (<c>database/migration/15_Audit_Mongodb_Bootstrap.json</c>). Aseguran
/// que el archivo real del repo se deserializa sin pérdida y que el
/// <see cref="EnvPasswordResolver"/> falla con mensajes accionables.
/// </summary>
public class AuditBootstrapDescriptorTests
{
    private static string FindDescriptor()
    {
        // El runner de tests corre desde tests/.../bin/Debug/netX. Subimos hasta
        // encontrar database/migration/ o explotamos con mensaje claro.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "database", "migration", "15_Audit_Mongodb_Bootstrap.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            "No se encontró database/migration/15_Audit_Mongodb_Bootstrap.json desde el cwd del test.");
    }

    [Fact]
    public void Real_descriptor_deserializa_estructura_completa()
    {
        var descriptor = AuditBootstrapRunner.LoadDescriptor(FindDescriptor());

        Assert.Equal("IngenIA365ERP_Audit", descriptor.Database);
        Assert.Equal(1, descriptor.Version);

        // 1 colección plantilla con 4 índices (3 query + 1 TTL).
        Assert.Single(descriptor.Collections);
        var template = descriptor.Collections[0];
        Assert.Equal("audit_events_template", template.Name);
        Assert.True(template.CreateIfMissing);
        Assert.Equal(4, template.Indexes.Count);

        // Feature 009 (FR-052): un solo TTL sobre expiresAt; cada documento trae su vencimiento por modulo.
        var ttl = template.Indexes.Single(i => i.Name == "ttl_expiresAt");
        Assert.Equal(0, ttl.ExpireAfterSeconds);
        Assert.Equal(1, ttl.Keys["expiresAt"]);

        // 2 roles: appendOnly + readOnly, ambos sobre IngenIA365ERP_Audit.
        Assert.Equal(2, descriptor.Roles.Count);
        var appendOnly = descriptor.Roles.Single(r => r.Role == "audit_appendOnly");
        Assert.Contains("insert", appendOnly.Privileges[0].Actions);
        Assert.DoesNotContain("update", appendOnly.Privileges[0].Actions);
        Assert.DoesNotContain("remove", appendOnly.Privileges[0].Actions);

        // 2 usuarios: writer + reader.
        Assert.Equal(2, descriptor.Users.Count);
        var writer = descriptor.Users.Single(u => u.User == "audit_writer");
        Assert.Equal("env:AUDIT_WRITER_PASSWORD", writer.PasswordSource);
    }

    [Fact]
    public void Parser_rechaza_descriptor_sin_database()
    {
        const string json = """{ "version": 1, "collections": [] }""";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            AuditBootstrapRunner.ParseDescriptor(json));

        Assert.Contains("database", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnvPasswordResolver_lee_variable_existente()
    {
        const string varName = "AUDIT_BOOTSTRAP_TEST_PWD";
        Environment.SetEnvironmentVariable(varName, "super-secret-123");
        try
        {
            var resolver = new EnvPasswordResolver();
            var pwd = resolver.Resolve($"env:{varName}");
            Assert.Equal("super-secret-123", pwd);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    public void EnvPasswordResolver_falla_claramente_cuando_falta_la_variable()
    {
        const string varName = "AUDIT_BOOTSTRAP_VAR_QUE_NO_EXISTE_42";
        Environment.SetEnvironmentVariable(varName, null);

        var resolver = new EnvPasswordResolver();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve($"env:{varName}"));

        Assert.Contains(varName, ex.Message);
    }

    [Fact]
    public void EnvPasswordResolver_rechaza_prefijo_no_soportado()
    {
        var resolver = new EnvPasswordResolver();
        Assert.Throws<NotSupportedException>(() => resolver.Resolve("literal:abc"));
    }
}
