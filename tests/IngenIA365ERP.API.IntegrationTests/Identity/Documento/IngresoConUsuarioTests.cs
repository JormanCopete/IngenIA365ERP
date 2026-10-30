using System.Net;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (T092, US4): la tabla de ingreso del borrador contra el motor real. La cédula entra con y sin
/// puntos y con «CC»; los demás tipos con su prefijo, en mayúsculas o minúsculas; una CC y una CE del mismo
/// número son dos personas distintas; y una cuenta con el correo sin verificar (invitada que nunca aceptó)
/// no entra con él y responde lo mismo que una inexistente.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class IngresoConUsuarioTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_tabla_de_ingreso_por_usuario()
    {
        var esc = new Escenario017(fx);
        using var http = Escenario017.Cliente(fx);
        var maestro = await fx.IniciarSesionMaestroAsync(http);
        var coop = await esc.CooperativaAsync(http, maestro, "tenant_f017_ingreso", "900171001");

        await esc.PersonaAsync(http, maestro, coop, "cc.ingreso@f017.test", "CC", "63100100");
        await esc.PersonaAsync(http, maestro, coop, "ce.ingreso@f017.test", "CE", "63100100");
        await esc.PersonaAsync(http, maestro, coop, "pa.ingreso@f017.test", "PA", "AB12345");
        await esc.PersonaAsync(http, maestro, coop, "ppt.ingreso@f017.test", "PPT", "1234567");

        var casos = new (string Identificador, string CorreoEsperado)[]
        {
            ("63100100", "cc.ingreso@f017.test"),
            ("63.100.100", "cc.ingreso@f017.test"),
            ("CC63100100", "cc.ingreso@f017.test"),
            ("cc 63.100.100", "cc.ingreso@f017.test"),
            ("CE63100100", "ce.ingreso@f017.test"),
            ("ce63100100", "ce.ingreso@f017.test"),
            ("PAAB12345", "pa.ingreso@f017.test"),
            ("paab12345", "pa.ingreso@f017.test"),
            ("PPT1234567", "ppt.ingreso@f017.test"),
            ("ppt-1.234.567", "ppt.ingreso@f017.test"),
        };
        foreach (var (identificador, correo) in casos)
        {
            var r = await Escenario017.IngresarAsync(http, identificador);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"«{identificador}» no entró: {await r.Content.ReadAsStringAsync()}");
            var cuerpo = await Escenario017.LeerAsync(r);
            Assert.Equal(correo, cuerpo.GetProperty("email").GetString(), StringComparer.OrdinalIgnoreCase);
        }

        // Correo sin verificar: invitada que nunca aceptó. No entra con él, y responde lo mismo que nadie.
        await esc.InvitarAsync(http, maestro, coop, "sin.verificar@f017.test", "CC", "63100199");
        var sinVerificar = await Escenario017.IngresarAsync(http, "sin.verificar@f017.test", "Cualquier-Clave-1");
        var inexistente = await Escenario017.IngresarAsync(http, "nadie.aqui@f017.test", "Cualquier-Clave-1");
        Assert.Equal(HttpStatusCode.Unauthorized, sinVerificar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal(await Escenario017.CodigoAsync(inexistente), await Escenario017.CodigoAsync(sinVerificar));
    }
}
