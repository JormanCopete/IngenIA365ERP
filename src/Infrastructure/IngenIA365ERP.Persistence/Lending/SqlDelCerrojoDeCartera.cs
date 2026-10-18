using System.Globalization;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Persistence.Inventory;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Persistence.Lending;

/// <summary>
/// El SQL del cerrojo de Cartera para cada motor (feature 013, T34, T0206; data-model §0.10; feature 014, T031: el nivel «cuenta»). Puro: arma las sentencias y
/// <see cref="CerrojoDeCartera"/> las ejecuta sobre la conexión y la transacción del propio <c>ApplicationDbContext</c>. Lo prueba
/// <c>CerrojoDeCarteraSqlTests</c>. Molde: <see cref="SqlDelCerrojo"/> del inventario.
///
/// <para>
/// Las operaciones <b>esperan</b> (<c>FOR UPDATE</c> / <c>UPDLOCK, ROWLOCK, HOLDLOCK</c>) y el proceso <b>salta</b>
/// (<c>FOR UPDATE SKIP LOCKED</c> / <c>UPDLOCK, ROWLOCK, READPAST</c>). <c>LND_Setup</c> compartido es <c>FOR SHARE</c> /
/// <c>ROWLOCK, HOLDLOCK</c>; exclusivo, <c>FOR UPDATE</c> / <c>XLOCK, ROWLOCK, HOLDLOCK</c> (en SQL Server <c>UPDLOCK</c> convive
/// con el compartido: el cierre no esperaría a las operaciones en vuelo).
/// </para>
///
/// <para>
/// La fila de <c>LND_DebtorControls</c> que falta se crea con <c>INSERT … ON CONFLICT DO NOTHING</c> (PostgreSQL, sobre
/// <c>UK_LND_DebtorControls_PersonId</c>) o <c>INSERT … WHERE NOT EXISTS … WITH (UPDLOCK, HOLDLOCK)</c> (SQL Server). Ese INSERT no
/// pasa por EF ni por la auditoría: escribe él mismo <c>PublicId</c> (<c>gen_random_uuid()</c> / <c>NEWID()</c>), <c>CreatedAt</c>
/// (UTC, parámetro <c>{0}</c>), <c>CreatedBy</c> (nombre del actor, parámetro <c>{1}</c>) e <c>IsDeleted</c> falso. Los Ids son
/// enteros y van literales; ningún texto entra al SQL salvo como parámetro. (nuevo)
/// </para>
/// </summary>
public static class SqlDelCerrojoDeCartera
{
    public const string TablaSetup = "LND_Setup";
    public const string TablaDeudores = "LND_DebtorControls";
    public const string TablaCreditos = "LND_LoanControls";

    /// <summary>Feature 014 (T031): el nivel «cuenta», después del crédito y antes de la numeración.</summary>
    public const string TablaCuentas = "LND_MemberAccountControls";
    public const string TablaNumeracion = "LND_NumberSequences";

    private const string Esquema = "dbo";

    /// <summary>Paso 1: la fila única de <c>LND_Setup</c>, compartida o exclusiva.</summary>
    public static SentenciaDelCerrojo Setup(DatabaseProvider motor, bool exclusivo)
    {
        var q = new Dialecto(motor);
        var sql = q.Pg
            ? $"SELECT {q.C("Id")} FROM {q.T(TablaSetup)} ORDER BY {q.C("Id")} {(exclusivo ? "FOR UPDATE" : "FOR SHARE")};"
            : $"SELECT {q.C("Id")} FROM {q.T(TablaSetup)} WITH ({(exclusivo ? "XLOCK, ROWLOCK, HOLDLOCK" : "ROWLOCK, HOLDLOCK")}) ORDER BY {q.C("Id")};";
        return new(TablaSetup, sql, []);
    }

    /// <summary>
    /// Paso 2: el INSERT de las filas de deudor que falten y el bloqueo exclusivo por Id, en ese orden. Sin personas, ninguna
    /// sentencia.
    /// </summary>
    public static IReadOnlyList<SentenciaDelCerrojo> Deudores(DatabaseProvider motor, IEnumerable<int> personIds, ModoDeEspera modo,
        string actor, DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante de creación va en UTC.", nameof(ahoraUtc));
        ArgumentNullException.ThrowIfNull(actor);
        var ids = Ordenados(personIds);
        if (ids.Count == 0) return [];

        var q = new Dialecto(motor);
        var valores = $"(VALUES {string.Join(", ", ids.Select(i => $"({Literal(i)})"))}) AS v({q.C("PersonId")})";
        var columnas = string.Join(", ", new[] { "PublicId", "PersonId", "CreatedAt", "CreatedBy", "IsDeleted" }.Select(q.C));
        var insertar = q.Pg
            ? $"INSERT INTO {q.T(TablaDeudores)} ({columnas}) SELECT gen_random_uuid(), v.{q.C("PersonId")}, {{0}}, {{1}}, FALSE FROM {valores} ON CONFLICT DO NOTHING;"
            : $"INSERT INTO {q.T(TablaDeudores)} ({columnas}) SELECT NEWID(), v.{q.C("PersonId")}, {{0}}, {{1}}, 0 FROM {valores} "
              + $"WHERE NOT EXISTS (SELECT 1 FROM {q.T(TablaDeudores)} t WITH (UPDLOCK, HOLDLOCK) WHERE t.{q.C("PersonId")} = v.{q.C("PersonId")});";

        return
        [
            new(TablaDeudores, insertar, [ahoraUtc, actor]),
            new(TablaDeudores, q.BloquearPor(TablaDeudores, "Id", "PersonId", ids, modo), []),
        ];
    }

    /// <summary>
    /// Paso 3: los <c>LND_LoanControls</c> de los créditos, exclusivos, ordenados por Id y en una sentencia que devuelve los
    /// <c>LoanId</c> bloqueados (columna <c>Value</c>, para <c>SqlQueryRaw&lt;int&gt;</c>). Sin créditos, nulo.
    /// </summary>
    public static SentenciaDelCerrojo? Creditos(DatabaseProvider motor, IEnumerable<int> loanIds, ModoDeEspera modo)
    {
        var ids = Ordenados(loanIds);
        return ids.Count == 0 ? null : new(TablaCreditos, new Dialecto(motor).BloquearPor(TablaCreditos, "LoanId", "LoanId", ids, modo), []);
    }

    /// <summary>
    /// Paso 4 (feature 014, T031; R08, data-model §0.6): los <c>LND_MemberAccountControls</c> de las cuentas, exclusivos, ordenados por Id y en una
    /// sentencia que devuelve los <c>MemberAccountId</c> bloqueados (columna <c>Value</c>). Sin cuentas, nulo.
    /// </summary>
    public static SentenciaDelCerrojo? Cuentas(DatabaseProvider motor, IEnumerable<int> memberAccountIds, ModoDeEspera modo)
    {
        var ids = Ordenados(memberAccountIds);
        return ids.Count == 0
            ? null
            : new(TablaCuentas, new Dialecto(motor).BloquearPor(TablaCuentas, "MemberAccountId", "MemberAccountId", ids, modo), []);
    }

    /// <summary>Paso 5: la fila de numeración, exclusiva y al final.</summary>
    public static SentenciaDelCerrojo Numeracion(DatabaseProvider motor, int numberSequenceId) =>
        new(TablaNumeracion, new Dialecto(motor).BloquearPor(TablaNumeracion, "Id", "Id", [numberSequenceId], ModoDeEspera.Esperar), []);

    private static List<int> Ordenados(IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Distinct().Order().ToList();
    }

    private static string Literal(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>Lo único que cambia entre los dos motores: comillas y candados.</summary>
    private sealed class Dialecto(DatabaseProvider motor)
    {
        public bool Pg => motor == DatabaseProvider.PostgreSql;

        public string C(string identificador) => Pg ? $"\"{identificador}\"" : $"[{identificador}]";

        public string T(string tabla) => $"{C(Esquema)}.{C(tabla)}";

        /// <summary><c>SELECT {columna} AS Value … WHERE {filtro} IN (…) ORDER BY Id</c> con el candado exclusivo del modo.</summary>
        public string BloquearPor(string tabla, string columna, string filtro, IReadOnlyList<int> ids, ModoDeEspera modo)
        {
            var seleccion = $"{C(columna)} AS {C("Value")}";
            var donde = $" WHERE {C(filtro)} IN ({string.Join(", ", ids.Select(Literal))})";
            return Pg
                ? $"SELECT {seleccion} FROM {T(tabla)}{donde} ORDER BY {C("Id")} {(modo == ModoDeEspera.Saltar ? "FOR UPDATE SKIP LOCKED" : "FOR UPDATE")};"
                : $"SELECT {seleccion} FROM {T(tabla)} WITH ({(modo == ModoDeEspera.Saltar ? "UPDLOCK, ROWLOCK, READPAST" : "UPDLOCK, ROWLOCK, HOLDLOCK")}){donde} ORDER BY {C("Id")};";
        }
    }
}
