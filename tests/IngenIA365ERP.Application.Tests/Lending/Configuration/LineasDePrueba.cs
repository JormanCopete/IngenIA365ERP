using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// El escenario común de las pruebas de líneas de crédito (feature 013, US1): el de <see cref="GobiernoDePrueba"/> (InMemory, hoy local
/// 2026-12-01, actor, permisos, alcance total, perfil con actividad financiera, auditoría real) con los <b>lectores reales</b> de valores
/// nacionales y documentos normativos sobre la misma base —el catálogo de modalidades con consumo, microcrédito y vivienda, y las usuras
/// sembradas como copias de una réplica—, una aseguradora del maestro y los handlers armados a mano. Las alertas del faltante quedan en
/// <see cref="Levantadas"/>. (nuevo)
/// </summary>
internal sealed class LineasDePrueba : IDisposable
{
    public const string ClaveConsumo = "CONSUMO_Y_ORDINARIO";
    public const string ClaveMicro = "MICROCREDITO";
    public static readonly DateOnly Hoy = GobiernoDePrueba.Hoy;
    public static readonly DateTime Ahora = GobiernoDePrueba.Ahora;

    public const string Catalogo = """
        {"modalities":[
          {"code":"CONSUMO","name":"Consumo","combinations":[
            {"subsegment":"ConLibranza","sarcSegment":"CONSUMO","expectedLossSegment":"CONSUMO_LIBRANZA","usuryKey":"CONSUMO_Y_ORDINARIO"},
            {"subsegment":"SinLibranza","sarcSegment":"CONSUMO","expectedLossSegment":"CONSUMO_SIN_LIBRANZA","usuryKey":"CONSUMO_Y_ORDINARIO"}]},
          {"code":"MICROCREDITO","name":"Microcrédito","combinations":[{"sarcSegment":"MICROCREDITO","usuryKey":"MICROCREDITO"}]},
          {"code":"VIVIENDA","name":"Vivienda","combinations":[{"sarcSegment":"VIVIENDA","housingRateCap":true,"admitsUvr":true}]}
        ]}
        """;

    private static long _secuencia = 50_000;

    public LineasDePrueba()
    {
        Alertas.LevantarAsync(Arg.Do<AlertaALevantar>(Levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        Aseguradora = G.Persona("Seguros", "Vida Deudores", "860001");
        Jefe = G.Usuario(G.Persona("Juana", "Cartera", "9001"), "jefe@coop.test");
        G.ActuarComo(Jefe);
    }

    public GobiernoDePrueba G { get; } = new();
    public TestApplicationDbContext Db => G.Db;
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public List<AlertaALevantar> Levantadas { get; } = [];
    public Person Aseguradora { get; }
    public Domain.Entities.Security.User Jefe { get; }

    public void Dispose() => G.Dispose();

    // ------------------------------------------------------------------------------------------------------- piezas --

    public LectorDeValoresNacionales Valores() => new(Db, Alertas);
    public LectorDeDocumentoNormativo Normativa() => new(Db, Alertas);
    public ReglasDeLineaDeCredito Reglas() => new(Normativa(), Valores(), G.Perfil);
    public AltaDeVersionDeLinea Alta() => new(Db, G.Reloj);

    public CreateCreditLineCommandHandler Crear() => new(Db, Reglas(), Alta(), G.Reloj);
    public AddCreditLineVersionCommandHandler Versionar() => new(Db, Reglas(), Alta(), G.Alcance, G.Auditoria, G.Reloj);
    public CloseCreditLineCommandHandler Cerrar() => new(Db, G.Actor, G.Alcance, G.Auditoria, G.Reloj);
    public ReopenCreditLineCommandHandler Reabrir() => new(Db, Reglas(), G.Alcance, G.Auditoria, G.Reloj);
    public DeleteCreditLineCommandHandler Borrar() => new(Db, G.Actor, G.Alcance, G.Auditoria, G.Reloj);
    public GetCreditLineQueryHandler Leer() => new(Db, G.Alcance, G.Auditoria, G.Reloj);
    public ListCreditLinesQueryHandler Listar() => new(Db, Reglas(), G.Alcance, G.Reloj);
    public CheckCreditLineUsuryQueryHandler Consultar() => new(Db, Reglas(), G.Alcance, G.Auditoria, G.Reloj);
    public SetCreditLineScoringCommandHandler Puntuar() => new(Db, Reglas(), Alta(), new MatrizVigenteDeAtribuciones(Db), G.Alcance, G.Auditoria, G.Reloj);

    // ----------------------------------------------------------------------------------------- normativa nacional --

    /// <summary>El catálogo vigente desde el 2026-01-01, la usura de consumo (25 % EA) y la de microcrédito (40 % EA) desde hoy.</summary>
    public async Task NormativaAsync(decimal usuraConsumo = 0.25m, bool conUsura = true)
    {
        await CatalogoAsync(Catalogo, new DateOnly(2026, 1, 1));
        if (!conUsura) return;
        await UsuraAsync(ClaveConsumo, usuraConsumo, Hoy);
        await UsuraAsync(ClaveMicro, 0.40m, Hoy);
    }

    public async Task<NationalValue> UsuraAsync(string clave, decimal valor, DateOnly desde)
    {
        var copia = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, clave, desde, valor, Ahora.AddDays(-1), unidad: "EA");
        await CopiasDePrueba.SembrarAsync(Db, Ahora.AddDays(-1), copia);
        Db.ChangeTracker.Clear();
        return copia;
    }

    public async Task IbrAsync(decimal valor, DateOnly desde, string clave = "3M")
    {
        await CopiasDePrueba.SembrarAsync(Db, Ahora.AddDays(-1),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Ibr, clave, desde, valor, Ahora.AddDays(-1), unidad: "NominalMV"));
        Db.ChangeTracker.Clear();
    }

    public async Task CatalogoAsync(string contenido, DateOnly desde)
    {
        var replica = new NationalValueReplication
        {
            FromSequence = 1, ToSequence = 1, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = Ahora.AddDays(-1), FinishedAt = Ahora.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.CatalogoDeModalidades), Code = "CATMOD", ValidFrom = desde, Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = true,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = Interlocked.Increment(ref _secuencia),
            CentralRegisteredAt = Ahora.AddDays(-1), KnownAt = Ahora.AddDays(-1),
        });
        Db.NationalValueReplications.Add(replica);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------------------------------------------ líneas --

    /// <summary>Una versión de consumo sin libranza: tasa fija EA, mora relativa, montos de 0,5 a 30 millones, 1 a 60 meses, mensual, real/365.</summary>
    public static CreditLineVersionInput Version(decimal tasa = 0.20m, DateOnly? desde = null, string modalidad = "CONSUMO", string? subsegmento = "SinLibranza") => new()
    {
        ValidFrom = desde ?? Hoy,
        Reason = "Tasa aprobada por el consejo",
        ModalityCode = modalidad,
        SubsegmentCode = subsegmento,
        Rates = new CreditLineRatesInput
        {
            Current = new CurrentRateInput(RateKind.Fixed, tasa),
            Default = new DefaultRateInput(DefaultRateKind.RelativeToCurrent, null, 1.5m),
        },
        Amounts = new AmountRangeInput(500_000m, 30_000_000m),
        Terms = new TermRangeInput(1, 60, 60),
        Periodicities = [RepaymentPeriodicity.Monthly],
        DefaultPeriodicity = RepaymentPeriodicity.Monthly,
        DayCount = DayCountConvention.Actual365,
        PaymentForms = [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll],
        DefaultPaymentForm = RepaymentChannel.Cashier,
        Documents = [new DocumentRequirementInput("CEDULA", "Copia de cédula")],
    };

    public InsuranceInput Seguro(decimal tarifa = 0.004m, decimal? retenida = null) =>
        new("VIDA", "Vida deudores", Aseguradora.PublicId, InsuranceBase.OutstandingBalance, tarifa, RepaymentPeriodicity.Monthly, RetainedFraction: retenida);

    public static CreateCreditLineCommand Alta(string codigo, CreditLineVersionInput version) =>
        new(codigo, $"Línea {codigo}", "Libre inversión", version) { OperationKey = Guid.NewGuid() };

    /// <summary>Crea la línea y falla la prueba si no pudo.</summary>
    public async Task<CreditLineDto> LineaAsync(string codigo, CreditLineVersionInput? version = null)
    {
        var r = await Crear().Handle(Alta(codigo, version ?? Version()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return r.Value;
    }

    /// <summary>El <c>data</c> de un error como JSON.</summary>
    public static JsonElement Datos(Error error) => GobiernoDePrueba.Datos(error);
}
