using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T361 (FR-039; contracts/api.md §11; data-model §7.1): resolver una diferencia de traslado crea el documento que la
/// resuelve —devolución al origen (<c>ReturnOf</c>), baja desde el tránsito con causa, recepción tardía o ajuste positivo del
/// sobrante— y lo deja en aprobación con <c>Subject = TransferDiscrepancy</c> y <c>SourceType = TransferDiscrepancy</c>, con la
/// política del tipo de la recepción o, sin ella, un nivel con <c>Inventory.Transfers.Approve</c>. Quien despachó, quien recibió y
/// quien propone no aprueban. Aprobada, el documento se confirma y la diferencia queda resuelta (por la cantidad aprobada);
/// rechazada, vuelve a pendiente.
/// </summary>
public class ResolverDiferenciaDeTrasladoTests
{
    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Despacha 10 (usuario 7), recibe <paramref name="recibidas"/> (el receptor) y deja actuando a quien propone.</summary>
    private static async Task<(TrasladosDePrueba T, Guid Despacho, Guid Diferencia)> FaltanteAsync(decimal recibidas = 9m, decimal? sobrante = null)
    {
        var t = await TrasladosDePrueba.CrearAsync();
        t.UsarMotorReal();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        t.ComoUsuario(TrasladosDePrueba.Receptor);
        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, recibidas, sobrante));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        t.ComoUsuario(TrasladosDePrueba.Proponente);
        var diferencia = sobrante is not null ? r.Value.Surpluses[0].DiscrepancyPublicId : r.Value.Shortages[0].DiscrepancyPublicId;
        return (t, despacho, diferencia);
    }

    [Fact]
    public async Task La_baja_desde_el_transito_queda_en_aprobacion_con_la_regla_fija_y_aprobada_resuelve()
    {
        var (t, _, diferencia) = await FaltanteAsync();

        var r = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.WriteOffFromTransit,
            causa: t.K.Causa(AdjustmentCausesSeeder.CodigoReclamacionAlTransportador));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.DocumentClass.Should().Be(DocumentClass.WriteOff);
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        var baja = t.Documento(r.Value.DocumentPublicId);
        baja.WarehouseId.Should().Be(t.TR01.Id, "la baja sale del tránsito");
        var solicitud = await t.Db.ApprovalRequests.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.ApprovalRequestPublicId);
        solicitud.Subject.Should().Be(ApprovalSubjects.TransferDiscrepancy);
        solicitud.SourceType.Should().Be(ApprovalSourceTypes.TransferDiscrepancy);
        solicitud.SourcePublicId.Should().Be(diferencia);
        solicitud.PolicyId.Should().BeNull("sin política, la regla fija de un nivel");
        solicitud.NivelesRequeridos().Should().ContainSingle().Which.PermissionCode.Should().Be("Inventory.Transfers.Approve");
        t.Diferencia(diferencia).Estado.Should().Be(TransferDiscrepancy.EstadoEnAprobacion);

        var aprobada = await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        t.Documento(r.Value.DocumentPublicId).Status.Should().Be(DocumentStatus.Confirmed);
        var resuelta = t.Diferencia(diferencia);
        resuelta.Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
        resuelta.ResolvedAt.Should().NotBeNull();
        t.Fisico(t.K.P1, t.TR01).Should().Be(0m, "el tránsito queda en cero exacto");
        var kardex = await t.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == baja.Id && k.Kind == KardexEntryKind.Exit).SingleAsync();
        kardex.UnitCost.Should().Be(1_000m, "sale al costo de la línea de despacho");
        var mensaje = await t.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == r.Value.DocumentPublicId);
        mensaje.Type.Should().Be(AjusteInventarioAprobadoV1.Type);
        using var json = JsonDocument.Parse(mensaje.PayloadJson);
        json.RootElement.GetProperty("operation").GetString().Should().Be("Baja");
    }

    [Fact]
    public async Task Quien_despacho_quien_recibio_y_quien_propone_no_aprueban()
    {
        var (t, _, diferencia) = await FaltanteAsync();
        (await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt)).IsSuccess.Should().BeTrue();

        foreach (var usuario in new[] { TrasladosDePrueba.Despachador, TrasladosDePrueba.Receptor, TrasladosDePrueba.Proponente })
        {
            var r = await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve, usuario);
            t.Codigo(r).Should().Be("Approvals.SelfApprovalForbidden", $"el usuario {usuario} participó");
        }
        t.Diferencia(diferencia).Estado.Should().Be(TransferDiscrepancy.EstadoEnAprobacion);
    }

    [Fact]
    public async Task Devolver_al_origen_saca_del_transito_y_vuelve_al_origen()
    {
        var (t, despacho, diferencia) = await FaltanteAsync();

        var r = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.ReturnToOrigin, motivo: "no se cargó en el camión");
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.DocumentClass.Should().Be(DocumentClass.TransferReceipt);
        var devolucion = t.Documento(r.Value.DocumentPublicId);
        var vinculo = await t.Db.DocumentLinks.AsNoTracking().SingleAsync(l => l.TargetDocumentId == devolucion.Id);
        vinculo.Kind.Should().Be(DocumentLinkKind.ReturnOf);
        vinculo.SourceDocumentId.Should().Be(t.Documento(despacho).Id);

        (await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve)).IsSuccess.Should().BeTrue();

        t.Fisico(t.K.P1, t.PRIN).Should().Be(11m, "20 − 10 despachadas + 1 devuelta");
        t.Fisico(t.K.P1, t.TR01).Should().Be(0m);
        t.Diferencia(diferencia).Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
        var mensaje = await t.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == r.Value.DocumentPublicId);
        mensaje.Type.Should().Be(TrasladoRecibidoV1.Type);
    }

    [Fact]
    public async Task La_recepcion_tardia_entra_al_destino()
    {
        var (t, _, diferencia) = await FaltanteAsync();

        (await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt, motivo: "llegó en el segundo viaje")).IsSuccess.Should().BeTrue();
        (await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve)).IsSuccess.Should().BeTrue();

        t.Fisico(t.K.P1, t.PV2).Should().Be(10m);
        t.Fisico(t.K.P1, t.TR01).Should().Be(0m);
        t.Diferencia(diferencia).Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
    }

    [Fact]
    public async Task El_sobrante_se_resuelve_con_un_ajuste_positivo_en_el_destino_al_costo_vigente()
    {
        var (t, _, diferencia) = await FaltanteAsync(recibidas: 10m, sobrante: 1m);

        var r = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.SurplusAdjustment,
            causa: t.K.Causa(AdjustmentCausesSeeder.CodigoDiferenciaDeConteo), motivo: "venía una de más");
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.DocumentClass.Should().Be(DocumentClass.PositiveAdjustment);
        (await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve)).IsSuccess.Should().BeTrue();

        t.Fisico(t.K.P1, t.PV2).Should().Be(11m);
        var resuelta = t.Diferencia(diferencia);
        resuelta.Estado.Should().Be(TransferDiscrepancy.EstadoResuelta);
        resuelta.UnitCost.Should().Be(1_000m, "el sobrante entra al costo vigente al resolverse");
    }

    [Fact]
    public async Task Rechazada_vuelve_a_pendiente_y_descarta_el_documento()
    {
        var (t, _, diferencia) = await FaltanteAsync();
        var r = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt);

        var rechazada = await t.DecidirAsync(diferencia, ApprovalDecisionKind.Reject, motivo: "no ha llegado nada");

        rechazada.IsSuccess.Should().BeTrue(rechazada.IsFailure ? $"{rechazada.Error.Code}: {rechazada.Error.Message}" : string.Empty);
        t.Diferencia(diferencia).Estado.Should().Be(TransferDiscrepancy.EstadoPendiente);
        t.Documento(r.Value.DocumentPublicId).Status.Should().Be(DocumentStatus.Discarded);
        t.Fisico(t.K.P1, t.TR01).Should().Be(1m);

        t.ComoUsuario(TrasladosDePrueba.Proponente);
        (await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.ReturnToOrigin)).IsSuccess.Should().BeTrue("se puede volver a pedir");
    }

    [Fact]
    public async Task Aprobada_por_una_parte_resuelve_esa_parte()
    {
        var (t, _, diferencia) = await FaltanteAsync(recibidas: 8m);

        (await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt, cantidad: 1m)).IsSuccess.Should().BeTrue();
        (await t.DecidirAsync(diferencia, ApprovalDecisionKind.Approve)).IsSuccess.Should().BeTrue();

        var parcial = t.Diferencia(diferencia);
        parcial.Estado.Should().Be(TransferDiscrepancy.EstadoPendiente);
        parcial.ResolvedQuantityBase.Should().Be(1m);
        parcial.Pendiente().Should().Be(1m);
        t.Fisico(t.K.P1, t.TR01).Should().Be(1m);
    }

    [Fact]
    public async Task Los_errores_de_la_resolucion()
    {
        var (t, _, diferencia) = await FaltanteAsync();

        var otraSalida = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.SurplusAdjustment, causa: t.K.Causa(AdjustmentCausesSeeder.CodigoDiferenciaDeConteo));
        t.Codigo(otraSalida).Should().Be("Inventory.TransferDiscrepancy.ResolutionNotAllowed");
        Datos(otraSalida.Error).GetProperty("kind").GetString().Should().Be("Shortage");
        Datos(otraSalida.Error).GetProperty("allowed").EnumerateArray().Select(a => a.GetString()).Should()
            .Equal("ReturnToOrigin", "WriteOffFromTransit", "LateReceipt");

        var demasiado = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt, cantidad: 5m);
        t.Codigo(demasiado).Should().Be("Inventory.TransferDiscrepancy.QuantityExceeds");
        Datos(demasiado.Error).GetProperty("pendingBase").GetDecimal().Should().Be(1m);

        var sinCausa = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.WriteOffFromTransit);
        t.Codigo(sinCausa).Should().Be("Inventory.Document.FieldRequired");

        var causaQueNoSirve = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.WriteOffFromTransit, causa: t.K.Causa("MERMA"));
        t.Codigo(causaQueNoSirve).Should().Be("Inventory.TransferDiscrepancy.CauseNotAllowed");

        (await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt)).IsSuccess.Should().BeTrue();
        var otraVez = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.ReturnToOrigin);
        t.Codigo(otraVez).Should().Be("Inventory.TransferDiscrepancy.NotPending");
    }

    [Fact]
    public async Task Con_politica_del_tipo_de_la_recepcion_la_solicitud_la_sella()
    {
        var (t, _, diferencia) = await FaltanteAsync();
        var recepcion = t.K.Tipo("TRR");
        var politica = new ApprovalPolicy
        {
            Module = ApprovalPolicy.ModuloInventario, Subject = ApprovalSubjects.TransferDiscrepancy, DocumentTypePublicId = recepcion.PublicId,
            PolicyKey = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.TransferDiscrepancy, recepcion.PublicId),
            Version = 1, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            Levels =
            [
                new ApprovalPolicyLevel { Order = 1, Threshold = 0m, PermissionCode = "Inventory.Transfers.Approve" },
                new ApprovalPolicyLevel { Order = 2, Threshold = 0m, PermissionCode = "Inventory.Transfers.Approve" },
            ],
        };
        t.Db.ApprovalPolicies.Add(politica);
        await t.Db.SaveChangesAsync();

        var r = await t.ResolverAsync(diferencia, TransferDiscrepancyResolution.LateReceipt);

        var solicitud = await t.Db.ApprovalRequests.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.ApprovalRequestPublicId);
        solicitud.PolicyId.Should().Be(politica.Id);
        solicitud.NivelesRequeridos().Should().HaveCount(2);
    }

    [Fact]
    public async Task La_semilla_deja_la_politica_de_diferencias_en_el_tipo_de_recepcion_de_traslado()
    {
        var c = await CatalogoDePrueba.CrearAsync();

        await InventoryDocumentTypesSeeder.AplicarAsync(c.Db, default);
        var otraVez = await InventoryDocumentTypesSeeder.AplicarAsync(c.Db, default);

        otraVez.Should().Be(0, "es idempotente");
        var recepcion = await c.Db.InventoryDocumentTypes.SingleAsync(x => x.Class == DocumentClass.TransferReceipt);
        var politica = await c.Db.ApprovalPolicies.Include(p => p.Levels).SingleAsync(p => p.Subject == ApprovalSubjects.TransferDiscrepancy);
        politica.DocumentTypePublicId.Should().Be(recepcion.PublicId);
        politica.Levels.Should().ContainSingle().Which.Should().Match<ApprovalPolicyLevel>(l =>
            l.Order == 1 && l.Threshold == 0m && l.PermissionCode == InventoryDocumentTypesSeeder.PermisoDeAprobacionDeDiferencias);
    }
}
