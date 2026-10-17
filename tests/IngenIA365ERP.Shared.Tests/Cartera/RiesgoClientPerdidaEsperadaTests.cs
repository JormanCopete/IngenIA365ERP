using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 013, K6a (fase 22, bloque 3, T1038–T1039; contracts/api.md §12.4, §12.5): lo que <see cref="RiesgoClient"/> suma para la pérdida esperada.
/// Registrar una adopción o un desistimiento lleva su <c>Idempotency-Key</c>; la lista de elecciones no; ninguna pone <c>Authorization</c>; el error
/// <c>NoticeOutOfTerm</c> deja leer la fecha más temprana admitida; la línea de un cierre en pérdida esperada trae sus componentes. (nuevo)
/// </summary>
public class RiesgoClientPerdidaEsperadaTests
{
    private static readonly Guid Propuesta = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Punto = Guid.Parse("22222222-3333-4444-5555-666666666666");
    private static readonly Guid Estudio = Guid.Parse("33333333-4444-5555-6666-777777777777");
    private static readonly Guid Aviso = Guid.Parse("44444444-5555-6666-7777-888888888888");

    private readonly ServidorDeCartera _servidor = new();
    private readonly RiesgoClient _cliente;

    public RiesgoClientPerdidaEsperadaTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new RiesgoClient(http, auth);
    }

    private static NuevaEleccionDelModeloRequest Pedido() =>
        new(Propuesta, "VoluntaryAdoption", ["CONSUMO_LIBRANZA"], new DateOnly(2027, 3, 1), Punto, Estudio, new DateOnly(2026, 12, 1), Aviso, "Decisión del consejo");

    [Fact]
    public async Task Registrar_la_eleccion_lleva_su_clave_y_no_pone_Authorization()
    {
        await _cliente.RegistrarEleccionAsync(Pedido(), new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/lending/risk/impairment-model/elections");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Listar_las_elecciones_no_lleva_clave()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, Array.Empty<object>());

        var r = await _cliente.EleccionesAsync();

        r.IsSuccess.Should().BeTrue();
        var vista = _servidor.Vistas.Should().ContainSingle().Subject;
        vista.Metodo.Should().Be("GET");
        vista.Ruta.Should().Be("/api/lending/risk/impairment-model/elections");
        vista.Clave.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task El_aviso_fuera_de_plazo_trae_la_fecha_mas_temprana_admitida()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.UnprocessableEntity, new
        {
            code = "Lending.ImpairmentModel.NoticeOutOfTerm",
            message = "La norma pide 39 días desde el aviso.",
            data = new { validFrom = "2027-01-01", earliestValidFrom = "2027-01-09", noticeLeadDays = 39 },
            traceId = "t",
        });

        var r = await _cliente.RegistrarEleccionAsync(Pedido(), new ClaveDeOperacion());

        r.IsSuccess.Should().BeFalse();
        r.Dato<DateOnly?>("earliestValidFrom").Should().Be(new DateOnly(2027, 1, 9));
    }

    [Fact]
    public async Task La_linea_de_un_cierre_en_perdida_esperada_trae_sus_componentes()
    {
        var cierre = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new
        {
            items = new[]
            {
                new
                {
                    loanPublicId = Guid.NewGuid(), loanNumber = "CR-1", debtor = new { publicId = Guid.NewGuid(), name = "Ana" }, modality = "CONSUMO",
                    sarcSegment = "CONSUMO", daysPastDue = 0, category = "A", subcategory = (string?)null, accountingCategory = "A", cause = "Homologation",
                    principalBalance = 1_000_000m, impairmentBase = 1_000_000m, collateralAdmissibleValue = 0m, impairmentPercentage = 0.0123m,
                    totalImpairment = 12_300m, impairmentDelta = 300m, current = true, explanation = new[] { "PI × PDI × exposición" },
                    impairmentModel = "ExpectedLossAnnex2",
                    expectedLoss = new
                    {
                        segment = "CONSUMO_LIBRANZA", z = -1.2m, score = 0.2315m, modelRating = "A", isInDefault = false, daysInDefault = (int?)null,
                        probabilityOfDefaultKey = "COOP_ESPECIALIZADA_AC", probabilityOfDefault = 0.0273m, collateralType = "SIN_GARANTIA",
                        lossGivenDefault = 0.45m, exposureAtDefault = 1_000_000m, incompleteArrearsHistory = true,
                    },
                },
            },
            page = 1, pageSize = 50, totalCount = 1,
        });

        var r = await _cliente.LineasAsync(cierre);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var linea = r.Value!.Items.Should().ContainSingle().Subject;
        linea.ImpairmentModel.Should().Be("ExpectedLossAnnex2");
        linea.ExpectedLoss!.ProbabilityOfDefault.Should().Be(0.0273m);
        linea.ExpectedLoss.LossGivenDefault.Should().Be(0.45m);
        linea.ExpectedLoss.CollateralType.Should().Be("SIN_GARANTIA");
        linea.ExpectedLoss.IncompleteArrearsHistory.Should().BeTrue();
    }
}
