using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// Feature 013, T0257 (US17; T69, C88, FR-006; api.md §14.4): <see cref="ListCreditReportClaimsQuery"/> en K1 es sólo lectura sobre
/// <c>LND_CreditReportClaims</c> (lo migrado): filtra por estado y persona, respeta el alcance por la oficina del crédito —lo de otra
/// oficina no aparece ni cuenta— y Cartera no expone en K1 ningún comando que escriba reclamos.
/// </summary>
public sealed class CreditReportClaimQueriesTests : IDisposable
{
    private const int Norte = 1;
    private const int Sur = 2;

    private readonly GobiernoDePrueba _g = new();
    private readonly Person _ana;
    private readonly Person _beto;

    public CreditReportClaimQueriesTests()
    {
        _ana = _g.Persona("Ana", "Deudora", "9001");
        _beto = _g.Persona("Beto", "Deudor", "9002");
        var deNorte = Credito("CR-001", Norte, _ana);
        var deSur = Credito("CR-002", Sur, _beto);
        Reclamo(_ana, deNorte, CreditReportClaimStatus.InProcess, new DateOnly(2026, 11, 1));
        Reclamo(_ana, deNorte, CreditReportClaimStatus.Answered, new DateOnly(2026, 10, 1));
        Reclamo(_beto, deSur, CreditReportClaimStatus.InProcess, new DateOnly(2026, 11, 2), prorrogado: true);
        Reclamo(_beto, null, CreditReportClaimStatus.InProcess, new DateOnly(2026, 11, 3));
    }

    public void Dispose() => _g.Dispose();

    private Loan Credito(string numero, int oficina, Person deudor)
    {
        var l = new Loan { Number = numero, BranchId = oficina, CreditLineId = 1, DebtorPersonId = deudor.Id, OpenedOn = new DateOnly(2025, 1, 10) };
        _g.Db.Loans.Add(l);
        _g.Db.SaveChanges();
        return l;
    }

    private void Reclamo(Person p, Loan? credito, CreditReportClaimStatus estado, DateOnly recibido, bool prorrogado = false)
    {
        _g.Db.CreditReportClaims.Add(new CreditReportClaim
        {
            PersonId = p.Id, LoanId = credito?.Id, Kind = CreditReportClaimKind.Claim, Status = estado, ReceivedOn = recibido, Channel = "Oficina",
            Description = "No reconoce la mora reportada", DueDate = recibido.AddDays(21), ExtendedDueDate = prorrogado ? recibido.AddDays(33) : null,
        });
        _g.Db.SaveChanges();
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<IngenIA365ERP.Application.Common.Paging.PagedResult<CreditReportClaimDto>>> Listar(
        ListCreditReportClaimsQuery q) => new ListCreditReportClaimsQueryHandler(_g.Db, _g.Alcance).Handle(q, default);

    [Fact]
    public async Task Con_todas_las_oficinas_ve_todos_los_reclamos_el_mas_reciente_primero()
    {
        var r = await Listar(new ListCreditReportClaimsQuery());

        r.Value.TotalCount.Should().Be(4);
        r.Value.Items.Select(i => i.ReceivedAt).Should().BeInDescendingOrder();
        var prorrogado = r.Value.Items.Single(i => i.Loan?.Number == "CR-002");
        prorrogado.Extended.Should().BeTrue();
        prorrogado.DueDate.Should().Be(new DateOnly(2026, 12, 5), "el vencimiento es el prorrogado");
    }

    [Fact]
    public async Task Filtra_por_estado_y_por_persona()
    {
        var enTramite = await Listar(new ListCreditReportClaimsQuery(Status: CreditReportClaimStatus.InProcess));
        var deAna = await Listar(new ListCreditReportClaimsQuery(PersonPublicId: _ana.PublicId));

        enTramite.Value.TotalCount.Should().Be(3);
        deAna.Value.Items.Should().HaveCount(2).And.OnlyContain(i => i.Person.PersonPublicId == _ana.PublicId);
    }

    [Fact]
    public async Task Respeta_el_alcance_por_la_oficina_del_credito_y_lo_de_fuera_no_cuenta()
    {
        _g.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { Norte }, TodasLasLineas = true };

        var r = await Listar(new ListCreditReportClaimsQuery());

        r.Value.TotalCount.Should().Be(2, "los de Sur y el reclamo sin crédito quedan fuera del alcance");
        r.Value.Items.Should().OnlyContain(i => i.Loan!.Number == "CR-001");
    }

    [Fact]
    public void Los_comandos_que_escriben_reclamos_son_los_de_K4()
    {
        var comandos = typeof(ListCreditReportClaimsQuery).Assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith("IngenIA365ERP.Application.Lending", StringComparison.Ordinal) == true)
            .Where(t => t.GetInterfaces().Any(i => i == typeof(IRequest) || i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .Where(t => t.Name.Contains("CreditReportClaim", StringComparison.Ordinal))
            .Select(t => t.Name)
            .ToList();

        comandos.Should().BeEquivalentTo(nameof(ListCreditReportClaimsQuery), "RegisterCreditReportClaimCommand", "ExtendCreditReportClaimCommand",
            "AnswerCreditReportClaimCommand", "WithdrawCreditReportClaimCommand");
    }
}
