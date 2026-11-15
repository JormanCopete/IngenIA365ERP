using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Platform.Modules;
using IngenIA365ERP.Application.Transport.Catalogs;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Transport.Platform;

/// <summary>
/// Lo que Transporte deja listo en la cooperativa al encenderse (feature 020, R6, R12; contracts/mensajes.md §4): el participante
/// <c>TRN</c> de la bandeja, que escucha los pagos y anulaciones como destino opcional, y los catálogos sembrados. Idempotente.
///
/// <para>
/// <b>Apagar no cierra el participante</b>, a propósito: la spec (bordes) pide que, con el módulo deshabilitado y facturas de
/// servicios pendientes, sus pagos se sigan aplicando y los envíos salgan si el conector sigue habilitado. El consumidor descarta en
/// una consulta lo que no toca ningún cargo, así que dejarlo escuchando no cuesta. Deshabilitar sólo cierra pantallas y rutas.
/// </para>
/// </summary>
public sealed class ProvisionadorDeTransporte(
    IApplicationDbContext db,
    EscritorDelRegistroDeIntegracion escritor,
    SembradoDeTransporte sembrado,
    IDateTimeService reloj,
    ILogger<ProvisionadorDeTransporte> logger) : IProvisionadorDeModulo
{
    /// <summary>El código del participante de Transporte en el registro de integración.</summary>
    public const string CodigoDelParticipante = "TRN";

    /// <summary>El código del participante de los conectores en el registro de integración.</summary>
    public const string CodigoDeConectores = "CON";

    private const string Motivo = "Transporte habilitado por el administrador de la plataforma.";

    /// <summary>Lo que Transporte adopta como destino opcional (R12).</summary>
    public static readonly IReadOnlyList<VersionAdoptada> Adopciones =
    [
        new(PagoAplicadoV1.Type, 1),
        new(PagoReversadoV1.Type, 1),
        new(ReciboEmitidoV1.Type, 1),
        new(ReciboAnuladoV1.Type, 1),
        new(DocumentoAnuladoV1.Type, 1),
    ];

    public string Modulo => ModulosHabilitables.Transporte;

    public async Task AsegurarEncendidoAsync(CancellationToken ct)
    {
        await AsegurarParticipanteAsync(CodigoDelParticipante, IntegrationParticipantClass.Transport, IntegrationDestinations.Transport, Adopciones, ct);
        // T154 (contracts/mensajes.md §4): el consumidor de los avisos a sistemas externos. Sin él, la solicitud no tendría entrega.
        await AsegurarParticipanteAsync(CodigoDeConectores, IntegrationParticipantClass.Connectors, IntegrationDestinations.Connectors,
            [new(Common.Integration.Contracts.Transport.SolicitudDeConectorV1.Type, 1)], ct);
        await sembrado.AsegurarAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public Task AsegurarApagadoAsync(CancellationToken ct) => Task.CompletedTask;

    /// <summary>Registra el participante si falta, lo habilita si está apagado o le suma las adopciones que le falten.</summary>
    internal async Task AsegurarParticipanteAsync(
        string codigo, IntegrationParticipantClass clase, string destino, IReadOnlyList<VersionAdoptada> adopciones, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var vigente = await db.IntegrationParticipants.AsNoTracking().Include(p => p.Adoptions)
            .Where(p => !p.IsDeleted && p.Code == codigo && p.Role == IntegrationParticipantRole.MessageDestination
                        && p.Destination == destino && p.ValidTo == null)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(ct);

        if (vigente is null)
        {
            var nuevo = await escritor.RegistrarAsync(new NuevoParticipante(
                codigo, clase, IntegrationParticipantRole.MessageDestination, destino, IsEnabled: true, IsTestDouble: false,
                adopciones, hoy, Motivo), ct);
            if (nuevo.IsFailure) logger.LogWarning("[Transporte.Participante] {Codigo}: {Error}", codigo, nuevo.Error.Message);
            return;
        }

        // Un cambio el mismo día en que nació la fila se cruzaría con ella: espera a mañana.
        if (vigente.ValidFrom >= hoy) return;

        if (!vigente.IsEnabled)
        {
            var habilitado = await escritor.CambiarHabilitacionAsync(codigo, IntegrationParticipantRole.MessageDestination, destino, true, hoy, Motivo, ct);
            if (habilitado.IsFailure) logger.LogWarning("[Transporte.Participante] {Codigo}: {Error}", codigo, habilitado.Error.Message);
            return;
        }

        var adoptadas = vigente.Adoptions.Where(a => !a.IsDeleted && a.ValidTo == null).Select(a => (a.ContractName, (int)a.Version)).ToHashSet();
        var faltan = adopciones.Where(a => !adoptadas.Contains((a.Contrato, a.Version))).ToList();
        if (faltan.Count == 0) return;
        var adoptado = await escritor.AdoptarAsync(codigo, IntegrationParticipantRole.MessageDestination, destino, faltan, hoy, Motivo, ct);
        if (adoptado.IsFailure) logger.LogWarning("[Transporte.Participante] {Codigo}: {Error}", codigo, adoptado.Error.Message);
    }
}
