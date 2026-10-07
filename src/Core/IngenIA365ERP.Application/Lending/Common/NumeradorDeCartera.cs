using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>
/// El único que numera en Cartera (feature 013, C48, T0207; data-model §0.3, §1.10): el único que toca <c>LND_NumberSequences</c>
/// y el que arma el <c>Number</c> de créditos, operaciones, solicitudes, órdenes, archivos, actas y documentos. Lo llama la operación
/// <b>al final</b> del orden de cerrojos (T34) y dentro de su transacción:
/// <list type="number">
/// <item>busca la fila de (<see cref="LendingNumberKind"/>, ámbito); si falta, la agrega con el prefijo pedido (la inserta el
/// <c>SaveChanges</c> de la operación);</item>
/// <item>si existe, la bloquea con <see cref="ICerrojoDeCartera.BloquearNumeracionAsync"/> y la relee ya bloqueada;</item>
/// <item>arma <c>Prefix + NextValue</c> con ceros a la izquierda hasta <c>MinDigits</c>, <b>salta</b> los números que ya existen en
/// su tabla (migrados o reservados: no tienen que ser continuos, FR-057) e incrementa <c>NextValue</c> por EF; el guardado de la
/// operación lo escribe todo junto.</item>
/// </list>
/// Un borrador no consume número: la solicitud se numera al radicar (<see cref="NumerarSolicitudAsync"/> rechaza un borrador) y el
/// acta al cerrar. Nadie más escribe <c>NextValue</c> ni asigna <c>Number</c> en <c>Entities/Lending</c> (<c>SoloElNumeradorNumera</c>).
/// (nuevo)
/// </summary>
public sealed class NumeradorDeCartera(IApplicationDbContext db, ICerrojoDeCartera cerrojo)
{
    /// <summary>Dígitos mínimos de una fila nueva (con ceros a la izquierda).</summary>
    public const int DigitosPorDefecto = 6;

    /// <summary>Largo máximo de un número de Cartera (<c>nvarchar(20)</c>, data-model §0.3).</summary>
    public const int LargoMaximo = 20;

    /// <summary>
    /// El siguiente número de la clase en su ámbito. <paramref name="prefijo"/> sólo se usa al crear la fila (después rige el de
    /// la fila); <paramref name="ambito"/> es <c>''</c> o la clave del ámbito (clase de operación, <c>PublicId:N</c> del órgano…).
    /// </summary>
    public async Task<string> AsignarAsync(LendingNumberKind clase, string ambito, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ambito);
        ArgumentNullException.ThrowIfNull(prefijo);

        // Una fila ya seguida en esta unidad de trabajo (agregada por otra operación de la tanda, o ya bloqueada antes): se sigue
        // con ella, que lleva el NextValue al día y, si venía de la base, el candado tomado.
        var secuencia = db.LendingNumberSequences.Local.FirstOrDefault(s => s.Kind == clase && s.ScopeKey == ambito);
        if (secuencia is null)
        {
            var id = await db.LendingNumberSequences.AsNoTracking()
                .Where(s => s.Kind == clase && s.ScopeKey == ambito)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct);
            if (id is null)
            {
                secuencia = new LendingNumberSequence { Kind = clase, ScopeKey = ambito, Prefix = prefijo, NextValue = 1, MinDigits = DigitosPorDefecto };
                db.LendingNumberSequences.Add(secuencia);
            }
            else
            {
                // Bloquea primero y lee después: así se lee el NextValue que dejó el último que numeró.
                await cerrojo.BloquearNumeracionAsync(id.Value, ct);
                secuencia = await db.LendingNumberSequences.FirstAsync(s => s.Id == id.Value, ct);
            }
        }

        while (true)
        {
            var numero = Formatear(secuencia.Prefix, secuencia.NextValue, secuencia.MinDigits);
            secuencia.NextValue = secuencia.NextValue + 1;
            if (numero.Length > LargoMaximo)
                throw new InvalidOperationException($"El número {numero} de {clase} pasa de {LargoMaximo} caracteres.");
            if (!await ExisteAsync(clase, numero, ct)) return numero;
        }
    }

    /// <summary>El radicado de una solicitud que se envía a análisis (nunca un borrador, data-model §2.1).</summary>
    public async Task NumerarSolicitudAsync(CreditApplication solicitud, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Status == CreditApplicationStatus.Draft)
            throw new InvalidOperationException("Un borrador no consume número: la solicitud se numera al radicarla.");
        if (solicitud.Number is not null)
            throw new InvalidOperationException($"La solicitud {solicitud.PublicId} ya tiene número.");
        solicitud.Number = await AsignarAsync(LendingNumberKind.Application, string.Empty, prefijo, ct);
    }

    /// <summary>El número del acta de una sesión que se cierra: ámbito = el órgano (<c>PublicId:N</c>), prefijo = el del órgano (C48).</summary>
    public async Task NumerarActaAsync(Session sesion, GoverningBody organo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sesion);
        ArgumentNullException.ThrowIfNull(organo);
        if (sesion.Status == SessionStatus.Draft)
            throw new InvalidOperationException("Un acta en borrador no consume número: se numera al cerrar la sesión.");
        if (sesion.Number is not null)
            throw new InvalidOperationException($"La sesión {sesion.PublicId} ya tiene número.");
        sesion.Number = await AsignarAsync(LendingNumberKind.SessionMinutes, AmbitoDeOrgano(organo), organo.MinutesPrefix, ct);
    }

    /// <summary>
    /// El consecutivo de una carga de migración (feature 013, T0232; data-model §29.1: <c>Number</c> entero, único). No tiene fila en
    /// <c>LND_NumberSequences</c> (<c>LendingNumberKind</c> no la nombra): es el mayor de <c>LND_MigrationLoads</c>, borradas incluidas, más
    /// uno; el índice único es la defensa ante dos cargas a la vez.
    /// </summary>
    public async Task NumerarCargaAsync(MigrationLoad carga, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(carga);
        if (carga.Number != 0)
            throw new InvalidOperationException($"La carga {carga.PublicId} ya tiene número.");
        var ultimo = await db.MigrationLoads.IgnoreQueryFilters().MaxAsync(l => (int?)l.Number, ct) ?? 0;
        var enEsteAmbito = db.MigrationLoads.Local.Select(l => l.Number).DefaultIfEmpty(0).Max();
        carga.Number = Math.Max(ultimo, enEsteAmbito) + 1;
    }

    /// <summary>El ámbito del consecutivo de actas de un órgano.</summary>
    public static string AmbitoDeOrgano(GoverningBody organo) => organo.PublicId.ToString("N", CultureInfo.InvariantCulture);

    /// <summary><c>Prefix</c> + el número con ceros a la izquierda hasta <paramref name="digitos"/>.</summary>
    public static string Formatear(string prefijo, long valor, int digitos) =>
        prefijo + valor.ToString(CultureInfo.InvariantCulture).PadLeft(digitos, '0');

    /// <summary>¿El número ya existe en la tabla de su clase? (migrado, reservado o asignado antes; FR-057.)</summary>
    private Task<bool> ExisteAsync(LendingNumberKind clase, string numero, CancellationToken ct) => clase switch
    {
        LendingNumberKind.Loan => ExisteCreditoAsync(numero, ct),
        LendingNumberKind.Operation => db.LoanOperations.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct),
        LendingNumberKind.Application => db.CreditApplications.IgnoreQueryFilters().AnyAsync(a => a.Number == numero, ct),
        LendingNumberKind.DisbursementOrder => db.DisbursementOrders.IgnoreQueryFilters().AnyAsync(o => o.Number == numero, ct),
        LendingNumberKind.DisbursementFile => db.DisbursementFiles.IgnoreQueryFilters().AnyAsync(f => f.Number == numero, ct),
        LendingNumberKind.SessionMinutes => db.LendingSessions.IgnoreQueryFilters().AnyAsync(s => s.Number == numero, ct),
        LendingNumberKind.IssuedDocument => db.IssuedLendingDocuments.IgnoreQueryFilters().AnyAsync(d => d.Number == numero, ct),
        // K2 (archivo de descuentos) y K5 (archivos regulatorios): sus tablas nacen con su entrega.
        _ => Task.FromResult(false),
    };

    private async Task<bool> ExisteCreditoAsync(string numero, CancellationToken ct) =>
        await db.Loans.IgnoreQueryFilters().AnyAsync(l => l.Number == numero, ct)
        || await db.LoanNumberReservations.IgnoreQueryFilters().AnyAsync(r => r.Number == numero, ct);
}
