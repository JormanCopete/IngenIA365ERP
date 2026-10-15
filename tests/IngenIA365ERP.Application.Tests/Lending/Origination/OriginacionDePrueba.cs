using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.Extensions.Options;
using NSubstitute;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// El escenario común de la originación (feature 013, US2, bloque 6A): el de <see cref="LineasDePrueba"/> (InMemory, hoy local 2026-12-01, los
/// lectores reales de valores nacionales, usura de consumo 25 % EA) con
/// <list type="bullet">
/// <item>la línea <c>CONLIB</c> (20 % EA fija, real/365, 0,5 a 30 millones, 1 a 60 meses, mensual, caja o nómina propia, cédula obligatoria);</item>
/// <item>Ana (asociada desde 2024-01-01, nacida en 1985, empleada), Beto (asociado), Carla (no asociada) y Darío (asociado); la oficina Norte;
/// los usuarios de la asesora y del analista, con su persona, y uno sin persona;</item>
/// <item>Cartera activada; la matriz del ensayo (analista 5, excepción, gerencia 20, comité 100 millones, consejo) con el comité y el consejo;</item>
/// <item>los límites legales <c>LIBRANZA_CAPACIDAD_DESCUENTO</c> 0,5, <c>CONCENTRACION_INDIVIDUAL</c> 0,10 y con garantía 0,20, y un patrimonio
/// técnico de 1.000.000.000 con corte 2026-11-30;</item>
/// <item>dobles de los puertos que construye 6B (listas sin coincidencia, sin cupo, sin bloqueos), del cerrojo y de las autorizaciones (todas
/// vigentes salvo las que la prueba quite).</item>
/// </list>
/// (nuevo)
/// </summary>
internal sealed class OriginacionDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = GobiernoDePrueba.Hoy;

    private readonly HashSet<(Guid Persona, DataAuthorizationPurpose Finalidad)> _sinAutorizacion = [];

    private OriginacionDePrueba()
    {
        Ana = Persona("Ana", "Solicitante", "52000001", asociadaDesde: new DateOnly(2024, 1, 1), nacimiento: new DateOnly(1985, 3, 15));
        Ana.IsEmployee = true;
        Ana.Email = "ana@correo.test";
        Beto = Persona("Beto", "Codeudor", "79000002", asociadaDesde: new DateOnly(2020, 6, 1), nacimiento: new DateOnly(1980, 1, 1));
        Carla = Persona("Carla", "Cliente", "41000003", asociadaDesde: null, nacimiento: new DateOnly(1990, 1, 1));
        Dario = Persona("Darío", "Asociado", "80000004", asociadaDesde: new DateOnly(2019, 1, 1), nacimiento: new DateOnly(1975, 1, 1));
        Db.SaveChanges();

        Asesora = G.Usuario(G.Persona("Asesora", "Oficina Norte", "1001001"), "asesora@coop.test");
        Analista = G.Usuario(G.Persona("Analista", "Crédito", "1001002"), "analista@coop.test");
        SinPersona = G.Usuario(null, "sinpersona@coop.test");
        Norte = new Branch { Name = "Oficina Norte", CreatedBy = "test" };
        Db.Branches.Add(Norte);
        Db.SaveChanges();

        G.Limite(CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, 0.5m, "Ley 1527 de 2012");
        G.Limite(CatalogoDeLimitesLegales.ConcentracionIndividual, 0.10m, "Decreto 961 de 2018");
        G.Limite(CatalogoDeLimitesLegales.ConcentracionIndividualConGarantia, 0.20m, "Decreto 961 de 2018");
        PatrimonioTecnico = new IndicadorRegistradoDto(Guid.NewGuid(), 1_000_000_000m, "COP", new DateOnly(2026, 11, 30), "Contabilidad");
        Contabilidad.IndicadoresDeLaEntidadAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new IndicadoresDeLaEntidadDto(ci.Arg<DateOnly>(), 0m, 0m, PatrimonioTecnico, null))));

        Autorizaciones.VigenteAsync(Arg.Any<Guid>(), Arg.Any<DataAuthorizationPurpose>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci => _sinAutorizacion.Contains((ci.ArgAt<Guid>(0), ci.ArgAt<DataAuthorizationPurpose>(1)))
                ? null
                : new AutorizacionVigente("Accepted", true, Guid.NewGuid(), 1, G.UtcNow.AddYears(-1), ci.ArgAt<string?>(2))
                {
                    Purpose = ci.ArgAt<DataAuthorizationPurpose>(1), HasEvidence = true, ConsentPublicId = Guid.NewGuid(),
                });
        Autorizaciones.AgregarAsync(Arg.Do<AutorizacionAAgregar>(Agregadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(Guid.NewGuid()));

        Listas.ConsultarAsync(Arg.Any<PedidoDeConsultaDeListas>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var pedido = ci.Arg<PedidoDeConsultaDeListas>();
            var resultado = ResultadoDeListas(pedido.PersonId);
            var fila = new ScreeningResult
            {
                PersonId = pedido.PersonId, Context = pedido.Contexto, ApplicationId = pedido.ApplicationId, ChannelCode = "CARGA-PROPIA",
                ListVersions = "[]", Outcome = resultado, QueriedAt = G.UtcNow,
            };
            Db.ScreeningResults.Add(fila);
            return Task.FromResult(new ConsultaDeListasRegistrada(fila, fila.Bloquea, fila.CuentaComoConsulta(enProduccion: false)));
        });
        Cupos.CalcularAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(CupoDe.GetValueOrDefault(ci.ArgAt<int>(0), DisponibleDeCupoCalculado.SinCupo)));
        Bloqueos.VigentesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<DebtorBlock>>(BloqueosVigentes.Where(b => ci.Arg<IReadOnlyCollection<int>>().Contains(b.PersonId)).ToList()));
    }

    /// <summary>Arma el escenario: normativa, línea <c>CONLIB</c>, matriz y Cartera activada (salvo <paramref name="activada"/> falso).</summary>
    /// <remarks><paramref name="usuraHasta"/>: la usura de consumo y de microcrédito rige desde hoy sólo hasta esa fecha (para probar el faltante).</remarks>
    public static async Task<OriginacionDePrueba> CrearAsync(bool activada = true, CreditLineVersionInput? version = null, DateOnly? usuraHasta = null)
    {
        var o = new OriginacionDePrueba();
        if (usuraHasta is { } hasta)
        {
            await o.L.NormativaAsync(conUsura: false);
            await CopiasDePrueba.SembrarAsync(o.Db, o.G.UtcNow.AddDays(-1),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveConsumo, Hoy, 0.25m, o.G.UtcNow.AddDays(-1), hasta, unidad: "EA"),
                CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, LineasDePrueba.ClaveMicro, Hoy, 0.40m, o.G.UtcNow.AddDays(-1), hasta, unidad: "EA"));
            o.Db.ChangeTracker.Clear();
        }
        else
            await o.L.NormativaAsync();
        o.Linea = await o.L.LineaAsync("CONLIB", version ?? VersionDeLinea());
        await o.MatrizAsync();
        if (activada) o.Activar();
        o.G.ActuarComo(o.Asesora);
        return o;
    }

    public LineasDePrueba L { get; } = new();
    public GobiernoDePrueba G => L.G;
    public TestApplicationDbContext Db => L.Db;

    public IAutorizacionDeDatos Autorizaciones { get; } = Substitute.For<IAutorizacionDeDatos>();
    public List<AutorizacionAAgregar> Agregadas { get; } = [];
    public IContabilidadParaCartera Contabilidad { get; } = Substitute.For<IContabilidadParaCartera>();
    public IConsultaDeListasDeCartera Listas { get; } = Substitute.For<IConsultaDeListasDeCartera>();
    public IDisponibleDeCupo Cupos { get; } = Substitute.For<IDisponibleDeCupo>();
    public IBloqueosDelDeudor Bloqueos { get; } = Substitute.For<IBloqueosDelDeudor>();
    public ICerrojoDeCartera Cerrojo { get; } = Substitute.For<ICerrojoDeCartera>();
    public CanalesExternosOptions Opciones { get; } = new() { AmbienteDePruebas = true };
    public Dictionary<int, DisponibleDeCupoCalculado> CupoDe { get; } = [];
    public List<DebtorBlock> BloqueosVigentes { get; } = [];
    public Dictionary<int, ScreeningOutcome> ListasDe { get; } = [];
    public IndicadorRegistradoDto? PatrimonioTecnico { get; set; }

    public Person Ana { get; }
    public Person Beto { get; }
    public Person Carla { get; }
    public Person Dario { get; }
    public User Asesora { get; }
    public User Analista { get; }
    public User SinPersona { get; }
    public Branch Norte { get; }
    public CreditLineDto Linea { get; private set; } = null!;
    public GoverningBody Comite { get; private set; } = null!;
    public GoverningBody Consejo { get; private set; } = null!;

    public void Dispose() => L.Dispose();

    // ------------------------------------------------------------------------------------------------------------ datos --

    /// <summary>La versión de <c>CONLIB</c>: la de <see cref="LineasDePrueba.Version"/> con caja y nómina propia.</summary>
    public static CreditLineVersionInput VersionDeLinea() => LineasDePrueba.Version() with
    {
        PaymentForms = [RepaymentChannel.Cashier, RepaymentChannel.OwnPayroll],
        Documents = [new DocumentRequirementInput("CEDULA", "Copia de cédula")],
    };

    public Person Persona(string nombre, string apellido, string documento, DateOnly? asociadaDesde, DateOnly? nacimiento)
    {
        var p = new Person
        {
            FirstName = nombre, LastName = apellido, TaxId = documento, IdType = "C", CreatedBy = "test", DateOfBirth = nacimiento,
            IsAssociate = asociadaDesde is not null,
        };
        Db.People.Add(p);
        Db.SaveChanges();
        if (asociadaDesde is { } desde)
        {
            Db.Associates.Add(new Associate { PersonId = p.Id, JoinDate = desde, Status = "A", CreatedBy = "test" });
            Db.SaveChanges();
        }
        return p;
    }

    public void Activar()
    {
        var setup = Db.LendingSetups.FirstOrDefault(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        if (setup is null)
        {
            setup = new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica };
            Db.LendingSetups.Add(setup);
        }
        setup.ActivationDate = Hoy.AddDays(-30);
        setup.ActivatedAt = G.UtcNow.AddDays(-30);
        Db.SaveChanges();
    }

    public void QuitarAutorizacion(Person p, DataAuthorizationPurpose finalidad) => _sinAutorizacion.Add((p.PublicId, finalidad));

    private ScreeningOutcome ResultadoDeListas(int persona) => ListasDe.GetValueOrDefault(persona, ScreeningOutcome.NoMatch);

    /// <summary>Un crédito vivo con su capital (una fila de capital del libro), y codeudores si se dan.</summary>
    public Loan Credito(Person deudor, decimal capital, string numero, params Person[] codeudores)
    {
        var credito = new Loan
        {
            Number = numero, DebtorPersonId = deudor.Id, BranchId = Norte.Id, CreditLineId = 1, Status = LoanStatus.Active,
            ApprovedAmount = capital, OpenedOn = new DateOnly(2026, 1, 15), CreatedBy = "test",
        };
        Db.Loans.Add(credito);
        Db.SaveChanges();
        Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = credito.Id, Kind = LoanTransactionKind.Disbursement, Concept = LoanConcept.Principal, Amount = capital,
            ValueDate = credito.OpenedOn, AccountingDate = credito.OpenedOn, RegisteredAt = G.UtcNow.AddMonths(-10), OperationId = 1,
        });
        foreach (var c in codeudores)
            Db.LoanParties.Add(new LoanParty { LoanId = credito.Id, PersonId = c.Id, Role = CreditPartyRole.CoDebtor, JoinedOn = credito.OpenedOn });
        Db.SaveChanges();
        return credito;
    }

    // ------------------------------------------------------------------------------------------------------- gobierno --

    private async Task MatrizAsync()
    {
        Comite = G.Organo("COMCRED", "Comité de crédito", GoverningBodyKind.CreditCommittee);
        Consejo = G.Organo("CONSEJO", "Consejo de administración", GoverningBodyKind.BoardOfDirectors, vinculadosN: 4, vinculadosD: 5);
        List<AttributionLevelInput> niveles =
        [
            new("ANALISTA", "Analista", 10, AttributionLevelKind.Individual, "Lending.Decisions.Analyst", ConsolidatedAmountCeiling: 5_000_000m),
            new("EXCEP", "Jefe de cartera (excepción)", 15, AttributionLevelKind.Individual, "Lending.Decisions.Exceptions",
                ConsolidatedAmountCeiling: 5_000_000m, IsExceptionLevel: true),
            new("GERENCIA", "Gerencia", 20, AttributionLevelKind.Individual, "Lending.Decisions.Management", ConsolidatedAmountCeiling: 20_000_000m),
            new("COMITE", "Comité de crédito", 30, AttributionLevelKind.Collegiate, GoverningBodyPublicId: Comite.PublicId,
                ConsolidatedAmountCeiling: 100_000_000m),
            new("CONSEJO", "Consejo de administración", 40, AttributionLevelKind.Collegiate, GoverningBodyPublicId: Consejo.PublicId, IsBoardLevel: true),
        ];
        var r = await new AddAttributionMatrixVersionCommandHandler(Db, G.Actor, G.Reloj).Handle(
            new AddAttributionMatrixVersionCommand(Hoy, "Reglamento de crédito", "Acuerdo 12", niveles, []) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        Db.ChangeTracker.Clear();
    }

    // --------------------------------------------------------------------------------------------------------- piezas --

    public ReglasDeParametrosDeCartera Parametros() => new(G.Valores, G.Parametros());
    public SimuladorDeCredito Simulador() => new(Db, L.Reglas());
    public SimulacionEnAlcance Simulacion() => new(Db, Simulador(), G.Alcance, G.Auditoria);
    public SimulateRepaymentScheduleQueryHandler Simular() => new(Simulacion(), G.Reloj);
    public ExposicionConsolidadaDelDeudor Exposicion() => new(Db);
    public GrupoConectadoDelDeudor Grupos() => new(Db);
    public ValidacionDeConcentracion Concentracion() =>
        new(Db, Contabilidad, Parametros(), Exposicion(), Grupos(), L.Alertas);
    public CapacidadDePago Capacidad() => new(Db, G.Valores);
    public ValidacionesDeOriginacion Validaciones() =>
        new(Db, Listas, new GuardiaDeCanalesExternos(Options.Create(Opciones)), Capacidad(), Exposicion(), Cupos, Concentracion(),
            new VinculadosVigentes(Db), Bloqueos, Autorizaciones, SaldosDeAportes(), Convenios());
    /// <summary>K2 (T0815): los convenios de libranza, para la capacidad de descuento de una solicitud por pagaduría.</summary>
    public Application.Lending.Payroll.ConveniosDeLibranza Convenios() => new(Db, Parametros(), G.Valores, G.Perfil);
    public Application.Lending.Origination.CreditLimits.SaldoDeAportesVigente SaldosDeAportes() =>
        new(Db, new Application.Common.Integration.Savings.ConsultaDeAportesNoDisponible());
    public EnrutamientoDeSolicitud Enrutamiento() => new(Db, new MatrizVigenteDeAtribuciones(Db), Exposicion(), new VinculadosVigentes(Db), G.Reloj);
    public EvaluacionDeSolicitud Evaluacion() => new(Db, Simulador(), Validaciones(), Enrutamiento(), G.Reloj);
    public ArmadoDeSolicitud Armado() => new(Db, Simulador(), G.Perfil, G.Alcance, G.Auditoria);

    public CreateCreditApplicationCommandHandler Crear() => new(Db, Armado(), Autorizaciones, G.Actor, G.Reloj);
    public UpdateCreditApplicationCommandHandler Actualizar() => new(Db, Armado(), Autorizaciones, G.Alcance, G.Auditoria, G.Reloj);
    public AttachCreditApplicationDocumentCommandHandler Adjuntar() => new(Db, G.Alcance, G.Auditoria, G.Reloj);
    public RemoveCreditApplicationDocumentCommandHandler Quitar() => new(Db, G.Alcance, G.Auditoria);
    public SubmitCreditApplicationCommandHandler Radicar() =>
        new(Db, Evaluacion(), Enrutamiento(), G.Intervenciones(), new NumeradorDeCartera(Db, Cerrojo), Cerrojo, Autorizaciones, G.Alcance, G.Auditoria,
            G.Reloj);
    public RecordCreditApplicationAnalysisCommandHandler Analizar() =>
        new(Db, Evaluacion(), Enrutamiento(), G.Intervenciones(), G.Alcance, G.Auditoria, G.Reloj);
    public GetCreditApplicationRoutingQueryHandler Enrutado() => new(Db, Enrutamiento(), G.Alcance, G.Auditoria, G.Reloj);

    // --------------------------------------------------------------------------------------- piezas del bloque 6B --

    public Application.Lending.Attributions.AtribucionDelDecisor Atribucion() => new(Db, G.Permisos);
    public GuardiaDeCanalesExternos Guardia() => new(Options.Create(Opciones));
    public Application.Lending.Origination.CreditLimits.BloqueosDelDeudor BloqueosReales() => new(Db);
    public Application.Lending.Origination.CreditLimits.DisponibleDeCupo Disponible() => new(Db, new VersionVigenteDeLinea(Db));
    public Application.Lending.Origination.CreditLimits.EnrutamientoDeCupo EnrutamientoDeCupo() =>
        new(Db, new MatrizVigenteDeAtribuciones(Db), Exposicion(), new VinculadosVigentes(Db), new VersionVigenteDeLinea(Db), G.Reloj);
    public DecisionSobreSolicitud DecisionDeSolicitud() =>
        new(Db, Evaluacion(), Enrutamiento(), Concentracion(), G.Intervenciones(), Atribucion(), Parametros(), Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public Application.Lending.Origination.CreditLimits.DecisionSobreCupo DecisionDeCupo() =>
        new(Db, EnrutamientoDeCupo(), Concentracion(), BloqueosReales(), G.Intervenciones(), Atribucion(), Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public Application.Lending.Attributions.RecordCreditDecisionCommandHandler Decidir() => new([DecisionDeSolicitud(), DecisionDeCupo()]);
    public Application.Lending.Origination.CreditLimits.AssignCreditLimitCommandHandler Asignar(IConsultaDeListasDeCartera? listas = null,
        Application.Lending.Origination.CreditLimits.SaldoDeAportesVigente? saldos = null) =>
        new(Db, EnrutamientoDeCupo(), Concentracion(), listas ?? Listas, BloqueosReales(), saldos ?? SaldosDeAportes(), new VersionVigenteDeLinea(Db),
            Autorizaciones, G.Perfil, Guardia(), G.Intervenciones(), Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public Application.Lending.Origination.CreditLimits.RevalidacionDeCupos Revalidacion() =>
        new(Db, new VinculadosVigentes(Db), Concentracion(), Disponible(), L.Alertas, G.Actor, G.Reloj);

    /// <summary>Un rol con <c>Lending.CreditLimits.Assign</c> para el usuario, con el monto operativo dado (nulo = sin fila: sin límite).</summary>
    public void MontoOperativo(User usuario, decimal? maximo)
    {
        var permiso = Db.Permissions.FirstOrDefault(p => p.Resource == "Lending.CreditLimits" && p.Action == "Assign");
        if (permiso is null)
        {
            permiso = new Permission { Resource = "Lending.CreditLimits", Action = "Assign" };
            Db.Permissions.Add(permiso);
            Db.SaveChanges();
        }
        var rol = new Role { Code = "CUPOS" + usuario.Id, Name = "Cupos " + usuario.Id, IsActive = true };
        Db.Roles.Add(rol);
        Db.SaveChanges();
        Db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = permiso.Id });
        Db.UserRoles.Add(new UserRole { UserId = usuario.Id, RoleId = rol.Id });
        if (maximo is { } m)
            Db.PermissionAmountLimits.Add(new PermissionAmountLimit
            {
                RoleId = rol.Id, PermissionCode = "Lending.CreditLimits.Assign", MaxAmount = m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            });
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Un usuario con persona (para decidir, votar o liberar).</summary>
    public User UsuarioConPersona(string nombre, string documento) => G.Usuario(G.Persona(nombre, "Prueba", documento), $"{nombre.ToLowerInvariant()}@coop.test");

    // ------------------------------------------------------------------------------------------------------ solicitudes --

    public CreditApplicationInput Solicitud(decimal monto = 3_000_000m, int cuotas = 24, Person? solicitante = null, params Person[] codeudores) => new()
    {
        ApplicantPersonPublicId = (solicitante ?? Ana).PublicId,
        OfficePublicId = Norte.PublicId,
        CreditLinePublicId = Linea.PublicId,
        Purpose = "Libre inversión",
        Amount = monto,
        Installments = cuotas,
        PaymentForm = RepaymentChannel.Cashier,
        PrepaymentPreference = PrepaymentChoice.ReduceTerm,
        ApplicantCapacity = new CapacityInput(5_000_000m, 1_500_000m, 0m),
        CoDebtors = codeudores.Select(c => new CreditApplicationPartyInput(c.PublicId, 4_000_000m, 1_000_000m, 0m)).ToList(),
    };

    /// <summary>Crea la solicitud y falla la prueba si no pudo.</summary>
    public async Task<CreditApplicationDto> BorradorAsync(CreditApplicationInput? entrada = null)
    {
        var r = await Crear().Handle(new CreateCreditApplicationCommand(entrada ?? Solicitud()) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return r.Value;
    }

    /// <summary>Adjunta los documentos obligatorios de la solicitud (un adjunto <c>Available</c> por requisito y parte).</summary>
    public async Task DocumentosAsync(CreditApplicationDto s, Person? de = null)
    {
        foreach (var requisito in s.Requirements.Where(r => r.Mandatory))
        {
            var adjunto = Adjunto(s.PublicId);
            var r = await Adjuntar().Handle(new AttachCreditApplicationDocumentCommand(s.PublicId, requisito.Code, adjunto.PublicId, Hoy, de?.PublicId)
            {
                OperationKey = Guid.NewGuid(),
            }, default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        }
        Db.ChangeTracker.Clear();
    }

    public Attachment Adjunto(Guid solicitud)
    {
        var a = new Attachment
        {
            OwnerEntityType = Application.Attachments.Common.AdjuntosDeModulo.DocumentoDeSolicitud, OwnerEntityPublicId = solicitud,
            FileName = "cedula.pdf", ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x",
            Status = Domain.Enums.Core.EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        Db.Attachments.Add(a);
        Db.SaveChanges();
        return a;
    }

    public SubmitCreditApplicationCommand Radicacion(CreditApplicationDto s) => new(s.PublicId) { OperationKey = Guid.NewGuid() };

    /// <summary>Crea, adjunta y radica; falla la prueba si no pudo.</summary>
    public async Task<CreditApplicationDto> RadicadaAsync(CreditApplicationInput? entrada = null)
    {
        var s = await BorradorAsync(entrada);
        await DocumentosAsync(s);
        var r = await Radicar().Handle(Radicacion(s), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return r.Value;
    }

    public static System.Text.Json.JsonElement Datos(Error error) => GobiernoDePrueba.Datos(error);
}
