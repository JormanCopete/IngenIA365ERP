using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// Feature 028 (T053, T056; HU8, FR-028; api.md §5): la vista <c>payoffs</c> («Recogidas») lista, por producto recogido en los desembolsos del rango, lo
/// proyectado, lo aplicado, la diferencia y su causa —sólo con proyección (E-07)—, la cuota causada, quién radicó, decidió y desembolsó y el estado (aplicada,
/// reversada u omitida); filtra por rango, sucursal, línea y asesor del crédito nuevo y por la clase del producto; y se declara con datos del deudor. El
/// escenario es <see cref="DesembolsoDePrueba"/> con el contrato doble y el crédito de libro de Ana (1.000.000). (nuevo)
/// </summary>
public sealed class PayoffsReportQueryTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private Loan _viejo = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static int Columna(string nombre) => PayoffsReportQueryHandler.Columnas.Select(c => c.Nombre).ToList().IndexOf(nombre);

    private static object? Valor(FilaExportable fila, string columna) => fila.Valores[Columna(columna)];

    private FiltrosDeInformeDeCartera Filtros() => new() { From = DesembolsoDePrueba.Hoy.AddDays(-5), To = DesembolsoDePrueba.Hoy.AddDays(5) };

    private async Task<TablaExportable> InformeAsync(FiltrosDeInformeDeCartera? filtros = null, string? clase = null)
    {
        var r = await new PayoffsReportQueryHandler(_d.Db, _d.G.Alcance, _d.G.Reloj).Handle(new PayoffsReportQuery(filtros ?? Filtros(), clase), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<(CreditApplicationDto Solicitud, DisbursementResultDto Desembolso)> DesembolsadoAsync(Action? antes = null)
    {
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, _viejo.PublicId, PayoffMode.Total)] });
        antes?.Invoke();
        var vista = await _d.VistaPrevia().Handle(new PreviewDisbursementQuery(_d.Entrada(s)), default);
        _d.Db.ChangeTracker.Clear();
        var r = await _d.DesembolsadoAsync(_d.Entrada(s) with { AcknowledgedPayoffDifferences = true, ExpectedPreviewSha256 = vista.Value.PreviewSha256 });
        return (s, r);
    }

    /// <summary>Un pago en caja después de la proyección: su operación y lo que bajó del capital.</summary>
    private void Pago(Loan credito, decimal monto)
    {
        var operacion = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = "PAG-CAJA", Sequence = 1, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow.AddMinutes(5), Amount = monto, CollectorCode = "REC",
            IdempotencyKey = $"REC:{Guid.NewGuid():N}", Payload = "{}", InputsSha256 = "x", RulesVersion = "LND-2026.1",
        };
        _d.Db.LoanOperations.Add(operacion);
        _d.Db.SaveChanges();
        _d.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = credito.Id, Kind = LoanTransactionKind.PaymentAllocation, Concept = LoanConcept.Principal, Amount = -monto, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = operacion.RegisteredAt, OperationId = operacion.Id,
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------- la vista --

    [Fact]
    public void La_vista_se_declara_con_datos_del_deudor_y_sus_filtros()
    {
        var v = PayoffsReportQueryHandler.Vista;

        (v.Key, v.Name, v.FileName).Should().Be(("payoffs", "Recogidas", "recogidas"));
        v.PersonalData.Should().BeTrue("trae el nombre del deudor: exige Lending.Loans.Read y, al exportar, ExportPersonalData");
        v.RequiredPermission.Should().Be(VistaDeInformeDeCartera.PermisoDeDatosDelDeudor);
        v.Filters.Should().BeEquivalentTo("from", "to", "branch", "line", "advisor");
        v.OwnFilters.Should().Equal(PayoffsReportQueryHandler.FiltroDeClase);
        PayoffsReportQueryHandler.Columnas.Select(c => c.Nombre).Should().ContainInOrder("Fecha", "Crédito nuevo", "Solicitud", "Deudor", "Oficina", "Línea",
            "Producto", "Tipo", "Modo", "Proyectado", "Aplicado", "Diferencia", "Causa", "Cuota causada", "Interés devuelto", "Interés cobrado", "Asesor",
            "Decisor", "Desembolsó", "Estado");
        PayoffsReportQueryHandler.Columnas.Last().Clave.Should().Be("_credito", "la fila abre el crédito nuevo");
    }

    [Fact]
    public async Task Una_recogida_sale_con_lo_proyectado_lo_aplicado_y_quien_vendio_decidio_y_desembolso()
    {
        var (s, r) = await DesembolsadoAsync();

        var t = await InformeAsync();

        var fila = t.Filas.Should().ContainSingle().Subject;
        Valor(fila, "Fecha").Should().Be(DesembolsoDePrueba.Hoy);
        (Valor(fila, "Crédito nuevo"), Valor(fila, "Solicitud"), Valor(fila, "Producto"), Valor(fila, "Tipo"), Valor(fila, "Modo")).Should()
            .Be(((object?)r.LoanNumber, (object?)s.Number, (object?)"CR-VIEJO", (object?)"Crédito", (object?)"Total"));
        ((string)Valor(fila, "Deudor")!).Should().Contain("Ana");
        (Valor(fila, "Proyectado"), Valor(fila, "Aplicado"), Valor(fila, "Diferencia"), Valor(fila, "Causa")).Should()
            .Be(((object?)1_000_000m, (object?)1_000_000m, (object?)0m, (object?)string.Empty));
        (Valor(fila, "Cuota causada"), Valor(fila, "Estado")).Should().Be(((object?)"Incluida", (object?)PayoffsReportQueryHandler.Aplicada),
            "en la entrega E1 la cuota causada siempre se incluye; la recogida está viva");
        (Valor(fila, "Asesor"), Valor(fila, "Decisor"), Valor(fila, "Desembolsó")).Should()
            .Be(((object?)_d.O.Asesora.Email, (object?)_d.O.Analista.Email, (object?)_d.Cajera.Email));
        fila.Valores.Last().Should().Be(r.LoanPublicId.ToString());
        fila.Resaltada.Should().BeFalse();
        t.Totales!.Valores[Columna("Aplicado")].Should().Be(1_000_000m);
        t.Titulo.Should().Be("Recogidas");
    }

    [Fact]
    public async Task Una_diferencia_por_pagos_sale_con_su_causa_y_los_totales_la_suman()
    {
        await DesembolsadoAsync(() => Pago(_viejo, 250_000m));

        var fila = (await InformeAsync()).Filas.Should().ContainSingle().Subject;

        (Valor(fila, "Proyectado"), Valor(fila, "Aplicado"), Valor(fila, "Diferencia")).Should()
            .Be(((object?)1_000_000m, (object?)750_000m, (object?)(-250_000m)));
        Valor(fila, "Causa").Should().Be("Pagos recibidos desde la proyección");
        (Valor(fila, "Interés devuelto"), Valor(fila, "Interés cobrado")).Should().Be(((object?)0m, (object?)0m),
            "lo de ahora por concepto es lo proyectado más la diferencia");
    }

    [Fact]
    public async Task Sin_proyeccion_no_hay_diferencia_y_la_reversada_va_marcada_y_fuera_de_los_totales()
    {
        // Una fila copiada de PayoffRequest (E-07): la solicitud no guardó proyección.
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, _viejo.PublicId, PayoffMode.Total)] });
        var proyectada = await _d.Db.CreditApplicationPayoffs.SingleAsync(f => !f.IsDeleted);
        _d.Db.Entry(proyectada).Property(f => f.ProjectionSha256).CurrentValue = string.Empty;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        await _d.DesembolsadoAsync(_d.Entrada(s));

        var fila = (await InformeAsync()).Filas.Should().ContainSingle().Subject;
        (Valor(fila, "Proyectado"), Valor(fila, "Aplicado"), Valor(fila, "Diferencia"), Valor(fila, "Causa")).Should()
            .Be(((object?)null, (object?)1_000_000m, (object?)null, (object?)string.Empty), "sin proyección no hay con qué compararse");
        (Valor(fila, "Interés devuelto"), Valor(fila, "Interés cobrado")).Should().Be(((object?)null, (object?)null));

        var recogida = await _d.Db.DisbursementPayoffs.SingleAsync();
        recogida.Reversar(_d.G.UtcNow, recogida.IdempotencyKey + ":reverso");
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var t = await InformeAsync();
        var reversada = t.Filas.Should().ContainSingle().Subject;
        Valor(reversada, "Estado").Should().Be(PayoffsReportQueryHandler.Reversada);
        reversada.Resaltada.Should().BeTrue();
        t.Totales!.Valores[Columna("Aplicado")].Should().Be(0m, "los totales no cuentan las reversadas");
    }

    // ------------------------------------------------------------------------------------------------------- filtros --

    [Fact]
    public async Task Los_filtros_de_rango_linea_y_clase_acotan_y_una_clase_desconocida_se_rechaza()
    {
        await DesembolsadoAsync();

        (await InformeAsync(clase: "Loan")).Filas.Should().ContainSingle();
        (await InformeAsync(clase: "account")).Filas.Should().BeEmpty("sin distinguir mayúsculas; ninguna cuenta se recogió");
        (await InformeAsync(new FiltrosDeInformeDeCartera { From = DesembolsoDePrueba.Hoy.AddDays(1), To = DesembolsoDePrueba.Hoy.AddDays(5) }))
            .Filas.Should().BeEmpty("el rango es la fecha valor del desembolso");
        var otraLinea = Filtros();
        otraLinea.Line = Guid.NewGuid();
        var vacia = await InformeAsync(otraLinea);
        vacia.Filas.Should().BeEmpty("una línea que no existe no muestra nada");
        vacia.Totales.Should().BeNull();
        var asesor = Filtros();
        asesor.Advisor = _d.O.Asesora.PublicId;
        (await InformeAsync(asesor)).Filas.Should().ContainSingle("la radicó la asesora");
        asesor.Advisor = _d.O.Analista.PublicId;
        (await InformeAsync(asesor)).Filas.Should().BeEmpty();

        var mala = await new PayoffsReportQueryHandler(_d.Db, _d.G.Alcance, _d.G.Reloj).Handle(new PayoffsReportQuery(Filtros(), "Ahorros"), default);
        mala.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
    }
}
