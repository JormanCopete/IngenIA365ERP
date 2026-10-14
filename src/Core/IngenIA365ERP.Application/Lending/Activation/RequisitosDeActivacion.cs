using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Los requisitos de la activación de Cartera (feature 013, T0233, C107; US8, T0747; FR-142, T84; data-model §30.1; api.md §19.3,
/// <c>ActivationReadinessDto</c>). Cada requisito sale con su estado (<see cref="EstadosDeRequisito"/>) y, si falta, quién lo corrige:
/// <list type="bullet">
/// <item><c>LoadApplied</c>, <c>DifferencesAccepted</c>, <c>SalesReconciled</c>: la carga está <c>Applied</c>, su cuadre y su conciliación de ventas
/// sin diferencias o con todas aceptadas (la carga sin partes de C107 está cuadrada y conciliada);</item>
/// <item><c>DateInTerm</c> y <c>AccountingMonthOpen</c>: hoy no pasa del plazo de la carga y el mes de la fecha de activación está abierto en
/// Contabilidad (<see cref="IContabilidadParaCartera.EstadoDelPeriodoAsync"/>);</item>
/// <item><c>ReceiptsCollectorRegistered</c> y <c>TreasuryDestinationRegistered</c>: un recaudador <c>Receipts</c> y un destino <c>Treasury</c> que
/// cuentan en el registro —en producción, reales y con la adopción vigente de <c>ProveedorDeObligaciones</c> y de <c>OrdenDeGiroEmitida</c>—; fuera de producción también los dobles <c>Test</c>
/// (<c>TST</c>, <c>TES</c>); en producción un doble nunca cuenta;</item>
/// <item><c>ExpectedLossAvailable</c>: la cooperativa obligada a la pérdida esperada a la fecha de activación —o desde una fecha posterior, por un
/// perfil regulatorio ya registrado— necesita la entrega que la trae (K6a); fuera de producción no bloquea y la activación sella
/// <c>ObligatoryModelUnavailable</c> (FR-105). <c>ExpectedLossParameters</c> y <c>ArrearsHistoryComplete</c> sólo aplican donde esa entrega está
/// desplegada;</item>
/// <item><b>en producción</b> (data-model §30.1): <c>Annex1Current</c> (el Anexo 1 vigente a la fecha de activación), <c>UsuryForActiveLines</c> (la
/// usura certificada de la modalidad de cada línea activa a esa fecha, <see cref="IValoresNacionales.LeerSerieAsync"/>, sin alertar),
/// <c>LendingRolesCreated</c> (el rol de cada plantilla <c>cartera.*</c> de <see cref="PerfilesSugeridos"/>, por su código; notas de coordinación,
/// contradicción 13), <c>OfficesLinkedToAccountingBranches</c> (toda oficina con créditos en la carga vinculada a una sucursal contable) y
/// <c>PayrollBridgeRuleResolved</c> (la regla <c>RecaudoPorAplicar</c> de la clase <c>Payroll</c>, preguntada a
/// <see cref="IContabilidadParaCartera.EvaluarAsync"/> con un <c>PagoAplicado</c> de nómina). Fuera de producción se evalúan igual y lo que falte
/// queda <c>Waived</c>: no bloquea.</item>
/// </list>
/// Avisos que no bloquean: <c>CommercialParametersToAdjust</c>, <c>PayrollConceptAccounts</c> (lo pregunta la pantalla a Nómina; el comando no lee
/// <c>PAY_</c>) y <c>UsersWithoutScope</c>. El carácter de producción es el del despliegue (<see cref="IAmbienteDeDespliegue"/>, que la API toma de
/// <c>IHostEnvironment.IsProduction()</c>), nunca el de la cooperativa. Las dependencias que suma US8 son opcionales: sin ellas sus requisitos quedan
/// <c>NotApplicable</c> (las pruebas de la activación sin cartera previa las omiten). Sólo lee. (nuevo)
/// </summary>
public sealed class RequisitosDeActivacion(
    IRegistroDeIntegracion registro,
    IContabilidadParaCartera contabilidad,
    IAmbienteDeDespliegue ambiente,
    IApplicationDbContext? db = null,
    INormativaVigente? normativa = null,
    IPerfilRegulatorio? perfiles = null,
    IValoresNacionales? valores = null)
{
    public const string CargaAplicada = "LoadApplied";
    public const string DiferenciasAceptadas = "DifferencesAccepted";
    public const string VentasConciliadas = "SalesReconciled";
    public const string FechaEnPlazo = "DateInTerm";
    public const string MesContableAbierto = "AccountingMonthOpen";
    public const string RecaudadorRegistrado = "ReceiptsCollectorRegistered";
    public const string TesoreriaRegistrada = "TreasuryDestinationRegistered";
    public const string PerdidaEsperadaDisponible = "ExpectedLossAvailable";
    public const string ParametrosDePerdidaEsperada = "ExpectedLossParameters";
    public const string HistorialDeMoraCompleto = "ArrearsHistoryComplete";
    public const string Anexo1Vigente = "Annex1Current";
    public const string UsuraDeLineasActivas = "UsuryForActiveLines";
    public const string RolesDeCarteraCreados = "LendingRolesCreated";
    public const string OficinasVinculadas = "OfficesLinkedToAccountingBranches";
    public const string PuenteDeNominaResuelto = "PayrollBridgeRuleResolved";

    public const string AvisoParametrosComerciales = "CommercialParametersToAdjust";
    public const string AvisoCuentasDeNomina = "PayrollConceptAccounts";
    public const string AvisoUsuariosSinAlcance = "UsersWithoutScope";

    /// <summary>Los requisitos que la activación responde con su propio código y no en <c>RequirementsMissing</c>.</summary>
    public static IReadOnlySet<string> ConCodigoPropio { get; } = new HashSet<string>(StringComparer.Ordinal) { DiferenciasAceptadas, VentasConciliadas };

    public bool EsProduccion => ambiente.EsProduccion;

    /// <summary>
    /// Cada requisito con su estado para activar la carga <paramref name="carga"/> en <paramref name="fechaDeActivacion"/>, mirado
    /// <paramref name="hoy"/>. Sólo lee.
    /// </summary>
    public async Task<IReadOnlyList<RequisitoDeActivacionDto>> EvaluarAsync(
        MigrationLoad? carga, DateOnly fechaDeActivacion, DateOnly hoy, CancellationToken ct) =>
        (await EvaluarConAvisosAsync(carga, fechaDeActivacion, hoy, ct)).Requisitos;

    /// <summary>Los requisitos, los avisos que no bloquean y si la cooperativa obligada se activa sin el modelo obligatorio (FR-105).</summary>
    public async Task<EvaluacionDeActivacion> EvaluarConAvisosAsync(MigrationLoad? carga, DateOnly fechaDeActivacion, DateOnly hoy, CancellationToken ct)
    {
        var requisitos = new List<RequisitoDeActivacionDto>(16)
        {
            carga is { Status: MigrationLoadStatus.Applied }
                ? Cumple(CargaAplicada, "Hay una carga aplicada.")
                : Falta(CargaAplicada, "aplicar una carga de migración (aunque no traiga créditos)", "Cartera", "/cartera/migracion", "Lending.Migration.Load"),

            carga is { Status: MigrationLoadStatus.Applied, IsBalanced: true }
                ? Cumple(DiferenciasAceptadas, "El cuadre contra Contabilidad no tiene diferencias pendientes.")
                : Falta(DiferenciasAceptadas, "aceptar con motivo y acta las diferencias del cuadre de la carga, o corregir la carga", "Cartera",
                    "/cartera/migracion", "Lending.Migration.AcceptDifferences"),

            carga is { Status: MigrationLoadStatus.Applied, SalesReconciled: true }
                ? Cumple(VentasConciliadas, "La conciliación de ventas no tiene diferencias pendientes.")
                : Falta(VentasConciliadas, "conciliar las ventas a crédito anteriores al corte y aceptar las que quedan sin migrar", "Cartera",
                    "/cartera/migracion", "Lending.Migration.AcceptDifferences"),

            carga?.ActivationDeadline is { } plazo && hoy <= plazo
                ? Cumple(FechaEnPlazo, string.Create(CultureInfo.InvariantCulture, $"Se registra a más tardar el {plazo:yyyy-MM-dd}."))
                : Falta(FechaEnPlazo, carga?.ActivationDeadline is { } vencido
                    ? string.Create(CultureInfo.InvariantCulture, $"activar dentro del plazo de la carga (venció el {vencido:yyyy-MM-dd}): aplique una carga con un corte nuevo")
                    : "una carga aplicada con plazo de activación", "Cartera", "/cartera/migracion", "Lending.Migration.Load"),
        };

        var periodo = await contabilidad.EstadoDelPeriodoAsync(fechaDeActivacion, ct);
        requisitos.Add(periodo.IsSuccess && periodo.Value is { AccountingInitialized: true, State: EstadoDePeriodoContable.Open }
            ? Cumple(MesContableAbierto, string.Create(CultureInfo.InvariantCulture, $"El mes de {fechaDeActivacion:yyyy-MM} está abierto en Contabilidad."))
            : Falta(MesContableAbierto, periodo.IsSuccess && !periodo.Value.AccountingInitialized
                ? "iniciar la contabilidad"
                : string.Create(CultureInfo.InvariantCulture, $"el mes de {fechaDeActivacion:yyyy-MM} abierto en Contabilidad"),
                "Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen"));

        var participantes = await registro.QueCuentanAsync(fechaDeActivacion, ct);
        var recaudador = participantes.Any(p => p.Role == IntegrationParticipantRole.ObligationsCollector
            && (p.Class == IntegrationParticipantClass.Receipts || (!ambiente.EsProduccion && p.Class == IntegrationParticipantClass.Test))
            && (!ambiente.EsProduccion || p.Adopciones.Any(a => a.Contrato == ContratosDeIntegracion.ProveedorDeObligaciones)));
        requisitos.Add(recaudador
            ? Cumple(RecaudadorRegistrado, "Hay un recaudador de Recaudo en el registro.")
            : Falta(RecaudadorRegistrado, "registrar el recaudador de Recaudo en el registro de integración", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        var tesoreria = await registro.DestinoAsync(IntegrationDestinations.Treasury, fechaDeActivacion, ct);
        requisitos.Add(tesoreria is not null
                       && (!ambiente.EsProduccion || (!tesoreria.IsTestDouble && tesoreria.Adopciones.Any(a => a.Contrato == OrdenDeGiroEmitidaV1.Type)))
            ? Cumple(TesoreriaRegistrada, "Hay un destino de Tesorería en el registro.")
            : Falta(TesoreriaRegistrada, "registrar el destino de Tesorería en el registro de integración", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        var (perdida, sinModeloObligatorio) = await PerdidaEsperadaAsync(fechaDeActivacion, ct);
        requisitos.Add(perdida);
        var k6a = SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente);
        requisitos.Add(k6a
            ? Cumple(ParametrosDePerdidaEsperada, "La entrega desplegada calcula la pérdida esperada con los parámetros vigentes.")
            : NoAplica(ParametrosDePerdidaEsperada, "Sólo aplica donde está desplegada la entrega de pérdida esperada."));
        requisitos.Add(k6a
            ? Cumple(HistorialDeMoraCompleto, "El historial de mora de la carga se validó al aplicarla.")
            : NoAplica(HistorialDeMoraCompleto, "Sólo aplica donde está desplegada la entrega de pérdida esperada."));

        requisitos.Add(await Anexo1Async(fechaDeActivacion, ct));
        requisitos.Add(await UsuraAsync(fechaDeActivacion, ct));
        requisitos.Add(await RolesAsync(ct));
        requisitos.Add(await OficinasAsync(carga, ct));
        requisitos.Add(await PuenteDeNominaAsync(fechaDeActivacion, ct));

        return new EvaluacionDeActivacion(requisitos, await AvisosAsync(ct), sinModeloObligatorio);
    }

    // ------------------------------------------------------------------------------------------------- pérdida esperada --

    private async Task<(RequisitoDeActivacionDto Requisito, bool SinModeloObligatorio)> PerdidaEsperadaAsync(DateOnly fecha, CancellationToken ct)
    {
        if (normativa is null || perfiles is null)
            return (NoAplica(PerdidaEsperadaDisponible, "No se evaluó la obligación de pérdida esperada."), false);
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, ct: ct);
        if (regla.IsFailure) return (Cumple(PerdidaEsperadaDisponible, "No hay regla de pérdida esperada vigente: la cooperativa no está obligada."), false);

        var obligada = Obligada(regla.Value.Documento, await perfiles.VigenteAsync(fecha, null, ct));
        DateOnly? desde = null;
        if (!obligada && db is not null)
        {
            // Obligada desde una fecha posterior: una versión del perfil ya registrada que empieza después y cae bajo la regla.
            var posteriores = await db.RegulatoryProfileVersions.AsNoTracking().Where(v => !v.IsDeleted && v.ValidFrom > fecha)
                .OrderBy(v => v.ValidFrom).ToListAsync(ct);
            desde = posteriores.FirstOrDefault(v => regla.Value.Documento.Obliga(v.OrganizationTypeCode, v.EmployeeFundCategory))?.ValidFrom;
        }
        if (!obligada && desde is null)
            return (Cumple(PerdidaEsperadaDisponible, "La cooperativa no está obligada a la pérdida esperada."), false);
        if (SelectorDeModeloDeDeterioro.PerdidaEsperadaDisponible(CatalogoDeCartera.EntregaVigente))
            return (Cumple(PerdidaEsperadaDisponible, "La entrega desplegada trae la pérdida esperada."), false);

        var cuando = desde is { } d ? string.Create(CultureInfo.InvariantCulture, $" desde el {d:yyyy-MM-dd}") : string.Empty;
        return ambiente.EsProduccion
            ? (Falta(PerdidaEsperadaDisponible, $"desplegar la entrega de pérdida esperada: la cooperativa está obligada{cuando}", "Plataforma", null, null), false)
            : (Dispensa(PerdidaEsperadaDisponible, $"La cooperativa está obligada a la pérdida esperada{cuando} y esta entrega no la trae: fuera de producción se activa con la marca «modelo obligatorio no disponible».",
                "Plataforma", null, null), true);
    }

    private static bool Obligada(ReglaDePerdidaEsperada regla, PerfilRegulatorioDto? perfil) =>
        perfil is not null && regla.Obliga(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory);

    // ------------------------------------------------------------------------------------------------------- producción --

    private async Task<RequisitoDeActivacionDto> Anexo1Async(DateOnly fecha, CancellationToken ct)
    {
        if (normativa is null) return NoAplica(Anexo1Vigente, "No se evaluó el Anexo 1.");
        var anexo = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, ct: ct);
        return anexo.IsSuccess
            ? Cumple(Anexo1Vigente, string.Create(CultureInfo.InvariantCulture, $"El Anexo 1 vigente rige desde el {anexo.Value.ValidFrom:yyyy-MM-dd}."))
            : DeProduccion(Anexo1Vigente, "el Anexo 1 vigente a la fecha de activación en los documentos normativos", "Plataforma", null, null);
    }

    private async Task<RequisitoDeActivacionDto> UsuraAsync(DateOnly fecha, CancellationToken ct)
    {
        if (db is null || normativa is null || valores is null) return NoAplica(UsuraDeLineasActivas, "No se evaluó la usura de las líneas.");
        var lineas = await db.CreditLines.AsNoTracking().Where(l => l.Status == CreditLineStatus.Active && !l.IsDeleted).Select(l => new { l.Id, l.Code })
            .ToListAsync(ct);
        if (lineas.Count == 0) return Cumple(UsuraDeLineasActivas, "No hay líneas activas.");
        var versiones = await new Configuration.VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), fecha, ct: ct);
        var catalogo = await normativa.CatalogoDeModalidadesAsync(fecha, null, ct);
        var faltan = new List<string>();
        foreach (var linea in lineas)
        {
            if (!versiones.TryGetValue(linea.Id, out var v)) continue;
            var combinacion = catalogo.IsSuccess ? catalogo.Value.Documento.Buscar(v.Version.ModalityCode, v.Version.SubsegmentCode) : null;
            if (catalogo.IsSuccess && combinacion is { UsuryKey: null }) continue;
            if (combinacion?.UsuryKey is not { } clave)
            {
                faltan.Add(linea.Code);
                continue;
            }
            var serie = await valores.LeerSerieAsync(CodigosDeValorNacional.Usura, clave, fecha, fecha, ct: ct);
            if (serie.En(fecha)?.Valor is null) faltan.Add(linea.Code);
        }
        return faltan.Count == 0
            ? Cumple(UsuraDeLineasActivas, "Cada línea activa tiene la usura certificada de su modalidad a la fecha de activación.")
            : DeProduccion(UsuraDeLineasActivas, $"la usura certificada de la modalidad de las líneas {string.Join(", ", faltan)} a la fecha de activación",
                "Plataforma", "/cartera/lineas", "Lending.CreditLines.View");
    }

    private async Task<RequisitoDeActivacionDto> RolesAsync(CancellationToken ct)
    {
        if (db is null) return NoAplica(RolesDeCarteraCreados, "No se evaluaron los roles.");
        var esperados = PerfilesSugeridos.DelModulo(PerfilesSugeridos.ModuloCartera)
            .Select(p => CreateSuggestedLendingRolesCommand.CodigoDeRol(p.Key)).ToList();
        var existentes = await db.Roles.AsNoTracking().Where(r => esperados.Contains(r.Code) && !r.IsDeleted).Select(r => r.Code).ToListAsync(ct);
        var faltan = esperados.Except(existentes, StringComparer.OrdinalIgnoreCase).ToList();
        return faltan.Count == 0
            ? Cumple(RolesDeCarteraCreados, "Están creados los roles de Cartera de las plantillas.")
            : DeProduccion(RolesDeCarteraCreados, $"crear los roles de Cartera desde sus plantillas (faltan {faltan.Count})", "Seguridad",
                "/cartera/activacion", "Security.Roles.Create");
    }

    private async Task<RequisitoDeActivacionDto> OficinasAsync(MigrationLoad? carga, CancellationToken ct)
    {
        if (db is null || carga is null) return NoAplica(OficinasVinculadas, "No se evaluaron las oficinas de la carga.");
        var partes = db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);
        var oficinas = (await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.OfficePublicId).Distinct().ToListAsync(ct))
            .Concat(await db.MigrationChargedOffRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.OfficePublicId).Distinct().ToListAsync(ct))
            .Distinct().ToList();
        if (oficinas.Count == 0) return Cumple(OficinasVinculadas, "La carga no trae créditos.");
        var vinculadas = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.PublicId) && b.TenantBranchPublicId != null && !b.IsDeleted)
            .Select(b => b.PublicId).ToListAsync(ct);
        var faltan = oficinas.Except(vinculadas).Count();
        return faltan == 0
            ? Cumple(OficinasVinculadas, "Toda oficina con créditos en la carga está vinculada a una sucursal contable.")
            : DeProduccion(OficinasVinculadas, $"vincular a una sucursal contable las {faltan} oficinas con créditos en la carga", "Contabilidad",
                "/maestros/agencias", "Core.Branches.Manage");
    }

    private async Task<RequisitoDeActivacionDto> PuenteDeNominaAsync(DateOnly fecha, CancellationToken ct)
    {
        if (db is null) return NoAplica(PuenteDeNominaResuelto, "No se evaluó la regla de recaudo por nómina.");
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.Status == CreditLineStatus.Active && !l.IsDeleted).OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.Code }).FirstOrDefaultAsync(ct);
        var sucursal = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        if (linea is null || sucursal is null) return Cumple(PuenteDeNominaResuelto, "No hay líneas activas que recauden por nómina.");
        var version = await new Configuration.VersionVigenteDeLinea(db).LeerVigenteOProximaAsync(linea.Id, fecha, ct);
        var hecho = new MensajeContableDto(
            new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = PagoAplicadoV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = sucursal.Value,
            },
            new PagoAplicadoV1
            {
                Dimensions = new LoanFactDimensionsV1
                {
                    CreditLineCode = linea.Code, ModalityCode = version?.Version.ModalityCode ?? string.Empty, SubsegmentCode = version?.Version.SubsegmentCode,
                    RiskCategory = CategoriaContableDeCartera.CategoriaDeCreditoNuevo, RiskSubcategory = "None", CollectorClass = nameof(ClaseDeRecaudador.Payroll),
                    BranchPublicId = sucursal.Value, ImpairmentModel = "ArrearsAnnex1", RiskSegment = version?.Version.ModalityCode ?? string.Empty, ValueUnit = "Pesos",
                },
                Amounts = [new ConceptAmountV1 { Concept = nameof(LoanConcept.Principal), Amount = 1m }],
            });
        var evaluado = await contabilidad.EvaluarAsync([hecho], ct);
        var resuelta = evaluado.IsSuccess && !evaluado.Value.Errors.Any(e => string.Equals(e.Role, ReglaDelPuente, StringComparison.Ordinal));
        return resuelta
            ? Cumple(PuenteDeNominaResuelto, "La regla del recaudo por nómina para Cartera está resuelta en Contabilidad.")
            : DeProduccion(PuenteDeNominaResuelto, "la regla «recaudo por aplicar» de la clase nómina en la matriz contable de Cartera", "Contabilidad",
                "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage");
    }

    /// <summary>El rol de la matriz que resuelve el recaudo de un pago aplicado (contabilidad.md §3.7).</summary>
    public const string ReglaDelPuente = "RecaudoPorAplicar";

    // ----------------------------------------------------------------------------------------------------------- avisos --

    private async Task<IReadOnlyList<AvisoDeActivacionDto>> AvisosAsync(CancellationToken ct)
    {
        var avisos = new List<AvisoDeActivacionDto>
        {
            new(AvisoParametrosComerciales,
                "Ajuste en Comercial la fecha de habilitación de Cartera (igual a la de activación), la política «sin respuesta» y quién registra la cuenta por cobrar.",
                new QuienCorrigeDto("Comercial", "/ventas/parametros", "Inventory.Parameters.Manage")),
            new(AvisoCuentasDeNomina,
                "Revise que el concepto de nómina del descuento de Cartera tenga sus cuentas contables antes de la primera nómina.",
                new QuienCorrigeDto("Nómina", "/nomina/conceptos", "Payroll.Concepts.Manage")),
        };
        if (db is not null)
        {
            var roles = await db.Roles.AsNoTracking().Where(r => r.Code.StartsWith("CARTERA-") && !r.IsDeleted).Select(r => r.Id).ToListAsync(ct);
            var usuarios = await db.UserRoles.AsNoTracking().Where(u => roles.Contains(u.RoleId)).Select(u => u.UserId).Distinct().ToListAsync(ct);
            var conAlcance = await db.UserCreditLineScopes.AsNoTracking().Where(s => usuarios.Contains(s.UserId) && !s.IsDeleted).Select(s => s.UserId).Distinct()
                .ToListAsync(ct);
            var sinAlcance = usuarios.Except(conAlcance).Count();
            if (sinAlcance > 0)
                avisos.Add(new AvisoDeActivacionDto(AvisoUsuariosSinAlcance,
                    string.Create(CultureInfo.InvariantCulture, $"{sinAlcance} usuarios con roles de Cartera no tienen alcance de líneas asignado."),
                    new QuienCorrigeDto("Cartera", "/cartera/alcances", "Lending.Scopes.Manage")));
        }
        return avisos;
    }

    // ------------------------------------------------------------------------------------------------------------ armado --

    private static RequisitoDeActivacionDto Cumple(string codigo, string detalle) => new(codigo, true, detalle, null, EstadosDeRequisito.Met);

    private static RequisitoDeActivacionDto NoAplica(string codigo, string detalle) => new(codigo, true, detalle, null, EstadosDeRequisito.NotApplicable);

    private static RequisitoDeActivacionDto Falta(string codigo, string detalle, string modulo, string? pagina, string? permiso) =>
        new(codigo, false, detalle, new QuienCorrigeDto(modulo, pagina, permiso), EstadosDeRequisito.Missing);

    private static RequisitoDeActivacionDto Dispensa(string codigo, string detalle, string modulo, string? pagina, string? permiso) =>
        new(codigo, true, detalle, new QuienCorrigeDto(modulo, pagina, permiso), EstadosDeRequisito.Waived);

    /// <summary>Un requisito de producción que falta: en producción bloquea; fuera de ella queda dispensado con lo que falta.</summary>
    private RequisitoDeActivacionDto DeProduccion(string codigo, string detalle, string modulo, string? pagina, string? permiso) =>
        ambiente.EsProduccion ? Falta(codigo, detalle, modulo, pagina, permiso) : Dispensa(codigo, $"Falta {detalle} (fuera de producción no bloquea).", modulo, pagina, permiso);
}

/// <summary>Los estados de un requisito de la activación (api.md §19.3). (nuevo)</summary>
public static class EstadosDeRequisito
{
    public const string Met = "Met";
    public const string Missing = "Missing";
    public const string NotApplicable = "NotApplicable";

    /// <summary>Falta, pero fuera de producción no bloquea.</summary>
    public const string Waived = "Waived";
}

/// <summary>Un requisito de la activación con su estado y, si falta, quién lo corrige (api.md §19.3). (nuevo)</summary>
public sealed record RequisitoDeActivacionDto(string Code, bool Met, string Detail, QuienCorrigeDto? WhoFixes, string Status = EstadosDeRequisito.Met);

/// <summary>Un aviso de la activación que no bloquea (api.md §19.3). (nuevo)</summary>
public sealed record AvisoDeActivacionDto(string Code, string Detail, QuienCorrigeDto? WhoFixes);

/// <summary>Los requisitos, los avisos y la marca «modelo obligatorio no disponible» (FR-105). (nuevo)</summary>
public sealed record EvaluacionDeActivacion(
    IReadOnlyList<RequisitoDeActivacionDto> Requisitos,
    IReadOnlyList<AvisoDeActivacionDto> Avisos,
    bool ObligatoryModelUnavailable)
{
    /// <summary>Lo que bloquea: los requisitos que faltan.</summary>
    public IReadOnlyList<RequisitoDeActivacionDto> Faltantes => Requisitos.Where(r => !r.Met).ToList();
}
