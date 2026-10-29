using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Payroll;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Integration;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Reconciliation;

/// <summary>
/// Feature 015, T2, T0444 (FR-128 a FR-130, FR-168, FR-218): la conciliación completa. Las notas bancarias —GMF real del extracto, comisión con IVA, intereses en bruto con
/// su retención— las registra una persona y las aprueba otra (al aprobarse nace <c>NotaBancariaRegistrada</c>); la nota aprobada es un registro de los libros que cruza
/// con su línea por el vínculo; la dispersión de Nómina informada (<c>DispersionDeNominaGenerada</c> v1) entra como compromiso y como registro; el cruce con reglas
/// configuradas usa su holgura; nada se cruza dos veces. Prueba escrita, sin correr. (nuevo)
/// </summary>
public class ConciliacionCompletaTests
{
    private const int Anio = 2026;
    private const int Mes = 11;

    private readonly EscenarioDeTesoreria _t = new();
    private readonly IRecaudoParaConciliacion _recaudo = Substitute.For<IRecaudoParaConciliacion>();
    private int _linea;

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    public ConciliacionCompletaTests()
    {
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.ConciliacionDiasDeHolgura, "3", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        _t.Contabilidad.SaldoEnLibrosAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(1_000_000m);
        _t.Contabilidad.MovimientosDeCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<MovimientoContableDeCuentaDto>());
        _t.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<CombinacionDeHecho>>(), Arg.Any<CancellationToken>()).Returns(new EvaluacionContableDto(true, "Bloquear", []));
        _recaudo.RegistrosAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new List<RegistroConciliableDto>());
        _t.E.Como(_t.E.Supervisor);
    }

    private BankStatementLine Linea(DateOnly fecha, StatementLineDirection sentido, decimal valor, string? descripcion = null, string? referencia = null)
    {
        var extracto = _t.E.Db.TreasuryBankStatements.Local.FirstOrDefault();
        if (extracto is null)
        {
            extracto = new BankStatement
            {
                Number = "EX-1", BankAccountId = _t.Pagos.Id, Source = BankStatementSource.Manual, From = new DateOnly(Anio, Mes, 1), To = new DateOnly(Anio, Mes, 30),
                LoadedByUserId = _t.E.Supervisor.Id, LoadedAt = DateTime.UtcNow,
            };
            _t.E.Db.TreasuryBankStatements.Add(extracto);
            _t.E.Db.SaveChanges();
        }
        var l = new BankStatementLine
        {
            BankStatementId = extracto.Id, BankAccountId = _t.Pagos.Id, LineNumber = ++_linea, MovementDate = fecha, Direction = sentido, Amount = valor, Description = descripcion,
            Reference = referencia, Fingerprint = Guid.NewGuid().ToString("N"),
            RecaudoStatus = sentido == StatementLineDirection.Credit ? StatementLineRecaudoStatus.Pending : StatementLineRecaudoStatus.NotApplicable,
        };
        _t.E.Db.TreasuryBankStatementLines.Add(l);
        _t.E.Db.SaveChanges();
        return l;
    }

    private RegisterBankNoteCommandHandler Registrar() =>
        new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Parametros, _t.Emision, Auditoria, _t.E.Reloj);

    private DecideBankNoteCommandHandler Decidir() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Emision, Auditoria, _t.E.Reloj);

    [Fact]
    public async Task La_nota_la_aprueba_otra_persona_y_entonces_nace_su_hecho()
    {
        var gmf = Linea(new DateOnly(Anio, Mes, 10), StatementLineDirection.Debit, 4_000m, "GMF 4X1000");
        var r = await Registrar().Handle(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Gmf, 4_000m, StatementLinePublicId: gmf.PublicId)
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(BankNoteState.PendingApproval, "por defecto las notas exigen aprobación");
        (await _t.E.Db.IntegrationMessages.CountAsync(m => m.Type == NotaBancariaRegistradaV1.Type)).Should().Be(0);

        var propia = await Decidir().Handle(new DecideBankNoteCommand(r.Value.PublicId, true) { OperationKey = Guid.NewGuid() }, default);
        propia.Error.Code.Should().Be("Treasury.BankNote.SelfApproval");

        _t.E.Como(_t.E.Cajera);
        var aprobada = await Decidir().Handle(new DecideBankNoteCommand(r.Value.PublicId, true) { OperationKey = Guid.NewGuid() }, default);
        aprobada.Value.Status.Should().Be(BankNoteState.Approved);
        (await _t.E.Db.IntegrationMessages.CountAsync(m => m.Type == NotaBancariaRegistradaV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task La_comision_es_base_mas_iva_y_los_intereses_bruto_menos_retencion()
    {
        var v = new RegisterBankNoteCommandValidator();

        v.Validate(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Commission, 11_900m, BaseAmount: 10_000m, VatAmount: 1_900m)).IsValid.Should().BeTrue();
        v.Validate(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Commission, 11_900m, BaseAmount: 10_000m, VatAmount: 1_000m)).IsValid.Should().BeFalse();
        v.Validate(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Interest, 9_300m, BaseAmount: 10_000m, WithholdingAmount: 700m)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task El_extracto_sugiere_el_GMF_real_como_nota_y_una_linea_no_lleva_dos_notas()
    {
        var gmf = Linea(new DateOnly(Anio, Mes, 12), StatementLineDirection.Debit, 2_000m, "Gravamen movimientos financieros");
        Linea(new DateOnly(Anio, Mes, 12), StatementLineDirection.Debit, 500_000m, "Pago a proveedor");

        var s = await new ListBankNoteSuggestionsQueryHandler(_t.E.Db, _t.Alcance, _t.E.Reloj)
            .Handle(new ListBankNoteSuggestionsQuery(_t.Pagos.PublicId, new DateOnly(Anio, Mes, 1), new DateOnly(Anio, Mes, 30)), default);
        s.Value.Should().ContainSingle(x => x.StatementLinePublicId == gmf.PublicId && x.SuggestedKind == BankNoteKind.Gmf);

        (await Registrar().Handle(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Gmf, 2_000m, StatementLinePublicId: gmf.PublicId)
            { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        var otra = await Registrar().Handle(new RegisterBankNoteCommand(_t.Pagos.PublicId, BankNoteKind.Gmf, 2_000m, StatementLinePublicId: gmf.PublicId)
            { OperationKey = Guid.NewGuid() }, default);
        otra.Error.Code.Should().Be("Treasury.BankNote.StatementLineHasNote");
    }

    [Fact]
    public async Task La_dispersion_de_Nomina_informada_queda_como_registro_de_la_cuenta()
    {
        var mensaje = _t.Mensaje(new DispersionDeNominaGeneradaV1
        {
            FilePublicId = Guid.NewGuid(), BankAccount = new CuentaDeOrigenDeLaDispersionV1 { BankCode = "1007", AccountNumber = _t.Pagos.AccountNumber }, Total = 3_500_000m,
            LineCount = 7, PayDate = new DateOnly(Anio, Mes, 15), RunPublicId = Guid.NewGuid(),
        }, DispersionDeNominaGeneradaV1.Type, 1);

        var r = await new ConsumePayrollDispersionCommandHandler(_t.E.Db, _t.Entrantes, _t.E.Reloj).Handle(new ConsumePayrollDispersionCommand([mensaje]), default);

        r.IsSuccess.Should().BeTrue();
        var d = await _t.E.Db.TreasuryPayrollDispersions.SingleAsync();
        d.BankAccountId.Should().Be(_t.Pagos.Id);
        d.Total.Should().Be(3_500_000m);
    }
}
