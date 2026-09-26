using FluentAssertions;
using IngenIA365ERP.Application.Audit.VerifyIntegrity;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Audit.Integrity;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Audit;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit;

/// <summary>
/// Feature 012, T411 (US12-5; T38, FR-008): la verificación de integridad de la auditoría sobre una cadena en memoria
/// sellada con <see cref="SelloDeIntegridad"/> y un lector falso de Mongo. El testigo SQL (<c>COR_AuditOutbox</c>) tiene
/// los <c>seq</c> 1..5; lo que se toca es el lado Mongo: un campo cambiado es <c>Altered</c>, un <c>seq</c> que falta es
/// <c>Deleted</c>, otro documento en la misma posición es <c>Interleaved</c>, un ancla con HMAC inválido es
/// <c>AnchorInvalid</c> y un evento que falta por vencer su plazo es <c>PurgedByRetention</c>. El rango de más de diez
/// años no pasa el validador, y la verificación queda en la auditoría con su resultado aunque no haya incidentes.
/// </summary>
public class VerifyAuditIntegrityQueryHandlerTests
{
    private static readonly Guid Cooperativa = Guid.NewGuid();
    private static readonly string Flujo = AuditoriaEncadenada.Flujo(Cooperativa);
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Inicio = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentTenantService _cooperativa = Substitute.For<ICurrentTenantService>();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly LectorEnMemoria _mongo = new();
    private readonly List<AuditLogCommand> _registrados = [];

    public VerifyAuditIntegrityQueryHandlerTests()
    {
        _cooperativa.TenantId.Returns(Cooperativa.ToString("N"));
        _reloj.UtcNow.Returns(Ahora);
        _auditoria.LogAsync(Arg.Do<AuditLogCommand>(_registrados.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    /// <summary>Sella <paramref name="cuantos"/> eventos: fila delgada en SQL y documento en el Mongo falso.</summary>
    private void Sellar(int cuantos, DateTime? primero = null)
    {
        var previo = SelloDeIntegridad.HashInicial;
        for (var seq = 1L; seq <= cuantos; seq++)
        {
            var id = Guid.NewGuid();
            var ocurrio = (primero ?? Inicio).AddMinutes(seq);
            var campos = Campos(id, seq, ocurrio, previo, "Inventory.Document.Confirmed");
            var hash = SelloDeIntegridad.Hash(previo, SelloDeIntegridad.Canonico(campos));
            _db.AuditOutbox.Add(new AuditOutboxEntry
            {
                EventId = id, Stream = Flujo, Module = "Inventory", OccurredAt = ocurrio, Forwarded = true,
                Seq = seq, PrevHash = previo, Hash = hash,
            });
            _mongo.Documentos.Add(new DocumentoFalso(seq, id.ToString("D"), ocurrio, previo, hash, campos));
            previo = hash;
        }
        _db.SaveChanges();
    }

    private static Dictionary<string, object?> Campos(Guid id, long seq, DateTime ocurrio, string previo, string accion) => new()
    {
        ["_id"] = id.ToString("D"),
        ["action"] = accion,
        ["occurredAt"] = ocurrio,
        ["chain"] = new Dictionary<string, object?> { ["stream"] = Flujo, ["seq"] = seq, ["prevHash"] = previo, ["alg"] = "SHA-256", ["v"] = 1 },
    };

    private Task<IngenIA365ERP.Application.Common.Models.Result<VerifyAuditIntegrityResult>> Verificar(DateTime? desde = null, DateTime? hasta = null) =>
        new VerifyAuditIntegrityQueryHandler(_db, _cooperativa, _mongo, _auditoria, _reloj)
            .Handle(new VerifyAuditIntegrityQuery(desde ?? Inicio, hasta ?? Ahora), CancellationToken.None);

    [Fact]
    public async Task Una_cadena_intacta_no_tiene_incidentes_y_la_verificacion_queda_en_la_auditoria()
    {
        Sellar(5);

        var r = await Verificar();

        r.IsSuccess.Should().BeTrue();
        r.Value.Checked.Should().Be(5);
        r.Value.Incidents.Should().BeEmpty();
        var registro = _registrados.Should().ContainSingle().Subject;
        registro.Action.Should().Be("AuditLog.IntegrityVerified");
        registro.Metadata!["Result"].Should().Be("Clean");
        registro.Metadata["Incidents"].Should().Be("0");
    }

    [Fact]
    public async Task Un_campo_cambiado_es_Altered()
    {
        Sellar(5);
        _mongo.Alterar(3, "Inventory.Document.Voided");

        var r = await Verificar();

        r.Value.Incidents.Should().ContainSingle().Which.Should().Match<AuditIntegrityIncident>(i => i.Kind == "Altered" && i.Seq == 3);
        _registrados.Single().Metadata!["Result"].Should().Be("Altered");
    }

    [Fact]
    public async Task Un_seq_que_falta_es_Deleted()
    {
        Sellar(5);
        _mongo.Documentos.RemoveAll(d => d.Seq == 4);

        var r = await Verificar();

        r.Value.Incidents.Should().ContainSingle().Which.Should().Match<AuditIntegrityIncident>(i => i.Kind == "Deleted" && i.Seq == 4 && i.EventId != null);
    }

    [Fact]
    public async Task Un_evento_insertado_con_un_seq_repetido_es_Interleaved()
    {
        Sellar(5);
        var intruso = Guid.NewGuid();
        var original = _mongo.Documentos.Single(d => d.Seq == 2);
        _mongo.Documentos.Add(original with { EventId = intruso.ToString("D"), Campos = Campos(intruso, 2, Inicio, original.PrevHash!, "Inventory.Document.Created") });

        var r = await Verificar();

        r.Value.Incidents.Should().ContainSingle().Which.Should().Match<AuditIntegrityIncident>(i =>
            i.Kind == "Interleaved" && i.Seq == 2 && i.EventId == intruso.ToString("D"));
    }

    [Fact]
    public async Task Un_ancla_con_HMAC_invalido_es_AnchorInvalid()
    {
        Sellar(5);
        var hash3 = _db.AuditOutbox.Single(e => e.Seq == 3).Hash!;
        _db.AuditAnchors.AddRange(
            new AuditAnchor { Stream = Flujo, Kind = AuditAnchorKind.EveryN, Seq = 3, Hash = hash3, AnchoredAt = Inicio.AddHours(1), Hmac = LectorEnMemoria.HmacValido, KeyVersion = "k1" },
            new AuditAnchor { Stream = Flujo, Kind = AuditAnchorKind.EveryN, Seq = 5, Hash = _db.AuditOutbox.Single(e => e.Seq == 5).Hash!, AnchoredAt = Inicio.AddHours(2), Hmac = "falsificado", KeyVersion = "k1" });
        await _db.SaveChangesAsync();

        var r = await Verificar();

        r.Value.AnchorsChecked.Should().Be(2);
        r.Value.Incidents.Should().ContainSingle().Which.Should().Match<AuditIntegrityIncident>(i => i.Kind == "AnchorInvalid" && i.Seq == 5);
    }

    [Fact]
    public async Task Un_tramo_anterior_a_la_retencion_es_PurgedByRetention_y_no_Deleted()
    {
        var hace11Anios = Ahora.AddYears(-11);
        Sellar(3, hace11Anios);
        _mongo.Documentos.RemoveAll(d => d.Seq <= 2);

        var r = await Verificar(hace11Anios, hace11Anios.AddDays(1));

        r.Value.Incidents.Select(i => (i.Kind, i.Seq)).Should().Equal(("PurgedByRetention", 1L), ("PurgedByRetention", 2L));
    }

    [Fact]
    public void Un_rango_de_mas_de_diez_anios_no_pasa_el_validador()
    {
        var v = new VerifyAuditIntegrityQueryValidator();

        v.Validate(new VerifyAuditIntegrityQuery(Inicio, Inicio.AddYears(10).AddDays(1))).IsValid.Should().BeFalse();
        v.Validate(new VerifyAuditIntegrityQuery(Inicio, Inicio.AddYears(10))).IsValid.Should().BeTrue();
        v.Validate(new VerifyAuditIntegrityQuery(Inicio, Inicio.AddDays(-1))).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_eventos_en_el_rango_tambien_registra_la_verificacion()
    {
        var r = await Verificar();

        r.Value.Checked.Should().Be(0);
        r.Value.Incidents.Should().BeEmpty();
        _registrados.Should().ContainSingle(c => c.Action == "AuditLog.IntegrityVerified");
    }

    // ------------------------------------------------------------------------------------ el Mongo falso --

    private sealed record DocumentoFalso(long Seq, string EventId, DateTime OccurredAt, string? PrevHash, string? Hash, Dictionary<string, object?> Campos);

    /// <summary>Recalcula el hash de cada documento tal como está, como el lector real con <see cref="SelloDeIntegridad"/>.</summary>
    private sealed class LectorEnMemoria : ILectorDeCadenaDeAuditoria
    {
        public const string HmacValido = "hmac-valido";

        public List<DocumentoFalso> Documentos { get; } = [];

        public void Alterar(long seq, string accion)
        {
            var i = Documentos.FindIndex(d => d.Seq == seq);
            var campos = new Dictionary<string, object?>(Documentos[i].Campos) { ["action"] = accion };
            Documentos[i] = Documentos[i] with { Campos = campos };
        }

        public Task<IReadOnlyList<EventoDeCadena>> LeerAsync(string tenantId, string stream, long desdeSeq, long hastaSeq, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EventoDeCadena>>(Documentos
                .Where(d => d.Seq >= desdeSeq && d.Seq <= hastaSeq)
                .OrderBy(d => d.Seq)
                .Select(d => new EventoDeCadena(d.Seq, d.EventId, d.OccurredAt, d.PrevHash, d.Hash,
                    SelloDeIntegridad.Hash(d.PrevHash ?? SelloDeIntegridad.HashInicial, SelloDeIntegridad.Canonico(d.Campos))))
                .ToList());

        public bool AnclaValida(string stream, long seq, string hash, DateTime anchoredAt, string hmac, string keyVersion) => hmac == HmacValido;
    }
}
