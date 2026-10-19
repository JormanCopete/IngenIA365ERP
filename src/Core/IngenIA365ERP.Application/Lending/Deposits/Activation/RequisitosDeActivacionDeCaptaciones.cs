using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>
/// Los requisitos de la activación de aportes y de captaciones (feature 014, T054; FR-093, FR-116, FR-117, R21, Q1; api.md §16 de la 014). Cada uno
/// sale con su estado y quién lo corrige, con los mismos tipos que la activación de créditos (<see cref="RequisitoDeActivacionDto"/>,
/// <see cref="EstadosDeRequisito"/>):
/// <list type="bullet">
/// <item><c>LoansActivated</c>: los créditos se activan primero (los cruces al retiro y la consulta de aportes los necesitan), con fecha ≤ la de esta
/// naturaleza;</item>
/// <item><c>LoadApplied</c> y <c>DifferencesAccepted</c>: en la activación <b>sin carga</b> (cooperativa sin historia, FR-117) no aplican; la activación
/// con carga llega con la migración (US9);</item>
/// <item><c>DateInTerm</c>: la fecha no es futura;</item>
/// <item><c>AccountingMonthOpen</c>: como en la activación de créditos (decisión del dueño del 2026-10-14) un mes cerrado no limita y sale como aviso; sin
/// contabilidad iniciada falta;</item>
/// <item><c>AccountsCollectorRegistered</c>: un recaudador con <c>ProveedorDeCuentas</c> adoptado; en producción, que no sea un doble de prueba;</item>
/// <item><c>TreasuryDestinationRegistered</c>: un destino <c>Treasury</c> con <c>OrdenDeGiroEmitida</c> v2 adoptada en todos los ambientes (las órdenes
/// de captaciones salen sólo en v2, mensajes.md §5.3; D14); en producción, real;</item>
/// <item><c>PayrollConceptsReady</c>: no se puede saber desde aquí (la Cartera no lee <c>PAY_</c>): no aplica y sale el aviso de revisar
/// <c>DESC_APORTES</c>/<c>DESC_AHORROS</c>;</item>
/// <item><b>en todos los ambientes</b> (decisión del dueño del 2026-10-18, research §13 D14: hasta esa fecha sólo bloqueaban en producción y fuera de
/// ella quedaban <c>Waived</c>, research E32): <c>DeliveriesComplete</c> (K7 a K11 desplegadas, Q1), <c>ProfileAllowsClasses</c> (captaciones: el
/// perfil regulatorio vigente autoriza captar, FR-018) y <c>LinesWithMatrix</c> (cada línea activa de la naturaleza contabiliza su hecho principal,
/// preguntado a <see cref="IContabilidadParaCartera.EvaluarAsync"/>). Fuera de producción sólo cambia que el recaudador y la tesorería pueden ser los
/// dobles de prueba;</item>
/// <item><c>DepositRolesCreated</c> (decisión del dueño del 2026-10-18, D18): el rol de cada una de las cuatro plantillas de captaciones y aportes de
/// <see cref="PerfilesSugeridos.DeCaptaciones"/>; los trece del crédito los exige sólo la activación de créditos.</item>
/// </list>
/// Aparte, <see cref="EntregaNoDisponible"/> es el bloqueo por entrega (<c>Lending.Savings.DeliveryNotAvailable</c>, api.md §1.3): sin K7 no hay aportes
/// y sin K8 no hay captaciones, en cualquier ambiente. Sólo lee. (nuevo)
/// </summary>
public sealed class RequisitosDeActivacionDeCaptaciones(
    IApplicationDbContext db,
    IRegistroDeIntegracion registro,
    IContabilidadParaCartera contabilidad,
    IAmbienteDeDespliegue ambiente,
    IPerfilRegulatorio? perfiles = null,
    EntregaDeCartera? entregaDesplegada = null)
{
    public const string CreditosActivados = "LoansActivated";
    public const string CargaAplicada = RequisitosDeActivacion.CargaAplicada;
    public const string DiferenciasAceptadas = RequisitosDeActivacion.DiferenciasAceptadas;
    public const string FechaEnPlazo = RequisitosDeActivacion.FechaEnPlazo;
    public const string MesContableAbierto = RequisitosDeActivacion.MesContableAbierto;
    public const string RecaudadorDeCuentasRegistrado = "AccountsCollectorRegistered";
    public const string TesoreriaRegistrada = RequisitosDeActivacion.TesoreriaRegistrada;
    public const string ConceptosDeNominaListos = "PayrollConceptsReady";
    public const string EntregasCompletas = "DeliveriesComplete";
    public const string PerfilAutorizaClases = "ProfileAllowsClasses";
    public const string LineasConMatriz = "LinesWithMatrix";
    public const string RolesDeCaptacionesCreados = "DepositRolesCreated";

    /// <summary>El bloqueo por entrega (api.md §1.3).</summary>
    public const string CodigoEntregaNoDisponible = "Lending.Savings.DeliveryNotAvailable";

    /// <summary>La versión de <c>OrdenDeGiroEmitida</c> que necesitan las órdenes de captaciones.</summary>
    public const int VersionDeLaOrdenDeCaptaciones = 2;

    /// <summary>La entrega desplegada: la del catálogo, salvo que una prueba la fije.</summary>
    public EntregaDeCartera Entrega => entregaDesplegada ?? CatalogoDeCartera.EntregaVigente;

    public bool EsProduccion => ambiente.EsProduccion;

    /// <summary>La entrega desde la que una naturaleza se puede activar: aportes con K7, captaciones con K8.</summary>
    public static EntregaDeCartera EntregaQueLaTrae(LineNature naturaleza) =>
        naturaleza == LineNature.Contribution ? EntregaDeCartera.K7 : EntregaDeCartera.K8;

    /// <summary><c>Lending.Savings.DeliveryNotAvailable</c> si el despliegue no trae la entrega de la naturaleza; nulo si la trae.</summary>
    public Error? EntregaNoDisponible(LineNature naturaleza)
    {
        var necesaria = EntregaQueLaTrae(naturaleza);
        if (Entrega >= necesaria) return null;
        var que = naturaleza == LineNature.Contribution ? "Los aportes" : "Las captaciones";
        return new ErrorConDatos(CodigoEntregaNoDisponible,
            $"{que} llegan con la entrega {necesaria} de la Cartera, que todavía no está desplegada.",
            new { nature = naturaleza.ToString(), availableFrom = necesaria.ToString() });
    }

    /// <summary>Los requisitos para activar sin carga <paramref name="naturaleza"/> el <paramref name="fecha"/>, mirado <paramref name="hoy"/>.</summary>
    public async Task<EvaluacionDeActivacion> EvaluarAsync(LineNature naturaleza, DateOnly fecha, DateOnly hoy, CancellationToken ct)
    {
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var requisitos = new List<RequisitoDeActivacionDto>(12)
        {
            fechas.Creditos is { } creditos && creditos <= fecha
                ? Cumple(CreditosActivados, string.Create(CultureInfo.InvariantCulture, $"Los créditos están activados desde el {creditos:yyyy-MM-dd}."))
                : Falta(CreditosActivados, fechas.Creditos is { } despues
                    ? string.Create(CultureInfo.InvariantCulture, $"una fecha igual o posterior a la activación de los créditos ({despues:yyyy-MM-dd})")
                    : "activar primero los créditos de la Cartera", "Cartera", "/cartera/activacion", "Lending.Activation.Activate"),
            NoAplica(CargaAplicada, "Cooperativa sin historia: se activa sin carga (FR-117)."),
            NoAplica(DiferenciasAceptadas, "Sin carga no hay cuadre que aceptar."),
            fecha <= hoy
                ? Cumple(FechaEnPlazo, string.Create(CultureInfo.InvariantCulture, $"La activación rige desde el {fecha:yyyy-MM-dd}."))
                : Falta(FechaEnPlazo, "una fecha de activación que no sea futura", "Cartera", "/cartera/captaciones/activacion", "Lending.Deposits.Activate"),
        };

        var avisos = new List<AvisoDeActivacionDto>();
        var periodo = await contabilidad.EstadoDelPeriodoAsync(fecha, ct);
        if (periodo.IsSuccess && periodo.Value is { AccountingInitialized: true, State: EstadoDePeriodoContable.Open })
            requisitos.Add(Cumple(MesContableAbierto, string.Create(CultureInfo.InvariantCulture, $"El mes de {fecha:yyyy-MM} está abierto en Contabilidad.")));
        else if (periodo.IsSuccess && periodo.Value.AccountingInitialized)
        {
            var estado = periodo.Value.State == EstadoDePeriodoContable.NotFound ? "no existe" : "está cerrado";
            requisitos.Add(NoAplica(MesContableAbierto, string.Create(CultureInfo.InvariantCulture,
                $"El mes de {fecha:yyyy-MM} {estado} en Contabilidad: no impide activar.")));
            avisos.Add(new AvisoDeActivacionDto(RequisitosDeActivacion.AvisoMesContableCerrado, string.Create(CultureInfo.InvariantCulture,
                    $"El mes de {fecha:yyyy-MM} {estado} en Contabilidad. La activación procede y su informativo se registra en el primer mes abierto."),
                new QuienCorrigeDto("Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen")));
        }
        else
            requisitos.Add(Falta(MesContableAbierto, periodo.IsSuccess
                    ? "iniciar la contabilidad"
                    : string.Create(CultureInfo.InvariantCulture, $"saber el estado del mes de {fecha:yyyy-MM} en Contabilidad ({periodo.Error.Message})"),
                "Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen"));

        var participantes = await registro.QueCuentanAsync(fecha, ct);
        var recaudador = participantes.Any(p => p.Role == IntegrationParticipantRole.ObligationsCollector
            && p.Adopciones.Any(a => a.Contrato == ContratosDeIntegracion.ProveedorDeCuentas)
            && (!ambiente.EsProduccion || (!p.IsTestDouble && p.Class != IntegrationParticipantClass.Test)));
        requisitos.Add(recaudador
            ? Cumple(RecaudadorDeCuentasRegistrado, "Hay un recaudador de cuentas en el registro.")
            : Falta(RecaudadorDeCuentasRegistrado, "registrar un recaudador (Recaudo, Tesorería o Nómina) con el contrato de cuentas adoptado", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        var tesoreria = await registro.DestinoAsync(IntegrationDestinations.Treasury, fecha, ct);
        requisitos.Add(tesoreria is not null
                       && (!ambiente.EsProduccion || !tesoreria.IsTestDouble)
                       && tesoreria.Adopciones.Any(a => a.Contrato == OrdenDeGiroEmitidaV1.Type && a.Version >= VersionDeLaOrdenDeCaptaciones)
            ? Cumple(TesoreriaRegistrada, "Hay un destino de Tesorería en el registro.")
            : Falta(TesoreriaRegistrada, "registrar el destino de Tesorería con la orden de giro v2 adoptada", "Core",
                "/admin/participantes-de-integracion", "Core.IntegrationParticipants.Manage"));

        requisitos.Add(NoAplica(ConceptosDeNominaListos, "Lo revisa la pantalla con Nómina: la Cartera no lee los conceptos de nómina."));
        avisos.Add(new AvisoDeActivacionDto(ConceptosDeNominaListos,
            naturaleza == LineNature.Contribution
                ? "Revise que el concepto de nómina del descuento de aportes tenga sus cuentas contables antes de la primera nómina."
                : "Revise que el concepto de nómina del descuento de ahorros tenga sus cuentas contables antes de la primera nómina.",
            new QuienCorrigeDto("Nómina", "/nomina/conceptos", "Payroll.Concepts.Manage")));

        requisitos.Add(Entrega >= EntregaDeCartera.K11
            ? Cumple(EntregasCompletas, "El despliegue trae las entregas K7 a K11 de captaciones.")
            : Falta(EntregasCompletas, $"desplegar las entregas K7 a K11 de captaciones (la desplegada es {Entrega})", "Plataforma", null, null));
        requisitos.Add(await PerfilAsync(naturaleza, fecha, ct));
        requisitos.Add(await LineasConMatrizAsync(naturaleza, fecha, ct));
        requisitos.Add(await RolesAsync(ct));

        return new EvaluacionDeActivacion(requisitos, avisos, false);
    }

    // ------------------------------------------------------------------------------------------------------- perfil --

    private async Task<RequisitoDeActivacionDto> PerfilAsync(LineNature naturaleza, DateOnly fecha, CancellationToken ct)
    {
        if (naturaleza != LineNature.Deposit) return NoAplica(PerfilAutorizaClases, "Los aportes no requieren autorización para captar.");
        if (perfiles is null) return NoAplica(PerfilAutorizaClases, "No se evaluó el perfil regulatorio.");
        var perfil = await perfiles.VigenteAsync(fecha, null, ct);
        return perfil is { MayTakeDeposits: true }
            ? Cumple(PerfilAutorizaClases, "El perfil regulatorio vigente autoriza captar ahorro.")
            : Falta(PerfilAutorizaClases, perfil is null
                    ? "un perfil regulatorio vigente que autorice captar ahorro"
                    : "que el perfil regulatorio vigente autorice captar ahorro", "Core", "/maestros/perfil-regulatorio", "Core.RegulatoryProfile.Manage");
    }

    // ------------------------------------------------------------------------------------------------------ matriz --

    /// <summary>
    /// Cada línea activa de la naturaleza contabiliza su hecho principal —<c>AporteRecibido</c> en aportes, <c>ConsignacionEnCuentaAplicada</c> en
    /// captaciones—, preguntado a Contabilidad por el puerto (la Cartera no lee la matriz). Sin líneas activas, no hay nada que contabilizar.
    /// </summary>
    private async Task<RequisitoDeActivacionDto> LineasConMatrizAsync(LineNature naturaleza, DateOnly fecha, CancellationToken ct)
    {
        var lineas = await db.CreditLines.AsNoTracking()
            .Where(l => l.Nature == naturaleza && l.Status == CreditLineStatus.Active && !l.IsDeleted).OrderBy(l => l.Code)
            .Select(l => l.Code).ToListAsync(ct);
        var sucursal = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        if (lineas.Count == 0 || sucursal is null) return Cumple(LineasConMatriz, "No hay líneas activas de la naturaleza que contabilizar.");

        var hechos = lineas.Select(codigo => HechoRepresentativo(naturaleza, codigo, sucursal.Value)).ToList();
        var evaluado = await contabilidad.EvaluarAsync(hechos, ct);
        return evaluado.IsSuccess && evaluado.Value.Errors.Count == 0
            ? Cumple(LineasConMatriz, "La matriz contable resuelve el hecho principal de cada línea activa.")
            : Falta(LineasConMatriz, "las reglas de la matriz contable de Cartera para los hechos de la naturaleza", "Contabilidad",
                "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage");
    }

    private static MensajeContableDto HechoRepresentativo(LineNature naturaleza, string linea, Guid sucursal)
    {
        var dimensiones = new DepositFactDimensionsV1
        {
            CreditLineCode = linea, LineNature = naturaleza.ToString(), AccountFlag = nameof(AccountFlag.Standard), BranchPublicId = sucursal,
            DepositClass = naturaleza == LineNature.Deposit ? nameof(DepositClass.Demand) : null,
        };
        var recaudador = new CollectorRefV1 { CollectorClass = nameof(ClaseDeRecaudador.Receipts), MeansClass = "Cash" };
        object contenido = naturaleza == LineNature.Contribution
            ? new AporteRecibidoV1
            {
                Dimensions = dimensiones, Collector = recaudador,
                Amounts = [new DepositConceptAmountV1
                {
                    Concept = nameof(MemberAccountConcept.ContributionOrdinary), ContributionClass = nameof(ContributionClass.Ordinary), Amount = 1m,
                }],
            }
            : new ConsignacionEnCuentaAplicadaV1
            {
                Dimensions = dimensiones, Collector = recaudador,
                Amounts = [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = 1m }],
            };
        var tipo = naturaleza == LineNature.Contribution ? AporteRecibidoV1.Type : ConsignacionEnCuentaAplicadaV1.Type;
        return new MensajeContableDto(
            new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = tipo, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = sucursal,
            },
            contenido);
    }

    // -------------------------------------------------------------------------------------------------------- roles --

    /// <summary>Los cuatro roles de captaciones y aportes, por su código (D18): sin ellos la operación de la naturaleza no tiene a quién repartir.</summary>
    private async Task<RequisitoDeActivacionDto> RolesAsync(CancellationToken ct)
    {
        var esperados = PerfilesSugeridos.DeCaptaciones().Select(p => CreateSuggestedLendingRolesCommand.CodigoDeRol(p.Key)).ToList();
        var existentes = await db.Roles.AsNoTracking().Where(r => esperados.Contains(r.Code) && !r.IsDeleted).Select(r => r.Code).ToListAsync(ct);
        var faltan = esperados.Except(existentes, StringComparer.OrdinalIgnoreCase).ToList();
        return faltan.Count == 0
            ? Cumple(RolesDeCaptacionesCreados, "Están creados los roles de captaciones y aportes de las plantillas.")
            : Falta(RolesDeCaptacionesCreados, $"crear los roles de captaciones y aportes desde sus plantillas (faltan {faltan.Count})", "Seguridad",
                "/cartera/captaciones/activacion", "Security.Roles.Create");
    }

    // ------------------------------------------------------------------------------------------------------- armado --

    private static RequisitoDeActivacionDto Cumple(string codigo, string detalle) => new(codigo, true, detalle, null, EstadosDeRequisito.Met);

    private static RequisitoDeActivacionDto NoAplica(string codigo, string detalle) => new(codigo, true, detalle, null, EstadosDeRequisito.NotApplicable);

    private static RequisitoDeActivacionDto Falta(string codigo, string detalle, string modulo, string? pagina, string? permiso) =>
        new(codigo, false, detalle, new QuienCorrigeDto(modulo, pagina, permiso), EstadosDeRequisito.Missing);
}
