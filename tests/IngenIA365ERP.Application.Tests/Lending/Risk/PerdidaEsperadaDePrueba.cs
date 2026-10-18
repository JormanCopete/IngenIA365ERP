using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Las piezas de las pruebas de K6a (feature 013, fase 22, bloque 2): la regla de pérdida esperada y el Anexo 2 sembrados replicados con la vigencia
/// que se pida (el Anexo 2, opcionalmente con una PI <c>VOLUNTARIA</c> inventada —copia la de las cooperativas de ahorro y crédito—, porque la
/// semilla no la trae: la de la CE 100 está por registrar), el perfil regulatorio del doble, el SMMLV y un punto de acta cerrado. (nuevo)
/// </summary>
internal static class PerdidaEsperadaDePrueba
{
    private static long _secuencia = 95_000;

    public static async Task ReglaAsync(TestApplicationDbContext db, DateTime ahora, DateOnly desde) =>
        await DocumentoAsync(db, ahora, RegulatoryDocumentKind.ReglaDePerdidaEsperada, "CE100-PRUEBA",
            PlatformNationalValuesSeeder.Contenido("regla-de-perdida-esperada.json"), desde);

    public static async Task Anexo2Async(TestApplicationDbContext db, DateTime ahora, DateOnly desde, bool conVoluntaria = false) =>
        await DocumentoAsync(db, ahora, RegulatoryDocumentKind.SarcAnexo2, "ANEXO2-PRUEBA", Anexo2(conVoluntaria), desde);

    /// <summary>El Anexo 2 sembrado; con <paramref name="conVoluntaria"/>, más una PI <c>VOLUNTARIA</c> igual a la de COOP_AHORRO_CREDITO (inventada).</summary>
    public static string Anexo2(bool conVoluntaria)
    {
        var contenido = PlatformNationalValuesSeeder.Contenido("sarc-anexo-2.json");
        if (!conVoluntaria) return contenido;
        var lineas = contenido.Split('\n').ToList();
        for (var i = lineas.Count - 1; i >= 0; i--)
        {
            if (!lineas[i].Contains("\"organizationType\": \"COOP_AHORRO_CREDITO\"", StringComparison.Ordinal)) continue;
            lineas.Insert(i + 2, lineas[i].Replace("COOP_AHORRO_CREDITO", Anexo2Clave, StringComparison.Ordinal));
            lineas.Insert(i + 3, lineas[i + 1]);
        }
        return string.Join('\n', lineas);
    }

    private const string Anexo2Clave = Domain.Lending.Risk.Normativa.Anexo2.ClaveVoluntaria;

    public static async Task DocumentoAsync(TestApplicationDbContext db, DateTime ahora, RegulatoryDocumentKind clase, string codigo, string contenido,
        DateOnly desde)
    {
        var secuencia = Interlocked.Increment(ref _secuencia);
        var replica = new NationalValueReplication
        {
            FromSequence = secuencia, ToSequence = secuencia, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = ahora.AddDays(-1), FinishedAt = ahora.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = clase.ToString(), Code = codigo, ValidFrom = desde, Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = false,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = secuencia,
            CentralRegisteredAt = ahora.AddDays(-1), KnownAt = ahora.AddDays(-1),
        });
        db.NationalValueReplications.Add(replica);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    /// <summary>El perfil regulatorio que devuelve el doble de la escena, desde 2026-01-01.</summary>
    public static void Perfil(GobiernoDePrueba g, string tipo) =>
        g.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), tipo, true, 1, null, 2, false, null, null,
                "Resolución de prueba", null, "Perfil de prueba", null, "Prueba", g.UtcNow.AddYears(-1)));

    /// <summary>El SMMLV del doble de valores nacionales.</summary>
    public static void Smmlv(GobiernoDePrueba g, decimal valor = 1_423_500m) =>
        g.Valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Smmlv, string.Empty, valor, "Pesos",
                null, new DateOnly(2026, 1, 1), null, null, "Decreto de prueba", 1, g.UtcNow.AddYears(-1), EstadoDeRevision.Validada)));

    /// <summary>Un punto <c>Decision</c> aprobado de una sesión cerrada del consejo sobre <paramref name="sujeto"/>.</summary>
    public static Guid Acta(TestApplicationDbContext db, DateOnly fecha, Guid sujeto, string tipo = LendingDecisionSubjects.ImpairmentModelChange)
    {
        var organo = new GoverningBody
        {
            Code = "CONSEJO" + db.GoverningBodies.Count(), Name = "Consejo de administración", Kind = GoverningBodyKind.BoardOfDirectors, MinutesPrefix = "CA-",
            CreatedBy = "test",
        };
        db.GoverningBodies.Add(organo);
        db.SaveChanges();
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = fecha, Status = SessionStatus.Closed, CreatedBy = "test" };
        db.LendingSessions.Add(sesion);
        db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = tipo, SubjectPublicId = sujeto,
            Title = "Cambio del modelo de deterioro", Outcome = CreditDecisionOutcome.Approve, CreatedBy = "test",
        };
        db.SessionItems.Add(punto);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return punto.PublicId;
    }
}
