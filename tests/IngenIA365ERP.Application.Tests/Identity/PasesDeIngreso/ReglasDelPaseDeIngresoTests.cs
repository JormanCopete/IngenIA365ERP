using FluentAssertions;
using IngenIA365ERP.Application.Identity.PasesDeIngreso;

namespace IngenIA365ERP.Application.Tests.Identity.PasesDeIngreso;

/// <summary>Feature 027 (HU4, HU6, FR-011, FR-019): quién puede conceder un pase de ingreso a quién.</summary>
public sealed class ReglasDelPaseDeIngresoTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Operador = Guid.NewGuid();

    private static ReglasDelPaseDeIngreso.Hechos Valido() => new(
        Actor: Admin, ActorEsMaestro: false, Destinatario: Operador, DestinatarioEsMaestro: false, DestinatarioEsAdministrador: false,
        DestinatarioMiembroActivo: true, DestinatarioActivo: true, CorreoVerificado: true, TieneSegundoFactor: true, OpcionEncendida: true,
        HorasMaximasDeLaCooperativa: 12, DuracionPedida: TimeSpan.FromHours(12), ConcedidosAlUsuarioEnLaVentana: 0,
        ConcedidosEnLaCooperativaEnLaVentana: 0);

    [Fact]
    public void Un_administrador_concede_a_un_operador_con_segundo_factor() =>
        ReglasDelPaseDeIngreso.Evaluar(Valido()).Should().BeNull();

    public static TheoryData<string, Func<ReglasDelPaseDeIngreso.Hechos, ReglasDelPaseDeIngreso.Hechos>> Rechazos => new()
    {
        { "LoginPass.Disabled", h => h with { OpcionEncendida = false } },
        { "LoginPass.TargetIsMaster", h => h with { DestinatarioEsMaestro = true, ActorEsMaestro = true } },
        { "LoginPass.Self", h => h with { Destinatario = Admin } },
        { "LoginPass.NotMember", h => h with { DestinatarioMiembroActivo = false } },
        { "LoginPass.NotMember", h => h with { DestinatarioActivo = false } },
        { "LoginPass.TargetIsAdmin", h => h with { DestinatarioEsAdministrador = true } },
        { "LoginPass.EmailNotVerified", h => h with { CorreoVerificado = false } },
        { "LoginPass.NoSecondFactor", h => h with { TieneSegundoFactor = false } },
        { "LoginPass.HoursOutOfRange", h => h with { DuracionPedida = TimeSpan.FromHours(13) } },
        { "LoginPass.HoursOutOfRange", h => h with { HorasMaximasDeLaCooperativa = 4, DuracionPedida = TimeSpan.FromHours(5) } },
        { "LoginPass.HoursOutOfRange", h => h with { DuracionPedida = TimeSpan.Zero } },
        { "LoginPass.TooMany", h => h with { ConcedidosAlUsuarioEnLaVentana = 5 } },
        { "LoginPass.TooMany", h => h with { ConcedidosEnLaCooperativaEnLaVentana = 20 } },
    };

    [Theory]
    [MemberData(nameof(Rechazos))]
    public void Rechaza(string codigo, Func<ReglasDelPaseDeIngreso.Hechos, ReglasDelPaseDeIngreso.Hechos> cambio) =>
        ReglasDelPaseDeIngreso.Evaluar(cambio(Valido()))!.Codigo.Should().Be(codigo);

    [Fact]
    public void El_pase_a_un_administrador_lo_da_el_maestro() =>
        ReglasDelPaseDeIngreso.Evaluar(Valido() with { DestinatarioEsAdministrador = true, ActorEsMaestro = true }).Should().BeNull();

    [Fact]
    public void Los_topes_admiten_hasta_el_limite() =>
        ReglasDelPaseDeIngreso.Evaluar(Valido() with { ConcedidosAlUsuarioEnLaVentana = 4, ConcedidosEnLaCooperativaEnLaVentana = 19 })
            .Should().BeNull();

    [Theory]
    [InlineData("AbCdEfGhIjKlMnOpQrStUv_-0123456789", "AbCdEfGhIjKlMnOpQrStUv_-0123456789")]
    [InlineData("  AbCdEfGhIjKlMnOpQrStUv_-0123456789  ", "AbCdEfGhIjKlMnOpQrStUv_-0123456789")]
    [InlineData("https://erp.ingenia365.com/security/login?pase=AbCdEfGhIjKlMnOpQrStUv_-0123456789", "AbCdEfGhIjKlMnOpQrStUv_-0123456789")]
    [InlineData("https://erp.ingenia365.com/security/login?x=1&pase=AbCdEfGhIjKlMnOpQrStUv_-0123456789&y=2", "AbCdEfGhIjKlMnOpQrStUv_-0123456789")]
    [InlineData("corto", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Lee_el_pase_pegado_o_del_enlace(string? valor, string? esperado) =>
        UsarPaseDeIngresoCommandHandler.ExtraerToken(valor).Should().Be(esperado);
}
