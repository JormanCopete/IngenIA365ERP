using System.Globalization;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Deposits.Assembly.PlantillaDeBaseDeRetornos.C;

namespace IngenIA365ERP.Application.Lending.Deposits.Assembly;

/// <summary>
/// La plantilla de la base de los retornos (feature 014, respuesta 90 C del dueño, E261): la asamblea define sobre qué se reparten los retornos cooperativos
/// —el uso de servicios, los ahorros u otra base— y la cooperativa sube <b>una fila por asociado</b> con esa base, para una decisión en borrador cuya base es
/// «subida por plantilla». Todo o nada, con revisión previa y errores por fila y columna; la plantilla no crea personas ni borra: una carga nueva completa o
/// corrige la anterior (un <c>0</c> saca al asociado). (nuevo)
/// </summary>
public static class PlantillaDeBaseDeRetornos
{
    public const string Clave = "lending.assembly-return-bases";

    public static class C
    {
        public const string Decision = "decision";
        public const string Persona = "persona";
        public const string Base = "base";
        public const string Concepto = "concepto";
    }

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Base de los retornos de la asamblea", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(C.Decision, TipoDeValor.Texto, Obligatoria: true, Largo: 20,
                Reglas: "número de la decisión de asamblea (ASM-…) en borrador, de excedentes, registrada con la base «subida por plantilla»; todo el archivo es de una sola decisión",
                Ejemplo: "ASM-000001"),
            new(C.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "llave; documento sin puntos ni dígito de verificación; debe existir en Maestros › Personas y ser asociado con aportes vivos (o haberse retirado en el ejercicio); esta plantilla no crea personas"),
            new(C.Base, TipoDeValor.Monto, Obligatoria: true,
                Reglas: "la base del asociado que definió la asamblea (uso de servicios, ahorros…), mayor o igual a cero; 0 = no participa", Ejemplo: "1250000,00"),
            new(C.Concepto, TipoDeValor.Texto, Largo: AssemblyReturnBase.LargoDelConcepto,
                Reglas: "sobre qué se reparte (informativo)", Ejemplo: "Uso de servicios 2026"),
        ]),
    ]);
}

public sealed record ImportAssemblyReturnBasesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportAssemblyReturnBasesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    DistribucionesDeAsamblea distribuciones) : IRequestHandler<ImportAssemblyReturnBasesCommand, Result<ImportResultDto>>
{
    public const string CodigoDecisionInvalida = "Lending.AssemblyReturnBases.DecisionInvalid";

    public Task<Result<ImportResultDto>> Handle(ImportAssemblyReturnBasesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeBaseDeRetornos.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        if (filas.Count == 0) return;

        // Una decisión por archivo, en borrador, de excedentes y con la base subida por plantilla.
        if (await distribuciones.AlcanceTotalAsync(null, ct) is { IsFailure: true } fuera)
        {
            ctx.ErrorSinFila(null, C.Decision, ErroresDeAsamblea.CodigoInexistente, fuera.Error.Message);
            return;
        }
        var numeros = filas.Select(f => f.Crudo(C.Decision)).OfType<string>().Select(n => n.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).ToList();
        if (numeros.Count > 1)
        {
            foreach (var f in filas) f.Error(C.Decision, ImportErrors.CellFormat, $"El archivo mezcla decisiones ({string.Join(", ", numeros)}): suba un archivo por decisión.");
            return;
        }
        var numero = numeros.FirstOrDefault();
        var decision = numero is null ? null : await db.AssemblyDistributions.AsNoTracking().FirstOrDefaultAsync(x => x.Number == numero && !x.IsDeleted, ct);
        string? problema = decision switch
        {
            null => $"No hay una decisión de asamblea «{numero}».",
            { Kind: not AssemblyDistributionKind.SurplusDistribution } => $"La decisión {numero} no es de excedentes: la base por plantilla sólo se usa en los retornos.",
            { BaseMethod: not RevaluationBase.UploadedBase } => $"La decisión {numero} usa otra base: regístrela con la base «subida por plantilla».",
            { Status: not AssemblyDistributionStatus.Simulated } d => $"La decisión {numero} está «{d.Status}»: la base sólo se sube mientras está en borrador.",
            _ => null,
        };
        if (problema is not null || decision is null)
        {
            foreach (var f in filas) f.Error(C.Decision, ImportErrors.CellNotFound, problema ?? "La decisión no existe.");
            return;
        }

        var documentos = filas.Select(f => f.Crudo(C.Persona)).OfType<string>().Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var personas = (await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).ToListAsync(ct))
            .GroupBy(p => p.TaxId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var ids = personas.Values.ToList();
        var desde = new DateOnly(decision.FiscalYear, 1, 1);
        var conAportes = (await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.HolderPersonId) && a.Nature == LineNature.Contribution && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .Select(a => a.HolderPersonId).Distinct().ToListAsync(ct)).ToHashSet();
        var retiros = await db.MembershipWithdrawals.AsNoTracking().Where(w => ids.Contains(w.PersonId) && !w.IsDeleted)
            .Select(w => new { w.PersonId, w.Status, w.EffectiveDate }).ToListAsync(ct);
        var retirados = retiros.Where(w => w.Status is MembershipWithdrawalStatus.Settled or MembershipWithdrawalStatus.Paid && w.EffectiveDate >= desde)
            .Select(w => w.PersonId).ToHashSet();
        var enRetiro = retiros.Where(w => w.Status is MembershipWithdrawalStatus.Approved or MembershipWithdrawalStatus.ToRedo).Select(w => w.PersonId).ToHashSet();

        // La carga vigente: la nueva versión es la anterior corregida con lo que trae el archivo (la plantilla no borra).
        var version = await db.AssemblyReturnBases.AsNoTracking().Where(b => b.AssemblyDistributionId == decision.Id).MaxAsync(b => (int?)b.LoadVersion, ct) ?? 0;
        var anteriores = version == 0
            ? new Dictionary<int, AssemblyReturnBase>()
            : await db.AssemblyReturnBases.AsNoTracking().Where(b => b.AssemblyDistributionId == decision.Id && b.LoadVersion == version)
                .ToDictionaryAsync(b => b.PersonId, ct);

        var nuevas = new Dictionary<int, (decimal Base, string? Concepto)>();
        var cambios = 0;
        foreach (var fila in filas)
        {
            var documento = fila.Texto(C.Persona, 20);
            var valor = fila.Monto(C.Base);
            var concepto = fila.Texto(C.Concepto, AssemblyReturnBase.LargoDelConcepto);
            fila.Requerido(C.Persona);
            fila.Requerido(C.Base);
            if (!ctx.Datos.LlaveUnica(fila, documento, C.Persona) || fila.TieneErrores || documento is null || valor is null) continue;
            if (valor < 0m)
            {
                fila.Error(C.Base, ImportErrors.CellFormat, "La base no es negativa (0 = no participa).");
                continue;
            }
            if (!personas.TryGetValue(documento, out var persona))
            {
                fila.Error(C.Persona, ImportErrors.CellNotFound, $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }
            if (!conAportes.Contains(persona) && !retirados.Contains(persona))
            {
                fila.Error(C.Persona, ImportErrors.CellNotFound, enRetiro.Contains(persona)
                    ? $"La persona «{documento}» tiene un retiro en trámite: queda fuera de la distribución hasta que se liquide."
                    : $"La persona «{documento}» no tiene una cuenta de aportes viva ni se retiró en el ejercicio {decision.FiscalYear}: no participa en los retornos.");
                continue;
            }

            nuevas[persona] = (valor.Value, concepto);
            if (anteriores.TryGetValue(persona, out var previa) && previa.BaseAmount == valor && string.Equals(previa.Concept, concepto, StringComparison.Ordinal))
            {
                ctx.Registrar(fila, documento, AccionDeImportacion.Unchanged);
                continue;
            }
            cambios++;
            ctx.Registrar(fila, documento, previa is null ? AccionDeImportacion.Create : AccionDeImportacion.Update,
                previa is null ? null : [new(C.Base, previa.BaseAmount.ToString(CultureInfo.InvariantCulture), valor.Value.ToString(CultureInfo.InvariantCulture))]);
        }
        if (ctx.HayErrores || cambios == 0) return;

        // La versión nueva: lo anterior que el archivo no toca y lo que el archivo trae.
        foreach (var (persona, previa) in anteriores)
            if (!nuevas.ContainsKey(persona)) nuevas[persona] = (previa.BaseAmount, previa.Concept);
        foreach (var (persona, dato) in nuevas.OrderBy(x => x.Key))
            db.AssemblyReturnBases.Add(new AssemblyReturnBase
            {
                AssemblyDistributionId = decision.Id, LoadVersion = version + 1, PersonId = persona, BaseAmount = dato.Base, Concept = dato.Concepto,
            });
    }
}
