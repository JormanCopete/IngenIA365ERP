using System.Globalization;
using System.Text;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Persistence.Inventory;

/// <summary>Una sentencia del cerrojo: SQL con marcadores <c>{0}</c>, <c>{1}</c>… y sus valores. (nuevo)</summary>
public sealed record SentenciaDelCerrojo(string Tabla, string Sql, IReadOnlyList<object> Parametros);

/// <summary>
/// El SQL del cerrojo de inventario para cada motor (feature 012, T15, T138; data-model §0). Puro: arma las sentencias
/// en el orden canónico y <see cref="CerrojoDeInventario"/> las ejecuta con <c>ExecuteSqlRawAsync</c> sobre la
/// conexión y la transacción del propio <c>ApplicationDbContext</c>. Lo prueba <c>CerrojoDeInventarioSqlTests</c>.
///
/// <para>
/// Las filas de proyección que faltan se crean antes de bloquear: <c>INSERT … ON CONFLICT DO NOTHING</c> (PostgreSQL) o
/// <c>INSERT … WHERE NOT EXISTS … WITH (UPDLOCK, HOLDLOCK)</c> (SQL Server). Ese INSERT no pasa por EF ni por el
/// interceptor de auditoría, así que escribe él mismo <c>PublicId</c> (<c>gen_random_uuid()</c> / <c>NEWID()</c>),
/// <c>CreatedAt</c> (UTC, parámetro <c>{0}</c>), <c>CreatedBy</c> (el nombre del actor, parámetro <c>{1}</c>),
/// <c>IsDeleted</c> falso y ceros en cantidades y valores. Los Ids de las claves son enteros y van literales: ningún
/// texto de la persona entra al SQL salvo como parámetro.
/// </para>
/// </summary>
public static class SqlDelCerrojo
{
    public const string TablaSetup = "INV_Setup";
    public const string TablaBodegas = "INV_Warehouses";
    public const string TablaDocumentos = "INV_Documents";
    public const string TablaEstadosDeCosto = "INV_CostStates";
    public const string TablaExistencias = "INV_StockBalances";
    public const string TablaDetalles = "INV_StockDetails";
    public const string TablaSecuencias = "INV_DocumentSequences";

    private const string Esquema = "dbo";

    /// <summary>Las sentencias de los pasos 1 a 4, en el orden en que se ejecutan.</summary>
    public static IReadOnlyList<SentenciaDelCerrojo> Sentencias(DatabaseProvider motor, PedidoDeCerrojo pedido, string actor, DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("El instante de creación va en UTC.", nameof(ahoraUtc));

        var q = new Dialecto(motor);
        var creacion = new object[] { ahoraUtc, actor };
        var sentencias = new List<SentenciaDelCerrojo>();

        // 1. INV_Setup (fila única).
        var setupExclusivo = pedido.Setup == ModoDeBloqueoDelSetup.Exclusivo;
        sentencias.Add(new(TablaSetup, q.Bloquear(TablaSetup, null, setupExclusivo), []));

        // 2. INV_Warehouses, compartido.
        var bodegas = Ordenados(pedido.Bodegas);
        if (bodegas.Count > 0)
            sentencias.Add(new(TablaBodegas, q.Bloquear(TablaBodegas, bodegas, exclusivo: false), []));

        // 3. Documentos de origen, exclusivos.
        var origenes = Ordenados(pedido.DocumentosDeOrigen);
        if (origenes.Count > 0)
            sentencias.Add(new(TablaDocumentos, q.Bloquear(TablaDocumentos, origenes, exclusivo: true), []));

        // 4. Proyecciones: asegurar y bloquear, cada tabla en su turno.
        var costos = pedido.EstadosDeCosto.DistinctBy(c => (c.ProductId, c.ScopeWarehouseId))
            .OrderBy(c => c.ProductId).ThenBy(c => c.ScopeWarehouseId).ToList();
        if (costos.Count > 0)
        {
            var valores = costos.Select(c => new object?[] { c.ProductId, c.ScopeWarehouseId, (int)c.Method }).ToList();
            string[] clave = ["ProductId", "ScopeWarehouseId"];
            sentencias.Add(new(TablaEstadosDeCosto, q.Asegurar(TablaEstadosDeCosto,
                ["ProductId", "ScopeWarehouseId", "Method"], clave,
                ["Quantity", "Value", "AverageCost", "LastUnitCost"], valores), creacion));
            sentencias.Add(new(TablaEstadosDeCosto, q.BloquearPorClave(TablaEstadosDeCosto, clave,
                valores.Select(v => v[..2]).ToList()), []));
        }

        var existencias = pedido.Existencias.Distinct()
            .OrderBy(e => e.ProductId).ThenBy(e => e.WarehouseId).ToList();
        if (existencias.Count > 0)
        {
            var valores = existencias.Select(e => new object?[] { e.ProductId, e.WarehouseId }).ToList();
            string[] clave = ["ProductId", "WarehouseId"];
            sentencias.Add(new(TablaExistencias, q.Asegurar(TablaExistencias, clave, clave, ["Physical", "Reserved"], valores), creacion));
            sentencias.Add(new(TablaExistencias, q.BloquearPorClave(TablaExistencias, clave, valores), []));
        }

        var detalles = pedido.Detalles.Distinct()
            .OrderBy(d => d.ProductId).ThenBy(d => d.WarehouseId).ThenBy(d => d.LocationId).ThenBy(d => d.LotId ?? 0).ToList();
        if (detalles.Count > 0)
        {
            var valores = detalles.Select(d => new object?[] { d.ProductId, d.WarehouseId, d.LocationId, d.LotId }).ToList();
            string[] clave = ["ProductId", "WarehouseId", "LocationId", "LotId"];
            sentencias.Add(new(TablaDetalles, q.Asegurar(TablaDetalles, clave, clave, ["Quantity"], valores), creacion));
            sentencias.Add(new(TablaDetalles, q.BloquearPorClave(TablaDetalles, clave, valores), []));
        }

        return sentencias;
    }

    /// <summary>La sentencia del paso 5: la fila de numeración, exclusiva y al final.</summary>
    public static SentenciaDelCerrojo Numeracion(DatabaseProvider motor, int documentSequenceId) =>
        new(TablaSecuencias, new Dialecto(motor).Bloquear(TablaSecuencias, [documentSequenceId], exclusivo: true), []);

    private static List<int> Ordenados(IEnumerable<int> ids) => ids.Distinct().Order().ToList();

    /// <summary>Lo único que cambia entre los dos motores: comillas, el candado y el valor de verdad.</summary>
    private sealed class Dialecto(DatabaseProvider motor)
    {
        private bool Pg => motor == DatabaseProvider.PostgreSql;

        private string C(string identificador) => Pg ? $"\"{identificador}\"" : $"[{identificador}]";

        private string T(string tabla) => $"{C(Esquema)}.{C(tabla)}";

        private string Falso => Pg ? "FALSE" : "0";

        private string NuevoGuid => Pg ? "gen_random_uuid()" : "NEWID()";

        /// <summary><c>SELECT Id … ORDER BY Id</c> con el candado de cada motor; sin ids, toda la tabla (el setup).</summary>
        public string Bloquear(string tabla, IReadOnlyList<int>? ids, bool exclusivo)
        {
            var filtro = ids is null ? "" : $" WHERE {C("Id")} IN ({string.Join(", ", ids.Select(i => Literal(i)))})";
            return Pg
                ? $"SELECT {C("Id")} FROM {T(tabla)}{filtro} ORDER BY {C("Id")} {(exclusivo ? "FOR UPDATE" : "FOR SHARE")};"
                : $"SELECT {C("Id")} FROM {T(tabla)} WITH ({(exclusivo ? "UPDLOCK, ROWLOCK, HOLDLOCK" : "ROWLOCK, HOLDLOCK")}){filtro} ORDER BY {C("Id")};";
        }

        /// <summary>Bloqueo exclusivo de las filas cuyas claves están en la lista, por <c>Id</c>.</summary>
        public string BloquearPorClave(string tabla, IReadOnlyList<string> clave, IReadOnlyList<object?[]> filas)
        {
            var join = Unir(clave);
            return Pg
                ? $"SELECT t.{C("Id")} FROM {T(tabla)} t JOIN {Valores(filas, clave)} ON {join} ORDER BY t.{C("Id")} FOR UPDATE OF t;"
                : $"SELECT t.{C("Id")} FROM {T(tabla)} t WITH (UPDLOCK, ROWLOCK, HOLDLOCK) JOIN {Valores(filas, clave)} ON {join} ORDER BY t.{C("Id")};";
        }

        /// <summary>Crea las filas que falten con auditoría propia y ceros en <paramref name="ceros"/>.</summary>
        public string Asegurar(string tabla, IReadOnlyList<string> columnas, IReadOnlyList<string> clave,
            IReadOnlyList<string> ceros, IReadOnlyList<object?[]> filas)
        {
            var destino = new[] { "PublicId" }.Concat(columnas).Concat(ceros).Concat(["CreatedAt", "CreatedBy", "IsDeleted"]);
            var origen = new[] { NuevoGuid }.Concat(columnas.Select(c => $"v.{C(c)}")).Concat(ceros.Select(_ => "0"))
                .Concat(["{0}", "{1}", Falso]);

            var sql = new StringBuilder()
                .Append($"INSERT INTO {T(tabla)} ({string.Join(", ", destino.Select(C))}) ")
                .Append($"SELECT {string.Join(", ", origen)} FROM {Valores(filas, columnas)}");

            if (Pg)
                sql.Append(" ON CONFLICT DO NOTHING;");
            else
                sql.Append($" WHERE NOT EXISTS (SELECT 1 FROM {T(tabla)} t WITH (UPDLOCK, HOLDLOCK) WHERE {Unir(clave)});");
            return sql.ToString();
        }

        /// <summary><c>(VALUES (…), (…)) AS v(col, …)</c>; un nulo va tipado para que los dos motores infieran int.</summary>
        private string Valores(IReadOnlyList<object?[]> filas, IReadOnlyList<string> columnas) =>
            $"(VALUES {string.Join(", ", filas.Select(f => "(" + string.Join(", ", f.Select(Literal)) + ")"))}) AS v({string.Join(", ", columnas.Select(C))})";

        /// <summary><c>t.col = v.col</c> para cada columna de la clave; las anulables (lote) comparan nulo con nulo.</summary>
        private string Unir(IReadOnlyList<string> clave) => string.Join(" AND ", clave.Select(c => c == "LotId"
            ? $"(t.{C(c)} = v.{C(c)} OR (t.{C(c)} IS NULL AND v.{C(c)} IS NULL))"
            : $"t.{C(c)} = v.{C(c)}"));

        private static string Literal(object? valor) => valor switch
        {
            null => "CAST(NULL AS int)",
            int i => i.ToString(CultureInfo.InvariantCulture),
            _ => throw new ArgumentException($"El cerrojo sólo lleva enteros literales; recibió {valor.GetType().Name}."),
        };
    }
}
