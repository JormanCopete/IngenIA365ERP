using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.BulkCharges;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.BulkCharges;

/// <summary>Lo que necesita nacer una obligación de cargo masivo, resuelto una vez por corrida.</summary>
public sealed record ContextoDeNacimiento(
    BulkChargeRun Corrida,
    CreditLine Linea,
    Guid? ContrapartePublicId,
    ModoDeEntrega Modo,
    PrevalidationOutcome ValidacionPrevia,
    int? UsuarioId,
    Guid? UsuarioCentral);

/// <summary>
/// El nacimiento de una obligación de cargo masivo (2026-11-25, pedido del dueño; feature 026; R1 de la investigación), con la secuencia del de una venta a
/// crédito sin lo de la venta: el crédito con <c>Origin = BulkCharge</c> y la línea de la corrida; la operación <c>Disbursement</c> de forma
/// <c>Charge</c> —sin orden de giro— con su fila <c>+Principal</c> por <see cref="RegistroDeCartera"/>, la persona que confirmó la corrida como actor;
/// las condiciones <c>BulkCharge</c> con el plan del simulador de la línea; la posición del día; el vínculo de forma de pago (caja, nómina propia o
/// pagaduría con su convenio); y el hecho <c>CargoMasivoRegistrado</c> sellado al lote de la corrida. Guarda dentro de la transacción de quien llama, bajo
/// el cerrojo del deudor que ya tomó; no confirma. (nuevo)
/// </summary>
public sealed class NacimientoDeCargoMasivo(
    IApplicationDbContext db,
    CorridasDeCargoMasivo corridas,
    TramoDeDesembolso tramos,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<Loan>> NacerAsync(ContextoDeNacimiento c, BulkChargeLine linea, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(linea);
        var run = c.Corrida;
        var fecha = run.ChargeDate;
        var ahora = reloj.UtcNow;
        var persona = await db.People.AsNoTracking().Where(p => p.Id == linea.PersonId).Select(p => new { p.PublicId, p.IsEmployee }).FirstAsync(ct);

        var sim = await corridas.SimularAsync(c.Linea, run, linea.Amount, linea.Installments, linea.FirstDueDate, persona.PublicId, ct);
        if (sim.IsFailure) return Result.Failure<Loan>(sim.Error);
        var sucursal = await emision.SucursalContableAsync(linea.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<Loan>(sucursal.Error);
        var contable = await fechaContable.CalcularAsync(fecha, ct);
        if (contable.IsFailure) return Result.Failure<Loan>(contable.Error);
        var monto = sim.Value.Plan.TotalCapital;

        var credito = new Loan
        {
            DebtorPersonId = linea.PersonId,
            DebtorKind = DebtorKind.Associate,
            BranchId = linea.BranchId,
            CreditLineId = c.Linea.Id,
            Origin = LoanOrigin.BulkCharge,
            Status = LoanStatus.Active,
            ApprovedAmount = monto,
            OpenedOn = fecha,
            BulkChargeLineId = linea.Id,
        };
        await numerador.NumerarCreditoAsync(credito, TramoDeDesembolso.PrefijoDeCredito, ct);
        db.Loans.Add(credito);
        var control = registro.IniciarControl(credito, fecha, ahora);

        var entradas = JsonSerializer.Serialize(new
        {
            bulkChargeRun = run.PublicId, number = run.Number, line = c.Linea.Code, amount = monto, installments = linea.Installments,
            periodicity = run.Periodicity.ToString(), firstDueDate = linea.FirstDueDate, channel = linea.RepaymentChannel.ToString(),
            counterpart = c.ContrapartePublicId, simulation = sim.Value.Huella,
        }, Json);
        var numeroDeOperacion = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Disbursement, TramoDeDesembolso.PrefijoDeDesembolso, ct);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
        {
            Loan = credito,
            Kind = LoanOperationKind.Disbursement,
            Number = numeroDeOperacion,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = monto,
            ActorKind = ActorKind.Person,
            ActorUserId = c.UsuarioId,
            OriginUserCentralId = c.UsuarioCentral,
            DisbursementForm = DisbursementForm.Charge,
            TrancheNumber = 1,
            Payload = entradas,
            InputsSha256 = TramoDeDesembolso.Sha256(entradas),
            RulesVersion = sim.Value.Plan.RulesVersion,
            PrevalidationOutcome = c.ValidacionPrevia.ToString(),
        });
        registro.RegistrarTransacciones(
        [
            new LoanTransaction
            {
                Loan = credito,
                Operation = operacion,
                Kind = LoanTransactionKind.Disbursement,
                Concept = LoanConcept.Principal,
                Amount = monto,
                ValueDate = fecha,
                AccountingDate = contable.Value,
                RegisteredAt = operacion.RegisteredAt,
            },
        ]);
        var moraSobreIntereses = (await parametros.LeerAsync(ParametrosDeCartera.MoraSobreIntereses, fecha, ct: ct)) is { IsSuccess: true } m
                                 && m.Value.Valor is true
                                 && !string.Equals(sim.Value.Version.Version.ModalityCode, "VIVIENDA", StringComparison.OrdinalIgnoreCase);
        var condiciones = tramos.Condiciones(credito, operacion, sim.Value, fecha, moraSobreIntereses, TermsVersionCause.BulkCharge);
        await tramos.PlanAsync(credito, condiciones, operacion, fecha, monto, sim.Value.Huella, 1, sim.Value.Plan, ct);
        registro.RegistrarPosicion(control, Posicion(credito, fecha, monto, operacion, condiciones));
        await db.SaveChangesAsync(ct);

        // El vínculo de forma de pago con que nace (C44): caja sin pagaduría; nómina propia con el deudor como pagador (G7) y el convenio propio; la
        // pagaduría con su convenio.
        int? pagador = linea.RepaymentChannel switch
        {
            RepaymentChannel.OwnPayroll => linea.PersonId,
            RepaymentChannel.PaymasterDeduction when linea.PayrollAgreementId is { } convenioId =>
                await db.PayrollAgreements.AsNoTracking().Where(a => a.Id == convenioId).Select(a => a.PaymasterPersonId).FirstAsync(ct),
            _ => null,
        };
        var vinculo = LoanPayrollLink.AlOriginar(credito.Id, linea.RepaymentChannel, pagador, operacion.Id, fecha);
        if (linea.RepaymentChannel is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction)
        {
            vinculo.PayrollAgreementId = linea.PayrollAgreementId;
            vinculo.LibranzaDate = fecha;
        }
        db.LoanPayrollLinks.Add(vinculo);

        var hecho = new HechoDeCredito(credito, condiciones, c.Linea.Code, persona.PublicId, persona.IsEmployee, sucursal.Value, contable.Value);
        var plan = TramoDeDesembolso.Resumen(sim.Value, 1);
        var emitido = await emision.CargoMasivoRegistradoAsync(hecho, operacion, monto, plan, run, c.ContrapartePublicId ?? persona.PublicId, c.Modo,
            c.ValidacionPrevia, ct);
        if (emitido.IsFailure) return Result.Failure<Loan>(emitido.Error);
        linea.Crear(credito.Id, ahora);
        await db.SaveChangesAsync(ct);
        return Result.Success(credito);
    }

    /// <summary>La posición del día del cargo: el capital, sin mora (data-model §6.1).</summary>
    private static LoanDailyPosition Posicion(Loan credito, DateOnly fecha, decimal monto, LoanOperation operacion, LoanTermsVersion condiciones) => new()
    {
        Loan = credito,
        PositionDate = fecha,
        Version = 1,
        RegisteredAt = operacion.RegisteredAt,
        Status = LoanStatus.Active,
        BranchId = credito.BranchId,
        CreditLineId = credito.CreditLineId,
        ModalityCode = condiciones.ModalityCode,
        SubsegmentCode = condiciones.SubsegmentCode,
        Principal = monto,
        PayoffAmount = monto,
        AppliedAnnualRate = condiciones.AnnualRate,
        InputsSha256 = operacion.InputsSha256,
    };
}
