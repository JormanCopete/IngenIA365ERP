using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

// ================================================================================================================ asociados ==

/// <summary>
/// La vista <c>associates</c> (2026-11-27, pedido del dueño): el listado completo de asociados con los datos de su persona y de su afiliación, para
/// revisar y exportar a Excel en un paso. Es información personal: consultarla exige <c>Lending.Loans.Read</c> (C84) y exportarla, además,
/// <c>Lending.Reports.ExportPersonalData</c>; cada exportación queda en la auditoría (la ruta). Respeta el alcance de oficinas de quien consulta
/// (un asociado sin oficina sólo lo ve quien no tiene restricción). Filtro propio <c>status</c>: <c>Active</c>, <c>Withdrawn</c> o vacío (todos). (nuevo)
/// </summary>
public sealed record AssociatesReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status) : IRequest<Result<TablaExportable>>;

public sealed class AssociatesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<AssociatesReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Listado de asociados";
    public const string Activos = "Active";
    public const string Retirados = "Withdrawn";

    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor("associates", Titulo,
        "Todos los asociados con sus datos personales, de contacto y de afiliación (ingreso, estado, retiro, oficina, grupo, aporte). Filtre por activos o retirados.",
        "listado-de-asociados", ["branch"], ["status"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Tipo de documento"), new("Documento"), new("Dígito de verificación"), new("Usuario"),
        new("Primer nombre"), new("Otros nombres"), new("Primer apellido"), new("Segundo apellido"), new("Razón social"), new("Nombre completo"),
        new("Tipo de persona"), new("Género"), new("Estado civil"), new("Fecha de nacimiento", TipoDeColumna.Fecha),
        new("Fecha de expedición", TipoDeColumna.Fecha), new("Lugar de expedición"),
        new("Dirección"), new("Ciudad"), new("Teléfono 1"), new("Teléfono 2"), new("Celular"), new("Correo"),
        new("Nivel educativo"), new("Estrato"), new("Tipo de vivienda"), new("Cabeza de hogar"),
        new("Fecha de ingreso", TipoDeColumna.Fecha), new("Estado"), new("Código de estado"), new("Fecha de retiro", TipoDeColumna.Fecha),
        new("Motivo de retiro"), new("Fecha de reingreso", TipoDeColumna.Fecha),
        new("Oficina"), new("Grupo"), new("Tasa de aporte", TipoDeColumna.Decimal), new("Aporte comprometido", TipoDeColumna.Moneda),
        new("Día de corte", TipoDeColumna.Entero), new("Forma de pago"), new("Periodo de deducción"), new("Tipo de deducción"), new("Clase de asociado"),
        new("Calificación"), new("Empresa donde trabaja"), new("Salario", TipoDeColumna.Moneda), new("En cobro jurídico"), new("Fallecido"),
        new("_persona", Clave: "_persona"),
    ];

    public async Task<Result<TablaExportable>> Handle(AssociatesReportQuery request, CancellationToken ct)
    {
        var estado = FiltrosDeInformeDeCartera.Elegir("status", request.Status, ["All", Activos, Retirados], "All");
        if (estado.IsFailure) return Result.Failure<TablaExportable>(estado.Error);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        int? oficina = null;
        if (request.Filtros.Branch is { } b)
        {
            oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
        }

        var consulta = from a in db.Associates.AsNoTracking()
                       join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PersonId equals p.Id
                       where !a.IsDeleted
                       select new { a, p };
        if (oficina is { } o) consulta = consulta.Where(x => x.a.BranchId == o);
        var filasCrudas = await consulta.ToListAsync(ct);
        filasCrudas = filasCrudas.Where(x => alcance.TodasLasOficinas || (x.a.BranchId is { } id && alcance.IncluyeOficina(id))).ToList();
        if (estado.Value == Activos) filasCrudas = filasCrudas.Where(x => !AssociateRegistrar.EsRetirado(x.a.Status)).ToList();
        if (estado.Value == Retirados) filasCrudas = filasCrudas.Where(x => AssociateRegistrar.EsRetirado(x.a.Status)).ToList();

        var ciudadIds = filasCrudas.Select(x => x.p.CityId).OfType<int>().Distinct().ToList();
        var ciudades = await db.Cities.AsNoTracking().IgnoreQueryFilters().Where(c => ciudadIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var oficinaIds = filasCrudas.Select(x => x.a.BranchId).OfType<int>().Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(c => oficinaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var grupoIds = filasCrudas.Select(x => x.a.AssociateGroupId).OfType<int>().Distinct().ToList();
        var grupos = await db.AssociateGroups.AsNoTracking().IgnoreQueryFilters().Where(c => grupoIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var motivoIds = filasCrudas.Select(x => x.a.WithdrawalReasonId).OfType<int>().Distinct().ToList();
        var motivos = await db.WithdrawalReasons.AsNoTracking().IgnoreQueryFilters().Where(c => motivoIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        static string SiNo(bool v) => v ? "Sí" : "No";
        var filas = filasCrudas
            .OrderBy(x => PersonFactory.NombreVisible(x.p.FirstName, x.p.OtherNames, x.p.LastName, x.p.SecondLastName, x.p.BusinessName), StringComparer.CurrentCultureIgnoreCase)
            .Select(x =>
            {
                var (a, p) = (x.a, x.p);
                var retirado = AssociateRegistrar.EsRetirado(a.Status);
                return new FilaExportable(
                [
                    p.IdType, p.TaxId, p.TaxIdCheckDigit, p.Username,
                    p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName,
                    PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName),
                    p.PersonType switch { "02" => "Jurídica", "01" => "Natural", _ => p.PersonType }, p.Gender, p.MaritalStatus, p.DateOfBirth,
                    p.IdIssueDate, p.IdIssuedAt,
                    p.Address, p.CityId is { } c ? ciudades.GetValueOrDefault(c) : null, p.Phone1, p.Phone2, p.Mobile, p.Email,
                    p.EducationLevel, p.SocialStratum, p.HousingType, SiNo(p.IsHeadOfHousehold),
                    a.JoinDate, retirado ? "Retirado" : "Activo", a.Status, a.WithdrawalDate,
                    a.WithdrawalReasonId is { } m ? motivos.GetValueOrDefault(m) : null, a.RejoinDate,
                    a.BranchId is { } ofi ? oficinas.GetValueOrDefault(ofi) : null, a.AssociateGroupId is { } g ? grupos.GetValueOrDefault(g) : null,
                    a.ContributionRate, a.ContributionPledged, (int)a.CutoffDay, a.PaymentType, a.DeductionPeriod, a.DeductionType, a.AssociateClass,
                    a.ManualRating ?? a.CategoryRating, a.ExternalEmployerName, a.ExternalSalary, SiNo(a.IsInLegalCollection), SiNo(p.IsDeceased),
                    p.PublicId.ToString(),
                ], Resaltada: retirado);
            })
            .ToList();

        var activos = filasCrudas.Count(x => !AssociateRegistrar.EsRetirado(x.a.Status));
        var notas = new List<string>
        {
            $"{filas.Count} asociados: {activos} activos y {filas.Count - activos} retirados.",
            "Información personal protegida: úsela sólo para la finalidad autorizada por el titular. Cada exportación queda registrada en la auditoría.",
        };
        return Result.Success(new TablaExportable(Titulo, $"{filas.Count} asociados", Columnas, filas, null, notas));
    }
}

// =================================================================================================== cuadre con contabilidad ==

/// <summary>
/// La vista <c>accounting-reconciliation</c> (2026-11-27, pedido del dueño): la conciliación contable de Cartera
/// (<see cref="GetLendingAccountingReconciliationQuery"/>, la misma de <c>/cartera/conciliacion-contable</c>) como tabla exportable: por conjunto de
/// cuentas el saldo de Cartera, el contable, la diferencia y lo que la explica; debajo, hecho por hecho, lo que todavía no pasó a Contabilidad; las
/// combinaciones de Cartera sin regla en la matriz (que Contabilidad no puede mirar) y los puentes de recaudo. Son cifras sin datos de deudores, con
/// el alcance de oficinas de quien consulta (lo aplica la conciliación; aquí se lee para decirlo en la nota). (nuevo)
/// </summary>
public sealed record AccountingReconciliationReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class AccountingReconciliationReportQueryHandler(ISender sender, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<AccountingReconciliationReportQuery, Result<TablaExportable>>
{
    public const string Titulo = "Cuadre de Cartera con Contabilidad";
    public const string SeccionConjuntos = "1. Saldos de Cartera frente a Contabilidad";
    public const string SeccionHechos = "2. Hechos de Cartera que no han pasado a Contabilidad";
    public const string SeccionSinRegla = "3. Saldos de Cartera sin cuenta en la matriz contable";
    public const string SeccionPuentes = "4. Cuentas puente de recaudo";

    public static readonly VistaDeInformeDeCartera Vista = new("accounting-reconciliation", Titulo,
        "Saldo de Cartera y saldo contable por conjunto de cuentas, la diferencia y lo que la explica, los hechos que no han pasado a Contabilidad y los saldos sin regla.",
        "cuadre-cartera-contabilidad", ["asOf", "branch"]);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Concepto"), new("Cuentas contables"), new("Líneas"), new("Detalle"),
        new("Saldo en Cartera", TipoDeColumna.Moneda), new("Saldo contable", TipoDeColumna.Moneda), new("Diferencia", TipoDeColumna.Moneda),
        new("Explicado por hechos pendientes", TipoDeColumna.Moneda), new("Sin explicar", TipoDeColumna.Moneda), new("Observación"),
    ];

    public async Task<Result<TablaExportable>> Handle(AccountingReconciliationReportQuery request, CancellationToken ct)
    {
        var corte = request.Filtros.ALaFecha(reloj.HoyLocal);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var leida = await sender.Send(new GetLendingAccountingReconciliationQuery(corte, request.Filtros.Branch), ct);
        if (leida.IsFailure) return Result.Failure<TablaExportable>(leida.Error);
        var c = leida.Value;

        var filas = new List<FilaExportable>();
        foreach (var s in c.Sets.OrderByDescending(s => Math.Abs(s.Difference)).ThenBy(s => s.Family, StringComparer.Ordinal))
        {
            var explicado = s.ExplainedBy.Pending + s.ExplainedBy.InBatch + s.ExplainedBy.Rejected + s.ExplainedBy.NotPosted;
            var lineas = string.Join(", ", s.Combinations.Select(x => x.CreditLine).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal));
            var detalle = explicado == 0m ? string.Empty
                : $"Pendiente {s.ExplainedBy.Pending:N2} · en lote {s.ExplainedBy.InBatch:N2} · rechazado {s.ExplainedBy.Rejected:N2} · no contabilizado {s.ExplainedBy.NotPosted:N2}";
            var observacion = s.Difference == 0m ? "Cuadra"
                : s.Unexplained == 0m ? "La diferencia la explican hechos que no han pasado"
                : s.UnexplainedIsOtherSide ? "Lo no explicado está del otro lado (puente)" : "Diferencia sin explicar: revisar";
            filas.Add(new FilaExportable(
            [
                s.Family, string.Join(", ", s.Accounts.Select(a => $"{a.Code} {a.Name}")), lineas, detalle,
                s.LendingBalance, s.AccountingBalance, s.Difference, explicado, s.Unexplained, observacion,
            ], SeccionConjuntos, Resaltada: s.Unexplained != 0m && !s.UnexplainedIsOtherSide));
        }
        foreach (var s in c.Sets)
            foreach (var h in s.Facts.OrderBy(h => h.AccountingDate))
                filas.Add(new FilaExportable(
                [
                    h.Type, string.Join(", ", s.Accounts.Select(a => a.Code)), null, $"{h.Number} · {h.AccountingDate:yyyy-MM-dd} · {h.Status}",
                    h.Amount, null, null, h.Amount, null, s.Family,
                ], SeccionHechos, Resaltada: true));
        foreach (var u in c.UnresolvedCombinations)
            filas.Add(new FilaExportable(
            [
                u.Role, null, u.CreditLine, string.Join(" · ", new[] { u.Concept, u.Modality, u.Category }.Where(x => !string.IsNullOrWhiteSpace(x))),
                u.LendingBalance, null, null, null, u.LendingBalance, "Sin regla en la matriz: Contabilidad no tiene cuenta donde mirarlo",
            ], SeccionSinRegla, Resaltada: true));
        foreach (var p in c.Bridges)
            filas.Add(new FilaExportable([p.CollectorClass, null, null, null, null, p.Balance, null, null, null, "Saldo del puente de recaudo"], SeccionPuentes));

        var conjuntos = c.Sets.ToList();
        var totales = new FilaExportable(["Total", null, null, null, conjuntos.Sum(s => s.LendingBalance), conjuntos.Sum(s => s.AccountingBalance),
            conjuntos.Sum(s => s.Difference), null, c.UnexplainedTotal, null], Resaltada: true);
        var notas = new List<string>
        {
            "Los dos saldos van en el sentido de Cartera. La diferencia que explican los hechos pendientes, en lote, rechazados o no contabilizados se cierra cuando pasen a Contabilidad.",
            c.UnexplainedTotal == 0m ? "No hay diferencias sin explicar." : $"Sin explicar en total: {c.UnexplainedTotal:N2}. Revise las filas resaltadas.",
            "El detalle de cada hecho y sus créditos está en Cartera → Conciliación contable, pestaña «Hechos».",
        };
        if (!alcance.TodasLasOficinas) notas.Add("Con el alcance de oficinas de quien consulta: no es el cuadre de toda la cooperativa.");
        return Result.Success(new TablaExportable(Titulo, $"Al {corte:yyyy-MM-dd}", Columnas, filas, totales, notas));
    }
}
