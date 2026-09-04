using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Audit.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace IngenIA365ERP.Application.Tests.Audit;

/// <summary>
/// La colección <c>audit_events</c> guarda dos formas de documento: la canónica en
/// camelCase (la que indexa y expira) y la heredada en PascalCase que escribió el
/// class map de <c>AuditLog</c> hasta el 2026-09-04. La consola de auditoría
/// devolvía 500 al encontrar la primera —<c>FormatException: Element 'tenantId'
/// does not match any field or property of class AuditLog</c>— porque leía con el
/// class map. Estas pruebas fijan que la lectura entiende las dos y la escritura
/// produce una sola.
/// </summary>
public class AuditDocumentSchemaTests
{
    private static readonly DateTime Instante = new(2026, 9, 4, 20, 40, 46, DateTimeKind.Utc);

    // ------------------------------------------------------------ lectura --

    [Fact]
    public void LeeUnDocumentoCanonico_ComoLoEscribeAppendOnlyAuditWriter()
    {
        var doc = new BsonDocument
        {
            { "_id", ObjectId.Parse("66d8a1b2c3d4e5f60718293a") },
            { "tenantId", "c6c8da53-6a9a-4c95-911c-6b732858af70" },
            { "userId", "ed3420f6-1cb9-4897-839b-395ab54f6971" },
            { "userName", "jorman@example.com" },
            { "action", "Invitation.Accepted" },
            { "entityType", "Invitation" },
            { "entityPublicId", "8c2a1f00-0000-0000-0000-000000000001" },
            { "module", "Identity" },
            { "oldValuesJson", "" },
            { "newValuesJson", "{\"Status\":\"Accepted\"}" },
            { "changedFields", new BsonArray(new[] { "Status", "AcceptedAt" }) },
            { "ipAddress", "190.1.2.3" },
            { "userAgent", "Mozilla" },
            { "endpoint", "/api/invitations/accept" },
            { "httpMethod", "POST" },
            { "httpStatusCode", 200 },
            { "durationMs", 941L },
            { "occurredAt", Instante },
        };

        var e = AuditDocumentSchema.ToEntry(doc);

        e.Id.Should().Be("66d8a1b2c3d4e5f60718293a");
        e.TenantId.Should().Be("c6c8da53-6a9a-4c95-911c-6b732858af70");
        e.UserId.Should().Be("ed3420f6-1cb9-4897-839b-395ab54f6971");
        e.UserName.Should().Be("jorman@example.com");
        e.Action.Should().Be("Invitation.Accepted");
        e.EntityType.Should().Be("Invitation");
        e.EntityId.Should().Be("8c2a1f00-0000-0000-0000-000000000001");
        e.Module.Should().Be("Identity");
        e.OldValues.Should().BeNull("una cadena vacía es «sin valores», no un JSON vacío");
        e.NewValues.Should().Be("{\"Status\":\"Accepted\"}");
        e.ChangedFields.Should().Equal("Status", "AcceptedAt");
        e.IpAddress.Should().Be("190.1.2.3");
        e.Endpoint.Should().Be("/api/invitations/accept");
        e.DurationMs.Should().Be(941);
        e.Timestamp.Should().Be(Instante);
        e.Timestamp.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void LeeUnDocumentoHeredado_ComoLoEscribiaElClassMapDeAuditLog()
    {
        // Exactamente lo que insertaba MongoAuditService antes: PascalCase,
        // Timestamp en vez de occurredAt, EntityId en vez de entityPublicId, y los
        // valores como subdocumentos en vez de JSON en texto.
        var doc = new BsonDocument
        {
            { "_id", ObjectId.Parse("66d8a1b2c3d4e5f60718293b") },
            { "TenantId", "Global" },
            { "UserId", "1" },
            { "UserName", "master@ingenia365.com" },
            { "Action", "Update" },
            { "EntityType", "TenantMfaPolicy" },
            { "EntityId", "42" },
            { "Module", "Security" },
            { "OldValues", new BsonDocument("IsRequired", false) },
            { "NewValues", new BsonDocument("IsRequired", true) },
            { "ChangedFields", new BsonArray(new[] { "IsRequired" }) },
            { "IpAddress", "10.0.0.1" },
            { "UserAgent", "curl" },
            { "Endpoint", "/api/tenants/x/mfa-policy" },
            { "HttpMethod", "PUT" },
            { "HttpStatusCode", 200 },
            { "DurationMs", 12L },
            { "Timestamp", Instante },
            { "Metadata", BsonNull.Value },
        };

        var e = AuditDocumentSchema.ToEntry(doc);

        e.TenantId.Should().Be("Global");
        e.UserName.Should().Be("master@ingenia365.com");
        e.Action.Should().Be("Update");
        e.EntityType.Should().Be("TenantMfaPolicy");
        e.EntityId.Should().Be("42");
        e.OldValues.Should().Be("{ \"IsRequired\" : false }");
        e.NewValues.Should().Be("{ \"IsRequired\" : true }");
        e.ChangedFields.Should().Equal("IsRequired");
        e.DurationMs.Should().Be(12);
        e.Timestamp.Should().Be(Instante);
    }

    [Fact]
    public void UnElementoDesconocido_NoEsUnError()
    {
        // Es lo que el class map se negaba a hacer. Un campo nuevo en el escritor
        // no puede volver a tumbar la consola.
        var doc = new BsonDocument
        {
            { "tenantId", "t" },
            { "action", "X" },
            { "entityType", "Y" },
            { "occurredAt", Instante },
            { "campoQueNadieConoce", "sorpresa" },
        };

        var act = () => AuditDocumentSchema.ToEntry(doc);

        act.Should().NotThrow();
        act().Action.Should().Be("X");
    }

    [Fact]
    public void UnDocumentoSinFechaNiNumeros_DevuelveValoresNeutros_NoLanza()
    {
        var doc = new BsonDocument { { "action", "X" }, { "entityType", "Y" } };

        var e = AuditDocumentSchema.ToEntry(doc);

        e.Id.Should().BeEmpty();
        e.TenantId.Should().BeNull();
        e.ChangedFields.Should().BeNull();
        e.DurationMs.Should().Be(0);
        e.Timestamp.Should().Be(DateTime.MinValue);
    }

    // ----------------------------------------------------------- escritura --

    [Fact]
    public void ElEventoDeIdentidad_SeEscribeSoloConNombresCanonicos()
    {
        var evento = new AuditEventDocument(
            TenantId: "t", UserId: "u", UserName: null, Action: "Login.Ok",
            EntityType: "CentralUser", EntityPublicId: "p", Module: "Identity",
            OldValuesJson: null, NewValuesJson: "{}", ChangedFields: null,
            IpAddress: null, UserAgent: null, Endpoint: "/api/auth/login",
            HttpMethod: "POST", HttpStatusCode: null, DurationMs: null,
            OccurredAt: Instante);

        var doc = AuditDocumentSchema.ToDocument(evento);

        doc.Names.Should().OnlyContain(n => char.IsLower(n[0]),
            "ningún nombre PascalCase puede volver a entrar en la colección");
        doc["userName"].AsString.Should().BeEmpty("un UserName null no puede lanzar ni escribir BsonNull");
        doc["changedFields"].AsBsonArray.Should().BeEmpty();
        doc["httpStatusCode"].AsInt32.Should().Be(0);
        doc["durationMs"].AsInt64.Should().Be(0);
        doc["occurredAt"].ToUniversalTime().Should().Be(Instante);
    }

    [Fact]
    public void LaEntradaDeLaCola_SeEscribeIgualQueElEventoDeIdentidad()
    {
        var entrada = new AuditLog
        {
            TenantId = "t",
            UserId = "u",
            UserName = "n",
            Action = "Update",
            EntityType = "Person",
            EntityId = "77",
            Module = "Core",
            OldValues = new BsonDocument("Name", "a"),
            NewValues = new BsonDocument("Name", "b"),
            ChangedFields = ["Name"],
            DurationMs = 5,
            Timestamp = Instante,
            Metadata = new BsonDocument("origen", "prueba"),
        };

        var doc = AuditDocumentSchema.ToDocument(entrada);

        doc.Names.Should().OnlyContain(n => char.IsLower(n[0]));
        doc.Contains("Timestamp").Should().BeFalse();
        doc.Contains("EntityId").Should().BeFalse();
        doc["entityPublicId"].AsString.Should().Be("77");
        doc["oldValuesJson"].AsString.Should().Be("{ \"Name\" : \"a\" }", "los valores van como JSON en texto, como en el otro escritor");
        doc["occurredAt"].ToUniversalTime().Should().Be(Instante);
        doc["metadata"].AsBsonDocument["origen"].AsString.Should().Be("prueba");

        // Y lo que se escribe se vuelve a leer igual: ida y vuelta.
        var e = AuditDocumentSchema.ToEntry(doc);
        e.EntityId.Should().Be("77");
        e.NewValues.Should().Be("{ \"Name\" : \"b\" }");
        e.Timestamp.Should().Be(Instante);
    }

    // ------------------------------------------------------------ filtros --

    [Fact]
    public void ElFiltroPideCadaCampo_PorSuNombreCanonicoOElHeredado()
    {
        var q = new AuditQueryParameters
        {
            UserId = "u1",
            Module = "Security",
            From = Instante.AddDays(-1),
            To = Instante,
        };

        var json = Render(AuditDocumentSchema.Filtro(q)).ToJson();

        json.Should().Contain("\"userId\" : \"u1\"").And.Contain("\"UserId\" : \"u1\"");
        json.Should().Contain("\"module\" : \"Security\"").And.Contain("\"Module\" : \"Security\"");
        json.Should().Contain("\"occurredAt\"").And.Contain("\"Timestamp\"");
        json.Should().Contain("$or");
    }

    [Fact]
    public void SinCondiciones_ElFiltroEsVacio()
    {
        Render(AuditDocumentSchema.Filtro(new AuditQueryParameters()))
            .ElementCount.Should().Be(0);
    }

    [Fact]
    public void ElOrdenPoneLosCanonicosPrimero_YLuegoLosHeredados()
    {
        var orden = AuditDocumentSchema.MasRecientePrimero
            .Render(new RenderArgs<BsonDocument>(
                BsonSerializer.LookupSerializer<BsonDocument>(), BsonSerializer.SerializerRegistry));

        orden.Names.Should().Equal("occurredAt", "Timestamp");
        orden["occurredAt"].AsInt32.Should().Be(-1);
        orden["Timestamp"].AsInt32.Should().Be(-1);
    }

    private static BsonDocument Render(FilterDefinition<BsonDocument> filtro) =>
        filtro.Render(new RenderArgs<BsonDocument>(
            BsonSerializer.LookupSerializer<BsonDocument>(), BsonSerializer.SerializerRegistry));
}
