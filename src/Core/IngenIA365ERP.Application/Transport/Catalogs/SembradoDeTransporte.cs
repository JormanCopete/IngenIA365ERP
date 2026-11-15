using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Catalogs;

/// <summary>
/// Siembra los catálogos y los tipos de documento de Transporte en la cooperativa en curso (feature 020, R6, T049, T071). Agrega sólo
/// lo que falta por código —también entre los borrados, para no resucitar ni duplicar lo que la cooperativa retiró— y no toca lo que
/// la cooperativa renombró, inactivó o configuró. No guarda: guarda quien lo llama (<c>ProvisionadorDeTransporte</c>). Lo corre la
/// tarea de aprovisionamiento al encender el módulo y en cada pasada mientras siga encendido, así que una versión nueva de
/// <see cref="SemillaDeTransporte"/> llega sola.
/// </summary>
public sealed class SembradoDeTransporte(IApplicationDbContext db, IDateTimeService reloj)
{
    private const string Sembrador = "Seed";

    /// <summary>Lo que agregó la última llamada (para el registro).</summary>
    public int Agregados { get; private set; }

    public async Task AsegurarAsync(CancellationToken ct)
    {
        Agregados = 0;
        var ahora = reloj.UtcNow;

        var existentes = (await db.TransportCatalogItems.IgnoreQueryFilters().AsNoTracking()
                .Select(c => new { c.Kind, c.Code })
                .ToListAsync(ct))
            .Select(c => (c.Kind, c.Code.ToUpperInvariant()))
            .ToHashSet();
        foreach (var valor in SemillaDeTransporte.Catalogos.Where(v => !existentes.Contains((v.Kind, v.Codigo))))
        {
            db.TransportCatalogItems.Add(new TransportCatalogItem
            {
                Kind = valor.Kind, Code = valor.Codigo, Name = valor.Nombre, IsActive = true, IsSystem = true,
                CreatedAt = ahora, CreatedBy = Sembrador,
            });
            Agregados++;
        }

        var tipos = (await db.TransportDocumentTypes.IgnoreQueryFilters().AsNoTracking().Select(t => t.Code).ToListAsync(ct))
            .Select(c => c.ToUpperInvariant())
            .ToHashSet();
        foreach (var tipo in SemillaDeTransporte.TiposDeDocumento.Where(t => !tipos.Contains(t.Codigo)))
        {
            db.TransportDocumentTypes.Add(new TransportDocumentType
            {
                Code = tipo.Codigo, Name = tipo.Nombre, IsRequired = tipo.Obligatorio, WarningDays = tipo.DiasDeAviso,
                IsActive = true, IsSystem = true, CreatedAt = ahora, CreatedBy = Sembrador,
            });
            Agregados++;
        }
    }
}
