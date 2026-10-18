using System.Globalization;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using static IngenIA365ERP.API.IntegrationTests.Lending.CausacionE2E;
using static IngenIA365ERP.API.IntegrationTests.Lending.DesembolsoE2E;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// T0903 (feature 013, K3, US14; quickstart §5, paso 10; FR-102; colección «Cartera e2e», los dos motores por <c>DB_PROVIDER</c>): la evaluación de
/// cartera por sus rutas (<c>/api/lending/risk/evaluations</c>) sobre una cooperativa aislada activada con la contabilidad iniciada y la matriz
/// contable completa, y un comité de riesgos con tres miembros. Un crédito desembolsado hoy (mes M):
/// <list type="bullet">
/// <item>pasado el mes, se cierra M (el crédito en A por las reglas) y se prepara la evaluación con corte el último día de M y efecto en M+1, que
/// propone D con su justificación; otra sobre el mismo corte responde <c>Lending.PortfolioEvaluation.Overlaps</c>;</item>
/// <item>el comité de riesgos la recomienda (punto <c>Recommendation</c>) y el consejo la aprueba (punto <c>Decision</c>); aplicarla con los puntos
/// cruzados responde <c>Lending.Session.ItemMismatch</c> y con los suyos la deja <c>Applied</c>;</item>
/// <item>el cierre de M+1 (el mes de efecto) califica el crédito en D con la causa <c>Evaluation</c> y el reporte de cambios lo dice; el cierre de M
/// no cambia (la evaluación nunca reescribe un cierre anterior).</item>
/// </list>
/// Bloque D (decisiones del dueño del 2026-10-15): prepararla avisa la periodicidad de la norma si sigue pendiente (<c>warnings[]</c>), y una
/// evaluación sin aplicar se descarta con motivo (<c>POST /{id}/discard</c>), lo que libera su corte.
/// Si M es diciembre, el ensayo abre el ejercicio contable siguiente: así no depende del día en que corre. (nuevo)
/// </summary>
[Collection(CarteraCollection.Nombre)]
public sealed class EvaluacionDeCarteraTests(CentralIdentityApiFixture fx)
{
    private const string Cooperativa = "evaluacion_cartera";
    private const string Evaluaciones = CierreE2E.Riesgo + "/evaluations";

    [Fact]
    public async Task La_evaluacion_recomendada_y_aprobada_recalifica_desde_el_cierre_de_su_mes_de_efecto()
    {
        var esc = await EscenarioAsync(fx, Cooperativa);
        using var http = fx.CreateClient();
        var admin = esc.Coop.TokenAdmin;
        var jefe = esc.Usuarios.Jefe.Token;
        var tenant = esc.Coop.TenantPublicId;
        var hoy = esc.Hoy;
        var despachador = new ConductorDelDespachador(fx);
        await ContabilidadDeCarteraE2E.MatrizPorPlantillaAsync(http, admin, hoy, conHuecos: false);

        // El comité de riesgos con tres miembros del maestro.
        var comite = (await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, OriginacionE2E.Organos, new
        {
            code = "RIESGOS", name = "Comité de riesgos", kind = "RiskCommittee", decidesCredit = false, minutesPrefix = "CR-",
            rules = new
            {
                quorum = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                ordinaryMajority = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                validFrom = hoy, reason = "Reglas del comité de riesgos (e2e)", source = "Reglamento del SARC, art. 12 (e2e)",
            },
        })).GetProperty("publicId").GetGuid();
        for (var i = 1; i <= 3; i++)
        {
            var persona = await CarteraE2E.PersonaAsync(http, admin, $"Riesgos{i}", "Evaluacion");
            await CarteraE2E.ExitoAsync(http, admin, HttpMethod.Post, $"{OriginacionE2E.Organos}/{comite}/members", new
            {
                personPublicId = persona, position = "Member", votes = true, validFrom = hoy, appointmentNotes = "Nombramiento del comité de riesgos (e2e)",
            });
        }

        var (deudor, _) = await DeudorAsync(http, admin, "Evaluado");
        var credito = await CierreE2E.DesembolsadoAsync(http, esc, deudor, 1_000_000m);

        var finDeM = new DateOnly(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        var inicioDeEfecto = finDeM.AddDays(1);
        var finDeEfecto = new DateOnly(inicioDeEfecto.Year, inicioDeEfecto.Month, DateTime.DaysInMonth(inicioDeEfecto.Year, inicioDeEfecto.Month));
        var mesDeEfecto = inicioDeEfecto.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        Guid evaluacion, cierreDeM;
        string huellaDeM;

        using (RelojEn(fx, inicioDeEfecto))
        {
            // ----------------------------------------------------------------------- el cierre de M: el crédito en A --
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            await despachador.PasadasAsync(tenant, 3);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, await CierreE2E.OrdenarAsync(http, jefe, finDeM.Year, finDeM.Month));
            cierreDeM = await CierreE2E.MensualAsync(http, jefe, finDeM.Year, finDeM.Month, version: 1);
            (await CierreE2E.LineasAsync(http, jefe, cierreDeM))[credito].Categoria.Should().Be("A");
            huellaDeM = (await CarteraE2E.GetAsync(http, jefe, $"{CierreE2E.Cierres}/{cierreDeM}")).GetProperty("linesSha256").GetString()!;

            // -------------------------------------------------------------------------------------- preparar --
            var cuerpo = new
            {
                cutoffDate = finDeM, effectMonth = mesDeEfecto, criteria = new { capacidadDePago = "deteriorada", fuente = "Evaluación semestral (e2e)" },
                loans = new[] { new { loanPublicId = credito, proposedCategory = "D", reason = "Pérdida del empleo del deudor (e2e)" } },
            };
            // Bloque D: una primera que se descarta con motivo (libera el corte) y el aviso de la periodicidad pendiente de la norma.
            var descartable = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Evaluaciones, cuerpo);
            descartable.StatusCode.Should().Be(HttpStatusCode.Created, await descartable.Content.ReadAsStringAsync());
            var aDescartar = await CarteraE2E.LeerAsync(descartable);
            aDescartar.GetProperty("warnings").EnumerateArray().Select(a => a.GetProperty("code").GetString())
                .Should().Contain("PortfolioEvaluationPeriodicityPending", "la periodicidad de la norma sigue pendiente de validar");
            var idDescartable = aDescartar.GetProperty("publicId").GetGuid();
            var sinMotivo = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Evaluaciones}/{idDescartable}/discard", new { reason = "" });
            sinMotivo.StatusCode.Should().Be(HttpStatusCode.BadRequest, await sinMotivo.Content.ReadAsStringAsync());
            var descartada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Evaluaciones}/{idDescartable}/discard",
                new { reason = "Se rehace con otros criterios (e2e)" });
            descartada.StatusCode.Should().Be(HttpStatusCode.OK, await descartada.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(descartada)).GetProperty("status").GetInt32().Should().Be((int)PortfolioEvaluationStatus.Discarded);
            var otraVezDescartada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, $"{Evaluaciones}/{idDescartable}/discard",
                new { reason = "Otra vez (e2e)" });
            (await CarteraE2E.CodigoDeErrorAsync(otraVezDescartada)).Should().Be("Lending.PortfolioEvaluation.InvalidStatus");

            var preparada = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Evaluaciones, cuerpo);
            preparada.StatusCode.Should().Be(HttpStatusCode.Created, await preparada.Content.ReadAsStringAsync());
            var leida = await CarteraE2E.LeerAsync(preparada);
            evaluacion = leida.GetProperty("publicId").GetGuid();
            leida.GetProperty("status").GetInt32().Should().Be((int)PortfolioEvaluationStatus.Draft);
            var linea = leida.GetProperty("loans").EnumerateArray().Should().ContainSingle().Subject;
            linea.GetProperty("ruleRatingAtCutoff").GetInt32().Should().Be((int)RatingCategory.A, "por las reglas del cierre al corte");
            linea.GetProperty("proposedCategory").GetInt32().Should().Be((int)RatingCategory.D);

            var otra = await CarteraE2E.ConClaveAsync(http, admin, HttpMethod.Post, Evaluaciones, cuerpo);
            otra.IsSuccessStatusCode.Should().BeFalse(await otra.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(otra)).Should().Be("Lending.PortfolioEvaluation.Overlaps");

            // --------------------------------------------------- el comité la recomienda y el consejo la aprueba --
            var recomendacion = await ActasE2E.DecididoAsync(http, esc, comite, LendingDecisionSubjects.PortfolioEvaluation, evaluacion, inicioDeEfecto,
                "Recommendation", recomiendaA: esc.Originacion.Consejo);
            var aprobacion = await ActasE2E.DecididoAsync(http, esc, esc.Originacion.Consejo, LendingDecisionSubjects.PortfolioEvaluation, evaluacion,
                inicioDeEfecto, "Decision");
            var aprobada = await CarteraE2E.GetAsync(http, jefe, $"{Evaluaciones}/{evaluacion}");
            aprobada.GetProperty("status").GetInt32().Should().Be((int)PortfolioEvaluationStatus.Approved, aprobada.ToString());
            aprobada.GetProperty("recommendationItem").GetProperty("itemPublicId").GetGuid().Should().Be(recomendacion);
            aprobada.GetProperty("approvalItem").GetProperty("itemPublicId").GetGuid().Should().Be(aprobacion);

            // ------------------------------------------------------------------------------------------ aplicar --
            var cruzada = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Secretaria.Token, HttpMethod.Post, $"{Evaluaciones}/{evaluacion}/apply",
                new { recommendationSessionItemPublicId = aprobacion, approvalSessionItemPublicId = recomendacion });
            cruzada.IsSuccessStatusCode.Should().BeFalse(await cruzada.Content.ReadAsStringAsync());
            (await CarteraE2E.CodigoDeErrorAsync(cruzada)).Should().Be("Lending.Session.ItemMismatch");
            var aplicar = await CarteraE2E.ConClaveAsync(http, esc.Usuarios.Secretaria.Token, HttpMethod.Post, $"{Evaluaciones}/{evaluacion}/apply",
                new { recommendationSessionItemPublicId = recomendacion, approvalSessionItemPublicId = aprobacion });
            aplicar.StatusCode.Should().Be(HttpStatusCode.OK, await aplicar.Content.ReadAsStringAsync());
            (await CarteraE2E.LeerAsync(aplicar)).GetProperty("status").GetInt32().Should().Be((int)PortfolioEvaluationStatus.Applied);
            (await CarteraE2E.GetAsync(http, jefe, Evaluaciones)).EnumerateArray()
                .Should().Contain(e => e.GetProperty("publicId").GetGuid() == evaluacion && e.GetProperty("status").GetInt32() == (int)PortfolioEvaluationStatus.Applied);
        }

        // Si el mes de efecto es de otro año, su ejercicio contable se abre (el cierre de Cartera exige el mes abierto en Contabilidad).
        if (inicioDeEfecto.Year != finDeM.Year)
        {
            var abrir = await CarteraE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = inicioDeEfecto.Year });
            abrir.IsSuccessStatusCode.Should().BeTrue($"abrir el ejercicio {inicioDeEfecto.Year}: «{await abrir.Content.ReadAsStringAsync()}»");
        }

        using (RelojEn(fx, finDeEfecto.AddDays(1)))
        {
            // ------------------------------------------------- el cierre del mes de efecto: D por la evaluación --
            await CicloDeHoyAsync(fx, http, jefe, tenant);
            await despachador.PasadasAsync(tenant, 3);
            await CierreE2E.CorrerAsync(fx, http, jefe, tenant, await CierreE2E.OrdenarAsync(http, jefe, finDeEfecto.Year, finDeEfecto.Month));
            var cierreDeEfecto = await CierreE2E.MensualAsync(http, jefe, finDeEfecto.Year, finDeEfecto.Month, version: 1);
            var lineas = await CierreE2E.LineasAsync(http, jefe, cierreDeEfecto);
            (lineas[credito].Categoria, lineas[credito].Causa).Should().Be(("D", "Evaluation"), "la evaluación aplicada es la candidata del mes de efecto");
            var cambios = (await CarteraE2E.GetAsync(http, jefe, $"{CierreE2E.Cierres}/{cierreDeEfecto}/changes?pageSize=50")).GetProperty("items")
                .EnumerateArray().ToList();
            cambios.Should().Contain(c => c.GetProperty("loanPublicId").GetGuid() == credito && c.GetProperty("current").GetString() == "D"
                                          && c.GetProperty("cause").GetString() == "Evaluation", "el reporte de cambios dice la causa");

            // El cierre de M no cambió.
            (await CarteraE2E.GetAsync(http, jefe, $"{CierreE2E.Cierres}/{cierreDeM}")).GetProperty("linesSha256").GetString().Should().Be(huellaDeM);
            (await CierreE2E.LineasAsync(http, jefe, cierreDeM))[credito].Categoria.Should().Be("A");
        }
    }
}
