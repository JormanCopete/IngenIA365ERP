using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Notifications;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Correo;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common.Notifications;

/// <summary>
/// El modo de prueba del correo (2026-11-17): Enviar deja pasar; Suspender no envía y deja constancia (pendiente con el mensaje entero o
/// descartado); Redirigir manda al buzón con la marca y la franja; sin cooperativa en curso o con <c>SiempreAlDestinatario</c> no se toca; y
/// el reenvío de los pendientes sólo corre con el envío ya en Enviar.
/// </summary>
public sealed class CorreoSegunModoDeEnvioTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 11, 17);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly List<EmailMessage> _salieron = [];
    private readonly IEmailSender _smtp = Substitute.For<IEmailSender>();
    private readonly ICurrentTenantService _cooperativa = Substitute.For<ICurrentTenantService>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public CorreoSegunModoDeEnvioTests()
    {
        _smtp.SendAsync(Arg.Do<EmailMessage>(_salieron.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _cooperativa.TenantId.Returns("abc123");
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc));
    }

    public void Dispose() => _db.Dispose();

    private CorreoSegunModoDeEnvio Correo()
    {
        var servicios = new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(_db)
            .AddSingleton<ILectorDeParametros>(new LectorDeParametros(_db))
            .AddSingleton(_cooperativa)
            .AddSingleton(_reloj)
            .BuildServiceProvider();
        return new CorreoSegunModoDeEnvio(_smtp, servicios, NullLogger<CorreoSegunModoDeEnvio>.Instance);
    }

    private async Task Parametro(string clave, string valor)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCorreo.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 11, 1), Reason = "prueba",
        });
        await _db.SaveChangesAsync();
    }

    private static EmailMessage Factura(bool siempre = false) => new("cliente@ejemplo.com", "Factura FE-10", "<p>Su factura</p>",
        Attachments: [new EmailAttachment("FE-10.zip", "application/zip", [1, 2, 3])], SiempreAlDestinatario: siempre);

    [Fact]
    public async Task Sin_parametros_el_correo_sale_como_siempre()
    {
        await Correo().SendAsync(Factura(), default);

        _salieron.Should().ContainSingle().Which.To.Should().Be("cliente@ejemplo.com");
        (await _db.HeldEmails.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Suspendido_y_sin_pendientes_no_sale_y_queda_descartado_sin_el_cuerpo()
    {
        await Parametro(ParametrosDeCorreo.EnvioATerceros, ParametrosDeCorreo.Suspender);

        await Correo().SendAsync(Factura(), default);

        _salieron.Should().BeEmpty();
        var retenido = await _db.HeldEmails.Include(h => h.Attachments).SingleAsync();
        retenido.Status.Should().Be(HeldEmailStatus.Discarded);
        retenido.OriginalTo.Should().Be("cliente@ejemplo.com");
        retenido.BodyHtml.Should().BeNull();
        retenido.Attachments.Should().BeEmpty();
        retenido.AttachmentCount.Should().Be(1);
    }

    [Fact]
    public async Task Suspendido_con_pendientes_guarda_el_mensaje_entero_y_se_reenvia_al_volver_a_enviar()
    {
        await Parametro(ParametrosDeCorreo.EnvioATerceros, ParametrosDeCorreo.Suspender);
        await Parametro(ParametrosDeCorreo.SuspendidosQuedanPendientes, "true");
        await Correo().SendAsync(Factura(), default);

        var retenido = await _db.HeldEmails.Include(h => h.Attachments).SingleAsync();
        retenido.Status.Should().Be(HeldEmailStatus.Pending);
        retenido.BodyHtml.Should().Be("<p>Su factura</p>");
        retenido.Attachments.Should().ContainSingle().Which.Content.Should().Equal(1, 2, 3);

        var reenvio = new ResendCorreosRetenidosCommandHandler(_db, new RemitenteDirecto(_smtp), new LectorDeParametros(_db), _reloj,
            Substitute.For<ICurrentUserService>());
        var bloqueado = await reenvio.Handle(new ResendCorreosRetenidosCommand([]), default);
        bloqueado.IsFailure.Should().BeTrue();
        bloqueado.Error.Code.Should().Be("Correo.ModoDePruebaActivo");
        _salieron.Should().BeEmpty();

        var vigente = await _db.ParameterVersions.SingleAsync(v => v.Key == ParametrosDeCorreo.EnvioATerceros);
        vigente.Value = ParametrosDeCorreo.Enviar;
        await _db.SaveChangesAsync();
        reenvio = new ResendCorreosRetenidosCommandHandler(_db, new RemitenteDirecto(_smtp), new LectorDeParametros(_db), _reloj,
            Substitute.For<ICurrentUserService>());

        var r = await reenvio.Handle(new ResendCorreosRetenidosCommand([]), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Processed.Should().Be(1);
        var salio = _salieron.Should().ContainSingle().Subject;
        salio.To.Should().Be("cliente@ejemplo.com");
        salio.Attachments.Should().ContainSingle();
        var final = await _db.HeldEmails.SingleAsync();
        final.Status.Should().Be(HeldEmailStatus.Resent);
        final.BodyHtml.Should().BeNull();
    }

    [Fact]
    public async Task Redirigido_va_a_cada_buzon_con_la_marca_y_la_franja()
    {
        await Parametro(ParametrosDeCorreo.EnvioATerceros, ParametrosDeCorreo.Redirigir);
        await Parametro(ParametrosDeCorreo.BuzonDePrueba, "pruebas@coop.com; contadora@coop.com");

        await Correo().SendAsync(Factura(), default);

        _salieron.Select(m => m.To).Should().Equal("pruebas@coop.com", "contadora@coop.com");
        _salieron.Should().OnlyContain(m => m.Subject == "[PRUEBA] Factura FE-10" && m.BodyHtml.Contains("cliente@ejemplo.com")
            && m.BodyHtml.EndsWith("<p>Su factura</p>") && m.Attachments!.Count == 1);
        var retenido = await _db.HeldEmails.SingleAsync();
        retenido.Status.Should().Be(HeldEmailStatus.Redirected);
        retenido.RedirectedTo.Should().Be("pruebas@coop.com, contadora@coop.com");
    }

    [Fact]
    public async Task Redirigir_sin_buzon_no_envia_al_destinatario_real()
    {
        await Parametro(ParametrosDeCorreo.EnvioATerceros, ParametrosDeCorreo.Redirigir);

        await Correo().SendAsync(Factura(), default);

        _salieron.Should().BeEmpty();
        (await _db.HeldEmails.SingleAsync()).Mode.Should().Be(HeldEmailMode.Suspended);
    }

    [Fact]
    public async Task Los_correos_para_entrar_y_los_sin_cooperativa_no_se_tocan()
    {
        await Parametro(ParametrosDeCorreo.EnvioATerceros, ParametrosDeCorreo.Suspender);

        await Correo().SendAsync(Factura(siempre: true), default);
        _cooperativa.TenantId.Returns((string?)null);
        await Correo().SendAsync(Factura(), default);

        _salieron.Should().HaveCount(2).And.OnlyContain(m => m.To == "cliente@ejemplo.com");
        (await _db.HeldEmails.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("a@b.co", true)]
    [InlineData("a@b.co; c@d.com", true)]
    [InlineData("a@b.co,c@d.com", true)]
    [InlineData("no-es-correo", false)]
    [InlineData("a@b.co;;", false)]
    public void El_buzon_admite_una_o_varias_direcciones(string texto, bool admitido)
    {
        var definicion = Domain.Common.Parametros.CatalogoDeParametros.Buscar(ParametrosDeCorreo.Modulo, ParametrosDeCorreo.BuzonDePrueba)!;
        definicion.Interpretar(texto).Admitido.Should().Be(admitido);
    }
}
