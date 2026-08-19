using FluentAssertions;
using IngenIA365ERP.Persistence.Configuration;
using Microsoft.Extensions.Configuration;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// El resolutor decide contra QUÉ base corre la aplicación. Si se equivoca en
/// silencio, el ERP arranca apuntando a una base vacía o —peor— a la de otro
/// ambiente. Por eso cada caso de error se prueba explícitamente.
/// </summary>
public class InfraestructuraConfiguracionTests
{
    private static IConfiguration Configurar(Dictionary<string, string?> pares) =>
        new ConfigurationBuilder().AddInMemoryCollection(pares).Build();

    private static Dictionary<string, string?> CatalogoCompleto() => new()
    {
        ["Infraestructura:PostgreSQL:Local:Operativa"] = "Host=localhost;Port=5432;Database=erp",
        ["Infraestructura:PostgreSQL:Local:Admin"] = "Host=localhost;Port=5432;Database=erp_admin",
        ["Infraestructura:PostgreSQL:Docker:Operativa"] = "Host=localhost;Port=5433;Database=erp",
        ["Infraestructura:PostgreSQL:Docker:Admin"] = "Host=localhost;Port=5433;Database=erp_admin",
        ["Infraestructura:Redis:Local"] = "localhost:6379",
        ["Infraestructura:Redis:Docker"] = "localhost:6380",
        ["Infraestructura:Redis:Wsl"] = "172.24.80.1:6379",
        ["Infraestructura:MongoDB:Local"] = "mongodb://localhost:27017/audit",
        ["Infraestructura:Smtp:Docker:Host"] = "127.0.0.1",
        ["Infraestructura:Smtp:Docker:Port"] = "1025",
    };

    [Fact]
    public void SinSeccion_NoHaceNada()
    {
        // Producción resuelve todo por variables de entorno y no declara la
        // sección. Si esto proyectara algo, cambiaría el comportamiento de un
        // ambiente que nadie tocó.
        var resultado = InfraestructuraConfiguracion.Resolver(
            Configurar(new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSQL" }));

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void CadaServicioPuedeElegirSuDestinoPorSeparado()
    {
        // Es el motivo de todo esto: en Windows la base va instalada en el
        // sistema y Redis en un contenedor. Un interruptor global no serviría.
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:PostgreSQL"] = "Local";
        pares["Infraestructura:Destinos:Redis"] = "Docker";

        var r = InfraestructuraConfiguracion.Resolver(Configurar(pares));

        r["Database:ConnectionStrings:PostgreSQL"].Should().Contain("Port=5432");
        r["ConnectionStrings:Redis"].Should().Be("localhost:6380");
    }

    [Fact]
    public void LaBaseProyectaOperativaYAdminPorSeparado()
    {
        // Son dos bases distintas y el principio IV exige que no se mezclen.
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:PostgreSQL"] = "Docker";

        var r = InfraestructuraConfiguracion.Resolver(Configurar(pares));

        r["Database:ConnectionStrings:PostgreSQL"].Should().Contain("Database=erp");
        r["Database:AdminConnectionStrings:PostgreSQL"].Should().Contain("Database=erp_admin");
    }

    [Fact]
    public void ElSmtpProyectaHostYPuerto()
    {
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:Smtp"] = "Docker";

        var r = InfraestructuraConfiguracion.Resolver(Configurar(pares));

        r["Smtp:Host"].Should().Be("127.0.0.1");
        r["Smtp:Port"].Should().Be("1025");
    }

    [Fact]
    public void DestinoInexistente_FallaAlArrancarYDiceCualesHay()
    {
        // Degradar en silencio dejaría la aplicación contra la base equivocada.
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:Redis"] = "Kubernetes";

        var accion = () => InfraestructuraConfiguracion.Resolver(Configurar(pares));

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*Redis*Kubernetes*")
            .WithMessage("*Docker*", "el mensaje debe listar los destinos disponibles");
    }

    [Fact]
    public void DestinoValidoPeroNoDefinido_LoDistingueDeUnoMalEscrito()
    {
        // Wsl es un destino legítimo; el problema es que falta declararlo para
        // ese servicio. El mensaje tiene que decir eso y no "no existe".
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:MongoDB"] = "Wsl";

        var accion = () => InfraestructuraConfiguracion.Resolver(Configurar(pares));

        accion.Should().Throw<InvalidOperationException>()
            .WithMessage("*válido pero no está definido*");
    }

    [Fact]
    public void ServicioDesconocido_EsRechazado()
    {
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:RabbitMQ"] = "Docker";

        var accion = () => InfraestructuraConfiguracion.Resolver(Configurar(pares));

        accion.Should().Throw<InvalidOperationException>().WithMessage("*RabbitMQ*");
    }

    [Fact]
    public void BaseSinAdmin_EsRechazada()
    {
        var pares = new Dictionary<string, string?>
        {
            ["Infraestructura:Destinos:PostgreSQL"] = "Local",
            ["Infraestructura:PostgreSQL:Local:Operativa"] = "Host=localhost;Database=erp",
        };

        var accion = () => InfraestructuraConfiguracion.Resolver(Configurar(pares));

        accion.Should().Throw<InvalidOperationException>().WithMessage("*Admin*");
    }

    [Fact]
    public void ElNombreDelDestinoNoDistingueMayusculas()
    {
        // Escribir "docker" en minúscula no debería costar media hora de
        // depuración.
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:Redis"] = "docker";

        InfraestructuraConfiguracion.Resolver(Configurar(pares))["ConnectionStrings:Redis"]
            .Should().Be("localhost:6380");
    }

    [Fact]
    public void UnServicioSinDestinoElegido_SeDejaComoEsta()
    {
        // Sólo se proyecta lo que se eligió; lo demás sigue saliendo de las
        // cadenas de siempre.
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:Redis"] = "Local";

        var r = InfraestructuraConfiguracion.Resolver(Configurar(pares));

        r.Should().ContainKey("ConnectionStrings:Redis");
        r.Should().NotContainKey("ConnectionStrings:MongoDB");
        r.Should().NotContainKey("Database:ConnectionStrings:PostgreSQL");
    }

    [Fact]
    public void DentroDeUnContenedor_NoResuelveNada()
    {
        // La VPS de DEV corre con ASPNETCORE_ENVIRONMENT=Development, o sea que
        // lee el MISMO appsettings.Development.json que un portatil, con sus
        // catalogos apuntando a localhost. La guardia es por contenedor y no
        // por nombre de ambiente justamente por eso.
        var previo = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

            var pares = CatalogoCompleto();
            pares["Infraestructura:Destinos:PostgreSQL"] = "Local";
            pares["Infraestructura:Destinos:Redis"] = "Docker";

            InfraestructuraConfiguracion.EstaEnContenedor().Should().BeTrue();
            InfraestructuraConfiguracion.Resolver(Configurar(pares)).Should().BeEmpty(
                "en un contenedor la configuración la inyecta el despliegue");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", previo);
        }
    }

    [Fact]
    public void DentroDeUnContenedor_UnDestinoInvalidoTampocoRompeElArranque()
    {
        // Un catálogo pensado para desarrollo no puede tumbar un pod. La
        // guardia se evalúa ANTES de validar, no después.
        var previo = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
        try
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", "true");

            var pares = CatalogoCompleto();
            pares["Infraestructura:Destinos:Redis"] = "DestinoQueNoExiste";

            var accion = () => InfraestructuraConfiguracion.Resolver(Configurar(pares));

            accion.Should().NotThrow();
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER", previo);
        }
    }

    [Fact]
    public void ElSistemaOperativoActualCoincideConElNombreDeArchivo()
    {
        // Si esto devolviera algo distinto, el archivo por sistema operativo
        // nunca se cargaría y el fallo sería invisible: appsettings los declara
        // opcionales.
        var so = InfraestructuraConfiguracion.SistemaOperativoActual();

        so.Should().BeOneOf("Windows", "Linux", "macOS", "Desconocido");
        if (OperatingSystem.IsWindows()) so.Should().Be("Windows");
    }

    [Fact]
    public void LaDescripcionResumeQueSeEligio()
    {
        var pares = CatalogoCompleto();
        pares["Infraestructura:Destinos:PostgreSQL"] = "Docker";
        pares["Infraestructura:Destinos:Redis"] = "Local";

        var texto = InfraestructuraConfiguracion.Describir(Configurar(pares));

        texto.Should().Contain("PostgreSQL=Docker").And.Contain("Redis=Local");
    }
}
