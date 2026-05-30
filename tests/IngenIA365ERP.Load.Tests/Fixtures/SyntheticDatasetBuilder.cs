namespace IngenIA365ERP.Load.Tests.Fixtures;

/// <summary>
/// T132 — Dataset sintético para load tests: 1.000 users, 50 roles,
/// 50.000 audit events. La construcción es idempotente: el builder
/// detecta si ya hay datos y solo agrega lo faltante (útil para nightly
/// que reusa el dataset de la corrida anterior).
///
/// <para>
/// El builder NO se ejecuta in-process desde NBomber — se invoca antes
/// de los escenarios via <c>dotnet run --project ... -- seed</c> o desde
/// un step inicial del CI. Aquí solo defininimos las constantes y una API
/// declarativa que el seeder (futuro) implementa.
/// </para>
/// </summary>
public static class SyntheticDatasetBuilder
{
    public const int UserCount = 1_000;
    public const int RoleCount = 50;
    public const int AuditEventCount = 50_000;

    public static DatasetSpec Default => new(
        TenantSubdomain: "loadtest",
        Users: UserCount,
        Roles: RoleCount,
        AuditEvents: AuditEventCount,
        PasswordForAllUsers: "LoadTest#2026!");
}

public sealed record DatasetSpec(
    string TenantSubdomain,
    int Users,
    int Roles,
    int AuditEvents,
    string PasswordForAllUsers);
