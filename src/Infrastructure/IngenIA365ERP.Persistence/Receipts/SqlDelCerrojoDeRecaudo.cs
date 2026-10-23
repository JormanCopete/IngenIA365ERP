using System.Globalization;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Persistence.Inventory;
using IngenIA365ERP.Persistence.Providers;

namespace IngenIA365ERP.Persistence.Receipts;

/// <summary>
/// El SQL de los cerrojos de Recaudo y de Tesorería para cada motor (feature 015, T0038; R-12; data-model §0.5). Puro: arma las sentencias y
/// <see cref="CerrojoDeRecaudo"/> y <c>CerrojoDeTesoreria</c> las ejecutan sobre la conexión y la transacción del propio contexto. Lo prueban
/// <c>CerrojoDeRecaudoTests</c> y <c>CerrojoDeTesoreriaTests</c>. Molde: <c>SqlDelCerrojoDeCartera</c>.
///
/// <para>
/// Exclusivo: <c>FOR UPDATE</c> en PostgreSQL y <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c> en SQL Server. Compartido: <c>FOR SHARE</c> y
/// <c>WITH (ROWLOCK, HOLDLOCK)</c>. La configuración exclusiva usa <c>XLOCK</c> en SQL Server (con <c>UPDLOCK</c> convive con la compartida).
/// Una sentencia por nivel, ordenada por <c>Id</c>; los Ids son enteros y van literales, ningún texto entra al SQL salvo como parámetro.
/// La fila de <c>RCD_PayerControls</c> que falta se crea con <c>INSERT … ON CONFLICT DO NOTHING</c> o <c>INSERT … WHERE NOT EXISTS … WITH
/// (UPDLOCK, HOLDLOCK)</c>, como la del deudor en Cartera; la de numeración en <c>COR_Sequences</c>, igual, sobre
/// <c>UK_COR_Sequences_Name_RcdTes</c>. (nuevo)
/// </para>
/// </summary>
public static class SqlDelCerrojoDeRecaudo
{
    // Los niveles de Recaudo en el orden de data-model §0.5 (sin el 7, que toman los contratos, ni el 8, el puerto de Tesorería).
    public const string TablaSetup = "RCD_Setup";
    public const string TablaDiasDelPunto = "RCD_CollectionPointDays";
    public const string TablaSesiones = "RCD_CashSessions";
    public const string TablaBovedas = "RCD_Vaults";
    public const string TablaPagadores = "RCD_PayerControls";
    public const string TablaSaldosAFavor = "RCD_PayerCreditBalances";
    public const string TablaPartidas = "RCD_UnidentifiedItems";
    public const string TablaLineasDeGiro = "RCD_PaymasterRemittanceLines";
    public const string TablaCheques = "RCD_ReceivedChecks";
    public const string TablaRecibos = "RCD_Receipts";
    public const string TablaNumeracion = "COR_Sequences";

    /// <summary>Las tablas que admite cada nivel de <see cref="ICerrojoDeRecaudo.BloquearAsync"/>.</summary>
    public static readonly IReadOnlyDictionary<NivelDeRecaudo, IReadOnlyList<string>> TablasDelNivel = new Dictionary<NivelDeRecaudo, IReadOnlyList<string>>
    {
        [NivelDeRecaudo.DiaDelPunto] = [TablaDiasDelPunto],
        [NivelDeRecaudo.Sesion] = [TablaSesiones],
        [NivelDeRecaudo.Boveda] = [TablaBovedas],
        [NivelDeRecaudo.Origenes] = [TablaSaldosAFavor, TablaPartidas, TablaLineasDeGiro, TablaCheques, TablaRecibos],
    };

    /// <summary>Nivel 1: la fila única de la configuración, compartida o exclusiva.</summary>
    public static SentenciaDelCerrojo Setup(DatabaseProvider motor, string tabla, bool exclusivo)
    {
        var q = new Dialecto(motor);
        var sql = q.Pg
            ? $"SELECT {q.C("Id")} FROM {q.T(tabla)} ORDER BY {q.C("Id")} {(exclusivo ? "FOR UPDATE" : "FOR SHARE")};"
            : $"SELECT {q.C("Id")} FROM {q.T(tabla)} WITH ({(exclusivo ? "XLOCK, ROWLOCK, HOLDLOCK" : "ROWLOCK, HOLDLOCK")}) ORDER BY {q.C("Id")};";
        return new(tabla, sql, []);
    }

    /// <summary>Las filas por <c>Id</c> de una tabla, en una sentencia ordenada por <c>Id</c>. Sin Ids, nula.</summary>
    public static SentenciaDelCerrojo? Filas(DatabaseProvider motor, string tabla, IEnumerable<long> ids, bool exclusivo)
    {
        var lista = Ordenados(ids);
        if (lista.Count == 0) return null;
        var q = new Dialecto(motor);
        var donde = $" WHERE {q.C("Id")} IN ({string.Join(", ", lista.Select(Literal))})";
        var sql = q.Pg
            ? $"SELECT {q.C("Id")} FROM {q.T(tabla)}{donde} ORDER BY {q.C("Id")} {(exclusivo ? "FOR UPDATE" : "FOR SHARE")};"
            : $"SELECT {q.C("Id")} FROM {q.T(tabla)} WITH ({(exclusivo ? "UPDLOCK, ROWLOCK, HOLDLOCK" : "ROWLOCK, HOLDLOCK")}){donde} ORDER BY {q.C("Id")};";
        return new(tabla, sql, []);
    }

    /// <summary>
    /// Nivel 5: el INSERT de las filas de pagador que falten y el bloqueo exclusivo, en ese orden. Los parámetros son el instante UTC y el actor.
    /// Sin personas, ninguna sentencia.
    /// </summary>
    public static IReadOnlyList<SentenciaDelCerrojo> Pagadores(DatabaseProvider motor, IEnumerable<int> personIds, string actor, DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante de creación va en UTC.", nameof(ahoraUtc));
        ArgumentNullException.ThrowIfNull(actor);
        var ids = personIds.Distinct().Order().ToList();
        if (ids.Count == 0) return [];

        var q = new Dialecto(motor);
        var valores = $"(VALUES {string.Join(", ", ids.Select(i => $"({Literal(i)})"))}) AS v({q.C("PersonId")})";
        var columnas = string.Join(", ", new[] { "PublicId", "PersonId", "CreatedAt", "CreatedBy", "IsDeleted" }.Select(q.C));
        var insertar = q.Pg
            ? $"INSERT INTO {q.T(TablaPagadores)} ({columnas}) SELECT gen_random_uuid(), v.{q.C("PersonId")}, {{0}}, {{1}}, FALSE FROM {valores} ON CONFLICT DO NOTHING;"
            : $"INSERT INTO {q.T(TablaPagadores)} ({columnas}) SELECT NEWID(), v.{q.C("PersonId")}, {{0}}, {{1}}, 0 FROM {valores} "
              + $"WHERE NOT EXISTS (SELECT 1 FROM {q.T(TablaPagadores)} t WITH (UPDLOCK, HOLDLOCK) WHERE t.{q.C("PersonId")} = v.{q.C("PersonId")});";
        var donde = $" WHERE {q.C("PersonId")} IN ({string.Join(", ", ids.Select(i => Literal(i)))})";
        var bloquear = q.Pg
            ? $"SELECT {q.C("Id")} FROM {q.T(TablaPagadores)}{donde} ORDER BY {q.C("Id")} FOR UPDATE;"
            : $"SELECT {q.C("Id")} FROM {q.T(TablaPagadores)} WITH (UPDLOCK, ROWLOCK, HOLDLOCK){donde} ORDER BY {q.C("Id")};";
        return [new(TablaPagadores, insertar, [ahoraUtc, actor]), new(TablaPagadores, bloquear, [])];
    }

    /// <summary>La fila de numeración, exclusiva y al final.</summary>
    public static SentenciaDelCerrojo Numeracion(DatabaseProvider motor, int sequenceId) =>
        Filas(motor, TablaNumeracion, [sequenceId], exclusivo: true)!;

    /// <summary>
    /// El INSERT de la fila de numeración que falta (<c>NextSequence = 1</c>), sin error si otra operación la acaba de crear. Los parámetros son,
    /// en orden, el instante UTC, el actor, la clave (<c>Name</c>), el prefijo (<c>ShortName</c>) y el módulo (<c>DocumentType</c>: <c>RCD</c> o
    /// <c>TES</c>).
    /// </summary>
    public static SentenciaDelCerrojo NumeracionNueva(DatabaseProvider motor, string clave, string prefijo, string modulo, string actor, DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("El instante de creación va en UTC.", nameof(ahoraUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(prefijo);
        ArgumentNullException.ThrowIfNull(actor);
        if (modulo is not ("RCD" or "TES")) throw new ArgumentException("La numeración de esta feature es de RCD o TES.", nameof(modulo));
        var q = new Dialecto(motor);
        var columnas = string.Join(", ", new[] { "PublicId", "Name", "ShortName", "DocumentType", "IsAutomatic", "NextSequence", "CreatedAt", "CreatedBy", "IsDeleted" }
            .Select(q.C));
        var sql = q.Pg
            ? $"INSERT INTO {q.T(TablaNumeracion)} ({columnas}) VALUES (gen_random_uuid(), {{2}}, {{3}}, {{4}}, TRUE, 1, {{0}}, {{1}}, FALSE) ON CONFLICT DO NOTHING;"
            : $"INSERT INTO {q.T(TablaNumeracion)} ({columnas}) SELECT NEWID(), {{2}}, {{3}}, {{4}}, 1, 1, {{0}}, {{1}}, 0 "
              + $"WHERE NOT EXISTS (SELECT 1 FROM {q.T(TablaNumeracion)} t WITH (UPDLOCK, HOLDLOCK) WHERE t.{q.C("Name")} = {{2}} AND t.{q.C("DocumentType")} IN ('RCD', 'TES'));";
        return new(TablaNumeracion, sql, [ahoraUtc, actor, clave, prefijo, modulo]);
    }

    private static List<long> Ordenados(IEnumerable<long> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Distinct().Order().ToList();
    }

    private static string Literal(long valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>Lo único que cambia entre los dos motores: comillas.</summary>
    internal sealed class Dialecto(DatabaseProvider motor)
    {
        public bool Pg => motor == DatabaseProvider.PostgreSql;

        public string C(string identificador) => Pg ? $"\"{identificador}\"" : $"[{identificador}]";

        public string T(string tabla) => $"{C("dbo")}.{C(tabla)}";
    }
}
