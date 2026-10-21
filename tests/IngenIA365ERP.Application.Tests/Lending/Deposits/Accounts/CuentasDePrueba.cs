using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;
using IngenIA365ERP.Application.Lending.Deposits.Accounts.Parties;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;

/// <summary>
/// El escenario de las cuentas de captación y de aportes (feature 014, US2, T077): sobre <see cref="GobiernoDePrueba"/> (hoy = 2026-12-01), la Cartera con
/// créditos, aportes y captaciones activados, una oficina, una línea a la vista que admite cotitulares y menores sin depósito de apertura, una de
/// aportes, personas asociadas con su fecha de nacimiento y su autorización de datos, el perfil que autoriza captar, las listas restrictivas que coinciden
/// con quien se diga (dejan su resultado y el bloqueo automático, como la consulta real) y dos asesores con persona. Los handlers se arman con las
/// piezas reales salvo el cerrojo, las listas y la autorización de datos. (nuevo)
/// </summary>
internal sealed class CuentasDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = GobiernoDePrueba.Hoy;

    private readonly HashSet<int> _coinciden = [];
    private readonly HashSet<int> _sinAutorizacion = [];

    public CuentasDePrueba()
    {
        Contexto.Entrega.Returns(_ => Entrega);
        Contexto.PerfilAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Perfil));
        Contexto.TopesDeAportesAsync(Arg.Any<DateOnly>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success(TopesDeAportes.Ninguno));
        Contexto.AnticipacionDelAvisoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0);
        Autorizaciones.VigenteAsync(Arg.Any<Guid>(), Arg.Any<DataAuthorizationPurpose>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => _sinAutorizacion.Contains(PersonaDe(ci.ArgAt<Guid>(0)))
                ? null
                : new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, G.UtcNow.AddYears(-1), null)
                {
                    Purpose = ci.ArgAt<DataAuthorizationPurpose>(1), HasEvidence = true, ConsentPublicId = Guid.NewGuid(),
                });
        Listas.ConsultarAsync(Arg.Any<PedidoDeConsultaDeListas>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var pedido = ci.Arg<PedidoDeConsultaDeListas>();
            var coincide = _coinciden.Contains(pedido.PersonId);
            var fila = new ScreeningResult
            {
                PersonId = pedido.PersonId, Context = pedido.Contexto, ChannelCode = "CARGA-PROPIA", ListVersions = "[]",
                Outcome = coincide ? Domain.Enums.Lending.ScreeningOutcome.Match : Domain.Enums.Lending.ScreeningOutcome.NoMatch, QueriedAt = G.UtcNow,
            };
            G.Db.ScreeningResults.Add(fila);
            if (coincide)
                G.Db.DebtorBlocks.Add(DebtorBlock.Nuevo(pedido.PersonId, DebtorBlockReason.RestrictiveListMatch, fila.PublicId.ToString(), G.HoyLocal,
                    "Coincidencia de prueba", null));
            return Task.FromResult(new ConsultaDeListasRegistrada(fila, coincide, true));
        });

        G.Db.LendingSetups.Add(new LendingSetup
        {
            Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = Hoy.AddMonths(-6), DepositsActivationDate = Hoy.AddMonths(-1),
            ContributionsActivationDate = Hoy.AddMonths(-1), CreatedBy = "SYSTEM",
        });
        Oficina = new Branch { Name = "Principal", CreatedBy = "test" };
        G.Db.Branches.Add(Oficina);
        G.Db.SaveChanges();

        Asesora = G.Usuario(G.Persona("Laura", "Asesora", "5001"), "asesora@coop.test");
        Jefe = G.Usuario(G.Persona("Mario", "Jefe", "5002"), "jefe@coop.test");
        G.ActuarComo(Asesora);
        G.Conceder(PermisosDeLineas.VerCaptacion, PermisosDeLineas.AdministrarCaptacion, PermisosDeLineas.VerAportes, PermisosDeLineas.AdministrarAportes);

        Ana = Persona("Ana", "Titular", "1001", new DateOnly(1985, 3, 10), asociada: true);
        Beto = Persona("Beto", "Cotitular", "1002", new DateOnly(1980, 7, 1), asociada: true);
        Carla = Persona("Carla", "Beneficiaria", "1003", new DateOnly(2010, 1, 5), asociada: false);
        Dario = Persona("Dario", "Beneficiario", "1004", new DateOnly(2012, 6, 9), asociada: false);
        Externa = Persona("Elena", "NoAsociada", "1005", new DateOnly(1990, 1, 1), asociada: false);
    }

    public GobiernoDePrueba G { get; } = new();
    public IContextoDeLineasDeCaptacion Contexto { get; } = Substitute.For<IContextoDeLineasDeCaptacion>();
    public IAutorizacionDeDatos Autorizaciones { get; } = Substitute.For<IAutorizacionDeDatos>();
    public IConsultaDeListasDeCartera Listas { get; } = Substitute.For<IConsultaDeListasDeCartera>();
    public ICerrojoDeCartera Cerrojo { get; } = Substitute.For<ICerrojoDeCartera>();
    public EntregaDeCartera Entrega { get; set; } = EntregaDeCartera.K8;
    public PerfilParaLineas Perfil { get; set; } = new(true, false, false, "AHORRO_CREDITO", Guid.NewGuid());

    /// <summary>La entrega con que se leen los parámetros (los de K8, como la política del GMF, no se aceptan antes).</summary>
    public EntregaDeCartera EntregaDeParametros { get; set; } = EntregaDeCartera.K8;

    public Branch Oficina { get; }
    public User Asesora { get; }
    public User Jefe { get; }
    public Person Ana { get; }
    public Person Beto { get; }
    public Person Carla { get; }
    public Person Dario { get; }
    public Person Externa { get; }

    public void Dispose() => G.Dispose();

    // ---------------------------------------------------------------------------------------------------------- datos --

    public Person Persona(string nombre, string apellido, string documento, DateOnly? nacimiento, bool asociada)
    {
        var p = new Person
        {
            FirstName = nombre, LastName = apellido, TaxId = documento, IdType = "C", PersonType = "01", DateOfBirth = nacimiento, IsAssociate = asociada,
            CreatedBy = "test",
        };
        G.Db.People.Add(p);
        G.Db.SaveChanges();
        if (asociada)
        {
            G.Db.Associates.Add(new Associate { PersonId = p.Id, JoinDate = new DateOnly(2020, 1, 1), Status = "A", CreatedBy = "test" });
            G.Db.SaveChanges();
        }
        return p;
    }

    public void Coincide(Person p) => _coinciden.Add(p.Id);

    public void SinAutorizacion(Person p) => _sinAutorizacion.Add(p.Id);

    private int PersonaDe(Guid publicId) => G.Db.People.Local.FirstOrDefault(p => p.PublicId == publicId)?.Id
                                            ?? G.Db.People.Where(p => p.PublicId == publicId).Select(p => p.Id).FirstOrDefault();

    /// <summary>Una versión a la vista al 1 % con cotitulares y menores, sin depósito de apertura.</summary>
    public static DepositLineVersionInput ALaVista(decimal minimoDeApertura = 0m, bool menores = true, bool cotitulares = true) => new()
    {
        ValidFrom = Hoy,
        Reason = "Tasa aprobada por el consejo, acta 12",
        AllowsCoHolders = cotitulares,
        AllowsMinors = menores,
        AllowsPartialWithdrawals = true,
        Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.01m },
        Minimums = new DepositMinimumsInput(minimoDeApertura, 0m, 0m),
    };

    public async Task<LendingLineDto> LineaDeCaptacionAsync(string codigo = "AHVISTA", DepositLineVersionInput? version = null,
        DepositClass clase = DepositClass.Demand)
    {
        var r = await new CreateDepositLineCommandHandler(G.Db, new AltaDeVersionDeCaptacion(G.Db, G.Reloj), Contexto, G.Reloj)
            .Handle(new CreateDepositLineCommand(codigo, $"Ahorro {codigo}", clase, true, version ?? ALaVista()) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    public async Task<LendingLineDto> LineaDeAportesAsync(string codigo = "APORTES", ContributionLineVersionInput? propia = null)
    {
        var version = propia ?? new ContributionLineVersionInput
        {
            ValidFrom = Hoy,
            Reason = "Estatutos, art. 20",
            MinimumNonReducible = new ContributionAmountInput(ContributionAmountMode.FixedAmount, 500_000m),
            Installment = new ContributionInstallmentInput { Kind = ContributionAmountMode.FixedAmount, Amount = 50_000m, DueDay = 5 },
            RefundTermDays = 60,
        };
        var r = await new CreateContributionLineCommandHandler(G.Db, new AltaDeVersionDeCaptacion(G.Db, G.Reloj), Contexto, G.Reloj)
            .Handle(new CreateContributionLineCommand(codigo, "Aportes sociales", true, version) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------------- handlers --

    public ReglasDeParametrosDeCartera Parametros() => new(G.Valores, new LectorDeParametros(G.Db, EntregaDeParametros));

    public TitularidadDeCuentas Titularidad() => new(G.Db, Autorizaciones, Parametros());

    public OpenMemberAccountCommandHandler Abrir() => new(G.Db, Contexto, Titularidad(), Parametros(), Listas, G.Intervenciones(), new RegistroDeCuentas(G.Db),
        new NumeradorDeCartera(G.Db, Cerrojo), Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    public ApproveMemberAccountOpeningCommandHandler Aprobar() => new(G.Db, G.Intervenciones(), Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    public SetMemberAccountPartiesCommandHandler CambiarPartes() => new(G.Db, Titularidad(), Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    public ConfirmMemberAccountDataUpdateCommandHandler ConfirmarDatos() => new(G.Db, Autorizaciones, G.Alcance, G.Auditoria, G.Reloj);

    public MarkGmfExemptAccountCommandHandler Marcar() => new(G.Db, Parametros(), Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    public EndGmfExemptAccountCommandHandler Desmarcar() => new(G.Db, G.Alcance, G.Auditoria, G.Actor, G.Reloj);

    public MayoriaDeEdad Mayoria() => new(G.Db, Parametros());

    public ListMemberAccountsQueryHandler Listar() => new(G.Db, G.Alcance, G.Auditoria);

    public GetMemberAccountQueryHandler Leer() => new(G.Db, G.Alcance, G.Auditoria, G.Reloj);

    public GetMemberAccountPartiesQueryHandler LeerPartes() => new(G.Db, G.Alcance, G.Auditoria, G.Reloj);

    // ------------------------------------------------------------------------------------------------------- entradas --

    public OpenAccountInput Apertura(LendingLineDto linea, Person titular, IReadOnlyList<MemberAccountPartyInput>? partes = null,
        IReadOnlyList<MemberAccountBeneficiaryInput>? beneficiarios = null, HandlingMode manejo = HandlingMode.Indistinct, bool exenta = false) => new()
    {
        LinePublicId = linea.PublicId,
        HolderPersonPublicId = titular.PublicId,
        OfficePublicId = Oficina.PublicId,
        HandlingMode = manejo,
        Parties = partes ?? [],
        Beneficiaries = beneficiarios ?? [],
        // D34 (2026-10-18): con beneficiarios, la constancia se exige también al abrir.
        BeneficiaryRecordAttachmentPublicId = beneficiarios is { Count: > 0 } ? Guid.NewGuid() : null,
        MarkGmfExempt = exenta,
    };

    public async Task<Result<MemberAccountDto>> AbrirAsync(OpenAccountInput entrada)
    {
        var r = await Abrir().Handle(new OpenMemberAccountCommand(entrada) { OperationKey = Guid.NewGuid() }, default);
        G.Db.ChangeTracker.Clear();
        return r;
    }

    public async Task<MemberAccountDto> AbiertaAsync(OpenAccountInput entrada)
    {
        var r = await AbrirAsync(entrada);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }
}
