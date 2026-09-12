using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Storage.Configuration;
using IngenIA365ERP.Storage.Services;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// El relay de respaldo: cuando la cuenta principal agota sus reintentos, el mismo
/// correo sale por la segunda cuenta, con SU remitente. Nació el 2026-09-12: en
/// producción el buzón autenticado no era el del remitente y el servidor respondía
/// «You are not allowed to send emails as X while logged as Y».
/// </summary>
public class RelayDeRespaldoSmtpTests
{
    /// <summary>
    /// Un SmtpClient que no toca la red: registra a qué host se conectó, con qué
    /// usuario y qué remitente llevaba el mensaje, y falla según lo que se le diga.
    /// </summary>
    private sealed class ClienteFalso(Func<string, Exception?> falloPorHost, List<(string Host, string? Usuario, string De)> envios) : SmtpClient
    {
        private string _host = "";
        private string? _usuario;

        public override Task ConnectAsync(string host, int port, SecureSocketOptions options, CancellationToken cancellationToken = default)
        {
            _host = host;
            return falloPorHost(host) is { } ex ? throw ex : Task.CompletedTask;
        }

        // La sobrecarga (usuario, contraseña) no es virtual: delega en ésta.
        public override Task AuthenticateAsync(System.Text.Encoding encoding, System.Net.ICredentials credentials, CancellationToken cancellationToken = default)
        {
            _usuario = credentials.GetCredential(new Uri("smtp://" + _host), "")?.UserName;
            return Task.CompletedTask;
        }

        public override Task<string> SendAsync(MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress? progress = null)
        {
            envios.Add((_host, _usuario, message.From.Mailboxes.Single().Address));
            return Task.FromResult("250 OK");
        }

        public override Task DisconnectAsync(bool quit, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static readonly EmailMessage Mensaje = new("copesan@hotmail.com", "Invitación", "<p>hola</p>");

    private static SmtpSettings Configuracion(bool conRespaldo) => new()
    {
        Host = "principal.test", Port = 587, Username = "noresponder@coop.test", Password = "x",
        FromAddress = "noresponder@coop.test", FromName = "No responder",
        MaxRetries = 1, InitialBackoffSeconds = 0,
        Respaldo = conRespaldo ? new SmtpSettings
        {
            Host = "respaldo.test", Port = 587, Username = "ingeniaerp@coop.test", Password = "y",
            FromAddress = "ingeniaerp@coop.test", FromName = "IngenIA ERP",
            MaxRetries = 0, InitialBackoffSeconds = 0,
        } : null,
    };

    private static (SmtpEmailSender Sender, List<(string Host, string? Usuario, string De)> Envios) Armar(
        SmtpSettings config, Func<string, Exception?> falloPorHost)
    {
        var envios = new List<(string, string?, string)>();
        var sender = new SmtpEmailSender(
            Options.Create(config), NullLogger<SmtpEmailSender>.Instance,
            () => new ClienteFalso(falloPorHost, envios));
        return (sender, envios);
    }

    [Fact]
    public async Task Si_la_principal_responde_el_respaldo_no_se_toca()
    {
        var (sender, envios) = Armar(Configuracion(conRespaldo: true), _ => null);

        await sender.SendAsync(Mensaje, CancellationToken.None);

        envios.Should().ContainSingle().Which.Should().Be(("principal.test", "noresponder@coop.test", "noresponder@coop.test"));
    }

    [Fact]
    public async Task Si_la_principal_agota_sus_reintentos_sale_por_el_respaldo_con_su_propio_remitente()
    {
        var (sender, envios) = Armar(Configuracion(conRespaldo: true),
            host => host == "principal.test" ? new InvalidOperationException("You are not allowed to send emails as A while logged as B") : null);

        await sender.SendAsync(Mensaje, CancellationToken.None);

        // El remitente es el del respaldo: el servidor exige que coincida con la cuenta autenticada.
        envios.Should().ContainSingle().Which.Should().Be(("respaldo.test", "ingeniaerp@coop.test", "ingeniaerp@coop.test"));
    }

    [Fact]
    public async Task Si_las_dos_fallan_la_excepcion_lleva_los_dos_motivos()
    {
        var (sender, _) = Armar(Configuracion(conRespaldo: true),
            host => new InvalidOperationException(host == "principal.test" ? "buzón bloqueado" : "contraseña vencida"));

        var acto = () => sender.SendAsync(Mensaje, CancellationToken.None);

        (await acto.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("buzón bloqueado").And.Contain("contraseña vencida");
    }

    [Fact]
    public async Task Sin_respaldo_configurado_el_fallo_de_la_principal_sube_tal_cual()
    {
        var (sender, _) = Armar(Configuracion(conRespaldo: false), _ => new InvalidOperationException("buzón bloqueado"));

        var acto = () => sender.SendAsync(Mensaje, CancellationToken.None);

        (await acto.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be("buzón bloqueado");
    }

    [Fact]
    public async Task Un_respaldo_sin_host_cuenta_como_no_configurado()
    {
        var config = Configuracion(conRespaldo: true);
        config.Respaldo!.Host = "";
        var (sender, _) = Armar(config, _ => new InvalidOperationException("buzón bloqueado"));

        var acto = () => sender.SendAsync(Mensaje, CancellationToken.None);

        (await acto.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Be("buzón bloqueado");
    }

    [Fact]
    public async Task Cancelar_no_dispara_el_respaldo()
    {
        var (sender, envios) = Armar(Configuracion(conRespaldo: true),
            host => host == "principal.test" ? new OperationCanceledException() : null);

        var acto = () => sender.SendAsync(Mensaje, CancellationToken.None);

        await acto.Should().ThrowAsync<OperationCanceledException>();
        envios.Should().BeEmpty();
    }
}
