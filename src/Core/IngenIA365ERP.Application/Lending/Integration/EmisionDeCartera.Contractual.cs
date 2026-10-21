using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Los hechos del ahorro contractual tal como se emiten (feature 014, US7, T183; mensajes.md §1.1 #16 y #18, §4.1; contabilidad.md §3 filas 16 y 18; research
/// §19): la bonificación —reconocida al cumplirse o provisionada, con signo— y la penalidad del incumplimiento, cada una con sus valores por concepto y, la
/// bonificación que se abona, con la retención que se le practica. (nuevo)
/// </summary>
public sealed record HechoDeContractualAEmitir(
    HechoDeCuenta Hecho,
    MemberAccountOperation Operacion,
    PrevalidationOutcome ValidacionPrevia)
{
    /// <summary>Lo emitió el ciclo diario (el vencimiento, el incumplimiento, la provisión).</summary>
    public bool PorElProceso { get; init; }

    /// <summary>
    /// La bonificación (<c>BonificacionReconocida</c>): sus valores por concepto (+ <c>Bonus</c> al provisionar; − <c>Bonus</c> y + <c>Principal</c> al abonar
    /// la provisionada; + <c>Principal</c> la que se reconoce y abona al cumplirse; − <c>Bonus</c> al reversar la provisión por incumplimiento) y el cumplimiento.
    /// </summary>
    public (IReadOnlyList<DepositConceptAmountV1> Valores, ContractualBonusV1 Contrato)? Bonificacion { get; init; }

    /// <summary>La penalidad del incumplimiento (<c>PenalidadAplicada</c>, <c>ContractualBreach</c>): sus valores (−interés por pagar, −capital) y su base.</summary>
    public (IReadOnlyList<DepositConceptAmountV1> Valores, decimal Base)? Penalidad { get; init; }

    /// <summary>La retención de la bonificación abonada (sale del capital: [Principal −r, Withholding +r]).</summary>
    public RetencionDeCdat? Retencion { get; init; }
}

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen del vencimiento de un contractual (la bonificación al cumplirse).</summary>
    public const string ClaseVencimientoDeContractual = "ContractualMaturity";

    /// <summary>La clase del origen del incumplimiento de un contractual (la penalidad y la reversión de la provisión).</summary>
    public const string ClaseIncumplimientoDeContractual = "ContractualBreach";

    /// <summary>La clase del origen de la provisión de la bonificación de un contractual (P26, <c>Provisioned</c>).</summary>
    public const string ClaseBonificacionDeContractual = "ContractualBonus";

    /// <summary>La clase de <c>PenalidadAplicada</c> del incumplimiento del contractual (mensajes.md §4.1).</summary>
    public const string PenalidadPorIncumplimiento = "ContractualBreach";

    /// <summary>
    /// US7 (T183): la unidad del contractual —la bonificación (principal si la hay) y la penalidad del incumplimiento, con la retención de la bonificación
    /// abonada como acompañante (mensajes.md §2.1)—. Admite proceso; origen la operación con la clase dada. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> HechosDeContractualAsync(HechoDeContractualAEmitir e, string clase, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        var h = e.Hecho;
        var op = e.Operacion;
        var contenidos = new List<object>();
        string? tipo = null;
        if (e.Bonificacion is { } b && b.Valores.Count > 0)
        {
            contenidos.Add(new BonificacionReconocidaV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = op.ValueDate, RegisteredAt = Instante(op.RegisteredAt),
                Dimensions = h.Dimensiones(), Amounts = b.Valores, OperationNumber = op.Number, RulesVersion = op.RulesVersion, Contractual = b.Contrato,
            });
            tipo = BonificacionReconocidaV1.Type;
        }
        if (e.Penalidad is { } p && p.Valores.Count > 0)
        {
            contenidos.Add(new PenalidadAplicadaV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, ValueDate = op.ValueDate, RegisteredAt = Instante(op.RegisteredAt),
                Dimensions = h.Dimensiones(), Amounts = p.Valores, OperationNumber = op.Number, RulesVersion = op.RulesVersion, Kind = PenalidadPorIncumplimiento,
                Basis = p.Base,
            });
            tipo ??= PenalidadAplicadaV1.Type;
        }
        if (e.Retencion is { } r && r.Retencion.SeRetiene && tipo == BonificacionReconocidaV1.Type)
        {
            var linea = new DepositFactLineV1
            {
                Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, Dimensions = h.Dimensiones(), ValueDate = op.ValueDate,
                Amounts =
                [
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -r.Retencion.Valor },
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Withholding), Amount = r.Retencion.Valor },
                ],
            };
            contenidos.Add(new RetencionPracticadaV1
            {
                RunKind = e.PorElProceso ? nameof(LendingProcessKind.DailyCycle) : null, FactDate = op.ValueDate, BatchNumber = 1, PartNumber = 1,
                Totals = [new DepositFactTotalV1 { Dimensions = h.Dimensiones(), Amounts = linea.Amounts, AccountCount = 1 }], Lines = [linea], LineCount = 1,
                ControlTotal = 0m, Withholdings = [DetalleDeRetencion(r.Retencion, r.Politica, r.Perfil, r.Anio, DepositClass.Contractual)],
            });
        }
        if (tipo is null) return Result.Success(new UnidadEmitida([]));

        var modo = await modoDePaso.ResolverAsync(tipo, h.FechaContable, e.PorElProceso, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, clase, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value, ValidacionPrevia: e.ValidacionPrevia), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }
}
