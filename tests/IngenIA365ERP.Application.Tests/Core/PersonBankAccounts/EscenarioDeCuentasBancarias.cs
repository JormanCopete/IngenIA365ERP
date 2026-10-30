using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.PersonBankAccounts;

/// <summary>
/// El escenario común de las pruebas de las cuentas bancarias de la persona (feature 015, T0012, T0013): una persona, un banco, una
/// certificación disponible, dos actores (quien pide y quien aprueba), el reloj en Colombia, los parámetros de espera y marca, las alertas y la
/// auditoría (que sin cooperativa va por <see cref="IAuditService"/>).
/// </summary>
internal sealed class EscenarioDeCuentasBancarias
{
    public static readonly DateOnly Hoy = new(2026, 12, 1);

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
    public IActorActual Actor { get; } = Substitute.For<IActorActual>();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public ILectorDeParametros Parametros { get; } = Substitute.For<ILectorDeParametros>();
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public IAuditService Auditoria { get; } = Substitute.For<IAuditService>();
    public IServiceProvider Servicios { get; }

    public Person Persona { get; }
    public Person Otra { get; }
    public Bank Banco { get; }
    public Guid Certificacion { get; }

    public EscenarioDeCuentasBancarias()
    {
        Reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        Reloj.HoyLocal.Returns(Hoy);
        Espera(null);
        Marca(null);
        Alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        Servicios = new ServiceCollection().AddSingleton(Auditoria).BuildServiceProvider();
        ComoQuienPide();

        Persona = new Person { FirstName = "Ana", LastName = "Pérez", TaxId = "1010" };
        Otra = new Person { FirstName = "Luis", LastName = "Gómez", TaxId = "2020" };
        Banco = new Bank { Name = "Banco de Bogotá", TransferCode = "1001", LegacyCode = "01" };
        Db.People.AddRange(Persona, Otra);
        Db.Banks.Add(Banco);
        Db.SaveChanges();
        Certificacion = CertificacionDe(Persona);
    }

    public Guid CertificacionDe(Person persona)
    {
        var adjunto = new Attachment
        {
            OwnerEntityType = AdjuntosDeModulo.CertificacionBancariaDePersona, OwnerEntityPublicId = persona.PublicId, FileName = "cert.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x", Status = EstadoDeAdjunto.Available,
        };
        Db.Attachments.Add(adjunto);
        Db.SaveChanges();
        return adjunto.PublicId;
    }

    public void ComoQuienPide() => Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(ActorCon(10, "Paula Pide"));

    public void ComoQuienAprueba() => Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(ActorCon(20, "Andrés Aprueba"));

    public void Espera(int? dias) => Parametro(ParametrosDeRecaudoYTesoreria.CuentasDestinoDiasDeEspera, dias);

    public void Marca(int? dias) => Parametro(ParametrosDeRecaudoYTesoreria.CuentasDestinoDiasDeMarca, dias);

    private void Parametro(string clave, int? dias)
    {
        var def = CatalogoDeParametros.Buscar(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, clave)!;
        Parametros.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, clave, Arg.Any<DateOnly>(), Arg.Any<ParameterScopeKind>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(def, dias?.ToString() ?? string.Empty, dias, null)));
    }

    private static Actor ActorCon(int userId, string nombre) =>
        new(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(), nombre, null, ExecutionChannel.Web, "pruebas", null, null);
}
