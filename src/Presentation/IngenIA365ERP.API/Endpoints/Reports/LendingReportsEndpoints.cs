using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.AspNetCore.Routing;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// El centro de informes de Cartera (feature 013, US15, T0796, T0798, T99; contracts/api.md §17, decisiones-transversales §2.15): una ruta por
/// vista bajo <c>/api/reports/lending/{vista}</c>, todas con los filtros comunes de <see cref="FiltrosDeInformeDeCartera"/> y
/// <c>format=json|xlsx|pdf|docx</c>, entregadas por <see cref="EntregaDeInformes.EntregarAsync"/>. Reescrito como el de Inventario: las rutas
/// heredadas <c>aging</c>, <c>aging/pdf</c> y <c>loan-statement/{id}/pdf</c> las retiró la fase 3 (R-08) y no vuelven, ni con alias; cada entrega
/// registra sus vistas en <see cref="MapVistas"/> con <see cref="LendingReportsRoutes.MapVistaDeCartera"/>. (reescrito)
///
/// <para>
/// <c>GET /api/reports/lending</c> es el <b>registro de vistas</b>: las publicadas, leídas de la metadata de cada ruta, sin las de una entrega
/// posterior a la vigente ni las que exigen un permiso que quien pregunta no tiene (las de datos del deudor, sin <c>Lending.Loans.Read</c>). De
/// ahí arma su selector la pantalla <c>/cartera/informes</c>.
/// </para>
/// </summary>
public class LendingReportsEndpoints : ICarterModule
{
    public const string Ruta = "/api/reports/lending";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Ruta)
            .WithTags("Lending Reports")
            .RequireAuthorization();

        group.MapGet("/", async (HttpContext http, CancellationToken ct) =>
            {
                var publicadas = http.RequestServices.GetRequiredService<EndpointDataSource>().Endpoints
                    .Select(e => e.Metadata.GetMetadata<VistaDeInformeDeCartera>())
                    .OfType<VistaDeInformeDeCartera>()
                    .Where(v => v.AvailableFrom <= CatalogoDeCartera.EntregaVigente)
                    .DistinctBy(v => v.Key)
                    .OrderBy(v => v.Name, StringComparer.CurrentCulture)
                    .ToList();
                var visibles = new List<VistaDeInformeDeCartera>(publicadas.Count);
                foreach (var vista in publicadas)
                {
                    ct.ThrowIfCancellationRequested();
                    // K5 (T0982): una vista con datos del deudor sólo en algunos valores de un filtro (concentration) se ofrece a todos; la ruta
                    // pide el permiso adicional sólo en esos valores.
                    if (vista.RequiredPermission is null || vista.PersonalDataExceptWhen is not null
                        || await PermissionAuthorizationFilter.TieneAsync(http, vista.RequiredPermission))
                        visibles.Add(vista);
                }
                return Results.Ok(visibles);
            })
            .WithName("Reportes_Cartera_Vistas")
            .RequirePermission(LendingReportsRoutes.PermisoDeVer);

        MapVistas(group);
    }

    /// <summary>
    /// Aquí registra cada entrega sus vistas, una línea por vista: <c>group.MapVistaDeCartera(…Handler.Vista, (f, q) =&gt; new …Query(f, …));</c>.
    /// K1 (C109, T0798): las siete de la base; K2–K4 sumaron las suyas en sus fases y K5 (T0982) las cuatro de US15.
    /// </summary>
    private static void MapVistas(RouteGroupBuilder group)
    {
        // K1 (T0798): edades, colocación, recaudo, movimientos, usura, mensajes al deudor y conciliación de la migración. Los filtros propios
        // (groupBy, dateKind, kind, ranges, points, purpose, channel, status, load) los valida cada consulta: un valor no admitido es 422
        // Lending.Report.FilterInvalid con los admitidos.
        group.MapVistaDeCartera(AgingReportQueryHandler.Vista, (f, q) => new AgingReportQuery(f, Texto(q, "ranges")));
        group.MapVistaDeCartera(PlacementReportQueryHandler.Vista, (f, q) => new PlacementReportQuery(f, Texto(q, "groupBy")));
        group.MapVistaDeCartera(ReceiptsReportQueryHandler.Vista, (f, q) => new ReceiptsReportQuery(f, Texto(q, "groupBy")));
        group.MapVistaDeCartera(TransactionsReportQueryHandler.Vista,
            (f, q) => new TransactionsReportQuery(f, Id(q, "loan"), Texto(q, "kind"), Texto(q, "dateKind")));
        group.MapVistaDeCartera(UsuryReportQueryHandler.Vista, (f, q) => new UsuryReportQuery(f, Texto(q, "points")));
        group.MapVistaDeCartera(DebtorMessagesReportQueryHandler.Vista,
            (f, q) => new DebtorMessagesReportQuery(f, Texto(q, "purpose"), Texto(q, "channel"), Texto(q, "status")));
        group.MapVistaDeCartera(MigrationReconciliationReportQueryHandler.Vista, (f, q) => new MigrationReconciliationReportQuery(f, Id(q, "load")));
        // K2 (US10, T0846, T0850): las pagadurías al corte y el cuadre de un convenio y período (agreement = su PublicId, year, month).
        group.MapVistaDeCartera(PayrollAgreementsReportQueryHandler.Vista, (f, _) => new PayrollAgreementsReportQuery(f));
        group.MapVistaDeCartera(PayrollDeductionReconciliationReportQueryHandler.Vista,
            (f, q) => new PayrollDeductionReconciliationReportQuery(f, Id(q, "agreement"), Entero(q, "year"), Entero(q, "month")));
        // K3 (US11, T0887, T0890): los reestructurados al corte, con el filtro propio mechanism.
        group.MapVistaDeCartera(RestructuredReportQueryHandler.Vista, (f, q) => new RestructuredReportQuery(f, Texto(q, "mechanism")));
        // K3 (US14, T0911, T0915): la cartera castigada hasta `to`, con lo recuperado en el rango.
        group.MapVistaDeCartera(ChargeOffsReportQueryHandler.Vista, (f, _) => new ChargeOffsReportQuery(f));
        // K4 (US12, T0953): la cartera por etapa al corte (stage, collector), las promesas, los acuerdos de pago y los avisos previos (status).
        group.MapVistaDeCartera(CollectionStagesReportQueryHandler.Vista, (f, _) => new CollectionStagesReportQuery(f));
        group.MapVistaDeCartera(PromisesReportQueryHandler.Vista, (f, q) => new PromisesReportQuery(f, Texto(q, "status")));
        group.MapVistaDeCartera(PaymentAgreementsReportQueryHandler.Vista, (f, q) => new PaymentAgreementsReportQuery(f, Texto(q, "status")));
        group.MapVistaDeCartera(PriorNoticesReportQueryHandler.Vista, (f, q) => new PriorNoticesReportQuery(f, Texto(q, "status")));
        // K5 (US15, T0982): la calidad (groupBy), las cosechas, la concentración (by, top, warnAt; con datos del deudor sólo por deudor y por grupo)
        // y los documentos emitidos (kind, status).
        group.MapVistaDeCartera(QualityReportQueryHandler.Vista, (f, q) => new QualityReportQuery(f, Texto(q, "groupBy")));
        group.MapVistaDeCartera(VintagesReportQueryHandler.Vista, (f, _) => new VintagesReportQuery(f));
        group.MapVistaDeCartera(ConcentrationReportQueryHandler.Vista,
            (f, q) => new ConcentrationReportQuery(f, Texto(q, "by"), Texto(q, "top"), Texto(q, "warnAt")));
        group.MapVistaDeCartera(IssuedDocumentsReportQueryHandler.Vista, (f, q) => new IssuedDocumentsReportQuery(f, Texto(q, "kind"), Texto(q, "status")));
        // K5 (US16, T1006): los excluidos del archivo de una central (bureau obligatorio; year y month, o el último generado).
        group.MapVistaDeCartera(BureauExclusionsReportQueryHandler.Vista,
            (f, q) => new BureauExclusionsReportQuery(f, Texto(q, "bureau"), Entero(q, "year"), Entero(q, "month")));
        // Feature 014, US3 (T088; api.md §18.1): los aportes por asociado (class) y las cuotas de aportes en mora, con datos del titular.
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.ContributionsByMemberReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.ContributionsByMemberReportQuery(f, Texto(q, "class")));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.InstallmentsInArrearsReportQueryHandler.Vista,
            (f, _) => new Application.Lending.Deposits.Reports.InstallmentsInArrearsReportQuery(f));
        // Feature 014, US4 (T121; api.md §18.1): los saldos de captaciones (groupBy), los movimientos del libro (account, kind, dateKind), el GMF del mes
        // (year, month) y los cargos (status).
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.DepositBalancesReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.DepositBalancesReportQuery(f, Texto(q, "groupBy")));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.DepositTransactionsReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.DepositTransactionsReportQuery(f, Texto(q, "account"), Texto(q, "kind"), Texto(q, "dateKind")));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.GmfReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.GmfReportQuery(f, Entero(q, "year"), Entero(q, "month")));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.ChargesReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.ChargesReportQuery(f, Texto(q, "status")));
        // Feature 014, US5 (T137): las liquidaciones de intereses con su retención (from, to, class), con datos del titular.
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.InterestAndWithholdingReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.InterestAndWithholdingReportQuery(f, Texto(q, "class")));
        // Feature 014, US8 (T146): la conciliación contable de captaciones y aportes (asOf, branch), sin datos de titulares.
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.DepositsReconciliationReportQueryHandler.Vista,
            (f, _) => new Application.Lending.Deposits.Reports.DepositsReconciliationReportQuery(f));
        // Feature 014, US6 (T176): los vencimientos de CDAT (from, to, issueTermBand), las tasas negociadas (from, to) y la concentración de depositantes
        // (asOf, top), con datos del titular.
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.TermDepositMaturitiesReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.TermDepositMaturitiesReportQuery(f, Texto(q, "issueTermBand")));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.NegotiatedRatesReportQueryHandler.Vista,
            (f, _) => new Application.Lending.Deposits.Reports.NegotiatedRatesReportQuery(f));
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.DepositorConcentrationReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.DepositorConcentrationReportQuery(f, Entero(q, "top")));
        // Feature 014, US11 (T189): los embargos recibidos (from, to, status), con datos del titular.
        group.MapVistaDeCartera(Application.Lending.Deposits.Reports.GarnishmentsReportQueryHandler.Vista,
            (f, q) => new Application.Lending.Deposits.Reports.GarnishmentsReportQuery(f, Texto(q, "status")));
    }

    private static int? Entero(IQueryCollection q, string clave) =>
        int.TryParse(q[clave].ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;

    private static Guid? Id(IQueryCollection q, string clave) => Guid.TryParse(q[clave].ToString(), out var id) ? id : null;

    private static string? Texto(IQueryCollection q, string clave) => string.IsNullOrWhiteSpace(q[clave].ToString()) ? null : q[clave].ToString().Trim();
}

/// <summary>La extensión con la que se publica cada vista de Cartera (feature 013, US15, T0796). Molde: <c>InventoryReportsRoutes</c>. (nuevo)</summary>
public static class LendingReportsRoutes
{
    public const string PermisoDeVer = "Lending.Reports.View";
    public const string PermisoDeExportar = "Lending.Reports.Export";
    public const string PermisoDeDatosPersonales = "Lending.Reports.ExportPersonalData";

    /// <summary>
    /// Publica <c>GET {grupo}/{vista.Key}</c> con todo lo que §17 pide de cada vista, en un solo sitio para que ninguna entrega lo olvide:
    /// <list type="bullet">
    /// <item><c>.RequirePermission("Lending.Reports.View")</c> y, si la vista lo declara, su permiso adicional
    /// (<see cref="VistaDeInformeDeCartera.RequiredPermission"/>: <c>Lending.Loans.Read</c> en las de datos del deudor);</item>
    /// <item><c>.RequirePermissionWhenExporting("Lending.Reports.Export")</c>, más <c>Lending.Reports.ExportPersonalData</c> en las vistas con
    /// datos del deudor (C84); sin ellos, el mismo 404 que lo inexistente y sin auditar;</item>
    /// <item>el rango de los filtros comunes (hasta cinco años, no al revés) antes de consultar, en la fecha local;</item>
    /// <item>la auditoría <c>Lending.Report.Exported</c> por <see cref="LendingAuditEmitter"/> en cada exportación a archivo que sale bien, con la
    /// vista, los filtros, el formato y las filas (la cooperativa la pone el emisor): la emite esta ruta, y las consultas no la repiten;</item>
    /// <item>la entrega por <see cref="EntregaDeInformes.EntregarAsync"/> (tabla o archivo; el error con el sobre).</item>
    /// </list>
    /// </summary>
    /// <param name="grupo">El grupo <c>/api/reports/lending</c> (ya con <c>RequireAuthorization()</c>).</param>
    /// <param name="vista">Lo que la vista declara; va también como metadata de la ruta (el registro de vistas).</param>
    /// <param name="consulta">Arma la consulta con los filtros comunes y la query (para los filtros propios).</param>
    public static RouteHandlerBuilder MapVistaDeCartera(
        this RouteGroupBuilder grupo,
        VistaDeInformeDeCartera vista,
        Func<FiltrosDeInformeDeCartera, IQueryCollection, IRequest<Result<TablaExportable>>> consulta)
    {
        ArgumentNullException.ThrowIfNull(vista);
        ArgumentNullException.ThrowIfNull(consulta);

        var ruta = grupo.MapGet($"/{vista.Key}", async (
                [AsParameters] FiltrosDeInformeDeCartera filtros, HttpContext http, ISender sender, LendingAuditEmitter auditoria,
                IngenIA365ERP.Application.Common.Interfaces.IDateTimeService reloj, CancellationToken ct) =>
            {
                var rango = filtros.ValidarRango(reloj.HoyLocal);
                if (rango.IsFailure)
                    return await EntregaDeInformes.EntregarAsync(Result.Failure<TablaExportable>(rango.Error), filtros.Format, vista.FileName, http);

                var resultado = await sender.Send(consulta(filtros, http.Request.Query), ct);
                var exporta = EntregaDeInformes.EsExportacion(filtros.Format) && EntregaDeInformes.EsFormatoValido(filtros.Format);
                if (exporta && resultado.IsSuccess)
                    await auditoria.EmitirExportacionAsync(vista.Key, filtros.ParaAuditoria(), EntregaDeInformes.Normalizar(filtros.Format),
                        resultado.Value.Filas.Count, ct);
                return await EntregaDeInformes.EntregarAsync(resultado, filtros.Format, vista.FileName, http);
            })
            .WithName($"Reportes_Cartera_{vista.Key}")
            .WithMetadata(vista)
            .RequirePermission(PermisoDeVer)
            .RequirePermissionWhenExporting(PermisoDeExportar);

        if (vista.PersonalDataExceptWhen is { } excepcion)
        {
            // K5 (T0982): los datos del deudor dependen de un filtro propio (concentration por deudor o grupo): el permiso adicional y, al exportar,
            // el de datos personales se piden sólo cuando la consulta los trae; sin ellos, el mismo 404 que lo inexistente y sin auditar (C84).
            ruta.AddEndpointFilter(async (contexto, siguiente) =>
            {
                var http = contexto.HttpContext;
                if (!vista.TraeDatosDelDeudor(http.Request.Query[excepcion.Filter].ToString())) return await siguiente(contexto);
                if (vista.RequiredPermission is { } requerido && !await PermissionAuthorizationFilter.TieneAsync(http, requerido))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);
                if (EntregaDeInformes.EsExportacion(http.Request.Query["format"].ToString())
                    && !await PermissionAuthorizationFilter.TieneAsync(http, PermisoDeDatosPersonales))
                    return PermissionAuthorizationFilter.NotFoundEnvelope(http);
                return await siguiente(contexto);
            });
            return ruta;
        }
        if (vista.RequiredPermission is { } adicional) ruta.RequirePermission(adicional);
        if (vista.PersonalData) ruta.WithMetadata(new RequirePermissionWhenExportingAttribute(PermisoDeDatosPersonales));
        return ruta;
    }
}
