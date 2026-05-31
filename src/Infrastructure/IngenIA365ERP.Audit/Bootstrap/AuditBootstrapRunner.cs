using System.Text.Json;
using IngenIA365ERP.Audit.Bootstrap.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Bootstrap;

/// <summary>
/// Aplica un <see cref="AuditBootstrapDescriptor"/> contra una instancia
/// MongoDB. Todas las operaciones son idempotentes — la segunda corrida
/// no produce cambios.
///
/// <para>
/// Las operaciones admin (createRole, createUser) requieren que el
/// <see cref="IMongoClient"/> esté conectado con credenciales con privilegios
/// sobre la BD <c>admin</c> + la BD destino. El runner NO valida esto; el
/// servidor responderá con NotAuthorized y la excepción se propaga.
/// </para>
/// </summary>
public sealed class AuditBootstrapRunner
{
    private readonly IMongoClient _client;
    private readonly IPasswordResolver _passwordResolver;
    private readonly ILogger<AuditBootstrapRunner> _log;

    public AuditBootstrapRunner(
        IMongoClient client,
        IPasswordResolver passwordResolver,
        ILogger<AuditBootstrapRunner> log)
    {
        _client = client;
        _passwordResolver = passwordResolver;
        _log = log;
    }

    public static AuditBootstrapDescriptor LoadDescriptor(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Descriptor no encontrado: {filePath}", filePath);

        var json = File.ReadAllText(filePath);
        return ParseDescriptor(json);
    }

    public static AuditBootstrapDescriptor ParseDescriptor(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var descriptor = JsonSerializer.Deserialize<AuditBootstrapDescriptor>(json, options)
            ?? throw new InvalidOperationException("El descriptor JSON está vacío o no se pudo deserializar.");

        if (string.IsNullOrWhiteSpace(descriptor.Database))
            throw new InvalidOperationException("El descriptor no especifica la propiedad 'database'.");

        return descriptor;
    }

    public async Task<AuditBootstrapResult> ApplyAsync(
        AuditBootstrapDescriptor descriptor, CancellationToken ct = default)
    {
        var result = new AuditBootstrapResult { Database = descriptor.Database };
        var db = _client.GetDatabase(descriptor.Database);

        await ApplyCollectionsAsync(db, descriptor.Collections, result, ct);
        await ApplyRolesAsync(db, descriptor.Roles, result, ct);
        await ApplyUsersAsync(db, descriptor.Users, result, ct);

        _log.LogInformation(
            "Audit bootstrap aplicado en BD '{Database}': {Changes} cambios netos " +
            "(collections+{C}/={Cx}, indexes+{I}/={Ix}, roles+{R}/={Rx}, users+{Uc}/~{Uu}).",
            descriptor.Database, result.TotalChanges,
            result.CollectionsCreated.Count, result.CollectionsAlreadyExisted.Count,
            result.IndexesCreated.Count, result.IndexesAlreadyExisted.Count,
            result.RolesCreated.Count, result.RolesAlreadyExisted.Count,
            result.UsersCreated.Count, result.UsersUpdated.Count);

        return result;
    }

    // -------------------- Collections + indexes --------------------

    private async Task ApplyCollectionsAsync(
        IMongoDatabase db, List<CollectionSpec> specs, AuditBootstrapResult result, CancellationToken ct)
    {
        var existing = await db.ListCollectionNames().ToListAsync(ct);

        foreach (var spec in specs)
        {
            if (existing.Contains(spec.Name))
            {
                result.CollectionsAlreadyExisted.Add(spec.Name);
            }
            else if (spec.CreateIfMissing)
            {
                await db.CreateCollectionAsync(spec.Name, cancellationToken: ct);
                result.CollectionsCreated.Add(spec.Name);
                _log.LogInformation("Colección creada: {Collection}", spec.Name);
            }

            var coll = db.GetCollection<BsonDocument>(spec.Name);
            await ApplyIndexesAsync(coll, spec.Name, spec.Indexes, result, ct);
        }
    }

    private async Task ApplyIndexesAsync(
        IMongoCollection<BsonDocument> coll,
        string collectionName,
        List<IndexSpec> specs,
        AuditBootstrapResult result,
        CancellationToken ct)
    {
        var existingNames = new HashSet<string>(StringComparer.Ordinal);
        using (var cursor = await coll.Indexes.ListAsync(ct))
        {
            await cursor.ForEachAsync(doc =>
            {
                if (doc.TryGetValue("name", out var name)) existingNames.Add(name.AsString);
            }, ct);
        }

        foreach (var spec in specs)
        {
            var label = $"{collectionName}.{spec.Name}";
            if (existingNames.Contains(spec.Name))
            {
                result.IndexesAlreadyExisted.Add(label);
                continue;
            }

            var keysDoc = new BsonDocument();
            foreach (var kv in spec.Keys) keysDoc.Add(kv.Key, kv.Value);

            var options = new CreateIndexOptions { Name = spec.Name, Unique = spec.Unique };
            if (spec.ExpireAfterSeconds.HasValue)
                options.ExpireAfter = TimeSpan.FromSeconds(spec.ExpireAfterSeconds.Value);

            var model = new CreateIndexModel<BsonDocument>(keysDoc, options);
            await coll.Indexes.CreateOneAsync(model, cancellationToken: ct);
            result.IndexesCreated.Add(label);
            _log.LogInformation("Índice creado: {Label}", label);
        }
    }

    // -------------------- Roles --------------------

    private async Task ApplyRolesAsync(
        IMongoDatabase db, List<RoleSpec> specs, AuditBootstrapResult result, CancellationToken ct)
    {
        foreach (var spec in specs)
        {
            if (await RoleExistsAsync(db, spec.Role, ct))
            {
                result.RolesAlreadyExisted.Add(spec.Role);
                continue;
            }

            var cmd = new BsonDocument
            {
                { "createRole", spec.Role },
                { "privileges", BuildPrivileges(spec.Privileges) },
                { "roles", BuildRoleRefs(spec.Roles) },
            };

            await db.RunCommandAsync<BsonDocument>(cmd, cancellationToken: ct);
            result.RolesCreated.Add(spec.Role);
            _log.LogInformation("Rol creado: {Role}", spec.Role);
        }
    }

    private static async Task<bool> RoleExistsAsync(IMongoDatabase db, string roleName, CancellationToken ct)
    {
        var cmd = new BsonDocument
        {
            { "rolesInfo", new BsonDocument { { "role", roleName }, { "db", db.DatabaseNamespace.DatabaseName } } }
        };
        var resp = await db.RunCommandAsync<BsonDocument>(cmd, cancellationToken: ct);
        return resp.TryGetValue("roles", out var roles)
            && roles is BsonArray arr
            && arr.Count > 0;
    }

    private static BsonArray BuildPrivileges(List<PrivilegeSpec> specs)
    {
        var arr = new BsonArray();
        foreach (var p in specs)
        {
            var resource = new BsonDocument { { "db", p.Resource.Db }, { "collection", p.Resource.Collection } };
            var actions = new BsonArray(p.Actions);
            arr.Add(new BsonDocument { { "resource", resource }, { "actions", actions } });
        }
        return arr;
    }

    private static BsonArray BuildRoleRefs(List<RoleRef> specs)
    {
        var arr = new BsonArray();
        foreach (var r in specs)
            arr.Add(new BsonDocument { { "role", r.Role }, { "db", r.Db } });
        return arr;
    }

    // -------------------- Users --------------------

    private async Task ApplyUsersAsync(
        IMongoDatabase db, List<UserSpec> specs, AuditBootstrapResult result, CancellationToken ct)
    {
        foreach (var spec in specs)
        {
            var password = _passwordResolver.Resolve(spec.PasswordSource);
            var roles = BuildRoleRefs(spec.Roles);

            if (await UserExistsAsync(db, spec.User, ct))
            {
                // updateUser refresca password y roles. Idempotente respecto al estado declarado.
                var updateCmd = new BsonDocument
                {
                    { "updateUser", spec.User },
                    { "pwd", password },
                    { "roles", roles },
                };
                await db.RunCommandAsync<BsonDocument>(updateCmd, cancellationToken: ct);
                result.UsersUpdated.Add(spec.User);
                _log.LogInformation("Usuario actualizado: {User}", spec.User);
            }
            else
            {
                var createCmd = new BsonDocument
                {
                    { "createUser", spec.User },
                    { "pwd", password },
                    { "roles", roles },
                };
                await db.RunCommandAsync<BsonDocument>(createCmd, cancellationToken: ct);
                result.UsersCreated.Add(spec.User);
                _log.LogInformation("Usuario creado: {User}", spec.User);
            }
        }
    }

    private static async Task<bool> UserExistsAsync(IMongoDatabase db, string userName, CancellationToken ct)
    {
        var cmd = new BsonDocument { { "usersInfo", userName } };
        var resp = await db.RunCommandAsync<BsonDocument>(cmd, cancellationToken: ct);
        return resp.TryGetValue("users", out var users)
            && users is BsonArray arr
            && arr.Count > 0;
    }
}
