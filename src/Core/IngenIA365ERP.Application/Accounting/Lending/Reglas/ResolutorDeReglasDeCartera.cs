using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// Los valores que un importe de un hecho busca en la matriz (§3.3, paso 2): las dimensiones del crédito a la fecha contable (en las
/// partes, las de cada línea por crédito), el concepto y si es «por cuenta del asociado», la clase de recaudador, el motivo y la sucursal
/// del hecho (en un cambio de oficina, la de cada punta). La sucursal va por <c>PublicId</c>: el resolutor la traduce al <c>Id</c> de Core.
/// Lo que el rol no admite se ignora. (nuevo)
/// </summary>
public sealed record ValoresDeCartera(
    string? CreditLineCode = null,
    string? ModalityCode = null,
    string? SubsegmentCode = null,
    string? RiskCategory = null,
    string? ConceptCode = null,
    bool? IsEmployee = null,
    bool? InGracePeriod = null,
    bool? OnBehalfOfAssociate = null,
    bool? Restructured = null,
    string? CollectorClass = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null);

/// <summary>
/// Una pregunta a la matriz de Cartera: el hecho (el <c>Type</c> del mensaje), el rol, los valores, la fecha de las reglas —la fecha contable
/// del hecho, también en los reversos (§9)—, el importe —con cero no se pide regla (§3.3)— y los créditos que suman ahí (para el error y
/// para el mapa de líneas). (nuevo)
/// </summary>
public sealed record PeticionDeReglaDeCartera(
    string Operation,
    string Role,
    ValoresDeCartera Valores,
    DateOnly Fecha,
    decimal Importe,
    IReadOnlyList<string> Loans);

/// <summary>
/// La regla que ganó, con su cuenta ya cargada, la sucursal del hecho traducida a <c>Id</c> y el centro de costo de la regla, que se asigna a
/// la línea sólo si la cuenta lo maneja (C77). (nuevo)
/// </summary>
public sealed record ReglaDeCarteraResuelta(
    PeticionDeReglaDeCartera Peticion,
    LendingPostingRule Regla,
    Domain.Entities.Accounting.ChartOfAccount Cuenta,
    Rules.CuentaParaReglas CuentaParaReglas,
    int? BranchId,
    int? CostCenterId);

/// <summary>Una candidata de una pregunta (<c>ResolveLendingRuleQuery</c>): la regla, su peso y si casó. (nuevo)</summary>
public sealed record CandidataDeCartera(LendingPostingRule Regla, bool Coincide);

/// <summary>Un hecho ya contabilizado que limita una regla (§3.5): su fecha contable y el documento. (nuevo)</summary>
public sealed record HechoContabilizado(Guid MessagePublicId, DateOnly OperationDate, string Documento);

/// <summary>
/// La resolución de la matriz de Cartera (feature 013, T0641, T52; contracts/contabilidad.md §3.3 a §3.5). Carga en bloque las reglas que
/// rigen en un rango de fechas con sus cuentas y los mapas de sucursales y centros (<see cref="CargarAsync"/>, sin seguimiento) y responde,
/// sin más consultas, qué regla gana para cada importe (<see cref="MatrizVigenteDeCartera.Resolver"/>, puro: el mismo código en la
/// validación previa, el consumo, la vista previa, la conciliación y <c>ResolveLendingRuleQuery</c>). Además busca el hecho contabilizado
/// que coincide con una regla, para «sin retroactividad sobre lo contabilizado» (<see cref="UltimoQueCoincideAsync"/>). Molde:
/// <c>ResolutorDeReglas</c> de la 012. (nuevo)
/// </summary>
public sealed class ResolutorDeReglasDeCartera(IApplicationDbContext db)
{
    /// <summary>Cuántos recibos se leen por tanda al buscar en el contenido de los hechos.</summary>
    private const int Tanda = 500;

    /// <summary>
    /// Las reglas no borradas cuya vigencia toca <c>[desde, hasta]</c>, con sus cuentas (y las tarifas de éstas) y los mapas
    /// <c>PublicId → Id</c> de sucursales y centros de costo. Sin seguimiento: nada de esto se escribe.
    /// </summary>
    public async Task<MatrizVigenteDeCartera> CargarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var reglas = await db.LendingPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.ValidFrom <= hasta && (r.ValidTo == null || r.ValidTo >= desde))
            .ToListAsync(ct);
        var idsDeCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates)
            .Where(a => idsDeCuenta.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var centros = await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted).ToDictionaryAsync(c => c.Id, c => c.PublicId, ct);
        return new MatrizVigenteDeCartera(desde, hasta, reglas, cuentas, sucursales, centros);
    }

    // ---------------------------------------------------------------------------- retroactividad (§3.5) --

    /// <summary>
    /// El hecho contabilizado (con comprobante) más reciente, con fecha contable en <c>[desde, hasta]</c>, que tiene un importe que podría
    /// casar con <paramref name="regla"/>; nulo si ninguno. Sirve a las dos reglas de §3.5: una versión nueva de una clave no puede empezar en
    /// o antes de él, y una clave nueva puede empezar antes sólo si no hay ninguno desde su <c>ValidFrom</c> (así se rescatan hechos
    /// rechazados por regla faltante sin cambiar el resultado de nada contabilizado). La comparación es sobre los valores del contenido y
    /// del sobre y es <b>conservadora</b>: cuenta como coincidencia que cada código fijado aparezca en el hecho (los booleanos no se
    /// comparan), para no dejar pasar nunca una regla que habría cambiado un comprobante. Sólo se miran los hechos que pueden mover el rol.
    /// </summary>
    public async Task<HechoContabilizado?> UltimoQueCoincideAsync(LendingPostingRule regla, DateOnly desde, DateOnly? hasta, CancellationToken ct)
    {
        var tipos = OperacionesDeCartera.DeNegocio
            .Where(o => (regla.Operation == LendingPostingRule.Cualquiera || o.Codigo == regla.Operation) && o.TieneRol(regla.Role))
            .Select(o => o.Codigo)
            .ToList();
        if (tipos.Count == 0) return null;

        var sucursal = regla.BranchId is { } b
            ? await db.Branches.AsNoTracking().Where(x => x.Id == b).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;

        var consulta =
            from p in db.LendingPostings.AsNoTracking()
            join m in db.IntegrationMessages.AsNoTracking() on p.MessagePublicId equals m.PublicId
            where p.AccountingDocumentId != null && !p.IsDeleted && tipos.Contains(p.MessageType) && p.OperationDate >= desde
            select new
            {
                p.Id, p.MessagePublicId, p.OperationDate, p.SourceDocumentTypeCode, p.SourceDocumentNumber, p.SourceDocumentClass, p.LoanNumber,
                m.PayloadJson, m.BranchPublicId,
            };
        if (hasta is { } h) consulta = consulta.Where(x => x.OperationDate <= h);
        consulta = consulta.OrderByDescending(x => x.OperationDate).ThenByDescending(x => x.Id);

        for (var salto = 0; ; salto += Tanda)
        {
            var tanda = await consulta.Skip(salto).Take(Tanda).ToListAsync(ct);
            foreach (var x in tanda)
            {
                var valores = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { x.BranchPublicId.ToString() };
                Recoger(Leer(x.PayloadJson), valores);
                if (!Coincide(regla, sucursal, valores)) continue;
                var documento = string.Join(' ', new[] { x.SourceDocumentTypeCode ?? x.SourceDocumentClass, x.SourceDocumentNumber, x.LoanNumber is null ? null : "· crédito " + x.LoanNumber }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                return new HechoContabilizado(x.MessagePublicId, x.OperationDate, documento);
            }
            if (tanda.Count < Tanda) return null;
        }
    }

    private static bool Coincide(LendingPostingRule regla, Guid? sucursal, HashSet<string> valores)
    {
        bool Esta(string? codigo) => codigo is null || valores.Contains(codigo);

        if (!Esta(regla.CreditLineCode) || !Esta(regla.ModalityCode) || !Esta(regla.SubsegmentCode) || !Esta(regla.RiskCategory)
            || !Esta(regla.ConceptCode) || !Esta(regla.CollectorClass) || !Esta(regla.ReasonCode))
            return false;
        return sucursal is not { } s || valores.Contains(s.ToString());
    }

    private static void Recoger(JsonElement? elemento, HashSet<string> valores)
    {
        if (elemento is not { } e) return;
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propiedad in e.EnumerateObject()) Recoger(propiedad.Value, valores);
                break;
            case JsonValueKind.Array:
                foreach (var item in e.EnumerateArray()) Recoger(item, valores);
                break;
            case JsonValueKind.String:
                if (e.GetString() is { Length: > 0 } texto)
                {
                    valores.Add(texto.Trim());
                    // La categoría de un hecho puede venir con su subcategoría (E1): la regla la lleva por su letra (§3.4).
                    if (texto.Trim().Length == 2 && char.IsLetter(texto.Trim()[0]) && char.IsDigit(texto.Trim()[1])) valores.Add(texto.Trim()[..1]);
                }
                break;
        }
    }

    private static JsonElement? Leer(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
