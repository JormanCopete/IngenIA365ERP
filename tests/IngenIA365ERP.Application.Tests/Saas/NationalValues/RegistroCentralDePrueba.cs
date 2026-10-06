using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Saas.NationalValues;

/// <summary>
/// El escenario común de las pruebas del registro central (feature 013, T0022, T0023): la base administrativa en memoria (con la
/// guarda de inmutabilidad de <c>AdminDbContext</c>), el maestro con segundo factor, un reloj fijo y los validadores de la
/// plataforma. (nuevo)
/// </summary>
public sealed class RegistroCentralDePrueba : IDisposable
{
    public static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    public static readonly Guid MaestroId = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
    public const string MaestroCorreo = "master@ingenia365.com";

    public TestAdminDbContext Db { get; }
    public ICurrentCentralUserContext Maestro { get; } = Substitute.For<ICurrentCentralUserContext>();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public IReadOnlyList<IValidadorDeDocumentoNormativo> Validadores { get; } = [new ValidadorDeCatalogoDeModalidades(), new ValidadorDeTiposDeOrganizacion()];

    public RegistroCentralDePrueba(string? nombreDeLaBase = null)
    {
        Db = TestAdminDbContext.Create(nombreDeLaBase);
        Maestro.IsAuthenticated.Returns(true);
        Maestro.CentralUserId.Returns(MaestroId);
        Maestro.IsGlobalMasterAdmin.Returns(true);
        Maestro.Email.Returns(MaestroCorreo);
        Reloj.UtcNow.Returns(Ahora);
        Reloj.TodayUtc.Returns(DateOnly.FromDateTime(Ahora));
        Reloj.HoyLocal.Returns(DateOnly.FromDateTime(Ahora));
    }

    public Task<Result<Guid>> RegistrarAsync(RegisterNationalValueCommand comando) =>
        new RegisterNationalValueCommandHandler(Db, Maestro, Reloj).Handle(comando, CancellationToken.None);

    public Task<Result<Guid>> CorregirAsync(CorrectNationalValueCommand comando) =>
        new CorrectNationalValueCommandHandler(Db, Maestro, Reloj).Handle(comando, CancellationToken.None);

    public Task<Result<Guid>> RegistrarDocumentoAsync(RegisterRegulatoryDocumentCommand comando) =>
        new RegisterRegulatoryDocumentCommandHandler(Db, Maestro, Reloj, Validadores).Handle(comando, CancellationToken.None);

    /// <summary>El catálogo de modalidades de la semilla central (claves de usura <c>CONSUMO_Y_ORDINARIO</c>, <c>MICROCREDITO</c>…).</summary>
    public Task<Result<Guid>> CatalogoDeModalidadesAsync(DateOnly? desde = null) =>
        RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.CatalogoDeModalidades),
            "MODALIDADES-PRUEBA", desde ?? new DateOnly(2026, 1, 1), null, 1, PlatformNationalValuesSeeder.Contenido("catalogo-de-modalidades.json"),
            false, "Propuesta E4/E10"));

    public static RegisterNationalValueCommand Smmlv(decimal valor = 1_750_905m, DateOnly? desde = null) =>
        new(CodigosDeValorNacional.Smmlv, null, desde ?? new DateOnly(2026, 1, 1), null, null, valor, nameof(UnidadDeValorNacional.Pesos),
            "Decreto 1469 de 2025");

    public void Dispose() => Db.Dispose();
}
