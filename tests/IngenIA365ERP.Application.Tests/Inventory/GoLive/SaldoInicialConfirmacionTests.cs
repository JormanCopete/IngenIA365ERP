using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.GoLive;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.GoLive;

/// <summary>
/// Feature 012, T298 (FR-089, US4-4, US4-5; api.md §13.1; mensajes.md §7.1): confirmar el saldo inicial pasa por la aprobación
/// sembrada del tipo (un nivel, umbral 0, <c>Inventory.OpeningBalance.Approve</c>) que no se puede vaciar ni aprobar su creador;
/// aprobado, entra al costo cargado en la fecha de corte y emite <c>SaldoInicialCargado</c> informativo sin modo de paso; su
/// anulación es informativa y sólo con la bodega no activa; y la excepción de puesta en marcha (T18, D8) lo admite antes de
/// ventas ya registradas en otra bodega del ámbito, con un <c>AjusteDeCostoReconocido</c> por documento afectado.
/// </summary>
public class SaldoInicialConfirmacionTests
{
    private const int Aprobador = 8;

    private static string Mensaje<T>(IngenIA365ERP.Application.Common.Models.Result<T> r) => r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty;

    // ------------------------------------------------------------------------------------------- aprobación --

    [Fact]
    public async Task Confirmar_queda_en_aprobacion_por_la_politica_sembrada_y_su_creador_no_la_aprueba()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var motor = p.UsarMotorReal();
        var documento = await p.BorradorAsync(p.B3, "P1", 10m, 1500m);

        var r = await p.ConfirmarAsync(documento);

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull();
        r.Value.Approval!.Levels.Should().ContainSingle().Which.PermissionCode.Should().Be("Inventory.OpeningBalance.Approve");

        var solicitud = await p.Db.ApprovalRequests.AsNoTracking().SingleAsync();
        var propia = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, ApprovalDecisionKind.Approve, null, solicitud.ContentSha256, null), default);
        propia.IsFailure.Should().BeTrue();
        propia.Error.Code.Should().Be("Approvals.SelfApprovalForbidden");
        (await p.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.PublicId == documento)).Status.Should().Be(DocumentStatus.PendingApproval);
    }

    [Fact]
    public async Task La_politica_del_tipo_de_saldo_inicial_no_se_puede_vaciar()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var reglas = new ReglasDePlataformaDeInventario(p.Db, p.K.Alcance, p.K.Lector(), p.K.Permisos);
        var tipo = await p.Db.InventoryDocumentTypes.SingleAsync(t => t.Class == DocumentClass.OpeningBalance);

        var r = await new SaveApprovalPolicyCommandHandler(p.Db, reglas).Handle(
            new SaveApprovalPolicyCommand(ApprovalSubjects.DocumentConfirmation, tipo.PublicId, new DateOnly(2026, 10, 1), "Sin aprobación", []), default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Approvals.Policy.RequiredForClass");
    }

    [Fact]
    public async Task Aprobado_por_otra_persona_entra_al_costo_cargado_en_el_corte_y_emite_SaldoInicialCargado_informativo()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var motor = p.UsarMotorReal();
        var documento = await p.BorradorAsync(p.B3, "P1", 10m, 1500m);
        (await p.ConfirmarAsync(documento)).Value.Status.Should().Be(DocumentStatus.PendingApproval);
        var solicitud = await p.Db.ApprovalRequests.AsNoTracking().SingleAsync();

        p.ComoUsuario(Aprobador);
        var r = await motor.DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, ApprovalDecisionKind.Approve, null, solicitud.ContentSha256, null), default);

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        var confirmado = await p.Db.InventoryDocuments.AsNoTracking().SingleAsync(d => d.PublicId == documento);
        confirmado.Status.Should().Be(DocumentStatus.Confirmed);
        confirmado.PostingMode.Should().BeNull("el saldo inicial sólo emite informativos: no lleva modo de paso");
        var entrada = await p.Db.KardexEntries.AsNoTracking().SingleAsync(k => k.DocumentId == confirmado.Id);
        entrada.OperationDate.Should().Be(PuestaEnMarchaDePrueba.Corte);
        entrada.Kind.Should().Be(KardexEntryKind.Entry);
        entrada.QuantityBase.Should().Be(10m);
        entrada.UnitCost.Should().Be(1500m, "entra al costo cargado (FR-044)");

        var mensaje = await p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == documento);
        mensaje.Type.Should().Be(SaldoInicialCargadoV1.Type);
        mensaje.Kind.Should().Be(IntegrationMessageKind.Informational);
        var contenido = JsonDocument.Parse(mensaje.PayloadJson).RootElement;
        contenido.GetProperty("cutoffDate").GetString().Should().Be("2026-09-10");
        var linea = contenido.GetProperty("lines").EnumerateArray().Single();
        linea.GetProperty("accountingGroupCode").GetString().Should().Be("ABARR");
        linea.GetProperty("warehouseCode").GetString().Should().Be("B3");
        linea.GetProperty("cost").GetDecimal().Should().Be(15_000m);
    }

    [Fact]
    public async Task Sin_niveles_la_confirmacion_no_tiene_validacion_previa()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var documento = await p.BorradorAsync(p.B3, "P1", 2m, 100m);

        var r = await p.ConfirmarAsync(documento);

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        r.Value.PostingMode.Should().BeNull();
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
    }

    // -------------------------------------------------------------------------------------------- anulación --

    [Fact]
    public async Task Anular_con_la_bodega_no_activa_es_informativo_y_con_la_bodega_activa_se_rechaza()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        var enB3 = await p.SaldoConfirmadoAsync(p.B3, "P1", 10m, 1500m);
        var enB4 = await p.SaldoConfirmadoAsync(p.B4, "P2", 3m, 800m);

        var anulada = await p.Anular().Handle(new VoidInventoryDocumentCommand(enB3, DocumentClassGroup.OpeningBalance, "Conteo mal digitado"), default);

        anulada.IsSuccess.Should().BeTrue(Mensaje(anulada));
        var mensaje = await p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == anulada.Value.VoidingDocumentPublicId);
        mensaje.Type.Should().Be(DocumentoAnuladoV1.Type);
        mensaje.Kind.Should().Be(IntegrationMessageKind.Informational, "hereda el Kind del saldo inicial (§7.1)");

        p.Db.ChangeTracker.Clear();
        var b4 = await p.Db.Warehouses.SingleAsync(w => w.Id == p.B4.Id);
        b4.Activar(PuestaEnMarchaDePrueba.Corte, KardexDePrueba.Usuario, DateTimeOffset.UtcNow);
        await p.Db.SaveChangesAsync();
        p.Db.ChangeTracker.Clear();

        var activa = await p.Anular().Handle(new VoidInventoryDocumentCommand(enB4, DocumentClassGroup.OpeningBalance, "Tarde"), default);

        activa.IsFailure.Should().BeTrue();
        activa.Error.Code.Should().Be(GoLiveErrors.OpeningBalanceWarehouseActiveCode);
    }

    // ------------------------------------------------------------------------ excepción de puesta en marcha --

    [Fact]
    public async Task Con_retroactivos_prohibidos_el_saldo_de_la_segunda_bodega_se_confirma_y_reconoce_el_ajuste_de_costo_de_las_ventas()
    {
        var p = await PuestaEnMarchaDePrueba.CrearAsync();
        p.K.Parametro(ParametrosDeInventario.CosteoRetroactivosPermitidos, "false");
        var k = p.K;
        var (_, entrada) = await k.AjusteAsync(k.Borrador("AJP", fecha: new DateOnly(2026, 9, 12), lineas: [k.Linea(k.P1, 10m, 1000m)]));
        entrada.IsSuccess.Should().BeTrue(Mensaje(entrada));
        var (venta, salida) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), fecha: new DateOnly(2026, 9, 15), lineas: [k.Linea(k.P1, 4m)]));
        salida.IsSuccess.Should().BeTrue(Mensaje(salida));
        p.Db.ChangeTracker.Clear();

        var saldo = await p.BorradorAsync(p.B3, "P1", 10m, 2000m);
        var r = await p.ConfirmarAsync(saldo);

        r.IsSuccess.Should().BeTrue(Mensaje(r));
        var ajuste = await p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == AjusteDeCostoReconocidoV1.Type);
        ajuste.RelatedPublicId.Should().Be(venta, "un AjusteDeCostoReconocido por documento afectado");
        ajuste.Kind.Should().Be(IntegrationMessageKind.Business);
        var linea = JsonDocument.Parse(ajuste.PayloadJson).RootElement.GetProperty("lines").EnumerateArray().Single();
        Math.Abs(linea.GetProperty("soldAmount").GetDecimal()).Should().Be(2_000m, "4 unidades salieron a 1.000 y debieron salir al promedio de 1.500 (el signo lo fija US3)");

        p.Db.ChangeTracker.Clear();
        var (_, digitado) = await k.AjusteAsync(k.Borrador("AJP", fecha: PuestaEnMarchaDePrueba.Corte, lineas: [k.Linea(k.P1, 1m, 1000m)]));
        digitado.IsFailure.Should().BeTrue();
        digitado.Error.Code.Should().Be("Inventory.Costing.RetroactiveNotAllowed", "la excepción es sólo del saldo inicial de una bodega no activa");
    }
}
